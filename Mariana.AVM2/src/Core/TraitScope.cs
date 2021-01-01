using System;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Specifies the scope(s) that should be considered when looking up a trait in a class.
    /// </summary>
    [Flags]
    public enum TraitScope : short {

        /// <summary>
        /// An instance trait declared by a class or interface.
        /// </summary>
        INSTANCE_DECLARED = 1,

        /// <summary>
        /// An instance trait inherited from a parent class or interface.
        /// </summary>
        INSTANCE_INHERITED = 2,

        /// <summary>
        /// A static trait declared by a class.
        /// </summary>
        STATIC = 4,

        /// <summary>
        /// An instance trait on a class or interface (declared or inherited).
        /// </summary>
        INSTANCE = INSTANCE_DECLARED | INSTANCE_INHERITED,

        /// <summary>
        /// A trait declared on a class or interface (instance or static).
        /// </summary>
        DECLARED = INSTANCE_DECLARED | STATIC,

        /// <summary>
        /// Matches any trait (instance or static, declared or inherited).
        /// </summary>
        ALL = INSTANCE | STATIC,

    }

}
