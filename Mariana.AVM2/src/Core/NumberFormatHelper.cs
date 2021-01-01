using System;

namespace Mariana.AVM2.Core {

    internal static class NumberFormatHelper {

        /// <summary>
        /// Number format strings for the ActionScript toFixed() function.
        /// </summary>
        private static readonly string[] s_toFixedFormatStrings = {
            "F0", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10",
            "F11", "F12", "F13", "F14", "F15", "F16", "F17", "F18", "F19", "F20",
        };

        /// <summary>
        /// Number format strings for the ActionScript toExponential() function.
        /// </summary>
        private static readonly string[] s_toExponentialFormatStrings = {
            "0e+0",
            "0.0e+0",
            "0.00e+0",
            "0.000e+0",
            "0.0000e+0",
            "0.00000e+0",
            "0.000000e+0",
            "0.0000000e+0",
            "0.00000000e+0",
            "0.000000000e+0",
            "0.0000000000e+0",
            "0.00000000000e+0",
            "0.000000000000e+0",
            "0.0000000000000e+0",
            "0.00000000000000e+0",
            "0.000000000000000e+0",
            "0.0000000000000000e+0",
            "0.00000000000000000e+0",
            "0.000000000000000000e+0",
            "0.0000000000000000000e+0",
            "0.00000000000000000000e+0",
        };

        /// <summary>
        /// The maximum number of fractional digits in <see cref="doubleToString"/>,
        /// <see cref="intToString"/> and <see cref="longToString"/>.
        /// </summary>
        private const int MAX_FRACTIONAL_DIGITS = 30;

        /// <summary>
        /// Temporary buffer used by the <see cref="doubleToString"/>, <see cref="intToString"/>
        /// and <see cref="longToString"/> functions.
        /// </summary>
        [ThreadStatic]
        private static char[] s_threadBuffer;

        private static readonly double[] s_powersOf10Table = _createPowersOf10Table();

        private static double[] _createPowersOf10Table() {
            double[] arr = new double[632];
            int i;
            double f = 1;
            for (i = 323; i < 632; i++) {
                arr[i] = f;
                f *= 10.0;
            }
            f = 0.1;
            for (i = 322; i >= 0; i--) {
                arr[i] = f;
                f /= 10.0;
            }
            return arr;
        }

        /// <summary>
        /// Gets the format string for formatting a number using ActionScript 3's toFixed function.
        /// </summary>
        /// <param name="prec">The precision argument of toFixed.</param>
        /// <returns>The format string which can be used in functions such as String.Format.</returns>
        public static string getToFixedFormatString(int prec) => s_toFixedFormatStrings[prec];

        /// <summary>
        /// Gets the format string for formatting a number using ActionScript 3's toExponential
        /// function.
        /// </summary>
        /// <param name="prec">The precision argument of toExponential.</param>
        /// <returns>The format string which can be used in functions such as String.Format.</returns>
        public static string getToExponentialFormatString(int prec) => s_toExponentialFormatStrings[prec];

        /// <summary>
        /// Expands a temporary buffer.
        /// </summary>
        /// <param name="buffer">The buffer to expand. This will be overwritten by the expanded
        /// buffer.</param>
        /// <param name="position">The current write position in the buffer. This will be
        /// overwritten with the new position.</param>
        /// <param name="newSize">An output argument into which the expanded buffer size will be written.</param>
        /// <param name="reverse">If true, copies the content of the existing buffer at the end
        /// (instead of the beginning) of the new buffer.</param>
        /// <returns>The expanded buffer array.</returns>
        private static char[] _expandBuffer(ref char[] buffer, ref int position, out int newSize, bool reverse = false) {
            int oldLen = buffer.Length;
            var newBuffer = new char[oldLen * 2];

            if (reverse) {
                int copyStart = position + 1;
                buffer.AsSpan(copyStart).CopyTo(newBuffer.AsSpan(oldLen + copyStart));
                position += oldLen;
            }
            else {
                buffer.AsSpan(0, position).CopyTo(newBuffer.AsSpan(0, position));
            }

            buffer = newBuffer;
            newSize = newBuffer.Length;
            return newBuffer;
        }

        /// <summary>
        /// Returns a string representation of the number in the given base.
        /// </summary>
        /// <param name="num">The number. This must not be infinite or NaN.</param>
        /// <param name="radix">The base. This must be between 2 and 36.</param>
        /// <returns>The string representation of the number in the given base.</returns>
        public static string doubleToString(double num, int radix) {
            // Check if the number can be represented as an integer without precision loss
            // (in which case a faster method can be called)
            int num_i = (int)num;
            if ((double)num_i == num)
                return intToString(num_i, radix);

            if (s_threadBuffer == null)
                s_threadBuffer = new char[16];

            char[] buffer = s_threadBuffer;
            int bufPos = 0, bufSize = buffer.Length;

            if (num < 0) {
                num = -num;
                buffer[0] = '-';
                bufPos = 1;
            }

            int log = (int)Math.Floor(Math.Log(num) / Math.Log(radix));
            double div = Math.Pow(radix, log);

            if (Double.IsInfinity(div))
                // Number cannot be converted to string
                return null;

            if (log < 0) {
                buffer[bufPos++] = '0';
                buffer[bufPos++] = '.';

                for (int i = log; i < -1; i++) {
                    if (bufPos >= bufSize)
                        buffer = _expandBuffer(ref s_threadBuffer, ref bufPos, out bufSize, false);

                    buffer[bufPos++] = '0';
                }
            }

            while (num != 0 && div != 0 && log >= -MAX_FRACTIONAL_DIGITS) {
                int digit = (int)(num / div);

                if (bufPos >= bufSize)
                    buffer = _expandBuffer(ref s_threadBuffer, ref bufPos, out bufSize, false);

                buffer[bufPos++] = (char)((digit > 9) ? 87 + digit : 48 + digit);
                num %= div;
                div = (log > 0) ? Math.Round(div / radix) : div / radix;
                log--;

                if (log == -1) {
                    if (bufPos >= bufSize)
                        buffer = _expandBuffer(ref s_threadBuffer, ref bufPos, out bufSize, false);

                    buffer[bufPos++] = '.';
                }
            }

            // Fill in remaining zeroes if necessary
            while (log >= 0) {
                if (bufPos >= bufSize)
                    buffer = _expandBuffer(ref s_threadBuffer, ref bufPos, out bufSize, false);

                buffer[bufPos++] = '0';
                log--;
            }

            // If there is a trailing decimal point, ignore it
            return new string(buffer, 0, (buffer[bufPos - 1] == '.') ? bufPos - 1 : bufPos);
        }

        /// <summary>
        /// Returns a string representation of the number in the given base.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <param name="radix">The base. This must be between 2 and 36.</param>
        /// <returns>The string representation of the number in the given base.</returns>
        /// <remarks>
        /// The most negative 64-bit integer (2^63-1) cannot be used with this method.
        /// </remarks>
        public static string longToString(long num, int radix) {
            if (s_threadBuffer == null)
                s_threadBuffer = new char[16];

            char[] buffer = s_threadBuffer;
            int bufPos = buffer.Length - 1;
            bool neg = num < 0;
            if (neg)
                num = -num;

            int bitShift = -1;
            switch (radix) {
                case 2:
                    bitShift = 1;
                    break;
                case 4:
                    bitShift = 2;
                    break;
                case 8:
                    bitShift = 3;
                    break;
                case 16:
                    bitShift = 4;
                    break;
                case 32:
                    bitShift = 5;
                    break;
            }

            if (bitShift != -1) {
                int mask = (1 << bitShift) - 1;
                while (num != 0) {
                    int digit = (int)num & mask;

                    if (bufPos < 0)
                        buffer = _expandBuffer(ref s_threadBuffer, ref bufPos, out _, true);

                    buffer[bufPos--] = (char)((digit > 9) ? ('a' - 10) + digit : '0' + digit);
                    num >>= bitShift;
                }
            }
            else {
                while (num != 0) {
                    long num2 = num / radix;
                    int digit = (int)(num - num2 * radix);

                    if (bufPos < 0)
                        buffer = _expandBuffer(ref s_threadBuffer, ref bufPos, out _, true);

                    buffer[bufPos--] = (char)((digit > 9) ? ('a' - 10) + digit : '0' + digit);
                    num = num2;
                }
            }

            if (neg) {
                if (bufPos < 0)
                    buffer = _expandBuffer(ref s_threadBuffer, ref bufPos, out _, true);

                buffer[bufPos--] = '-';
            }

            return new string(buffer, bufPos + 1, buffer.Length - bufPos - 1);
        }

        /// <summary>
        /// Returns a string representation of the number in the given base.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <param name="radix">The base. This must be between 2 and 36.</param>
        /// <returns>The string representation of the number in the given base.</returns>
        public static string intToString(int num, int radix) {
            if (s_threadBuffer == null)
                s_threadBuffer = new char[16];

            char[] buffer = s_threadBuffer;
            int bufPos = buffer.Length - 1;
            bool neg = num < 0;
            if (neg) {
                if (num == Int32.MinValue)
                    // This is a special value that cannot be negated in 32 bits, so widen it to 64 bits
                    return longToString((long)num, radix);
                num = -num;
            }

            int bitShift = -1;
            switch (radix) {
                case 2:
                    bitShift = 1;
                    break;
                case 4:
                    bitShift = 2;
                    break;
                case 8:
                    bitShift = 3;
                    break;
                case 16:
                    bitShift = 4;
                    break;
                case 32:
                    bitShift = 5;
                    break;
            }

            if (bitShift != -1) {
                // Optimized for power-of-two bases
                while (num != 0) {
                    int num2 = num >> bitShift;
                    num -= num2 << bitShift;

                    if (bufPos < 0)
                        buffer = _expandBuffer(ref s_threadBuffer, ref bufPos, out _, true);

                    buffer[bufPos--] = (char)((num > 9) ? 87 + num : 48 + num);
                    num = num2;
                }
            }
            else {
                // For non-power-of-two bases
                while (num != 0) {
                    int num2 = num / radix;
                    num -= num2 * radix;
                    if (bufPos < 0)
                        buffer = _expandBuffer(ref s_threadBuffer, ref bufPos, out _, true);

                    buffer[bufPos--] = (char)((num > 9) ? 87 + num : 48 + num);
                    num = num2;
                }
            }

            if (neg) {
                if (bufPos < 0)
                    buffer = _expandBuffer(ref s_threadBuffer, ref bufPos, out _, true);

                buffer[bufPos--] = '-';
            }

            return new string(buffer, bufPos + 1, buffer.Length - bufPos - 1);
        }

        /// <summary>
        /// Returns the index of the first character in the given span that is not considered
        /// a white space character for the purpose of number parsing.
        /// </summary>
        /// <param name="span">The span of characters.</param>
        /// <returns>The index of the first non-whitespace character in <paramref name="span"/>, or
        /// the length of <paramref name="span"/> if it only contains whitespace characters.</returns>
        public static int indexOfFirstNonSpace(ReadOnlySpan<char> span) {
            // See: skipSpaces function in avmplus source
            // https://github.com/adobe/avmplus/blob/master/core/MathUtils.cpp
            int i;
            for (i = 0; i < span.Length; i++) {
                char ch = span[i];
                if (ch == ' ' || (uint)(ch - 9) <= 4) {
                    // \r, \n, \f, \t, \v, space
                    continue;
                }

                if (ch < 0x2000 || (ch > 0x200B && ch != 0x2028 && ch != 0x2029 && ch != 0x205F&& ch != 0x3000))
                    break;
            }

            return i;
        }

        /// <summary>
        /// Parses a string or character span into a 64-bit IEEE 754 floating-point number.
        /// </summary>
        ///
        /// <param name="span">The character span to parse.</param>
        /// <param name="value">The parsed floating-point number.</param>
        /// <param name="charsRead">The number of characters consumed from <paramref name="span"/>, if
        /// parsing was successful.</param>
        /// <param name="strict">If this is true, non-numeric non-space trailing characters are not
        /// allowed and will result in the string being invalid.</param>
        /// <param name="allowHex">If this is true, the 0x or 0X prefix results in the string being
        /// interpreted as a hexadecimal integer.</param>
        ///
        /// <returns>True if the string is valid, false otherwise.</returns>
        public static bool stringToDouble(
            ReadOnlySpan<char> span, out double value, out int charsRead, bool strict = true, bool allowHex = true)
        {
            var originalSpan = span;

            value = 0;
            charsRead = 0;

            span = span.Slice(indexOfFirstNonSpace(span));
            if (span.Length == 0)
                return false;

            double num = 0;
            bool isNeg = false, isHex = false;

            if (span[0] == '-') {
                isNeg = true;
                span = span.Slice(1);
            }
            else if (span[0] == '+') {
                span = span.Slice(1);
            }

            if (span.Length >= 2 && span[0] == '0' && (span[1] == 'x' || span[1] == 'X'))
            {
                // 0x prefix - hexadecimal
                if (!allowHex)
                    return false;
                isHex = true;
                span = span.Slice(2);
            }

            if (span.Length == 0)
                return false;

            var numSpan = span;

            if (isHex) {
                // Hexadecimal numbers are parsed as integers, and decimal points are illegal
                int i;
                for (i = 0; i < span.Length; i++) {
                    char c = span[i];

                    if ((uint)(c - '0') <= 9)  // 0-9
                        num = (c - '0') + num * 16;
                    else if ((uint)(c - 'A') <= 5)  // A-F
                        num = (c - ('A' - 10)) + num * 16;
                    else if ((uint)(c - 'a') <= 5)  // a-f
                        num = (c - ('a' - 10)) + num * 16;
                    else if (c == ' ' || (c <= 13 && c >= 9))
                        break;
                    else
                        return false;
                }
                span = span.Slice(i);
            }
            else {
                if ((uint)span.Length >= 8 && span[0] == 'I'
                    && span.Slice(0, 8).Equals("Infinity", StringComparison.Ordinal))
                {
                    num = Double.PositiveInfinity;
                    span = span.Slice(8);
                }
                else {
                    _internalParseDoubleNoSign(span, out num, out int consumed);
                    span = span.Slice(consumed);
                }
            }

            if (numSpan.Length == span.Length)
                return false;

            if (strict) {
                if (indexOfFirstNonSpace(span) != span.Length)
                    return false;
                span = default;
            }

            charsRead = originalSpan.Length - span.Length;
            value = isNeg ? -num : num;
            return true;
        }

        private static void _internalParseDoubleNoSign(ReadOnlySpan<char> span, out double val, out int charsRead) {
            double mag = 0.0;
            int effectiveExponent = 0;

            int length = span.Length;
            int curIndex = 0;
            int dotPos = -1;
            bool hasSigDigits = false;
            double[] p10Table = s_powersOf10Table;

            // Get to the first significant digit.
            do {
                char ch = span[curIndex];
                if (ch == '.') {
                    if (dotPos != -1)
                        break;
                    dotPos = curIndex;
                }
                else if (ch != '0') {
                    if (ch >= '1' && ch <= '9')
                        hasSigDigits = true;
                    break;
                }
                curIndex++;
            } while (curIndex < length);

            if (dotPos == 0 && curIndex == 1 && !hasSigDigits) {
                // Don't consume a single decimal point without any digits before or after it.
                val = 0.0;
                charsRead = 0;
                return;
            }

            if (hasSigDigits) {
                // Parse the magnitude.
                // To avoid a potential loss of precision due to otherwise unnecessary
                // zeroes (e.g. in "234.356000000000000000000"), we keep a track of the
                // number of consecutive zeroes and multiply by the corresponding power of
                // 10 whenever a nonzero digit is encountered.

                int zeroChainLength = 0;

                int readEndIndex = curIndex + 19;
                if (readEndIndex > length)
                    readEndIndex = length;

                while (curIndex < readEndIndex) {
                    char ch = span[curIndex];

                    if (ch == '0') {
                        zeroChainLength++;
                    }
                    else if ((uint)(ch - '1') <= 8) {
                        switch (zeroChainLength) {
                            case 0:
                                mag = mag * 10.0 + (double)(ch - '0');
                                break;
                            case 1:
                                mag = mag * 100.0 + (double)(ch - '0');
                                zeroChainLength = 0;
                                break;
                            case 2:
                                mag = mag * 1000.0 + (double)(ch - '0');
                                zeroChainLength = 0;
                                break;
                            case 3:
                                mag = mag * 10000.0 + (double)(ch - '0');
                                zeroChainLength = 0;
                                break;
                            default:
                                mag = mag * p10Table[zeroChainLength + 324] + (double)(ch - '0');
                                zeroChainLength = 0;
                                break;
                        }
                    }
                    else if (ch == '.' && dotPos == -1) {
                        dotPos = curIndex;
                    }
                    else {
                        break;
                    }

                    curIndex++;
                }

                // Discard any extra digits (treat them as zero)
                while (curIndex < length) {
                    char ch = span[curIndex];

                    if ((uint)(ch - '0') <= 9)
                        zeroChainLength++;
                    else if (ch == '.' && dotPos == -1)
                        dotPos = curIndex;
                    else
                        break;

                    curIndex++;
                }

                // If there is are trailing zeroes, absorb them into the effective exponent.
                if (dotPos == -1)
                    effectiveExponent = zeroChainLength;
                else
                    effectiveExponent = zeroChainLength - (curIndex - dotPos - 1);
            }

            if (curIndex < length && (span[curIndex] == 'e' || span[curIndex] == 'E')) {
                // Parse the exponent.

                bool expNegative = false;
                bool hasExpDigit = false;
                int expStartIndex = curIndex;
                int expValue = 0;

                curIndex++;

                if (curIndex < length) {
                    if (span[curIndex] == '-') {
                        expNegative = true;
                        curIndex++;
                    }
                    else if (span[curIndex] == '+') {
                        curIndex++;
                    }
                }

                while (curIndex < length) {
                    char ch = span[curIndex];
                    if ((uint)(ch - '0') <= 9) {
                        hasExpDigit = true;
                        expValue = expValue * 10 + (ch - '0');
                    }
                    else {
                        break;
                    }
                    curIndex++;
                }

                if (!hasExpDigit) {
                    // If the exponent string is only "e", "e+" or "e-" without any following
                    // digits, it should not be consumed.
                    expValue = 0;
                    curIndex = expStartIndex;
                }
                else if (expNegative) {
                    expValue = -expValue;
                }

                effectiveExponent += expValue;
            }

            charsRead = curIndex;

            if (effectiveExponent > 308) {
                val = Double.PositiveInfinity;
                return;
            }

            while (effectiveExponent < -323 && mag >= 10.0) {
                mag /= 10.0;
                effectiveExponent++;
            }

            if (effectiveExponent < -323) {
                val = (effectiveExponent == -324) ? (mag / 10.0) * p10Table[0] : 0.0;
                return;
            }

            if (effectiveExponent > 0 || effectiveExponent < -308)
                val = mag * p10Table[effectiveExponent + 323];
            else if (effectiveExponent < 0)
                val = mag / p10Table[323 - effectiveExponent];
            else
                val = mag;
        }

        /// <summary>
        /// Parses a string or character span into a 32-bit signed integer.
        /// </summary>
        ///
        /// <param name="span">The character span to parse.</param>
        /// <param name="charsRead">The number of characters read from <paramref name="span"/>, if
        /// parsing was successful.</param>
        /// <param name="value">The parsed integer.</param>
        /// <param name="strict">If this is true, non-numeric non-space trailing characters are not
        /// allowed and will result in the string being invalid.</param>
        /// <param name="allowHex">If this is true, the 0x or 0X prefix results in the string being
        /// interpreted as a hexadecimal integer.</param>
        ///
        /// <returns>True if the string is valid, false otherwise.</returns>
        /// <remarks>
        /// If the substring begins with 0x or 0X, it is interpreted as a hexadecimal integer.
        /// </remarks>
        public static bool stringToInt(
            ReadOnlySpan<char> span, out int value, out int charsRead, bool strict = true, bool allowHex = true)
        {
            var originalSpan = span;

            value = 0;
            charsRead = 0;

            span = span.Slice(indexOfFirstNonSpace(span));
            if (span.Length == 0)
                return false;

            int num = 0;

            bool neg = false, hex = false;
            if (span[0] == '-') {
                neg = true;
                span = span.Slice(1);
            }
            else if (span[0] == '+') {
                span = span.Slice(1);
            }

            if (span.Length >= 2 && span[0] == '0' && (span[1] == 'x' || span[1] == 'X')) {
                if (!allowHex)
                    return false;
                hex = true;
                span = span.Slice(2);
            }

            if (span.Length == 0)
                return false;

            var numSpan = span;

            if (hex) {
                int i;
                for (i = 0; i < span.Length; i++) {
                    char c = span[i];
                    if ((uint)(c - '0') <= 9)
                        num = (c - '0') + (num << 4);
                    else if ((uint)(c - 'A') <= 5)
                        num = (c - ('A' - 10)) + (num << 4);
                    else if ((uint)(c - 'a') <= 9)
                        num = (c - ('a' - 10)) + (num << 4);
                    else
                        break;
                }
                span = span.Slice(i);
            }
            else {
                int i;
                for (i = 0; i < span.Length; i++) {
                    char c = span[i];
                    if ((uint)(c - '0') > 9)
                        break;
                    num = c - '0' + num * 10;
                }
                span = span.Slice(i);
            }

            if (span.Length == numSpan.Length)
                return false;

            if (strict) {
                if (indexOfFirstNonSpace(span) != span.Length)
                    return false;
                span = default;
            }

            charsRead = originalSpan.Length - span.Length;
            value = neg ? -num : num;
            return true;
        }

        /// <summary>
        /// Parses a string or character span into a 32-bit unsigned integer.
        /// </summary>
        ///
        /// <param name="str">The character span to parse.</param>
        /// <param name="value">The parsed integer.</param>
        /// <param name="charsRead">The number of characters read from <paramref name="str"/>, if
        /// parsing was successful.</param>
        /// <param name="strict">If this is true, non-numeric non-space trailing characters are not
        /// allowed and will result in the string being invalid.</param>
        /// <param name="allowHex">If this is true, the 0x or 0X prefix results in the string being
        /// interpreted as a hexadecimal integer.</param>
        ///
        /// <returns>True if the string is valid, false otherwise.</returns>
        /// <remarks>
        /// If the substring begins with 0x or 0X, it is interpreted as a hexadecimal integer.
        /// </remarks>
        public static bool stringToUint(
            ReadOnlySpan<char> str, out uint value, out int charsRead, bool strict = true, bool allowHex = true)
        {
            bool success = stringToInt(str, out int ival, out charsRead, strict, allowHex);
            value = (uint)ival;
            return success;
        }

        /// <summary>
        /// Parses a string into an unsigned integer. The string should be in the format required for
        /// an array index (no sign, no decimal point, no exponent, no spaces, no hex prefix and no
        /// overflows)
        /// </summary>
        ///
        /// <param name="s">The string.</param>
        /// <param name="allowLeadingZeroes">Set this to true to allow leading zeroes.</param>
        /// <param name="index">The array index as an integer.</param>
        /// <returns>True, if the string is a valid array index, otherwise false.</returns>
        public static bool parseArrayIndex(string s, bool allowLeadingZeroes, out uint index) {
            index = 0;

            if (s == null || s.Length == 0)
                return false;

            if (!allowLeadingZeroes) {
                if (s[0] == '0')
                    // Even if leading zeroes are not allowed, a single zero is legal.
                    return s.Length == 1;

                if (s.Length > 10)
                    // Numbers >10 digits will definitely overflow
                    return false;
            }

            uint num = 0;

            if (s.Length >= 10) {
                // For numbers of at least 10 digits, check for overflows.
                const uint uintMaxDiv10 = UInt32.MaxValue / 10u;

                for (int i = 0; i < s.Length; i++) {
                    uint c = s[i];
                    if ((uint)(c - '0') > 9)
                        return false;
                    c -= '0';

                    if (num > uintMaxDiv10)
                        return false;
                    num *= 10;
                    if (num > UInt32.MaxValue - c)
                        return false;
                    num += c;
                }
            }
            else {
                for (int i = 0; i < s.Length; i++) {
                    uint c = s[i];
                    if ((uint)(c - '0') > 9)
                        return false;
                    num = c - 48 + num * 10;
                }
            }

            index = num;
            return true;
        }

    }

}
