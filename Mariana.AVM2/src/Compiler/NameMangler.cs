using System;
using System.Globalization;
using System.Text;
using Mariana.AVM2.Core;
using Mariana.CodeGen;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Generates mangled names for dynamic types and members emitted by the compiler.
    /// </summary>
    internal static class NameMangler {

        private static char[] s_localNameSpecialChars = {'.', ':', '{', '\\', '\0'};

        private static char[] s_namesapceSpecialChars = {'{', '\\', '\0'};

        [ThreadStatic]
        private static StringBuilder s_threadStringBuilder;

        /// <summary>
        /// Returns the thread-static string builder used by the name mangler.
        /// </summary>
        /// <returns>A thread-static instance of <see cref="StringBuilder"/>.</returns>
        private static StringBuilder _getStringBuilder() {
            StringBuilder sb = s_threadStringBuilder;

            if (sb == null)
                sb = s_threadStringBuilder = new StringBuilder();

            return sb;
        }

        /// <summary>
        /// Creates a mangled name from the given <see cref="QName"/>.
        /// </summary>
        /// <returns>The mangled name string.</returns>
        /// <param name="name">The qualified name from which to create a mangled name.</param>
        public static string createName(in QName name) {
            string nsStr = _mangleNamespace(name.ns);
            string localStr = _mangleLocalName(name.localName);
            return (nsStr.Length == 0) ? localStr : nsStr + localStr;
        }

        /// <summary>
        /// Creates a mangled type name from the given <see cref="QName"/>.
        /// </summary>
        /// <returns>The mangled <see cref="TypeName"/>.</returns>
        /// <param name="name">The qualified name from which to create a mangled type name.</param>
        public static TypeName createTypeName(in QName name) {
            string nsStr = _mangleNamespace(name.ns, true);
            string localStr = _mangleLocalName(name.localName);
            return (nsStr.Length == 0) ? new TypeName(localStr) : new TypeName(nsStr, localStr);
        }

        /// <summary>
        /// Creates a mangled name for a property getter from the given <see cref="QName"/>.
        /// </summary>
        /// <returns>The mangled name string.</returns>
        /// <param name="propName">The qualified name of the property from which to create a
        /// mangled name for the getter.</param>
        public static string createGetterName(QName propName) {
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
        public static string createSetterName(QName propName) {
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
        public static QName createGetterQualifiedName(QName propName) {
            string localStr = "get{" + _mangleLocalName(propName.localName) + "}";
            return new QName(propName.ns, localStr);
        }

        /// <summary>
        /// Creates a mangled name for a property setter from the given <see cref="QName"/>.
        /// </summary>
        /// <returns>The mangled qualified name for the setter.</returns>
        /// <param name="propName">The qualified name of the property from which to create a
        /// mangled name for the setter.</param>
        public static QName createSetterQualifiedName(QName propName) {
            string localStr = "set{" + _mangleLocalName(propName.localName) + "}";
            return new QName(propName.ns, localStr);
        }

        /// <summary>
        /// Creates a mangled name for a script initializer.
        /// </summary>
        /// <returns>The mangled name for the script initializer.</returns>
        /// <param name="uid">A unique identifier for the script.</param>
        public static string createScriptInitName(int uid) => "scriptinit{" + uid.ToString(CultureInfo.InvariantCulture) + "}";

        /// <summary>
        /// Creates a mangled name for a catch scope class.
        /// </summary>
        /// <returns>The mangled name for the catch scope class.</returns>
        /// <param name="uid">A unique identifier for the catch scope.</param>
        public static string createCatchScopeClassName(int uid) => "__CatchScope{" + uid.ToString(CultureInfo.InvariantCulture) + "}";

        /// <summary>
        /// Creates a mangled name for an activation class of a method.
        /// </summary>
        /// <returns>The mangled name for the activation class.</returns>
        /// <param name="uid">A unique identifier for the activation object.</param>
        public static string createActivationClassName(int uid) => "__Activation{" + uid.ToString(CultureInfo.InvariantCulture) + "}";

        /// <summary>
        /// Creates a mangled name for a captured scope container class.
        /// </summary>
        /// <returns>The mangled name for the captured scope container class.</returns>
        /// <param name="uid">A unique identifier for the captured scope container.</param>
        public static string createScopeContainerName(int uid) => "__Scope{" + uid.ToString(CultureInfo.InvariantCulture) + "}";

        /// <summary>
        /// Creates a mangled name for an anonymous function.
        /// </summary>
        /// <returns>The mangled name for the anonymous function.</returns>
        /// <param name="uid">A unique identifier for the anonymous function.</param>
        public static string createAnonFunctionName(int uid) => "function{" + uid.ToString(CultureInfo.InvariantCulture) + "}";

        /// <summary>
        /// Creates a name for an interface method stub.
        /// </summary>
        /// <returns>The name for the interface method stub.</returns>
        /// <param name="ifaceMangledName">The mangled name of the interface.</param>
        /// <param name="methodMangledName">The mangled name of the method being implemented.</param>
        public static string createMethodImplStubName(string ifaceMangledName, string methodMangledName) =>
            "IS:" + ifaceMangledName + ":{" + methodMangledName + "}";

        private static string _mangleLocalName(string str) {
            if (str.Length == 0)
                return "{}";

            int curIndex = 0;
            StringBuilder sb = null;

            while (curIndex < str.Length) {
                int index = str.IndexOfAny(s_localNameSpecialChars, curIndex);
                if (index == -1)
                    break;

                if (sb == null)
                    sb = _getStringBuilder();
                sb.Append(str, curIndex, index - curIndex);
                sb.Append('\\');
                sb.Append((str[index] == '\0') ? '0' : str[index]);
                curIndex = index + 1;
            }

            if (sb != null) {
                sb.Append(str, curIndex, str.Length - curIndex);
                return sb.ToString();
            }

            return str;
        }

        private static string _mangleNamespace(Namespace ns, bool noNameSeparator = false) {
            string str = (ns.kind == NamespaceKind.PRIVATE)
                ? ns.privateNamespaceId.ToString(CultureInfo.InvariantCulture)
                : ns.uri;

            if (str == null)
                str = "";

            if (str.Length == 0 && ns.kind == NamespaceKind.NAMESPACE)
                return "";

            int curIndex = 0;
            StringBuilder sb = null;

            while (curIndex < str.Length) {
                int index = str.IndexOfAny(s_namesapceSpecialChars, curIndex);
                if (index == -1)
                    break;

                if (sb == null)
                    sb = _getStringBuilder();
                sb.Append(str, curIndex, index - curIndex);
                sb.Append('\\');
                sb.Append((str[index] == '\0') ? '0' : str[index]);
                curIndex = index + 1;
            }

            if (sb != null) {
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
