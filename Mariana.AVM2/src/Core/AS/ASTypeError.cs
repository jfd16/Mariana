using System;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A TypeError is thrown when a type conversion fails, the type of an argument passed to a
    /// method is not accepted by that method, when operations that require a non-null object
    /// (such as accessing a property) are performed on a null or undefined object, or during some
    /// other illegal operations.
    /// </summary>
    [AVM2ExportClass(name = "TypeError", isDynamic = true)]
    public class ASTypeError : ASError {

        /// <summary>
        /// The value of the "length" property of the AS3 TypeError class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// Creates a new instance of TypeError.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="id">The error code.</param>
        [AVM2ExportTrait]
        public ASTypeError([ParamDefaultValue("")] ASAny message, int id = 0) : base(message, id) {
            name = "TypeError";
        }

    }

}
