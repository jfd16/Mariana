using System;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The Namespace class is used to represent XML namespaces.
    /// </summary>
    [AVM2ExportClass(name = "Namespace", hasPrototypeMethods = true)]
    [AVM2ExportClassInternal(tag = ClassTag.NAMESPACE)]
    public sealed class ASNamespace : ASObject {

        /// <summary>
        /// The value of the "length" property of the AS3 Namespace class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 2;

        /// <summary>
        /// The default namespace for the current thread. This is the default namespace used for
        /// elements and attributes on all new XML objects created with unqualified names, and is a
        /// namespace that is implicitly included in namespace sets for all methods of XML and XMLList
        /// that take a namespace set parameter.
        /// </summary>
        [ThreadStatic]
        private static ASNamespace s_defaultNS;

        /// <summary>
        /// The <see cref="ASNamespace"/> instance representing the public namespace.
        /// </summary>
        ///
        /// <remarks>
        /// This is the default namespace used for elements and attributes on all new XML objects with
        /// unqualified names, if the default namespace is not set (using <see cref="setDefault"/>)
        /// to some other value. This is a special namespace whose prefix is always the empty string,
        /// and attempts to define a new prefix for it will result in a TypeError being thrown.
        /// </remarks>
        public static readonly ASNamespace @public = new ASNamespace("", "");

        /// <summary>
        /// The <see cref="ASNamespace"/> instance representing the "any" namespace.
        /// </summary>
        /// <remarks>
        /// If this namespace is used in a QName for matching XML elements or attributes, it specifies
        /// that the namespace URI of the element or attribute must be ignored for the matching.
        /// </remarks>
        public static readonly ASNamespace any = new ASNamespace(null);

        /// <summary>
        /// The URI of the XML namespace.
        /// </summary>
        /// <remarks>
        /// The "any" namespace (which can match XML elements and attributes in any namespace) has a
        /// null URI.
        /// </remarks>
        [AVM2ExportTrait]
        public readonly string uri;

        /// <summary>
        /// The prefix associated with the XML namespace.
        /// </summary>
        ///
        /// <remarks>
        /// If this is null, the namespace is prefixless and it cannot be used with certain methods
        /// that require prefixed namespaces. The public namespace with the empty string as its URI is
        /// a special namespace which cannot have any prefix other than the empty string assigned to
        /// it.
        /// </remarks>
        public readonly string prefix;

        /// <summary>
        /// Creates a new <see cref="ASNamespace"/> with only a URI
        /// </summary>
        /// <param name="uri">The URI of the namespace. If this is null, this constructor returns the
        /// "any" namespace/</param>
        /// <remarks>
        /// The prefix of the namespace will not be set (i.e. is set to null), except when the URI is
        /// the empty string, in which case the prefix will be set to the empty string.
        /// </remarks>
        public ASNamespace(string uri) {
            this.uri = uri;
            this.prefix = (uri != null && uri.Length == 0) ? "" : null;
        }

        /// <summary>
        /// Creates a new <see cref="ASNamespace"/> with a URI and a prefix.
        /// </summary>
        ///
        /// <param name="prefix">The prefix of the namespace. If this is not a valid XML name (or
        /// null), the namespace is considered to be prefixless and cannot be used with methods
        /// requiring prefixed namespaces.</param>
        /// <param name="uri">
        /// The URI of the namespace. If this is the empty string, the prefix must be either null or
        /// the empty string (otherwise an error is thrown), and in this case the prefix of the
        /// namespace is always set to the empty string, even if the prefix parameter is null. If this
        /// is null (which represents the "any" namespace), the prefix will always be set to null.
        /// </param>
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
        public ASNamespace(string prefix, string uri) : this(prefix, uri, false) { }

        /// <summary>
        /// Creates a new <see cref="ASNamespace"/> with a URI and a prefix, optionally disabling
        /// validation.
        /// </summary>
        ///
        /// <param name="prefix">The prefix of the namespace. If this is not a valid XML identifier
        /// (or null), the namespace is considered to be prefixless and cannot be used with methods
        /// requiring prefixed namespaces.</param>
        /// <param name="uri">
        /// The URI of the namespace. If this is the empty string, the prefix must be either null or
        /// the empty string (otherwise an error is thrown), and in this case the prefix of the
        /// namespace is always set to the empty string, even if the prefix parameter is null. If this
        /// is null or the string "*" (which stands for the any namespace), the prefix will always be
        /// set to null.
        /// </param>
        /// <param name="disableChecks">If this is set to true, all parameter validation is disabled
        /// and the prefix and URI are set directly, with no exceptions being thrown for invalid
        /// parameters. Set this to true only for prefixes and URIs that are guaranteed to be valid,
        /// such as those obtained from the XML parser which validates them.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1098</term>
        /// <description>If <paramref name="uri"/> is the empty string, but
        /// <paramref name="prefix"/> is a non-null, non-empty string (and
        /// <paramref name="disableChecks"/> is false).</description>
        /// </item>
        /// </list>
        /// </exception>
        internal ASNamespace(string prefix, string uri, bool disableChecks) {
            if (disableChecks) {
                this.prefix = prefix;
                this.uri = uri;
                return;
            }

            if (uri == null) {
                this.uri = null;
                this.prefix = null;
                return;
            }

            if (uri.Length == 0) {
                if (prefix != null && prefix.Length != 0)
                    throw ErrorHelper.createError(ErrorCode.XML_ILLEGAL_PREFIX_PUBLIC_NAMESPACE, prefix);
                this.uri = "";
                this.prefix = "";
                return;
            }

            this.prefix = null;
            if (prefix != null && (prefix.Length == 0 || XMLHelper.isValidName(prefix)))
                this.prefix = prefix;

            this.uri = uri;
        }

        /// <summary>
        /// Returns the prefix of this namespace, or undefined if the namespace has no prefix.
        /// </summary>
        [AVM2ExportTrait(name = "prefix")]
        public ASAny AS_prefix => prefix ?? ASAny.undefined;

        /// <summary>
        /// Returns a string representation of the object. For the Namespace class, this method
        /// returns the namespace URI (or "*" for the any namespace).
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
        public new string AS_toString() => uri;

        /// <summary>
        /// Returns the primitive type representation of the object.
        /// </summary>
        /// <returns>A primitive representation of the object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public new string valueOf() => uri;

        /// <summary>
        /// Returns a value indicating whether two <see cref="ASNamespace"/> instances are equal
        /// according to the definition of the strict equality (===) operator in AS3.
        /// </summary>
        /// <param name="ns1">The first namespace.</param>
        /// <param name="ns2">The second namespace.</param>
        /// <returns>True if <paramref name="ns1"/> is equal to <paramref name="ns2"/>, false
        /// otherwise.</returns>
        /// <remarks>
        /// Two Namespace instances are equal if their URIs are equal. Prefixes are ignored.
        /// </remarks>
        public static bool AS_equals(ASNamespace ns1, ASNamespace ns2) => ns1.uri == ns2.uri;

        /// <summary>
        /// Gets the default XML namespace for the current thread.
        /// </summary>
        /// <returns>The default XML namespace.</returns>
        ///
        /// <remarks>
        /// <para>The default XML namespace is used for elements and attributes on all new XML objects
        /// created with unqualified names, and is a namespace that is implicitly included in
        /// namespace sets for all methods of XML and XMLList that take a namespace set
        /// parameter.</para>
        /// <para>The prefix of the default namespace is always the empty string.</para>
        /// </remarks>
        public static ASNamespace getDefault() {
            var defaultNS = s_defaultNS;
            if (defaultNS == null)
                defaultNS = s_defaultNS = @public;
            return defaultNS;
        }

        /// <summary>
        /// Sets the default XML namespace for the current thread.
        /// </summary>
        /// <param name="ns">The default XML namespace.</param>
        ///
        /// <remarks>
        /// <para>The default XML namespace is used for elements and attributes on all new XML objects
        /// created with unqualified names, and is a namespace that is implicitly included in
        /// namespace sets for all methods of XML and XMLList that take a namespace set
        /// parameter.</para>
        /// <para>This value cannot be the "any" namespace (the namespace whose URI is null). If the
        /// any namespace is assigned to this property, it will be set to the public namespace
        /// instead. The public namespace is the default value of this property for all new
        /// threads.</para>
        /// </remarks>
        public static void setDefault(ASNamespace ns) {
            if (ns == null || ns.uri == null)
                s_defaultNS = @public;
            else if (ns.prefix != null && ns.prefix.Length == 0)
                s_defaultNS = ns;
            else
                s_defaultNS = new ASNamespace("", ns.uri);
        }

        /// <exclude/>
        ///
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and code generated by the
        /// ABCIL compiler to invoke the AS3 Namespace constructor. This should not be called by
        /// outside code. To create a namespace from .NET code, use one of the the XMLNamespace
        /// constructor overloads.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) => __AS_CONSTRUCT(args);

        /// <exclude/>
        ///
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and code generated by the
        /// ABCIL compiler to invoke the AS3 Namespace constructor. This should not be called by
        /// outside code. To create a namespace from .NET code, use one of the the
        /// <see cref="ASNamespace"/> constructor overloads.
        /// </summary>
        internal static new ASAny __AS_CONSTRUCT(ReadOnlySpan<ASAny> args) {
            if (args.Length == 0) {
                // No arguments: Return the public namespace.
                return @public;
            }
            else if (args.Length == 1) {
                if (args[0].value is ASNamespace ns)
                    return ns;

                if (args[0].value is ASQName qname)
                    return qname.getNamespace();

                string nsURI = ASAny.AS_convertString(args[0]);

                if (nsURI != null && nsURI.Length == 0)
                    return @public;
                if (nsURI.Length == 1 && nsURI[0] == '*')
                    return any;

                return new ASNamespace(nsURI);
            }
            else {
                string prefix = ASAny.AS_coerceString(args[0]);
                string uri;

                if (args[1].value is ASQName qname)
                    uri = qname.uri;
                else if (args[1].value is ASNamespace ns)
                    uri = ns.uri;
                else
                    uri = ASAny.AS_convertString(args[1]);

                return new ASNamespace(prefix, uri);
            }
        }

    }

}

