using System;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// An EvalError is thrown when the Function constructor is called with arguments.
    /// </summary>
    ///
    /// <remarks>
    /// The Function constructor is a feature of JavaScript that allows functions to be generated
    /// at runtime from source code strings, but this is unavailable in AS3. Calling the Function
    /// constructor with no arguments does not throw this error; an empty function is returned
    /// instead.
    /// </remarks>
    [AVM2ExportClass(name = "EvalError", isDynamic = true)]
    public class ASEvalError : ASError {

        /// <summary>
        /// The value of the "length" property of the AS3 EvalError class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// Creates a new instance of EvalError
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="id">The error code.</param>
        [AVM2ExportTrait]
        public ASEvalError(string message = "", int id = 0) : base(message, id) {
            name = "EvalError";
        }

    }

}
