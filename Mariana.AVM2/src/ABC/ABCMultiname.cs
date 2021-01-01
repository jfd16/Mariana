using System;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// Represents a multiname in an ABC file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A multiname is a name that is used for name lookup in the AVM2. It consists
    /// of a local name, and either a single namespace or a set of namespaces. The
    /// local name, and the namespace (but not a namespace set) may be defined in the
    /// ABC file or provided at runtime.
    /// </para>
    /// </remarks>
    public readonly struct ABCMultiname : IEquatable<ABCMultiname> {

        private const int MASK_RT_NAME =
              (1 << (int)ABCConstKind.RTQNameL)
            | (1 << (int)ABCConstKind.RTQNameLA)
            | (1 << (int)ABCConstKind.MultinameL)
            | (1 << (int)ABCConstKind.MultinameLA);

        private const int MASK_RT_NAMESPACE =
             (1 << (int)ABCConstKind.RTQName)
           | (1 << (int)ABCConstKind.RTQNameA)
           | (1 << (int)ABCConstKind.RTQNameL)
           | (1 << (int)ABCConstKind.RTQNameLA);

        private const int MASK_NS_SET =
              (1 << (int)ABCConstKind.Multiname)
            | (1 << (int)ABCConstKind.MultinameA)
            | (1 << (int)ABCConstKind.MultinameL)
            | (1 << (int)ABCConstKind.MultinameLA);

        private const int MASK_ATTRIBUTE =
              (1 << (int)ABCConstKind.QNameA)
            | (1 << (int)ABCConstKind.RTQNameA)
            | (1 << (int)ABCConstKind.RTQNameLA)
            | (1 << (int)ABCConstKind.MultinameA)
            | (1 << (int)ABCConstKind.MultinameLA);

        private const int MASK_ALL_MULTINAMES =
              (1 << (int)ABCConstKind.QName)
            | (1 << (int)ABCConstKind.QNameA)
            | (1 << (int)ABCConstKind.RTQName)
            | (1 << (int)ABCConstKind.RTQNameA)
            | (1 << (int)ABCConstKind.RTQNameL)
            | (1 << (int)ABCConstKind.RTQNameLA)
            | (1 << (int)ABCConstKind.Multiname)
            | (1 << (int)ABCConstKind.MultinameA)
            | (1 << (int)ABCConstKind.MultinameL)
            | (1 << (int)ABCConstKind.MultinameLA);

        private readonly ABCConstKind m_kind;
        private readonly int m_index1;
        private readonly int m_index2;

        internal ABCMultiname(ABCConstKind kind, int index1, int index2) {
            m_kind = kind;
            m_index1 = index1;
            m_index2 = index2;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="ABCMultiname"/> instance represents
        /// a valid multiname.
        /// </summary>
        /// <remarks>
        /// The default value of the <see cref="ABCMultiname"/> type does not represent a
        /// valid multiname and this value is false. For all other multinames, this value is
        /// true.
        /// </remarks>
        public bool isValid => ((1 << (int)kind) & MASK_ALL_MULTINAMES) != 0;

        /// <summary>
        /// Gets the kind of the multiname.
        /// </summary>
        public ABCConstKind kind => m_kind;

        /// <summary>
        /// Gets a value indicating whether the namespace of this multiname is provided at runtime.
        /// </summary>
        public bool hasRuntimeNamespace => ((1 << (int)kind) & MASK_RT_NAMESPACE) != 0;

        /// <summary>
        /// Gets a value indicating whether the local name of this multiname is provided at runtime.
        /// </summary>
        public bool hasRuntimeLocalName => ((1 << (int)kind) & MASK_RT_NAME) != 0;

        /// <summary>
        /// Gets a value indicating whether the local name and/or namespace of this multiname is
        /// provided at runtime.
        /// </summary>
        public bool hasRuntimeArguments => ((1 << (int)kind) & (MASK_RT_NAME | MASK_RT_NAMESPACE)) != 0;

        /// <summary>
        /// Gets a value indicating whether this multiname uses a namespace set.
        /// </summary>
        public bool usesNamespaceSet => ((1 << (int)kind) & MASK_NS_SET) != 0;

        /// <summary>
        /// Gets a value indicating whether this multiname represents an XML attribute name.
        /// </summary>
        public bool isAttributeName => ((1 << (int)kind) & MASK_ATTRIBUTE) != 0;

        /// <summary>
        /// Gets the constant pool index of the local name of this multiname.
        /// </summary>
        /// <value>The constant pool index of the local name. To obtain the actual local
        /// name, this index must be passed to the <see cref="ABCFile.resolveString"/>
        /// method the <see cref="ABCFile"/> instance from which this multiname was obtained.
        /// If the local name is provided at runtime, or this multiname is a generic class
        /// name, this value is -1.</value>
        public int localNameIndex => (kind == ABCConstKind.GenericClassName) ? -1 : m_index2;

        /// <summary>
        /// Gets the constant pool index of the namespace (or namespace set) of this multiname.
        /// </summary>
        /// <value>The constant pool index of the namespace or namespace set (depending
        /// on the value of <see cref="usesNamespaceSet"/>). To obtain the actual namespace
        /// or namespace set, this index must be passed to the <see cref="ABCFile.resolveNamespace"/>
        /// or <see cref="ABCFile.resolveNamespaceSet"/> method the <see cref="ABCFile"/> instance
        /// from which this multiname was obtained. If the namespace is provided at runtime,
        /// or this multiname is a generic class name, this value is -1.</value>
        public int namespaceIndex => (kind == ABCConstKind.GenericClassName) ? -1 : m_index1;

        /// <summary>
        /// Gets the constant pool index of the multiname of the generic definition of
        /// a generic class name.
        /// </summary>
        /// <value>The constant pool index of the multiname of the generic definition. To
        /// obtain the actual multiname, this index must be passed to the
        /// <see cref="ABCFile.resolveMultiname"/> method of the <see cref="ABCFile"/> instance
        /// from which this multiname was obtained. If the value of <see cref="kind"/> is not
        /// <see cref="ABCConstKind.GenericClassName"/>, this value is -1.</value>
        public int genericDefIndex => (kind != ABCConstKind.GenericClassName) ? -1 : m_index1;

        /// <summary>
        /// Gets the constant pool index of the list of generic arguments of
        /// a generic class name.
        /// </summary>
        /// <value>The constant pool index of the multiname of the generic argument list. To
        /// obtain the actual multiname, this index must be passed to the
        /// <see cref="ABCFile.resolveGenericArgList"/> method of the <see cref="ABCFile"/> instance
        /// from which this multiname was obtained. If the value of <see cref="kind"/> is not
        /// <see cref="ABCConstKind.GenericClassName"/>, this value is -1.</value>
        public int genericArgListIndex => (kind != ABCConstKind.GenericClassName) ? -1 : m_index2;

        /// <summary>
        /// Returns a value indicating whether this <see cref="ABCMultiname"/> instance is
        /// equal to <paramref name="other"/>
        /// </summary>
        /// <param name="other">The <see cref="ABCMultiname"/> instance to compare with this instance.</param>
        /// <returns>True if this instance is equal to <paramref name="other"/>, otherwise false.</returns>
        /// <remarks>
        /// Two <see cref="ABCMultiname"/> instances are considered to be equal by this method if they
        /// are of the same kind and the constant pool indices applicable to that kind of multiname
        /// are equal. The result of the equality comparison only makes sense when both multinames are
        /// from the same ABC file.
        /// </remarks>
        public bool Equals(ABCMultiname other) => this == other;

        /// <summary>
        /// Returns a value indicating whether this <see cref="ABCMultiname"/> instance is
        /// equal to <paramref name="other"/>
        /// </summary>
        /// <param name="other">The object to compare with this instance.</param>
        /// <returns>True if this instance is equal to <paramref name="other"/>, otherwise false.</returns>
        /// <remarks>
        /// Two <see cref="ABCMultiname"/> instances are considered to be equal by this method if they
        /// are of the same kind and the constant pool indices applicable to that kind of multiname
        /// are equal. The result of the equality comparison only makes sense when both multinames are
        /// from the same ABC file.
        /// </remarks>
        public override bool Equals(object other) => other is ABCMultiname mn && this == mn;

        /// <summary>
        /// Returns a hash code for this <see cref="ABCMultiname"/> instance.
        /// </summary>
        /// <returns>A hash code for this <see cref="ABCMultiname"/> instance.</returns>
        public override int GetHashCode() =>
            ((m_index1 * 1194163 + m_index2) * 1617817 + (int)m_kind) * 60688217;

        /// <summary>
        /// Returns a value indicating whether two <see cref="ABCMultiname"/> instances are equal.
        /// </summary>
        /// <param name="x">The first <see cref="ABCMultiname"/> instance.</param>
        /// <param name="y">The second <see cref="ABCMultiname"/> instance.</param>
        /// <returns>True if <paramref name="x"/> is equal to <paramref name="y"/>, otherwise false.</returns>
        /// <remarks>
        /// Two <see cref="ABCMultiname"/> instances are considered to be equal by this method if they
        /// are of the same kind and the constant pool indices applicable to that kind of multiname
        /// are equal. The result of the equality comparison is well-defined only when both multinames are
        /// from the same ABC file.
        /// </remarks>
        public static bool operator ==(in ABCMultiname x, in ABCMultiname y) =>
            x.m_kind == y.m_kind && x.m_index1 == y.m_index1 && x.m_index2 == y.m_index2;

        /// <summary>
        /// Returns a value indicating whether two <see cref="ABCMultiname"/> instances are not equal.
        /// </summary>
        /// <param name="x">The first <see cref="ABCMultiname"/> instance.</param>
        /// <param name="y">The second <see cref="ABCMultiname"/> instance.</param>
        /// <returns>True if <paramref name="x"/> is not equal to <paramref name="y"/>, otherwise false.</returns>
        /// <remarks>
        /// Two <see cref="ABCMultiname"/> instances are considered to be equal by this method if they
        /// are of the same kind and the constant pool indices applicable to that kind of multiname
        /// are equal. The result of the equality comparison is well-defined only when both multinames are
        /// from the same ABC file.
        /// </remarks>
        public static bool operator !=(in ABCMultiname x, in ABCMultiname y) => !(x == y);

    }

}
