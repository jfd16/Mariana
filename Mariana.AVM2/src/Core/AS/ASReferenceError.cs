using System;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A ReferenceError is thrown when a property binding operation on an object fails, such as
    /// when no trait with the property name is found in the object's class and the object is not
    /// dynamic, or a value is assigned to a read-only property.
    /// </summary>
    [AVM2ExportClass(name = "ReferenceError", isDynamic = true)]
    public class ASReferenceError : ASError {

        /// <summary>
        /// The value of the "length" property of the AS3 ReferenceError class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// Creates a new instance of ReferenceError.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="id">The error code.</param>
        [AVM2ExportTrait]
        public ASReferenceError(string message = "", int id = 0) : base(message, id) {

        }

    }

}