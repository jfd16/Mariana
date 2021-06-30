using System;
using System.Text;

namespace Mariana.AVM2.Tests.Helpers {

    /// <summary>
    /// This should be used to wrap strings that may contain invalid UTF-16 (e.g. lone surrogates)
    /// in xUint theory data to avoid errors.
    /// See this issue: https://github.com/microsoft/vstest/issues/2602
    /// </summary>
    public readonly struct StringWrapper : IEquatable<StringWrapper> {

        public readonly string value;

        public StringWrapper(string str) => value = str;

        public static implicit operator StringWrapper(string str) => new StringWrapper(str);

        public override bool Equals(object obj) => obj is StringWrapper sw && value == sw.value;

        public override int GetHashCode() => value.GetHashCode();

        public bool Equals(StringWrapper other) => value == other.value;

        public static bool operator ==(StringWrapper x, StringWrapper y) => x.value == y.value;

        public static bool operator !=(StringWrapper x, StringWrapper y) => x.value != y.value;

        public override string ToString() {
            var sb = new StringBuilder();
            sb.Append('"');

            for (int i = 0; i < value.Length; i++) {
                char ch = value[i];

                if (ch == '"' || ch == '\\') {
                    sb.Append('\\').Append(ch);
                }
                else if (ch <= 0x7F && !Char.IsControl(ch)) {
                    sb.Append(ch);
                }
                else {
                    sb.Append('\\');
                    switch (ch) {
                        case '\n':
                            sb.Append('n');
                            break;
                        case '\r':
                            sb.Append('r');
                            break;
                        case '\t':
                            sb.Append('t');
                            break;
                        default:
                            sb.AppendFormat("u{0:X4}", (int)ch);
                            break;
                    }
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

    }

}
