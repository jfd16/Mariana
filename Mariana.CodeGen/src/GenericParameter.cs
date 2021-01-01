using System;
using System.Reflection;
using System.Reflection.Metadata;
using Mariana.Common;

namespace Mariana.CodeGen {

    /// <summary>
    /// Represents a type parameter in generic type or method.
    /// </summary>
    internal readonly struct GenericParameter {

        /// <summary>
        /// The handle of the type or method that declares this type parameter.
        /// </summary>
        public readonly EntityHandle ownerHandle;

        /// <summary>
        /// The name of the type parameter.
        /// </summary>
        public readonly string name;

        /// <summary>
        /// The zero-based position of the type parameter in the declaring type or method.
        /// </summary>
        public readonly int position;

        /// <summary>
        /// The attributes of this type parameter.
        /// </summary>
        public readonly GenericParameterAttributes attributes;

        /// <summary>
        /// The base class and/or interface constraints for this type parameter.
        /// </summary>
        private readonly EntityHandle[] m_constraints;

        /// <summary>
        /// Creates a new instance of <see cref="GenericParameter"/>.
        /// </summary>
        /// <param name="ownerHandle">The handle of the type or method that declares the type parameter.</param>
        /// <param name="position">The position of the type parameter in the declaring type or method.</param>
        /// <param name="name">The name of the type parameter.</param>
        /// <param name="attributes">The attributes of the type parameter.</param>
        /// <param name="constraints">The base class and/or interface constraints for this type parameter.</param>
        internal GenericParameter(
            EntityHandle ownerHandle, int position, string name,
            GenericParameterAttributes attributes, EntityHandle[] constraints)
        {
            this.ownerHandle = ownerHandle;
            this.name = name;
            this.position = position;
            this.attributes = attributes;
            this.m_constraints = constraints;
        }

        /// <summary>
        /// Returns a <see cref="ReadOnlyArrayView{EntityHandle}"/> instance containing the handles for the base
        /// class and/or interface types to which this type parameter is constrained to.
        /// </summary>
        public ReadOnlyArrayView<EntityHandle> getConstraints() => new ReadOnlyArrayView<EntityHandle>(m_constraints);

    }

}
