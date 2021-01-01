using Mariana.AVM2.Core;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Represents the result of a property lookup on an object.
    /// </summary>
    internal struct ResolvedProperty {

        /// <summary>
        /// The unique index of this <see cref="ResolvedProperty"/> instance. A <see cref="ResolvedProperty"/>
        /// instance can be retrieved from its index using <see cref="MethodCompilation.getResolvedProperty"/>.
        /// </summary>
        public int id;

        /// <summary>
        /// A value from the <see cref="DataNodeType"/> enumeration representing the type
        /// of the object on which the property is resolved.
        /// </summary>
        public DataNodeType objectType;

        /// <summary>
        /// A value from the <see cref="DataNodeType"/> enumeration representing the type
        /// of the runtime namespace argument on the stack. If there is no runtime namespace
        /// argument, this has the value <see cref="DataNodeType.UNKNOWN"/>.
        /// </summary>
        public DataNodeType rtNamespaceType;

        /// <summary>
        /// A value from the <see cref="DataNodeType"/> enumeration representing the type
        /// of the runtime name argument on the stack. If there is no runtime name
        /// argument, this has the value <see cref="DataNodeType.UNKNOWN"/>.
        /// </summary>
        public DataNodeType rtNameType;

        /// <summary>
        /// A value from the <see cref="ResolvedPropertyKind"/> enumeration representing the
        /// nature of the resolved property.
        /// </summary>
        public ResolvedPropertyKind propKind;

        /// <summary>
        /// The class of the object on which the property is resolved, if <see cref="objectType"/>
        /// is <see cref="DataNodeType.OBJECT"/> or <see cref="DataNodeType.CLASS"/>.
        /// </summary>
        public Class objectClass;

        /// <summary>
        /// An object representing the resolved property. The type of this object depends
        /// on the value of <see cref="propKind"/>.
        /// </summary>
        public object propInfo;

    }

    /// <summary>
    /// Enumerates the kinds of resolved properties used by the compiler.
    /// </summary>
    internal enum ResolvedPropertyKind : byte {

        /// <summary>
        /// Used by the compiler during resoultion to indicate that property resolution information is
        /// not known yet.
        /// </summary>
        UNKNOWN,

        /// <summary>
        /// The property resolves to a trait. <see cref="ResolvedProperty.propInfo"/> will contain
        /// the resolved <see cref="Trait"/> instance.
        /// </summary>
        TRAIT,

        /// <summary>
        /// The property resolves to a numeric index lookup on an object that supports it
        /// (such as Array or Vector). <see cref="ResolvedProperty.propInfo"/> contains
        /// the <see cref="IndexProperty"/> instance representing the index
        /// property.
        /// </summary>
        INDEX,

        /// <summary>
        /// The property resolves to an intrinsic function implemented by the compiler.
        /// <see cref="ResolvedProperty.propInfo"/> contains the resolved <see cref="Intrinsic"/>
        /// instance.
        /// </summary>
        INTRINSIC,

        /// <summary>
        /// The compiler is not able to resolve the property and runtime lookup must be used.
        /// </summary>
        RUNTIME,

        /// <summary>
        /// The property resolves to a trait, but an invoke or construct operation must be bound
        /// at runtime. <see cref="ResolvedProperty.propInfo"/> will contain the resolved
        /// <see cref="Trait"/> instance.
        /// </summary>
        TRAIT_RT_INVOKE,

    }

}
