using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Mariana.AVM2.ABC;
using Mariana.AVM2.Core;
using Mariana.CodeGen;
using Mariana.CodeGen.IL;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    internal partial class MethodCompilation {

        private ScriptCompileContext m_context;

        private ABCScriptInfo m_currentScript;

        private object m_currentMethod;

        private ABCMethodInfo m_currentMethodInfo;

        private ABCMethodBodyInfo m_currentMethodBodyInfo;

        private ReadOnlyArrayView<MethodTraitParameter> m_currentMethodParams;

        private MethodCompilationFlags m_flags;

        private ScriptClass m_declClass;

        private DynamicArray<Instruction> m_instructions = new DynamicArray<Instruction>(256);

        private DynamicArray<BasicBlock> m_basicBlocks = new DynamicArray<BasicBlock>(32);

        private DynamicArray<ExceptionHandler> m_excHandlers = new DynamicArray<ExceptionHandler>(8);

        private DynamicArray<DataNode> m_dataNodes = new DynamicArray<DataNode>(256);

        private DynamicArray<ResolvedProperty> m_resolvedProperties = new DynamicArray<ResolvedProperty>(64);

        private StaticArrayPool<int> m_staticIntArrayPool = new StaticArrayPool<int>(1024);

        private DynamicArrayPool<int> m_intArrayPool = new DynamicArrayPool<int>();

        private DynamicArrayPool<CFGNodeRef> m_cfgNodeRefArrayPool = new DynamicArrayPool<CFGNodeRef>();

        private DynamicArrayPool<DataNodeOrInstrRef> m_dataNodeOrInstrRefArrayPool = new DynamicArrayPool<DataNodeOrInstrRef>();

        private int m_computedMaxStack;

        private int m_computedMaxScope;

        private DynamicArray<int> m_basicBlockReversePostorder = new DynamicArray<int>(16);

        private bool m_basicBlockReversePostorderDirty;

        private DynamicArray<int> m_initialLocalNodeIds = new DynamicArray<int>(16);

        private CapturedScope m_capturedScope;

        private ScriptClass m_activationClass;

        private DynamicArray<ScriptClass> m_catchScopeClasses = new DynamicArray<ScriptClass>(8);

        private ILBuilder m_ilBuilder;

        private InstructionDecoder m_instructionDecoderPass;
        private ControlFlowAssembler m_cfAssemblyPass;
        private DataFlowAssembler m_dfAssemblyPass;
        private SemanticBinder m_semanticBindingPass;
        private CodeGenerator m_codegenPass;

        public MethodCompilation(ScriptCompileContext context) {
            m_context = context;
            m_ilBuilder = new ILBuilder(context.assemblyBuilder.metadataContext.ilTokenProvider);

            m_instructionDecoderPass = new InstructionDecoder(this);
            m_cfAssemblyPass = new ControlFlowAssembler(this);
            m_dfAssemblyPass = new DataFlowAssembler(this);
            m_semanticBindingPass = new SemanticBinder(this);
            m_codegenPass = new CodeGenerator(this);
        }

        /// <summary>
        /// Gets the ABC file that defines the method being compiled.
        /// </summary>
        public ABCFile abcFile => m_context.abcFile;

        /// <summary>
        /// Gets the application domain in which the global classes and traits defined in the
        /// compilation are registered.
        /// </summary>
        public ApplicationDomain applicationDomain => m_context.applicationDomain;

        /// <summary>
        /// Gets the class that declares the method being compiled. For global methods, the value
        /// of this property is null.
        /// </summary>
        public ScriptClass declaringClass => m_declClass;

        /// <summary>
        /// Gets the <see cref="ABCMethodInfo"/> for the method being compiled.
        /// </summary>
        public ABCMethodInfo methodInfo => m_currentMethodInfo;

        /// <summary>
        /// Gets the <see cref="ABCMethodBodyInfo"/> for the body of the method being compiled.
        /// </summary>
        public ABCMethodBodyInfo methodBodyInfo => m_currentMethodBodyInfo;

        /// <summary>
        /// Gets the <see cref="ScriptCompileOptions"/> instance containing the current compiler
        /// configuration.
        /// </summary>
        public ScriptCompileOptions compileOptions => m_context.compileOptions;

        /// <summary>
        /// Gets the <see cref="MetadataContext"/> for the assembly into which this compilation
        /// is emitting the method.
        /// </summary>
        public MetadataContext metadataContext =>
            // MetadataContext has its own thread safety, so it can be exposed without taking a context lock.
            m_context.assemblyBuilder.metadataContext;

        /// <summary>
        /// Returns the <see cref="CapturedScope"/> representing the scope stack captured by this
        /// method from its outer context.
        /// </summary>
        public CapturedScope capturedScope => m_capturedScope;

        /// <summary>
        /// Gets the static integer array pool used by this <see cref="MethodCompilation"/> instance.
        /// </summary>
        public StaticArrayPool<int> staticIntArrayPool => m_staticIntArrayPool;

        /// <summary>
        /// Gets the dynamic integer array pool used by this <see cref="MethodCompilation"/> instance.
        /// </summary>
        public DynamicArrayPool<int> intArrayPool => m_intArrayPool;

        /// <summary>
        /// Gets the dynamic array pool used by this <see cref="MethodCompilation"/> for arrays of type
        /// <see cref="CFGNodeRef"/>.
        /// </summary>
        public DynamicArrayPool<CFGNodeRef> cfgNodeRefArrayPool => m_cfgNodeRefArrayPool;

        /// <summary>
        /// Gets the dynamic array pool used by this <see cref="MethodCompilation"/> for arrays of type
        /// <see cref="DataNodeOrInstrRef"/>.
        /// </summary>
        public DynamicArrayPool<DataNodeOrInstrRef> dataNodeOrInstrRefArrayPool => m_dataNodeOrInstrRefArrayPool;

        /// <summary>
        /// Gets the maximum permitted stack depth, as defined in the method body definition in the ABC file.
        /// </summary>
        public int maxStackSize => m_currentMethodBodyInfo.maxStackSize;

        /// <summary>
        /// Gets the maximum permitted scope stack depth, as defined in the method body definition in the ABC file.
        /// </summary>
        public int maxScopeStackSize => m_currentMethodBodyInfo.maxScopeDepth - m_currentMethodBodyInfo.initScopeDepth;

        /// <summary>
        /// Gets the maximum stack depth that was computed during the dataflow assembly process.
        /// If this has not yet been computed, returns -1. This value is never greater than
        /// <see cref="maxStackSize"/>.
        /// </summary>
        public int computedMaxStackSize => m_computedMaxStack;

        /// <summary>
        /// Gets the maximum scope stack depth that was computed during the dataflow assembly process.
        /// If this has not yet been computed, returns -1. This value is never greater than
        /// <see cref="maxScopeStackSize"/>.
        /// </summary>
        public int computedMaxScopeSize => m_computedMaxScope;

        /// <summary>
        /// Gets the number of local variable slots used by this method.
        /// </summary>
        public int localCount => m_currentMethodBodyInfo.localCount;

        /// <summary>
        /// Gets a <see cref="LockedObject{ScriptCompileContext}"/> that provides access to the
        /// context in which the current method compilation is running.
        /// </summary>
        public LockedObject<ScriptCompileContext> getContext() => m_context.getLocked();

        /// <summary>
        /// Gets the <see cref="ScriptMethod"/> representing the method being compiled.
        /// </summary>
        /// <returns>The <see cref="ScriptMethod"/> representing the method being compiled. If the current
        /// method being compiled is a constructor, returns null. </returns>
        public ScriptMethod getCurrentMethod() => m_currentMethod as ScriptMethod;

        /// <summary>
        /// Gets the <see cref="ScriptClassConstructor"/> representing the constructor being compiled.
        /// </summary>
        /// <returns>The <see cref="ScriptClassConstructor"/> representing the constructor being compiled.
        /// If the current method being compiled is not a constructor, returns null.</returns>
        public ScriptClassConstructor getCurrentConstructor() => m_currentMethod as ScriptClassConstructor;

        /// <summary>
        /// Gets a read-only array view containing the parameters of the current method or constructor.
        /// </summary>
        /// <returns>A read-only array view containing the <see cref="MethodTraitParameter"/> instances
        /// representing the formal parameters of the current method or constructor.</returns>
        public ReadOnlyArrayView<MethodTraitParameter> getCurrentMethodParams() => m_currentMethodParams;

        /// <summary>
        /// Gets the basic block at the given index.
        /// </summary>
        /// <returns>A reference to the <see cref="BasicBlock"/> instance representing the
        /// basic block at the given index.</returns>
        /// <param name="index">The index of the basic block to be obtained.</param>
        public ref BasicBlock getBasicBlock(int index) => ref m_basicBlocks[index];

        /// <summary>
        /// Gets the instruction at the given index.
        /// </summary>
        /// <returns>A reference to the <see cref="Instruction"/> instance representing the
        /// instruction at the given index.</returns>
        /// <param name="index">The index of the instruction to be obtained.</param>
        public ref Instruction getInstruction(int index) => ref m_instructions[index];

        /// <summary>
        /// Gets the basic block containing the instruction at the given index.
        /// </summary>
        /// <returns>A reference to the <see cref="BasicBlock"/> instance representing
        /// the basic block containing the instruction at the given index.</returns>
        /// <param name="index">The instruction index.</param>
        public ref BasicBlock getBasicBlockOfInstruction(int index) => ref m_basicBlocks[m_instructions[index].blockId];

        /// <summary>
        /// Gets the basic block containing the given instruction.
        /// </summary>
        /// <returns>A reference to the <see cref="BasicBlock"/> instance representing
        /// the basic block containing the given instruction.</returns>
        /// <param name="instr">A reference to an <see cref="Instruction"/> instance.</param>
        public ref BasicBlock getBasicBlockOfInstruction(in Instruction instr) => ref m_basicBlocks[instr.blockId];

        /// <summary>
        /// Gets the exception handler at the given index.
        /// </summary>
        /// <param name="index">The index of the exception handler.</param>
        /// <returns>A reference to the <see cref="ExceptionHandler"/> instance representing the
        /// exception handler at the given index.</returns>
        public ref ExceptionHandler getExceptionHandler(int index) => ref m_excHandlers[index];

        /// <summary>
        /// Gets the data node in this compilation at the given index.
        /// </summary>
        /// <param name="index">The index of the data node.</param>
        /// <returns>A reference to the <see cref="DataNode"/> instance representing the
        /// data node at the given index.</returns>
        public ref DataNode getDataNode(int index) => ref m_dataNodes[index];

        /// <summary>
        /// Gets the <see cref="ResolvedProperty"/> in this compilation at the given index.
        /// </summary>
        /// <param name="index">The index of the <see cref="ResolvedProperty"/> to obtain.</param>
        /// <returns>A reference to the <see cref="ResolvedProperty"/> instance allocated in this
        /// compilation at the given index.</returns>
        public ref ResolvedProperty getResolvedProperty(int index) => ref m_resolvedProperties[index];

        /// <summary>
        /// Returns a span containing the <see cref="BasicBlock"/> instances representing the
        /// basic blocks in the method body.
        /// </summary>
        /// <returns>A span containing the basic blocks in this method body.</returns>
        public Span<BasicBlock> getBasicBlocks() => m_basicBlocks.asSpan();

        /// <summary>
        /// Returns a span containing the <see cref="Instruction"/> instances representing the
        /// instructions in the method body.
        /// </summary>
        /// <returns>A span containing the method body instructions.</returns>
        public Span<Instruction> getInstructions() => m_instructions.asSpan();

        /// <summary>
        /// Returns a span containing the <see cref="Instruction"/> instances representing the
        /// instructions in the basic block with the given index.
        /// </summary>
        /// <param name="bbIndex">The index of the basic block.</param>
        /// <returns>A span containing the instructions in the basic block.</returns>
        public Span<Instruction> getInstructionsInBasicBlock(int bbIndex)
            => getInstructionsInBasicBlock(in m_basicBlocks[bbIndex]);

        /// <summary>
        /// Returns a span containing the <see cref="Instruction"/> instances representing the
        /// instructions in the given basic block.
        /// </summary>
        /// <param name="block">A reference to a <see cref="BasicBlock"/> instance representing
        /// the basic block.</param>
        /// <returns>A span containing the instructions in the basic block.</returns>
        public Span<Instruction> getInstructionsInBasicBlock(in BasicBlock block) =>
            m_instructions.asSpan().Slice(block.firstInstrId, block.instrCount);

        /// <summary>
        /// Returns a span containing the <see cref="ExceptionHandler"/> instances
        /// representing the exception handlers in the method body.
        /// </summary>
        /// <returns>A span containing the method body exception handlers.</returns>
        public Span<ExceptionHandler> getExceptionHandlers() => m_excHandlers.asSpan();

        /// <summary>
        /// Returns a span containing the <see cref="DataNode"/> instances
        /// representing the data nodes in this compilation.
        /// </summary>
        /// <returns>A span containing the data nodes allocated in this compilation.</returns>
        public Span<DataNode> getDataNodes() => m_dataNodes.asSpan();

        /// <summary>
        /// Returns a span containing the <see cref="ResolvedProperty"/> instances
        /// allocated in this compilation.
        /// </summary>
        /// <returns>A span containing the <see cref="ResolvedProperty"/> instances allocated
        /// in this compilation.</returns>
        public Span<ResolvedProperty> getResolvedProperties() => m_resolvedProperties.asSpan();

        /// <summary>
        /// Returns a read-only span containing the data node ids for the local variables at the
        /// initial entry point of the method.
        /// </summary>
        /// <returns>A read-only span containing the data node ids for the initial local variables.</returns>
        public ReadOnlySpan<int> getInitialLocalNodeIds() => m_initialLocalNodeIds.asSpan();

        /// <summary>
        /// Adds a definition to a data node in this compilation.
        /// </summary>
        /// <param name="nodeId">The index of the data node.</param>
        /// <param name="def">The <see cref="DataNodeOrInstrRef"/> to be added as a definition to the
        /// data node with the id <paramref name="nodeId"/>.</param>
        public void addDataNodeDef(int nodeId, DataNodeOrInstrRef def) {
            ref DataNode node = ref m_dataNodes[nodeId];
            ref DataNodeDUInfo flow = ref node.defUseInfo;

            if ((node.flags & DataNodeFlags.HAS_SINGLE_DEF) != 0) {
                DataNodeOrInstrRef currentDef = DataNodeDUInfo.singleDef(ref flow);
                if (def == currentDef)
                    return;

                DataNodeDUInfo.defs(ref flow) = m_dataNodeOrInstrRefArrayPool.allocate(2, out Span<DataNodeOrInstrRef> nodeDefs);

                nodeDefs[0] = currentDef;
                nodeDefs[1] = def;
                node.flags &= ~DataNodeFlags.HAS_SINGLE_DEF;
            }
            else if (DataNodeDUInfo.defs(ref flow).isDefault) {
                DataNodeDUInfo.singleDef(ref flow) = def;
                node.flags |= DataNodeFlags.HAS_SINGLE_DEF;
            }
            else {
                var token = DataNodeDUInfo.defs(ref flow);

                // We don't do duplicate checking for instructions because a single instruction
                // will def and/or use a node at most once.
                if (def.isDataNode) {
                    var currentDefs = m_dataNodeOrInstrRefArrayPool.getSpan(token);
                    for (int i = 0; i < currentDefs.Length; i++) {
                        if (currentDefs[i] == def)
                            return;
                    }
                }

                m_dataNodeOrInstrRefArrayPool.append(token, def);
            }
        }

        /// <summary>
        /// Adds a use to a data node in this compilation.
        /// </summary>
        /// <param name="nodeId">The index of the data node.</param>
        /// <param name="use">The <see cref="DataNodeOrInstrRef"/> to be added as a use to the
        /// data node with the id <paramref name="nodeId"/>.</param>
        public void addDataNodeUse(int nodeId, DataNodeOrInstrRef use) {
            ref DataNode node = ref m_dataNodes[nodeId];
            ref DataNodeDUInfo flow = ref node.defUseInfo;

            if ((node.flags & DataNodeFlags.HAS_SINGLE_USE) != 0) {
                DataNodeOrInstrRef currentUse = DataNodeDUInfo.singleUse(ref flow);
                if (use == currentUse)
                    return;

                DataNodeDUInfo.uses(ref flow) = m_dataNodeOrInstrRefArrayPool.allocate(2, out Span<DataNodeOrInstrRef> nodeUses);

                nodeUses[0] = currentUse;
                nodeUses[1] = use;
                node.flags &= ~DataNodeFlags.HAS_SINGLE_USE;
            }
            else if (DataNodeDUInfo.uses(ref flow).isDefault) {
                DataNodeDUInfo.singleUse(ref flow) = use;
                node.flags |= DataNodeFlags.HAS_SINGLE_USE;
            }
            else {
                var token = DataNodeDUInfo.uses(ref flow);
                var currentUses = m_dataNodeOrInstrRefArrayPool.getSpan(token);

                // We don't do duplicate checking for instructions because a single instruction
                // will def and/or use a node at most once.
                if (use.isDataNode) {
                    for (int i = 0; i < currentUses.Length; i++) {
                        if (currentUses[i] == use)
                            return;
                    }
                }

                m_dataNodeOrInstrRefArrayPool.append(token, use);
            }
        }

        /// <summary>
        /// Returns a read-only span containing the data flow definitions for the data node with the given id.
        /// </summary>
        /// <param name="nodeId">The id of the data node.</param>
        /// <returns>A read-only span containing <see cref="DataNodeOrInstrRef"/> instances representing the
        /// definitions for the node.</returns>
        public ReadOnlySpan<DataNodeOrInstrRef> getDataNodeDefs(int nodeId) => getDataNodeDefs(m_dataNodes[nodeId]);

        /// <summary>
        /// Returns a read-only span containing the data flow uses for the data node with the given id.
        /// </summary>
        /// <param name="nodeId">The id of the data node.</param>
        /// <returns>A read-only span containing <see cref="DataNodeOrInstrRef"/> instances representing the
        /// uses for the node.</returns>
        public ReadOnlySpan<DataNodeOrInstrRef> getDataNodeUses(int nodeId) => getDataNodeUses(m_dataNodes[nodeId]);

        /// <summary>
        /// Returns the number of data flow definitions for the data node with the given id.
        /// </summary>
        /// <param name="nodeId">The id of the data node.</param>
        /// <returns>The number of data flow definitions for the data node with the given id.</returns>
        public int getDataNodeDefCount(int nodeId) => getDataNodeDefCount(m_dataNodes[nodeId]);

        /// <summary>
        /// Returns the number of data flow uses for the data node with the given id.
        /// </summary>
        /// <param name="nodeId">The id of the data node.</param>
        /// <returns>The number of data flow uses for the data node with the given id.</returns>
        public int getDataNodeUseCount(int nodeId) => getDataNodeUseCount(m_dataNodes[nodeId]);

        /// <summary>
        /// Returns the node id of the stack item popped by the instruction with the given id.
        /// </summary>
        /// <param name="instrId">The id of the instruction.</param>
        /// <returns>The node id of the stack item popped by the instruction whose id is <paramref name="instrId"/>.
        /// If the instruction does not pop anything or pops more than one item, returns -1.</returns>
        public int getInstructionStackPoppedNode(int instrId) => getInstructionStackPoppedNode(m_instructions[instrId]);

        /// <summary>
        /// Returns a read-only span containing the node ids of the stack items popped by
        /// the instruction with the given id.
        /// </summary>
        /// <param name="instrId">The id of the instruction.</param>
        /// <returns>A read-only span containing the node ids of the stack items popped by
        /// the instruction whose id is <paramref name="instrId"/>.</returns>
        public ReadOnlySpan<int> getInstructionStackPoppedNodes(int instrId) => getInstructionStackPoppedNodes(m_instructions[instrId]);

        /// <summary>
        /// Returns the node id of the stack node popped by an instruction.
        /// </summary>
        /// <param name="instr">A reference to an <see cref="Instruction"/> instance.</param>
        /// <returns>The node id of the popped stack node, or -1 if the instruction does not
        /// pop anything or pops more than one item off the stack.</returns>
        public int getInstructionStackPoppedNode(in Instruction instr) =>
            ((instr.flags & InstructionFlags.HAS_SINGLE_STACK_POP) != 0) ? instr.stackPoppedNodeIds.single : -1;

        /// <summary>
        /// Returns a read-only span containing the node ids of the stack items popped by
        /// an instruction.
        /// </summary>
        /// <param name="instr">A reference to an <see cref="Instruction"/> instance.</param>
        /// <returns>A read-only span containing the node ids of the stack items popped by
        /// the instruction represented by <paramref name="instr"/>.</returns>
        public ReadOnlySpan<int> getInstructionStackPoppedNodes(in Instruction instr) {
            if ((instr.flags & InstructionFlags.HAS_SINGLE_STACK_POP) != 0) {
                return MemoryMarshal.CreateReadOnlySpan(
                    ref Unsafe.AsRef(in instr.stackPoppedNodeIds.single),
                    length: 1
                );
            }

            return m_staticIntArrayPool.getSpan(instr.stackPoppedNodeIds.staticToken);
        }

        /// <summary>
        /// Sets the stack popped nodes for an instruction.
        /// </summary>
        /// <param name="instr">A reference to the instruction.</param>
        /// <param name="nodeIds">A span containing the data node ids of the stack items popped
        /// by this instruction (in bottom-to-top order).</param>
        public void setInstructionStackPoppedNodes(ref Instruction instr, ReadOnlySpan<int> nodeIds) {
            if (nodeIds.Length == 0) {
                instr.flags &= ~InstructionFlags.HAS_SINGLE_STACK_POP;
                instr.stackPoppedNodeIds = default;
            }
            else if (nodeIds.Length == 1) {
                instr.flags |= InstructionFlags.HAS_SINGLE_STACK_POP;
                instr.stackPoppedNodeIds.single = nodeIds[0];
            }
            else {
                var token = m_staticIntArrayPool.allocate(nodeIds.Length, out Span<int> nodeIdsCopy);
                nodeIds.CopyTo(nodeIdsCopy);

                instr.flags &= ~InstructionFlags.HAS_SINGLE_STACK_POP;
                instr.stackPoppedNodeIds.staticToken = token;
            }
        }

        /// <summary>
        /// Returns a span containing the data flow definitions for the given data node.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <returns>A span containing <see cref="DataNodeOrInstrRef"/> instances representing the
        /// definitions for the node.</returns>
        public ReadOnlySpan<DataNodeOrInstrRef> getDataNodeDefs(in DataNode node) {
            if ((node.flags & DataNodeFlags.HAS_SINGLE_DEF) != 0) {
                return MemoryMarshal.CreateReadOnlySpan(
                    ref Unsafe.AsRef(in DataNodeDUInfo.singleDefReadonly(node.defUseInfo)),
                    length: 1
                );
            }

            return m_dataNodeOrInstrRefArrayPool.getSpan(DataNodeDUInfo.defsReadonly(node.defUseInfo));
        }

        /// <summary>
        /// Returns a read-only span containing the data flow uses for the given data node.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <returns>A read-only span containing <see cref="DataNodeOrInstrRef"/> instances representing the
        /// uses for the node.</returns>
        public ReadOnlySpan<DataNodeOrInstrRef> getDataNodeUses(in DataNode node) {
            if ((node.flags & DataNodeFlags.HAS_SINGLE_USE) != 0) {
                return MemoryMarshal.CreateReadOnlySpan(
                    ref Unsafe.AsRef(in DataNodeDUInfo.singleUseReadonly(node.defUseInfo)),
                    length: 1
                );
            }

            return m_dataNodeOrInstrRefArrayPool.getSpan(DataNodeDUInfo.usesReadonly(node.defUseInfo));
        }

        /// <summary>
        /// Returns the number of data flow definitions for the given data node.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <returns>The number of data flow definitions for the given data node.</returns>
        public int getDataNodeDefCount(in DataNode node) {
            if ((node.flags & DataNodeFlags.HAS_SINGLE_DEF) != 0)
                return 1;

            return m_dataNodeOrInstrRefArrayPool.getLength(DataNodeDUInfo.defsReadonly(node.defUseInfo));
        }

        /// <summary>
        /// Returns the number of data flow uses for the given data node.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <returns>The number of data flow uses for the given data node.</returns>
        public int getDataNodeUseCount(in DataNode node) {
            if ((node.flags & DataNodeFlags.HAS_SINGLE_USE) != 0)
                return 1;

            return m_dataNodeOrInstrRefArrayPool.getLength(DataNodeDUInfo.usesReadonly(node.defUseInfo));
        }

        /// <summary>
        /// Returns the instruction id of the instruction that pushed the given node onto the stack.
        /// </summary>
        /// <param name="node">A reference to a <see cref="DataNode"/> instance.</param>
        /// <returns>The id of the pushing instruction. If <paramref name="node"/> does not represent
        /// a node on the stack or is a phi node, returns -1.</returns>
        public int getStackNodePushInstrId(in DataNode node) {
            if (node.slot.kind != DataNodeSlotKind.STACK)
                return -1;

            var defs = getDataNodeDefs(node);
            if (defs.Length != 1 || !defs[0].isInstruction)
                return -1;

            return defs[0].instrOrNodeId;
        }

        /// <summary>
        /// Returns a read-only span containing the ids of the basic blocks in the reverse order of a
        /// postorder traversal of the control flow graph.
        /// </summary>
        /// <returns>A read-only span containing the ids of the basic blocks in the reverse order of a
        /// postorder traversal of the control flow graph.</returns>
        /// <remarks>
        /// The returned permutation is computed from the values of the <see cref="BasicBlock.postorderIndex"/>
        /// fields of each basic block in this <see cref="MethodCompilation"/> instance. Ensure that these
        /// fields are set to the correct values before calling this method.
        /// </remarks>
        public ReadOnlySpan<int> getBasicBlockReversePostorder() {
            if (!m_basicBlockReversePostorderDirty)
                return m_basicBlockReversePostorder.asSpan();

            var blocks = m_basicBlocks.asSpan();
            var rpo = m_basicBlockReversePostorder.clearAndAddUninitialized(m_basicBlocks.length);

            for (int i = 0; i < blocks.Length; i++)
                rpo[blocks.Length - blocks[i].postorderIndex - 1] = i;

            m_basicBlockReversePostorderDirty = false;
            return rpo;
        }

        /// <summary>
        /// Compiles a method.
        /// </summary>
        /// <param name="methodOrCtor">The <see cref="ScriptMethod"/> or <see cref="ScriptClassConstructor"/>
        /// representing the method or constructor to be compiled.</param>
        /// <param name="capturedScope">A read-only array view of <see cref="CapturedScope"/> instances
        /// representing the captured scope stack of this method.</param>
        /// <param name="methodBuilder">The <see cref="MethodBuilder"/> into which to emit the
        /// method body IL.</param>
        /// <param name="initFlags">A set of flags from the <see cref="MethodCompilationFlags"/>
        /// enumeration describing the type of the function being compiled and the initial state
        /// of the compilation.</param>
        public void compile(
            object methodOrCtor,
            CapturedScope capturedScope,
            MethodBuilder methodBuilder,
            MethodCompilationFlags initFlags = 0
        ) {
            try {
                using (var lockedContext = getContext()) {
                    if (methodOrCtor is ScriptMethod method) {
                        m_currentMethodInfo = method.abcMethodInfo;
                        m_declClass = (ScriptClass)method.declaringClass;
                        m_currentScript = lockedContext.value.getExportingScript(method);
                        m_currentMethodParams = method.getParameters();

                        if (m_declClass != null && !method.isStatic)
                            setFlag(MethodCompilationFlags.IS_INSTANCE_METHOD);

                        if (method.hasReturn)
                            setFlag(MethodCompilationFlags.HAS_RETURN_VALUE);

                        if (method.hasRest)
                            setFlag(MethodCompilationFlags.HAS_REST_PARAM);
                    }
                    else {
                        var ctor = (ScriptClassConstructor)methodOrCtor;
                        m_currentMethodInfo = ctor.abcMethodInfo;
                        m_declClass = (ScriptClass)ctor.declaringClass;
                        m_currentScript = lockedContext.value.getExportingScript(m_declClass);
                        m_currentMethodParams = ctor.getParameters();

                        setFlag(MethodCompilationFlags.IS_INSTANCE_METHOD);

                        if (ctor.hasRest)
                            setFlag(MethodCompilationFlags.HAS_REST_PARAM);
                    }

                    m_currentMethodBodyInfo = lockedContext.value.getMethodBodyInfo(m_currentMethodInfo);
                }

                setFlag(initFlags);

                if (isAnyFlagSet(MethodCompilationFlags.IS_SCOPED_FUNCTION))
                    // Remove the ScopedClosureReceiver implicit parameter
                    m_currentMethodParams = m_currentMethodParams.slice(1, m_currentMethodParams.length - 1);

                m_currentMethod = methodOrCtor;
                m_capturedScope = capturedScope;

                m_computedMaxStack = -1;
                m_computedMaxScope = -1;

                _createInitialLocalDataNodes();

                m_catchScopeClasses.addDefault(m_currentMethodBodyInfo.getExceptionInfo().length);

                m_instructionDecoderPass.run();
                m_cfAssemblyPass.run();
                m_dfAssemblyPass.run();
                m_semanticBindingPass.run();
#if DEBUG
                if (compileOptions.enableTracing)
                    trace();
#endif
                m_codegenPass.run();
                methodBuilder.setMethodBody(m_ilBuilder.createMethodBody());
            }
            catch (AVM2Exception e)
                when (!compileOptions.earlyThrowMethodBodyErrors && e.thrownValue.value is ASError err)
            {
                m_ilBuilder.reset();
                ILEmitHelper.emitThrowError(m_ilBuilder, err.GetType(), (ErrorCode)err.errorID, err.message);
                methodBuilder.setMethodBody(m_ilBuilder.createMethodBody());
            }
            finally {
                _resetState();
            }
        }

        private void _resetState() {
            m_instructions.clear();
            m_basicBlocks.clear();
            m_excHandlers.clear();
            m_dataNodes.clear();
            m_resolvedProperties.clear();
            m_initialLocalNodeIds.clear();
            m_staticIntArrayPool.clear();
            m_cfgNodeRefArrayPool.clear();
            m_dataNodeOrInstrRefArrayPool.clear();
            m_basicBlockReversePostorder.clear();
            m_catchScopeClasses.clear();

            m_flags = 0;
            m_activationClass = null;

            m_ilBuilder.reset();
        }

        private void _createInitialLocalDataNodes() {
            Span<int> nodeIds = m_initialLocalNodeIds.addUninitialized(m_currentMethodBodyInfo.localCount);

            for (int i = 0; i < nodeIds.Length; i++) {
                ref DataNode node = ref createDataNode();
                node.slot = new DataNodeSlot(DataNodeSlotKind.LOCAL, i);
                nodeIds[i] = node.id;
            }

            if (nodeIds.Length == 0)
                return;

            // First local is always the "this" argument.

            ref DataNode thisNode = ref getDataNode(nodeIds[0]);
            thisNode.isNotNull = true;

            if ((m_flags & MethodCompilationFlags.IS_SCOPED_FUNCTION) != 0) {
                thisNode.dataType = DataNodeType.OBJECT;
                thisNode.constant = new DataNodeConstant(DataNodeTypeHelper.getClass(DataNodeType.OBJECT));
            }
            if ((m_flags & MethodCompilationFlags.IS_INSTANCE_METHOD) != 0) {
                thisNode.dataType = DataNodeType.THIS;
            }
            else if (m_declClass != null) {
                // "this" in a static method is the declaring class.
                thisNode.dataType = DataNodeType.CLASS;
                thisNode.constant = new DataNodeConstant(m_declClass);
                thisNode.isConstant = true;
            }
            else {
                thisNode.dataType = DataNodeType.GLOBAL;
                thisNode.isConstant = true;
            }

            var parameters = getCurrentMethodParams().asSpan();
            for (int i = 0; i < parameters.Length && i + 1 < nodeIds.Length; i++) {
                ref DataNode node = ref getDataNode(nodeIds[i + 1]);
                node.setDataTypeFromClass(parameters[i].type);
                node.isArgument = true;
            }

            int argsEndIndex = parameters.Length + 1;

            if (parameters.Length + 1 < nodeIds.Length) {
                ref DataNode argsOrRestNode = ref getDataNode(nodeIds[argsEndIndex]);
                var methodFlags = m_currentMethodInfo.flags;

                if ((methodFlags & ABCMethodFlags.NEED_ARGUMENTS) != 0) {
                    argsOrRestNode.dataType = DataNodeType.OBJECT;
                    argsOrRestNode.constant = new DataNodeConstant(Class.fromType<ASArray>());
                    argsOrRestNode.isNotNull = true;
                    argsOrRestNode.isArgument = true;
                    argsEndIndex++;
                }
                else if ((methodFlags & ABCMethodFlags.NEED_REST) != 0) {
                    argsOrRestNode.dataType = DataNodeType.REST;
                    argsOrRestNode.isNotNull = true;
                    argsOrRestNode.isArgument = true;
                    argsEndIndex++;
                }
            }

            // Init all remaining locals to undefined.
            for (int i = argsEndIndex; i < nodeIds.Length; i++) {
                ref DataNode node = ref getDataNode(nodeIds[i]);
                node.dataType = DataNodeType.UNDEFINED;
                node.isConstant = true;
            }
        }

        /// <summary>
        /// Adds an instruction to the list of instructions in this <see cref="MethodCompilation"/>
        /// instance.
        /// </summary>
        /// <returns>A reference to the created <see cref="Instruction"/> instance.</returns>
        /// <remarks>
        /// Calling this method may invalidate references to existing instructions, in which case new
        /// references for them must be obtained from their ids using methods such as
        /// <see cref="getInstruction"/>.
        /// </remarks>
        public ref Instruction addInstruction() {
            ref Instruction instr = ref m_instructions.addUninitialized(1)[0];
            instr = default;
            instr.id = m_instructions.length - 1;
            return ref instr;
        }

        /// <summary>
        /// Adds an instruction to the list of instructions in this <see cref="MethodCompilation"/>
        /// instance.
        /// </summary>
        /// <returns>A reference to the created <see cref="BasicBlock"/> instance.</returns>
        /// <remarks>
        /// Calling this method may invalidate references to existing basic blocks, in which case new
        /// references for them must be obtained from their ids using methods such as
        /// <see cref="getBasicBlock"/>.
        /// </remarks>
        public ref BasicBlock addBasicBlock() {
            ref BasicBlock bb = ref m_basicBlocks.addUninitialized(1)[0];
            bb = default;
            bb.id = m_basicBlocks.length - 1;
            m_basicBlockReversePostorderDirty = true;
            return ref bb;
        }

        /// <summary>
        /// Adds an exception handler to the list of exception handlers in this
        /// <see cref="MethodCompilation"/> instance.
        /// </summary>
        /// <returns>A reference to the created <see cref="BasicBlock"/> instance.</returns>
        /// <remarks>
        /// Calling this method may invalidate references to existing handlers, in which case new
        /// references for them must be obtained from their ids using methods such as
        /// <see cref="getExceptionHandler"/>.
        /// </remarks>
        public ref ExceptionHandler addExceptionHandler() {
            ref ExceptionHandler handler = ref m_excHandlers.addUninitialized(1)[0];
            handler = default;
            handler.id = m_excHandlers.length - 1;
            return ref handler;
        }

        /// <summary>
        /// Allocates a <see cref="DataNode"/> instance in this compilation.
        /// </summary>
        /// <returns>A reference to the allocated <see cref="DataNode"/>. The reference can
        /// be retrieved later by passing the node's index (available in the
        /// <see cref="DataNode.id"/> field) to <see cref="getDataNode"/>.</returns>
        /// <remarks>
        /// Calling this method may invalidate references to existing nodes, in which case new
        /// references for them must be obtained from their ids using methods such as
        /// <see cref="getDataNode"/>.
        /// </remarks>
        public ref DataNode createDataNode() {
            ref DataNode node = ref m_dataNodes.addUninitialized(1)[0];
            node = default;
            node.id = m_dataNodes.length - 1;
            return ref node;
        }

        /// <summary>
        /// Allocates a <see cref="ResolvedProperty"/> instance in this compilation.
        /// </summary>
        /// <returns>A reference to the allocated <see cref="ResolvedProperty"/>. The reference can
        /// be retrieved later by passing the index of the allocated instance (available in the
        /// <see cref="ResolvedProperty.id"/> field) to <see cref="getResolvedProperty"/>.</returns>
        /// <remarks>
        /// Calling this method may invalidate references to existing <see cref="ResolvedProperty"/>
        /// instances in this compilation, in which case new references for them must be obtained
        /// from their ids using methods such as <see cref="getResolvedProperty"/>.
        /// </remarks>
        public ref ResolvedProperty createResolvedProperty() {
            ref ResolvedProperty prop = ref m_resolvedProperties.addUninitialized(1)[0];
            prop = default;
            prop.id = m_resolvedProperties.length - 1;
            return ref prop;
        }

        /// <summary>
        /// Sets the computed maximum stack and scope stack depth for the method.
        /// </summary>
        /// <param name="maxStack">The computed maximum stack depth.</param>
        /// <param name="maxScope">The computed maximum scope stack depth.</param>
        public void setComputedStackLimits(int maxStack, int maxScope) {
            Debug.Assert(maxStack <= this.maxStackSize);
            Debug.Assert(maxScope <= this.maxScopeStackSize);
            m_computedMaxStack = maxStack;
            m_computedMaxScope = maxScope;
        }

        /// <summary>
        /// Gets the immediate dominator of the given node in the control flow graph.
        /// </summary>
        /// <returns>A <see cref="CFGNodeRef"/> instance representing the immediate dominator
        /// of <paramref name="node"/>.</returns>
        /// <param name="node">A <see cref="CFGNodeRef"/> instance representing the node for
        /// which to obtain the immediate dominator.</param>
        public CFGNodeRef getImmediateDominator(CFGNodeRef node) =>
            node.isBlock ? m_basicBlocks[node.id].immediateDominator : CFGNodeRef.start;

        /// <summary>
        /// Returns the <see cref="Class"/> representing the data type of the data node with the given id.
        /// </summary>
        /// <param name="nodeId">The id of the data node.</param>
        /// <returns>A <see cref="Class"/> representing the data type of the node whose id
        /// is <paramref name="nodeId"/>.</returns>
        public Class getDataNodeClass(int nodeId) => getDataNodeClass(m_dataNodes[nodeId]);

        /// <summary>
        /// Returns the <see cref="Class"/> representing the data type of the given node.
        /// </summary>
        /// <param name="node">A reference to a data node.</param>
        /// <returns>A <see cref="Class"/> representing the data type of the given node.</returns>
        public Class getDataNodeClass(in DataNode node) {
            if (node.dataType == DataNodeType.OBJECT)
                return node.constant.classValue;

            if (node.dataType == DataNodeType.THIS)
                return m_declClass;

            return DataNodeTypeHelper.getClass(node.dataType);
        }

        /// <summary>
        /// Returns a value indicating whether the data type of the given node is a final class.
        /// </summary>
        /// <param name="node">A reference to a data node.</param>
        /// <returns>True if the data type of <paramref name="node"/> is a final class, otherwise false.</returns>
        public bool isDataNodeClassFinal(in DataNode node) {
            Class klass = getDataNodeClass(node);
            return klass != null && klass.isFinal;
        }

        /// <summary>
        /// Returns the name of the data type of the data node with the given id.
        /// </summary>
        /// <param name="nodeId">The id of the data node.</param>
        /// <returns>The name of the data type of the node whose id is <paramref name="nodeId"/>.</returns>
        public string getDataNodeTypeName(int nodeId) => getDataNodeTypeName(m_dataNodes[nodeId]);

        /// <summary>
        /// Returns the name of the data type of the given node.
        /// </summary>
        /// <param name="node">A reference to a data node.</param>
        /// <returns>The name of the data type of the given node.</returns>
        public string getDataNodeTypeName(in DataNode node) {
            return node.dataType switch {
                DataNodeType.UNKNOWN => "?",
                DataNodeType.OBJECT => node.constant.classValue.ToString(),
                DataNodeType.CLASS => "class " + node.constant.classValue.ToString(),
                DataNodeType.THIS => m_declClass.name.ToString(),
                DataNodeType.GLOBAL => "global",
                DataNodeType.ANY or DataNodeType.UNDEFINED => "*",
                DataNodeType.NULL => "null",
                _ => DataNodeTypeHelper.getClass(node.dataType).name.ToString(),
            };
        }

        /// <summary>
        /// Returns a read-only array view of <see cref="CapturedScopeItem"/> instances representing
        /// the scope stack captured by the current method from its outer context.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{CapturedScopeItem}"/> representing the
        /// captured scope stack for this method. The stack elements are ordered from bottom
        /// to top.</returns>
        public ReadOnlyArrayView<CapturedScopeItem> getCapturedScopeItems() {
            if (m_capturedScope == null)
                return default;
            return m_capturedScope.getItems(isStatic: !isAnyFlagSet(MethodCompilationFlags.IS_INSTANCE_METHOD));
        }

        /// <summary>
        /// Returns a value indicating whether any one of the flags in <paramref name="mask"/>
        /// are set in this <see cref="MethodCompilation"/>.
        /// </summary>
        /// <param name="mask">A bitwise-or combination of flags from <see cref="MethodCompilationFlags"/>.</param>
        /// <returns>True if any one of the flags in <paramref name="mask"/> are set, otherwise false.</returns>
        public bool isAnyFlagSet(MethodCompilationFlags mask) => (m_flags & mask) != 0;

        /// <summary>
        /// Returns a value indicating whether all of the flags in <paramref name="mask"/>
        /// are set in this <see cref="MethodCompilation"/>.
        /// </summary>
        /// <param name="mask">A bitwise-or combination of flags from <see cref="MethodCompilationFlags"/>.</param>
        /// <returns>True if all of the flags in <paramref name="mask"/> are set, otherwise false.</returns>
        public bool areAllFlagsSet(MethodCompilationFlags mask) => (m_flags & mask) == mask;

        /// <summary>
        /// Sets or unsets a flag in this <see cref="MethodCompilation"/>.
        /// </summary>
        /// <param name="mask">A flag from <see cref="MethodCompilationFlags"/> or a bitwise-or combination
        /// of multiple flags.</param>
        /// <param name="value">True if the flag(s) should be set, false if they should be unset.</param>
        public void setFlag(MethodCompilationFlags mask, bool value = true) {
            if (value)
                m_flags |= mask;
            else
                m_flags &= ~mask;
        }

        /// <summary>
        /// Gets a string containing the name of the method and the class in which it was declared.
        /// This string can be used for error messages and debugging, but must not be used as a
        /// dictionary key as it is not guaranteed to be unique for a given method.
        /// </summary>
        /// <returns>A string consisting of the name of the method being compiled and the class
        /// in which it is declared.</returns>
        public string getMethodNameString() {
            string className, methodName;

            ScriptMethod method = getCurrentMethod();
            if (method != null) {
                className = (method.declaringClass == null) ? "global" : method.declaringClass.name.ToString();
                methodName = method.name.ToString();
            }
            else {
                ScriptClassConstructor ctor = getCurrentConstructor();
                className = ctor.declaringClass.name.ToString();
                methodName = "constructor";
            }

            return className + "/" + methodName;
        }

        /// <summary>
        /// Creates an <see cref="AVM2Exception"/> object for a compilation error.
        /// </summary>
        /// <returns>An <see cref="AVM2Exception"/> instance that can be thrown.</returns>
        /// <param name="errCode">The error code.</param>
        /// <param name="instrId">The index of the instruction at which the error occurred. For error
        /// codes that do not include an instruction byte offset, set this to -1.</param>
        /// <param name="args">The arguments for a formatted error message. This must not include the
        /// method name or instruction offset, which are added by this method.</param>
        public AVM2Exception createError(ErrorCode errCode, int instrId, params object[] args) {
            object[] newArgs;
            if (instrId != -1) {
                newArgs = new object[args.Length + 2];
                newArgs[args.Length + 1] = getInstruction(instrId).byteOffset;
            }
            else {
                newArgs = new object[args.Length + 1];
            }

            newArgs[args.Length] = getMethodNameString();

            for (int i = 0; i < args.Length; i++)
                newArgs[i] = args[i];

            return ErrorHelper.createError(errCode, newArgs);
        }

        /// <summary>
        /// Returns the <see cref="ABCScriptInfo"/> representing the script containing the
        /// method currently being compiled.
        /// </summary>
        /// <returns>A <see cref="ABCScriptInfo"/> representing the script containing the
        /// method currently being compiled.</returns>
        public ABCScriptInfo currentScriptInfo => m_currentScript;

        /// <summary>
        /// Returns the class that must be instantiated for a newcatch instruction.
        /// </summary>
        /// <param name="excInfoId">The index of the exception_info in the ABC file that is
        /// provided as the immediate operand to the newcatch instruction.</param>
        /// <returns>A <see cref="Class"/> representing the catch scope class that should be
        /// instantiated.</returns>
        public Class getClassForCatchScope(int excInfoId) {
            ref ScriptClass klass = ref m_catchScopeClasses[excInfoId];

            if (klass != null)
                return klass;

            using (var lockedContext = getContext())
                klass = lockedContext.value.createCatchScopeClass(methodBodyInfo.getExceptionInfo()[excInfoId]);

            return klass;
        }

        /// <summary>
        /// Returns the class that must be instantiated for a newactivation instruction.
        /// </summary>
        /// <returns>A <see cref="Class"/> representing the activation class that should be
        /// instantiated. If the method does not define an activation type, returns null.</returns>
        public Class getClassForActivation() {
            if (m_activationClass == null && (methodInfo.flags & ABCMethodFlags.NEED_ACTIVATION) != 0) {
                var traits = methodBodyInfo.getActivationTraits();

                for (int i = 0; i < traits.length; i++) {
                    var kind = traits[i].kind;
                    if (kind != ABCTraitFlags.Slot && kind != ABCTraitFlags.Const)
                        throw createError(ErrorCode.MARIANA__ABC_ACTIVATION_INVALID_TRAIT_KIND, (int)kind);
                }

                using var lockedContext = getContext();
                m_activationClass = lockedContext.value.createActivationClass(traits);
            }

            return m_activationClass;
        }

        /// <summary>
        /// Returns the <see cref="ILBuilder"/> used by this <see cref="MethodCompilation"/> for
        /// emitting code.
        /// </summary>
        public ILBuilder ilBuilder => m_ilBuilder;

    }

}
