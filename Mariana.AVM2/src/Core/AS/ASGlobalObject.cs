using System;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// An object that can be used to access traits and dynamic properties in the global
    /// scope of an application domain.
    /// </summary>
    /// <remarks>
    /// The global object for an application domain can be obtained using the
    /// <see cref="ApplicationDomain.globalObject" qualifyHint="true"/> property.
    /// </remarks>
    internal sealed class ASGlobalObject : ASObject {

        private readonly ApplicationDomain m_domain;

        internal ASGlobalObject(ApplicationDomain domain) {
            m_domain = domain;
        }

        /// <summary>
        /// Returns the <see cref="ApplicationDomain"/> instance representing the application
        /// domain to which this global object belongs.
        /// </summary>
        public ApplicationDomain applicationDomain => m_domain;

        /// <summary>
        /// Performs a trait lookup on the object.
        /// </summary>
        /// <param name="name">The name of the trait to find.</param>
        /// <param name="trait">The trait with the name <paramref name="name"/>, if one
        /// exists.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the lookup.</returns>
        internal override BindStatus AS_lookupTrait(in QName name, out Trait trait) {
            BindStatus bindStatus = m_domain.lookupGlobalTrait(name, noInherited: false, out trait);
            if (bindStatus != BindStatus.NOT_FOUND)
                return bindStatus;

            return AS_class.lookupTrait(name, isStatic: false, out trait);
        }

        /// <summary>
        /// Performs a trait lookup on the object.
        /// </summary>
        /// <param name="name">The name of the trait to find.</param>
        /// <param name="nsSet">A set of namespaces in which to search for the trait.</param>
        /// <param name="trait">The trait with the name <paramref name="name"/> in a namespace of
        /// <paramref name="nsSet"/>, if one exists.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the lookup.</returns>
        internal override BindStatus AS_lookupTrait(string name, in NamespaceSet nsSet, out Trait trait) {
            BindStatus bindStatus = m_domain.lookupGlobalTrait(name, nsSet, noInherited: false, out trait);
            if (bindStatus != BindStatus.NOT_FOUND)
                return bindStatus;

            return AS_class.lookupTrait(name, nsSet, isStatic: false, out trait);
        }

    }
}
