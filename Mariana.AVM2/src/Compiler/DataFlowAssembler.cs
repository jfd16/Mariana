using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mariana.AVM2.ABC;
using Mariana.AVM2.Core;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    internal sealed class DataFlowAssembler {

        private MethodCompilation m_compilation;

        private Queue<int> m_queuedBlockIds = new Queue<int>();

        private int m_computedMaxStack;

        private int m_computedMaxScope;

        private DynamicArray<DynamicArrayPoolToken<CFGNodeRef>> m_stackDefNodeSets =
            new DynamicArray<DynamicArrayPoolToken<CFGNodeRef>>(16);

        private DynamicArray<DynamicArrayPoolToken<CFGNodeRef>> m_scopeStackDefNodeSets =
            new DynamicArray<DynamicArrayPoolToken<CFGNodeRef>>(16);

        private DynamicArray<DynamicArrayPoolToken<CFGNodeRef>> m_localDefNodeSets =
            new DynamicArray<DynamicArrayPoolToken<CFGNodeRef>>(16);

        private DynamicArray<uint> m_blockNodeDFsFast = new DynamicArray<uint>(16);
        private DynamicArray<uint> m_catchNodeDFsFast = new DynamicArray<uint>(4);

        private DynamicArray<DynamicArrayPoolToken<int>> m_blockNodeDFsSlow =
            new DynamicArray<DynamicArrayPoolToken<int>>(16);

        private DynamicArray<DynamicArrayPoolToken<int>> m_catchNodeDFsSlow =
            new DynamicArray<DynamicArrayPoolToken<int>>(4);

        private DynamicArray<int> m_curStackNodeIds;
        private DynamicArray<int> m_curScopeNodeIds;
        private DynamicArray<int> m_curLocalNodeIds;

        private DynamicArray<PooledIntegerSet> m_tryRegionLocalNodeSets;

        public DataFlowAssembler(MethodCompilation compilation) {
            m_compilation = compilation;
        }

        public void run() {
            try {
                _initDefNodeArrays();
                _runFirstPass();
                _createPhiNodes();
                _runSecondPass();
            }
            finally {
                _releasePooledResources();
            }
        }

        private void _initDefNodeArrays() {
            var cfgNodeRefArrPool = m_compilation.cfgNodeRefArrayPool;

            var stackDefNodes = m_stackDefNodeSets.clearAndAddUninitialized(m_compilation.maxStackSize);
            var scopeStackDefNodes = m_scopeStackDefNodeSets.clearAndAddUninitialized(m_compilation.maxScopeStackSize);
            var localDefNodes = m_localDefNodeSets.clearAndAddUninitialized(m_compilation.localCount);

            for (int i = 0; i < stackDefNodes.Length; i++)
                stackDefNodes[i] = cfgNodeRefArrPool.allocate(0);

            for (int i = 0; i < scopeStackDefNodes.Length; i++)
                scopeStackDefNodes[i] = cfgNodeRefArrPool.allocate(0);

            for (int i = 0; i < localDefNodes.Length; i++) {
                // The start node provides definitions for all locals.
                localDefNodes[i] = cfgNodeRefArrPool.allocate(1, out Span<CFGNodeRef> span);
                span[0] = CFGNodeRef.start;
            }
        }

        /// <summary>
        /// Allocates an integer array of the given length from the current compilation's static
        /// integer array pool. Each element of the allocated array is initialized to -1.
        /// </summary>
        /// <param name="length">The length of the array to allocate.</param>
        /// <returns>The token for the allocated array.</returns>
        private StaticArrayPoolToken<int> _allocDataNodeIdArray(int length) {
            if (length == 0)
                return default(StaticArrayPoolToken<int>);

            var token = m_compilation.staticIntArrayPool.allocate(length, out Span<int> span);
            span.Fill(-1);
            return token;
        }

        private void _runFirstPass() {
            m_computedMaxStack = 0;
            m_computedMaxScope = 0;

            Span<BasicBlock> blocks = m_compilation.getBasicBlocks();
            Queue<int> queue = m_queuedBlockIds;
            queue.Clear();

            ref BasicBlock firstBlock = ref m_compilation.getBasicBlockOfInstruction(0);
            firstBlock.localsAtEntry = _allocDataNodeIdArray(m_compilation.localCount);
            firstBlock.flags |= BasicBlockFlags.TOUCHED;

            queue.Enqueue(firstBlock.id);

            while (queue.Count > 0) {
                ref BasicBlock block = ref blocks[queue.Dequeue()];
                if ((block.flags & BasicBlockFlags.VISITED) == 0)
                    _firstPassVisitBasicBlock(ref block);
            }

            m_compilation.setComputedStackLimits(m_computedMaxStack, m_computedMaxScope);
            _clearVisitedFlags();
        }

        private void _firstPassVisitBasicBlock(ref BasicBlock block) {

            var cfgNodeRefArrPool = m_compilation.cfgNodeRefArrayPool;
            var staticIntArrPool = m_compilation.staticIntArrayPool;

            int entryStackSize = staticIntArrPool.getLength(block.stackAtEntry);
            int entryScopeSize = staticIntArrPool.getLength(block.scopeStackAtEntry);

            int curStackSize = entryStackSize;
            int minStackSize = curStackSize;
            int maxStackSize = curStackSize;
            int stackSizeLimit = m_compilation.maxStackSize;

            int curScopeSize = entryScopeSize;
            int minScopeSize = curScopeSize;
            int maxScopeSize = curScopeSize;
            int scopeSizeLimit = m_compilation.maxScopeStackSize;

            int localCount = staticIntArrPool.getLength(block.localsAtEntry);

            CFGNodeRef thisBlockRef = CFGNodeRef.forBasicBlock(block.id);
            bool hasLocalAssignments = false;

            Span<Instruction> instructions = m_compilation.getInstructionsInBasicBlock(block.id);

            for (int i = 0; i < instructions.Length; i++) {
                ref Instruction instr = ref instructions[i];

                if (instr.opcode == ABCOp.swap) {
                    if (curStackSize < 2)
                        throw m_compilation.createError(ErrorCode.STACK_UNDERFLOW, instr.id);

                    // This ensures that if a stack node from the input to this block is swapped,
                    // it is considered as a redefinition (for the purpose of phi-node placement)
                    minStackSize = Math.Min(minStackSize, curStackSize - 2);
                    continue;
                }

                ABCOp opcode = instr.opcode;

                ABCOpInfo opInfo = ABCOpInfo.getInfo(opcode);
                if (!opInfo.isValid)
                    throw m_compilation.createError(ErrorCode.ILLEGAL_OPCODE, instr.id, (int)opcode);

                Debug.Assert(opInfo.stackPushCount <= 1);

                int popCount = (opInfo.stackPopCount != -1) ? opInfo.stackPopCount : _getInstrStackPopCount(instr);

                curStackSize -= popCount;

                // Store the pop count here temporarily until the second pass, where the actual node
                // ids are available.
                instr.stackPoppedNodeIds.single = popCount;

                // dup and checkfilter don't consume anything, but there must be something on the stack.
                if (curStackSize < 0 || (curStackSize == 0 && (opcode == ABCOp.dup || opcode == ABCOp.checkfilter)))
                    throw m_compilation.createError(ErrorCode.STACK_UNDERFLOW, instr.id);

                minStackSize = Math.Min(minStackSize, curStackSize);

                curStackSize += opInfo.stackPushCount;
                if (curStackSize > stackSizeLimit)
                    throw m_compilation.createError(ErrorCode.STACK_OVERFLOW, instr.id);

                maxStackSize = Math.Max(maxStackSize, curStackSize);

                // Update scope stack.

                if (opInfo.pushesToScopeStack) {
                    curScopeSize++;
                    if (curScopeSize > scopeSizeLimit)
                        throw m_compilation.createError(ErrorCode.SCOPE_STACK_OVERFLOW, instr.id);

                    maxScopeSize = Math.Max(maxScopeSize, curScopeSize);
                }
                else if (opInfo.popsFromScopeStack) {
                    curScopeSize--;
                    if (curScopeSize < 0)
                        throw m_compilation.createError(ErrorCode.SCOPE_STACK_UNDERFLOW, instr.id);

                    minScopeSize = Math.Min(minScopeSize, curScopeSize);
                }

                // Update locals.

                if (opcode == ABCOp.hasnext2) {
                    ref var data = ref instr.data.hasnext2;

                    if (data.localId1 >= localCount)
                        throw m_compilation.createError(ErrorCode.INVALID_REGISTER_ACCESS, instr.id, data.localId1);

                    if (data.localId2 >= localCount)
                        throw m_compilation.createError(ErrorCode.INVALID_REGISTER_ACCESS, instr.id, data.localId2);

                    cfgNodeRefArrPool.append(m_localDefNodeSets[data.localId1], thisBlockRef);
                    cfgNodeRefArrPool.append(m_localDefNodeSets[data.localId2], thisBlockRef);
                    hasLocalAssignments = true;
                }
                else if (opInfo.readsLocal || opInfo.writesLocal) {
                    int localId = instr.data.getSetLocal.localId;

                    if (localId >= localCount)
                        throw m_compilation.createError(ErrorCode.INVALID_REGISTER_ACCESS, instr.id, localId);

                    if (opInfo.writesLocal) {
                        cfgNodeRefArrPool.append(m_localDefNodeSets[localId], thisBlockRef);
                        hasLocalAssignments = true;
                    }
                }
            }

            m_computedMaxStack = Math.Max(m_computedMaxStack, maxStackSize);
            m_computedMaxScope = Math.Max(m_computedMaxScope, maxScopeSize);

            // Anything on the stack at the exit of the block starting at the minimum
            // stack depth attained during the block's execution would have been pushed
            // by code inside the block (and hence is a definition). Same for the scope stack.

            var definedStackNodes = m_stackDefNodeSets.asSpan(minStackSize, curStackSize - minStackSize);
            var definedScopeNodes = m_scopeStackDefNodeSets.asSpan(minScopeSize, curScopeSize - minScopeSize);

            for (int i = 0; i < definedStackNodes.Length; i++)
                cfgNodeRefArrPool.append(definedStackNodes[i], thisBlockRef);

            for (int i = 0; i < definedScopeNodes.Length; i++)
                cfgNodeRefArrPool.append(definedScopeNodes[i], thisBlockRef);

            bool noStackChange = minStackSize == curStackSize && curStackSize == entryStackSize;
            bool noScopeChange = minScopeSize == curScopeSize && curScopeSize == entryScopeSize;

            block.flags |= BasicBlockFlags.VISITED;

            Span<int> exitBlockIds = staticIntArrPool.getSpan(block.exitBlockIds);

            for (int i = 0; i < exitBlockIds.Length; i++) {
                ref BasicBlock nextBlock = ref m_compilation.getBasicBlock(exitBlockIds[i]);

                if ((nextBlock.flags & BasicBlockFlags.TOUCHED) == 0) {
                    // Allocate the state arrays for the successor blocks.
                    // If there is no change in the stack, scope or local state and the successor
                    // block has no other predecessors, the array tokens for the state can be shared
                    // between the two blocks as a memory optimization.

                    bool nextBlockHasSingleEntry = cfgNodeRefArrPool.getLength(nextBlock.entryPoints) == 1;

                    nextBlock.stackAtEntry = (nextBlockHasSingleEntry && noStackChange)
                        ? block.stackAtEntry
                        : _allocDataNodeIdArray(curStackSize);

                    nextBlock.scopeStackAtEntry = (nextBlockHasSingleEntry && noScopeChange)
                        ? block.scopeStackAtEntry
                        : _allocDataNodeIdArray(curScopeSize);

                    nextBlock.localsAtEntry = (nextBlockHasSingleEntry && !hasLocalAssignments)
                        ? block.localsAtEntry
                        : _allocDataNodeIdArray(localCount);

                    nextBlock.flags |= BasicBlockFlags.TOUCHED;
                    m_queuedBlockIds.Enqueue(nextBlock.id);
                }
                else {
                    // Check for stack/scope stack size mismatch.

                    Debug.Assert(cfgNodeRefArrPool.getLength(nextBlock.entryPoints) > 1);
                    int lastInstrId = instructions[instructions.Length - 1].id;

                    int nextEntryStackSize = staticIntArrPool.getLength(nextBlock.stackAtEntry);
                    int nextEntryScopeSize = staticIntArrPool.getLength(nextBlock.scopeStackAtEntry);

                    if (nextEntryStackSize != curStackSize)
                        throw m_compilation.createError(ErrorCode.STACK_DEPTH_UNBALANCED, lastInstrId, curStackSize, nextEntryStackSize);

                    if (nextEntryScopeSize != curScopeSize)
                        throw m_compilation.createError(ErrorCode.SCOPE_DEPTH_UNBALANCED, lastInstrId, curScopeSize, nextEntryScopeSize);
                }
            }

            if (block.excHandlerId != -1)
                _firstPassVisitExcHandler(ref m_compilation.getExceptionHandler(block.excHandlerId));

        }

        private void _firstPassVisitExcHandler(ref ExceptionHandler eh) {
            if ((eh.flags & ExceptionHandlerFlags.VISITED) != 0)
                return;

            if (eh.parentId != -1)
                _firstPassVisitExcHandler(ref m_compilation.getExceptionHandler(eh.parentId));

            eh.flags |= ExceptionHandlerFlags.VISITED;

            int localCount = m_compilation.localCount;
            ref Instruction targetInstr = ref m_compilation.getInstruction(eh.catchTargetInstrId);

            if (m_compilation.maxStackSize == 0)
                throw m_compilation.createError(ErrorCode.STACK_OVERFLOW, targetInstr.id);

            var catchNodeRef = CFGNodeRef.forCatch(eh.id);
            m_compilation.cfgNodeRefArrayPool.append(m_stackDefNodeSets[0], catchNodeRef);

            // Catch nodes redefine all local variables for SSA.
            var localDefNodeSets = m_localDefNodeSets.asSpan();
            for (int i = 0; i < localDefNodeSets.Length; i++)
                m_compilation.cfgNodeRefArrayPool.append(localDefNodeSets[i], catchNodeRef);

            ref BasicBlock targetBlock = ref m_compilation.getBasicBlock(targetInstr.blockId);
            Debug.Assert(targetBlock.firstInstrId == eh.catchTargetInstrId);

            if ((targetBlock.flags & BasicBlockFlags.TOUCHED) == 0) {
                targetBlock.stackAtEntry = _allocDataNodeIdArray(1);
                targetBlock.scopeStackAtEntry = _allocDataNodeIdArray(0);
                targetBlock.localsAtEntry = _allocDataNodeIdArray(localCount);
                targetBlock.flags |= BasicBlockFlags.TOUCHED;
                m_queuedBlockIds.Enqueue(targetBlock.id);
            }
            else {
                int targetEntryStackSize = m_compilation.staticIntArrayPool.getLength(targetBlock.stackAtEntry);
                int targetEntryScopeSize = m_compilation.staticIntArrayPool.getLength(targetBlock.scopeStackAtEntry);

                if (targetEntryStackSize != 1)
                    throw m_compilation.createError(ErrorCode.STACK_DEPTH_UNBALANCED, targetInstr.id, 1, targetEntryStackSize);

                if (targetEntryScopeSize != 0)
                    throw m_compilation.createError(ErrorCode.SCOPE_DEPTH_UNBALANCED, targetInstr.id, 0, targetEntryScopeSize);
            }
        }

        private void _clearVisitedFlags() {
            var blocks = m_compilation.getBasicBlocks();
            var excHandlers = m_compilation.getExceptionHandlers();

            for (int i = 0; i < blocks.Length; i++)
                blocks[i].flags &= ~(BasicBlockFlags.VISITED | BasicBlockFlags.TOUCHED);

            for (int i = 0; i < excHandlers.Length; i++)
                excHandlers[i].flags &= ~ExceptionHandlerFlags.VISITED;
        }

        /// <summary>
        /// Creates a new data node.
        /// </summary>
        /// <param name="slotKind">The slot kind (stack/scope/local) for the data node.</param>
        /// <param name="slotId">The slot index for the data node.</param>
        /// <param name="isPhi">True to create a phi node, otherwise false.</param>
        /// <returns>The data node id for the newly created node.</returns>
        private int _createDataNode(DataNodeSlotKind slotKind, int slotId, bool isPhi) {
            ref DataNode node = ref m_compilation.createDataNode();
            node.slot = new DataNodeSlot(slotKind, slotId);
            node.isPhi = isPhi;
            return node.id;
        }

        private void _createPhiNodes() {
            if (m_compilation.getBasicBlocks().Length <= 32)
                _createPhiNodesFast();
            else
                _createPhiNodesSlow();
        }

        private void _createPhiNodesFast() {
            var blocks = m_compilation.getBasicBlocks();
            var excHandlers = m_compilation.getExceptionHandlers();

            var staticIntArrayPool = m_compilation.staticIntArrayPool;
            var intArrayPool = m_compilation.intArrayPool;
            var cfgNodeRefArrayPool = m_compilation.cfgNodeRefArrayPool;

            var blockNodeDFs = m_blockNodeDFsFast.clearAndAddDefault(blocks.Length);
            var catchNodeDFs = m_catchNodeDFsFast.clearAndAddDefault(excHandlers.Length);

            ReadOnlySpan<int> rpo = m_compilation.getBasicBlockReversePostorder();

            for (int i = 0; i < rpo.Length; i++) {
                ref BasicBlock block = ref blocks[rpo[i]];
                Debug.Assert(block.id < 32);

                Span<CFGNodeRef> entryPoints = cfgNodeRefArrayPool.getSpan(block.entryPoints);
                if (entryPoints.Length < 2)
                    continue;

                CFGNodeRef idom = m_compilation.getImmediateDominator(CFGNodeRef.forBasicBlock(block.id));

                for (int j = 0; j < entryPoints.Length; j++) {
                    CFGNodeRef runner = entryPoints[j];
                    while (runner != idom) {
                        Debug.Assert(!runner.isStart);

                        ref uint df = ref (runner.isBlock ? ref blockNodeDFs[runner.id] : ref catchNodeDFs[runner.id]);
                        df |= 1u << block.id;

                        runner = m_compilation.getImmediateDominator(runner);
                    }
                }
            }

            var stackDefNodeSets = m_stackDefNodeSets.asSpan();
            var scopeDefNodeSets = m_scopeStackDefNodeSets.asSpan();
            var localDefNodeSets = m_localDefNodeSets.asSpan();

            for (int stackIndex = 0; stackIndex < stackDefNodeSets.Length; stackIndex++) {
                Span<CFGNodeRef> defNodes = cfgNodeRefArrayPool.getSpan(stackDefNodeSets[stackIndex]);
                if (defNodes.Length == 0)
                    continue;

                uint blockBits = _computeIteratedDFFast(defNodes, blockNodeDFs, catchNodeDFs);

                for (int blockId = 0; blockBits != 0; blockId++, blockBits >>= 1) {
                    if ((blockBits & 1) == 0)
                        continue;

                    ref BasicBlock block = ref blocks[blockId];
                    Span<int> stackAtEntry = staticIntArrayPool.getSpan(block.stackAtEntry);

                    if (stackIndex < stackAtEntry.Length) {
                        stackAtEntry[stackIndex] = _createDataNode(DataNodeSlotKind.STACK, stackIndex, true);
                        block.flags |= BasicBlockFlags.DEFINES_PHI_NODES;
                    }
                }
            }

            for (int scopeIndex = 0; scopeIndex < scopeDefNodeSets.Length; scopeIndex++) {
                Span<CFGNodeRef> defNodes = cfgNodeRefArrayPool.getSpan(scopeDefNodeSets[scopeIndex]);
                if (defNodes.Length == 0)
                    continue;

                uint blockBits = _computeIteratedDFFast(defNodes, blockNodeDFs, catchNodeDFs);

                for (int blockId = 0; blockBits != 0; blockId++, blockBits >>= 1) {
                    if ((blockBits & 1) == 0)
                        continue;

                    ref BasicBlock block = ref blocks[blockId];
                    Span<int> scopeAtEntry = staticIntArrayPool.getSpan(block.scopeStackAtEntry);

                    if (scopeIndex < scopeAtEntry.Length) {
                        scopeAtEntry[scopeIndex] = _createDataNode(DataNodeSlotKind.SCOPE, scopeIndex, true);
                        block.flags |= BasicBlockFlags.DEFINES_PHI_NODES;
                    }
                }
            }

            for (int localIndex = 0; localIndex < localDefNodeSets.Length; localIndex++) {
                Span<CFGNodeRef> defNodes = cfgNodeRefArrayPool.getSpan(localDefNodeSets[localIndex]);
                if (defNodes.Length == 0)
                    continue;

                uint blockBits = _computeIteratedDFFast(defNodes, blockNodeDFs, catchNodeDFs);

                for (int blockId = 0; blockBits != 0; blockId++, blockBits >>= 1) {
                    if ((blockBits & 1) == 0)
                        continue;

                    ref BasicBlock block = ref blocks[blockId];
                    Span<int> localsAtEntry = staticIntArrayPool.getSpan(blocks[blockId].localsAtEntry);

                    localsAtEntry[localIndex] = _createDataNode(DataNodeSlotKind.LOCAL, localIndex, true);
                    block.flags |= BasicBlockFlags.DEFINES_PHI_NODES;
                }
            }
        }

        /// <summary>
        /// Computes the iterated dominance frontier for the given set of control flow nodes as
        /// a bit vector. This method only works when the number of basic blocks in the method
        /// body is at most 32.
        /// </summary>
        /// <param name="nodes">A span containing the control flow nodes for which to compute
        /// the iterated DF.</param>
        /// <param name="blockDFs">A span containing the DFs of the basic blocks as bit vectors,
        /// indexed by block id.</param>
        /// <param name="catchDFs">A span containing the DFs of the catch nodes as bit vectors,
        /// indexed by exception handler id.</param>
        /// <returns>The computed iterated dominance frontier as a 32-bit vector.</returns>
        private static uint _computeIteratedDFFast(
            ReadOnlySpan<CFGNodeRef> nodes, ReadOnlySpan<uint> blockDFs, ReadOnlySpan<uint> catchDFs)
        {
            uint idf = 0;
            uint current = 0;

            for (int i = 0; i < nodes.Length; i++) {
                var node = nodes[i];

                if (node.isBlock)
                    current |= blockDFs[node.id];
                else if (node.isCatch)
                    current |= catchDFs[node.id];
            }

            while (current != 0) {
                idf |= current;
                uint next = 0;

                for (int blockId = 0; current != 0; blockId++, current >>= 1) {
                    if ((current & 1) != 0)
                        next |= blockDFs[blockId] & ~idf;
                }

                current = next;
            }

            return idf;
        }

        private void _createPhiNodesSlow() {
            var blocks = m_compilation.getBasicBlocks();
            var excHandlers = m_compilation.getExceptionHandlers();

            var staticIntArrayPool = m_compilation.staticIntArrayPool;
            var intArrayPool = m_compilation.intArrayPool;
            var cfgNodeRefArrayPool = m_compilation.cfgNodeRefArrayPool;

            var blockNodeDFs = m_blockNodeDFsSlow.clearAndAddDefault(blocks.Length);
            var catchNodeDFs = m_catchNodeDFsSlow.clearAndAddDefault(excHandlers.Length);

            ReadOnlySpan<int> rpo = m_compilation.getBasicBlockReversePostorder();

            for (int i = 0; i < rpo.Length; i++) {
                ref BasicBlock block = ref blocks[rpo[i]];

                Span<CFGNodeRef> entryPoints = cfgNodeRefArrayPool.getSpan(block.entryPoints);
                if (entryPoints.Length < 2)
                    continue;

                CFGNodeRef idom = m_compilation.getImmediateDominator(CFGNodeRef.forBasicBlock(block.id));

                for (int j = 0; j < entryPoints.Length; j++) {
                    CFGNodeRef runner = entryPoints[j];
                    while (runner != idom) {
                        Debug.Assert(!runner.isStart);

                        ref var df = ref (runner.isBlock ? ref blockNodeDFs[runner.id] : ref catchNodeDFs[runner.id]);
                        if (df.isDefault)
                            df = intArrayPool.allocate(0);

                        intArrayPool.append(df, block.id);
                        runner = m_compilation.getImmediateDominator(runner);
                    }
                }
            }

            var stackDefNodeSets = m_stackDefNodeSets.asSpan();
            var scopeDefNodeSets = m_scopeStackDefNodeSets.asSpan();
            var localDefNodeSets = m_localDefNodeSets.asSpan();

            var idfResultToken = intArrayPool.allocate(0);

            try {
                for (int stackIndex = 0; stackIndex < stackDefNodeSets.Length; stackIndex++) {
                    Span<CFGNodeRef> defNodes = cfgNodeRefArrayPool.getSpan(stackDefNodeSets[stackIndex]);
                    if (defNodes.Length == 0)
                        continue;

                    Span<int> blockIds = _computeIteratedDFSlow(defNodes, blockNodeDFs, catchNodeDFs, idfResultToken);

                    for (int i = 0; i < blockIds.Length; i++) {
                        ref BasicBlock block = ref blocks[blockIds[i]];
                        Span<int> stackAtEntry = staticIntArrayPool.getSpan(block.stackAtEntry);

                        if (stackIndex < stackAtEntry.Length) {
                            stackAtEntry[stackIndex] = _createDataNode(DataNodeSlotKind.STACK, stackIndex, true);
                            block.flags |= BasicBlockFlags.DEFINES_PHI_NODES;
                        }
                    }
                }

                for (int scopeIndex = 0; scopeIndex < scopeDefNodeSets.Length; scopeIndex++) {
                    Span<CFGNodeRef> defNodes = cfgNodeRefArrayPool.getSpan(scopeDefNodeSets[scopeIndex]);
                    if (defNodes.Length == 0)
                        continue;

                    Span<int> blockIds = _computeIteratedDFSlow(defNodes, blockNodeDFs, catchNodeDFs, idfResultToken);

                    for (int i = 0; i < blockIds.Length; i++) {
                        ref BasicBlock block = ref blocks[blockIds[i]];
                        Span<int> scopeAtEntry = staticIntArrayPool.getSpan(block.scopeStackAtEntry);

                        if (scopeIndex < scopeAtEntry.Length) {
                            scopeAtEntry[scopeIndex] = _createDataNode(DataNodeSlotKind.SCOPE, scopeIndex, true);
                            block.flags |= BasicBlockFlags.DEFINES_PHI_NODES;
                        }
                    }
                }

                for (int localIndex = 0; localIndex < localDefNodeSets.Length; localIndex++) {
                    Span<CFGNodeRef> defNodes = cfgNodeRefArrayPool.getSpan(localDefNodeSets[localIndex]);
                    if (defNodes.Length == 0)
                        continue;

                    Span<int> blockIds = _computeIteratedDFSlow(defNodes, blockNodeDFs, catchNodeDFs, idfResultToken);

                    for (int i = 0; i < blockIds.Length; i++) {
                        ref BasicBlock block = ref blocks[blockIds[i]];
                        Span<int> localsAtEntry = staticIntArrayPool.getSpan(block.localsAtEntry);

                        localsAtEntry[localIndex] = _createDataNode(DataNodeSlotKind.LOCAL, localIndex, true);
                        block.flags |= BasicBlockFlags.DEFINES_PHI_NODES;
                    }
                }
            }
            finally {
                intArrayPool.free(idfResultToken);
            }
        }

        /// <summary>
        /// Computes the iterated dominance frontier for the given set of control flow nodes.
        /// </summary>
        ///
        /// <param name="nodes">A span containing the control flow nodes for which to compute the
        /// iterated DF.</param>
        /// <param name="blockDFs">A span containing the DFs of the basic blocks as tokens from
        /// the dynamic int array pool, indexed by block id.</param>
        /// <param name="catchDFs">A span containing the DFs of the catch nodes as tokens from
        /// the dynamic int array pool, indexed by exception handler id.</param>
        /// <param name="dest">A token representing an allocated array in the dynamic int
        /// array pool into which to write the block ids of the computed set.</param>
        ///
        /// <returns>A span containing the block ids of the computed iterated dominance frontier
        /// of the given set of nodes. This span is always a slice of the array in the dynamic
        /// int array pool associated with the <paramref name="dest"/> token.</returns>
        private Span<int> _computeIteratedDFSlow(
            ReadOnlySpan<CFGNodeRef> nodes,
            ReadOnlySpan<DynamicArrayPoolToken<int>> blockDFs,
            ReadOnlySpan<DynamicArrayPoolToken<int>> catchDFs,
            DynamicArrayPoolToken<int> dest
        ) {
            var intArrayPool = m_compilation.intArrayPool;

            var idf = new PooledIntegerSet(intArrayPool);
            var current = new PooledIntegerSet(intArrayPool);
            var next = new PooledIntegerSet(intArrayPool);

            try {
                for (int i = 0; i < nodes.Length; i++) {
                    CFGNodeRef node = nodes[i];

                    if (node.isBlock)
                        current.add(intArrayPool.getSpan(blockDFs[node.id]));
                    else if (node.isCatch)
                        current.add(intArrayPool.getSpan(catchDFs[node.id]));
                }

                while (current.count > 0) {
                    idf.add(current);

                    foreach (int blockId in current) {
                        Span<int> dfBlockIds = intArrayPool.getSpan(blockDFs[blockId]);
                        for (int i = 0; i < dfBlockIds.Length; i++) {
                            int nextBlockId = dfBlockIds[i];
                            if (!idf.contains(nextBlockId))
                                next.add(nextBlockId);
                        }
                    }

                    // Swapping two PooledIntegerSet instances is safe even though value copying is not.
                    (current, next) = (next, current);
                    next.clear();
                }

                Span<int> destSpan = intArrayPool.getSpan(dest);

                if (destSpan.Length < idf.count)
                    intArrayPool.resize(dest, idf.count, out destSpan);
                else
                    destSpan = destSpan.Slice(0, idf.count);

                idf.writeValues(destSpan);
                return destSpan;
            }
            finally {
                idf.free();
                current.free();
                next.free();
            }
        }

        private void _runSecondPass() {
            Span<BasicBlock> blocks = m_compilation.getBasicBlocks();
            ref BasicBlock firstBlock = ref m_compilation.getBasicBlockOfInstruction(0);

            m_curStackNodeIds.clearAndAddUninitialized(m_computedMaxStack);
            m_curScopeNodeIds.clearAndAddUninitialized(m_computedMaxScope);
            m_curLocalNodeIds.clearAndAddUninitialized(m_compilation.localCount);

            m_tryRegionLocalNodeSets.clearAndAddUninitialized(m_compilation.getExceptionHandlers().Length);
            for (int i = 0; i < m_tryRegionLocalNodeSets.length; i++)
                m_tryRegionLocalNodeSets[i] = new PooledIntegerSet(m_compilation.intArrayPool);

            m_queuedBlockIds.Clear();

            firstBlock.flags |= BasicBlockFlags.TOUCHED;
            m_queuedBlockIds.Enqueue(firstBlock.id);

            _initStateAndMarkCatchTargets();
            _initStateForMainEntryBlock();

            while (m_queuedBlockIds.Count > 0) {
                ref BasicBlock block = ref blocks[m_queuedBlockIds.Dequeue()];
                if ((block.flags & BasicBlockFlags.VISITED) == 0)
                    _secondPassVisitBasicBlock(ref block);
            }

            m_queuedBlockIds.Clear();
            _clearVisitedFlags();

            _assignSourcesForCatchPhiLocals();
        }

        private void _initStateForMainEntryBlock() {
            // Fill in the locals at entry for the main entry block.
            ref BasicBlock firstBlock = ref m_compilation.getBasicBlockOfInstruction(0);
            var firstBlockEntryLocals = m_compilation.staticIntArrayPool.getSpan(firstBlock.localsAtEntry);
            var initialLocalNodeIds = m_compilation.getInitialLocalNodeIds();

            for (int i = 0; i < firstBlockEntryLocals.Length; i++) {
                int nodeId = initialLocalNodeIds[i];
                ref int entryNodeId = ref firstBlockEntryLocals[i];

                if (entryNodeId == -1) {
                    entryNodeId = nodeId;
                }
                else {
                    m_compilation.getDataNode(nodeId).flags |= DataNodeFlags.PHI_SOURCE;
                    m_compilation.addDataNodeDef(entryNodeId, DataNodeOrInstrRef.forDataNode(nodeId));
                    m_compilation.addDataNodeUse(nodeId, DataNodeOrInstrRef.forDataNode(entryNodeId));
                }
            }
        }

        private void _initStateAndMarkCatchTargets() {
            var excHandlers = m_compilation.getExceptionHandlers();

            for (int i = 0; i < excHandlers.Length; i++) {
                ref ExceptionHandler eh = ref excHandlers[i];
                ref BasicBlock targetBlock = ref m_compilation.getBasicBlockOfInstruction(eh.catchTargetInstrId);

                ref DataNode catchStackNode = ref m_compilation.createDataNode();
                catchStackNode.setDataTypeFromClass(eh.errorType);
                catchStackNode.slot = new DataNodeSlot(DataNodeSlotKind.STACK, 0);

                eh.catchStackNodeId = catchStackNode.id;

                var targetEntryStack = m_compilation.staticIntArrayPool.getSpan(targetBlock.stackAtEntry);
                Debug.Assert(targetEntryStack.Length == 1);

                if (targetEntryStack[0] == -1) {
                    targetEntryStack[0] = catchStackNode.id;
                }
                else {
                    catchStackNode.flags |= DataNodeFlags.PHI_SOURCE;
                    m_compilation.addDataNodeDef(targetEntryStack[0], DataNodeOrInstrRef.forDataNode(catchStackNode.id));
                    m_compilation.addDataNodeUse(catchStackNode.id, DataNodeOrInstrRef.forDataNode(targetEntryStack[0]));
                }

                // Create phi nodes for all local variables at the catch target of the exception
                // handler. These nodes will be linked to those in the try block.
                var targetEntryLocals = m_compilation.staticIntArrayPool.getSpan(targetBlock.localsAtEntry);
                for (int j = 0; j < targetEntryLocals.Length; j++) {
                    if (targetEntryLocals[j] == -1)
                        targetEntryLocals[j] = _createDataNode(DataNodeSlotKind.LOCAL, j, true);
                }

                if (targetEntryLocals.Length > 0)
                    targetBlock.flags |= BasicBlockFlags.DEFINES_PHI_NODES;

                targetBlock.flags |= BasicBlockFlags.TOUCHED;
                m_queuedBlockIds.Enqueue(targetBlock.id);
            }
        }

        private void _secondPassVisitBasicBlock(ref BasicBlock block) {
            var staticIntArrayPool = m_compilation.staticIntArrayPool;

            var curStackNodeIds = m_curStackNodeIds.asSpan();
            var curScopeNodeIds = m_curScopeNodeIds.asSpan();
            var curLocalNodeIds = m_curLocalNodeIds.asSpan();

            var stackAtEntry = staticIntArrayPool.getSpan(block.stackAtEntry);
            var scopeAtEntry = staticIntArrayPool.getSpan(block.scopeStackAtEntry);
            var localsAtEntry = staticIntArrayPool.getSpan(block.localsAtEntry);

            Debug.Assert(stackAtEntry.IndexOf(-1) == -1);
            Debug.Assert(scopeAtEntry.IndexOf(-1) == -1);
            Debug.Assert(localsAtEntry.IndexOf(-1) == -1);

            stackAtEntry.CopyTo(curStackNodeIds.Slice(0, stackAtEntry.Length));
            scopeAtEntry.CopyTo(curScopeNodeIds.Slice(0, scopeAtEntry.Length));
            localsAtEntry.CopyTo(curLocalNodeIds);

            if (block.excHandlerId != -1)
                m_tryRegionLocalNodeSets[block.excHandlerId].add(curLocalNodeIds);

            int curStackSize = stackAtEntry.Length, curScopeSize = scopeAtEntry.Length;

            var instructions = m_compilation.getInstructionsInBasicBlock(block);

            for (int i = 0; i < instructions.Length; i++) {
                ref Instruction instr = ref instructions[i];
                ABCOpInfo opInfo = ABCOpInfo.getInfo(instr.opcode);

                var instrDataFlowNodeRef = DataNodeOrInstrRef.forInstruction(instr.id);

                if (instr.opcode == ABCOp.dup) {
                    ref DataNode inputNode = ref m_compilation.getDataNode(curStackNodeIds[curStackSize - 1]);
                    int dupNodeId = _createDataNode(DataNodeSlotKind.STACK, curStackSize, false);

                    instr.data.dupOrSwap.nodeId1 = inputNode.id;
                    instr.data.dupOrSwap.nodeId2 = dupNodeId;
                    instr.stackPushedNodeId = dupNodeId;

                    m_compilation.addDataNodeUse(inputNode.id, instrDataFlowNodeRef);
                    m_compilation.addDataNodeDef(dupNodeId, instrDataFlowNodeRef);
                    curStackNodeIds[curStackSize] = dupNodeId;
                    curStackSize++;

                    continue;
                }

                // swap and checkfilter are special cases as they do not create defs or uses for
                // any of the stack nodes.

                if (instr.opcode == ABCOp.swap) {
                    ref int stackSlot1 = ref curStackNodeIds[curStackSize - 2];
                    ref int stackSlot2 = ref curStackNodeIds[curStackSize - 1];

                    int nodeId1 = stackSlot1, nodeId2 = stackSlot2;
                    instr.data.dupOrSwap.nodeId1 = nodeId1;
                    instr.data.dupOrSwap.nodeId2 = nodeId2;
                    instr.stackPushedNodeId = -1;

                    stackSlot1 = nodeId2;
                    stackSlot2 = nodeId1;

                    continue;
                }

                if (instr.opcode == ABCOp.checkfilter) {
                    instr.data.checkFilter.stackNodeId = curStackNodeIds[curStackSize - 1];
                    continue;
                }

                // Pop from stack.

                // The pop count was stored in the "single" field in the first pass, so retrive it
                // here and replace it with the node ids.
                int stackPopCount = instr.stackPoppedNodeIds.single;
                instr.stackPoppedNodeIds = default;

                ReadOnlySpan<int> poppedNodeIds = curStackNodeIds.Slice(curStackSize - stackPopCount, stackPopCount);
                m_compilation.setInstructionStackPoppedNodes(ref instr, poppedNodeIds);

                for (int j = 0; j < poppedNodeIds.Length; j++)
                    m_compilation.addDataNodeUse(poppedNodeIds[j], instrDataFlowNodeRef);

                curStackSize -= stackPopCount;

                // Push to stack.
                if (opInfo.stackPushCount == 1) {
                    int pushedStackNodeId = _createDataNode(DataNodeSlotKind.STACK, curStackSize, false);

                    instr.stackPushedNodeId = pushedStackNodeId;
                    m_compilation.addDataNodeDef(pushedStackNodeId, instrDataFlowNodeRef);
                    curStackNodeIds[curStackSize] = pushedStackNodeId;
                    curStackSize++;
                }
                else {
                    instr.stackPushedNodeId = -1;
                }

                // Save any nodes left on the stack after returning because these values
                // have to be popped.
                if (opInfo.controlType == ABCOpInfo.ControlType.RETURN && curStackSize > 0) {
                    var token = m_compilation.staticIntArrayPool.allocate(curStackSize, out Span<int> excessStack);
                    instr.data.returnVoidOrValue.excessStackNodeIds = token;
                    curStackNodeIds.Slice(0, curStackSize).CopyTo(excessStack);
                }

                // Update scope stack.
                if (opInfo.pushesToScopeStack) {
                    int pushedScopeNodeId = _createDataNode(DataNodeSlotKind.SCOPE, curScopeSize, false);

                    instr.data.pushScope.pushedNodeId = pushedScopeNodeId;
                    m_compilation.addDataNodeDef(pushedScopeNodeId, instrDataFlowNodeRef);
                    curScopeNodeIds[curScopeSize] = pushedScopeNodeId;
                    curScopeSize++;
                }
                else if (opInfo.popsFromScopeStack) {
                    curScopeSize--;
                }

                // Capture scope stack for newclass/newfunction.
                if (instr.opcode == ABCOp.newclass || instr.opcode == ABCOp.newfunction) {
                    ref var capturedScopeToken = ref (
                        (instr.opcode == ABCOp.newclass)
                        ? ref instr.data.newClass.capturedScopeNodeIds
                        : ref instr.data.newFunction.capturedScopeNodeIds
                    );

                    capturedScopeToken = m_compilation.staticIntArrayPool.allocate(curScopeSize, out Span<int> capturedScope);
                    curScopeNodeIds.Slice(0, curScopeSize).CopyTo(capturedScope);
                }

                // Update locals.

                if (instr.opcode == ABCOp.hasnext2) {
                    ref var instrData = ref instr.data.hasnext2;

                    int oldNodeId1 = curLocalNodeIds[instrData.localId1];
                    int oldNodeId2 = curLocalNodeIds[instrData.localId2];
                    int newNodeId1 = _createDataNode(DataNodeSlotKind.LOCAL, instrData.localId1, false);
                    int newNodeId2 = _createDataNode(DataNodeSlotKind.LOCAL, instrData.localId2, false);

                    m_compilation.addDataNodeUse(oldNodeId1, instrDataFlowNodeRef);
                    m_compilation.addDataNodeUse(oldNodeId2, instrDataFlowNodeRef);
                    m_compilation.addDataNodeDef(newNodeId1, instrDataFlowNodeRef);
                    m_compilation.addDataNodeDef(newNodeId2, instrDataFlowNodeRef);

                    curLocalNodeIds[instrData.localId1] = newNodeId1;
                    curLocalNodeIds[instrData.localId2] = newNodeId2;

                    instrData.nodeIds = staticIntArrayPool.allocate(4, out Span<int> instrNodeIds);
                    instrNodeIds[0] = oldNodeId1;
                    instrNodeIds[1] = oldNodeId2;
                    instrNodeIds[2] = newNodeId1;
                    instrNodeIds[3] = newNodeId2;

                    if (block.excHandlerId != -1) {
                        ref var tryRegionLocalSet = ref m_tryRegionLocalNodeSets[block.excHandlerId];
                        tryRegionLocalSet.add(newNodeId1);
                        tryRegionLocalSet.add(newNodeId2);
                    }
                }
                else {
                    if (opInfo.readsLocal) {
                        int localId = instr.data.getSetLocal.localId;
                        int curNodeId = curLocalNodeIds[localId];

                        instr.data.getSetLocal.nodeId = curNodeId;
                        m_compilation.addDataNodeUse(curNodeId, instrDataFlowNodeRef);
                    }

                    if (opInfo.writesLocal) {
                        int localId = instr.data.getSetLocal.localId;
                        int newLocalNodeId = _createDataNode(DataNodeSlotKind.LOCAL, localId, false);

                        instr.data.getSetLocal.nodeId = curLocalNodeIds[localId];
                        instr.data.getSetLocal.newNodeId = newLocalNodeId;
                        m_compilation.addDataNodeDef(newLocalNodeId, instrDataFlowNodeRef);

                        curLocalNodeIds[localId] = newLocalNodeId;
                        if (block.excHandlerId != -1)
                            m_tryRegionLocalNodeSets[block.excHandlerId].add(newLocalNodeId);
                    }
                }

            }

            var exitStackNodeIds = curStackNodeIds.Slice(0, curStackSize);
            var exitScopeNodeIds = curScopeNodeIds.Slice(0, curScopeSize);

            block.flags |= BasicBlockFlags.VISITED;

            Span<int> exitBlockIds = staticIntArrayPool.getSpan(block.exitBlockIds);

            for (int i = 0; i < exitBlockIds.Length; i++) {
                ref BasicBlock nextBlock = ref m_compilation.getBasicBlock(exitBlockIds[i]);

                _mergeNextBlockEntryState(ref block, exitStackNodeIds, staticIntArrayPool.getSpan(nextBlock.stackAtEntry));
                _mergeNextBlockEntryState(ref block, exitScopeNodeIds, staticIntArrayPool.getSpan(nextBlock.scopeStackAtEntry));
                _mergeNextBlockEntryState(ref block, curLocalNodeIds, staticIntArrayPool.getSpan(nextBlock.localsAtEntry));

                if ((nextBlock.flags & BasicBlockFlags.TOUCHED) == 0) {
                    nextBlock.flags |= BasicBlockFlags.TOUCHED;
                    m_queuedBlockIds.Enqueue(nextBlock.id);
                }
            }

        }

        /// <summary>
        /// Gets the number of stack items popped by the given instruction. This method can be
        /// used with instructions where the number of items popped is dependent on instruction
        /// operands (and <see cref="ABCOpInfo.stackPopCount"/> would not be of any use).
        /// </summary>
        /// <returns>The number of stack items popped by <paramref name="instr"/>.</returns>
        /// <param name="instr">The instruction for which to compute the number of items popped
        /// from the operand stack.</param>
        private int _getInstrStackPopCount(in Instruction instr) {
            int multinameId, popCount;

            switch (instr.opcode) {
                case ABCOp.newarray:
                    return instr.data.newArrOrObj.elementCount;

                case ABCOp.newobject:
                    return checked(instr.data.newArrOrObj.elementCount * 2);

                case ABCOp.call:
                    return instr.data.callOrConstruct.argCount + 2;

                case ABCOp.construct:
                case ABCOp.callmethod:
                case ABCOp.callstatic:
                case ABCOp.constructsuper:
                    return instr.data.callOrConstruct.argCount + 1;

                case ABCOp.callproperty:
                case ABCOp.callproplex:
                case ABCOp.callpropvoid:
                case ABCOp.callsuper:
                case ABCOp.callsupervoid:
                case ABCOp.constructprop:
                    popCount = instr.data.callProperty.argCount + 1;
                    multinameId = instr.data.callProperty.multinameId;
                    break;

                case ABCOp.finddef:
                case ABCOp.findproperty:
                case ABCOp.findpropstrict:
                    popCount = 0;
                    multinameId = instr.data.accessProperty.multinameId;
                    break;

                case ABCOp.deleteproperty:
                case ABCOp.getdescendants:
                case ABCOp.getproperty:
                case ABCOp.getsuper:
                    popCount = 1;
                    multinameId = instr.data.accessProperty.multinameId;
                    break;

                case ABCOp.initproperty:
                case ABCOp.setproperty:
                case ABCOp.setsuper:
                    popCount = 2;
                    multinameId = instr.data.accessProperty.multinameId;
                    break;

                case ABCOp.applytype:
                    return instr.data.applyType.argCount + 1;

                default:
                    return ABCOpInfo.getInfo(instr.opcode).stackPopCount;
            }

            // For opcodes with a multiname operand, the name and/or namespace may also exist on the
            // stack which have to be popped along with the other arguments.

            ABCMultiname multiname = m_compilation.abcFile.resolveMultiname(multinameId);
            if (multiname.hasRuntimeNamespace)
                popCount++;
            if (multiname.hasRuntimeLocalName)
                popCount++;

            return popCount;
        }

        private void _mergeNextBlockEntryState(
            ref BasicBlock exitBlock, ReadOnlySpan<int> exitNodeIds, Span<int> entryNodeIds)
        {
            Debug.Assert(exitNodeIds.Length == entryNodeIds.Length);

            for (int j = 0; j < exitNodeIds.Length; j++) {
                int exitId = exitNodeIds[j], entryId = entryNodeIds[j];
                if (exitId == entryId)
                    continue;

                if (entryId == -1) {
                    entryNodeIds[j] = exitId;
                }
                else {
                    ref DataNode exitNode = ref m_compilation.getDataNode(exitId);
                    ref DataNode entryNode = ref m_compilation.getDataNode(entryId);

                    Debug.Assert(entryNode.isPhi);

                    m_compilation.addDataNodeDef(entryId, DataNodeOrInstrRef.forDataNode(exitId));
                    m_compilation.addDataNodeUse(exitId, DataNodeOrInstrRef.forDataNode(entryId));

                    if (exitBlock.exitPhiSources.isDefault)
                        exitBlock.exitPhiSources = m_compilation.intArrayPool.allocate(0);

                    if ((exitNode.flags & DataNodeFlags.PHI_SOURCE) == 0) {
                        exitNode.flags |= DataNodeFlags.PHI_SOURCE;
                        m_compilation.intArrayPool.append(exitBlock.exitPhiSources, exitId);
                    }
                }
            }
        }

        private void _assignSourcesForCatchPhiLocals() {
            var dataNodes = m_compilation.getDataNodes();
            var excHandlers = m_compilation.getExceptionHandlers();
            var tryLocalNodeSets = m_tryRegionLocalNodeSets.asSpan();

            for (int i = excHandlers.Length - 1; i >= 0; i--) {
                ref PooledIntegerSet localSet = ref tryLocalNodeSets[i];
                ref ExceptionHandler handler = ref excHandlers[i];

                if (handler.parentId != -1) {
                    Debug.Assert(handler.parentId < i);
                    tryLocalNodeSets[handler.parentId].add(localSet);
                }

                ref BasicBlock targetBlock = ref m_compilation.getBasicBlockOfInstruction(handler.catchTargetInstrId);
                Span<int> localsAtEntry = m_compilation.staticIntArrayPool.getSpan(targetBlock.localsAtEntry);

                foreach (int localNodeId in localSet) {
                    ref DataNode node = ref dataNodes[localNodeId];
                    Debug.Assert(node.slot.kind == DataNodeSlotKind.LOCAL);

                    int entryNodeId = localsAtEntry[node.slot.id];
                    Debug.Assert(dataNodes[entryNodeId].isPhi);

                    var nodeRef = DataNodeOrInstrRef.forDataNode(localNodeId);

                    // Check if the phi node at the catch entry already has the node from the try region
                    // as a source before adding it. This can happen when the catch target block is also
                    // reachable through normal control flow, or when two exception handlers have the same
                    // catch target block.
                    var entryNodeSources = m_compilation.getDataNodeDefs(entryNodeId);
                    if (entryNodeSources.Length != 0 && entryNodeSources.IndexOf(nodeRef) != -1)
                        continue;

                    m_compilation.addDataNodeDef(entryNodeId, nodeRef);
                    m_compilation.addDataNodeUse(localNodeId, DataNodeOrInstrRef.forDataNode(entryNodeId));
                }
            }
        }

        private void _releasePooledResources() {
            var cfgNodeRefArrPool = m_compilation.cfgNodeRefArrayPool;

            var stackDefNodes = m_stackDefNodeSets.asSpan();
            var scopeStackDefNodes = m_scopeStackDefNodeSets.asSpan();
            var localDefNodes = m_localDefNodeSets.asSpan();
            var tryRegionLocalSets = m_tryRegionLocalNodeSets.asSpan();

            for (int i = 0; i < stackDefNodes.Length; i++)
                cfgNodeRefArrPool.free(stackDefNodes[i]);

            for (int i = 0; i < scopeStackDefNodes.Length; i++)
                cfgNodeRefArrPool.free(scopeStackDefNodes[i]);

            for (int i = 0; i < localDefNodes.Length; i++)
                cfgNodeRefArrPool.free(localDefNodes[i]);

            for (int i = 0; i < tryRegionLocalSets.Length; i++)
                tryRegionLocalSets[i].free();
        }

    }

}
