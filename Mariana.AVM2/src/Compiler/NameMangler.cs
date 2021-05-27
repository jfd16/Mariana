using System;
using System.Globalization;
using System.Text;
using Mariana.AVM2.Core;
using Mariana.CodeGen;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Generates mangled names for dynamic types and members emitted by the compiler.
    /// </summary>
    internal class NameMangler {

        private const char ESCAPE_CHAR = '!';

        private static char[] s_localNameSpecialChars = {'.', ':', '{', '\0', ESCAPE_CHAR};
        private static char[] s_namesapceSpecialChars = {'{', '\0', ESCAPE_CHAR};

        private StringBuilder m_stringBuilder;

        /// <summary>
        /// A namespace that can be used for the emitted type names for compiler-generated types.
        /// This is guaranteed not to conflict with mangled namespace names generated by
        /// <see cref="createTypeName"/>.
        /// </summary>
        public const string INTERNAL_NAMESPACE = "{internal}";

        private const string MODULE_RESERVED_TYPE_NAME = "<Module>";

        /// <summary>
        /// Creates a mangled name from the given <see cref="QName"/>.
        /// </summary>
        /// <returns>The mangled name string.</returns>
        /// <param name="name">The qualified name from which to create a mangled name.</param>
        public string createName(in QName name) {
            string nsStr = _mangleNamespace(name.ns);
            string localStr = _mangleLocalName(name.localName);
            return (nsStr.Length == 0) ? localStr : nsStr + localStr;
        }

        /// <summary>
        /// Creates a mangled type name from the given <see cref="QName"/>.
        /// </summary>
        /// <returns>The mangled <see cref="TypeName"/>.</returns>
        /// <param name="name">The qualified name from which to create a mangled type name.</param>
        public TypeName createTypeName(in QName name) {
            string nsStr = _mangleNamespace(name.ns, true);

            string localStr;
            if (nsStr.Length == 0 && name.localName == MODULE_RESERVED_TYPE_NAME) {
                // Escape this because the type name "<Module>" is reserved.
                localStr = ESCAPE_CHAR + "<Module" + ESCAPE_CHAR + ">";
            }
            else {
                localStr = _mangleLocalName(name.localName);
            }

            return (nsStr.Length == 0) ? new TypeName(localStr) : new TypeName(nsStr, localStr);
        }

        /// <summary>
        /// Creates a mangled name for a property getter from the given <see cref="QName"/>.
        /// </summary>
        /// <returns>The mangled name string.</returns>
        /// <param name="propName">The qualified name of the property from which to create a
        /// mangled name for the getter.</param>
        public string createGetterName(QName propName) {
            string nsStr = _mangleNamespace(propName.ns);
            string localStr = "get{" + _mangleLocalName(propName.localName) + "}";
            return (nsStr.Length == 0) ? localStr : nsStr + localStr;
        }

        /// <summary>
        /// Creates a mangled name for a property setter from the given <see cref="QName"/>.
        /// </summary>
        /// <returns>The mangled name string.</returns>
        /// <param name="propName">The qualified name of the property from which to create a
        /// mangled name for the setter.</param>
        public string createSetterName(QName propName) {
            string nsstr = _mangleNamespace(propName.ns);
            string localStr = "set{" + _mangleLocalName(propName.localName) + "}";
            return (nsstr.Length == 0) ? localStr : nsstr + localStr;
        }

        /// <summary>
        /// Creates a mangled qualified name for a property getter from the given <see cref="QName"/>.
        /// </summary>
        /// <returns>The mangled qualified name for the getter.</returns>
        /// <param name="propName">The qualified name of the property from which to create a
        /// mangled name for the getter.</param>
        public QName createGetterQualifiedName(QName propName) {
            string localStr = "get{" + _mangleLocalName(propName.localName) + "}";
            return new QName(propName.ns, localStr);
        }

        /// <summary>
        /// Creates a mangled name for a property setter from the given <see cref="QName"/>.
        /// </summary>
        /// <returns>The mangled qualified name for the setter.</returns>
        /// <param name="propName">The qualified name of the property from which to create a
        /// mangled name for the setter.</param>
        public QName createSetterQualifiedName(QName propName) {
            string localStr = "set{" + _mangleLocalName(propName.localName) + "}";
            return new QName(propName.ns, localStr);
        }

        /// <summary>
        /// Creates a mangled name for a catch scope class.
        /// </summary>
        /// <returns>The mangled name for the catch scope class.</returns>
        /// <param name="uid">A unique identifier for the catch scope.</param>
        public string createCatchScopeClassName(int uid) => "CatchScope" + uid.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Creates a mangled name for an activation class of a method.
        /// </summary>
        /// <returns>The mangled name for the activation class.</returns>
        /// <param name="uid">A unique identifier for the activation object.</param>
        public string createActivationClassName(int uid) => "Activation" + uid.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Creates a mangled name for a script container class.
        /// </summary>
        /// <returns>The mangled name for the script container class.</returns>
        /// <param name="uid">A unique identifier for the script.</param>
        public string createScriptContainerName(int uid) => "Script" + uid.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Creates a mangled name for an anonymous function.
        /// </summary>
        /// <returns>The mangled name for the anonymous function.</returns>
        /// <param name="uid">A unique identifier for the anonymous function.</param>
        public string createAnonFunctionName(int uid) {
            return "func" + uid.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Creates a name for an interface method stub.
        /// </summary>
        /// <returns>The name for the interface method stub.</returns>
        /// <param name="ifaceMangledName">The mangled name of the interface.</param>
        /// <param name="methodMangledName">The mangled name of the method being implemented.</param>
        public string createMethodImplStubName(string ifaceMangledName, string methodMangledName) =>
            "IS:" + ifaceMangledName + ":{" + methodMangledName + "}";

        private string _mangleLocalName(string str) {
            if (str.Length == 0)
                return "{}";

            int curIndex = 0;
            bool usesStringBuilder = false;
            StringBuilder sb = m_stringBuilder;

            while (curIndex < str.Length) {
                int index = str.IndexOfAny(s_localNameSpecialChars, curIndex);
                if (index == -1)
                    break;

                if (!usesStringBuilder) {
                    if (sb == null)
                        sb = m_stringBuilder = new StringBuilder();
                    sb.Clear();
                    usesStringBuilder = true;
                }

                sb.Append(str, curIndex, index - curIndex);
                sb.Append(ESCAPE_CHAR);
                sb.Append((str[index] == '\0') ? '0' : str[index]);
                curIndex = index + 1;
            }

            if (usesStringBuilder) {
                sb.Append(str, curIndex, str.Length - curIndex);
                return sb.ToString();
            }

            return str;
        }

        private string _mangleNamespace(Namespace ns, bool noNameSeparator = false) {
            string str = (ns.kind == NamespaceKind.PRIVATE)
                ? ns.privateNamespaceId.ToString(CultureInfo.InvariantCulture)
                : ns.uri;

            if (str == null)
                str = "";

            if (str.Length == 0 && ns.kind == NamespaceKind.NAMESPACE)
                return "";

            int curIndex = 0;
            bool usesStringBuilder = false;
            StringBuilder sb = m_stringBuilder;

            while (curIndex < str.Length) {
                int index = str.IndexOfAny(s_namesapceSpecialChars, curIndex);
                if (index == -1)
                    break;

                if (!usesStringBuilder) {
                    if (sb == null)
                        sb = m_stringBuilder = new StringBuilder();
                    sb.Clear();
                    usesStringBuilder = true;
                }

                sb.Append(str, curIndex, index - curIndex);
                sb.Append(ESCAPE_CHAR);
                sb.Append((str[index] == '\0') ? '0' : str[index]);
                curIndex = index + 1;
            }

            if (usesStringBuilder) {
                sb.Append(str, curIndex, str.Length - curIndex);
                str = sb.ToString();
            }

            switch (ns.kind) {
                case NamespaceKind.NAMESPACE:
                    if (noNameSeparator)
                        return str;
                    if (str.IndexOf(':') == -1)
                        return str + ".";
                    return str + "::";

                case NamespaceKind.ANY:
                    return noNameSeparator ? "A{}" : "A{}::";

                case NamespaceKind.EXPLICIT:
                    return "E{" + str + (noNameSeparator ? "}" : "}::");

                case NamespaceKind.PRIVATE:
                    return "P{" + str + (noNameSeparator ? "}" : "}::");

                case NamespaceKind.PROTECTED:
                    return "Q{" + str + (noNameSeparator ? "}" : "}::");

                case NamespaceKind.STATIC_PROTECTED:
                    return "S{" + str + (noNameSeparator ? "}" : "}::");

                case NamespaceKind.PACKAGE_INTERNAL:
                    return "I{" + str + (noNameSeparator ? "}" : "}::");
            }

            return null;
        }

    }

}
