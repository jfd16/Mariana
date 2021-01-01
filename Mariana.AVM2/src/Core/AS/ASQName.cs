using System;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The QName class is used for qualified names for use in XML.
    /// </summary>
    [AVM2ExportClass(name = "QName", hasPrototypeMethods = true)]
    [AVM2ExportClassInternal(tag = ClassTag.QNAME)]
    sealed public class ASQName : ASObject {

        /// <summary>
        /// The value of the "length" property of the AS3 QName class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 2;

        /// <summary>
        /// The "any" name.
        /// </summary>
        /// <remarks>
        /// If this name for matching XML elements or attributes, it specifies that all elements or
        /// attributes must match irrespective of their name.
        /// </remarks>
        public static readonly ASQName any = new ASQName(ASNamespace.any, "*");

        /// <summary>
        /// The URI of the namespace of the XML name.
        /// </summary>
        [AVM2ExportTrait]
        public readonly string uri;

        /// <summary>
        /// The namespace prefix of the XML name.
        /// </summary>
        public readonly string prefix;

        /// <summary>
        /// The local name of the XML name. This is the name of the XML element or attribute without
        /// the namespace.
        /// </summary>
        [AVM2ExportTrait]
        public readonly string localName;

        /// <summary>
        /// Creates a new <see cref="ASQName"/> object from a local name. The default namespace will
        /// be used.
        /// </summary>
        /// <param name="localName">The local name of the XML name. This is the name of the XML
        /// element or attribute without the namespace. If this is the string "*", the QName will
        /// match XML elements and attributes with any name in any namespace. Otherwise, its namespace
        /// will be set to the default XML namespace.</param>
        public ASQName(string localName) {
            if (localName != null && localName.Length == 1 && localName[0] == '*') {
                this.uri = null;
                this.prefix = null;
            }
            else {
                ASNamespace defaultNS = ASNamespace.getDefault();
                this.uri = defaultNS.uri;
                this.prefix = defaultNS.prefix;
            }
            this.localName = ASString.AS_convertString(localName);
        }

        /// <summary>
        /// Creates a new <see cref="ASQName"/> object from a namespace and a local name.
        /// </summary>
        /// <param name="ns">The namespace of the XML name.</param>
        /// <param name="localName">The local name of the XML name. This is the name of the XML
        /// element or attribute without the namespace. If this is the string "*", the QName will
        /// match XML elements and attributes with any local name.</param>
        public ASQName(ASNamespace ns, string localName) {
            if (ns != null) {
                this.uri = ns.uri;
                this.prefix = ns.prefix;
            }
            this.localName = ASString.AS_convertString(localName);
        }

        /// <summary>
        /// Creates a new <see cref="ASQName"/> object with a non-prefixed namespace specified by a
        /// URI and a local name.
        /// </summary>
        ///
        /// <param name="uri">The URI of the namespace of the XML name. If this is null, the QName
        /// will match XML elements and attributes in any namespace.</param>
        /// <param name="localName">The local name of the XML name. This is the name of the XML
        /// element or attribute without the namespace. If this is the string "*", the QName will
        /// match XML elements and attributes with any local name.</param>
        public ASQName(string uri, string localName) {
            this.prefix = (uri != null && uri.Length == 0) ? "" : null;
            this.uri = uri;
            this.localName = ASString.AS_convertString(localName);
        }

        /// <summary>
        /// Creates a new <see cref="ASQName"/> object with a prefixed namespace specified by a URI
        /// and prefix, and a local name.
        /// </summary>
        ///
        /// <param name="prefix">The prefix of the namespace of the XML name.</param>
        /// <param name="uri">The URI of the namespace of the XML name. If this is null the QName will
        /// match XML elements and attributes in any namespace.</param>
        /// <param name="localName">The local name of the XML name. This is the name of the XML
        /// element or attribute without the namespace. If this is null or the string "*", the QName
        /// will match XML elements and attributes with any local name.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1098</term>
        /// <description>If <paramref name="uri"/> is the empty string, but
        /// <paramref name="prefix"/> is a non-null, non-empty string.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASQName(string prefix, string uri, string localName)
            : this(prefix, uri, localName, false) {}

        internal ASQName(string prefix, string uri, string localName, bool disableChecks) {
            if (disableChecks) {
                this.prefix = prefix;
                this.uri = uri;
                this.localName = localName;
                return;
            }

            if (uri == null) {
                this.uri = null;
                this.prefix = null;
            }
            else if (uri.Length == 0) {
                if (prefix != null && prefix.Length != 0)
                    throw ErrorHelper.createError(ErrorCode.XML_ILLEGAL_PREFIX_PUBLIC_NAMESPACE, prefix);
                this.uri = "";
                this.prefix = "";
            }
            else {
                this.prefix = null;
                if (prefix != null && (prefix.Length == 0 || XMLHelper.isValidName(prefix)))
                    this.prefix = prefix;
                this.uri = uri;
            }

            this.localName = ASString.AS_convertString(localName);
        }

        /// <summary>
        /// Returns a string representation of the object.
        /// </summary>
        /// <returns>The string representation of the object.</returns>
        ///
        /// <remarks>
        /// This method is exported to the AVM2 with the name <c>toString</c>, but must be called
        /// from .NET code with the name <c>AS_toString</c> to avoid confusion with the
        /// <see cref="Object.ToString" qualifyHint="true"/> method.
        /// </remarks>
        [AVM2ExportTrait(name = "toString", nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod(name = "toString")]
        public new string AS_toString() {
            if (uri == null)
                return "*::" + localName;
            if (uri.Length == 0)
                return localName;
            return uri + "::" + localName;
        }

        /// <summary>
        /// Returns the primitive type representation of the object.
        /// </summary>
        /// <returns>A primitive representation of the object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public new ASQName valueOf() => this;

        /// <summary>
        /// Returns a value indicating whether two <see cref="ASQName"/> instances are equal
        /// according to the definition of the strict equality (===) operator in AS3.
        /// </summary>
        /// <param name="qname1">The first QName.</param>
        /// <param name="qname2">The second QName.</param>
        /// <returns>True if <paramref name="qname1"/> is equal to <paramref name="qname2"/>,
        /// false otherwise.</returns>
        /// <remarks>
        /// Two QName instances are equal if their namespace URIs and local names are equal. Prefixes
        /// are ignored.
        /// </remarks>
        public static bool AS_equals(ASQName qname1, ASQName qname2) {
            if (qname1 == qname2)
                return true;
            if (qname1 == null || qname2 == null)
                return false;
            return qname1.localName == qname2.localName && qname1.uri == qname2.uri;
        }

        /// <summary>
        /// Gets a value indicating whether the current QName represents a fully qualified name.
        /// </summary>
        /// <returns>A fully qualified name is one that has a non-null local name and a namespace that
        /// is not the "any" namespace.</returns>
        public bool isFullyQualified => uri != null && localName != null;

        /// <summary>
        /// Gets a value indicating whether the current QName matches any local name.
        /// </summary>
        public bool hasAnyLocalName => localName.Length == 1 && localName[0] == '*';

        /// <summary>
        /// Returns a Namespace object representing the namespace of this QName.
        /// </summary>
        /// <returns>A <see cref="ASNamespace"/> instance.</returns>
        ///
        /// <remarks>
        /// A call to this method will create a new <see cref="ASNamespace"/> object. The namespace
        /// of this QName can also be accessed directly, without allocating a new object, through the
        /// <see cref="prefix"/> and <see cref="uri"/> fields.
        /// </remarks>
        public ASNamespace getNamespace() => new ASNamespace(prefix, uri, disableChecks: true);

        /// <summary>
        /// Returns a hash code for the <see cref="ASQName"/> object.
        /// </summary>
        /// <remarks>
        /// This is called from the <see cref="ASObject.GetHashCode" qualifyHint="true"/> method
        /// when the object passed is a QName.
        /// </remarks>
        internal int internalGetHashCode() {
            int hash = 0;

            hash += (uri == null) ? 0 : uri.GetHashCode();
            hash *= 1194163;
            hash += (localName == null) ? 0 : localName.GetHashCode();
            hash *= 5598379;

            return hash;
        }

        /// <summary>
        /// Parses a string into an <see cref="ASQName"/> object.
        /// </summary>
        /// <param name="s">The string to parse to an <see cref="ASQName"/>.</param>
        /// <returns>The <see cref="ASQName"/> created by parsing the string
        /// <paramref name="s"/>.</returns>
        ///
        /// <remarks>
        /// If the string contains an '::', the part before it is taken as the namespace URI (the
        /// prefix not being set), and the part after it is taken as the local name. Otherwise, the
        /// entire string is taken as the local name, with the namespace being set to the default
        /// namespace.
        /// </remarks>
        public static ASQName parse(string s) {
            if (s == null)
                return new ASQName(null);

            if (s.Length > 1) {
                for (int i = s.Length - 1; i > 0; i--) {
                    if (s[i] != ':' || s[i - 1] != ':')
                        continue;

                    string uri = s.Substring(0, i - 1), local = s.Substring(i + 1);
                    if (i == 2 && uri[0] == '*')
                        uri = null;
                    return new ASQName(uri, local);
                }
            }

            if (s.Length == 1 && s[0] == '*')
                return any;

            return new ASQName(s);
        }

        /// <exclude/>
        ///
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and code generated by the
        /// ABCIL compiler to invoke the AS3 Namespace constructor. This should not be called by
        /// outside code. To create a namespace from .NET code, use one of the the
        /// <see cref="ASQName"/> constructor overloads.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) => __AS_CONSTRUCT(args);

        /// <exclude/>
        ///
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and code generated by the
        /// ABCIL compiler to invoke the AS3 Namespace constructor. This should not be called by
        /// outside code. To create a namespace from .NET code, use one of the the XMLNamespace
        /// constructor overloads.
        /// </summary>
        internal static new ASAny __AS_CONSTRUCT(ReadOnlySpan<ASAny> args) {
            if (args.Length == 0) {
                return new ASQName("", "");
            }
            else if (args.Length == 1) {
                if (args[0].value is ASQName qname)
                    return qname;

                if (args[0].value is ASNamespace ns)
                    return new ASQName(ns, "");

                string localName = args[0].isDefined ? AS_convertString(args[0].value) : "";
                return new ASQName(localName);
            }
            else {
                string localName;

                if (args[1].value is ASQName qname)
                    localName = qname.localName;
                else
                    localName = args[1].isDefined ? AS_convertString(args[1].value) : "";

                if (!args[0].isDefined)
                    return new ASQName(localName);

                if (args[0].value == null)
                    return new ASQName(ASNamespace.any, localName);

                if (args[0].value is ASNamespace ns)
                    return new ASQName(ns, localName);

                return new ASQName(ASAny.AS_convertString(args[0]), localName);
            }
        }

    }

}
