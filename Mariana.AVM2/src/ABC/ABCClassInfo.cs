using System;
using Mariana.AVM2.Core;
using Mariana.Common;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// Represents a class defined in an ABC file.
    /// </summary>
    public sealed class ABCClassInfo {

        private int m_abcIndex;

        private QName m_name;

        private ABCMultiname m_parentName;

        private ABCMultiname[] m_ifaceNames;

        private Namespace m_protectedNs;

        private ABCClassFlags m_flags;

        private ABCMethodInfo m_instanceInit;

        private ABCMethodInfo m_staticInit;

        private ABCTraitInfo[] m_instanceTraits;

        private ABCTraitInfo[] m_staticTraits;

        /// <summary>
        /// Gets the zero-based index of the class entry in the ABC file metadata.
        /// </summary>
        public int abcIndex => m_abcIndex;

        /// <summary>
        /// Gets the name of the class defined in the class entry in the ABC file metadata.
        /// </summary>
        public QName name => m_name;

        /// <summary>
        /// Gets the name of the base class of this class.
        /// </summary>
        public ABCMultiname parentName => m_parentName;

        /// <summary>
        /// Gets the protected namespace used by this class.
        /// </summary>
        /// <value>The protected namespace used by this class. If this class does not use
        /// a protected namespace, the "any" namespace is returned.</value>
        public Namespace protectedNamespace => m_protectedNs;

        /// <summary>
        /// Gets a set of flags from the <see cref="ABCClassFlags"/> enumeration associated with
        /// this class.
        /// </summary>
        public ABCClassFlags flags => m_flags;

        /// <summary>
        /// Gets a <see cref="ABCMethodInfo"/> instance representing the method used as this
        /// class's instance constructor.
        /// </summary>
        public ABCMethodInfo instanceInitMethod => m_instanceInit;

        /// <summary>
        /// Gets a <see cref="ABCMethodInfo"/> instance representing the method used as this
        /// class's static constructor.
        /// </summary>
        public ABCMethodInfo staticInitMethod => m_staticInit;

        /// <summary>
        /// Gets an array containing the names of the interfaces implemented by this class.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{ABCMultiname}"/> instance containing multinames
        /// representing the names of the interfaces that this class implements.</returns>
        public ReadOnlyArrayView<ABCMultiname> getInterfaceNames() => new ReadOnlyArrayView<ABCMultiname>(m_ifaceNames);

        /// <summary>
        /// Gets an array containing the <see cref="ABCTraitInfo"/> objects representing the
        /// instance traits declared by this class.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{ABCMultiname}"/> instance containing the
        /// <see cref="ABCTraitInfo"/> objects representing the instance traits declared by
        /// this class.</returns>
        public ReadOnlyArrayView<ABCTraitInfo> getInstanceTraits() => new ReadOnlyArrayView<ABCTraitInfo>(m_instanceTraits);

        /// <summary>
        /// Gets an array containing the <see cref="ABCTraitInfo"/> objects representing the
        /// static traits declared by this class.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{ABCMultiname}"/> instance containing the
        /// <see cref="ABCTraitInfo"/> objects representing the static traits declared by
        /// this class.</returns>
        public ReadOnlyArrayView<ABCTraitInfo> getStaticTraits() => new ReadOnlyArrayView<ABCTraitInfo>(m_staticTraits);

        internal ABCClassInfo(int abcIndex) {
            m_abcIndex = abcIndex;

            // This will be set from init()
            m_ifaceNames = null!;

            // These will be set from initInstanceInfo and initStaticInfo
            m_instanceInit = null!;
            m_instanceTraits = null!;
            m_staticInit = null!;
            m_staticTraits = null!;
        }

        internal void init(
            in QName name, ABCMultiname parentName, ABCMultiname[] ifaceNames, Namespace protectedNs, ABCClassFlags flags)
        {
            m_name = name;
            m_parentName = parentName;
            m_ifaceNames = ifaceNames;
            m_protectedNs = protectedNs;
            m_flags = flags;
        }

        internal void initInstanceInfo(ABCMethodInfo instanceInit, ABCTraitInfo[] instanceTraits) {
            m_instanceInit = instanceInit;
            m_instanceTraits = instanceTraits;
        }

        internal void initStaticInfo(ABCMethodInfo staticInit, ABCTraitInfo[] staticTraits) {
            m_staticInit = staticInit;
            m_staticTraits = staticTraits;
        }

    }
}
