using System;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A VerifyError is thrown when the ActionScript 3 bytecode compiler encounters invalid code.
    /// </summary>
    ///
    /// <remarks>
    /// <para>Some (but not all) reasons for a VerifyError being thrown are:</para>
    /// <list type="bullet">
    /// <item><description>A class inherits an interface or implements a class as an interface, or an
    /// interface inherits a class.</description></item>
    /// <item><description>A class inherits a final class, overrides a final method or does not implement
    /// an interface method.</description></item>
    /// <item><description>The target of a branch instruction in a method body is out of bounds or not at the
    /// first byte of a multibyte instruction.</description></item>
    /// <item><description>The code of a method contains the dxns, dxnslate or newactivation opcodes without
    /// the associated flags set in the method's signature.</description></item>
    /// <item><description>The code of a method refers to a parameter, local variable or scope stack position
    /// that is out of bounds.</description></item>
    /// </list>
    /// <para>A VerifyError is usually an indication of a bug in the compiler or other tool used
    /// to generate the ActionScript bytecode.</para>
    /// </remarks>
    [AVM2ExportClass(name = "VerifyError", isDynamic = true)]
    public class ASVerifyError : ASError {

        /// <summary>
        /// The value of the "length" property of the AS3 VerifyError class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// Creates a new instance of VerifyError.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="id">The error code.</param>
        [AVM2ExportTrait]
        public ASVerifyError([ParamDefaultValue("")] ASAny message, int id = 0) : base(message, id) {
            name = "VerifyError";
        }

    }

}
