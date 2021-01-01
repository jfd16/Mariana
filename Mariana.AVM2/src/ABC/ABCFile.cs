using System;
using System.IO;
using Mariana.AVM2.Core;
using Mariana.Common;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// Represents an ActionScript 3 byte code file.
    /// </summary>
    public sealed class ABCFile {

        private ushort m_majorVersion;

        private ushort m_minorVersion;

        private int[] m_intPool;

        private uint[] m_uintPool;

        private double[] m_doublePool;

        private string[] m_stringPool;

        private Namespace[] m_namespacePool;

        private NamespaceSet[] m_nsSetPool;

        private ABCMultiname[] m_multinamePool;

        private ABCMultiname[][] m_genericArgListPool;

        private ABCMethodInfo[] m_methodInfo;

        private MetadataTag[] m_metadata;

        private ABCClassInfo[] m_classInfo;

        private ABCScriptInfo[] m_scriptInfo;

        private ABCMethodBodyInfo[] m_methodBodyInfo;

        /// <summary>
        /// Reads an ActionScript 3 bytecode file from a file.
        /// </summary>
        /// <returns>An <see cref="ABCFile"/> instance containing the ABC data parsed
        /// from the file.</returns>
        /// <param name="filename">The name of the file to read.</param>
        /// <param name="parseOptions">A set of flags from the <see cref="ABCParseOptions"/>
        /// enumeration specifying any options for the parser.</param>
        public static ABCFile readFromFile(string filename, ABCParseOptions parseOptions = 0) {
            if (filename == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(filename));

            using (FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read))
                return read(fs, parseOptions);
        }

        /// <summary>
        /// Reads an ActionScript 3 bytecode file from a byte array.
        /// </summary>
        /// <returns>An <see cref="ABCFile"/> instance containing the ABC data parsed
        /// from the byte array.</returns>
        /// <param name="data">The byte array containing the ActioNScript 3 bytecode file.</param>
        /// <param name="parseOptions">A set of flags from the <see cref="ABCParseOptions"/>
        /// enumeration specifying any options for the parser.</param>
        public static ABCFile read(byte[] data, ABCParseOptions parseOptions = 0) {
            if (data == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(data));

            using (var stream = new MemoryStream(data))
                return read(stream, parseOptions);
        }

        /// <summary>
        /// Reads an ActionScript 3 bytecode file from a stream.
        /// </summary>
        /// <returns>An <see cref="ABCFile"/> instance containing the ABC data parsed
        /// from the stream.</returns>
        /// <param name="stream">The <see cref="Stream"/> instance representing the stream
        /// from which to read the ABC file.</param>
        /// <param name="parseOptions">A set of flags from the <see cref="ABCParseOptions"/>
        /// enumeration specifying any options for the parser.</param>
        /// <remarks>
        /// This method does not close <paramref name="stream"/> after the ABC file has been read.
        /// </remarks>
        public static ABCFile read(Stream stream, ABCParseOptions parseOptions = 0) {
            if (stream == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(stream));

            ABCParser parser = new ABCParser();
            return parser.parse(stream, parseOptions);
        }

        internal ABCFile() { }

        /// <summary>
        /// Gets the ABC major version of this ABC file.
        /// </summary>
        public int majorVersion => m_majorVersion;

        /// <summary>
        /// Gets the ABC minor version of this ABC file.
        /// </summary>
        public int minorVersion => m_minorVersion;

        /// <summary>
        /// Resolves an integer constant in this ABC file.
        /// </summary>
        /// <returns>The integer constant.</returns>
        /// <param name="index">The index of the constant in the ABC file's constant pool.</param>
        public int resolveInt(int index) {
            if ((uint)index >= (uint)m_intPool.Length)
                throw ErrorHelper.createError(ErrorCode.CONSTANT_POOL_OUT_OF_RANGE, index, m_intPool.Length);
            return m_intPool[index];
        }

        /// <summary>
        /// Resolves an unsigned integer constant in this ABC file.
        /// </summary>
        /// <returns>The unsigned integer constant.</returns>
        /// <param name="index">The index of the constant in the ABC file's constant pool.</param>
        public uint resolveUint(int index) {
            if ((uint)index >= (uint)m_uintPool.Length)
                throw ErrorHelper.createError(ErrorCode.CONSTANT_POOL_OUT_OF_RANGE, index, m_uintPool.Length);
            return m_uintPool[index];
        }

        /// <summary>
        /// Resolves a floating-point constant in this ABC file.
        /// </summary>
        /// <returns>The floating-point constant.</returns>
        /// <param name="index">The index of the constant in the ABC file's constant pool.</param>
        public double resolveDouble(int index) {
            if ((uint)index >= (uint)m_doublePool.Length)
                throw ErrorHelper.createError(ErrorCode.CONSTANT_POOL_OUT_OF_RANGE, index, m_doublePool.Length);
            return m_doublePool[index];
        }

        /// <summary>
        /// Resolves a string constant in this ABC file.
        /// </summary>
        /// <returns>The string constant.</returns>
        /// <param name="index">The index of the constant in the ABC file's constant pool.</param>
        public string resolveString(int index) {
            if ((uint)index >= (uint)m_stringPool.Length)
                throw ErrorHelper.createError(ErrorCode.CONSTANT_POOL_OUT_OF_RANGE, index, m_stringPool.Length);
            return m_stringPool[index];
        }

        /// <summary>
        /// Resolves a namespace constant in this ABC file.
        /// </summary>
        /// <returns>The namespace constant.</returns>
        /// <param name="index">The index of the constant in the ABC file's constant pool.</param>
        public Namespace resolveNamespace(int index) {
            if ((uint)index >= (uint)m_namespacePool.Length)
                throw ErrorHelper.createError(ErrorCode.CONSTANT_POOL_OUT_OF_RANGE, index, m_namespacePool.Length);
            return m_namespacePool[index];
        }

        /// <summary>
        /// Resolves a namespace set constant in this ABC file.
        /// </summary>
        /// <returns>The namespace set constant.</returns>
        /// <param name="index">The index of the constant in the ABC file's constant pool.</param>
        public NamespaceSet resolveNamespaceSet(int index) {
            if ((uint)index >= (uint)m_nsSetPool.Length)
                throw ErrorHelper.createError(ErrorCode.CONSTANT_POOL_OUT_OF_RANGE, index, m_nsSetPool.Length);
            return m_nsSetPool[index];
        }

        /// <summary>
        /// Resolves a multiname constant in this ABC file.
        /// </summary>
        /// <returns>The multiname constant.</returns>
        /// <param name="index">The index of the constant in the ABC file's constant pool.</param>
        public ABCMultiname resolveMultiname(int index) {
            if ((uint)index >= (uint)m_multinamePool.Length)
                throw ErrorHelper.createError(ErrorCode.CONSTANT_POOL_OUT_OF_RANGE, index, m_multinamePool.Length);
            return m_multinamePool[index];
        }

        /// <summary>
        /// Resolves a generic argument list constant in this ABC file.
        /// </summary>
        /// <returns>The generic argument list as a read-only array view of <see cref="ABCMultiname"/>
        /// instances.</returns>
        /// <param name="index">The index of the constant in the ABC file's constant pool.</param>
        public ReadOnlyArrayView<ABCMultiname> resolveGenericArgList(int index) {
            if ((uint)index >= (uint)m_multinamePool.Length)
                throw ErrorHelper.createError(ErrorCode.CONSTANT_POOL_OUT_OF_RANGE, index, m_genericArgListPool.Length);
            return new ReadOnlyArrayView<ABCMultiname>(m_genericArgListPool[index]);
        }

        /// <summary>
        /// Resolves a constant value in the ABC file.
        /// </summary>
        /// <returns>The constant value, boxed into an <see cref="ASAny"/> instance.</returns>
        /// <param name="kind">The type of the constant.</param>
        /// <param name="index">The index of the constant in the ABC file, in the constant pool
        /// determined by <paramref name="kind"/>.</param>
        public ASAny resolveConstant(ABCConstKind kind, int index) {
            switch (kind) {
                case ABCConstKind.Int:
                    return resolveInt(index);
                case ABCConstKind.UInt:
                    return resolveUint(index);
                case ABCConstKind.Double:
                    return resolveDouble(index);
                case ABCConstKind.Utf8:
                    return resolveString(index);
                case ABCConstKind.True:
                    return true;
                case ABCConstKind.False:
                    return false;
                case ABCConstKind.Null:
                    return ASAny.@null;
                case ABCConstKind.Undefined:
                    return ASAny.undefined;
                case ABCConstKind.Namespace:
                case ABCConstKind.PackageNamespace:
                case ABCConstKind.PackageInternalNs:
                case ABCConstKind.ProtectedNamespace:
                case ABCConstKind.StaticProtectedNs:
                case ABCConstKind.ExplicitNamespace:
                case ABCConstKind.PrivateNs:
                    return new ASNamespace(resolveNamespace(index).uri);
                default:
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(kind));
            }
        }

        /// <summary>
        /// Gets a <see cref="MetadataTag"/> instance representing the metadata tag at the given index.
        /// </summary>
        /// <returns>The <see cref="MetadataTag"/> instance.</returns>
        /// <param name="index">The index of the metadata tag in the ABC file.</param>
        public MetadataTag resolveMetadata(int index) {
            if ((uint)index >= (uint)m_metadata.Length)
                throw null;
            return m_metadata[index];
        }

        /// <summary>
        /// Gets a <see cref="ABCMethodInfo"/> instance representing the method at the given index.
        /// </summary>
        /// <returns>The <see cref="ABCMethodInfo"/> instance.</returns>
        /// <param name="index">The index of the method in the ABC file.</param>
        public ABCMethodInfo resolveMethodInfo(int index) {
            if ((uint)index >= (uint)m_methodInfo.Length)
                throw ErrorHelper.createError(ErrorCode.METHOD_INFO_OUT_OF_RANGE, index, m_methodInfo.Length);
            return m_methodInfo[index];
        }

        /// <summary>
        /// Gets a <see cref="ABCClassInfo"/> instance representing the class at the given index.
        /// </summary>
        /// <returns>The <see cref="ABCClassInfo"/> instance.</returns>
        /// <param name="index">The index of the class in the ABC file.</param>
        public ABCClassInfo resolveClassInfo(int index) {
            if ((uint)index >= (uint)m_classInfo.Length)
                throw ErrorHelper.createError(ErrorCode.CLASS_INFO_OUT_OF_RANGE, index, m_classInfo.Length);
            return m_classInfo[index];
        }

        /// <summary>
        /// Gets a read-only array view containing the values of this ABC file's integer constant pool.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{Int32}"/> containing the values in the integer
        /// constant pool.</returns>
        public ReadOnlyArrayView<int> getIntPool() => new ReadOnlyArrayView<int>(m_intPool);

        /// <summary>
        /// Gets a read-only array view containing the values of this ABC file's unsigned integer constant pool.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{UInt32}"/> containing the values in the
        /// unsigned integer constant pool.</returns>
        public ReadOnlyArrayView<uint> getUintPool() => new ReadOnlyArrayView<uint>(m_uintPool);

        /// <summary>
        /// Gets a read-only array view containing the values of this ABC file's floating point constant pool.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{Double}"/> containing the values in the
        /// floating point constant pool.</returns>
        public ReadOnlyArrayView<double> getDoublePool() => new ReadOnlyArrayView<double>(m_doublePool);

        /// <summary>
        /// Gets a read-only array view containing the values of this ABC file's string constant pool.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{Double}"/> containing the values in the
        /// string constant pool.</returns>
        public ReadOnlyArrayView<string> getStringPool() => new ReadOnlyArrayView<string>(m_stringPool);

        /// <summary>
        /// Gets a read-only array view containing the values of this ABC file's namespace constant pool.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{Namespace}"/> containing the values in the
        /// namespace constant pool.</returns>
        public ReadOnlyArrayView<Namespace> getNamespacePool() => new ReadOnlyArrayView<Namespace>(m_namespacePool);

        /// <summary>
        /// Gets a read-only array view containing the values of this ABC file's namespace set constant pool.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{NamespaceSet}"/> containing the values in the
        /// namespace set constant pool.</returns>
        public ReadOnlyArrayView<NamespaceSet> getNamespaceSetPool() => new ReadOnlyArrayView<NamespaceSet>(m_nsSetPool);

        /// <summary>
        /// Gets a read-only array view containing the values of this ABC file's multiname constant pool.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{NamespaceSet}"/> containing the values in the
        /// multiname constant pool.</returns>
        public ReadOnlyArrayView<ABCMultiname> getMultinamePool() => new ReadOnlyArrayView<ABCMultiname>(m_multinamePool);

        /// <summary>
        /// Gets an array containing the method definitions in this ABC file.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{ABCMethodInfo}"/> containing the method definitions
        /// in this ABC file.</returns>
        public ReadOnlyArrayView<ABCMethodInfo> getMethodInfo() => new ReadOnlyArrayView<ABCMethodInfo>(m_methodInfo);

        /// <summary>
        /// Gets an array containing the metadata tag definitions in this ABC file.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{TraitMetadataTag}"/> containing the metadata tag definitions
        /// in this ABC file.</returns>
        public ReadOnlyArrayView<MetadataTag> getMetadata() => new ReadOnlyArrayView<MetadataTag>(m_metadata);

        /// <summary>
        /// Gets an array containing the script definitions in this ABC file.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{ABCScriptInfo}"/> containing the script definitions
        /// in this ABC file.</returns>
        public ReadOnlyArrayView<ABCClassInfo> getClassInfo() => new ReadOnlyArrayView<ABCClassInfo>(m_classInfo);

        /// <summary>
        /// Gets an array containing the class definitions in this ABC file.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{ABCClassInfo}"/> containing the class definitions
        /// in this ABC file.</returns>
        public ReadOnlyArrayView<ABCScriptInfo> getScriptInfo() => new ReadOnlyArrayView<ABCScriptInfo>(m_scriptInfo);

        /// <summary>
        /// Gets an array containing the method body definitions in this ABC file.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{ABCMethodBodyInfo}"/> containing the method body definitions
        /// in this ABC file.</returns>
        public ReadOnlyArrayView<ABCMethodBodyInfo> getMethodBodyInfo() => new ReadOnlyArrayView<ABCMethodBodyInfo>(m_methodBodyInfo);

        /// <summary>
        /// Returns a value indicating whether the given multiname represents the "any" name
        /// when resolved in this ABC file.
        /// </summary>
        /// <returns>True if <paramref name="multiname"/> represents the "any" name,
        /// otherwise false.</returns>
        /// <param name="multiname">A multiname.</param>
        public bool isAnyName(in ABCMultiname multiname) {
            if (multiname.kind != ABCConstKind.QName)
                return false;

            return resolveString(multiname.localNameIndex) == null
                && resolveNamespace(multiname.namespaceIndex).kind == NamespaceKind.ANY;
        }

        /// <summary>
        /// Returns a string representation of a multiname when resolved in this ABC file.
        /// </summary>
        /// <returns>The string representation of the given multiname.</returns>
        /// <param name="multiname">A multiname.</param>
        public string multinameToString(in ABCMultiname multiname) {
            if (multiname.kind == ABCConstKind.GenericClassName) {
                string baseStr = multinameToString(resolveMultiname(multiname.genericDefIndex));
                var args = resolveGenericArgList(multiname.genericArgListIndex);
                var argStrs = new string[args.length];

                for (int i = 0; i < args.length; i++)
                    argStrs[i] = multinameToString(args[i]);

                return baseStr + ".<" + String.Join(",", argStrs) + ">";
            }

            string nsStr;

            if (multiname.hasRuntimeNamespace)
                nsStr = "{RTns}";
            else if (multiname.usesNamespaceSet)
                nsStr = resolveNamespaceSet(multiname.namespaceIndex).ToString();
            else
                nsStr = resolveNamespace(multiname.namespaceIndex).ToString();

            string localStr;

            if (multiname.hasRuntimeLocalName)
                localStr = "{RTname}";
            else
                localStr = resolveString(multiname.localNameIndex);

            if (localStr == null) {
                if (nsStr == "*")
                    return "*";
                localStr = "*";
            }

            if (multiname.isAttributeName)
                localStr = "@" + localStr;

            if (nsStr.Length == 0)
                return localStr;

            return nsStr + "::" + localStr;
        }

        internal void setVersion(ushort major, ushort minor) {
            m_majorVersion = major;
            m_minorVersion = minor;
        }

        internal void setIntPool(int[] intPool) => m_intPool = intPool;

        internal void setUintPool(uint[] uintPool) => m_uintPool = uintPool;

        internal void setDoublePool(double[] doublePool) => m_doublePool = doublePool;

        internal void setStringPool(string[] stringPool) => m_stringPool = stringPool;

        internal void setNamespacePool(Namespace[] nsPool) => m_namespacePool = nsPool;

        internal void setNamespaceSetPool(NamespaceSet[] nsSetPool) => m_nsSetPool = nsSetPool;

        internal void setMultinamePool(ABCMultiname[] multinamePool) => m_multinamePool = multinamePool;

        internal void setGenericArgListPool(ABCMultiname[][] genericArgListPool) => m_genericArgListPool = genericArgListPool;

        internal void setMethodInfo(ABCMethodInfo[] methodInfo) => m_methodInfo = methodInfo;

        internal void setMetadata(MetadataTag[] metadata) => m_metadata = metadata;

        internal void setClassInfo(ABCClassInfo[] classInfo) => m_classInfo = classInfo;

        internal void setScriptInfo(ABCScriptInfo[] scriptInfo) => m_scriptInfo = scriptInfo;

        internal void setMethodBodyInfo(ABCMethodBodyInfo[] methodBodyInfo) => m_methodBodyInfo = methodBodyInfo;

    }

}
