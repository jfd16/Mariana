using System;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// Represents an exception handling block in a method body.
    /// </summary>
    public sealed class ABCExceptionInfo {

        private int m_tryStart;
        private int m_tryEnd;
        private int m_catchTarget;
        private ABCMultiname m_catchTypeName;
        private ABCMultiname m_catchVarName;

        /// <summary>
        /// The offset, in bytes, of the beginning of the try block of this exception handler.
        /// </summary>
        public int tryStart => m_tryStart;

        /// <summary>
        /// The offset, in bytes, of the instruction immediately following the end of the
        /// try block of this exception handler.
        /// </summary>
        public int tryEnd => m_tryEnd;

        /// <summary>
        /// The offset, in bytes, of the instruction to which control must be transferred to
        /// when an exception is caught.
        /// </summary>
        public int catchTarget => m_catchTarget;

        /// <summary>
        /// A multiname representing the name of the type of the exception that this
        /// exception handler can catch.
        /// </summary>
        public ABCMultiname catchTypeName => m_catchTypeName;

        /// <summary>
        /// A multiname representing the name of the variable that an exception caught
        /// by this handler must be assigned to.
        /// </summary>
        public ABCMultiname catchVarName => m_catchVarName;

        internal ABCExceptionInfo(
            int tryStart, int tryEnd, int catchTarget, ABCMultiname catchTypeName, ABCMultiname catchVarName)
        {
            m_tryStart = tryStart;
            m_tryEnd = tryEnd;
            m_catchTarget = catchTarget;
            m_catchTypeName = catchTypeName;
            m_catchVarName = catchVarName;
        }

    }

}
