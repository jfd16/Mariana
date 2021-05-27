using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mariana.AVM2.ABC;
using Mariana.AVM2.Core;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    internal sealed class ControlFlowAssembler {

        private struct _EHRegion {

            private const int EXC_INFO_ID_MASK = 0x7FFFFFFF;
            private const int REACHABLE_FLAG = unchecked((int)0x80000000u);

            public int tryStartInstrId;
            public int tryEndInstrId;
            public int catchInstrId;
            public int parentId;
            private int m_excInfoIdAndFlags;

            public int excInfoId {
                get => m_excInfoIdAndFlags & EXC_INFO_ID_MASK;
                set => m_excInfoIdAndFlags = (m_excInfoIdAndFlags & ~EXC_INFO_ID_MASK) | value;
            }

            public bool isReachable {
                get => (m_excInfoIdAndFlags & REACHABLE_FLAG) != 0;
                set => m_excInfoIdAndFlags = (m_excInfoIdAndFlags & ~REACHABLE_FLAG) | (value ? REACHABLE_FLAG : 0);
            }

            public static int compare(in _EHRegion x, in _EHRegion y) {
                // Safe to use subtraction here as the ids are always positive.
                if (x.tryStartInstrId != y.tryStartInstrId)
                    return x.tryStartInstrId - y.tryStartInstrId;

                if (x.tryEndInstrId != y.tryEndInstrId)
                    return y.tryEndInstrId - x.tryEndInstrId;

                // If the try regions are the same, sort by descending order of the exception info index
                // in the ABC file, so that exception handlers for a common try region are processed
                // in the same order as they are declared.
                // Not using the excInfoId property here to avoid defensive copies.
                return (y.m_excInfoIdAndFlags & EXC_INFO_ID_MASK) - (x.m_excInfoIdAndFlags & EXC_INFO_ID_MASK);
            }

        }

        private MethodCompilation m_compilation;

        private Queue<int> m_queuedBlockIds;

        private DynamicArray<_EHRegion> m_ehRegions;

        private DynamicArray<int> m_tempIntArray;

        public ControlFlowAssembler(MethodCompilation compilation) {
            m_compilation = compilation;
            m_queuedBlockIds = new Queue<int>();
        }

        /// <summary>
        /// Runs the control flow assembler.
        /// </summary>
        public void run() {
            m_queuedBlockIds.Clear();
            m_ehRegions.clear();

            // Set the initial blockId of all instructions to -1.
            var instructions = m_compilation.getInstructions();
            for (int i = 0; i < instructions.Length; i++)
                instructions[i].blockId = -1;

            _constructFlowFromEntryPoint(0);
            _constructExceptionHandlers();
            _assignExceptionHandlersToBasicBlocks();
            _setBasicBlockEntryPoints();
            _clearBasicBlockVisitedFlags();

            _assignBasicBlockPostorderIndices();
            _assignBasicBlockImmediateDominators();
        }

        private void _constructFlowFromEntryPoint(int firstInstrId) {
            var staticIntArrayPool = m_compilation.staticIntArrayPool;

            int curInstrId = firstInstrId;
            int curBlockId = _getTargetBasicBlockId(curInstrId);

            m_queuedBlockIds.Enqueue(curBlockId);

            while (m_queuedBlockIds.Count != 0) {

                curBlockId = m_queuedBlockIds.Dequeue();
                ref BasicBlock curBlock = ref m_compilation.getBasicBlock(curBlockId);

                if ((curBlock.flags & BasicBlockFlags.VISITED) != 0)
                    continue;

                curBlock.flags |= BasicBlockFlags.VISITED;
                curInstrId = curBlock.firstInstrId;

                _readBasicBlockInstructions(ref curBlock, curInstrId);

                ref Instruction lastInstr = ref m_compilation.getInstruction(curBlock.firstInstrId + curBlock.instrCount - 1);
                int nextBlockInstrId = lastInstr.id + 1;

                lastInstr.flags |= InstructionFlags.ENDS_BASIC_BLOCK;

                ABCOpInfo opInfo = ABCOpInfo.getInfo(lastInstr.opcode);

                if (!opInfo.isValid || opInfo.controlType == ABCOpInfo.ControlType.NONE) {
                    int nextBlockId = _getTargetBasicBlockId(nextBlockInstrId);

                    // Creating a new block may invaildate the existing reference to curBlock, so get it again.
                    curBlock = ref m_compilation.getBasicBlock(curBlockId);
                    curBlock.exitType = BasicBlockExitType.JUMP;
                    curBlock.exitBlockIds = staticIntArrayPool.allocate(1, out Span<int> exitBlockIds);
                    exitBlockIds[0] = nextBlockId;

                    if ((m_compilation.getBasicBlock(nextBlockId).flags & BasicBlockFlags.VISITED) == 0)
                        m_queuedBlockIds.Enqueue(nextBlockId);
                }
                else if (opInfo.controlType == ABCOpInfo.ControlType.JUMP
                    || opInfo.controlType == ABCOpInfo.ControlType.BRANCH)
                {
                    int targetInstrId = _resolveTargetOffset(nextBlockInstrId, lastInstr.data.jump.targetOffset);
                    if (targetInstrId == -1)
                        throw m_compilation.createError(ErrorCode.INVALID_BRANCH_TARGET, lastInstr.id);

                    bool isBranch = opInfo.controlType == ABCOpInfo.ControlType.BRANCH;
                    int nextBlockId = -1;

                    if (isBranch) {
                        if (nextBlockInstrId == m_compilation.getInstructions().Length)
                            throw m_compilation.createError(ErrorCode.CODE_FALLOFF_END_OF_METHOD, -1);
                        nextBlockId = _getTargetBasicBlockId(nextBlockInstrId);
                    }

                    int targetBlockId = _getTargetBasicBlockId(targetInstrId);

                    // The basic block containing the ending instruction may have changed because
                    // the jump/branch target instruction may be inside the block itself (leading to
                    // a split). So we need to get the block id again.

                    curBlock = ref m_compilation.getBasicBlockOfInstruction(lastInstr);
                    curBlockId = curBlock.id;

                    if (isBranch) {
                        curBlock.exitType = BasicBlockExitType.BRANCH;
                        curBlock.exitBlockIds = staticIntArrayPool.allocate(2, out Span<int> exitBlocksIds);
                        exitBlocksIds[0] = targetBlockId;
                        exitBlocksIds[1] = nextBlockId;

                        if ((m_compilation.getBasicBlock(nextBlockId).flags & BasicBlockFlags.VISITED) == 0)
                            m_queuedBlockIds.Enqueue(nextBlockId);
                    }
                    else {
                        curBlock.exitType = BasicBlockExitType.JUMP;
                        curBlock.exitBlockIds = staticIntArrayPool.allocate(1, out Span<int> exitBlockIds);
                        exitBlockIds[0] = targetBlockId;
                    }

                    if ((m_compilation.getBasicBlock(targetBlockId).flags & BasicBlockFlags.VISITED) == 0)
                        m_queuedBlockIds.Enqueue(targetBlockId);
                }
                else if (opInfo.controlType == ABCOpInfo.ControlType.SWITCH) {
                    int caseCount = lastInstr.data.@switch.caseCount;
                    Span<int> caseOffsets = staticIntArrayPool.getSpan(lastInstr.data.@switch.caseOffsets);

                    curBlock.exitType = BasicBlockExitType.SWITCH;
                    curBlock.exitBlockIds = staticIntArrayPool.allocate(caseCount + 1, out Span<int> exitBlockIds);

                    for (int i = 0; i < exitBlockIds.Length; i++) {
                        // The base instruction for a switch case offset is the switch instruction
                        // itself, not the instruction after it as is the case for other branch instructions.

                        int targetInstrId = _resolveTargetOffset(lastInstr.id, caseOffsets[i]);
                        if (targetInstrId == -1)
                            throw m_compilation.createError(ErrorCode.INVALID_BRANCH_TARGET, lastInstr.id);

                        int targetBlockId = _getTargetBasicBlockId(targetInstrId);
                        exitBlockIds[i] = targetBlockId;

                        if ((m_compilation.getBasicBlock(targetBlockId).flags & BasicBlockFlags.VISITED) == 0)
                            m_queuedBlockIds.Enqueue(targetBlockId);
                    }
                }
                else if (opInfo.controlType == ABCOpInfo.ControlType.RETURN) {
                    curBlock.exitType = BasicBlockExitType.RETURN;
                }
                else if (opInfo.controlType == ABCOpInfo.ControlType.THROW) {
                    curBlock.exitType = BasicBlockExitType.THROW;
                }

            }
        }

        private void _readBasicBlockInstructions(ref BasicBlock block, int startInstrId) {
            Span<Instruction> instructions = m_compilation.getInstructions();
            int curInstrId = startInstrId;

            while (true) {
                if (curInstrId == instructions.Length)
                    throw m_compilation.createError(ErrorCode.CODE_FALLOFF_END_OF_METHOD, -1);

                ref Instruction instr = ref instructions[curInstrId];
                instr.blockId = block.id;

                var opInfo = ABCOpInfo.getInfo(instr.opcode);
                var controlType = opInfo.isValid ? opInfo.controlType : ABCOpInfo.ControlType.NONE;

                curInstrId++;

                if (controlType != ABCOpInfo.ControlType.NONE)
                    break;

                if ((uint)curInstrId < (uint)instructions.Length
                    && (instructions[curInstrId].flags & InstructionFlags.STARTS_BASIC_BLOCK) != 0)
                {
                    break;
                }
            }

            block.instrCount = curInstrId - block.firstInstrId;
        }

        /// <summary>
        /// Returns the index of the instruction at the given byte offset from a base instruction.
        /// </summary>
        /// <param name="baseInstrId">The index of the base instruction, or one greater than
        /// the index of the last instruction to compute the offset from the end of the method
        /// body.</param>
        /// <param name="offset">The offset in bytes from the base instruction.</param>
        /// <returns>The index of the instruction at the given byte offset from the base
        /// instruction. If the byte at that offset is not the first byte of an instruction
        /// or the offset is out of the bounds of the method body, returns -1.</returns>
        private int _resolveTargetOffset(int baseInstrId, int offset) {
            int bytecodeSize = m_compilation.methodBodyInfo.getCode().length;
            Span<Instruction> instructions = m_compilation.getInstructions();

            int absoluteOffset;
            if (baseInstrId == instructions.Length)
                absoluteOffset = bytecodeSize + offset;
            else
                absoluteOffset = instructions[baseInstrId].byteOffset + offset;

            if ((uint)absoluteOffset >= (uint)bytecodeSize)
                throw m_compilation.createError(ErrorCode.CODE_FALLOFF_END_OF_METHOD, -1);

            if (offset == 0)
                // A jump of zero bytes is the same as no jump. Return the base instruction index.
                return baseInstrId;

            // Binary search to find the target instruction.

            int low, high;
            if (offset < 0) {
                low = 0;
                high = baseInstrId - 1;
            }
            else {
                low = baseInstrId;
                high = instructions.Length - 1;
            }

            while (low < high) {
                int mid = low + ((high - low) >> 1);
                int midOffset = instructions[mid].byteOffset;

                if (midOffset == absoluteOffset)
                    // Target found
                    return mid;

                if (midOffset < absoluteOffset)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            if (low != high || instructions[low].byteOffset != absoluteOffset)
                // Jump to the middle of an instruction
                return -1;

            // Target instruction found
            return low;
        }

        /// <summary>
        /// Returns the id of the basic block that starts with the instruction whose id is given,
        /// or creates a new block if that instruction does not start an existing basic block.
        /// </summary>
        /// <param name="instrId">The id of the instruction.</param>
        /// <returns>The id of the basic block (created or existing) that starts with the
        /// instruction given by <paramref name="instrId"/>.</returns>
        /// <remarks>
        /// This method may create a new basic block, which may invalidate any references to
        /// existing blocks obtained from methods such as <see cref="MethodCompilation.getBasicBlock"/>.
        /// </remarks>
        private int _getTargetBasicBlockId(int instrId) {
            ref Instruction instr = ref m_compilation.getInstruction(instrId);

            if ((instr.flags & InstructionFlags.STARTS_BASIC_BLOCK) != 0)
                return instr.blockId;

            ref BasicBlock newBlock = ref m_compilation.addBasicBlock();
            newBlock.firstInstrId = instrId;
            newBlock.excHandlerId = -1;

            instr.flags |= InstructionFlags.STARTS_BASIC_BLOCK;

            if (instr.blockId == -1) {
                instr.blockId = newBlock.id;
                return newBlock.id;
            }

            // If the instruction is in the middle of a basic block that has already
            // been visited, the block needs to be split.

            ref BasicBlock currentBlock = ref m_compilation.getBasicBlock(instr.blockId);
            newBlock.instrCount = currentBlock.firstInstrId + currentBlock.instrCount - newBlock.firstInstrId;
            currentBlock.instrCount = instrId - currentBlock.firstInstrId;

            Span<Instruction> newBlockInstructions = m_compilation.getInstructionsInBasicBlock(newBlock);
            for (int i = 0; i < newBlockInstructions.Length; i++)
                newBlockInstructions[i].blockId = newBlock.id;

            newBlock.flags |= BasicBlockFlags.VISITED;
            newBlock.exitType = currentBlock.exitType;
            newBlock.exitBlockIds = currentBlock.exitBlockIds;

            currentBlock.exitType = BasicBlockExitType.JUMP;
            currentBlock.exitBlockIds = m_compilation.staticIntArrayPool.allocate(1, out Span<int> exitBlockIds);
            exitBlockIds[0] = newBlock.id;

            m_compilation.getInstruction(instrId - 1).flags |= InstructionFlags.ENDS_BASIC_BLOCK;

            return newBlock.id;
        }

        private void _constructExceptionHandlers() {
            if (m_compilation.methodBodyInfo.getExceptionInfo().length == 0)
                return;

            _constructEHRegions();
            _constructControlFlowForCatchClauses();

            for (int i = 0; i < m_ehRegions.length; i++)
                _fixEHRegionTryBounds(ref m_ehRegions[i]);

            DataStructureUtil.sortSpan(m_ehRegions.asSpan(), _EHRegion.compare);

            _fixEHRegionOverlapsAndAssignParents();
            _createExceptionHandlersFromEHRegions();
        }

        private void _constructEHRegions() {
            var excInfos = m_compilation.methodBodyInfo.getExceptionInfo();
            int bytecodeSize = m_compilation.methodBodyInfo.getCode().length;

            for (int i = 0; i < excInfos.length; i++) {
                ABCExceptionInfo excInfo = excInfos[i];
                if (excInfo.tryStart >= bytecodeSize || excInfo.tryEnd > bytecodeSize || excInfo.catchTarget >= bytecodeSize)
                    throw m_compilation.createError(ErrorCode.ILLEGAL_EXCEPTION_TABLE, -1);

                var region = new _EHRegion {
                    excInfoId = i,
                    parentId = -1,
                    tryStartInstrId = _resolveTargetOffset(0, excInfo.tryStart),
                    tryEndInstrId = (excInfo.tryEnd == bytecodeSize)
                        ? m_compilation.getInstructions().Length
                        : _resolveTargetOffset(0, excInfo.tryEnd),
                };

                if (region.tryStartInstrId == -1 || region.tryEndInstrId == -1
                    || region.tryStartInstrId > region.tryEndInstrId)
                {
                    throw m_compilation.createError(ErrorCode.ILLEGAL_EXCEPTION_TABLE, -1);
                }

                if (region.tryStartInstrId == region.tryEndInstrId) {
                    // Empty try block is useless.
                    continue;
                }

                region.catchInstrId = _resolveTargetOffset(0, excInfo.catchTarget);
                if (region.catchInstrId == -1)
                    throw m_compilation.createError(ErrorCode.ILLEGAL_EXCEPTION_TABLE, -1);

                m_ehRegions.add(in region);
            }
        }

        private void _constructControlFlowForCatchClauses() {
            Span<_EHRegion> ehRegions = m_ehRegions.asSpan();
            bool newCatchClausesAdded = true;

            // We need to construct the flow graph for the catch clauses of all try blocks
            // where at least one instruction is reachable. As catch clauses themselves may
            // contain additional try-blocks, we need to keep looping over the try blocks
            // list until a pass does not uncover any new try blocks.

            while (newCatchClausesAdded) {
                newCatchClausesAdded = false;

                for (int i = 0; i < ehRegions.Length; i++) {
                    ref _EHRegion region = ref ehRegions[i];
                    if (region.isReachable || !_isEHRegionReachable(ref region))
                        continue;

                    region.isReachable = true;
                    newCatchClausesAdded = true;
                    _constructFlowFromEntryPoint(region.catchInstrId);
                }
            }

            // Remove unreachable regions.

            int reachableCount = 0;
            for (int i = 0; i < ehRegions.Length; i++) {
                if (!ehRegions[i].isReachable)
                    continue;
                if (i != reachableCount)
                    ehRegions[reachableCount] = ehRegions[i];
                reachableCount++;
            }
            m_ehRegions.removeRange(reachableCount, m_ehRegions.length - reachableCount);
        }

        /// <summary>
        /// Returns a value indicating whether a try-region for an exception handler contains at least
        /// one reachable instruction.
        /// </summary>
        /// <param name="region">An <see cref="_EHRegion"/> instance.</param>
        /// <returns>True if the try region contains at least one reachable instruction, otherwise
        /// false.</returns>
        private bool _isEHRegionReachable(ref _EHRegion region) {
            Span<Instruction> instructions = m_compilation.getInstructions();
            var regionInstructions = instructions.Slice(region.tryStartInstrId, region.tryEndInstrId - region.tryStartInstrId);

            for (int i = 0; i < regionInstructions.Length; i++) {
                if (regionInstructions[i].blockId != -1)
                    return true;
            }

            return false;
        }

        private void _fixEHRegionOverlapsAndAssignParents() {
            int curIndex = 0, nextIndex = 1;
            bool containsSplit = false;

            while (nextIndex < m_ehRegions.length) {
                ref _EHRegion curRegion = ref m_ehRegions[curIndex];
                ref _EHRegion nextRegion = ref m_ehRegions[nextIndex];

                if (nextRegion.tryStartInstrId >= curRegion.tryEndInstrId) {
                    if (curRegion.parentId != -1) {
                        curIndex = curRegion.parentId;
                    }
                    else {
                        // Try regions are disjoint.
                        curIndex = nextIndex;
                        nextIndex++;
                    }
                }
                else if (nextRegion.tryEndInstrId <= curRegion.tryEndInstrId) {
                    // One is nested within the other.
                    nextRegion.parentId = curIndex;
                    curIndex = nextIndex;
                    nextIndex++;
                }
                else {
                    // This case is a bit tricky. Here, two try regions overlap but one is not
                    // contained in the other. To fix such handlers, the try region must be split.

                    var splitRegion = new _EHRegion {
                        tryStartInstrId = curRegion.tryEndInstrId,
                        tryEndInstrId = nextRegion.tryEndInstrId,
                        excInfoId = nextRegion.excInfoId,
                        parentId = -1,
                    };
                    m_ehRegions.add(in splitRegion);
                    nextRegion.tryEndInstrId = curRegion.tryEndInstrId;

                    // Move the new split-off EHRegion to its correct sorted position.
                    int splitIndex = m_ehRegions.length - 1;
                    while (true) {
                        ref _EHRegion x = ref m_ehRegions[splitIndex - 1];
                        ref _EHRegion y = ref m_ehRegions[splitIndex];

                        if (_EHRegion.compare(x, y) < 0)
                            break;

                        _EHRegion t = x;
                        x = y;
                        y = t;
                        splitIndex--;
                    }

                    containsSplit = true;
                }
            }

            if (containsSplit) {
                for (int i = 0; i < m_ehRegions.length; i++)
                    _fixEHRegionTryBounds(ref m_ehRegions[i]);
            }
        }

        /// <summary>
        /// Adjusts the try region of an exception handler so that it starts and ends with
        /// reachable instructions.
        /// </summary>
        /// <param name="region">A reference to an <see cref="_EHRegion"/> instance.</param>
        /// <returns>True if there is any change to the try region, otherwise false.</returns>
        private bool _fixEHRegionTryBounds(ref _EHRegion region) {
            int tryStart = region.tryStartInstrId;
            int tryEnd = region.tryEndInstrId;

            while (tryEnd > tryStart && m_compilation.getInstruction(tryEnd - 1).blockId == -1)
                tryEnd--;

            while (tryStart < tryEnd && m_compilation.getInstruction(tryStart).blockId == -1)
                tryStart++;

            if (tryStart == region.tryStartInstrId && tryEnd == region.tryEndInstrId)
                return false;

            region.tryStartInstrId = tryStart;
            region.tryEndInstrId = tryEnd;
            return true;
        }

        private void _createExceptionHandlersFromEHRegions() {
            var excInfos = m_compilation.methodBodyInfo.getExceptionInfo();

            using (var lockedContext = m_compilation.getContext()) {
                for (int i = 0; i < m_ehRegions.length; i++) {
                    ref _EHRegion ehRegion = ref m_ehRegions[i];

                    ref ExceptionHandler handler = ref m_compilation.addExceptionHandler();
                    handler.tryStartInstrId = ehRegion.tryStartInstrId;
                    handler.tryEndInstrId = ehRegion.tryEndInstrId;
                    handler.catchTargetInstrId = ehRegion.catchInstrId;
                    handler.parentId = ehRegion.parentId;

                    ABCExceptionInfo excInfo = excInfos[ehRegion.excInfoId];
                    handler.errorType = lockedContext.value.getClassByMultiname(excInfo.catchTypeName, allowAny: true);
                }
            }

            var handlers = m_compilation.getExceptionHandlers();

            for (int i = 0; i < handlers.Length; i++) {
                int depth = 0;
                int curHandlerId = i;
                while (curHandlerId != -1) {
                    depth++;
                    curHandlerId = handlers[curHandlerId].parentId;
                }

                handlers[i].flattenedCatchTargetBlockIds = m_compilation.staticIntArrayPool.allocate(depth, out Span<int> blockIds);

                depth = 0;
                curHandlerId = i;
                while (curHandlerId != -1) {
                    blockIds[depth] = m_compilation.getInstruction(handlers[curHandlerId].catchTargetInstrId).blockId;
                    depth++;
                    curHandlerId = handlers[curHandlerId].parentId;
                }
            }
        }

        private void _assignExceptionHandlersToBasicBlocks() {
            var handlers = m_compilation.getExceptionHandlers();
            var instructions = m_compilation.getInstructions();

            // Partition the basic blocks so that every block is covered by at most one
            // exception handler's try-region.
            for (int i = 0; i < handlers.Length; i++) {
                ref ExceptionHandler eh = ref handlers[i];

                if (eh.tryStartInstrId == eh.tryEndInstrId)
                    continue;

                if (eh.tryStartInstrId < instructions.Length) {
                    Debug.Assert(instructions[eh.tryStartInstrId].blockId != -1);
                    _getTargetBasicBlockId(eh.tryStartInstrId);
                }

                if (eh.tryEndInstrId < instructions.Length && instructions[eh.tryEndInstrId].blockId != -1)
                    _getTargetBasicBlockId(eh.tryEndInstrId);
            }

            var basicBlocks = m_compilation.getBasicBlocks();

            // The assignment procedure followed requires the basic blocks to be sorted
            // by their instruction ids.
            var blockLinearOrdering = m_tempIntArray.clearAndAddUninitialized(basicBlocks.Length);
            DataStructureUtil.getSpanSortPermutation(
                basicBlocks,
                blockLinearOrdering,
                (in BasicBlock x, in BasicBlock y) => x.firstInstrId - y.firstInstrId
            );

            // Assign an exception handler to each basic block.

            int curHandlerId = -1, nextHandlerId = 0;

            for (int i = 0; i < blockLinearOrdering.Length; i++) {
                ref BasicBlock curBlock = ref basicBlocks[blockLinearOrdering[i]];

                while (curHandlerId != -1 && curBlock.firstInstrId >= handlers[curHandlerId].tryEndInstrId)
                    curHandlerId = handlers[curHandlerId].parentId;

                // This loop ensures that the innermost handler is selected in case of
                // handlers with nested try regions.
                while (nextHandlerId < handlers.Length
                    && handlers[nextHandlerId].tryStartInstrId == curBlock.firstInstrId)
                {
                    curHandlerId = nextHandlerId;
                    nextHandlerId++;
                }

                curBlock.excHandlerId = curHandlerId;
            }
        }

        /// <summary>
        /// Sets the entry points for all basic blocks in the compilation based
        /// on the current flow graph.
        /// </summary>
        private void _setBasicBlockEntryPoints() {
            var blocks = m_compilation.getBasicBlocks();
            var excHandlers = m_compilation.getExceptionHandlers();
            var nodeRefArrayPool = m_compilation.cfgNodeRefArrayPool;

            for (int i = 0; i < blocks.Length; i++)
                blocks[i].entryPoints = nodeRefArrayPool.allocate(0);

            // Start of method
            nodeRefArrayPool.append(m_compilation.getBasicBlockOfInstruction(0).entryPoints, CFGNodeRef.start);

            // Exception catch targets
            for (int i = 0; i < excHandlers.Length; i++) {
                ref BasicBlock handlerBlock = ref m_compilation.getBasicBlockOfInstruction(excHandlers[i].catchTargetInstrId);
                nodeRefArrayPool.append(handlerBlock.entryPoints, CFGNodeRef.forCatch(i));
            }

            // Control transfers between blocks (based on exit ids)
            for (int i = 0; i < blocks.Length; i++) {
                var exitIds = m_compilation.staticIntArrayPool.getSpan(blocks[i].exitBlockIds);
                for (int j = 0; j < exitIds.Length; j++)
                    nodeRefArrayPool.append(blocks[exitIds[j]].entryPoints, CFGNodeRef.forBasicBlock(i));
            }
        }

        /// <summary>
        /// Clears the "visited" flag from all basic blocks.
        /// </summary>
        private void _clearBasicBlockVisitedFlags() {
            var blocks = m_compilation.getBasicBlocks();
            for (int i = 0; i < blocks.Length; i++)
                blocks[i].flags &= ~BasicBlockFlags.VISITED;
        }

        private void _assignBasicBlockPostorderIndices() {
            ref var stack = ref m_tempIntArray;
            stack.clear();

            // These special values are used for postOrderIndex to track blocks during traversal.
            const int NOT_VISITED = -1, VISITED = -2;

            var blocks = m_compilation.getBasicBlocks();
            var excHandlers = m_compilation.getExceptionHandlers();
            var staticIntArrayPool = m_compilation.staticIntArrayPool;

            int curPostorderIndex = 0;

            for (int i = 0; i < blocks.Length; i++)
                blocks[i].postorderIndex = NOT_VISITED;

            stack.add(m_compilation.getInstruction(0).blockId);

            void pushChildren(ref DynamicArray<int> _stack, Span<BasicBlock> _blocks, ReadOnlySpan<int> childIds) {
                for (int i = 0; i < childIds.Length; i++) {
                    ref BasicBlock nextBlock = ref _blocks[childIds[i]];
                    if (nextBlock.postorderIndex == NOT_VISITED)
                        _stack.add(nextBlock.id);
                }
            }

            while (stack.length > 0) {
                ref BasicBlock block = ref blocks[stack[stack.length - 1]];

                if (block.postorderIndex == NOT_VISITED) {
                    // Push the block's successors.
                    // If this block is in a try region, its associated catch block(s) should
                    // also be considered as successors.

                    if (block.excHandlerId != -1) {
                        var catchTargetBlockIds = staticIntArrayPool.getSpan(excHandlers[block.excHandlerId].flattenedCatchTargetBlockIds);
                        pushChildren(ref stack, blocks, catchTargetBlockIds);
                    }

                    pushChildren(ref stack, blocks, staticIntArrayPool.getSpan(block.exitBlockIds));
                    block.postorderIndex = VISITED;
                }
                else {
                    stack.removeLast();

                    if (block.postorderIndex == VISITED) {
                        block.postorderIndex = curPostorderIndex;
                        curPostorderIndex++;
                    }
                }
            }

            Debug.Assert(curPostorderIndex == blocks.Length);
        }

        private void _assignBasicBlockImmediateDominators() {
            // Immediate dominators are computed using the algorithm
            // described in https://www.cs.rice.edu/~keith/EMBED/dom.pdf, with some
            // modifications to accommodate exception handlers ("catch" nodes).

            // We don't have to explicitly compute the IDOMs of catch nodes, because
            // they are by definition the virtual "start" node (whch is also the IDOM
            // of the first basic block)

            var blocks = m_compilation.getBasicBlocks();
            var cfgNodeRefArrayPool = m_compilation.cfgNodeRefArrayPool;

            ReadOnlySpan<int> rpo = m_compilation.getBasicBlockReversePostorder();
            bool hasChanges = true;

            while (hasChanges) {
                hasChanges = false;

                for (int i = 0; i < rpo.Length; i++) {
                    ref BasicBlock block = ref blocks[rpo[i]];
                    ReadOnlySpan<CFGNodeRef> entryPoints = cfgNodeRefArrayPool.getSpan(block.entryPoints);

                    CFGNodeRef idom = entryPoints[0];

                    for (int j = 1; j < entryPoints.Length; j++) {
                        CFGNodeRef node = entryPoints[j];

                        if (node.isStart) {
                            idom = CFGNodeRef.start;
                            continue;
                        }

                        if (node.isBlock && (blocks[node.id].flags & BasicBlockFlags.VISITED) == 0)
                            continue;

                        CFGNodeRef node2 = idom;
                        while (node != node2) {
                            while (_compareNodesPostOrder(node, node2) < 0)
                                node = node.isBlock ? blocks[node.id].immediateDominator : CFGNodeRef.start;

                            while (_compareNodesPostOrder(node, node2) > 0)
                                node2 = node2.isBlock ? blocks[node2.id].immediateDominator : CFGNodeRef.start;
                        }

                        idom = node;
                    }

                    if ((block.flags & BasicBlockFlags.VISITED) == 0 || block.immediateDominator != idom) {
                        block.flags |= BasicBlockFlags.VISITED;
                        block.immediateDominator = idom;
                        hasChanges = true;
                    }
                }
            }

            _clearBasicBlockVisitedFlags();
        }

        /// <summary>
        /// Compares two control flow nodes based on their visit order in a postorder traversal
        /// of the control flow graph.
        /// </summary>
        /// <returns>Zero if the two nodes are the same, a negative value if the first node is
        /// visited before the second, or a positive value if the first node is visited after the
        /// second one.</returns>
        /// <param name="node1">A <see cref="CFGNodeRef"/> instance referring to the first node.</param>
        /// <param name="node2">A <see cref="CFGNodeRef"/> instance referring to the second node.</param>
        private int _compareNodesPostOrder(CFGNodeRef node1, CFGNodeRef node2) {
            CFGNodeRefType t1 = node1.type, t2 = node2.type;

            if (t1 != t2)
                // This works because of how the values in CFGNodeRefType are defined.
                return (int)t1 - (int)t2;

            if (t1 == CFGNodeRefType.START)
                return 0;

            int index1, index2;
            if (t1 == CFGNodeRefType.BLOCK) {
                index1 = m_compilation.getBasicBlock(node1.id).postorderIndex;
                index2 = m_compilation.getBasicBlock(node2.id).postorderIndex;
            }
            else {
                index1 = node1.id;
                index2 = node2.id;
            }

            Debug.Assert(index1 >= 0 && index2 >= 0);
            return index1 - index2;
        }

    }

}
