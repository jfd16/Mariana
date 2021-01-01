namespace Mariana.AVM2.Core {

    /// <summary>
    /// Contains values representing the status of a property binding operation on an object.
    /// </summary>
    public enum BindStatus : byte {

        /// <summary>
        /// The requested property does not exist.
        /// </summary>
        NOT_FOUND,

        /// <summary>
        /// The requested property exists, and the operation has completed successfully.
        /// </summary>
        SUCCESS,

        /// <summary>
        /// The requested property does not exist; however, the binding is reported as a "soft"
        /// success, with the value set to some default value (usually undefined). While this result
        /// is usually considered as a success; when searching a property in a scope stack it is
        /// considered as a failure.
        /// </summary>
        SOFT_SUCCESS,

        /// <summary>
        /// More than one property matching the requested name exists. This occurs with multiname
        /// lookup (i.e. when a name and set of namespaces are specified, as opposed to a single
        /// qualified name), or when the "any" namespace is used.
        /// </summary>
        AMBIGUOUS,

        /// <summary>
        /// Indicates an attempt to call a method as a constructor (which is an illegal operation).
        /// </summary>
        FAILED_METHODCONSTRUCT,

        /// <summary>
        /// Indicates an attempt to invoke an object that is not a callable object. (Callable objects
        /// are Function and Class objects).
        /// </summary>
        FAILED_NOTFUNCTION,

        /// <summary>
        /// Indicates an attempt to invoke an object that is not a constructible object as a
        /// constructor. (Constructible objects are Function objects which are not method closures,
        /// and Class objects).
        /// </summary>
        FAILED_NOTCONSTRUCTOR,

        /// <summary>
        /// Indicates an attempt to define a dynamic property on an object using a namespace that is
        /// not the public namespace.
        /// </summary>
        FAILED_CREATEDYNAMICNONPUBLIC,

        /// <summary>
        /// Indicates an attempt to write to a read-only property, such as a read-only field or an
        /// accessor property without a setter method.
        /// </summary>
        FAILED_READONLY,

        /// <summary>
        /// Indicates an attempt to read to a write-only property, such as an accessor property
        /// without a getter method.
        /// </summary>
        FAILED_WRITEONLY,

        /// <summary>
        /// Indicates an attempt to assign a new value to an object method.
        /// </summary>
        FAILED_ASSIGNMETHOD,

        /// <summary>
        /// Indicates an attempt to assign a new value to a class.
        /// </summary>
        FAILED_ASSIGNCLASS,

        /// <summary>
        /// Indicates an attempt to use the descendants (..) operator on an object that does not
        /// support it (i.e. not an XML or XMLList object)
        /// </summary>
        FAILED_DESCENDANTOP,

    }

}
