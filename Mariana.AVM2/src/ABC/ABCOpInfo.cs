using System;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// Provides information for AVM2 opcodes such as operand types, control flow and
    /// stack behaviour.
    /// </summary>
    public struct ABCOpInfo {

        /// <summary>
        /// Specifies the kind of immediate data that is used with an instruction in bytecode.
        /// </summary>
        public enum ImmediateType : byte {

            /// <summary>
            /// The instruction does not contain any immediate data.
            /// </summary>
            NONE,

            /// <summary>
            /// The instruction has a single U30 immediate value (a variable-length unsigned integer).
            /// </summary>
            U30,

            /// <summary>
            /// The instruction has two U30 immediate values.
            /// </summary>
            U30_U30,

            /// <summary>
            /// The instruction has a 24-bit signed integer immediate value.
            /// </summary>
            S24,

            /// <summary>
            /// The instruction has a single byte immediate value.
            /// </summary>
            BYTE,

            /// <summary>
            /// The instruction is the <c>debug</c> instruction. This has a special kind of immediate data.
            /// </summary>
            DEBUG,

            /// <summary>
            /// The instruction is the <c>lookupswitch</c> instruction. This has a special kind of immediate data.
            /// </summary>
            SWITCH,

        }

        /// <summary>
        /// Specifies the effect of an instruction on control flow.
        /// </summary>
        public enum ControlType : byte {

            /// <summary>
            /// The instruction has no effect on control flow.
            /// </summary>
            NONE,

            /// <summary>
            /// The instruction is an unconditional branch instruction.
            /// </summary>
            JUMP,

            /// <summary>
            /// The instruction is a conditional branch instruction.
            /// </summary>
            BRANCH,

            /// <summary>
            /// The instruction is the <c>lookupswitch</c> instruction, which is used for
            /// multi-way conditional branching.
            /// </summary>
            SWITCH,

            /// <summary>
            /// The instruction is used to return from a function.
            /// </summary>
            RETURN,

            /// <summary>
            /// The instruction is used to throw an exception.
            /// </summary>
            THROW,

        }

        private const int SCOPE_PUSH_FLAG = 1 << 10;
        private const int SCOPE_POP_FLAG = 1 << 11;
        private const int LOCAL_READ_FLAG = 1 << 12;
        private const int LOCAL_WRITE_FLAG = 1 << 13;
        private const int DEBUG_FLAG = 1 << 14;
        private const int VALID_FLAG = 1 << 15;

        private static readonly ABCOpInfo[] s_opinfo = _initOpInfoTable();

        private static readonly string[] s_names = _initNameTable();

        private short m_data;

        private ABCOpInfo(
            ImmediateType opType = ImmediateType.NONE, ControlType controlType = ControlType.NONE,
            int stackPopCount = 0, int stackPushCount = 0,
            bool pushesToScopeStack = false, bool popsFromScopeStack = false,
            bool localRead = false, bool localWrite = false,
            bool isDebug = false)
        {
            int data = VALID_FLAG;
            data |= (int)opType;
            data |= (int)controlType << 3;

            if (stackPopCount == -1)
                stackPopCount = 3;
            data |= stackPopCount << 6;

            data |= stackPushCount << 8;

            if (pushesToScopeStack)
                data |= SCOPE_PUSH_FLAG;
            if (popsFromScopeStack)
                data |= SCOPE_POP_FLAG;
            if (localRead)
                data |= LOCAL_READ_FLAG;
            if (localWrite)
                data |= LOCAL_WRITE_FLAG;
            if (isDebug)
                data |= DEBUG_FLAG;

            m_data = (short)data;
        }

        /// <summary>
        /// Gets the <see cref="ABCOpInfo"/> instance for the given opcode.
        /// </summary>
        /// <returns>An <see cref="ABCOpInfo"/> instance.</returns>
        /// <param name="opcode">The opcode.</param>
        public static ABCOpInfo getInfo(ABCOp opcode) => s_opinfo[(byte)opcode];

        /// <summary>
        /// Gets the name of the given opcode as a string.
        /// </summary>
        /// <returns>The name of the opcode. If the opcode is not valid, returns null.</returns>
        /// <param name="opcode">The opcode.</param>
        public static string getName(ABCOp opcode) => s_names[(byte)opcode];

        /// <summary>
        /// Gets the kind of immediate data associated with an instruction of this opcode in the bytecode.
        /// </summary>
        public ImmediateType immediateType => (ImmediateType)(m_data & 7);

        /// <summary>
        /// Gets the effect that an instruction of this opcode has on the program's control flow.
        /// </summary>
        public ControlType controlType => (ControlType)((m_data >> 3) & 7);

        /// <summary>
        /// Gets the number of items that an instruction of this opcode pushes onto the stack.
        /// </summary>
        /// <value>The number of stack items pushed.</value>
        public int stackPushCount => (m_data >> 8) & 3;

        /// <summary>
        /// Gets the number of items that an instruction of this opcode pops from the stack.
        /// </summary>
        /// <value>The number of stack items popped. If the number of items popped depend on
        /// the instruction's operands, this value is -1.</value>
        public int stackPopCount {
            get {
                int count = (m_data >> 6) & 3;
                return (count == 3) ? -1 : count;
            }
        }

        /// <summary>
        /// Gets a value indicating whether an instruction of this opcode pushes to the scope stack.
        /// </summary>
        public bool pushesToScopeStack => (m_data & SCOPE_PUSH_FLAG) != 0;

        /// <summary>
        /// Gets a value indicating whether an instruction of this opcode pops from the scope stack.
        /// </summary>
        public bool popsFromScopeStack => (m_data & SCOPE_POP_FLAG) != 0;

        /// <summary>
        /// Gets a value indicating whether an instruction of this opcode reads a local variable.
        /// </summary>
        public bool readsLocal => (m_data & LOCAL_READ_FLAG) != 0;

        /// <summary>
        /// Gets a value indicating whether an instruction of this opcode writes to a local variable.
        /// </summary>
        public bool writesLocal => (m_data & LOCAL_WRITE_FLAG) != 0;

        /// <summary>
        /// Gets a value indicating whether this opcode represents a debugging instruction.
        /// </summary>
        public bool isDebug => (m_data & DEBUG_FLAG) != 0;

        /// <summary>
        /// Gets a value indicating whether this opcode is a valid opcode.
        /// </summary>
        public bool isValid => (m_data & VALID_FLAG) != 0;

        /// <summary>
        /// Gets the number of items popped from the stack by an instruction of the given opcode
        /// with the given operands.
        /// </summary>
        /// <returns>The number of items popped from the stack.</returns>
        /// <param name="opcode">The instruction opcode.</param>
        /// <param name="multinameKind">The type of the multiname operand for the instruction.
        /// If <paramref name="opcode"/> uses a multiname operand, this must be a valid multiname
        /// kind; otherwise, this argument is ignored.</param>
        /// <param name="argCount">The argument count for a call-like instruction. If
        /// <paramref name="opcode"/> does not use an argument count, this argument is ignored.</param>
        public int getStackPopCount(ABCOp opcode, ABCConstKind multinameKind = 0, int argCount = 0) {
            if (argCount < 0)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(argCount));

            ABCOpInfo opInfo = s_opinfo[(byte)opcode];

            if (!opInfo.isValid)
                return -1;

            int popCount;
            bool hasMultiname = false;

            switch (opcode) {
                case ABCOp.newarray:
                    popCount = argCount;
                    break;

                case ABCOp.newobject:
                    popCount = argCount * 2;
                    break;

                case ABCOp.call:
                    popCount = argCount + 2;
                    break;

                case ABCOp.construct:
                case ABCOp.callmethod:
                case ABCOp.callstatic:
                case ABCOp.constructsuper:
                case ABCOp.applytype:
                    popCount = argCount + 1;
                    break;

                case ABCOp.callproperty:
                case ABCOp.callproplex:
                case ABCOp.callpropvoid:
                case ABCOp.callsuper:
                case ABCOp.callsupervoid:
                case ABCOp.constructprop:
                    popCount = argCount + 1;
                    hasMultiname = true;
                    break;

                case ABCOp.finddef:
                case ABCOp.findproperty:
                case ABCOp.findpropstrict:
                    popCount = 0;
                    hasMultiname = true;
                    break;

                case ABCOp.deleteproperty:
                case ABCOp.getdescendants:
                case ABCOp.getproperty:
                case ABCOp.getsuper:
                case ABCOp.@in:
                    popCount = 1;
                    hasMultiname = true;
                    break;

                case ABCOp.initproperty:
                case ABCOp.setproperty:
                case ABCOp.setsuper:
                    popCount = 2;
                    hasMultiname = true;
                    break;

                default:
                    popCount = opInfo.stackPopCount;
                    break;
            }

            if (!hasMultiname)
                return popCount;

            switch (multinameKind) {
                case ABCConstKind.QName:
                case ABCConstKind.QNameA:
                case ABCConstKind.Multiname:
                case ABCConstKind.MultinameA:
                    return popCount;

                case ABCConstKind.RTQName:
                case ABCConstKind.RTQNameA:
                case ABCConstKind.MultinameL:
                case ABCConstKind.MultinameLA:
                    return popCount + 1;

                case ABCConstKind.RTQNameL:
                case ABCConstKind.RTQNameLA:
                    return popCount + 2;

                default:
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(multinameKind));
            }
        }

        private static ABCOpInfo[] _initOpInfoTable() {

            ABCOpInfo[] info = new ABCOpInfo[256];

            info[(int)ABCOp.add] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.add_i] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.applytype] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.astype] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.astypelate] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.bitand] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.bitnot] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.bitor] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.bitxor] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.bkpt] =
                new ABCOpInfo(isDebug: true);

            info[(int)ABCOp.bkptline] =
                new ABCOpInfo(isDebug: true, opType: ImmediateType.U30);

            info[(int)ABCOp.call] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.callmethod] =
                new ABCOpInfo(opType: ImmediateType.U30_U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.callproperty] =
                new ABCOpInfo(opType: ImmediateType.U30_U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.callproplex] =
                new ABCOpInfo(opType: ImmediateType.U30_U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.callpropvoid] =
                new ABCOpInfo(opType: ImmediateType.U30_U30, stackPopCount: -1);

            info[(int)ABCOp.callstatic] =
                new ABCOpInfo(opType: ImmediateType.U30_U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.callsuper] =
                new ABCOpInfo(opType: ImmediateType.U30_U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.callsupervoid] =
                new ABCOpInfo(opType: ImmediateType.U30_U30, stackPopCount: -1);

            info[(int)ABCOp.checkfilter] =
                new ABCOpInfo(stackPopCount: 0, stackPushCount: 0);

            info[(int)ABCOp.coerce] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.coerce_a] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.coerce_b] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.coerce_d] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.coerce_i] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.coerce_o] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.coerce_s] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.coerce_u] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.construct] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.constructprop] =
                new ABCOpInfo(opType: ImmediateType.U30_U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.constructsuper] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1);

            info[(int)ABCOp.convert_b] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.convert_d] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.convert_i] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.convert_o] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.convert_s] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.convert_u] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.debug] =
                new ABCOpInfo(isDebug: true, opType: ImmediateType.DEBUG);

            info[(int)ABCOp.debugfile] =
                new ABCOpInfo(isDebug: true, opType: ImmediateType.U30);

            info[(int)ABCOp.debugline] =
                new ABCOpInfo(isDebug: true, opType: ImmediateType.U30);

            info[(int)ABCOp.declocal] =
                new ABCOpInfo(opType: ImmediateType.U30, localRead: true, localWrite: true);

            info[(int)ABCOp.declocal_i] =
                new ABCOpInfo(opType: ImmediateType.U30, localRead: true, localWrite: true);

            info[(int)ABCOp.decrement] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.decrement_i] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.deleteproperty] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.divide] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.dup] =
                new ABCOpInfo(stackPopCount: 0, stackPushCount: 1);

            info[(int)ABCOp.dxns] =
                new ABCOpInfo(opType: ImmediateType.U30);

            info[(int)ABCOp.dxnslate] =
                new ABCOpInfo(stackPopCount: 1);

            info[(int)ABCOp.equals] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.esc_xattr] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.esc_xelem] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.finddef] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.findproperty] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.findpropstrict] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.getdescendants] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.getglobalscope] =
                new ABCOpInfo(stackPushCount: 1);

            info[(int)ABCOp.getglobalslot] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPushCount: 1);

            info[(int)ABCOp.getlex] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPushCount: 1);

            info[(int)ABCOp.getlocal] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPushCount: 1, localRead: true);

            info[(int)ABCOp.getlocal0] =
                new ABCOpInfo(stackPushCount: 1, localRead: true);

            info[(int)ABCOp.getlocal1] =
                new ABCOpInfo(stackPushCount: 1, localRead: true);

            info[(int)ABCOp.getlocal2] =
                new ABCOpInfo(stackPushCount: 1, localRead: true);

            info[(int)ABCOp.getlocal3] =
                new ABCOpInfo(stackPushCount: 1, localRead: true);

            info[(int)ABCOp.getproperty] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.getscopeobject] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPushCount: 1);

            info[(int)ABCOp.getslot] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.getsuper] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.greaterequals] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.greaterthan] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.hasnext] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.hasnext2] =
                new ABCOpInfo(opType: ImmediateType.U30_U30, stackPushCount: 1, localRead: true, localWrite: true);

            info[(int)ABCOp.ifeq] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.BRANCH, stackPopCount: 2);

            info[(int)ABCOp.iffalse] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.BRANCH, stackPopCount: 1);

            info[(int)ABCOp.ifge] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.BRANCH, stackPopCount: 2);

            info[(int)ABCOp.ifgt] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.BRANCH, stackPopCount: 2);

            info[(int)ABCOp.ifle] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.BRANCH, stackPopCount: 2);

            info[(int)ABCOp.iflt] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.BRANCH, stackPopCount: 2);

            info[(int)ABCOp.ifne] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.BRANCH, stackPopCount: 2);

            info[(int)ABCOp.ifnge] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.BRANCH, stackPopCount: 2);

            info[(int)ABCOp.ifngt] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.BRANCH, stackPopCount: 2);

            info[(int)ABCOp.ifnle] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.BRANCH, stackPopCount: 2);

            info[(int)ABCOp.ifnlt] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.BRANCH, stackPopCount: 2);

            info[(int)ABCOp.ifstricteq] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.BRANCH, stackPopCount: 2);

            info[(int)ABCOp.ifstrictne] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.BRANCH, stackPopCount: 2);

            info[(int)ABCOp.iftrue] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.BRANCH, stackPopCount: 1);

            info[(int)ABCOp.@in] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.inclocal] =
                new ABCOpInfo(opType: ImmediateType.U30, localRead: true, localWrite: true);

            info[(int)ABCOp.inclocal_i] =
                new ABCOpInfo(opType: ImmediateType.U30, localRead: true, localWrite: true);

            info[(int)ABCOp.increment] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.increment_i] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.initproperty] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1);

            info[(int)ABCOp.istype] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.istypelate] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.instanceof] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.jump] =
                new ABCOpInfo(opType: ImmediateType.S24, controlType: ControlType.JUMP);

            info[(int)ABCOp.kill] =
                new ABCOpInfo(opType: ImmediateType.U30, localWrite: true);

            info[(int)ABCOp.label] =
                new ABCOpInfo(opType: ImmediateType.NONE);

            info[(int)ABCOp.lessequals] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.lessthan] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.lookupswitch] =
                new ABCOpInfo(opType: ImmediateType.SWITCH, controlType: ControlType.SWITCH, stackPopCount: 1);

            info[(int)ABCOp.lshift] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.modulo] =
                 new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.multiply] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.multiply_i] =
                 new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.negate] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.negate_i] =
                 new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.newactivation] =
                new ABCOpInfo(stackPushCount: 1);

            info[(int)ABCOp.newarray] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.newcatch] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPushCount: 1);

            info[(int)ABCOp.newclass] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.newfunction] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPushCount: 1);

            info[(int)ABCOp.newobject] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1, stackPushCount: 1);

            info[(int)ABCOp.nextname] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.nextvalue] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.nop] =
                new ABCOpInfo(opType: ImmediateType.NONE);

            info[(int)ABCOp.not] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.pop] =
                new ABCOpInfo(stackPopCount: 1);

            info[(int)ABCOp.popscope] =
                new ABCOpInfo(popsFromScopeStack: true);

            info[(int)ABCOp.pushbyte] =
                new ABCOpInfo(opType: ImmediateType.BYTE, stackPushCount: 1);

            info[(int)ABCOp.pushdouble] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPushCount: 1);

            info[(int)ABCOp.pushfalse] =
                new ABCOpInfo(stackPushCount: 1);

            info[(int)ABCOp.pushint] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPushCount: 1);

            info[(int)ABCOp.pushnamespace] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPushCount: 1);

            info[(int)ABCOp.pushnan] =
                new ABCOpInfo(stackPushCount: 1);

            info[(int)ABCOp.pushnull] =
                new ABCOpInfo(stackPushCount: 1);

            info[(int)ABCOp.pushscope] =
                new ABCOpInfo(stackPopCount: 1, pushesToScopeStack: true);

            info[(int)ABCOp.pushshort] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPushCount: 1);

            info[(int)ABCOp.pushstring] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPushCount: 1);

            info[(int)ABCOp.pushtrue] =
                new ABCOpInfo(stackPushCount: 1);

            info[(int)ABCOp.pushuint] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPushCount: 1);

            info[(int)ABCOp.pushundefined] =
                new ABCOpInfo(stackPushCount: 1);

            info[(int)ABCOp.pushwith] =
                new ABCOpInfo(stackPopCount: 1, pushesToScopeStack: true);

            info[(int)ABCOp.returnvalue] =
                new ABCOpInfo(controlType: ControlType.RETURN, stackPopCount: 1);

            info[(int)ABCOp.returnvoid] =
                new ABCOpInfo(controlType: ControlType.RETURN);

            info[(int)ABCOp.rshift] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.setglobalslot] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: 1);

            info[(int)ABCOp.setlocal] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: 1, localWrite: true);

            info[(int)ABCOp.setlocal0] =
                new ABCOpInfo(stackPopCount: 1, localWrite: true);

            info[(int)ABCOp.setlocal1] =
                new ABCOpInfo(stackPopCount: 1, localWrite: true);

            info[(int)ABCOp.setlocal2] =
                new ABCOpInfo(stackPopCount: 1, localWrite: true);

            info[(int)ABCOp.setlocal3] =
                new ABCOpInfo(stackPopCount: 1, localWrite: true);

            info[(int)ABCOp.setproperty] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1);

            info[(int)ABCOp.setslot] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: 2);

            info[(int)ABCOp.setsuper] =
                new ABCOpInfo(opType: ImmediateType.U30, stackPopCount: -1);

            info[(int)ABCOp.strictequals] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.subtract] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.subtract_i] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            info[(int)ABCOp.swap] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 2);

            info[(int)ABCOp.@throw] =
                new ABCOpInfo(stackPopCount: 1, controlType: ControlType.THROW);

            info[(int)ABCOp.timestamp] =
                new ABCOpInfo(isDebug: true);

            info[(int)ABCOp.@typeof] =
                new ABCOpInfo(stackPopCount: 1, stackPushCount: 1);

            info[(int)ABCOp.urshift] =
                new ABCOpInfo(stackPopCount: 2, stackPushCount: 1);

            return info;

        }

        private static string[] _initNameTable() {
            string[] names = new string[256];

            for (int i = 0; i < 256; i++) {
                if (s_opinfo[i].isValid)
                    names[i] = ((ABCOp)i).ToString();
            }

            return names;
        }

    }

}
