using System;
using System.Reflection;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Represents the constructor function of a class in the AVM2.
    /// </summary>
    public abstract class ClassConstructor {

        private Class m_declaringClass;

        private ConstructorInfo m_underlyingCtorInfo;

        private MethodTraitParameter[] m_params;

        private bool m_hasRest;

        private int m_requiredArgCount;

        private LazyInitObject<RuntimeDispatch.MethodStub> m_lazyRuntimeDispatch;

        private MetadataTagCollection m_metadata;

        internal ClassConstructor(Class declaringClass) {
            m_declaringClass = declaringClass;
            m_metadata = MetadataTagCollection.empty;

            m_lazyRuntimeDispatch = new LazyInitObject<RuntimeDispatch.MethodStub>(
                () => RuntimeDispatch.generateCtorStub(this)
            );
        }

        /// <summary>
        /// Gets the class on which the constructor is declared.
        /// </summary>
        public Class declaringClass => m_declaringClass;

        /// <summary>
        /// Gets the underlying <see cref="ConstructorInfo"/> for the class constructor
        /// represented by this instance.
        /// </summary>
        public ConstructorInfo underlyingConstructorInfo => m_underlyingCtorInfo;

        /// <summary>
        /// Gets a Boolean value indicating whether the constructor has a "rest" parameter.
        /// </summary>
        public bool hasRest => m_hasRest;

        /// <summary>
        /// Gets the number of formal parameters declared by the constructor.
        /// </summary>
        public int paramCount => m_params.Length;

        /// <summary>
        /// Returns the number of formal parameters in this constructor that have no default values.
        /// </summary>
        public int requiredParamCount => m_requiredArgCount;

        /// <summary>
        /// Returns an array containing <see cref="MethodTraitParameter"/> instances representing
        /// this constructor's formal parameters.
        /// </summary>
        /// <returns>An array containing <see cref="MethodTraitParameter"/> instances representing
        /// the constructor's formal parameters.</returns>
        public ReadOnlyArrayView<MethodTraitParameter> getParameters() =>
            new ReadOnlyArrayView<MethodTraitParameter>(m_params);

        /// <summary>
        /// Sets the underlying <see cref="ConstructorInfo"/> of the class constructor represented by
        /// this instance.
        /// </summary>
        /// <param name="underlyingCtorInfo">The underlying <see cref="ConstructorInfo"/> for this
        /// constructor.</param>
        protected private void setUnderlyingCtorInfo(ConstructorInfo underlyingCtorInfo) {
            m_underlyingCtorInfo = underlyingCtorInfo;
        }

        /// <summary>
        /// Sets the signature of this constructor.
        /// </summary>
        /// <param name="parameters">An array of <see cref="MethodTraitParameter"/> instances
        /// representing the method's formal parameters.</param>
        /// <param name="hasRest">True if the method accepts a "rest" parameter.</param>
        protected private void setSignature(MethodTraitParameter[] parameters, bool hasRest) {
            m_params = parameters;
            m_hasRest = hasRest;

            int requiredArgCount = 0;

            for (int i = 0; i < parameters.Length; i++) {
                if (!parameters[i].isOptional)
                    requiredArgCount = i + 1;
            }

            m_requiredArgCount = requiredArgCount;
        }

        /// <summary>
        /// Sets the metadata of this constructor.
        /// </summary>
        /// <param name="metadata">A <see cref="MetadataTagCollection"/> containing the metadata of the constructor.</param>
        protected private void setMetadata(MetadataTagCollection metadata) {
            m_metadata = metadata ?? MetadataTagCollection.empty;
        }

        /// <summary>
        /// Invokes the constructor with the specified arguments and returns the object created by it.
        /// </summary>
        /// <param name="args">The arguments to pass to the constructor.</param>
        /// <returns>The object created by the constructor.</returns>
        /// <exception cref="AVM2Exception">
        /// <list>
        /// <item>
        /// <description>ArgumentError #1063: The length of <paramref name="args"/>is less than the number of required parameters
        /// declared by this constructor, or is greater than the number of parameters and the constructor
        /// does not have a "rest" parameter.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASAny invoke(ReadOnlySpan<ASAny> args) {
            if (args.Length < m_requiredArgCount || (!m_hasRest && args.Length > m_params.Length))
                throw ErrorHelper.createArgCountMismatchError(this, args.Length);

            return m_lazyRuntimeDispatch.value(default(ASAny), args);
        }

        /// <summary>
        /// Gets a <see cref="MetadataTagCollection"/> containing the metadata associated with
        /// this <see cref="ClassConstructor"/>.
        /// </summary>
        public MetadataTagCollection metadata => m_metadata;

        /// <summary>
        /// Returns a string representation of the currend <see cref="ClassConstructor"/>.
        /// </summary>
        /// <returns>A string representation of the currend
        /// <see cref="ClassConstructor"/>.</returns>
        public override string ToString() {
            var sb = new System.Text.StringBuilder();

            sb.Append("function ");
            sb.Append(declaringClass.name.ToString());

            sb.Append('(');
            MethodTraitParameter.paramListToString(m_params, sb);

            if (hasRest) {
                if (paramCount != 0)
                    sb.Append(", ");
                sb.Append("...rest");
            }

            sb.Append(')');
            return sb.ToString();
        }

    }

}
