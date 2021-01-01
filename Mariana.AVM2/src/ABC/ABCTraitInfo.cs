﻿using System;
using Mariana.AVM2.Core;
using Mariana.Common;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// Represents a trait defined in an ABC file.
    /// </summary>
    public sealed class ABCTraitInfo {

        private class _Field {
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
        /// Gets the <see cref="ABCClassInfo"/> instance representing the class definition
        /// for a class trait.
        /// </summary>
        /// <value>The <see cref="ABCClassInfo"/> instance representing the class definition.
        /// If this trait is not a class trait, this value is null.</value>
        public ABCClassInfo classInfo {
            get {
                if ((m_flags & ABCTraitFlags.KIND_MASK) == ABCTraitFlags.Class)
                    return (ABCClassInfo)m_obj;
                return null;
            }
        }

        /// <summary>
        /// Gets the <see cref="ABCMethodInfo"/> instance representing the method definition
        /// for a method, function, getter or setter trait.
        /// </summary>
        /// <value>The <see cref="ABCMethodInfo"/> instance representing the method definition.
        /// If this trait is not a method, function, getter or setter trait, this value is null.</value>
        public ABCMethodInfo methodInfo {
            get {
                ABCTraitFlags kind = (m_flags & ABCTraitFlags.KIND_MASK);
                if ((kind >= ABCTraitFlags.Method && kind <= ABCTraitFlags.Setter) || kind == ABCTraitFlags.Function)
                    return (ABCMethodInfo)m_obj;
                return null;
            }
        }

        /// <summary>
        /// Gets a multiname representing the name of the field type for a field trait.
        /// </summary>
        /// <value>A multiname representing the name of the field type for a field trait.
        /// If this trait is not a field trait (<see cref="kind"/> is not equal to
        /// <see cref="ABCTraitFlags.Slot"/> or <see cref="ABCTraitFlags.Const"/>),
        /// this value is the default value of <see cref="ABCMultiname"/>.</value>
        public ABCMultiname fieldTypeName {
            get {
                ABCTraitFlags kind = m_flags & ABCTraitFlags.KIND_MASK;
                if (kind == ABCTraitFlags.Slot || kind == ABCTraitFlags.Const)
                    return ((_Field)m_obj).typeName;
                return default(ABCMultiname);
            }
        }

        /// <summary>
        /// Gets a value indicating whether a field trait has a default value associated with it.
        /// </summary>
        /// <value>True if this instance represents a field trait with a default value. If this
        /// trait is not a field trait (<see cref="kind"/> is not equal to
        /// <see cref="ABCTraitFlags.Slot"/> or <see cref="ABCTraitFlags.Const"/>), or the
        /// field does not have an associated default value, this value is false.</value>
        public bool fieldHasDefault {
            get {
                ABCTraitFlags kind = m_flags & ABCTraitFlags.KIND_MASK;
                if (kind == ABCTraitFlags.Slot || kind == ABCTraitFlags.Const)
                    return ((_Field)m_obj).hasDefault;
                return false;
            }
        }

        /// <summary>
        /// Gets the default value of a field trait.
        /// </summary>
        /// <value>The default value, if this instance represents a field trait with a
        /// default value. If this trait is not a field trait (<see cref="kind"/> is not equal to
        /// <see cref="ABCTraitFlags.Slot"/> or <see cref="ABCTraitFlags.Const"/>), or the
        /// field does not have an associated default value, this value is undefined.</value>
        public ASAny fieldDefaultValue {
            get {
                ABCTraitFlags kind = m_flags & ABCTraitFlags.KIND_MASK;
                if (kind == ABCTraitFlags.Slot || kind == ABCTraitFlags.Const)
                    return ((_Field)m_obj).defaultVal;
                return ASAny.undefined;
            }
        }

        /// <summary>
        /// Gets the slot index of this trait, as defined in the ABC file metadata.
        /// </summary>
        /// <value>The slot index of this trait. If this is a method, getter or setter
        /// trait, this value is -1. A value of 0 indicates that the trait does not have
        /// an explicit slot index.</value>
        public int slotId {
            get {
                ABCTraitFlags kind = m_flags & ABCTraitFlags.KIND_MASK;
                if (kind >= ABCTraitFlags.Method && kind <= ABCTraitFlags.Setter)
                    return -1;
                return m_slotOrDispId;
            }
        }

        /// <summary>
        /// Gets the method dispatch index of this trait, as defined in the ABC file metadata.
        /// </summary>
        /// <value>The method dispatch index (<c>disp_id</c>) of this trait. If this is not a
        /// method, getter or setter trait, this value is -1. A value of 0 indicates that the
        /// method does not have an explicit dispatch index assigned.</value>
        public int methodDispId {
            get {
                ABCTraitFlags kind = m_flags & ABCTraitFlags.KIND_MASK;
                if (kind >= ABCTraitFlags.Method && kind <= ABCTraitFlags.Setter)
                    return m_slotOrDispId;
                return -1;
            }
        }

        /// <summary>
        /// Gets a <see cref="MetadataTagCollection"/> containing the metadata tags defined for this trait.
        /// </summary>
        /// <returns>A <see cref="MetadataTagCollection"/> containing the metadata tags
        /// defined for this trait.</returns>
        public MetadataTagCollection metadata => m_metadata;

        internal ABCTraitInfo(in QName name, ABCTraitFlags flags, int slotOrDispId, object classOrMthdInfo) {
            m_name = name;
            m_flags = flags;
            m_slotOrDispId = slotOrDispId;
            m_metadata = MetadataTagCollection.empty;
            m_obj = classOrMthdInfo;
        }

        internal ABCTraitInfo(
            in QName name, ABCTraitFlags flags, int slotId,
            ABCMultiname fieldTypeName, bool fieldHasDefault, ASAny fieldDefaultVal)
        {
            m_name = name;
            m_flags = flags;
            m_slotOrDispId = slotId;
            m_metadata = MetadataTagCollection.empty;
            m_obj = new _Field {typeName = fieldTypeName, hasDefault = fieldHasDefault, defaultVal = fieldDefaultVal};
        }

        internal void setMetadata(MetadataTagCollection metadata) => m_metadata = metadata;

    }

}
