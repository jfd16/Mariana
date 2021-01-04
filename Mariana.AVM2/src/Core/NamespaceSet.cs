using System;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A NamespaceSet is used for multiname lookups in the AVM2, where a property with a given
    /// name in one of a set of namespaces is requested.
    /// </summary>
    public struct NamespaceSet {

        /// <summary>
        /// The bit set in the <see cref="m_flags"/> field if this namespace set contains the public
        /// namespace.
        /// </summary>
        private const int PUBLIC_FLAG = 0x100;

        /// <summary>
        /// The namespaces in the set.
        /// </summary>
        private Namespace[] m_namespaces;

        /// <summary>
        /// A bit mask used to check whether a namespace of a given kind belongs to this set.
        /// </summary>
        ///
        /// <remarks>
        /// For each value of the <see cref="NamespaceKind"/> enumeration, the bit whose position is
        /// equal to the integer value of that enumerator is set if this namespace set contains a
        /// namespace in that kind. In addition, if the namespace set contains the public
        /// namespace, the <see cref="PUBLIC_FLAG"/> bit is set.
        /// </remarks>
        private int m_flags;

        /// <summary>
        /// Creates a new namespace set with the given namespaces.
        /// </summary>
        /// <param name="arr">The namespaces in the set.</param>
        public NamespaceSet(params Namespace[] arr) {
            m_flags = 0;

            if (arr == null || arr.Length == 0) {
                m_namespaces = null;
                return;
            }

            var list = new DynamicArray<Namespace>(arr.Length);

            for (int i = 0; i < arr.Length; i++) {
                Namespace ns = arr[i];

                bool exists = false;
                for (int j = 0; j < list.length; j++) {
                    if (list[j] == ns) {
                        exists = true;
                        break;
                    }
                }

                if (!exists) {
                    list.add(ns);
                    m_flags |= 1 << (int)ns.kind;
                    if (ns.isPublic)
                        m_flags |= PUBLIC_FLAG;
                }
            }

            m_namespaces = list.toArray();
        }

        /// <summary>
        /// Returns true if the namespace set contains the "any" namespace.
        /// </summary>
        public bool containsAny => (m_flags & (1 << (int)NamespaceKind.ANY)) != 0;

        /// <summary>
        /// Returns true if the namespace set contains the public namespace.
        /// </summary>
        public bool containsPublic => (m_flags & PUBLIC_FLAG) != 0;

        /// <summary>
        /// Returns the number of namespaces in this set.
        /// </summary>
        public int count => (m_namespaces == null) ? 0 : m_namespaces.Length;

        /// <summary>
        /// Returns true if the namespace set contains a namespace of the given kind.
        /// </summary>
        /// <param name="kind">The namespace kind to check.</param>
        /// <returns>True if the namespace set contains a namespace of the kind
        /// <paramref name="kind"/>, otherwise false.</returns>
        public bool contains(NamespaceKind kind) => (m_flags & (1 << (int)kind)) != 0;

        /// <summary>
        /// Returns true if the namespace set contains a namespace with the given URI whose kind
        /// is <see cref="NamespaceKind.NAMESPACE"/>.
        /// </summary>
        /// <param name="nsUri">The namespace URI to check.</param>
        /// <returns>True if the namespace set contains a namespace whose URI is <paramref name="nsUri"/>,
        /// and whose kind is <see cref="NamespaceKind.NAMESPACE"/>, otherwise false.</returns>
        public bool contains(string nsUri) {
            if ((m_flags & (1 << (int)NamespaceKind.NAMESPACE)) == 0)
                return false;

            var namespaces = m_namespaces;

            if (namespaces == null)
                return false;

            for (int i = 0; i < namespaces.Length; i++) {
                Namespace ns = namespaces[i];
                if (ns.kind == NamespaceKind.NAMESPACE && ns.uri == nsUri)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the namespace set contains the given namespace.
        /// </summary>
        /// <param name="ns">The namespace to check.</param>
        /// <returns>True if the namespace set contains the namespace <paramref name="ns"/>,
        /// otherwise false.</returns>
        public bool contains(in Namespace ns) {
            if ((m_flags & (1 << (int)ns.kind)) == 0)
                return false;

            var namespaces = m_namespaces;

            if (namespaces == null)
                return false;

            for (int i = 0; i < namespaces.Length; i++) {
                if (namespaces[i] == ns)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns an <see cref="ReadOnlyArrayView{Namespace}"/> containing all the namespaces in the set.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{Namespace}"/> containing all the namespaces in the
        /// set.</returns>
        public ReadOnlyArrayView<Namespace> getNamespaces() => new ReadOnlyArrayView<Namespace>(m_namespaces);

        /// <summary>
        /// Returns a string representation of the namespace set.
        /// </summary>
        /// <returns>A string representation of the namespace set.</returns>
        public override string ToString() {
            if (m_namespaces == null)
                return "[]";

            var sb = new System.Text.StringBuilder();
            sb.Append('[');

            var namespaces = m_namespaces;

            for (int i = 0; i < namespaces.Length; i++) {
                if (i != 0) {
                    sb.Append(',');
                    sb.Append(' ');
                }
                if (namespaces[i].isPublic)
                    sb.Append("<public>");
                else
                    sb.Append(namespaces[i].ToString());
            }

            sb.Append(']');
            return sb.ToString();
        }

    }

}
