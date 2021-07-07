using System;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// Represents a trait defined in an ABC file.
    /// </summary>
    public sealed class ABCTraitInfo {

        private sealed class _Field {
            public ABCMultiname typeName;
            public bool hasDefault;
            public ASAny defaultVal;
        }

        private QName m_name;

        private ABCTraitFlags m_flags;

        private int m_slotOrDispId;

        private object m_obj;   // ABCClassInfo, ABCMethodInfo or _Field.

        private MetadataTagCollection m_metadata;

        /// <summary>
        /// Gets the name of this trait.
        /// </summary>
        public QName name => m_name;

        /// <summary>
        /// Gets the flags from the <see cref="ABCTraitFlags"/> enumeration associated with
        /// this trait. This includes the trait kind and some additional attributes.
        /// </summary>
        public ABCTraitFlags flags => m_flags;

        /// <summary>
        /// Gets the kind of the trait.
        /// </summary>
        /// <value>The kind of the trait. This will be one of <see cref="ABCTraitFlags.Slot"/>,
        /// <see cref="ABCTraitFlags.Const"/>, <see cref="ABCTraitFlags.Method"/>,
        /// <see cref="ABCTraitFlags.Getter"/>, <see cref="ABCTraitFlags.Setter"/>,
        /// <see cref="ABCTraitFlags.Function"/> or <see cref="ABCTraitFlags.Class"/>.</value>
        public ABCTraitFlags kind => m_flags & ABCTraitFlags.KIND_MASK;

        /// <summary>
        /// Returns a value indicating whether this <see cref="ABCTraitInfo"/> represents a field trait
        /// (that is, its kind is <see cref="ABCTraitFlags.Slot"/> or <see cref="ABCTraitFlags.Const"/>)
        /// </summary>
        public bool isField {
            get {
                var kind = m_flags & ABCTraitFlags.KIND_MASK;
                return kind == ABCTraitFlags.Slot || kind == ABCTraitFlags.Const;
            }
        }

        /// <summary>
        /// Returns a value indicating whether this <see cref="ABCTraitInfo"/> represents a
        /// class trait (that is, its kind is <see cref="ABCTraitFlags.Class"/>).
        /// </summary>
        public bool isClass => kind == ABCTraitFlags.Class;

        /// <summary>
        /// Returns a value indicating whether this <see cref="ABCTraitInfo"/> represents a
        /// function trait (that is, its kind is <see cref="ABCTraitFlags.Function"/>).
        /// </summary>
        public bool isFunction => kind == ABCTraitFlags.Function;

        /// <summary>
        /// Returns a value indicating whether this <see cref="ABCTraitInfo"/> represents a
        /// method trait (that is, its kind is <see cref="ABCTraitFlags.Method"/>).
        /// </summary>
        public bool isMethod => kind == ABCTraitFlags.Method;

        /// <summary>
        /// Returns a value indicating whether this <see cref="ABCTraitInfo"/> represents a method,
        /// getter or setter trait.
        /// </summary>
        public bool isMethodOrAccessor {
            get {
                var kind = m_flags & ABCTraitFlags.KIND_MASK;
                return kind >= ABCTraitFlags.Method && kind <= ABCTraitFlags.Setter;
            }
        }

        /// <summary>
        /// Gets the <see cref="ABCClassInfo"/> instance representing the class definition
        /// for a class trait.
        /// </summary>
        /// <value>The <see cref="ABCClassInfo"/> instance representing the class definition.</value>
        /// <exception cref="AVM2Exception">Error #10332: This <see cref="ABCTraitInfo"/> does not
        /// represent a class trait.</exception>
        public ABCClassInfo classInfo {
            get {
                if (isClass)
                    return (ABCClassInfo)m_obj;

                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__ABC_TRAIT_INFO_UNSUPPORTED_PROPERTY, nameof(classInfo), kind);
            }
        }

        /// <summary>
        /// Gets the <see cref="ABCMethodInfo"/> instance representing the method definition
        /// for a method, function, getter or setter trait.
        /// </summary>
        /// <value>The <see cref="ABCMethodInfo"/> instance representing the method definition.</value>
        /// <exception cref="AVM2Exception">Error #10332: This <see cref="ABCTraitInfo"/> does not
        /// represent a method, function, getter or setter trait.</exception>
        public ABCMethodInfo methodInfo {
            get {
                if (isMethodOrAccessor || isFunction)
                    return (ABCMethodInfo)m_obj;

                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__ABC_TRAIT_INFO_UNSUPPORTED_PROPERTY, nameof(methodInfo), kind);
            }
        }

        /// <summary>
        /// Gets a multiname representing the name of the field type for a field trait.
        /// </summary>
        /// <value>A multiname representing the name of the field type for a field trait.</value>
        /// <exception cref="AVM2Exception">Error #10332: This <see cref="ABCTraitInfo"/> does not
        /// represent a field trait (<see cref="ABCTraitFlags.Slot"/> or
        /// <see cref="ABCTraitFlags.Const"/>).</exception>
        public ABCMultiname fieldTypeName {
            get {
                if (isField)
                    return ((_Field)m_obj).typeName;

                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__ABC_TRAIT_INFO_UNSUPPORTED_PROPERTY, nameof(fieldTypeName), kind);
            }
        }

        /// <summary>
        /// Gets a value indicating whether a field trait has a default value associated with it.
        /// </summary>
        /// <value>True if this instance represents a field trait with a default value, false otherwise.</value>
        /// <exception cref="AVM2Exception">Error #10332: This <see cref="ABCTraitInfo"/> does not
        /// represent a field trait (<see cref="ABCTraitFlags.Slot"/> or
        /// <see cref="ABCTraitFlags.Const"/>).</exception>
        public bool fieldHasDefault {
            get {
                if (isField)
                    return ((_Field)m_obj).hasDefault;

                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__ABC_TRAIT_INFO_UNSUPPORTED_PROPERTY, nameof(fieldHasDefault), kind);
            }
        }

        /// <summary>
        /// Gets the default value of a field trait.
        /// </summary>
        /// <value>The default value, if this instance represents a field trait with a
        /// default value. If the field does not have an associated default value, the value of
        /// this property is undefined.</value>
        /// <exception cref="AVM2Exception">Error #10332: This <see cref="ABCTraitInfo"/> does not
        /// represent a field trait (<see cref="ABCTraitFlags.Slot"/> or
        /// <see cref="ABCTraitFlags.Const"/>).</exception>
        public ASAny fieldDefaultValue {
            get {
                if (isField)
                    return ((_Field)m_obj).defaultVal;

                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__ABC_TRAIT_INFO_UNSUPPORTED_PROPERTY, nameof(fieldDefaultValue), kind);
            }
        }

        /// <summary>
        /// Gets the slot index of this trait, as defined in the ABC file metadata.
        /// </summary>
        /// <value>The slot index of this field, class or function trait. A value of 0
        /// indicates that the trait does not have an explicit slot index.</value>
        /// <exception cref="AVM2Exception">Error #10332: This <see cref="ABCTraitInfo"/>
        /// represents a method, function, getter or setter trait.</exception>
        public int slotId {
            get {
                if (!isMethodOrAccessor)
                    return m_slotOrDispId;

                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__ABC_TRAIT_INFO_UNSUPPORTED_PROPERTY, nameof(slotId), kind);
            }
        }

        /// <summary>
        /// Gets the method dispatch index of this trait, as defined in the ABC file metadata.
        /// </summary>
        /// <value>The method dispatch index (<c>disp_id</c>) of this trait. A value of 0 indicates that the
        /// method does not have an explicit dispatch index assigned.</value>
        /// <exception cref="AVM2Exception">Error #10332: This <see cref="ABCTraitInfo"/> does not
        /// represent a method, function, getter or setter trait.</exception>
        public int methodDispId {
            get {
                if (isMethodOrAccessor)
                    return m_slotOrDispId;

                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__ABC_TRAIT_INFO_UNSUPPORTED_PROPERTY, nameof(methodDispId), kind);
            }
        }

        /// <summary>
        /// Gets a <see cref="MetadataTagCollection"/> containing the metadata tags defined for this trait.
        /// </summary>
        /// <returns>A <see cref="MetadataTagCollection"/> containing the metadata tags
        /// defined for this trait.</returns>
        public MetadataTagCollection metadata => m_metadata;

        internal ABCTraitInfo(in QName name, ABCTraitFlags flags, int slotOrDispId, object classOrMethodInfo) {
            m_name = name;
            m_flags = flags;
            m_slotOrDispId = slotOrDispId;
            m_metadata = MetadataTagCollection.empty;
            m_obj = classOrMethodInfo;
        }

        internal ABCTraitInfo(
            in QName name,
            ABCTraitFlags flags,
            int slotId,
            ABCMultiname fieldTypeName,
            bool fieldHasDefault,
            ASAny fieldDefaultVal
        ) {
            m_name = name;
            m_flags = flags;
            m_slotOrDispId = slotId;
            m_metadata = MetadataTagCollection.empty;
            m_obj = new _Field {typeName = fieldTypeName, hasDefault = fieldHasDefault, defaultVal = fieldDefaultVal};
        }

        internal void setMetadata(MetadataTagCollection metadata) => m_metadata = metadata;

    }

}
