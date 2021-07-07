using System;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A RangeError is thrown in the following cases: (i) When an argument passed to a method is
    /// not within the range of values accepted by that method's definition, (ii) A Vector is
    /// indexed with an index that is out of bounds, (iii) The length of a fixed-length Vector is
    /// changed.
    /// </summary>
    [AVM2ExportClass(name = "RangeError", isDynamic = true)]
    public class ASRangeError : ASError {

        /// <summary>
        /// The value of the "length" property of the AS3 RangeError class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// Creates a new instance of RangeError
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="id">The error code.</param>
        [AVM2ExportTrait]
        public ASRangeError([ParamDefaultValue("")] ASAny message, int id = 0) : base(message, id) {
            name = "RangeError";
        }

    }
}
