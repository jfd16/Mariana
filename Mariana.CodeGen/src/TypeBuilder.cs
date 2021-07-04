using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Mariana.CodeGen.IL;
using Mariana.Common;

namespace Mariana.CodeGen {

    /// <summary>
    /// Represents a type being emitted in an <see cref="AssemblyBuilder"/>.
    /// </summary>
    public sealed class TypeBuilder {

        private struct _GenParam {
            public string name;
            public GenericParameterAttributes attrs;
            public EntityHandle[] constraints;
        }

        private MetadataContext m_metadataContext;

        private TypeName m_name;

        private EntityHandle m_handle;

        private TypeSignature m_signature;

        private EntityHandle m_parent;

        private TypeAttributes m_typeAttrs;

        private bool m_isValueType;

        private _GenParam[] m_genParams;

        private DynamicArray<EntityHandle> m_interfaces;

        private DynamicArray<FieldBuilder> m_fieldDefs;

        private DynamicArray<MethodBuilder> m_methodDefs;

        private DynamicArray<PropertyBuilder> m_propertyDefs;

        private DynamicArray<EventBuilder> m_eventDefs;

        private Dictionary<EntityHandle, EntityHandle> m_methodImplDict = new Dictionary<EntityHandle, EntityHandle>();

        internal TypeBuilder(
            TypeName name, TypeAttributes attrs, bool isValueType, int genParamCount,
            MetadataContext metadataContext, EntityHandle handle)
        {
            m_name = name;
            m_typeAttrs = attrs;
            m_isValueType = isValueType;
            m_genParams = (genParamCount != 0) ? new _GenParam[genParamCount] : Array.Empty<_GenParam>();
            m_handle = handle;
            m_metadataContext = metadataContext;
            m_signature = isValueType ? TypeSignature.forValueType(handle) : TypeSignature.forClassType(handle);
        }

        /// <summary>
        /// Returns a handle that can be used to refer to this type definition in metadata
        /// and IL code in the same assembly.
        /// </summary>
        public EntityHandle handle => m_handle;

        /// <summary>
        /// Returns a <see cref="TypeSignature"/> instance representing the signature of this
        /// type definition.
        /// </summary>
        public TypeSignature signature => m_signature;

        /// <summary>
        /// Returns the <see cref="TypeAttributes"/> flags that were provided when the type was
        /// defined using <see cref="AssemblyBuilder.defineType"/>.
        /// </summary>
        public TypeAttributes attributes => m_typeAttrs;

        /// <summary>
        /// Returns a <see cref="TypeName"/> instance representing the name of the type.
        /// </summary>
        public TypeName typeName => m_name;

        /// <summary>
        /// Returns the number of generic type parameters for the type represented by this
        /// <see cref="TypeBuilder"/>. For a non-generic type, returns zero.
        /// </summary>
        public int genericParamCount => m_genParams.Length;

        /// <summary>
        /// Returns true if this <see cref="TypeBuilder"/> represents a value type.
        /// </summary>
        public bool isValueType => m_isValueType;

        internal MetadataContext metadataContext => m_metadataContext;

        /// <summary>
        /// Sets the base type for the type represented by this <see cref="TypeBuilder"/>.
        /// </summary>
        /// <param name="parentHandle">A handle to the base type.</param>
        ///
        /// <exception cref="ArgumentException"><paramref name="parentHandle"/> is a null handle, or
        /// not a handle to a TypeDef, TypeRef or TypeSpec.</exception>
        /// <exception cref="InvalidOperationException">This <see cref="TypeBuilder"/> represents an
        /// interface type.</exception>
        ///
        /// <remarks>
        /// If a base type has not been set for this <see cref="TypeBuilder"/>, the default base
        /// type will be <see cref="ValueType"/> if this <see cref="TypeBuilder"/> represents
        /// a value type, or <see cref="Object"/> otherwise. If this <see cref="TypeBuilder"/>
        /// represents a value type, any base type other than <see cref="ValueType"/> or
        /// <see cref="Enum"/> will result in an invalid assembly.
        /// </remarks>
        public void setParent(EntityHandle parentHandle) {
            if ((m_typeAttrs & TypeAttributes.Interface) != 0)
                throw new InvalidOperationException("An interface type cannot have a parent type.");

            _validateParentOrInterfaceHandle(parentHandle);
            m_parent = parentHandle;
        }

        /// <summary>
        /// Adds an interface to the list of interfaces implemented by the type represented by
        /// this <see cref="TypeBuilder"/>.
        /// </summary>
        /// <param name="interfaceHandle">A handle to the interface type.</param>
        /// <exception cref="ArgumentException"><paramref name="interfaceHandle"/> is a null handle, or
        /// not a handle to a TypeDef, TypeRef or TypeSpec.</exception>
        public void addInterface(EntityHandle interfaceHandle) {
            _validateParentOrInterfaceHandle(interfaceHandle);

            if (m_interfaces.asSpan().IndexOf(interfaceHandle) == -1)
                m_interfaces.add(interfaceHandle);
        }

        /// <summary>
        /// Defines a field on the dynamic type represented by this <see cref="TypeBuilder"/>.
        /// </summary>
        ///
        /// <param name="name">The name of the field.</param>
        /// <param name="fieldType">A <see cref="TypeSignature"/> representing the field's type.</param>
        /// <param name="attrs">The attributes of the field, as a set of bit flags from
        /// <see cref="FieldAttributes"/>.</param>
        /// <param name="constantValue">If <paramref name="attrs"/> has the <see cref="FieldAttributes.Literal"/>
        /// flag set, set this to the constant value of the field. If the literal flag is not set, this
        /// argument is ignored.</param>
        ///
        /// <returns>A <see cref="FieldBuilder"/> representing the defined field.</returns>
        ///
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item><description><paramref name="name"/> is null or the empty string.</description></item>
        /// <item><description><paramref name="attrs"/> has the <see cref="FieldAttributes.Literal"/> flag set
        /// but not <see cref="FieldAttributes.Static"/>.</description></item>
        /// </list>
        /// </exception>
        /// <exception cref="NotSupportedException"><paramref name="attrs"/> has the
        /// <see cref="FieldAttributes.HasFieldRVA"/> or <see cref="FieldAttributes.HasFieldMarshal"/>
        /// flag set.</exception>
        public FieldBuilder defineField(
            string name, in TypeSignature fieldType, FieldAttributes attrs, object? constantValue = null)
        {
            if (name == null || name.Length == 0)
                throw new ArgumentException("Name cannot be null or empty.", nameof(name));

            _validateFieldAttributes(attrs);

            var fieldHandle = m_metadataContext.getNewVirtualFieldDefHandle();
            var fieldNameHandle = m_metadataContext.getStringHandle(name);
            var fieldSigHandle = m_metadataContext.createFieldSignature(fieldType);

            var fieldBuilder = new FieldBuilder(
                this, name, attrs, fieldHandle, fieldNameHandle, fieldSigHandle, constantValue);

            m_fieldDefs.add(fieldBuilder);

            return fieldBuilder;
        }

        /// <summary>
        /// Defines a method on the dynamic type represented by this <see cref="TypeBuilder"/>.
        /// </summary>
        ///
        /// <param name="name">The name of the method.</param>
        /// <param name="attributes">The attributes of the method, as a set of bit flags from
        /// <see cref="MethodAttributes"/>.</param>
        /// <param name="returnType">A <see cref="TypeSignature"/> representing the method's return type.</param>
        /// <param name="paramTypes">A span of <see cref="TypeSignature"/> instances representing
        /// the types of the method's parameters.</param>
        /// <param name="genParamCount">The number of type parameters in the method, or zero if the
        /// method should not be generic.</param>
        ///
        /// <returns>A <see cref="MethodBuilder"/> representing the defined method and that can
        /// be used to emit its definition.</returns>
        ///
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item><description><paramref name="name"/> is null or the empty string.</description></item>
        /// <item><description><paramref name="attributes"/> contains invalid attribute combinations (such as
        /// <see cref="MethodAttributes.Virtual"/> and <see cref="MethodAttributes.Static"/>
        /// set together), or this <see cref="TypeBuilder"/> represents an interface and
        /// <paramref name="attributes"/> does not have either the <see cref="MethodAttributes.Static"/>
        /// or <see cref="MethodAttributes.Abstract"/> flag set.</description></item>
        /// </list>
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="paramTypes"/> is greater
        /// than 65535.</exception>
        /// <exception cref="NotSupportedException"><paramref name="attributes"/> has the
        /// <see cref="MethodAttributes.PinvokeImpl"/> flag set.</exception>
        public MethodBuilder defineMethod(
            string name, MethodAttributes attributes, in TypeSignature returnType,
            ReadOnlySpan<TypeSignature> paramTypes = default, int genParamCount = 0)
        {
            if (name == null || name.Length == 0)
                throw new ArgumentException("Name cannot be null or empty.", nameof(name));
            if (paramTypes.Length > 65535)
                throw new ArgumentOutOfRangeException("Number of method parameters must not exceed 65535.", nameof(paramTypes));

            _validateMethodAttributes(attributes);

            CallingConventions callConv = CallingConventions.Standard;
            if ((attributes & MethodAttributes.Static) == 0)
                callConv |= CallingConventions.HasThis;

            var methodHandle = m_metadataContext.getNewVirtualMethodDefHandle();
            var methodNameHandle = m_metadataContext.getStringHandle(name);

            // Since the ExplicitThis calling convention is never used, we don't need to
            // pass the correct signature for thisType, just use the default.

            var methodSigHandle = m_metadataContext.createMethodSignature(
                callConv, genParamCount, default(TypeSignature), returnType,
                paramTypes, ReadOnlySpan<TypeSignature>.Empty
            );

            var methodBuilder = new MethodBuilder(
                this, name, attributes, methodHandle, methodNameHandle, methodSigHandle, paramTypes.Length, genParamCount);

            m_methodDefs.add(methodBuilder);

            var stackChangeInfo = new MethodStackChangeInfo(
                argsPopped: paramTypes.Length,
                popsThis: (callConv & CallingConventions.HasThis) != 0,
                hasReturn: !returnType.Equals(TypeSignature.forPrimitiveType(PrimitiveTypeCode.Void))
            );
            m_metadataContext.setMethodStackChange(methodHandle, stackChangeInfo);

            return methodBuilder;
        }

        private void _validateParentOrInterfaceHandle(EntityHandle handle) {
            if (handle.IsNil)
                throw new ArgumentException("Parent or interface handle cannot be null.");

            HandleKind kind = handle.Kind;
            if (kind != HandleKind.TypeDefinition && kind != HandleKind.TypeReference
                && kind != HandleKind.TypeSpecification)
            {
                throw new ArgumentException("Parent or interface handle must be a TypeDef, TypeRef or TypeSpec.");
            }
        }

        private void _validateFieldAttributes(FieldAttributes attributes) {
            if ((attributes & (FieldAttributes.HasFieldMarshal | FieldAttributes.HasFieldRVA)) != 0)
                throw new NotSupportedException("HasFieldMarshal and HasFieldRVA field attributes are not supported.");

            if ((attributes & FieldAttributes.Literal) != 0 && (attributes & FieldAttributes.Static) == 0)
                throw new ArgumentException("The Literal attribute can only be used on static fields.", nameof(attributes));
        }

        private void _validateMethodAttributes(MethodAttributes attributes) {
             if ((attributes & MethodAttributes.Virtual) != 0) {
                if ((attributes & MethodAttributes.Static) != 0)
                    throw new ArgumentException("Static and Virtual attributes cannot be set together.", nameof(attributes));
            }
            else {
                if ((attributes & MethodAttributes.Final) != 0 || (attributes & MethodAttributes.Abstract) != 0) {
                    throw new ArgumentException(
                        "Final or Abstract attribute cannot be set when Virtual is not set.", nameof(attributes));
                }
            }

            if ((m_typeAttrs & TypeAttributes.Interface) != 0
                && (attributes & MethodAttributes.Static) == 0
                && (attributes & MethodAttributes.Abstract) == 0)
            {
                throw new ArgumentException("An instance method on an interface must be abstract.", nameof(attributes));
            }

            if ((attributes & MethodAttributes.PinvokeImpl) != 0)
                throw new NotSupportedException("The PInvokeImpl method attribute is not supported.");
        }

        /// <summary>
        /// Defines a property on the dynamic type represented by this <see cref="TypeBuilder"/>.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="attributes">The attributes of the property, as a set of bit flags from
        /// <see cref="PropertyAttributes"/>.</param>
        /// <param name="isStatic">True if the property should be static, false if it should be an
        /// instance property.</param>
        /// <param name="propertyType">A <see cref="TypeSignature"/> representing the type of the property.</param>
        /// <param name="paramTypes">A span of <see cref="TypeSignature"/> instances representing
        /// the types of the property's index parameters. If the property should not have index
        /// parameters, pass an empty span.</param>
        /// <param name="constantValue">A constant value to associate with this property in the
        /// assembly metadata.</param>
        ///
        /// <returns>A <see cref="PropertyBuilder"/> representing the defined property and that can
        /// be used to emit its definition.</returns>
        ///
        /// <exception cref="ArgumentException"><paramref name="name"/> is null or the empty string.</exception>
        public PropertyBuilder defineProperty(
            string name,
            PropertyAttributes attributes,
            bool isStatic,
            in TypeSignature propertyType,
            ReadOnlySpan<TypeSignature> paramTypes = default,
            object? constantValue = null
        ) {
            if (name == null || name.Length == 0)
                throw new ArgumentException("Name cannot be null or empty.", nameof(name));

            var propNameHandle = m_metadataContext.getStringHandle(name);
            var propSigHandle = m_metadataContext.createPropertySignature(isStatic, propertyType, paramTypes);

            var propBuilder = new PropertyBuilder(
                this, name, attributes, propNameHandle, propSigHandle, constantValue);

            m_propertyDefs.add(propBuilder);

            return propBuilder;
        }

        /// <summary>
        /// Defines an event on the dynamic type represented by this <see cref="TypeBuilder"/>.
        /// </summary>
        ///
        /// <param name="name">The name of the event.</param>
        /// <param name="attributes">The attributes of the event, as a set of bit flags from
        /// <see cref="EventAttributes"/>.</param>
        /// <param name="eventType">A handle for the type of the event.</param>
        ///
        /// <returns>A <see cref="EventBuilder"/> representing the defined property and that can
        /// be used to emit its definition.</returns>
        ///
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item><description><paramref name="name"/> is null or the empty string.</description></item>
        /// <item><description><paramref name="eventType"/> is the null handle, or is not a TypeDef, TypeRef or TypeSpec.</description></item>
        /// </list>
        /// </exception>
        public EventBuilder defineEvent(string name, EventAttributes attributes, EntityHandle eventType) {
            if (name == null || name.Length == 0)
                throw new ArgumentException("Name cannot be null or empty.", nameof(name));

            if (eventType.IsNil)
                throw new ArgumentException("Handle cannot be null.", nameof(eventType));

            HandleKind eventTypeKind = eventType.Kind;
            if (eventTypeKind != HandleKind.TypeDefinition
                && eventTypeKind != HandleKind.TypeReference
                && eventTypeKind != HandleKind.TypeSpecification)
            {
                throw new ArgumentException("Event type handle must refer to a TypeDef, TypeRef or TypeSpec.", nameof(eventType));
            }

            var eventNameHandle = m_metadataContext.getStringHandle(name);
            var eventBuilder = new EventBuilder(this, name, attributes, eventNameHandle, eventType);

            m_eventDefs.add(eventBuilder);

            return eventBuilder;
        }

        /// <summary>
        /// Defines a constructor on the dynamic type represented by this <see cref="TypeBuilder"/>.
        /// </summary>
        ///
        /// <param name="attributes">The attributes of the method, as a set of bit flags from
        /// <see cref="MethodAttributes"/>. Set the <see cref="MethodAttributes.Static"/> flag
        /// to define a static constructor; do not set it to define an instance constructor.</param>
        /// <param name="paramTypes">A span of <see cref="TypeSignature"/> instances representing
        /// the types of the method's parameters.</param>
        ///
        /// <returns>A <see cref="MethodBuilder"/> representing the defined constructor method and which can
        /// be used to emit its definition.</returns>
        ///
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item><description><paramref name="attributes"/> has the <see cref="MethodAttributes.Static"/>
        /// flag set, and <paramref name="paramTypes"/> is not empty.</description></item>
        /// <item><description><paramref name="attributes"/> does not have the <see cref="MethodAttributes.Static"/>
        /// flag set, and this <see cref="TypeBuilder"/> represents an interface type.</description></item>
        /// <item><description><paramref name="attributes"/> has the <see cref="MethodAttributes.Virtual"/>
        /// flag set.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// The <see cref="MethodAttributes.SpecialName"/> and <see cref="MethodAttributes.RTSpecialName"/>
        /// flags, which are required for constructors, need not be set in the <paramref name="attributes"/>
        /// argument as they will be added by this method.
        /// </remarks>
        public MethodBuilder defineConstructor(MethodAttributes attributes, ReadOnlySpan<TypeSignature> paramTypes = default) {
            if ((attributes & MethodAttributes.Virtual) != 0)
                throw new ArgumentException("The Virtual attribute cannot be set for constructors.", nameof(attributes));

            string methodName;

            if ((attributes & MethodAttributes.Static) != 0) {
                if (paramTypes.Length != 0)
                    throw new ArgumentException("Static constructors must not have parameters.", nameof(paramTypes));
                methodName = ConstructorInfo.TypeConstructorName;
            }
            else {
                if ((m_typeAttrs & TypeAttributes.Interface) != 0)
                    throw new ArgumentException("The Static attribute must be set for a constructor on an interface type.", nameof(attributes));
                methodName = ConstructorInfo.ConstructorName;
            }

            attributes |= MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;

            return defineMethod(methodName, attributes, TypeSignature.forPrimitiveType(PrimitiveTypeCode.Void), paramTypes);
        }

        /// <summary>
        /// Defines the name, attributes and constraints of a generic type parameter on the type
        /// represented by this <see cref="TypeBuilder"/>.
        /// </summary>
        ///
        /// <param name="position">The zero-based position of the type parameter.</param>
        /// <param name="name">The name of the type parameter at <paramref name="position"/>.</param>
        /// <param name="attributes">The attributes of the type parameter at <paramref name="position"/>,
        /// as a set of bit flags from <see cref="GenericParameterAttributes"/>.</param>
        /// <param name="constraints">The base class and/or interface constraints for the generic
        /// parameter at <paramref name="position"/>, as a span of handles to the constraint
        /// types. If there should not be any constraints on the type parameter, pass an empty
        /// span.</param>
        ///
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative
        /// or not less than the number of generic type parameters declared by this type.</exception>
        /// <exception cref="ArgumentException"><paramref name="name"/> is null or the empty string,
        /// or one of the handles in <paramref name="constraints"/> does not refer to a TypeDef,
        /// TypeRef or TypeSpec.</exception>
        public void defineGenericParameter(
            int position,
            string name,
            GenericParameterAttributes attributes = GenericParameterAttributes.None,
            ReadOnlySpan<EntityHandle> constraints = default
        ) {
            if ((uint)position >= (uint)m_genParams.Length)
                throw new ArgumentOutOfRangeException(nameof(position));

            if (name == null || name.Length == 0)
                throw new ArgumentException("Generic parameter name must not be null or an empty string.", nameof(name));

            for (int i = 0; i < constraints.Length; i++) {
                HandleKind handleKind = constraints[i].Kind;

                if (constraints[i].IsNil
                    || (handleKind != HandleKind.TypeDefinition
                        && handleKind != HandleKind.TypeReference
                        && handleKind != HandleKind.TypeSpecification))
                {
                    throw new ArgumentException(
                        "Generic parameter constraint handle must be a TypeDef, TypeRef or TypeSpec.", $"{nameof(constraints)}[{i}]");
                }
            }

            ref var genParam = ref m_genParams[position];
            genParam.name = name;
            genParam.attrs = attributes;
            genParam.constraints = constraints.IsEmpty ? Array.Empty<EntityHandle>() : constraints.ToArray();
        }

        /// <summary>
        /// Declares that a method defined by this type supplies the implementation for an
        /// interface or abstract method, or overrides a base class method.
        /// </summary>
        ///
        /// <param name="methodDecl">A handle to the interface or abstract method to be
        /// implemented, or the base class virtual method to be overridden.</param>
        /// <param name="methodImpl">A handle to the method that supplies the method body
        /// for <paramref name="methodDecl"/>. This must refer to a method defined on
        /// this type or an ancestor of it.</param>
        ///
        /// <exception cref="ArgumentException"><paramref name="methodDecl"/> or
        /// <paramref name="methodImpl"/> is a null handles or refers to a table
        /// other than MethodDef or MemberRef, or an implementation for
        /// <paramref name="methodDecl"/> was defined on this type by an earlier
        /// call to this method.</exception>
        ///
        /// <remarks>
        /// Declaring a method implementation explicitly using this method is only necessary
        /// if the declaration and implementation methods have different names.
        /// </remarks>
        public void defineMethodImpl(EntityHandle methodDecl, EntityHandle methodImpl) {
            HandleKind declKind = methodDecl.Kind, implKind = methodImpl.Kind;

            if (methodDecl.IsNil || (declKind != HandleKind.MethodDefinition && declKind != HandleKind.MemberReference))
                throw new ArgumentException("Method handle must be a MethodDef or MemberRef.", nameof(methodDecl));

            if (methodImpl.IsNil || (implKind != HandleKind.MethodDefinition && implKind != HandleKind.MemberReference))
                throw new ArgumentException("Method handle must be a MethodDef or MemberRef.", nameof(methodImpl));

            bool added = m_methodImplDict.TryAdd(methodDecl, methodImpl);
            if (!added)
                throw new ArgumentException("An implementation is already defined for the method.", nameof(methodDecl));
        }

        /// <summary>
        /// Writes the type definition and member definitions for all declared members to
        /// the assembly metadata. MethodImpl entries and heneric parameter information for
        /// generic types is not written.
        /// </summary>
        /// <param name="builder">The <see cref="MetadataBuilder"/> into which to write the
        /// type and member definitions.</param>
        /// <param name="tokenMapping">The token mapping into which the associations of virtual handles
        /// for field and method definitions with their real handles (which will be determined when their
        /// metadata is written) should be stored.</param>
        internal void writeTypeMetadata(MetadataBuilder builder, TokenMapping tokenMapping) {
            var handle = (TypeDefinitionHandle)m_handle;
            EntityHandle parentHandle = m_parent;

            if (parentHandle.IsNil && (m_typeAttrs & TypeAttributes.Interface) == 0)
                parentHandle = m_metadataContext.getTypeHandle(m_isValueType ? typeof(ValueType) : typeof(object));

            var typeName = m_metadataContext.getStringHandle(m_name.name);
            var typeNamespace = m_metadataContext.getStringHandle(m_name.ns);

            _sortInterfacesByCodedIndex();
            Span<EntityHandle> interfaces = m_interfaces.asSpan();

            Span<FieldBuilder> fieldDefs = m_fieldDefs.asSpan();
            Span<MethodBuilder> methodDefs = m_methodDefs.asSpan();
            Span<PropertyBuilder> propertyDefs = m_propertyDefs.asSpan();
            Span<EventBuilder> eventDefs = m_eventDefs.asSpan();

            int firstFieldRow = builder.GetRowCount(TableIndex.Field) + 1;
            int firstMethodRow = builder.GetRowCount(TableIndex.MethodDef) + 1;

            builder.AddTypeDefinition(
                m_typeAttrs,
                typeNamespace,
                typeName,
                parentHandle,
                MetadataTokens.FieldDefinitionHandle(firstFieldRow),
                MetadataTokens.MethodDefinitionHandle(firstMethodRow)
            );

            for (int i = 0; i < interfaces.Length; i++)
                builder.AddInterfaceImplementation(handle, interfaces[i]);

            for (int i = 0; i < fieldDefs.Length; i++) {
                var fieldDef = fieldDefs[i];
                tokenMapping.mapFieldDef(MetadataTokens.GetRowNumber(fieldDef.handle), firstFieldRow + i);
                fieldDef.writeMetadata(builder);
            }

            for (int i = 0; i < methodDefs.Length; i++) {
                var methodDef = methodDefs[i];
                tokenMapping.mapMethodDef(MetadataTokens.GetRowNumber(methodDef.handle), firstMethodRow + i);
                methodDef.writeMetadata(builder);
            }

            if (propertyDefs.Length != 0) {
                builder.AddPropertyMap(
                    handle, MetadataTokens.PropertyDefinitionHandle(builder.GetRowCount(TableIndex.Property) + 1));

                for (int i = 0; i < propertyDefs.Length; i++)
                    propertyDefs[i].writeMetadata(builder, tokenMapping);
            }

            if (eventDefs.Length != 0) {
                builder.AddEventMap(
                    handle, MetadataTokens.EventDefinitionHandle(builder.GetRowCount(TableIndex.Event) + 1));

                for (int i = 0; i < eventDefs.Length; i++)
                    eventDefs[i].writeMetadata(builder, tokenMapping);
            }
        }

        private void _sortInterfacesByCodedIndex() {
            var interfaceCodedIndices = new int[m_interfaces.length];
            for (int i = 0; i < interfaceCodedIndices.Length; i++)
                interfaceCodedIndices[i] = CodedIndex.TypeDefOrRefOrSpec(m_interfaces[i]);

            // We have to use getUnderlyingArray() until there is a Sort method for spans.
            Array.Sort(interfaceCodedIndices, m_interfaces.getUnderlyingArray(), 0, m_interfaces.length);
        }

        /// <summary>
        /// Writes the method implementation table entries for this type to
        /// the assembly metadata.
        /// </summary>
        /// <param name="builder">The <see cref="MetadataBuilder"/> into which to write the
        /// MethodImpl table entries.</param>
        /// <param name="tokenMapping">The token mapping into which the associations of virtual handles
        /// for field and method definitions with their real handles (which will be determined when their
        /// metadata is written) should be stored.</param>
        internal void writeMethodImplEntries(MetadataBuilder builder, TokenMapping tokenMapping) {
            var handle = (TypeDefinitionHandle)m_handle;

            foreach (var methodImplEntry in m_methodImplDict) {
                builder.AddMethodImplementation(
                    handle,
                    tokenMapping.getMappedHandle(methodImplEntry.Value),
                    tokenMapping.getMappedHandle(methodImplEntry.Key)
                );
            }
        }

        /// <summary>
        /// Appends the definitions of the generic type parameters of this type definition
        /// and all its method definitions to the given list.
        /// </summary>
        /// <param name="tokenMapping">The token mapping from which to obtain the real
        /// metadata handles for generic method definitions.</param>
        /// <param name="entriesList">The list to which to append the generic parameter entries
        /// for this type definition (if it is generic) and all of its declared generic methods.</param>
        internal void appendGenParamEntries(
            TokenMapping tokenMapping, ref DynamicArray<GenericParameter> entriesList)
        {
            _GenParam[] genParams = m_genParams;
            for (int i = 0; i < genParams.Length; i++) {
                ref var genParam = ref genParams[i];
                entriesList.add(new GenericParameter(m_handle, i, genParam.name, genParam.attrs, genParam.constraints));
            }

            Span<MethodBuilder> methodDefs = m_methodDefs.asSpan();
            for (int i = 0; i < methodDefs.Length; i++)
                methodDefs[i].appendGenParamEntries(tokenMapping, ref entriesList);
        }

        /// <summary>
        /// Assigns the IL stream addresses for the methods in this type definition that have bodies.
        /// </summary>
        /// <param name="currentAddress">The position in the assembly's IL stream at which the
        /// first method body from this type definition should be written.</param>
        /// <returns>The position in the IL stream at which the first method body after all method bodies in
        /// this type definition should be written.</returns>
        internal int assignMethodBodyAddresses(int currentAddress) {
            Span<MethodBuilder> methodDefs = m_methodDefs.asSpan();
            for (int i = 0; i < methodDefs.Length; i++)
                currentAddress = methodDefs[i].assignMethodBodyAddress(currentAddress);

            return currentAddress;
        }

        /// <summary>
        /// Writes the method bodies for the methods defined in this <see cref="TypeBuilder"/>
        /// to the given blob stream.
        /// </summary>
        /// <param name="blob">The <see cref="BlobBuilder"/> into which to write the method
        /// bodies.</param>
        /// <param name="tokenMapping">The token mapping for replacing virtual tokens in method
        /// bodies before they are written to the assembly.</param>
        internal void writeMethodBodies(BlobBuilder blob, TokenMapping tokenMapping) {
            Span<MethodBuilder> methodDefs = m_methodDefs.asSpan();
            for (int i = 0; i < methodDefs.Length; i++)
                methodDefs[i].writeMethodBody(blob, tokenMapping);
        }

    }

}
