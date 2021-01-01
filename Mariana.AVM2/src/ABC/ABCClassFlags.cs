using System;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// A set of flags that are associated with a class in an ABC file to define certain
    /// attributes.
    /// </summary>
    /// <seealso cref="ABCClassInfo.flags"/>
    [Flags]
    public enum ABCClassFlags : byte {

        /// <summary>
        /// Specifies that the class is a sealed class (instances cannot have dynamic properties).
        /// </summary>
        ClassSealed = 0x01,

        /// <summary>
        /// Specifies that the class is a final class and cannot be inherited from.
        /// </summary>
        ClassFinal = 0x02,

        /// <summary>
        /// Specifies that the class is an interface.
        /// </summary>
        ClassInterface = 0x04,

        /// <summary>
        /// Specifies that the class uses a protected namespace.
        /// </summary>
        ClassProtectedNs = 0x08,

    }

}
