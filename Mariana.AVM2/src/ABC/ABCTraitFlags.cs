using System;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// A set of flags used to define a trait's type and some additional attributes.
    /// </summary>
    /// <seealso cref="ABCTraitInfo.flags"/> 
    [Flags]
    public enum ABCTraitFlags {

        /// <summary>
        /// The trait is a (non-constant) field.
        /// </summary>
        Slot = 0,

        /// <summary>
        /// The trait is a method.
        /// </summary>
        Method = 1,

        /// <summary>
        /// The trait is a property getter.
        /// </summary>
        Getter = 2,

        /// <summary>
        /// The trait is a property setter.
        /// </summary>
        Setter = 3,

        /// <summary>
        /// The trait is a class trait.
        /// </summary>
        Class = 4,

        /// <summary>
        /// The trait is a function trait.
        /// </summary>
        Function = 5,

        /// <summary>
        /// The trait is a constant field.
        /// </summary>
        Const = 6,

        /// <summary>
        /// Indicates that a method is final and cannot be overridden. Can only be set
        /// along with <see cref="Method"/>, <see cref="Getter"/> or <see cref="Setter"/>.  
        /// </summary>
        ATTR_Final = 0x10,

        /// <summary>
        /// Indicates that a method overrides a base class method. Can only be set
        /// along with <see cref="Method"/>, <see cref="Getter"/> or <see cref="Setter"/>.
        /// </summary>
        ATTR_Override = 0x20,

        /// <summary>
        /// Indicates that the trait contains metadata.
        /// </summary>
        ATTR_Metadata = 0x40,

        /// <summary>
        /// A mask that can be ANDed with a trait's flags value to obtain its kind.
        /// </summary>
        KIND_MASK = 0x0F,

    }

}
