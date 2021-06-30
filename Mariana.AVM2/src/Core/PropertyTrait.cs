using System;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A trait object representing an accessor property in a class.
    /// </summary>
    public class PropertyTrait : Trait {

        private MethodTrait m_getter;
        private MethodTrait m_setter;

        internal PropertyTrait(
            in QName name,
            Class declaringClass,
            ApplicationDomain appDomain,
            bool isStatic,
            MethodTrait getter,
            MethodTrait setter
        )
            : base(name, declaringClass, appDomain, isStatic)
        {
            m_getter = getter;
            m_setter = setter;
        }

        /// <inheritdoc/>
        public sealed override TraitType traitType => TraitType.PROPERTY;

        /// <summary>
        /// The getter method of the property. For write-only properties, this value is null.
        /// </summary>
        public MethodTrait getter => m_getter;

        /// <summary>
        /// The setter method of the property. For read-only properties, this value is null.
        /// </summary>
        public MethodTrait setter => m_setter;

        /// <summary>
        /// Gets the type of the property's value.
        /// </summary>
        public Class propertyType {
            get {
                if (m_setter == null)
                    return m_getter.returnType;

                var setterParams = m_setter.getParameters();
                if (setterParams.length != 1)
                    return null;

                Class paramType = setterParams[0].type;
                if (m_getter != null && paramType != m_getter.returnType)
                    return null;

                return paramType;
            }
        }

        /// <summary>
        /// Sets the accessor methods of this property.
        /// </summary>
        /// <param name="getter">Getter.</param>
        /// <param name="setter">Setter.</param>
        protected private void setAccessors(MethodTrait getter, MethodTrait setter) {
            m_getter = getter;
            m_setter = setter;
        }

        /// <summary>
        /// Attempts to merge the definitions of two properties.
        /// </summary>
        /// <returns>The merged definition, or null if the merge failed. The merged definition
        /// may be <paramref name="prop"/> itself, or a new instance or <see cref="PropertyTrait"/>.</returns>
        /// <param name="prop">A property trait.</param>
        /// <param name="otherProp">The property trait to be merged into <paramref name="prop"/>.</param>
        /// <param name="declClass">The class declaring or inheriting <paramref name="prop"/> from which
        /// the merge is requested.</param>
        internal static PropertyTrait tryMerge(PropertyTrait prop, PropertyTrait otherProp, Class declClass) {
            if (prop.setter != null) {
                if (prop.getter != null
                    || otherProp.getter == null
                    || (otherProp.setter != null && !prop.setter.isOverride))
                {
                    return null;
                }

                if (prop.declaringClass == declClass) {
                    prop.setAccessors(otherProp.getter, prop.setter);
                    return prop;
                }

                return new PropertyTrait(
                    prop.name, declClass, declClass.applicationDomain, prop.isStatic, otherProp.getter, prop.setter);
            }

            if (prop.getter != null) {
                if (otherProp.setter == null
                    || (otherProp.getter != null && !prop.getter.isOverride))
                {
                    return null;
                }

                if (prop.declaringClass == declClass) {
                    prop.setAccessors(prop.getter, otherProp.setter);
                    return prop;
                }

                return new PropertyTrait(
                    prop.name, declClass, declClass.applicationDomain, prop.isStatic, prop.getter, otherProp.setter);
            }

            return null;
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
            if (target.isUndefined && !isStatic)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);

            MethodTrait getter = this.getter;
            if (getter == null) {
                value = default(ASAny);
                return BindStatus.FAILED_WRITEONLY;
            }

            return getter.tryInvoke(target, Array.Empty<ASAny>(), out value);
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
            if (target.isUndefined && !isStatic)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);

            MethodTrait setter = this.setter;
            if (setter == null)
                return BindStatus.FAILED_READONLY;

            return setter.tryInvoke(target, new[] {value}, out _);
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
            if (target.isUndefined && !isStatic)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);

            MethodTrait getter = this.getter;
            if (getter == null) {
                result = default(ASAny);
                return BindStatus.FAILED_WRITEONLY;
            }

            getter.tryInvoke(target, Array.Empty<ASAny>(), out ASAny func);

            return func.AS_tryInvoke(receiver, args, out result)
                ? BindStatus.SUCCESS
                : BindStatus.FAILED_NOTFUNCTION;
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
            if (target.isUndefined && !isStatic)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);

            MethodTrait getter = this.getter;
            if (getter == null) {
                result = default(ASAny);
                return BindStatus.FAILED_WRITEONLY;
            }

            getter.tryInvoke(target, Array.Empty<ASAny>(), out ASAny func);
            return func.AS_tryConstruct(args, out result)
                ? BindStatus.SUCCESS
                : BindStatus.FAILED_NOTFUNCTION;
        }

        /// <summary>
        /// Returns a string representation of the currend <see cref="PropertyTrait"/>.
        /// </summary>
        /// <returns>A string representation of the currend <see cref="PropertyTrait"/>.</returns>
        public override string ToString() {
            var sb = new System.Text.StringBuilder();
            if (isStatic)
                sb.Append("static ");
            if (setter == null)
                sb.Append("const ");
            sb.Append("property ");
            sb.Append(name.ToString());
            sb.Append(": ");
            sb.Append((propertyType == null) ? "*" : propertyType.name.ToString());
            return sb.ToString();
        }

    }

}
