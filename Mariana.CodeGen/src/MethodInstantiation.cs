using System;
using System.Reflection.Metadata;
using Mariana.Common;

namespace Mariana.CodeGen {

    /// <summary>
    /// Represents an instantiation of a generic method.
    /// </summary>
    public readonly struct MethodInstantiation : IEquatable<MethodInstantiation> {

        private readonly EntityHandle m_handle;
        private readonly byte[] m_signature;

        /// <summary>
        /// Creates a new instance of <see cref="MethodInstantiation"/>.
        /// </summary>
        /// <param name="definition">A handle to the generic method definition. This must refer to an
        /// entry in the MethodDef or MemberRef table.</param>
        /// <param name="typeArguments">A span containing the signatures of the type arguments with
        /// which the generic method should be instantiated.</param>
        public MethodInstantiation(EntityHandle definition, ReadOnlySpan<TypeSignature> typeArguments) {
            HandleKind defKind = definition.Kind;

            if (defKind != HandleKind.MethodDefinition && defKind != HandleKind.MemberReference)
                throw new ArgumentException("Definition must be a MethodDef or MemberRef.", nameof(definition));

            if (typeArguments.Length == 0)
                throw new ArgumentException("Number of type arguments must not be zero.", nameof(typeArguments));

            m_handle = definition;
            m_signature = _createSignature(typeArguments);
        }

        /// <summary>
        /// Creates the signature for a MethodSpec from the given type arguments.
        /// </summary>
        /// <param name="typeArguments">The signatures of the type arguments used to instantiate the method.</param>
        /// <returns>A byte array containing the binary MethodSpec signature.</returns>
        private static byte[] _createSignature(ReadOnlySpan<TypeSignature> typeArguments) {
            int typeArgsByteLength = 0;
            for (int i = 0; i < typeArguments.Length; i++)
                typeArgsByteLength += typeArguments[i].byteLength;

            if (typeArguments.Length <= 127 && typeArgsByteLength <= 254) {
                Span<byte> buffer = stackalloc byte[typeArgsByteLength + 2];
                buffer[0] = 0x0A;
                buffer[1] = (byte)typeArguments.Length;

                Span<byte> curSpan = buffer.Slice(2);
                for (int i = 0; i < typeArguments.Length; i++) {
                    typeArguments[i].writeToSpan(curSpan);
                    curSpan = curSpan.Slice(typeArguments[i].byteLength);
                }

                return buffer.ToArray();
            }
            else {
                var blobBuilder = new BlobBuilder();

                blobBuilder.WriteByte(0x0A);
                blobBuilder.WriteCompressedInteger(typeArguments.Length);

                for (int i = 0; i < typeArguments.Length; i++)
                    typeArguments[i].writeToBlobBuilder(blobBuilder);

                return blobBuilder.ToArray();
            }
        }

        /// <summary>
        /// Gets the handle that represents the generic method definition whose instantiation
        /// is represented by this <see cref="MethodInstantiation"/> instance.
        /// </summary>
        public EntityHandle definitionHandle => m_handle;

        /// <summary>
        /// Gets a binary signature that represents the type arguments used for the instantiation
        /// of the generic method.
        /// </summary>
        /// <returns>The binary signature containing the instantiation type arguments, as a
        /// <see cref="ReadOnlyArrayView{Byte}"/> instance.</returns>
        public ReadOnlyArrayView<byte> getSignature() => new ReadOnlyArrayView<byte>(m_signature);

        /// <summary>
        /// Returns a value indicating whether this <see cref="MethodInstantiation"/> instance is equal to
        /// <paramref name="other"/>
        /// </summary>
        /// <param name="other">The <see cref="MethodInstantiation"/> instance to compare with this instance.</param>
        /// <returns>True if this instance equals <paramref name="other"/>, otherwise false.</returns>
        public bool Equals(MethodInstantiation other) {
            return m_handle == other.m_handle
                && (m_signature == other.m_signature || m_signature.AsSpan().SequenceEqual(other.m_signature));
        }

        /// <summary>
        /// Returns a value indicating whether this <see cref="MethodInstantiation"/> instance is equal to
        /// <paramref name="other"/>
        /// </summary>
        /// <param name="other">The instance to compare with this instance.</param>
        /// <returns>True if this instance equals <paramref name="other"/>, otherwise false.</returns>
        public override bool Equals(object other) => other is MethodInstantiation inst && Equals(inst);

        /// <summary>
        /// Returns a hash code for this <see cref="MethodInstantiation"/> instance.
        /// </summary>
        public override int GetHashCode() {
            int hash = 17215217;
            byte[] signature = m_signature;

            // Start at index 1 as all signatures start with 0x0A.
            for (int i = 1; i < signature.Length; i++)
                hash = (hash + signature[i]) * 5874263;

            return hash;
        }

    }

}
