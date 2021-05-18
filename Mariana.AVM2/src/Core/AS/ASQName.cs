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
        public static readonly ASQName any = new ASQName((ASNamespace)null, "*");

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
                (uri, prefix) = (null, null);
            }
            else {
                ASNamespace defaultNS = ASNamespace.getDefault();
                (uri, prefix) = (defaultNS.uri, defaultNS.prefix);
            }

            this.localName = ASString.AS_convertString(localName);
        }

        /// <summary>
        /// Creates a new <see cref="ASQName"/> object from a namespace and a local name.
        /// </summary>
        /// <param name="ns">The namespace of the XML name. If this is null, the QName will match
        /// XML elements and attributes in any namespace.</param>
        /// <param name="localName">The local name of the XML name. If this is the string "*",
        /// the QName will match XML elements and attributes with any local name.</param>
        public ASQName(ASNamespace ns, string localName) {
            if (ns != null)
                (uri, prefix) = (ns.uri, ns.prefix);

            this.localName = ASString.AS_convertString(localName);
        }

        /// <summary>
        /// Creates a new <see cref="ASQName"/> object with a non-prefixed namespace specified by a
        /// URI and a local name.
        /// </summary>
        ///
        /// <param name="uri">The URI of the namespace of the XML name. If this is null, the QName
        /// will match XML elements and attributes in any namespace.</param>
        /// <param name="localName">The local name of the XML name. If this is the string "*",
        /// the QName will match XML elements and attributes with any local name.</param>
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
        /// <param name="uri">The URI of the namespace of the XML name. If this is null, the QName will
        /// match XML elements and attributes in any namespace and <paramref name="prefix"/> is ignored.</param>
        /// <param name="localName">The local name of the XML name. This is the name of the XML
        /// element or attribute without the namespace. If this is the string "*", the QName
        /// will match XML elements and attributes with any local name.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1098</term>
        /// <description>If <paramref name="uri"/> is the empty string, but
        /// <paramref name="prefix"/> is not the empty string.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If <paramref name="prefix"/> is null, the QName will not have a prefix. This differs from
        /// the behaviour of the <see cref="ASNamespace(String,String)"/> constructor, which converts
        /// a null prefix to the string "null".
        /// </remarks>
        public ASQName(string prefix, string uri, string localName) {
            if (uri == null) {
                (this.uri, this.prefix) = (null, null);
            }
            else if (uri.Length == 0) {
                if (prefix != null && prefix.Length != 0)
                    throw ErrorHelper.createError(ErrorCode.XML_ILLEGAL_PREFIX_PUBLIC_NAMESPACE, prefix);
                (this.uri, this.prefix) = ("", "");
            }
            else {
                bool isValidPrefix = prefix == null || prefix.Length == 0 || XMLHelper.isValidName(prefix);
                (this.uri, this.prefix) = (uri, isValidPrefix ? prefix : null);
            }

            this.localName = ASString.AS_convertString(localName);
        }

        private ASQName(string prefix, string uri, string localName, bool _unsafeMarker) =>
            (this.prefix, this.uri, this.localName) = (prefix, uri, localName);

        /// <summary>
        /// Use for creating a new QName internally when all arguments are known to be valid.
        /// This does not check the validity of arguments, use with caution!
        /// </summary>
        /// <param name="prefix">The namespace prefix.</param>
        /// <param name="uri">The namespace URI.</param>
        /// <param name="localName">The local name.</param>
        /// <returns>The created <see cref="ASQName"/> instance.</returns>
        internal static ASQName unsafeCreate(string prefix, string uri, string localName) =>
            new ASQName(prefix, uri, localName, true);

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

        /// <inheritdoc/>
        public override int AS_nextIndex(int index) => (index >= 2) ? 0 : index + 1;

        /// <inheritdoc/>
        public override ASAny AS_nameAtIndex(int index) {
            if (index == 1)
                return nameof(uri);
            if (index == 2)
                return nameof(localName);
            return ASAny.undefined;
        }

        /// <inheritdoc/>
        public override ASAny AS_valueAtIndex(int index) {
            if (index == 1)
                return uri;
            if (index == 2)
                return localName;
            return ASAny.undefined;
        }

        /// <inheritdoc/>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public override bool propertyIsEnumerable(ASAny name = default) {
            string nameStr = ASAny.AS_convertString(name);
            return nameStr == nameof(uri) || nameStr == nameof(localName);
        }

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
        /// Returns a Namespace object representing the namespace of this QName.
        /// </summary>
        /// <returns>A <see cref="ASNamespace"/> instance. If the QName has the "any" namespace,
        /// returns null.</returns>
        ///
        /// <remarks>
        /// A call to this method will create a new <see cref="ASNamespace"/> object. The namespace
        /// of this QName can also be accessed directly, without allocating a new object, through the
        /// <see cref="prefix"/> and <see cref="uri"/> fields.
        /// </remarks>
        public ASNamespace getNamespace() => (uri == null) ? null : ASNamespace.unsafeCreate(prefix, uri);

        /// <summary>
        /// Parses a string into an <see cref="ASQName"/> object.
        /// </summary>
        /// <param name="s">The string to parse to an <see cref="ASQName"/>.</param>
        /// <returns>The <see cref="ASQName"/> created by parsing the string
        /// <paramref name="s"/>.</returns>
        ///
        /// <remarks>
        /// If the string contains an '::', the part before it is taken as the namespace URI (the
        /// prefix not being set), and the part after it is taken as the local name. If the string
        /// is "*", <see cref="ASQName.any"/> is returned. Otherwise, the entire string is taken as
        /// the local name, with the namespace being set to the default namespace.
        /// </remarks>
        public static ASQName parse(string s) {
            if (s == null || (s.Length == 1 && s[0] == '*'))
                return any;

            int separatorPos = s.LastIndexOf("::", StringComparison.Ordinal);
            if (separatorPos != -1) {
                string uri = s.Substring(0, separatorPos);
                string localName = s.Substring(separatorPos + 2);

                if (uri.Length == 1 && uri[0] == '*')
                    uri = null;

                return new ASQName(uri, localName);
            }

            return new ASQName(s);
        }

        /// <summary>
        /// Returns a <see cref="ASQName"/> that matches XML element and attribute names in any
        /// namespace with the given local name.
        /// </summary>
        /// <param name="localName">The local name of the XML name. If this is the string "*", the
        /// QName will match elements and attributes with any local name.</param>
        /// <returns>An <see cref="ASQName"/> instance.</returns>
        public static ASQName anyNamespace(string localName) => new ASQName((ASNamespace)null, localName);

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
            if (args.Length == 0)
                return new ASQName("", "");

            if (args.Length == 1)
                return XMLHelper.objectToQName(args[0], isAttr: false);

            string localName;
            ASNamespace ns;

            if (args[1].value is ASQName qname)
                localName = qname.localName;
            else
                localName = args[1].isDefined ? AS_convertString(args[1].value) : "";

            if (args[0].isUndefined)
                ns = ASNamespace.getDefault();
            else if (args[0].isNull)
                ns = null;
            else
                ns = XMLHelper.objectToNamespace(args[0]);

            return new ASQName(ns, localName);
        }

    }

}
