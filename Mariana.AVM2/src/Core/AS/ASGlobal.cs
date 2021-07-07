using System;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The top-level global functions.
    /// </summary>
    [AVM2ExportModule]
    public static class ASGlobal {

        /// <summary>
        /// The non-alphanumeric characters that must be not escaped by the <see cref="encodeURI"/>
        /// method.
        /// </summary>
        private const string ENCODE_URI_NO_ENCODE = ";/?:@&=+$,#-_.!~*'()";

        /// <summary>
        /// The non-alphanumeric characters that must not be escaped by the <see cref="encodeURIComponent"/> method.
        /// </summary>
        private const string ENCODE_URI_COMPONENT_NO_ENCODE = "-_.!~*'()";

        /// <summary>
        /// The non-alphanumeric characters that must not be escaped by the <see cref="escape"/>
        /// method.
        /// </summary>
        private const string ESCAPE_NO_ENCODE = "@-_.*+/";

        /// <summary>
        /// The characters whose escape sequences must not be decoded by the <see cref="decodeURI"/>
        /// method.
        /// </summary>
        private const string DECODE_URI_NO_DECODE = "#$&+,/:;=?@";

        /// <summary>
        /// The floating-point value of positive infinity.
        /// </summary>
        [AVM2ExportTrait]
        public const double Infinity = Double.PositiveInfinity;

        /// <summary>
        /// The floating-point value of negative infinity.
        /// </summary>
        /// <remarks>
        /// The exported name of this constant is <c>-Infinity</c>. This name is not used as the
        /// member name as it is not a valid identifier.
        /// </remarks>
        [AVM2ExportTrait(name = "-Infinity")]
        public const double minusInfinity = Double.NegativeInfinity;

        /// <summary>
        /// The floating-point value of NaN (Not-a-number).
        /// </summary>
        [AVM2ExportTrait]
        public const double NaN = Double.NaN;

        /// <summary>
        /// The AS3 namespace, which is used for the names of class-based methods of the core classes.
        /// </summary>
        [AVM2ExportTrait]
        public static readonly ASNamespace AS3 = new ASNamespace(Namespace.AS3.uri!);

        /// <summary>
        /// Decodes a URI-encoded string.
        /// </summary>
        /// <param name="uri">The URI string to decode.</param>
        /// <returns>The decoded URI string.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>URIError #1052: The URI cannot be decoded; for example, because it contains a percent sign
        /// that is not followed by hexadecimal digits.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method is to be used for full URIs, as it it does not the escape sequences of
        /// separator characters (<c># $ &amp; + , / : ; = ?</c>). For decoding a part of a URI
        /// (such as a parameter of a query string), use the <see cref="decodeURIComponent"/>
        /// method, which resolves all escape sequences.
        /// </remarks>
        [AVM2ExportTrait]
        public static string decodeURI(string uri = "undefined") {
            if (uri == null)
                return "null";

            if (URIUtil.tryDecode(uri, DECODE_URI_NO_DECODE, failOnInvalidSurrogate: false, out string? decoded))
                return decoded;

            throw ErrorHelper.createError(ErrorCode.INVALID_URI, "decodeURI");
        }

        /// <summary>
        /// Decodes a URI-encoded string.
        /// </summary>
        /// <param name="uri">The URI string to decode.</param>
        /// <returns>The decoded URI string.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>URIError #1052: The URI cannot be decoded; for example, because it contains a percent sign
        /// that is not followed by hexadecimal digits.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method is to be used for parts of URIs, as it resolves all escape sequences. For
        /// decoding a full URI, use the <see cref="decodeURI"/> method, which does not resolve
        /// escape sequences of URI separator characters.
        /// </remarks>
        [AVM2ExportTrait]
        public static string decodeURIComponent(string uri = "undefined") {
            if (uri == null)
                return "null";

            if (URIUtil.tryDecode(uri, "", failOnInvalidSurrogate: false, out string? decoded))
                return decoded;

            throw ErrorHelper.createError(ErrorCode.INVALID_URI, "decodeURIComponent");
        }

        /// <summary>
        /// Encodes the given string as a URI.
        /// </summary>
        /// <param name="uri">The string to encode.</param>
        /// <returns>The encoded URI string.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>URIError #1052: The string cannot be encoded as a URI.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method is intended for encoding a full URI, as it does not escape separator
        /// characters such as '/' and '?'. To escape a string which is used as a part of a URI (such
        /// as a query string parameter), use the <see cref="encodeURIComponent"/> method which
        /// escapes the separator characters. The following characters are not escaped: letters (A-Z
        /// and a-z), digits (0-9) and these special characters: <c>; / ? : &amp; = + $ , # - _ . !
        /// * ' ( )</c>
        /// </remarks>
        [AVM2ExportTrait]
        public static string encodeURI(string uri = "undefined") {
            if (uri == null)
                return "null";

            if (URIUtil.tryEncode(uri, ENCODE_URI_NO_ENCODE, failOnInvalidSurrogate: false, out string? encoded))
                return encoded;

            throw ErrorHelper.createError(ErrorCode.INVALID_URI, "encodeURI");
        }

        /// <summary>
        /// Encodes the given string as a URI component.
        /// </summary>
        /// <param name="uri">The string to encode.</param>
        /// <returns>The encoded URI component string.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>URIError #1052: The string cannot be encoded as a URI.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method is intended for use for encoding a part of a URI, as it escapes separator
        /// characters. Use the <see cref="encodeURI"/> method for encoding a full URI. The
        /// following characters are not escaped: letters (A-Z and a-z), digits (0-9) and these
        /// special characters: <c>- _ . ! * ' ( )</c>
        /// </remarks>
        [AVM2ExportTrait]
        public static string encodeURIComponent(string uri = "undefined") {
            if (uri == null)
                return "null";

            if (URIUtil.tryEncode(uri, ENCODE_URI_COMPONENT_NO_ENCODE, failOnInvalidSurrogate: false, out string? encoded))
                return encoded;

            throw ErrorHelper.createError(ErrorCode.INVALID_URI, "encodeURIComponent");
        }

        /// <summary>
        /// Replaces special characters in the given string with escaped percent-encoded values and
        /// returns the encoded string.
        /// </summary>
        /// <param name="str">The string to encode.</param>
        /// <returns>The encoded string.</returns>
        ///
        /// <remarks>
        /// <para>
        /// Characters whose code point values are greater than 0xFF (one byte) are encoded using the
        /// notation <c>%uxxxx</c>, where xxxx are hexadecimal digits representing the character's
        /// code point. For all other escaped characters, the encoding <c>%xx</c> is used (where
        /// xx are the hexadecimal digits of the character's code point) The following characters are
        /// not escaped: letters (A-Z and a-z), digits (0-9) and these special characters: <c>@ - _
        /// . * + /</c>
        /// </para>
        /// <para>This method must not be used for encoding URIs; use <see cref="encodeURI"/> or
        /// <see cref="encodeURIComponent"/> instead. It is only retained for backward compatibility
        /// with older versions of ECMAScript.</para>
        /// </remarks>
        [AVM2ExportTrait]
        public static string escape(string str = "undefined") {
            if (str == null)
                return "null";

            int strlen = str.Length;

            char[] buffer = new char[strlen];
            int bufPos = 0, bufLen = 0;

            for (int i = 0; i < str.Length; i++) {
                char ch = str[i];

                bool noEncode =
                    ((uint)(ch - '0') <= 9)
                    || ((uint)(ch - 'A') <= 25)
                    || ((uint)(ch - 'a') <= 25)
                    || (ch <= 0x7F && ESCAPE_NO_ENCODE.Contains(ch));

                if (noEncode) {
                    // No percent encoding
                    if (bufPos == bufLen) {
                        DataStructureUtil.expandArray(ref buffer);
                        bufLen = buffer.Length;
                    }
                    buffer[bufPos++] = ch;
                    continue;
                }

                if (ch > 0xFF) {
                    if (bufLen - bufPos < 6) {
                        DataStructureUtil.expandArray(ref buffer, 6);
                        bufLen = buffer.Length;
                    }

                    var bufferSpan = buffer.AsSpan(6);
                    bufferSpan[0] = '%';
                    bufferSpan[1] = 'u';
                    URIUtil.byteToHex((byte)(ch >> 8), bufferSpan.Slice(2));
                    URIUtil.byteToHex((byte)ch, bufferSpan.Slice(4));
                    bufPos += 6;
                }
                else {
                    if (bufLen - bufPos < 3) {
                        DataStructureUtil.expandArray(ref buffer, 3);
                        bufLen = buffer.Length;
                    }
                    buffer[bufPos] = '%';
                    URIUtil.byteToHex((byte)ch, buffer.AsSpan(bufPos + 1));
                    bufPos += 3;
                }
            }

            return new string(buffer, 0, bufPos);
        }

        /// <summary>
        /// Returns true if the given number is finite.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <returns>True if <paramref name="num"/> is finite, false if it is infinite or
        /// NaN.</returns>
        [AVM2ExportTrait]
        public static bool isFinite(double num = Double.NaN) => Double.IsFinite(num);

        /// <summary>
        /// Returns true if the given number is NaN, otherwise returns false. This method must be used
        /// to check NaNs, as the equality operators do not consider them as equal.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <returns>True if <paramref name="num"/> is NaN, false otherwise.</returns>
        [AVM2ExportTrait]
        public static bool isNaN(double num = Double.NaN) => Double.IsNaN(num);

        /// <summary>
        /// Returns a Boolean value indicating whether the given string is a valid name for an XML
        /// element or attribute.
        /// </summary>
        /// <param name="str">The string to check.</param>
        /// <returns>True if the string is a valid XML name, false otherwise.</returns>
        [AVM2ExportTrait]
        public static bool isXMLName(string? str = null) => XMLHelper.isValidName(str);

        /// <summary>
        /// Parses a string into a floating-point number.
        /// </summary>
        /// <param name="str">The string to parse.</param>
        /// <returns>The number, or NaN if <paramref name="str"/> is null.</returns>
        ///
        /// <remarks>
        /// This is similar to the Number conversion function, except that it ignores trailing
        /// non-numeric characters and returns NaN instead of 0 if the input string is null, empty or
        /// containing only white space.
        /// </remarks>
        [AVM2ExportTrait]
        public static double parseFloat(string? str = null) {
            if (str != null && NumberFormatHelper.stringToDouble(str, out double val, out _, strict: false))
                return val;

            return Double.NaN;
        }

        /// <summary>
        /// Parses an integer from a string, and returns it as a floating-point value.
        /// </summary>
        ///
        /// <param name="str">
        /// The string to parse. If this string starts with '0x' or '0X' and
        /// <paramref name="radix"/> is 0 or 16, the number is parsed as a hexadecimal integer.
        /// Otherwise, it is interpreted as an integer in base <paramref name="radix"/>, with
        /// letters starting from A (case-insensitive) being used as digits after 9 in bases greater
        /// than 10.
        /// </param>
        /// <param name="radix">The base in which the string must be interpreted. If this is
        /// zero (the default), base 10 will be used except when the string has the '0x' or '0X'
        /// prefix, in which case base 16 is used. If this is not zero, it must be between 2
        /// and 36 (inclusive).</param>
        ///
        /// <returns>The parsed integer as a 64-bit floating-point number. If <paramref name="str"/>
        /// is null, empty or not a valid numeric string, or <paramref name="radix"/> is not 0 or
        /// between 2 and 36 inclusive, NaN is returned.</returns>
        ///
        /// <remarks>
        /// <para>
        /// Leading spaces in the string are ignored. If the string contains a character that
        /// is not a valid digit for the given radix, the prefix of the string until and not
        /// including the first such character will be parsed.
        /// </para>
        /// <para>Integers outside the range of ±2^53 cannot be exactly represented; an approximate
        /// value will be returned in such cases.</para>
        /// </remarks>
        [AVM2ExportTrait]
        public static double parseInt(string? str = null, int radix = 0) {
            if (str == null || str.Length == 0)
                return Double.NaN;

            bool neg = false, checkHexPrefix = true;

            ReadOnlySpan<char> span = str.AsSpan(NumberFormatHelper.indexOfFirstNonSpace(str));
            if (span.Length == 0)
                return Double.NaN;

            if (span[0] == '-') {
                neg = true;
                span = span.Slice(1);
            }
            else if (span[0] == '+') {
                span = span.Slice(1);
            }

            if (radix != 0) {
                if (radix < 2 || radix > 36)
                    return Double.NaN;
                checkHexPrefix = radix == 16;
            }
            else {
                radix = 10;
            }

            if (checkHexPrefix && span.Length >= 2 && span[0] == '0' && (span[1] == 'x' || span[1] == 'X')) {
                radix = 16;
                span = span.Slice(2);
            }

            double num = NumberFormatHelper.stringToDoubleIntRadix(span, radix, out int charsConsumed);

            if (charsConsumed == 0)
                return Double.NaN;

            return neg ? -num : num;
        }

        /// <summary>
        /// Writes the string representations of the arguments to the standard output.
        /// </summary>
        /// <param name="args">The arguments to write to the standard output.</param>
        /// <remarks>
        /// If more than one argument is given, the arguments are separated by spaces. A new line is
        /// written after all arguments have been output.
        /// </remarks>
        [AVM2ExportTrait]
        public static void trace(RestParam args = default) {
            switch (args.length) {
                case 0:
                    Console.WriteLine();
                    break;
                case 1:
                    Console.WriteLine(ASAny.AS_convertString(args[0]));
                    break;
                default:
                    for (int i = 0, n = args.length; i < n; i++) {
                        if (i != 0)
                            Console.Write(' ');
                        Console.Write(ASAny.AS_convertString(args[i]));
                    }
                    Console.WriteLine();
                    break;
            }
        }

        /// <summary>
        /// Decodes a string that was encoded by the <see cref="escape"/> function.
        /// </summary>
        /// <param name="str">The string encoded by <see cref="escape"/> to decode.</param>
        /// <returns>The decoded string.</returns>
        ///
        /// <remarks>
        /// <para>
        /// This replaces all <c>%xx</c> and <c>%uxxxx</c> sequences (where x is a hexadecimal
        /// digit) with the characters whose code points are represented by those hexadecimal
        /// sequences. '%' characters not followed by valid escape sequences are not an error; they
        /// are interpreted as literal percent signs.
        /// </para>
        /// <para>This method must not be used for decoding URIs; use <see cref="decodeURI"/> or
        /// <see cref="decodeURIComponent"/> instead. It is only retained for backward compatibility
        /// with older versions of ECMAScript.</para>
        /// </remarks>
        [AVM2ExportTrait]
        public static string unescape(string str = "undefined") {
            if (str == null)
                return "null";

            char[] buffer = new char[str.Length];
            int bufPos = 0;

            ReadOnlySpan<char> strSpan = str;

            while (strSpan.Length > 0) {
                char ch = strSpan[0];

                if (ch != '%' || strSpan.Length <= 1) {
                    buffer[bufPos++] = ch;
                    strSpan = strSpan.Slice(1);
                    continue;
                }

                if (strSpan[1] == 'u' && strSpan.Length >= 6) {
                    if (URIUtil.hexToByte(strSpan.Slice(2), out byte b1)
                        && URIUtil.hexToByte(strSpan.Slice(4), out byte b2))
                    {
                        ch = (char)(b1 << 8 | b2);
                        buffer[bufPos++] = ch;
                        strSpan = strSpan.Slice(6);
                    }
                }
                else if (strSpan.Length >= 3) {
                    if (URIUtil.hexToByte(strSpan.Slice(1), out byte b)) {
                        ch = (char)b;
                        buffer[bufPos++] = ch;
                        strSpan = strSpan.Slice(3);
                    }
                }
                else {
                    buffer[bufPos++] = ch;
                    strSpan = strSpan.Slice(1);
                }
            }

            return new string(buffer, 0, bufPos);
        }

    }

}
