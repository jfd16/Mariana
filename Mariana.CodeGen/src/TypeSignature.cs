using System;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Mariana.Common;

namespace Mariana.CodeGen {

    /// <summary>
    /// Represents a signature for a type in metadata.
    /// </summary>
    /// <remarks>
    /// The signature for a type may be obtained using methods and properties such as
    /// <see cref="MetadataContext.getTypeSignature"/>, <see cref="TypeBuilder.signature"/>,
    /// <see cref="forClassType"/>, <see cref="forValueType"/>, <see cref="forPrimitiveType"/>,
    /// <see cref="forGenericTypeParam"/> or <see cref="forGenericMethodParam"/>. Signatures
    /// for composite types (such as arrays, pointers or generic instantiations) can be
    /// created using the <see cref="makeArray"/>, <see cref="makeSZArray"/>, <see cref="makePointer"/>,
    /// <see cref="makeByRef"/> and <see cref="makeGenericInstance"/> methods.
    /// </remarks>
    public readonly struct TypeSignature : IEquatable<TypeSignature> {

        private static readonly ReferenceDictionary<Type, TypeSignature> s_primitiveTypeSignatures =
            new ReferenceDictionary<Type, TypeSignature> {
                [typeof(bool)] = forPrimitiveType(PrimitiveTypeCode.Boolean),
                [typeof(sbyte)] = forPrimitiveType(PrimitiveTypeCode.SByte),
                [typeof(byte)] = forPrimitiveType(PrimitiveTypeCode.Byte),
                [typeof(short)] = forPrimitiveType(PrimitiveTypeCode.Int16),
                [typeof(ushort)] = forPrimitiveType(PrimitiveTypeCode.UInt16),
                [typeof(int)] = forPrimitiveType(PrimitiveTypeCode.Int32),
                [typeof(uint)] = forPrimitiveType(PrimitiveTypeCode.UInt32),
                [typeof(long)] = forPrimitiveType(PrimitiveTypeCode.Int64),
                [typeof(ulong)] = forPrimitiveType(PrimitiveTypeCode.UInt64),
                [typeof(float)] = forPrimitiveType(PrimitiveTypeCode.Single),
                [typeof(double)] = forPrimitiveType(PrimitiveTypeCode.Double),
                [typeof(char)] = forPrimitiveType(PrimitiveTypeCode.Char),
                [typeof(object)] = forPrimitiveType(PrimitiveTypeCode.Object),
                [typeof(string)] = forPrimitiveType(PrimitiveTypeCode.String),
                [typeof(void)] = forPrimitiveType(PrimitiveTypeCode.Void),
                [typeof(IntPtr)] = forPrimitiveType(PrimitiveTypeCode.IntPtr),
                [typeof(UIntPtr)] = forPrimitiveType(PrimitiveTypeCode.UIntPtr),
                [typeof(TypedReference)] = forPrimitiveType(PrimitiveTypeCode.TypedReference),
            };

        private readonly long m_compact;
        private readonly byte[]? m_bytes;

        internal TypeSignature(long compact) {
            m_compact = compact;
            m_bytes = null;
        }

        internal TypeSignature(byte[] bytes) {
            m_compact = 0L;
            m_bytes = bytes;
        }

        /// <summary>
        /// Returns the value of the first byte of the signature.
        /// </summary>
        public byte getFirstByte() => (m_bytes != null) ? m_bytes[0] : (byte)m_compact;

        /// <summary>
        /// Returns the value of the byte of the signature at the given index.
        /// </summary>
        public byte getByte(int index) {
            if (m_bytes != null)
                return m_bytes[index];

            int compactLength = (byte)(m_compact >> 56);
            if ((uint)index >= (uint)compactLength)
                throw new IndexOutOfRangeException();

            return (byte)(m_compact >> (index << 3));
        }

        /// <summary>
        /// Gets the length of the signature in bytes.
        /// </summary>
        public int byteLength => (m_bytes != null) ? m_bytes.Length : (int)(m_compact >> 56);

        /// <summary>
        /// Returns a byte array containing the binary signature.
        /// </summary>
        /// <returns>A byte array containing the binary signature.</returns>
        public byte[] getBytes() {
            if (m_bytes != null)
                return m_bytes.AsSpan().ToArray();

            int length = byteLength;
            byte[] arr = new byte[length];
            for (int i = 0, shift = 0; i < length; i++, shift += 8)
                arr[i] = (byte)(m_compact >> shift);
            return arr;
        }

        /// <summary>
        /// Writes the signature to the given span.
        /// </summary>
        /// <param name="span">The span into which the signature must be written. This must have a
        /// length of at least that of the signature (the value of the <see cref="byteLength"/>
        /// property.</param>
        public void writeToSpan(Span<byte> span) {
            if (m_bytes != null) {
                m_bytes.CopyTo(span.Slice(0, m_bytes.Length));
            }
            else {
                int compactLength = (byte)(m_compact >> 56);
                for (int i = 0, shift = 0; i < compactLength; i++, shift += 8)
                    span[i] = (byte)(m_compact >> shift);
            }
        }


        /// <summary>
        /// Writes the signature to the given <see cref="BlobBuilder"/>.
        /// </summary>
        /// <param name="blob">The <see cref="BlobBuilder"/> into which the signature must be written.</param>
        public void writeToBlobBuilder(BlobBuilder blob) {
            if (m_bytes != null) {
                blob.WriteBytes(m_bytes);
            }
            else {
                int compactLength = (byte)(m_compact >> 56);
                for (int i = 0, shift = 0; i < compactLength; i++, shift += 8)
                    blob.WriteByte((byte)(m_compact >> shift));
            }
        }

        /// <summary>
        /// Returns a signature that represents a one-dimensional zero-indexed array whose
        /// element type is represented by this signature.
        /// </summary>
        /// <returns>A <see cref="TypeSignature"/> representing the one-dimensional zero-indexed
        /// array type whose element type is represented by this signature.</returns>
        /// <exception cref="InvalidOperationException">This signature represents a by-ref type.</exception>
        public TypeSignature makeSZArray() => _addTypeCodePrefix(SignatureTypeCode.SZArray);

        /// <summary>
        /// Returns a signature that represents a pointer type whose underlying type is represented
        /// by this signature.
        /// </summary>
        /// <returns>A <see cref="TypeSignature"/> representing the pointer type whose element type
        /// is represented by this signature.</returns>
        /// <exception cref="InvalidOperationException">This signature represents a by-ref type.</exception>
        public TypeSignature makePointer() => _addTypeCodePrefix(SignatureTypeCode.Pointer);

        /// <summary>
        /// Returns a signature that represents a by-reference type whose underlying type is
        /// represented by this signature.
        /// </summary>
        /// <returns>A <see cref="TypeSignature"/> representing the by-ref type whose element type
        /// is represented by this signature.</returns>
        /// <exception cref="InvalidOperationException">This signature represents a by-ref type.</exception>
        /// <remarks>
        /// A by-reference type can only be used as the return type of a method, or the type
        /// of a method parameter or local variable. Use of a by-reference type in any other
        /// context results in an invalid assembly.
        /// </remarks>
        public TypeSignature makeByRef() => _addTypeCodePrefix(SignatureTypeCode.ByReference);

        private TypeSignature _addTypeCodePrefix(SignatureTypeCode code) {
            if (isByRef())
                throw new InvalidOperationException("Cannot use a by-ref type as the element type of an array, pointer or by-ref type.");

            int compactLength = (int)(m_compact >> 56);

            if (compactLength < 7)
                return new TypeSignature((long)(compactLength + 1) << 56 | m_compact << 8 | (byte)code);

            var builder = TypeSignatureBuilder.getInstance();
            try {
                builder.appendByte((byte)code);
                builder.appendSignature(this);
                return builder.makeSignature();
            }
            finally {
                builder.clear();
            }
    }

        /// <summary>
        /// Returns a signature representing a general array type whose element type is represented
        /// by this signature.
        /// </summary>
        ///
        /// <param name="rank">The number of array dimensions.</param>
        /// <param name="lengths">A span containing the lengths for each dimension. The length of
        /// this argument must not be greater than <paramref name="rank"/>.</param>
        /// <param name="lowerBounds">A span containing the lower bounds for each dimension. The length of
        /// this argument must not be greater than <paramref name="rank"/>.</param>
        ///
        /// <returns>A <see cref="TypeSignature"/> representing the array type whose element type
        /// is represented by this signature and whose rank, lengths and lower bounds are given.</returns>
        ///
        /// <exception cref="InvalidOperationException">This signature represents a by-ref type.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The rank is negative or zero, or the length
        /// of <paramref name="lengths"/> or <paramref name="lowerBounds"/> is greater than the rank.</exception>
        ///
        /// <remarks>
        /// This method creates a signature for a general array. To create a signature for a single-dimensional
        /// zero-based array, use <see cref="makeSZArray"/>. In particular, setting <paramref name="rank"/>
        /// to 1 and <paramref name="lengths"/> and <paramref name="lowerBounds"/> to empty spans does not
        /// create the same signature as <see cref="makeSZArray"/>.
        /// </remarks>
        public TypeSignature makeArray(
            int rank, ReadOnlySpan<int> lengths = default, ReadOnlySpan<int> lowerBounds = default)
        {
            if (rank <= 0)
                throw new ArgumentOutOfRangeException(nameof(rank));

            if (lengths.Length > rank || lowerBounds.Length > rank)
                throw new ArgumentOutOfRangeException("Number of lengths or lower bounds is greater than the array rank.");

            if (isByRef())
                throw new InvalidOperationException("Cannot use a by-ref type as the element type of an array, pointer or by-ref type.");

            var builder = TypeSignatureBuilder.getInstance();
            try {
                builder.appendByte((byte)SignatureTypeCode.Array);
                builder.appendSignature(this);

                builder.appendCompressedUnsignedInt(rank);

                builder.appendCompressedUnsignedInt(lengths.Length);
                for (int i = 0; i < lengths.Length; i++)
                    builder.appendCompressedUnsignedInt(lengths[i]);

                builder.appendCompressedUnsignedInt(lowerBounds.Length);
                for (int i = 0; i < lowerBounds.Length; i++)
                    builder.appendCompressedSignedInt(lowerBounds[i]);

                return builder.makeSignature();
            }
            finally {
                builder.clear();
            }
        }

        /// <summary>
        /// Returns a signature that represents a primitive type.
        /// </summary>
        /// <param name="code">The code for the primitive type, as a value from the
        /// <see cref="PrimitiveTypeCode"/> enumeration.</param>
        /// <returns>A <see cref="TypeSignature"/> representing the given primitive type.</returns>
        public static TypeSignature forPrimitiveType(PrimitiveTypeCode code) =>
            new TypeSignature((1L << 56) | (byte)code);

        /// <summary>
        /// Returns a signature that represents a value type.
        /// </summary>
        /// <param name="handle">A handle for the value type.</param>
        /// <returns>A <see cref="TypeSignature"/> representing the value type whose handle
        /// is given.</returns>
        public static TypeSignature forValueType(EntityHandle handle) => _forClassOrValueType(handle, true);

        /// <summary>
        /// Returns a signature that represents a class (reference) type.
        /// </summary>
        /// <param name="handle">A handle for the class type.</param>
        /// <returns>A <see cref="TypeSignature"/> representing the class type whose handle
        /// is given.</returns>
        public static TypeSignature forClassType(EntityHandle handle) => _forClassOrValueType(handle, false);

        private static TypeSignature _forClassOrValueType(EntityHandle handle, bool isValueType) {
            if (handle.IsNil)
                throw new ArgumentException("Handle for a class or value type must not be null.", nameof(handle));

            var builder = TypeSignatureBuilder.getInstance();
            try {
                builder.appendByte((byte)(isValueType ? SignatureTypeKind.ValueType : SignatureTypeKind.Class));
                builder.appendCompressedUnsignedInt(CodedIndex.TypeDefOrRefOrSpec(handle));
                return builder.makeSignature();
            }
            finally {
                builder.clear();
            }
        }

        /// <summary>
        /// Returns a signature representing the type parameter of a generic type at the
        /// given position.
        /// </summary>
        /// <param name="position">The zero-based position of the type parameter.</param>
        /// <returns>A <see cref="TypeSignature"/> representing the type parameter of a generic type at the
        /// given position.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is zero or negative.</exception>
        public static TypeSignature forGenericTypeParam(int position) {
            if (position < 0)
                throw new ArgumentOutOfRangeException(nameof(position));

            if (position < 127) {
                return new TypeSignature(
                    (2L << 56) | (long)(position << 8) | (byte)SignatureTypeCode.GenericTypeParameter);
            }

            var builder = TypeSignatureBuilder.getInstance();
            try {
                builder.appendByte((byte)SignatureTypeCode.GenericTypeParameter);
                builder.appendCompressedUnsignedInt(position);
                return builder.makeSignature();
            }
            finally {
                builder.clear();
            }
        }

        /// <summary>
        /// Returns a signature representing the type parameter of a generic method at the
        /// given position.
        /// </summary>
        /// <param name="position">The zero-based position of the type parameter.</param>
        /// <returns>A <see cref="TypeSignature"/> representing the type parameter of a generic method at the
        /// given position.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is zero or negative.</exception>
        public static TypeSignature forGenericMethodParam(int position) {
            if (position < 0)
                throw new ArgumentOutOfRangeException(nameof(position));

            if (position < 127) {
                return new TypeSignature(
                    (2L << 56) | (long)(position << 8) | (byte)SignatureTypeCode.GenericMethodParameter);
            }

            var builder = TypeSignatureBuilder.getInstance();
            try {
                builder.appendByte((byte)SignatureTypeCode.GenericMethodParameter);
                builder.appendCompressedUnsignedInt(position);
                return builder.makeSignature();
            }
            finally {
                builder.clear();
            }
        }

        /// <summary>
        /// Returns a signature that represents an instantiation of the generic type represented
        /// by this signature.
        /// </summary>
        /// <param name="typeArguments">The type arguments of the instantiation.</param>
        /// <returns>A <see cref="TypeSignature"/> representing the instantiation of the generic
        /// type whose definition is represented by this signature and whose type arguments are
        /// given.</returns>
        /// <exception cref="ArgumentException"><paramref name="typeArguments"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">This signature does not represent a class or value
        /// type.</exception>
        public TypeSignature makeGenericInstance(ReadOnlySpan<TypeSignature> typeArguments) {
            if (typeArguments.Length == 0)
                throw new ArgumentException("Type arguments must not be empty.", nameof(typeArguments));

            byte firstByte = getFirstByte();

            if (firstByte != (byte)SignatureTypeKind.Class && firstByte != (byte)SignatureTypeKind.ValueType)
                throw new InvalidOperationException("Generic instantiation is only allowed for class and valuetype signatures.");

            var builder = TypeSignatureBuilder.getInstance();
            try {
                builder.appendByte((byte)SignatureTypeCode.GenericTypeInstance);
                builder.appendSignature(this);

                builder.appendCompressedUnsignedInt(typeArguments.Length);
                for (int i = 0; i < typeArguments.Length; i++)
                    builder.appendSignature(typeArguments[i]);

                return builder.makeSignature();
            }
            finally {
                builder.clear();
            }
        }

        /// <summary>
        /// Returns a signature that represents an instantiation of the generic type represented
        /// by this signature with its own type parameters as type arguments.
        /// </summary>
        /// <param name="typeParamCount">The number of type parameters in the generic type.</param>
        /// <returns>A <see cref="TypeSignature"/> representing the instantiation of the generic
        /// type whose definition is represented by this signature and whose type arguments are
        /// the generic type's own type parameters.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="typeParamCount"/> is zero or negative.</exception>
        /// <exception cref="InvalidOperationException">This signature does not represent a class or value
        /// type.</exception>
        public TypeSignature makeGenericSelfInstance(int typeParamCount) {
            if (typeParamCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(typeParamCount));

            byte firstByte = getFirstByte();

            if (firstByte != (byte)SignatureTypeKind.Class && firstByte != (byte)SignatureTypeKind.ValueType)
                throw new InvalidOperationException("Generic instantiation is only allowed for class or valuetype signatures.");

            var builder = TypeSignatureBuilder.getInstance();
            try {
                builder.appendByte((byte)SignatureTypeCode.GenericTypeInstance);
                builder.appendSignature(this);

                builder.appendCompressedUnsignedInt(typeParamCount);
                for (int i = 0; i < typeParamCount; i++) {
                    builder.appendByte((byte)SignatureTypeCode.GenericTypeParameter);
                    builder.appendCompressedUnsignedInt(i);
                }

                return builder.makeSignature();
            }
            finally {
                builder.clear();
            }
        }

        /// <summary>
        /// Returns true if this <see cref="TypeSignature"/> represents an array type (other than SZArray).
        /// </summary>
        public bool isArray() => getFirstByte() == (byte)SignatureTypeCode.Array;

        /// <summary>
        /// Returns true if this <see cref="TypeSignature"/> represents an array type
        /// having a single dimension with a zero-based index.
        /// </summary>
        public bool isSZArray() => getFirstByte() == (byte)SignatureTypeCode.SZArray;

        /// <summary>
        /// Returns true if this <see cref="TypeSignature"/> represents a pointer type.
        /// </summary>
        public bool isPointer() => getFirstByte() == (byte)SignatureTypeCode.Pointer;

        /// <summary>
        /// Returns true if this <see cref="TypeSignature"/> represents a by-reference type.
        /// </summary>
        public bool isByRef() => getFirstByte() == (byte)SignatureTypeCode.ByReference;

        /// <summary>
        /// Returns true if this <see cref="TypeSignature"/> represents a class type or
        /// a generic instantiation of a class type. (Array types are not considered as
        /// class types.)
        /// </summary>
        public bool isClassType() {
            byte firstByte = getFirstByte();

            if (firstByte == (byte)SignatureTypeKind.Class)
                return true;

            if (firstByte == (byte)SignatureTypeCode.GenericTypeInstance) {
                byte second = (m_bytes != null) ? m_bytes[1] : (byte)(m_compact >> 8);
                return second == (byte)SignatureTypeKind.Class;
            }

            return false;
        }

        /// <summary>
        /// Returns true if this <see cref="TypeSignature"/> represents a (non-primitive) value type or
        /// a generic instantiation of a value type.
        /// </summary>
        public bool isValueType() {
            byte firstByte = getFirstByte();

            if (firstByte == (byte)SignatureTypeKind.ValueType)
                return true;

            if (firstByte == (byte)SignatureTypeCode.GenericTypeInstance) {
                byte second = (m_bytes != null) ? m_bytes[1] : (byte)(m_compact >> 8);
                return second == (byte)SignatureTypeKind.ValueType;
            }

            return false;
        }

        /// <summary>
        /// Returns true if this <see cref="TypeSignature"/> represents a reference type.
        /// Reference types include class types (including generic instantiations of them),
        /// array types, and the Object and String primitive types.
        /// </summary>
        public bool isReferenceType() {
            byte firstByte = getFirstByte();

            const int refTypeMask =
                  1 << (int)SignatureTypeKind.Class
                | 1 << (int)SignatureTypeCode.SZArray
                | 1 << (int)SignatureTypeCode.Array
                | 1 << (int)SignatureTypeCode.String
                | 1 << (int)SignatureTypeCode.Object;

            if (firstByte <= 31 && ((1 << firstByte) & refTypeMask) != 0)
                return true;

            if (firstByte == (byte)SignatureTypeCode.GenericTypeInstance) {
                byte second = (m_bytes != null) ? m_bytes[1] : (byte)(m_compact >> 8);
                return second == (byte)SignatureTypeKind.Class;
            }

            return false;
        }

        /// <summary>
        /// Returns true if this <see cref="TypeSignature"/> represents a type parameter of a generic
        /// type.
        /// </summary>
        public bool isGenericTypeParameter() => getFirstByte() == (byte)SignatureTypeCode.GenericTypeParameter;

        /// <summary>
        /// Returns true if this <see cref="TypeSignature"/> represents a type parameter of a generic
        /// method.
        /// </summary>
        public bool isGenericMethodParameter() => getFirstByte() == (byte)SignatureTypeCode.GenericMethodParameter;

        /// <summary>
        /// Returns true if this <see cref="TypeSignature"/> represents an instantiation of a generic type.
        /// </summary>
        public bool isGenericInstantiation() => getFirstByte() == (byte)SignatureTypeCode.GenericTypeInstance;

        /// <summary>
        /// Returns a handle for the class or value type represented by this signature.
        /// If this signature represents a generic instantiation, the handle for the generic
        /// type definition is returned.
        /// </summary>
        /// <exception cref="InvalidOperationException">The type signature does not represent
        /// a non-primitive class or value type or a generic instantiation of such a type.</exception>
        public EntityHandle getHandleOfClassOrValueType() {
            int byteIndex = 0;
            byte firstByte = getFirstByte();

            if (firstByte == (byte)SignatureTypeCode.GenericTypeInstance) {
                byteIndex++;
                firstByte = getByte(byteIndex);
            }

            if (firstByte != (byte)SignatureTypeKind.Class && firstByte != (byte)SignatureTypeKind.ValueType)
                throw new InvalidOperationException("The type signature must represent a class type, value type or a generic instantiation.");

            int codedIndex;

            if ((firstByte & 0x80) == 0)
                codedIndex = firstByte;
            else if ((firstByte & 0xC0) == 0x80)
                codedIndex = getByte(byteIndex + 1) | (firstByte & 0x7F) << 8;
            else
                codedIndex = getByte(byteIndex + 3) | getByte(byteIndex + 2) << 8 | getByte(byteIndex + 1) << 16 | (firstByte & 0x3F) << 24;

            int tokenType = codedIndex & 2;
            int tableIndex = codedIndex >> 2;

            if (tokenType == 0)
                return MetadataTokens.TypeDefinitionHandle(tableIndex);
            else if (tokenType == 1)
                return MetadataTokens.TypeReferenceHandle(tableIndex);
            else
                return MetadataTokens.TypeSpecificationHandle(tableIndex);
        }

        /// <summary>
        /// Returns the <see cref="Type"/> for the primitive type that this signature represents.
        /// </summary>
        /// <returns>The <see cref="Type"/> for the primitive type that this signature represents,
        /// or null if this signature does not represent a primitive type.</returns>
        public Type? getPrimitiveType() {
            if (byteLength != 1)
                return null;

            return (SignatureTypeCode)getFirstByte() switch {
                SignatureTypeCode.Boolean => typeof(bool),
                SignatureTypeCode.Byte => typeof(byte),
                SignatureTypeCode.Char => typeof(char),
                SignatureTypeCode.Double => typeof(double),
                SignatureTypeCode.Int16 => typeof(short),
                SignatureTypeCode.Int32 => typeof(int),
                SignatureTypeCode.Int64 => typeof(long),
                SignatureTypeCode.IntPtr => typeof(IntPtr),
                SignatureTypeCode.Object => typeof(object),
                SignatureTypeCode.SByte => typeof(sbyte),
                SignatureTypeCode.Single => typeof(float),
                SignatureTypeCode.String => typeof(string),
                SignatureTypeCode.TypedReference => typeof(TypedReference),
                SignatureTypeCode.UInt16 => typeof(ushort),
                SignatureTypeCode.UInt32 => typeof(uint),
                SignatureTypeCode.UInt64 => typeof(ulong),
                SignatureTypeCode.UIntPtr => typeof(UIntPtr),
                SignatureTypeCode.Void => typeof(void),
                _ => null,
            };
        }

        /// <summary>
        /// Creates a type signature from a <see cref="Type"/> instance.
        /// </summary>
        ///
        /// <param name="type">The type for which to create a signature.</param>
        /// <param name="handleGenerator">A handle generating function for obtaining handles of
        /// non-primitive class and value types. if this is null, only signatures of primitive types
        /// and array/pointer/by-ref types derived from primitive types can be created.</param>
        ///
        /// <returns>A <see cref="TypeSignature"/> representing the signature of <paramref name="type"/>.</returns>
        ///
        /// <exception cref="ArgumentNullException"><paramref name="handleGenerator"/> is null and
        /// <paramref name="type"/> does not represent a primitive type, an array, pointer or by-ref
        /// type composed from primitive types, or a generic type or method parameter.</exception>
        public static TypeSignature fromType(Type type, Func<Type, EntityHandle>? handleGenerator = null) {
            if (type.IsGenericParameter) {
                int position = type.GenericParameterPosition;
                return type.IsGenericMethodParameter ? forGenericMethodParam(position) : forGenericTypeParam(position);
            }

            if (type.IsSZArray) {
                return fromType(type.GetElementType(), handleGenerator).makeSZArray();
            }
            else if (type.IsByRef) {
                return fromType(type.GetElementType(), handleGenerator).makeByRef();
            }
            else if (type.IsPointer) {
                return fromType(type.GetElementType(), handleGenerator).makePointer();
            }
            else if (type.IsArray) {
                // There is no way to determine lengths and lower bounds from reflection,
                // so only encode the rank.
                int rank = type.GetArrayRank();
                return fromType(type.GetElementType(), handleGenerator).makeArray(rank, ReadOnlySpan<int>.Empty, ReadOnlySpan<int>.Empty);
            }
            else if (type.IsConstructedGenericType) {
                Type[] arguments = type.GetGenericArguments();
                var argumentSignatures = new TypeSignature[arguments.Length];

                for (int i = 0; i < arguments.Length; i++)
                    argumentSignatures[i] = fromType(arguments[i], handleGenerator);

                return fromType(type.GetGenericTypeDefinition(), handleGenerator).makeGenericInstance(argumentSignatures);
            }
            else if (s_primitiveTypeSignatures.tryGetValue(type, out TypeSignature primitiveSig)) {
                return primitiveSig;
            }
            else {
                if (handleGenerator == null) {
                    throw new ArgumentNullException(
                        nameof(handleGenerator),
                        "A handle generator must be available to create a signature of a non-primitive class or value type."
                    );
                }
                EntityHandle handle = handleGenerator(type);
                return type.IsValueType ? forValueType(handle) : forClassType(handle);
            }
        }

        /// <summary>
        /// Returns a value indicating whether this <see cref="TypeSignature"/> instance is equal to
        /// <paramref name="other"/>
        /// </summary>
        /// <param name="other">The <see cref="TypeSignature"/> instance to compare with this instance.</param>
        /// <returns>True if this instance equals <paramref name="other"/>, otherwise false.</returns>
        public bool Equals(TypeSignature other) {
            if (m_bytes == null)
                return other.m_bytes == null && m_compact == other.m_compact;

            if (other.m_bytes == null)
                return false;

            return m_bytes == other.m_bytes || m_bytes.AsSpan().SequenceEqual(other.m_bytes);
        }

        /// <summary>
        /// Returns a value indicating whether this <see cref="TypeSignature"/> instance is equal to
        /// <paramref name="other"/>
        /// </summary>
        /// <param name="other">The instance to compare with this instance.</param>
        /// <returns>True if this instance equals <paramref name="other"/>, otherwise false.</returns>
        public override bool Equals(object other) => other is TypeSignature sig && Equals(sig);

        /// <summary>
        /// Returns a hash code for this <see cref="TypeSignature"/> instance.
        /// </summary>
        public override int GetHashCode() {
            if (m_bytes == null)
                return m_compact.GetHashCode();

            var bytes = m_bytes;

            int hash = 93824883;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash + bytes[i] + 401) * 467237;

            return hash;
        }

        /// <summary>
        /// Returns a value indicating whether two <see cref="TypeSignature"/> instances are equal.
        /// </summary>
        /// <param name="x">The first <see cref="TypeSignature"/> instance.</param>
        /// <param name="y">The second <see cref="TypeSignature"/> instance.</param>
        public static bool operator ==(in TypeSignature x, in TypeSignature y) => x.Equals(y);

        /// <summary>
        /// Returns a value indicating whether two <see cref="TypeSignature"/> instances are not equal.
        /// </summary>
        /// <param name="x">The first <see cref="TypeSignature"/> instance.</param>
        /// <param name="y">The second <see cref="TypeSignature"/> instance.</param>
        public static bool operator !=(in TypeSignature x, in TypeSignature y) => !x.Equals(y);

    }

    /// <summary>
    /// Represents the type signature of a local variable in a method.
    /// </summary>
    public readonly struct LocalTypeSignature {

        private readonly TypeSignature m_sig;
        private readonly bool m_isPinned;

        /// <summary>
        /// Creates a new instance of <see cref="LocalTypeSignature"/>
        /// </summary>
        /// <param name="type">A <see cref="TypeSignature"/> representing the type of the local
        /// variable.</param>
        /// <param name="isPinned">Set to true if the local variable holds a reference that
        /// must be pinned.</param>
        public LocalTypeSignature(in TypeSignature type, bool isPinned = false) {
            m_sig = type;
            m_isPinned = isPinned;
        }

        /// <summary>
        /// Returns the <see cref="TypeSignature"/> for the local variable's type.
        /// </summary>
        public TypeSignature type => m_sig;

        /// <summary>
        /// Returns true if the local variable holds a reference that must be pinned.
        /// </summary>
        public bool isPinned => m_isPinned;

        /// <summary>
        /// Converts an instance of <see cref="TypeSignature"/> to an instance of
        /// <see cref="LocalTypeSignature"/>.
        /// </summary>
        /// <param name="type">The <see cref="TypeSignature"/> instance to convert to <see cref="LocalTypeSignature"/>.</param>
        public static implicit operator LocalTypeSignature(in TypeSignature type) => new LocalTypeSignature(type);

        /// <summary>
        /// Creates a binary signature representing the local variable declarations in a method body.
        /// </summary>
        /// <param name="localTypes">A span of <see cref="LocalTypeSignature"/> instances representing the
        /// types of the local variable declarations.</param>
        /// <returns>A byte array containing the signature.</returns>
        public static byte[] makeLocalSignature(ReadOnlySpan<LocalTypeSignature> localTypes) {
            var builder = TypeSignatureBuilder.getInstance();
            try {
                builder.appendByte((byte)SignatureKind.LocalVariables);
                builder.appendCompressedUnsignedInt(localTypes.Length);

                for (int i = 0; i < localTypes.Length; i++) {
                    ref readonly var local = ref localTypes[i];
                    if (local.isPinned)
                        builder.appendByte((byte)SignatureTypeCode.Pinned);
                    builder.appendSignature(local.type);
                }

                return builder.makeByteArray();
            }
            finally {
                builder.clear();
            }
        }

    }

    internal sealed class TypeSignatureBuilder {

        [ThreadStatic]
        private static TypeSignatureBuilder? s_threadInstance;

        private byte[] m_initialBuffer;
        private byte[] m_currentBuffer;
        private int m_pos;

        private TypeSignatureBuilder() {
            m_initialBuffer = new byte[64];
            m_currentBuffer = m_initialBuffer;
            m_pos = 0;
        }

        public static TypeSignatureBuilder getInstance() {
            ref var inst = ref s_threadInstance;
            if (inst == null)
                inst = new TypeSignatureBuilder();

            return inst;
        }

        public void clear() {
            m_currentBuffer = m_initialBuffer;
            m_pos = 0;
        }

        public void appendByte(byte value) {
            if (m_pos == m_currentBuffer.Length)
                DataStructureUtil.expandArray(ref m_currentBuffer);

            m_currentBuffer[m_pos++] = value;
        }

        public void appendSignature(in TypeSignature sig) {
            int sigLength = sig.byteLength;

            if (m_pos > m_currentBuffer.Length - sigLength)
                DataStructureUtil.resizeArray(ref m_currentBuffer, m_pos, m_pos + sigLength);

            sig.writeToSpan(m_currentBuffer.AsSpan(m_pos, sigLength));
            m_pos += sigLength;
        }

        public void appendCompressedUnsignedInt(int value) {
            if (m_pos > m_currentBuffer.Length - 4)
                DataStructureUtil.expandArray(ref m_currentBuffer, 4);

            Span<byte> span = m_currentBuffer.AsSpan(m_pos, 4);

            if ((value & ~0x7F) == 0) {
                span[0] = (byte)value;
                m_pos++;
            }
            else if ((value & ~0x3FFF) == 0) {
                span[1] = (byte)value;
                span[0] = (byte)(value >> 8 | 0x80);
                m_pos += 2;
            }
            else if ((value & ~0x1FFFFFFF) == 0) {
                span[3] = (byte)value;
                span[2] = (byte)(value >> 8);
                span[1] = (byte)(value >> 16);
                span[0] = (byte)(value >> 24 | 0xC0);
                m_pos += 4;
            }
            else {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        public void appendCompressedSignedInt(int value) {
            if (m_pos > m_currentBuffer.Length - 4)
                DataStructureUtil.expandArray(ref m_currentBuffer, 4);

            Span<byte> span = m_currentBuffer.AsSpan(m_pos, 4);

            if ((uint)(value + 0x40) < 0x80) {
                value = ((value << 1) & 0x7F) | ((value >> 6) & 1);
                span[0] = (byte)value;
                m_pos++;
            }
            else if ((uint)(value + 0x2000) < 0x4000) {
                value = ((value << 1) & 0x3FFF) | ((value >> 13) & 1);
                span[1] = (byte)value;
                span[0] = (byte)(value >> 8 | 0x80);
                m_pos += 2;
            }
            else if ((uint)(value + 0x10000000) < 0x20000000) {
                value = ((value << 1) & 0x1FFFFFFF) | ((value >> 28) & 1);
                span[3] = (byte)value;
                span[2] = (byte)(value >> 8);
                span[1] = (byte)(value >> 16);
                span[0] = (byte)(value >> 24 | 0xC0);
                m_pos += 4;
            }
            else {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        public byte[] makeByteArray() {
            var array = m_currentBuffer.AsSpan(0, m_pos).ToArray();
            return array;
        }

        public TypeSignature makeSignature() {
            TypeSignature signature;

            if (m_pos <= 7) {
                long compactSig = (long)m_pos << 56;
                for (int i = 0, shift = 0; i < m_pos; i++, shift += 8)
                    compactSig |= (long)m_currentBuffer[i] << shift;

                signature = new TypeSignature(compactSig);
            }
            else {
                signature = new TypeSignature(m_currentBuffer.AsSpan(0, m_pos).ToArray());
            }

            return signature;
        }

    }

}
