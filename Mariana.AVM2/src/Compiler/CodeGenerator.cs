using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using Mariana.AVM2.ABC;
using Mariana.AVM2.Core;
using Mariana.CodeGen;
using Mariana.CodeGen.IL;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    using static DataNodeConstHelper;
    using static DataNodeTypeHelper;

    internal sealed class CodeGenerator {

        private readonly struct LocalVarWithClass {
            public readonly Class? type;
            public readonly ILBuilder.Local local;

            public LocalVarWithClass(Class? type, ILBuilder.Local local) =>
                (this.type, this.local) = (type, local);
        }

        private enum RuntimeBindingKind {
            QNAME,
            MULTINAME,
            KEY,
            KEY_MULTINAME,
        }

        private struct BlockEmitInfo {
            public ILBuilder.Label backwardLabel;
            public ILBuilder.Label forwardLabel;
            public bool needsStackStashAndRestore;
            public StaticArrayPoolToken<ILBuilder.Local> stackStashVars;
        }

        private struct TwoWayBranchEmitInfo {
            public int trueBlockId;
            public int falseBlockId;
            public ILBuilder.Label trueLabel;
            public ILBuilder.Label falseLabel;
            public bool trueBlockNeedsTransition;
            public bool falseBlockNeedsTransition;
            public bool trueIsFallThrough;
        }

        private static readonly Class s_objectClass = Class.fromType(typeof(ASObject))!;
        private static readonly Class s_numberClass = Class.fromType(typeof(double))!;
        private static readonly Class s_arrayClass = Class.fromType(typeof(ASArray))!;

        private static readonly MethodTraitParameter[] s_dateCtorComponentsParams = {
            new("y", s_numberClass, false, false, default),
            new("m", s_numberClass, false, false, default),
            new("d", s_numberClass, true, true, 1d),
            new("hr", s_numberClass, true, true, 0d),
            new("min", s_numberClass, true, true, 0d),
            new("sec", s_numberClass, true, true, 0d),
            new("ms", s_numberClass, true, true, 0d),
        };

        private static readonly ReferenceDictionary<MethodTrait, MethodInfo> s_primitiveTypeMethodMap = _initPrimitiveTypeMethodMap();

        private MethodCompilation m_compilation;

        private ILBuilder m_ilBuilder;

        private StaticArrayPool<ILBuilder.Local> m_localVarArrayPool = new(32);

        private DynamicArrayPool<LocalVarWithClass> m_localVarWithClassArrayPool = new();

        private DynamicArray<DynamicArrayPoolToken<LocalVarWithClass>> m_scopeVars;

        private DynamicArray<DynamicArrayPoolToken<LocalVarWithClass>> m_localVars;

        private DynamicArray<BlockEmitInfo> m_blockEmitInfo;

        private DynamicArray<ILBuilder.Local> m_catchLocalVarSyncTable;

        private ReferenceDictionary<FieldTrait, int> m_fieldInitInstructionIds = new();

        private bool m_hasExceptionHandling;

        private bool m_needsFinallyBlock;

        private ILBuilder.Local m_restOrArgumentsArrayLocal;
        private ILBuilder.Local m_scopedFuncThisLocal;
        private ILBuilder.Local m_scopedFuncScopeLocal;
        private ILBuilder.Local m_oldDxnsLocal;
        private ILBuilder.Local m_excReturnValueLocal;
        private ILBuilder.Local m_rtScopeStackLocal;
        private ILBuilder.Local m_curExcHandlerIdLocal;
        private ILBuilder.Local m_excThrownValueLocal;

        private ILBuilder.Label m_excReturnLabel;

        private ILBuilder.Local m_globalMemReadSpanLocal;
        private ILBuilder.Local m_globalMemWriteSpanLocal;
        private ILBuilder.Label m_globalMemOutOfBoundsErrLabel;

        private DynamicArray<int> m_tempIntArray;
        private DynamicArray<ILBuilder.Label> m_tempLabelArray;
        private DynamicArray<ILBuilder.Local> m_tempLocalArray;

        public CodeGenerator(MethodCompilation compilation) {
            m_compilation = compilation;
            m_ilBuilder = null!;
        }

        private static ReferenceDictionary<MethodTrait, MethodInfo> _initPrimitiveTypeMethodMap() {
            var map = new ReferenceDictionary<MethodTrait, MethodInfo>();

            Class intClass = Class.fromType(typeof(int))!,
                  uintClass = Class.fromType(typeof(uint))!,
                  numberClass = Class.fromType(typeof(double))!,
                  boolClass = Class.fromType(typeof(bool))!,
                  stringClass = Class.fromType(typeof(string))!;

            // toString, valueOf are special cases.

            QName toStringName = new QName(Namespace.AS3, "toString"),
                  valueOfName = new QName(Namespace.AS3, "valueOf");

            map[intClass.getMethod(toStringName)!] = KnownMembers.intToStringWithRadix;
            map[intClass.getMethod(valueOfName)!] = KnownMembers.intValueOf;
            map[uintClass.getMethod(toStringName)!] = KnownMembers.uintToStringWithRadix;
            map[uintClass.getMethod(valueOfName)!] = KnownMembers.uintValueOf;
            map[numberClass.getMethod(toStringName)!] = KnownMembers.numberToStringWithRadix;
            map[numberClass.getMethod(valueOfName)!] = KnownMembers.numberValueOf;
            map[boolClass.getMethod(toStringName)!] = KnownMembers.boolToString;
            map[boolClass.getMethod(valueOfName)!] = KnownMembers.boolValueOf;
            map[stringClass.getMethod(toStringName)!] = KnownMembers.strToString;
            map[stringClass.getMethod(valueOfName)!] = KnownMembers.strValueOf;

            map[stringClass.getProperty("length")!.getter!] = KnownMembers.strGetLength;

            load(intClass);
            load(uintClass);
            load(numberClass);
            load(boolClass);
            load(stringClass);

            return map;

            void load(Class klass) {
                var traits = klass.getTraits(TraitType.ALL, TraitScope.INSTANCE_DECLARED);

                for (int i = 0; i < traits.length; i++) {
                    if (traits[i] is not MethodTrait method)
                        continue;

                    if (method.name.localName == toStringName.localName
                        || method.name.localName == valueOfName.localName)
                    {
                        // These were special-cased above.
                        continue;
                    }

                    ParameterInfo[] paramInfos = method.underlyingMethodInfo.GetParameters();
                    Type[] paramTypes = new Type[paramInfos.Length + 1];
                    paramTypes[0] = Class.getUnderlyingOrPrimitiveType(klass);

                    for (int j = 0; j < paramInfos.Length; j++)
                        paramTypes[j + 1] = paramInfos[j].ParameterType;

                    MethodInfo primitiveMethod = klass.underlyingType.GetMethod(
                        name: method.underlyingMethodInfo.Name,
                        bindingAttr: BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
                        binder: null,
                        types: paramTypes,
                        modifiers: null
                    );

                    Debug.Assert(primitiveMethod != null);
                    map[method] = primitiveMethod;
                }
            }
        }

        public void run() {
            try {
                m_ilBuilder = m_compilation.ilBuilder;

                m_scopeVars.clearAndAddDefault(m_compilation.computedMaxScopeSize);
                m_localVars.clearAndAddDefault(m_compilation.localCount);

                _createBlockEmitInfo();
                _createCatchLocalVarSyncTable();

                _emitHeader();

                var rpo = m_compilation.getBasicBlockReversePostorder();
                for (int i = 0; i < rpo.Length; i++)
                    _visitBasicBlock(m_compilation.getBasicBlock(rpo[i]));

                _emitTrailer();
            }
            finally {
                m_localVarArrayPool.clear();
                m_localVarWithClassArrayPool.clear();
                m_fieldInitInstructionIds.clear();
            }
        }

        private void _createBlockEmitInfo() {
            Span<BasicBlock> blocks = m_compilation.getBasicBlocks();
            m_blockEmitInfo.clearAndAddUninitialized(blocks.Length);

            for (int i = 0; i < blocks.Length; i++) {
                ref BasicBlock block = ref blocks[i];
                ref BlockEmitInfo emitInfo = ref m_blockEmitInfo[i];

                emitInfo.forwardLabel = m_ilBuilder.createLabel();

                // If there are backward jumps into this basic block or if it is the catch
                // target of an exception handler, and it is entered with a non-empty stack,
                // we may need to reserve local variables into which values on the stack will
                // be stashed in before executing a backward jump or an IL "leave" instruction
                // that transfers control to a catch handler when an exception is caught.
                // This is needed because the CLR requires the stack to be empty in both these
                // situations.

                bool mustStashAndRestoreStack =
                    m_compilation.staticIntArrayPool.getLength(block.stackAtEntry) > 0
                    && hasBackwardEntryOrCatch(block);

                if (!mustStashAndRestoreStack) {
                    emitInfo.backwardLabel = emitInfo.forwardLabel;
                    emitInfo.needsStackStashAndRestore = false;
                }
                else {
                    emitInfo.backwardLabel = m_ilBuilder.createLabel();
                    emitInfo.needsStackStashAndRestore = true;
                    emitInfo.stackStashVars = _createStackStashForBlockEntry(block);
                }
            }

            bool hasBackwardEntryOrCatch(in BasicBlock bb) {
                var entryPoints = m_compilation.cfgNodeRefArrayPool.getSpan(bb.entryPoints);
                for (int i = 0; i < entryPoints.Length; i++) {
                    CFGNodeRef ep = entryPoints[i];
                    if (ep.isCatch || (ep.isBlock && m_compilation.getBasicBlock(ep.id).postorderIndex <= bb.postorderIndex))
                        return true;
                }
                return false;
            }
        }

        private StaticArrayPoolToken<ILBuilder.Local> _createStackStashForBlockEntry(in BasicBlock block) {
            var stackAtEntry = m_compilation.staticIntArrayPool.getSpan(block.stackAtEntry);
            var token = m_localVarArrayPool.allocate(stackAtEntry.Length, out Span<ILBuilder.Local> vars);

            using (var lockedContext = m_compilation.getContext()) {
                for (int i = stackAtEntry.Length - 1; i >= 0; i--) {
                    ref DataNode node = ref m_compilation.getDataNode(stackAtEntry[i]);
                    if (node.isNotPushed || node.dataType == DataNodeType.THIS || node.dataType == DataNodeType.REST)
                        continue;

                    Class? nodeClass = _getPushedClassOfNode(node);
                    vars[i] = m_ilBuilder.acquireTempLocal(lockedContext.value.getTypeSignature(nodeClass));
                }
            }

            // We release the acquired temporary variables immediately. This means that the
            // transition code emitted before a backward jump to this block must NOT acquire
            // any temporary variables while stashing the stack, as this may accidentally
            // overwrite the stash slots!

            for (int i = 0; i < vars.Length; i++) {
                if (!vars[i].isDefault)
                    m_ilBuilder.releaseTempLocal(vars[i]);
            }

            return token;
        }

        private void _createCatchLocalVarSyncTable() {
            var excHandlers = m_compilation.getExceptionHandlers();
            if (excHandlers.Length == 0)
                return;

            m_catchLocalVarSyncTable.clearAndAddDefault(excHandlers.Length * m_compilation.localCount);

            // The local synchronization table is used to ensure that any local variable definitions
            // in try blocks will be available in the associated catch blocks when an exception is
            // caught, if the definition in the try block and the catch block have different types
            // (and so do not share the same IL local slot).
            //
            // This table is a 2D row-major matrix, where the element whose row number is an exception
            // handler id and column number is a local slot index contains the IL local variable associated
            // with the definition in the catch clause. If the local definition at that slot index for
            // that catch clause does not have an associated IL local variable (it is a constant, for
            // example), the default value of ILBuilder.Local (which never represents a valid IL local
            // variable) is used.
            //
            // Synchronization is best explained with the following code example (assume B extends A,
            // and C and D extend B):
            //
            //  var x = null;           // No sync needed, not visible in any try block
            //  x = new A();            // No sync needed, same type and IL local slot as catch block
            //  try {                   // EH id 0
            //      x = new C();        // Sync needed with catch of EH#0 and EH#1
            //      try {               // EH id 1
            //          x = new B();    // Sync needed with catch of EH#0 but not EH#1
            //          x = new D();    // Sync needed with catch of EH#0 and EH#1
            //      } catch (e) {
            //          trace(x);       // x has static type B
            //      }
            //  } catch (f) {
            //      trace(x);           // x has static type A
            //  }

            for (int i = 0; i < excHandlers.Length; i++) {
                ref BasicBlock catchBlock = ref m_compilation.getBasicBlockOfInstruction(excHandlers[i].catchTargetInstrId);
                var entryLocalNodeIds = m_compilation.staticIntArrayPool.getSpan(catchBlock.localsAtEntry);

                var catchLocalVars = m_catchLocalVarSyncTable.asSpan(i * m_compilation.localCount, m_compilation.localCount);

                for (int j = 0; j < entryLocalNodeIds.Length; j++) {
                    ref DataNode node = ref m_compilation.getDataNode(entryLocalNodeIds[j]);

                    // We don't add a sync variable to the table if
                    // - The local node defined in the target basic block for the catch clause has
                    //   no uses. Syncing in this case would be useless.
                    // - The local node does not have an associated IL local variable (e.g. it is
                    //   a constant). No syncing would be needed in this case.
                    // - The IL local variable associated with the node is also a sync variable for
                    //   an ancestor of the exception handler for the local slot at the same index.
                    //   This prevents sync writes that are redundant (when transferring from a parent
                    //   to a child try region) or duplicate.

                    if (m_compilation.getDataNodeUseCount(node) > 0
                        && _tryGetLocalVarForNode(node, out var localVar)
                        && !hasExistingLocalVarInTable(i, j, localVar))
                    {
                        catchLocalVars[j] = localVar;
                    }
                }
            }

            bool hasExistingLocalVarInTable(int handlerId, int localSlotId, ILBuilder.Local localVar) {
                while (handlerId != -1) {
                    if (localVar == m_catchLocalVarSyncTable[handlerId * m_compilation.localCount + localSlotId])
                        return true;
                    handlerId = m_compilation.getExceptionHandler(handlerId).parentId;
                }
                return false;
            }
        }

        private void _emitHeader() {
            bool hasExceptionHandlers = m_compilation.getExceptionHandlers().Length > 0;

            bool methodSetsDxns = m_compilation.isAnyFlagSet(MethodCompilationFlags.SETS_DXNS);
            bool methodUsesDxns = m_compilation.isAnyFlagSet(MethodCompilationFlags.MAY_USE_DXNS);

            m_needsFinallyBlock = methodSetsDxns || methodUsesDxns;
            m_hasExceptionHandling = m_needsFinallyBlock || hasExceptionHandlers;

            if (m_hasExceptionHandling) {
                m_excReturnLabel = m_ilBuilder.createLabel();
                m_ilBuilder.beginExceptionHandler();

                if (m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_RETURN_VALUE)) {
                    using var lockedContext = m_compilation.getContext();
                    var retTypeSig = lockedContext.value.getTypeSignature(m_compilation.getCurrentMethod()!.returnType);
                    m_excReturnValueLocal = m_ilBuilder.declareLocal(retTypeSig);
                }
            }

            if (hasExceptionHandlers) {
                m_curExcHandlerIdLocal = m_ilBuilder.declareLocal(typeof(int));
                m_excThrownValueLocal = m_ilBuilder.declareLocal(typeof(ASAny));
            }

            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.IS_SCOPED_FUNCTION)) {
                var containerTypeHandle = m_compilation.capturedScope!.container.typeHandle;

                m_scopedFuncThisLocal = m_ilBuilder.declareLocal(typeof(ASObject));
                m_scopedFuncScopeLocal = m_ilBuilder.declareLocal(TypeSignature.forClassType(containerTypeHandle));

                m_ilBuilder.emit(ILOp.ldarg_0);
                m_ilBuilder.emit(ILOp.ldfld, KnownMembers.scopedClosureReceiverObj);
                m_ilBuilder.emit(ILOp.stloc, m_scopedFuncThisLocal);
                m_ilBuilder.emit(ILOp.ldarg_0);
                m_ilBuilder.emit(ILOp.ldfld, KnownMembers.scopedClosureReceiverScope);
                m_ilBuilder.emit(ILOp.castclass, containerTypeHandle);
                m_ilBuilder.emit(ILOp.stloc, m_scopedFuncScopeLocal);
            }

            if (methodSetsDxns || methodUsesDxns) {
                // Save the old DXNS so that it can be restored at the end.
                m_oldDxnsLocal = m_ilBuilder.declareLocal(typeof(ASNamespace));

                if (methodSetsDxns || m_compilation.capturedScope == null || !m_compilation.capturedScope.capturesDxns) {
                    m_ilBuilder.emit(ILOp.ldnull);
                }
                else {
                    _emitPushCapturedScope();
                    m_ilBuilder.emit(ILOp.ldfld, m_compilation.capturedScope.container.dxnsFieldHandle);
                }

                m_ilBuilder.emit(ILOp.ldloca, m_oldDxnsLocal);
                m_ilBuilder.emit(ILOp.call, KnownMembers.setDxnsGetOld, -2);
            }

            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_RUNTIME_SCOPE_STACK))
                _emitInitRuntimeScopeStack();

            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_REST_ARRAY)) {
                m_restOrArgumentsArrayLocal = m_ilBuilder.declareLocal(typeof(ASArray));
                m_ilBuilder.emit(ILOp.ldarga, _getIndexOfRestArg());
                m_ilBuilder.emit(ILOp.call, KnownMembers.restParamGetSpan, 0);
                m_ilBuilder.emit(ILOp.newobj, KnownMembers.arrayCtorWithSpan, 0);
                m_ilBuilder.emit(ILOp.stloc, m_restOrArgumentsArrayLocal);
            }
            else if ((m_compilation.methodInfo.flags & ABCMethodFlags.NEED_ARGUMENTS) != 0) {
                // Emit the code for initializing the arguments array only if it is is
                // actually used.
                var initialLocalNodeIds = m_compilation.getInitialLocalNodeIds();
                int argsLocalSlotId = m_compilation.getCurrentMethodParams().length + 1;

                if (argsLocalSlotId < initialLocalNodeIds.Length
                    && m_compilation.getDataNodeUseCount(initialLocalNodeIds[argsLocalSlotId]) > 0)
                {
                    _emitInitArgumentsArray();
                }
            }

            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.READ_GLOBAL_MEMORY | MethodCompilationFlags.WRITE_GLOBAL_MEMORY))
                _emitSetupGlobalMemory();

            _emitInitFieldsToDefaultValues();

            // In the rare case where the entry point of the method is not the first basic
            // block that will be emitted (based on reverse postorder index), emit a jump
            // to the entry point.
            ref BasicBlock firstBlock = ref m_compilation.getBasicBlockOfInstruction(0);
            if (firstBlock.postorderIndex != m_compilation.getBasicBlocks().Length - 1)
                m_ilBuilder.emit(ILOp.br, m_blockEmitInfo[firstBlock.id].forwardLabel);
        }

        /// <summary>
        /// Emits code to initialize the runtime scope stack for the method.
        /// </summary>
        private void _emitInitRuntimeScopeStack() {
            m_rtScopeStackLocal = m_ilBuilder.declareLocal(typeof(RuntimeScopeStack));

            m_ilBuilder.emit(ILOp.ldc_i4, m_compilation.computedMaxScopeSize);

            // Get the runtime scope stack for the captured scope.
            if (m_compilation.declaringClass != null) {
                using var lockedContext = m_compilation.getContext();

                CapturedScope? classScope = lockedContext.value.getClassCapturedScope(m_compilation.declaringClass);
                if (classScope != null) {
                    EntityHandle classScopeField =
                        lockedContext.value.getClassCapturedScopeFieldHandle(m_compilation.declaringClass);

                    m_ilBuilder.emit(ILOp.ldsfld, classScopeField);
                    m_ilBuilder.emit(ILOp.call, classScope.container.rtStackGetMethodHandle, 0);
                }
                else {
                    m_ilBuilder.emit(ILOp.ldnull);
                }
            }
            else if (m_compilation.isAnyFlagSet(MethodCompilationFlags.IS_SCOPED_FUNCTION)) {
                CapturedScope funcScope = m_compilation.capturedScope!;

                m_ilBuilder.emit(ILOp.ldloc, m_scopedFuncScopeLocal);
                m_ilBuilder.emit(ILOp.call, funcScope.container.rtStackGetMethodHandle, 0);
            }
            else {
                m_ilBuilder.emit(ILOp.ldnull);
            }

            m_ilBuilder.emit(ILOp.newobj, KnownMembers.rtScopeStackNew, -1);
            m_ilBuilder.emit(ILOp.stloc, m_rtScopeStackLocal);

            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.IS_INSTANCE_METHOD)) {
                // For instance methods (and constructors) we need to push the declaring class
                // at the bottom (although it is part of the captured scope, it is not pushed
                // onto the runtime scope stack stored in the container so that it can also
                // be used by static methods)

                m_ilBuilder.emit(ILOp.ldloc, m_rtScopeStackLocal);
                _emitPushTraitConstant(m_compilation.declaringClass!);
                m_ilBuilder.emit(ILOp.callvirt, KnownMembers.classGetClassObj, 0);
                m_ilBuilder.emit(ILOp.ldc_i4, (int)BindOptions.SEARCH_TRAITS);
                m_ilBuilder.emit(ILOp.call, KnownMembers.rtScopeStackPush, -3);
            }
        }

        /// <summary>
        /// Emits code for the initialization of the "arguments" array in methods that
        /// use it.
        /// </summary>
        private void _emitInitArgumentsArray() {
            const MethodCompilationFlags hasThisArgFlags =
                MethodCompilationFlags.IS_INSTANCE_METHOD | MethodCompilationFlags.IS_SCOPED_FUNCTION;

            int firstArgIndex = m_compilation.isAnyFlagSet(hasThisArgFlags) ? 1 : 0;
            var restArgIndex = m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_REST_PARAM) ? _getIndexOfRestArg() : -1;

            var parameters = m_compilation.getCurrentMethodParams().asSpan();

            var arrLocal = m_ilBuilder.declareLocal(typeof(ASArray));
            m_restOrArgumentsArrayLocal = arrLocal;

            var il = m_ilBuilder;

            il.emit(ILOp.ldc_i4, parameters.Length);
            if (restArgIndex != -1) {
                il.emit(ILOp.ldarga, restArgIndex);
                il.emit(ILOp.call, KnownMembers.restParamGetLength, 0);
                il.emit(ILOp.add);
            }
            il.emit(ILOp.newobj, KnownMembers.arrayCtorWithLength, 0);
            il.emit(ILOp.stloc, arrLocal);

            for (int i = 0; i < parameters.Length; i++) {
                // We don't emit any OptionalParam<T> parameters in compiled methods, so no
                // need to handle them here.
                il.emit(ILOp.ldloc, arrLocal);
                il.emit(ILOp.ldc_i4, i);
                il.emit(ILOp.ldarg, firstArgIndex + i);
                ILEmitHelper.emitTypeCoerceToAny(il, parameters[i].type);
                il.emit(ILOp.call, KnownMembers.arraySetUintIndex, -3);
            }

            if (restArgIndex != -1) {
                // Add the rest arguments to the array. We use an inline loop for this,
                // as there is (currently) no method for copying values from a span
                // into an existing ASArray.

                var curIndexLocal = il.acquireTempLocal(typeof(int));
                var label1 = il.createLabel();
                var label2 = il.createLabel();

                il.emit(ILOp.ldc_i4_0);
                il.emit(ILOp.stloc, curIndexLocal);
                il.emit(ILOp.br, label2);

                il.markLabel(label1);
                il.emit(ILOp.ldloc, arrLocal);
                il.emit(ILOp.ldc_i4, parameters.Length);
                il.emit(ILOp.ldloc, curIndexLocal);
                il.emit(ILOp.add);
                il.emit(ILOp.ldarga, restArgIndex);
                il.emit(ILOp.ldloc, curIndexLocal);
                il.emit(ILOp.call, KnownMembers.restParamGetElementI, -1);
                il.emit(ILOp.call, KnownMembers.arraySetUintIndex, -3);

                il.emit(ILOp.ldloc, curIndexLocal);
                il.emit(ILOp.ldc_i4_1);
                il.emit(ILOp.add);
                il.emit(ILOp.stloc, curIndexLocal);

                il.markLabel(label2);
                il.emit(ILOp.ldloc, curIndexLocal);
                il.emit(ILOp.ldarga, restArgIndex);
                il.emit(ILOp.call, KnownMembers.restParamGetLength, 0);
                il.emit(ILOp.blt_un, label1);

                il.releaseTempLocal(curIndexLocal);
            }

            // Initialize the arguments.callee property.
            // This is not supported for instance constructors as we cannot create Function objects for them.

            MethodTrait? currentMethod = m_compilation.getCurrentMethod();

            if (currentMethod != null) {
                m_ilBuilder.emit(ILOp.ldloc, arrLocal);
                m_ilBuilder.emit(ILOp.call, KnownMembers.getObjectDynamicPropCollection, 0);
                m_ilBuilder.emit(ILOp.ldstr, "callee");

                if (m_compilation.isAnyFlagSet(MethodCompilationFlags.IS_SCOPED_FUNCTION)) {
                    // For a scoped function, arguments.callee can be obtained from the ScopedClosureReceiver.
                    m_ilBuilder.emit(ILOp.ldarg_0);
                    m_ilBuilder.emit(ILOp.ldfld, KnownMembers.scopedClosureReceiverCallee);
                }
                else {
                    // For an instance or static method, arguments.callee is a method closure with
                    // the current receiver (null for static methods).
                    _emitPushTraitConstant(currentMethod);
                    m_ilBuilder.emit(ILOp.castclass, typeof(MethodTrait));

                    if (m_compilation.isAnyFlagSet(MethodCompilationFlags.IS_INSTANCE_METHOD))
                        m_ilBuilder.emit(ILOp.ldarg_0);
                    else
                        m_ilBuilder.emit(ILOp.ldnull);

                    m_ilBuilder.emit(ILOp.call, KnownMembers.methodTraitCreateMethodClosure, -1);
                }

                m_ilBuilder.emit(ILOp.call, KnownMembers.anyFromObject, 0);
                m_ilBuilder.emit(ILOp.ldc_i4_0);    // callee property must have isEnumerable = false

                m_ilBuilder.emit(ILOp.callvirt, KnownMembers.dynamicPropCollectionSet, -4);
            }
        }

        /// <summary>
        /// Emits code to initialize fields to their default values in a constructor,
        /// static constructor or script initializer.
        /// </summary>
        private void _emitInitFieldsToDefaultValues() {
            ReadOnlyArrayView<Trait> traits = default;

            ClassConstructor? ctor = m_compilation.getCurrentConstructor();
            if (ctor != null) {
                traits = m_compilation.declaringClass!.getTraits(TraitType.ALL, TraitScope.INSTANCE_DECLARED);
            }
            else if (m_compilation.isAnyFlagSet(MethodCompilationFlags.IS_STATIC_INIT)) {
                traits = m_compilation.declaringClass!.getTraits(TraitType.ALL, TraitScope.STATIC);
            }
            else if (m_compilation.isAnyFlagSet(MethodCompilationFlags.IS_SCRIPT_INIT)) {
                using var lockedContext = m_compilation.getContext();
                traits = lockedContext.value.getScriptTraits(m_compilation.currentScriptInfo!);
            }

            if (traits.length == 0)
                return;

            _checkForFieldsInitInMethodBody();

            using (var lockedContext = m_compilation.getContext()) {
                for (int i = 0; i < traits.length; i++) {
                    if (traits[i] is not ScriptField field)
                        continue;

                    if (m_fieldInitInstructionIds.tryGetValue(field, out int initId) && initId != -1)
                        continue;

                    if (lockedContext.value.tryGetDefaultValueOfField(field, out ASAny initVal)) {
                        if (ILEmitHelper.isImplicitDefault(initVal, field.fieldType))
                            continue;

                        if (!field.isStatic)
                            m_ilBuilder.emit(ILOp.ldarg_0);

                        ILEmitHelper.emitPushConstantAsType(m_ilBuilder, initVal, field.fieldType);
                        m_ilBuilder.emit(field.isStatic ? ILOp.stsfld : ILOp.stfld, lockedContext.value.getEntityHandle(field));
                    }
                    else if (field.fieldType != null && field.fieldType.tag == ClassTag.NUMBER) {
                        if (!field.isStatic)
                            m_ilBuilder.emit(ILOp.ldarg_0);

                        m_ilBuilder.emit(ILOp.ldc_r4, Single.NaN);
                        m_ilBuilder.emit(field.isStatic ? ILOp.stsfld : ILOp.stfld, lockedContext.value.getEntityHandle(field));
                    }
                }
            }
        }

        private void _checkForFieldsInitInMethodBody() {
            // Check for fields that are definitely initialized in the body of a constructor/
            // static initializer/script initializer so that we do not have to set them to
            // their declared default values. We don't do a full definite assignment analysis
            // (at least right now). We only consider field assignments in the first basic block
            // that is executed at method entry, before any other use of the "this" argument
            // (the constructed object for an instance ctor, the class object for a static
            // ctor or the global object for a script initializer) in the same basic block.

            var initLocalNodeIds = m_compilation.getInitialLocalNodeIds();
            if (initLocalNodeIds.Length == 0)
                return;

            ref DataNode thisArgNode = ref m_compilation.getDataNode(initLocalNodeIds[0]);

            ref BasicBlock firstBlock = ref m_compilation.getBasicBlockOfInstruction(0);
            var instrInFirstBlock = m_compilation.getInstructionsInBasicBlock(firstBlock);

            bool hasRtScopeStack = m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_RUNTIME_SCOPE_STACK);
            int firstUseOfThisArgId = Int32.MaxValue;

            for (int i = 0; i < instrInFirstBlock.Length; i++) {
                int pushedNodeId = instrInFirstBlock[i].stackPushedNodeId;
                if (pushedNodeId == -1)
                    continue;

                ref DataNode node = ref m_compilation.getDataNode(pushedNodeId);
                if (node.dataType != thisArgNode.dataType || node.constant != thisArgNode.constant)
                    continue;

                var nodeUses = m_compilation.getDataNodeUses(node);

                for (int j = 0; j < nodeUses.Length; j++) {
                    if (!nodeUses[j].isInstruction)
                        continue;

                    ref Instruction useInstr = ref m_compilation.getInstruction(nodeUses[j].instrOrNodeId);

                    if (useInstr.blockId != firstBlock.id)
                        // We don't care about uses in other basic blocks, as we are only
                        // considering initializations in the first block.
                        continue;

                    // In addition to setproperty/setslot/initproperty, we allow the following
                    // uses of the this argument which do not expose uninitialized fields:
                    // dup, pop, setlocal and pushscope (the last one only when there is no
                    // runtime scope stack in the method.)

                    if (useInstr.opcode == ABCOp.dup
                        || useInstr.opcode == ABCOp.pop
                        || useInstr.opcode == ABCOp.setlocal
                        || (useInstr.opcode == ABCOp.pushscope && !hasRtScopeStack))
                    {
                        continue;
                    }

                    // We also allow constructsuper if the base class is Object. (We can't
                    // allow arbitrary base classes because their constructors may read derived
                    // class fields through late binding.)
                    if (useInstr.opcode == ABCOp.constructsuper
                        && m_compilation.declaringClass!.parent!.isObjectClass
                        && useInstr.data.constructSuper.argCount == 0)
                    {
                        continue;
                    }

                    int resolvedPropId;

                    if (useInstr.opcode == ABCOp.setproperty || useInstr.opcode == ABCOp.initproperty) {
                        resolvedPropId = useInstr.data.accessProperty.resolvedPropId;
                    }
                    else if (useInstr.opcode == ABCOp.setslot) {
                        resolvedPropId = useInstr.data.getSetSlot.resolvedPropId;
                    }
                    else {
                        firstUseOfThisArgId = Math.Min(firstUseOfThisArgId, useInstr.id);
                        continue;
                    }

                    ref ResolvedProperty rp = ref m_compilation.getResolvedProperty(resolvedPropId);
                    if (rp.propKind == ResolvedPropertyKind.TRAIT
                        && rp.propInfo is FieldTrait field
                        && field.declaringClass == m_compilation.declaringClass)
                    {
                        int lastInitId;
                        if (!m_fieldInitInstructionIds.tryGetValue(field, out lastInitId))
                            lastInitId = Int32.MaxValue;

                        m_fieldInitInstructionIds[field] = Math.Min(lastInitId, useInstr.id);
                        continue;
                    }

                    firstUseOfThisArgId = Math.Min(firstUseOfThisArgId, useInstr.id);
                }
            }

            // We must ignore initializations after the first use of the "this" argument
            // (other than for the initializations themselves). Since deleting items from the
            // dictionary may interfere with iteration, set the instruction ids to -1
            // instead.

            foreach (var entry in m_fieldInitInstructionIds) {
                if (entry.Value >= firstUseOfThisArgId)
                    m_fieldInitInstructionIds[entry.Key] = -1;
            }
        }

        private void _emitSetupGlobalMemory() {
            bool hasLoadInstr = m_compilation.isAnyFlagSet(MethodCompilationFlags.READ_GLOBAL_MEMORY);
            bool hasStoreInstr = m_compilation.isAnyFlagSet(MethodCompilationFlags.WRITE_GLOBAL_MEMORY);

            // This should only be called when any of the two flags is set.
            Debug.Assert(hasLoadInstr || hasStoreInstr);

            if (hasLoadInstr)
                m_globalMemReadSpanLocal = m_ilBuilder.declareLocal(typeof(ReadOnlySpan<byte>));

            if (hasStoreInstr)
                m_globalMemWriteSpanLocal = m_ilBuilder.declareLocal(typeof(Span<byte>));

            using (var context = m_compilation.getContext())
                m_ilBuilder.emit(ILOp.ldsfld, context.value.emitConstData.appDomainFieldHandle);

            m_ilBuilder.emit(ILOp.callvirt, KnownMembers.appDomainGetGlobalMem, 1);

            if (hasStoreInstr) {
                m_ilBuilder.emit(ILOp.stloc, m_globalMemWriteSpanLocal);
                if (hasLoadInstr) {
                    m_ilBuilder.emit(ILOp.ldloc, m_globalMemWriteSpanLocal);
                    m_ilBuilder.emit(ILOp.call, KnownMembers.roSpanOfByteFromSpan, 0);
                    m_ilBuilder.emit(ILOp.stloc, m_globalMemReadSpanLocal);
                }
            }
            else {
                m_ilBuilder.emit(ILOp.call, KnownMembers.roSpanOfByteFromSpan, 0);
                m_ilBuilder.emit(ILOp.stloc, m_globalMemReadSpanLocal);
            }

            m_globalMemOutOfBoundsErrLabel = m_ilBuilder.createLabel();
        }

        private void _emitTrailer() {
            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.READ_GLOBAL_MEMORY | MethodCompilationFlags.WRITE_GLOBAL_MEMORY)) {
                // If there are global memory instructions, emit the range check failure code at the end.
                m_ilBuilder.markLabel(m_globalMemOutOfBoundsErrLabel);
                m_ilBuilder.emit(ILOp.call, KnownMembers.createGlobalMemRangeCheckError);
                m_ilBuilder.emit(ILOp.@throw);
            }

            if (m_hasExceptionHandling) {
                if (m_compilation.getExceptionHandlers().Length > 0) {
                    m_ilBuilder.beginFilterClause();
                    _emitExceptionFilterBlock();
                    m_ilBuilder.beginCatchClause();
                    _emitExceptionCatchBlock();
                }

                if (m_needsFinallyBlock)
                    m_ilBuilder.beginFinallyClause();

                if (!m_oldDxnsLocal.isDefault) {
                    // Restore the original DXNS
                    m_ilBuilder.emit(ILOp.ldloc, m_oldDxnsLocal);
                    m_ilBuilder.emit(ILOp.call, KnownMembers.setDxns, -1);
                }

                m_ilBuilder.endExceptionHandler();
                m_ilBuilder.markLabel(m_excReturnLabel);

                if (m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_RETURN_VALUE))
                    m_ilBuilder.emit(ILOp.ldloc, m_excReturnValueLocal);

                m_ilBuilder.emit(ILOp.ret);
            }
        }

        private void _visitBasicBlock(in BasicBlock block) {
            if ((block.flags & BasicBlockFlags.UNREACHABLE) != 0)
                return;

            _emitBasicBlockHeader(block);

            var instructions = m_compilation.getInstructionsInBasicBlock(block);
            for (int i = 0; i < instructions.Length; i++)
                _visitInstruction(ref instructions[i]);

            if (block.exitType == BasicBlockExitType.JUMP) {
                var exitBlockIds = m_compilation.staticIntArrayPool.getSpan(block.exitBlockIds);
                _emitJumpFromBasicBlock(block, m_compilation.getBasicBlock(exitBlockIds[0]));
            }
        }

        private void _emitBasicBlockHeader(in BasicBlock block) {
            ref BlockEmitInfo emitInfo = ref m_blockEmitInfo[block.id];

            if (emitInfo.backwardLabel != emitInfo.forwardLabel)
                m_ilBuilder.markLabel(emitInfo.backwardLabel);

            if (emitInfo.needsStackStashAndRestore) {
                var stackAtEntry = m_compilation.staticIntArrayPool.getSpan(block.stackAtEntry);
                var stashVars = m_localVarArrayPool.getSpan(emitInfo.stackStashVars);

                for (int i = 0; i < stashVars.Length; i++) {
                    if (!stashVars[i].isDefault)
                        m_ilBuilder.emit(ILOp.ldloc, stashVars[i]);
                    else
                        _emitPushConstantNode(m_compilation.getDataNode(stackAtEntry[i]));
                }
            }

            m_ilBuilder.markLabel(emitInfo.forwardLabel);

            if (m_compilation.getExceptionHandlers().Length > 0) {
                // We don't need to set the current handler id if all of the block's
                // predecessors are in the same try-region as the block.

                int blockExcHandlerId = block.excHandlerId;
                var blockEntryPoints = m_compilation.cfgNodeRefArrayPool.getSpan(block.entryPoints);
                bool mustSetCurrentHandlerId = false;

                for (int i = 0; i < blockEntryPoints.Length && !mustSetCurrentHandlerId; i++) {
                    mustSetCurrentHandlerId = !blockEntryPoints[i].isBlock
                        || m_compilation.getBasicBlock(blockEntryPoints[i].id).excHandlerId != blockExcHandlerId;
                }

                if (mustSetCurrentHandlerId) {
                    m_ilBuilder.emit(ILOp.ldc_i4, blockExcHandlerId);
                    m_ilBuilder.emit(ILOp.stloc, m_curExcHandlerIdLocal);
                }

                _syncLocalsWithCatchVarsOnTryEntry(block);
            }
        }

        private void _visitInstruction(ref Instruction instr) {
            switch (instr.opcode) {
                case ABCOp.getlocal:
                    _visitGetLocal(ref instr);
                    break;

                case ABCOp.setlocal:
                    _visitSetLocal(ref instr);
                    break;

                case ABCOp.getscopeobject:
                    _visitGetScopeObject(ref instr);
                    break;

                case ABCOp.pushscope:
                case ABCOp.pushwith:
                    _visitPushScope(ref instr);
                    break;

                case ABCOp.popscope:
                    _visitPopScope(ref instr);
                    break;

                case ABCOp.returnvalue:
                    _visitReturnValue(ref instr);
                    break;

                case ABCOp.returnvoid:
                    _visitReturnVoid(ref instr);
                    break;

                case ABCOp.@throw:
                    _visitThrow(ref instr);
                    break;

                case ABCOp.pushbyte:
                case ABCOp.pushshort:
                case ABCOp.pushint:
                case ABCOp.pushuint:
                case ABCOp.pushdouble:
                case ABCOp.pushnan:
                case ABCOp.pushfalse:
                case ABCOp.pushtrue:
                case ABCOp.pushnull:
                case ABCOp.pushundefined:
                case ABCOp.pushstring:
                case ABCOp.pushnamespace:
                    _visitPushConst(ref instr);
                    break;

                case ABCOp.pop:
                    _visitPop(ref instr);
                    break;

                case ABCOp.dup:
                    _visitDup(ref instr);
                    break;

                case ABCOp.swap:
                    _visitSwap(ref instr);
                    break;

                case ABCOp.bitnot:
                case ABCOp.negate:
                case ABCOp.negate_i:
                case ABCOp.increment:
                case ABCOp.increment_i:
                case ABCOp.decrement:
                case ABCOp.decrement_i:
                case ABCOp.sxi1:
                case ABCOp.sxi8:
                case ABCOp.sxi16:
                    _visitUnaryOp(ref instr);
                    break;

                case ABCOp.inclocal:
                case ABCOp.inclocal_i:
                case ABCOp.declocal:
                case ABCOp.declocal_i:
                    _visitIncDecLocal(ref instr);
                    break;

                case ABCOp.not:
                    _visitNot(ref instr);
                    break;

                case ABCOp.coerce:
                    _visitCoerce(ref instr);
                    break;

                case ABCOp.coerce_a:
                case ABCOp.convert_b:
                case ABCOp.convert_d:
                case ABCOp.convert_i:
                case ABCOp.convert_o:
                case ABCOp.coerce_s:
                case ABCOp.convert_s:
                case ABCOp.convert_u:
                    _visitConvertX(ref instr);
                    break;

                case ABCOp.newarray:
                    _visitNewArray(ref instr);
                    break;

                case ABCOp.newobject:
                    _visitNewObject(ref instr);
                    break;

                case ABCOp.add:
                    _visitAdd(ref instr);
                    break;

                case ABCOp.subtract:
                case ABCOp.multiply:
                case ABCOp.divide:
                case ABCOp.modulo:
                    _visitBinaryNumberOp(ref instr);
                    break;

                case ABCOp.add_i:
                case ABCOp.subtract_i:
                case ABCOp.multiply_i:
                case ABCOp.bitand:
                case ABCOp.bitor:
                case ABCOp.bitxor:
                case ABCOp.lshift:
                case ABCOp.rshift:
                case ABCOp.urshift:
                    _visitBinaryIntegerOp(ref instr);
                    break;

                case ABCOp.equals:
                case ABCOp.strictequals:
                case ABCOp.lessthan:
                case ABCOp.lessequals:
                case ABCOp.greaterthan:
                case ABCOp.greaterequals:
                    _visitBinaryCompareOp(ref instr);
                    break;

                case ABCOp.istype:
                case ABCOp.astype:
                    _visitIsAsType(ref instr);
                    break;

                case ABCOp.istypelate:
                case ABCOp.astypelate:
                    _visitIsAsTypeLate(ref instr);
                    break;

                case ABCOp.instanceof:
                    _visitInstanceof(ref instr);
                    break;

                case ABCOp.@typeof:
                    _visitTypeof(ref instr);
                    break;

                case ABCOp.dxns:
                    _visitDxns(ref instr);
                    break;

                case ABCOp.dxnslate:
                    _visitDxnsLate(ref instr);
                    break;

                case ABCOp.esc_xelem:
                case ABCOp.esc_xattr:
                    _visitEscapeXML(ref instr);
                    break;

                case ABCOp.hasnext:
                case ABCOp.nextname:
                case ABCOp.nextvalue:
                    _visitHasNextNameValue(ref instr);
                    break;

                case ABCOp.hasnext2:
                    _visitHasnext2(ref instr);
                    break;

                case ABCOp.checkfilter:
                    _visitCheckFilter(ref instr);
                    break;

                case ABCOp.newactivation:
                    _visitNewActivation(ref instr);
                    break;

                case ABCOp.newcatch:
                    _visitNewCatch(ref instr);
                    break;

                case ABCOp.@in:
                    _visitIn(ref instr);
                    break;

                case ABCOp.applytype:
                    _visitApplyType(ref instr);
                    break;

                case ABCOp.getproperty:
                case ABCOp.getsuper:
                    _visitGetProperty(ref instr);
                    break;

                case ABCOp.setproperty:
                case ABCOp.initproperty:
                case ABCOp.setsuper:
                    _visitSetProperty(ref instr);
                    break;

                case ABCOp.deleteproperty:
                    _visitDeleteProperty(ref instr);
                    break;

                case ABCOp.getslot:
                    _visitGetSlot(ref instr);
                    break;

                case ABCOp.setslot:
                    _visitSetSlot(ref instr);
                    break;

                case ABCOp.getglobalscope:
                    _visitGetGlobalScope(ref instr);
                    break;

                case ABCOp.getglobalslot:
                    _visitGetGlobalSlot(ref instr);
                    break;

                case ABCOp.setglobalslot:
                    _visitSetGlobalSlot(ref instr);
                    break;

                case ABCOp.callproperty:
                case ABCOp.callpropvoid:
                case ABCOp.callproplex:
                case ABCOp.callsuper:
                case ABCOp.callsupervoid:
                case ABCOp.constructprop:
                    _visitCallOrConstructProp(ref instr);
                    break;

                case ABCOp.constructsuper:
                    _visitConstructSuper(ref instr);
                    break;

                case ABCOp.call:
                case ABCOp.construct:
                    _visitCallOrConstruct(ref instr);
                    break;

                case ABCOp.callmethod:
                case ABCOp.callstatic:
                    _visitCallMethodOrStatic(ref instr);
                    break;

                case ABCOp.getdescendants:
                    _visitGetDescendants(ref instr);
                    break;

                case ABCOp.findproperty:
                case ABCOp.findpropstrict:
                    _visitFindProperty(ref instr);
                    break;

                case ABCOp.getlex:
                    _visitGetLex(ref instr);
                    break;

                case ABCOp.newclass:
                    _visitNewClass(ref instr);
                    break;

                case ABCOp.newfunction:
                    _visitNewFunction(ref instr);
                    break;

                case ABCOp.iftrue:
                case ABCOp.iffalse:
                    _visitIfTrueFalse(ref instr);
                    break;

                case ABCOp.ifeq:
                case ABCOp.ifne:
                case ABCOp.ifstricteq:
                case ABCOp.ifstrictne:
                case ABCOp.ifgt:
                case ABCOp.ifngt:
                case ABCOp.ifge:
                case ABCOp.ifnge:
                case ABCOp.iflt:
                case ABCOp.ifnlt:
                case ABCOp.ifle:
                case ABCOp.ifnle:
                    _visitBinaryCompareBranch(ref instr);
                    break;

                case ABCOp.lookupswitch:
                    _visitLookupSwitch(ref instr);
                    break;

                case ABCOp.li8:
                case ABCOp.lix8:
                case ABCOp.li16:
                case ABCOp.lix16:
                case ABCOp.li32:
                case ABCOp.lf32:
                case ABCOp.lf64:
                    _visitGlobalMemoryLoad(ref instr);
                    break;

                case ABCOp.si8:
                case ABCOp.si16:
                case ABCOp.si32:
                case ABCOp.sf32:
                case ABCOp.sf64:
                    _visitGlobalMemoryStore(ref instr);
                    break;
            }

            if (instr.stackPushedNodeId != -1)
                _emitOnPushTypeCoerce(m_compilation.getDataNode(instr.stackPushedNodeId));
        }

        private void _visitGetLocal(ref Instruction instr) {
            ref DataNode local = ref m_compilation.getDataNode(instr.data.getSetLocal.nodeId);
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (pushed.isNotPushed)
                return;

            if (pushed.isConstant) {
                _emitPushConstantNode(pushed);
                return;
            }

            // Check if a dup instruction can be used.
            bool canUseDup = false;
            if (instr.id != 0) {
                ref Instruction prevInstr = ref m_compilation.getInstruction(instr.id - 1);
                canUseDup = prevInstr.blockId == instr.blockId
                    && prevInstr.opcode == ABCOp.getlocal
                    && prevInstr.data.getSetLocal.nodeId == instr.data.getSetLocal.nodeId
                    && m_compilation.getDataNode(prevInstr.stackPushedNodeId).onPushCoerceType == DataNodeType.UNKNOWN;
            }

            if (canUseDup)
                m_ilBuilder.emit(ILOp.dup);
            else
                _emitLoadScopeOrLocalNode(local);
        }

        private void _visitGetScopeObject(ref Instruction instr) {
            ref DataNode scope = ref m_compilation.getDataNode(instr.data.getScopeObject.nodeId);
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (pushed.isNotPushed)
                return;

            if (pushed.isConstant)
                _emitPushConstantNode(pushed);
            else
                _emitLoadScopeOrLocalNode(scope);
        }

        private void _visitPushScope(ref Instruction instr) {
            ref DataNode popped = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));
            ref DataNode scope = ref m_compilation.getDataNode(instr.data.pushScope.pushedNodeId);

            bool isCoercedToObject = false;

            if (scope.isConstant || scope.dataType == DataNodeType.THIS || scope.dataType == DataNodeType.REST) {
                if (m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_RUNTIME_SCOPE_STACK)) {
                    if (popped.isNotPushed)
                        _emitPushConstantNode(popped, ignoreNoPush: true);

                    emitPushToRuntimeScope(popped, scope);
                }
                else {
                    _emitDiscardTopOfStack(popped);
                }
                return;
            }

            if (isAnyOrUndefined(popped.dataType)) {
                Debug.Assert(scope.dataType == DataNodeType.OBJECT);
                _emitTypeCoerceForTopOfStack(popped, DataNodeType.OBJECT);
                isCoercedToObject = true;
            }

            if (!popped.isNotNull) {
                // Null reference errors must be thrown when attempting to push a null value onto
                // the scope stack.
                Debug.Assert(!isNonNullable(popped.dataType));

                var label = m_ilBuilder.createLabel();
                m_ilBuilder.emit(ILOp.dup);
                m_ilBuilder.emit(ILOp.brtrue, label);
                m_ilBuilder.emit(ILOp.call, KnownMembers.createNullRefException, 1);
                m_ilBuilder.emit(ILOp.@throw);
                m_ilBuilder.markLabel(label);
            }

            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_RUNTIME_SCOPE_STACK)) {
                // Push onto runtime scope stack

                if (popped.isNotPushed) {
                    // We need to force push the node if it has been marked as nopush.
                    _emitPushConstantNode(popped, ignoreNoPush: true);
                }
                else {
                    m_ilBuilder.emit(ILOp.dup);
                }
                emitPushToRuntimeScope(popped, scope);
            }

            m_ilBuilder.emit(ILOp.stloc, _getLocalVarForNode(scope));

            void emitPushToRuntimeScope(in DataNode _popped, in DataNode _scope) {
                if (!isCoercedToObject)
                    _emitTypeCoerceForTopOfStack(_popped, DataNodeType.OBJECT, isForcePushed: true);

                BindOptions bindOpts = BindOptions.SEARCH_TRAITS;
                if (_scope.dataType == DataNodeType.GLOBAL)
                    bindOpts |= BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC;
                else if (_scope.isWithScope)
                    bindOpts |= BindOptions.SEARCH_DYNAMIC;

                var tempvar = m_ilBuilder.acquireTempLocal(typeof(ASObject));

                m_ilBuilder.emit(ILOp.stloc, tempvar);
                m_ilBuilder.emit(ILOp.ldloc, m_rtScopeStackLocal);
                m_ilBuilder.emit(ILOp.ldloc, tempvar);
                m_ilBuilder.emit(ILOp.ldc_i4, (int)bindOpts);
                m_ilBuilder.emit(ILOp.call, KnownMembers.rtScopeStackPush, -3);

                m_ilBuilder.releaseTempLocal(tempvar);
            }
        }

        private void _visitSetLocal(ref Instruction instr) {
            ref DataNode popped = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));
            ref DataNode local = ref m_compilation.getDataNode(instr.data.getSetLocal.newNodeId);

            local = ref _checkForLocalWriteThrough(ref local);

            if (m_compilation.getDataNodeUseCount(local) == 0) {
                _emitDiscardTopOfStack(popped);
            }
            else if (local.isConstant || local.dataType == DataNodeType.THIS || local.dataType == DataNodeType.REST) {
                _emitDiscardTopOfStack(popped);
                _syncLocalWriteWithCatchVars(instr, local);
            }
            else {
                var localVar = _getLocalVarForNode(local);

                if (popped.dataType == DataNodeType.UNDEFINED && local.dataType == DataNodeType.ANY) {
                    _emitDiscardTopOfStack(popped);
                    m_ilBuilder.emit(ILOp.ldloca, localVar);
                    m_ilBuilder.emit(ILOp.initobj, typeof(ASAny));
                }
                else {
                    if (popped.isNotPushed) {
                        _emitPushConstantNode(popped, ignoreNoPush: true);
                        _emitTypeCoerceForTopOfStack(popped, m_compilation.getDataNodeClass(local), isForcePushed: true);
                    }
                    else {
                        _emitTypeCoerceForTopOfStack(popped, m_compilation.getDataNodeClass(local));
                    }
                    m_ilBuilder.emit(ILOp.stloc, localVar);
                }

                _syncLocalWriteWithCatchVars(instr, local, localVar);
            }
        }

        private void _visitReturnVoid(ref Instruction instr) {
            var excessStack = m_compilation.staticIntArrayPool.getSpan(instr.data.returnVoidOrValue.excessStackNodeIds);
            for (int i = excessStack.Length - 1; i >= 0; i--)
                _emitDiscardTopOfStack(excessStack[i]);

            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_RETURN_VALUE)) {
                Class? returnType = m_compilation.getCurrentMethod()!.returnType;

                if (returnType != null && !returnType.getClassImpl().isUnderlyingTypeAvailable) {
                    // Classes without an underlying type (i.e. those currently being compiled)
                    // are always reference types, so the default value is always null.
                    m_ilBuilder.emit(ILOp.ldnull);
                }
                else {
                    ILEmitHelper.emitPushDefaultValueOfType(
                        m_ilBuilder,
                        Class.getUnderlyingOrPrimitiveType(returnType),
                        useNaNForFloats: true
                    );
                }

                if (m_hasExceptionHandling)
                    m_ilBuilder.emit(ILOp.stloc, m_excReturnValueLocal);
            }

            if (m_hasExceptionHandling)
                m_ilBuilder.emit(ILOp.leave, m_excReturnLabel);
            else
                m_ilBuilder.emit(ILOp.ret);
        }

        private void _visitThrow(ref Instruction instr) {
            ref DataNode popped = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));
            _emitTypeCoerceForTopOfStack(popped, DataNodeType.ANY);

            m_ilBuilder.emit(ILOp.newobj, KnownMembers.newException, 0);
            m_ilBuilder.emit(ILOp.@throw);
        }

        private void _visitReturnValue(ref Instruction instr) {
            ref DataNode popped = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));

            var excessStack = m_compilation.staticIntArrayPool.getSpan(instr.data.returnVoidOrValue.excessStackNodeIds);
            if (excessStack.Length > 0) {
                // Pop any excess values off the stack.

                int popsRequired = 0;
                for (int i = 0; i < excessStack.Length; i++) {
                    if (!m_compilation.getDataNode(excessStack[i]).isNotPushed)
                        popsRequired++;
                }

                if (popsRequired > 0) {
                    var stashVar = _emitStashTopOfStack(popped);

                    for (int i = 0; i < popsRequired; i++)
                        m_ilBuilder.emit(ILOp.pop);

                    _emitUnstash(stashVar);
                }
            }

            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_RETURN_VALUE)) {
                Class? returnType = m_compilation.getCurrentMethod()!.returnType;
                _emitTypeCoerceForTopOfStack(popped, returnType);

                if (m_hasExceptionHandling)
                    m_ilBuilder.emit(ILOp.stloc, m_excReturnValueLocal);
            }
            else {
                _emitDiscardTopOfStack(popped);
            }

            if (m_hasExceptionHandling)
                m_ilBuilder.emit(ILOp.leave, m_excReturnLabel);
            else
                m_ilBuilder.emit(ILOp.ret);
        }

        private void _visitPushConst(ref Instruction instr) {
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            _emitPushConstantNode(pushed);
        }

        private void _visitPop(ref Instruction instr) {
            ref DataNode popped = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));
            _emitDiscardTopOfStack(popped);
        }

        private void _visitPopScope(ref Instruction instr) {
            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_RUNTIME_SCOPE_STACK)) {
                m_ilBuilder.emit(ILOp.ldloc, m_rtScopeStackLocal);
                m_ilBuilder.emit(ILOp.call, KnownMembers.rtScopeStackPop, -1);
            }
        }

        private void _visitDup(ref Instruction instr) {
            ref DataNode node = ref m_compilation.getDataNode(instr.data.dupOrSwap.nodeId1);
            ref DataNode dupedNode = ref m_compilation.getDataNode(instr.data.dupOrSwap.nodeId2);

            if (dupedNode.isNotPushed)
                return;

            if (dupedNode.isConstant) {
                if (dupedNode.dataType != node.dataType
                    || node.onPushCoerceType != DataNodeType.UNKNOWN
                    || node.isNotPushed)
                {
                    _emitPushConstantNode(dupedNode);
                    return;
                }
            }

            if (node.isNotPushed) {
                _emitPushConstantNode(node, ignoreNoPush: true);
                return;
            }

            m_ilBuilder.emit(ILOp.dup);
        }

        private void _visitSwap(ref Instruction instr) {
            ref DataNode leftNode = ref m_compilation.getDataNode(instr.data.dupOrSwap.nodeId1);
            ref DataNode rightNode = ref m_compilation.getDataNode(instr.data.dupOrSwap.nodeId2);

            if (((leftNode.flags | rightNode.flags) & DataNodeFlags.NO_PUSH) != 0)
                // If any one node is marked as no-push then no need to swap.
                return;

            var stashRight = _emitStashTopOfStack(rightNode);
            var stashLeft = _emitStashTopOfStack(leftNode);
            _emitUnstash(stashRight);
            _emitUnstash(stashLeft);
        }

        private void _visitUnaryOp(ref Instruction instr) {
            ref DataNode inputNode = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));
            ref DataNode outputNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (outputNode.isConstant) {
                _emitDiscardTopOfStack(inputNode);
                _emitPushConstantNode(outputNode);
                return;
            }

            switch (instr.opcode) {
                case ABCOp.bitnot:
                    _emitTypeCoerceForTopOfStack(inputNode, DataNodeType.INT);
                    m_ilBuilder.emit(ILOp.not);
                    break;

                case ABCOp.negate_i:
                    _emitTypeCoerceForTopOfStack(inputNode, DataNodeType.INT);
                    m_ilBuilder.emit(ILOp.neg);
                    break;

                case ABCOp.negate:
                    _emitTypeCoerceForTopOfStack(inputNode, outputNode.dataType);
                    m_ilBuilder.emit(ILOp.neg);
                    break;

                case ABCOp.increment:
                case ABCOp.decrement:
                {
                    _emitTypeCoerceForTopOfStack(inputNode, outputNode.dataType);

                    m_ilBuilder.emit(ILOp.ldc_i4_1);
                    if (outputNode.dataType == DataNodeType.NUMBER)
                        m_ilBuilder.emit(ILOp.conv_r8);

                    m_ilBuilder.emit((instr.opcode == ABCOp.decrement) ? ILOp.sub : ILOp.add);

                    break;
                }

                case ABCOp.increment_i:
                case ABCOp.decrement_i:
                {
                    _emitTypeCoerceForTopOfStack(inputNode, DataNodeType.INT);

                    m_ilBuilder.emit(ILOp.ldc_i4_1);
                    m_ilBuilder.emit((instr.opcode == ABCOp.decrement_i) ? ILOp.sub : ILOp.add);

                    break;
                }

                case ABCOp.sxi1:
                    _emitTypeCoerceForTopOfStack(inputNode, DataNodeType.INT);
                    m_ilBuilder.emit(ILOp.ldc_i4_1);
                    m_ilBuilder.emit(ILOp.and);
                    m_ilBuilder.emit(ILOp.neg);
                    break;

                case ABCOp.sxi8:
                case ABCOp.sxi16:
                    _emitTypeCoerceForTopOfStack(inputNode, DataNodeType.INT);
                    m_ilBuilder.emit((instr.opcode == ABCOp.sxi8) ? ILOp.conv_i1 : ILOp.conv_i2);
                    break;
            }
        }

        private void _visitNot(ref Instruction instr) {
            ref DataNode inputNode = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));
            ref DataNode outputNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (outputNode.isConstant) {
                _emitDiscardTopOfStack(inputNode);
                _emitPushConstantNode(outputNode);
                return;
            }

            switch (_getPushedTypeOfNode(inputNode)) {
                case DataNodeType.INT:
                case DataNodeType.UINT:
                case DataNodeType.BOOL:
                    m_ilBuilder.emit(ILOp.ldc_i4_0);
                    m_ilBuilder.emit(ILOp.ceq);
                    break;

                case DataNodeType.OBJECT: {
                    if (!_getPushedClassOfNode(inputNode)!.isObjectClass) {
                        // If the input cannot be a boxed primitive then this is a simple null check.
                        m_ilBuilder.emit(ILOp.ldnull);
                        m_ilBuilder.emit(ILOp.ceq);
                        break;
                    }
                    goto default;
                }

                default:
                    _emitTypeCoerceForTopOfStack(inputNode, DataNodeType.BOOL);
                    goto case DataNodeType.BOOL;
            }
        }

        private void _visitIncDecLocal(ref Instruction instr) {
            ref DataNode oldLocal = ref m_compilation.getDataNode(instr.data.getSetLocal.nodeId);
            ref DataNode newLocal = ref m_compilation.getDataNode(instr.data.getSetLocal.newNodeId);

            if (m_compilation.getDataNodeUseCount(newLocal) == 0)
                return;

            if (newLocal.isConstant) {
                _syncLocalWriteWithCatchVars(instr, newLocal);
                return;
            }

            DataNodeType type =
                (instr.opcode == ABCOp.inclocal_i || instr.opcode == ABCOp.declocal_i) ? DataNodeType.INT : DataNodeType.NUMBER;

            Debug.Assert(type == newLocal.dataType);

            _emitLoadScopeOrLocalNode(oldLocal);
            _emitTypeCoerceForTopOfStack(oldLocal, type);

            m_ilBuilder.emit(ILOp.ldc_i4_1);
            if (type == DataNodeType.NUMBER)
                m_ilBuilder.emit(ILOp.conv_r8);

            m_ilBuilder.emit((instr.opcode == ABCOp.declocal || instr.opcode == ABCOp.declocal_i) ? ILOp.sub : ILOp.add);

            var newLocalVar = _getLocalVarForNode(newLocal);
            m_ilBuilder.emit(ILOp.stloc, newLocalVar);
            _syncLocalWriteWithCatchVars(instr, newLocal, newLocalVar);
        }

        private void _visitCoerce(ref Instruction instr) {
            ref DataNode inputNode = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));
            ref DataNode outputNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (outputNode.isConstant) {
                _emitDiscardTopOfStack(inputNode);
                _emitPushConstantNode(outputNode);
            }
            else {
                _emitTypeCoerceForTopOfStack(inputNode, m_compilation.getDataNodeClass(outputNode));
            }
        }

        private void _visitConvertX(ref Instruction instr) {
            ref DataNode inputNode = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));
            ref DataNode outputNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (outputNode.isConstant) {
                _emitDiscardTopOfStack(inputNode);
                _emitPushConstantNode(outputNode);
            }
            else if (instr.opcode != ABCOp.coerce_a) {
                _emitTypeCoerceForTopOfStack(inputNode, outputNode.dataType, useConvertStr: instr.opcode == ABCOp.convert_s);
            }
        }

        private void _visitNewArray(ref Instruction instr) {
            var poppedNodeIds = m_compilation.getInstructionStackPoppedNodes(instr);
            int elementCount = instr.data.newArrOrObj.elementCount;

            if (elementCount == 0) {
                m_ilBuilder.emit(ILOp.newobj, KnownMembers.arrayCtorWithNoArgs, 1);
                return;
            }

            if (tryGetHelper(poppedNodeIds, out EntityHandle helperHandle)) {
                m_ilBuilder.emit(ILOp.call, helperHandle, -poppedNodeIds.Length + 1);
                return;
            }

            // The number of elements is non-zero and we cannot use a helper, so emit inline code.

            m_ilBuilder.emit(ILOp.ldc_i4, elementCount);
            m_ilBuilder.emit(ILOp.newobj, KnownMembers.arrayCtorWithLength, 0);

            var arrLocal = m_ilBuilder.acquireTempLocal(typeof(ASArray));
            var elemLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny));

            m_ilBuilder.emit(ILOp.stloc, arrLocal);

            for (int i = elementCount - 1; i >= 0; i--) {
                ref DataNode node = ref m_compilation.getDataNode(poppedNodeIds[i]);
                _emitTypeCoerceForTopOfStack(node, DataNodeType.ANY);

                m_ilBuilder.emit(ILOp.stloc, elemLocal);
                m_ilBuilder.emit(ILOp.ldloc, arrLocal);
                m_ilBuilder.emit(ILOp.ldc_i4, i);
                m_ilBuilder.emit(ILOp.ldloc, elemLocal);
                m_ilBuilder.emit(ILOp.call, KnownMembers.arraySetUintIndex, -3);
            }

            m_ilBuilder.emit(ILOp.ldloc, arrLocal);

            m_ilBuilder.releaseTempLocal(elemLocal);
            m_ilBuilder.releaseTempLocal(arrLocal);

            bool tryGetHelper(ReadOnlySpan<int> _argsOnStack, out EntityHandle _helperHandle) {
                _helperHandle = default;

                using var lockedContext = m_compilation.getContext();
                HelperEmitter helperEmitter = lockedContext.value.helperEmitter;

                if (_argsOnStack.Length > helperEmitter.newArrayHelperMaxSize)
                    return false;

                // Use a helper only if no type conversions are needed.
                // This is not a significant limitation in practice because type conversions to "any" are
                // considered as non-side-effecting, and so would be hoisted except in some complex data flow
                // graphs.

                for (int i = 0; i < _argsOnStack.Length; i++) {
                    DataNodeType pushedType = _getPushedTypeOfNode(m_compilation.getDataNode(_argsOnStack[i]));
                    if (!isAnyOrUndefined(pushedType))
                        return false;
                }

                return helperEmitter.tryGetNewArrayHelper(_argsOnStack.Length, out _helperHandle);
            }
        }

        private void _visitNewObject(ref Instruction instr) {
            var poppedNodeIds = m_compilation.getInstructionStackPoppedNodes(instr);
            int propertyCount = instr.data.newArrOrObj.elementCount;

            Debug.Assert(poppedNodeIds.Length == propertyCount * 2);

            if (propertyCount == 0) {
                m_ilBuilder.emit(ILOp.newobj, KnownMembers.objectCtor, 1);
                return;
            }

            if (tryGetHelper(poppedNodeIds, out EntityHandle helperHandle)) {
                m_ilBuilder.emit(ILOp.call, helperHandle, -poppedNodeIds.Length + 1);
                return;
            }

            // The number of properties is non-zero and we cannot use a helper, so emit inline code.

            m_ilBuilder.emit(ILOp.newobj, KnownMembers.objectCtor, 1);

            var objLocal = m_ilBuilder.acquireTempLocal(typeof(ASObject));
            var dynPropLocal = m_ilBuilder.acquireTempLocal(typeof(DynamicPropertyCollection));
            var keyLocal = m_ilBuilder.acquireTempLocal(typeof(string));
            var valLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny));

            m_ilBuilder.emit(ILOp.dup);
            m_ilBuilder.emit(ILOp.stloc, objLocal);
            m_ilBuilder.emit(ILOp.call, KnownMembers.getObjectDynamicPropCollection, 0);
            m_ilBuilder.emit(ILOp.stloc, dynPropLocal);

            // Object properties are set in top-to-bottom stack order.
            // So for example if the arguments are "a", 1, "b", 2, "a", 3 then the value of the property
            // "a" in the created object is 1, not 3.

            for (int i = propertyCount - 1; i >= 0; i--) {
                int keyStackPos = i * 2;
                ref DataNode keyNode = ref m_compilation.getDataNode(poppedNodeIds[keyStackPos]);
                ref DataNode valNode = ref m_compilation.getDataNode(poppedNodeIds[keyStackPos + 1]);

                _emitTypeCoerceForTopOfStack(valNode, DataNodeType.ANY);
                m_ilBuilder.emit(ILOp.stloc, valLocal);
                _emitTypeCoerceForTopOfStack(keyNode, DataNodeType.STRING);
                m_ilBuilder.emit(ILOp.stloc, keyLocal);

                m_ilBuilder.emit(ILOp.ldloc, dynPropLocal);
                m_ilBuilder.emit(ILOp.ldloc, keyLocal);
                m_ilBuilder.emit(ILOp.ldloc, valLocal);
                m_ilBuilder.emit(ILOp.ldc_i4_1);    // isEnum = true
                m_ilBuilder.emit(ILOp.call, KnownMembers.dynamicPropCollectionSet, -4);
            }

            m_ilBuilder.emit(ILOp.ldloc, objLocal);

            m_ilBuilder.releaseTempLocal(objLocal);
            m_ilBuilder.releaseTempLocal(keyLocal);
            m_ilBuilder.releaseTempLocal(valLocal);
            m_ilBuilder.releaseTempLocal(dynPropLocal);

            bool tryGetHelper(ReadOnlySpan<int> _argsOnStack, out EntityHandle _helperHandle) {
                _helperHandle = default;

                using var lockedContext = m_compilation.getContext();
                HelperEmitter helperEmitter = lockedContext.value.helperEmitter;

                if (_argsOnStack.Length > helperEmitter.newObjectHelperMaxSize * 2)
                    return false;

                // Use a helper only if no type conversions are needed.
                // This is not a significant limitation in practice because: (i) the keys are usually literal strings
                // and (ii) type conversions of the values to "any" are considered as non-side-effecting, and so would
                // be hoisted except in some complex data flow graphs.

                for (int i = 0; i < _argsOnStack.Length; i++) {
                    DataNodeType pushedType = _getPushedTypeOfNode(m_compilation.getDataNode(_argsOnStack[i]));
                    bool argIsKey = (i & 1) == 0;

                    if ((argIsKey && pushedType != DataNodeType.STRING) || (!argIsKey && !isAnyOrUndefined(pushedType)))
                        return false;
                }

                return helperEmitter.tryGetNewObjectHelper(_argsOnStack.Length >> 1, out _helperHandle);
            }
        }

        private void _visitAdd(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            ref DataNode leftInputNode = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode rightInputNode = ref m_compilation.getDataNode(stackPopIds[1]);
            ref DataNode outputNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (outputNode.isConstant) {
                _emitDiscardTopOfStack(rightInputNode);
                _emitDiscardTopOfStack(leftInputNode);
                _emitPushConstantNode(outputNode);
                return;
            }

            switch (outputNode.dataType) {
                case DataNodeType.INT:
                case DataNodeType.UINT:
                    _emitTypeCoerceForStackTop2(leftInputNode, rightInputNode, DataNodeType.INT, DataNodeType.INT);
                    m_ilBuilder.emit(ILOp.add);
                    break;

                case DataNodeType.NUMBER:
                    _emitTypeCoerceForStackTop2(leftInputNode, rightInputNode, DataNodeType.NUMBER, DataNodeType.NUMBER);
                    m_ilBuilder.emit(ILOp.add);
                    break;

                case DataNodeType.STRING: {
                    if (instr.data.add.isConcatTreeInternalNode) {
                        // Internal node of a concat tree - leave the inputs on the stack.
                        break;
                    }
                    else if (instr.data.add.isConcatTreeRoot) {
                        // String concat tree root node.
                        _emitStringConcatTree(instr);
                    }
                    else {
                        // Not part of a concat tree. Just a simple binary concatenation.
                        // The concatentation helpers convert null strings to "null", so don't emit conversions
                        // if both operands are already strings.
                        bool useConvertStr = !isStringOrNull(_getPushedTypeOfNode(leftInputNode)) && !isStringOrNull(_getPushedTypeOfNode(rightInputNode));

                        _emitTypeCoerceForStackTop2(leftInputNode, rightInputNode, DataNodeType.STRING, DataNodeType.STRING, useConvertStr);
                        m_ilBuilder.emit(ILOp.call, KnownMembers.stringAdd2, -1);
                    }
                    break;
                }

                case DataNodeType.OBJECT: {
                    DataNodeType inputType = instr.data.add.argsAreAnyType ? DataNodeType.ANY : DataNodeType.OBJECT;
                    _emitTypeCoerceForStackTop2(leftInputNode, rightInputNode, inputType, inputType);
                    m_ilBuilder.emit(ILOp.call, instr.data.add.argsAreAnyType ? KnownMembers.anyAdd : KnownMembers.objectAdd, -1);
                    break;
                }
            }
        }

        private void _visitBinaryNumberOp(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            ref DataNode leftInputNode = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode rightInputNode = ref m_compilation.getDataNode(stackPopIds[1]);
            ref DataNode outputNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (outputNode.isConstant) {
                _emitDiscardTopOfStack(rightInputNode);
                _emitDiscardTopOfStack(leftInputNode);
                _emitPushConstantNode(outputNode);
                return;
            }

            if (instr.opcode == ABCOp.subtract || instr.opcode == ABCOp.multiply) {
                _emitTypeCoerceForStackTop2(leftInputNode, rightInputNode, outputNode.dataType, outputNode.dataType);
                m_ilBuilder.emit((instr.opcode == ABCOp.subtract) ? ILOp.sub : ILOp.mul);
            }
            else {
                // Divide and modulo

                _emitTypeCoerceForStackTop2(leftInputNode, rightInputNode, outputNode.dataType, outputNode.dataType);

                if (outputNode.dataType == DataNodeType.NUMBER) {
                    // Floating-point division/remainder.
                    m_ilBuilder.emit((instr.opcode == ABCOp.divide) ? ILOp.div : ILOp.rem);
                    return;
                }

                bool isUnsigned = shouldUseUnsignedDivOrMod(instr, leftInputNode, rightInputNode);

                ILOp opcode = isUnsigned
                    ? ((instr.opcode == ABCOp.divide) ? ILOp.div_un : ILOp.rem_un)
                    : ((instr.opcode == ABCOp.divide) ? ILOp.div : ILOp.rem);

                // For integer divisions we need to add checks to prevent divide-by-zero and
                // overflow exceptions. These check the divisor for 0 (and for signed divisions, -1).
                // If the divisor is a constant that is not one of these values, the checks can be
                // skipped.

                bool willNeverThrow = rightInputNode.isConstant && rightInputNode.constant.intValue != 0
                    && (isUnsigned || rightInputNode.constant.intValue != -1);

                if (willNeverThrow) {
                    m_ilBuilder.emit(opcode);
                    return;
                }

                bool shouldCheckForMinus1 = !isUnsigned && (!rightInputNode.isConstant || rightInputNode.constant.intValue == -1);

                var label1 = m_ilBuilder.createLabel();
                var label2 = m_ilBuilder.createLabel();

                m_ilBuilder.emit(ILOp.dup);
                m_ilBuilder.emit(ILOp.brfalse, label1);

                if (shouldCheckForMinus1) {
                    m_ilBuilder.emit(ILOp.dup);
                    m_ilBuilder.emit(ILOp.ldc_i4_m1);
                    m_ilBuilder.emit(ILOp.beq, label1);
                }

                m_ilBuilder.emit(opcode);
                m_ilBuilder.emit(ILOp.br, label2);
                m_ilBuilder.markLabel(label1);

                if (!shouldCheckForMinus1) {
                    // Only checking for zero in this case, and the result is
                    // always zero (NaN converted to an integer), so AND the dividend with 0.
                    m_ilBuilder.emit(ILOp.and);
                }
                else if (instr.opcode == ABCOp.divide) {
                    // x / 0 is zero (= x * 0), x / -1 is -x (= x * -1)
                    m_ilBuilder.emit(ILOp.mul);
                }
                else {
                    // x % 0 and x % -1 are both zero.
                    m_ilBuilder.emit(ILOp.pop);
                    m_ilBuilder.emit(ILOp.pop);
                    m_ilBuilder.emit(ILOp.ldc_i4_0);
                }

                m_ilBuilder.markLabel(label2);
            }

            static bool shouldUseUnsignedDivOrMod(in Instruction _instr, in DataNode _left, in DataNode _right) {
                if (_instr.data.binaryOp.forceUnsignedDivOrMod)
                    return true;

                bool leftIsUnsigned =
                    _left.dataType == DataNodeType.UINT || (_left.isConstant && _left.constant.intValue >= 0);

                bool rightIsUnsigned =
                    _right.dataType == DataNodeType.UINT || (_right.isConstant && _right.constant.intValue >= 0);

                return leftIsUnsigned && rightIsUnsigned;
            }
        }

        private void _visitBinaryIntegerOp(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            ref DataNode leftInputNode = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode rightInputNode = ref m_compilation.getDataNode(stackPopIds[1]);
            ref DataNode outputNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (outputNode.isConstant) {
                _emitDiscardTopOfStack(rightInputNode);
                _emitDiscardTopOfStack(leftInputNode);
                _emitPushConstantNode(outputNode);
                return;
            }

            _emitTypeCoerceForStackTop2(leftInputNode, rightInputNode, DataNodeType.INT, DataNodeType.INT);

            m_ilBuilder.emit(instr.opcode switch {
                ABCOp.add_i => ILOp.add,
                ABCOp.subtract_i => ILOp.sub,
                ABCOp.multiply_i => ILOp.mul,
                ABCOp.bitand => ILOp.and,
                ABCOp.bitor => ILOp.or,
                ABCOp.bitxor => ILOp.xor,
                ABCOp.lshift => ILOp.shl,
                ABCOp.rshift => ILOp.shr,
                ABCOp.urshift => ILOp.shr_un
            });
        }

        private void _visitBinaryCompareOp(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            ref DataNode leftInputNode = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode rightInputNode = ref m_compilation.getDataNode(stackPopIds[1]);
            ref DataNode outputNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (outputNode.isConstant) {
                _emitDiscardTopOfStack(rightInputNode);
                _emitDiscardTopOfStack(leftInputNode);
                _emitPushConstantNode(outputNode);
                return;
            }

            if (instr.data.compare.compareType == ComparisonType.STR_CHARAT_L
                || instr.data.compare.compareType == ComparisonType.STR_CHARAT_R)
            {
                // branchEmitInfo is ignored since this instruction is not a branch, so use a dummy value.
                TwoWayBranchEmitInfo branchEmitInfo = default;
                _emitStringCharAtIntrinsicCompare(instr, leftInputNode, rightInputNode, branchEmitInfo);
                return;
            }

            if (instr.opcode == ABCOp.equals || instr.opcode == ABCOp.strictequals)
                emitEquals(instr, leftInputNode, rightInputNode);
            else
                emitCompare(instr, leftInputNode, rightInputNode);

            void emitEquals(in Instruction _instr, in DataNode _left, in DataNode _right) {
                var compareType = _instr.data.compare.compareType;

                switch (compareType) {
                    case ComparisonType.INT:
                    case ComparisonType.UINT:
                        _emitTypeCoerceForStackTop2(_left, _right, DataNodeType.INT, DataNodeType.INT);
                        m_ilBuilder.emit(ILOp.ceq);
                        break;

                    case ComparisonType.NUMBER:
                        _emitTypeCoerceForStackTop2(_left, _right, DataNodeType.NUMBER, DataNodeType.NUMBER);
                        m_ilBuilder.emit(ILOp.ceq);
                        break;

                    case ComparisonType.BOOL:
                        _emitTypeCoerceForStackTop2(_left, _right, DataNodeType.BOOL, DataNodeType.BOOL);
                        m_ilBuilder.emit(ILOp.ceq);
                        break;

                    case ComparisonType.STRING:
                        _emitTypeCoerceForStackTop2(_left, _right, DataNodeType.STRING, DataNodeType.STRING);
                        m_ilBuilder.emit(ILOp.call, KnownMembers.strEquals, -1);
                        break;

                    case ComparisonType.NAMESPACE:
                        Debug.Assert(_left.dataType == DataNodeType.NAMESPACE && _right.dataType == DataNodeType.NAMESPACE);
                        m_ilBuilder.emit(ILOp.call, KnownMembers.xmlNamespaceEquals, -1);
                        break;

                    case ComparisonType.QNAME:
                        Debug.Assert(_left.dataType == DataNodeType.QNAME && _right.dataType == DataNodeType.QNAME);
                        m_ilBuilder.emit(ILOp.call, KnownMembers.xmlQnameEquals, -1);
                        break;

                    case ComparisonType.OBJ_REF:
                        _emitTypeCoerceForStackTop2(_left, _right, DataNodeType.OBJECT, DataNodeType.OBJECT);
                        m_ilBuilder.emit(ILOp.ceq);
                        break;

                    case ComparisonType.OBJECT:
                        _emitTypeCoerceForStackTop2(_left, _right, DataNodeType.OBJECT, DataNodeType.OBJECT);
                        m_ilBuilder.emit(ILOp.call, (_instr.opcode == ABCOp.equals) ? KnownMembers.objWeakEq : KnownMembers.objStrictEq, -1);
                        break;

                    case ComparisonType.ANY:
                        _emitTypeCoerceForStackTop2(_left, _right, DataNodeType.ANY, DataNodeType.ANY);
                        m_ilBuilder.emit(ILOp.call, (_instr.opcode == ABCOp.equals) ? KnownMembers.anyWeakEq : KnownMembers.anyStrictEq, -1);
                        break;

                    case ComparisonType.INT_ZERO_L:
                    case ComparisonType.INT_ZERO_R:
                    {
                        var constFlags = (compareType == ComparisonType.INT_ZERO_L) ? _left.flags : _right.flags;
                        if ((constFlags & DataNodeFlags.NO_PUSH) == 0)
                            goto case ComparisonType.UINT;

                        m_ilBuilder.emit(ILOp.ldc_i4_0);
                        m_ilBuilder.emit(ILOp.ceq);
                        break;
                    }

                    case ComparisonType.BOOL_CONST_L:
                    case ComparisonType.BOOL_CONST_R:
                    {
                        var constFlags = (compareType == ComparisonType.BOOL_CONST_L) ? _left.flags : _right.flags;
                        if ((constFlags & DataNodeFlags.NO_PUSH) == 0)
                            goto case ComparisonType.UINT;

                        bool constValue =
                            (compareType == ComparisonType.BOOL_CONST_L) ? _left.constant.boolValue : _right.constant.boolValue;

                        // x == true is a no-op when x is boolean.
                        if (!constValue) {
                            m_ilBuilder.emit(ILOp.ldc_i4_0);
                            m_ilBuilder.emit(ILOp.ceq);
                        }
                        break;
                    }

                    case ComparisonType.OBJ_NULL_L:
                    case ComparisonType.OBJ_NULL_R:
                    {
                        var constFlags = (compareType == ComparisonType.OBJ_NULL_L) ? _left.flags : _right.flags;

                        if ((constFlags & DataNodeFlags.NO_PUSH) == 0) {
                            ref readonly DataNode varNode =
                                ref ((compareType == ComparisonType.OBJ_NULL_L) ? ref _right : ref _left);

                            if (_getPushedTypeOfNode(varNode) != DataNodeType.STRING)
                                _emitTypeCoerceForStackTop2(_left, _right, DataNodeType.OBJECT, DataNodeType.OBJECT);

                            m_ilBuilder.emit(ILOp.ceq);
                        }
                        else {
                            m_ilBuilder.emit(ILOp.ldnull);
                            m_ilBuilder.emit(ILOp.ceq);
                        }
                        break;
                    }

                    case ComparisonType.ANY_UNDEF_L:
                    case ComparisonType.ANY_UNDEF_R:
                    {
                        var constFlags = (compareType == ComparisonType.ANY_UNDEF_L) ? _left.flags : _right.flags;
                        if ((constFlags & DataNodeFlags.NO_PUSH) == 0)
                            goto case ComparisonType.ANY;

                        var tmp = m_ilBuilder.acquireTempLocal(typeof(ASAny));
                        m_ilBuilder.emit(ILOp.stloc, tmp);
                        m_ilBuilder.emit(ILOp.ldloca, tmp);
                        m_ilBuilder.emit(ILOp.call, KnownMembers.anyGetIsDefined, 0);
                        break;
                    }
                }
            }

            void emitNumericCompareOp(ABCOp opcode, bool isUnsigned = false, bool isFloat = false) {
                switch (opcode) {
                    case ABCOp.lessthan:
                        m_ilBuilder.emit(isUnsigned ? ILOp.clt_un : ILOp.clt);
                        break;

                    case ABCOp.lessequals:
                        m_ilBuilder.emit((isUnsigned | isFloat) ? ILOp.cgt_un : ILOp.cgt);
                        m_ilBuilder.emit(ILOp.ldc_i4_0);
                        m_ilBuilder.emit(ILOp.ceq);
                        break;

                    case ABCOp.greaterthan:
                        m_ilBuilder.emit(isUnsigned ? ILOp.cgt_un : ILOp.cgt);
                        break;

                    case ABCOp.greaterequals:
                        m_ilBuilder.emit((isUnsigned | isFloat) ? ILOp.clt_un : ILOp.clt);
                        m_ilBuilder.emit(ILOp.ldc_i4_0);
                        m_ilBuilder.emit(ILOp.ceq);
                        break;
                }
            }

            void emitCompare(in Instruction _instr, in DataNode _left, in DataNode _right) {
                var compareType = _instr.data.compare.compareType;

                switch (compareType) {
                    case ComparisonType.INT:
                        _emitTypeCoerceForStackTop2(_left, _right, DataNodeType.INT, DataNodeType.INT);
                        emitNumericCompareOp(_instr.opcode);
                        break;

                    case ComparisonType.UINT:
                        _emitTypeCoerceForStackTop2(_left, _right, DataNodeType.INT, DataNodeType.INT);
                        emitNumericCompareOp(_instr.opcode, isUnsigned: true);
                        break;

                    case ComparisonType.NUMBER:
                        _emitTypeCoerceForStackTop2(_left, _right, DataNodeType.NUMBER, DataNodeType.NUMBER);
                        emitNumericCompareOp(_instr.opcode, isFloat: true);
                        break;

                    case ComparisonType.STRING: {
                        _emitTypeCoerceForStackTop2(_left, _right, DataNodeType.STRING, DataNodeType.STRING);

                        MethodInfo method = _instr.opcode switch {
                            ABCOp.lessthan => KnownMembers.strLt,
                            ABCOp.lessequals => KnownMembers.strLeq,
                            ABCOp.greaterthan => KnownMembers.strGt,
                            ABCOp.greaterequals => KnownMembers.strGeq
                        };

                        m_ilBuilder.emit(ILOp.call, method, -1);
                        break;
                    }

                    case ComparisonType.OBJECT: {
                        _emitTypeCoerceForStackTop2(_left, _right, DataNodeType.OBJECT, DataNodeType.OBJECT);

                        MethodInfo method = _instr.opcode switch {
                            ABCOp.lessthan => KnownMembers.objLt,
                            ABCOp.lessequals => KnownMembers.objLeq,
                            ABCOp.greaterthan => KnownMembers.objGt,
                            ABCOp.greaterequals => KnownMembers.objGeq,
                        };

                        m_ilBuilder.emit(ILOp.call, method, -1);
                        break;
                    }

                    case ComparisonType.ANY: {
                        _emitTypeCoerceForStackTop2(_left, _right, DataNodeType.ANY, DataNodeType.ANY);

                        MethodInfo method = _instr.opcode switch {
                            ABCOp.lessthan => KnownMembers.anyLt,
                            ABCOp.lessequals => KnownMembers.anyLeq,
                            ABCOp.greaterthan => KnownMembers.anyGt,
                            ABCOp.greaterequals => KnownMembers.anyGeq,
                        };

                        m_ilBuilder.emit(ILOp.call, method, -1);
                        break;
                    }

                    case ComparisonType.INT_ZERO_L:
                    case ComparisonType.INT_ZERO_R:
                    {
                        Debug.Assert(_instr.opcode == ((compareType == ComparisonType.INT_ZERO_L) ? ABCOp.lessthan : ABCOp.greaterthan));

                        var constFlags = (compareType == ComparisonType.INT_ZERO_L) ? _left.flags : _right.flags;
                        if ((constFlags & DataNodeFlags.NO_PUSH) == 0)
                            goto case ComparisonType.UINT;

                        m_ilBuilder.emit(ILOp.ldc_i4_0);
                        m_ilBuilder.emit(ILOp.cgt_un);
                        break;
                    }

                    default:
                        Debug.Assert(false, $"Invalid compare type for non-equality comparison: {compareType}.");
                        break;
                }
            }
        }

        private void _visitIsAsType(ref Instruction instr) {
            ref DataNode inputNode = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));
            ref DataNode outputNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            ABCMultiname multiname = m_compilation.abcFile.resolveMultiname(instr.data.coerceOrIsType.multinameId);

            Class klass;
            using (var lockedContext = m_compilation.getContext())
                klass = lockedContext.value.getClassByMultiname(multiname)!;

            if (ClassTagSet.numeric.contains(klass.tag) || !isObjectType(inputNode.dataType))
                _emitTypeCoerceForTopOfStack(inputNode, DataNodeType.OBJECT);

            _emitIsOrAsType(klass, instr.opcode, outputNode.id);
        }

        private void _visitIsAsTypeLate(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);

            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode typeNode = ref m_compilation.getDataNode(stackPopIds[1]);
            ref DataNode outputNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            Class? klass = (typeNode.dataType == DataNodeType.CLASS) ? typeNode.constant.classValue : null;

            if (klass != null && !klass.hasUnderlyingType(typeof(ASVector<>))) {
                _emitDiscardTopOfStack(typeNode);

                if (ClassTagSet.numeric.contains(klass.tag) || !isObjectType(objectNode.dataType))
                    _emitTypeCoerceForTopOfStack(objectNode, DataNodeType.OBJECT);

                _emitIsOrAsType(klass, instr.opcode, outputNode.id);
            }
            else {
                MethodInfo method = (instr.opcode == ABCOp.istypelate) ? KnownMembers.objIsType : KnownMembers.objAsType;

                _emitTypeCoerceForStackTop2(objectNode, typeNode, DataNodeType.OBJECT, DataNodeType.OBJECT);
                m_ilBuilder.emit(ILOp.call, method, -1);
            }
        }

        private void _visitInstanceof(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode typeNode = ref m_compilation.getDataNode(stackPopIds[1]);

            _emitTypeCoerceForStackTop2(objectNode, typeNode, DataNodeType.OBJECT, DataNodeType.OBJECT);
            m_ilBuilder.emit(ILOp.call, KnownMembers.objInstanceof, -1);
        }

        private void _visitTypeof(ref Instruction instr) {
            ref DataNode inputNode = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));

            if (isAnyOrUndefined(inputNode.dataType)) {
                m_ilBuilder.emit(ILOp.call, KnownMembers.anyTypeof, 0);
            }
            else {
                _emitTypeCoerceForTopOfStack(inputNode, DataNodeType.OBJECT);
                m_ilBuilder.emit(ILOp.call, KnownMembers.objTypeof, 0);
            }
        }

        private void _visitDxns(ref Instruction instr) {
            string uri = m_compilation.abcFile.resolveString(instr.data.dxns.uriId) ?? "";
            _emitPushXmlNamespaceConstant(new Namespace(uri));
            m_ilBuilder.emit(ILOp.call, KnownMembers.setDxns, -1);
        }

        private void _visitDxnsLate(ref Instruction instr) {
            ref DataNode inputNode = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));

            if (inputNode.isConstant
                && (inputNode.dataType == DataNodeType.NAMESPACE || inputNode.dataType == DataNodeType.STRING))
            {
                _emitDiscardTopOfStack(inputNode);

                Namespace constNamespace = (inputNode.dataType == DataNodeType.NAMESPACE)
                    ? inputNode.constant.namespaceValue
                    : new Namespace(inputNode.constant.stringValue);

                _emitPushXmlNamespaceConstant(constNamespace);
            }
            else if (inputNode.dataType != DataNodeType.NAMESPACE) {
                _emitTypeCoerceForTopOfStack(inputNode, DataNodeType.STRING);
                m_ilBuilder.emit(ILOp.newobj, KnownMembers.xmlNsCtorFromURI, 0);
            }

            m_ilBuilder.emit(ILOp.call, KnownMembers.setDxns, -1);
        }

        private void _visitEscapeXML(ref Instruction instr) {
            ref DataNode inputNode = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));
            _emitTypeCoerceForTopOfStack(inputNode, DataNodeType.STRING, useConvertStr: true);

            var method = (instr.opcode == ABCOp.esc_xelem) ? KnownMembers.escapeXmlElem : KnownMembers.escapeXmlAttr;
            m_ilBuilder.emit(ILOp.call, method, 0);
        }

        private void _visitHasNextNameValue(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode indexNode = ref m_compilation.getDataNode(stackPopIds[1]);

            _emitTypeCoerceForStackTop2(objectNode, indexNode, DataNodeType.OBJECT, DataNodeType.INT);

            if (instr.opcode == ABCOp.hasnext) {
                if (objectNode.isNotNull) {
                    m_ilBuilder.emit(ILOp.callvirt, KnownMembers.objHasNext, -1);
                }
                else {
                    // hasnext must return 0 if the object is null.
                    var tempvar = m_ilBuilder.acquireTempLocal(typeof(int));
                    var label1 = m_ilBuilder.createLabel();
                    var label2 = m_ilBuilder.createLabel();

                    m_ilBuilder.emit(ILOp.stloc, tempvar);
                    m_ilBuilder.emit(ILOp.dup);
                    m_ilBuilder.emit(ILOp.brfalse, label1);
                    m_ilBuilder.emit(ILOp.ldloc, tempvar);
                    m_ilBuilder.emit(ILOp.callvirt, KnownMembers.objHasNext, -1);
                    m_ilBuilder.emit(ILOp.br, label2);

                    m_ilBuilder.markLabel(label1);
                    m_ilBuilder.emit(ILOp.pop);
                    m_ilBuilder.emit(ILOp.ldc_i4_0);
                    m_ilBuilder.markLabel(label2);

                    m_ilBuilder.releaseTempLocal(tempvar);
                }
            }
            else {
                m_ilBuilder.emit(
                    ILOp.callvirt, (instr.opcode == ABCOp.nextname) ? KnownMembers.objNextName : KnownMembers.objNextValue, -1);
            }
        }

        private void _visitHasnext2(ref Instruction instr) {
            var nodeIds = m_compilation.staticIntArrayPool.getSpan(instr.data.hasnext2.nodeIds);

            ref DataNode oldObject = ref m_compilation.getDataNode(nodeIds[0]);
            ref DataNode oldIndex = ref m_compilation.getDataNode(nodeIds[1]);
            ref DataNode newObject = ref m_compilation.getDataNode(nodeIds[2]);
            ref DataNode newIndex = ref m_compilation.getDataNode(nodeIds[3]);

            var objLocal = _getLocalVarForNode(newObject);
            var indLocal = _getLocalVarForNode(newIndex);

            bool oldObjHasLocal = _tryGetLocalVarForNode(oldObject, out var oldObjLocal);
            bool oldIndHasLocal = _tryGetLocalVarForNode(oldIndex, out var oldIndLocal);

            if (!oldObjHasLocal || oldObjLocal != objLocal) {
                if (oldObjHasLocal)
                    m_ilBuilder.emit(ILOp.ldloc, oldObjLocal);
                else
                    _emitLoadScopeOrLocalNode(oldObject);

                _emitTypeCoerceForTopOfStack(oldObject, DataNodeType.OBJECT);
                m_ilBuilder.emit(ILOp.stloc, objLocal);
            }

            if (!oldIndHasLocal || oldIndLocal != indLocal) {
                if (oldIndHasLocal)
                    m_ilBuilder.emit(ILOp.ldloc, oldIndLocal);
                else
                    _emitLoadScopeOrLocalNode(oldIndex);

                _emitTypeCoerceForTopOfStack(oldIndex, DataNodeType.INT);
                m_ilBuilder.emit(ILOp.stloc, indLocal);
            }

            m_ilBuilder.emit(ILOp.ldloca, objLocal);
            m_ilBuilder.emit(ILOp.ldloca, indLocal);
            m_ilBuilder.emit(ILOp.call, KnownMembers.hasnext2, -1);

            _syncLocalWriteWithCatchVars(instr, newObject, objLocal);
            _syncLocalWriteWithCatchVars(instr, newIndex, indLocal);
        }

        private void _visitCheckFilter(ref Instruction instr) {
            ref DataNode inputNode = ref m_compilation.getDataNode(instr.data.checkFilter.stackNodeId);

            if (inputNode.dataType == DataNodeType.OBJECT
                && ClassTagSet.xmlOrXmlList.contains(inputNode.constant.classValue.tag))
            {
                return;
            }

            if (inputNode.isNotPushed) {
                // We need to force push the node if it has been marked as no-push.
                _emitPushConstantNode(inputNode, ignoreNoPush: true);
                _emitTypeCoerceForTopOfStack(inputNode, DataNodeType.OBJECT, isForcePushed: true);
            }
            else {
                m_ilBuilder.emit(ILOp.dup);
                _emitTypeCoerceForTopOfStack(inputNode, DataNodeType.OBJECT);
            }

            m_ilBuilder.emit(ILOp.call, KnownMembers.objCheckFilter, -1);
        }

        private void _visitNewActivation(ref Instruction instr) {
            var klass = m_compilation.getClassForActivation()!;
            using var lockedContext = m_compilation.getContext();
            m_ilBuilder.emit(ILOp.newobj, lockedContext.value.getEntityHandleForCtor(klass), 1);
        }

        private void _visitNewCatch(ref Instruction instr) {
            var klass = m_compilation.getClassForCatchScope(instr.data.newCatch.excInfoId);
            using var lockedContext = m_compilation.getContext();
            m_ilBuilder.emit(ILOp.newobj, lockedContext.value.getEntityHandleForCtor(klass), 1);
        }

        private void _visitIn(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            ref DataNode nameNode = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[1]);

            ILBuilder.Local objLocal;

            var bindOpts = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC;
            Class? objClass = _getPushedClassOfNode(objectNode);

            if (objClass == null) {
                objLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny));
            }
            else {
                objLocal = m_ilBuilder.acquireTempLocal(typeof(ASObject));
                _emitTypeCoerceForTopOfStack(objectNode, DataNodeType.OBJECT);
            }
            m_ilBuilder.emit(ILOp.stloc, objLocal);

            _emitTypeCoerceForTopOfStack(nameNode, DataNodeType.STRING);

            ILBuilder.Local nameLocal;
            MethodInfo method;

            if (objClass != null && !objClass.isObjectClass && !ClassTagSet.xmlOrXmlList.contains(objClass.tag)) {
                // If the object cannot be an XML or XMLList then the default XML namespace will
                // never be used for the lookup, so we can use a public QName.
                nameLocal = m_ilBuilder.acquireTempLocal(typeof(QName));
                m_ilBuilder.emit(ILOp.call, KnownMembers.qnamePublicName, 0);
                m_ilBuilder.emit(ILOp.stloc, nameLocal);

                m_ilBuilder.emit(ILOp.ldloc, objLocal);
                m_ilBuilder.emit(ILOp.ldloca, nameLocal);

                method = KnownMembers.objHasPropertyQName;
            }
            else {
                // Use a namespace set containing only the public namespace instead of a QName so
                // that the default XML namespace will also be searched if the object is an XML or XMLList.
                nameLocal = m_ilBuilder.acquireTempLocal(typeof(string));
                m_ilBuilder.emit(ILOp.stloc, nameLocal);

                m_ilBuilder.emit((objClass == null) ? ILOp.ldloca : ILOp.ldloc, objLocal);
                m_ilBuilder.emit(ILOp.ldloc, nameLocal);

                using (var lockedContext = m_compilation.getContext()) {
                    m_ilBuilder.emit(ILOp.ldsfld, lockedContext.value.emitConstData.nsSetArrayFieldHandle);
                    m_ilBuilder.emit(ILOp.ldc_i4, lockedContext.value.getEmitConstDataIdForPublicNamespaceSet());
                    m_ilBuilder.emit(ILOp.ldelema, typeof(NamespaceSet));
                }

                method = (objClass == null) ? KnownMembers.anyHasPropertyNsSet : KnownMembers.objHasPropertyNsSet;
            }

            m_ilBuilder.releaseTempLocal(objLocal);
            m_ilBuilder.releaseTempLocal(nameLocal);

            m_ilBuilder.emit(ILOp.ldc_i4, (int)bindOpts);
            m_ilBuilder.emit((objClass == null) ? ILOp.call : ILOp.callvirt, method);
        }

        private void _visitApplyType(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            ref DataNode result = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (result.isConstant) {
                for (int i = stackPopIds.Length - 1; i >= 0; i--)
                    _emitDiscardTopOfStack(stackPopIds[i]);

                _emitPushConstantNode(result);
                return;
            }

            ref DataNode def = ref m_compilation.getDataNode(stackPopIds[0]);
            var argIds = stackPopIds.Slice(stackPopIds.Length - instr.data.applyType.argCount);

            ILBuilder.Local argsLocal = default;
            if (argIds.Length > 0)
                argsLocal = _emitCollectStackArgsIntoArray(argIds);

            _emitTypeCoerceForTopOfStack(def, DataNodeType.OBJECT);

            if (argIds.Length > 0) {
                m_ilBuilder.emit(ILOp.ldloc, argsLocal);
                m_ilBuilder.emit(ILOp.newobj, KnownMembers.roSpanOfAnyFromArray, 0);
                m_ilBuilder.releaseTempLocal(argsLocal);
            }
            else {
                m_ilBuilder.emit(ILOp.call, KnownMembers.roSpanOfAnyEmpty, 1);
            }

            m_ilBuilder.emit(ILOp.call, KnownMembers.applyType, -1);
        }

        private void _visitGetProperty(ref Instruction instr) {
            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.accessProperty.resolvedPropId);

            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode resultNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            var multiname = m_compilation.abcFile.resolveMultiname(instr.data.accessProperty.multinameId);
            _getRuntimeMultinameArgIds(stackPopIds.Slice(1), multiname, out int rtNsNodeId, out int rtNameNodeId);

            bool isSuper = instr.opcode == ABCOp.getsuper;

            if (resolvedProp.propKind == ResolvedPropertyKind.TRAIT) {
                Debug.Assert(rtNsNodeId == -1 || m_compilation.getDataNode(rtNsNodeId).isNotPushed);
                Debug.Assert(rtNameNodeId == -1 || m_compilation.getDataNode(rtNameNodeId).isNotPushed);

                if (resultNode.isConstant) {
                    _emitDiscardTopOfStack(objectNode);
                    _emitPushConstantNode(resultNode);
                }
                else {
                    _emitGetPropertyTrait((Trait)resolvedProp.propInfo!, objectNode, isSuper);
                }
            }
            else if (resolvedProp.propKind == ResolvedPropertyKind.INDEX) {
                ref DataNode nameNode = ref m_compilation.getDataNode(rtNameNodeId);
                Debug.Assert(rtNsNodeId == -1 || m_compilation.getDataNode(rtNsNodeId).isNotPushed);
                _emitGetPropertyIndex((IndexProperty)resolvedProp.propInfo!, objectNode, nameNode);
            }
            else if (resolvedProp.propKind == ResolvedPropertyKind.RUNTIME) {
                Debug.Assert(!objectNode.isNotPushed);
                _emitGetPropertyRuntime(objectNode, multiname, rtNsNodeId, rtNameNodeId);
            }
        }

        private void _visitSetProperty(ref Instruction instr) {
            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.accessProperty.resolvedPropId);

            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode valueNode = ref m_compilation.getDataNode(stackPopIds[^1]);

            var multiname = m_compilation.abcFile.resolveMultiname(instr.data.accessProperty.multinameId);
            _getRuntimeMultinameArgIds(stackPopIds.Slice(1), multiname, out int rtNsNodeId, out int rtNameNodeId);

            bool isSuper = instr.opcode == ABCOp.setsuper;

            if (resolvedProp.propKind == ResolvedPropertyKind.TRAIT
                || resolvedProp.propKind == ResolvedPropertyKind.TRAIT_RT_INVOKE)
            {
                Debug.Assert(rtNsNodeId == -1 || m_compilation.getDataNode(rtNsNodeId).isNotPushed);
                Debug.Assert(rtNameNodeId == -1 || m_compilation.getDataNode(rtNameNodeId).isNotPushed);

                var trait = (Trait)resolvedProp.propInfo!;
                bool isRtInvoke = resolvedProp.propKind == ResolvedPropertyKind.TRAIT_RT_INVOKE;

                _emitSetPropertyTrait(trait, objectNode, valueNode, isSuper, isRtInvoke);
            }
            else if (resolvedProp.propKind == ResolvedPropertyKind.INDEX) {
                ref DataNode nameNode = ref m_compilation.getDataNode(rtNameNodeId);
                Debug.Assert(rtNsNodeId == -1 || m_compilation.getDataNode(rtNsNodeId).isNotPushed);

                var indexProp = (IndexProperty)resolvedProp.propInfo!;
                Span<int> argIds = stackalloc int[] {rtNameNodeId, valueNode.id};

                _emitCallToMethod(indexProp.setMethod!, _getPushedClassOfNode(objectNode), argIds, noReturn: true);
            }
            else if (resolvedProp.propKind == ResolvedPropertyKind.RUNTIME) {
                Debug.Assert(!objectNode.isNotPushed);

                var valueLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny));
                _emitTypeCoerceForTopOfStack(valueNode, DataNodeType.ANY);
                m_ilBuilder.emit(ILOp.stloc, valueLocal);

                _emitPrepareRuntimeBinding(
                    multiname,
                    rtNsNodeId,
                    rtNameNodeId,
                    _getPushedClassOfNode(objectNode),
                    isOnRuntimeScopeStack: false,
                    out RuntimeBindingKind bindingKind
                );

                m_ilBuilder.emit(ILOp.ldloc, valueLocal);
                m_ilBuilder.releaseTempLocal(valueLocal);

                bool isObjAny = isAnyOrUndefined(_getPushedTypeOfNode(objectNode));

                MethodInfo method = (bindingKind, isObjAny) switch {
                    (RuntimeBindingKind.QNAME, false) => KnownMembers.objSetPropertyQName,
                    (RuntimeBindingKind.QNAME, true) => KnownMembers.anySetPropertyQName,
                    (RuntimeBindingKind.MULTINAME, false) => KnownMembers.objSetPropertyNsSet,
                    (RuntimeBindingKind.MULTINAME, true) => KnownMembers.anySetPropertyNsSet,
                    (RuntimeBindingKind.KEY, false) => KnownMembers.objSetPropertyKey,
                    (RuntimeBindingKind.KEY, true) => KnownMembers.anySetPropertyKey,
                    (RuntimeBindingKind.KEY_MULTINAME, false) => KnownMembers.objSetPropertyKeyNsSet,
                    (RuntimeBindingKind.KEY_MULTINAME, true) => KnownMembers.anySetPropertyKeyNsSet,
                };

                var bindOpts =
                    BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | _getBindingOptionsForMultiname(multiname);

                m_ilBuilder.emit(ILOp.ldc_i4, (int)bindOpts);
                m_ilBuilder.emit(isObjAny ? ILOp.call : ILOp.callvirt, method);
            }
        }

        private void _visitDeleteProperty(ref Instruction instr) {
            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.accessProperty.resolvedPropId);

            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);

            var multiname = m_compilation.abcFile.resolveMultiname(instr.data.accessProperty.multinameId);
            _getRuntimeMultinameArgIds(stackPopIds.Slice(1), multiname, out int rtNsNodeId, out int rtNameNodeId);

            Debug.Assert(resolvedProp.propKind != ResolvedPropertyKind.TRAIT);

            if (resolvedProp.propKind == ResolvedPropertyKind.INDEX) {
                ref DataNode nameNode = ref m_compilation.getDataNode(rtNameNodeId);
                Debug.Assert(rtNsNodeId == -1 || m_compilation.getDataNode(rtNsNodeId).isNotPushed);

                var indexProp = (IndexProperty)resolvedProp.propInfo!;
                Span<int> argIds = stackalloc int[] {rtNameNodeId};

                _emitCallToMethod(indexProp.deleteMethod!, _getPushedClassOfNode(objectNode), argIds);
            }
            else if (resolvedProp.propKind == ResolvedPropertyKind.RUNTIME) {
                Debug.Assert(!objectNode.isNotPushed);

                _emitPrepareRuntimeBinding(
                    multiname,
                    rtNsNodeId,
                    rtNameNodeId,
                    _getPushedClassOfNode(objectNode),
                    isOnRuntimeScopeStack: false,
                    out RuntimeBindingKind bindingKind
                );

                bool isObjAny = isAnyOrUndefined(objectNode.dataType);

                MethodInfo method = (bindingKind, isObjAny) switch {
                    (RuntimeBindingKind.QNAME, false) => KnownMembers.objDelPropertyQName,
                    (RuntimeBindingKind.QNAME, true) => KnownMembers.anyDelPropertyQName,
                    (RuntimeBindingKind.MULTINAME, false) => KnownMembers.objDelPropertyNsSet,
                    (RuntimeBindingKind.MULTINAME, true) => KnownMembers.anyDelPropertyNsSet,
                    (RuntimeBindingKind.KEY, false) => KnownMembers.objDelPropertyKey,
                    (RuntimeBindingKind.KEY, true) => KnownMembers.anyDelPropertyKey,
                    (RuntimeBindingKind.KEY_MULTINAME, false) => KnownMembers.objDelPropertyKeyNsSet,
                    (RuntimeBindingKind.KEY_MULTINAME, true) => KnownMembers.anyDelPropertyKeyNsSet,
                };

                var bindOpts = BindOptions.SEARCH_DYNAMIC | _getBindingOptionsForMultiname(multiname);

                m_ilBuilder.emit(ILOp.ldc_i4, (int)bindOpts);
                m_ilBuilder.emit(isObjAny ? ILOp.call : ILOp.callvirt, method);
            }
        }

        private void _visitGetSlot(ref Instruction instr) {
            ref DataNode objectNode = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));
            ref DataNode resultNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.getSetSlot.resolvedPropId);

            if (resultNode.isConstant) {
                _emitDiscardTopOfStack(objectNode);
                _emitPushConstantNode(resultNode);
            }
            else {
                _emitGetPropertyTrait((Trait)resolvedProp.propInfo!, objectNode, isSuper: false);
            }
        }

        private void _visitSetSlot(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);

            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode valueNode = ref m_compilation.getDataNode(stackPopIds[1]);

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.getSetSlot.resolvedPropId);
            bool isRtInvoke = resolvedProp.propKind == ResolvedPropertyKind.TRAIT_RT_INVOKE;

            _emitSetPropertyTrait((Trait)resolvedProp.propInfo!, objectNode, valueNode, isSuper: false, isRtInvoke);
        }

        private void _visitGetGlobalScope(ref Instruction instr) {
            ref DataNode result = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            Debug.Assert(result.dataType == DataNodeType.GLOBAL);
            _emitPushConstantNode(result);
        }

        private void _visitGetGlobalSlot(ref Instruction instr) {
            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.getSetSlot.resolvedPropId);
            var trait = (Trait)resolvedProp.propInfo!;

            Debug.Assert(trait.isStatic);

            ref DataNode result = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (result.isConstant) {
                _emitPushConstantNode(result);
                return;
            }

            DataNode dummyObject = default;
            dummyObject.id = -1;
            dummyObject.slot = new DataNodeSlot(DataNodeSlotKind.STACK, -1);
            dummyObject.dataType = DataNodeType.GLOBAL;
            dummyObject.flags = DataNodeFlags.CONSTANT | DataNodeFlags.NO_PUSH;

            _emitGetPropertyTrait(trait, dummyObject, isSuper: false);
        }

        private void _visitSetGlobalSlot(ref Instruction instr) {
            ref DataNode valueNode = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.getSetSlot.resolvedPropId);

            var trait = (Trait)resolvedProp.propInfo!;
            bool isRtInvoke = resolvedProp.propKind == ResolvedPropertyKind.TRAIT_RT_INVOKE;

            Debug.Assert(trait.isStatic);

            DataNode dummyObject = new DataNode {
                id = -1,
                slot = new DataNodeSlot(DataNodeSlotKind.STACK, -1),
                dataType = DataNodeType.GLOBAL,
                flags = DataNodeFlags.CONSTANT | DataNodeFlags.NO_PUSH
            };

            _emitSetPropertyTrait(trait, dummyObject, valueNode, isSuper: false, isRtInvoke);
        }

        private void _visitCallOrConstructProp(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            var argNodeIds = stackPopIds.Slice(stackPopIds.Length - instr.data.callProperty.argCount);
            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.callProperty.resolvedPropId);

            var multiname = m_compilation.abcFile.resolveMultiname(instr.data.callProperty.multinameId);
            _getRuntimeMultinameArgIds(stackPopIds.Slice(1), multiname, out int rtNsNodeId, out int rtNameNodeId);

            bool isConstruct = false, isSuper = false, isLex = false;

            switch (instr.opcode) {
                case ABCOp.callproplex:
                    isLex = true;
                    break;
                case ABCOp.callsuper:
                case ABCOp.callsupervoid:
                    isSuper = true;
                    break;
                case ABCOp.constructprop:
                    isConstruct = true;
                    break;
            }

            int resultId = instr.stackPushedNodeId;
            if (resultId != -1 && m_compilation.getDataNode(resultId).isNotPushed)
                resultId = -1;

            if (resolvedProp.propKind == ResolvedPropertyKind.TRAIT
                || resolvedProp.propKind == ResolvedPropertyKind.TRAIT_RT_INVOKE)
            {
                Debug.Assert(rtNsNodeId == -1 || m_compilation.getDataNode(rtNsNodeId).isNotPushed);
                Debug.Assert(rtNameNodeId == -1 || m_compilation.getDataNode(rtNameNodeId).isNotPushed);

                var trait = (Trait)resolvedProp.propInfo!;

                if (resolvedProp.propKind == ResolvedPropertyKind.TRAIT)
                    _emitCallOrConstructTrait(trait, objectNode.id, argNodeIds, isConstruct, isSuper, resultId);
                else
                    _emitCallOrConstructTraitAtRuntime(trait, objectNode.id, argNodeIds, isConstruct, isLex, resultId == -1);
            }
            else if (resolvedProp.propKind == ResolvedPropertyKind.INTRINSIC) {
                Debug.Assert(rtNsNodeId == -1 || m_compilation.getDataNode(rtNsNodeId).isNotPushed);
                Debug.Assert(rtNameNodeId == -1 || m_compilation.getDataNode(rtNameNodeId).isNotPushed);

                var intrinsic = (Intrinsic)resolvedProp.propInfo!;
                _emitInvokeIntrinsic(intrinsic, objectNode.id, argNodeIds, isConstruct, resultId);
            }
            else if (resolvedProp.propKind == ResolvedPropertyKind.INDEX) {
                Debug.Assert(rtNsNodeId == -1 || m_compilation.getDataNode(rtNsNodeId).isNotPushed);
                ref DataNode nameNode = ref m_compilation.getDataNode(rtNameNodeId);

                var indexProp = (IndexProperty)resolvedProp.propInfo!;
                _emitCallOrConstructIndexProp(
                    indexProp, objectNode, nameNode, argNodeIds, isConstruct, isLex, noReturn: resultId == -1);
            }
            else if (resolvedProp.propKind == ResolvedPropertyKind.RUNTIME) {
                Debug.Assert(!objectNode.isNotPushed);

                ILBuilder.Local argsLocal = default;
                if (argNodeIds.Length > 0)
                    argsLocal = _emitCollectStackArgsIntoArray(argNodeIds);

                _emitPrepareRuntimeBinding(
                    multiname,
                    rtNsNodeId,
                    rtNameNodeId,
                    _getPushedClassOfNode(objectNode),
                    isOnRuntimeScopeStack: false,
                    out RuntimeBindingKind bindingKind
                );

                bool isObjAny = isAnyOrUndefined(objectNode.dataType);
                MethodInfo method = getRuntimeBindMethod(bindingKind, isObjAny, isConstruct);

                var bindOpts =
                    BindOptions.SEARCH_TRAITS
                    | BindOptions.SEARCH_DYNAMIC
                    | BindOptions.SEARCH_PROTOTYPE
                    | _getBindingOptionsForMultiname(multiname);

                if (isLex)
                    bindOpts |= BindOptions.NULL_RECEIVER;

                if (argNodeIds.Length > 0) {
                    m_ilBuilder.emit(ILOp.ldloc, argsLocal);
                    m_ilBuilder.emit(ILOp.newobj, KnownMembers.roSpanOfAnyFromArray, 0);
                    m_ilBuilder.releaseTempLocal(argsLocal);
                }
                else {
                    m_ilBuilder.emit(ILOp.call, KnownMembers.roSpanOfAnyEmpty, 1);
                }

                m_ilBuilder.emit(ILOp.ldc_i4, (int)bindOpts);
                m_ilBuilder.emit(isObjAny ? ILOp.call : ILOp.callvirt, method);

                if (resultId == -1)
                    m_ilBuilder.emit(ILOp.pop);
            }

            static MethodInfo getRuntimeBindMethod(RuntimeBindingKind bindingKind, bool isAny, bool isCons) {
                return (bindingKind, isCons, isAny) switch {
                    (RuntimeBindingKind.QNAME, false, false) => KnownMembers.objCallPropertyQName,
                    (RuntimeBindingKind.QNAME, false, true) => KnownMembers.anyCallPropertyQName,
                    (RuntimeBindingKind.QNAME, true, false) => KnownMembers.objConstructPropertyQName,
                    (RuntimeBindingKind.QNAME, true, true) => KnownMembers.anyConstructPropertyQName,

                    (RuntimeBindingKind.MULTINAME, false, false) => KnownMembers.objCallPropertyNsSet,
                    (RuntimeBindingKind.MULTINAME, false, true) => KnownMembers.anyCallPropertyNsSet,
                    (RuntimeBindingKind.MULTINAME, true, false) => KnownMembers.objConstructPropertyNsSet,
                    (RuntimeBindingKind.MULTINAME, true, true) => KnownMembers.anyConstructPropertyNsSet,

                    (RuntimeBindingKind.KEY, false, false) => KnownMembers.objCallPropertyKey,
                    (RuntimeBindingKind.KEY, false, true) => KnownMembers.anyCallPropertyKey,
                    (RuntimeBindingKind.KEY, true, false) => KnownMembers.objConstructPropertyKey,
                    (RuntimeBindingKind.KEY, true, true) => KnownMembers.anyConstructPropertyKey,

                    (RuntimeBindingKind.KEY_MULTINAME, false, false) => KnownMembers.objCallPropertyKeyNsSet,
                    (RuntimeBindingKind.KEY_MULTINAME, false, true) => KnownMembers.anyCallPropertyKeyNsSet,
                    (RuntimeBindingKind.KEY_MULTINAME, true, false) => KnownMembers.objConstructPropertyKeyNsSet,
                    (RuntimeBindingKind.KEY_MULTINAME, true, true) => KnownMembers.anyConstructPropertyKeyNsSet
                };
            }
        }

        private void _visitCallOrConstruct(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            var argIds = stackPopIds.Slice(stackPopIds.Length - instr.data.callOrConstruct.argCount);

            bool isConstruct = instr.opcode == ABCOp.construct;
            int receiverId = isConstruct ? -1 : stackPopIds[1];

            ref DataNode func = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode result = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            int resultId = instr.stackPushedNodeId;
            if (resultId != -1 && m_compilation.getDataNode(resultId).isNotPushed)
                resultId = -1;

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.callOrConstruct.resolvedPropId);

            if (resolvedProp.propKind == ResolvedPropertyKind.RUNTIME) {
                ILBuilder.Local argsLocal = default;
                if (argIds.Length > 0)
                    argsLocal = _emitCollectStackArgsIntoArray(argIds);

                ILBuilder.Local receiverLocal = default;
                if (receiverId != -1) {
                    receiverLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny));
                    _emitTypeCoerceForTopOfStack(receiverId, DataNodeType.ANY);
                    m_ilBuilder.emit(ILOp.stloc, receiverLocal);
                }

                MethodInfo method;

                if (func.dataType == DataNodeType.ANY) {
                    var funcLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny));
                    m_ilBuilder.emit(ILOp.stloc, funcLocal);
                    m_ilBuilder.emit(ILOp.ldloca, funcLocal);
                    m_ilBuilder.releaseTempLocal(funcLocal);

                    method = isConstruct ? KnownMembers.anyConstruct : KnownMembers.anyInvoke;
                }
                else {
                    method = isConstruct ? KnownMembers.objConstruct : KnownMembers.objInvoke;
                }

                if (receiverId != -1) {
                    m_ilBuilder.emit(ILOp.ldloc, receiverLocal);
                    m_ilBuilder.releaseTempLocal(receiverLocal);
                }

                if (argIds.Length > 0) {
                    m_ilBuilder.emit(ILOp.ldloc, argsLocal);
                    m_ilBuilder.emit(ILOp.newobj, KnownMembers.roSpanOfAnyFromArray, 0);
                    m_ilBuilder.releaseTempLocal(argsLocal);
                }
                else {
                    m_ilBuilder.emit(ILOp.call, KnownMembers.roSpanOfAnyEmpty, 1);
                }

                m_ilBuilder.emit((func.dataType == DataNodeType.ANY) ? ILOp.call : ILOp.callvirt, method);
                if (resultId == -1)
                    m_ilBuilder.emit(ILOp.pop);

                return;
            }

            if (resolvedProp.propKind == ResolvedPropertyKind.TRAIT
                || resolvedProp.propKind == ResolvedPropertyKind.TRAIT_RT_INVOKE)
            {
                var trait = (Trait)resolvedProp.propInfo!;

                if (resolvedProp.propKind == ResolvedPropertyKind.TRAIT) {
                    _emitCallOrConstructTrait(trait, receiverId, argIds, isConstruct, isSuper: false, resultId);
                }
                else {
                    _emitCallOrConstructTraitAtRuntime(
                        trait, receiverId, argIds, isConstruct, nullReceiver: false, noReturn: resultId == -1);
                }
            }
            else if (resolvedProp.propKind == ResolvedPropertyKind.INTRINSIC) {
                var intrinsic = (Intrinsic)resolvedProp.propInfo!;
                _emitInvokeIntrinsic(intrinsic, receiverId, argIds, isConstruct, resultId);
            }

            if (!func.isNotPushed) {
                ILBuilder.Local resultStash = default;
                if (resultId != -1)
                    resultStash = _emitStashTopOfStack(m_compilation.getDataNode(resultId), usePrePushType: true);

                m_ilBuilder.emit(ILOp.pop);

                if (resultId != -1)
                    _emitUnstash(resultStash);
            }
        }

        private void _visitCallMethodOrStatic(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            var argIds = stackPopIds.Slice(stackPopIds.Length - instr.data.callMethod.argCount);
            int receiverId = stackPopIds[0];

            int resultId = instr.stackPushedNodeId;
            if (resultId != -1 && m_compilation.getDataNode(resultId).isNotPushed)
                resultId = -1;

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.callMethod.resolvedPropId);
            var trait = (Trait)resolvedProp.propInfo!;

            if (resolvedProp.propKind == ResolvedPropertyKind.TRAIT) {
                _emitCallOrConstructTrait(trait, receiverId, argIds, isConstruct: false, isSuper: false, resultId);
            }
            else if (resolvedProp.propKind == ResolvedPropertyKind.TRAIT_RT_INVOKE) {
                _emitCallOrConstructTraitAtRuntime(
                    trait, receiverId, argIds, isConstruct: false, nullReceiver: false, noReturn: resultId == -1);
            }
        }

        private void _visitConstructSuper(ref Instruction instr) {
            Class parentClass = m_compilation.declaringClass!.parent!;

            int argCount = instr.data.constructSuper.argCount;
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);

            if (!checkArgCountValidAndEmitError())
                return;

            if (parentClass.isObjectClass) {
                // We need to special-case Object because its constructor is intrinsic.
                m_ilBuilder.emit(ILOp.call, KnownMembers.objectCtor);
            }
            else if (parentClass.constructor != null) {
                var ctor = parentClass.constructor;
                var argsOnStack = stackPopIds.Slice(stackPopIds.Length - argCount);
                _emitPrepareMethodCallArguments(ctor.getParameters().asSpan(), ctor.hasRest, argsOnStack, null, null);

                using var lockedContext = m_compilation.getContext();
                m_ilBuilder.emit(ILOp.call, lockedContext.value.getEntityHandleForCtor(parentClass));
            }
            else {
                var error = ErrorHelper.createErrorObject(ErrorCode.CLASS_CANNOT_BE_INSTANTIATED, parentClass.name.ToString());
                ILEmitHelper.emitThrowError(m_ilBuilder, error.GetType(), (ErrorCode)error.errorID, error.message);
            }

            bool checkArgCountValidAndEmitError() {
                ClassConstructor? ctor = parentClass.constructor;

                if (ctor == null && !parentClass.isObjectClass) {
                    // The parent constructor does not exist and a different kind of error will be emitted.
                    // No checking the argument count in this case.
                    return true;
                }

                var (requiredParamCount, paramCount) = (ctor != null) ? (ctor.requiredParamCount, ctor.paramCount) : (0, 0);

                if (argCount >= requiredParamCount && (argCount <= paramCount || (ctor != null && ctor.hasRest)))
                    return true;

                var error = ErrorHelper.createErrorObject(
                    ErrorCode.ARG_COUNT_MISMATCH,
                    parentClass.name.ToString(),
                    requiredParamCount,
                    argCount
                );
                ILEmitHelper.emitThrowError(m_ilBuilder, error.GetType(), (ErrorCode)error.errorID, error.message);

                return false;
            }
        }

        private void _visitGetDescendants(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);

            var multiname = m_compilation.abcFile.resolveMultiname(instr.data.getDescendants.multinameId);
            _getRuntimeMultinameArgIds(stackPopIds.Slice(1), multiname, out int rtNsNodeId, out int rtNameNodeId);

            _emitPrepareRuntimeBinding(
                multiname,
                rtNsNodeId,
                rtNameNodeId,
                _getPushedClassOfNode(objectNode),
                isOnRuntimeScopeStack: false,
                out RuntimeBindingKind bindingKind
            );

            bool isObjAny = isAnyOrUndefined(objectNode.dataType);

            MethodInfo method = (bindingKind, isObjAny) switch {
                (RuntimeBindingKind.QNAME, false) => KnownMembers.objGetDescQName,
                (RuntimeBindingKind.QNAME, true) => KnownMembers.anyGetDescQName,
                (RuntimeBindingKind.MULTINAME, false) => KnownMembers.objGetDescNsSet,
                (RuntimeBindingKind.MULTINAME, true) => KnownMembers.anyGetDescNsSet,
                (RuntimeBindingKind.KEY, false) => KnownMembers.objGetDescKey,
                (RuntimeBindingKind.KEY, true) => KnownMembers.anyGetDescKey,
                (RuntimeBindingKind.KEY_MULTINAME, false) => KnownMembers.objGetDescKeyNsSet,
                (RuntimeBindingKind.KEY_MULTINAME, true) => KnownMembers.anyGetDescKeyNsSet,
            };

            var bindOpts = BindOptions.SEARCH_DYNAMIC | _getBindingOptionsForMultiname(multiname);

            m_ilBuilder.emit(ILOp.ldc_i4, (int)bindOpts);
            m_ilBuilder.emit(isObjAny ? ILOp.call : ILOp.callvirt, method);
        }

        private void _visitFindProperty(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);

            var multiname = m_compilation.abcFile.resolveMultiname(instr.data.findProperty.multinameId);
            _getRuntimeMultinameArgIds(stackPopIds, multiname, out int rtNsNodeId, out int rtNameNodeId);

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.findProperty.resolvedPropId);
            var scopeRef = instr.data.findProperty.scopeRef;

            if (scopeRef.isNull) {
                Debug.Assert(m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_RUNTIME_SCOPE_STACK));

                _emitPrepareRuntimeBinding(
                    multiname,
                    rtNsNodeId,
                    rtNameNodeId,
                    objectType: null,
                    isOnRuntimeScopeStack: true,
                    out RuntimeBindingKind bindingKind
                );

                MethodInfo method = bindingKind switch {
                    RuntimeBindingKind.QNAME => KnownMembers.rtScopeStackFindQname,
                    RuntimeBindingKind.MULTINAME => KnownMembers.rtScopeStackFindNsSet,
                    RuntimeBindingKind.KEY => KnownMembers.rtScopeStackFindKey,
                    RuntimeBindingKind.KEY_MULTINAME => KnownMembers.rtScopeStackFindKeyNsSet
                };

                m_ilBuilder.emit(ILOp.ldc_i4_0);
                m_ilBuilder.emit(ILOp.ldc_i4, multiname.isAttributeName ? 1 : 0);
                m_ilBuilder.emit(ILOp.ldc_i4, (instr.opcode == ABCOp.findpropstrict) ? 1 : 0);
                m_ilBuilder.emit(ILOp.call, method);
            }
            else {
                Debug.Assert(resolvedProp.propKind == ResolvedPropertyKind.TRAIT);
                Debug.Assert(rtNsNodeId == -1 || m_compilation.getDataNode(rtNsNodeId).isNotPushed);
                Debug.Assert(rtNameNodeId == -1 || m_compilation.getDataNode(rtNameNodeId).isNotPushed);

                ref DataNode result = ref m_compilation.getDataNode(instr.stackPushedNodeId);

                if (result.isNotPushed)
                    return;

                if (result.isConstant)
                    _emitPushConstantNode(result);
                else if (scopeRef.isLocal)
                    _emitLoadScopeOrLocalNode(m_compilation.getDataNode(scopeRef.idOrCaptureHeight));
                else
                    _emitPushCapturedScopeItem(scopeRef.idOrCaptureHeight);
            }
        }

        private void _visitGetLex(ref Instruction instr) {
            var multiname = m_compilation.abcFile.resolveMultiname(instr.data.findProperty.multinameId);

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.findProperty.resolvedPropId);
            var scopeRef = instr.data.findProperty.scopeRef;

            if (scopeRef.isNull) {
                Debug.Assert(m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_RUNTIME_SCOPE_STACK));

                _emitPrepareRuntimeBinding(
                    multiname,
                    rtNsNodeId: -1,
                    rtNameNodeId: -1,
                    objectType: null,
                    isOnRuntimeScopeStack: true,
                    out RuntimeBindingKind bindingKind
                );

                MethodInfo method = bindingKind switch {
                    RuntimeBindingKind.QNAME => KnownMembers.rtScopeStackGetQname,
                    RuntimeBindingKind.MULTINAME => KnownMembers.rtScopeStackGetNsSet,
                    RuntimeBindingKind.KEY => KnownMembers.rtScopeStackGetKey,
                    RuntimeBindingKind.KEY_MULTINAME => KnownMembers.rtScopeStackGetKeyNsSet
                };

                m_ilBuilder.emit(ILOp.ldc_i4_0);
                m_ilBuilder.emit(ILOp.ldc_i4, multiname.isAttributeName ? 1 : 0);
                m_ilBuilder.emit(ILOp.ldc_i4, 1);
                m_ilBuilder.emit(ILOp.call, method);
            }
            else {
                ref DataNode result = ref m_compilation.getDataNode(instr.stackPushedNodeId);
                if (result.isNotPushed)
                    return;

                if (result.isConstant) {
                    _emitPushConstantNode(result);
                    return;
                }

                Trait? trait = null;
                bool mustPushObject;

                if (resolvedProp.propKind == ResolvedPropertyKind.TRAIT) {
                    trait = (Trait)resolvedProp.propInfo!;
                    mustPushObject = !trait.isStatic;
                }
                else {
                    mustPushObject = true;
                }

                DataNode dummyNode = default;
                dummyNode.id = -1;
                dummyNode.slot = new DataNodeSlot(DataNodeSlotKind.SCOPE, -1);

                if (scopeRef.isLocal) {
                    ref DataNode scopeNode = ref m_compilation.getDataNode(scopeRef.idOrCaptureHeight);

                    dummyNode.dataType = scopeNode.dataType;
                    dummyNode.isConstant = scopeNode.isConstant;
                    dummyNode.constant = scopeNode.constant;

                    if (mustPushObject)
                        _emitLoadScopeOrLocalNode(scopeNode);
                }
                else {
                    ref readonly CapturedScopeItem capturedItem =
                        ref m_compilation.getCapturedScopeItems()[scopeRef.idOrCaptureHeight];

                    dummyNode.dataType = capturedItem.dataType;
                    dummyNode.isConstant = isConstantType(capturedItem.dataType);

                    if (capturedItem.objClass != null)
                        dummyNode.constant = new DataNodeConstant(capturedItem.objClass);

                    if (mustPushObject)
                        _emitPushCapturedScopeItem(scopeRef.idOrCaptureHeight);
                }

                if (trait != null) {
                    _emitGetPropertyTrait(trait, dummyNode, isSuper: false);
                }
                else {
                    Debug.Assert(resolvedProp.propKind == ResolvedPropertyKind.RUNTIME);
                    _emitGetPropertyRuntime(dummyNode, multiname, -1, -1);
                }
            }
        }

        private void _visitNewClass(ref Instruction instr) {
            ref DataNode baseClass = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));
            _emitDiscardTopOfStack(baseClass);

            using (var lockedContext = m_compilation.getContext()) {
                ScriptClass klass = lockedContext.value.getClassFromClassInfo(instr.data.newClass.classInfoId);
                CapturedScope? classCapturedScope = lockedContext.value.getClassCapturedScope(klass);

                if (classCapturedScope != null) {
                    EntityHandle classCapturedScopeField = lockedContext.value.getClassCapturedScopeFieldHandle(klass);
                    Span<int> localScopeNodeIds = m_compilation.staticIntArrayPool.getSpan(instr.data.newClass.capturedScopeNodeIds);

                    var capturedScopeLocal = _emitCaptureCurrentScope(classCapturedScope, localScopeNodeIds);

                    m_ilBuilder.emit(ILOp.ldloc, capturedScopeLocal);
                    m_ilBuilder.emit(ILOp.stsfld, classCapturedScopeField);
                    m_ilBuilder.releaseTempLocal(capturedScopeLocal);
                }

                MethodTrait? staticInit = lockedContext.value.getClassStaticInitMethod(klass);
                if (staticInit != null)
                    _emitCallToMethod(staticInit, null, ReadOnlySpan<int>.Empty, noReturn: true);
            }

            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            _emitPushConstantNode(pushed);
        }

        private void _visitNewFunction(ref Instruction instr) {
            ScriptMethod func;
            CapturedScope funcCapturedScope;

            using (var lockedContext = m_compilation.getContext()) {
                func = (ScriptMethod)lockedContext.value.getMethodOrCtorForMethodInfo(instr.data.newFunction.methodInfoId)!;
                funcCapturedScope = lockedContext.value.getFunctionCapturedScope(func)!;
            }

            Span<int> localScopeNodeIds = m_compilation.staticIntArrayPool.getSpan(instr.data.newFunction.capturedScopeNodeIds);
            var capturedScopeLocal = _emitCaptureCurrentScope(funcCapturedScope, localScopeNodeIds);

            _emitPushTraitConstant(func);
            m_ilBuilder.emit(ILOp.castclass, typeof(MethodTrait));
            m_ilBuilder.emit(ILOp.ldloc, capturedScopeLocal);
            m_ilBuilder.emit(ILOp.call, KnownMembers.methodTraitCreateFunctionClosure, -1);

            m_ilBuilder.releaseTempLocal(capturedScopeLocal);
        }

        private void _visitIfTrueFalse(ref Instruction instr) {
            ref DataNode inputNode = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));

            ref BasicBlock thisBlock = ref m_compilation.getBasicBlockOfInstruction(instr);
            var exitBlockIds = m_compilation.staticIntArrayPool.getSpan(thisBlock.exitBlockIds);

            ref BasicBlock trueBlock = ref m_compilation.getBasicBlock(exitBlockIds[0]);
            ref BasicBlock falseBlock = ref m_compilation.getBasicBlock(exitBlockIds[1]);

            if (instr.data.ifTrueFalse.isConstant) {
                _emitDiscardTopOfStack(inputNode);

                if (instr.data.ifTrueFalse.constResult)
                    _emitJumpFromBasicBlock(thisBlock, trueBlock);
                else
                    _emitJumpFromBasicBlock(thisBlock, falseBlock);

                return;
            }

            Class? inputClass = _getPushedClassOfNode(inputNode);

            if (inputClass == null || inputClass.isObjectClass
                || inputClass.tag == ClassTag.NUMBER || inputClass.tag == ClassTag.STRING)
            {
                _emitTypeCoerceForTopOfStack(inputNode, DataNodeType.BOOL);
            }

            TwoWayBranchEmitInfo emitInfo = _getTwoWayBranchEmitInfo(thisBlock, trueBlock, falseBlock);

            if (emitInfo.trueIsFallThrough)
                m_ilBuilder.emit((instr.opcode == ABCOp.iftrue) ? ILOp.brfalse : ILOp.brtrue, emitInfo.falseLabel);
            else
                m_ilBuilder.emit((instr.opcode == ABCOp.iftrue) ? ILOp.brtrue : ILOp.brfalse, emitInfo.trueLabel);

            _finishTwoWayConditionalBranch(thisBlock, emitInfo);
        }

        private void _visitBinaryCompareBranch(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);

            ref DataNode leftInputNode = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode rightInputNode = ref m_compilation.getDataNode(stackPopIds[1]);

            ref BasicBlock thisBlock = ref m_compilation.getBasicBlockOfInstruction(instr);
            var exitBlockIds = m_compilation.staticIntArrayPool.getSpan(thisBlock.exitBlockIds);

            ref BasicBlock trueBlock = ref m_compilation.getBasicBlock(exitBlockIds[0]);
            ref BasicBlock falseBlock = ref m_compilation.getBasicBlock(exitBlockIds[1]);

            if (instr.data.compareBranch.compareType == ComparisonType.CONSTANT) {
                _emitDiscardTopOfStack(rightInputNode);
                _emitDiscardTopOfStack(leftInputNode);

                if (instr.data.compareBranch.constResult)
                    _emitJumpFromBasicBlock(thisBlock, trueBlock);
                else
                    _emitJumpFromBasicBlock(thisBlock, falseBlock);

                return;
            }

            TwoWayBranchEmitInfo branchEmitInfo = _getTwoWayBranchEmitInfo(thisBlock, trueBlock, falseBlock);

            ComparisonType cmpType = instr.data.compareBranch.compareType;

            if (cmpType == ComparisonType.STR_CHARAT_L || cmpType == ComparisonType.STR_CHARAT_R) {
                _emitStringCharAtIntrinsicCompare(instr, leftInputNode, rightInputNode, branchEmitInfo);
                _finishTwoWayConditionalBranch(thisBlock, branchEmitInfo);
                return;
            }

            ILOp ilOp = 0, invIlOp = 0;

            switch (cmpType) {
                case ComparisonType.INT:
                    _emitTypeCoerceForStackTop2(leftInputNode, rightInputNode, DataNodeType.INT, DataNodeType.INT);
                    getILOpForIntCompare(instr.opcode, isUnsigned: false, out ilOp, out invIlOp);
                    break;

                case ComparisonType.UINT:
                    _emitTypeCoerceForStackTop2(leftInputNode, rightInputNode, DataNodeType.UINT, DataNodeType.UINT);
                    getILOpForIntCompare(instr.opcode, isUnsigned: true, out ilOp, out invIlOp);
                    break;

                case ComparisonType.BOOL:
                    _emitTypeCoerceForStackTop2(leftInputNode, rightInputNode, DataNodeType.BOOL, DataNodeType.BOOL);
                    getILOpForIntCompare(instr.opcode, isUnsigned: true, out ilOp, out invIlOp);
                    break;

                case ComparisonType.NUMBER:
                    _emitTypeCoerceForStackTop2(leftInputNode, rightInputNode, DataNodeType.NUMBER, DataNodeType.NUMBER);
                    getILOpForFloatCompare(instr.opcode, out ilOp, out invIlOp);
                    break;

                case ComparisonType.STRING:
                    _emitTypeCoerceForStackTop2(leftInputNode, rightInputNode, DataNodeType.STRING, DataNodeType.STRING);
                    m_ilBuilder.emit(ILOp.call, getMethodForStrCompare(instr.opcode));
                    (ilOp, invIlOp) = isNegativeCompare(instr.opcode) ? (ILOp.brfalse, ILOp.brtrue) : (ILOp.brtrue, ILOp.brfalse);
                    break;

                case ComparisonType.NAMESPACE:
                    Debug.Assert(leftInputNode.dataType == DataNodeType.NAMESPACE && rightInputNode.dataType == DataNodeType.NAMESPACE);
                    m_ilBuilder.emit(ILOp.call, KnownMembers.xmlNamespaceEquals, -1);
                    (ilOp, invIlOp) = isNegativeCompare(instr.opcode) ? (ILOp.brfalse, ILOp.brtrue) : (ILOp.brtrue, ILOp.brfalse);
                    break;

                case ComparisonType.QNAME:
                    Debug.Assert(leftInputNode.dataType == DataNodeType.QNAME && rightInputNode.dataType == DataNodeType.QNAME);
                    m_ilBuilder.emit(ILOp.call, KnownMembers.xmlQnameEquals, -1);
                    (ilOp, invIlOp) = isNegativeCompare(instr.opcode) ? (ILOp.brfalse, ILOp.brtrue) : (ILOp.brtrue, ILOp.brfalse);
                    break;

                case ComparisonType.OBJECT:
                    _emitTypeCoerceForStackTop2(leftInputNode, rightInputNode, DataNodeType.OBJECT, DataNodeType.OBJECT);
                    m_ilBuilder.emit(ILOp.call, getMethodForObjectCompare(instr.opcode), -1);
                    (ilOp, invIlOp) = isNegativeCompare(instr.opcode) ? (ILOp.brfalse, ILOp.brtrue) : (ILOp.brtrue, ILOp.brfalse);
                    break;

                case ComparisonType.ANY:
                    _emitTypeCoerceForStackTop2(leftInputNode, rightInputNode, DataNodeType.ANY, DataNodeType.ANY);
                    m_ilBuilder.emit(ILOp.call, getMethodForAnyCompare(instr.opcode), -1);
                    (ilOp, invIlOp) = isNegativeCompare(instr.opcode) ? (ILOp.brfalse, ILOp.brtrue) : (ILOp.brtrue, ILOp.brfalse);
                    break;

                case ComparisonType.OBJ_REF:
                    (ilOp, invIlOp) = isNegativeCompare(instr.opcode) ? (ILOp.bne_un, ILOp.beq) : (ILOp.beq, ILOp.bne_un);
                    break;

                case ComparisonType.INT_ZERO_L:
                case ComparisonType.OBJ_NULL_L:
                {
                    if (instr.opcode == ABCOp.ifeq || instr.opcode == ABCOp.ifstricteq)
                        (ilOp, invIlOp) = !leftInputNode.isNotPushed ? (ILOp.beq, ILOp.bne_un) : (ILOp.brfalse, ILOp.brtrue);
                    else
                        (ilOp, invIlOp) = !leftInputNode.isNotPushed ? (ILOp.bne_un, ILOp.beq) : (ILOp.brtrue, ILOp.brfalse);
                    break;
                }

                case ComparisonType.INT_ZERO_R:
                case ComparisonType.OBJ_NULL_R:
                {
                    if (instr.opcode == ABCOp.ifeq || instr.opcode == ABCOp.ifstricteq)
                        (ilOp, invIlOp) = !rightInputNode.isNotPushed ? (ILOp.beq, ILOp.bne_un) : (ILOp.brfalse, ILOp.brtrue);
                    else
                        (ilOp, invIlOp) = !rightInputNode.isNotPushed ? (ILOp.bne_un, ILOp.beq) : (ILOp.brtrue, ILOp.brfalse);
                    break;
                }

                case ComparisonType.BOOL_CONST_L:
                case ComparisonType.BOOL_CONST_R:
                {
                    ref DataNode constNode = ref ((cmpType == ComparisonType.BOOL_CONST_L) ? ref leftInputNode : ref rightInputNode);

                    if (!constNode.isNotPushed)
                        (ilOp, invIlOp) = (ILOp.beq, ILOp.bne_un);
                    else if (constNode.constant.boolValue)
                        (ilOp, invIlOp) = (ILOp.brtrue, ILOp.brfalse);
                    else
                        (ilOp, invIlOp) = (ILOp.brfalse, ILOp.brtrue);

                    if (instr.opcode == ABCOp.ifne || instr.opcode == ABCOp.ifstrictne)
                        (ilOp, invIlOp) = (invIlOp, ilOp);

                    break;
                }

                case ComparisonType.ANY_UNDEF_L:
                case ComparisonType.ANY_UNDEF_R:
                {
                    if ((cmpType == ComparisonType.ANY_UNDEF_L) ? !leftInputNode.isNotPushed : !rightInputNode.isNotPushed)
                        goto case ComparisonType.ANY;

                    var tempvar = m_ilBuilder.acquireTempLocal(typeof(ASAny));
                    m_ilBuilder.emit(ILOp.stloc, tempvar);
                    m_ilBuilder.emit(ILOp.ldloca, tempvar);
                    m_ilBuilder.emit(ILOp.call, KnownMembers.anyGetIsDefined, 0);

                    if (instr.opcode == ABCOp.ifeq || instr.opcode == ABCOp.ifstricteq)
                        (ilOp, invIlOp) = (ILOp.brtrue, ILOp.brfalse);
                    else
                        (ilOp, invIlOp) = (ILOp.brfalse, ILOp.brtrue);
                    break;
                }
            }

            if (branchEmitInfo.trueIsFallThrough)
                m_ilBuilder.emit(invIlOp, branchEmitInfo.falseLabel);
            else
                m_ilBuilder.emit(ilOp, branchEmitInfo.trueLabel);

            _finishTwoWayConditionalBranch(thisBlock, branchEmitInfo);

            bool isNegativeCompare(ABCOp abcOp) {
                return abcOp == ABCOp.ifne || abcOp == ABCOp.ifstrictne
                    || ((int)abcOp >= (int)ABCOp.ifnlt && (int)abcOp <= (int)ABCOp.ifnge);
            }

            void getILOpForIntCompare(ABCOp abcOp, bool isUnsigned, out ILOp _ilOp, out ILOp _invIlOp) {
                (_ilOp, _invIlOp) = (abcOp, isUnsigned) switch {
                    (ABCOp.ifeq or ABCOp.ifstricteq, _) => (ILOp.beq, ILOp.bne_un),
                    (ABCOp.ifne or ABCOp.ifstrictne, _) => (ILOp.bne_un, ILOp.beq),

                    (ABCOp.iflt or ABCOp.ifnge, false) => (ILOp.blt, ILOp.bge),
                    (ABCOp.iflt or ABCOp.ifnge, true) => (ILOp.blt_un, ILOp.bge_un),
                    (ABCOp.ifle or ABCOp.ifngt, false) => (ILOp.ble, ILOp.bgt),
                    (ABCOp.ifle or ABCOp.ifngt, true) => (ILOp.ble_un, ILOp.bgt_un),
                    (ABCOp.ifgt or ABCOp.ifnle, false) => (ILOp.bgt, ILOp.ble),
                    (ABCOp.ifgt or ABCOp.ifnle, true) => (ILOp.bgt_un, ILOp.ble_un),
                    (ABCOp.ifge or ABCOp.ifnlt, false) => (ILOp.bge, ILOp.blt),
                    (ABCOp.ifge or ABCOp.ifnlt, true) => (ILOp.bge_un, ILOp.blt_un)
                };
            }

            void getILOpForFloatCompare(ABCOp abcOp, out ILOp _ilOp, out ILOp _invIlOp) {
                (_ilOp, _invIlOp) = abcOp switch {
                    ABCOp.ifeq or ABCOp.ifstricteq => (ILOp.beq, ILOp.bne_un),
                    ABCOp.ifne or ABCOp.ifstrictne => (ILOp.bne_un, ILOp.beq),
                    ABCOp.iflt => (ILOp.blt, ILOp.bge_un),
                    ABCOp.ifle => (ILOp.ble, ILOp.bgt_un),
                    ABCOp.ifgt => (ILOp.bgt, ILOp.ble_un),
                    ABCOp.ifge => (ILOp.bge, ILOp.blt_un),
                    ABCOp.ifnlt => (ILOp.bge_un, ILOp.blt),
                    ABCOp.ifnle => (ILOp.bgt_un, ILOp.ble),
                    ABCOp.ifngt => (ILOp.ble_un, ILOp.bgt),
                    ABCOp.ifnge => (ILOp.blt_un, ILOp.bge)
                };
            }

            MethodInfo getMethodForStrCompare(ABCOp abcOp) {
                return abcOp switch {
                    ABCOp.ifeq or ABCOp.ifne or ABCOp.ifstricteq or ABCOp.ifstrictne => KnownMembers.strEquals,
                    ABCOp.iflt or ABCOp.ifnlt => KnownMembers.strLt,
                    ABCOp.ifle or ABCOp.ifnle => KnownMembers.strLeq,
                    ABCOp.ifgt or ABCOp.ifngt => KnownMembers.strGt,
                    ABCOp.ifge or ABCOp.ifnge => KnownMembers.strGeq
                };
            }

            MethodInfo getMethodForObjectCompare(ABCOp abcOp) {
                return abcOp switch {
                    ABCOp.ifeq or ABCOp.ifne => KnownMembers.objWeakEq,
                    ABCOp.ifstricteq or ABCOp.ifstrictne => KnownMembers.objStrictEq,
                    ABCOp.iflt or ABCOp.ifnlt => KnownMembers.objLt,
                    ABCOp.ifle or ABCOp.ifnle => KnownMembers.objLeq,
                    ABCOp.ifgt or ABCOp.ifngt => KnownMembers.objGt,
                    ABCOp.ifge or ABCOp.ifnge => KnownMembers.objGeq
                };
            }

            MethodInfo getMethodForAnyCompare(ABCOp abcOp) {
                return abcOp switch {
                    ABCOp.ifeq or ABCOp.ifne => KnownMembers.anyWeakEq,
                    ABCOp.ifstricteq or ABCOp.ifstrictne => KnownMembers.anyStrictEq,
                    ABCOp.iflt or ABCOp.ifnlt => KnownMembers.anyLt,
                    ABCOp.ifle or ABCOp.ifnle => KnownMembers.anyLeq,
                    ABCOp.ifgt or ABCOp.ifngt => KnownMembers.anyGt,
                    ABCOp.ifge or ABCOp.ifnge => KnownMembers.anyGeq
                };
            }
        }

        private void _visitLookupSwitch(ref Instruction instr) {
            ref DataNode inputNode = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));
            _emitTypeCoerceForTopOfStack(inputNode, DataNodeType.INT);

            ref BasicBlock thisBlock = ref m_compilation.getBasicBlockOfInstruction(instr);
            var exitBlockIds = m_compilation.staticIntArrayPool.getSpan(thisBlock.exitBlockIds);

            ref BasicBlock defaultBlock = ref m_compilation.getBasicBlock(exitBlockIds[0]);
            var caseBlockIds = exitBlockIds.Slice(1);
            var caseLabels = m_tempLabelArray.clearAndAddUninitialized(caseBlockIds.Length);

            for (int i = 0; i < caseBlockIds.Length; i++) {
                ref BasicBlock caseBlock = ref m_compilation.getBasicBlock(caseBlockIds[i]);
                if (_isBlockTransitionRequired(thisBlock, caseBlock))
                    caseLabels[i] = m_ilBuilder.createLabel();
                else
                    caseLabels[i] = _getLabelForJumpToBlock(thisBlock, caseBlock);
            }

            m_ilBuilder.emit(ILOp.@switch, caseLabels);

            _emitBlockTransition(thisBlock, defaultBlock);
            if (!_isBasicBlockImmediatelyBefore(thisBlock, defaultBlock))
                m_ilBuilder.emit(ILOp.br, _getLabelForJumpToBlock(thisBlock, defaultBlock));

            for (int i = 0; i < caseBlockIds.Length; i++) {
                ref BasicBlock caseBlock = ref m_compilation.getBasicBlock(caseBlockIds[i]);
                var targetLabel = _getLabelForJumpToBlock(thisBlock, caseBlock);

                if (caseLabels[i] != targetLabel) {
                    m_ilBuilder.markLabel(caseLabels[i]);
                    _emitBlockTransition(thisBlock, caseBlock);
                    m_ilBuilder.emit(ILOp.br, targetLabel);
                }
            }
        }

        private void _visitGlobalMemoryLoad(ref Instruction instr) {
            ref DataNode address = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(instr));

            _emitTypeCoerceForTopOfStack(address, DataNodeType.INT);

            var addrTemp = m_ilBuilder.acquireTempLocal(typeof(int));
            m_ilBuilder.emit(ILOp.stloc, addrTemp);

            // Emit length check
            m_ilBuilder.emit(ILOp.ldloc, addrTemp);
            m_ilBuilder.emit(ILOp.ldloca, m_globalMemReadSpanLocal);
            m_ilBuilder.emit(ILOp.call, KnownMembers.roSpanOfByteLength, 0);
            m_ilBuilder.emit(ILOp.bge_un, m_globalMemOutOfBoundsErrLabel);

            if (instr.opcode == ABCOp.li8 || instr.opcode == ABCOp.lix8) {
                // For li8 and lix8 we can use the ReadOnlySpan indexer.
                m_ilBuilder.emit(ILOp.ldloca, m_globalMemReadSpanLocal);
                m_ilBuilder.emit(ILOp.ldloc, addrTemp);
                m_ilBuilder.emit(ILOp.call, KnownMembers.roSpanOfByteGet, -1);
                m_ilBuilder.emit((instr.opcode == ABCOp.lix8) ? ILOp.ldind_i1 : ILOp.ldind_u1);

                m_ilBuilder.releaseTempLocal(addrTemp);
                return;
            }

            m_ilBuilder.emit(ILOp.ldloca, m_globalMemReadSpanLocal);
            m_ilBuilder.emit(ILOp.ldloc, addrTemp);
            m_ilBuilder.emit(ILOp.call, KnownMembers.roSpanOfByteSliceIndex, -1);

            (Type valueTempType, MethodInfo readMethod) = instr.opcode switch {
                ABCOp.li16 => (typeof(ushort), KnownMembers.tryReadUint16LittleEndian),
                ABCOp.lix16 => (typeof(short), KnownMembers.tryReadInt16LittleEndian),
                ABCOp.li32 or ABCOp.lf32 => (typeof(int), KnownMembers.tryReadInt32LittleEndian),
                ABCOp.lf64 => (typeof(long), KnownMembers.tryReadInt64LittleEndian)
            };

            var valueTemp = m_ilBuilder.acquireTempLocal(valueTempType);

            m_ilBuilder.emit(ILOp.ldloca, valueTemp);
            m_ilBuilder.emit(ILOp.call, readMethod, -1);
            m_ilBuilder.emit(ILOp.brfalse, m_globalMemOutOfBoundsErrLabel);

            m_ilBuilder.emit(ILOp.ldloc, valueTemp);

            // For lf32/lf64 we need to bit-cast the read integer value to a float/double.
            if (instr.opcode == ABCOp.lf32) {
                m_ilBuilder.emit(ILOp.call, KnownMembers.int32BitsToFloat, 0);
                m_ilBuilder.emit(ILOp.conv_r8);
            }
            else if (instr.opcode == ABCOp.lf64) {
                m_ilBuilder.emit(ILOp.call, KnownMembers.int64BitsToDouble, 0);
            }

            m_ilBuilder.releaseTempLocal(valueTemp);
            m_ilBuilder.releaseTempLocal(addrTemp);
        }

        private void _visitGlobalMemoryStore(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(instr);
            ref DataNode valueNode = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode addressNode = ref m_compilation.getDataNode(stackPopIds[1]);

            (DataNodeType valueType, Type valueTempType) = instr.opcode switch {
                ABCOp.sf32 => (DataNodeType.NUMBER, typeof(float)),
                ABCOp.sf64 => (DataNodeType.NUMBER, typeof(double)),
                _ => (DataNodeType.INT, typeof(int))
            };

            var addrTemp = m_ilBuilder.acquireTempLocal(typeof(int));
            var valueTemp = m_ilBuilder.acquireTempLocal(valueTempType);

            _emitTypeCoerceForTopOfStack(addressNode, DataNodeType.INT);
            m_ilBuilder.emit(ILOp.stloc, addrTemp);
            _emitTypeCoerceForTopOfStack(valueNode, valueType);
            m_ilBuilder.emit(ILOp.stloc, valueTemp);

            // Emit length check
            m_ilBuilder.emit(ILOp.ldloc, addrTemp);
            m_ilBuilder.emit(ILOp.ldloca, m_globalMemWriteSpanLocal);
            m_ilBuilder.emit(ILOp.call, KnownMembers.spanOfByteLength, 0);
            m_ilBuilder.emit(ILOp.bge_un, m_globalMemOutOfBoundsErrLabel);

            if (instr.opcode == ABCOp.si8) {
                // For si8 we can use the Span indexer.
                m_ilBuilder.emit(ILOp.ldloca, m_globalMemWriteSpanLocal);
                m_ilBuilder.emit(ILOp.ldloc, addrTemp);
                m_ilBuilder.emit(ILOp.call, KnownMembers.spanOfByteGet, -1);
                m_ilBuilder.emit(ILOp.ldloc, valueTemp);
                m_ilBuilder.emit(ILOp.stind_i1);

                m_ilBuilder.releaseTempLocal(addrTemp);
                m_ilBuilder.releaseTempLocal(valueTemp);
                return;
            }

            m_ilBuilder.emit(ILOp.ldloca, m_globalMemWriteSpanLocal);
            m_ilBuilder.emit(ILOp.ldloc, addrTemp);
            m_ilBuilder.emit(ILOp.call, KnownMembers.spanOfByteSliceIndex, -1);

            m_ilBuilder.emit(ILOp.ldloc, valueTemp);

            // For lf32/lf64 we need to bit-cast the float or double to the same sized integer.
            if (instr.opcode == ABCOp.sf32)
                m_ilBuilder.emit(ILOp.call, KnownMembers.floatToInt32Bits, 0);
            else if (instr.opcode == ABCOp.sf64)
                m_ilBuilder.emit(ILOp.call, KnownMembers.doubleToInt64Bits, 0);

            MethodInfo writeMethod = instr.opcode switch {
                ABCOp.si16 => KnownMembers.tryWriteInt16LittleEndian,
                ABCOp.si32 or ABCOp.sf32 => KnownMembers.tryWriteInt32LittleEndian,
                ABCOp.sf64 => KnownMembers.tryWriteInt64LittleEndian
            };

            m_ilBuilder.emit(ILOp.call, writeMethod, -1);
            m_ilBuilder.emit(ILOp.brfalse, m_globalMemOutOfBoundsErrLabel);

            m_ilBuilder.releaseTempLocal(valueTemp);
            m_ilBuilder.releaseTempLocal(addrTemp);
        }

        /// <summary>
        /// Returns the type of a node after it has been pushed. This takes into account
        /// any type conversion that may be applied immediately after pushing it onto the stack.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <returns>The type of <paramref name="node"/> after it has been pushed onto
        /// the stack.</returns>
        private DataNodeType _getPushedTypeOfNode(in DataNode node) =>
            (node.onPushCoerceType != DataNodeType.UNKNOWN) ? node.onPushCoerceType : node.dataType;

        /// <summary>
        /// Returns the class of a node after it has been pushed. This takes into account
        /// any type conversion that may be applied immediately after pushing it onto the stack.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <returns>A <see cref="Class"/> representing the data type of <paramref name="node"/>
        /// after it has been pushed onto the stack.</returns>
        private Class? _getPushedClassOfNode(in DataNode node) {
            return (node.onPushCoerceType != DataNodeType.UNKNOWN)
                ? getClass(node.onPushCoerceType)
                : m_compilation.getDataNodeClass(node);
        }

        /// <summary>
        /// Emits code for any type conversion that is required for a value that has been
        /// pushed onto the stack by the latest emitted instruction.
        /// </summary>
        /// <param name="pushedNode">A reference to a <see cref="DataNode"/> reprsenting the node
        /// that has been pushed onto the stack by the latest emitted instruction.</param>
        private void _emitOnPushTypeCoerce(in DataNode pushedNode) {
            if (pushedNode.isNotPushed)
                return;

            if (pushedNode.onPushCoerceType != DataNodeType.UNKNOWN) {
                _emitTypeCoerceForTopOfStack(
                    pushedNode,
                    pushedNode.onPushCoerceType,
                    usePrePushType: true,
                    useConvertStr: (pushedNode.flags & DataNodeFlags.PUSH_CONVERT_STRING) != 0
                );
            }

            if ((pushedNode.flags & DataNodeFlags.PUSH_OPTIONAL_PARAM) != 0) {
                TypeSignature optParamTypeSig;
                using (var lockedContext = m_compilation.getContext())
                    optParamTypeSig = lockedContext.value.getTypeSigForOptionalParam(_getPushedClassOfNode(pushedNode));

                var ctorHandle = m_compilation.metadataContext.getMemberHandle(
                    KnownMembers.optionalParamCtor,
                    m_compilation.metadataContext.getTypeHandle(optParamTypeSig)
                );

                m_ilBuilder.emit(ILOp.newobj, ctorHandle, 0);
            }
        }

        /// <summary>
        /// Returns a value indicating whether a value on the stack of type <paramref name="nodeType"/>
        /// can be trivially (i.e. without emitting any code) converted to <paramref name="toType"/>.
        /// </summary>
        /// <param name="nodeType">The current type of the value on the stack.</param>
        /// <param name="toType">The type that the value has to be converted to.</param>
        /// <returns>True if a value of type <paramref name="nodeType"/> is trivially convertible to
        /// the type <paramref name="toType"/>, otherwise false.</returns>
        private bool _isTrivialTypeConversion(DataNodeType nodeType, DataNodeType toType) {
            if (nodeType == toType)
                return true;

            if (toType == DataNodeType.OBJECT)
                return isObjectType(nodeType) || nodeType == DataNodeType.NULL;

            if (toType == DataNodeType.ANY)
                return nodeType == DataNodeType.UNDEFINED;

            if (isInteger(toType))
                return isInteger(nodeType);

            return false;
        }

        /// <summary>
        /// Returns true if a value on the stack of the given type can be trivially (i.e.
        /// without emitting any code) be coerced to another type.
        /// </summary>
        /// <param name="fromClass">The type of the value on the stack.</param>
        /// <param name="toClass">The type to which the value must be coerced to.</param>
        /// <returns>True if the conversion from <paramref name="fromClass"/> to
        /// <paramref name="toClass"/> is trivial, otherwise false.</returns>
        private static bool _isTrivialTypeConversion(Class? fromClass, Class? toClass) {
            if (fromClass == toClass)
                return true;

            if (fromClass == null || toClass == null)
                return false;

            var tagSet = new ClassTagSet(fromClass.tag, toClass.tag);

            if (ClassTagSet.integer.containsAll(tagSet))
                return true;    // int and uint are trivially convertible to each other.
            if (ClassTagSet.primitive.containsAny(tagSet))
                return false;
            if (!fromClass.canAssignTo(toClass))
                return false;
            if (fromClass.isInterface && toClass.isObjectClass)
                return false;

            return true;
        }

        /// <summary>
        /// Returns true if a value on the stack of the given type can be trivially (i.e.
        /// without emitting any code) be coerced to another type.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> representing the
        /// value on the stack.</param>
        /// <param name="toClass">The type to which the value must be coerced to.</param>
        /// <returns>True if the conversion from the type of <paramref name="node"/> to
        /// <paramref name="toClass"/> is trivial, otherwise false.</returns>
        private bool _isTrivialTypeConversion(in DataNode node, Class? toClass) {
            if (_getPushedTypeOfNode(node) == DataNodeType.NULL)
                return toClass != null && !ClassTagSet.numericOrBool.contains(toClass.tag);

            return _isTrivialTypeConversion(_getPushedClassOfNode(node), toClass);
        }

        /// <summary>
        /// Emits IL to coerce the type of the top value on the stack to the given class.
        /// </summary>
        /// <param name="nodeId">The data node id of the node currently at the top of the stack.</param>
        /// <param name="toType">The type to which the value represented by <paramref name="nodeId"/>
        /// is to be coerced.</param>
        /// <param name="usePrePushType">Set to true to ignore the value of
        /// <see cref="DataNode.onPushCoerceType"/> when determining the current type of
        /// the node given by <paramref name="nodeId"/>.</param>
        /// <param name="useConvertStr">Set to true to use convert_s semantics (instead of
        /// coerce_s) when coercing to a string.</param>
        private void _emitTypeCoerceForTopOfStack(
            int nodeId, DataNodeType toType, bool usePrePushType = false, bool useConvertStr = false)
        {
            _emitTypeCoerceForTopOfStack(m_compilation.getDataNode(nodeId), toType, usePrePushType, useConvertStr);
        }

        /// <summary>
        /// Emits IL to coerce the type of the top value on the stack to the given class.
        /// </summary>
        /// <param name="node">The node at the top of the stack.</param>
        /// <param name="toType">The type to which the value represented by <paramref name="node"/>
        /// is to be coerced.</param>
        /// <param name="usePrePushType">Set to true to ignore the value of
        /// <see cref="DataNode.onPushCoerceType"/> when determining the current type of
        /// <paramref name="node"/>.</param>
        /// <param name="useConvertStr">Set to true to use convert_s semantics (instead of
        /// coerce_s) when coercing to a string.</param>
        /// <param name="isForcePushed">Set to true if <paramref name="node"/> may have been force pushed
        /// onto the stack after being marked as no-push.</param>
        private void _emitTypeCoerceForTopOfStack(
            in DataNode node,
            DataNodeType toType,
            bool usePrePushType = false,
            bool useConvertStr = false,
            bool isForcePushed = false
        ) {
            Debug.Assert(isForcePushed || !node.isNotPushed);
            Debug.Assert(node.dataType != DataNodeType.REST || m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_REST_ARRAY));

            DataNodeType nodeType = usePrePushType ? node.dataType : _getPushedTypeOfNode(node);

            bool needsStringConv =
                useConvertStr
                && toType == DataNodeType.STRING
                && !node.isNotNull
                && (usePrePushType || (node.flags & DataNodeFlags.PUSH_CONVERT_STRING) == 0);

            // We don't exit early if toType is OBJECT because the node type may be an
            // interface, and conversion requires a cast to the Object class. ILEmitHelper
            // will check this and only emit the cast instruction if needed.
            if (nodeType == toType && toType != DataNodeType.OBJECT && !needsStringConv)
                return;

            Class? currentClass = usePrePushType ? m_compilation.getDataNodeClass(node) : _getPushedClassOfNode(node);

            switch (toType) {
                case DataNodeType.INT:
                    ILEmitHelper.emitTypeCoerceToInt(m_ilBuilder, currentClass, m_compilation.compileOptions.useNativeDoubleToIntegerConversions);
                    break;
                case DataNodeType.UINT:
                    ILEmitHelper.emitTypeCoerceToUint(m_ilBuilder, currentClass, m_compilation.compileOptions.useNativeDoubleToIntegerConversions);
                    break;
                case DataNodeType.NUMBER:
                    ILEmitHelper.emitTypeCoerceToNumber(m_ilBuilder, currentClass);
                    break;
                case DataNodeType.BOOL:
                    ILEmitHelper.emitTypeCoerceToBoolean(m_ilBuilder, currentClass);
                    break;
                case DataNodeType.ANY:
                    ILEmitHelper.emitTypeCoerceToAny(m_ilBuilder, currentClass);
                    break;

                case DataNodeType.STRING:
                    if (needsStringConv || node.dataType != DataNodeType.NULL)
                        ILEmitHelper.emitTypeCoerceToString(m_ilBuilder, currentClass, needsStringConv);
                    break;

                case DataNodeType.OBJECT:
                    if (node.dataType != DataNodeType.NULL)
                        ILEmitHelper.emitTypeCoerceToObject(m_ilBuilder, currentClass);
                    break;

                case DataNodeType.NAMESPACE:
                case DataNodeType.QNAME:
                {
                    if (node.dataType == DataNodeType.NULL)
                        break;

                    using var lockedContext = m_compilation.getContext();
                    Class toClass = getClass(toType)!;

                    if (currentClass == null) {
                        m_ilBuilder.emit(ILOp.call, lockedContext.value.getEntityHandleForAnyCast(toClass), 0);
                    }
                    else {
                        ILEmitHelper.emitTypeCoerceToObject(m_ilBuilder, currentClass);
                        m_ilBuilder.emit(ILOp.call, lockedContext.value.getEntityHandleForObjectCast(toClass), 0);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Emits IL to coerce the type of the top value on the stack to the given class.
        /// </summary>
        /// <param name="node">The node at the top of the stack.</param>
        /// <param name="toClass">The <see cref="Class"/> representing the type to which
        /// the value represented by <paramref name="node"/> is to be coerced.</param>
        /// <param name="isForcePushed">Set to true if <paramref name="node"/> may have been force
        /// pushed onto the stack after being marked as no-push.</param>
        private void _emitTypeCoerceForTopOfStack(in DataNode node, Class? toClass, bool isForcePushed = false) {
            Debug.Assert(isForcePushed || !node.isNotPushed);

            DataNodeType nodeType = _getPushedTypeOfNode(node);
            DataNodeType nodeTypeForClass = getDataTypeOfClass(toClass);

            if (nodeTypeForClass != DataNodeType.OBJECT || toClass!.isObjectClass) {
                _emitTypeCoerceForTopOfStack(node, nodeTypeForClass, isForcePushed: isForcePushed);
            }
            else if (isAnyOrUndefined(nodeType)) {
                using var lockedContext = m_compilation.getContext();
                m_ilBuilder.emit(ILOp.call, lockedContext.value.getEntityHandleForAnyCast(toClass), 0);
            }
            else {
                if (nodeType == DataNodeType.NULL)
                    return;

                Class nodeClass;
                if (isObjectType(nodeType)) {
                    nodeClass = _getPushedClassOfNode(node)!;
                }
                else if (nodeType == DataNodeType.REST) {
                    Debug.Assert(m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_REST_ARRAY));
                    nodeClass = s_arrayClass;
                }
                else {
                    _emitTypeCoerceForTopOfStack(node, DataNodeType.OBJECT);
                    nodeClass = s_objectClass;
                }

                if (!nodeClass.canAssignTo(toClass)) {
                    using var lockedContext = m_compilation.getContext();
                    m_ilBuilder.emit(ILOp.call, lockedContext.value.getEntityHandleForObjectCast(toClass), 0);
                }
            }
        }

        /// <summary>
        /// Emits IL to coerce the type of the top value on the stack to the given class.
        /// </summary>
        /// <param name="fromClass">The <see cref="Class"/> representing the type of the value
        /// currently at the top of the stack.</param>
        /// <param name="toClass">The <see cref="Class"/> representing the type to which
        /// the value of type <paramref name="fromClass"/> is to be coerced.</param>
        private void _emitTypeCoerceForTopOfStack(Class? fromClass, Class? toClass) {
            if (fromClass == toClass)
                return;

            if (toClass == null || toClass.isPrimitiveClass) {
                ILEmitHelper.emitTypeCoerce(
                    m_ilBuilder, fromClass, toClass, m_compilation.compileOptions.useNativeDoubleToIntegerConversions);
                return;
            }

            if (toClass.isObjectClass) {
                ILEmitHelper.emitTypeCoerceToObject(m_ilBuilder, fromClass);
                return;
            }

            using var lockedContext = m_compilation.getContext();

            if (fromClass == null) {
                m_ilBuilder.emit(ILOp.call, lockedContext.value.getEntityHandleForAnyCast(toClass), 0);
            }
            else if (fromClass.isPrimitiveClass || !fromClass.canAssignTo(toClass)) {
                ILEmitHelper.emitTypeCoerceToObject(m_ilBuilder, fromClass);
                m_ilBuilder.emit(ILOp.call, lockedContext.value.getEntityHandleForObjectCast(toClass), 0);
            }
        }

        /// <summary>
        /// Emits IL to coerce the types of the top two values on the stack to the given types.
        /// </summary>
        /// <param name="node1">The node immediately under the topmost node on the stack.</param>
        /// <param name="node2">The node at the top of the stack.</param>
        /// <param name="toType1">The type to which the value represented by <paramref name="node1"/>
        /// is to be coerced.</param>
        /// <param name="toType2">The type to which the value represented by <paramref name="node2"/>
        /// is to be coerced.</param>
        /// <param name="useConvertStr">Set to true to use convert_s semantics (instead of
        /// coerce_s) when coercing to a string.</param>
        private void _emitTypeCoerceForStackTop2(
            in DataNode node1, in DataNode node2, DataNodeType toType1, DataNodeType toType2, bool useConvertStr = false)
        {
            Debug.Assert(((node1.flags | node2.flags) & DataNodeFlags.NO_PUSH) == 0);

            if (_isTrivialTypeConversion(_getPushedTypeOfNode(node1), toType1)
                && (!useConvertStr || toType1 != DataNodeType.STRING || node1.isNotNull))
            {
                _emitTypeCoerceForTopOfStack(node2, toType2, useConvertStr: useConvertStr);
                return;
            }

            var stashVar = _emitStashTopOfStack(node2, preserveObjectClass: false);
            _emitTypeCoerceForTopOfStack(node1, toType1, useConvertStr: useConvertStr);
            _emitUnstash(stashVar);
            _emitTypeCoerceForTopOfStack(node2, toType2, useConvertStr: useConvertStr);
        }

        /// <summary>
        /// Emits code to push the value of a constant data node onto the stack.
        /// </summary>
        /// <param name="node">A reference to the data node containing the constant value. This
        /// may also be a node having the type <see cref="DataNodeType.THIS"/> or
        /// <see cref="DataNodeType.REST"/>.</param>
        /// <param name="ignoreNoPush">If true, emits the pushing code even if the node has the
        /// <see cref="DataNodeFlags.NO_PUSH"/> flag set.</param>
        private void _emitPushConstantNode(in DataNode node, bool ignoreNoPush = false) {
            Debug.Assert(node.isConstant || node.dataType == DataNodeType.THIS || node.dataType == DataNodeType.REST);

            if (node.isNotPushed && !ignoreNoPush)
                return;

            if (_canUseDupForConstantNode(node)) {
                m_ilBuilder.emit(ILOp.dup);
                return;
            }

            switch (node.dataType) {
                case DataNodeType.INT:
                case DataNodeType.UINT:
                    m_ilBuilder.emit(ILOp.ldc_i4, node.constant.intValue);
                    break;

                case DataNodeType.NUMBER:
                    ILEmitHelper.emitPushDoubleConstant(m_ilBuilder, node.constant.doubleValue);
                    break;

                case DataNodeType.STRING:
                    m_ilBuilder.emit(ILOp.ldstr, node.constant.stringValue);
                    break;

                case DataNodeType.BOOL:
                    m_ilBuilder.emit(node.constant.boolValue ? ILOp.ldc_i4_1 : ILOp.ldc_i4_0);
                    break;

                case DataNodeType.NULL:
                    m_ilBuilder.emit(ILOp.ldnull);
                    break;

                case DataNodeType.UNDEFINED:
                    m_ilBuilder.emit(ILOp.ldsfld, KnownMembers.undefinedField);
                    break;

                case DataNodeType.NAMESPACE:
                    _emitPushXmlNamespaceConstant(node.constant.namespaceValue);
                    break;

                case DataNodeType.QNAME: {
                    using var lockedContext = m_compilation.getContext();
                    var emitConstData = lockedContext.value.emitConstData;
                    var index = emitConstData.getXMLQNameIndex(node.constant.qnameValue);
                    m_ilBuilder.emit(ILOp.ldsfld, emitConstData.xmlQnameArrayFieldHandle);
                    m_ilBuilder.emit(ILOp.ldc_i4, index);
                    m_ilBuilder.emit(ILOp.ldelem_ref);
                    break;
                }

                case DataNodeType.CLASS:
                    _emitPushTraitConstant(node.constant.classValue);
                    m_ilBuilder.emit(ILOp.callvirt, KnownMembers.classGetClassObj, 0);
                    break;

                case DataNodeType.FUNCTION:
                    _emitPushTraitConstant(node.constant.methodValue);
                    m_ilBuilder.emit(ILOp.castclass, typeof(MethodTrait));
                    m_ilBuilder.emit(ILOp.ldnull);
                    m_ilBuilder.emit(ILOp.call, KnownMembers.methodTraitCreateMethodClosure);
                    break;

                case DataNodeType.GLOBAL: {
                    using var lockedContext = m_compilation.getContext();
                    m_ilBuilder.emit(ILOp.ldsfld, lockedContext.value.emitConstData.globalObjFieldHandle);
                    break;
                }

                case DataNodeType.THIS:
                    m_ilBuilder.emit(ILOp.ldarg_0);
                    break;

                case DataNodeType.REST:
                    _emitPushRestArg();
                    break;

                default:
                    Debug.Assert(false, $"Invalid data type for constant node: {node.dataType}.");
                    break;
            }
        }

        /// <summary>
        /// Checks if a constant value can be pushed onto the IL stack using a dup instruction.
        /// </summary>
        /// <param name="node">A reference to the data node that represents the constant value pushed
        /// onto the stack.</param>
        /// <returns>True if a dup instruction can be used, otherwise false.</returns>
        private bool _canUseDupForConstantNode(in DataNode node) {
            if (node.isPhi || node.slot.kind != DataNodeSlotKind.STACK)
                return false;

            if (!isNumeric(node.dataType) && node.dataType != DataNodeType.STRING)
                return false;

            if (isInteger(node.dataType) && node.constant.intValue >= -1 && node.constant.intValue <= 8) {
                // There are no size savings for emitting a dup instruction for these constants,
                // as they have single-byte IL opcodes.
                return false;
            }

            ref Instruction pushInstr = ref m_compilation.getInstruction(m_compilation.getStackNodePushInstrId(node));
            if (pushInstr.id == 0)
                return false;

            // Is the instruction preceding the one that has pushed the constant in the same
            // basic block, and has it also pushed something?
            ref Instruction prevInstr = ref m_compilation.getInstruction(pushInstr.id - 1);
            if (prevInstr.blockId != pushInstr.blockId || prevInstr.stackPushedNodeId == -1)
                return false;

            // Has the preceding instruction pushed a constant of the same type and value,
            // and has that value been pushed onto the IL stack with no type conversion?
            ref DataNode prevInstrPushedNode = ref m_compilation.getDataNode(prevInstr.stackPushedNodeId);
            if (!prevInstrPushedNode.isConstant
                || prevInstrPushedNode.isNotPushed
                || prevInstrPushedNode.dataType != node.dataType
                || prevInstrPushedNode.constant != node.constant
                || prevInstrPushedNode.onPushCoerceType != DataNodeType.UNKNOWN)
            {
                return false;
            }

            // Has the instruction that has pushed the node not popped anything from the IL stack?
            var poppedNodeIds = m_compilation.getInstructionStackPoppedNodes(pushInstr);
            for (int i = 0; i < poppedNodeIds.Length; i++) {
                if (!m_compilation.getDataNode(poppedNodeIds[i]).isNotPushed)
                    return false;
            }

            return true;
        }

        private void _emitPushXmlNamespaceConstant(Namespace value) {
            using var lockedContext = m_compilation.getContext();
            var emitConstData = lockedContext.value.emitConstData;
            var index = emitConstData.getXMLNamespaceIndex(value);
            m_ilBuilder.emit(ILOp.ldsfld, emitConstData.xmlNsArrayFieldHandle);
            m_ilBuilder.emit(ILOp.ldc_i4, index);
            m_ilBuilder.emit(ILOp.ldelem_ref);
        }

        private void _emitPushTraitConstant(Trait trait) {
            using var lockedContext = m_compilation.getContext();
            var emitConstData = lockedContext.value.emitConstData;
            int index;

            if (trait is Class klass) {
                index = emitConstData.getClassIndex(klass);
                m_ilBuilder.emit(ILOp.ldsfld, emitConstData.classesArrayFieldHandle);
            }
            else {
                index = emitConstData.getTraitIndex(trait);
                m_ilBuilder.emit(ILOp.ldsfld, emitConstData.traitsArrayFieldHandle);
            }

            m_ilBuilder.emit(ILOp.ldc_i4, index);
            m_ilBuilder.emit(ILOp.ldelem_ref);
        }

        private void _emitIsOrAsType(Class klass, ABCOp opcode, int outputNodeId) {
            if (ClassTagSet.numeric.contains(klass.tag)) {
                // Special cases for numeric types.

                MethodInfo method = klass.tag switch {
                    ClassTag.INT => KnownMembers.objIsInt,
                    ClassTag.UINT => KnownMembers.objIsUint,
                    ClassTag.NUMBER => KnownMembers.objIsNumeric
                };

                if (opcode == ABCOp.istype || opcode == ABCOp.istypelate) {
                    m_ilBuilder.emit(ILOp.call, method, 0);
                }
                else {
                    var label = m_ilBuilder.createLabel();

                    m_ilBuilder.emit(ILOp.dup);
                    m_ilBuilder.emit(ILOp.call, method, 0);
                    m_ilBuilder.emit(ILOp.brtrue, label);

                    m_ilBuilder.emit(ILOp.pop);
                    m_ilBuilder.emit(ILOp.ldnull);
                    m_ilBuilder.markLabel(label);
                }
            }
            else {
                using (var lockedContext = m_compilation.getContext())
                    m_ilBuilder.emit(ILOp.isinst, lockedContext.value.getEntityHandle(klass, noPrimitiveTypes: true));

                // If the opcode is istype/istypelate we need to do a != null comparison to get a Boolean result,
                // but we don't need to do this if the output node is used only as the input to an iftrue or
                // iffalse instruction.

                if ((opcode == ABCOp.istype || opcode == ABCOp.istypelate) && !isOutputUsedOnlyInIfTrueOrFalse()) {
                    m_ilBuilder.emit(ILOp.ldnull);
                    m_ilBuilder.emit(ILOp.cgt_un);
                }
            }

            bool isOutputUsedOnlyInIfTrueOrFalse() {
                if (outputNodeId == -1)
                    return false;

                var nodeUses = m_compilation.getDataNodeUses(outputNodeId);
                if (nodeUses.Length != 1 || !nodeUses[0].isInstruction)
                    return false;

                ABCOp useInstrOp = m_compilation.getInstruction(nodeUses[0].instrOrNodeId).opcode;
                return useInstrOp == ABCOp.iftrue || useInstrOp == ABCOp.iffalse;
            }
        }

        private int _getIndexOfRestArg() {
            const MethodCompilationFlags hasThisArgFlags =
                MethodCompilationFlags.IS_INSTANCE_METHOD | MethodCompilationFlags.IS_SCOPED_FUNCTION;

            int restArgIndex = m_compilation.getCurrentMethodParams().length;
            if (m_compilation.isAnyFlagSet(hasThisArgFlags))
                restArgIndex++;

            return restArgIndex;
        }

        private void _emitPushRestArg() {
            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_REST_ARRAY))
                m_ilBuilder.emit(ILOp.ldloc, m_restOrArgumentsArrayLocal);
            else
                m_ilBuilder.emit(ILOp.ldarga, _getIndexOfRestArg());
        }

        private void _emitPushCapturedScope() {
            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.IS_SCOPED_FUNCTION)) {
                m_ilBuilder.emit(ILOp.ldloc, m_scopedFuncScopeLocal);
            }
            else {
                Debug.Assert(m_compilation.declaringClass != null);

                using var lockedContext = m_compilation.getContext();
                var fieldHandle = lockedContext.value.getClassCapturedScopeFieldHandle(m_compilation.declaringClass);
                m_ilBuilder.emit(ILOp.ldsfld, fieldHandle);
            }
        }

        private void _emitPushCapturedScopeItem(int height) {
            _emitPushCapturedScope();
            m_ilBuilder.emit(ILOp.ldfld, m_compilation.capturedScope!.container.getFieldHandle(height));
        }

        private void _emitLoadScopeOrLocalNode(in DataNode node) {
            Debug.Assert(node.slot.kind != DataNodeSlotKind.STACK);

            if (node.isConstant) {
                _emitPushConstantNode(node);
                return;
            }

            const MethodCompilationFlags hasThisArgFlags =
                MethodCompilationFlags.IS_INSTANCE_METHOD | MethodCompilationFlags.IS_SCOPED_FUNCTION;

            if (node.dataType == DataNodeType.THIS) {
                m_ilBuilder.emit(ILOp.ldarg_0);
            }
            else if (node.dataType == DataNodeType.REST) {
                _emitPushRestArg();
            }
            else if (node.isArgument) {
                int argIndex = node.slot.id, slotIndex = node.slot.id;

                if (!m_compilation.isAnyFlagSet(hasThisArgFlags))
                    argIndex--;

                Debug.Assert(argIndex >= 0 && slotIndex <= m_compilation.getCurrentMethodParams().length + 1);

                if (slotIndex == 0 && argIndex == 0) {
                    // The this case (for instance methods) was already handled, so the only
                    // possibility is the receiver of a free function.
                    Debug.Assert(m_compilation.isAnyFlagSet(MethodCompilationFlags.IS_SCOPED_FUNCTION));
                    m_ilBuilder.emit(ILOp.ldloc, m_scopedFuncThisLocal);
                }
                else if (slotIndex == m_compilation.getCurrentMethodParams().length + 1) {
                    // Rest case was already handled earlier, so arguments is the only possibility.
                    Debug.Assert((m_compilation.methodInfo.flags & ABCMethodFlags.NEED_ARGUMENTS) != 0);
                    m_ilBuilder.emit(ILOp.ldloc, m_restOrArgumentsArrayLocal);
                }
                else {
                    // No need to handle the OptionalParam<T> case as we don't yet emit them in compiled code.
                    m_ilBuilder.emit(ILOp.ldarg, argIndex);
                }
            }
            else {
                m_ilBuilder.emit(ILOp.ldloc, _getLocalVarForNode(node));
            }
        }

        /// <summary>
        /// Checks if a binding to a local data node is eligible for write-through optimization.
        /// This optimization applies when the node is a source for a single phi node and has
        /// no other uses.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> representing the
        /// local variable being assigned its value.</param>
        /// <returns>A reference to the <see cref="DataNode"/> representing the write-through
        /// node for <paramref name="node"/>, or <paramref name="node"/> itself if it is not
        /// eligible for write-through.</returns>
        private ref DataNode _checkForLocalWriteThrough(ref DataNode node) {
            while (true) {
                if ((node.flags & DataNodeFlags.LOCAL_WRITE_THROUGH) != 0)
                    break;

                var uses = m_compilation.getDataNodeUses(node);
                if (uses.Length != 1 || !uses[0].isDataNode)
                    break;

                node.flags |= DataNodeFlags.LOCAL_WRITE_THROUGH;
                node = ref m_compilation.getDataNode(uses[0].instrOrNodeId);
            }

            return ref node;
        }

        /// <summary>
        /// Performs catch synchronization for a local variable assignment in a try-block. This
        /// ensures that the associated local node(s) at the entry of any catch clauses will
        /// have the latest assigned value should an exception be caught.
        /// </summary>
        /// <param name="writeInstr">A reference to the <see cref="Instruction"/> representing the
        /// instruction that does the assignment to the local variable.</param>
        /// <param name="localNode">A reference to the <see cref="DataNode"/> representing the local
        /// node being assigned.</param>
        /// <param name="localVar">An instance of <see cref="ILBuilder.Local"/> representing the
        /// IL local variable associated with <paramref name="localNode"/>. If <paramref name="localNode"/>
        /// does not have an associated local variable in IL (for example, it is a constant),
        /// do not pass this argument.</param>
        private void _syncLocalWriteWithCatchVars(
            in Instruction writeInstr, in DataNode localNode, ILBuilder.Local localVar = default)
        {
            int excHandlerId = m_compilation.getBasicBlockOfInstruction(writeInstr).excHandlerId;
            if (excHandlerId == -1)
                // We're not in a try region, no need to sync anything.
                return;

            if ((localNode.flags & DataNodeFlags.LOCAL_WRITE_THROUGH) != 0)
                // The local assignment has already been synced with the catch node by write-through.
                return;

            _syncLocalWithCatchVars(localNode, excHandlerId, -1, localVar);
        }

        private void _syncLocalsWithCatchVarsOnTryEntry(in BasicBlock block) {
            if (block.excHandlerId == -1)
                // We're not in a try region, no need to sync anything.
                return;

            var entryPoints = m_compilation.cfgNodeRefArrayPool.getSpan(block.entryPoints);
            bool needsSync = false;
            int syncStopAtHandlerId = -1;

            for (int i = 0; i < entryPoints.Length; i++) {
                if (!entryPoints[i].isBlock) {
                    (needsSync, syncStopAtHandlerId) = (true, -1);
                    break;
                }

                int entryFromHandlerId = m_compilation.getBasicBlock(entryPoints[i].id).excHandlerId;

                // No need of syncing locals when transferring from child to parent or within
                // the same try-region.
                if (isExcHandlerSameOrAncestorOf(block.excHandlerId, entryFromHandlerId))
                    continue;

                // We use the following rule to determine for which exception handlers we need
                // to sync local variables with the variables in the catch blocks:
                // - When all entry points into the block are from the try-region of the same
                //   exception handler (A), and that handler is an ancestor of the handler whose
                //   try-region the current block is in (B), we sync with all exception handlers
                //   in the path from B to A but not including A.
                // - Otherwise, we sync with B and and all its ancestors.

                if (entryFromHandlerId != -1 && !isExcHandlerSameOrAncestorOf(entryFromHandlerId, block.excHandlerId)) {
                    (needsSync, syncStopAtHandlerId) = (true, -1);
                }
                else if (!needsSync || syncStopAtHandlerId == entryFromHandlerId) {
                    (needsSync, syncStopAtHandlerId) = (true, entryFromHandlerId);
                }
                else {
                    syncStopAtHandlerId = -1;
                }
            }

            if (!needsSync)
                return;

            var localsAtEntry = m_compilation.staticIntArrayPool.getSpan(block.localsAtEntry);
            for (int i = 0; i < localsAtEntry.Length; i++) {
                ref DataNode localNode = ref m_compilation.getDataNode(localsAtEntry[i]);
                _tryGetLocalVarForNode(localNode, out var localVarForNode);
                _syncLocalWithCatchVars(localNode, block.excHandlerId, syncStopAtHandlerId, localVarForNode);
            }

            bool isExcHandlerSameOrAncestorOf(int ancestorId, int handlerId) {
                while (handlerId != -1) {
                    if (handlerId == ancestorId)
                        return true;
                    handlerId = m_compilation.getExceptionHandler(handlerId).parentId;
                }
                return false;
            }
        }

        private void _syncLocalWithCatchVars(
            in DataNode localNode, int handlerId, int stopHandlerId, ILBuilder.Local localVar)
        {
            int localSlotId = localNode.slot.id;

            while (handlerId != stopHandlerId && handlerId != -1) {
                ref ExceptionHandler handler = ref m_compilation.getExceptionHandler(handlerId);
                var catchLocalVar = m_catchLocalVarSyncTable[handlerId * m_compilation.localCount + localSlotId];

                if (!catchLocalVar.isDefault && catchLocalVar != localVar) {
                    if (!localVar.isDefault)
                        m_ilBuilder.emit(ILOp.ldloc, localVar);
                    else
                        _emitLoadScopeOrLocalNode(localNode);

                    ref BasicBlock catchBlock = ref m_compilation.getBasicBlockOfInstruction(handler.catchTargetInstrId);
                    var entryLocalNodeIds = m_compilation.staticIntArrayPool.getSpan(catchBlock.localsAtEntry);
                    ref DataNode catchLocalNode = ref m_compilation.getDataNode(entryLocalNodeIds[localSlotId]);

                    _emitTypeCoerceForTopOfStack(localNode, m_compilation.getDataNodeClass(catchLocalNode));
                    m_ilBuilder.emit(ILOp.stloc, catchLocalVar);
                }

                handlerId = handler.parentId;
            }
        }

        /// <summary>
        /// Emits code to discard (pop) the top node from the stack.
        /// </summary>
        /// <param name="nodeId">The node id of the node currently at the top of the stack.</param>
        private void _emitDiscardTopOfStack(int nodeId) => _emitDiscardTopOfStack(m_compilation.getDataNode(nodeId));

        /// <summary>
        /// Emits code to discard (pop) the top node from the stack.
        /// </summary>
        /// <param name="node">The node currently at the top of the stack.</param>
        private void _emitDiscardTopOfStack(in DataNode node) {
            Debug.Assert(node.slot.kind == DataNodeSlotKind.STACK);
            if (!node.isNotPushed)
                m_ilBuilder.emit(ILOp.pop);
        }

        /// <summary>
        /// Emits code to pop the value at the top of the stack and store it in a temporary local variable.
        /// </summary>
        /// <param name="node">A reference to the <see cref="DataNode"/> representing the value
        /// currently at the top of the stack.</param>
        /// <param name="preserveObjectClass">Set this to true to preserve the class of a
        /// value of an object type. If this is false, and the type of <paramref name="node"/>
        /// is an object type, the temporary local variable created will be typed as
        /// <see cref="ASObject"/>.</param>
        /// <param name="usePrePushType">Set to true to ignore the value of
        /// <see cref="DataNode.onPushCoerceType"/> when determining the current type of
        /// <paramref name="node"/>.</param>
        /// <returns>An instance of <see cref="ILBuilder.Local"/> representing the temporary
        /// variable holding the popped value.</returns>
        private ILBuilder.Local _emitStashTopOfStack(
            in DataNode node, bool preserveObjectClass = true, bool usePrePushType = false)
        {
            Debug.Assert(!node.isNotPushed);

            TypeSignature typeSig;

            using (var lockedContext = m_compilation.getContext()) {
                if (node.onPushCoerceType != DataNodeType.UNKNOWN && !usePrePushType)
                    typeSig = lockedContext.value.getTypeSignature(getClass(node.onPushCoerceType));
                else if (node.dataType == DataNodeType.REST)
                    typeSig = m_compilation.metadataContext.getTypeSignature(typeof(RestParam));
                else if (!preserveObjectClass && isObjectType(node.dataType))
                    typeSig = lockedContext.value.getTypeSignature(s_objectClass);
                else
                    typeSig = lockedContext.value.getTypeSignature(m_compilation.getDataNodeClass(node));
            }

            var tempLocal = m_ilBuilder.acquireTempLocal(typeSig);

            m_ilBuilder.emit(ILOp.stloc, tempLocal);
            return tempLocal;
        }

        /// <summary>
        /// Emits code to restore a value onto the stack that had been stashed with the
        /// <see cref="_emitStashTopOfStack"/> method. This pushes the value from the
        /// temporary variable onto the stack, and then releases the temporary.
        /// </summary>
        /// <param name="stashedLocal">The <see cref="ILBuilder.Local"/> instance that was
        /// obtained from the <see cref="_emitStashTopOfStack"/> method, representing the
        /// local variable holding the value that must be pushed.</param>
        private void _emitUnstash(ILBuilder.Local stashedLocal) {
            m_ilBuilder.emit(ILOp.ldloc, stashedLocal);
            m_ilBuilder.releaseTempLocal(stashedLocal);
        }

        /// <summary>
        /// Returns true if the given data node has an associated local variable in the emitted IL.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <returns>True if <paramref name="node"/> has an IL local variable associated with
        /// it, otherwise false.</returns>
        private bool _nodeHasLocalVar(in DataNode node) {
            return node.slot.kind != DataNodeSlotKind.STACK
                && (node.flags & (DataNodeFlags.ARGUMENT | DataNodeFlags.CONSTANT)) == 0
                && node.dataType != DataNodeType.THIS
                && node.dataType != DataNodeType.REST;
        }

        /// <summary>
        /// Returns the IL local variable associated with a given data node.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance. This must be
        /// a node for which <see cref="_nodeHasLocalVar"/> returns true.</param>
        /// <returns>An instance of <see cref="ILBuilder.Local"/> representing the local variable
        /// associated with <paramref name="node"/>.</returns>
        private ILBuilder.Local _getLocalVarForNode(in DataNode node) {
            Debug.Assert(_nodeHasLocalVar(node));

            // int and uint can share the same local slots in IL.
            var klass = (node.dataType == DataNodeType.UINT)
                ? getClass(DataNodeType.INT)
                : m_compilation.getDataNodeClass(node);

            ref var variables = ref ((node.slot.kind == DataNodeSlotKind.SCOPE) ? ref m_scopeVars : ref m_localVars);
            ref var slotVars = ref variables[node.slot.id];

            if (slotVars.isDefault)
                slotVars = m_localVarWithClassArrayPool.allocate(0);

            Span<LocalVarWithClass> slotVarsSpan = m_localVarWithClassArrayPool.getSpan(slotVars);

            for (int i = 0; i < slotVarsSpan.Length; i++) {
                if (slotVarsSpan[i].type == klass)
                    return slotVarsSpan[i].local;
            }

            TypeSignature localTypeSig;
            using (var lockedContext = m_compilation.getContext())
                localTypeSig = lockedContext.value.getTypeSignature(klass);

            var newLocal = m_compilation.ilBuilder.declareLocal(localTypeSig);
            m_localVarWithClassArrayPool.append(slotVars, new LocalVarWithClass(klass, newLocal));

            return newLocal;
        }

        /// <summary>
        /// Checks if a data node has an associated local variable in the emitted IL, and
        /// gets the <see cref="ILBuilder.Local"/> instance representing that local variable
        /// if it exists.
        /// </summary>
        /// <returns>True if <paramref name="node"/> has an associated local variable in
        /// the emitted IL, otherwise false.</returns>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance..</param>
        /// <param name="localVar">An instance of <see cref="ILBuilder.Local"/> representing the local
        /// variable associated with <paramref name="node"/>. If <paramref name="node"/> does
        /// not have an associated local variable in IL, this is set to the default value of
        /// the <see cref="ILBuilder.Local"/> type.</param>
        private bool _tryGetLocalVarForNode(in DataNode node, out ILBuilder.Local localVar) {
            bool hasLocalVar = _nodeHasLocalVar(node);
            localVar = hasLocalVar ? _getLocalVarForNode(node) : default;
            return hasLocalVar;
        }

        /// <summary>
        /// Determines the node ids of the runtime namespace and/or name arguments popped from
        /// the stack for a multiname lookup.
        /// </summary>
        /// <param name="stackNodeIds">A read-only span containing the data node ids on the stack,
        /// with the runtime name arguments at the beginning.</param>
        /// <param name="multiname">The multiname for which to obtain the runtime arguments.</param>
        /// <param name="rtNsNodeId">An output parameter into which the node id of the runtime namespace
        /// argument (or -1 if the multiname does not use a runtime namespace argument) will be
        /// written.</param>
        /// <param name="rtNameNodeId">An output parameter into which the node id of the runtime name
        /// argument (or -1 if the multiname does not use a runtime name argument) will be
        /// written.</param>
        private static void _getRuntimeMultinameArgIds(
            ReadOnlySpan<int> stackNodeIds, in ABCMultiname multiname, out int rtNsNodeId, out int rtNameNodeId)
        {
            rtNsNodeId = -1;
            rtNameNodeId = -1;

            int i = 0;
            if (multiname.hasRuntimeNamespace) {
                rtNsNodeId = stackNodeIds[i];
                i++;
            }
            if (multiname.hasRuntimeLocalName) {
                rtNameNodeId = stackNodeIds[i];
            }
        }

        /// <summary>
        /// Emits code to prepare for a runtime binding. Only the target object, and any runtime namespace and/or
        /// name arguments, must be on the stack when this code is emitted. Any other values on the stack (such
        /// as function call arguments) must be stashed into local variables and restored after calling
        /// this method.
        /// </summary>
        /// <param name="multiname">The <see cref="ABCMultiname"/> representing the property name
        /// to be resolved.</param>
        /// <param name="rtNsNodeId">The node id of the runtime namespace argument on the stack. -1 if
        /// there is no runtime namespace.</param>
        /// <param name="rtNameNodeId">The node id of the runtime name argument on the stack. -1 if
        /// there is no runtime name.</param>
        /// <param name="objectType">The type of the target object on the stack, null for the "any" type.</param>
        /// <param name="isOnRuntimeScopeStack">Set to true if the property lookup is being performed on
        /// the runtime scope stack. In this case, there must be no target object on the stack and
        /// <paramref name="objectType"/> is ignored.</param>
        /// <param name="bindingKind">A value from <see cref="RuntimeBindingKind"/> indicating which type
        /// of binding function should be called will be written to this argument.</param>
        private void _emitPrepareRuntimeBinding(
            in ABCMultiname multiname,
            int rtNsNodeId,
            int rtNameNodeId,
            Class? objectType,
            bool isOnRuntimeScopeStack,
            out RuntimeBindingKind bindingKind
        ) {
            Debug.Assert(multiname.kind != ABCConstKind.GenericClassName);

            ABCFile abc = m_compilation.abcFile;
            bool needToPrepareObject = isOnRuntimeScopeStack || objectType == null || objectType.isPrimitiveClass;

            bindingKind = default;

            switch (multiname.kind) {
                case ABCConstKind.QName:
                case ABCConstKind.QNameA:
                {
                    Namespace ns = abc.resolveNamespace(multiname.namespaceIndex);
                    string? localName = abc.resolveString(multiname.localNameIndex);

                    if (needToPrepareObject)
                        emitPrepareObject(objectType, isOnRuntimeScopeStack);

                    using (var lockedContext = m_compilation.getContext()) {
                        int qnameConstId = lockedContext.value.emitConstData.getQNameIndex(new QName(ns, localName));
                        m_ilBuilder.emit(ILOp.ldsfld, lockedContext.value.emitConstData.qnameArrayFieldHandle);
                        m_ilBuilder.emit(ILOp.ldc_i4, qnameConstId);
                        m_ilBuilder.emit(ILOp.ldelema, typeof(QName));
                    }

                    bindingKind = RuntimeBindingKind.QNAME;
                    break;
                }

                case ABCConstKind.Multiname:
                case ABCConstKind.MultinameA:
                {
                    string? localName = abc.resolveString(multiname.localNameIndex);

                    if (needToPrepareObject)
                        emitPrepareObject(objectType, isOnRuntimeScopeStack);

                    m_ilBuilder.emit(ILOp.ldstr, localName);
                    emitPushNsSet(multiname.namespaceIndex);

                    bindingKind = RuntimeBindingKind.MULTINAME;
                    break;
                }

                case ABCConstKind.RTQName:
                case ABCConstKind.RTQNameA:
                {
                    string? localName = abc.resolveString(multiname.localNameIndex);

                    ref DataNode nsNode = ref m_compilation.getDataNode(rtNsNodeId);
                    Debug.Assert(nsNode.dataType == DataNodeType.NAMESPACE && !nsNode.isNotPushed);

                    var nsLocal = m_ilBuilder.acquireTempLocal(typeof(Namespace));
                    var qnameLocal = m_ilBuilder.acquireTempLocal(typeof(QName));

                    m_ilBuilder.emit(ILOp.call, KnownMembers.namespaceFromXmlNs, 0);
                    m_ilBuilder.emit(ILOp.stloc, nsLocal);

                    if (needToPrepareObject)
                        emitPrepareObject(objectType, isOnRuntimeScopeStack);

                    m_ilBuilder.emit(ILOp.ldloca, qnameLocal);
                    m_ilBuilder.emit(ILOp.dup);
                    m_ilBuilder.emit(ILOp.ldloca, nsLocal);
                    m_ilBuilder.emit(ILOp.ldstr, localName);
                    m_ilBuilder.emit(ILOp.call, KnownMembers.qnameCtorFromNsAndLocalName, -3);

                    m_ilBuilder.releaseTempLocal(nsLocal);
                    m_ilBuilder.releaseTempLocal(qnameLocal);

                    bindingKind = RuntimeBindingKind.QNAME;
                    break;
                }

                case ABCConstKind.RTQNameL:
                case ABCConstKind.RTQNameLA:
                {
                    ref DataNode nsNode = ref m_compilation.getDataNode(rtNsNodeId);
                    ref DataNode nameNode = ref m_compilation.getDataNode(rtNameNodeId);

                    Debug.Assert(nsNode.dataType == DataNodeType.NAMESPACE && !nsNode.isNotPushed && !nameNode.isNotPushed);

                    var nameLocal = m_ilBuilder.acquireTempLocal(typeof(string));
                    var nsLocal = m_ilBuilder.acquireTempLocal(typeof(Namespace));
                    var qnameLocal = m_ilBuilder.acquireTempLocal(typeof(QName));

                    _emitTypeCoerceForTopOfStack(nameNode, DataNodeType.STRING);
                    m_ilBuilder.emit(ILOp.stloc, nameLocal);

                    m_ilBuilder.emit(ILOp.call, KnownMembers.namespaceFromXmlNs, 0);
                    m_ilBuilder.emit(ILOp.stloc, nsLocal);

                    if (needToPrepareObject)
                        emitPrepareObject(objectType, isOnRuntimeScopeStack);

                    m_ilBuilder.emit(ILOp.ldloca, qnameLocal);
                    m_ilBuilder.emit(ILOp.dup);
                    m_ilBuilder.emit(ILOp.ldloca, nsLocal);
                    m_ilBuilder.emit(ILOp.ldloc, nameLocal);
                    m_ilBuilder.emit(ILOp.call, KnownMembers.qnameCtorFromNsAndLocalName, -3);

                    m_ilBuilder.releaseTempLocal(nameLocal);
                    m_ilBuilder.releaseTempLocal(nsLocal);
                    m_ilBuilder.releaseTempLocal(qnameLocal);

                    bindingKind = RuntimeBindingKind.QNAME;
                    break;
                }

                case ABCConstKind.MultinameL:
                case ABCConstKind.MultinameLA:
                {
                    ref DataNode nameNode = ref m_compilation.getDataNode(rtNameNodeId);
                    Debug.Assert(!nameNode.isNotPushed);

                    var nameNodeType = _getPushedTypeOfNode(nameNode);

                    if (nameNodeType == DataNodeType.QNAME && nameNode.isNotNull) {
                        var qnameLocal = m_ilBuilder.acquireTempLocal(typeof(QName));

                        m_ilBuilder.emit(ILOp.call, KnownMembers.qnameFromXmlQname, 0);
                        m_ilBuilder.emit(ILOp.stloc, qnameLocal);

                        if (needToPrepareObject)
                            emitPrepareObject(objectType, isOnRuntimeScopeStack);

                        m_ilBuilder.emit(ILOp.ldloca, qnameLocal);

                        m_ilBuilder.releaseTempLocal(qnameLocal);
                        bindingKind = RuntimeBindingKind.QNAME;
                    }
                    else if (nameNodeType == DataNodeType.STRING) {
                        if (needToPrepareObject) {
                            var tempvar = m_ilBuilder.acquireTempLocal(typeof(string));
                            m_ilBuilder.emit(ILOp.stloc, tempvar);
                            emitPrepareObject(objectType, isOnRuntimeScopeStack);
                            m_ilBuilder.emit(ILOp.ldloc, tempvar);
                            m_ilBuilder.releaseTempLocal(tempvar);
                        }

                        emitPushNsSet(multiname.namespaceIndex);
                        bindingKind = RuntimeBindingKind.MULTINAME;
                    }
                    else {
                        _emitTypeCoerceForTopOfStack(nameNode, DataNodeType.ANY);

                        if (needToPrepareObject) {
                            var tempvar = m_ilBuilder.acquireTempLocal(typeof(ASAny));
                            m_ilBuilder.emit(ILOp.stloc, tempvar);
                            emitPrepareObject(objectType, isOnRuntimeScopeStack);
                            m_ilBuilder.emit(ILOp.ldloc, tempvar);
                            m_ilBuilder.releaseTempLocal(tempvar);
                        }

                        emitPushNsSet(multiname.namespaceIndex);
                        bindingKind = RuntimeBindingKind.KEY_MULTINAME;
                    }

                    break;
                }
            }

            void emitPrepareObject(Class? type, bool isRtScopeStack) {
                if (isRtScopeStack) {
                    m_ilBuilder.emit(ILOp.ldloc, m_rtScopeStackLocal);
                    return;
                }

                if (type == null) {
                    var tempvar = m_ilBuilder.acquireTempLocal(typeof(ASAny));
                    m_ilBuilder.emit(ILOp.stloc, tempvar);
                    m_ilBuilder.emit(ILOp.ldloca, tempvar);
                    m_ilBuilder.releaseTempLocal(tempvar);
                }
                else {
                    ILEmitHelper.emitTypeCoerceToObject(m_ilBuilder, type);
                }
            }

            void emitPushNsSet(int abcIndex) {
                using var lockedContext = m_compilation.getContext();
                m_ilBuilder.emit(ILOp.ldsfld, lockedContext.value.emitConstData.nsSetArrayFieldHandle);
                m_ilBuilder.emit(ILOp.ldc_i4, lockedContext.value.getEmitConstDataIdForNamespaceSet(abcIndex));
                m_ilBuilder.emit(ILOp.ldelema, typeof(NamespaceSet));
            }
        }

        /// <summary>
        /// Gets the appropriate flags from the <see cref="BindOptions"/> enumeration that
        /// must be used for a runtime property binding with the given multiname.
        /// </summary>
        /// <param name="mn">An instance of <see cref="ABCMultiname"/>.</param>
        /// <returns>The flags from the <see cref="BindOptions"/> enumeration that
        /// must be used for a runtime property binding with the given multiname.</returns>
        private BindOptions _getBindingOptionsForMultiname(in ABCMultiname mn) {
            BindOptions bindOpts = 0;

            if (mn.hasRuntimeNamespace)
                bindOpts |= BindOptions.RUNTIME_NAMESPACE;
            if (mn.hasRuntimeLocalName)
                bindOpts |= BindOptions.RUNTIME_NAME;
            if (mn.isAttributeName)
                bindOpts |= BindOptions.ATTRIBUTE;

            return bindOpts;
        }

        /// <summary>
        /// Emits code to get the value of a trait on an object.
        /// </summary>
        /// <param name="trait">The trait for which to emit code to get the value.</param>
        /// <param name="objectNode">A reference to a <see cref="DataNode"/> representing the
        /// object from which to get the trait value.</param>
        /// <param name="isSuper">Set to true when emitting a getsuper instruction.</param>
        private void _emitGetPropertyTrait(Trait trait, in DataNode objectNode, bool isSuper) {
            if (trait.isStatic && objectNode.slot.kind == DataNodeSlotKind.STACK)
                _emitDiscardTopOfStack(objectNode);

            if (objectNode.dataType == DataNodeType.REST
                && !m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_REST_ARRAY))
            {
                Debug.Assert(trait.name == QName.publicName("length") && trait.declaringClass!.tag == ClassTag.ARRAY);
                m_ilBuilder.emit(ILOp.call, KnownMembers.restParamGetLength);
                return;
            }

            TraitType traitType = trait.traitType;

            if (traitType == TraitType.FIELD) {
                EntityHandle fieldHandle;
                using (var lockedContext = m_compilation.getContext())
                    fieldHandle = lockedContext.value.getEntityHandle((FieldTrait)trait);

                m_ilBuilder.emit(trait.isStatic ? ILOp.ldsfld : ILOp.ldfld, fieldHandle);
            }
            else if (traitType == TraitType.PROPERTY) {
                _emitCallToMethod(
                    ((PropertyTrait)trait).getter!, _getPushedClassOfNode(objectNode), ReadOnlySpan<int>.Empty, isSuper);
            }
            else if (traitType == TraitType.CONSTANT) {
                ILEmitHelper.emitPushConstant(m_ilBuilder, ((ConstantTrait)trait).constantValue);
            }
            else if (traitType == TraitType.METHOD) {
                // Create method closure.

                ILBuilder.Local objLocal = default;
                if (!trait.isStatic) {
                    _emitTypeCoerceForTopOfStack(objectNode, DataNodeType.OBJECT);
                    objLocal = m_ilBuilder.acquireTempLocal(typeof(ASObject));
                    m_ilBuilder.emit(ILOp.stloc, objLocal);
                }

                _emitPushTraitConstant(trait);
                m_ilBuilder.emit(ILOp.castclass, typeof(MethodTrait));

                if (trait.isStatic) {
                    m_ilBuilder.emit(ILOp.ldnull);
                }
                else {
                    m_ilBuilder.emit(ILOp.ldloc, objLocal);
                    m_ilBuilder.releaseTempLocal(objLocal);
                }

                m_ilBuilder.emit(ILOp.call, KnownMembers.methodTraitCreateMethodClosure);
            }
        }

        /// <summary>
        /// Emits code to get the value of an index property on an object.
        /// </summary>
        /// <param name="indexProp">The index property for which to emit code to get the value.</param>
        /// <param name="objectNode">A reference to a <see cref="DataNode"/> representing the
        /// object from which to get the value at the index.</param>
        /// <param name="indexNode">A reference to a <see cref="DataNode"/> representing the index.</param>
        private void _emitGetPropertyIndex(IndexProperty indexProp, in DataNode objectNode, in DataNode indexNode) {
            if (objectNode.dataType == DataNodeType.REST
                && !m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_REST_ARRAY))
            {
                MethodInfo method = indexNode.dataType switch {
                    DataNodeType.INT => KnownMembers.restParamGetElementI,
                    DataNodeType.UINT => KnownMembers.restParamGetElementU,
                    DataNodeType.NUMBER => KnownMembers.restParamGetElementD,
                };

                m_ilBuilder.emit(ILOp.call, method, -1);
                return;
            }

            Span<int> argIds = stackalloc int[] {indexNode.id};
            _emitCallToMethod(indexProp.getMethod!, _getPushedClassOfNode(objectNode), argIds);
        }

        /// <summary>
        /// Emits code to get the value of a property on an object at runtime.
        /// </summary>
        /// <param name="objectNode">A reference to a <see cref="DataNode"/> representing the
        /// object from which to get the property value.</param>
        /// <param name="multiname">The <see cref="ABCMultiname"/> representing the property name.</param>
        /// <param name="rtNsNodeId">The node id of the runtime namespace argument on the stack, -1
        /// if no runtime namespace is present.</param>
        /// <param name="rtNameNodeId">The node id of the runtime name argument on the stack, -1
        /// if no runtime name argument is present.</param>
        private void _emitGetPropertyRuntime(in DataNode objectNode, in ABCMultiname multiname, int rtNsNodeId, int rtNameNodeId) {
            _emitPrepareRuntimeBinding(
                multiname,
                rtNsNodeId,
                rtNameNodeId,
                _getPushedClassOfNode(objectNode),
                isOnRuntimeScopeStack: false,
                out RuntimeBindingKind bindingKind
            );

            bool isObjAny = isAnyOrUndefined(objectNode.dataType);

            MethodInfo method = (bindingKind, isObjAny) switch {
                (RuntimeBindingKind.QNAME, false) => KnownMembers.objGetPropertyQName,
                (RuntimeBindingKind.QNAME, true) => KnownMembers.anyGetPropertyQName,
                (RuntimeBindingKind.MULTINAME, false) => KnownMembers.objGetPropertyNsSet,
                (RuntimeBindingKind.MULTINAME, true) => KnownMembers.anyGetPropertyNsSet,
                (RuntimeBindingKind.KEY, false) => KnownMembers.objGetPropertyKey,
                (RuntimeBindingKind.KEY, true) => KnownMembers.anyGetPropertyKey,
                (RuntimeBindingKind.KEY_MULTINAME, false) => KnownMembers.objGetPropertyKeyNsSet,
                (RuntimeBindingKind.KEY_MULTINAME, true) => KnownMembers.anyGetPropertyKeyNsSet,
            };

            var bindOpts =
                BindOptions.SEARCH_TRAITS
                | BindOptions.SEARCH_DYNAMIC
                | BindOptions.SEARCH_PROTOTYPE
                | _getBindingOptionsForMultiname(multiname);

            m_ilBuilder.emit(ILOp.ldc_i4, (int)bindOpts);
            m_ilBuilder.emit(isObjAny ? ILOp.call : ILOp.callvirt, method);
        }

        private void _emitSetPropertyTrait(
            Trait trait, in DataNode objectNode, in DataNode valueNode, bool isSuper, bool invokeAtRuntime)
        {
            if (invokeAtRuntime) {
                _emitSetPropertyTraitAtRuntime(trait, objectNode, valueNode);
                return;
            }

            TraitType traitType = trait.traitType;

            if (traitType == TraitType.FIELD) {
                var field = (FieldTrait)trait;
                _emitTypeCoerceForTopOfStack(valueNode, field.fieldType);

                EntityHandle fieldHandle;
                using (var lockedContext = m_compilation.getContext())
                    fieldHandle = lockedContext.value.getEntityHandle(field);

                m_ilBuilder.emit(trait.isStatic ? ILOp.stsfld : ILOp.stfld, fieldHandle);
            }
            else if (traitType == TraitType.PROPERTY) {
                Span<int> argIds = stackalloc int[] {valueNode.id};
                _emitCallToMethod(
                    ((PropertyTrait)trait).setter!, _getPushedClassOfNode(objectNode), argIds, isSuper, noReturn: true);
            }

            if (trait.isStatic)
                _emitDiscardTopOfStack(objectNode);
        }

        private void _emitSetPropertyTraitAtRuntime(Trait trait, in DataNode objectNode, in DataNode valueNode) {
            ILBuilder.Local valueLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny));

            _emitTypeCoerceForTopOfStack(valueNode, DataNodeType.ANY);
            m_ilBuilder.emit(ILOp.stloc, valueLocal);

            ILBuilder.Local objLocal = default;
            if (!trait.isStatic) {
                objLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny));
                _emitTypeCoerceForTopOfStack(objectNode, DataNodeType.ANY);
                m_ilBuilder.emit(ILOp.stloc, objLocal);
            }

            _emitPushTraitConstant(trait);

            if (!trait.isStatic) {
                m_ilBuilder.emit(ILOp.ldloc, objLocal);
                m_ilBuilder.releaseTempLocal(objLocal);
            }

            m_ilBuilder.emit(ILOp.ldloc, valueLocal);
            m_ilBuilder.releaseTempLocal(valueLocal);

            m_ilBuilder.emit(
                ILOp.callvirt, trait.isStatic ? KnownMembers.traitSetValueStatic : KnownMembers.traitSetValueInst);

            if (trait.isStatic)
                _emitDiscardTopOfStack(objectNode);
        }

        private void _emitCallOrConstructTrait(
            Trait trait, int objectId, ReadOnlySpan<int> argIds, bool isConstruct, bool isSuper, int resultId)
        {
            TraitType traitType = trait.traitType;
            Debug.Assert(traitType == TraitType.METHOD || traitType == TraitType.CLASS);

            if (traitType == TraitType.METHOD) {
                Debug.Assert(!isConstruct);

                Class? objectType = (objectId == -1) ? null : _getPushedClassOfNode(m_compilation.getDataNode(objectId));
                _emitCallToMethod((MethodTrait)trait, objectType, argIds, isSuper, noReturn: resultId == -1);
            }
            else if (traitType == TraitType.CLASS) {
                var klass = (Class)trait;

                if (isConstruct) {
                    ClassConstructor ctor = klass.constructor!;
                    _emitPrepareMethodCallArguments(ctor.getParameters().asSpan(), ctor.hasRest, argIds, null, null);

                    EntityHandle ctorHandle;
                    using (var lockedContext = m_compilation.getContext())
                        ctorHandle = lockedContext.value.getEntityHandleForCtor(klass);

                    m_ilBuilder.emit(ILOp.newobj, ctorHandle);
                }
                else {
                    Debug.Assert(argIds.Length == 1);
                    _emitTypeCoerceForTopOfStack(m_compilation.getDataNode(argIds[0]), klass);
                }
            }

            if (trait.isStatic && objectId != -1 && !m_compilation.getDataNode(objectId).isNotPushed) {
                // Remove the receiver object if the method called is static.
                ILBuilder.Local resultStash = default;
                if (resultId != -1)
                    resultStash = _emitStashTopOfStack(m_compilation.getDataNode(resultId), usePrePushType: true);

                m_ilBuilder.emit(ILOp.pop);

                if (resultId != -1)
                    _emitUnstash(resultStash);
            }
        }

        private void _emitCallOrConstructTraitAtRuntime(
            Trait trait, int objectId, ReadOnlySpan<int> argIds, bool isConstruct, bool nullReceiver, bool noReturn)
        {
            ILBuilder.Local argsLocal = default;
            if (argIds.Length > 0)
                argsLocal = _emitCollectStackArgsIntoArray(argIds);

            if (trait.isStatic && objectId != -1)
                _emitDiscardTopOfStack(objectId);

            bool isDirectInvoke;

            // For fields and properties we read the field/property, coerce to any type and
            // call AS_invoke/AS_construct.
            // For other trait types, we load the Trait instance from the constant pool and
            // call invoke/construct on it.

            if (trait.traitType == TraitType.FIELD || trait.traitType == TraitType.PROPERTY) {
                isDirectInvoke = true;

                ILBuilder.Local objLocal = default;

                if (!trait.isStatic && !isConstruct && !nullReceiver) {
                    // We need to pass the receiver to AS_invoke, so save it in a temporary variable.
                    objLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny));
                    m_ilBuilder.emit(ILOp.dup);
                    _emitTypeCoerceForTopOfStack(objectId, DataNodeType.ANY);
                    m_ilBuilder.emit(ILOp.stloc, objLocal);
                }

                if (trait is FieldTrait field) {
                    EntityHandle fieldHandle;
                    using (var lockedContext = m_compilation.getContext())
                        fieldHandle = lockedContext.value.getEntityHandle(field);

                    m_ilBuilder.emit(field.isStatic ? ILOp.ldsfld : ILOp.ldfld, fieldHandle);
                    ILEmitHelper.emitTypeCoerceToAny(m_ilBuilder, field.fieldType);
                }
                else {
                    var propGetter = ((PropertyTrait)trait).getter!;
                    Class? objectType = (objectId == -1) ? null : _getPushedClassOfNode(m_compilation.getDataNode(objectId));

                    _emitCallToMethod(propGetter, objectType, ReadOnlySpan<int>.Empty);
                    if (propGetter.hasReturn)
                        ILEmitHelper.emitTypeCoerceToAny(m_ilBuilder, propGetter.returnType);
                }

                var funcLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny));
                m_ilBuilder.emit(ILOp.stloc, funcLocal);
                m_ilBuilder.emit(ILOp.ldloca, funcLocal);
                m_ilBuilder.releaseTempLocal(funcLocal);

                if (!isConstruct) {
                    if (nullReceiver || trait.isStatic) {
                        m_ilBuilder.emit(ILOp.ldnull);
                        m_ilBuilder.emit(ILOp.call, KnownMembers.anyFromObject, 0);
                    }
                    else {
                        m_ilBuilder.emit(ILOp.ldloc, objLocal);
                        m_ilBuilder.releaseTempLocal(objLocal);
                    }
                }
            }
            else {
                isDirectInvoke = false;

                if (!trait.isStatic) {
                    var objLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny));
                    _emitTypeCoerceForTopOfStack(objectId, DataNodeType.ANY);
                    m_ilBuilder.emit(ILOp.stloc, objLocal);

                    _emitPushTraitConstant(trait);

                    m_ilBuilder.emit(ILOp.ldloc, objLocal);
                    m_ilBuilder.releaseTempLocal(objLocal);
                }
                else {
                    _emitPushTraitConstant(trait);

                    // For callproplex we use the overload Trait.invoke(target, receiver, args)
                    // so for static methods we push undefined as the target (which will be ignored).
                    if (nullReceiver)
                        m_ilBuilder.emit(ILOp.ldsfld, KnownMembers.undefinedField);
                }

                if (nullReceiver) {
                    m_ilBuilder.emit(ILOp.ldnull);
                    m_ilBuilder.emit(ILOp.call, KnownMembers.anyFromObject, 0);
                }
            }

            if (argIds.Length > 0) {
                m_ilBuilder.emit(ILOp.ldloc, argsLocal);
                m_ilBuilder.emit(ILOp.newobj, KnownMembers.roSpanOfAnyFromArray, 0);
                m_ilBuilder.releaseTempLocal(argsLocal);
            }
            else {
                m_ilBuilder.emit(ILOp.call, KnownMembers.roSpanOfAnyEmpty, 1);
            }

            MethodInfo method;

            if (isDirectInvoke)
                method = isConstruct ? KnownMembers.anyConstruct : KnownMembers.anyInvoke;
            else if (nullReceiver)
                method = KnownMembers.traitInvokeInstWithReceiver;
            else if (isConstruct)
                method = trait.isStatic ? KnownMembers.traitConstructStatic : KnownMembers.traitConstructInst;
            else
                method = trait.isStatic ? KnownMembers.traitInvokeStatic : KnownMembers.traitInvokeInst;

            m_ilBuilder.emit(isDirectInvoke ? ILOp.call : ILOp.callvirt, method);

            if (noReturn)
                m_ilBuilder.emit(ILOp.pop);
        }

        private void _emitCallOrConstructIndexProp(
            IndexProperty indexProperty,
            in DataNode objectNode,
            in DataNode nameNode,
            ReadOnlySpan<int> argNodeIds,
            bool isConstruct,
            bool nullReceiver,
            bool noReturn
        ) {
            ILBuilder.Local argsLocal = default;
            if (argNodeIds.Length > 0)
                argsLocal = _emitCollectStackArgsIntoArray(argNodeIds);

            ILBuilder.Local objLocal = default;
            if (!isConstruct && !nullReceiver) {
                // Save the target object, as it has to be passed in as the receiver.
                var indexLocal = _emitStashTopOfStack(nameNode);
                objLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny));

                m_ilBuilder.emit(ILOp.dup);
                _emitTypeCoerceForTopOfStack(objectNode, DataNodeType.ANY);
                m_ilBuilder.emit(ILOp.stloc, objLocal);

                _emitUnstash(indexLocal);
            }

            MethodTrait getter = indexProperty.getMethod!;
            Span<int> getterArgs = stackalloc int[] {nameNode.id};

            _emitCallToMethod(getter, _getPushedClassOfNode(objectNode), getterArgs);

            if (getter.hasReturn)
                ILEmitHelper.emitTypeCoerceToAny(m_ilBuilder, getter.returnType);

            var funcLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny));
            m_ilBuilder.emit(ILOp.stloc, funcLocal);
            m_ilBuilder.emit(ILOp.ldloca, funcLocal);
            m_ilBuilder.releaseTempLocal(funcLocal);

            if (!isConstruct) {
                if (nullReceiver) {
                    m_ilBuilder.emit(ILOp.ldnull);
                    m_ilBuilder.emit(ILOp.call, KnownMembers.anyFromObject, 0);
                }
                else {
                    m_ilBuilder.emit(ILOp.ldloc, objLocal);
                    m_ilBuilder.releaseTempLocal(objLocal);
                }
            }

            if (argNodeIds.Length > 0) {
                m_ilBuilder.emit(ILOp.ldloc, argsLocal);
                m_ilBuilder.emit(ILOp.newobj, KnownMembers.roSpanOfAnyFromArray, 0);
                m_ilBuilder.releaseTempLocal(argsLocal);
            }
            else {
                m_ilBuilder.emit(ILOp.call, KnownMembers.roSpanOfAnyEmpty, 1);
            }

            MethodInfo method = isConstruct ? KnownMembers.anyConstruct : KnownMembers.anyInvoke;
            m_ilBuilder.emit(ILOp.call, method);

            if (noReturn)
                m_ilBuilder.emit(ILOp.pop);
        }

        /// <summary>
        /// Emits IL for calling the given method.
        /// </summary>
        /// <param name="method">The method to call.</param>
        /// <param name="receiverType">The type of the receiver on the stack, if <paramref name="method"/>
        /// is an instance method. If <paramref name="method"/> is static or this argument is null,
        /// the receiver type check is skipped.</param>
        /// <param name="argsOnStack">A span containing the node ids of the arguments on the stack.</param>
        /// <param name="isCallSuper">True if the call is to a base class method from a getsuper/setsuper/
        /// callsuper/callsupervoid instruction.</param>
        /// <param name="noReturn">True if a return value is not expected from the method call. If this is
        /// true and the method returns a value, it is popped. If this is false and the method does not return
        /// a value, undefined is pushed.</param>
        private void _emitCallToMethod(
            MethodTrait method,
            Class? receiverType,
            ReadOnlySpan<int> argsOnStack,
            bool isCallSuper = false,
            bool noReturn = false
        ) {
            _emitPrepareMethodCallArguments(
                method.getParameters().asSpan(),
                method.hasRest,
                argsOnStack,
                method.isStatic ? null : receiverType,
                method.declaringClass
            );

            if (!method.isStatic && method.declaringClass!.isPrimitiveClass
                && s_primitiveTypeMethodMap.tryGetValue(method, out MethodInfo? primitiveMethod))
            {
                ILOp callOp = primitiveMethod.IsStatic ? ILOp.call : ILOp.callvirt;
                m_ilBuilder.emit(callOp, primitiveMethod);
            }
            else {
                ILOp callOp = (method.isStatic || isCallSuper) ? ILOp.call : ILOp.callvirt;
                using var lockedContext = m_compilation.getContext();
                m_ilBuilder.emit(callOp, lockedContext.value.getEntityHandle(method));
            }

            if (noReturn && method.hasReturn)
                m_ilBuilder.emit(ILOp.pop);
            else if (!noReturn && !method.hasReturn)
                m_ilBuilder.emit(ILOp.ldsfld, KnownMembers.undefinedField);
        }

        /// <summary>
        /// Emits code to prepare the arguments on the stack for a method or constructor call.
        /// </summary>
        /// <param name="parameters">The formal parameters declared by the method or constructor.</param>
        /// <param name="hasRest">True if the method declares a rest parameter, otherwise false.</param>
        /// <param name="argsOnStack">A span containing the node ids for the arguments on the stack.</param>
        /// <param name="receiverType">The type of the receiver for an instance method call, or null
        /// if the method is not an instance method or if the receiver type check can be skipped.</param>
        /// <param name="receiverExpectedType">The expected type of the receiever for an instance method call.
        /// Only applicable if <paramref name="receiverType"/> is not null.</param>
        private void _emitPrepareMethodCallArguments(
            ReadOnlySpan<MethodTraitParameter> parameters,
            bool hasRest,
            ReadOnlySpan<int> argsOnStack,
            Class? receiverType,
            Class? receiverExpectedType
        ) {
            // Ensures that:
            // - All arguments on the stack (including the receiver for an instance method, if any)
            //   are coerced to the types of the corresponding parameters.
            // - All arguments in excess of the number of formal parameters are collected into a
            //   RestParam.
            // - If the method has a "rest" parameter and there are no excess arguments, there
            //   is an empty RestParam on the stack at the end.
            // - Default values of optional parameters with no corresponding arguments are pushed
            //   onto the stack.

            ILBuilder.Local restArrLocal = default;
            bool hasRestArgs = false;

            if (argsOnStack.Length > parameters.Length) {
                Debug.Assert(hasRest);
                hasRestArgs = true;
                restArrLocal = _emitCollectStackArgsIntoArray(argsOnStack.Slice(parameters.Length));
                argsOnStack = argsOnStack.Slice(0, parameters.Length);
            }

            int indexOfFirstNonTrivialArg = -1;
            bool mustPrepareReceiver = false;

            // Find the first argument on the stack (starting at the bottom) that needs a
            // (nontrivial) coercion to the corresponding parameter type. However, if we
            // need to coerce the receiver of an instance method then all arguments would
            // have to be popped off the stack anyway.

            if (receiverType != null && !_isTrivialTypeConversion(receiverType, receiverExpectedType)) {
                indexOfFirstNonTrivialArg = 0;
                mustPrepareReceiver = true;
            }
            else {
                for (int i = 0; i < argsOnStack.Length; i++) {
                    ref DataNode arg = ref m_compilation.getDataNode(argsOnStack[i]);
                    var param = parameters[i];

                    if (!_isTrivialTypeConversion(arg, param.type)
                        || (param.isOptional && !param.hasDefault && (arg.flags & DataNodeFlags.PUSH_OPTIONAL_PARAM) == 0))
                    {
                        indexOfFirstNonTrivialArg = i;
                        break;
                    }
                }
            }


            if (indexOfFirstNonTrivialArg != -1) {
                var argsToPrepare = argsOnStack.Slice(indexOfFirstNonTrivialArg);
                var paramsForPrepArgs = parameters.Slice(indexOfFirstNonTrivialArg);

                emitPrepareArgs(
                    argsToPrepare, paramsForPrepArgs, receiverType, mustPrepareReceiver ? receiverExpectedType : null);
            }

            if (argsOnStack.Length < parameters.Length)
                emitMissingArgs(parameters.Slice(argsOnStack.Length));

            if (hasRestArgs) {
                m_ilBuilder.emit(ILOp.ldloc, restArrLocal);
                m_ilBuilder.emit(ILOp.newobj, KnownMembers.restParamFromArray, 0);
                m_ilBuilder.releaseTempLocal(restArrLocal);
            }
            else if (hasRest) {
                // Emit an empty RestParam
                var restLoc = m_ilBuilder.acquireTempLocal(typeof(RestParam));
                m_ilBuilder.emit(ILOp.ldloca, restLoc);
                m_ilBuilder.emit(ILOp.initobj, typeof(RestParam));
                m_ilBuilder.emit(ILOp.ldloc, restLoc);
                m_ilBuilder.releaseTempLocal(restLoc);
            }

            void emitPrepareArgs(
                ReadOnlySpan<int> argsToPrepare,
                ReadOnlySpan<MethodTraitParameter> paramsForPrepArgs,
                Class? recvType,
                Class? recvExpectedType
            ) {
                Span<ILBuilder.Local> argStashLocals = m_tempLocalArray.clearAndAddDefault(argsToPrepare.Length);

                var mdContext = m_compilation.metadataContext;

                for (int i = argsToPrepare.Length - 1; i >= 0; i--) {
                    ref DataNode arg = ref m_compilation.getDataNode(argsToPrepare[i]);
                    var param = paramsForPrepArgs[i];

                    _emitTypeCoerceForTopOfStack(arg, param.type);

                    ILBuilder.Local stash;

                    if (param.isOptional && !param.hasDefault) {
                        TypeSignature optParamTypeSig;
                        using (var lockedContext = m_compilation.getContext())
                            optParamTypeSig = lockedContext.value.getTypeSigForOptionalParam(param.type);

                        if ((arg.flags & DataNodeFlags.PUSH_OPTIONAL_PARAM) == 0) {
                            var ctorHandle = mdContext.getMemberHandle(
                                KnownMembers.optionalParamCtor,
                                mdContext.getTypeHandle(optParamTypeSig)
                            );
                            m_ilBuilder.emit(ILOp.newobj, ctorHandle, 0);
                        }

                        stash = m_ilBuilder.acquireTempLocal(optParamTypeSig);
                    }
                    else {
                        using var lockedContext = m_compilation.getContext();
                        stash = m_ilBuilder.acquireTempLocal(lockedContext.value.getTypeSignature(param.type));
                    }

                    m_ilBuilder.emit(ILOp.stloc, stash);
                    argStashLocals[i] = stash;
                }

                if (recvExpectedType != null)
                    _emitTypeCoerceForTopOfStack(recvType, recvExpectedType);

                for (int i = 0; i < argStashLocals.Length; i++) {
                    m_ilBuilder.emit(ILOp.ldloc, argStashLocals[i]);
                    m_ilBuilder.releaseTempLocal(argStashLocals[i]);
                }
            }

            void emitMissingArgs(ReadOnlySpan<MethodTraitParameter> missingParams) {
                var mdContext = m_compilation.metadataContext;

                for (int i = 0; i < missingParams.Length; i++) {
                    var param = missingParams[i];

                    // If a required parameter is missing then SemanticAnalyzer would mark the
                    // call as a runtime invocation, so we should not reach here.
                    Debug.Assert(param.isOptional);

                    if (param.hasDefault) {
                        ILEmitHelper.emitPushConstantAsType(m_ilBuilder, param.defaultValue, param.type);
                    }
                    else {
                        TypeSignature optParamTypeSig;
                        using (var lockedContext = m_compilation.getContext())
                            optParamTypeSig = lockedContext.value.getTypeSigForOptionalParam(param.type);

                        var missingHandle = mdContext.getMemberHandle(KnownMembers.optionalParamMissing, mdContext.getTypeHandle(optParamTypeSig));
                        m_ilBuilder.emit(ILOp.ldsfld, missingHandle);
                    }
                }
            }
        }

        /// <summary>
        /// Emits code to create an array of type <see cref="ASAny"/> from values on the stack.
        /// </summary>
        /// <param name="argsOnStack">A span containing the node ids for the stack nodes from
        /// which to create the array. These must be the topmost N nodes on the stack, ordered
        /// from bottom to top.</param>
        /// <returns>A <see cref="ILBuilder.Local"/> representing a local variable containing
        /// the created array. This must be released using <see cref="ILBuilder.releaseTempLocal"/>
        /// after using it.</returns>
        private ILBuilder.Local _emitCollectStackArgsIntoArray(ReadOnlySpan<int> argsOnStack) {
            Debug.Assert(argsOnStack.Length > 0);

            var arrLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny[]));

            if (tryGetHelper(argsOnStack, out EntityHandle helperHandle)) {
                m_ilBuilder.emit(ILOp.call, helperHandle, -argsOnStack.Length + 1);
                m_ilBuilder.emit(ILOp.stloc, arrLocal);
                return arrLocal;
            }

            // We can't use a helper, so emit the array creation code inline.

            var elemLocal = m_ilBuilder.acquireTempLocal(typeof(ASAny));

            m_ilBuilder.emit(ILOp.ldc_i4, argsOnStack.Length);
            m_ilBuilder.emit(ILOp.newarr, typeof(ASAny));
            m_ilBuilder.emit(ILOp.stloc, arrLocal);

            for (int i = argsOnStack.Length - 1; i >= 0; i--) {
                _emitTypeCoerceForTopOfStack(m_compilation.getDataNode(argsOnStack[i]), DataNodeType.ANY);

                m_ilBuilder.emit(ILOp.stloc, elemLocal);
                m_ilBuilder.emit(ILOp.ldloc, arrLocal);
                m_ilBuilder.emit(ILOp.ldc_i4, i);
                m_ilBuilder.emit(ILOp.ldloc, elemLocal);
                m_ilBuilder.emit(ILOp.stelem, typeof(ASAny));
            }

            m_ilBuilder.releaseTempLocal(elemLocal);
            return arrLocal;

            bool tryGetHelper(ReadOnlySpan<int> _argsOnStack, out EntityHandle _helperHandle) {
                _helperHandle = default;

                using var lockedContext = m_compilation.getContext();
                HelperEmitter helperEmitter = lockedContext.value.helperEmitter;

                if (_argsOnStack.Length > helperEmitter.argArrayHelperMaxSize)
                    return false;

                // Use a helper only if no type conversions are needed.
                // This is not a significant limitation in practice because type conversions to "any" are
                // considered as non-side-effecting, and so would be hoisted except in some complex data flow
                // graphs.

                for (int i = 0; i < _argsOnStack.Length; i++) {
                    DataNodeType pushedType = _getPushedTypeOfNode(m_compilation.getDataNode(_argsOnStack[i]));
                    if (!isAnyOrUndefined(pushedType))
                        return false;
                }

                return helperEmitter.tryGetArgArrayHelper(_argsOnStack.Length, out _helperHandle);
            }
        }

        /// <summary>
        /// Emits IL for evaluating a string concatenation tree, assuming that the leaf node values
        /// are on the stack.
        /// </summary>
        /// <param name="instr">The instruction at the root of the concatentation tree.</param>
        private void _emitStringConcatTree(in Instruction instr) {
            Debug.Assert(instr.opcode == ABCOp.add && instr.data.add.isConcatTreeRoot);

            // Walk the concatenation tree and get the stack node ids of the leaf nodes.
            // These will be the arguments on the IL stack.

            ref DynamicArray<int> leafNodeIds = ref m_tempIntArray;
            leafNodeIds.clear();

            var rootChildIds = m_compilation.getInstructionStackPoppedNodes(instr);
            collectLeafNodes(rootChildIds[0], ref leafNodeIds);
            collectLeafNodes(rootChildIds[1], ref leafNodeIds);

            void collectLeafNodes(int nodeId, ref DynamicArray<int> _leafNodeIds) {
                ref DataNode node = ref m_compilation.getDataNode(nodeId);

                bool isLeaf = true;
                int leftChildId = -1, rightChildId = -1;

                if (!node.isConstant) {
                    int pushInstrId = m_compilation.getStackNodePushInstrId(node);
                    if (pushInstrId != -1) {
                        ref Instruction pushInstr = ref m_compilation.getInstruction(pushInstrId);
                        if (pushInstr.opcode == ABCOp.add && pushInstr.data.add.isConcatTreeInternalNode) {
                            // This is an internal node of the concat tree. We need to recurse into it.
                            isLeaf = false;
                            var childNodeIds = m_compilation.getInstructionStackPoppedNodes(pushInstr);
                            (leftChildId, rightChildId) = (childNodeIds[0], childNodeIds[1]);
                        }
                    }
                }

                if (isLeaf) {
                    _leafNodeIds.add(node.id);
                }
                else {
                    collectLeafNodes(leftChildId, ref _leafNodeIds);
                    collectLeafNodes(rightChildId, ref _leafNodeIds);
                }
            }

            Span<int> leafNodeIdsSpan = leafNodeIds.asSpan();
            Debug.Assert(leafNodeIdsSpan.Length >= 2);

            if (leafNodeIdsSpan.Length <= 4) {
                // Use a specialized concatenation method for a small number of arguments.
                _emitStringConcatTreeSpecial(leafNodeIdsSpan);
                return;
            }

            // If a specialized method is not available, use the general concatenation
            // method which requires allocating an array containing the arguments.

            var strArrayLocal = m_ilBuilder.acquireTempLocal(typeof(string[]));
            var strTempLocal = m_ilBuilder.acquireTempLocal(typeof(string));

            m_ilBuilder.emit(ILOp.ldc_i4, leafNodeIdsSpan.Length);
            m_ilBuilder.emit(ILOp.newarr, typeof(string));
            m_ilBuilder.emit(ILOp.stloc, strArrayLocal);

            for (int i = leafNodeIdsSpan.Length - 1; i >= 0; i--) {
                ref DataNode leafNode = ref m_compilation.getDataNode(leafNodeIdsSpan[i]);

                // Don't emit string conversions for string arguments because the concatenation
                // helper does the null to "null" conversion.
                if (!isStringOrNull(_getPushedTypeOfNode(leafNode)))
                    _emitTypeCoerceForTopOfStack(leafNodeIdsSpan[i], DataNodeType.STRING, useConvertStr: true);

                m_ilBuilder.emit(ILOp.stloc, strTempLocal);
                m_ilBuilder.emit(ILOp.ldloc, strArrayLocal);
                m_ilBuilder.emit(ILOp.ldc_i4, i);
                m_ilBuilder.emit(ILOp.ldloc, strTempLocal);
                m_ilBuilder.emit(ILOp.stelem_ref);
            }

            m_ilBuilder.emit(ILOp.ldloc, strArrayLocal);
            m_ilBuilder.emit(ILOp.call, KnownMembers.stringAddArray, 0);

            m_ilBuilder.releaseTempLocal(strArrayLocal);
            m_ilBuilder.releaseTempLocal(strTempLocal);
        }

        private void _emitStringConcatTreeSpecial(ReadOnlySpan<int> argsOnStack) {
            Debug.Assert(argsOnStack.Length <= 4);

            // Find the deepest non-string value on the stack.

            int heightOfFirstNonString = -1;
            for (int i = 0; i < argsOnStack.Length; i++) {
                ref DataNode node = ref m_compilation.getDataNode(argsOnStack[i]);
                DataNodeType pushedType = _getPushedTypeOfNode(node);

                if (!isStringOrNull(pushedType)) {
                    heightOfFirstNonString = i;
                    break;
                }
            }

            // Convert any non-string values to strings.

            if (heightOfFirstNonString != -1) {
                int nStashVarsRequired = argsOnStack.Length - heightOfFirstNonString - 1;

                m_tempLocalArray.clearAndAddUninitialized(nStashVarsRequired);
                var stashVars = m_tempLocalArray.asSpan();

                for (int i = 0; i < stashVars.Length; i++) {
                    ref DataNode node = ref m_compilation.getDataNode(argsOnStack[argsOnStack.Length - i - 1]);

                    // Don't emit string conversions for string arguments because the concatenation
                    // helper does the null to "null" conversion.
                    if (!isStringOrNull(_getPushedTypeOfNode(node)))
                        _emitTypeCoerceForTopOfStack(node, DataNodeType.STRING, useConvertStr: true);

                    var stashLocal = m_ilBuilder.acquireTempLocal(typeof(string));
                    m_ilBuilder.emit(ILOp.stloc, stashLocal);

                    stashVars[stashVars.Length - i - 1] = stashLocal;
                }

                _emitTypeCoerceForTopOfStack(argsOnStack[heightOfFirstNonString], DataNodeType.STRING, useConvertStr: true);

                for (int i = 0; i < stashVars.Length; i++) {
                    m_ilBuilder.emit(ILOp.ldloc, stashVars[i]);
                    m_ilBuilder.releaseTempLocal(stashVars[i]);
                }
            }

            m_ilBuilder.emit(ILOp.call, argsOnStack.Length switch {
                2 => KnownMembers.stringAdd2,
                3 => KnownMembers.stringAdd3,
                4 => KnownMembers.stringAdd4
            });
        }

        /// <summary>
        /// Emits the IL for a recognized charAt or charCodeAt comparison pattern.
        /// </summary>
        /// <param name="instr">A reference to the <see cref="Instruction"/> representing the
        /// compare instruction.</param>
        /// <param name="left">A reference to the <see cref="DataNode"/> representing the left
        /// operand of the comparison.</param>
        /// <param name="right">A reference to the <see cref="DataNode"/> representing the right
        /// operand of the comparison.</param>
        /// <param name="branchEmitInfo">A <see cref="TwoWayBranchEmitInfo"/> instance, only applicable
        /// if <paramref name="instr"/> is a compare-and-branch instruction.</param>
        private void _emitStringCharAtIntrinsicCompare(
            in Instruction instr, in DataNode left, in DataNode right, in TwoWayBranchEmitInfo branchEmitInfo)
        {
            bool isBranch = ABCOpInfo.getInfo(instr.opcode).controlType == ABCOpInfo.ControlType.BRANCH;

            bool isNegative =
                instr.opcode == ABCOp.ifne
                || instr.opcode == ABCOp.ifstrictne
                || ((int)instr.opcode >= (int)ABCOp.ifnlt && (int)instr.opcode <= (int)ABCOp.ifnge);

            bool isLeftComparand = ComparisonType.STR_CHARAT_L
                == (isBranch ? instr.data.compareBranch.compareType : instr.data.compare.compareType);

            var effectiveOp = (instr.opcode, isLeftComparand) switch {
                (ABCOp.equals or ABCOp.ifeq or ABCOp.ifne, _) => ABCOp.equals,
                (ABCOp.ifstricteq or ABCOp.ifstrictne or ABCOp.strictequals, _) => ABCOp.equals,
                (ABCOp.lessthan or ABCOp.iflt or ABCOp.ifnlt, false) => ABCOp.lessthan,
                (ABCOp.lessthan or ABCOp.iflt or ABCOp.ifnlt, true) => ABCOp.greaterthan,
                (ABCOp.lessequals or ABCOp.ifle or ABCOp.ifnle, false) => ABCOp.lessequals,
                (ABCOp.lessequals or ABCOp.ifle or ABCOp.ifnle, true) => ABCOp.greaterequals,
                (ABCOp.greaterthan or ABCOp.ifgt or ABCOp.ifngt, false) => ABCOp.greaterthan,
                (ABCOp.greaterthan or ABCOp.ifgt or ABCOp.ifngt, true) => ABCOp.lessthan,
                (ABCOp.greaterequals or ABCOp.ifge or ABCOp.ifnge, false) => ABCOp.greaterequals,
                (ABCOp.greaterequals or ABCOp.ifge or ABCOp.ifnge, true) => ABCOp.lessequals,
            };

            ref readonly DataNode comparandNode = ref (isLeftComparand ? ref left : ref right);

            // If the comparand is of a numeric type, then this is a charCodeAt compare pattern.
            // Otherwise it is a string constant and this is a charAt compare pattern.
            bool isCharCodeAt = isNumeric(comparandNode.dataType);

            bool isComparandConstant = false;
            int comparandConstValue = 0;

            if (isCharCodeAt) {
                isComparandConstant = tryGetConstant(comparandNode, out comparandConstValue);
            }
            else {
                Debug.Assert(
                    comparandNode.isConstant
                    && comparandNode.dataType == DataNodeType.STRING
                    && comparandNode.constant.stringValue.Length == 1
                );
                isComparandConstant = true;
                comparandConstValue = comparandNode.constant.stringValue[0];
            }

            // If the index is out of the string bounds, the result of the comparison is false if:
            // This is a charCodeAt compare pattern (since the result is NaN, for which all compare
            // operations return false)
            // Or this is a charAt compare pattern and the operation is not lessthan or lessequals
            // (charAt returns an empty string, which compares less than a single-character string)

            bool outOfBoundsFail = isCharCodeAt
                || effectiveOp == ABCOp.equals || effectiveOp == ABCOp.greaterthan || effectiveOp == ABCOp.greaterequals;

            if (isNegative)
                outOfBoundsFail = !outOfBoundsFail;

            var strTemp = m_ilBuilder.acquireTempLocal(typeof(string));
            var indexTemp = m_ilBuilder.acquireTempLocal(typeof(int));

            // If the comparand is a non-constant integer in a charCodeAt compare intrinsic, we need another
            // temporary variable for it.
            ILBuilder.Local comparandTemp = default;
            if (!isComparandConstant)
                comparandTemp = m_ilBuilder.acquireTempLocal(typeof(int));

            if (!isLeftComparand) {
                if (isComparandConstant)
                    _emitDiscardTopOfStack(right);
                else
                    m_ilBuilder.emit(ILOp.stloc, comparandTemp);
            }

            m_ilBuilder.emit(ILOp.stloc, indexTemp);
            m_ilBuilder.emit(ILOp.stloc, strTemp);

            if (isLeftComparand) {
                if (isComparandConstant)
                    _emitDiscardTopOfStack(left);
                else
                    m_ilBuilder.emit(ILOp.stloc, comparandTemp);
            }

            m_ilBuilder.emit(ILOp.ldloc, indexTemp);
            m_ilBuilder.emit(ILOp.ldloc, strTemp);
            m_ilBuilder.emit(ILOp.call, KnownMembers.strGetLength, 0);

            if (isBranch) {
                m_ilBuilder.emit(ILOp.blt_un_s, outOfBoundsFail ? branchEmitInfo.falseLabel : branchEmitInfo.trueLabel);

                m_ilBuilder.emit(ILOp.ldloc, strTemp);
                m_ilBuilder.emit(ILOp.ldloc, indexTemp);
                m_ilBuilder.emit(ILOp.call, KnownMembers.strCharAtNative, -1);

                if (isComparandConstant)
                    m_ilBuilder.emit(ILOp.ldc_i4, comparandConstValue);
                else
                    m_ilBuilder.emit(ILOp.ldloc, comparandTemp);

                (ILOp ilOp, ILOp invIlOp) = effectiveOp switch {
                    ABCOp.equals => (ILOp.beq, ILOp.bne_un),
                    ABCOp.lessthan => (ILOp.blt, ILOp.bge),
                    ABCOp.greaterequals => (ILOp.bge, ILOp.blt),
                    ABCOp.greaterthan => (ILOp.bgt, ILOp.ble),
                    ABCOp.lessequals => (ILOp.ble, ILOp.bgt)
                };

                if (isNegative)
                    (ilOp, invIlOp) = (invIlOp, ilOp);

                if (branchEmitInfo.trueIsFallThrough)
                    m_ilBuilder.emit(invIlOp, branchEmitInfo.falseLabel);
                else
                    m_ilBuilder.emit(ilOp, branchEmitInfo.trueLabel);
            }
            else {
                var lengthCheckLabel = m_ilBuilder.createLabel();
                var endLabel = m_ilBuilder.createLabel();

                m_ilBuilder.emit(ILOp.blt_un_s, lengthCheckLabel);
                m_ilBuilder.emit(ILOp.ldc_i4, outOfBoundsFail ? 0 : 1);
                m_ilBuilder.emit(ILOp.br_s, endLabel);

                m_ilBuilder.markLabel(lengthCheckLabel);

                m_ilBuilder.emit(ILOp.ldloc, strTemp);
                m_ilBuilder.emit(ILOp.ldloc, indexTemp);
                m_ilBuilder.emit(ILOp.call, KnownMembers.strCharAtNative, -1);

                if (isComparandConstant)
                    m_ilBuilder.emit(ILOp.ldc_i4, comparandConstValue);
                else
                    m_ilBuilder.emit(ILOp.ldloc, comparandTemp);

                switch (effectiveOp) {
                    case ABCOp.equals:
                        m_ilBuilder.emit(ILOp.ceq);
                        break;
                    case ABCOp.lessthan:
                    case ABCOp.greaterequals:
                        m_ilBuilder.emit(ILOp.clt);
                        isNegative = effectiveOp == ABCOp.greaterequals;
                        break;
                    case ABCOp.greaterthan:
                    case ABCOp.lessequals:
                        m_ilBuilder.emit(ILOp.cgt);
                        isNegative = effectiveOp == ABCOp.lessequals;
                        break;
                }

                if (isNegative) {
                    m_ilBuilder.emit(ILOp.ldc_i4_0);
                    m_ilBuilder.emit(ILOp.ceq);
                }
                m_ilBuilder.markLabel(endLabel);
            }

            m_ilBuilder.releaseTempLocal(strTemp);
            m_ilBuilder.releaseTempLocal(indexTemp);

            if (!isComparandConstant)
                m_ilBuilder.releaseTempLocal(comparandTemp);
        }

        /// <summary>
        /// Emits IL for invoking an intrinsic function.
        /// </summary>
        /// <param name="intrinsic">An <see cref="Intrinsic"/> representing the intrinsic function.</param>
        /// <param name="objectId">The node id of the receiver on the stack, -1 if no receiver present.</param>
        /// <param name="argsOnStack">A span containing the node ids of the arguments on the stack.</param>
        /// <param name="isConstruct">True if the function is being invoked as a constructor.</param>
        /// <param name="resultId">The node id for the return value on the stack, -1 if no return value
        /// is to be pushed.</param>
        private void _emitInvokeIntrinsic(
            Intrinsic intrinsic, int objectId, ReadOnlySpan<int> argsOnStack, bool isConstruct, int resultId)
        {
            bool hasResult = resultId != -1;

            if (hasResult) {
                // Check if the result is a constant.
                ref DataNode result = ref m_compilation.getDataNode(resultId);
                if (result.isConstant) {
                    for (int i = argsOnStack.Length - 1; i >= 0; i--)
                        _emitDiscardTopOfStack(argsOnStack[i]);

                    if (objectId != -1)
                        _emitDiscardTopOfStack(objectId);

                    _emitPushConstantNode(result);
                    return;
                }
            }
            else {
                // If all arguments are marked as no-push and the return value is to
                // be discarded, don't emit anything.

                bool areAllArgsNotPushed = true;
                for (int i = 0; i < argsOnStack.Length && areAllArgsNotPushed; i++)
                    areAllArgsNotPushed &= m_compilation.getDataNode(argsOnStack[i]).isNotPushed;

                if (areAllArgsNotPushed) {
                    if (objectId != -1)
                        _emitDiscardTopOfStack(objectId);
                    return;
                }
            }

            if (intrinsic.name == IntrinsicName.VECTOR_T_PUSH_1) {
                Debug.Assert(objectId != -1 && !isConstruct);

                var mdContext = m_compilation.metadataContext;
                var vectorClass = (Class)intrinsic.arg!;

                EntityHandle vectorTypeHandle;
                using (var lockedContext = m_compilation.getContext())
                    vectorTypeHandle = lockedContext.value.getEntityHandle(vectorClass);

                ref DataNode arg = ref m_compilation.getDataNode(argsOnStack[0]);
                _emitTypeCoerceForTopOfStack(arg, vectorClass.vectorElementType);
                m_ilBuilder.emit(ILOp.callvirt, mdContext.getMemberHandle(KnownMembers.vectorPushOneArg, vectorTypeHandle), -1);

                if (!hasResult)
                    m_ilBuilder.emit(ILOp.pop);
                return;
            }

            if (intrinsic.name == IntrinsicName.ARRAY_PUSH_1) {
                Debug.Assert(objectId != -1 && !isConstruct);

                _emitTypeCoerceForTopOfStack(argsOnStack[0], DataNodeType.ANY);
                m_ilBuilder.emit(ILOp.callvirt, KnownMembers.arrayPushOneArg, -1);

                if (!hasResult)
                    m_ilBuilder.emit(ILOp.pop);
                return;
            }

            if (intrinsic.name == IntrinsicName.STRING_CHARAT || intrinsic.name == IntrinsicName.STRING_CCODEAT) {
                Debug.Assert(objectId != -1 && !isConstruct);
                _emitTypeCoerceForTopOfStack(argsOnStack[0], DataNodeType.NUMBER);

                MethodInfo method = (intrinsic.name == IntrinsicName.STRING_CHARAT)
                    ? KnownMembers.strCharAt
                    : KnownMembers.strCharCodeAt;

                m_ilBuilder.emit(ILOp.call, method, -1);
                if (!hasResult)
                    m_ilBuilder.emit(ILOp.pop);
                return;
            }

            if (intrinsic.name == IntrinsicName.STRING_CHARAT_I || intrinsic.name == IntrinsicName.STRING_CCODEAT_I) {
                Debug.Assert(objectId != -1 && !isConstruct);
                Debug.Assert(isInteger(_getPushedTypeOfNode(m_compilation.getDataNode(argsOnStack[0]))));

                MethodInfo method = (intrinsic.name == IntrinsicName.STRING_CHARAT_I)
                    ? KnownMembers.strCharAtIntIndex
                    : KnownMembers.strCharCodeAtIntIndex;

                m_ilBuilder.emit(ILOp.call, method, -1);
                if (!hasResult)
                    m_ilBuilder.emit(ILOp.pop);
                return;
            }

            if (intrinsic.name == IntrinsicName.STRING_CHARAT_CMP || intrinsic.name == IntrinsicName.STRING_CCODEAT_CMP) {
                Debug.Assert(objectId != -1 && !isConstruct);
                Debug.Assert(isInteger(_getPushedTypeOfNode(m_compilation.getDataNode(argsOnStack[0]))));

                // This intrinsic is used when the result of String.charAt() or charCodeAt() is used in
                // a recognized comparison expression. In this case the actual code generation will be done
                // when emitting the code for the compare instruction.
                return;
            }

            if (intrinsic.name == IntrinsicName.STRING_CCODEAT_I_I) {
                // This is a specialized intrinsic for String::charCodeAt when the index is an integer and
                // the result is coerced to an integer, which is implemented using inline IL.
                // It returns the value 0 when the index is out of bounds.
                // The CLR JIT should (hopefully) detect the the length check here and remove the internal
                // length check from the string indexer.

                Debug.Assert(objectId != -1 && !isConstruct);
                Debug.Assert(isInteger(_getPushedTypeOfNode(m_compilation.getDataNode(argsOnStack[0]))));

                var strTemp = m_ilBuilder.acquireTempLocal(typeof(string));
                var indexTemp = m_ilBuilder.acquireTempLocal(typeof(int));
                var label1 = m_ilBuilder.createLabel();
                var label2 = m_ilBuilder.createLabel();

                m_ilBuilder.emit(ILOp.stloc, indexTemp);
                m_ilBuilder.emit(ILOp.stloc, strTemp);

                m_ilBuilder.emit(ILOp.ldloc, indexTemp);
                m_ilBuilder.emit(ILOp.ldloc, strTemp);
                m_ilBuilder.emit(ILOp.call, KnownMembers.strGetLength, 0);
                m_ilBuilder.emit(ILOp.blt_un_s, label1);

                m_ilBuilder.emit(ILOp.ldc_i4_0);
                m_ilBuilder.emit(ILOp.br_s, label2);

                m_ilBuilder.markLabel(label1);
                m_ilBuilder.emit(ILOp.ldloc, strTemp);
                m_ilBuilder.emit(ILOp.ldloc, indexTemp);
                m_ilBuilder.emit(ILOp.call, KnownMembers.strCharAtNative, -1);

                m_ilBuilder.markLabel(label2);
                m_ilBuilder.releaseTempLocal(strTemp);
                m_ilBuilder.releaseTempLocal(indexTemp);
                return;
            }

            // All other intrinsics are static.

            if (argsOnStack.Length == 0) {
                if (objectId != -1)
                    _emitDiscardTopOfStack(objectId);

                switch (intrinsic.name) {
                    case IntrinsicName.INT_NEW_0:
                    case IntrinsicName.UINT_NEW_0:
                    case IntrinsicName.BOOLEAN_NEW_0:
                        m_ilBuilder.emit(ILOp.ldc_i4_0);
                        break;
                    case IntrinsicName.NUMBER_NEW_0:
                        m_ilBuilder.emit(ILOp.ldc_i4_0);
                        m_ilBuilder.emit(ILOp.conv_r8);
                        break;
                    case IntrinsicName.STRING_NEW_0:
                        m_ilBuilder.emit(ILOp.ldstr, "");
                        break;
                    case IntrinsicName.OBJECT_NEW_0:
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.objectCtor, 1);
                        break;
                    case IntrinsicName.ARRAY_NEW_0:
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.arrayCtorWithNoArgs, 1);
                        break;
                    case IntrinsicName.VECTOR_ANY_CTOR:
                        m_ilBuilder.emit(ILOp.ldc_i4_0);
                        m_ilBuilder.emit(ILOp.ldc_i4_0);
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.vectorObjectCtor, -1);
                        break;
                    case IntrinsicName.NAMESPACE_NEW_0:
                        _emitPushXmlNamespaceConstant(Namespace.@public);
                        break;
                    case IntrinsicName.XML_NEW_0:
                        m_ilBuilder.emit(ILOp.ldnull);
                        m_ilBuilder.emit(ILOp.call, KnownMembers.xmlParse, 0);
                        break;
                    case IntrinsicName.XMLLIST_NEW_0:
                        m_ilBuilder.emit(ILOp.call, KnownMembers.xmlListCtorEmpty, 1);
                        break;
                    case IntrinsicName.DATE_NEW_0:
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.dateCtorNoArgs, 1);
                        break;
                    case IntrinsicName.DATE_CALL_0:
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.dateCtorNoArgs, 1);
                        m_ilBuilder.emit(ILOp.callvirt, KnownMembers.dateToString, 0);
                        break;
                    case IntrinsicName.REGEXP_NEW_PATTERN:
                        m_ilBuilder.emit(ILOp.ldstr, "");
                        m_ilBuilder.emit(ILOp.dup);
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.regexpCtorWithPattern, -1);
                        break;
                }

                if (!hasResult)
                    m_ilBuilder.emit(ILOp.pop);

                return;
            }

            ref DataNode arg1 = ref m_compilation.getDataNode(argsOnStack[0]);

            switch (intrinsic.name) {
                case IntrinsicName.INT_NEW_1:
                    _emitTypeCoerceForTopOfStack(arg1, DataNodeType.INT);
                    break;
                case IntrinsicName.UINT_NEW_1:
                    _emitTypeCoerceForTopOfStack(arg1, DataNodeType.UINT);
                    break;
                case IntrinsicName.NUMBER_NEW_1:
                    _emitTypeCoerceForTopOfStack(arg1, DataNodeType.NUMBER);
                    break;
                case IntrinsicName.STRING_NEW_1:
                    _emitTypeCoerceForTopOfStack(arg1, DataNodeType.STRING, useConvertStr: true);
                    break;
                case IntrinsicName.BOOLEAN_NEW_1:
                    _emitTypeCoerceForTopOfStack(arg1, DataNodeType.BOOL);
                    break;
                case IntrinsicName.ARRAY_NEW_1_LEN:
                    _emitTypeCoerceForTopOfStack(arg1, DataNodeType.INT);
                    m_ilBuilder.emit(ILOp.newobj, KnownMembers.arrayCtorWithLength, 0);
                    break;

                case IntrinsicName.DATE_NEW_1:
                    if (arg1.dataType == DataNodeType.STRING) {
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.dateCtorFromString, 0);
                    }
                    else {
                        _emitTypeCoerceForTopOfStack(arg1, DataNodeType.NUMBER);
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.dateCtorFromValue, 0);
                    }
                    break;

                case IntrinsicName.NAMESPACE_NEW_1:
                    if (arg1.dataType == DataNodeType.STRING) {
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.xmlNsCtorFromURI, 0);
                    }
                    else if (arg1.dataType == DataNodeType.NAMESPACE) {
                        // No-op
                        Debug.Assert(arg1.isNotNull);
                    }
                    else {
                        Debug.Assert(false, "Namespace intrinsic constructor: Argument must be string or Namespace");
                    }
                    break;

                case IntrinsicName.QNAME_NEW_1:
                    if (arg1.dataType == DataNodeType.STRING) {
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.xmlQnameCtorFromLocalName, 0);
                    }
                    else if (arg1.dataType == DataNodeType.QNAME) {
                        // No-op
                        Debug.Assert(arg1.isNotNull);
                    }
                    else {
                        Debug.Assert(false, "QName intrinsic constructor: Argument must be string or QName");
                    }
                    break;

                case IntrinsicName.XML_CALL_1:
                case IntrinsicName.XML_NEW_1:
                {
                    if (isStringOrNull(arg1.dataType)) {
                        m_ilBuilder.emit(ILOp.call, KnownMembers.xmlParse, 0);
                        break;
                    }

                    Debug.Assert(arg1.dataType == DataNodeType.OBJECT);

                    bool construct = intrinsic.name == IntrinsicName.XML_NEW_1;
                    ClassTag argClassTag = arg1.constant.classValue.tag;

                    Debug.Assert(argClassTag == ClassTag.XML || argClassTag == ClassTag.XML_LIST);

                    if (argClassTag == ClassTag.XML_LIST) {
                        m_ilBuilder.emit(ILOp.ldc_i4, construct ? 1 : 0);
                        m_ilBuilder.emit(ILOp.call, KnownMembers.xmlFromXmlList);
                    }
                    else if (construct) {
                        m_ilBuilder.emit(ILOp.call, KnownMembers.xmlFromXmlCopy, 0);
                    }
                    break;
                }

                case IntrinsicName.XMLLIST_CALL_1:
                case IntrinsicName.XMLLIST_NEW_1:
                {
                    if (isStringOrNull(arg1.dataType)) {
                        m_ilBuilder.emit(ILOp.call, KnownMembers.xmlListParse, 0);
                        break;
                    }

                    Debug.Assert(arg1.dataType == DataNodeType.OBJECT);

                    bool construct = intrinsic.name == IntrinsicName.XMLLIST_NEW_1;
                    ClassTag argClassTag = arg1.constant.classValue.tag;

                    Debug.Assert(argClassTag == ClassTag.XML || argClassTag == ClassTag.XML_LIST);

                    if (argClassTag == ClassTag.XML)
                        m_ilBuilder.emit(ILOp.call, KnownMembers.xmlListFromXml, 0);
                    else if (construct)
                        m_ilBuilder.emit(ILOp.call, KnownMembers.xmlListFromXmlListCopy, 0);

                    break;
                }

                case IntrinsicName.VECTOR_ANY_CALL_1: {
                    _emitTypeCoerceForTopOfStack(arg1, DataNodeType.OBJECT);
                    m_ilBuilder.emit(ILOp.call, KnownMembers.vecAnyFromObject, 0);
                    break;
                }

                case IntrinsicName.VECTOR_T_CALL_1: {
                    _emitTypeCoerceForTopOfStack(arg1, DataNodeType.OBJECT);

                    MetadataContext mdContext = m_compilation.metadataContext;
                    EntityHandle vectorTypeHandle;
                    using (var lockedContext = m_compilation.getContext())
                        vectorTypeHandle = lockedContext.value.getEntityHandle((Class)intrinsic.arg!);

                    m_ilBuilder.emit(ILOp.call, mdContext.getMemberHandle(KnownMembers.vectorFromObject, vectorTypeHandle), 0);

                    break;
                }

                case IntrinsicName.DATE_NEW_7:
                    _emitPrepareMethodCallArguments(
                        s_dateCtorComponentsParams,
                        hasRest: false,
                        argsOnStack,
                        receiverType: null,
                        receiverExpectedType: null
                    );
                    m_ilBuilder.emit(ILOp.ldc_i4_0);    // isUTC = false
                    m_ilBuilder.emit(ILOp.newobj, KnownMembers.dateCtorFromComponents, -7);
                    break;

                case IntrinsicName.ARRAY_NEW: {
                    if (argsOnStack.Length > 0) {
                        var argsLocal = _emitCollectStackArgsIntoArray(argsOnStack);
                        m_ilBuilder.emit(ILOp.ldloc, argsLocal);
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.roSpanOfAnyFromArray, 0);
                        m_ilBuilder.releaseTempLocal(argsLocal);
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.arrayCtorWithSpan, 0);
                    }
                    else {
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.arrayCtorWithNoArgs, 1);
                    }
                    break;
                }

                case IntrinsicName.MATH_MAX_2:
                case IntrinsicName.MATH_MIN_2:
                {
                    ref DataNode arg2 = ref m_compilation.getDataNode(argsOnStack[1]);
                    _emitTypeCoerceForStackTop2(arg1, arg2, DataNodeType.NUMBER, DataNodeType.NUMBER);
                    MethodInfo method = (intrinsic.name == IntrinsicName.MATH_MAX_2) ? KnownMembers.mathMax2D : KnownMembers.mathMin2D;
                    m_ilBuilder.emit(ILOp.call, method, -1);
                    break;
                }

                case IntrinsicName.MATH_MAX_2_I:
                case IntrinsicName.MATH_MIN_2_I:
                {
                    ref DataNode arg2 = ref m_compilation.getDataNode(argsOnStack[1]);
                    _emitTypeCoerceForStackTop2(arg1, arg2, DataNodeType.INT, DataNodeType.INT);
                    MethodInfo method = (intrinsic.name == IntrinsicName.MATH_MAX_2_I) ? KnownMembers.mathMax2I : KnownMembers.mathMin2I;
                    m_ilBuilder.emit(ILOp.call, method, -1);
                    break;
                }

                case IntrinsicName.MATH_MAX_2_U:
                case IntrinsicName.MATH_MIN_2_U:
                {
                    ref DataNode arg2 = ref m_compilation.getDataNode(argsOnStack[1]);
                    _emitTypeCoerceForStackTop2(arg1, arg2, DataNodeType.UINT, DataNodeType.UINT);
                    MethodInfo method = (intrinsic.name == IntrinsicName.MATH_MAX_2_U) ? KnownMembers.mathMax2U : KnownMembers.mathMin2U;
                    m_ilBuilder.emit(ILOp.call, method, -1);
                    break;
                }

                case IntrinsicName.NAMESPACE_NEW_2:
                    m_ilBuilder.emit(ILOp.newobj, KnownMembers.xmlNsCtorFromPrefixAndURI, -1);
                    break;

                case IntrinsicName.QNAME_NEW_2:
                    if (arg1.dataType == DataNodeType.NAMESPACE)
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.xmlQnameCtorFromNsAndLocal, -1);
                    else
                        m_ilBuilder.emit(ILOp.newobj, KnownMembers.xmlQnameCtorFromUriAndLocal, -1);
                    break;

                case IntrinsicName.REGEXP_CALL_RE:
                    // No-op
                    break;

                case IntrinsicName.REGEXP_NEW_RE:
                    m_ilBuilder.emit(ILOp.newobj, KnownMembers.regexpCtorWithRegexp, 0);
                    break;

                case IntrinsicName.REGEXP_NEW_PATTERN:
                    if (argsOnStack.Length == 1)
                        m_ilBuilder.emit(ILOp.ldstr, "");
                    m_ilBuilder.emit(ILOp.newobj, KnownMembers.regexpCtorWithPattern, -1);
                    break;

                case IntrinsicName.REGEXP_NEW_CONST: {
                    Debug.Assert(!arg1.isNotPushed);

                    string pattern = arg1.constant.stringValue;
                    string flags;

                    if (argsOnStack.Length == 2) {
                        ref DataNode arg2 = ref m_compilation.getDataNode(argsOnStack[1]);
                        Debug.Assert(!arg2.isNotPushed);
                        flags = arg2.constant.stringValue;
                    }
                    else {
                        flags = "";
                        m_ilBuilder.emit(ILOp.ldstr, flags);
                    }

                    using var lockedContext = m_compilation.getContext();
                    var emitConstData = lockedContext.value.emitConstData;
                    int constId = emitConstData.addRegExp(pattern, flags);

                    m_ilBuilder.emit(ILOp.ldsfld, emitConstData.regexpArrayFieldHandle);
                    m_ilBuilder.emit(ILOp.ldc_i4, constId);
                    m_ilBuilder.emit(ILOp.ldelema, typeof(ASRegExp));
                    m_ilBuilder.emit(ILOp.call, KnownMembers.regexpLazyConstruct, -2);

                    break;
                }
            }

            // Remove the receiver object from the stack if there is any.
            if (!hasResult) {
                m_ilBuilder.emit(ILOp.pop);
                if (objectId != -1)
                    _emitDiscardTopOfStack(objectId);
            }
            else if (objectId != -1 && !m_compilation.getDataNode(objectId).isNotPushed) {
                var resultStash = _emitStashTopOfStack(m_compilation.getDataNode(resultId), usePrePushType: true);
                m_ilBuilder.emit(ILOp.pop);
                _emitUnstash(resultStash);
            }
        }

        /// <summary>
        /// Emits code for capturing the current scope stack.
        /// </summary>
        /// <param name="capturedScope">The <see cref="CapturedScope"/> instance representing the definition
        /// of the captured scope that needs to be created.</param>
        /// <param name="localScopeNodeIds">A span containing the scope stack node ids representing the
        /// local scope stack to be captured.</param>
        /// <returns>An instance of <see cref="ILBuilder.Local"/> representing a temporary variable
        /// holding the created container instance for <paramref name="capturedScope"/>. This must be
        /// released with <see cref="ILBuilder.releaseTempLocal"/> after use.</returns>
        private ILBuilder.Local _emitCaptureCurrentScope(CapturedScope capturedScope, ReadOnlySpan<int> localScopeNodeIds) {
            var captureItems = capturedScope.getItems(isStatic: true).asSpan();
            var captureContainer = capturedScope.container;

            int heightOfFirstLocalItem = m_compilation.getCapturedScopeItems().length;
            Debug.Assert(heightOfFirstLocalItem + localScopeNodeIds.Length == captureItems.Length);

            var newScopeLocal = m_ilBuilder.acquireTempLocal(TypeSignature.forClassType(captureContainer.typeHandle));
            m_ilBuilder.emit(ILOp.newobj, captureContainer.ctorHandle, 1);
            m_ilBuilder.emit(ILOp.stloc, newScopeLocal);

            for (int i = 0; i < captureItems.Length; i++) {
                if (isConstantType(captureItems[i].dataType))
                    continue;

                m_ilBuilder.emit(ILOp.ldloc, newScopeLocal);

                if (i >= heightOfFirstLocalItem)
                    _emitLoadScopeOrLocalNode(m_compilation.getDataNode(localScopeNodeIds[i - heightOfFirstLocalItem]));
                else
                    _emitPushCapturedScopeItem(i);

                m_ilBuilder.emit(ILOp.stfld, captureContainer.getFieldHandle(i));
            }

            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_RUNTIME_SCOPE_STACK)) {
                m_ilBuilder.emit(ILOp.ldloc, newScopeLocal);
                m_ilBuilder.emit(ILOp.ldloc, m_rtScopeStackLocal);
                m_ilBuilder.emit(ILOp.call, KnownMembers.rtScopeStackClone, 0);
                m_ilBuilder.emit(ILOp.stfld, captureContainer.rtStackFieldHandle);
            }

            if (capturedScope.capturesDxns) {
                m_ilBuilder.emit(ILOp.ldloc, newScopeLocal);

                if (m_compilation.isAnyFlagSet(MethodCompilationFlags.SETS_DXNS)) {
                    m_ilBuilder.emit(ILOp.call, KnownMembers.getDxns, 1);
                }
                else if (m_compilation.capturedScope != null && m_compilation.capturedScope.capturesDxns) {
                    _emitPushCapturedScope();
                    m_ilBuilder.emit(ILOp.ldfld, m_compilation.capturedScope.container.dxnsFieldHandle);
                }
                else {
                    m_ilBuilder.emit(ILOp.ldnull);
                }

                m_ilBuilder.emit(ILOp.stfld, captureContainer.dxnsFieldHandle);
            }

            return newScopeLocal;
        }

        /// <summary>
        /// Emits an unconditional jump from the given basic block to the given target block.
        /// </summary>
        /// <param name="fromBlock">A reference to a <see cref="BasicBlock"/> representing
        /// the block from which the jump is to be emitted.</param>
        /// <param name="toBlock">A reference to a <see cref="BasicBlock"/> representing the
        /// target of the jump.</param>
        private void _emitJumpFromBasicBlock(in BasicBlock fromBlock, in BasicBlock toBlock) {
            _emitBlockTransition(fromBlock, toBlock);

            if (!_isBasicBlockImmediatelyBefore(fromBlock, toBlock))
                m_ilBuilder.emit(ILOp.br, _getLabelForJumpToBlock(fromBlock, toBlock));
        }

        /// <summary>
        /// Returns a <see cref="TwoWayBranchEmitInfo"/> containing information on how a
        /// two-way conditional branch is to be emitted.
        /// </summary>
        /// <param name="fromBlock">A reference to a <see cref="BasicBlock"/> representing the
        /// basic block that exits with the two-way branch.</param>
        /// <param name="trueBlock">A reference to a <see cref="BasicBlock"/> representing the
        /// basic block that is the target of the true branch.</param>
        /// <param name="falseBlock">A reference to a <see cref="BasicBlock"/> representing the
        /// basic block that is the target of the false branch.</param>
        /// <returns>A <see cref="TwoWayBranchEmitInfo"/> instance.</returns>
        private TwoWayBranchEmitInfo _getTwoWayBranchEmitInfo(
            in BasicBlock fromBlock, in BasicBlock trueBlock, in BasicBlock falseBlock)
        {
            TwoWayBranchEmitInfo emitInfo = default;

            emitInfo.trueBlockId = trueBlock.id;
            emitInfo.falseBlockId = falseBlock.id;

            emitInfo.trueBlockNeedsTransition = _isBlockTransitionRequired(fromBlock, trueBlock);
            emitInfo.falseBlockNeedsTransition = _isBlockTransitionRequired(fromBlock, falseBlock);

            if (!emitInfo.trueBlockNeedsTransition && !emitInfo.falseBlockNeedsTransition) {
                emitInfo.trueLabel = _getLabelForJumpToBlock(fromBlock, trueBlock);
                emitInfo.falseLabel = _getLabelForJumpToBlock(fromBlock, falseBlock);
                emitInfo.trueIsFallThrough = _isBasicBlockImmediatelyBefore(fromBlock, trueBlock);
            }
            else if (emitInfo.trueBlockNeedsTransition && emitInfo.falseBlockNeedsTransition) {
                emitInfo.trueLabel = m_ilBuilder.createLabel();
                emitInfo.falseLabel = m_ilBuilder.createLabel();
            }
            else if (emitInfo.trueBlockNeedsTransition) {
                emitInfo.trueLabel = m_ilBuilder.createLabel();
                emitInfo.falseLabel = _getLabelForJumpToBlock(fromBlock, falseBlock);
                emitInfo.trueIsFallThrough = true;
            }
            else {
                emitInfo.trueLabel = _getLabelForJumpToBlock(fromBlock, trueBlock);
                emitInfo.falseLabel = m_ilBuilder.createLabel();
            }

            return emitInfo;
        }

        /// <summary>
        /// Emits the necessary code for a two-way conditional branch. This must be called after emitting
        /// the conditional branch instruction.
        /// </summary>
        /// <param name="fromBlock">A reference to a <see cref="BasicBlock"/> representing the
        /// basic block that exits with the two-way branch.</param>
        /// <param name="emitInfo">A <see cref="TwoWayBranchEmitInfo"/> obtained from a call to the
        /// <see cref="_getTwoWayBranchEmitInfo"/> method.</param>
        private void _finishTwoWayConditionalBranch(in BasicBlock fromBlock, in TwoWayBranchEmitInfo emitInfo) {
            ref BasicBlock trueBlock = ref m_compilation.getBasicBlock(emitInfo.trueBlockId);
            ref BasicBlock falseBlock = ref m_compilation.getBasicBlock(emitInfo.falseBlockId);

            if (!emitInfo.trueBlockNeedsTransition && !emitInfo.falseBlockNeedsTransition) {
                // No transitions required. If the fallthrough block is not immediately after the exiting one,
                // emit an unconditional jump to it.

                ref BasicBlock fallThroughBlock = ref (emitInfo.trueIsFallThrough ? ref trueBlock : ref falseBlock);
                var fallThroughLabel = emitInfo.trueIsFallThrough ? emitInfo.trueLabel : emitInfo.falseLabel;

                if (!_isBasicBlockImmediatelyBefore(fromBlock, fallThroughBlock))
                    m_ilBuilder.emit(ILOp.br, fallThroughLabel);
            }
            else {
                Debug.Assert(
                    emitInfo.trueIsFallThrough == (emitInfo.trueBlockNeedsTransition && !emitInfo.falseBlockNeedsTransition)
                );

                if (emitInfo.falseBlockNeedsTransition) {
                    m_ilBuilder.markLabel(emitInfo.falseLabel);
                    _emitBlockTransition(fromBlock, falseBlock);
                    m_ilBuilder.emit(ILOp.br, _getLabelForJumpToBlock(fromBlock, falseBlock));
                }
                if (emitInfo.trueBlockNeedsTransition) {
                    m_ilBuilder.markLabel(emitInfo.trueLabel);
                    _emitBlockTransition(fromBlock, trueBlock);
                    m_ilBuilder.emit(ILOp.br, _getLabelForJumpToBlock(fromBlock, trueBlock));
                }
            }
        }

        /// <summary>
        /// Returns the label for emitting a jump from one basic block to another.
        /// </summary>
        /// <param name="fromBlock">The <see cref="BasicBlock"/> instance representing the basic
        /// block from which the jump is to be done.</param>
        /// <param name="toBlock">The <see cref="BasicBlock"/> instance representing the basic
        /// block to which the jump is to be done.</param>
        /// <returns>An instance of <see cref="ILBuilder.Label"/> representing the label to
        /// be used when emitting the jump/branch/switch instruction.</returns>
        private ILBuilder.Label _getLabelForJumpToBlock(in BasicBlock fromBlock, in BasicBlock toBlock) {
            bool isBackward = fromBlock.postorderIndex <= toBlock.postorderIndex;
            ref BlockEmitInfo toBlockEmitInfo = ref m_blockEmitInfo[toBlock.id];
            return isBackward ? toBlockEmitInfo.backwardLabel : toBlockEmitInfo.forwardLabel;
        }

        /// <summary>
        /// Returns a value indicating whether any transition code needs to be emitted to
        /// ensure that stack, scope and local variable types are compatible when transferring
        /// control from on basic block to another
        /// </summary>
        /// <param name="fromBlock">A reference to a <see cref="BasicBlock"/> representing the
        /// basic block from which control is leaving.</param>
        /// <param name="toBlock">A reference to a <see cref="BasicBlock"/> representing the
        /// basic block from which control is being transferred to from <paramref name="fromBlock"/>.</param>
        /// <returns>True if transition code is required, otherwise false.</returns>
        private bool _isBlockTransitionRequired(in BasicBlock fromBlock, in BasicBlock toBlock) {
            if (fromBlock.postorderIndex <= toBlock.postorderIndex
                && m_compilation.staticIntArrayPool.getLength(toBlock.stackAtEntry) > 0)
            {
                // A backward jump with a non-empty stack definitely requires a transition.
                return true;
            }

            if ((toBlock.flags & BasicBlockFlags.DEFINES_PHI_NODES) == 0)
                return false;

            var dataNodes = m_compilation.getDataNodes();
            var phiSourceNodeIds = m_compilation.intArrayPool.getSpan(fromBlock.exitPhiSources);

            var entryStack = m_compilation.staticIntArrayPool.getSpan(toBlock.stackAtEntry);
            var entryScope = m_compilation.staticIntArrayPool.getSpan(toBlock.scopeStackAtEntry);
            var entryLocals = m_compilation.staticIntArrayPool.getSpan(toBlock.localsAtEntry);

            for (int i = 0; i < phiSourceNodeIds.Length; i++) {
                ref DataNode node = ref dataNodes[phiSourceNodeIds[i]];

                if (node.slot.kind == DataNodeSlotKind.STACK) {
                    int entryNodeId = entryStack[node.slot.id];
                    if (entryNodeId != node.id && !_isStackNodeCompatibleWithPhi(node, dataNodes[entryNodeId]))
                        return true;
                }
                else {
                    int entryNodeId = (node.slot.kind == DataNodeSlotKind.SCOPE)
                        ? entryScope[node.slot.id]
                        : entryLocals[node.slot.id];

                    if (entryNodeId != node.id && !_isScopeOrLocalNodeCompatibleWithPhi(node, dataNodes[entryNodeId]))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Emits the transition code to ensure that stack, scope and local variable types are
        /// compatible when transferring control from on basic block to another. This transition
        /// code should be emitted before the jump/branch/switch instruction that transfers
        /// control.
        /// </summary>
        /// <param name="fromBlock">A reference to a <see cref="BasicBlock"/> representing the
        /// basic block from which control is leaving.</param>
        /// <param name="toBlock">A reference to a <see cref="BasicBlock"/> representing the
        /// basic block from which control is being transferred to from <paramref name="fromBlock"/>.</param>
        private void _emitBlockTransition(in BasicBlock fromBlock, in BasicBlock toBlock) {
            bool isBackwardJumpWithNonEmptyStack =
                fromBlock.postorderIndex <= toBlock.postorderIndex
                && m_compilation.staticIntArrayPool.getLength(toBlock.stackAtEntry) != 0;

            if ((toBlock.flags & BasicBlockFlags.DEFINES_PHI_NODES) == 0 && !isBackwardJumpWithNonEmptyStack)
                return;

            var dataNodes = m_compilation.getDataNodes();
            var phiSourceNodeIds = m_compilation.intArrayPool.getSpan(fromBlock.exitPhiSources);

            var entryStack = m_compilation.staticIntArrayPool.getSpan(toBlock.stackAtEntry);
            var entryScope = m_compilation.staticIntArrayPool.getSpan(toBlock.scopeStackAtEntry);
            var entryLocals = m_compilation.staticIntArrayPool.getSpan(toBlock.localsAtEntry);

            var exitStackForConversion = m_tempIntArray.clearAndAddUninitialized(entryStack.Length);
            exitStackForConversion.Fill(-1);

            for (int i = 0; i < phiSourceNodeIds.Length; i++) {
                ref DataNode node = ref dataNodes[phiSourceNodeIds[i]];

                if (node.slot.kind == DataNodeSlotKind.STACK) {
                    int entryNodeId = entryStack[node.slot.id];

                    if (entryNodeId != node.id && !_isStackNodeCompatibleWithPhi(node, dataNodes[entryNodeId]))
                        exitStackForConversion[node.slot.id] = node.id;
                }
                else {
                    int entryNodeId = (node.slot.kind == DataNodeSlotKind.SCOPE)
                        ? entryScope[node.slot.id]
                        : entryLocals[node.slot.id];

                    if (entryNodeId == node.id)
                        continue;

                    ref DataNode entryNode = ref dataNodes[entryNodeId];
                    if (_isScopeOrLocalNodeCompatibleWithPhi(node, entryNode))
                        continue;

                    var localVarForEntry = _getLocalVarForNode(entryNode);

                    if (node.dataType == DataNodeType.UNDEFINED && entryNode.dataType == DataNodeType.ANY) {
                        m_ilBuilder.emit(ILOp.ldloca, localVarForEntry);
                        m_ilBuilder.emit(ILOp.initobj, typeof(ASAny));
                    }
                    else {
                        _emitLoadScopeOrLocalNode(node);
                        _emitTypeCoerceForTopOfStack(node, m_compilation.getDataNodeClass(entryNode));
                        m_ilBuilder.emit(ILOp.stloc, localVarForEntry);
                    }
                }
            }

            if (entryStack.Length == 0)
                // No need to fix the stack, so end here
                return;

            if (entryStack.Length == 1) {
                _emitBlockTransitionFixStackOneItem(
                    exitStackForConversion[0], entryStack[0], toBlock, isBackwardJumpWithNonEmptyStack);
            }
            else if (isBackwardJumpWithNonEmptyStack) {
                _emitBlockTransitionFixStackBackward(exitStackForConversion, entryStack, toBlock);
            }
            else {
                _emitBlockTransitionFixStackForward(exitStackForConversion, entryStack);
            }
        }

        private void _emitBlockTransitionFixStackOneItem(
            int exitNodeId, int entryNodeId, in BasicBlock toBlock, bool isBackwardJump)
        {
            if (exitNodeId != -1) {
                _emitTypeCoerceForTopOfStack(
                    m_compilation.getDataNode(exitNodeId),
                    m_compilation.getDataNodeClass(entryNodeId)
                );
            }

            if (isBackwardJump) {
                var stashVars = m_localVarArrayPool.getSpan(m_blockEmitInfo[toBlock.id].stackStashVars);
                if (!stashVars[0].isDefault)
                    m_ilBuilder.emit(ILOp.stloc, stashVars[0]);
            }
        }

        private void _emitBlockTransitionFixStackForward(ReadOnlySpan<int> exitStackForFixing, ReadOnlySpan<int> entryStack) {
            int firstStackIndexToFix = -1;
            for (int i = 0; i < exitStackForFixing.Length; i++) {
                if (exitStackForFixing[i] != -1) {
                    firstStackIndexToFix = i;
                    break;
                }
            }

            if (firstStackIndexToFix == -1)
                return;

            exitStackForFixing = exitStackForFixing.Slice(firstStackIndexToFix);
            entryStack = entryStack.Slice(firstStackIndexToFix);

            var stashVars = m_tempLocalArray.clearAndAddDefault(exitStackForFixing.Length);

            for (int i = exitStackForFixing.Length - 1; i >= 0; i--) {
                Class? entryNodeClass = m_compilation.getDataNodeClass(entryStack[i]);

                if (exitStackForFixing[i] != -1)
                    _emitTypeCoerceForTopOfStack(m_compilation.getDataNode(exitStackForFixing[i]), entryNodeClass);

                using (var lockedContext = m_compilation.getContext())
                    stashVars[i] = m_ilBuilder.acquireTempLocal(lockedContext.value.getTypeSignature(entryNodeClass));

                m_ilBuilder.emit(ILOp.stloc, stashVars[i]);
            }

            for (int i = 0; i < stashVars.Length; i++) {
                m_ilBuilder.emit(ILOp.ldloc, stashVars[i]);
                m_ilBuilder.releaseTempLocal(stashVars[i]);
            }
        }

        private void _emitBlockTransitionFixStackBackward(
            ReadOnlySpan<int> exitStackForFixing, ReadOnlySpan<int> entryStack, in BasicBlock toBlock)
        {
            var stashVars = m_localVarArrayPool.getSpan(m_blockEmitInfo[toBlock.id].stackStashVars);

            bool needsFixing = false;
            for (int i = 0; i < exitStackForFixing.Length && !needsFixing; i++)
                needsFixing = exitStackForFixing[i] != -1;

            if (!needsFixing) {
                for (int i = stashVars.Length - 1; i >= 0; i--) {
                    if (!stashVars[i].isDefault)
                        m_ilBuilder.emit(ILOp.stloc, stashVars[i]);
                    else
                        _emitDiscardTopOfStack(entryStack[i]);
                }

                return;
            }

            // If a type conversion is needed for any of the stack values then we cannot
            // write directly to the backward-jump stash variables. This is because these
            // variables were acquired using acquireTempVar and released immediately, so
            // the expectation is that no temporaries will be acquired once we start writing
            // to them (otherwise the stash variables could be accidentally overwritten!)
            // However, some type conversions may use temporaries, so we have to stash the
            // converted stack values in another set of temporary variables and then copy
            // them to the stash variables used for backward jumping once we are done.

            var convertVars = m_tempLocalArray.clearAndAddDefault(exitStackForFixing.Length);
            int stackCount = exitStackForFixing.Length;

            for (int i = stackCount - 1; i >= 0; i--) {
                if (stashVars[i].isDefault) {
                    _emitDiscardTopOfStack(entryStack[i]);
                    continue;
                }

                Class? entryNodeClass = m_compilation.getDataNodeClass(entryStack[i]);

                using (var lockedContext = m_compilation.getContext())
                    convertVars[i] = m_ilBuilder.acquireTempLocal(lockedContext.value.getTypeSignature(entryNodeClass));

                if (exitStackForFixing[i] != -1)
                    _emitTypeCoerceForTopOfStack(m_compilation.getDataNode(exitStackForFixing[i]), entryNodeClass);

                m_ilBuilder.emit(ILOp.stloc, convertVars[i]);
            }

            for (int i = 0; i < convertVars.Length; i++) {
                if (convertVars[i].isDefault)
                    continue;

                // Don't emit an unnecessary copy if the temporary variable that we acquired
                // to store the converted stack value happens to be the same as that expected
                // by the target block for that stack depth.
                if (convertVars[i] != stashVars[i]) {
                    m_ilBuilder.emit(ILOp.ldloc, convertVars[i]);
                    m_ilBuilder.emit(ILOp.stloc, stashVars[i]);
                }
                m_ilBuilder.releaseTempLocal(convertVars[i]);
            }
        }

        /// <summary>
        /// Returns true if the given data node on the stack is compatible with a
        /// phi node when transferring control to another basic block.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance representing the
        /// node on the stack.</param>
        /// <param name="phiNode">A reference to the <see cref="DataNode"/> instance representing
        /// the phi node at the same stack depth in the basic block to which control is to be
        /// transferred to.</param>
        /// <returns>True if the node is compatible (no type conversion is required), otherwise
        /// false.</returns>
        private bool _isStackNodeCompatibleWithPhi(in DataNode node, in DataNode phiNode) {
            if (phiNode.isNotPushed) {
                Debug.Assert(node.isNotPushed);
                return true;
            }

            DataNodeType pushedNodeType = _getPushedTypeOfNode(node);

            if (pushedNodeType == phiNode.dataType)
                return true;

            if (isInteger(pushedNodeType))
                return isInteger(phiNode.dataType);

            if (isObjectType(pushedNodeType)) {
                if (!isObjectType(phiNode.dataType))
                    return false;

                if (_getPushedClassOfNode(node)!.isInterface)
                    return m_compilation.getDataNodeClass(phiNode)!.isInterface;

                return true;
            }

            if (isAnyOrUndefined(pushedNodeType))
                return isAnyOrUndefined(phiNode.dataType);

            if (pushedNodeType == DataNodeType.REST) {
                Debug.Assert(m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_REST_ARRAY));
                return isObjectType(phiNode.dataType);
            }

            return false;
        }

        /// <summary>
        /// Returns true if the given data node in a scope stack or local variable
        /// slot is compatible with a phi node when transferring control to another basic block.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance representing the
        /// scope stack or local variable node.</param>
        /// <param name="phiNode">A reference to the <see cref="DataNode"/> instance representing
        /// the phi node in the same scope stack or local slot as <paramref name="node"/> in the basic
        /// block to which control is to be transferred to.</param>
        /// <returns>True if the node is compatible (no type conversion is required), otherwise
        /// false.</returns>
        private bool _isScopeOrLocalNodeCompatibleWithPhi(in DataNode node, in DataNode phiNode) {
            if (node.slot.kind == DataNodeSlotKind.LOCAL
                && ((node.flags & DataNodeFlags.LOCAL_WRITE_THROUGH) != 0 || isDead(phiNode)))
            {
                // If a local assignment has already been written through to the phi node, or
                // if the phi node is not live, the merge can be eliminated.
                return true;
            }

            if (!_nodeHasLocalVar(phiNode))
                return true;
            if (!_nodeHasLocalVar(node))
                return false;
            if (m_compilation.getDataNodeClass(node) == m_compilation.getDataNodeClass(phiNode))
                return true;

            if (isInteger(node.dataType)) {
                // int and uint are considered to be the same type when mapping nodes to IL local variables.
                return isInteger(phiNode.dataType);
            }

            return false;

            bool isDead(in DataNode nodeToCheck) {
                // To avoid cycles, if we don't reach a node with zero uses after a limited number
                // of iterations of following through phi nodes, we conservatively assume that the
                // node isn't dead.

                const int ITER_LIMIT = 15;

                for (int i = 0; i < ITER_LIMIT; i++) {
                    var nodeUses = m_compilation.getDataNodeUses(nodeToCheck);
                    if (nodeUses.Length == 0)
                        return true;
                    if (nodeUses.Length > 1 || !nodeUses[0].isDataNode)
                        return false;
                    nodeToCheck = ref m_compilation.getDataNode(nodeUses[0].instrOrNodeId);
                }

                return false;
            }
        }

        /// <summary>
        /// Returns true if the basic block <paramref name="first"/> is immediately before the
        /// basic block <paramref name="second"/> in the order in which the blocks are emitted in IL.
        /// </summary>
        /// <param name="first">A reference to a <see cref="BasicBlock"/> representing the first
        /// basic block.</param>
        /// <param name="second">A reference to a <see cref="BasicBlock"/> representing the second
        /// basic block.</param>
        /// <returns>True if <paramref name="first"/> is immediately before <paramref name="second"/>,
        /// otherwise false.</returns>
        private bool _isBasicBlockImmediatelyBefore(in BasicBlock first, in BasicBlock second) {
            if (m_blockEmitInfo[second.id].needsStackStashAndRestore) {
                // A jump instruction is needed for skipping the stack restore code in the forward case.
                return false;
            }

            if (second.postorderIndex == first.postorderIndex - 1)
                return true;

            if (second.postorderIndex > first.postorderIndex) {
                // Backward jump
                return false;
            }

            // Check if there is any reachable block in between.

            ReadOnlySpan<int> basicBlockRpo = m_compilation.getBasicBlockReversePostorder();
            int firstRpoIndex = basicBlockRpo.Length - first.postorderIndex - 1;
            int secondRpoIndex = basicBlockRpo.Length - second.postorderIndex - 1;

            for (int i = firstRpoIndex + 1; i < secondRpoIndex; i++) {
                if ((m_compilation.getBasicBlock(basicBlockRpo[i]).flags & BasicBlockFlags.UNREACHABLE) == 0)
                    return false;
            }

            return true;
        }

        private void _emitExceptionFilterBlock() {
            var endFilterLabel = m_ilBuilder.createLabel();

            // Unwrap the caught exception and check if it should be handled.
            m_ilBuilder.emit(ILOp.isinst, typeof(Exception));
            m_ilBuilder.emit(ILOp.ldloca, m_excThrownValueLocal);
            m_ilBuilder.emit(ILOp.call, KnownMembers.tryUnwrapCaughtException, -1);
            m_ilBuilder.emit(ILOp.dup);
            m_ilBuilder.emit(ILOp.brfalse, endFilterLabel);

            m_ilBuilder.emit(ILOp.pop);

            _emitExceptionFilterTypeCheckers(endFilterLabel);

            m_ilBuilder.markLabel(endFilterLabel);
            m_ilBuilder.emit(ILOp.endfilter);
        }

        private void _emitExceptionFilterTypeCheckers(ILBuilder.Label endFilterLabel) {
            var handlers = m_compilation.getExceptionHandlers();
            var labels = m_ilBuilder.createLabelGroup(handlers.Length);

            m_ilBuilder.emit(ILOp.ldloc, m_curExcHandlerIdLocal);
            m_ilBuilder.emit(ILOp.@switch, labels);

            // Don't catch the exception in the default case. This happens when we are outside
            // a try-region, i.e. the current handler id is -1.
            m_ilBuilder.emit(ILOp.ldc_i4_0);
            m_ilBuilder.emit(ILOp.br, endFilterLabel);

            for (int i = 0; i < handlers.Length; i++) {
                m_ilBuilder.markLabel(labels[i]);

                ref ExceptionHandler handler = ref handlers[i];
                Class? errorType = handler.errorType;

                if (errorType == null) {
                    // A catch type of "any" will catch all exceptions, including null and undefined.
                    m_ilBuilder.emit(ILOp.ldc_i4_1);
                    m_ilBuilder.emit(ILOp.br, endFilterLabel);
                    continue;
                }

                m_ilBuilder.emit(ILOp.ldloca, m_excThrownValueLocal);
                m_ilBuilder.emit(ILOp.call, KnownMembers.anyGetObject, 0);
                _emitIsOrAsType(errorType, ABCOp.istype, -1);

                if (handler.parentId == -1) {
                    m_ilBuilder.emit(ILOp.br, endFilterLabel);
                }
                else {
                    // If the type check fails and the handler is nested, check if the parent
                    // handler can catch the exception. Set the current handler id to the parent
                    // id because the catching code (if the exception is caught) will use it to
                    // jump to the catch target instruction.
                    m_ilBuilder.emit(ILOp.dup);
                    m_ilBuilder.emit(ILOp.brtrue, endFilterLabel);
                    m_ilBuilder.emit(ILOp.pop);
                    m_ilBuilder.emit(ILOp.ldc_i4, handler.parentId);
                    m_ilBuilder.emit(ILOp.stloc, m_curExcHandlerIdLocal);
                    m_ilBuilder.emit(ILOp.br, labels[handler.parentId]);
                }
            }
        }

        private void _emitExceptionCatchBlock() {
            m_ilBuilder.emit(ILOp.pop);

            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_RUNTIME_SCOPE_STACK)) {
                // Clear the runtime scope stack. However, for an instance method or constructor we
                // need to keep the class that was pushed to the bottom of the stack at the beginning.
                m_ilBuilder.emit(ILOp.ldloc, m_rtScopeStackLocal);
                m_ilBuilder.emit(ILOp.ldc_i4, m_compilation.isAnyFlagSet(MethodCompilationFlags.IS_INSTANCE_METHOD) ? 1 : 0);
                m_ilBuilder.emit(ILOp.call, KnownMembers.rtScopeStackClear);
            }

            var handlers = m_compilation.getExceptionHandlers();
            var labels = m_ilBuilder.createLabelGroup(handlers.Length);

            m_ilBuilder.emit(ILOp.ldloc, m_curExcHandlerIdLocal);
            m_ilBuilder.emit(ILOp.@switch, labels);

            // Rethrow in the default case
            m_ilBuilder.emit(ILOp.rethrow);

            for (int i = 0; i < handlers.Length; i++) {
                ref ExceptionHandler handler = ref handlers[i];
                m_ilBuilder.markLabel(labels[i]);

                int targetBlockId = m_compilation.getInstruction(handler.catchTargetInstrId).blockId;
                ref BlockEmitInfo blockEmitInfo = ref m_blockEmitInfo[targetBlockId];

                Debug.Assert(blockEmitInfo.needsStackStashAndRestore);
                var stashLocal = m_localVarArrayPool.getSpan(blockEmitInfo.stackStashVars)[0];

                // The caught exception must be converted to the type of the stack node at the entry
                // to the basic block where control will be transferred to. This may not be the same as
                // the error type in the handler, as there may be other entry points into that block
                // (from normal control flow or other exception handlers) with a stack value of a
                // different type.

                var targetBlockStackAtEntry =
                    m_compilation.staticIntArrayPool.getSpan(m_compilation.getBasicBlock(targetBlockId).stackAtEntry);

                ref DataNode targetStackNode = ref m_compilation.getDataNode(targetBlockStackAtEntry[0]);
                Class? targetStackNodeClass = _getPushedClassOfNode(targetStackNode);

                if (targetStackNodeClass == null) {
                    m_ilBuilder.emit(ILOp.ldloc, m_excThrownValueLocal);
                }
                else if (targetStackNodeClass.isPrimitiveClass) {
                    m_ilBuilder.emit(ILOp.ldloc, m_excThrownValueLocal);
                    ILEmitHelper.emitTypeCoerce(m_ilBuilder, null, targetStackNodeClass);
                }
                else {
                    // If the type is not primitive then we can emit a castclass instruction
                    // instead of calling AS_cast because the type of the exception was checked
                    // by the filter, so there is no possibility of the cast failing. This is true
                    // even if the target stack node has a different type, as that will always
                    // be a supertype of the caught exception type.

                    m_ilBuilder.emit(ILOp.ldloca, m_excThrownValueLocal);
                    m_ilBuilder.emit(ILOp.call, KnownMembers.anyGetObject, 0);

                    if (!targetStackNodeClass.isObjectClass) {
                        using var lockedContext = m_compilation.getContext();
                        m_ilBuilder.emit(ILOp.castclass, lockedContext.value.getEntityHandle(targetStackNodeClass));
                    }
                }

                m_ilBuilder.emit(ILOp.stloc, stashLocal);

                // Jumping from a catch block to its own try block is legal, see ECMA-335 section III.3.46
                m_ilBuilder.emit(ILOp.leave, blockEmitInfo.backwardLabel);
            }
        }

    }

}
