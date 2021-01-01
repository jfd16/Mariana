using System;
using System.Globalization;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A namespace is used by the AVM2 to qualify names.
    /// </summary>
    public readonly struct Namespace : IEquatable<Namespace> {

        private const int PRIVATE_MAX_ID = 0xFFFFFFF;

        /// <summary>
        /// The "any" namespace. If this namespace is used in a QName or namespace set, it will match
        /// traits/classes in any namespace provided that its local name matches. This is the
        /// namespace created by the default constructor.
        /// </summary>
        public static readonly Namespace any = default(Namespace);

        /// <summary>
        /// The public namespace.
        /// </summary>
        public static readonly Namespace @public = new Namespace(NamespaceKind.NAMESPACE, "");

        /// <summary>
        /// The AS3 namespace. This is the namespace in which class-based methods of core classes
        /// reside (the public namespace contains the corresponding prototype-based methods)
        /// </summary>
        public static readonly Namespace AS3 = new Namespace("http://adobe.com/AS3/2006/builtin");

        /// <summary>
        /// The counter for generating private namespace identifiers.
        /// </summary>
        private static IncrementCounter s_privateIdGenerator = new IncrementCounter();

        /// <summary>
        /// Contains the namespace kind in the lowest four bits and the unique identifier for
        /// a private namespace in the upper 28 bits (which should be zero for non-private namespaces)
        /// </summary>
        private readonly uint m_kindAndId;

        /// <summary>
        /// The name or URI of the namespace. For non-private namespaces, this is non-null; for
        /// private namespaces and the "any" namespace, this is null.
        /// </summary>
        public readonly string uri;

        /// <summary>
        /// Gets the kind of the namespace.
        /// </summary>
        public NamespaceKind kind => (NamespaceKind)(m_kindAndId & 15);

        /// <summary>
        /// Creates a new non-private namespace with the given type and name.
        /// </summary>
        ///
        /// <param name="kind">
        /// The kind of namespace to create. This cannot be
        /// <see cref="NamespaceKind.PRIVATE" qualifyHint="true"/>. (To create a private namespace, use
        /// the <see cref="Namespace.createPrivate()" qualifyHint="true"/> method), or
        /// <see cref="NamespaceKind.ANY" qualifyHint="true"/> (which is reserved for the "any"
        /// namespace).
        /// </param>
        /// <param name="uri">The name or URI of the namespace.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>ArgumentError #10013</term>
        /// <description>The <paramref name="kind"/> argument is
        /// <see cref="NamespaceKind.PRIVATE" qualifyHint="true"/>.</description>
        /// </item>
        /// <item>
        /// <term>ArgumentError #10014</term>
        /// <description>The <paramref name="kind"/> argument is
        /// <see cref="NamespaceKind.ANY" qualifyHint="true"/>.</description>
        /// </item>
        /// <item>
        /// <term>ArgumentError #10016</term>
        /// <description>The <paramref name="kind"/> argument is not a valid value of the
        /// <see cref="NamespaceKind"/> enumeration.</description>
        /// </item>
        /// <item>
        /// <term>ArgumentError #10017</term>
        /// <description>The <paramref name="uri"/> argument is null.</description>
        /// </item>
        /// </list>
        /// </exception>
        public Namespace(NamespaceKind kind, string uri) {
            if (kind == NamespaceKind.PRIVATE)
                throw ErrorHelper.createError(ErrorCode.MARIANA__NAMESPACE_CTOR_PRIVATE);
            if (kind == NamespaceKind.ANY)
                throw ErrorHelper.createError(ErrorCode.MARIANA__NAMESPACE_CTOR_ANY);
            if ((uint)kind > (uint)NamespaceKind.PRIVATE)
                throw ErrorHelper.createError(ErrorCode.MARIANA__INVALID_NS_CATEGORY, (int)kind);
            if (uri == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__NAMESPACE_NULL_NAME);

            this.uri = uri;
            this.m_kindAndId = (uint)kind;
        }

        /// <summary>
        /// Creates a new namespace with the given name.
        /// </summary>
        /// <param name="uri">The name or URI of the namespace. If this is null, the namespace
        /// returned will be the "any" namespace. Otherwise, the kind of the returned namespace is
        /// <see cref="NamespaceKind.NAMESPACE" qualifyHint="true"/>.</param>
        public Namespace(string uri) {
            this.uri = uri;
            this.m_kindAndId = (uri == null) ? (uint)NamespaceKind.ANY : (uint)NamespaceKind.NAMESPACE;
        }

        /// <summary>
        /// Creates a new private namespace with a given ID.
        /// </summary>
        /// <param name="privateId">An ID given to the private namespace. Two private namespaces with
        /// the same ID are considered as equal.</param>
        private Namespace(int privateId) {
            this.uri = null;
            this.m_kindAndId = (uint)((int)NamespaceKind.PRIVATE | privateId << 4);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current namespace.
        /// </summary>
        /// <param name="obj">The object to compare with the current namespace.</param>
        /// <returns>True if the specified object is a Namespace and is equal to the current
        /// Namespace; otherwise, false.</returns>
        public override bool Equals(object obj) => obj is Namespace ns && this == ns;

        /// <summary>
        /// Determines whether the specified namespace is equal to the current namespace.
        /// </summary>
        /// <param name="o">The namespace to compare with the current namespace.</param>
        /// <returns>True if the specified Namespace is equal to the current Namespace; otherwise,
        /// false.</returns>
        public bool Equals(Namespace o) => this == o;

        /// <summary>
        /// Serves as a hash function for a <see cref="Namespace"/> object.
        /// </summary>
        /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and
        /// data structures such as a hash table.</returns>
        public override int GetHashCode() =>
            (((uri == null) ? 0 : uri.GetHashCode()) * 1495451 + (int)m_kindAndId) * 5598379;

        /// <summary>
        /// Gets a value indicating whether the current Namespace instance represents the public
        /// namespace.
        /// </summary>
        ///
        /// <remarks>
        /// This method returns true if and only if <see cref="kind"/> is
        /// <see cref="NamespaceKind.NAMESPACE" qualifyHint="true"/>, and <see cref="uri"/> is the
        /// empty string.
        /// </remarks>
        public bool isPublic => m_kindAndId == (uint)NamespaceKind.NAMESPACE && uri.Length == 0;

        /// <summary>
        /// Gets a unique identifier that identifies a private namespace.
        /// </summary>
        internal int privateNamespaceId => (kind != NamespaceKind.PRIVATE) ? -1 : (int)(m_kindAndId >> 4);

        /// <summary>
        /// Returns a string that represents the current namespace.
        /// </summary>
        /// <returns>A string that represents the current namespace.</returns>
        public override string ToString() {
            switch (kind) {
                case NamespaceKind.ANY:
                    return "*";
                case NamespaceKind.NAMESPACE:
                    return uri;
                case NamespaceKind.EXPLICIT:
                    return "<explicit " + uri + ">";
                case NamespaceKind.PACKAGE_INTERNAL:
                    return "<internal " + uri + ">";
                case NamespaceKind.PRIVATE:
                    return "<private #" + (m_kindAndId >> 4).ToString(CultureInfo.InvariantCulture) + ">";
                case NamespaceKind.PROTECTED:
                    return "<protected " + uri + ">";
                case NamespaceKind.STATIC_PROTECTED:
                    return "<static protected " + uri + ">";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Creates a new private namespace. The created namespace is unique and not equal to any
        /// other namespace.
        /// </summary>
        /// <returns>The created namespace.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>Error #10015</term>
        /// <description>The maximum private namespace limit of 2^28 is exceeded.</description>
        /// </item>
        /// </list>
        /// </exception>
        public static Namespace createPrivate() {
            int id = s_privateIdGenerator.atomicNext();
            if (id > PRIVATE_MAX_ID)
                throw ErrorHelper.createError(ErrorCode.MARIANA__PRIVATE_NS_LIMIT_EXCEEDED);
            return new Namespace(id);
        }

        /// <summary>
        /// Creates a private namespace with the given identifier.
        /// </summary>
        /// <param name="id">The identifier for the private namespace. This must be an integer between
        /// 0 and 2^28-1, inclusive. This identifier is used for equality of private namespaces.</param>
        /// <returns>The created namespace.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>Error #10015</term>
        /// <description>id is negative or greater than 2^28-1.</description>
        /// </item>
        /// </list>
        /// </exception>
        public static Namespace createPrivate(int id) {
            if ((uint)id > PRIVATE_MAX_ID)
                throw ErrorHelper.createError(ErrorCode.MARIANA__PRIVATE_NS_LIMIT_EXCEEDED);
            return new Namespace(id);
        }

        /// <summary>
        /// Creates a new <see cref="Namespace"/> from an AS3 Namespace object.
        /// </summary>
        /// <param name="ns">The AS3 Namespace object.</param>
        /// <returns>A <see cref="Namespace"/> created from <paramref name="ns"/>.</returns>
        ///
        /// <remarks>
        /// If <paramref name="ns"/> is null or has a null URI, the "any" namespace is returned.
        /// Otherwise, the created <see cref="Namespace"/> will have its kind set to
        /// <see cref="NamespaceKind.NAMESPACE" qualifyHint="true"/> and its name set to the XML
        /// namespace URI.
        /// </remarks>
        public static Namespace fromASNamespace(ASNamespace ns) => (ns == null) ? Namespace.any : new Namespace(ns.uri);

        /// <summary>
        /// Converts a string to a namespace. The type of the namespace created will be
        /// <see cref="NamespaceKind.NAMESPACE" qualifyHint="true"/> and the string will be its URI.
        /// </summary>
        /// <param name="uri">The string to convert to a namespace.</param>
        /// <returns>The <see cref="Namespace"/> created from the string
        /// <paramref name="uri"/>.</returns>
        public static implicit operator Namespace(string uri) => new Namespace(uri);

        /// <summary>
        /// Determines whether two namespaces are equal.
        /// </summary>
        /// <param name="ns1">The first namespace.</param>
        /// <param name="ns2">The second namespace.</param>
        /// <returns>True if <paramref name="ns1"/> is not equal to <paramref name="ns2"/>,
        /// otherwise false.</returns>
        /// <remarks>
        /// Two namespaces are equal if they have the same type, and if they have the same name (for
        /// non-private namespaces) or the same internal identifier (for private namespaces).
        /// </remarks>
        public static bool operator ==(in Namespace ns1, in Namespace ns2) =>
            ns1.m_kindAndId == ns2.m_kindAndId && ns1.uri == ns2.uri;

        /// <summary>
        /// Determines whether two namespaces are not equal.
        /// </summary>
        /// <param name="ns1">The first namespace.</param>
        /// <param name="ns2">The second namespace.</param>
        /// <returns>True if <paramref name="ns1"/> is not equal to <paramref name="ns2"/>,
        /// otherwise false.</returns>
        /// <remarks>
        /// Two namespaces are equal if they have the same type, and if they have the same name (for
        /// non-private namespaces) or the same internal identifier (for private namespaces).
        /// </remarks>
        public static bool operator !=(in Namespace ns1, in Namespace ns2) =>
            ns1.m_kindAndId != ns2.m_kindAndId || ns1.uri != ns2.uri;

    }

}
