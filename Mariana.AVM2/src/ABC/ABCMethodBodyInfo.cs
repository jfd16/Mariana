using System;
using Mariana.Common;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// Represents a method body in an ABC file, which contains the bytecode of a method.
    /// </summary>
    public sealed class ABCMethodBodyInfo {

        private int m_abcIndex;

        private ABCMethodInfo m_methodInfo;

        private int m_maxStack;

        private int m_initScopeDepth;

        private int m_maxScopeDepth;

        private int m_localCount;

        private byte[] m_code;

        private ABCExceptionInfo[] m_exceptions;

        private ABCTraitInfo[] m_activation;

        /// <summary>
        /// Gets the index of the method body entry in the ABC file.
        /// </summary>
        public int abcIndex => m_abcIndex;

        /// <summary>
        /// Gets the <see cref="ABCMethodInfo"/> instance representing the method that uses
        /// this method body.
        /// </summary>
        public ABCMethodInfo methodInfo => m_methodInfo;

        /// <summary>
        /// Gets the maximum size of the operand stack that can be reached at any point
        /// during the execution of the method body.
        /// </summary>
        public int maxStackSize => m_maxStack;

        /// <summary>
        /// Gets the size of the scope stack at the start of execution of the method body.
        /// </summary>
        public int initScopeDepth => m_initScopeDepth;

        /// <summary>
        /// Gets the maximum size of the scope stack that can be reached at any point
        /// during the execution of the method body.
        /// </summary>
        public int maxScopeDepth => m_maxScopeDepth;

        /// <summary>
        /// Gets the number of local variables that this method body uses.
        /// </summary>
        public int localCount => m_localCount;

        /// <summary>
        /// Gets a read-only byte array view containing the ActionScript 3 bytecode of this method body.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{Byte}"/> containing the bytecode of this method body.</returns>
        public ReadOnlyArrayView<byte> getCode() => new ReadOnlyArrayView<byte>(m_code);

        /// <summary>
        /// Gets a read-only array view containing the <see cref="ABCExceptionInfo"/> instances representing the
        /// exception handling blocks in this method body.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{ABCExceptionInfo}"/> containing the objects
        /// representing the exception handling blocks in this method body. </returns>
        public ReadOnlyArrayView<ABCExceptionInfo> getExceptionInfo() => new ReadOnlyArrayView<ABCExceptionInfo>(m_exceptions);

        /// <summary>
        /// Gets a read-only array view containing the <see cref="ABCTraitInfo"/> instances representing the traits
        /// for this method body's activation object.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{ABCTraitInfo}"/> containing the objects
        /// representing the traits for this method body's activation object. If this method
        /// body does not use an activation object, an empty array is returned.</returns>
        public ReadOnlyArrayView<ABCTraitInfo> getActivationTraits() => new ReadOnlyArrayView<ABCTraitInfo>(m_activation);

        internal ABCMethodBodyInfo(
            int abcIndex,
            ABCMethodInfo methodInfo,
            int maxStack,
            int initScopeDepth,
            int maxScopeDepth,
            int localCount,
            byte[] code,
            ABCExceptionInfo[] exceptions,
            ABCTraitInfo[] activation
        ) {
            m_abcIndex = abcIndex;
            m_methodInfo = methodInfo;
            m_maxStack = maxStack;
            m_initScopeDepth = initScopeDepth;
            m_maxScopeDepth = maxScopeDepth;
            m_localCount = localCount;
            m_code = code;
            m_exceptions = exceptions;
            m_activation = activation;
        }

    }

}
