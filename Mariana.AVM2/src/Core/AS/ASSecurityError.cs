using System;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A SecurityError is thrown when a security check fails, such as when loading an external
    /// image or SWF from an untrusted source.
    /// </summary>
    [AVM2ExportClass(name = "SecurityError", isDynamic = true)]
    public class ASSecurityError : ASError {

        /// <summary>
        /// The value of the "length" property of the AS3 SecurityError class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// Creates a new instance of SecurityError.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="id">The error code.</param>
        [AVM2ExportTrait]
        public ASSecurityError([ParamDefaultValue("")] ASAny message, int id = 0) : base(message, id) {
            name = "SecurityError";
        }

    }
}
