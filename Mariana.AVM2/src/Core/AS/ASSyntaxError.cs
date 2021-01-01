using System;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A SyntaxError is thrown when invalid syntax is passed to the JSON or XML parser, or when
    /// an invalid pattern is passed to the RegExp constructor.
    /// </summary>
    [AVM2ExportClass(name = "SyntaxError", isDynamic = true)]
    public class ASSyntaxError : ASError {

        /// <summary>
        /// The value of the "length" property of the AS3 SyntaxError class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// Creates a new instance of SyntaxError.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="id">The error code.</param>
        [AVM2ExportTrait]
        public ASSyntaxError(string message = "", int id = 0) : base(message, id) {

        }

    }

}