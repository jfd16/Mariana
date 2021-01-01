using System;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Contains options that can be used to control a property binding operation.
    /// </summary>
    [Flags]
    public enum BindOptions : byte {

        /// <summary>
        /// Specifies that the traits table of the object's class should be searched for properties.
        /// </summary>
        SEARCH_TRAITS = 1,

        /// <summary>
        /// Specifies that the prototype chain of the object should be searched for properties. This
        /// is only applicable if the search includes the public namespace.
        /// </summary>
        SEARCH_PROTOTYPE = 2,

        /// <summary>
        /// Specifies that the dynamic properties of the object should be searched for properties.
        /// This is only applicable if the search includes the public namespace and the object is an
        /// instance of a dynamic class.
        /// </summary>
        SEARCH_DYNAMIC = 4,

        /// <summary>
        /// Specifies that the search must look for attributes (as opposed to elements) in of XML
        /// and XMLList objects. For any other type of object, setting this flag results in a failed
        /// binding.
        /// </summary>
        ATTRIBUTE = 8,

        /// <summary>
        /// Specifies that the namespace of the property was provided at runtime by
        /// ActionScript 3 code.
        /// </summary>
        RUNTIME_NAMESPACE = 16,

        /// <summary>
        /// Specifies that the local name of the property was provided at runtime by
        /// ActionScript 3 code.
        /// </summary>
        RUNTIME_NAME = 32,

        /// <summary>
        /// Specifies that the receiver passed to a function call must be null (instead of the
        /// object on which the property binding is invoked).
        /// </summary>
        NULL_RECEIVER = 64,

    }

}
