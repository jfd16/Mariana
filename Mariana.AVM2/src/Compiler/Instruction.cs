using System;
using Mariana.AVM2.Core;
using Mariana.AVM2.ABC;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Represents an AVM2 bytecode instruction.
    /// </summary>
    internal struct Instruction {

        /// <summary>
        /// The unique index of this instruction. An <see cref="Instruction"/> instance can be retrieved
        /// from its index using <see cref="MethodCompilation.getInstruction"/>.
        /// </summary>
        public int id;

        /// <summary>
        /// The offset of the instruction in the binary method body.
        /// </summary>
        public int byteOffset;

        /// <summary>
        /// The index of the basic block to which the instruction belongs.
        /// </summary>
        public int blockId;

        /// <summary>
        /// The instruction opcode.
        /// </summary>
        public ABCOp opcode;

        /// <summary>
        /// A bitwise combination of flags from <see cref="InstructionFlags"/> applicable for
        /// this instruction.
        /// </summary>
        public InstructionFlags flags;

        /// <summary>
        /// The node ids of the stack nodes popped by the instruction. If the
        /// <see cref="InstructionFlags.HAS_SINGLE_STACK_POP"/> flag is set then this
        /// contains the node id as a single value, otherwise it contains a token to
        /// an array allocated from the compilation's static integer array pool.
        /// </summary>
        public IntSingleArrayUnion stackPoppedNodeIds;

        /// <summary>
        /// The node id of the stack node pushed by the instruction, or -1 if nothing is pushed.
        /// </summary>
        public int stackPushedNodeId;

        /// <summary>
        /// Any additional instruction data which is opcode specific.
        /// </summary>
        public InstructionData data;

    }

    /// <summary>
    /// An enumeration of bit flags representing attributes of instructions stored in
    /// the <see cref="Instruction.flags"/> field.
    /// </summary>
    [Flags]
    internal enum InstructionFlags : short {

        /// <summary>
        /// The instruction starts a basic block.
        /// </summary>
        STARTS_BASIC_BLOCK = 1,

        /// <summary>
        /// The instruction ends a basic block.
        /// </summary>
        ENDS_BASIC_BLOCK = 2,

        /// <summary>
        /// The instruction pops a single item from the operand stack.
        /// </summary>
        HAS_SINGLE_STACK_POP = 4,

    }

}
