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

        // These arrays are initialized to null.
        // They will be set to non-null values by ABCParser.

        private int[] m_intPool = null!;
        private uint[] m_uintPool = null!;
        private double[] m_doublePool = null!;
        private string?[] m_stringPool = null!;
        private Namespace[] m_namespacePool = null!;
        private NamespaceSet[] m_nsSetPool = null!;
        private ABCMultiname[] m_multinamePool = null!;
        private ABCMultiname[][] m_genericArgListPool = null!;
        private ABCMethodInfo[] m_methodInfo = null!;
        private MetadataTag[] m_metadata = null!;
        private ABCClassInfo[] m_classInfo = null!;
        private ABCScriptInfo[] m_scriptInfo = null!;
        private ABCMethodBodyInfo[] m_methodBodyInfo = null!;

        /// <summary>
        /// Reads an ActionScript 3 bytecode file from a file.
        /// </summary>
        /// <returns>An <see cref="ABCFile"/> instance containing the ABC data parsed
        /// from the file.</returns>
        /// <param name="filename">The name of the file to read.</param>
        /// <param name="parseOptions">A set of flags from the <see cref="ABCParseOptions"/>
        /// enumeration specifying any options for the parser.</param>
        /// <exception cref="AVM2Exception">ArgumentError #10060: <paramref name="filename"/> is null.</exception>
        public static ABCFile readFromFile(string filename, ABCParseOptions parseOptions = 0) {
            if (filename == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(filename));

            using FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read);
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
        /// <exception cref="AVM2Exception">ArgumentError #10060: <paramref name="data"/> is null.</exception>
        public static ABCFile read(byte[] data, ABCParseOptions parseOptions = 0) {
            if (data == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(data));

            using var stream = new MemoryStream(data);
            return read(stream, parseOptions);
        }

        /// <summary>
        /// Reads an ActionScript 3 bytecode file from a stream.
        /// </summary>
        /// <returns>An <see cref="ABCFile"/> instance containing the ABC data parsed
        /// from the stream.</returns>
        ///
        /// <param name="stream">The <see cref="Stream"/> instance representing the stream
        /// from which to read the ABC file.</param>
        /// <param name="parseOptions">A set of flags from the <see cref="ABCParseOptions"/>
        /// enumeration specifying any options for the parser.</param>
        ///
        /// <remarks>
        /// This method does not close <paramref name="stream"/> after the ABC file has been read.
        /// </remarks>
        ///
        /// <exception cref="AVM2Exception">ArgumentError #10060: <paramref name="stream"/> is null.</exception>
        public static ABCFile read(Stream stream, ABCParseOptions parseOptions = 0) {
            if (stream == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(stream));

            var file = new ABCFile();
            var parser = new ABCParser();
            parser.parse(stream, parseOptions, file);

            return file;
        }

        private ABCFile() { }

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
        /// <exception cref="AVM2Exception">VerifyError #1032: <paramref name="index"/> is negative
        /// or outside the range of the integer constant pool.</exception>
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
        /// <exception cref="AVM2Exception">VerifyError #1032: <paramref name="index"/> is negative
        /// or outside the range of the unsigned integer constant pool.</exception>
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
        /// <exception cref="AVM2Exception">VerifyError #1032: <paramref name="index"/> is negative
        /// or outside the range of the floating-point constant pool.</exception>
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
        /// <exception cref="AVM2Exception">VerifyError #1032: <paramref name="index"/> is negative
        /// or outside the range of the string constant pool.</exception>
        public string? resolveString(int index) {
            if ((uint)index >= (uint)m_stringPool.Length)
                throw ErrorHelper.createError(ErrorCode.CONSTANT_POOL_OUT_OF_RANGE, index, m_stringPool.Length);

            return m_stringPool[index];
        }

        /// <summary>
        /// Resolves a namespace constant in this ABC file.
        /// </summary>
        /// <returns>The namespace constant.</returns>
        /// <param name="index">The index of the constant in the ABC file's constant pool.</param>
        /// <exception cref="AVM2Exception">VerifyError #1032: <paramref name="index"/> is negative
        /// or outside the range of the namespace constant pool.</exception>
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
        /// <exception cref="AVM2Exception">VerifyError #1032: <paramref name="index"/> is negative
        /// or outside the range of the namespace set constant pool.</exception>
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
        /// <exception cref="AVM2Exception">VerifyError #1032: <paramref name="index"/> is negative
        /// or outside the range of the multiname constant pool.</exception>
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
        /// <exception cref="AVM2Exception">VerifyError #1032: <paramref name="index"/> is negative
        /// or outside the range of the generic argument list constant pool.</exception>
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
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>VerifyError #1032: <paramref name="index"/> is negative
        /// or outside the range of the constant pool for the type specified by
        /// <paramref name="kind"/>.</description></item>
        /// <item><description>ArgumentError #10061: <paramref name="kind"/> is not
        /// a valid constant kind.</description></item>
        /// </list>
        /// </exception>
        public ASAny resolveConstant(ABCConstKind kind, int index) {
            return kind switch {
                ABCConstKind.Int => resolveInt(index),
                ABCConstKind.UInt => resolveUint(index),
                ABCConstKind.Double => resolveDouble(index),
                ABCConstKind.Utf8 => resolveString(index),
                ABCConstKind.True => true,
                ABCConstKind.False => false,
                ABCConstKind.Null => ASAny.@null,
                ABCConstKind.Undefined => ASAny.undefined,

                ABCConstKind.Namespace
                or ABCConstKind.PackageNamespace
                or ABCConstKind.PackageInternalNs
                or ABCConstKind.ProtectedNamespace
                or ABCConstKind.StaticProtectedNs
                or ABCConstKind.ExplicitNamespace
                or ABCConstKind.PrivateNs =>
                    new ASNamespace(resolveNamespace(index).uri!),

                _ => throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(kind)),
            };
        }

        /// <summary>
        /// Gets a <see cref="MetadataTag"/> instance representing the metadata tag at the given index.
        /// </summary>
        /// <returns>The <see cref="MetadataTag"/> instance.</returns>
        /// <param name="index">The index of the metadata tag in the ABC file.</param>
        /// <exception cref="AVM2Exception">VerifyError #10323: <paramref name="index"/> is negative
        /// or not less than the number of <c>metadata_info</c> entries in the ABC file.</exception>
        public MetadataTag resolveMetadata(int index) {
            if ((uint)index >= (uint)m_metadata.Length)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ABC_METADATA_INFO_OUT_OF_RANGE, index, m_metadata.Length);

            return m_metadata[index];
        }

        /// <summary>
        /// Gets a <see cref="ABCMethodInfo"/> instance representing the method at the given index.
        /// </summary>
        /// <returns>The <see cref="ABCMethodInfo"/> instance.</returns>
        /// <param name="index">The index of the method in the ABC file.</param>
        /// <exception cref="AVM2Exception">VerifyError #1027: <paramref name="index"/> is negative
        /// or not less than the number of <c>method_info</c> entries in the ABC file.</exception>
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
        /// <exception cref="AVM2Exception">VerifyError #1060: <paramref name="index"/> is negative
        /// or not less than the number of <c>class_info</c> entries in the ABC file.</exception>
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
        public ReadOnlyArrayView<string?> getStringPool() => new ReadOnlyArrayView<string?>(m_stringPool);

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
        /// Returns a string representation of a multiname when resolved in this ABC file.
        /// </summary>
        /// <returns>The string representation of the multiname whose constant pool index is
        /// <paramref name="multinameIndex"/>.</returns>
        /// <param name="multinameIndex">The index of the constant in the ABC file's multiname constant pool.</param>
        /// <exception cref="AVM2Exception">VerifyError #1032: <paramref name="multinameIndex"/> is negative
        /// or outside the range of the multiname constant pool.</exception>
        public string multinameToString(int multinameIndex) => multinameToString(resolveMultiname(multinameIndex));

        /// <summary>
        /// Returns a string representation of a multiname when resolved in this ABC file.
        /// </summary>
        /// <returns>The string representation of the given multiname.</returns>
        /// <param name="multiname">A multiname obtained from this ABC file.</param>
        public string multinameToString(in ABCMultiname multiname) {
            if (multiname.kind == ABCConstKind.GenericClassName) {
                string baseStr = multinameToString(multiname.genericDefIndex);
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

            string? localStr = multiname.hasRuntimeLocalName ? "{RTname}" : resolveString(multiname.localNameIndex);

            if (localStr == null && nsStr == "*")
                return "*";

            localStr ??= "*";

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

        internal void setStringPool(string?[] stringPool) => m_stringPool = stringPool;

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
