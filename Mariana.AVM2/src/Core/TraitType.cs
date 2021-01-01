using System;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Specifies the type of a trait.
    /// </summary>
    [Flags]
    public enum TraitType : short {

        /// <summary>
        /// A class trait.
        /// </summary>
        CLASS = 1,

        /// <summary>
        /// A field trait.
        /// </summary>
        FIELD = 2,

        /// <summary>
        /// An accessor property trait.
        /// </summary>
        PROPERTY = 4,

        /// <summary>
        /// A method trait.
        /// </summary>
        METHOD = 8,

        /// <summary>
        /// A constant trait.
        /// </summary>
        CONSTANT = 16,

        /// <summary>
        /// A bitwise-OR combination of all values of this enumeration.
        /// </summary>
        ALL = CLASS | FIELD | PROPERTY | METHOD | CONSTANT,

    }

}
