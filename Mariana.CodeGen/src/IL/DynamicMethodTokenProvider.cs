using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Mariana.Common;

namespace Mariana.CodeGen.IL {

    /// <summary>
    /// An IL token provider for use when using <see cref="ILBuilder"/> to emit code for a
    /// <see cref="DynamicMethod"/>.
    /// </summary>
    /// <remarks>
    /// Methods of this type are not threadsafe.
    /// </remarks>
    public sealed class DynamicMethodTokenProvider : ILTokenProvider {

        private DynamicILInfo m_dynamicILInfo;
        private ReferenceDictionary<object, int> m_tokenCache = new ReferenceDictionary<object, int>();
        private Func<Type, EntityHandle> m_typeHandleGenerator;
        private Dictionary<EntityHandle, MethodStackChangeInfo>? m_methodStackChangeInfo;

        /// <summary>
        /// Creates a new instance of <see cref="DynamicMethodTokenProvider"/>.
        /// </summary>
        /// <param name="dynamicILInfo">The <see cref="DynamicILInfo"/> instance for the dynamic method
        /// for which code is being emitted.</param>
        /// <param name="trackMethodStackChanges">If this is set to true, computes stack change
        /// information for external methods for computing the maxstack value of the emitted
        /// method body. If this is false, all method calls are assumed to pop nothing and push
        /// a return value for the purpose of computing maxstack. Note that computing stack
        /// change information requires reflection calls that may have a performance cost.</param>
        public DynamicMethodTokenProvider(DynamicILInfo dynamicILInfo, bool trackMethodStackChanges = false) {
            m_dynamicILInfo = null!;
            setDynamicILInfo(dynamicILInfo);

            m_typeHandleGenerator = type => getHandle(type);

            if (trackMethodStackChanges)
                m_methodStackChangeInfo = new Dictionary<EntityHandle, MethodStackChangeInfo>();
        }

        /// <summary>
        /// Sets the <see cref="DynamicILInfo"/> instance for the dynamic method for which code
        /// is being emitted.
        /// </summary>
        /// <param name="dynamicILInfo">A <see cref="DynamicILInfo"/> instance.</param>
        public void setDynamicILInfo(DynamicILInfo dynamicILInfo) {
            if (dynamicILInfo == null)
                throw new ArgumentNullException(nameof(dynamicILInfo));

            if (dynamicILInfo == m_dynamicILInfo)
                return;

            m_dynamicILInfo = dynamicILInfo;
            m_tokenCache.clear();

            if (m_methodStackChangeInfo != null)
                m_methodStackChangeInfo.Clear();
        }

        /// <inheritdoc/>
        public override EntityHandle getHandle(Type type) {
            if (m_tokenCache.tryGetValue(type, out int token))
                return MetadataTokens.EntityHandle(token);

            token = m_dynamicILInfo.GetTokenFor(type.TypeHandle);
            m_tokenCache[type] = token;
            return MetadataTokens.EntityHandle(token);
        }

        /// <inheritdoc/>
        public override EntityHandle getHandle(FieldInfo fieldInfo) {
            if (m_tokenCache.tryGetValue(fieldInfo, out int token))
                return MetadataTokens.EntityHandle(token);

            token = fieldInfo.DeclaringType.IsConstructedGenericType
                ? m_dynamicILInfo.GetTokenFor(fieldInfo.FieldHandle, fieldInfo.DeclaringType.TypeHandle)
                : m_dynamicILInfo.GetTokenFor(fieldInfo.FieldHandle);

            m_tokenCache[fieldInfo] = token;
            return MetadataTokens.EntityHandle(token);
        }

        /// <inheritdoc/>
        public override EntityHandle getHandle(MethodBase method) {
            if (m_tokenCache.tryGetValue(method, out int token))
                return MetadataTokens.EntityHandle(token);

            if (method is DynamicMethod dm) {
                token = m_dynamicILInfo.GetTokenFor(dm);
            }
            else {
                token = method.DeclaringType.IsConstructedGenericType
                    ? m_dynamicILInfo.GetTokenFor(method.MethodHandle, method.DeclaringType.TypeHandle)
                    : m_dynamicILInfo.GetTokenFor(method.MethodHandle);
            }

            EntityHandle handle = MetadataTokens.EntityHandle(token);

            if (m_methodStackChangeInfo != null)
                m_methodStackChangeInfo.Add(handle, new MethodStackChangeInfo(method));

            m_tokenCache[method] = token;
            return handle;
        }

        /// <inheritdoc/>
        public override UserStringHandle getHandle(string str) {
            if (m_tokenCache.tryGetValue(str, out int token))
                return MetadataTokens.UserStringHandle(token);

            token = m_dynamicILInfo.GetTokenFor(str);
            m_tokenCache[str] = token;
            return MetadataTokens.UserStringHandle(token);
        }

        /// <inheritdoc/>
        public override EntityHandle getHandle(in TypeSignature type) => type.getHandleOfClassOrValueType();

        /// <inheritdoc/>
        public override bool isVirtualHandle(EntityHandle handle) => false;

        /// <inheritdoc/>
        public override TypeSignature getTypeSignature(Type type) => TypeSignature.fromType(type, m_typeHandleGenerator);

        /// <inheritdoc/>
        public override bool useLocalSigHelper => true;

        /// <inheritdoc/>
        public override StandaloneSignatureHandle getLocalSignatureHandle(ReadOnlySpan<byte> localSig) => default;

        /// <inheritdoc/>
        public override int getMethodStackDelta(EntityHandle handle, ILOp opcode) {
            if (m_methodStackChangeInfo != null
                && m_methodStackChangeInfo.TryGetValue(handle, out MethodStackChangeInfo stackChangeInfo))
            {
                return stackChangeInfo.getStackDelta(opcode);
            }
            return 1;
        }

    }

}
