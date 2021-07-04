using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Mariana.CodeGen.IL;
using Mariana.Common;

namespace Mariana.CodeGen {

    /// <summary>
    /// A metadata context for an <see cref="AssemblyBuilder"/>, from which metadata handles can
    /// be obtained to refer to types and members from the assembly or from external assemblies in
    /// emitted definitions and IL.
    /// </summary>
    /// <remarks>
    /// <b>Thread safety note</b>: All public methods on instances of <see cref="MetadataContext"/>
    /// are safe to call concurrently from multiple threads.
    /// </remarks>
    public sealed class MetadataContext {

        private readonly struct InstantiatedMemberRef : IEquatable<InstantiatedMemberRef> {
            private readonly object m_def;
            private readonly EntityHandle m_instantiatedType;

            public InstantiatedMemberRef(object definition, EntityHandle instantiatedType) {
                m_def = definition;
                m_instantiatedType = instantiatedType;
            }

            public override bool Equals(object other) => other is InstantiatedMemberRef x && Equals(x);

            public bool Equals(InstantiatedMemberRef other) =>
                m_def == other.m_def && m_instantiatedType == other.m_instantiatedType;

            public override int GetHashCode() =>
                m_def.GetHashCode() ^ m_instantiatedType.GetHashCode();
        }

        private static readonly StringHandle s_nullStringHandle = MetadataTokens.StringHandle(0);
        private static readonly BlobHandle s_nullBlobHandle = MetadataTokens.BlobHandle(0);

        private ReferenceDictionary<Type, EntityHandle> m_extTypeHandleCache = new();

        private Dictionary<TypeSignature, TypeSpecificationHandle> m_typeSpecCache = new();

        private ReferenceDictionary<Type, TypeSignature> m_typeSignatureCache = new();

        private ReferenceDictionary<MemberInfo, EntityHandle> m_extMemberHandleCache = new();

        private ReferenceDictionary<MemberInfo, BlobHandle> m_memberSigBlobCache = new();

        private Dictionary<InstantiatedMemberRef, EntityHandle> m_instantiatedMemberRefCache = new();

        private ReferenceDictionary<Assembly, AssemblyReferenceHandle> m_assemblyRefCache = new();

        private IndexedSet<MethodInstantiation> m_methodSpecEntries = new();

        private MetadataBuilder m_metadataBuilder;

        private ConcurrentBag<BlobBuilder> m_internalBlobBuilders = new();

        private ConcurrentDictionary<EntityHandle, MethodStackChangeInfo> m_methodTokenStackInfo = new();

        private ILTokenProvider m_ilTokenProvider;

        private IncrementCounter m_virtualFieldDefRowCounter = new IncrementCounter(1);

        private IncrementCounter m_virtualMethodDefRowCounter = new IncrementCounter(1);

        private Func<Type, EntityHandle> m_typeHandleGenerator;

        private object m_contextLock = new object();

        internal MetadataContext(MetadataBuilder metadataBuilder) {
            m_metadataBuilder = metadataBuilder;
            m_ilTokenProvider = new _ILTokenProvider(this);
            m_typeHandleGenerator = type => _getTypeHandleInternal(type);
        }

        /// <summary>
        /// Gets the <see cref="MetadataBuilder"/> containing the metadata of the assembly that
        /// this <see cref="MetadataContext"/>. This can be used to emit metadata into the assembly.
        /// </summary>
        /// <remarks>
        /// The <see cref="MetadataBuilder"/> is wrapped in a locked object to provide thread-safe access.
        /// Ensure that <see cref="LockedObject{T}.Dispose"/> is called after you have finished working
        /// with the <see cref="MetadataBuilder"/> so that the lock is released.
        /// </remarks>
        internal LockedObject<MetadataBuilder> getMetadataBuilder() => new LockedObject<MetadataBuilder>(m_metadataBuilder, m_contextLock);

        /// <summary>
        /// Returns a handle for the given string in the string heap. If the string does not
        /// exist in the heap, a new string heap entry is created.
        /// </summary>
        /// <param name="str">The string whose string heap handle is to be returned.</param>
        internal StringHandle getStringHandle(string? str) {
            lock (m_contextLock)
                return _getStringHandleInternal(str);
        }

        private StringHandle _getStringHandleInternal(string? str) =>
            (str == null) ? s_nullStringHandle : m_metadataBuilder.GetOrAddString(str);

        /// <summary>
        /// Returns a handle for the given byte array in the blob heap. If the blob does not
        /// exist in the heap, a new blob heap entry is created.
        /// </summary>
        /// <param name="blob">The byte array whose blob heap handle is to be returned.</param>
        internal BlobHandle getBlobHandle(byte[]? blob) {
            lock (m_contextLock)
                return _getBlobHandleInternal(blob);
        }

        private BlobHandle _getBlobHandleInternal(byte[]? blob) =>
            (blob == null) ? s_nullBlobHandle : m_metadataBuilder.GetOrAddBlob(blob);

        private BlobBuilder _acquireBlobBuilder() {
            if (m_internalBlobBuilders.TryTake(out BlobBuilder blob)) {
                blob.Clear();
                return blob;
            }
            return new BlobBuilder();
        }

        private void _releaseBlobBuilder(BlobBuilder blob) => m_internalBlobBuilders.Add(blob);

        private AssemblyReferenceHandle _getAssemblyRefHandleInternal(Assembly assembly) {
            if (m_assemblyRefCache.tryGetValue(assembly, out AssemblyReferenceHandle handle))
                return handle;

            AssemblyName asmName = assembly.GetName();
            byte[] publicKeyToken = asmName.GetPublicKeyToken();

            handle = m_metadataBuilder.AddAssemblyReference(
                name: _getStringHandleInternal(asmName.Name),
                version: asmName.Version,
                culture: _getStringHandleInternal(asmName.CultureName),
                publicKeyOrToken: _getBlobHandleInternal(publicKeyToken),
                flags: 0,
                hashValue: s_nullBlobHandle
            );

            m_assemblyRefCache[assembly] = handle;
            return handle;
        }

        /// <summary>
        /// Returns a handle that can be used to refer to the given external type in the dynamic assembly
        /// that this <see cref="MetadataContext"/> belongs to.
        /// </summary>
        /// <param name="type">The type for which to obtain a handle for use in the assembly being
        /// created. This must not be a by-ref type or a generic type or method parameter.</param>
        /// <returns>A <see cref="EntityHandle"/> representing <paramref name="type"/> in this
        /// metadata context.</returns>
        /// <exception cref="ArgumentException"><paramref name="type"/> represents a by-reference
        /// type.</exception>
        public EntityHandle getTypeHandle(Type type) {
            lock (m_contextLock)
                return _getTypeHandleInternal(type);
        }

        private EntityHandle _getTypeHandleInternal(Type type) {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (m_extTypeHandleCache.tryGetValue(type, out EntityHandle handle))
                return handle;

            if (type.IsByRef)
                throw new ArgumentException("Type must not be a by-ref type.", nameof(type));

            if (type.IsArray || type.IsPointer || type.IsConstructedGenericType || type.IsGenericParameter) {
                // We need to create a TypeSpec for these types.
                handle = _getTypeSpecHandleInternal(_getTypeSignatureInternal(type));
            }
            else {
                EntityHandle resolutionScope = type.IsNested
                    ? _getTypeHandleInternal(type.DeclaringType)
                    : _getAssemblyRefHandleInternal(type.Assembly);

                handle = m_metadataBuilder.AddTypeReference(
                    resolutionScope, _getStringHandleInternal(type.Namespace), _getStringHandleInternal(type.Name));
            }

            m_extTypeHandleCache[type] = handle;
            return handle;
        }

        /// <summary>
        /// Returns a signature for the given external type that can be used to refer to the
        /// type in the dynamic assembly that this <see cref="MetadataContext"/> belongs to.
        /// </summary>
        /// <param name="type">The type for which to obtain a signature.</param>
        /// <returns>A <see cref="TypeSignature"/> representing <paramref name="type"/> in this
        /// metadata context.</returns>
        public TypeSignature getTypeSignature(Type type) {
            lock (m_contextLock)
                return _getTypeSignatureInternal(type);
        }

        private TypeSignature _getTypeSignatureInternal(Type type, bool forceInstantiation = false) {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            TypeSignature signature;

            if (!m_typeSignatureCache.tryGetValue(type, out signature)) {
                signature = TypeSignature.fromType(type, m_typeHandleGenerator);

                if (!type.IsGenericParameter)
                    m_typeSignatureCache[type] = signature;
            }

            if (forceInstantiation && type.IsGenericTypeDefinition)
                return signature.makeGenericSelfInstance(type.GetGenericArguments().Length);

            return signature;
        }

        /// <summary>
        /// Gets a handle that can be used to refer to the type having the given signature.
        /// </summary>
        /// <param name="signature">The signature of the type for which to obtain a handle.</param>
        /// <returns>An <see cref="EntityHandle"/> that represents the type with the given signature
        /// in this metadata context.</returns>
        public EntityHandle getTypeHandle(in TypeSignature signature) {
            var firstByte = signature.getFirstByte();
            if (firstByte == (byte)SignatureTypeKind.Class || firstByte == (byte)SignatureTypeKind.ValueType)
                return signature.getHandleOfClassOrValueType();

            Type? primitiveType = signature.getPrimitiveType();
            if (primitiveType != null)
                return getTypeHandle(primitiveType);

            lock (m_contextLock)
                return _getTypeSpecHandleInternal(signature);
        }

        private TypeSpecificationHandle _getTypeSpecHandleInternal(in TypeSignature signature) {
            if (m_typeSpecCache.TryGetValue(signature, out TypeSpecificationHandle handle))
                return handle;

            handle = m_metadataBuilder.AddTypeSpecification(m_metadataBuilder.GetOrAddBlob(signature.getBytes()));
            m_typeSpecCache[signature] = handle;

            return handle;
        }

        /// <summary>
        /// Returns a handle that can be used to refer to the given external member in the dynamic assembly
        /// that this <see cref="MetadataContext"/> belongs to.
        /// </summary>
        ///
        /// <param name="memberInfo">The member for which to obtain a handle for use in the assembly being
        /// created. This must be an instance of <see cref="FieldInfo"/>, <see cref="MethodInfo"/> or
        /// <see cref="ConstructorInfo"/>.</param>
        /// <param name="instantiatedType">If <paramref name="memberInfo"/> represents a member
        /// of a generic type definition and a handle must be obtained for the member on an
        /// instantiation of that generic type, pass a handle to the instantiated type
        /// (obtained from <see cref="getTypeHandle(Type)"/> or <see cref="getTypeHandle(in TypeSignature)"/>)
        /// as this argument.</param>
        ///
        /// <returns>An <see cref="EntityHandle"/> that represents the member given by
        /// <paramref name="memberInfo"/> in this metadata context.</returns>
        ///
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item><description><paramref name="instantiatedType"/> is not the null
        /// handle, and <paramref name="memberInfo"/> is not a member of a generic type definition.</description></item>
        /// <item><description><paramref name="memberInfo"/> is not a <see cref="FieldInfo"/>, <see cref="MethodInfo"/> or
        /// <see cref="ConstructorInfo"/>.</description></item>
        /// </list>
        /// </exception>
        public EntityHandle getMemberHandle(MemberInfo memberInfo, EntityHandle instantiatedType = default) {
            lock (m_contextLock)
                return _getMemberHandleInternal(memberInfo, instantiatedType);
        }

        private EntityHandle _getMemberHandleInternal(MemberInfo memberInfo, EntityHandle instantiatedType) {
            if (memberInfo == null)
                throw new ArgumentNullException(nameof(memberInfo));

            EntityHandle handle;

            if (!instantiatedType.IsNil) {
                if (!memberInfo.DeclaringType.IsGenericTypeDefinition) {
                    throw new ArgumentException(
                        nameof(instantiatedType) + " can only be specified if " +
                        nameof(memberInfo) + " is from a generic type definition.");
                }

                var instMemberRef = new InstantiatedMemberRef(memberInfo, instantiatedType);
                if (m_instantiatedMemberRefCache.TryGetValue(instMemberRef, out handle))
                    return handle;
            }
            else {
                if (m_extMemberHandleCache.tryGetValue(memberInfo, out handle))
                    return handle;
            }

            if (memberInfo is Type type)
                return _getTypeHandleInternal(type);

            if (memberInfo is MethodInfo methodInfo && methodInfo.IsConstructedGenericMethod) {
                // We need to create a MethodSpec.
                Type[] typeArgs = methodInfo.GetGenericArguments();

                var defHandle = _getMemberHandleInternal(methodInfo.GetGenericMethodDefinition(), instantiatedType);
                var argumentSignatures = new TypeSignature[typeArgs.Length];

                for (int i = 0; i < typeArgs.Length; i++)
                    argumentSignatures[i] = _getTypeSignatureInternal(typeArgs[i], true);

                handle = _getMethodSpecHandleInternal(new MethodInstantiation(defHandle, argumentSignatures));
            }
            else {
                EntityHandle declClass = instantiatedType.IsNil
                    ? _getTypeHandleInternal(memberInfo.ReflectedType)
                    : instantiatedType;

                StringHandle name = _getStringHandleInternal(memberInfo.Name);
                BlobHandle signature = _getMemberInfoSignatureBlob(memberInfo);

                handle = m_metadataBuilder.AddMemberReference(declClass, name, signature);

                if (memberInfo is MethodBase mb)
                    setMethodStackChange(handle, new MethodStackChangeInfo(mb));
            }

            if (instantiatedType.IsNil)
                m_extMemberHandleCache[memberInfo] = handle;
            else
                m_instantiatedMemberRefCache.Add(new InstantiatedMemberRef(memberInfo, instantiatedType), handle);

            return handle;
        }

        private BlobHandle _getMemberInfoSignatureBlob(MemberInfo memberInfo) {
            Type declaringType = memberInfo.DeclaringType;

            if (declaringType.IsConstructedGenericType) {
                // This ResolveMember "trick" seems to get the MemberInfo from the definition.
                memberInfo = memberInfo.Module.ResolveMember(memberInfo.MetadataToken);
                declaringType = memberInfo.DeclaringType;
            }

            if (m_memberSigBlobCache.tryGetValue(memberInfo, out BlobHandle handle))
                return handle;

            BlobBuilder blob = _acquireBlobBuilder();

            try {
                if (memberInfo is FieldInfo field) {
                    blob.WriteByte((byte)SignatureKind.Field);
                    _getTypeSignatureInternal(field.FieldType, true).writeToBlobBuilder(blob);
                }
                else if (memberInfo is MethodBase methodOrCtor) {
                    ParameterInfo? retParam = (methodOrCtor is MethodInfo method) ? method.ReturnParameter : null;
                    ParameterInfo[] parameters = methodOrCtor.GetParameters();
                    CallingConventions callConv = methodOrCtor.CallingConvention;
                    int genParamCount = methodOrCtor.IsGenericMethod ? methodOrCtor.GetGenericArguments().Length : 0;

                    _writeExternalMethodSigToBlob(callConv, genParamCount, declaringType, retParam, parameters, blob);
                }
                else {
                    throw new ArgumentException("Member must be a field, method or constructor.", nameof(memberInfo));
                }

                handle = m_metadataBuilder.GetOrAddBlob(blob);

                // We only memoize signature handles of members of generic type definitions, as they
                // are the only ones that are reused.
                if (declaringType.IsGenericTypeDefinition)
                    m_memberSigBlobCache[memberInfo] = handle;

                return handle;
            }
            finally {
                _releaseBlobBuilder(blob);
            }
        }

        /// <summary>
        /// Returns a handle that can be used to refer to the given <see cref="FieldBuilder"/> in IL code
        /// in the dynamic assembly that this <see cref="MetadataContext"/> belongs to.
        /// </summary>
        ///
        /// <param name="fieldBuilder">A <see cref="FieldBuilder"/> instance.</param>
        /// <param name="instantiatedType">If <paramref name="fieldBuilder"/> is from a generic
        /// type and a handle must be obtained for the field on an instantiation of that generic
        /// type, pass a handle to the instantiated type (obtained from <see cref="getTypeHandle(Type)"/>
        /// or <see cref="getTypeHandle(in TypeSignature)"/>) as this argument.</param>
        ///
        /// <returns>An <see cref="EntityHandle"/> that represents the member given by
        /// <paramref name="fieldBuilder"/> in this metadata context.</returns>
        ///
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item><description><paramref name="instantiatedType"/> is not the null handle,
        /// and <paramref name="fieldBuilder"/> is not a member of a generic type definition.</description></item>
        /// <item><description><paramref name="fieldBuilder"/> is not from the same assembly as this
        /// <see cref="MetadataContext"/>.</description></item>
        /// </list>
        /// </exception>
        public EntityHandle getMemberHandle(FieldBuilder fieldBuilder, EntityHandle instantiatedType = default) {
            if (fieldBuilder == null)
                throw new ArgumentNullException(nameof(fieldBuilder));

            if (fieldBuilder.declaringType.metadataContext != this)
                throw new ArgumentException("The definition must be from the same assembly.", nameof(fieldBuilder));

            if (!instantiatedType.IsNil && fieldBuilder.declaringType.genericParamCount == 0) {
                throw new ArgumentException(
                    nameof(instantiatedType) + " can only be specified if " +
                    nameof(fieldBuilder) + " is from a generic type definition.");
            }

            if (instantiatedType.IsNil)
                return fieldBuilder.handle;

            return _getMemberHandleOnInstantiatedType(
                fieldBuilder, fieldBuilder.nameHandle, fieldBuilder.signatureHandle, instantiatedType);
        }

        /// <summary>
        /// Returns a handle that can be used to refer to the given <see cref="MethodBuilder"/> in IL code
        /// in the dynamic assembly that this <see cref="MetadataContext"/> belongs to.
        /// </summary>
        ///
        /// <param name="methodBuilder">A <see cref="MethodBuilder"/> instance.</param>
        /// <param name="instantiatedType">If <paramref name="methodBuilder"/> is from a generic
        /// type and a handle must be obtained for the method on an instantiation of that generic
        /// type, pass a handle to the instantiated type (obtained from <see cref="getTypeHandle(Type)"/>
        /// or <see cref="getTypeHandle(in TypeSignature)"/>) as this argument.</param>
        ///
        /// <returns>An <see cref="EntityHandle"/> that represents the member given by
        /// <paramref name="methodBuilder"/> in this metadata context.</returns>
        ///
        /// <exception cref="ArgumentException">
        /// <list type="bullet">
        /// <item><description><paramref name="instantiatedType"/> is not the null handle,
        /// and <paramref name="methodBuilder"/> is not a member of a generic type definition.</description></item>
        /// <item><description><paramref name="methodBuilder"/> is not from the same assembly as this
        /// <see cref="MetadataContext"/>.</description></item>
        /// </list>
        /// </exception>
        public EntityHandle getMemberHandle(MethodBuilder methodBuilder, EntityHandle instantiatedType = default) {
            if (methodBuilder == null)
                throw new ArgumentNullException(nameof(methodBuilder));

            if (methodBuilder.declaringType.metadataContext != this)
                throw new ArgumentException("The definition must be from the same assembly.", nameof(methodBuilder));

            if (!instantiatedType.IsNil && methodBuilder.declaringType.genericParamCount == 0) {
                throw new ArgumentException(
                    nameof(instantiatedType) + " can only be specified if " +
                    nameof(methodBuilder) + " is from a generic type definition.");
            }

            if (instantiatedType.IsNil)
                return methodBuilder.handle;

            return _getMemberHandleOnInstantiatedType(
                methodBuilder, methodBuilder.nameHandle, methodBuilder.signatureHandle, instantiatedType);
        }

        private EntityHandle _getMemberHandleOnInstantiatedType(
            object fieldOrMethodBuilder, StringHandle name, BlobHandle sig, EntityHandle instantiatedType)
        {
            if (instantiatedType.Kind != HandleKind.TypeSpecification)
                throw new ArgumentException("Instantiated type must be a TypeSpec.", nameof(instantiatedType));

            var memberRef = new InstantiatedMemberRef(fieldOrMethodBuilder, instantiatedType);

            lock (m_contextLock) {
                EntityHandle handle;

                if (m_instantiatedMemberRefCache.TryGetValue(memberRef, out handle))
                    return handle;

                handle = m_metadataBuilder.AddMemberReference(instantiatedType, name, sig);
                m_instantiatedMemberRefCache[memberRef] = handle;

                if (fieldOrMethodBuilder is MethodBuilder mb)
                    _setMethodStackInfoFromOtherHandle(handle, mb.handle);

                return handle;
            }
        }

        /// <summary>
        /// Returns a handle that can be used to refer to an instantiation of a generic method in
        /// the dynamic assembly that this <see cref="MetadataContext"/> belongs to.
        /// </summary>
        /// <param name="inst">The method instantiation for which to obtain a handle for use in the
        /// assembly being created.</param>
        public MethodSpecificationHandle getMethodSpecHandle(in MethodInstantiation inst) {
            lock (m_contextLock)
                return _getMethodSpecHandleInternal(inst);
        }

        private MethodSpecificationHandle _getMethodSpecHandleInternal(in MethodInstantiation inst) {
            int index = m_methodSpecEntries.find(inst);
            if (index != -1)
                return MetadataTokens.MethodSpecificationHandle(index + 1);

            index = m_methodSpecEntries.findOrAdd(inst);
            var handle = MetadataTokens.MethodSpecificationHandle(index + 1);
            _setMethodStackInfoFromOtherHandle(handle, inst.definitionHandle);
            return handle;
        }

        /// <summary>
        /// Returns a handle that can be used to refer to a user string in the dynamic assembly
        /// that this <see cref="MetadataContext"/> belongs to. The handle can be used when
        /// emitting the <i>ldstr</i> instruction in a method body.
        /// </summary>
        /// <param name="str">The string for which to obtain a handle for use in the
        /// assembly being created. This must not be null.</param>
        public UserStringHandle getUserStringHandle(string str) {
            if (str == null)
                throw new ArgumentNullException(nameof(str));
            lock (m_contextLock)
                return m_metadataBuilder.GetOrAddUserString(str);
        }

        /// <summary>
        /// Creates a field signature blob in the metadata and returns a handle to it.
        /// </summary>
        /// <param name="fieldType">The signature for the field type.</param>
        /// <returns>A handle to the field signature blob.</returns>
        internal BlobHandle createFieldSignature(in TypeSignature fieldType) {
            BlobBuilder blob = _acquireBlobBuilder();
            try {
                blob.Clear();
                blob.WriteByte((byte)SignatureKind.Field);
                fieldType.writeToBlobBuilder(blob);

                lock (m_contextLock)
                    return m_metadataBuilder.GetOrAddBlob(blob);
            }
            finally {
                _releaseBlobBuilder(blob);
            }
        }

        /// <summary>
        /// Creates a method signature blob in the metadata and returns a handle to it.
        /// </summary>
        /// <param name="callConv">The calling convention for the method.</param>
        /// <param name="genParamCount">The number of generic parameters, or zero for a
        /// non-generic method.</param>
        /// <param name="thisType">The type signature of the method's "this" parameter. Ignored if
        /// <paramref name="callConv"/> does not have the <see cref="CallingConventions.ExplicitThis"/>
        /// bit set.</param>
        /// <param name="returnType">The type signature of the method's return value.</param>
        /// <param name="paramTypes">A span containing the type signatures of the method's parameters.</param>
        /// <param name="varargTypes">A span containing the type signatures of the additional
        /// arguments passed to a vararg method.</param>
        /// <returns>A handle to the method signature blob.</returns>
        internal BlobHandle createMethodSignature(
            CallingConventions callConv, int genParamCount,
            in TypeSignature thisType, in TypeSignature returnType,
            ReadOnlySpan<TypeSignature> paramTypes, ReadOnlySpan<TypeSignature> varargTypes)
        {
            BlobBuilder blob = _acquireBlobBuilder();

            try {
                blob.WriteByte(_getFirstByteOfMethodSig(callConv, genParamCount));

                if (genParamCount != 0)
                    blob.WriteCompressedInteger(genParamCount);

                bool explicitThis = (callConv & CallingConventions.ExplicitThis) != 0;
                int paramCount = paramTypes.Length + varargTypes.Length + (explicitThis ? 1 : 0);

                blob.WriteCompressedInteger(paramCount);

                returnType.writeToBlobBuilder(blob);

                if (explicitThis)
                    thisType.writeToBlobBuilder(blob);

                for (int i = 0; i < paramTypes.Length; i++)
                    paramTypes[i].writeToBlobBuilder(blob);

                if (varargTypes.Length != 0) {
                    blob.WriteByte((byte)SignatureTypeCode.Sentinel);
                    for (int i = 0; i < varargTypes.Length; i++)
                        varargTypes[i].writeToBlobBuilder(blob);
                }

                lock (m_contextLock)
                    return m_metadataBuilder.GetOrAddBlob(blob);
            }
            finally {
                _releaseBlobBuilder(blob);
            }
        }

        /// <summary>
        /// Creates a property signature blob in the metadata and returns a handle to it.
        /// </summary>
        /// <param name="isStatic">True if the property is a static property, false for an instance
        /// property.</param>
        /// <param name="propertyType">The type signature of the property's type.</param>
        /// <param name="paramTypes">A span containing the type signatures of the property's parameters.</param>
        /// <returns>A handle to the method signature blob.</returns>
        internal BlobHandle createPropertySignature(
            bool isStatic, in TypeSignature propertyType, ReadOnlySpan<TypeSignature> paramTypes)
        {
            BlobBuilder blob = _acquireBlobBuilder();

            try {
                blob.WriteByte((byte)((int)SignatureKind.Property | (int)(isStatic ? 0 : SignatureAttributes.Instance)));

                blob.WriteCompressedInteger(paramTypes.Length);
                propertyType.writeToBlobBuilder(blob);

                for (int i = 0; i < paramTypes.Length; i++)
                    paramTypes[i].writeToBlobBuilder(blob);

                lock (m_contextLock)
                    return m_metadataBuilder.GetOrAddBlob(blob);
            }
            finally {
                _releaseBlobBuilder(blob);
            }
        }

        /// <summary>
        /// Defines a standalone signature in the assembly metadata and returns a handle to it.
        /// </summary>
        /// <param name="signature">A span containing the binary signature.</param>
        /// <returns>A <see cref="StandaloneSignatureHandle"/> to the signature in the
        /// assembly metadata.</returns>
        public StandaloneSignatureHandle createStandaloneSignature(ReadOnlySpan<byte> signature) {
            lock (m_contextLock)
                return m_metadataBuilder.AddStandaloneSignature(m_metadataBuilder.GetOrAddBlob(signature.ToArray()));
        }

        /// <summary>
        /// Defines a standalone signature in the assembly metadata and returns a handle to it.
        /// </summary>
        /// <param name="signature">A <see cref="BlobBuilder"/> containing the binary signature.</param>
        /// <returns>A <see cref="StandaloneSignatureHandle"/> to the signature in the
        /// assembly metadata.</returns>
        public StandaloneSignatureHandle createStandaloneSignature(BlobBuilder signature) {
            lock (m_contextLock)
                return m_metadataBuilder.AddStandaloneSignature(m_metadataBuilder.GetOrAddBlob(signature));
        }

        /// <summary>
        /// Returns the first byte of a method signature for a method with the given calling
        /// convention and generic parameter count.
        /// </summary>
        /// <param name="callConv">The method's calling convention.</param>
        /// <param name="genParamCount">The number of generic parameters, zero for non-generic methods.</param>
        private static byte _getFirstByteOfMethodSig(CallingConventions callConv, int genParamCount) {
            byte firstByte = ((callConv & CallingConventions.Any) == CallingConventions.VarArgs)
                ? (byte)SignatureCallingConvention.VarArgs
                : (byte)SignatureCallingConvention.Default;

            if ((callConv & CallingConventions.HasThis) != 0)
                firstByte |= (byte)SignatureAttributes.Instance;

            if ((callConv & CallingConventions.ExplicitThis) != 0)
                firstByte |= (byte)SignatureAttributes.ExplicitThis;

            if (genParamCount != 0)
                firstByte |= (byte)SignatureAttributes.Generic;

            return firstByte;
        }

        private void _writeExternalMethodSigToBlob(
            CallingConventions callConv,
            int genParamCount,
            Type declaringType,
            ParameterInfo? retParam,
            ParameterInfo[] parameters,
            BlobBuilder blob
        ) {
            blob.WriteByte(_getFirstByteOfMethodSig(callConv, genParamCount));

            if (genParamCount != 0)
                blob.WriteCompressedInteger(genParamCount);

            bool explicitThis = (callConv & CallingConventions.ExplicitThis) != 0;
            int paramCount = parameters.Length + (explicitThis ? 1 : 0);

            blob.WriteCompressedInteger(paramCount);

            if (retParam != null)
                _writeExternalParamSigToBlob(retParam, blob);
            else
                blob.WriteByte((byte)SignatureTypeCode.Void);

            if (explicitThis)
                _getTypeSignatureInternal(declaringType, true).writeToBlobBuilder(blob);

            for (int i = 0; i < parameters.Length; i++)
                _writeExternalParamSigToBlob(parameters[i], blob);
        }

        private void _writeExternalParamSigToBlob(ParameterInfo paramInfo, BlobBuilder blob) {
            Type[] reqCustMods = paramInfo.GetRequiredCustomModifiers();
            Type[] optCustMods = paramInfo.GetOptionalCustomModifiers();

            for (int i = 0; i < reqCustMods.Length; i++) {
                blob.WriteByte((byte)SignatureTypeCode.RequiredModifier);
                var codedIndex = CodedIndex.TypeDefOrRefOrSpec(_getTypeHandleInternal(reqCustMods[i]));
                blob.WriteCompressedInteger(codedIndex);
            }

            for (int i = 0; i < optCustMods.Length; i++) {
                blob.WriteByte((byte)SignatureTypeCode.OptionalModifier);
                var codedIndex = CodedIndex.TypeDefOrRefOrSpec(_getTypeHandleInternal(optCustMods[i]));
                blob.WriteCompressedInteger(codedIndex);
            }

            _getTypeSignatureInternal(paramInfo.ParameterType, true).writeToBlobBuilder(blob);
        }

        /// <summary>
        /// Returns the virtual handle for the next field definition, and increments the next
        /// virtual field definition handle value.
        /// </summary>
        internal FieldDefinitionHandle getNewVirtualFieldDefHandle() =>
            MetadataTokens.FieldDefinitionHandle(m_virtualFieldDefRowCounter.atomicNext());

        /// <summary>
        /// Returns the virtual handle for the next method definition, and increments the next
        /// virtual method definition handle value.
        /// </summary>
        internal MethodDefinitionHandle getNewVirtualMethodDefHandle() =>
            MetadataTokens.MethodDefinitionHandle(m_virtualMethodDefRowCounter.atomicNext());

        /// <summary>
        /// Sets the stack height change information for a method that is used to compute the maximum stack
        /// height when emitting IL.
        /// </summary>
        /// <param name="handle">The method handle.</param>
        /// <param name="stackChangeInfo">A <see cref="MethodStackChangeInfo"/> instance.</param>
        internal void setMethodStackChange(EntityHandle handle, MethodStackChangeInfo stackChangeInfo) =>
            m_methodTokenStackInfo.TryAdd(handle, stackChangeInfo);

        private void _setMethodStackInfoFromOtherHandle(EntityHandle handle, EntityHandle otherHandle) {
            if (m_methodTokenStackInfo.TryGetValue(otherHandle, out MethodStackChangeInfo stackChangeInfo))
                m_methodTokenStackInfo.TryAdd(handle, stackChangeInfo);
        }

        /// <summary>
        /// Returns the number of rows needed in the FieldDef table in the assembly
        /// that this <see cref="MetadataContext"/> belongs to.
        /// </summary>
        internal int fieldDefRowCount => m_virtualFieldDefRowCounter.current - 1;

        /// <summary>
        /// Returns the number of rows needed in the MethodDef table in the assembly
        /// that this <see cref="MetadataContext"/> belongs to.
        /// </summary>
        internal int methodDefRowCount => m_virtualMethodDefRowCounter.current - 1;

        /// <summary>
        /// Emits the MethodSpec table created by this <see cref="MetadataContext"/> into the
        /// assembly metadata after patching any MethodDef tokens using the given patching map.
        /// </summary>
        /// <param name="tokenMapping">The token mapping for patching MethodDef tokens.</param>
        internal void emitMethodSpecTable(TokenMapping tokenMapping) {
            lock (m_contextLock) {
                for (int i = 0, n = m_methodSpecEntries.count; i < n; i++) {
                    MethodInstantiation inst = m_methodSpecEntries[i];

                    EntityHandle method = tokenMapping.getMappedHandle(inst.definitionHandle);
                    BlobHandle signature = getBlobHandle(inst.getSignature().toArray());

                    m_metadataBuilder.AddMethodSpecification(method, signature);
                }
            }
        }

        /// <summary>
        /// Returns an <see cref="ILTokenProvider"/> instance that provides metadata tokens for
        /// IL code in the dynamic assembly that this <see cref="MetadataContext"/> belongs to.
        /// </summary>
        /// <remarks>
        /// <b>Thread safety note</b>: All public methods on the <see cref="ILTokenProvider"/>
        /// instance returned by this property are safe to call concurrently from multiple threads.
        /// </remarks>
        public ILTokenProvider ilTokenProvider => m_ilTokenProvider;

        private sealed class _ILTokenProvider : ILTokenProvider {

            private MetadataContext m_context;

            public _ILTokenProvider(MetadataContext context) {
                m_context = context;
            }

            public override EntityHandle getHandle(Type type) => m_context.getTypeHandle(type);

            public override EntityHandle getHandle(FieldInfo field) => m_context.getMemberHandle(field);

            public override EntityHandle getHandle(MethodBase method) => m_context.getMemberHandle(method);

            public override UserStringHandle getHandle(string str) => m_context.getUserStringHandle(str);

            public override EntityHandle getHandle(in TypeSignature type) => m_context.getTypeHandle(type);

            public override int getMethodStackDelta(EntityHandle handle, ILOp opcode) {
                // If a stack delta is not available, return 1 as that is the worst case
                // (no arguments popped, return value pushed)
                if (!m_context.m_methodTokenStackInfo.TryGetValue(handle, out MethodStackChangeInfo stackChangeInfo))
                    return 1;
                return stackChangeInfo.getStackDelta(opcode);
            }

            public override bool isVirtualHandle(EntityHandle handle) {
                HandleKind kind = handle.Kind;
                return kind == HandleKind.FieldDefinition || kind == HandleKind.MethodDefinition;
            }

            public override TypeSignature getTypeSignature(Type type) =>
                m_context.getTypeSignature(type);

            public override bool useLocalSigHelper => false;

            public override StandaloneSignatureHandle getLocalSignatureHandle(ReadOnlySpan<byte> localSig) =>
                m_context.createStandaloneSignature(localSig);

        }

    }

}
