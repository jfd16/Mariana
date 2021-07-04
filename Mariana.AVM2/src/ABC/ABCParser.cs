using System;
using System.IO;
using System.Text;
using Mariana.AVM2.Core;
using Mariana.Common;

using static System.Buffers.Binary.BinaryPrimitives;

namespace Mariana.AVM2.ABC {

    internal struct ABCParser {

        /// <summary>
        /// The maximum length of strings in the ABC file string pool which must be interned.
        /// </summary>
        private const int STRING_INTERN_MAX_LEN = 30;

        private static readonly UTF8Encoding s_utf8EncodingWithReplaceFallback =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

        private static readonly UTF8Encoding s_utf8EncodingWithThrowFallback =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        private Stream m_stream;

        private ABCFile m_abcFile;

        private DynamicArray<int[]> m_genArgLists;

        private UTF8Encoding m_utf8Encoding;

        public ABCFile parse(Stream stream, ABCParseOptions options) {
            _init(stream, options);

            ushort majorVersion = _readU16();
            ushort minorVersion = _readU16();

            m_abcFile.setVersion(majorVersion, minorVersion);

            _readConstantPools();
            _readMethodInfo();
            _readMetadata();
            _readClassInfo();
            _readScriptInfo();
            _readMethodBodies();

            return m_abcFile;
        }

        private void _init(Stream stream, ABCParseOptions options) {
            m_stream = stream;
            m_abcFile = new ABCFile();
            m_genArgLists.clear();

            m_utf8Encoding = ((options & ABCParseOptions.NO_FAIL_INVALID_UTF8) != 0)
                ? s_utf8EncodingWithReplaceFallback
                : s_utf8EncodingWithThrowFallback;
        }

        private void _readBytesFromStream(Span<byte> buffer) {
            Span<byte> remaining = buffer;

            while (!remaining.IsEmpty) {
                int bytesRead = m_stream.Read(remaining);

                if (bytesRead <= 0)
                    throw ErrorHelper.createError(ErrorCode.ABC_DATA_CORRUPT);

                remaining = remaining.Slice(bytesRead);
            }
        }

        private byte _readU8() {
            int b = m_stream.ReadByte();
            if (b == -1)
                throw ErrorHelper.createError(ErrorCode.ABC_DATA_CORRUPT);
            return (byte)b;
        }

        private ushort _readU16() {
            int lowByte = m_stream.ReadByte();
            if (lowByte == -1)
                throw ErrorHelper.createError(ErrorCode.ABC_DATA_CORRUPT);

            int highByte = m_stream.ReadByte();
            if (highByte == -1)
                throw ErrorHelper.createError(ErrorCode.ABC_DATA_CORRUPT);

            return (ushort)(highByte << 8 | lowByte);
        }

        private uint _readU32() {
            uint value = 0;
            int shift = 0;

            for (int i = 0; i < 5; i++) {
                int byteValue = m_stream.ReadByte();
                if (byteValue == -1)
                    throw ErrorHelper.createError(ErrorCode.ABC_DATA_CORRUPT);

                value |= (uint)((byteValue & 127) << shift);
                if ((byteValue & 128) == 0)
                    break;

                shift += 7;
            }

            return value;
        }

        private int _readU30() {
            uint value = _readU32();
            if ((value & 0xC0000000u) != 0u)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_ILLEGAL_U30_VALUE);
            return (int)value;
        }

        private double _readD64() {
            Span<byte> buffer = stackalloc byte[8];
            _readBytesFromStream(buffer);
            return BitConverter.Int64BitsToDouble(ReadInt64LittleEndian(buffer));
        }

        private string _readString() {
            int length = _readU30();
            if (length == 0)
                return "";

            Span<byte> buffer = (length < 1024) ? stackalloc byte[length] : new byte[length];
            _readBytesFromStream(buffer);

            string str;
            try {
                str = m_utf8Encoding.GetString(buffer);
            }
            catch {
                throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_INVAILD_UTF8_STRING);
            }

            if (str.Length <= STRING_INTERN_MAX_LEN)
                str = String.Intern(str);

            return str;
        }

        private Namespace _readNamespace() {
            var constKind = (ABCConstKind)_readU8();
            int nameIndex = _readU30();

            if (constKind == ABCConstKind.PrivateNs) {
                // Private namespaces are not supposed to have names, but SWFs compiled
                // in debug mode do have names for private namespaces, probably for use
                // by a debugger. So don't check for nameIndex==0 here.
                return Namespace.createPrivate();
            }

            string name = m_abcFile.resolveString(nameIndex);
            var kind = constKind switch {
                ABCConstKind.Namespace => NamespaceKind.NAMESPACE,
                ABCConstKind.PackageNamespace => NamespaceKind.NAMESPACE,
                ABCConstKind.ExplicitNamespace => NamespaceKind.EXPLICIT,
                ABCConstKind.PackageInternalNs => NamespaceKind.PACKAGE_INTERNAL,
                ABCConstKind.ProtectedNamespace => NamespaceKind.PROTECTED,
                ABCConstKind.StaticProtectedNs => NamespaceKind.STATIC_PROTECTED,
                _ => throw ErrorHelper.createError(ErrorCode.ILLEGAL_NAMESPACE_VALUE),
            };

            return new Namespace(kind, name);
        }

        private ABCMultiname _readMultiname() {
            var kind = (ABCConstKind)_readU8();

            switch (kind) {
                case ABCConstKind.QName:
                case ABCConstKind.QNameA:
                {
                    int nsIndex = _readU30();
                    int nameIndex = _readU30();
                    return new ABCMultiname(kind, nsIndex, nameIndex);
                }

                case ABCConstKind.Multiname:
                case ABCConstKind.MultinameA:
                {
                    int nameIndex = _readU30();
                    int nsSetIndex = _readU30();
                    if (nsSetIndex == 0)
                        throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_ILLEGAL_MULTINAME_POOL_INDEX);
                    return new ABCMultiname(kind, nsSetIndex, nameIndex);
                }

                case ABCConstKind.RTQName:
                case ABCConstKind.RTQNameA:
                    return new ABCMultiname(kind, -1, _readU30());

                case ABCConstKind.MultinameL:
                case ABCConstKind.MultinameLA:
                    return new ABCMultiname(kind, _readU30(), -1);

                case ABCConstKind.RTQNameL:
                case ABCConstKind.RTQNameLA:
                    return new ABCMultiname(kind, -1, -1);

                case ABCConstKind.GenericClassName: {
                    int defIndex = _readU30();
                    int nArgs = _readU30();
                    if (defIndex == 0)
                        throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_ILLEGAL_MULTINAME_POOL_INDEX);

                    int[] argNameIndices = new int[nArgs];
                    for (int i = 0; i < nArgs; i++) {
                        int ind = _readU30();
                        if (ind == 0)
                            throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_ILLEGAL_MULTINAME_POOL_INDEX);
                        argNameIndices[i] = ind;
                    }

                    m_genArgLists.add(argNameIndices);
                    return new ABCMultiname(kind, defIndex, m_genArgLists.length - 1);
                }

                default:
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_ILLEGAL_MULTINAME_KIND, (int)kind);
            }
        }

        private void _readConstantPools() {
            int intPoolSize = _readU30();
            if (intPoolSize != 0)
                intPoolSize--;

            int[] intPool = new int[intPoolSize + 1];
            for (int i = 1; i < intPool.Length; i++)
                intPool[i] = (int)_readU32();

            m_abcFile.setIntPool(intPool);

            int uintPoolSize = _readU30();
            if (uintPoolSize != 0)
                uintPoolSize--;

            uint[] uintPool = new uint[uintPoolSize + 1];
            for (int i = 1; i < uintPool.Length; i++)
                uintPool[i] = _readU32();

            m_abcFile.setUintPool(uintPool);

            int doublePoolSize = _readU30();
            if (doublePoolSize != 0)
                doublePoolSize--;

            double[] doublePool = new double[doublePoolSize + 1];

            doublePool[0] = Double.NaN;
            for (int i = 1; i < doublePool.Length; i++)
                doublePool[i] = _readD64();

            m_abcFile.setDoublePool(doublePool);

            int stringPoolSize = _readU30();
            if (stringPoolSize != 0)
                stringPoolSize--;

            string[] stringPool = new string[stringPoolSize + 1];
            for (int i = 1; i < stringPool.Length; i++)
                stringPool[i] = _readString();

            m_abcFile.setStringPool(stringPool);

            int nsPoolSize = _readU30();
            if (nsPoolSize != 0)
                nsPoolSize--;

            Namespace[] nsPool = new Namespace[nsPoolSize + 1];
            for (int i = 1; i < nsPool.Length; i++)
                nsPool[i] = _readNamespace();

            m_abcFile.setNamespacePool(nsPool);

            int nsSetPoolSize = _readU30();
            if (nsSetPoolSize != 0)
                nsSetPoolSize--;

            NamespaceSet[] nsSetPool = new NamespaceSet[nsSetPoolSize + 1];

            for (int i = 1; i < nsSetPool.Length; i++) {
                int setCount = _readU30();
                Namespace[] nsInSet = new Namespace[setCount];
                for (int j = 0; j < nsInSet.Length; j++) {
                    Namespace ns = m_abcFile.resolveNamespace(_readU30());
                    if (ns.isPublic) {
                        // Set the public namespace to be first in a set to improve runtime lookup performance
                        nsInSet[j] = nsInSet[0];
                        nsInSet[0] = ns;
                    }
                    else {
                        nsInSet[j] = ns;
                    }
                }
                nsSetPool[i] = new NamespaceSet(nsInSet);
            }

            m_abcFile.setNamespaceSetPool(nsSetPool);

            int multinamePoolSize = _readU30();
            if (multinamePoolSize != 0)
                multinamePoolSize--;

            ABCMultiname[] multinamePool = new ABCMultiname[multinamePoolSize + 1];

            multinamePool[0] = new ABCMultiname(ABCConstKind.QName, 0, 0);
            for (int i = 1; i < multinamePool.Length; i++)
                multinamePool[i] = _readMultiname();

            m_abcFile.setMultinamePool(multinamePool);

            int genArgListPoolSize = m_genArgLists.length;
            ABCMultiname[][] genArgListPool = new ABCMultiname[genArgListPoolSize][];

            for (int i = 0; i < genArgListPool.Length; i++) {
                int[] argIndices = m_genArgLists[i];
                var arglist = new ABCMultiname[argIndices.Length];

                for (int j = 0; j < argIndices.Length; j++)
                    arglist[j] = m_abcFile.resolveMultiname(argIndices[j]);

                genArgListPool[i] = arglist;
            }

            m_abcFile.setGenericArgListPool(genArgListPool);
        }

        private void _readMethodInfo() {
            const ABCMethodFlags validMethodFlags =
                ABCMethodFlags.NEED_REST
              | ABCMethodFlags.HAS_OPTIONAL
              | ABCMethodFlags.NEED_ACTIVATION
              | ABCMethodFlags.NEED_ARGUMENTS
              | ABCMethodFlags.SET_DXNS
              | ABCMethodFlags.HAS_PARAM_NAMES;

            int methodCount = _readU30();
            var methodInfoArr = new ABCMethodInfo[methodCount];

            for (int i = 0; i < methodCount; i++) {
                int paramCount = _readU30();
                ABCMultiname retTypeName = m_abcFile.resolveMultiname(_readU30());

                ABCMultiname[] paramTypeNames = Array.Empty<ABCMultiname>();
                ASAny[] defaultValues = null;
                string[] paramNames = null;

                if (paramCount != 0) {
                    paramTypeNames = new ABCMultiname[paramCount];
                    for (int j = 0; j < paramCount; j++)
                        paramTypeNames[j] = m_abcFile.resolveMultiname(_readU30());
                }

                string mthdName = m_abcFile.resolveString(_readU30());
                ABCMethodFlags mthdFlags = (ABCMethodFlags)_readU8();

                if ((mthdFlags & ~validMethodFlags) != 0)
                    throw ErrorHelper.createError(ErrorCode.METHOD_INFO_INVALID_FLAGS, i, (int)mthdFlags);

                // NEED_ARGUMENTS and NEED_REST cannot be set together.
                const ABCMethodFlags needArgumentsOrRest = ABCMethodFlags.NEED_ARGUMENTS | ABCMethodFlags.NEED_REST;
                if ((mthdFlags & needArgumentsOrRest) == needArgumentsOrRest)
                    throw ErrorHelper.createError(ErrorCode.METHOD_INFO_INVALID_FLAGS, i, (int)mthdFlags);

                if ((mthdFlags & ABCMethodFlags.HAS_OPTIONAL) != 0) {
                    int optionCount = _readU30();

                    if (optionCount > paramCount)
                        throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_METHOD_INFO_OPTIONAL_EXCEEDS_PARAM, i);

                    defaultValues = new ASAny[optionCount];
                    for (int j = 0; j < optionCount; j++) {
                        int val = _readU30();
                        var kind = (ABCConstKind)_readU8();
                        defaultValues[j] = m_abcFile.resolveConstant(kind, val);
                    }
                }

                if ((mthdFlags & ABCMethodFlags.HAS_PARAM_NAMES) != 0) {
                    paramNames = new string[paramCount];
                    for (int j = 0; j < paramNames.Length; j++)
                        paramNames[j] = m_abcFile.resolveString(_readU30());
                }

                methodInfoArr[i] = new ABCMethodInfo(
                    i, retTypeName, mthdName, mthdFlags, paramTypeNames, paramNames, defaultValues);
            }

            m_abcFile.setMethodInfo(methodInfoArr);
        }

        private void _readMetadata() {
            // See AVM2 overview errata for metadata_info layout.

            int tagCount = _readU30();
            var metadata = new MetadataTag[tagCount];
            var keys = new DynamicArray<string>();
            var values = new DynamicArray<string>();

            for (int i = 0; i < metadata.Length; i++) {
                string tagName = m_abcFile.resolveString(_readU30());
                int valueCount = _readU30();

                keys.ensureCapacity(valueCount);
                values.ensureCapacity(valueCount);

                for (int j = 0; j < valueCount; j++)
                    keys.add(m_abcFile.resolveString(_readU30()));
                for (int j = 0; j < valueCount; j++)
                    values.add(m_abcFile.resolveString(_readU30()));

                metadata[i] = new MetadataTag(tagName, keys.asSpan(), values.asSpan());
                keys.clear();
                values.clear();
            }

            m_abcFile.setMetadata(metadata);
        }

        private void _readClassInfo() {
            const ABCClassFlags validClassFlags =
                  ABCClassFlags.ClassFinal
                | ABCClassFlags.ClassSealed
                | ABCClassFlags.ClassInterface
                | ABCClassFlags.ClassProtectedNs;

            int classCount = _readU30();
            var classInfoArr = new ABCClassInfo[classCount];

            // Read instance_info

            for (int i = 0; i < classInfoArr.Length; i++) {
                ABCMultiname className = m_abcFile.resolveMultiname(_readU30());
                if (className.kind != ABCConstKind.QName)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_CLASS_TRAIT_NAME_NOT_QNAME);

                QName classQualifiedName = new QName(
                    m_abcFile.resolveNamespace(className.namespaceIndex),
                    m_abcFile.resolveString(className.localNameIndex)
                );

                if (classQualifiedName.ns.kind == NamespaceKind.ANY || classQualifiedName.localName == null)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_CLASS_TRAIT_NAME_NULL);

                ABCMultiname parentName = m_abcFile.resolveMultiname(_readU30());

                var flags = (ABCClassFlags)_readU8();

                if ((flags & ~validClassFlags) != 0)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_INVALID_INSTANCE_INFO_FLAGS);

                const ABCClassFlags finalOrInterface = ABCClassFlags.ClassFinal | ABCClassFlags.ClassInterface;
                if ((flags & finalOrInterface) == finalOrInterface) {
                    // ClassFinal and ClassInterface cannot be used together
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_INVALID_INSTANCE_INFO_FLAGS);
                }

                Namespace protectedNS = default(Namespace);
                if ((flags & ABCClassFlags.ClassProtectedNs) != 0)
                    protectedNS = m_abcFile.resolveNamespace(_readU30());

                int interfaceCount = _readU30();
                ABCMultiname[] interfaceNames = Array.Empty<ABCMultiname>();

                if (interfaceCount != 0) {
                    interfaceNames = new ABCMultiname[interfaceCount];
                    for (int j = 0; j < interfaceNames.Length; j++)
                        interfaceNames[j] = m_abcFile.resolveMultiname(_readU30());
                }

                ABCMethodInfo instanceInit = m_abcFile.resolveMethodInfo(_readU30());
                ABCTraitInfo[] traits = _readTraitInfo(_readU30());

                classInfoArr[i] = new ABCClassInfo(
                    i, classQualifiedName, parentName, interfaceNames, protectedNS, flags);

                classInfoArr[i].setInstanceInfo(instanceInit, traits);
            }

            // Read class_info
            for (int i = 0; i < classCount; i++) {
                ABCMethodInfo staticInit = m_abcFile.resolveMethodInfo(_readU30());
                ABCTraitInfo[] traits = _readTraitInfo(_readU30());
                classInfoArr[i].setStaticInfo(staticInit, traits);
            }

            m_abcFile.setClassInfo(classInfoArr);
        }

        private ABCTraitInfo[] _readTraitInfo(int count) {
            const ABCTraitFlags validTraitAttrs =
                ABCTraitFlags.ATTR_Final | ABCTraitFlags.ATTR_Metadata | ABCTraitFlags.ATTR_Override;

            if (count == 0)
                return Array.Empty<ABCTraitInfo>();

            var traits = new ABCTraitInfo[count];

            for (int i = 0; i < traits.Length; i++) {
                ABCMultiname traitName = m_abcFile.resolveMultiname(_readU30());
                if (traitName.kind != ABCConstKind.QName)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_CLASS_TRAIT_NAME_NOT_QNAME);

                QName traitQualifiedName = new QName(
                    m_abcFile.resolveNamespace(traitName.namespaceIndex),
                    m_abcFile.resolveString(traitName.localNameIndex)
                );

                if (traitQualifiedName.ns.kind == NamespaceKind.ANY || traitQualifiedName.localName == null)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_CLASS_TRAIT_NAME_NULL);

                ABCTraitFlags flags = (ABCTraitFlags)_readU8();

                if ((flags & ~ABCTraitFlags.KIND_MASK & ~validTraitAttrs) != 0)
                    throw ErrorHelper.createError(ErrorCode.INVALID_TRAIT_KIND, (int)flags);

                ABCTraitFlags kind = flags & ABCTraitFlags.KIND_MASK;
                ABCTraitInfo traitInfo;

                if (kind == ABCTraitFlags.Slot || kind == ABCTraitFlags.Const) {
                    int slotId = _readU30();

                    ABCMultiname typeName = m_abcFile.resolveMultiname(_readU30());
                    ASAny defaultVal = ASAny.undefined;
                    int defaultValIndex = _readU30();
                    if (defaultValIndex != 0)
                        defaultVal = m_abcFile.resolveConstant((ABCConstKind)_readU8(), defaultValIndex);

                    traitInfo = new ABCTraitInfo(traitQualifiedName, flags, slotId, typeName, defaultValIndex != 0, defaultVal);
                }
                else if (kind == ABCTraitFlags.Class) {
                    int slotId = _readU30();
                    ABCClassInfo classInfo = m_abcFile.resolveClassInfo(_readU30());
                    traitInfo = new ABCTraitInfo(traitQualifiedName, flags, slotId, classInfo);
                }
                else if (kind >= ABCTraitFlags.Method && kind <= ABCTraitFlags.Function) {
                    int slotOrDispId = _readU30();
                    ABCMethodInfo methodInfo = m_abcFile.resolveMethodInfo(_readU30());
                    traitInfo = new ABCTraitInfo(traitQualifiedName, flags, slotOrDispId, methodInfo);
                }
                else {
                    throw ErrorHelper.createError(ErrorCode.INVALID_TRAIT_KIND, (int)flags);
                }

                if ((flags & ABCTraitFlags.ATTR_Metadata) != 0) {
                    int metadataCount = _readU30();
                    if (metadataCount != 0) {
                        MetadataTag[] metadata = new MetadataTag[metadataCount];

                        for (int j = 0; j < metadata.Length; j++)
                            metadata[j] = m_abcFile.resolveMetadata(_readU30());

                        traitInfo.setMetadata(new MetadataTagCollection(metadata));
                    }
                }

                traits[i] = traitInfo;
            }

            return traits;
        }

        private void _readScriptInfo() {
            int scriptCount = _readU30();
            var scriptInfo = new ABCScriptInfo[scriptCount];

            for (int i = 0; i < scriptCount; i++) {
                ABCMethodInfo init = m_abcFile.resolveMethodInfo(_readU30());
                ABCTraitInfo[] traits = _readTraitInfo(_readU30());
                scriptInfo[i] = new ABCScriptInfo(i, init, traits);
            }

            m_abcFile.setScriptInfo(scriptInfo);
        }

        internal void _readMethodBodies() {
            int methodBodyCount = _readU30();
            var methodBodies = new ABCMethodBodyInfo[methodBodyCount];

            for (int i = 0; i < methodBodies.Length; i++) {
                ABCMethodInfo methodInfo = m_abcFile.resolveMethodInfo(_readU30());

                int maxStack = _readU30();
                int localCount = _readU30();
                int initScopeDepth = _readU30();
                int maxScopeDepth = _readU30();

                if (initScopeDepth > maxScopeDepth)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_METHOD_BODY_INVALID_SCOPE_DEPTHS, i);

                int codeSize = _readU30();
                byte[] code = new byte[codeSize];

                _readBytesFromStream(code);

                int excBlockCount = _readU30();
                var excBlocks = Array.Empty<ABCExceptionInfo>();

                if (excBlockCount != 0) {
                    excBlocks = new ABCExceptionInfo[excBlockCount];

                    for (int j = 0; j < excBlocks.Length; j++) {
                        int start = _readU30();
                        int end = _readU30();
                        int catchTarget = _readU30();

                        // See AVM2 overview errata.
                        ABCMultiname errTypeName = m_abcFile.resolveMultiname(_readU30());
                        ABCMultiname errVarName = m_abcFile.resolveMultiname(_readU30());

                        excBlocks[j] = new ABCExceptionInfo(start, end, catchTarget, errTypeName, errVarName);
                    }
                }

                ABCTraitInfo[] activationTraits = _readTraitInfo(_readU30());

                methodBodies[i] = new ABCMethodBodyInfo(
                    i, methodInfo, maxStack, initScopeDepth, maxScopeDepth, localCount, code, excBlocks, activationTraits);
            }

            m_abcFile.setMethodBodyInfo(methodBodies);
        }

    }
}
