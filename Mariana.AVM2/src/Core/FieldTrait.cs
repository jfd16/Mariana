using System;
using System.Reflection;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A trait object representing a field in a class.
    /// </summary>
    public class FieldTrait : Trait {

        private FieldInfo m_underlyingFieldInfo;

        private Class m_fieldType;

        private bool m_isReadOnly;

        /// <summary>
        /// A stub method used for dynamically accessing the field at runtime.
        /// </summary>
        ///
        /// <remarks>
        /// This method takes three parameters: The first is a target object (which is ignored for
        /// static fields), the second is the value to set (which is ignored for a "get" operation),
        /// and the third is a boolean which must be set to true for a "set" operation or false for a
        /// "get" operation. It returns the field value for a "get" operation, and undefined for a
        /// "set" operation.
        /// </remarks>
        private LazyInitObject<RuntimeDispatch.FieldStub> m_lazyRuntimeDispatch;

        internal FieldTrait(in QName name, Class declaringClass, ApplicationDomain appDomain, bool isStatic)
            : base(name, declaringClass, appDomain, isStatic)
        {
            m_lazyRuntimeDispatch = new LazyInitObject<RuntimeDispatch.FieldStub>(
                () => RuntimeDispatch.generateFieldStub(this)
            );
        }

        /// <summary>
        /// Gets the underlying <see cref="FieldInfo"/> of the field trait represented by this
        /// instance.
        /// </summary>
        public FieldInfo underlyingFieldInfo => m_underlyingFieldInfo;

        /// <summary>
        /// Gets the type of the field value.
        /// </summary>
        public Class fieldType => m_fieldType;

        /// <summary>
        /// Gets a Boolean value indicating whether the field is read-only.
        /// </summary>
        public bool isReadOnly => m_isReadOnly;

        /// <inheritdoc/>
        public override TraitType traitType => TraitType.FIELD;

        /// <summary>
        /// Sets the underlying <see cref="FieldInfo"/> of the field trait represented by this
        /// instance.
        /// </summary>
        /// <param name="underlyingFieldInfo">The underlying <see cref="FieldInfo"/> for the field trait.</param>
        protected private void setUnderlyingFieldInfo(FieldInfo underlyingFieldInfo) {
            m_underlyingFieldInfo = underlyingFieldInfo;
        }

        /// <summary>
        /// Sets the type of this field.
        /// </summary>
        /// <param name="fieldType">The type of this field.</param>
        protected private void setFieldType(Class fieldType) {
            m_fieldType = fieldType;
        }

        /// <summary>
        /// Sets whether this field is read-only or not.
        /// </summary>
        /// <param name="isReadOnly">Set to true if this field is read-only, otherwise false.</param>
        protected private void setIsReadOnly(bool isReadOnly) {
            m_isReadOnly = isReadOnly;
        }

        /// <summary>
        /// Gets the value of the trait on a specified target object.
        /// </summary>
        /// <param name="target">The target object for which to get the value of the trait. This is
        /// ignored for static and global traits.</param>
        /// <param name="value">The value of the trait on the specified object.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public override BindStatus tryGetValue(ASAny target, out ASAny value) {
            if (!target.isDefined && !isStatic)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            value = m_lazyRuntimeDispatch.value(target, default(ASAny), false);
            return BindStatus.SUCCESS;
        }

        /// <summary>
        /// Sets the value of the trait on a specified target object.
        /// </summary>
        /// <param name="target">The target object for which to set the value of the trait. This is
        /// ignored for static and global traits.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public override BindStatus trySetValue(ASAny target, ASAny value) {
            if (!target.isDefined && !isStatic)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);
            if (isReadOnly)
                return BindStatus.FAILED_READONLY;
            m_lazyRuntimeDispatch.value(target, value, true);
            return BindStatus.SUCCESS;
        }

        /// <summary>
        /// Invokes the trait as a function on the specified target object.
        /// </summary>
        /// <param name="target">The target object on which to invoke the trait.</param>
        /// <param name="receiver">The receiver of the function call.</param>
        /// <param name="args">The arguments to the function call.</param>
        /// <param name="result">The return value of the function call.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public override BindStatus tryInvoke(ASAny target, ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) {
            if (!target.isDefined && !isStatic)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);

            ASObject f = m_lazyRuntimeDispatch.value(target, default(ASAny), false).value;
            if (f != null)
                return f.AS_tryInvoke(receiver, args, out result) ? BindStatus.SUCCESS : BindStatus.FAILED_NOTFUNCTION;

            result = default(ASAny);
            return BindStatus.FAILED_NOTFUNCTION;
        }

        /// <summary>
        /// Invokes the trait as a constructor on the specified target object.
        /// </summary>
        /// <param name="target">The target object on which to invoke the trait as a
        /// constructor.</param>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <param name="result">The object created by the constructor.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public override BindStatus tryConstruct(ASAny target, ReadOnlySpan<ASAny> args, out ASAny result) {
            if (!target.isDefined && !isStatic)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);

            ASObject f = m_lazyRuntimeDispatch.value(target, default(ASAny), false).value;
            if (f != null)
                return f.AS_tryConstruct(args, out result) ? BindStatus.SUCCESS : BindStatus.FAILED_NOTCONSTRUCTOR;

            result = default(ASAny);
            return BindStatus.FAILED_NOTCONSTRUCTOR;
        }

        /// <summary>
        /// Returns a string representation of the currend <see cref="FieldTrait"/>.
        /// </summary>
        /// <returns>A string representation of the currend <see cref="FieldTrait"/>.</returns>
        public override string ToString() {
            var sb = new System.Text.StringBuilder();
            if (isStatic)
                sb.Append("static ");
            sb.Append(isReadOnly ? "var " : "const ");
            sb.Append(name.ToString());
            sb.Append(':').Append(' ');
            sb.Append((fieldType == null) ? "*" : fieldType.name.ToString());
            return sb.ToString();
        }

    }

}
