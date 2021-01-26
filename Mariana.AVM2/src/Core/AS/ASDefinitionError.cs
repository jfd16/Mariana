using System;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A DefinitionError is thrown if an attempt is made to define a definition (class, function
    /// etc.) with a name that already exists.
    /// </summary>
    [AVM2ExportClass(name = "DefinitionError", isDynamic = true)]
    public class ASDefinitionError : ASError {

        /// <summary>
        /// The value of the "length" property of the AS3 DefinitionError class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// Creates a new instance of <see cref="ASDefinitionError"/>.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="id">The error code.</param>
        [AVM2ExportTrait]
        public ASDefinitionError(string message = "", int id = 0) : base(message, id) {
            name = "DefinitionError";
        }

    }

}
