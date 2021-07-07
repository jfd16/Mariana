using System;
using System.Diagnostics.CodeAnalysis;

namespace Mariana.Common {

    /// <summary>
    /// Functions for URI encoding and decoding.
    /// </summary>
    public static class URIUtil {

        /// <summary>
        /// Encodes a string for use as a URI. Any character other than a letter, number or a
        /// character in the whitelist will be percent-encoded as UTF-8.
        /// </summary>
        ///
        /// <param name="uri">The string to encode.</param>
        /// <param name="noEscapeChars">A string containing characters that are not letters or digits, that
        /// should not be escaped. If this is the empty string, all characters except letters and digits will be
        /// escaped. Only characters in the ASCII range (0-127) are checked against this string;
        /// characters outside this range are always escaped.</param>
        /// <param name="failOnInvalidSurrogate">If this is true, encoding fails if an invalid surrogate
        /// character is detected. Otherwise, invalid surrogates are replaced by '?' in the encoded
        /// string.</param>
        /// <param name="encodedURI">The encoded URI string.</param>
        ///
        /// <returns>True if the URI was encoded successfully, false if it is invalid.</returns>
        public static bool tryEncode(
            string uri, string noEscapeChars, bool failOnInvalidSurrogate, [NotNullWhen(true)] out string? encodedURI)
        {
            if (uri == null) {
                encodedURI = null;
                return false;
            }

            char[] buffer = new char[uri.Length];
            int bufPos = 0, bufLen = 0;

            bool error = false;

            for (int i = 0; i < uri.Length; i++) {
                char ch = uri[i];

                bool noEscape =
                    ((uint)(ch - '0') <= 9)
                    || ((uint)(ch - 'A') <= 25)
                    || ((uint)(ch - 'a') <= 25)
                    || (ch <= 0x7F && noEscapeChars.Contains(ch));

                if (bufPos == bufLen) {
                    DataStructureUtil.expandArray(ref buffer);
                    bufLen = buffer.Length;
                }

                if (noEscape) {
                    // No percent encoding
                    buffer[bufPos++] = ch;
                    continue;
                }

                uint bytes = 0;
                bool invalidSurrogate = false;

                if ((uint)(ch - 0xD800) < 0x400) {
                    // Check surrogate pairs
                    if (i == uri.Length - 1) {
                        invalidSurrogate = true;
                    }
                    else {
                        char trail = uri[i + 1];
                        if ((uint)(trail - 0xDC00) < 0x400) {
                            bytes = _getUTF8Bytes(ch, trail);
                            i++;
                        }
                        else {
                            invalidSurrogate = true;
                        }
                    }
                }
                else if ((uint)(ch - 0xDC00) < 0x400) {
                    invalidSurrogate = true;
                }
                else {
                    bytes = _getUTF8Bytes(ch);
                }

                if (invalidSurrogate) {
                    if (failOnInvalidSurrogate) {
                        error = true;
                        break;
                    }
                    buffer[bufPos++] = '?';
                    continue;
                }

                do {
                    // Write the percent encoding for each byte
                    if (bufLen - bufPos < 3) {
                        DataStructureUtil.expandArray(ref buffer, 3);
                        bufLen = buffer.Length;
                    }
                    buffer[bufPos] = '%';
                    byteToHex((byte)bytes, buffer.AsSpan(bufPos + 1));
                    bufPos += 3;
                    bytes >>= 8;
                } while (bytes != 0);
            }

            if (error) {
                encodedURI = null;
                return false;
            }

            encodedURI = new string(buffer, 0, bufPos);
            return true;
        }

        /// <summary>
        /// Decodes a URI-encoded string.
        /// </summary>
        ///
        /// <param name="uri">The URI to decode.</param>
        /// <param name="noDecodeChars">A string containing characters whose escape sequences must not be decoded.
        /// Only characters in the ASCII range (0-127) are checked against this string. If this is empty,
        /// all escape sequences are decoded.</param>
        /// <param name="failOnInvalidSurrogate">if this is true, decoding fails if the percent-encoded
        /// form of a surrogate character is detected. Otherwise, surrogates are decoded normally.</param>
        /// <param name="decodedURI">The decoded string.</param>
        ///
        /// <returns>True if <paramref name="uri"/> was decoded successfully, otherwise
        /// false.</returns>
        public static bool tryDecode(
            string uri, string noDecodeChars, bool failOnInvalidSurrogate, [NotNullWhen(true)] out string? decodedURI)
        {
            if (uri == null) {
                decodedURI = null;
                return false;
            }

            char[]? buffer = null;
            int bufPos = 0;

            ReadOnlySpan<char> uriSpan = uri;

            while (uriSpan.Length > 0) {
                int pcIndex = uriSpan.IndexOf('%');

                if (buffer == null) {
                    if (pcIndex == -1)
                        break;

                    buffer = new char[uri.Length];
                }

                var copyChars = (pcIndex == -1) ? uriSpan : uriSpan.Slice(0, pcIndex);
                copyChars.CopyTo(buffer.AsSpan(bufPos, copyChars.Length));
                bufPos += copyChars.Length;

                if (pcIndex == -1)
                    break;

                uriSpan = uriSpan.Slice(pcIndex);
                if ((uint)uriSpan.Length <= 2)
                    goto __error;

                if (!hexToByte(uriSpan.Slice(1), out byte hexDecodedByte)) {
                    // First escape sequence is invalid hexadecimal
                    goto __error;
                }

                if (hexDecodedByte <= 0x7F) {
                    // For escape sequences representing ASCII characters, check if they exist in the no-decode list
                    if (noDecodeChars.Contains((char)hexDecodedByte)) {
                        buffer[bufPos++] = '%';
                        uriSpan = uriSpan.Slice(1);
                    }
                    else {
                        buffer[bufPos++] = (char)hexDecodedByte;
                        uriSpan = uriSpan.Slice(3);
                    }
                    continue;
                }

                // Multi-byte characters
                int extraByteCount;

                if ((hexDecodedByte & 0xE0) == 0xC0)
                    extraByteCount = 1;
                else if ((hexDecodedByte & 0xF0) == 0xE0)
                    extraByteCount = 2;
                else if ((hexDecodedByte & 0xF8) == 0xF0)
                    extraByteCount = 3;
                else
                    goto __error;

                uriSpan = uriSpan.Slice(3);
                int charValue = (hexDecodedByte & ((1 << (6 - extraByteCount)) - 1)) << (extraByteCount * 6);

                for (int j = extraByteCount - 1; j >= 0; j--) {
                    if ((uint)uriSpan.Length <= 2
                        || uriSpan[0] != '%'
                        || !hexToByte(uriSpan.Slice(1), out hexDecodedByte)
                        || (hexDecodedByte & 192) != 128)
                    {
                        goto __error;
                    }

                    charValue |= (hexDecodedByte & 63) << (j * 6);
                    uriSpan = uriSpan.Slice(3);
                }

                if (extraByteCount == 3) {
                    // Four-byte characters in UTF-8 must be surrogated
                    if ((uint)(charValue - 0xFFFF) > 0x10FFFF - 0xFFFF)
                        goto __error;

                    buffer[bufPos] = (char)(((charValue - 0x10000) >> 10) | 0xD800);
                    buffer[bufPos + 1] = (char)((charValue & 0x3FF) | 0xDC00);
                    bufPos += 2;
                }
                else {
                    if (failOnInvalidSurrogate && (uint)(charValue - 0xD800) < 0x800
                        || charValue < ((extraByteCount == 2) ? 0x800 : 0x80))
                    {
                        goto __error;
                    }

                    buffer[bufPos++] = (char)charValue;
                }
            }

            decodedURI = (buffer == null) ? uri : new string(buffer, 0, bufPos);
            return true;

        __error:
            decodedURI = null;
            return false;
        }

        /// <summary>
        /// Reads a byte from a two-digit hexadecimal character sequence in a span.
        /// </summary>
        /// <param name="span">The span containing the hex sequence as the first two characters.</param>
        /// <param name="byteValue">The value of the byte represented by the hex sequence
        /// read.</param>
        /// <returns>True if a valid hexadecimal sequence was read, otherwise false.</returns>
        public static bool hexToByte(ReadOnlySpan<char> span, out byte byteValue) {
            byteValue = 0;

            if ((uint)span.Length <= 1)
                return false;

            char low = span[1], high = span[0];

            if ((uint)(low - '0') <= 9)
                byteValue = (byte)(low - '0');
            else if ((uint)(low - 'A') <= 5)
                byteValue = (byte)(low - ('A' - 10));
            else if ((uint)(low - 'a') <= 5)
                byteValue = (byte)(low - ('a' - 10));
            else
                return false;

            if ((uint)(high - '0') <= 9)
                byteValue |= (byte)((high - '0') << 4);
            else if ((uint)(high - 'A') <= 5)
                byteValue |= (byte)((high - ('A' - 10)) << 4);
            else if ((uint)(high - 'a') <= 5)
                byteValue |= (byte)((high - ('a' - 10)) << 4);
            else
                return false;

            return true;
        }

        /// <summary>
        /// Converts a byte to its hexadecimal representation and writes it to
        /// <paramref name="dest"/>.
        /// </summary>
        ///
        /// <param name="byteVal">The byte to convert to its hexadecimal representation.</param>
        /// <param name="dest">The span into which to write the hexadecimal representation of
        /// <paramref name="byteVal"/>.</param>
        ///
        /// <remarks>
        /// This method always writes two characters, even if the high 4 bits of the byte are zero.
        /// Digits greater than 9 are represented by uppercase letters (A to F).
        /// </remarks>
        public static void byteToHex(byte byteVal, Span<char> dest) {
            int low = byteVal & 15, high = byteVal >> 4;
            dest[1] = (char)((low < 10) ? low + 48 : low + 55);
            dest[0] = (char)((high < 10) ? high + 48 : high + 55);
        }

        /// <summary>
        /// Gets the UTF-8 bytes for a given character.
        /// </summary>
        /// <param name="c">The character whose UTF-8 representation to return.</param>
        /// <returns>The UTF-8 bytes of the character, as an unsigned integer. If there are less than
        /// four bytes in the UTF-8 representation of the character, extra bytes will be set to zero
        /// in the returned value.</returns>
        private static uint _getUTF8Bytes(char c) {
            if (c < 0x80)
                return c;
            if (c < 0x800)
                return (uint)((192 | c >> 6) | (128 | c & 63) << 8);
            return (uint)((224 | c >> 12) | (128 | (c >> 6) & 63) << 8 | (128 | c & 63) << 16);
        }

        /// <summary>
        /// Gets the UTF-8 bytes for a given surrogate pair.
        /// </summary>
        /// <param name="lead">The lead (high) surrogate of the pair whose UTF-8 representation to
        /// return.</param>
        /// <param name="trail">The trail (low) surrogate of the pair whose UTF-8 representation to
        /// return.</param>
        /// <returns>The UTF-8 bytes of the surrogate pair, as an unsigned integer.</returns>
        private static uint _getUTF8Bytes(char lead, char trail) {
            int code = 0x10000 + (lead - 0xD800) * 0x400 + (trail - 0xDC00);
            return (uint)(
                  (240 | code >> 18)
                | (128 | (code >> 12) & 63) << 8
                | (128 | (code >> 6) & 63) << 16
                | (128 | code & 63) << 24);
        }

    }
}
