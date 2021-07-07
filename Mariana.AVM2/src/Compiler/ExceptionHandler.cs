using System;
using Mariana.AVM2.Core;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Represents an exception handler in a method being compiled.
    /// </summary>
    internal struct ExceptionHandler {

        /// <summary>
        /// The index of the exception handler. An exception handler can be retrieved from its
        /// index using <see cref="MethodCompilation.getExceptionHandler"/>.
        /// </summary>
        public int id;

        /// <summary>
        /// The index of the first instruction of the exception handler's protected (try) region.
        /// </summary>
        public int tryStartInstrId;

        /// <summary>
        /// The index of the instruction immediately following the last instruction of the
        /// exception handler's protected (try) region.
        /// </summary>
        public int tryEndInstrId;

        /// <summary>
        /// A set of flags from the <see cref="ExceptionHandlerFlags"/> enumeration used by the compiler to
        /// define certain attributes of this handler and for tracking purposes.
        /// </summary>
        public ExceptionHandlerFlags flags;

        /// <summary>
        /// If this exception handler is nested in another, this is the index of the parent handler.
        /// Otherwise, this value is -1.
        /// </summary>
        public int parentId;

        /// <summary>
        /// The id of the target instruction of the catch clause of this handler.
        /// </summary>
        public int catchTargetInstrId;

        /// <summary>
        /// A token for an array in the compilation's static integer array pool containing
        /// the basic block ids of the target basic blocks for the catch clauses of this
        /// handler and its ancestors.
        /// </summary>
        public StaticArrayPoolToken<int> flattenedCatchTargetBlockIds;

        /// <summary>
        /// The type of the exception handled by this handler.
        /// </summary>
        public Class? errorType;

        /// <summary>
        /// The data node id for the caught exception pushed onto the stack.
        /// </summary>
        public int catchStackNodeId;

    }

    /// <summary>
    /// Used to define certain attributes of an exception handler, and to track the
    /// status of exception handlers during compilation.
    /// </summary>
    [Flags]
    internal enum ExceptionHandlerFlags {

        /// <summary>
        /// Indicates that this exception handler has been visited in the current pass.
        /// This is only used by compilation passes that need to track the visits of
        /// exception handlers.
        /// </summary>
        VISITED = 1,

    }

}
