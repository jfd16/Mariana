using System;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A qualified name used for AVM2 classes and traits, consisting of a local name and a
    /// namespace.
    /// </summary>
    public readonly struct QName : IEquatable<QName> {

        /// <summary>
        /// The namespace of the qualified name.
        /// </summary>
        public readonly Namespace ns;

        /// <summary>
        /// The local name of the qualified name.
        /// </summary>
        public readonly string localName;

        /// <summary>
        /// Creates a new <see cref="QName"/> with a given name and namespace.
        /// </summary>
        /// <param name="ns">The namespace of the <see cref="QName"/>.</param>
        /// <param name="localName">The local name of the <see cref="QName"/>.</param>
        public QName(in Namespace ns, string localName) {
            this.ns = ns;
            this.localName = localName;
        }

        /// <summary>
        /// Creates a new <see cref="QName"/> from a namespace URI and local name. The kind of
        /// the namespace is set to <see cref="NamespaceKind.NAMESPACE" qualifyHint="true"/>
        /// </summary>
        /// <param name="uri">The namespace name (URI) of the <see cref="QName"/>. If this is
        /// null, the namespace is set to the "any" namespace.</param>
        /// <param name="localName">The local name of the <see cref="QName"/>.</param>
        public QName(string uri, string localName) {
            this.ns = new Namespace(uri);
            this.localName = localName;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current <see cref="QName"/>.
        /// </summary>
        /// <param name="obj">The object to compare with the current <see cref="QName"/>.</param>
        /// <returns>True if the specified object is a <see cref="QName"/> and is equal to the
        /// current <see cref="QName"/>; otherwise, false.</returns>
        public override bool Equals(object obj) => obj is QName qname && this == qname;

        /// <summary>
        /// Determines whether the specified <see cref="QName"/> is equal to the current
        /// <see cref="QName"/>.
        /// </summary>
        /// <param name="obj">The <see cref="QName"/> to compare with the current
        /// <see cref="QName"/>.</param>
        /// <returns>True if the specified <see cref="QName"/> is equal to the current
        /// <see cref="QName"/>; otherwise, false.</returns>
        public bool Equals(QName obj) => this == obj;

        /// <summary>
        /// Serves as a hash function for a <see cref="QName"/> object.
        /// </summary>
        /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and
        /// data structures such as a hash table.</returns>
        public override int GetHashCode() =>
            (ns.GetHashCode() + ((localName == null) ? 0 : localName.GetHashCode())) * 4746821;

        /// <summary>
        /// Returns a string representation of the current <see cref="QName"/>.
        /// </summary>
        /// <returns>A string representation of the current <see cref="QName"/>.</returns>
        ///
        /// <remarks>
        /// String returned by this method are intended to be human-readable. They should not be used
        /// as dictionary keys, as it is not guaranteed that this method will never output the same
        /// string representation for two different QNames.
        /// </remarks>
        public override string ToString() {
            string localNameStr = localName ?? "null";
            return ns.isPublic ? localNameStr : ns.ToString() + "::" + localNameStr;
        }

        /// <summary>
        /// Converts a string into a <see cref="QName"/>. This conversion operator calls the
        /// <see cref="publicName"/> method.
        /// </summary>
        /// <param name="name">The string to covert to a <see cref="QName"/>.</param>
        /// <returns>A <see cref="QName"/> in the public namespace whose local name is
        /// <paramref name="name"/>.</returns>
        public static implicit operator QName(string name) => publicName(name);

        /// <summary>
        /// Determines whether two QNames are equal to each other.
        /// </summary>
        /// <param name="name1">The first <see cref="QName"/>.</param>
        /// <param name="name2">The second <see cref="QName"/>.</param>
        /// <returns>True if <paramref name="name1"/> is equal to <paramref name="name2"/>,
        /// otherwise false.</returns>
        /// <remarks>
        /// Two qualified names are equal if their local names are equal, and their namespaces are
        /// equal.
        /// </remarks>
        public static bool operator ==(in QName name1, in QName name2) =>
            name1.localName == name2.localName && name1.ns == name2.ns;

        /// <summary>
        /// Determines whether two QNames are not equal to each other.
        /// </summary>
        /// <param name="name1">The first <see cref="QName"/>.</param>
        /// <param name="name2">The second <see cref="QName"/>.</param>
        /// <returns>True if <paramref name="name1"/> is not equal to <paramref name="name2"/>,
        /// otherwise false.</returns>
        /// <remarks>
        /// Two qualified names are equal if their local names are equal and their namespaces are
        /// equal.
        /// </remarks>
        public static bool operator !=(in QName name1, in QName name2) =>
            name1.localName != name2.localName || name1.ns != name2.ns;

        /// <summary>
        /// Creates a <see cref="QName"/> object in the public namespace with the given local name.
        /// </summary>
        /// <param name="localName">The local name of the <see cref="QName"/>.</param>
        /// <returns>The created <see cref="QName"/>.</returns>
        public static QName publicName(string localName) => new QName(Namespace.@public, localName);

        /// <summary>
        /// Converts an AS3 object to a <see cref="QName"/>.
        /// </summary>
        /// <param name="obj">The object to convert to a <see cref="QName"/>.</param>
        /// <returns>The <see cref="QName"/> created from the object
        /// <paramref name="obj"/>.</returns>
        ///
        /// <remarks>
        /// <see cref="ASQName"/> objects will be converted into <see cref="QName"/> structures
        /// using both their local name and namespace URI; any other object is converted to a string
        /// and used as the local name with the namespace being set to the public namespace.
        /// </remarks>
        public static QName fromObject(ASObject obj) {
            if (obj is ASQName qName)
                return new QName(new Namespace(qName.uri), qName.localName);

            if (obj == null)
                return default(QName);

            return new QName(Namespace.@public, obj.ToString());
        }

        /// <summary>
        /// Creates a new <see cref="QName"/> object from an <see cref="ASQName"/> object.
        /// </summary>
        /// <param name="qname">The <see cref="ASQName"/> object to convert to a <see cref="QName"/>.</param>
        /// <returns>The created <see cref="QName"/>.</returns>
        ///
        /// <remarks>
        /// The namespace kind of the created <see cref="QName"/> is set to
        /// <see cref="NamespaceKind.NAMESPACE" qualifyHint="true"/>; the namespace URI and local name
        /// are taken from <paramref name="qname"/>. If <paramref name="qname"/> is null, the
        /// default value of this type is returned.
        /// </remarks>
        public static QName fromASQName(ASQName qname) =>
            (qname == null) ? default : new QName(new Namespace(qname.uri), qname.localName);

        /// <summary>
        /// Parses a string to a <see cref="QName"/>.
        /// </summary>
        /// <param name="name">The string to parse. If this is null, the "any" name is
        /// returned.</param>
        /// <returns>The <see cref="QName"/> obtained by parsing the string.</returns>
        ///
        /// <remarks>
        /// <para>The syntax of the name parameter is as follows:</para>
        /// <list type="bullet">
        /// <item>
        /// If the string has one or more double colons ("::"), then the part before the last double
        /// colon is treated as the namespace (with the kind
        /// <see cref="NamespaceKind.NAMESPACE" qualifyHint="true"/>) and the part after the double
        /// colon is the local name. If the namespace is the string "*", it is taken to be the "any"
        /// namespace.
        /// </item>
        /// <item>
        /// Otherwise, if the string has one or more periods ('.') then the part before the last
        /// period (which is not the last character of the name and is not followed by '&lt;') is the
        /// package name (the namespace kind will be
        /// <see cref="NamespaceKind.NAMESPACE" qualifyHint="true"/>) and the part after it is the local
        /// name.
        /// </item>
        /// <item>If the string has no double colon or period, the entire string is treated as the
        /// local name, with the namespace being the public namespace.</item>
        /// </list>
        /// </remarks>
        public static QName parse(string name) {
            if (name == null)
                return default(QName);

            int nameLength = name.Length;

            if (nameLength == 0)
                return new QName(Namespace.@public, "");
            if (nameLength == 1 && name[0] == '*')
                return new QName(Namespace.any, "*");

            int doubleColonPos = name.LastIndexOf("::", StringComparison.Ordinal);

            Namespace ns;
            string localName;

            if (doubleColonPos != -1) {
                ns = (doubleColonPos == 1 && name[0] == '*')
                    ? Namespace.any
                    : new Namespace(NamespaceKind.NAMESPACE, name.Substring(0, doubleColonPos));

                localName = (doubleColonPos == nameLength - 2) ? "" : name.Substring(doubleColonPos + 2);
                return new QName(ns, localName);
            }

            int dotPos = -1;
            int curPos = name.IndexOf('.');
            while (curPos != -1) {
                if ((uint)(curPos + 1) < name.Length && name[curPos + 1] == '<')
                    break;

                dotPos = curPos;
                curPos = name.IndexOf('.', curPos + 1);
            }

            if (dotPos != -1) {
                ns = new Namespace(NamespaceKind.NAMESPACE, name.Substring(0, dotPos));
                localName = name.Substring(dotPos + 1);
            }
            else {
                ns = Namespace.@public;
                localName = name;
            }

            return new QName(ns, localName);
        }

    }
}
