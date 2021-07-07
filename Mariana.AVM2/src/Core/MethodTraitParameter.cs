using System;
using System.Text;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// An object representing a formal parameter of a method or constructor.
    /// </summary>
    public sealed class MethodTraitParameter {

        private readonly string? m_name;
        private readonly Class? m_type;
        private readonly bool m_isOptional;
        private readonly bool m_hasDefault;
        private readonly ASAny m_defaultValue;

        /// <summary>
        /// The name of the parameter. If this is null, the parameter's name is not specified.
        /// </summary>
        public string? name => m_name;

        /// <summary>
        /// The type of the parameter. A null value is used for the any (*) type.
        /// </summary>
        public Class? type => m_type;

        /// <summary>
        /// A Boolean value indicating whether the parameter is an optional parameter,
        /// </summary>
        public bool isOptional => m_isOptional;

        /// <summary>
        /// A Boolean value indicating whether an optional parameter has a default value.
        /// </summary>
        public bool hasDefault => m_hasDefault;

        /// <summary>
        /// The default value of the parameter.
        /// </summary>
        public ASAny defaultValue => m_defaultValue;

        internal MethodTraitParameter(string? name, Class? type, bool isOptional, bool hasDefault, ASAny defaultValue) {
            m_name = name;
            m_type = type;
            m_isOptional = isOptional;
            m_hasDefault = hasDefault;
            m_defaultValue = defaultValue;
        }

        internal static void paramListToString(MethodTraitParameter[] paramList, StringBuilder outStringBuilder) {
            for (int i = 0; i < paramList.Length; i++) {
                if (i != 0)
                    outStringBuilder.Append(", ");

                MethodTraitParameter p = paramList[i];
                outStringBuilder.Append(p.name ?? "param" + ASint.AS_convertString(i));
                if (p.isOptional && !p.hasDefault)
                    outStringBuilder.Append('?');

                outStringBuilder.Append(':').Append(' ');
                outStringBuilder.Append((p.type == null) ? "*" : p.type.name.ToString());

                if (!p.hasDefault)
                    continue;

                if (p.defaultValue.isUndefined) {
                    outStringBuilder.Append(" = undefined");
                }
                else if (p.defaultValue.isNull) {
                    outStringBuilder.Append(" = null");
                }
                else {
                    Class valType = p.defaultValue.AS_class!;
                    outStringBuilder.Append(" = ");

                    if (valType.tag == ClassTag.STRING) {
                        string strval = p.defaultValue.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"");
                        outStringBuilder.Append('"');
                        outStringBuilder.Append(strval);
                        outStringBuilder.Append('"');
                    }
                    else {
                        outStringBuilder.Append(p.defaultValue.ToString());
                    }
                }
            }
        }

    }

}
