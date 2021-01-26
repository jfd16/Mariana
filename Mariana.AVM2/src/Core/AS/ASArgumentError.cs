using System;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// An ArgumentError is thrown if the value of an argument passed to a method is not accepted
    /// by that method's definition, or a method is given an incorrect number of arguments.
    /// </summary>
    [AVM2ExportClass(name = "ArgumentError", isDynamic = true)]
    public class ASArgumentError : ASError {

        /// <summary>
        /// Creates a new instance of <see cref="ASArgumentError"/>.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="id">The error code.</param>
        [AVM2ExportTrait]
        public ASArgumentError(string message = "", int id = 0) : base(message, id) {
            name = "ArgumentError";
        }

    }

}
