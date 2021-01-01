using System;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// Represents the types of constant values in ABC file data.
    /// </summary>
    public enum ABCConstKind : byte {

        /// <summary>
        /// An integer constant.
        /// </summary>
        Int = 0x03,

        /// <summary>
        /// An unsigned integer constant.
        /// </summary>
        UInt = 0x04,

        /// <summary>
        /// A floating-point constant.
        /// </summary>
        Double = 0x06,

        /// <summary>
        /// A string constant.
        /// </summary>
        Utf8 = 0x01,

        /// <summary>
        /// The Boolean value "true".
        /// </summary>
        True = 0x0B,

        /// <summary>
        /// The Boolean value "false".
        /// </summary>
        False = 0x0A,

        /// <summary>
        /// The null value.
        /// </summary>
        Null = 0x0C,

        /// <summary>
        /// The undefined value.
        /// </summary>
        Undefined = 0x00,

        /// <summary>
        /// A namespace constant whose kind is Namespace.
        /// </summary>
        Namespace = 0x08,

        /// <summary>
        /// A namespace constant whose kind is PackageNamespace.
        /// </summary>
        PackageNamespace = 0x16,

        /// <summary>
        /// A namespace constant whose kind is PackageInternalNs.
        /// </summary>
        PackageInternalNs = 0x17,

        /// <summary>
        /// A namespace constant whose kind is ProtectedNamespace.
        /// </summary>
        ProtectedNamespace = 0x18,

        /// <summary>
        /// A namespace constant whose kind is ExplicitNamespace.
        /// </summary>
        ExplicitNamespace = 0x19,

        /// <summary>
        /// A namespace constant whose kind is StaticProtectedNs.
        /// </summary>
        StaticProtectedNs = 0x1A,

        /// <summary>
        /// A namespace constant whose kind is PrivateNs.
        /// </summary>
        PrivateNs = 0x05,

        /// <summary>
        /// A multiname with a fixed single namespace and a fixed local name.
        /// </summary>
        QName = 0x07,

        /// <summary>
        /// A multiname with a fixed single namespace and a fixed local name. Used for XML attributes.
        /// </summary>
        QNameA = 0x0D,

        /// <summary>
        /// A multiname with a fixed local name and a namespace determined at runtime.
        /// </summary>
        RTQName = 0x0F,

        /// <summary>
        /// A multiname with a fixed local name and a namespace determined at runtime.
        /// Used for XML attributes.
        /// </summary>
        RTQNameA = 0x10,

        /// <summary>
        /// A multiname with both the namespace and local name determined at runtime.
        /// </summary>
        RTQNameL = 0x11,

        /// <summary>
        /// A multiname with both the namespace and local name determined at runtime. 
        /// Used for XML attributes.
        /// </summary>
        RTQNameLA = 0x12,

        /// <summary>
        /// A multiname with a fixed set of namespaces and a fixed local name.
        /// </summary>
        Multiname = 0x09,

        /// <summary>
        /// A multiname with a fixed set of namespaces and a fixed local name. Used for XML attributes.
        /// </summary>
        MultinameA = 0x0E,

        /// <summary>
        /// A multiname with a fixed set of namespaces, and a local name determined at runtime.
        /// </summary>
        MultinameL = 0x1B,

        /// <summary>
        /// A multiname with a fixed set of namespaces, and a local name determined at runtime.
        /// Used for XML attributes.
        /// </summary>
        MultinameLA = 0x1C,

        /// <summary>
        /// A special kind of multiname used for generic class names.
        /// </summary>
        GenericClassName = 0x1D,

    }

}
