using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mariana.AVM2.ABC;
using Mariana.AVM2.Core;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    using static DataNodeTypeHelper;
    using static DataNodeConstHelper;
    using static SemanticBinderSpecialTraits;

    internal static class SemanticBinderSpecialTraits {
        public static readonly Class objectClass = Class.fromType<ASObject>();
        public static readonly Class arrayClass = Class.fromType<ASArray>();
        public static readonly Class vectorClass = Class.fromType(typeof(ASVector<>));
        public static readonly Class vectorAnyClass = Class.fromType<ASVectorAny>();
        public static readonly Class functionClass = Class.fromType<ASFunction>();
        public static readonly Trait mathMinTrait = Class.fromType<ASMath>().getTrait("min");
        public static readonly Trait mathMaxTrait = Class.fromType<ASMath>().getTrait("max");
        public static readonly Trait strCharAtTrait = Class.fromType<string>().getTrait(new QName(Namespace.AS3, "charAt"));
        public static readonly Trait strCharCodeAtTrait = Class.fromType<string>().getTrait(new QName(Namespace.AS3, "charCodeAt"));
        public static readonly Trait arrayLengthTrait = Class.fromType<ASArray>().getTrait("length");
        public static readonly Trait objHasOwnPropertyTrait = Class.fromType<ASObject>().getTrait(new QName(Namespace.AS3, "hasOwnProperty"));

    }

    internal sealed class SemanticBinder {

        private MethodCompilation m_compilation;
        private SemanticBinderFirstPass m_firstPass;
        private SemanticBinderSecondPass m_secondPass;

        public SemanticBinder(MethodCompilation compilation) {
            m_compilation = compilation;
            m_firstPass = new SemanticBinderFirstPass(compilation);
            m_secondPass = new SemanticBinderSecondPass(compilation);
        }

        public void run() {
            m_firstPass.run();
            _clearBasicBlockVisitedFlags();
            m_secondPass.run();
            _clearBasicBlockVisitedFlags();
        }

        private void _clearBasicBlockVisitedFlags() {
            var blocks = m_compilation.getBasicBlocks();

            for (int i = 0; i < blocks.Length; i++)
                blocks[i].flags &= ~(BasicBlockFlags.VISITED | BasicBlockFlags.TOUCHED);
        }

    }

    internal sealed class SemanticBinderFirstPass {

        #pragma warning disable 0660, 0661

        /// <summary>
        /// A fixed snapshot containing the type information of a data node.
        /// </summary>
        private readonly struct DataNodeTypeSnapshot {

        #pragma warning restore

            public readonly DataNodeType type;
            public readonly DataNodeFlags flags;
            public readonly DataNodeConstant constant;

            /// <summary>
            /// Creates a new instance of <see cref="DataNodeTypeSnapshot"/> representing a snapshot of
            /// the given data node.
            /// </summary>
            /// <param name="node">The node for which to create a snapshot.</param>
            public DataNodeTypeSnapshot(in DataNode node) {
                type = node.dataType;
                flags = node.flags;
                constant = node.constant;
            }

            public static bool operator ==(in DataNodeTypeSnapshot x, in DataNodeTypeSnapshot y) =>
                x.type == y.type && x.flags == y.flags && x.constant == y.constant;

            public static bool operator !=(in DataNodeTypeSnapshot x, in DataNodeTypeSnapshot y) =>
                x.type != y.type || x.flags != y.flags || x.constant != y.constant;

        }

        private readonly struct StateSnapshot {
            public readonly StaticArrayPoolToken<DataNodeTypeSnapshot> stack;
            public readonly StaticArrayPoolToken<DataNodeTypeSnapshot> scope;
            public readonly StaticArrayPoolToken<DataNodeTypeSnapshot> locals;

            public StateSnapshot(
                StaticArrayPoolToken<DataNodeTypeSnapshot> stack,
                StaticArrayPoolToken<DataNodeTypeSnapshot> scope,
                StaticArrayPoolToken<DataNodeTypeSnapshot> locals
            ) {
                this.stack = stack;
                this.scope = scope;
                this.locals = locals;
            }
        }

        private MethodCompilation m_compilation;

        private Queue<int> m_queuedBlockIds = new Queue<int>();

        private DynamicArray<int> m_curScopeStackNodeIds;

        private bool m_scopeStateChangedFromLastVisit;

        private bool m_localStateChangedFromLastVisit;

        /// <summary>
        /// An array pool used to allocate <see cref="DataNodeTypeSnapshot"/> instances.
        /// </summary>
        private StaticArrayPool<DataNodeTypeSnapshot> m_nodeSnapshotArrayPool= new StaticArrayPool<DataNodeTypeSnapshot>(256);

        /// <summary>
        /// Contains snapshots of the stack, scope stack and local variable state at the entry
        /// to each basic block (indexed by block id), at the time it was last visited. Used
        /// for detecting state changes that may require revisiting.
        /// </summary>
        private DynamicArray<StateSnapshot> m_basicBlockEntrySnapshots = new DynamicArray<StateSnapshot>(32);

        public SemanticBinderFirstPass(MethodCompilation compilation) {
            m_compilation = compilation;
        }

        public void run() {
            try {
                var blocks = m_compilation.getBasicBlocks();

                m_basicBlockEntrySnapshots.clearAndAddUninitialized(blocks.Length);

                m_curScopeStackNodeIds.clear();
                m_curScopeStackNodeIds.ensureCapacity(m_compilation.computedMaxScopeSize);

                m_queuedBlockIds.Clear();

                var rpo = m_compilation.getBasicBlockReversePostorder();
                for (int i = 0; i < rpo.Length; i++)
                    _visitBasicBlock(ref blocks[rpo[i]]);

                while (m_queuedBlockIds.Count > 0)
                    _visitBasicBlock(ref blocks[m_queuedBlockIds.Dequeue()]);
            }
            finally {
                m_nodeSnapshotArrayPool.clear();
            }
        }

        private void _visitBasicBlock(ref BasicBlock block) {
            var staticIntArrayPool = m_compilation.staticIntArrayPool;

            var stackAtEntry = staticIntArrayPool.getSpan(block.stackAtEntry);
            var scopeAtEntry = staticIntArrayPool.getSpan(block.scopeStackAtEntry);
            var localsAtEntry = staticIntArrayPool.getSpan(block.localsAtEntry);

            if ((block.flags & BasicBlockFlags.DEFINES_PHI_NODES) != 0) {
                _computePhiNodeTypeInfo(stackAtEntry);
                _computePhiNodeTypeInfo(scopeAtEntry);
                _computePhiNodeTypeInfo(localsAtEntry);
            }

            bool isFirstVisit;
            ref StateSnapshot entrySnapshot = ref m_basicBlockEntrySnapshots[block.id];

            if ((block.flags & BasicBlockFlags.VISITED) == 0) {
                entrySnapshot = new StateSnapshot(
                    _createStateSnapshot(stackAtEntry),
                    _createStateSnapshot(scopeAtEntry),
                    _createStateSnapshot(localsAtEntry)
                );
                m_scopeStateChangedFromLastVisit = true;
                m_localStateChangedFromLastVisit = true;

                isFirstVisit = true;
            }
            else {
                bool stackChange = _checkAndUpdateStateSnapshot(stackAtEntry, m_nodeSnapshotArrayPool.getSpan(entrySnapshot.stack));
                bool scopeChange = _checkAndUpdateStateSnapshot(scopeAtEntry, m_nodeSnapshotArrayPool.getSpan(entrySnapshot.scope));
                bool localChange = _checkAndUpdateStateSnapshot(localsAtEntry, m_nodeSnapshotArrayPool.getSpan(entrySnapshot.locals));

                if (!stackChange && !scopeChange && !localChange)
                    // No change in entry state since last visit, so no need to revisit.
                    return;

                m_scopeStateChangedFromLastVisit = scopeChange;
                m_localStateChangedFromLastVisit = localChange;

                isFirstVisit = false;
            }

            block.flags |= BasicBlockFlags.VISITED;

            m_curScopeStackNodeIds.clearAndAddUninitialized(scopeAtEntry.Length);
            scopeAtEntry.CopyTo(m_curScopeStackNodeIds.asSpan());

            Span<Instruction> instructions = m_compilation.getInstructionsInBasicBlock(block.id);
            for (int i = 0; i < instructions.Length; i++) {
                ref Instruction instr = ref instructions[i];
                _visitInstruction(ref instr, isFirstVisit);
            }

            // If any exit blocks have already been visited, queue them to check for state changes.

            var exitBlockIds = staticIntArrayPool.getSpan(block.exitBlockIds);
            for (int i = 0; i < exitBlockIds.Length; i++) {
                if ((m_compilation.getBasicBlock(exitBlockIds[i]).flags & BasicBlockFlags.VISITED) != 0)
                    m_queuedBlockIds.Enqueue(exitBlockIds[i]);
            }

            // Do the same for any catch blocks if this block is in an exception handling
            // try region and this block affects the local variable state. (We don't care
            // about stack or scope stack state here as they are always empty when entering
            // a catch block).

            if (block.excHandlerId != -1 && m_localStateChangedFromLastVisit) {
                ref ExceptionHandler handler = ref m_compilation.getExceptionHandler(block.excHandlerId);
                var catchBlockIds = staticIntArrayPool.getSpan(handler.flattenedCatchTargetBlockIds);

                for (int i = 0; i < catchBlockIds.Length; i++) {
                    if ((m_compilation.getBasicBlock(catchBlockIds[i]).flags & BasicBlockFlags.VISITED) != 0)
                        m_queuedBlockIds.Enqueue(catchBlockIds[i]);
                }
            }
        }

        private StaticArrayPoolToken<DataNodeTypeSnapshot> _createStateSnapshot(ReadOnlySpan<int> nodeIds) {
            var dataNodes = m_compilation.getDataNodes();
            var token = m_nodeSnapshotArrayPool.allocate(nodeIds.Length, out Span<DataNodeTypeSnapshot> snapshot);

            for (int i = 0; i < snapshot.Length; i++)
                snapshot[i] = new DataNodeTypeSnapshot(dataNodes[nodeIds[i]]);

            return token;
        }

        private bool _checkAndUpdateStateSnapshot(ReadOnlySpan<int> nodeIds, Span<DataNodeTypeSnapshot> snapshot) {
            var dataNodes = m_compilation.getDataNodes();
            bool hasChanges = false;

            for (int i = 0; i < snapshot.Length; i++) {
                ref var nodeSnapshot = ref snapshot[i];
                var newNodeSnapshot = new DataNodeTypeSnapshot(dataNodes[nodeIds[i]]);

                if (nodeSnapshot != newNodeSnapshot) {
                    hasChanges = true;
                    nodeSnapshot = newNodeSnapshot;
                }
            }

            return hasChanges;
        }

        private void _computePhiNodeTypeInfo(ReadOnlySpan<int> nodeIds) {
            var dataNodes = m_compilation.getDataNodes();

            for (int i = 0; i < nodeIds.Length; i++) {
                ref DataNode node = ref dataNodes[nodeIds[i]];
                if (!node.isPhi)
                    continue;

                node.dataType = DataNodeType.UNKNOWN;
                node.constant = default;
                node.flags &= ~(DataNodeFlags.CONSTANT | DataNodeFlags.NOT_NULL);

                var sources = m_compilation.getDataNodeDefs(ref node);
                bool containsUnknownSource = false;

                for (int j = 0; j < sources.Length; j++) {
                    Debug.Assert(sources[j].isDataNode);
                    ref DataNode sourceNode = ref dataNodes[sources[j].instrOrNodeId];

                    _mergePhiNodeTypeFromSource(ref node, ref sourceNode);
                    containsUnknownSource |= sourceNode.dataType == DataNodeType.UNKNOWN;
                }

                if (node.isConstant && containsUnknownSource) {
                    // If any of the node's sources has unknown type information, we strip constant
                    // info for certain types. This is because it is very likely that the unknown source
                    // will turn out not to have the same constant value, or it may not even be a constant,
                    // which would result in the basic block, and possibly many others that follow it
                    // being revisited (incurring a performance penalty) had constant info been maintained
                    // at this point.
                    //
                    // For example, in a simple for-loop `for (i = 0; i < N; i++) {...}`
                    // without stripping constant info the loop body will be visited twice (first
                    // with i as constant 0, then with non-constant i).

                    switch (node.dataType) {
                        case DataNodeType.BOOL:
                        case DataNodeType.INT:
                        case DataNodeType.NAMESPACE:
                        case DataNodeType.NUMBER:
                        case DataNodeType.QNAME:
                        case DataNodeType.STRING:
                        case DataNodeType.UINT:
                            node.constant = default;
                            node.isConstant = false;
                            break;

                        case DataNodeType.UNDEFINED:
                            node.dataType = DataNodeType.ANY;
                            node.isConstant = false;
                            break;
                    }
                }
            }
        }

        private void _mergePhiNodeTypeFromSource(ref DataNode node, ref DataNode source) {
            DataNodeType nodeType = node.dataType, sourceType = source.dataType;

            if (sourceType == DataNodeType.UNKNOWN)
                return;

            if (nodeType == DataNodeType.UNKNOWN) {
                node.dataType = sourceType;
                node.constant = source.constant;

                const DataNodeFlags transferMask =
                    DataNodeFlags.CONSTANT
                    | DataNodeFlags.NOT_NULL
                    | DataNodeFlags.WITH_SCOPE
                    | DataNodeFlags.ARGUMENT
                    | DataNodeFlags.LATE_MULTINAME_BINDING;

                node.flags = (node.flags & ~transferMask) | (source.flags & transferMask);
                return;
            }

            if (node.isWithScope != source.isWithScope)
                throw m_compilation.createError(ErrorCode.MARIANA__ABC_SCOPE_WITH_NOT_MATCH, -1);

            node.isNotNull &= source.isNotNull;
            node.isArgument &= source.isArgument;

            node.flags |= source.flags & DataNodeFlags.LATE_MULTINAME_BINDING;

            if (nodeType == sourceType) {
                switch (nodeType) {
                    case DataNodeType.INT:
                    case DataNodeType.UINT:
                    case DataNodeType.NUMBER:
                    case DataNodeType.STRING:
                    case DataNodeType.BOOL:
                    case DataNodeType.NAMESPACE:
                    case DataNodeType.QNAME:
                    {
                        if (node.isConstant && (!source.isConstant || node.constant != source.constant)) {
                            node.isConstant = false;
                            node.constant = default;
                        }
                        break;
                    }

                    case DataNodeType.CLASS:
                    case DataNodeType.FUNCTION:
                    {
                        if (node.constant != source.constant) {
                            node.isConstant = false;
                            node.dataType = DataNodeType.OBJECT;
                            node.constant = new DataNodeConstant(getClass(nodeType));
                        }
                        break;
                    }

                    case DataNodeType.OBJECT:
                        node.constant = new DataNodeConstant(_getClassCommonAncestor(node.constant.classValue, source.constant.classValue));
                        break;
                }
            }
            else if (isNumeric(nodeType) && isNumeric(sourceType)) {
                // If the nodes are of numeric types, the common type is Number.
                // An exception is when one of them is an integer constant whose value is
                // in [0, 2^31-1] and thus representable as both a signed and an unsigned int,
                // and the other node is of an integer type.

                DataNodeType newType = DataNodeType.NUMBER;

                if (nodeType != DataNodeType.NUMBER && sourceType != DataNodeType.NUMBER) {
                    if (source.isConstant && source.constant.intValue >= 0)
                        newType = nodeType;
                    else if (node.isConstant && node.constant.intValue >= 0)
                        newType = sourceType;
                }

                node.dataType = newType;
                node.isConstant = false;
                node.constant = default;
            }
            else if ((nodeType == DataNodeType.STRING && sourceType == DataNodeType.NULL)
                || (nodeType == DataNodeType.NULL && sourceType == DataNodeType.STRING))
            {
                node.dataType = DataNodeType.STRING;
                node.isConstant = false;
                node.constant = default;
            }
            else {
                Class commonClass;

                if (nodeType == DataNodeType.ANY || nodeType == DataNodeType.UNDEFINED
                    || sourceType == DataNodeType.ANY || sourceType == DataNodeType.UNDEFINED)
                {
                    commonClass = null;
                }
                else if (isPrimitive(nodeType)
                    || isPrimitive(sourceType))
                {
                    commonClass = objectClass;
                }
                else if (nodeType == DataNodeType.NULL) {
                    commonClass = m_compilation.getDataNodeClass(source);
                }
                else if (sourceType == DataNodeType.NULL) {
                    commonClass = m_compilation.getDataNodeClass(node);
                }
                else {
                    commonClass = _getClassCommonAncestor(m_compilation.getDataNodeClass(node), m_compilation.getDataNodeClass(source));
                }

                if (commonClass == null) {
                    node.dataType = DataNodeType.ANY;
                    node.constant = default;
                }
                else {
                    node.dataType = DataNodeType.OBJECT;
                    node.constant = new DataNodeConstant(commonClass);
                }

                node.isConstant = false;
            }
        }

        /// <summary>
        /// Returns the common ancestor of two classes.
        /// </summary>
        /// <param name="x">The first class.</param>
        /// <param name="y">The second class.</param>
        /// <returns>A class that is assignable from both <paramref name="x"/> and <paramref name="y"/>.</returns>
        private static Class _getClassCommonAncestor(Class x, Class y) {
            if (x == y || x == null)
                return x;

            if (x.canAssignTo(y))
                return y;

            if (y.canAssignTo(x))
                return x;

            if (x.isInterface || y.isInterface)
                // We don't attempt to find common ancestors when interfaces are involved
                // except in the case where one is a subtype of the other because there
                // may be no unambiguous common type (in addition to the fact that multiple
                // inheritance makes this search expensive performance-wise). Instead
                // consider the common type to be Object (which is valid as it is a supertype
                // of anything that can implement an interface).
                return objectClass;

            while (x.parent != null) {
                x = x.parent;
                if (y.canAssignTo(x))
                    break;
            }
            return x;
        }

        private void _visitInstruction(ref Instruction instr, bool isFirstVisit) {
            switch (instr.opcode) {
                case ABCOp.pushfalse:
                case ABCOp.pushtrue:
                case ABCOp.pushnull:
                case ABCOp.pushundefined:
                case ABCOp.pushshort:
                case ABCOp.pushint:
                case ABCOp.pushuint:
                case ABCOp.pushdouble:
                case ABCOp.pushstring:
                case ABCOp.pushnamespace:
                    _visitPushConst(ref instr);
                    break;

                case ABCOp.newarray:
                case ABCOp.newobject:
                    _visitNewArrayObject(ref instr);
                    break;

                case ABCOp.getlocal:
                    _visitGetLocal(ref instr);
                    break;

                case ABCOp.setlocal:
                    _visitSetLocal(ref instr);
                    break;

                case ABCOp.kill:
                    _visitKill(ref instr);
                    break;

                case ABCOp.pushscope:
                case ABCOp.pushwith:
                    _visitPushScope(ref instr);
                    break;

                case ABCOp.popscope:
                    _visitPopScope(ref instr);
                    break;

                case ABCOp.getscopeobject:
                    _visitGetScopeObject(ref instr);
                    break;

                case ABCOp.dup:
                    _visitDup(ref instr);
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

                case ABCOp.bitnot:
                case ABCOp.negate:
                case ABCOp.negate_i:
                case ABCOp.increment:
                case ABCOp.decrement:
                case ABCOp.increment_i:
                case ABCOp.decrement_i:
                case ABCOp.not:
                    _visitUnaryArithmeticOp(ref instr);
                    break;

                case ABCOp.inclocal:
                case ABCOp.inclocal_i:
                case ABCOp.declocal:
                case ABCOp.declocal_i:
                    _visitIncDecLocal(ref instr);
                    break;

                case ABCOp.esc_xelem:
                case ABCOp.esc_xattr:
                    _visitEscapeXML(ref instr);
                    break;

                case ABCOp.astype:
                    _visitAsType(ref instr);
                    break;

                case ABCOp.add:
                    _visitAdd(ref instr);
                    break;

                case ABCOp.add_i:
                case ABCOp.subtract:
                case ABCOp.subtract_i:
                case ABCOp.multiply:
                case ABCOp.multiply_i:
                case ABCOp.divide:
                case ABCOp.modulo:
                case ABCOp.bitand:
                case ABCOp.bitor:
                case ABCOp.bitxor:
                case ABCOp.lshift:
                case ABCOp.rshift:
                case ABCOp.urshift:
                    _visitBinaryArithmeticOp(ref instr);
                    break;

                case ABCOp.equals:
                case ABCOp.strictequals:
                case ABCOp.lessthan:
                case ABCOp.lessequals:
                case ABCOp.greaterthan:
                case ABCOp.greaterequals:
                    _visitBinaryCompareOp(ref instr);
                    break;

                case ABCOp.astypelate:
                    _visitAsTypeLate(ref instr);
                    break;

                case ABCOp.istype:
                case ABCOp.istypelate:
                case ABCOp.instanceof:
                    _visitIsTypeOrInstanceof(ref instr);
                    break;

                case ABCOp.@typeof:
                    _visitTypeof(ref instr);
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

                case ABCOp.applytype:
                    _visitApplyType(ref instr);
                    break;

                case ABCOp.dxns:
                case ABCOp.dxnslate:
                    _visitDxns(ref instr);
                    break;

                case ABCOp.hasnext:
                    _visitHasNext(ref instr);
                    break;

                case ABCOp.hasnext2:
                    _visitHasnext2(ref instr);
                    break;

                case ABCOp.nextname:
                case ABCOp.nextvalue:
                    _visitNextNameValue(ref instr);
                    break;

                case ABCOp.getglobalscope:
                    _visitGetGlobalScope(ref instr);
                    break;

                case ABCOp.getproperty:
                case ABCOp.setproperty:
                case ABCOp.initproperty:
                case ABCOp.deleteproperty:
                    _visitGetSetDeleteProperty(ref instr, isFirstVisit);
                    break;

                case ABCOp.getdescendants:
                    _visitGetDescendants(ref instr);
                    break;

                case ABCOp.@in:
                    _visitIn(ref instr);
                    break;

                case ABCOp.callproperty:
                case ABCOp.callpropvoid:
                case ABCOp.callproplex:
                case ABCOp.constructprop:
                    _visitCallOrConstructProp(ref instr, isFirstVisit);
                    break;

                case ABCOp.getsuper:
                case ABCOp.setsuper:
                    _visitGetSetSuper(ref instr, isFirstVisit);
                    break;

                case ABCOp.callsuper:
                case ABCOp.callsupervoid:
                    _visitCallSuper(ref instr, isFirstVisit);
                    break;

                case ABCOp.constructsuper:
                    _visitConstructSuper(ref instr, isFirstVisit);
                    break;

                case ABCOp.getslot:
                case ABCOp.setslot:
                    _visitGetSetSlot(ref instr, isFirstVisit);
                    break;

                case ABCOp.getglobalslot:
                case ABCOp.setglobalslot:
                    _visitGetSetGlobalSlot(ref instr, isFirstVisit);
                    break;

                case ABCOp.call:
                    _visitCall(ref instr, isFirstVisit);
                    break;

                case ABCOp.construct:
                    _visitConstruct(ref instr, isFirstVisit);
                    break;

                case ABCOp.callmethod:
                case ABCOp.callstatic:
                    _visitCallMethodOrStatic(ref instr, isFirstVisit);
                    break;

                case ABCOp.findproperty:
                case ABCOp.findpropstrict:
                    _visitFindProperty(ref instr, isFirstVisit);
                    break;

                case ABCOp.getlex:
                    _visitGetLex(ref instr, isFirstVisit);
                    break;

                case ABCOp.newactivation:
                    _visitNewActivation(ref instr);
                    break;

                case ABCOp.newcatch:
                    _visitNewCatch(ref instr);
                    break;

                case ABCOp.newclass:
                    _visitNewClass(ref instr);
                    break;

                case ABCOp.newfunction:
                    _visitNewFunction(ref instr);
                    break;
            }
        }

        /// <summary>
        /// Copies type information from a source data node to a destination node.
        /// </summary>
        /// <param name="source">A reference to the source node containing type information.</param>
        /// <param name="dest">A reference to the destination node to which to copy type
        /// information from <paramref name="source"/>.</param>
        private static void _copyDataNodeTypeInfo(ref DataNode source, ref DataNode dest) {
            dest.dataType = source.dataType;
            dest.constant = source.constant;

            const DataNodeFlags transferFlags =
                DataNodeFlags.CONSTANT | DataNodeFlags.NOT_NULL | DataNodeFlags.LATE_MULTINAME_BINDING;

            dest.flags = (dest.flags & ~transferFlags) | (source.flags & transferFlags);
        }

        private void _visitGetLocal(ref Instruction instr) {
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            ref DataNode local = ref m_compilation.getDataNode(instr.data.getSetLocal.nodeId);
            _copyDataNodeTypeInfo(ref local, ref pushed);
        }

        private void _visitSetLocal(ref Instruction instr) {
            ref DataNode popped = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            ref DataNode local = ref m_compilation.getDataNode(instr.data.getSetLocal.newNodeId);
            _copyDataNodeTypeInfo(ref popped, ref local);

            m_localStateChangedFromLastVisit = true;
        }

        private void _visitKill(ref Instruction instr) {
            ref DataNode local = ref m_compilation.getDataNode(instr.data.getSetLocal.newNodeId);

            local.dataType = DataNodeType.UNDEFINED;
            local.isConstant = true;
            local.isNotNull = false;
            local.constant = default;

            m_localStateChangedFromLastVisit = true;
        }

        private void _visitDup(ref Instruction instr) {
            ref DataNode source = ref m_compilation.getDataNode(instr.data.dupOrSwap.nodeId1);
            ref DataNode dest = ref m_compilation.getDataNode(instr.data.dupOrSwap.nodeId2);
            _copyDataNodeTypeInfo(ref source, ref dest);
        }

        private void _visitPushConst(ref Instruction instr) {
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            pushed.isConstant = true;
            pushed.isNotNull = false;
            pushed.constant = default;

            switch (instr.opcode) {
                case ABCOp.pushfalse:
                    pushed.dataType = DataNodeType.BOOL;
                    pushed.constant = new DataNodeConstant(false);
                    break;

                case ABCOp.pushtrue:
                    pushed.dataType = DataNodeType.BOOL;
                    pushed.constant = new DataNodeConstant(true);
                    break;

                case ABCOp.pushnull:
                    pushed.dataType = DataNodeType.NULL;
                    break;

                case ABCOp.pushundefined:
                    pushed.dataType = DataNodeType.UNDEFINED;
                    break;

                case ABCOp.pushshort:
                    pushed.dataType = DataNodeType.INT;
                    pushed.constant = new DataNodeConstant((int)(short)instr.data.pushShort.value);
                    break;

                case ABCOp.pushint:
                    pushed.dataType = DataNodeType.INT;
                    pushed.constant = new DataNodeConstant(m_compilation.abcFile.resolveInt(instr.data.pushConst.poolId));
                    break;

                case ABCOp.pushuint:
                    pushed.dataType = DataNodeType.UINT;
                    pushed.constant = new DataNodeConstant((int)m_compilation.abcFile.resolveUint(instr.data.pushConst.poolId));
                    break;

                case ABCOp.pushdouble:
                    setToConstant(ref pushed, m_compilation.abcFile.resolveDouble(instr.data.pushConst.poolId));
                    break;

                case ABCOp.pushnamespace:
                    pushed.dataType = DataNodeType.NAMESPACE;
                    pushed.constant = new DataNodeConstant(m_compilation.abcFile.resolveNamespace(instr.data.pushConst.poolId));
                    break;

                case ABCOp.pushstring: {
                    string value = m_compilation.abcFile.resolveString(instr.data.pushConst.poolId);
                    if (value == null)
                        goto case ABCOp.pushnull;

                    pushed.dataType = DataNodeType.STRING;
                    pushed.constant = new DataNodeConstant(value);
                    pushed.isNotNull = true;
                    break;
                }
            }

            if (pushed.dataType != DataNodeType.NULL && pushed.dataType != DataNodeType.UNDEFINED)
                pushed.isNotNull = true;
        }

        private void _visitPushScope(ref Instruction instr) {
            ref DataNode popped = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            ref DataNode scope = ref m_compilation.getDataNode(instr.data.pushScope.pushedNodeId);

            if (isAnyOrUndefined(popped.dataType)) {
                scope.dataType = DataNodeType.OBJECT;
                scope.constant = new DataNodeConstant(objectClass);
                scope.isConstant = false;
                scope.isNotNull = false;
            }
            else {
                _copyDataNodeTypeInfo(ref popped, ref scope);
            }

            if (instr.opcode == ABCOp.pushwith)
                scope.isWithScope = true;

            m_curScopeStackNodeIds.add(scope.id);
            m_scopeStateChangedFromLastVisit = true;
        }

        private void _visitPopScope(ref Instruction instr) {
            m_curScopeStackNodeIds.removeLast();
        }

        private void _visitGetScopeObject(ref Instruction instr) {
            int scopeIndex = instr.data.getScopeObject.index;

            if ((uint)scopeIndex >= (uint)m_curScopeStackNodeIds.length)
                throw m_compilation.createError(ErrorCode.GETSCOPEOBJECT_OUT_OF_BOUNDS, instr.id, scopeIndex);

            ref DataNode scope = ref m_compilation.getDataNode(m_curScopeStackNodeIds[scopeIndex]);
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            instr.data.getScopeObject.nodeId = scope.id;
            _copyDataNodeTypeInfo(ref scope, ref pushed);
        }

        private void _visitCoerce(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (input.dataType == DataNodeType.NULL) {
                output.dataType = DataNodeType.NULL;
                output.isConstant = true;
                output.isNotNull = false;
                return;
            }

            if (output.dataType != DataNodeType.UNKNOWN && output.dataType != DataNodeType.NULL) {
                // Optimize this case when the instruction was visited before, as the output
                // type is not input-dependent except when it is a null constant.
                output.isNotNull = input.isNotNull;
                return;
            }

            ABCMultiname multiname = m_compilation.abcFile.resolveMultiname(instr.data.coerceOrIsType.multinameId);

            Class coerceToClass;
            using (var lockedContext = m_compilation.getContext())
                coerceToClass = lockedContext.value.getClassByMultiname(multiname);

            switch (coerceToClass.tag) {
                case ClassTag.INT:
                    instr.opcode = ABCOp.convert_i;
                    break;
                case ClassTag.UINT:
                    instr.opcode = ABCOp.convert_u;
                    break;
                case ClassTag.NUMBER:
                    instr.opcode = ABCOp.convert_d;
                    break;
                case ClassTag.BOOLEAN:
                    instr.opcode = ABCOp.convert_b;
                    break;
                case ClassTag.STRING:
                    instr.opcode = ABCOp.coerce_s;
                    break;
                default:
                    if (coerceToClass == objectClass)
                        instr.opcode = ABCOp.convert_o;
                    break;
            }

            if (instr.opcode != ABCOp.coerce) {
                _visitConvertX(ref instr);
                return;
            }

            output.dataType = getDataTypeOfClass(coerceToClass);
            if (output.dataType == DataNodeType.OBJECT)
                output.constant = new DataNodeConstant(coerceToClass);

            output.isConstant = false;
            output.isNotNull = input.isNotNull;
        }

        private void _visitConvertX(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (instr.opcode == ABCOp.coerce_a) {
                _copyDataNodeTypeInfo(ref input, ref output);

                // If the input type is not a final class, then any property binding on the
                // result of coerce_a with a namespace set must be deferred to runtime, as
                // the runtime type of the node may be a derived class that declares a trait
                // having the same name in another namespace of the set (which must be chosen
                // over the base class trait that compile-time binding would select).

                Class inputClass = m_compilation.getDataNodeClass(input);
                if (inputClass != null && !inputClass.isFinal)
                    output.flags |= DataNodeFlags.LATE_MULTINAME_BINDING;

                return;
            }

            output.isConstant = false;
            output.isNotNull = false;
            output.constant = default;

            if (tryEvalUnaryOp(ref input, ref output, instr.opcode))
                return;

            switch (instr.opcode) {
                case ABCOp.convert_b:
                    output.dataType = DataNodeType.BOOL;
                    output.isNotNull = true;
                    break;

                case ABCOp.convert_d:
                    output.dataType = DataNodeType.NUMBER;
                    output.isNotNull = true;
                    break;

                case ABCOp.convert_i:
                    output.dataType = DataNodeType.INT;
                    output.isNotNull = true;
                    break;

                case ABCOp.coerce_s:
                case ABCOp.convert_s:
                    output.dataType = DataNodeType.STRING;
                    output.isNotNull = input.isNotNull || instr.opcode != ABCOp.coerce_s;
                    break;

                case ABCOp.convert_u:
                    output.dataType = DataNodeType.UINT;
                    output.isNotNull = true;
                    break;

                case ABCOp.convert_o:
                    output.dataType = DataNodeType.OBJECT;
                    output.constant = new DataNodeConstant(objectClass);
                    break;
            }
        }

        private void _visitUnaryArithmeticOp(ref Instruction instr) {
            // Handles bitnot, negate, negate_i, increment, decrement, increment_i, decrement_i, not

            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            output.isConstant = false;
            output.isNotNull = false;
            output.constant = default;

            if (tryEvalUnaryOp(ref input, ref output, instr.opcode))
                return;

            switch (instr.opcode) {
                case ABCOp.bitnot:
                case ABCOp.negate_i:
                case ABCOp.increment_i:
                case ABCOp.decrement_i:
                    output.dataType = DataNodeType.INT;
                    break;
                case ABCOp.not:
                    output.dataType = DataNodeType.BOOL;
                    break;
                default:
                    output.dataType = DataNodeType.NUMBER;
                    break;
            }

            output.isNotNull = true;
        }

        private void _visitIncDecLocal(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(instr.data.getSetLocal.nodeId);
            ref DataNode output = ref m_compilation.getDataNode(instr.data.getSetLocal.newNodeId);

            output.isConstant = false;
            output.isNotNull = false;
            output.constant = default;

            m_localStateChangedFromLastVisit = true;

            if (tryEvalUnaryOp(ref input, ref output, instr.opcode))
                return;

            output.dataType = (instr.opcode == ABCOp.inclocal_i || instr.opcode == ABCOp.declocal_i)
                ? DataNodeType.INT
                : DataNodeType.NUMBER;

            output.isNotNull = true;
        }

        private void _visitEscapeXML(ref Instruction instr) {
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            pushed.dataType = DataNodeType.STRING;
            pushed.isNotNull = true;
        }

        private void _visitIsTypeOrInstanceof(ref Instruction instr) {
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            pushed.dataType = DataNodeType.BOOL;
            pushed.isNotNull = true;
        }

        private void _visitGetDescendants(ref Instruction instr) {
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            pushed.dataType = DataNodeType.ANY;
        }

        private void _visitTypeof(ref Instruction instr) {
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            pushed.dataType = DataNodeType.STRING;
            pushed.isNotNull = true;
        }

        private void _visitAsType(ref Instruction instr) {
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (output.dataType != DataNodeType.UNKNOWN)
                return;

            ABCMultiname mn = m_compilation.abcFile.resolveMultiname(instr.data.coerceOrIsType.multinameId);

            Class targetClass;
            using (var lockedContext = m_compilation.getContext())
                targetClass = lockedContext.value.getClassByMultiname(mn);

            if (ClassTagSet.primitive.contains(targetClass.tag))
                targetClass = objectClass;

            output.dataType = DataNodeType.OBJECT;
            output.constant = new DataNodeConstant(targetClass);
            output.isNotNull = false;
        }

        private void _visitAdd(ref Instruction instr) {
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode input1 = ref m_compilation.getDataNode(inputIds[0]);
            ref DataNode input2 = ref m_compilation.getDataNode(inputIds[1]);

            instr.data.add.argsAreAnyType = false;
            output.constant = default;
            output.isConstant = false;
            output.isNotNull = false;

            if (tryEvalBinaryOp(ref input1, ref input2, ref output, instr.opcode))
                return;

            const int numberAddDataTypeMask =
                (1 << (int)DataNodeType.INT)
                | (1 << (int)DataNodeType.UINT)
                | (1 << (int)DataNodeType.NUMBER)
                | (1 << (int)DataNodeType.BOOL)
                | (1 << (int)DataNodeType.NULL)
                | (1 << (int)DataNodeType.UNDEFINED);

            const int anyDataTypeMask = (1 << (int)DataNodeType.ANY) | (1 << (int)DataNodeType.UNDEFINED);
            const int stringDataTypeMask = 1 << (int)DataNodeType.STRING;

            int inputTypeBits = 1 << (int)input1.dataType | 1 << (int)input2.dataType;

            if (m_compilation.compileOptions.integerArithmeticMode == IntegerArithmeticMode.AGGRESSIVE
                && input1.dataType == input2.dataType
                && isInteger(input1.dataType))
            {
                output.dataType = input1.dataType;
            }
            else if ((inputTypeBits & numberAddDataTypeMask) == inputTypeBits) {
                output.dataType = DataNodeType.NUMBER;
            }
            else if ((inputTypeBits & stringDataTypeMask) != 0 && (input1.isNotNull || input2.isNotNull)) {
                // This instruction is definitely a string concatenation if at least one input is
                // definitely not null or undefined, because null + null => 0 and null + undefined => NaN
                output.dataType = DataNodeType.STRING;
            }
            else {
                output.dataType = DataNodeType.OBJECT;
                output.constant = new DataNodeConstant(objectClass);
                instr.data.add.argsAreAnyType = (inputTypeBits & anyDataTypeMask) != 0;
            }

            output.isNotNull = true;
        }

        private void _visitBinaryArithmeticOp(ref Instruction instr) {
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode input1 = ref m_compilation.getDataNode(inputIds[0]);
            ref DataNode input2 = ref m_compilation.getDataNode(inputIds[1]);

            output.constant = default;
            output.isConstant = false;
            output.isNotNull = false;

            if (tryEvalBinaryOp(ref input1, ref input2, ref output, instr.opcode))
                return;

            ABCOp opcode = instr.opcode;

            if (((int)opcode >= (int)ABCOp.add_i && (int)opcode <= (int)ABCOp.multiply_i)
                || ((int)opcode >= (int)ABCOp.lshift && (int)opcode <= (int)ABCOp.bitxor))
            {
                output.dataType = (opcode == ABCOp.urshift) ? DataNodeType.UINT : DataNodeType.INT;
            }
            else {
                var integerMode = m_compilation.compileOptions.integerArithmeticMode;

                bool areInputsIntegersOfSameType =
                    isInteger(input1.dataType) && isInteger(input2.dataType)
                    && (input1.dataType == input2.dataType
                        || (input1.isConstant && input1.constant.intValue >= 0)
                        || (input2.isConstant && input2.constant.intValue >= 0));

                // If both are operands are of the same type then we can only use integer arithmetic
                // if aggressive integer arithmetic is enabled or the operation is part of an
                // expression that is coerced to an integer type (which is checked in the sweep step)
                // The modulo operation is an exception, as the result of that operation is always
                // representable as an integer whenever both inputs are integers of the same signedness.

                if (areInputsIntegersOfSameType
                    && (integerMode == IntegerArithmeticMode.AGGRESSIVE
                        || (opcode == ABCOp.modulo && integerMode == IntegerArithmeticMode.DEFAULT)))
                {
                    output.dataType = input1.dataType;
                }
                else {
                    output.dataType = DataNodeType.NUMBER;
                }
            }

            output.isNotNull = true;
        }

        private void _visitBinaryCompareOp(ref Instruction instr) {
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode input1 = ref m_compilation.getDataNode(inputIds[0]);
            ref DataNode input2 = ref m_compilation.getDataNode(inputIds[1]);

            output.constant = default;
            output.isConstant = false;
            output.isNotNull = false;

            if (tryEvalCompareOp(ref input1, ref input2, ref output, instr.opcode))
                return;

            output.dataType = DataNodeType.BOOL;
            output.isNotNull = true;
        }

        private void _visitAsTypeLate(ref Instruction instr) {
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode objectNode = ref m_compilation.getDataNode(inputIds[0]);
            ref DataNode typeNode = ref m_compilation.getDataNode(inputIds[1]);

            output.dataType = DataNodeType.OBJECT;
            output.isNotNull = false;

            Class targetClass = null;

            if (typeNode.isConstant && typeNode.dataType == DataNodeType.CLASS) {
                targetClass = typeNode.constant.classValue;
                if (ClassTagSet.primitive.contains(targetClass.tag))
                    targetClass = null;
            }

            if (targetClass == null)
                targetClass = objectClass;

            output.constant = new DataNodeConstant(targetClass);
        }

        private void _visitNewArrayObject(ref Instruction instr) {
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            pushed.dataType = DataNodeType.OBJECT;
            pushed.isNotNull = true;

            Class klass = (instr.opcode == ABCOp.newarray) ? arrayClass : objectClass;
            pushed.constant = new DataNodeConstant(klass);
        }

        private void _visitBinaryCompareBranch(ref Instruction instr) {
            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode input1 = ref m_compilation.getDataNode(inputIds[0]);
            ref DataNode input2 = ref m_compilation.getDataNode(inputIds[1]);
        }

        private void _visitApplyType(ref Instruction instr) {
            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode defNode = ref m_compilation.getDataNode(inputIds[0]);
            ref DataNode resultNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (inputIds.Length == 2
                && defNode.dataType == DataNodeType.CLASS
                && defNode.constant.classValue == vectorClass)
            {
                ref DataNode argNode = ref m_compilation.getDataNode(inputIds[1]);
                Class vectorClass = null;

                if (argNode.dataType == DataNodeType.CLASS)
                    vectorClass = argNode.constant.classValue.getVectorClass();
                else if (argNode.dataType == DataNodeType.NULL)
                    vectorClass = vectorAnyClass;

                if (vectorClass != null) {
                    resultNode.isConstant = true;
                    resultNode.isNotNull = true;
                    resultNode.dataType = DataNodeType.CLASS;
                    resultNode.constant = new DataNodeConstant(vectorClass);
                    return;
                }
            }

            resultNode.isConstant = false;
            resultNode.isNotNull = true;
            resultNode.dataType = DataNodeType.OBJECT;
            resultNode.constant = new DataNodeConstant(objectClass);
        }

        private void _visitDxns(ref Instruction instr) {
            ABCMethodInfo methodInfo = m_compilation.methodInfo;
            if ((methodInfo.flags & ABCMethodFlags.SET_DXNS) == 0)
                throw m_compilation.createError(ErrorCode.CANNOT_SET_DEFAULT_XMLNS, instr.id);

            m_compilation.setFlag(MethodCompilationFlags.SETS_DXNS);
        }

        private void _visitHasNext(ref Instruction instr) {
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            pushed.dataType = DataNodeType.INT;
            pushed.isNotNull = true;
        }

        private void _visitHasnext2(ref Instruction instr) {
            var nodeIds = m_compilation.staticIntArrayPool.getSpan(instr.data.hasnext2.nodeIds);
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            ref DataNode objLocal = ref m_compilation.getDataNode(nodeIds[2]);
            ref DataNode indLocal = ref m_compilation.getDataNode(nodeIds[3]);

            objLocal.dataType = DataNodeType.OBJECT;
            objLocal.constant = new DataNodeConstant(objectClass);
            objLocal.isNotNull = false;
            indLocal.dataType = DataNodeType.INT;
            indLocal.isNotNull = true;
            pushed.dataType = DataNodeType.BOOL;
            pushed.isNotNull = true;
        }

        private void _visitNextNameValue(ref Instruction instr) {
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            pushed.dataType = DataNodeType.ANY;
        }

        private void _visitGetGlobalScope(ref Instruction instr) {
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            pushed.dataType = DataNodeType.GLOBAL;
            pushed.isConstant = true;
            pushed.isNotNull = true;
        }

        private void _visitGetSetDeleteProperty(ref Instruction instr, bool isFirstVisit) {
            if (isFirstVisit) {
                instr.data.accessProperty.resolvedPropId = m_compilation.createResolvedProperty().id;
                _fixGenericPropertyMultiname(ref instr.data.accessProperty.multinameId, instr.id);
            }

            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            var multiname = m_compilation.abcFile.resolveMultiname(instr.data.accessProperty.multinameId);

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.accessProperty.resolvedPropId);
            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);

            // Fast path: if the object's type is unchanged from the last visit and there
            // are no runtime multiname arguments, no changes need to be made.
            if (!isFirstVisit && !multiname.hasRuntimeArguments
                && _resolvedPropObjectTypeMatches(ref resolvedProp, ref objectNode))
            {
                return;
            }

            // Fast path: if the object was pushed by a findproperty/findpropstrict instruction
            // with the same multiname and there are no runtime multiname arguments, the
            // ResolvedProperty can be copied from there.
            bool isResolvedFromFindProp = false;
            if (!multiname.hasRuntimeArguments) {
                isResolvedFromFindProp = _tryGetResolveInfoFromFindProp(
                    ref objectNode, instr.data.accessProperty.multinameId, ref resolvedProp);
            }

            if (!isResolvedFromFindProp) {
                _getRuntimeMultinameArgIds(stackPopIds.Slice(1), multiname, out int rtNsNodeId, out int rtNameNodeId);
                _resolvePropertyOnObject(ref objectNode, multiname, rtNsNodeId, rtNameNodeId, instr.id, ref resolvedProp);
            }

            switch (instr.opcode) {
                case ABCOp.getproperty: {
                    ref DataNode resultNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);
                    _resolveGetProperty(ref resolvedProp, ref resultNode);
                    break;
                }
                case ABCOp.setproperty:
                case ABCOp.initproperty:
                {
                    ref DataNode valueNode = ref m_compilation.getDataNode(stackPopIds[stackPopIds.Length - 1]);
                    bool isInit = instr.opcode == ABCOp.initproperty;
                    _resolveSetProperty(ref resolvedProp, ref valueNode, isInit);
                    break;
                }
                case ABCOp.deleteproperty: {
                    ref DataNode resultNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);
                    _resolveDeleteProperty(ref resolvedProp, ref resultNode);
                    break;
                }

                default:
                    Debug.Assert(false);    // Should not reach here.
                    break;
            }
        }

        private void _visitIn(ref Instruction instr) {
            ref DataNode result = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            result.dataType = DataNodeType.BOOL;
            result.isNotNull = true;
        }

        private void _visitCallOrConstructProp(ref Instruction instr, bool isFirstVisit) {
            if (isFirstVisit) {
                instr.data.callProperty.resolvedPropId = m_compilation.createResolvedProperty().id;
                _fixGenericPropertyMultiname(ref instr.data.callProperty.multinameId, instr.id);
            }

            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            var multiname = m_compilation.abcFile.resolveMultiname(instr.data.callProperty.multinameId);

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.callProperty.resolvedPropId);
            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);

            // Fast path: if the object's type is unchanged from the last visit and there
            // are no runtime multiname arguments, no changes need to be made.
            // Don't do this for intrinsics with arguments though, as the intrinsic may be
            // dependent on the types of the arguments.
            if (!isFirstVisit && !multiname.hasRuntimeArguments
                && (resolvedProp.propKind != ResolvedPropertyKind.INTRINSIC || instr.data.callProperty.argCount == 0)
                && _resolvedPropObjectTypeMatches(ref resolvedProp, ref objectNode))
            {
                return;
            }

            // Fast path: if the object was pushed by a findproperty/findpropstrict instruction
            // with the same multiname and there are no runtime multiname arguments, the
            // ResolvedProperty can be copied from there.
            bool isResolvedFromFindProp = false;
            if (!multiname.hasRuntimeArguments) {
                isResolvedFromFindProp = _tryGetResolveInfoFromFindProp(
                    ref objectNode, instr.data.callProperty.multinameId, ref resolvedProp);
            }

            if (!isResolvedFromFindProp) {
                _getRuntimeMultinameArgIds(stackPopIds.Slice(1), multiname, out int rtNsNodeId, out int rtNameNodeId);
                _resolvePropertyOnObject(ref objectNode, multiname, rtNsNodeId, rtNameNodeId, instr.id, ref resolvedProp);
            }

            var argsOnStackIds = stackPopIds.Slice(stackPopIds.Length - instr.data.callProperty.argCount);

            _resolveCallOrConstructProperty(ref resolvedProp, instr.opcode, instr.stackPushedNodeId, argsOnStackIds);
        }

        private void _visitGetSetSuper(ref Instruction instr, bool isFirstVisit) {
            if (isFirstVisit)
                instr.data.accessProperty.resolvedPropId = m_compilation.createResolvedProperty().id;

            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            var multiname = m_compilation.abcFile.resolveMultiname(instr.data.accessProperty.multinameId);

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.accessProperty.resolvedPropId);
            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);

            if (objectNode.dataType != DataNodeType.THIS)
                throw m_compilation.createError(ErrorCode.ILLEGAL_SUPER, instr.id);

            // Fast path: if there are no runtime multiname arguments, no changes need to be made
            // after the first visit.
            if (!isFirstVisit && !multiname.hasRuntimeArguments)
                return;

            _getRuntimeMultinameArgIds(stackPopIds.Slice(1), multiname, out int rtNsNodeId, out int rtNameNodeId);
            _resolveInstancePropertyOnClass(m_compilation.declaringClass.parent, multiname, rtNsNodeId, rtNameNodeId, instr.id, ref resolvedProp);

            if (instr.opcode == ABCOp.getsuper) {
                ref DataNode resultNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);
                _resolveGetProperty(ref resolvedProp, ref resultNode);
            }
            else {
                ref DataNode valueNode = ref m_compilation.getDataNode(stackPopIds[stackPopIds.Length - 1]);
                _resolveSetProperty(ref resolvedProp, ref valueNode, false);
            }
        }

        private void _visitCallSuper(ref Instruction instr, bool isFirstVisit) {
            if (isFirstVisit)
                instr.data.callProperty.resolvedPropId = m_compilation.createResolvedProperty().id;

            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            var multiname = m_compilation.abcFile.resolveMultiname(instr.data.callProperty.multinameId);

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.callProperty.resolvedPropId);
            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);

            if (objectNode.dataType != DataNodeType.THIS)
                throw m_compilation.createError(ErrorCode.ILLEGAL_SUPER, instr.id);

            // Fast path: if there are no runtime multiname arguments, no changes need to be made
            // after the first visit.
            if (!isFirstVisit && !multiname.hasRuntimeArguments)
                return;

            _getRuntimeMultinameArgIds(stackPopIds.Slice(1), multiname, out int rtNsNodeId, out int rtNameNodeId);
            _resolveInstancePropertyOnClass(m_compilation.declaringClass.parent, multiname, rtNsNodeId, rtNameNodeId, instr.id, ref resolvedProp);

            var argsOnStackIds = stackPopIds.Slice(stackPopIds.Length - instr.data.callProperty.argCount);
            _resolveCallOrConstructProperty(ref resolvedProp, instr.opcode, instr.stackPushedNodeId, argsOnStackIds);
        }

        private void _visitConstructSuper(ref Instruction instr, bool isFirstVisit) {
            if (!isFirstVisit)
                return;

            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);

            if (objectNode.dataType != DataNodeType.THIS || m_compilation.getCurrentConstructor() == null)
                throw m_compilation.createError(ErrorCode.ILLEGAL_SUPER, instr.id);
        }

        private void _visitGetSetSlot(ref Instruction instr, bool isFirstVisit) {
            if (isFirstVisit)
                instr.data.getSetSlot.resolvedPropId = m_compilation.createResolvedProperty().id;

            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode objectNode = ref m_compilation.getDataNode(stackPopIds[0]);
            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.getSetSlot.resolvedPropId);

            if (!isFirstVisit && _resolvedPropObjectTypeMatches(ref resolvedProp, ref objectNode))
                return;

            _resolveSlotOnObject(ref objectNode, instr.data.getSetSlot.slotId, instr.id, ref resolvedProp);

            if (instr.opcode == ABCOp.getslot) {
                ref DataNode resultNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);
                _resolveGetProperty(ref resolvedProp, ref resultNode);
            }
            else {
                ref DataNode valueNode = ref m_compilation.getDataNode(stackPopIds[stackPopIds.Length - 1]);
                _resolveSetProperty(ref resolvedProp, ref valueNode, false);
            }
        }

        private void _visitGetSetGlobalSlot(ref Instruction instr, bool isFirstVisit) {
            if (!isFirstVisit)
                return;

            instr.data.getSetSlot.resolvedPropId = m_compilation.createResolvedProperty().id;

            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.getSetSlot.resolvedPropId);

            int slotId = instr.data.getSetSlot.slotId;
            Trait slotTrait;
            using (var lockedContext = m_compilation.getContext())
                slotTrait = lockedContext.value.getScriptTraitSlot(m_compilation.currentScriptInfo, slotId);

            if (slotTrait == null)
                throw m_compilation.createError(ErrorCode.ILLEGAL_EARLY_BINDING, instr.id, "global");

            resolvedProp.objectType = DataNodeType.GLOBAL;
            resolvedProp.propKind = ResolvedPropertyKind.TRAIT;
            resolvedProp.propInfo = slotTrait;

            if (instr.opcode == ABCOp.getglobalslot) {
                ref DataNode resultNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);
                _resolveGetProperty(ref resolvedProp, ref resultNode);
            }
            else {
                ref DataNode valueNode = ref m_compilation.getDataNode(stackPopIds[stackPopIds.Length - 1]);
                _resolveSetProperty(ref resolvedProp, ref valueNode, false);
            }
        }

        private void _visitCall(ref Instruction instr, bool isFirstVisit) {
            if (isFirstVisit)
                instr.data.callOrConstruct.resolvedPropId = m_compilation.createResolvedProperty().id;

            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode function = ref m_compilation.getDataNode(stackPopIds[0]);

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.callOrConstruct.resolvedPropId);

            if (function.dataType == DataNodeType.FUNCTION) {
                resolvedProp.propKind = ResolvedPropertyKind.TRAIT;
                resolvedProp.propInfo = function.constant.methodValue;
            }
            else if (function.dataType == DataNodeType.CLASS) {
                resolvedProp.propKind = ResolvedPropertyKind.TRAIT;
                resolvedProp.propInfo = function.constant.classValue;
            }
            else {
                resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
                resolvedProp.propInfo = null;
            }

            var argsOnStackIds = stackPopIds.Slice(stackPopIds.Length - instr.data.callOrConstruct.argCount);
            _resolveCallOrConstructProperty(ref resolvedProp, instr.opcode, instr.stackPushedNodeId, argsOnStackIds);
        }

        private void _visitConstruct(ref Instruction instr, bool isFirstVisit) {
            if (isFirstVisit)
                instr.data.callOrConstruct.resolvedPropId = m_compilation.createResolvedProperty().id;

            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode function = ref m_compilation.getDataNode(stackPopIds[0]);

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.callOrConstruct.resolvedPropId);

            if (function.dataType == DataNodeType.CLASS) {
                resolvedProp.propKind = ResolvedPropertyKind.TRAIT;
                resolvedProp.propInfo = function.constant.classValue;
            }
            else {
                resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
                resolvedProp.propInfo = null;
            }

            var argsOnStackIds = stackPopIds.Slice(stackPopIds.Length - instr.data.callOrConstruct.argCount);
            _resolveCallOrConstructProperty(ref resolvedProp, instr.opcode, instr.stackPushedNodeId, argsOnStackIds);
        }

        private void _visitCallMethodOrStatic(ref Instruction instr, bool isFirstVisit) {
            if (isFirstVisit)
                instr.data.callMethod.resolvedPropId = m_compilation.createResolvedProperty().id;

            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode obj = ref m_compilation.getDataNode(stackPopIds[0]);

            int methodId = instr.data.callMethod.methodOrDispId;
            MethodTrait method;

            if (instr.opcode == ABCOp.callmethod) {
                (Class klass, bool isStatic) = (obj.dataType == DataNodeType.CLASS)
                    ? (obj.constant.classValue, true)
                    : (m_compilation.getDataNodeClass(obj), false);

                method = (klass as ScriptClass)?.getMethodByDispId(instr.data.callMethod.methodOrDispId, isStatic);
                if (method == null) {
                    throw m_compilation.createError(
                        ErrorCode.DISP_ID_OUT_OF_RANGE,
                        instr.data.callMethod.methodOrDispId,
                        klass.name.ToString()
                    );
                }
            }
            else {
                // callstatic
                using (var lockedContext = m_compilation.getContext()) {
                    method = lockedContext.value.getMethodOrCtorForMethodInfo(methodId) as MethodTrait;

                    if (method == null || lockedContext.value.isMethodUsedAsFunction(method))
                        throw m_compilation.createError(ErrorCode.MARIANA__ABC_CALLSTATIC_METHOD_NOT_SUPPORTED, instr.id, methodId);
                }
            }

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.callMethod.resolvedPropId);
            resolvedProp.propKind = ResolvedPropertyKind.TRAIT;
            resolvedProp.propInfo = method;

            var argsOnStackIds = stackPopIds.Slice(1);
            _resolveCallOrConstructProperty(ref resolvedProp, instr.opcode, instr.stackPushedNodeId, argsOnStackIds);
        }

        private void _visitFindProperty(ref Instruction instr, bool isFirstVisit) {
            if (isFirstVisit) {
                instr.data.findProperty.resolvedPropId = m_compilation.createResolvedProperty().id;
                _fixGenericPropertyMultiname(ref instr.data.findProperty.multinameId, instr.id);
            }

            if (!isFirstVisit && !m_scopeStateChangedFromLastVisit)
                // Fast path: No change in the scope stack from the previous visit
                return;

            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            var multiname = m_compilation.abcFile.resolveMultiname(instr.data.findProperty.multinameId);

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.findProperty.resolvedPropId);
            ref LocalOrCapturedScopeRef scopeRef = ref instr.data.findProperty.scopeRef;

            _getRuntimeMultinameArgIds(stackPopIds, multiname, out int rtNsNodeId, out int rtNameNodeId);
            _resolvePropertyInCurrentScope(multiname, rtNsNodeId, rtNameNodeId, instr.id, ref scopeRef, ref resolvedProp);

            ref DataNode obj = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            obj.dataType = resolvedProp.objectType;
            obj.constant = (resolvedProp.objectClass != null) ? new DataNodeConstant(resolvedProp.objectClass) : default;
            obj.isConstant = isConstantType(obj.dataType);
            obj.isNotNull = true;
        }

        private void _visitGetLex(ref Instruction instr, bool isFirstVisit) {
            if (isFirstVisit) {
                instr.data.findProperty.resolvedPropId = m_compilation.createResolvedProperty().id;
                _fixGenericPropertyMultiname(ref instr.data.findProperty.multinameId, instr.id);
            }

            if (!isFirstVisit && !m_scopeStateChangedFromLastVisit)
                // Fast path: No change in the scope stack from the previous visit
                return;

            var multiname = m_compilation.abcFile.resolveMultiname(instr.data.findProperty.multinameId);
            if (multiname.hasRuntimeArguments)
                throw m_compilation.createError(ErrorCode.ILLEGAL_OPCODE_NAME_COMBINATION, instr.id, instr.opcode, multiname.kind);

            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.findProperty.resolvedPropId);
            ref LocalOrCapturedScopeRef scopeRef = ref instr.data.findProperty.scopeRef;

            _resolvePropertyInCurrentScope(multiname, -1, -1, instr.id, ref scopeRef, ref resolvedProp);

            ref DataNode result = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            _resolveGetProperty(ref resolvedProp, ref result);
        }

        private void _visitNewActivation(ref Instruction instr) {
            if ((m_compilation.methodInfo.flags & ABCMethodFlags.NEED_ACTIVATION) == 0)
                throw m_compilation.createError(ErrorCode.OP_NEWACTIVATION_NO_FLAG, instr.id);

            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            pushed.dataType = DataNodeType.OBJECT;
            pushed.constant = new DataNodeConstant(m_compilation.getClassForActivation());
            pushed.isNotNull = true;
        }

        private void _visitNewCatch(ref Instruction instr) {
            int excInfoId = instr.data.newCatch.excInfoId;

            if ((uint)excInfoId >= (uint)m_compilation.methodBodyInfo.getExceptionInfo().length)
                throw m_compilation.createError(ErrorCode.MARIANA__ABC_NEWCATCH_INVALID_INDEX, instr.id);

            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            pushed.dataType = DataNodeType.OBJECT;
            pushed.constant = new DataNodeConstant(m_compilation.getClassForCatchScope(excInfoId));
            pushed.isNotNull = true;
        }

        private void _visitNewClass(ref Instruction instr) {
            ref DataNode baseClassNode = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            Class klass;
            using (var lockedContext = m_compilation.getContext())
                klass = lockedContext.value.getClassFromClassInfo(instr.data.newClass.classInfoId);

            bool isCorrectBaseClass = klass.isInterface
                ? baseClassNode.dataType == DataNodeType.NULL
                : baseClassNode.dataType == DataNodeType.CLASS && baseClassNode.constant.classValue == klass.parent;

            if (!isCorrectBaseClass)
                throw m_compilation.createError(ErrorCode.OP_NEWCLASS_WRONG_BASE, instr.id);

            pushed.dataType = DataNodeType.CLASS;
            pushed.constant = new DataNodeConstant(klass);
            pushed.isConstant = true;
            pushed.isNotNull = true;
        }

        private void _visitNewFunction(ref Instruction instr) {
            ref DataNode pushed = ref m_compilation.getDataNode(instr.stackPushedNodeId);
            pushed.dataType = DataNodeType.OBJECT;
            pushed.constant = new DataNodeConstant(functionClass);
            pushed.isNotNull = true;
        }

        /// <summary>
        /// Sets the type of a data node to the return type of a method.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <param name="method">A <see cref="MethodTrait"/> instance representing the method.</param>
        private static void _setDataNodeToMethodReturn(ref DataNode node, MethodTrait method) {
            if (method.hasReturn) {
                node.setDataTypeFromClass(method.returnType);
                node.isConstant = false;
            }
            else {
                node.dataType = DataNodeType.UNDEFINED;
                node.isConstant = true;
                node.isNotNull = false;
            }
        }

        /// <summary>
        /// Determines the node ids of the runtime namespace and/or name arguments popped from
        /// the stack for a multiname lookup.
        /// </summary>
        /// <param name="stackNodeIds">A read-only span containing the data node ids on the stack,
        /// with the runtime name arguments at the beginning.</param>
        /// <param name="multiname">The multiname for which to obtain the runtime name arguments.</param>
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
        /// Attempts to obtain the property resolution information for a property lookup where
        /// the object was obtained from a prior scope stack lookup (findproperty/findpropstrict).
        /// </summary>
        /// <param name="objectNode">A reference to the <see cref="DataNode"/> representing the object on
        /// which the property is to be resolved.</param>
        /// <param name="multinameId">The index of the multiname in the ABC constant pool for the
        /// property name.</param>
        /// <param name="resolvedProp">A reference to a <see cref="ResolvedProperty"/> instance. If resolution
        /// information is available from a prior scope stack lookup, it will be copied to this instance.</param>
        /// <returns>True if resolution information could be obtained from a prior scope stack lookup,
        /// otherwise false.</returns>
        private bool _tryGetResolveInfoFromFindProp(ref DataNode objectNode, int multinameId, ref ResolvedProperty resolvedProp) {
            int objPushInstrId = m_compilation.getStackNodePushInstrId(ref objectNode);
            if (objPushInstrId == -1)
                return false;

            ref Instruction pushInstr = ref m_compilation.getInstruction(objPushInstrId);

            if ((pushInstr.opcode == ABCOp.findproperty || pushInstr.opcode == ABCOp.findpropstrict)
                && !pushInstr.data.findProperty.scopeRef.isNull
                && pushInstr.data.findProperty.multinameId == multinameId)
            {
                resolvedProp = m_compilation.getResolvedProperty(pushInstr.data.findProperty.resolvedPropId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the object type of a <see cref="ResolvedProperty"/> matches the
        /// type of a data node.
        /// </summary>
        /// <param name="prop">A reference to a <see cref="ResolvedProperty"/> instance.</param>
        /// <param name="obj">A reference to a <see cref="DataNode"/> instance representing the
        /// object node.</param>
        /// <returns>True if the type of the node <paramref name="obj"/> matches the object
        /// type in <paramref name="prop"/>, otherwise false.</returns>
        private bool _resolvedPropObjectTypeMatches(ref ResolvedProperty prop, ref DataNode obj) {
            if (prop.objectType != obj.dataType)
                return false;

            if (obj.dataType == DataNodeType.CLASS || obj.dataType == DataNodeType.OBJECT)
                return prop.objectClass == obj.constant.classValue;

            return true;
        }

        private void _fixGenericPropertyMultiname(ref int multinameId, int instrId) {
            ABCMultiname mn = m_compilation.abcFile.resolveMultiname(multinameId);

            if (mn.kind == ABCConstKind.GenericClassName) {
                // If the argument is a GenericClassName, Flash Player ignores the type arguments
                // provided that the number of type arguments is exactly 1, and the definition name
                // is not a GenericClassName. An implication of this is that resolving the name
                // Vector.<T> on the global object gives Vector, not Vector.<T>. (To supply a type
                // argument to Vector, the applytype opcode must be used.)

                if (m_compilation.abcFile.resolveGenericArgList(mn.genericArgListIndex).length != 1)
                    throw m_compilation.createError(ErrorCode.MARIANA__ABC_INVALID_USE_GENERIC_NAME, instrId);

                var defName = m_compilation.abcFile.resolveMultiname(mn.genericDefIndex);
                if (defName.kind == ABCConstKind.GenericClassName)
                    throw m_compilation.createError(ErrorCode.MARIANA__ABC_INVALID_USE_GENERIC_NAME, instrId);

                multinameId = mn.genericDefIndex;
            }
        }

        /// <summary>
        /// Extracts the namespace, namespace and local name components from a multiname.
        /// </summary>
        /// <param name="multiname">An <see cref="ABCMultiname"/> instance representing the multiname.</param>
        /// <param name="rtNsNodeId">The node id of the runtime namespace argument on the stack, if
        /// the kind of <paramref name="multiname"/> requires a runtime namespace argument. Otherwise
        /// this is ignored.</param>
        /// <param name="rtNameNodeId">The node id of the runtime name argument on the stack, if
        /// the kind of <paramref name="multiname"/> requires a runtime local name argument. Otherwise
        /// this is ignored.</param>
        /// <param name="localName">The extracted local name, null if the name could not be extracted
        /// (i.e. it is only available at runtime).</param>
        /// <param name="ns">The extracted namespace, null if the kind of <paramref name="multiname"/>
        /// uses a namespace set or the namespace is only available at runtime.</param>
        /// <param name="nsSet">The extracted namespace set, null if the kind of <paramref name="multiname"/>
        /// does not use a namespace set.</param>
        private void _extractMultinameComponents(
            in ABCMultiname multiname,
            int rtNsNodeId,
            int rtNameNodeId,
            out string localName,
            out Namespace? ns,
            out NamespaceSet? nsSet
        ) {
            localName = null;
            ns = null;
            nsSet = null;

            // We only extract constant values from runtime name arguments on the stack if they
            // are trivially elidable (i.e. they are not phi nodes and have only one use). This
            // is to avoid having to generate IL for popping and re-pushing values further up
            // the stack (e.g. function call arguments) to remove the name arguments should they
            // not be necessary due to a successful compile-time name resolution.

            if (multiname.usesNamespaceSet) {
                nsSet = m_compilation.abcFile.resolveNamespaceSet(multiname.namespaceIndex);
            }
            else if (!multiname.hasRuntimeNamespace) {
                ns = m_compilation.abcFile.resolveNamespace(multiname.namespaceIndex);
            }
            else {
                Debug.Assert(rtNsNodeId >= 0);
                ref DataNode node = ref m_compilation.getDataNode(rtNsNodeId);

                if (node.dataType == DataNodeType.NAMESPACE && node.isConstant
                    && !node.isPhi && m_compilation.getDataNodeUseCount(ref node) == 1)
                {
                    ns = node.constant.namespaceValue;
                }
            }

            if (!multiname.hasRuntimeLocalName) {
                localName = m_compilation.abcFile.resolveString(multiname.localNameIndex);
            }
            else {
                Debug.Assert(rtNameNodeId >= 0);
                ref DataNode node = ref m_compilation.getDataNode(rtNameNodeId);

                if (node.isConstant && !node.isPhi && m_compilation.getDataNodeUseCount(ref node) == 1) {
                    if (node.dataType == DataNodeType.STRING) {
                        localName = node.constant.stringValue;
                    }
                    else if (node.dataType == DataNodeType.QNAME && (ns != null || nsSet != null)) {
                        // QNames override any multiname-provided namespace.
                        QName qname = node.constant.qnameValue;
                        ns = qname.ns;
                        nsSet = null;
                        localName = qname.localName;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a value indicating whether the number of arguments passed to the given method
        /// is valid.
        /// </summary>
        /// <param name="method">The method being called.</param>
        /// <param name="argCount">The number of passed arguments.</param>
        /// <returns>True if <paramref name="argCount"/> is a valid argument count for
        /// <paramref name="method"/>, otherwise false.</returns>
        private static bool _isValidMethodArgCount(MethodTrait method, int argCount) =>
            argCount >= method.requiredParamCount && (method.hasRest || argCount <= method.paramCount);

        /// <summary>
        /// Returns a value indicating whether the number of arguments passed to the given constructor
        /// is valid.
        /// </summary>
        /// <param name="ctor">The constructor being called.</param>
        /// <param name="argCount">The number of passed arguments.</param>
        /// <returns>True if <paramref name="argCount"/> is a valid argument count for
        /// <paramref name="ctor"/>, otherwise false.</returns>
        private static bool _isValidCtorArgCount(ClassConstructor ctor, int argCount) =>
            argCount >= ctor.requiredParamCount && (ctor.hasRest || argCount <= ctor.paramCount);

        /// <summary>
        /// Resolves a property on an object.
        /// </summary>
        /// <param name="obj">A reference to a <see cref="DataNode"/> representing the object on
        /// which the property is to be resolved.</param>
        /// <param name="multiname">An <see cref="ABCMultiname"/> instance representing the property name.</param>
        /// <param name="rtNsNodeId">The node id of the runtime namespace argument on the stack, if
        /// the kind of <paramref name="multiname"/> requires a runtime namespace argument. Otherwise
        /// this is ignored.</param>
        /// <param name="rtNameNodeId">The node id of the runtime name argument on the stack, if
        /// the kind of <paramref name="multiname"/> requires a runtime local name argument. Otherwise
        /// this is ignored.</param>
        /// <param name="instrId">The id of the instruction that requested the property resolution.</param>
        /// <param name="resolvedProp">A <see cref="ResolvedProperty"/> instance into which the resolution
        /// information will be written.</param>
        private void _resolvePropertyOnObject(
            ref DataNode obj, in ABCMultiname multiname, int rtNsNodeId, int rtNameNodeId, int instrId, ref ResolvedProperty resolvedProp)
        {
            Debug.Assert(obj.dataType != DataNodeType.UNKNOWN);

            resolvedProp.objectType = obj.dataType;
            resolvedProp.rtNamespaceType = (rtNsNodeId != -1) ? m_compilation.getDataNode(rtNsNodeId).dataType : DataNodeType.UNKNOWN;
            resolvedProp.rtNameType = (rtNameNodeId != -1) ? m_compilation.getDataNode(rtNameNodeId).dataType : DataNodeType.UNKNOWN;

            if (obj.dataType == DataNodeType.OBJECT || obj.dataType == DataNodeType.CLASS)
                resolvedProp.objectClass = obj.constant.classValue;
            else
                resolvedProp.objectClass = null;

            resolvedProp.propKind = ResolvedPropertyKind.UNKNOWN;
            resolvedProp.propInfo = null;

            _extractMultinameComponents(
                multiname, rtNsNodeId, rtNameNodeId, out string localName, out Namespace? ns, out NamespaceSet? nsSet);

            if (localName == null) {
                if ((resolvedProp.objectType == DataNodeType.OBJECT || resolvedProp.objectType == DataNodeType.REST)
                    && isNumeric(resolvedProp.rtNameType)
                    && (ns.GetValueOrDefault().isPublic || nsSet.GetValueOrDefault().containsPublic))
                {
                    Class klass = (resolvedProp.objectType == DataNodeType.REST) ? arrayClass : resolvedProp.objectClass;
                    var specials = klass.classSpecials;

                    if (specials != null) {
                        if (resolvedProp.rtNameType == DataNodeType.INT && specials.intIndexProperty != null) {
                            resolvedProp.propKind = ResolvedPropertyKind.INDEX;
                            resolvedProp.propInfo = specials.intIndexProperty;
                        }
                        else if (resolvedProp.rtNameType == DataNodeType.UINT && specials.uintIndexProperty != null) {
                            resolvedProp.propKind = ResolvedPropertyKind.INDEX;
                            resolvedProp.propInfo = specials.uintIndexProperty;
                        }
                        else if (specials.numberIndexProperty != null) {
                            resolvedProp.propKind = ResolvedPropertyKind.INDEX;
                            resolvedProp.propInfo = specials.numberIndexProperty;
                        }
                    }
                }
            }
            else if ((ns == null && nsSet == null)
                || (nsSet.GetValueOrDefault().count > 1 && (obj.flags & DataNodeFlags.LATE_MULTINAME_BINDING) != 0))
            {
                resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
            }
            else if (obj.dataType == DataNodeType.GLOBAL) {
                // Search the global scope (i.e. the application domain)
                var domain = m_compilation.applicationDomain;

                Trait trait;
                BindStatus status = (nsSet != null)
                    ? domain.lookupGlobalTrait(localName, nsSet.GetValueOrDefault(), false, out trait)
                    : domain.lookupGlobalTrait(new QName(ns.GetValueOrDefault(), localName), false, out trait);

                if (status == BindStatus.SUCCESS) {
                    resolvedProp.propKind = ResolvedPropertyKind.TRAIT;
                    resolvedProp.propInfo = trait;
                }
            }
            else if (obj.dataType != DataNodeType.ANY
                && obj.dataType != DataNodeType.NULL
                && obj.dataType != DataNodeType.UNDEFINED)
            {
                Trait trait = null;

                if (resolvedProp.objectType == DataNodeType.CLASS) {
                    // If the object is a class constant, check for static traits on the class.
                    Trait staticTrait;

                    BindStatus status = (nsSet != null)
                        ? resolvedProp.objectClass.lookupTrait(localName, nsSet.GetValueOrDefault(), true, out staticTrait)
                        : resolvedProp.objectClass.lookupTrait(new QName(ns.GetValueOrDefault(), localName), true, out staticTrait);

                    if (status == BindStatus.SUCCESS)
                        trait = staticTrait;
                }

                if (trait == null) {
                    Class klass = (resolvedProp.objectType == DataNodeType.OBJECT)
                        ? resolvedProp.objectClass
                        : m_compilation.getDataNodeClass(obj);

                    Trait instanceTrait;

                    BindStatus status = (nsSet != null)
                        ? klass.lookupTrait(localName, nsSet.GetValueOrDefault(), false, out instanceTrait)
                        : klass.lookupTrait(new QName(ns.GetValueOrDefault(), localName), false, out instanceTrait);

                    if (status == BindStatus.SUCCESS)
                        trait = instanceTrait;
                }

                if (trait != null) {
                    resolvedProp.propKind = ResolvedPropertyKind.TRAIT;
                    resolvedProp.propInfo = trait;
                }
            }

            if (resolvedProp.propKind == ResolvedPropertyKind.UNKNOWN)
                // If nothing could be resolved then defer to runtime.
                resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;

        }

        /// <summary>
        /// Resolves an instance property on a class.
        /// </summary>
        /// <param name="klass">The class on which to resolve the property.</param>
        /// <param name="multiname">An <see cref="ABCMultiname"/> instance representing the property name.</param>
        /// <param name="rtNsNodeId">The node id of the runtime namespace argument on the stack, if
        /// the kind of <paramref name="multiname"/> requires a runtime namespace argument. Otherwise
        /// this is ignored.</param>
        /// <param name="rtNameNodeId">The node id of the runtime name argument on the stack, if
        /// the kind of <paramref name="multiname"/> requires a runtime local name argument. Otherwise
        /// this is ignored.</param>
        /// <param name="instrId">The id of the instruction that requested the property resolution.</param>
        /// <param name="resolvedProp">A <see cref="ResolvedProperty"/> instance into which the resolution
        /// information will be written.</param>
        private void _resolveInstancePropertyOnClass(
            Class klass, in ABCMultiname multiname, int rtNsNodeId, int rtNameNodeId, int instrId, ref ResolvedProperty resolvedProp)
        {
            resolvedProp.objectType = DataNodeType.OBJECT;
            resolvedProp.objectClass = klass;
            resolvedProp.rtNamespaceType = (rtNsNodeId != -1) ? m_compilation.getDataNode(rtNsNodeId).dataType : DataNodeType.UNKNOWN;
            resolvedProp.rtNameType = (rtNameNodeId != -1) ? m_compilation.getDataNode(rtNameNodeId).dataType : DataNodeType.UNKNOWN;

            resolvedProp.propKind = ResolvedPropertyKind.UNKNOWN;
            resolvedProp.propInfo = null;

            _extractMultinameComponents(
                multiname, rtNsNodeId, rtNameNodeId, out string localName, out Namespace? ns, out NamespaceSet? nsSet);

            if ((ns == null && nsSet == null) || localName == null) {
                resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
            }
            else {
                Trait instanceTrait;

                BindStatus status = (nsSet != null)
                    ? klass.lookupTrait(localName, nsSet.GetValueOrDefault(), false, out instanceTrait)
                    : klass.lookupTrait(new QName(ns.GetValueOrDefault(), localName), false, out instanceTrait);

                if (status == BindStatus.SUCCESS) {
                    resolvedProp.propKind = ResolvedPropertyKind.TRAIT;
                    resolvedProp.propInfo = instanceTrait;
                }
            }

            if (resolvedProp.propKind == ResolvedPropertyKind.UNKNOWN)
                // If nothing could be resolved then defer to runtime.
                resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
        }

        /// <summary>
        /// Resolves a property on an object from its slot index.
        /// </summary>
        /// <param name="obj">A reference to a <see cref="DataNode"/> representing the object on
        /// which the property is to be resolved.</param>
        /// <param name="slotId">The slot index of the property.</param>
        /// <param name="instrId">The id of the instruction that requested the property resolution.</param>
        /// <param name="resolvedProp">A <see cref="ResolvedProperty"/> instance into which the resolution
        /// information will be written.</param>
        private void _resolveSlotOnObject(ref DataNode obj, int slotId, int instrId, ref ResolvedProperty resolvedProp) {
            resolvedProp.objectType = obj.dataType;

            if (obj.dataType == DataNodeType.OBJECT || obj.dataType == DataNodeType.CLASS)
                resolvedProp.objectClass = obj.constant.classValue;
            else
                resolvedProp.objectClass = null;

            resolvedProp.rtNamespaceType = DataNodeType.UNKNOWN;
            resolvedProp.rtNameType = DataNodeType.UNKNOWN;
            resolvedProp.propKind = ResolvedPropertyKind.UNKNOWN;

            if (obj.dataType == DataNodeType.GLOBAL) {
                Trait trait;
                using (var lockedContext = m_compilation.getContext())
                    trait = lockedContext.value.getScriptTraitSlot(m_compilation.currentScriptInfo, slotId);

                if (trait != null) {
                    resolvedProp.propKind = ResolvedPropertyKind.TRAIT;
                    resolvedProp.propInfo = trait;
                }
            }
            else {
                (Class klass, bool isStatic) = (obj.dataType == DataNodeType.CLASS)
                    ? (obj.constant.classValue, true)
                    : (m_compilation.getDataNodeClass(obj), false);

                Trait trait = (klass is ScriptClass scriptClass) ? scriptClass.getTraitAtSlot(slotId, isStatic) : null;
                if (trait != null) {
                    resolvedProp.propKind = ResolvedPropertyKind.TRAIT;
                    resolvedProp.propInfo = trait;
                }
            }

            if (resolvedProp.propKind == ResolvedPropertyKind.UNKNOWN)
                throw m_compilation.createError(ErrorCode.ILLEGAL_EARLY_BINDING, instrId, m_compilation.getDataNodeTypeName(obj));
        }

        /// <summary>
        /// Resolves a property on the current scope stack.
        /// </summary>
        /// <param name="multiname">An <see cref="ABCMultiname"/> instance representing the property name.</param>
        /// <param name="rtNsNodeId">The node id of the runtime namespace argument on the stack, if
        /// the kind of <paramref name="multiname"/> requires a runtime namespace argument. Otherwise
        /// this is ignored.</param>
        /// <param name="rtNameNodeId">The node id of the runtime name argument on the stack, if
        /// the kind of <paramref name="multiname"/> requires a runtime local name argument. Otherwise
        /// this is ignored.</param>
        /// <param name="instrId">The id of the instruction that requested the property resolution.</param>
        /// <param name="scopeRef">A. output parameter into which a <see cref="LocalOrCapturedScopeRef"/>
        /// representing the object on the scope stack (local or captured) on which the property was
        /// resolved will be written. If lookup is to be deferred to runtime, this will be set to
        /// <see cref="LocalOrCapturedScopeRef.nullRef"/>.</param>
        /// <param name="resolvedProp">A <see cref="ResolvedProperty"/> instance into which the resolution
        /// information will be written.</param>
        private void _resolvePropertyInCurrentScope(
            in ABCMultiname multiname, int rtNsNodeId, int rtNameNodeId, int instrId,
            ref LocalOrCapturedScopeRef scopeRef, ref ResolvedProperty resolvedProp)
        {
            if (m_curScopeStackNodeIds.length == 0 && m_compilation.getCapturedScopeItems().length == 0)
                throw m_compilation.createError(ErrorCode.FINDPROPERTY_SCOPE_DEPTH_ZERO, instrId);

            scopeRef = LocalOrCapturedScopeRef.nullRef;

            resolvedProp.rtNamespaceType = (rtNsNodeId != -1) ? m_compilation.getDataNode(rtNsNodeId).dataType : DataNodeType.UNKNOWN;
            resolvedProp.rtNameType = (rtNameNodeId != -1) ? m_compilation.getDataNode(rtNameNodeId).dataType : DataNodeType.UNKNOWN;
            resolvedProp.propKind = ResolvedPropertyKind.UNKNOWN;
            resolvedProp.propInfo = null;

            _extractMultinameComponents(
                multiname, rtNsNodeId, rtNameNodeId, out string localName, out Namespace? ns, out NamespaceSet? nsSet);

            void setResolvedAtRuntime(ref ResolvedProperty _resolvedProp) {
                _resolvedProp.objectType = DataNodeType.OBJECT;
                _resolvedProp.objectClass = objectClass;
                _resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
                _resolvedProp.propInfo = null;
            }

            bool search(
                string _localName,
                in Namespace? _ns,
                in NamespaceSet? _nsSet,
                DataNodeType type,
                in DataNodeConstant constant,
                bool isWith,
                bool lateMultinameBinding,
                ref ResolvedProperty _resolvedProp,
                out bool deferToRuntime
            ) {
                _resolvedProp.objectType = type;
                _resolvedProp.objectClass = null;

                deferToRuntime = false;

                if (_nsSet.GetValueOrDefault().count > 1 && lateMultinameBinding) {
                    deferToRuntime = true;
                    return true;
                }

                if (type == DataNodeType.GLOBAL) {
                    // Search the global scope (i.e. the application domain)
                    var domain = m_compilation.applicationDomain;

                    Trait trait;
                    BindStatus status = (_nsSet != null)
                        ? domain.lookupGlobalTrait(_localName, _nsSet.GetValueOrDefault(), false, out trait)
                        : domain.lookupGlobalTrait(new QName(_ns.GetValueOrDefault(), _localName), false, out trait);

                    if (status == BindStatus.SUCCESS) {
                        _resolvedProp.propKind = ResolvedPropertyKind.TRAIT;
                        _resolvedProp.propInfo = trait;
                    }
                    else {
                        // The global scope must have its dynamic properties and prototype checked (including
                        // in non-with scopes), so defer to runtime.
                        setResolvedAtRuntime(ref _resolvedProp);
                        deferToRuntime = true;
                    }

                    return true;
                }
                else {
                    Trait trait = null;
                    Class klass;

                    if (type == DataNodeType.OBJECT)
                        klass = constant.classValue;
                    else if (type == DataNodeType.THIS)
                        klass = m_compilation.declaringClass;
                    else
                        klass = getClass(type);

                    if (type == DataNodeType.CLASS) {
                        _resolvedProp.objectClass = constant.classValue;

                        // If the object is a class constant, check for static traits on the class.
                        Trait staticTrait;

                        BindStatus status = (_nsSet != null)
                            ? _resolvedProp.objectClass.lookupTrait(_localName, _nsSet.GetValueOrDefault(), true, out staticTrait)
                            : _resolvedProp.objectClass.lookupTrait(new QName(_ns.GetValueOrDefault(), _localName), true, out staticTrait);

                        if (status == BindStatus.SUCCESS)
                            trait = staticTrait;
                    }

                    if (trait == null) {
                        if (type == DataNodeType.OBJECT)
                            _resolvedProp.objectClass = klass;

                        Trait instanceTrait;

                        BindStatus status = (_nsSet != null)
                            ? klass.lookupTrait(_localName, _nsSet.GetValueOrDefault(), false, out instanceTrait)
                            : klass.lookupTrait(new QName(_ns.GetValueOrDefault(), _localName), false, out instanceTrait);

                        if (status == BindStatus.SUCCESS)
                            trait = instanceTrait;
                    }

                    if (trait != null) {
                        _resolvedProp.propKind = ResolvedPropertyKind.TRAIT;
                        _resolvedProp.propInfo = trait;
                        return true;
                    }

                    // If an object was pushed onto the scope stack using pushwith (as opposed to pushscope),
                    // its dynamic properties must also be searched before going further down the stack. This
                    // means that the lookup must be deferred to runtime unless it is known that the object
                    // can never have dynamic properties, i.e. its class is non-dynamic and final.

                    if (isWith && (klass.isDynamic || !klass.isFinal)) {
                        setResolvedAtRuntime(ref _resolvedProp);
                        deferToRuntime = true;
                        return true;
                    }
                }

                return false;
            }

            if (localName == null || (ns == null && nsSet == null)) {
                // A non-constant runtime name argument is on the stack, so the lookup must be
                // deferred to runtime.
                setResolvedAtRuntime(ref resolvedProp);
                return;
            }
            // end search()

            var dataNodes = m_compilation.getDataNodes();
            var curScopeStack = m_curScopeStackNodeIds.asSpan();

            for (int i = curScopeStack.Length - 1; i >= 0; i--) {
                ref DataNode node = ref dataNodes[curScopeStack[i]];
                bool stopSearch = search(
                    localName,
                    ns,
                    nsSet,
                    node.dataType,
                    node.constant,
                    node.isWithScope,
                    (node.flags & DataNodeFlags.LATE_MULTINAME_BINDING) != 0,
                    ref resolvedProp,
                    out bool deferToRuntime
                );

                if (stopSearch) {
                    if (!deferToRuntime)
                        scopeRef = LocalOrCapturedScopeRef.forLocal(node.id);
                    return;
                }
            }

            var capturedScope = m_compilation.getCapturedScopeItems().asSpan();

            for (int i = capturedScope.Length - 1; i >= 0; i--) {
                ref readonly CapturedScopeItem captured = ref capturedScope[i];
                var constant = (captured.objClass != null) ? new DataNodeConstant(captured.objClass) : default;

                bool stopSearch = search(
                    localName,
                    ns,
                    nsSet,
                    captured.dataType,
                    constant,
                    captured.isWithScope,
                    captured.lateMultinameBinding,
                    ref resolvedProp,
                    out bool deferToRuntime
                );

                if (stopSearch) {
                    if (!deferToRuntime)
                        scopeRef = LocalOrCapturedScopeRef.forCaptured(i);
                    return;
                }
            }

            if (resolvedProp.propKind == ResolvedPropertyKind.UNKNOWN)
                setResolvedAtRuntime(ref resolvedProp);
        }

        private void _resolveGetProperty(ref ResolvedProperty resolvedProp, ref DataNode result) {
            result.isConstant = false;
            result.isNotNull = false;

            if (resolvedProp.propKind != ResolvedPropertyKind.INDEX
                && resolvedProp.objectType == DataNodeType.OBJECT
                && (resolvedProp.objectClass == objectClass || ClassTagSet.xmlOrXmlList.contains(resolvedProp.objectClass.tag)))
            {
                // getproperty on XML and XMLList (other than index access) is always resolved at runtime.
                resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
                resolvedProp.propInfo = null;
                result.dataType = DataNodeType.ANY;
                return;
            }

            switch (resolvedProp.propKind) {
                case ResolvedPropertyKind.TRAIT: {
                    var resolvedTrait = (Trait)resolvedProp.propInfo;

                    switch (resolvedTrait.traitType) {
                        case TraitType.FIELD:
                            result.setDataTypeFromClass(((FieldTrait)resolvedTrait).fieldType);
                            break;

                        case TraitType.PROPERTY: {
                            MethodTrait getter = ((PropertyTrait)resolvedTrait).getter;

                            if (getter != null && _isValidMethodArgCount(getter, 0)) {
                                result.setDataTypeFromClass(getter.returnType);
                            }
                            else {
                                resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
                                resolvedProp.propInfo = null;
                                result.dataType = DataNodeType.ANY;
                            }
                            break;
                        }

                        case TraitType.CONSTANT:
                            setToConstant(ref result, ((ConstantTrait)resolvedTrait).constantValue);
                            break;

                        case TraitType.METHOD: {
                            var method = (MethodTrait)resolvedTrait;
                            if (method.isStatic) {
                                result.dataType = DataNodeType.FUNCTION;
                                result.constant = new DataNodeConstant(method);
                                result.isConstant = true;
                            }
                            else {
                                result.dataType = DataNodeType.OBJECT;
                                result.constant = new DataNodeConstant(functionClass);
                            }
                            result.isNotNull = true;
                            break;
                        }

                        case TraitType.CLASS:
                            result.dataType = DataNodeType.CLASS;
                            result.constant = new DataNodeConstant((Class)resolvedTrait);
                            result.isConstant = true;
                            result.isNotNull = true;
                            break;
                    }

                    break;
                }

                case ResolvedPropertyKind.INDEX: {
                    MethodTrait getter = ((IndexProperty)resolvedProp.propInfo).getMethod;
                    if (getter != null && _isValidMethodArgCount(getter, 1)) {
                        result.setDataTypeFromClass(getter.returnType);
                    }
                    else {
                        resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
                        resolvedProp.propInfo = null;
                        result.dataType = DataNodeType.ANY;
                    }
                    break;
                }

                case ResolvedPropertyKind.RUNTIME:
                    result.dataType = DataNodeType.ANY;
                    break;
            }
        }

        /// <summary>
        /// Returns a value indicating whether assignment to the given trait is valid in the
        /// current context.
        /// </summary>
        /// <param name="trait">A <see cref="Trait"/> instance.</param>
        /// <param name="value">A reference to a <see cref="DataNode"/> representing the
        /// value being assigned to <paramref name="trait"/>.</param>
        /// <param name="isInit">True if the initproperty instruction is being used for assignment,
        /// false otherwise.</param>
        /// <returns>True if assignment of <paramref name="value"/> to <paramref name="trait"/>
        /// is valid in the current context.</returns>
        private bool _isTraitAssignmentValid(Trait trait, ref DataNode value, bool isInit) {
            switch (trait.traitType) {
                case TraitType.FIELD:
                    if (!((FieldTrait)trait).isReadOnly)
                        return true;
                    if (!isInit)
                        return false;

                    if (trait.declaringClass == null) {
                        if (!m_compilation.isAnyFlagSet(MethodCompilationFlags.IS_SCRIPT_INIT))
                            return false;

                        using (var lockedContext = m_compilation.getContext())
                            return lockedContext.value.getExportingScript(trait) == m_compilation.currentScriptInfo;
                    }
                    else if (trait.isStatic) {
                        return m_compilation.isAnyFlagSet(MethodCompilationFlags.IS_STATIC_INIT)
                            && trait.declaringClass == m_compilation.declaringClass;
                    }
                    else {
                        return m_compilation.getCurrentConstructor() != null
                            && trait.declaringClass == m_compilation.declaringClass;
                    }

                case TraitType.CLASS: {
                    if (!m_compilation.isAnyFlagSet(MethodCompilationFlags.IS_SCRIPT_INIT)
                        || value.dataType != DataNodeType.CLASS
                        || value.constant.classValue != trait)
                    {
                        return false;
                    }

                    using (var lockedContext = m_compilation.getContext())
                        return lockedContext.value.getExportingScript(trait) == m_compilation.currentScriptInfo;
                }

                case TraitType.CONSTANT:
                case TraitType.METHOD:
                    return false;

                case TraitType.PROPERTY: {
                    var prop = (PropertyTrait)trait;
                    return prop.setter != null && _isValidMethodArgCount(prop.setter, 1);
                }

                default:
                    return true;
            }
        }

        private void _resolveSetProperty(ref ResolvedProperty resolvedProp, ref DataNode value, bool isInit) {
            if (resolvedProp.propKind != ResolvedPropertyKind.INDEX
                && resolvedProp.objectType == DataNodeType.OBJECT
                && (resolvedProp.objectClass == objectClass || ClassTagSet.xmlOrXmlList.contains(resolvedProp.objectClass.tag)))
            {
                // setproperty on XML and XMLList (other than index access) is always resolved at runtime.
                resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
                resolvedProp.propInfo = null;
                return;
            }

            switch (resolvedProp.propKind) {
                case ResolvedPropertyKind.TRAIT: {
                    var resolvedTrait = (Trait)resolvedProp.propInfo;
                    if (!_isTraitAssignmentValid(resolvedTrait, ref value, isInit))
                        resolvedProp.propKind = ResolvedPropertyKind.TRAIT_RT_INVOKE;
                    break;
                }

                case ResolvedPropertyKind.INDEX: {
                    var setter = ((IndexProperty)resolvedProp.propInfo).setMethod;
                    if (setter == null || !_isValidMethodArgCount(setter, 2)) {
                        resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
                        resolvedProp.propInfo = null;
                    }
                    break;
                }
            }
        }

        private void _resolveDeleteProperty(ref ResolvedProperty resolvedProp, ref DataNode result) {
            result.dataType = DataNodeType.BOOL;
            result.isNotNull = true;

            switch (resolvedProp.propKind) {
                case ResolvedPropertyKind.INDEX:
                    if (((IndexProperty)resolvedProp.propInfo).deleteMethod == null)
                        goto default;
                    break;

                default:
                    resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
                    resolvedProp.propInfo = null;
                    break;
            }
        }

        private void _resolveCallOrConstructProperty(
            ref ResolvedProperty resolvedProp, ABCOp opcode, int resultNodeId, ReadOnlySpan<int> argsOnStackNodeIds)
        {
            bool isConstruct = opcode == ABCOp.construct || opcode == ABCOp.constructprop;

            // If there is no return value being pushed onto the stack (i.e. a callpropvoid or callsupervoid
            // instruction), write the result type to a dummy node.
            DataNode dummyNode = default;
            ref DataNode result = ref ((resultNodeId != -1) ? ref m_compilation.getDataNode(resultNodeId) : ref dummyNode);

            result.isConstant = false;
            result.isNotNull = false;
            result.constant = default;

            if (resolvedProp.propKind != ResolvedPropertyKind.TRAIT) {
                if (resolvedProp.propKind == ResolvedPropertyKind.INDEX
                    && ((IndexProperty)resolvedProp.propInfo).getMethod == null)
                {
                    resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
                    resolvedProp.propInfo = null;
                }

                result.dataType = DataNodeType.ANY;
                return;
            }

            var resolvedTrait = (Trait)resolvedProp.propInfo;

            var intrinsic = _resolveInvokeTraitIntrinsic(resolvedTrait, isConstruct, ref result, argsOnStackNodeIds);
            if (intrinsic != null) {
                resolvedProp.propKind = ResolvedPropertyKind.INTRINSIC;
                resolvedProp.propInfo = intrinsic;
                return;
            }

            switch (resolvedTrait.traitType) {
                case TraitType.FIELD:
                case TraitType.CONSTANT:
                    resolvedProp.propKind = ResolvedPropertyKind.TRAIT_RT_INVOKE;
                    result.dataType = DataNodeType.ANY;
                    break;

                case TraitType.PROPERTY:
                    if (((PropertyTrait)resolvedTrait).getter == null) {
                        resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
                        resolvedProp.propInfo = null;
                    }
                    else {
                        resolvedProp.propKind = ResolvedPropertyKind.TRAIT_RT_INVOKE;
                    }
                    result.dataType = DataNodeType.ANY;
                    break;

                case TraitType.METHOD: {
                    if (isConstruct) {
                        resolvedProp.propKind = ResolvedPropertyKind.TRAIT_RT_INVOKE;
                        result.dataType = DataNodeType.ANY;
                        break;
                    }

                    var resolvedMethod = (MethodTrait)resolvedTrait;
                    if (_isValidMethodArgCount(resolvedMethod, argsOnStackNodeIds.Length)) {
                        _setDataNodeToMethodReturn(ref result, resolvedMethod);
                    }
                    else if (!resolvedMethod.isFinal && !resolvedMethod.isStatic
                        && (opcode == ABCOp.callproperty || opcode == ABCOp.callproplex || opcode == ABCOp.callpropvoid))
                    {
                        // If the method is virtual and the argument count is not valid, we should do a
                        // full late binding where possible because an overridden method may have an
                        // implicit rest parameter from having the NEED_ARGUMENTS flag set.
                        resolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
                        result.dataType = DataNodeType.ANY;
                    }
                    else {
                        resolvedProp.propKind = ResolvedPropertyKind.TRAIT_RT_INVOKE;
                        result.dataType = DataNodeType.ANY;
                    }
                    break;
                }

                case TraitType.CLASS: {
                    var resolvedClass = (Class)resolvedTrait;

                    if (isConstruct) {
                        ClassConstructor ctor = resolvedClass.constructor;
                        if (ctor != null && _isValidCtorArgCount(ctor, argsOnStackNodeIds.Length)) {
                            // Constructor call.
                            result.setDataTypeFromClass(resolvedClass);
                            result.isNotNull = true;
                        }
                        else {
                            resolvedProp.propKind = ResolvedPropertyKind.TRAIT_RT_INVOKE;
                            result.dataType = DataNodeType.ANY;
                        }
                    }
                    else {
                        if ((resolvedClass.classSpecials == null || resolvedClass.classSpecials.specialInvoke == null)
                            && argsOnStackNodeIds.Length == 1)
                        {
                            // Call resolves to a type cast.
                            result.setDataTypeFromClass(resolvedClass);
                        }
                        else {
                            resolvedProp.propKind = ResolvedPropertyKind.TRAIT_RT_INVOKE;
                            result.dataType = DataNodeType.ANY;
                        }
                    }

                    break;
                }
            }
        }

        private Intrinsic _resolveInvokeTraitIntrinsic(
            Trait trait, bool isConstruct, ref DataNode result, ReadOnlySpan<int> argsOnStackIds)
        {
            if (trait is Class klass)
                return _resolveInvokeClassIntrinsic(klass, isConstruct, ref result, argsOnStackIds);

            if (trait is MethodTrait method)
                return _resolveInvokeMethodIntrinsic(method, isConstruct, ref result, argsOnStackIds);

            return null;
        }

        private Intrinsic _resolveInvokeMethodIntrinsic(
            MethodTrait method, bool isConstruct, ref DataNode result, ReadOnlySpan<int> argsOnStackIds)
        {
            if (isConstruct)
                return null;

            if ((method == mathMinTrait || method == mathMaxTrait) && argsOnStackIds.Length == 2) {
                ref DataNode arg1 = ref m_compilation.getDataNode(argsOnStackIds[0]);
                ref DataNode arg2 = ref m_compilation.getDataNode(argsOnStackIds[1]);

                result.isNotNull = true;

                if ((arg1.dataType == DataNodeType.INT || isConstantInt(ref arg1))
                    && (arg2.dataType == DataNodeType.INT || isConstantInt(ref arg2)))
                {
                    result.dataType = DataNodeType.INT;
                    return (method == mathMinTrait) ? Intrinsic.MATH_MIN_2_I : Intrinsic.MATH_MAX_2_I;
                }
                else if ((arg1.dataType == DataNodeType.UINT || isConstantUint(ref arg1))
                    && (arg2.dataType == DataNodeType.UINT || isConstantUint(ref arg2)))
                {
                    result.dataType = DataNodeType.UINT;
                    return (method == mathMinTrait) ? Intrinsic.MATH_MIN_2_U : Intrinsic.MATH_MAX_2_U;
                }
                else {
                    result.dataType = DataNodeType.NUMBER;
                    return (method == mathMinTrait) ? Intrinsic.MATH_MIN_2 : Intrinsic.MATH_MAX_2;
                }
            }

            if (argsOnStackIds.Length == 1
                && !method.isStatic
                && method.name.localName == "push"
                && method.name.ns == Namespace.AS3)
            {
                if (method.declaringClass.isVectorInstantiation && method.declaringClass != vectorAnyClass) {
                    result.dataType = DataNodeType.INT;
                    result.isNotNull = true;
                    return Intrinsic.VECTOR_T_PUSH_1(method.declaringClass);
                }
                else if (method.declaringClass == arrayClass) {
                    result.dataType = DataNodeType.UINT;
                    result.isNotNull = true;
                    return Intrinsic.ARRAY_PUSH_1;
                }
            }

            if ((method == strCharAtTrait || method == strCharCodeAtTrait) && argsOnStackIds.Length == 1) {
                ref DataNode arg = ref m_compilation.getDataNode(argsOnStackIds[0]);

                if (method == strCharAtTrait) {
                    result.dataType = DataNodeType.STRING;
                    result.isNotNull = true;
                    return isInteger(arg.dataType) ? Intrinsic.STRING_CHARAT_I : Intrinsic.STRING_CHARAT;
                }
                else {
                    result.dataType = DataNodeType.NUMBER;
                    result.isNotNull = true;
                    return isInteger(arg.dataType) ? Intrinsic.STRING_CCODEAT_I : Intrinsic.STRING_CCODEAT;
                }
            }

            return null;
        }

        private Intrinsic _resolveInvokeClassIntrinsic(
            Class klass, bool isConstruct, ref DataNode result, ReadOnlySpan<int> argsOnStackIds)
        {
            int argCount = argsOnStackIds.Length;

            void convertType(ref DataNode input, ref DataNode output, DataNodeType toType, ABCOp convertOpcode) {
                bool isConst = tryEvalUnaryOp(ref input, ref output, convertOpcode);
                if (!isConst) {
                    output.dataType = toType;
                    output.isConstant = false;
                    output.isNotNull = true;
                }
            }

            if (klass == objectClass) {
                result.dataType = DataNodeType.OBJECT;
                result.constant = new DataNodeConstant(objectClass);

                if (argCount == 0)
                    return Intrinsic.OBJECT_NEW_0;
                else if (argCount == 1)
                    return Intrinsic.OBJECT_NEW_1;
            }

            switch (klass.tag) {
                case ClassTag.INT: {
                    if (argCount == 0) {
                        setToConstant(ref result, 0);
                        return Intrinsic.INT_NEW_0;
                    }
                    else if (argCount == 1) {
                        ref DataNode input = ref m_compilation.getDataNode(argsOnStackIds[0]);
                        convertType(ref input, ref result, DataNodeType.INT, ABCOp.convert_i);
                        return Intrinsic.INT_NEW_1;
                    }
                    return null;
                }

                case ClassTag.UINT: {
                    if (argCount == 0) {
                        setToConstant(ref result, 0);
                        result.dataType = DataNodeType.UINT;
                        return Intrinsic.UINT_NEW_0;
                    }
                    else if (argCount == 1) {
                        ref DataNode input = ref m_compilation.getDataNode(argsOnStackIds[0]);
                        convertType(ref input, ref result, DataNodeType.UINT, ABCOp.convert_u);
                        return Intrinsic.UINT_NEW_1;
                    }
                    return null;
                }

                case ClassTag.NUMBER: {
                    if (argCount == 0) {
                        setToConstant(ref result, 0);
                        return Intrinsic.NUMBER_NEW_0;
                    }
                    else if (argCount == 1) {
                        ref DataNode input = ref m_compilation.getDataNode(argsOnStackIds[0]);
                        convertType(ref input, ref result, DataNodeType.NUMBER, ABCOp.convert_d);
                        return Intrinsic.NUMBER_NEW_1;
                    }
                    return null;
                }

                case ClassTag.STRING: {
                    if (argCount == 0) {
                        result.dataType = DataNodeType.STRING;
                        result.isConstant = true;
                        result.isNotNull = true;
                        result.constant = new DataNodeConstant("");
                        return Intrinsic.STRING_NEW_0;
                    }
                    else if (argCount == 1) {
                        ref DataNode input = ref m_compilation.getDataNode(argsOnStackIds[0]);
                        convertType(ref input, ref result, DataNodeType.STRING, ABCOp.convert_s);
                        return Intrinsic.STRING_NEW_1;
                    }
                    return null;
                }

                case ClassTag.BOOLEAN: {
                    if (argCount == 0) {
                        result.dataType = DataNodeType.BOOL;
                        result.isConstant = true;
                        result.isNotNull = true;
                        result.constant = new DataNodeConstant(false);
                        return Intrinsic.BOOLEAN_NEW_0;
                    }
                    else if (argCount == 1) {
                        ref DataNode input = ref m_compilation.getDataNode(argsOnStackIds[0]);
                        convertType(ref input, ref result, DataNodeType.BOOL, ABCOp.convert_b);
                        return Intrinsic.BOOLEAN_NEW_1;
                    }
                    return null;
                }

                case ClassTag.DATE:
                    if (!isConstruct) {
                        if (argCount == 0) {
                            result.dataType = DataNodeType.STRING;
                            result.isNotNull = true;
                            return Intrinsic.DATE_CALL_0;
                        }
                    }
                    else {
                        Intrinsic resolved = null;
                        if (argCount == 0) {
                            resolved = Intrinsic.DATE_NEW_0;
                        }
                        else if (argCount == 1) {
                            ref DataNode input = ref m_compilation.getDataNode(argsOnStackIds[0]);
                            if (isNumeric(input.dataType) || input.dataType == DataNodeType.STRING)
                                resolved = Intrinsic.DATE_NEW_1;
                        }
                        else if (argCount <= 7) {
                            bool areAllArgsNumeric = true;
                            for (int i = 0; i < argsOnStackIds.Length && areAllArgsNumeric; i++)
                                areAllArgsNumeric &= isNumeric(m_compilation.getDataNode(argsOnStackIds[i]).dataType);

                            if (areAllArgsNumeric)
                                resolved = Intrinsic.DATE_NEW_7;
                        }

                        if (resolved != null) {
                            result.dataType = DataNodeType.OBJECT;
                            result.constant = new DataNodeConstant(klass);
                            result.isNotNull = true;
                            return resolved;
                        }
                    }
                    return null;

                case ClassTag.ARRAY: {
                    result.dataType = DataNodeType.OBJECT;
                    result.constant = new DataNodeConstant(klass);
                    result.isNotNull = true;

                    if (argCount == 0) {
                        return Intrinsic.ARRAY_NEW_0;
                    }
                    else if (argCount == 1) {
                        ref DataNode input = ref m_compilation.getDataNode(argsOnStackIds[0]);
                        return isInteger(input.dataType) ? Intrinsic.ARRAY_NEW_1_LEN : null;
                    }

                    return Intrinsic.ARRAY_NEW;
                }

                case ClassTag.VECTOR:
                    if (!isConstruct) {
                        if (argCount == 1) {
                            result.setDataTypeFromClass(klass);
                            result.isNotNull = true;
                            return (klass == vectorAnyClass) ? Intrinsic.VECTOR_ANY_CALL_1 : Intrinsic.VECTOR_T_CALL_1(klass);
                        }
                    }
                    else {
                        if (klass == vectorAnyClass && argCount <= 2) {
                            result.setDataTypeFromClass(Class.fromType<ASVector<ASObject>>());
                            result.isNotNull = true;
                            return Intrinsic.VECTOR_ANY_CTOR;
                        }
                    }
                    return null;

                case ClassTag.REGEXP: {
                    result.dataType = DataNodeType.OBJECT;
                    result.constant = new DataNodeConstant(klass);
                    result.isNotNull = true;

                    if (argCount == 0)
                        return Intrinsic.REGEXP_NEW_PATTERN;

                    if (argCount == 1) {
                        ref DataNode arg = ref m_compilation.getDataNode(argsOnStackIds[0]);

                        if (arg.dataType == DataNodeType.OBJECT && arg.constant.classValue == klass)
                            return isConstruct ? Intrinsic.REGEXP_NEW_RE : Intrinsic.REGEXP_CALL_RE;

                        if (arg.dataType == DataNodeType.STRING)
                            return arg.isConstant ? Intrinsic.REGEXP_NEW_CONST : Intrinsic.REGEXP_NEW_PATTERN;
                    }
                    else if (argCount == 2) {
                        ref DataNode arg1 = ref m_compilation.getDataNode(argsOnStackIds[0]);
                        ref DataNode arg2 = ref m_compilation.getDataNode(argsOnStackIds[1]);

                        if (arg1.dataType == DataNodeType.STRING && arg2.dataType == DataNodeType.STRING)
                            return (arg1.isConstant && arg2.isConstant) ? Intrinsic.REGEXP_NEW_CONST : Intrinsic.REGEXP_NEW_PATTERN;
                    }

                    return null;
                }

                case ClassTag.NAMESPACE: {
                    result.dataType = DataNodeType.NAMESPACE;
                    result.isNotNull = true;

                    if (argCount == 0) {
                        result.isConstant = true;
                        result.constant = new DataNodeConstant(Namespace.@public);
                        return Intrinsic.NAMESPACE_NEW_0;
                    }
                    else if (argCount == 1) {
                        ref DataNode arg = ref m_compilation.getDataNode(argsOnStackIds[0]);

                        if (arg.dataType == DataNodeType.NAMESPACE && arg.isNotNull) {
                            result.isConstant = arg.isConstant;
                            result.constant = arg.constant;
                            return Intrinsic.NAMESPACE_NEW_1;
                        }
                        else if (arg.dataType == DataNodeType.STRING) {
                            if (arg.isConstant) {
                                result.isConstant = true;
                                result.constant = new DataNodeConstant(new Namespace(arg.constant.stringValue));
                            }
                            return Intrinsic.NAMESPACE_NEW_1;
                        }
                    }
                    else if (argCount == 2) {
                        ref DataNode arg1 = ref m_compilation.getDataNode(argsOnStackIds[0]);
                        ref DataNode arg2 = ref m_compilation.getDataNode(argsOnStackIds[1]);

                        if ((arg1.dataType == DataNodeType.STRING || arg1.dataType == DataNodeType.NULL)
                            && (arg2.dataType == DataNodeType.STRING || arg2.dataType == DataNodeType.NULL))
                        {
                            return Intrinsic.NAMESPACE_NEW_2;
                        }
                    }

                    return null;
                }

                case ClassTag.QNAME: {
                    result.dataType = DataNodeType.QNAME;
                    result.isNotNull = true;

                    if (argCount == 1) {
                        ref DataNode arg = ref m_compilation.getDataNode(argsOnStackIds[0]);

                        if (arg.dataType == DataNodeType.STRING) {
                            // We can't create constant QNames from constant strings as the constructor depends
                            // on the default XML namespace set at runtime.
                            return Intrinsic.QNAME_NEW_1;
                        }

                        if (arg.dataType == DataNodeType.QNAME && arg.isNotNull) {
                            if (arg.isConstant) {
                                result.isConstant = true;
                                result.constant = arg.constant;
                            }
                            return Intrinsic.QNAME_NEW_1;
                        }
                    }
                    else if (argCount == 2) {
                        ref DataNode arg1 = ref m_compilation.getDataNode(argsOnStackIds[0]);
                        ref DataNode arg2 = ref m_compilation.getDataNode(argsOnStackIds[1]);

                        if ((arg1.dataType == DataNodeType.STRING
                                || arg1.dataType == DataNodeType.NULL
                                || arg1.dataType == DataNodeType.NAMESPACE)
                            && (arg2.dataType == DataNodeType.STRING
                                || arg2.dataType == DataNodeType.NULL))
                        {
                            if (arg1.isConstant && arg2.isConstant) {
                                result.isConstant = true;

                                Namespace ns = Namespace.any;
                                string localName = null;

                                if (arg1.dataType == DataNodeType.STRING)
                                    ns = new Namespace(arg1.constant.stringValue);
                                else if (arg1.dataType == DataNodeType.NAMESPACE)
                                    ns = arg1.constant.namespaceValue;

                                if (arg2.dataType == DataNodeType.STRING)
                                    localName = arg2.constant.stringValue;

                                result.constant = new DataNodeConstant(new QName(ns, localName));
                            }

                            return Intrinsic.QNAME_NEW_2;
                        }
                    }

                    return null;
                }

                case ClassTag.XML:
                case ClassTag.XML_LIST:
                {
                    result.dataType = DataNodeType.OBJECT;
                    result.constant = new DataNodeConstant(klass);
                    result.isNotNull = true;

                    bool isXmlList = klass.tag == ClassTag.XML_LIST;

                    if (argCount == 0)
                        return isXmlList ? Intrinsic.XMLLIST_NEW_0 : Intrinsic.XML_NEW_0;

                    if (argCount == 1) {
                        ref DataNode arg = ref m_compilation.getDataNode(argsOnStackIds[0]);

                        if (arg.dataType == DataNodeType.STRING || arg.dataType == DataNodeType.NULL)
                            return isXmlList ? Intrinsic.XMLLIST_NEW_1 : Intrinsic.XML_NEW_1;

                        if (arg.dataType == DataNodeType.OBJECT
                            && ClassTagSet.xmlOrXmlList.contains(arg.constant.classValue.tag))
                        {
                            if (isConstruct)
                                return isXmlList ? Intrinsic.XMLLIST_NEW_1 : Intrinsic.XML_NEW_1;
                            else
                                return isXmlList ? Intrinsic.XMLLIST_CALL_1 : Intrinsic.XML_CALL_1;
                        }
                    }

                    return null;
                }

                default:
                    return null;
            }
        }

    }

    internal sealed class SemanticBinderSecondPass {

        private MethodCompilation m_compilation;
        private DynamicArray<int> m_tempIntArray;
        private DynamicArray<int> m_dataNodeIdsWithConstPushConversions;
        private bool m_hasRestOnScopeStack;
        private bool m_hasXmlWithOnScopeStack;

        public SemanticBinderSecondPass(MethodCompilation compilation) {
            m_compilation = compilation;
        }

        public void run() {
            m_dataNodeIdsWithConstPushConversions.clear();
            m_hasRestOnScopeStack = false;

            var basicBlocks = m_compilation.getBasicBlocks();
            var reversePostOrder = m_compilation.getBasicBlockReversePostorder();

            for (int i = 0; i < reversePostOrder.Length; i++)
                _visitBasicBlock(ref basicBlocks[reversePostOrder[i]]);

            for (int i = 0; i < reversePostOrder.Length; i++)
                _checkBlockEntryPhiNodes(ref basicBlocks[reversePostOrder[i]]);

            _checkDataNodeConstPushConversions();

            if (m_compilation.isAnyFlagSet(MethodCompilationFlags.HAS_RUNTIME_SCOPE_STACK)) {
                // If the rest argument is pushed onto the runtime scope stack, we have to
                // create the array.
                if (m_hasRestOnScopeStack)
                    m_compilation.setFlag(MethodCompilationFlags.HAS_REST_ARRAY);

                // If an object pushed onto the runtime scope stack with the "with" flag is possibly
                // an XML or XMLList, a property lookup on the runtime scope stack may access the default
                // XML namespace.
                if (m_hasXmlWithOnScopeStack) {
                    m_compilation.setFlag(MethodCompilationFlags.MAY_USE_DXNS);
                }
                else {
                    var capturedItems = m_compilation.getCapturedScopeItems();
                    for (int i = 0; i < capturedItems.length; i++) {
                        ref readonly var capturedItem = ref capturedItems[i];
                        if (capturedItem.isWithScope && capturedItem.dataType == DataNodeType.OBJECT
                            && (capturedItem.objClass == objectClass || ClassTagSet.xmlOrXmlList.contains(capturedItem.objClass.tag)))
                        {
                            m_compilation.setFlag(MethodCompilationFlags.MAY_USE_DXNS);
                            break;
                        }
                    }
                }
            }
        }

        private void _visitBasicBlock(ref BasicBlock block) {
            var instructions = m_compilation.getInstructionsInBasicBlock(block);

            for (int i = 0; i < instructions.Length; i++)
                _visitInstruction(ref instructions[i]);
        }

        private void _checkBlockEntryPhiNodes(ref BasicBlock block) {
            if ((block.flags & BasicBlockFlags.DEFINES_PHI_NODES) == 0)
                return;

            var stackAtEntry = m_compilation.staticIntArrayPool.getSpan(block.stackAtEntry);
            for (int i = 0; i < stackAtEntry.Length; i++) {
                ref DataNode node = ref m_compilation.getDataNode(stackAtEntry[i]);
                if (!node.isPhi)
                    continue;

                var nodeRef = DataNodeOrInstrRef.forDataNode(node.id);
                var defs = m_compilation.getDataNodeDefs(ref node);

                for (int j = 0; j < defs.Length; j++) {
                    ref DataNode def = ref m_compilation.getDataNode(defs[j].instrOrNodeId);
                    if (def.dataType != node.dataType)
                        _requireStackNodeAsType(ref def, node.dataType, nodeRef);
                }
            }

            checkScopeAndLocals(m_compilation.staticIntArrayPool.getSpan(block.scopeStackAtEntry));
            checkScopeAndLocals(m_compilation.staticIntArrayPool.getSpan(block.localsAtEntry));

            void checkScopeAndLocals(ReadOnlySpan<int> nodeIds) {
                for (int i = 0; i < nodeIds.Length; i++) {
                    ref DataNode node = ref m_compilation.getDataNode(nodeIds[i]);
                    if (!node.isPhi)
                        continue;

                    var defs = m_compilation.getDataNodeDefs(ref node);
                    for (int j = 0; j < defs.Length; j++) {
                        ref DataNode def = ref m_compilation.getDataNode(defs[j].instrOrNodeId);
                        if (def.dataType != node.dataType)
                            _checkForSpecialObjectCoerce(ref def);
                    }
                }
            }
        }

        private void _checkDataNodeConstPushConversions() {
            var nodeIds = m_dataNodeIdsWithConstPushConversions.asSpan();
            var nodes = m_compilation.getDataNodes();

            for (int i = 0; i < nodeIds.Length; i++) {
                ref DataNode node = ref nodes[nodeIds[i]];
                Debug.Assert(node.isConstant);

                if (node.onPushCoerceType == DataNodeType.UNKNOWN)
                    continue;

                DataNode dummyOutput = default;
                bool isConstConverted = false;

                switch (node.onPushCoerceType) {
                    case DataNodeType.INT:
                        isConstConverted = tryEvalUnaryOp(ref node, ref dummyOutput, ABCOp.convert_i);
                        break;
                    case DataNodeType.UINT:
                        isConstConverted = tryEvalUnaryOp(ref node, ref dummyOutput, ABCOp.convert_u);
                        break;
                    case DataNodeType.NUMBER:
                        isConstConverted = tryEvalUnaryOp(ref node, ref dummyOutput, ABCOp.convert_d);
                        break;
                    case DataNodeType.STRING:
                        isConstConverted = tryEvalUnaryOp(ref node, ref dummyOutput, ABCOp.coerce_s);
                        break;
                    case DataNodeType.BOOL:
                        isConstConverted = tryEvalUnaryOp(ref node, ref dummyOutput, ABCOp.convert_b);
                        break;
                    case DataNodeType.OBJECT:
                        isConstConverted = tryEvalUnaryOp(ref node, ref dummyOutput, ABCOp.convert_o);
                        break;
                }

                if (isConstConverted) {
                    node.dataType = dummyOutput.dataType;
                    node.isNotNull = dummyOutput.isNotNull;
                    node.constant = dummyOutput.constant;
                    node.onPushCoerceType = DataNodeType.UNKNOWN;
                }
            }
        }

        private void _visitInstruction(ref Instruction instr) {
            switch (instr.opcode) {
                case ABCOp.astype:
                case ABCOp.istype:
                    _visitIsAsType(ref instr);
                    break;

                case ABCOp.astypelate:
                case ABCOp.istypelate:
                    _visitIsAsTypeLate(ref instr);
                    break;

                case ABCOp.applytype:
                    _visitApplyType(ref instr);
                    break;

                case ABCOp.bitand:
                case ABCOp.bitor:
                case ABCOp.bitxor:
                case ABCOp.lshift:
                case ABCOp.rshift:
                case ABCOp.urshift:
                case ABCOp.add_i:
                case ABCOp.subtract_i:
                case ABCOp.multiply_i:
                    _visitBinaryIntegerOp(ref instr);
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

                case ABCOp.increment:
                case ABCOp.decrement:
                case ABCOp.negate:
                    _visitUnaryNumberOp(ref instr);
                    break;

                case ABCOp.increment_i:
                case ABCOp.decrement_i:
                case ABCOp.negate_i:
                    _visitUnaryIntegerOp(ref instr);
                    break;

                case ABCOp.not:
                    _visitNot(ref instr);
                    break;

                case ABCOp.newarray:
                    _visitNewArray(ref instr);
                    break;

                case ABCOp.newobject:
                    _visitNewObject(ref instr);
                    break;

                case ABCOp.coerce:
                    _visitCoerce(ref instr);
                    break;

                case ABCOp.coerce_a:
                    _visitCoerceA(ref instr);
                    break;

                case ABCOp.convert_b:
                case ABCOp.convert_d:
                case ABCOp.convert_i:
                case ABCOp.convert_o:
                case ABCOp.coerce_s:
                case ABCOp.convert_s:
                case ABCOp.convert_u:
                    _visitConvertX(ref instr);
                    break;

                case ABCOp.dxnslate:
                    _visitDxnsLate(ref instr);
                    break;

                case ABCOp.equals:
                case ABCOp.strictequals:
                case ABCOp.lessthan:
                case ABCOp.lessequals:
                case ABCOp.greaterthan:
                case ABCOp.greaterequals:
                    _visitBinaryCompareOp(ref instr);
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

                case ABCOp.iftrue:
                case ABCOp.iffalse:
                    _visitIfTrueFalse(ref instr);
                    break;

                case ABCOp.lookupswitch:
                    _visitLookupSwitch(ref instr);
                    break;

                case ABCOp.hasnext:
                case ABCOp.nextname:
                case ABCOp.nextvalue:
                    _visitHasNextNameValue(ref instr);
                    break;

                case ABCOp.hasnext2:
                    _visitHasnext2(ref instr);
                    break;

                case ABCOp.pop:
                    _visitPop(ref instr);
                    break;

                case ABCOp.returnvoid:
                case ABCOp.returnvalue:
                    _visitReturn(ref instr);
                    break;

                case ABCOp.@throw:
                    _visitThrow(ref instr);
                    break;

                case ABCOp.@typeof:
                    _visitTypeof(ref instr);
                    break;

                case ABCOp.instanceof:
                    _visitInstanceof(ref instr);
                    break;

                case ABCOp.pushscope:
                case ABCOp.pushwith:
                    _visitPushScope(ref instr);
                    break;

                case ABCOp.setlocal:
                    _visitSetLocal(ref instr);
                    break;

                case ABCOp.esc_xattr:
                case ABCOp.esc_xelem:
                    _visitEscapeXML(ref instr);
                    break;

                case ABCOp.getproperty:
                case ABCOp.getsuper:
                case ABCOp.deleteproperty:
                    _visitGetOrDeleteProperty(ref instr);
                    break;

                case ABCOp.getslot:
                    _visitGetSlot(ref instr);
                    break;

                case ABCOp.setproperty:
                case ABCOp.initproperty:
                case ABCOp.setsuper:
                    _visitSetProperty(ref instr);
                    break;

                case ABCOp.setslot:
                case ABCOp.setglobalslot:
                    _visitSetSlot(ref instr);
                    break;

                case ABCOp.getdescendants:
                    _visitGetDescendants(ref instr);
                    break;

                case ABCOp.@in:
                    _visitIn(ref instr);
                    break;

                case ABCOp.callproperty:
                case ABCOp.callproplex:
                case ABCOp.callpropvoid:
                case ABCOp.callsuper:
                case ABCOp.callsupervoid:
                case ABCOp.constructprop:
                    _visitCallOrConstructProp(ref instr);
                    break;

                case ABCOp.callmethod:
                case ABCOp.callstatic:
                    _visitCallMethodOrStatic(ref instr);
                    break;

                case ABCOp.call:
                case ABCOp.construct:
                    _visitCallOrConstruct(ref instr);
                    break;

                case ABCOp.constructsuper:
                    _visitConstructSuper(ref instr);
                    break;

                case ABCOp.findproperty:
                case ABCOp.findpropstrict:
                case ABCOp.getlex:
                    _visitFindPropertyOrGetLex(ref instr);
                    break;

                case ABCOp.newclass:
                    _visitNewClass(ref instr);
                    break;

                case ABCOp.newfunction:
                    _visitNewFunction(ref instr);
                    break;
            }
        }

        private void _visitIsAsType(ref Instruction instr) {
            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode objectNode = ref m_compilation.getDataNode(inputIds[0]);

            ABCMultiname multiname = m_compilation.abcFile.resolveMultiname(instr.data.coerceOrIsType.multinameId);

            Class klass;
            using (var lockedContext = m_compilation.getContext())
                klass = lockedContext.value.getClassByMultiname(multiname);

            if (ClassTagSet.numeric.contains(klass.tag))
                _requireStackNodeAsType(ref objectNode, DataNodeType.OBJECT, instr.id);
            else
                _requireStackNodeObjectOrInterface(ref objectNode, instr.id);
        }

        private void _visitIsAsTypeLate(ref Instruction instr) {
            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode objectNode = ref m_compilation.getDataNode(inputIds[0]);
            ref DataNode typeNode = ref m_compilation.getDataNode(inputIds[1]);

            Class klass = (typeNode.dataType == DataNodeType.CLASS) ? typeNode.constant.classValue : null;
            if (klass == null) {
                _requireStackNodeAsType(ref objectNode, DataNodeType.OBJECT, instr.id);
                _requireStackNodeAsType(ref typeNode, DataNodeType.OBJECT, instr.id);
            }
            else if (ClassTagSet.numeric.contains(klass.tag)) {
                _requireStackNodeAsType(ref objectNode, DataNodeType.OBJECT, instr.id);
                _markStackNodeAsNoPush(ref typeNode);
            }
            else {
                _requireStackNodeObjectOrInterface(ref objectNode, instr.id);
                _markStackNodeAsNoPush(ref typeNode);
            }
        }

        private void _visitUnaryIntegerOp(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (output.isConstant)
                _markStackNodeAsNoPush(ref input);
            else
                _requireStackNodeAsType(ref input, DataNodeType.INT, instr.id);
        }

        private void _visitNot(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (output.isConstant)
                _markStackNodeAsNoPush(ref input);
            else
                _checkForSpecialObjectCoerce(ref input);
        }

        private void _visitUnaryNumberOp(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (output.isConstant)
                _markStackNodeAsNoPush(ref input);
            else
                _requireStackNodeAsType(ref input, DataNodeType.NUMBER, instr.id);
        }

        private void _visitBinaryIntegerOp(ref Instruction instr) {
            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode input1 = ref m_compilation.getDataNode(inputIds[0]);
            ref DataNode input2 = ref m_compilation.getDataNode(inputIds[1]);
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (output.isConstant) {
                _markStackNodeAsNoPush(ref input1);
                _markStackNodeAsNoPush(ref input2);
            }
            else {
                DataNodeType input1Type = (instr.opcode == ABCOp.urshift) ? DataNodeType.UINT : DataNodeType.INT;
                _requireStackNodeAsType(ref input1, input1Type, instr.id);
                _requireStackNodeAsType(ref input2, DataNodeType.INT, instr.id);
            }
        }

        private void _visitBinaryNumberOp(ref Instruction instr) {
            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode input1 = ref m_compilation.getDataNode(inputIds[0]);
            ref DataNode input2 = ref m_compilation.getDataNode(inputIds[1]);
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (output.isConstant) {
                _markStackNodeAsNoPush(ref input1);
                _markStackNodeAsNoPush(ref input2);
            }
            else if (!isInteger(output.dataType)) {
                _requireStackNodeAsType(ref input1, DataNodeType.NUMBER, instr.id);
                _requireStackNodeAsType(ref input2, DataNodeType.NUMBER, instr.id);
            }
        }

        private void _visitAdd(ref Instruction instr) {
            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode input1 = ref m_compilation.getDataNode(inputIds[0]);
            ref DataNode input2 = ref m_compilation.getDataNode(inputIds[1]);
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (output.isConstant) {
                _markStackNodeAsNoPush(ref input1);
                _markStackNodeAsNoPush(ref input2);
            }
            else {
                DataNodeType inputType = instr.data.add.argsAreAnyType ? DataNodeType.ANY : output.dataType;
                _requireStackNodeAsType(ref input1, inputType, instr.id);
                _requireStackNodeAsType(ref input2, inputType, instr.id);

                if (inputType == DataNodeType.STRING)
                    _checkForStringConcatTree(ref instr, ref input1, ref input2);
            }
        }

        private void _visitNewArray(ref Instruction instr) {
            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            for (int i = 0; i < inputIds.Length; i++)
                _requireStackNodeAsType(inputIds[i], DataNodeType.ANY, instr.id);
        }

        private void _visitNewObject(ref Instruction instr) {
            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);

            for (int i = 0; i < inputIds.Length; i++) {
                _requireStackNodeAsType(
                    ref m_compilation.getDataNode(inputIds[i]),
                    ((i & 1) != 0) ? DataNodeType.ANY : DataNodeType.STRING,
                    instr.id
                );
            }
        }

        private void _visitEscapeXML(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            _checkForSpecialObjectCoerce(ref input);
        }

        private void _visitCoerce(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (output.isConstant || output.dataType == DataNodeType.THIS || output.dataType == DataNodeType.REST)
                _markStackNodeAsNoPush(ref input);
            else if (!isAnyOrUndefined(input.dataType))
                _requireStackNodeObjectOrInterface(ref input, instr.id);
        }

        private void _visitDxnsLate(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));

            if (input.isConstant && (input.dataType == DataNodeType.NAMESPACE || input.dataType == DataNodeType.STRING))
                _markStackNodeAsNoPush(ref input);
            else if (input.dataType != DataNodeType.NAMESPACE)
                _requireStackNodeAsType(ref input, DataNodeType.STRING, instr.id);
        }

        private void _visitCoerceA(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (output.isConstant || output.dataType == DataNodeType.THIS || output.dataType == DataNodeType.REST)
                _markStackNodeAsNoPush(ref input);
        }

        private void _visitConvertX(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (output.isConstant || output.dataType == DataNodeType.THIS || output.dataType == DataNodeType.REST) {
                _markStackNodeAsNoPush(ref input);
            }
            else {
                _checkForSpecialObjectCoerce(ref input);

                if (instr.opcode == ABCOp.convert_i)
                    _checkForFloatToIntegerOp(ref input, DataNodeType.INT);
                else if (instr.opcode == ABCOp.convert_u)
                    _checkForFloatToIntegerOp(ref input, DataNodeType.UINT);
            }
        }

        private void _visitApplyType(ref Instruction instr) {
            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode resultNode = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (resultNode.dataType == DataNodeType.CLASS) {
                for (int i = 0; i < inputIds.Length; i++)
                    _markStackNodeAsNoPush(inputIds[i]);
            }
            else {
                for (int i = 0; i < inputIds.Length; i++)
                    _requireStackNodeAsType(inputIds[i], DataNodeType.ANY, instr.id);
            }
        }

        private void _visitBinaryCompareOp(ref Instruction instr) {
            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode input1 = ref m_compilation.getDataNode(inputIds[0]);
            ref DataNode input2 = ref m_compilation.getDataNode(inputIds[1]);
            ref DataNode output = ref m_compilation.getDataNode(instr.stackPushedNodeId);

            if (output.isConstant) {
                _markStackNodeAsNoPush(ref input1);
                _markStackNodeAsNoPush(ref input2);
            }
            else {
                instr.data.compare.compareType = _getComparisonType(ref input1, ref input2, instr.opcode);
                _checkCompareOperation(ref input1, ref input2, instr.id, ref instr.data.compare.compareType);
            }
        }

        private void _visitBinaryCompareBranch(ref Instruction instr) {
            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode input1 = ref m_compilation.getDataNode(inputIds[0]);
            ref DataNode input2 = ref m_compilation.getDataNode(inputIds[1]);

            instr.data.compareBranch.compareType = _getComparisonType(ref input1, ref input2, instr.opcode);
            _checkCompareOperation(ref input1, ref input2, instr.id, ref instr.data.compareBranch.compareType);
        }

        private void _visitIfTrueFalse(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            _checkForSpecialObjectCoerce(ref input);
        }

        private void _visitLookupSwitch(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            _requireStackNodeAsType(ref input, DataNodeType.INT, instr.id);
        }

        private void _visitHasNextNameValue(ref Instruction instr) {
            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode input1 = ref m_compilation.getDataNode(inputIds[0]);
            ref DataNode input2 = ref m_compilation.getDataNode(inputIds[1]);

            _requireStackNodeAsType(ref input1, DataNodeType.OBJECT, instr.id);
            _requireStackNodeAsType(ref input2, DataNodeType.INT, instr.id);
        }

        private void _visitHasnext2(ref Instruction instr) {
            var nodeIds = m_compilation.staticIntArrayPool.getSpan(instr.data.hasnext2.nodeIds);
            ref DataNode oldObject = ref m_compilation.getDataNode(nodeIds[0]);
            ref DataNode oldIndex = ref m_compilation.getDataNode(nodeIds[1]);

            _checkForSpecialObjectCoerce(ref oldObject);
            _checkForSpecialObjectCoerce(ref oldIndex);
        }

        private void _visitTypeof(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            _requireStackNodeObjectOrAny(ref input, instr.id);
        }

        private void _visitInstanceof(ref Instruction instr) {
            var inputIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode input1 = ref m_compilation.getDataNode(inputIds[0]);
            ref DataNode input2 = ref m_compilation.getDataNode(inputIds[1]);

            _requireStackNodeAsType(ref input1, DataNodeType.OBJECT, instr.id);
            _requireStackNodeAsType(ref input2, DataNodeType.OBJECT, instr.id);
        }

        private void _visitPushScope(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            ref DataNode scope = ref m_compilation.getDataNode(instr.data.pushScope.pushedNodeId);

            if (scope.dataType == DataNodeType.REST) {
                m_hasRestOnScopeStack = true;
            }
            else if (instr.opcode == ABCOp.pushwith) {
                Class scopeClass = m_compilation.getDataNodeClass(scope);
                if (scopeClass == null || scopeClass == objectClass || ClassTagSet.xmlOrXmlList.contains(scopeClass.tag))
                    m_hasXmlWithOnScopeStack = true;
            }

            if (scope.isConstant || scope.dataType == DataNodeType.THIS || scope.dataType == DataNodeType.REST)
                _markStackNodeAsNoPush(ref input);
            else if (isAnyOrUndefined(input.dataType))
                _requireStackNodeAsType(ref input, DataNodeType.OBJECT, instr.id);
        }

        private void _visitSetLocal(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            if (input.isConstant || input.dataType == DataNodeType.THIS || input.dataType == DataNodeType.REST)
                _markStackNodeAsNoPush(ref input);
        }

        private void _visitThrow(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            _requireStackNodeAsType(ref input, DataNodeType.ANY, instr.id);
        }

        private void _visitReturn(ref Instruction instr) {
            if (instr.opcode == ABCOp.returnvalue) {
                ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));

                var method = m_compilation.getCurrentMethod();
                if (method != null && method.hasReturn) {
                    _requireStackNodeAsType(ref input, method.returnType, instr.id);
                }
                else {
                    if (input.isConstant)
                        _markStackNodeAsNoPush(ref input);
                }
            }

            var excessStack = m_compilation.staticIntArrayPool.getSpan(instr.data.returnVoidOrValue.excessStackNodeIds);
            for (int i = 0; i < excessStack.Length; i++) {
                ref DataNode node = ref m_compilation.getDataNode(excessStack[i]);
                if (node.isConstant)
                    _markStackNodeAsNoPush(ref node);
            }
        }

        private void _visitPop(ref Instruction instr) {
            ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            if (input.isConstant)
                _markStackNodeAsNoPush(ref input);
        }

        private void _visitGetDescendants(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode obj = ref m_compilation.getDataNode(stackPopIds[0]);

            _requireStackNodeObjectOrAny(ref obj, instr.id);

            ResolvedProperty dummyResolvedProp = default;
            dummyResolvedProp.propKind = ResolvedPropertyKind.RUNTIME;
            dummyResolvedProp.objectType = DataNodeType.OBJECT;
            dummyResolvedProp.objectClass = objectClass;

            var multiname = m_compilation.abcFile.resolveMultiname(instr.data.getDescendants.multinameId);

            int i = 1;
            if (multiname.hasRuntimeNamespace) {
                dummyResolvedProp.rtNamespaceType = m_compilation.getDataNode(stackPopIds[i]).dataType;
                i++;
            }
            if (multiname.hasRuntimeLocalName) {
                dummyResolvedProp.rtNameType = m_compilation.getDataNode(stackPopIds[i]).dataType;
            }

            _checkRuntimeMultinameArgs(ref dummyResolvedProp, stackPopIds.Slice(1), instr.id);

            if (multiname.usesNamespaceSet)
                m_compilation.setFlag(MethodCompilationFlags.MAY_USE_DXNS);
        }

        private void _visitGetOrDeleteProperty(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode obj = ref m_compilation.getDataNode(stackPopIds[0]);
            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.accessProperty.resolvedPropId);

            _checkRuntimeMultinameArgs(ref resolvedProp, stackPopIds.Slice(1), instr.id);

            // Check if a runtime binding may access the default XML namespace. This happens
            // when a namespace set is used and the object is possibly an XML or XMLList instance.
            ABCMultiname multiname = m_compilation.abcFile.resolveMultiname(instr.data.accessProperty.multinameId);
            if (multiname.usesNamespaceSet && resolvedProp.propKind == ResolvedPropertyKind.RUNTIME) {
                Class objClass = m_compilation.getDataNodeClass(obj);
                if (objClass == null || objClass == objectClass || ClassTagSet.xmlOrXmlList.contains(objClass.tag))
                    m_compilation.setFlag(MethodCompilationFlags.MAY_USE_DXNS);
            }

            Debug.Assert(resolvedProp.propKind != ResolvedPropertyKind.UNKNOWN);

            switch (resolvedProp.propKind) {
                case ResolvedPropertyKind.TRAIT: {
                    var trait = (Trait)resolvedProp.propInfo;
                    _checkTraitAccessObject(ref obj, trait, instr.id);
                    break;
                }

                case ResolvedPropertyKind.INDEX:
                    if (instr.opcode == ABCOp.deleteproperty && obj.dataType == DataNodeType.REST)
                        m_compilation.setFlag(MethodCompilationFlags.HAS_REST_ARRAY);
                    break;

                case ResolvedPropertyKind.RUNTIME:
                    _requireStackNodeObjectOrAny(ref obj, instr.id);
                    break;
            }
        }

        private void _visitSetProperty(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode obj = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode value = ref m_compilation.getDataNode(stackPopIds[stackPopIds.Length - 1]);
            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.accessProperty.resolvedPropId);

            _checkRuntimeMultinameArgs(ref resolvedProp, stackPopIds.Slice(1), instr.id);

            // Check if a runtime binding may access the default XML namespace. This happens
            // when a namespace set is used and the object is possibly an XML or XMLList instance.
            ABCMultiname multiname = m_compilation.abcFile.resolveMultiname(instr.data.accessProperty.multinameId);
            if (multiname.usesNamespaceSet && resolvedProp.propKind == ResolvedPropertyKind.RUNTIME) {
                Class objClass = m_compilation.getDataNodeClass(obj);
                if (objClass == null || objClass == objectClass || ClassTagSet.xmlOrXmlList.contains(objClass.tag))
                    m_compilation.setFlag(MethodCompilationFlags.MAY_USE_DXNS);
            }

            Debug.Assert(resolvedProp.propKind != ResolvedPropertyKind.UNKNOWN);

            switch (resolvedProp.propKind) {
                case ResolvedPropertyKind.TRAIT: {
                    var trait = (Trait)resolvedProp.propInfo;
                    _checkTraitAccessObject(ref obj, trait, instr.id);

                    switch (trait.traitType) {
                        case TraitType.CLASS:
                            Debug.Assert(value.isConstant);
                            _markStackNodeAsNoPush(ref value);
                            break;
                        case TraitType.FIELD:
                            _requireStackNodeAsType(ref value, ((FieldTrait)trait).fieldType, instr.id);
                            break;
                        case TraitType.PROPERTY:
                            _requireStackNodeAsType(ref value, ((PropertyTrait)trait).setter.getParameters()[0].type, instr.id);
                            break;
                        default:
                            Debug.Assert(false);    // Should not reach here.
                            break;
                    }
                    break;
                }

                case ResolvedPropertyKind.TRAIT_RT_INVOKE: {
                    var trait = (Trait)resolvedProp.propInfo;
                    _checkTraitAccessObject(ref obj, trait, instr.id);
                    _requireStackNodeAsType(ref value, DataNodeType.ANY, instr.id);
                    break;
                }

                case ResolvedPropertyKind.INDEX: {
                    if (obj.dataType == DataNodeType.REST)
                        m_compilation.setFlag(MethodCompilationFlags.HAS_REST_ARRAY);

                    _requireStackNodeAsType(ref value, ((IndexProperty)resolvedProp.propInfo).valueType, instr.id);
                    break;
                }

                case ResolvedPropertyKind.RUNTIME: {
                    _requireStackNodeObjectOrAny(ref obj, instr.id);
                    _requireStackNodeAsType(ref value, DataNodeType.ANY, instr.id);
                    break;
                }
            }
        }

        private void _visitGetSlot(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode obj = ref m_compilation.getDataNode(stackPopIds[0]);
            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.getSetSlot.resolvedPropId);

            Debug.Assert(resolvedProp.propKind == ResolvedPropertyKind.TRAIT);

            var trait = (Trait)resolvedProp.propInfo;
            _checkTraitAccessObject(ref obj, trait, instr.id);
        }

        private void _visitSetSlot(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode value = ref m_compilation.getDataNode(stackPopIds[stackPopIds.Length - 1]);
            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.getSetSlot.resolvedPropId);

            var trait = (Trait)resolvedProp.propInfo;

            if (instr.opcode != ABCOp.setglobalslot) {
                ref DataNode obj = ref m_compilation.getDataNode(stackPopIds[0]);
                _checkTraitAccessObject(ref obj, trait, instr.id);
            }

            if (resolvedProp.propKind == ResolvedPropertyKind.TRAIT_RT_INVOKE) {
                _requireStackNodeAsType(ref value, DataNodeType.ANY, instr.id);
                return;
            }

            switch (trait.traitType) {
                case TraitType.CLASS:
                    Debug.Assert(value.isConstant);
                    _markStackNodeAsNoPush(ref value);
                    break;
                case TraitType.FIELD:
                    _requireStackNodeAsType(ref value, ((FieldTrait)trait).fieldType, instr.id);
                    break;
                default:
                    Debug.Assert(false);    // Should not reach here.
                    break;
            }
        }

        private void _visitIn(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            ref DataNode name = ref m_compilation.getDataNode(stackPopIds[0]);
            ref DataNode obj = ref m_compilation.getDataNode(stackPopIds[1]);

            _requireStackNodeAsType(ref name, DataNodeType.STRING, instr.id);
            _requireStackNodeObjectOrAny(ref obj, instr.id);

            // Check if a runtime binding may access the default XML namespace. This happens
            // when the object is possibly an XML or XMLList instance.
            Class objClass = m_compilation.getDataNodeClass(obj);
            if (objClass == null || objClass == objectClass || ClassTagSet.xmlOrXmlList.contains(objClass.tag))
                m_compilation.setFlag(MethodCompilationFlags.MAY_USE_DXNS);
        }

        private void _visitCallOrConstructProp(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            var argNodeIds = stackPopIds.Slice(stackPopIds.Length - instr.data.callProperty.argCount);
            bool isConstruct = instr.opcode == ABCOp.constructprop;

            ref DataNode obj = ref m_compilation.getDataNode(stackPopIds[0]);
            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.callProperty.resolvedPropId);

            _checkRuntimeMultinameArgs(ref resolvedProp, stackPopIds.Slice(1), instr.id);

            Debug.Assert(resolvedProp.propKind != ResolvedPropertyKind.UNKNOWN);

            switch (resolvedProp.propKind) {
                case ResolvedPropertyKind.TRAIT:
                case ResolvedPropertyKind.TRAIT_RT_INVOKE:
                {
                    var trait = (Trait)resolvedProp.propInfo;
                    _checkTraitAccessObject(ref obj, trait, instr.id);

                    if (resolvedProp.propKind == ResolvedPropertyKind.TRAIT_RT_INVOKE)
                        _requireStackArgsAny(argNodeIds, instr.id);
                    else
                        _checkTraitInvokeOrConstructArgs(trait, isConstruct, argNodeIds, instr.id);

                    if (_traitInvokeMayUseDefaultXmlNamespace(trait))
                        m_compilation.setFlag(MethodCompilationFlags.MAY_USE_DXNS);

                    break;
                }

                case ResolvedPropertyKind.INTRINSIC:
                    _checkIntrinsicInvokeOrConstruct(ref resolvedProp, obj.id, argNodeIds, instr.stackPushedNodeId, instr.id);
                    break;

                case ResolvedPropertyKind.INDEX:
                    _requireStackArgsAny(argNodeIds, instr.id);
                    m_compilation.setFlag(MethodCompilationFlags.MAY_USE_DXNS);
                    break;

                case ResolvedPropertyKind.RUNTIME:
                    _requireStackNodeObjectOrAny(ref obj, instr.id);
                    _requireStackArgsAny(argNodeIds, instr.id);
                    m_compilation.setFlag(MethodCompilationFlags.MAY_USE_DXNS);
                    break;
            }
        }

        private void _visitCallMethodOrStatic(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            var argNodeIds = stackPopIds.Slice(stackPopIds.Length - instr.data.callMethod.argCount);

            ref DataNode obj = ref m_compilation.getDataNode(stackPopIds[0]);
            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.callMethod.resolvedPropId);

            Debug.Assert(
                resolvedProp.propKind == ResolvedPropertyKind.TRAIT
                || resolvedProp.propKind == ResolvedPropertyKind.TRAIT_RT_INVOKE
            );

            var method = (MethodTrait)resolvedProp.propInfo;
            _checkTraitAccessObject(ref obj, method, instr.id);

            if (resolvedProp.propKind == ResolvedPropertyKind.TRAIT_RT_INVOKE)
                _requireStackArgsAny(argNodeIds, instr.id);
            else
                _requireStackArgsAsParamTypes(argNodeIds, method.getParameters().asSpan(), instr.id);

            if (_traitInvokeMayUseDefaultXmlNamespace(method))
                m_compilation.setFlag(MethodCompilationFlags.MAY_USE_DXNS);
        }

        private void _visitCallOrConstruct(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            var argNodeIds = stackPopIds.Slice(stackPopIds.Length - instr.data.callOrConstruct.argCount);
            bool isConstruct = instr.opcode == ABCOp.construct;

            ref DataNode func = ref m_compilation.getDataNode(stackPopIds[0]);
            ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.callOrConstruct.resolvedPropId);

            switch (resolvedProp.propKind) {
                case ResolvedPropertyKind.TRAIT:
                case ResolvedPropertyKind.TRAIT_RT_INVOKE:
                {
                    _markStackNodeAsNoPush(ref func);

                    var trait = (Trait)resolvedProp.propInfo;

                    if (instr.opcode == ABCOp.call) {
                        ref DataNode obj = ref m_compilation.getDataNode(stackPopIds[1]);

                        if (!trait.isStatic)
                            _checkTraitAccessObject(ref obj, trait, instr.id);
                        else if (obj.isConstant)
                            _markStackNodeAsNoPush(ref obj);
                    }

                    if (resolvedProp.propKind == ResolvedPropertyKind.TRAIT_RT_INVOKE)
                        _requireStackArgsAny(argNodeIds, instr.id);
                    else
                        _checkTraitInvokeOrConstructArgs(trait, isConstruct, argNodeIds, instr.id);

                    if (_traitInvokeMayUseDefaultXmlNamespace(trait))
                        m_compilation.setFlag(MethodCompilationFlags.MAY_USE_DXNS);

                    break;
                }

                case ResolvedPropertyKind.INTRINSIC: {
                    _markStackNodeAsNoPush(ref func);
                    int objId = isConstruct ? -1 : stackPopIds[1];
                    _checkIntrinsicInvokeOrConstruct(ref resolvedProp, objId, argNodeIds, instr.stackPushedNodeId, instr.id);
                    break;
                }

                case ResolvedPropertyKind.RUNTIME: {
                    _requireStackNodeObjectOrAny(ref func, instr.id);
                    _requireStackArgsAny(argNodeIds, instr.id);
                    m_compilation.setFlag(MethodCompilationFlags.MAY_USE_DXNS);

                    if (instr.opcode == ABCOp.call)
                        _requireStackNodeAsType(stackPopIds[1], DataNodeType.ANY, instr.id);

                    break;
                }

                default:
                    Debug.Assert(false);    // Should not reach here.
                    break;
            }
        }

        private void _visitConstructSuper(ref Instruction instr) {
            var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
            var argNodeIds = stackPopIds.Slice(stackPopIds.Length - instr.data.constructSuper.argCount);

            ClassConstructor ctor = m_compilation.declaringClass.parent.constructor;
            if (ctor != null)
                _requireStackArgsAsParamTypes(argNodeIds, ctor.getParameters().asSpan(), instr.id);
        }

        private void _visitFindPropertyOrGetLex(ref Instruction instr) {
            if (instr.opcode != ABCOp.getlex) {
                var stackPopIds = m_compilation.getInstructionStackPoppedNodes(ref instr);
                ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(instr.data.findProperty.resolvedPropId);
                _checkRuntimeMultinameArgs(ref resolvedProp, stackPopIds, instr.id);
            }

            if (instr.data.findProperty.scopeRef.isNull)
                m_compilation.setFlag(MethodCompilationFlags.HAS_RUNTIME_SCOPE_STACK);
        }

        private void _visitNewClass(ref Instruction instr) {
            ref DataNode baseClassNode = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref instr));
            _markStackNodeAsNoPush(ref baseClassNode);

            var capturedNodeIds = m_compilation.staticIntArrayPool.getSpan(instr.data.newClass.capturedScopeNodeIds);

            bool captureDxns = m_compilation.isAnyFlagSet(MethodCompilationFlags.SETS_DXNS)
                || (m_compilation.capturedScope != null && m_compilation.capturedScope.capturesDxns);

            using (var lockedContext = m_compilation.getContext()) {
                ScriptClass klass = lockedContext.value.getClassFromClassInfo(instr.data.newClass.classInfoId);

                if (!m_compilation.isAnyFlagSet(MethodCompilationFlags.IS_SCRIPT_INIT)
                    || lockedContext.value.getExportingScript(klass) != m_compilation.currentScriptInfo)
                {
                    throw m_compilation.createError(ErrorCode.MARIANA__ABC_NEWCLASS_SCRIPT_INIT, instr.id);
                }

                if (lockedContext.value.getClassCapturedScope(klass) != null) {
                    // If a captured scope has already been set, it means that we are "creating"
                    // the class a second time.
                    throw m_compilation.createError(ErrorCode.MARIANA__ABC_NEWCLASS_ONCE, instr.id);
                }

                lockedContext.value.setClassCapturedScope(klass, _createCapturedScope(capturedNodeIds), captureDxns);
            }
        }

        private void _visitNewFunction(ref Instruction instr) {
            var methodInfo = m_compilation.abcFile.resolveMethodInfo(instr.data.newFunction.methodInfoId);
            var capturedNodeIds = m_compilation.staticIntArrayPool.getSpan(instr.data.newFunction.capturedScopeNodeIds);

            bool captureDxns = m_compilation.isAnyFlagSet(MethodCompilationFlags.SETS_DXNS)
                || (m_compilation.capturedScope != null && m_compilation.capturedScope.capturesDxns);

            using (var lockedContext = m_compilation.getContext()) {
                lockedContext.value.createNewFunction(
                    methodInfo,
                    m_compilation.currentScriptInfo,
                    _createCapturedScope(capturedNodeIds),
                    captureDxns
                );
            }
        }

        private CapturedScopeItem[] _createCapturedScope(ReadOnlySpan<int> innerCaptureIds) {
            var outerCapture = m_compilation.getCapturedScopeItems().asSpan();
            var scope = new CapturedScopeItem[outerCapture.Length + innerCaptureIds.Length];

            for (int i = 0; i < outerCapture.Length; i++)
                scope[i] = outerCapture[i];

            for (int i = 0; i < innerCaptureIds.Length; i++) {
                ref DataNode node = ref m_compilation.getDataNode(innerCaptureIds[i]);
                _checkForSpecialObjectCoerce(ref node);

                DataNodeType nodeType = node.dataType;
                Class nodeClass = null;
                bool isWithScope = (node.flags & DataNodeFlags.WITH_SCOPE) != 0;
                bool lateMultinameBinding = (node.flags & DataNodeFlags.LATE_MULTINAME_BINDING) != 0;

                switch (node.dataType) {
                    case DataNodeType.OBJECT:
                        nodeClass = node.constant.classValue;
                        break;
                    case DataNodeType.CLASS:
                        nodeClass = node.constant.classValue;
                        break;
                    case DataNodeType.THIS:
                        (nodeType, nodeClass) = (DataNodeType.OBJECT, m_compilation.declaringClass);
                        break;
                    case DataNodeType.FUNCTION:
                    case DataNodeType.REST:
                        (nodeType, nodeClass) = (DataNodeType.OBJECT, getClass(node.dataType));
                        break;
                }

                scope[outerCapture.Length + i] = new CapturedScopeItem(nodeType, nodeClass, isWithScope, lateMultinameBinding);
            }

            return scope;
        }

        private void _requireStackNodeAsType(ref DataNode node, Class toClass, int instrId) {
            if (node.dataType == DataNodeType.REST)
                m_compilation.setFlag(MethodCompilationFlags.HAS_REST_ARRAY);

            // Constant conversions/hoisting/integer arithmetic are only considered when the target
            // type is any, Object or a primitive.
            if (toClass != null && toClass != objectClass && !ClassTagSet.primitive.contains(toClass.tag))
                return;

            _requireStackNodeAsType(ref node, getDataTypeOfClass(toClass), instrId);
        }

        private void _requireStackNodeAsType(int nodeId, DataNodeType toType, int instrId) =>
            _requireStackNodeAsType(ref m_compilation.getDataNode(nodeId), toType, DataNodeOrInstrRef.forInstruction(instrId));

        private void _requireStackNodeAsType(ref DataNode node, DataNodeType toType, int instrId) =>
            _requireStackNodeAsType(ref node, toType, DataNodeOrInstrRef.forInstruction(instrId));

        private void _requireStackNodeAsType(ref DataNode node, DataNodeType toType, DataNodeOrInstrRef consumer) {
            Debug.Assert(node.slot.kind == DataNodeSlotKind.STACK);
            node.onPushCoerceType = DataNodeType.UNKNOWN;

            _checkForSpecialObjectCoerce(ref node);

            if (_checkForFloatToIntegerOp(ref node, toType))
                return;

            if (node.isPhi)
                return;

            DataNodeType fromType = node.dataType;

            if (fromType == toType && toType != DataNodeType.OBJECT)
                return;
            if (fromType == DataNodeType.UNDEFINED && toType == DataNodeType.ANY)
                return;

            if (toType == DataNodeType.OBJECT
                && isObjectType(fromType)
                && (fromType != DataNodeType.OBJECT || !node.constant.classValue.isInterface))
            {
                return;
            }

            if (m_compilation.getDataNodeDefCount(ref node) > 1)
                return;

            // The conversion can be hoisted to the push site only if it never causes a
            // side effect (including throwing). These conversions are conversion
            // to the any or Object type, and conversions between primitive types.
            // In particular, these conversions are not included:
            // - Object to primitive conversions may call an overridden toString/valueOf.
            // - Object to object conversions are either trivial or may throw an invalid cast error.

            bool canBeHoisted =
                toType == DataNodeType.ANY
                || toType == DataNodeType.OBJECT
                || (isPrimitive(fromType) && isPrimitive(toType));

            if (!canBeHoisted)
                return;

            // Hoisting conversions requires that the node is not used (popped) by any other
            // instruction. An exception is for constants where the only other uses
            // are dup instructions. In this case, the onPushCoerceType set on the node
            // is checked by the code generator when emitting the IL for the dup instruction.
            var nodeUses = m_compilation.getDataNodeUses(ref node);
            for (int i = 0; i < nodeUses.Length; i++) {
                var use = nodeUses[i];
                if (use == consumer)
                    continue;

                if (!use.isInstruction
                    || !(node.isConstant && m_compilation.getInstruction(use.instrOrNodeId).opcode == ABCOp.dup))
                {
                    return;
                }
            }

            node.onPushCoerceType = toType;
            if (node.isConstant)
                m_dataNodeIdsWithConstPushConversions.add(node.id);
        }

        private void _requireStackNodeObjectOrAny(ref DataNode node, int instrId) {
            if (!isAnyOrUndefined(node.dataType))
                _requireStackNodeAsType(ref node, DataNodeType.OBJECT, instrId);
        }

        private void _requireStackNodeObjectOrInterface(ref DataNode node, int instrId) {
            if (!isObjectType(node.dataType))
                _requireStackNodeAsType(ref node, DataNodeType.OBJECT, instrId);
        }

        private void _requireStackArgsAny(ReadOnlySpan<int> argsOnStackIds, int instrId) {
            for (int i = 0; i < argsOnStackIds.Length; i++)
                _requireStackNodeAsType(argsOnStackIds[i], DataNodeType.ANY, instrId);
        }

        private void _requireStackArgsAsParamTypes(
            ReadOnlySpan<int> argsOnStackIds, ReadOnlySpan<MethodTraitParameter> parameters, int instrId)
        {
            int length = Math.Min(argsOnStackIds.Length, parameters.Length);
            int i;

            for (i = 0; i < length; i++) {
                ref DataNode arg = ref m_compilation.getDataNode(argsOnStackIds[i]);
                var param = parameters[i];

                _requireStackNodeAsType(ref arg, param.type, instrId);
                if (param.isOptional && !param.hasDefault && m_compilation.getDataNodeUseCount(ref arg) == 1)
                    arg.flags |= DataNodeFlags.PUSH_OPTIONAL_PARAM;
            }

            for (; i < argsOnStackIds.Length; i++)
                _requireStackNodeAsType(argsOnStackIds[i], DataNodeType.ANY, instrId);
        }

        private void _markStackNodeAsNoPush(int nodeId) => _markStackNodeAsNoPush(ref m_compilation.getDataNode(nodeId));

        private void _markStackNodeAsNoPush(ref DataNode node) {
            Debug.Assert(node.isConstant || node.dataType == DataNodeType.THIS || node.dataType == DataNodeType.REST);

            ref var nodeSet = ref m_tempIntArray;
            nodeSet.clear();

            if (!walk(ref node, ref nodeSet))
                return;

            for (int i = 0; i < nodeSet.length; i++)
                m_compilation.getDataNode(nodeSet[i]).isNotPushed = true;

            bool walk(ref DataNode _node, ref DynamicArray<int> _nodeSet) {
                if (m_compilation.getDataNodeUseCount(ref _node) > 1) {
                    // In general, constants with more than one use cannot be elided because we
                    // do not know whether the other uses are elidable. However, dup and pop
                    // instructions are an exception because the code generator checks for the
                    // NO_PUSH flag when emitting IL for these instructions and handles
                    // these cases properly.

                    var uses = m_compilation.getDataNodeUses(ref _node);

                    int nonDupOrPopUseCount = 0;
                    for (int i = 0; i < uses.Length && nonDupOrPopUseCount <= 1; i++) {
                        if (!uses[i].isInstruction) {
                            if (uses.Length > 1)
                                // Don't mark the node as no-push if it is a source for a phi node except
                                // if it has no other uses (including dup/pop).
                                return false;

                            nonDupOrPopUseCount++;
                        }
                        else {
                            ref Instruction instr = ref m_compilation.getInstruction(uses[i].instrOrNodeId);
                            if (instr.opcode != ABCOp.dup && instr.opcode != ABCOp.pop)
                                nonDupOrPopUseCount++;
                        }
                    }

                    if (nonDupOrPopUseCount > 1)
                        return false;
                }

                if (_node.isPhi) {
                    var defs = m_compilation.getDataNodeDefs(ref _node);
                    for (int i = 0; i < defs.Length; i++) {
                        ref DataNode def = ref m_compilation.getDataNode(defs[i].instrOrNodeId);
                        if (!walk(ref def, ref _nodeSet))
                            return false;
                    }
                }

                _nodeSet.add(_node.id);
                return true;
            }
        }

        private bool _checkForSpecialObjectCoerce(ref DataNode node) {
            if (node.dataType == DataNodeType.REST) {
                // If a rest argument is being coerced to another type, we can't use
                // the RestParam directly, need to create an Array.
                m_compilation.setFlag(MethodCompilationFlags.HAS_REST_ARRAY);
                return true;
            }

            return false;
        }

        private bool _checkForSpecialObjectTraitAccess(ref DataNode node, Trait trait, int instrId) {
            if (node.dataType == DataNodeType.REST) {
                // RestParam only supports getting the length property; for anything else we
                // need to create the full array.
                if (trait == arrayLengthTrait && m_compilation.getInstruction(instrId).opcode == ABCOp.getproperty)
                    return true;

                m_compilation.setFlag(MethodCompilationFlags.HAS_REST_ARRAY);
            }
            return false;
        }

        /// <summary>
        /// Checks for floating-point arithmetic instructions that can be optimized to integer operations.
        /// </summary>
        /// <param name="node">A reference to the <see cref="DataNode"/> representing the output of the
        /// floating-point operation.</param>
        /// <param name="targetType">The target integer type.</param>
        /// <returns>True if any integer arithmetic optimization was made, otherwise false.</returns>
        private bool _checkForFloatToIntegerOp(ref DataNode node, DataNodeType targetType) {
            if (!isInteger(targetType)
                || m_compilation.compileOptions.integerArithmeticMode == IntegerArithmeticMode.EXPLICIT_ONLY
                || m_compilation.getDataNodeUseCount(ref node) > 1)
            {
                return false;
            }

            ref var nodeSet = ref m_tempIntArray;
            nodeSet.clear();

            if (!walk(ref node, true, ref nodeSet))
                return false;

            for (int i = 0; i < nodeSet.length; i++) {
                ref DataNode nodeInSet = ref m_compilation.getDataNode(nodeSet[i]);

                if (!isInteger(nodeInSet.dataType))
                    nodeInSet.dataType = targetType;
                else
                    nodeInSet.onPushCoerceType = DataNodeType.UNKNOWN;
            }

            return true;

            bool walk(ref DataNode _node, bool isTopLevel, ref DynamicArray<int> _nodeSet) {
                if (isInteger(_node.dataType)) {
                    if (_node.onPushCoerceType != DataNodeType.UNKNOWN)
                        _nodeSet.add(_node.id);

                    return true;
                }

                if (!isNumeric(_node.dataType))
                    return false;

                Debug.Assert(_node.dataType == DataNodeType.NUMBER);

                if (_node.isPhi) {
                    var defs = m_compilation.getDataNodeDefs(ref _node);

                    for (int i = 0; i < defs.Length; i++) {
                        ref DataNode def = ref m_compilation.getDataNode(defs[i].instrOrNodeId);
                        if (m_compilation.getDataNodeUseCount(ref def) > 1)
                            return false;
                        if (!walk(ref def, isTopLevel, ref _nodeSet))
                            return false;
                    }

                    _nodeSet.add(_node.id);
                    return true;
                }


                int pushInstrId = m_compilation.getStackNodePushInstrId(ref _node);
                if (pushInstrId == -1)
                    return false;

                ref Instruction pushInstr = ref m_compilation.getInstruction(pushInstrId);

                switch (pushInstr.opcode) {
                    case ABCOp.add:
                    case ABCOp.subtract:
                    case ABCOp.multiply:
                    case ABCOp.divide:
                    case ABCOp.modulo:
                    {
                        var popped = m_compilation.getInstructionStackPoppedNodes(ref pushInstr);
                        ref DataNode left = ref m_compilation.getDataNode(popped[0]);
                        ref DataNode right = ref m_compilation.getDataNode(popped[1]);

                        // For correctness reasons, we can only use integer division when
                        // - The expression is at the top level (i.e. not a subexpression of a larger
                        //   expression being coerced to an integer)
                        // - Both operands are known to be of the same integral type.

                        if (pushInstr.opcode == ABCOp.divide || pushInstr.opcode == ABCOp.modulo) {
                            if (!isTopLevel)
                                return false;

                            if (!isInteger(left.dataType) || !isInteger(right.dataType))
                                return false;

                            bool hasSameType =
                                left.dataType == right.dataType
                                || (left.isConstant && left.constant.intValue >= 0)
                                || (right.isConstant && right.constant.intValue >= 0);

                            if (!hasSameType)
                                return false;
                        }

                        if (!_areBinOpNodesUsedOnlyOnce(ref left, ref right, out bool isRightDupOfLeft)) {
                            // We allow nodes to be used elsewhere if both of them are known to be of
                            // integral types, as they will be left unchanged.
                            if (!isInteger(left.dataType) && !isInteger(right.dataType))
                                return false;
                        }

                        if (!walk(ref left, false, ref _nodeSet) || (!isRightDupOfLeft && !walk(ref right, false, ref _nodeSet)))
                            return false;

                        if (isRightDupOfLeft)
                            _nodeSet.add(right.id);

                        _nodeSet.add(_node.id);
                        return true;
                    }

                    case ABCOp.negate:
                    case ABCOp.increment:
                    case ABCOp.decrement:
                    {
                        ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref pushInstr));

                        if (!isInteger(input.dataType) && m_compilation.getDataNodeUseCount(ref input) > 1)
                            return false;

                        if (!walk(ref input, false, ref _nodeSet))
                            return false;

                        _nodeSet.add(_node.id);
                        return true;
                    }

                    case ABCOp.callproperty:
                    case ABCOp.callproplex:
                    {
                        // Check if we can use a specialized intrinsic for String.charCodeAt when the result
                        // is coerced to an integer (we only do this at the top level)
                        if (!isTopLevel)
                            return false;

                        ref ResolvedProperty resolvedProp = ref m_compilation.getResolvedProperty(pushInstr.data.callProperty.resolvedPropId);

                        if (resolvedProp.propKind == ResolvedPropertyKind.INTRINSIC
                            && resolvedProp.propInfo == Intrinsic.STRING_CCODEAT_I)
                        {
                            resolvedProp.propInfo = Intrinsic.STRING_CCODEAT_I_I;
                            _nodeSet.add(_node.id);
                            return true;
                        }
                        return false;
                    }

                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Checks if an add instruction that does string concatenation is part of a string concatenation tree.
        /// Concatenation trees are used to optimize multiple string concatenations by avoiding allocations of
        /// intermediate strings.
        /// </summary>
        /// <param name="instr">The instruction performing the string concatenation.</param>
        /// <param name="leftInput">The left input node for the string concatenation.</param>
        /// <param name="rightInput">The right input node for the string concatenation.</param>
        private void _checkForStringConcatTree(ref Instruction instr, ref DataNode leftInput, ref DataNode rightInput) {
            // We can only create a concatenation tree if the two inputs are not used anywhere
            // else. An exception is if right is the result of dup'ing the left.

            if (!_areBinOpNodesUsedOnlyOnce(ref leftInput, ref rightInput, out _)) {
                instr.data.add.isConcatTreeRoot = false;
                instr.data.add.isConcatTreeInternalNode = false;
                return;
            }

            instr.data.add.isConcatTreeRoot = true;
            markInternalNode(ref leftInput);
            markInternalNode(ref rightInput);

            void markInternalNode(ref DataNode node) {
                // Constant nodes are always leaf nodes.
                if (node.isConstant)
                    return;

                int pushInstrId = m_compilation.getStackNodePushInstrId(ref node);
                if (pushInstrId == -1)
                    return;

                ref Instruction pushInstr = ref m_compilation.getInstruction(pushInstrId);
                if (pushInstr.opcode == ABCOp.add && pushInstr.data.add.isConcatTreeRoot) {
                    pushInstr.data.add.isConcatTreeRoot = false;
                    pushInstr.data.add.isConcatTreeInternalNode = true;
                }
            }
        }

        /// <summary>
        /// Checks if the two input nodes for a binary operation instruction are consumed by that instruction
        /// only. As a special case, it allows the right input to be the result of performing a dup on the
        /// left input.
        /// </summary>
        /// <param name="left">The left input to the binary operation.</param>
        /// <param name="right">The right input to the binary operation.</param>
        /// <param name="isRightDupOfLeft">True if the right input is the result of duplicating the left one.</param>
        /// <returns>True if the inputs are consumed only by the binary operation and nowhere else, otherwise false.</returns>
        private bool _areBinOpNodesUsedOnlyOnce(ref DataNode left, ref DataNode right, out bool isRightDupOfLeft) {
            isRightDupOfLeft = false;

            if (m_compilation.getDataNodeUseCount(ref right) > 1)
                return false;

            var leftUses = m_compilation.getDataNodeUses(ref left);

            if (leftUses.Length == 1)
                return true;

            if (leftUses.Length > 2)
                return false;

            if (leftUses[0].isInstruction) {
                ref Instruction instr = ref m_compilation.getInstruction(leftUses[0].instrOrNodeId);
                if (instr.opcode == ABCOp.dup && instr.data.dupOrSwap.nodeId2 == right.id) {
                    isRightDupOfLeft = true;
                    return true;
                }
            }

            if (leftUses[1].isInstruction) {
                ref Instruction instr = ref m_compilation.getInstruction(leftUses[1].instrOrNodeId);
                if (instr.opcode == ABCOp.dup && instr.data.dupOrSwap.nodeId2 == right.id) {
                    isRightDupOfLeft = true;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns a value from the <see cref="ComparisonType"/> enumeration representing the kind
        /// of comparison to be made when a comparison instruction is executed on the given input nodes.
        /// </summary>
        /// <param name="input1">The left input node.</param>
        /// <param name="input2">The right input node.</param>
        /// <param name="opcode">The opcode for the comparison instruction.</param>
        /// <returns>A value from the <see cref="ComparisonType"/> enumeration that represents
        /// the type of comparison (based on the input node types).</returns>
        private ComparisonType _getComparisonType(ref DataNode input1, ref DataNode input2, ABCOp opcode) {
            bool isStrictEquals = false;

            ref var inputVar = ref input1;
            ref var inputConst = ref input2;
            bool isLeftConstant = false;

            if (input1.isConstant) {
                isLeftConstant = true;
                inputVar = ref input2;
                inputConst = ref input1;
            }

            switch (opcode) {
                case ABCOp.equals:
                case ABCOp.ifeq:
                case ABCOp.ifne:
                {
                    if (inputConst.isConstant) {
                        if (isInteger(inputVar.dataType) && isConstantZero(ref inputConst))
                            return isLeftConstant ? ComparisonType.INT_ZERO_L : ComparisonType.INT_ZERO_R;

                        if (isStrictEquals && inputConst.dataType == DataNodeType.UNDEFINED
                            && isAnyOrUndefined(inputVar.dataType))
                        {
                            // We only use this optimization for strict equality, because with weak equality null and the empty
                            // XMLList equal undefined.
                            return isLeftConstant ? ComparisonType.ANY_UNDEF_L : ComparisonType.ANY_UNDEF_R;
                        }

                        if (inputConst.dataType == DataNodeType.NULL && inputVar.dataType == DataNodeType.STRING)
                            return isLeftConstant ? ComparisonType.OBJ_NULL_L : ComparisonType.OBJ_NULL_R;

                        if (inputConst.dataType == DataNodeType.NULL && isObjectType(inputVar.dataType)) {
                            // Optimize the object-null equality only if it is a strict equality, or the object
                            // is known not to be an XML or XMLList. This is because a simple-content XML having
                            // the content "null" is equal to null when using weak equality.

                            Class varClass = m_compilation.getDataNodeClass(inputVar);
                            if (isStrictEquals
                                || (varClass != objectClass && !ClassTagSet.xmlOrXmlList.contains(varClass.tag)))
                            {
                                return isLeftConstant ? ComparisonType.OBJ_NULL_L : ComparisonType.OBJ_NULL_R;
                            }

                            return ComparisonType.OBJECT;
                        }
                    }

                    DataNodeType input1Ty = input1.dataType;
                    DataNodeType input2Ty = input2.dataType;
                    DataNodeType commonType = (input1Ty == input2Ty) ? input1Ty : DataNodeType.UNKNOWN;

                    switch (commonType) {
                        case DataNodeType.NAMESPACE:
                            return ComparisonType.NAMESPACE;
                        case DataNodeType.QNAME:
                            return ComparisonType.QNAME;
                        case DataNodeType.STRING:
                            return ComparisonType.STRING;
                        case DataNodeType.INT:
                            return ComparisonType.INT;
                        case DataNodeType.UINT:
                        case DataNodeType.BOOL:
                            return ComparisonType.UINT;
                    }

                    bool input1IsNumeric = isNumeric(input1Ty);
                    bool input2IsNumeric = isNumeric(input2Ty);

                    if (input1IsNumeric && input2IsNumeric) {
                        if (inputVar.dataType == DataNodeType.INT && isConstantInt(ref inputConst))
                            return ComparisonType.INT;

                        if (inputVar.dataType == DataNodeType.UINT && isConstantUint(ref inputConst))
                            return ComparisonType.UINT;

                        return ComparisonType.NUMBER;
                    }

                    if (!isStrictEquals
                        && (input1IsNumeric || input2IsNumeric || input1Ty == DataNodeType.BOOL || input2Ty == DataNodeType.BOOL))
                    {
                        return ComparisonType.NUMBER;
                    }

                    if (isAnyOrUndefined(input1Ty) || isAnyOrUndefined(input2Ty))
                        return ComparisonType.ANY;

                    Class input1Class = m_compilation.getDataNodeClass(input1);
                    Class input2Class = m_compilation.getDataNodeClass(input2);
                    var inputClassTagSet = new ClassTagSet(input1Class.tag, input2Class.tag);

                    if (input1Class == objectClass || input2Class == objectClass
                        || ClassTagSet.primitive.containsAny(inputClassTagSet))
                    {
                        return ComparisonType.OBJECT;
                    }

                    var specialEqTagSet = ClassTagSet.specialStrictEquality;
                    if (!isStrictEquals)
                        specialEqTagSet = specialEqTagSet.add(ClassTagSet.xmlOrXmlList);

                    if (specialEqTagSet.containsAll(inputClassTagSet))
                        return ComparisonType.OBJECT;

                    return ComparisonType.OBJ_REF;
                }

                case ABCOp.strictequals:
                case ABCOp.ifstricteq:
                case ABCOp.ifstrictne:
                    isStrictEquals = true;
                    goto case ABCOp.equals;

                // For unsigned integers, consider x > 0 or 0 < x to be a not-equals-zero comparison.

                case ABCOp.greaterthan:
                case ABCOp.ifgt:
                case ABCOp.ifnle:
                    if (input1.dataType == DataNodeType.UINT && isConstantZero(ref input2))
                        return ComparisonType.INT_ZERO_R;
                    goto default;

                case ABCOp.lessthan:
                case ABCOp.iflt:
                case ABCOp.ifnge:
                    if (input2.dataType == DataNodeType.UINT && isConstantZero(ref input1))
                        return ComparisonType.INT_ZERO_L;
                    goto default;

                default: {
                    DataNodeType input1Ty = input1.dataType;
                    DataNodeType input2Ty = input2.dataType;
                    DataNodeType commonType = (input1Ty == input2Ty) ? input1Ty : DataNodeType.UNKNOWN;

                    switch (commonType) {
                        case DataNodeType.INT:
                            return ComparisonType.INT;
                        case DataNodeType.UINT:
                        case DataNodeType.BOOL:
                            return ComparisonType.UINT;
                        case DataNodeType.NUMBER:
                            return ComparisonType.NUMBER;
                        case DataNodeType.STRING:
                            return ComparisonType.STRING;
                    }

                    if (isNumeric(input1.dataType) || isNumeric(input2.dataType)) {
                        if (inputVar.dataType == DataNodeType.INT && isConstantInt(ref inputConst))
                            return ComparisonType.INT;

                        if (inputVar.dataType == DataNodeType.UINT && isConstantUint(ref inputConst))
                            return ComparisonType.UINT;

                        return ComparisonType.NUMBER;
                    }

                    if (isAnyOrUndefined(input1.dataType) || isAnyOrUndefined(input2.dataType))
                        return ComparisonType.ANY;

                    return ComparisonType.OBJECT;
                }
            }
        }

        private void _checkCompareOperation(ref DataNode left, ref DataNode right, int instrId, ref ComparisonType cmpType) {
            switch (cmpType) {
                case ComparisonType.INT: {
                    if (_checkForIntrinsicCompareOps(ref left, ref right, ref cmpType))
                        break;
                    _requireStackNodeAsType(ref left, DataNodeType.INT, instrId);
                    _requireStackNodeAsType(ref right, DataNodeType.INT, instrId);
                    break;
                }

                case ComparisonType.UINT: {
                    if (_checkForIntrinsicCompareOps(ref left, ref right, ref cmpType))
                        break;
                    _requireStackNodeAsType(ref left, DataNodeType.UINT, instrId);
                    _requireStackNodeAsType(ref right, DataNodeType.UINT, instrId);
                    break;
                }

                case ComparisonType.NUMBER: {
                    if (_checkForIntrinsicCompareOps(ref left, ref right, ref cmpType))
                        break;
                    _requireStackNodeAsType(ref left, DataNodeType.NUMBER, instrId);
                    _requireStackNodeAsType(ref right, DataNodeType.NUMBER, instrId);
                    break;
                }

                case ComparisonType.STRING: {
                    if (_checkForIntrinsicCompareOps(ref left, ref right, ref cmpType))
                        break;
                    _requireStackNodeAsType(ref left, DataNodeType.STRING, instrId);
                    _requireStackNodeAsType(ref right, DataNodeType.STRING, instrId);
                    break;
                }

                case ComparisonType.ANY:
                    _requireStackNodeAsType(ref left, DataNodeType.ANY, instrId);
                    _requireStackNodeAsType(ref right, DataNodeType.ANY, instrId);
                    break;

                case ComparisonType.OBJECT:
                case ComparisonType.OBJ_REF:
                    _requireStackNodeObjectOrInterface(ref left, instrId);
                    _requireStackNodeObjectOrInterface(ref right, instrId);
                    break;

                case ComparisonType.NAMESPACE:
                    Debug.Assert(left.dataType == DataNodeType.NAMESPACE && right.dataType == DataNodeType.NAMESPACE);
                    break;

                case ComparisonType.QNAME:
                    Debug.Assert(left.dataType == DataNodeType.QNAME && right.dataType == DataNodeType.QNAME);
                    break;

                case ComparisonType.INT_ZERO_L:
                    _requireStackNodeAsType(ref right, DataNodeType.INT, instrId);
                    _markStackNodeAsNoPush(ref left);
                    break;

                case ComparisonType.INT_ZERO_R:
                    _requireStackNodeAsType(ref left, DataNodeType.INT, instrId);
                    _markStackNodeAsNoPush(ref right);
                    break;

                case ComparisonType.ANY_UNDEF_L:
                    _requireStackNodeAsType(ref right, DataNodeType.ANY, instrId);
                    _markStackNodeAsNoPush(ref left);
                    break;

                case ComparisonType.ANY_UNDEF_R:
                    _requireStackNodeAsType(ref left, DataNodeType.ANY, instrId);
                    _markStackNodeAsNoPush(ref right);
                    break;

                case ComparisonType.OBJ_NULL_L:
                    if (right.dataType != DataNodeType.STRING)
                        _requireStackNodeObjectOrInterface(ref right, instrId);
                    _markStackNodeAsNoPush(ref left);
                    break;

                case ComparisonType.OBJ_NULL_R:
                    if (left.dataType != DataNodeType.STRING)
                        _requireStackNodeObjectOrInterface(ref left, instrId);
                    _markStackNodeAsNoPush(ref right);
                    break;
            }
        }

        private bool _checkForIntrinsicCompareOps(ref DataNode left, ref DataNode right, ref ComparisonType cmpType) {
            // This checks for intrinsic compare patterns.
            // The only ones implemented currently are:
            // (string).charAt(int) op (single char const string)
            // (string).charCodeAt(int) op (int)

            bool isLeftComparand = false;

            Intrinsic intrinsic = checkResultOfStringCharAtIntrinsic(ref left);
            if (intrinsic == null) {
                intrinsic = checkResultOfStringCharAtIntrinsic(ref right);
                if (intrinsic != null)
                    isLeftComparand = true;
            }

            if (intrinsic == null)
                return false;

            ref DataNode indexOpNode = ref (isLeftComparand ? ref right : ref left);
            ref DataNode comparandNode = ref (isLeftComparand ? ref left : ref right);

            // We don't want the indexOpNode to be used anywhere other than in the comparison.
            if (m_compilation.getDataNodeUseCount(ref indexOpNode) > 1)
                return false;

            if (intrinsic == Intrinsic.STRING_CHARAT_I) {
                // String.charAt can be compared to a single-character string constant.
                if (!comparandNode.isConstant
                    || comparandNode.dataType != DataNodeType.STRING
                    || comparandNode.constant.stringValue.Length != 1)
                {
                    return false;
                }

                _markStackNodeAsNoPush(ref comparandNode);
                markAsIntrinsicCompare(ref indexOpNode);
                cmpType = isLeftComparand ? ComparisonType.STR_CHARAT_L : ComparisonType.STR_CHARAT_R;
                return true;
            }

            if (intrinsic == Intrinsic.STRING_CCODEAT_I) {
                // String.charCodeAt can be compared to an integer.
                if (!isInteger(comparandNode.dataType) && !isConstantUint(ref comparandNode))
                    return false;

                if (comparandNode.isConstant)
                    _markStackNodeAsNoPush(ref comparandNode);

                markAsIntrinsicCompare(ref indexOpNode);
                cmpType = isLeftComparand ? ComparisonType.STR_CHARAT_L : ComparisonType.STR_CHARAT_R;
                return true;
            }

            return false;

            Intrinsic checkResultOfStringCharAtIntrinsic(ref DataNode node) {
                int pushInstrId = m_compilation.getStackNodePushInstrId(ref node);
                if (pushInstrId == -1)
                    return null;

                ref Instruction pushInstr = ref m_compilation.getInstruction(pushInstrId);
                if (pushInstr.opcode != ABCOp.callproperty && pushInstr.opcode != ABCOp.callproplex)
                    return null;

                ref ResolvedProperty resProp = ref m_compilation.getResolvedProperty(pushInstr.data.callProperty.resolvedPropId);

                if (resProp.propKind != ResolvedPropertyKind.INTRINSIC
                    || (resProp.propInfo != Intrinsic.STRING_CHARAT_I && resProp.propInfo != Intrinsic.STRING_CCODEAT_I))
                {
                    return null;
                }

                return (Intrinsic)resProp.propInfo;
            }

            void markAsIntrinsicCompare(ref DataNode node) {
                ref Instruction pushInstr = ref m_compilation.getInstruction(m_compilation.getStackNodePushInstrId(ref node));
                ref ResolvedProperty resProp = ref m_compilation.getResolvedProperty(pushInstr.data.callProperty.resolvedPropId);

                if (resProp.propInfo == Intrinsic.STRING_CHARAT_I)
                    resProp.propInfo = Intrinsic.STRING_CHARAT_CMP;
                else
                    resProp.propInfo = Intrinsic.STRING_CCODEAT_CMP;
            }
        }

        private void _checkRuntimeMultinameArgs(ref ResolvedProperty resolvedProp, ReadOnlySpan<int> argsOnStack, int instrId) {
            switch (resolvedProp.propKind) {
                case ResolvedPropertyKind.TRAIT:
                case ResolvedPropertyKind.INTRINSIC:
                {
                    int i = 0;
                    if (resolvedProp.rtNamespaceType != DataNodeType.UNKNOWN) {
                        _markStackNodeAsNoPush(argsOnStack[i]);
                        i++;
                    }
                    if (resolvedProp.rtNameType != DataNodeType.UNKNOWN) {
                        _markStackNodeAsNoPush(argsOnStack[i]);
                    }
                    break;
                }

                case ResolvedPropertyKind.INDEX: {
                    int i = 0;
                    if (resolvedProp.rtNamespaceType != DataNodeType.UNKNOWN) {
                        _markStackNodeAsNoPush(argsOnStack[i]);
                        i++;
                    }

                    ref DataNode nameNode = ref m_compilation.getDataNode(argsOnStack[i]);
                    var indexPropInfo = (IndexProperty)resolvedProp.propInfo;

                    if (indexPropInfo.getMethod.getParameters()[0].type.tag == ClassTag.NUMBER) {
                        if (resolvedProp.objectType == DataNodeType.OBJECT
                            && resolvedProp.objectClass.isVectorInstantiation
                            && _checkForVectorIndexExprOptimization(ref nameNode, out DataNodeType optIndexType))
                        {
                            resolvedProp.rtNameType = optIndexType;
                            if (optIndexType == DataNodeType.INT)
                                resolvedProp.propInfo = resolvedProp.objectClass.classSpecials.intIndexProperty;
                            else
                                resolvedProp.propInfo = resolvedProp.objectClass.classSpecials.uintIndexProperty;
                            break;
                        }

                        if (resolvedProp.rtNameType != DataNodeType.NUMBER)
                            _requireStackNodeAsType(ref nameNode, DataNodeType.NUMBER, instrId);
                    }
                    break;
                }

                case ResolvedPropertyKind.RUNTIME: {
                    int i = 0;

                    if (resolvedProp.rtNamespaceType != DataNodeType.UNKNOWN) {
                        if (resolvedProp.rtNamespaceType != DataNodeType.NAMESPACE) {
                            // It is a compile-time error if the static type of a runtime namespace argument
                            // is not Namespace.
                            throw m_compilation.createError(
                                ErrorCode.ILLEGAL_OPERAND_TYPE_NAMESPACE, instrId, m_compilation.getDataNodeTypeName(argsOnStack[i]));
                        }
                        i++;
                    }

                    if (resolvedProp.rtNameType != DataNodeType.UNKNOWN) {
                        ref DataNode nameNode = ref m_compilation.getDataNode(argsOnStack[i]);

                        // If there is a runtime namespace then the name should be a string.
                        // Otherwise, it can be a string, a QName or the "any" type.

                        if (resolvedProp.rtNamespaceType != DataNodeType.UNKNOWN) {
                            _requireStackNodeAsType(ref nameNode, DataNodeType.STRING, instrId);
                        }
                        else if (resolvedProp.rtNameType != DataNodeType.STRING
                            && resolvedProp.rtNameType != DataNodeType.QNAME)
                        {
                            _requireStackNodeAsType(ref nameNode, DataNodeType.ANY, instrId);
                        }
                    }

                    break;
                }
            }
        }

        private bool _checkForVectorIndexExprOptimization(ref DataNode nameNode, out DataNodeType integerType) {
            integerType = DataNodeType.UNKNOWN;

            if (m_compilation.getDataNodeUseCount(ref nameNode) > 1)
                return false;

            int pushInstrId = m_compilation.getStackNodePushInstrId(ref nameNode);
            if (pushInstrId == -1)
                return false;

            ref Instruction pushInstr = ref m_compilation.getInstruction(pushInstrId);
            Debug.Assert(pushInstr.stackPushedNodeId == nameNode.id);

            // Optimize expressions of the form x + c and x - c where x is a signed or unsigned
            // integer and c is an integer constant to use integer addition and index lookup
            // instead of the more expensive floating-point equivalents. The range of permitted
            // values for c can be derived from the fact that valid indices for a Vector are
            // in the range [0, 2^31-2].

            if (pushInstr.opcode == ABCOp.add || pushInstr.opcode == ABCOp.subtract) {
                var poppedNodeIds = m_compilation.getInstructionStackPoppedNodes(ref pushInstr);

                ref DataNode left = ref m_compilation.getDataNode(poppedNodeIds[0]);
                ref DataNode right = ref m_compilation.getDataNode(poppedNodeIds[1]);

                double cval;

                if (isInteger(left.dataType)
                    && right.isConstant
                    && isInteger(right.dataType)
                    && tryGetConstant(ref right, out cval))
                {
                    (double minOffset, double maxOffset) =
                        (left.dataType == DataNodeType.UINT) ? (-2147483648d, 0d) : (-1d, 2147483647d);

                    if (pushInstr.opcode == ABCOp.subtract)
                        (minOffset, maxOffset) = (-maxOffset, -minOffset);

                    if (cval >= minOffset && cval <= maxOffset) {
                        nameNode.dataType = left.dataType;
                        integerType = left.dataType;

                        left.onPushCoerceType = DataNodeType.UNKNOWN;
                        right.onPushCoerceType = DataNodeType.UNKNOWN;
                    }
                }
                else if (isInteger(right.dataType)
                    && pushInstr.opcode == ABCOp.add
                    && left.isConstant
                    && isInteger(left.dataType)
                    && tryGetConstant(ref left, out cval))
                {
                    (double minOffset, double maxOffset) =
                        (right.dataType == DataNodeType.UINT) ? (-2147483648d, 0d) : (-1d, 2147483647d);

                    if (cval >= minOffset && cval <= maxOffset) {
                        nameNode.dataType = right.dataType;
                        integerType = right.dataType;

                        left.onPushCoerceType = DataNodeType.UNKNOWN;
                        right.onPushCoerceType = DataNodeType.UNKNOWN;
                    }
                }
            }
            else if (pushInstr.opcode == ABCOp.increment || pushInstr.opcode == ABCOp.decrement) {
                ref DataNode input = ref m_compilation.getDataNode(m_compilation.getInstructionStackPoppedNode(ref pushInstr));
                if (input.dataType == DataNodeType.INT
                    || (input.dataType == DataNodeType.UINT && pushInstr.opcode == ABCOp.decrement))
                {
                    nameNode.dataType = input.dataType;
                    integerType = input.dataType;
                    input.onPushCoerceType = DataNodeType.UNKNOWN;
                }
            }

            return isInteger(integerType);
        }

        private void _checkTraitAccessObject(ref DataNode node, Trait trait, int instrId) {
            if (trait.isStatic) {
                _markStackNodeAsNoPush(ref node);
                return;
            }

            if (_checkForSpecialObjectTraitAccess(ref node, trait, instrId))
                return;

            if (ClassTagSet.primitive.contains(trait.declaringClass.tag))
                _requireStackNodeAsType(ref node, getDataTypeOfClass(trait.declaringClass), instrId);
            else
                _requireStackNodeObjectOrInterface(ref node, instrId);
        }

        private void _checkTraitInvokeOrConstructArgs(
            Trait trait, bool isConstruct, ReadOnlySpan<int> argsOnStack, int instrId)
        {
            if (trait is MethodTrait method) {
                _requireStackArgsAsParamTypes(argsOnStack, method.getParameters().asSpan(), instrId);
            }
            else if (trait is Class klass) {
                if (isConstruct) {
                    _requireStackArgsAsParamTypes(argsOnStack, klass.constructor.getParameters().asSpan(), instrId);
                }
                else {
                    Debug.Assert(argsOnStack.Length == 1);
                    _requireStackNodeObjectOrAny(ref m_compilation.getDataNode(argsOnStack[0]), instrId);
                }
            }
        }

        private void _checkIntrinsicInvokeOrConstruct(
            ref ResolvedProperty resolvedProp, int objectId, ReadOnlySpan<int> argsOnStack, int resultId, int instrId)
        {
            bool resultIsConst = resultId != -1 && m_compilation.getDataNode(resultId).isConstant;
            bool isStaticFunc = true;

            var intrinsic = (Intrinsic)resolvedProp.propInfo;
            switch (intrinsic.name) {
                case IntrinsicName.OBJECT_NEW_1:
                    _requireStackNodeAsType(argsOnStack[0], DataNodeType.OBJECT, instrId);
                    break;

                case IntrinsicName.INT_NEW_1:
                case IntrinsicName.UINT_NEW_1:
                {
                    ref DataNode input = ref m_compilation.getDataNode(argsOnStack[0]);
                    if (resultIsConst) {
                        _markStackNodeAsNoPush(ref input);
                    }
                    else {
                        DataNodeType targetType = (intrinsic.name == IntrinsicName.UINT_NEW_1) ? DataNodeType.UINT : DataNodeType.INT;
                        _checkForSpecialObjectCoerce(ref input);
                        _checkForFloatToIntegerOp(ref input, targetType);
                    }
                    break;
                }

                case IntrinsicName.NUMBER_NEW_1:
                case IntrinsicName.STRING_NEW_1:
                case IntrinsicName.BOOLEAN_NEW_1:
                {
                    ref DataNode input = ref m_compilation.getDataNode(argsOnStack[0]);
                    if (resultIsConst)
                        _markStackNodeAsNoPush(ref input);
                    else
                        _checkForSpecialObjectCoerce(ref input);
                    break;
                }

                case IntrinsicName.DATE_NEW_1: {
                    ref DataNode arg = ref m_compilation.getDataNode(argsOnStack[0]);
                    if (arg.dataType != DataNodeType.STRING)
                        _requireStackNodeAsType(ref arg, DataNodeType.NUMBER, instrId);
                    break;
                }

                case IntrinsicName.DATE_NEW_7: {
                    for (int i = 0; i < argsOnStack.Length; i++)
                        _requireStackNodeAsType(argsOnStack[i], DataNodeType.NUMBER, instrId);
                    break;
                }

                case IntrinsicName.ARRAY_NEW:
                    _requireStackArgsAny(argsOnStack, instrId);
                    break;

                case IntrinsicName.VECTOR_ANY_CALL_1:
                case IntrinsicName.VECTOR_T_CALL_1:
                    _requireStackNodeAsType(argsOnStack[0], DataNodeType.OBJECT, instrId);
                    break;

                case IntrinsicName.VECTOR_ANY_CTOR:
                    if (argsOnStack.Length >= 1)
                        _requireStackNodeAsType(argsOnStack[0], DataNodeType.INT, instrId);
                    if (argsOnStack.Length >= 2)
                        _requireStackNodeAsType(argsOnStack[1], DataNodeType.BOOL, instrId);
                    break;

                case IntrinsicName.NAMESPACE_NEW_1:
                case IntrinsicName.NAMESPACE_NEW_2:
                case IntrinsicName.QNAME_NEW_1:
                case IntrinsicName.QNAME_NEW_2:
                {
                    if (resultIsConst) {
                        if (argsOnStack.Length >= 1)
                            _markStackNodeAsNoPush(argsOnStack[0]);
                        if (argsOnStack.Length >= 2)
                            _markStackNodeAsNoPush(argsOnStack[1]);
                    }

                    if (intrinsic.name == IntrinsicName.QNAME_NEW_1)
                        m_compilation.setFlag(MethodCompilationFlags.MAY_USE_DXNS);

                    break;
                }

                case IntrinsicName.XML_CALL_1:
                case IntrinsicName.XML_NEW_1:
                case IntrinsicName.XMLLIST_CALL_1:
                case IntrinsicName.XMLLIST_NEW_1:
                    m_compilation.setFlag(MethodCompilationFlags.MAY_USE_DXNS);
                    break;

                case IntrinsicName.MATH_MIN_2:
                case IntrinsicName.MATH_MAX_2:
                    _requireStackNodeAsType(argsOnStack[0], DataNodeType.NUMBER, instrId);
                    _requireStackNodeAsType(argsOnStack[1], DataNodeType.NUMBER, instrId);
                    break;

                case IntrinsicName.MATH_MIN_2_I:
                case IntrinsicName.MATH_MAX_2_I:
                    _requireStackNodeAsType(argsOnStack[0], DataNodeType.INT, instrId);
                    _requireStackNodeAsType(argsOnStack[1], DataNodeType.INT, instrId);
                    break;

                case IntrinsicName.MATH_MIN_2_U:
                case IntrinsicName.MATH_MAX_2_U:
                    _requireStackNodeAsType(argsOnStack[0], DataNodeType.UINT, instrId);
                    _requireStackNodeAsType(argsOnStack[1], DataNodeType.UINT, instrId);
                    break;

                case IntrinsicName.ARRAY_PUSH_1:
                    isStaticFunc = false;
                    _requireStackNodeAsType(argsOnStack[0], DataNodeType.ANY, instrId);
                    break;

                case IntrinsicName.VECTOR_T_PUSH_1:
                    isStaticFunc = false;
                    _requireStackNodeAsType(
                        ref m_compilation.getDataNode(argsOnStack[0]), (Class)intrinsic.arg, instrId);
                    break;

                case IntrinsicName.STRING_CHARAT:
                case IntrinsicName.STRING_CCODEAT:
                {
                    isStaticFunc = false;

                    ref DataNode index = ref m_compilation.getDataNode(argsOnStack[0]);

                    // Since the range of valid integer indices for these intrinsics is the same
                    // as that for vector indexing (0 to 2^32-1), we can perform the same analysis of
                    // simple constant add/subtract expressions that we do for vector indexing.

                    if (_checkForVectorIndexExprOptimization(ref index, out _)) {
                        resolvedProp.propInfo = (intrinsic.name == IntrinsicName.STRING_CHARAT)
                            ? Intrinsic.STRING_CHARAT_I
                            : Intrinsic.STRING_CCODEAT_I;
                    }
                    else {
                        _requireStackNodeAsType(ref index, DataNodeType.NUMBER, instrId);
                    }
                    break;
                }

                case IntrinsicName.STRING_CHARAT_I:
                case IntrinsicName.STRING_CCODEAT_I:
                    isStaticFunc = false;
                    break;
            }

            if (isStaticFunc && objectId != -1 && m_compilation.getDataNode(objectId).isConstant)
                _markStackNodeAsNoPush(objectId);
        }

        /// <summary>
        /// Returns true if the given trait may access the current default XML namespace when invoked.
        /// </summary>
        /// <param name="trait">The trait to check.</param>
        /// <returns>True if <paramref name="trait"/> may access the default XML namespace when invoked,
        /// otherwise false.</returns>
        private static bool _traitInvokeMayUseDefaultXmlNamespace(Trait trait) {
            if (trait is Class klass)
                return klass == objectClass || klass.tag == ClassTag.QNAME || ClassTagSet.xmlOrXmlList.contains(klass.tag);

            if (trait.traitType == TraitType.METHOD) {
                return trait == objHasOwnPropertyTrait
                    || (!trait.isStatic && ClassTagSet.xmlOrXmlList.contains(trait.declaringClass.tag));
            }

            return true;
        }

    }

}
