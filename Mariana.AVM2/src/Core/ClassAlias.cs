using System;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Provides an alias for a class with a name different from the class's actual name.
    /// </summary>
    internal sealed class ClassAlias : Class {

        private readonly ClassImpl m_impl;

        internal ClassAlias(
            in QName name,
            Class? declClass,
            ApplicationDomain appDomain,
            ClassImpl impl,
            MetadataTagCollection? metadata
        )
            : base(name, declClass, appDomain, impl.tag)
        {
            m_impl = impl;
            setMetadata(metadata);
        }

        /// <inheritdoc/>
        internal override ClassImpl getClassImpl() => m_impl;

    }

}
