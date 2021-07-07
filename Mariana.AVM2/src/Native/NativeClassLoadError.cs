using System;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Native {

    /// <summary>
    /// A <see cref="NativeClassLoadError"/> is thrown if an error occurs when loading a class
    /// written in .NET code into the AVM2.
    /// </summary>
    [AVM2ExportClass(nsUri = "__Mariana_AVM2Internal")]
    public sealed class NativeClassLoadError : ASError {

        /// <summary>
        /// Creates a new instance of <see cref="NativeClassLoadError"/>.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="id">The error code.</param>
        [AVM2ExportTrait]
        public NativeClassLoadError([ParamDefaultValue("")] ASAny message, int id = 0) : base(message, id) { }

    }

}
