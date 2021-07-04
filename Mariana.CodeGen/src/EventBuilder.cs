using System;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Mariana.Common;

namespace Mariana.CodeGen {

    /// <summary>
    /// Represents an event defined on a <see cref="TypeBuilder"/>.
    /// </summary>
    public sealed class EventBuilder {

        private TypeBuilder m_declType;

        private string m_name;

        private EventAttributes m_attrs;

        private StringHandle m_nameHandle;

        private EntityHandle m_eventTypeHandle;

        private MethodBuilder? m_add;

        private MethodBuilder? m_remove;

        private MethodBuilder? m_fire;

        private DynamicArray<MethodBuilder> m_otherMethods;

        internal EventBuilder(
            TypeBuilder declaringType,
            string name,
            EventAttributes attrs,
            StringHandle nameHandle,
            EntityHandle eventTypeHandle
        ) {
            m_declType = declaringType;
            m_name = name;
            m_attrs = attrs;
            m_nameHandle = nameHandle;
            m_eventTypeHandle = eventTypeHandle;
        }

        /// <summary>
        /// Returns the name of the event.
        /// </summary>
        public string name => m_name;

        /// <summary>
        /// Returns the <see cref="EventAttributes"/> flags that were provided when this event
        /// was defined with <see cref="TypeBuilder.defineEvent"/>.
        /// </summary>
        public EventAttributes attributes => m_attrs;

        /// <summary>
        /// Returns the <see cref="TypeBuilder"/> on which this event is defined.
        /// </summary>
        public TypeBuilder declaringType => m_declType;

        /// <summary>
        /// Sets the "add" method for this event.
        /// </summary>
        /// <param name="method">The <see cref="MethodBuilder"/> representing the "add" method (should
        /// be declared on the same type as the event), or null if the event should not have
        /// this method.</param>
        public void setAddMethod(MethodBuilder method) {
            if (method != null && method.declaringType != m_declType)
                throw new ArgumentException("Method must be declared on the same type as the event.", nameof(method));

            m_add = method;
        }

        /// <summary>
        /// Sets the "remove" method for this event.
        /// </summary>
        /// <param name="method">The <see cref="MethodBuilder"/> representing the "remove" method (should
        /// be declared on the same type as the event), or null if the event should not have
        /// this method.</param>
        public void setRemoveMethod(MethodBuilder method) {
            if (method != null && method.declaringType != m_declType)
                throw new ArgumentException("Method must be declared on the same type as the event.", nameof(method));

            m_remove = method;
        }

        /// <summary>
        /// Sets the "fire" method for this event.
        /// </summary>
        /// <param name="method">The <see cref="MethodBuilder"/> representing the "fire" method (should
        /// be declared on the same type as the event), or null if the event should not have
        /// this method.</param>
        public void setFireMethod(MethodBuilder method) {
            if (method != null && method.declaringType != m_declType)
                throw new ArgumentException("Method must be declared on the same type as the event.", nameof(method));

            m_fire = method;
        }

        /// <summary>
        /// Adds a method to the list of "other" methods for this event.
        /// </summary>
        /// <param name="method">The <see cref="MethodBuilder"/> representing the method (should be declared
        /// on the same type as the event).</param>
        public void addOtherMethod(MethodBuilder method) {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            if (method.declaringType != m_declType)
                throw new ArgumentException("Method must be declared on the same type as the event.", nameof(method));

            Span<MethodBuilder> otherMethodsSpan = m_otherMethods.asSpan();
            for (int i = 0; i < otherMethodsSpan.Length; i++) {
                if (otherMethodsSpan[i] == method)
                    return;
            }

            m_otherMethods.add(method);
        }

        /// <summary>
        /// Writes the event definition to the dynamic assembly metadata.
        /// </summary>
        /// <param name="builder">The <see cref="MetadataBuilder"/> into which to write the property
        /// definition.</param>
        /// <param name="tokenMapping">The token map for patching event method handles.</param>
        internal void writeMetadata(MetadataBuilder builder, TokenMapping tokenMapping) {
            var eventHandle = builder.AddEvent(attributes, m_nameHandle, m_eventTypeHandle);

            if (m_add != null) {
                builder.AddMethodSemantics(
                    eventHandle, MethodSemanticsAttributes.Adder,
                    (MethodDefinitionHandle)tokenMapping.getMappedHandle(m_add.handle)
                );
            }
            if (m_remove != null) {
                builder.AddMethodSemantics(
                    eventHandle, MethodSemanticsAttributes.Remover,
                    (MethodDefinitionHandle)tokenMapping.getMappedHandle(m_remove.handle)
                );
            }
            if (m_fire != null) {
                builder.AddMethodSemantics(
                    eventHandle, MethodSemanticsAttributes.Raiser,
                    (MethodDefinitionHandle)tokenMapping.getMappedHandle(m_fire.handle)
                );
            }

            Span<MethodBuilder> otherMethods = m_otherMethods.asSpan();
            for (int i = 0; i < otherMethods.Length; i++) {
                builder.AddMethodSemantics(
                    eventHandle, MethodSemanticsAttributes.Other,
                    (MethodDefinitionHandle)tokenMapping.getMappedHandle(otherMethods[i].handle)
                );
            }
        }

    }

}
