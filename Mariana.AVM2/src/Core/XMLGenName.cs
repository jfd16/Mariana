using System;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// An XMLGenName structure represents a generalized XML name. Generalized XML names
    /// can represent qualified XML names, multinames and numeric array indices (used to
    /// index an XMLList).
    /// </summary>
    internal readonly struct XMLGenName {

        /// <summary>
        /// The namespace prefix, or null if no namespace prefix is present.
        /// </summary>
        public readonly string prefix;

        /// <summary>
        /// The namespace URI. If this is null, the namespace must be ignored for matching.
        /// This must be ignored if <see cref="isMultiname"/> is true.
        /// </summary>
        public readonly string uri;

        /// <summary>
        /// The local name. If this is null, the local name must be ignored for matching.
        /// </summary>
        public readonly string localName;

        /// <summary>
        /// The numeric index, if <see cref="isIndex"/> is true.
        /// </summary>
        public readonly int index;

        /// <summary>
        /// If this is true, the generalized XML name represents a numeric index. The index is
        /// stored in the <see cref="index"/> field.
        /// </summary>
        public readonly bool isIndex;

        /// <summary>
        /// If this is true, the generalized XML name represents a multiname (a name
        /// consisting of a local name and a set of namespaces). The namespace set is
        /// stored in <see cref="nsSet"/>.
        /// </summary>
        public readonly bool isMultiname;

        /// <summary>
        /// If this is true, this generalized name represents an attribute name.
        /// </summary>
        public readonly bool isAttr;

        /// <summary>
        /// If this is true, this generalized name matches processing instructions.
        /// </summary>
        public readonly bool isProcInstr;

        /// <summary>
        /// The namespace set of a multiname. Only applicable if <see cref="isMultiname"/> is true.
        /// If <see cref="uri"/> is not null, that URI must be checked in addition to the namespaces
        /// in the set.
        /// </summary>
        public readonly NamespaceSet nsSet;

        private XMLGenName(
            string prefix = null, string uri = null, string localName = null, int index = -1,
            bool isIndex = false, bool isMultiname = false, bool isAttr = false, bool isProcInstr = false,
            NamespaceSet nsSet = default(NamespaceSet))
        {
            this.prefix = prefix;
            this.uri = uri;
            this.localName = localName;
            this.index = index;
            this.isIndex = isIndex;
            this.isMultiname = isMultiname;
            this.isAttr = isAttr;
            this.isProcInstr = isProcInstr;
            this.nsSet = nsSet;

            if (uri != null && uri.Length == 0)
                this.prefix = "";
        }

        /// <summary>
        /// Creates an <see cref="XMLGenName"/> instance from a <see cref="QName"/>.
        /// </summary>
        /// <returns>The created <see cref="XMLGenName"/> instance.</returns>
        /// <param name="qname">A <see cref="QName"/> instance.</param>
        /// <param name="bindOptions">The binding options used in the lookup for which a generalized
        /// name must be created. The following flags are used here:
        /// <see cref="BindOptions.ATTRIBUTE"/> and <see cref="BindOptions.RUNTIME_NAME"/>.</param>
        public static XMLGenName fromQName(QName qname, BindOptions bindOptions) {

            bool isAttr = (bindOptions & BindOptions.ATTRIBUTE) != 0;
            string uri = qname.ns.uri;
            string localName;

            if ((bindOptions & BindOptions.RUNTIME_NAME) != 0)
                localName = ASString.AS_convertString(qname.localName);
            else
                localName = qname.localName;

            if (localName == null || localName.Length == 0)
                return new XMLGenName(uri: uri, localName: localName, isAttr: isAttr);

            char firstChar = localName[0];

            if (qname.ns.isPublic && (uint)(firstChar - '0') <= 9) {
                NumberFormatHelper.parseArrayIndex(localName, false, out uint arrindex);
                if ((int)arrindex >= 0)
                    return new XMLGenName(index: (int)arrindex, isIndex: true, isAttr: isAttr);
            }

            if (firstChar == '@' && (bindOptions & BindOptions.ATTRIBUTE) == 0) {
                isAttr = true;
                localName = localName.Substring(1);
            }

            if (localName.Length == 1 && localName[0] == '*')
                localName = null;

            return new XMLGenName(uri: uri, localName: localName, isAttr: isAttr);

        }

        /// <summary>
        /// Creates an <see cref="XMLGenName"/> instance from a local name and a namespace set.
        /// </summary>
        /// <returns>The created <see cref="XMLGenName"/> instance.</returns>
        /// <param name="localName">The local name.</param>
        /// <param name="nsSet">The namespace set.</param>
        /// <param name="bindOptions">The binding options used in the lookup for which a generalized
        /// name must be created. The following flags are used here:
        /// <see cref="BindOptions.ATTRIBUTE"/> and <see cref="BindOptions.RUNTIME_NAME"/>.</param>
        public static XMLGenName fromMultiname(string localName, in NamespaceSet nsSet, BindOptions bindOptions) {
            bool isAttr = (bindOptions & BindOptions.ATTRIBUTE) != 0;

            if ((bindOptions & BindOptions.RUNTIME_NAME) != 0)
                localName = ASString.AS_convertString(localName);

            if (localName != null && localName.Length != 0) {
                char firstChar = localName[0];

                if (nsSet.containsPublic && (uint)(firstChar - '0') <= 9) {
                    NumberFormatHelper.parseArrayIndex(localName, false, out uint arrindex);
                    int index = (int)arrindex;
                    if (index >= 0)
                        return new XMLGenName(index: index, isIndex: true, isAttr: isAttr);
                }

                if (firstChar == '@' && (bindOptions & BindOptions.ATTRIBUTE) == 0) {
                    isAttr = true;
                    localName = localName.Substring(1);
                }

                if (localName.Length == 1 && localName[0] == '*')
                    localName = null;
            }

            bool isMultiname = true;
            string uri;

            if (nsSet.count == 1 && nsSet.contains(NamespaceKind.NAMESPACE) && !nsSet.containsPublic) {
                isMultiname = false;
                uri = nsSet.getNamespaces()[0].uri;
            }
            else if (localName == null) {
                // If the local name is the "any" name, Flash Player ignores the namespace set
                // and uses the "any" namespace instead. (This means that x.* and x.*::* are
                // equivalent expressions, even though they compile to different instructions)
                isMultiname = false;
                uri = null;
            }
            else {
                uri = ASNamespace.getDefault().uri;
            }

            return new XMLGenName(
                localName: localName, uri: uri, isMultiname: isMultiname, isAttr: isAttr, nsSet: nsSet);
        }

        /// <summary>
        /// Creates an <see cref="XMLGenName"/> instance from an ActionScript object.
        /// </summary>
        /// <returns>The created <see cref="XMLGenName"/> instance.</returns>
        /// <param name="obj">An <see cref="ASAny"/> instance.</param>
        /// <param name="bindOptions">The binding options used in the lookup for which a generalized
        /// name must be created. Only the <see cref="BindOptions.ATTRIBUTE"/> flag is used.</param>
        public static XMLGenName fromObject(ASAny obj, BindOptions bindOptions) {
            bool isAttr = (bindOptions & BindOptions.ATTRIBUTE) != 0;

            if (ASObject.AS_isNumeric(obj.value)) {
                double number = (double)obj.value;
                int index = (int)number;
                if (number == (double)index && index >= 0 && !isAttr)
                    return new XMLGenName(index: index, isIndex: true, isAttr: isAttr);
            }

            string uri, prefix, localName;

            ASQName qname = obj.value as ASQName;
            if (qname != null)
                (uri, prefix, localName) = (qname.uri, qname.prefix, qname.localName);
            else
                (uri, prefix, localName) = (ASNamespace.getDefault().uri, null, ASAny.AS_convertString(obj));

            if (localName.Length == 0)
                return new XMLGenName(prefix: prefix, uri: uri, localName: localName, isAttr: isAttr);

            char firstChar = localName[0];

            if ((uint)(firstChar - '0') <= 9 && (qname == null || (uri != null && uri.Length == 0))) {
                NumberFormatHelper.parseArrayIndex(localName, false, out uint arrindex);
                if ((int)arrindex >= 0)
                    return new XMLGenName(index: (int)arrindex, isIndex: true, isAttr: isAttr);
            }

            if (firstChar == '@') {
                isAttr = true;
                localName = localName.Substring(1);
                if (qname == null)
                    uri = "";
            }

            if (localName.Length == 1 && localName[0] == '*') {
                localName = null;
                if (qname == null)
                    uri = null;
            }

            return new XMLGenName(prefix: prefix, uri: uri, localName: localName, isAttr: isAttr);
        }

        /// <summary>
        /// Creates an <see cref="XMLGenName"/> instance from an ActionScript object and a
        /// namespace set.
        /// </summary>
        /// <returns>The created <see cref="XMLGenName"/> instance.</returns>
        /// <param name="obj">An <see cref="ASAny"/> instance.</param>
        /// <param name="nsSet">The namespace set.</param>
        /// <param name="bindOptions">The binding options used in the lookup for which a generalized
        /// name must be created. Only the <see cref="BindOptions.ATTRIBUTE"/> flag is used.</param>
        public static XMLGenName fromObjectMultiname(ASAny obj, in NamespaceSet nsSet, BindOptions bindOptions) {
            bool isAttr = (bindOptions & BindOptions.ATTRIBUTE) != 0;

            if (ASObject.AS_isNumeric(obj.value) && nsSet.containsPublic) {
                double number = (double)obj.value;
                int index = (int)number;
                if (number == (double)index && index >= 0)
                    return new XMLGenName(index: index, isIndex: true, isAttr: isAttr);
            }

            string localName, uri = null, prefix = null;
            bool isMultiname;

            ASQName qname = obj.value as ASQName;
            if (qname != null) {
                (uri, prefix, localName) = (qname.uri, qname.prefix, qname.localName);
                isMultiname = false;
            }
            else {
                localName = ASAny.AS_convertString(obj);
                isMultiname = true;
            }

            if (localName != null && localName.Length != 0) {
                char firstChar = localName[0];

                if (nsSet.containsPublic && (uint)(firstChar - '0') <= 9) {
                    NumberFormatHelper.parseArrayIndex(localName, false, out uint arrindex);
                    if ((int)arrindex >= 0)
                        return new XMLGenName(index: (int)arrindex, isIndex: true, isAttr: isAttr);
                }

                if (firstChar == '@' && (bindOptions & BindOptions.ATTRIBUTE) == 0) {
                    isAttr = true;
                    localName = localName.Substring(1);
                }

                if (localName.Length == 1 && localName[0] == '*')
                    localName = null;
            }

            if (isMultiname) {
                if (nsSet.count == 1 && nsSet.contains(NamespaceKind.NAMESPACE) && !nsSet.containsPublic) {
                    isMultiname = false;
                    uri = nsSet.getNamespaces()[0].uri;
                }
                else if (qname == null && localName == null) {
                    isMultiname = false;
                }
                else {
                    uri = ASNamespace.getDefault().uri;
                }
            }

            return new XMLGenName(
                prefix: prefix, uri: uri, localName: localName,
                isMultiname: isMultiname, isAttr: isAttr, nsSet: nsSet
            );
        }

        /// <summary>
        /// Creates an <see cref="XMLGenName"/> instance from an ActionScript object representing
        /// an attribute name. The name must not have the "@" prefix.
        /// </summary>
        /// <returns>The created <see cref="XMLGenName"/> instance.</returns>
        /// <param name="obj">An <see cref="ASAny"/> instance.</param>
        public static XMLGenName fromObjectAttrName(ASAny obj) {
            string prefix = null, uri, localName;

            if (obj.value is ASQName qname) {
                (uri, prefix, localName) = (qname.uri, qname.prefix, qname.localName);
                if (localName.Length == 1 && localName[0] == '*')
                    localName = null;
            }
            else {
                string nameStr = ASAny.AS_convertString(obj);
                (uri, localName) = (nameStr.Length == 1 && nameStr[0] == '*') ? (null, null) : ("", nameStr);
            }

            return new XMLGenName(prefix: prefix, uri: uri, localName: localName, isAttr: true);
        }

        /// <summary>
        /// Creates an <see cref="XMLGenName"/> instance from an ActionScript object representing
        /// a processing instruction name.
        /// </summary>
        /// <returns>The created <see cref="XMLGenName"/> instance.</returns>
        /// <param name="obj">An <see cref="ASAny"/> instance.</param>
        public static XMLGenName fromObjectProcInstrName(ASAny obj) {
            string prefix = null, uri, localName;

            if (obj.value is ASQName qname) {
                (uri, prefix, localName) = (qname.uri, qname.prefix, qname.localName);
                if (localName.Length == 1 && localName[0] == '*')
                    localName = null;
            }
            else {
                string nameStr = ASAny.AS_convertString(obj);
                (uri, localName) = (nameStr.Length == 1 && nameStr[0] == '*') ? (null, null) : ("", nameStr);
            }

            return new XMLGenName(prefix: prefix, uri: uri, localName: localName, isProcInstr: true);
        }

        /// <summary>
        /// Returns an <see cref="XMLGenName"/> instance that matches any child node.
        /// </summary>
        /// <returns>An <see cref="XMLGenName"/> instance that matches any child node.</returns>
        public static XMLGenName anyChild() => new XMLGenName();

        /// <summary>
        /// Returns an <see cref="XMLGenName"/> instance that matches any attribute.
        /// </summary>
        /// <returns>An <see cref="XMLGenName"/> instance that matches any attribute.</returns>
        public static XMLGenName anyAttribute() => new XMLGenName(isAttr: true);

        /// <summary>
        /// Returns an <see cref="XMLGenName"/> representing a qualified name.
        /// </summary>
        /// <returns>An <see cref="XMLGenName"/> representing a qualified name.</returns>
        /// <param name="uri">The namespace URI of the qualified name.</param>
        /// <param name="localName">The local name of the qualified name.</param>
        /// <param name="isAttr">Set to true for an attribute name, otherwise false.</param>
        public static XMLGenName qualifiedName(string uri, string localName, bool isAttr) =>
            new XMLGenName(uri: uri, localName: localName, isAttr: isAttr);

    }

}
