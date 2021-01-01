using System;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Represents a basic block, which is a node in the control flow graph containing a
    /// sequence of instructions that are always executed sequentially.
    /// </summary>
    internal struct BasicBlock {

        /// <summary>
        /// The index of the basic block. A <see cref="BasicBlock"/> instance can be retrieved
        /// from its index using <see cref="MethodCompilation.getBasicBlock"/>.
        /// </summary>
        public int id;

        /// <summary>
        /// The index of the exception handler whose try region covers this block. If not covered
        /// by any try region, this value is -1.
        /// </summary>
        public int excHandlerId;

        /// <summary>
        /// The index of the first instruction of this block.
        /// </summary>
        public int firstInstrId;

        /// <summary>
        /// The number of instructions in this block.
        /// </summary>
        public int instrCount;

        /// <summary>
        /// The entry points into this basic block.
        /// </summary>
        public DynamicArrayPoolToken<CFGNodeRef> entryPoints;

        /// <summary>
        /// The mode of exit from this basic block.
        /// </summary>
        public BasicBlockExitType exitType;

        /// <summary>
        /// The ids of the basic blocks into which this block exits through normal (non-exception) control flow.
        /// </summary>
        /// <remarks>
        /// If <see cref="exitType"/> is <see cref="BasicBlockExitType.JUMP"/>, this array must have only
        /// one element. If <see cref="exitType"/> is <see cref="BasicBlockExitType.BRANCH"/>, this array
        /// must have two elements, the first one for the true branch and the second for the false branch.
        /// If <see cref="exitType"/> is <see cref="BasicBlockExitType.SWITCH"/>, the first element of this
        /// array is for the default branch and subsequent elements are for the case branches. For any other
        /// value of <see cref="exitType"/>, this array must be empty.
        /// </remarks>
        public StaticArrayPoolToken<int> exitBlockIds;

        /// <summary>
        /// A set of bit flags used by the compiler, e.g. to mark the block as visited or
        /// unreachable. See the <see cref="BasicBlockFlags"/> enumeration for the values used.
        /// </summary>
        public BasicBlockFlags flags;

        /// <summary>
        /// A <see cref="CFGNodeRef"/> representing the node in the control flow graph that
        /// is the immediate dominator of this block.
        /// </summary>
        public CFGNodeRef immediateDominator;

        /// <summary>
        /// The index of this block in the postorder traversal of the control flow graph.
        /// </summary>
        /// <remarks>
        /// If the control flow graph does not contain cycles, a block with a higher
        /// postorder index will always exit into a block with a lower postorder index.
        /// </remarks>
        public int postorderIndex;

        /// <summary>
        /// A token for an array in the compilation's static integer array pool containing
        /// the node ids for the state of the operand stack at the entry to this block.
        /// </summary>
        public StaticArrayPoolToken<int> stackAtEntry;

        /// <summary>
        /// A token for an array in the compilation's static integer array pool containing
        /// the node ids for the state of the scope stack at the entry to this block.
        /// </summary>
        public StaticArrayPoolToken<int> scopeStackAtEntry;

        /// <summary>
        /// A token for an array in the compilation's static integer array pool containing
        /// the node ids for the state of the local variable slots at the entry to this block.
        /// </summary>
        public StaticArrayPoolToken<int> localsAtEntry;

        /// <summary>
        /// A token for an array in the compilation's dynamic integer array pool containing
        /// the node ids for the data nodes that are definitions for phi nodes in successor
        /// blocks.
        /// </summary>
        public DynamicArrayPoolToken<int> exitPhiSources;

    }

    /// <summary>
    /// Specifies the mode of exit from a basic block.
    /// </summary>
    internal enum BasicBlockExitType : byte {

        /// <summary>
        /// The basic block exits by an unconditional jump to another basic block.
        /// </summary>
        JUMP,

        /// <summary>
        /// The basic block exits by a conditional, two-way branch.
        /// </summary>
        BRANCH,

        /// <summary>
        /// The basic block exits by a conditional, multi-way branch (switch).
        /// </summary>
        SWITCH,

        /// <summary>
        /// The basic block exits by returning from the method.
        /// </summary>
        RETURN,

        /// <summary>
        /// The basic block exits by throwing an exception.
        /// </summary>
        THROW,

    }

    /// <summary>
    /// Used to define certain attributes of a basic block, and for tracking the status of basic
    /// blocks during compilation.
    /// </summary>
    [Flags]
    internal enum BasicBlockFlags : short {

        /// <summary>
        /// Indicates that the compiler has visited the block in the current pass.
        /// This flag must be cleared from all reachable blocks at the end of each pass.
        /// </summary>
        VISITED = 1,

        /// <summary>
        /// Indicates that the compiler has "touched" the block in the current pass, i.e. it has
        /// encountered the block as an exit from a visited block but may or may not yet have visited it.
        /// This flag must be cleared from all reachable blocks at the end of each pass.
        /// </summary>
        TOUCHED = 2,

        /// <summary>
        /// Indicates that the block defines new phi nodes at entry.
        /// </summary>
        DEFINES_PHI_NODES = 4,

    }

}
