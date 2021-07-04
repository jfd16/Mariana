using System;
using System.Reflection;

namespace Mariana.CodeGen.IL {

    /// <summary>
    /// Used for tracking stack size changes when emitting method calls.
    /// </summary>
    internal readonly struct MethodStackChangeInfo {

        private readonly ushort m_argsPopped;
        private readonly byte m_thisPopped;
        private readonly byte m_returnPushed;

        /// <summary>
        /// Creates a new instance of <see cref="MethodStackChangeInfo"/>
        /// </summary>
        /// <param name="argsPopped">The number of arguments popped from the stack, excluding the "this"
        /// argument if any.</param>
        /// <param name="popsThis">True if the method pops a "this" argument from the stack when called.</param>
        /// <param name="hasReturn">True if the method pushes a return value onto the stack when called.</param>
        public MethodStackChangeInfo(int argsPopped, bool popsThis, bool hasReturn) {
            m_argsPopped = checked((ushort)argsPopped);
            m_thisPopped = (byte)(popsThis ? 1 : 0);
            m_returnPushed = (byte)(hasReturn ? 1 : 0);
        }

        /// <summary>
        /// Creates a new instance of <see cref="MethodStackChangeInfo"/> from the given method.
        /// </summary>
        /// <param name="method">The <see cref="MethodBase"/> from which to create a <see cref="MethodStackChangeInfo"/>
        /// instance.</param>
        public MethodStackChangeInfo(MethodBase method) : this(
            argsPopped: method.GetParameters().Length,
            popsThis: !method.IsStatic,
            hasReturn: method is MethodInfo mi && mi.ReturnType != typeof(void)
        ) {}

        /// <summary>
        /// Returns the change in the stack size when the method is used as the operand to an
        /// instruction with the opcode <paramref name="opcode"/>.
        /// </summary>
        /// <param name="opcode">The instruction opcode. This must be one of call, callvirt,
        /// or newobj.</param>
        /// <returns>The change in the stack size.</returns>
        public int getStackDelta(ILOp opcode) {
            return opcode switch {
                ILOp.call or ILOp.callvirt => m_returnPushed - m_argsPopped - m_thisPopped,
                ILOp.newobj => 1 - m_argsPopped,
                _ => throw new ArgumentOutOfRangeException(nameof(opcode)),
            };
        }

    }

}
