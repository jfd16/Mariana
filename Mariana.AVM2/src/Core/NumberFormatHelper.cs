using System;
using System.Globalization;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    internal static class NumberFormatHelper {

        /// <summary>
        /// Number format strings for the ActionScript toFixed() function.
        /// </summary>
        private static readonly string[] s_toFixedFormatStrings = {
            "F0", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10",
            "F11", "F12", "F13", "F14", "F15", "F16", "F17", "F18", "F19", "F20", "F21",
        };

        /// <summary>
        /// Number format strings for the ActionScript toExponential() function.
        /// </summary>
        private static readonly string[] s_toExponentialFormatStrings = {
            "e0", "e1", "e2", "e3", "e4", "e5", "e6", "e7", "e8", "e9", "e10",
            "e11", "e12", "e13", "e14", "e15", "e16", "e17", "e18", "e19", "e20",
        };

        /// <summary>
        /// Temporary buffer used by string formatting methods.
        /// </summary>
        [ThreadStatic]
        private static char[] s_threadBuffer;

        /// <summary>
        /// Contains base-2 logarithms (floored) for integers [0, 36].
        /// </summary>
        private static readonly sbyte[] s_log2Table = new sbyte[37] {
            -1, 0, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3,
            4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
            5, 5, 5, 5, 5,
        };

        private static void _appendCharToBuffer(ref char[] buffer, ref int pos, char c) {
            char[] b = buffer;
            int p = pos;
            if ((uint)p < (uint)b.Length) {
                b[p] = c;
            }
            else {
                DataStructureUtil.resizeArray(ref buffer, pos, pos + 1, true);
                buffer[pos] = c;
            }
            pos++;
        }

        private static int _getDigitValue(char ch) {
            if ((uint)(ch - '0') <= 9)
                return ch - '0';
            else if ((uint)(ch - 'A') <= 'Z' - 'A')
                return ch - ('A' - 10);
            else if ((uint)(ch - 'a') <= 'z' - 'a')
                return ch - ('a' - 10);
            return -1;
        }

        private static char[] _getThreadStaticBuffer() {
            ref char[] buffer = ref s_threadBuffer;
            if (buffer == null)
                buffer = new char[64];

            return buffer;
        }

        /// <summary>
        /// Returns a string representation of the number in the given base.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <param name="radix">The base. This must be between 2 and 36.</param>
        /// <returns>The string representation of the number in the given base.</returns>
        /// <remarks>
        /// The most negative 64-bit integer (<see cref="Int64.MinValue"/>) cannot be used with this method.
        /// </remarks>
        public static string longToString(long num, int radix) {
            if (num == 0)
                return "0";

            bool neg = num < 0;
            if (neg)
                num = -num;

            Span<char> buffer = stackalloc char[65];
            int bufPos = buffer.Length - 1;

            if ((radix & (radix - 1)) == 0) {
                int bitShift = s_log2Table[radix];
                int mask = (1 << bitShift) - 1;
                while (num != 0) {
                    int digit = (int)num & mask;
                    buffer[bufPos--] = (char)((digit > 9) ? ('a' - 10) + digit : '0' + digit);
                    num >>= bitShift;
                }
            }
            else {
                while (num != 0) {
                    long num2 = num / radix;
                    int digit = (int)(num - num2 * radix);
                    buffer[bufPos--] = (char)((digit > 9) ? ('a' - 10) + digit : '0' + digit);
                    num = num2;
                }
            }

            if (neg)
                buffer[bufPos--] = '-';

            return new string(buffer.Slice(bufPos + 1));
        }

        /// <summary>
        /// Returns a string representation of the number in the given base.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <param name="radix">The base. This must be between 2 and 36.</param>
        /// <returns>The string representation of the number in the given base.</returns>
        public static string intToString(int num, int radix) {
            if (num == 0)
                return "0";

            bool neg = num < 0;
            if (neg) {
                if (num == Int32.MinValue)
                    // This is a special value that cannot be negated in 32 bits, so widen it to 64 bits
                    return longToString((long)num, radix);
                num = -num;
            }

            Span<char> buffer = stackalloc char[33];
            int bufPos = buffer.Length - 1;

            if ((radix & (radix - 1)) == 0) {
                // Optimized for power-of-two bases
                int bitShift = s_log2Table[radix];
                while (num != 0) {
                    int num2 = num >> bitShift;
                    num -= num2 << bitShift;
                    buffer[bufPos--] = (char)((num > 9) ? ('a' - 10) + num : '0' + num);
                    num = num2;
                }
            }
            else {
                // For non-power-of-two bases
                while (num != 0) {
                    int num2 = num / radix;
                    num -= num2 * radix;
                    buffer[bufPos--] = (char)((num > 9) ? ('a' - 10) + num : '0' + num);
                    num = num2;
                }
            }

            if (neg)
                buffer[bufPos--] = '-';

            return new string(buffer.Slice(bufPos + 1));
        }

        /// <summary>
        /// Returns a string representation of the number in the given base.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <param name="radix">The base. This must be between 2 and 36.</param>
        /// <returns>The string representation of the number in the given base.</returns>
        public static string uintToString(uint num, int radix) {
            if (num == 0)
                return "0";

            Span<char> buffer = stackalloc char[32];
            int bufPos = buffer.Length - 1;

            if ((radix & (radix - 1)) == 0) {
                // Optimized for power-of-two bases
                int bitShift = s_log2Table[radix];
                while (num != 0) {
                    uint num2 = num >> bitShift;
                    num -= num2 << bitShift;
                    buffer[bufPos--] = (char)((num > 9) ? ('a' - 10) + num : '0' + num);
                    num = num2;
                }
            }
            else {
                // For non-power-of-two bases
                while (num != 0) {
                    uint num2 = num / (uint)radix;
                    num -= num2 * (uint)radix;
                    buffer[bufPos--] = (char)((num > 9) ? ('a' - 10) + num : '0' + num);
                    num = num2;
                }
            }

            return new string(buffer.Slice(bufPos + 1));
        }

        /// <summary>
        /// Converts a floating-point number to a base-10 string. Fixed-point or scientific notation
        /// will be used as per ECMA-262 rules for Number.toString.
        /// </summary>
        /// <param name="num">The number to convert to a string.</param>
        /// <returns>The string representation of <paramref name="num"/>.</returns>
        public static string doubleToString(double num) {
            if (num == 0.0)
                return "0";
            if (Double.IsNaN(num))
                return "NaN";
            if (Double.IsInfinity(num))
                return Double.IsNegative(num) ? "-Infinity" : "Infinity";

            Span<char> buffer = stackalloc char[32];
            string format = Double.IsSubnormal(num) ? "g17" : "r";
            num.TryFormat(buffer, out int strLength, format, CultureInfo.InvariantCulture);

            double abs = Math.Abs(num);

            if (abs < 1E-6 || abs >= 1E+21) {
                // g17 and r will always output a scientific format string for numbers of this order
                // of magnitude. However, it outputs a leading zero for single-digit exponents,
                // which needs to be removed.
                int epos = (buffer[strLength - 4] == 'e') ? strLength - 4 : strLength - 5;

                Span<char> expSlice = buffer.Slice(epos);
                if (expSlice[2] == '0') {
                    expSlice[2] = expSlice[3];
                    strLength--;
                }

                return new string(buffer.Slice(0, strLength));
            }
            else {
                // If "r" generated scientific notation we need to convert it into fixed-point.
                // Since -6 <= exp <= 20 there is no possibility of a three-digit exponent.

                bool hasExponent = strLength >= 4 && buffer[strLength - 4] == 'e';
                if (!hasExponent)
                    return new string(buffer.Slice(0, strLength));

                ReadOnlySpan<char> expSlice = buffer.Slice(strLength - 4, 4);
                int expValue = (expSlice[2] - '0') * 10 + (expSlice[3] - '0');

                int nSign = (buffer[0] == '-') ? 1 : 0;
                int nDigits = strLength - 4 - nSign;

                // Remove the decimal point.
                if (buffer[nSign + 1] == '.') {
                    buffer[nSign + 1] = buffer[nSign];
                    if (nSign == 1)
                        buffer[1] = buffer[0];

                    buffer = buffer.Slice(1);
                    nDigits--;
                }

                if (expSlice[1] == '+') {
                    // Positive exponent. Add trailing zeros if needed.
                    int nRequiredDigits = expValue + 1;
                    if (nDigits < nRequiredDigits) {
                        buffer = buffer.Slice(0, nRequiredDigits + nSign);
                        buffer.Slice(nDigits + nSign).Fill('0');
                    }
                    return new string(buffer);
                }
                else {
                    // Negative exponent.
                    int nZerosAfterPoint = expValue - 1;
                    buffer.Slice(0, nDigits).CopyTo(buffer.Slice(nZerosAfterPoint + 2));
                    buffer[0] = '0';
                    buffer[1] = '.';
                    buffer.Slice(2, nZerosAfterPoint).Fill('0');

                    return new string(buffer.Slice(0, nDigits + nZerosAfterPoint + 2));
                }
            }
        }

        /// <summary>
        /// Returns a string representation of the given floating-point number in exponential
        /// (scientific) notation in base 10.
        /// </summary>
        /// <param name="num">The number for which to create the string representation.</param>
        /// <param name="precision">The number of decimal places in the mantissa of the scientific
        /// notation string. Must be between 0 and 20.</param>
        /// <returns>The string representaion of <paramref name="num"/> in scientific
        /// notation with the number of decimal places given by <paramref name="precision"/>.</returns>
        public static string doubleToStringExpNotation(double num, int precision) {
            if (Double.IsNaN(num))
                return "NaN";
            if (Double.IsInfinity(num))
                return Double.IsNegative(num) ? "-Infinity" : "Infinity";
            if (num == 0.0)
                num = 0.0;  // Don't expose negative zero

            Span<char> buffer = stackalloc char[32];
            num.TryFormat(buffer, out int bufferLength, s_toExponentialFormatStrings[precision], CultureInfo.InvariantCulture);

            // We always get a three digit exponent, so remove any leading zeros from it.
            Span<char> expDigits = buffer.Slice(bufferLength - 3);
            ref char ed0 = ref expDigits[0];
            ref char ed1 = ref expDigits[1];
            ref char ed2 = ref expDigits[2];

            if (ed0 == '0' && ed1 == '0') {
                ed0 = ed2;
                bufferLength -= 2;
            }
            else if (ed0 == '0') {
                (ed0, ed1) = (ed1, ed2);
                bufferLength--;
            }

            return new string(buffer.Slice(0, bufferLength));
        }

        /// <summary>
        /// Returns a string representation of the given floating-point number in base 10 with
        /// the given number of significant digits.
        /// </summary>
        /// <param name="num">The number for which to create the string representation.</param>
        /// <param name="precision">The number of significant digits. Must be between 1 and 21.</param>
        /// <returns>The string representaion of <paramref name="num"/> with the number
        /// of significant digits given by <paramref name="precision"/>. If the absolute value of
        /// <paramref name="num"/> is greater than or equal to 10^<paramref name="precision"/>
        /// then scientific notation is used, otherwise fixed-point notation is used.</returns>
        public static string doubleToStringPrecision(double num, int precision) {
            if (Double.IsNaN(num))
                return "NaN";
            if (Double.IsInfinity(num))
                return Double.IsNegative(num) ? "-Infinity" : "Infinity";
            if (num == 0.0)
                num = 0.0;  // Don't expose negative zero

            Span<char> buffer = stackalloc char[32];
            num.TryFormat(buffer, out int charsWritten, s_toExponentialFormatStrings[precision - 1], CultureInfo.InvariantCulture);

            Span<char> expDigits = buffer.Slice(charsWritten - 4, 4);
            int expAbsVal = (expDigits[1] - '0') * 100 + (expDigits[2] - '0') * 10 + expDigits[3] - '0';
            bool expNegative = expDigits[0] == '-';

            if (expAbsVal >= precision && !expNegative) {
                // Output scientific notation. We only need to strip leading 0's from the exponent.
                if (expAbsVal < 10) {
                    expDigits[1] = expDigits[3];
                    charsWritten -= 2;
                }
                else if (expAbsVal < 100) {
                    (expDigits[1], expDigits[2]) = (expDigits[2], expDigits[3]);
                    charsWritten--;
                }
                return new string(buffer.Slice(0, charsWritten));
            }

            // Convert the scientific notation into fixed-point.

            if (expAbsVal == 0) {
                // If the exponent is zero then simply return the mantissa (with the sign) as
                // the fixed point string.
                return new string(buffer.Slice(0, charsWritten - 5));
            }

            // Strip the decimal point and exponent from the buffer.
            bool isNegative = buffer[0] == '-';
            int numberStart, digitsStart;
            int numberEnd = charsWritten - 5;

            if (precision == 1) {
                // No decimal point
                (numberStart, digitsStart) = (0, isNegative ? 1 : 0);
            }
            else if (isNegative) {
                buffer[2] = buffer[1];
                buffer[1] = '-';
                (numberStart, digitsStart) = (1, 2);
            }
            else {
                buffer[1] = buffer[0];
                (numberStart, digitsStart) = (1, 1);
            }

            if (!expNegative) {
                if (expAbsVal == precision - 1)
                    // No decimal point to be inserted.
                    return new string(buffer.Slice(numberStart, numberEnd - numberStart));

                int dotPos = digitsStart + expAbsVal + 1;
                buffer.Slice(dotPos, numberEnd - dotPos).CopyTo(buffer.Slice(dotPos + 1));
                buffer[dotPos] = '.';
                return new string(buffer.Slice(numberStart, numberEnd - numberStart + 1));
            }
            else {
                int fractionDigits = precision + expAbsVal - 1;
                int fractionZeros = fractionDigits - precision;
                int stringLength = fractionDigits + 2 + (isNegative ? 1 : 0);
                ReadOnlySpan<char> digitsInBuffer = buffer.Slice(digitsStart, numberEnd - digitsStart);

                // If the buffer already allocated has sufficient space for the output, it can be reused.
                Span<char> outBuffer = (stringLength <= buffer.Length) ? buffer : stackalloc char[stringLength];

                Span<char> outBufferDigits = outBuffer;
                if (isNegative) {
                    // This is safe even when `outBuffer` is aliasing `buffer`, as the first
                    // character for a negative number is never a digit.
                    outBuffer[0] = '-';
                    outBufferDigits = outBuffer.Slice(1);
                }

                digitsInBuffer.CopyTo(outBufferDigits.Slice(fractionZeros + 2));
                outBufferDigits[0] = '0';
                outBufferDigits[1] = '.';
                outBufferDigits.Slice(2, fractionZeros).Fill('0');

                return new string(outBuffer.Slice(0, stringLength));
            }
        }

        /// <summary>
        /// Returns a string representation of the given floating-point number base-10 fixed
        /// point notation.
        /// </summary>
        /// <param name="num">The number for which to create the string representation.</param>
        /// <param name="precision">The number of decimal places. Must be between 0 and 21.</param>
        /// <returns>The string representaion of <paramref name="num"/> in fixed point
        /// notation with the number of decimal places given by <paramref name="precision"/>.</returns>
        public static string doubleToStringFixedNotation(double num, int precision) {
            if (Double.IsNaN(num))
                return "NaN";
            if (Double.IsInfinity(num))
                return Double.IsNegative(num) ? "-Infinity" : "Infinity";
            if (num == 0.0)
                num = 0.0;  // Don't expose negative zero

            return num.ToString(s_toFixedFormatStrings[precision], CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts a floating-point number to a string in a power-of-two radix.
        /// </summary>
        /// <param name="num">The number to convert to a string..</param>
        /// <param name="radix">The radix in which the string is to be interpreted. This
        /// must be one of: 2, 4, 8, 16 or 32.</param>
        /// <returns>The string representation of <paramref name="num"/> in the given radix.</returns>
        public static string doubleToStringPow2Radix(double num, int radix) {
            if (num == 0.0)
                return "0";
            if (Double.IsNaN(num))
                return "NaN";
            if (Double.IsInfinity(num))
                return Double.IsNegative(num) ? "-Infinity" : "Infinity";

            int radixBits = s_log2Table[radix];

            long bits = BitConverter.DoubleToInt64Bits(num);
            int exponent = ((int)(bits >> 52) & 0x7FF) - 1023;

            ulong mantissa = (ulong)(bits & 0xFFFFFFFFFFFFFL);
            if (exponent == -1023)
                exponent = -1022;
            else
                mantissa |= 1uL << 52;

            int exponentAlign = (exponent + radixBits * 1024) % radixBits;
            mantissa <<= 12 - radixBits + exponentAlign;
            exponent -= exponentAlign;

            char[] buffer = _getThreadStaticBuffer();
            int bufPos = 0;

            if (num < 0.0)
                _appendCharToBuffer(ref buffer, ref bufPos, '-');

            if (exponent < 0) {
                int nZerosAfterPoint = (-exponent / radixBits) - 1;
                _appendCharToBuffer(ref buffer, ref bufPos, '0');
                _appendCharToBuffer(ref buffer, ref bufPos, '.');
                for (int i = 0; i < nZerosAfterPoint; i++)
                    _appendCharToBuffer(ref buffer, ref bufPos, '0');
            }

            int digitShift = 64 - radixBits;
            int digitMask = (1 << radixBits) - 1;

            while (mantissa != 0) {
                int digit = (int)(mantissa >> digitShift) & digitMask;
                char digitChar = (char)((digit <= 9) ? '0' + digit : ('a' - 10) + digit);
                _appendCharToBuffer(ref buffer, ref bufPos, digitChar);

                mantissa <<= radixBits;
                if (exponent == 0 && mantissa != 0)
                    _appendCharToBuffer(ref buffer, ref bufPos, '.');
                exponent -= radixBits;
            }
            while (exponent >= 0) {
                _appendCharToBuffer(ref buffer, ref bufPos, '0');
                exponent -= radixBits;
            }

            return new string(buffer, 0, bufPos);
        }

        /// <summary>
        /// Returns a string representation of an integral floating-point number in the given base.
        /// </summary>
        /// <param name="num">The number from which to create the string reprsentation.</param>
        /// <param name="radix">The base. This must be between 2 and 36.</param>
        /// <returns>The string representation of the number in the given base.</returns>
        public static string doubleIntegerToStringRadix(double num, int radix) {
            if (Double.IsNaN(num))
                return "NaN";
            if (Double.IsInfinity(num))
                return Double.IsNegative(num) ? "-Infinity" : "Infinity";

            num = Math.Truncate(num);

            long num_l = (long)num;
            if ((double)num_l == num && num_l != Int64.MinValue) {
                // Fast path for numbers representable as long integers (except the minimum value,
                // which does not work with longToString)
                return longToString(num_l, radix);
            }

            if ((radix & (radix - 1)) == 0) {
                // Fast path for power-of-two radix
                return doubleToStringPow2Radix(num, radix);
            }

            bool isNegative = num < 0.0;
            num = Math.Abs(num);

            char[] buffer = _getThreadStaticBuffer();
            int bufPos = 0;

            // We are using a big-endian bigint here because we divide by the radix to get
            // each digit, and the division procedure being used iterates from the most
            // significant to the least significant element. Big-endian allows the division
            // to be implemented with a forward loop over the buffer, which (unlike a
            // reverse loop) allows the JIT to eliminate bounds checks.

            // We're doing a quadratic-time operation here, but since conversions to bases other
            // than 10 or powers of two (for which this method is never called) are rare, this is
            // not worth optimizing further.

            Span<uint> bigint = stackalloc uint[32];
            loadBigInt(bigint, num, out int curIndex);
            while (curIndex < bigint.Length) {
                int next = nextDigit(bigint, ref curIndex, radix);
                char digitChar = (char)((next > 9) ? ('a' - 10) + next : '0' + next);
                _appendCharToBuffer(ref buffer, ref bufPos, digitChar);
            }

            if (isNegative)
                _appendCharToBuffer(ref buffer, ref bufPos, '-');

            Span<char> bufferSpan = buffer.AsSpan(0, bufPos);
            bufferSpan.Reverse();
            return new string(bufferSpan);

            void loadBigInt(Span<uint> sp, double v, out int startIndex) {
                long bits = BitConverter.DoubleToInt64Bits(v);
                int exponent = ((int)(bits >> 52) & 0x7FF) - 1023;
                long mantissa = (bits & 0xFFFFFFFFFFFFF) | 0x10000000000000;

                startIndex = 31 - (exponent >> 5);

                int highBits = (exponent & 31) + 1;
                int midBits = Math.Min(53 - highBits, 32);
                int lowBits = 53 - (highBits + midBits);

                // We don't have to handle the case where the mantissa fits only in the
                // least significant element (index 31), as in that case the number is
                // representable as a long integer and that would have been handled by longToString.

                sp[startIndex] = (uint)(mantissa >> (53 - highBits));
                sp[startIndex + 1] = (uint)(mantissa >> lowBits) << (32 - midBits);
                if (startIndex != 30 && lowBits != 0)
                    sp[startIndex + 2] = (uint)mantissa << (32 - lowBits);
            }

            int nextDigit(Span<uint> sp, ref int start, int rdx) {
                // Gets the remainder from dividing the number in the buffer by the radix,
                // and updates the buffer with the quotient. Simple grammar-school division.

                ulong remainder = 0;

                for (int i = 0; i < sp.Length; i++) {
                    remainder = remainder << 32 | sp[i];
                    if (remainder < (ulong)rdx) {
                        sp[i] = 0;
                    }
                    else {
                        uint quot = (uint)(remainder / (ulong)rdx);
                        sp[i] = quot;
                        remainder -= (ulong)rdx * quot;
                    }
                }

                while (start < sp.Length && sp[start] == 0)
                    start++;

                return (int)remainder;
            }
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

                if (ch < 0x2000 || (ch > 0x200B && ch != 0x2028 && ch != 0x2029 && ch != 0x205F && ch != 0x3000))
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
            if (span.IsEmpty)
                return false;

            double num;
            bool isNeg = false, isHex = false;

            if (span[0] == '-') {
                isNeg = true;
                span = span.Slice(1);
            }
            else if (span[0] == '+') {
                span = span.Slice(1);
            }

            if ((uint)span.Length >= 2 && span[0] == '0' && (span[1] == 'x' || span[1] == 'X')) {
                // 0x prefix - hexadecimal
                if (!allowHex)
                    return false;
                isHex = true;
                span = span.Slice(2);
            }

            if (span.IsEmpty)
                return false;

            int charsConsumed;

            if (isHex) {
                num = stringToDoubleIntPow2Radix(span, 16, out charsConsumed);
            }
            else if ((uint)span.Length >= 8 && span[0] == 'I'
                && span.Slice(0, 8).Equals("Infinity", StringComparison.Ordinal))
            {
                num = Double.PositiveInfinity;
                charsConsumed = 8;
            }
            else {
                charsConsumed = getPrefixLength(span);
                if (charsConsumed != 0) {
                    var style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
                    num = Double.Parse(span.Slice(0, charsConsumed), style, CultureInfo.InvariantCulture);
                }
                else {
                    num = 0;
                }
            }

            if (charsConsumed == 0) // Nothing was read.
                return false;

            span = span.Slice(charsConsumed);

            if (strict) {
                if (indexOfFirstNonSpace(span) != span.Length)
                    return false;
                span = default;
            }

            charsRead = originalSpan.Length - span.Length;
            value = isNeg ? -num : num;
            return true;

            int getPrefixLength(ReadOnlySpan<char> sp) {
                int dotpos = Int32.MinValue, epos = Int32.MinValue, esignpos = Int32.MinValue;
                int i;

                for (i = 0; i < sp.Length; i++) {
                    char ch = sp[i];
                    if ((uint)(ch - '0') <= 9)
                        continue;

                    if (ch == '.' && dotpos == Int32.MinValue && epos == Int32.MinValue)
                        dotpos = i;
                    else if ((ch == 'e' || ch == 'E') && epos == Int32.MinValue)
                        epos = i;
                    else if (i == epos + 1 && (ch == '+' || ch == '-'))
                        esignpos = i;
                    else
                        break;
                }

                if (dotpos == 0 && (i == 1 || epos == 1)) {
                    // Don't consume "." or anything beginning with ".e"
                    return 0;
                }

                if (epos == 0 || i == epos + 1 || i == esignpos + 1) {
                    // Don't consume "e", "e+" or "e-" at the beginning or end of the string.
                    return epos;
                }

                return i;
            }
        }

        /// <summary>
        /// Parses a string representing an integer in a power-of-2 radix to a floating-point
        /// number. Parsing stops at the first character in the span that is not a valid digit
        /// for the radix, or when the end of the span is reached. The string cannot contain
        /// a positive or negative sign.
        /// </summary>
        /// <param name="span">The span containing the string to be parsed.</param>
        /// <param name="radix">The radix in which the string is to be interpreted. This must be
        /// one of: 2, 4, 8, 16 or 32.</param>
        /// <param name="charsRead">The number of digits read from <paramref name="span"/>.</param>
        /// <returns>The parsed value, or zero if there are no valid digits.</returns>
        public static double stringToDoubleIntPow2Radix(ReadOnlySpan<char> span, int radix, out int charsRead) {
            int nRadixBits = s_log2Table[radix];
            long mantissa = 0;
            const long MASK_54_BITS = (1L << 54) - 1;

            int i = 0;
            int nMantissaBits = 0, nExcessBits = 0;
            bool isExcessNonZero = false;

            // Skip leading zeroes
            while (true) {
                if ((uint)i >= (uint)span.Length || span[i] != '0')
                    break;
                i++;
            }

            while (true) {
                if ((uint)i >= (uint)span.Length)
                    break;

                int digit = _getDigitValue(span[i]);
                if ((uint)digit >= radix)
                    break;

                if ((mantissa & ~MASK_54_BITS) == 0) {
                    mantissa = (mantissa << nRadixBits) | (long)digit;
                    nMantissaBits += nRadixBits;
                }
                else {
                    nExcessBits += nRadixBits;
                    isExcessNonZero |= digit != 0;
                }

                i++;
            }

            charsRead = i;
            if (mantissa == 0)
                return 0;

            long mantissaBitMask = 1L << (nMantissaBits - 1);
            while ((mantissa & mantissaBitMask) == 0) {
                mantissaBitMask >>= 1;
                nMantissaBits--;
            }

            int exponent;

            if (nMantissaBits <= 53) {
                mantissa <<= 53 - nMantissaBits;
                exponent = nMantissaBits - 1;
            }
            else {
                int roundMask = (1 << (nMantissaBits - 53)) - 1;
                int roundBits = (int)mantissa & roundMask;

                mantissa >>= nMantissaBits - 53;
                exponent = nMantissaBits + nExcessBits - 1;

                bool hasNonZeroAfterFirstRoundBit = isExcessNonZero || (roundBits & (roundMask >> 1)) != 0;
                int firstRoundBitMask = (roundMask >> 1) + 1;

                if ((roundBits & firstRoundBitMask) != 0 && (hasNonZeroAfterFirstRoundBit || (mantissa & 1) != 0)) {
                    mantissa++;
                    if ((mantissa & (1L << 53)) != 0) {
                        exponent++;
                        mantissa >>= 1;
                    }
                }
            }

            if (exponent > 1023)
                return Double.PositiveInfinity;

            return BitConverter.Int64BitsToDouble((long)(exponent + 1023) << 52 | (mantissa & ~(1L << 52)));
        }

        /// <summary>
        /// Parses a string representing an integer in the given radix to a floating-point number having
        /// that integer value. Parsing stops at the first character in the span that is not a valid digit,
        /// or when the end of the span is reached. The string cannot contain a positive or negative sign.
        /// </summary>
        /// <param name="span">The span containing the string to parse.</param>
        /// <param name="radix">The radix. Must be between 2 and 36.</param>
        /// <param name="charsRead">The number of characters read from <paramref name="span"/>.</param>
        /// <returns>The parsed integer value as a double, or 0 if the span does not contain valid
        /// digits.</returns>
        public static double stringToDoubleIntRadix(ReadOnlySpan<char> span, int radix, out int charsRead) {
            if ((radix & (radix - 1)) == 0) {
                // Fast path for power-of-two radix
                return stringToDoubleIntPow2Radix(span, radix, out charsRead);
            }

            int i = 0;

            // Check if the number fits into a long integer.
            // If that is the case, convert the long to a double and return it.
            long longValue = 0;
            long maxLongDivRadix = (radix == 10) ? Int64.MaxValue / 10 : Int64.MaxValue / radix;
            bool fitsInLong = true;

            while (true) {
                if ((uint)i >= (uint)span.Length)
                    break;

                int digit = _getDigitValue(span[i]);
                if ((uint)digit >= radix)
                    break;

                if (longValue > maxLongDivRadix) {
                    fitsInLong = false;
                    break;
                }
                long newLongVal = longValue * radix + digit;
                if (newLongVal < 0) {
                    fitsInLong = false;
                    break;
                }
                longValue = newLongVal;
                i++;
            }

            if (fitsInLong) {
                charsRead = i;
                return (double)longValue;
            }

            // The number does not fit into a long integer and we need to use bignum arithmetic.
            // If the base is 10, we use Double.Parse as that seems to be more optimized.
            if (radix == 10) {
                while (true) {
                    if ((uint)i >= (uint)span.Length || (uint)(span[i] - '0') > 9)
                        break;
                    i++;
                }
                charsRead = i;
                return Double.Parse(span.Slice(0, charsRead));
            }

            // Unlike doubleIntegerStringRadix, we use a little-endian bigint here because we are doing
            // multiplication (instead of division), which starts from the least significant bit.

            Span<uint> bigint = stackalloc uint[32];
            bigint[0] = (uint)longValue;
            bigint[1] = (uint)(longValue >> 32);
            int curSize = 2;
            bool overflow = false;

            while (true) {
                if ((uint)i >= (uint)span.Length)
                    break;

                int digit = _getDigitValue(span[i]);
                if ((uint)digit >= radix)
                    break;

                // We don't exit the loop early if an overflow is detected so that any
                // additional numeric characters are consumed (even though the result will
                // be infinity)

                if (!overflow)
                    overflow = !pushDigit(bigint, ref curSize, radix, digit);

                i++;
            }

            charsRead = i;
            if (overflow)
                return Double.PositiveInfinity;

            int exponent = getExponent(bigint, curSize);
            long mantissa = getMantissa(bigint, curSize, ref exponent);

            // The exponent could have overflowed from the rounding up of the mantissa,
            // so we need to check that.
            if (exponent > 1023)
                return Double.PositiveInfinity;

            return BitConverter.Int64BitsToDouble((long)(exponent + 1023) << 52 | mantissa);

            bool pushDigit(Span<uint> sp, ref int size, int rdx, int dg) {
                // sp <= (sp * rdx) + dg. Returns false on overflow.
                uint carry = (uint)dg;
                Span<uint> sp2 = sp.Slice(0, size);

                for (int j = 0; j < sp2.Length; j++) {
                    ulong prod = (ulong)sp2[j] * (uint)rdx + carry;
                    sp2[j] = (uint)prod;
                    carry = (uint)(prod >> 32);
                }

                if (carry == 0)
                    return true;

                if (size >= sp.Length)
                    return false;

                sp[size++] = carry;
                return true;
            }

            int getExponent(Span<uint> sp, int size) {
                uint msb = sp[size - 1];
                int msBitIndex = 31;
                while ((msb & (1 << msBitIndex)) == 0)
                    msBitIndex--;
                return ((size - 1) << 5) | msBitIndex;
            }

            long getMantissa(Span<uint> sp, int size, ref int exp) {
                int highBits = (exp & 31) + 1;
                int midBits = Math.Min(53 - highBits, 32);
                int lowBits = 53 - (highBits + midBits);

                long mantissaBits = (long)(ulong)sp[size - 1] << (53 - highBits);
                if (size >= 2)
                    mantissaBits |= (long)(ulong)(sp[size - 2] >> (32 - midBits)) << lowBits;
                if (size >= 3 && lowBits > 0)
                    mantissaBits |= (long)(ulong)(sp[size - 3] >> (32 - lowBits));

                // We round up the mantissa if:
                // - There are more than 53 bits, and
                // - The rounding bit (54th from MSB) is set, and
                // - Either the mantissa is odd (i.e. its LSB is set), or there are additional
                //   bits after the rounding bit and at least one of them is set.
                // Since numbers fitting into a long integer were handled by longToString it is
                // guaranteed that we will have more than 54 bits here, no need to check for that.
                int bitCount = exp + 1;
                bool roundup = isBitSet(sp, bitCount - 54)
                    && ((mantissaBits & 1) != 0 || !areLowBitsZero(sp, bitCount - 54));

                if (roundup) {
                    mantissaBits++;
                    if ((mantissaBits & (1L << 53)) != 0) {
                        mantissaBits >>= 1;
                        exp++;
                    }
                }

                return mantissaBits & 0xFFFFFFFFFFFFFL;
            }

            bool isBitSet(Span<uint> sp, int bitIndex) => (sp[bitIndex >> 5] & (1 << (bitIndex & 31))) != 0;

            bool areLowBitsZero(Span<uint> sp, int count) {
                int j;
                for (j = 0; count >= 32; j++, count -= 32) {
                    if (sp[j] != 0)
                        return false;
                }
                return (sp[j] & ((1u << count) - 1)) == 0;
            }
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
        /// If the span begins with 0x or 0X, it is interpreted as a hexadecimal integer.
        /// </remarks>
        public static bool stringToInt(
            ReadOnlySpan<char> span, out int value, out int charsRead, bool strict = true, bool allowHex = true)
        {
            var originalSpan = span;

            value = 0;
            charsRead = 0;

            span = span.Slice(indexOfFirstNonSpace(span));
            if (span.IsEmpty)
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

            if ((uint)span.Length >= 2 && span[0] == '0' && (span[1] == 'x' || span[1] == 'X')) {
                if (!allowHex)
                    return false;
                hex = true;
                span = span.Slice(2);
            }

            if (span.IsEmpty)
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
                    else if ((uint)(c - 'a') <= 5)
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
        /// If the span begins with 0x or 0X, it is interpreted as a hexadecimal integer.
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
