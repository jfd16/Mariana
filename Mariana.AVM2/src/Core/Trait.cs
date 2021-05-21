using System;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The base class for all traits (fields, methods, properties, constants and classes) in the
    /// AVM2.
    /// </summary>
    public abstract class Trait {

        private readonly QName m_name;

        private Class m_declaringClass;

        private ApplicationDomain m_appDomain;

        private bool m_isStatic;

        private MetadataTagCollection m_metadata;

        /// <summary>
        /// Creates a new trait object.
        /// </summary>
        ///
        /// <param name="name">The qualified name of the trait.</param>
        /// <param name="declaringClass">The class on which the trait is declared. (May be
        /// null)</param>
        /// <param name="appDomain">The application domain in which the trait is declared.</param>
        /// <param name="isStatic">Set this to true for static and global traits, and false for
        /// instance traits.</param>
        internal Trait(in QName name, Class declaringClass, ApplicationDomain appDomain, bool isStatic) {
            m_name = name;
            m_declaringClass = declaringClass;
            m_appDomain = appDomain;
            m_isStatic = isStatic;
            m_metadata = MetadataTagCollection.empty;
        }

        /// <summary>
        /// Gets the qualified name of the trait.
        /// </summary>
        public ref readonly QName name => ref m_name;

        /// <summary>
        /// The class on which the trait is declared.
        /// </summary>
        ///
        /// <remarks>
        /// For traits that are not declared at the class level (for instance, global traits, closure
        /// methods defined in ABC scripts and standalone methods created by
        /// <see cref="MethodTrait.createNativeMethod" qualifyHint="true"/>), this value is
        /// null.
        /// </remarks>
        public Class declaringClass => m_declaringClass;

        /// <summary>
        /// A value indicating whether this trait is a static trait (a trait not associated with an
        /// instance of a class). This is true for both static traits declared at the class level and
        /// global traits (which are not part of any class).
        /// </summary>
        public bool isStatic => m_isStatic;

        /// <summary>
        /// Gets the application domain in which the trait is declared.
        /// </summary>
        public ApplicationDomain applicationDomain => m_appDomain;

        /// <summary>
        /// Gets a <see cref="MetadataTagCollection"/> containing the metadata associated with
        /// the trait.
        /// </summary>
        public MetadataTagCollection metadata => m_metadata;

        /// <summary>
        /// Gets the type of the trait represented by this instance.
        /// </summary>
        public abstract TraitType traitType {
            get;
        }

        /// <summary>
        /// Gets the value of the trait on a specified target object.
        /// </summary>
        /// <param name="target">The target object for which to get the value of the trait. This is
        /// ignored for static and global traits.</param>
        /// <param name="value">The value of the trait on the specified object.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public abstract BindStatus tryGetValue(ASAny target, out ASAny value);

        /// <summary>
        /// Sets the value of the trait on a specified target object.
        /// </summary>
        /// <param name="target">The target object for which to set the value of the trait. This is
        /// ignored for static and global traits.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public abstract BindStatus trySetValue(ASAny target, ASAny value);

        /// <summary>
        /// Invokes the trait as a function on the specified target object.
        /// </summary>
        /// <param name="target">The target object on which to invoke the trait. This is also the
        /// receiver of the function call.</param>
        /// <param name="args">The arguments to the function call.</param>
        /// <param name="result">The return value of the function call.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public BindStatus tryInvoke(ASAny target, ReadOnlySpan<ASAny> args, out ASAny result) =>
            tryInvoke(target, target, args, out result);

        /// <summary>
        /// Invokes the trait as a function on the specified target object.
        /// </summary>
        /// <param name="target">The target object on which to invoke the trait.</param>
        /// <param name="receiver">The receiver of the function call.</param>
        /// <param name="args">The arguments to the function call.</param>
        /// <param name="result">The return value of the function call.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public abstract BindStatus tryInvoke(ASAny target, ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result);

        /// <summary>
        /// Invokes the trait as a constructor on the specified target object.
        /// </summary>
        /// <param name="target">The target object on which to invoke the trait as a
        /// constructor.</param>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <param name="result">The object created by the constructor.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public abstract BindStatus tryConstruct(ASAny target, ReadOnlySpan<ASAny> args, out ASAny result);

        /// <summary>
        /// Gets the value of the trait on a specified target object.
        /// </summary>
        /// <param name="target">The target object for which to get the value of the trait. This is
        /// ignored for static and global traits.</param>
        /// <returns>The value of the trait on the specified target object.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public ASAny getValue(ASAny target) {
            BindStatus bindStatus = tryGetValue(target, out ASAny value);
            if (bindStatus != BindStatus.SUCCESS) {
                throw ErrorHelper.createBindingError(
                    (m_declaringClass == null) ? "global" : m_declaringClass.name.ToString(), name.ToString(), bindStatus);
            }
            return value;
        }

        /// <summary>
        /// Gets the value of a static or global trait.
        /// </summary>
        /// <returns>The value of the static or global trait.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>Error #10045: This method is called on an instance trait.</description></item>
        /// <item><description>Any error: An error occurs during the operation.</description></item>
        /// </list>
        /// </exception>
        public ASAny getValue() {
            if (!m_isStatic)
                throw ErrorHelper.createError(ErrorCode.MARIANA__TRAIT_INVOKE_NO_RECV_NONSTATIC);
            return getValue(ASAny.@null);
        }

        /// <summary>
        /// Sets the value of the trait on a specified target object.
        /// </summary>
        /// <param name="target">The target object for which to set the value of the trait. This is
        /// ignored for static and global traits.</param>
        /// <param name="value">The value to set.</param>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public void setValue(ASAny target, ASAny value) {
            BindStatus bindStatus = trySetValue(target, value);
            if (bindStatus != BindStatus.SUCCESS) {
                throw ErrorHelper.createBindingError(
                    (m_declaringClass == null) ? "global" : m_declaringClass.name.ToString(), name.ToString(), bindStatus);
            }
        }

        /// <summary>
        /// Sets the value of a static or global trait.
        /// </summary>
        /// <param name="value">The value to set.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>Error #10045: This method is called on an instance trait.</description></item>
        /// <item><description>Any error: An error occurs during the operation.</description></item>
        /// </list>
        /// </exception>
        public void setValue(ASAny value) {
            if (!m_isStatic)
                throw ErrorHelper.createError(ErrorCode.MARIANA__TRAIT_INVOKE_NO_RECV_NONSTATIC);
            setValue(ASAny.@null, value);
        }

        /// <summary>
        /// Invokes the trait as a function on the specified target object.
        /// </summary>
        /// <param name="target">The target object on which to invoke the trait. This is also the
        /// receiver of the function call.</param>
        /// <param name="args">The arguments to the function call.</param>
        /// <returns>The return value of the function call.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public ASAny invoke(ASAny target, ReadOnlySpan<ASAny> args) => invoke(target, target, args);

        /// <summary>
        /// Invokes the trait as a function on the specified target object.
        /// </summary>
        /// <param name="target">The target object on which to invoke the trait.</param>
        /// <param name="receiver">The receiver of the function call.</param>
        /// <param name="args">The arguments to the function call.</param>
        /// <returns>The return value of the function call.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public ASAny invoke(ASAny target, ASAny receiver, ReadOnlySpan<ASAny> args) {
            BindStatus bindStatus = tryInvoke(target, receiver, args, out ASAny returnValue);
            if (bindStatus != BindStatus.SUCCESS) {
                throw ErrorHelper.createBindingError(
                    (m_declaringClass == null) ? "global" : m_declaringClass.name.ToString(), name.ToString(), bindStatus);
            }
            return returnValue;
        }

        /// <summary>
        /// Invokes a static or global trait as a function.
        /// </summary>
        /// <param name="args">The arguments to the function call.</param>
        /// <returns>The return value of the function call.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>Error #10045: This method is called on an instance trait.</description></item>
        /// <item><description>Any error: An error occurs during the operation.</description></item>
        /// </list>
        /// </exception>
        public ASAny invoke(ReadOnlySpan<ASAny> args) {
            if (!m_isStatic)
                throw ErrorHelper.createError(ErrorCode.MARIANA__TRAIT_INVOKE_NO_RECV_NONSTATIC);
            return invoke(ASAny.@null, args);
        }

        /// <summary>
        /// Invokes the trait as a constructor on the specified target object.
        /// </summary>
        /// <param name="target">The target object on which to invoke the trait as a
        /// constructor.</param>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <returns>The object created by the constructor.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public ASAny construct(ASAny target, ReadOnlySpan<ASAny> args) {
            BindStatus bindStatus = tryConstruct(target, args, out ASAny returnValue);
            if (bindStatus != BindStatus.SUCCESS) {
                throw ErrorHelper.createBindingError(
                    (m_declaringClass == null) ? "global" : m_declaringClass.name.ToString(), name.ToString(), bindStatus);
            }
            return returnValue;
        }

        /// <summary>
        /// Invokes a static or global trait as a constructor.
        /// </summary>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <returns>The object created by the constructor.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>Error #10045: This method is called on an instance trait.</description></item>
        /// <item><description>Any error: An error occurs during the operation.</description></item>
        /// </list>
        /// </exception>
        public ASAny construct(ReadOnlySpan<ASAny> args) {
            if (!m_isStatic)
                throw ErrorHelper.createError(ErrorCode.MARIANA__TRAIT_INVOKE_NO_RECV_NONSTATIC);
            return construct(ASAny.@null, args);
        }

        /// <summary>
        /// Sets the metadata of this trait.
        /// </summary>
        /// <param name="metadata">A <see cref="MetadataTagCollection"/> containing the metadata of the trait.</param>
        protected private void setMetadata(MetadataTagCollection metadata) {
            m_metadata = metadata ?? MetadataTagCollection.empty;
        }

    }

}
