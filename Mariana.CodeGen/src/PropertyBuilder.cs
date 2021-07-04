using System;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Mariana.Common;

namespace Mariana.CodeGen {

    /// <summary>
    /// Represents a property defined on a <see cref="TypeBuilder"/>.
    /// </summary>
    public sealed class PropertyBuilder {

        private TypeBuilder m_declType;

        private string m_name;

        private PropertyAttributes m_attrs;

        private StringHandle m_nameHandle;

        private BlobHandle m_sigHandle;

        private object? m_constantValue;

        private MethodBuilder? m_getter;

        private MethodBuilder? m_setter;

        private DynamicArray<MethodBuilder> m_otherMethods;

        internal PropertyBuilder(
            TypeBuilder declaringType,
            string name,
            PropertyAttributes attrs,
            StringHandle nameHandle,
            BlobHandle sigHandle,
            object? constantValue
        ) {
            m_declType = declaringType;
            m_name = name;
            m_attrs = attrs;
            m_nameHandle = nameHandle;
            m_sigHandle = sigHandle;
            m_constantValue = constantValue;
        }

        /// <summary>
        /// Returns the name of the property.
        /// </summary>
        public string name => m_name;

        /// <summary>
        /// Returns the <see cref="PropertyAttributes"/> flags that were provided when this property
        /// was defined with <see cref="TypeBuilder.defineProperty"/>.
        /// </summary>
        public PropertyAttributes attributes => m_attrs;

        /// <summary>
        /// Returns the <see cref="TypeBuilder"/> on which this property is defined.
        /// </summary>
        public TypeBuilder declaringType => m_declType;

        /// <summary>
        /// Sets the getter method for this property.
        /// </summary>
        /// <param name="method">The <see cref="MethodBuilder"/> representing the getter method (should
        /// be declared on the same type as the property), or null if the property should not have
        /// a getter.</param>
        public void setGetMethod(MethodBuilder method) {
            if (method != null && method.declaringType != m_declType)
                throw new ArgumentException("Method must be declared on the same type as the property.", nameof(method));

            m_getter = method;
        }

        /// <summary>
        /// Sets the setter method for this property.
        /// </summary>
        /// <param name="method">The <see cref="MethodBuilder"/> representing the setter method (should
        /// be declared on the same type as the property), or null if the property should not have
        /// a setter.</param>
        public void setSetMethod(MethodBuilder method) {
            if (method != null && method.declaringType != m_declType)
                throw new ArgumentException("Method must be declared on the same type as the property.", nameof(method));

            m_setter = method;
        }

        /// <summary>
        /// Adds a method to the list of "other" methods for this property.
        /// </summary>
        /// <param name="method">The <see cref="MethodBuilder"/> representing the method (should be declared
        /// on the same type as the property).</param>
        public void addOtherMethod(MethodBuilder method) {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            if (method.declaringType != m_declType)
                throw new ArgumentException("Method must be declared on the same type as the property.", nameof(method));

            Span<MethodBuilder> otherMethodsSpan = m_otherMethods.asSpan();
            for (int i = 0; i < otherMethodsSpan.Length; i++) {
                if (otherMethodsSpan[i] == method)
                    return;
            }

            m_otherMethods.add(method);
        }

        /// <summary>
        /// Writes the property definition to the dynamic assembly metadata.
        /// </summary>
        /// <param name="builder">The <see cref="MetadataBuilder"/> into which to write the property
        /// definition.</param>
        /// <param name="tokenMapping">The token map for patching accessor method handles.</param>
        internal void writeMetadata(MetadataBuilder builder, TokenMapping tokenMapping) {
            var propHandle = builder.AddProperty(attributes, m_nameHandle, m_sigHandle);

            if ((attributes & PropertyAttributes.HasDefault) != 0)
                builder.AddConstant(propHandle, m_constantValue);

            if (m_getter != null) {
                builder.AddMethodSemantics(
                    propHandle, MethodSemanticsAttributes.Getter,
                    (MethodDefinitionHandle)tokenMapping.getMappedHandle(m_getter.handle)
                );
            }

            if (m_setter != null) {
                builder.AddMethodSemantics(
                    propHandle, MethodSemanticsAttributes.Setter,
                    (MethodDefinitionHandle)tokenMapping.getMappedHandle(m_setter.handle)
                );
            }

            Span<MethodBuilder> otherMethods = m_otherMethods.asSpan();
            for (int i = 0; i < otherMethods.Length; i++) {
                builder.AddMethodSemantics(
                    propHandle, MethodSemanticsAttributes.Other,
                    (MethodDefinitionHandle)tokenMapping.getMappedHandle(otherMethods[i].handle)
                );
            }
        }

    }

}
