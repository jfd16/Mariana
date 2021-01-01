using System;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// A set of flags that are associated with a method in an ABC file to define certain
    /// attributes.
    /// </summary>
    /// <seealso cref="ABCMethodInfo.flags"/>
    [Flags]
    public enum ABCMethodFlags : byte {

        /// <summary>
        /// Specifies that the method uses the "arguments" object to access its arguments.
        /// </summary>
        NEED_ARGUMENTS = 0x1,

        /// <summary>
        /// Specifies that the method contains a newactivation opcode.
        /// </summary>
        NEED_ACTIVATION = 0x2,

        /// <summary>
        /// Specifies that the method takes a "rest" parameter.
        /// </summary>
        NEED_REST = 0x4,

        /// <summary>
        /// Specifies that the method contains optional parameters.
        /// </summary>
        HAS_OPTIONAL = 0x8,

        /// <summary>
        /// Specifies that the method contains a dxns or dxnslate opcode.
        /// </summary>
        SET_DXNS = 0x40,

        /// <summary>
        /// Specifies that the method defines names for its formal parameters.
        /// </summary>
        HAS_PARAM_NAMES = 0x80,

    }

}
