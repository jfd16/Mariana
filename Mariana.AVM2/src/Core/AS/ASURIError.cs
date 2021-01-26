using System;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A URIError is thrown when an invalid URI is passed to a global URI handling function.
    /// </summary>
    [AVM2ExportClass(name = "URIError", isDynamic = true)]
    public class ASURIError : ASError {

        /// <summary>
        /// The value of the "length" property of the AS3 URIError class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// Creates a new instance of <see cref="ASURIError"/>.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="id">The error code.</param>
        [AVM2ExportTrait]
        public ASURIError(string message = "", int id = 0) : base(message, id) {
            name = "URIError";
        }

    }

}
