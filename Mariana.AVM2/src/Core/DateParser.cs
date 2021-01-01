using System;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Date string parser, used by the <see cref="ASDate"/> class.
    /// </summary>
    internal struct DateParser {

        /// <summary>
        /// The possible types of tokens collected from a date string by the parser. (All tokens are
        /// case insensitive)
        /// </summary>
        private enum TokenType : byte {
            // Regular expression representations of the tokens are given for understanding, but DateParser
            // does not actually use regex.

            NONE,                 // Unused value
            NUMBER,               // [0-9]+
            NUMGROUP_SLASH2,      // [0-9]+\/[0-9]+
            NUMGROUP_SLASH3,      // [0-9]+\/[0-9]+\/[0-9]+
            NUMGROUP_DASH2,       // [0-9]+\-[0-9]+
            NUMGROUP_DASH3,       // [0-9]+\-[0-9]+\-[0-9]+
            NUMGROUP_DOT2,        // [0-9]+\.[0-9]+
            NUMGROUP_DOT3,        // [0-9]+\.[0-9]+\.[0-9]+
            NUMGROUP_DOT4,        // [0-9]+\.[0-9]+\.[0-9]+\.[0-9]+
            NUMGROUP_COLONDOT2,   // [0-9]+[\.:][0-9]+
            NUMGROUP_COLONDOT3,   // [0-9]+[\.:][0-9]+[\.:][0-9]+
            NUMGROUP_COLONDOT4,   // [0-9]+[\.:][0-9]+[\.:][0-9]+[\.:][0-9]+
            WORD,                 // [a-zA-Z](\.?[a-zA-Z])*

            UTC,                  // UTC|GMT|Z
            EXPECT_TIME,          // T
            MONTH,                // (Short or full month name)
            WEEKDAY,              // (Short or full weekday name)
            AM,                   // AM
            PM,                   // PM
            INVALID_WORD,         // (Any other word token)
        }

        /// <summary>
        /// Used during token processing to keep track of which date components have already been
        /// assigned. This is used to detect duplicate assignments, and to disambiguate tokens whose
        /// meaning is dependent on whether some other component has already been assigned or not.
        /// </summary>
        [Flags]
        private enum SetFlags {
            YEAR = 0x1,
            MONTH = 0x2,
            DAY = 0x4,
            HOUR = 0x8,
            MINUTE = 0x10,
            SECOND = 0x20,
            MILLI = 0x40,
            AM = 0x80,
            PM = 0x100,
            WEEKDAY = 0x200,
            UTC = 0x400,
            TIMEZONE = 0x800,
        }

        /// <summary>
        /// A PackedInt stores an integer parsed from a date string along with its sign and number of
        /// digits.
        /// </summary>
        ///
        /// <remarks>
        /// A PackedInt represents an integer by a magnitude, a sign and a digit count. The sign can
        /// be -1, 0 or 1. A sign of 0 indicates that the number parsed did not have an explicit sign
        /// (and is implicitly positive). The digit count is the number of digits in the parsed
        /// number, including leading zeroes, and can be a value from 0 to 7 (7 indicates that there
        /// are 7 or more digits). A PackedInt can be compressed into a 32-bit integer, and this
        /// compressed (packed) form is used for storing numeric token data in
        /// <see cref="m_tokenData"/>.
        /// </remarks>
        private readonly struct PackedInt {
            /// <summary>
            /// The maximum possible magnitude of a PackedInt.
            /// </summary>
            public const int MAX_MAGNITUDE = 0x7FFFFFF;

            public readonly int value;
            public readonly short sign;
            public readonly short nDigits;

            public PackedInt(int sign, int value, int nDigits) {
                this.value = value;
                this.sign = (short)sign;
                this.nDigits = (short)((nDigits > 7) ? 7 : nDigits);
            }

            /// <summary>
            /// Creates a new PackedInt by decoding its packed form.
            /// </summary>
            /// <param name="bits">The packed form of the PackedInt, obtained by calling the
            /// <see cref="encode"/> method.</param>
            public PackedInt(int bits) {
                sign = (short)(bits >> 30);
                nDigits = (short)(bits >> 27 & 7);
                value = bits & 0x7FFFFFF;
            }

            /// <summary>
            /// Returns the signed value of the PackedInt.
            /// </summary>
            public int signedValue => (sign == -1) ? -value : value;

            /// <summary>
            /// Encodes the PackedInt to packed form.
            /// </summary>
            /// <returns>The packed form of the PackedInt.</returns>
            public int encode() {
                int enc = 0;

                if (sign == -1)
                    enc |= unchecked((int)0xC0000000u);
                else if (sign == 1)
                    enc |= unchecked((int)0x40000000u);

                enc |= (nDigits << 27) | value;
                return enc;
            }
        }

        /// <summary>
        /// The full month names. All strings must be lowercase here, since words are normalized to
        /// lowercase before comparing against this list.
        /// </summary>
        private static readonly string[] s_fullMonthNames = {
            "january", "february", "march", "april", "may", "june",
            "july", "august", "september", "october", "november", "december",
        };

        /// <summary>
        /// The short month names. All strings must be lowercase here, since words are normalized to
        /// lowercase before comparing against this list.
        /// </summary>
        private static readonly string[] s_shortMonthNames = {
            "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"
        };

        /// <summary>
        /// The full weekday names. All strings must be lowercase here, since words are normalized to
        /// lowercase before comparing against this list.
        /// </summary>
        private static readonly string[] s_fullWeekdayNames = {
            "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday",
        };

        /// <summary>
        /// The short weekday names. All strings must be lowercase here, since words are normalized to
        /// lowercase before comparing against this list.
        /// </summary>
        private static readonly string[] s_shortWeekdayNames = {
            "sun", "mon", "tue", "wed", "thu", "fri", "sat",
        };

        /// <summary>
        /// The <see cref="DateParser"/> instance for the current thread.
        /// </summary>
        [ThreadStatic]
        private static DateParser s_threadInstance;

        /// <summary>
        /// The source string being parsed.
        /// </summary>
        private string m_str;

        /// <summary>
        /// The current read position in the source string.
        /// </summary>
        private int m_strpos;

        /// <summary>
        /// The type of the current token being parsed.
        /// </summary>
        private TokenType m_curTokenType;

        /// <summary>
        /// A buffer used to hold the data of the current token while it is being parsed. When a token
        /// ends, the contents of this buffer are parsed and cleared.
        /// </summary>
        private char[] m_buffer;

        /// <summary>
        /// The number of characters written to the parse buffer for the current token.
        /// </summary>
        private int m_bufferpos;

        /// <summary>
        /// A list containing the types of the tokens which have been fully parsed.
        /// </summary>
        private DynamicArray<TokenType> m_tokenTypes;

        /// <summary>
        /// Contains any additional (e.g. numeric) data that may be needed by certain tokens.
        /// </summary>
        ///
        /// <remarks>
        /// The number of elements in this array needed by a token depends on its type. Data is
        /// written to this array in the same order in which the tokens are written to
        /// <see cref="m_tokenTypes"/> so that it can be read during token processing.
        /// </remarks>
        private DynamicArray<int> m_tokenData;

        /// <summary>
        /// During token processing, this is the index in <see cref="m_tokenData"/> of the next
        /// value that is to be read.
        /// </summary>
        private int m_tokenDataReadPtr;

        /// <summary>
        /// Indicates whether the current date being parsed is valid or not.
        /// </summary>
        private bool m_isValid;

        /// <summary>
        /// These flags indicate which date components have been assigned during token processing.
        /// </summary>
        private SetFlags m_setFlags;

        /// <summary>
        /// During token processing, this is set if a 'T' token is encountered, to indicate that only
        /// time and no date components must follow.
        /// </summary>
        private bool m_timeExpected;

        // The date components read during token processing.
        private int m_year;
        private int m_month;
        private int m_day;
        private int m_hour;
        private int m_minute;
        private int m_second;
        private int m_millisec;
        private int m_timeZoneOffset;

        /// <summary>
        /// Parses a date string and returns a timestsamp for the date using the convention followed
        /// by the <see cref="ASDate"/> class to represent dates internally.
        /// </summary>
        /// <param name="str">The date string to parse.</param>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns>True if <paramref name="str"/> represents a valid date, false
        /// otherwise.</returns>
        ///
        /// <remarks>
        /// The timestamp returned is not the same as the ECMA/ActionScript date value that is
        /// returned by methods such as <see cref="ASDate.parse" qualifyHint="true"/> or
        /// <see cref="ASDate.getTime" qualifyHint="true"/>. (A constant bias is added to it to
        /// ensure that all timestamps of representable dates are nonnegative). See remarks on
        /// <see cref="ASDate"/> for further information.
        /// </remarks>
        internal static bool tryParse(string str, out long timestamp) {
            return s_threadInstance._parse(str, out timestamp);
        }

        /// <summary>
        /// Parses a date string and returns a timestsamp for the date using the convention followed
        /// by the <see cref="ASDate"/> class to represent dates internally.
        /// </summary>
        /// <param name="str">The date string to parse.</param>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns>True if <paramref name="str"/> represents a valid date, false
        /// otherwise.</returns>
        private bool _parse(string str, out long timestamp) {
            timestamp = 0;

            if (str == null || str.Length == 0)
                return false;

            _resetState();
            _tokenizeString(str);

            if (!m_isValid)
                return false;

            timestamp = _processTokensAndGetTimestamp();
            if (!m_isValid)
                return false;

            return true;
        }

        /// <summary>
        /// Resets the state of this <see cref="DateParser"/> instance. Call this before parsing a
        /// new string.
        /// </summary>
        private void _resetState() {

            if (m_buffer == null)
                m_buffer = new char[20];

            m_bufferpos = 0;
            m_curTokenType = TokenType.NONE;
            m_isValid = true;
            m_year = 0;
            m_month = 0;
            m_day = 0;
            m_hour = 0;
            m_minute = 0;
            m_second = 0;
            m_millisec = 0;
            m_timeZoneOffset = 0;
            m_setFlags = 0;
            m_timeExpected = false;
            m_tokenDataReadPtr = 0;

            m_tokenTypes.clear();
            m_tokenData.clear();

        }

        /// <summary>
        /// Parses a date string, creating tokens that must be processed by the
        /// <see cref="_processTokensAndGetTimestamp"/> method to return a timestamp.
        /// </summary>
        /// <param name="str">The date string to parse.</param>
        private void _tokenizeString(string str) {

            m_str = str;
            m_strpos = 0;

            _goToNextNonSpace();

            int len = str.Length;

            while (m_strpos < len && m_isValid) {

                char c = str[m_strpos];

                if (c == ' ' || (c <= 13 && c >= 9)) {
                    _parseWhiteSpace();
                    if (m_strpos >= len)
                        break;
                    c = str[m_strpos];
                }

                if (c == ',') {
                    // A comma is always a token separator, independent of context.
                    _writeToken();
                    m_strpos++;
                    continue;
                }

                if (c == '(') {
                    // Anything in parentheses (not nestable) is a "comment" and is ignored during parsing.
                    // This is done to ensure that strings returned by some implementations of the Date.toString
                    // method, which include the local time zone name in parentheses, are parsed correctly.
                    int closeParenIndex = str.IndexOf(')', m_strpos);
                    if (closeParenIndex == -1)
                        break;
                    m_strpos = closeParenIndex + 1;
                    continue;
                }

                if (c > 0x7A) {
                    // Since DateParser is designed to parse English date strings only, foreign characters
                    // can be immediately ruled out.
                    m_isValid = false;
                    return;
                }

                if (m_curTokenType == TokenType.NONE) {
                    // Initiate a new token.
                    if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) {
                        m_curTokenType = TokenType.WORD;
                    }
                    else if ((c >= '0' && c <= '9') || c == '+' || c == '-') {
                        m_curTokenType = TokenType.NUMBER;
                    }
                    else {
                        m_isValid = false;
                        return;
                    }
                }

                switch (m_curTokenType) {
                    case TokenType.WORD:
                        _continueWordToken();
                        break;

                    case TokenType.NUMBER:
                        _continueNumberToken();
                        break;

                    case TokenType.NUMGROUP_SLASH2:
                    case TokenType.NUMGROUP_DASH2:
                    case TokenType.NUMGROUP_DOT2:
                    case TokenType.NUMGROUP_COLONDOT2:
                        _continueNumGroup2Token();
                        break;

                    case TokenType.NUMGROUP_SLASH3:
                    case TokenType.NUMGROUP_DASH3:
                    case TokenType.NUMGROUP_DOT3:
                    case TokenType.NUMGROUP_COLONDOT3:
                        _continueNumGroup3Token();
                        break;

                    case TokenType.NUMGROUP_COLONDOT4:
                    case TokenType.NUMGROUP_DOT4:
                        _continueNumGroup4Token();
                        break;
                }

            }

            // Write the last token.
            _writeToken();

        }

        private void _parseWhiteSpace() {
            _goToNextNonSpace();
            if (m_strpos >= m_str.Length)
                return;

            char c = m_str[m_strpos];
            char lastBufferChar = _getLastCharFromBuffer();

            if (((c >= '0' && c <= '9') || (c >= 'A' && c <= 'z') || (c >= 'a' && c <= 'z'))
                && lastBufferChar != '.')
            {
                // If there is a letter or number after a space, it begins a new token.
                // But not if it is after a separator.
                _writeToken();
            }
            else if (c == '-' && m_strpos != m_str.Length - 1 && lastBufferChar != '.') {
                // If there is a dash following whitespace and that dash is followed
                // by a digit, but not after a separator, it also begins a new token.
                char next = m_str[m_strpos + 1];
                if (next >= '0' && next <= '9')
                    _writeToken();
            }
        }

        /// <summary>
        /// Moves the internal parser pointer to the next character in the string that is not a
        /// whitespace character, if the character at the current position is a whitespace character.
        /// </summary>
        private void _goToNextNonSpace() {
            int pos = m_strpos, len = m_str.Length;
            while (pos < len) {
                char c = m_str[pos];
                if (c != ' ' && (c > 13 || c < 9)) {
                    m_strpos = pos;
                    return;
                }
                pos++;
            }
            m_strpos = pos;
        }

        /// <summary>
        /// Gets the last character that was written to the parse buffer.
        /// </summary>
        /// <returns>The last written character, or null if the buffer is empty.</returns>
        private char _getLastCharFromBuffer() => (m_bufferpos == 0) ? '\0' : m_buffer[m_bufferpos - 1];

        /// <summary>
        /// Writes a character to the parse buffer.
        /// </summary>
        /// <param name="c">The character to write.</param>
        private void _writeCharToBuffer(char c) {
            if (m_buffer.Length == m_bufferpos)
                DataStructureUtil.resizeArray(ref m_buffer, m_bufferpos, m_bufferpos + 1, false);
            m_buffer[m_bufferpos++] = c;
        }

        /// <summary>
        /// Reads a character from the input string with the current token type as a word.
        /// </summary>
        ///
        /// <remarks>
        /// If the end of the token has been reached, the <see cref="m_curTokenType"/> field is set
        /// to <see cref="TokenType.NONE" qualifyHint="true"/> and the parse buffer is cleared once
        /// the token has been processed. The current read position will then indicate where the next
        /// token must begin.
        /// </remarks>
        private void _continueWordToken() {

            char c = m_str[m_strpos];

            if (c >= 'A' && c <= 'Z') {
                // Convert all uppercase letters in words to lowercase to ensure case insensitivity.
                // Since the date parser accepts only ASCII characters, there is no need to use
                // char.ToLowerCase()
                _writeCharToBuffer((char)(c | 0x20));
                m_strpos++;
                return;
            }

            if (c >= 'a' && c <= 'z') {
                _writeCharToBuffer(c);
                m_strpos++;
                return;
            }

            if (c == '.' && m_strpos != m_str.Length - 1) {
                // A single dot between two characters of a word is allowed so that
                // things like A.M. and P.M. are interpreted correctly.
                char next = m_str[m_strpos + 1];
                if (next >= 'A' && next <= 'Z') {
                    _writeCharToBuffer((char)(next | 0x20));
                    m_strpos += 2;
                    return;
                }
                if (next >= 'a' && next <= 'z') {
                    _writeCharToBuffer(next);
                    m_strpos += 2;
                    return;
                }
            }

            // End the word token.
            _writeToken();

            // Ignore any token separator after a word
            if (c == '.' || c == '/' || c == ',') {
                m_strpos++;
            }
            else if (c == '-' && m_strpos != 0 && m_strpos != m_str.Length - 1
                && m_tokenTypes[m_tokenTypes.length - 1] == TokenType.MONTH)
            {
                // A dash at the end of a word token is tricky because it can be used as a
                // negative sign in certain contexts which are dependent on the meaning of
                // the word and spacing. For example:
                // "1-January-2000", "1 - January - 2000": Must be in year 2000 (Not -2000!)
                // "1 January -2000": Must be in year -2000.
                // "UTC-5": Must be a negative timezone offset.
                // The following rule is used to disambiguate: If the previously written token
                // (the word which ended here) is a month name, and the dash is either preceded
                // by a letter or not followed by a digit, it is skipped and therefore not
                // considered as a potential negative sign for the next token.
                char prevChar = m_str[m_strpos - 1], nextChar = m_str[m_strpos + 1];
                if (nextChar < '0' || nextChar > '9'
                    || (prevChar >= 'A' && prevChar <= 'Z') || (prevChar >= 'a' && prevChar <= 'z'))
                {
                    m_strpos++;
                }
            }

        }

        /// <summary>
        /// Reads a character from the input string with the current token type as a single number.
        /// </summary>
        ///
        /// <remarks>
        /// If the end of the token has been reached, the <see cref="m_curTokenType"/> field is set
        /// to <see cref="TokenType.NONE" qualifyHint="true"/> and the parse buffer is cleared once
        /// the token has been processed. The current read position will then indicate where the next
        /// token must begin.
        /// </remarks>
        private void _continueNumberToken() {

            char c = m_str[m_strpos];

            if (c >= '0' && c <= '9') {
                _writeCharToBuffer(c);
                m_strpos++;
                return;
            }

            if (c == '/') {
                _writeCharToBuffer('.');
                m_strpos++;
                m_curTokenType = TokenType.NUMGROUP_SLASH2;
            }
            else if (c == '-') {
                // Dashes in a number token are interpreted as follows:
                // If the dash is the first character, it is taken as a negative sign.
                // If the dash is the second character and the first character was a sign,
                // the string is invalid.
                // In all other cases, the dash is a group separator.
                // This rule also applies to number groups.

                if (m_bufferpos == 0) {
                    _writeCharToBuffer(c);
                    m_strpos++;
                }
                else if (m_bufferpos == 1 && (m_buffer[0] == '+' || m_buffer[0] == '-')) {
                    m_isValid = false;
                }
                else {
                    _writeCharToBuffer('.');
                    m_strpos++;
                    m_curTokenType = TokenType.NUMGROUP_DASH2;
                }
            }
            else if (c == '.') {
                _writeCharToBuffer('.');
                m_strpos++;
                m_curTokenType = TokenType.NUMGROUP_DOT2;
            }
            else if (c == ':') {
                _writeCharToBuffer('.');
                m_strpos++;
                m_curTokenType = TokenType.NUMGROUP_COLONDOT2;
            }
            else if (c == '+' && m_bufferpos == 0) {
                _writeCharToBuffer(c);
                m_strpos++;
            }
            else {
                _writeToken();
            }

        }

        /// <summary>
        /// Reads a character from the input string with the current token type as a 2-number group.
        /// </summary>
        ///
        /// <remarks>
        /// If the end of the token has been reached, the <see cref="m_curTokenType"/> field is set
        /// to <see cref="TokenType.NONE" qualifyHint="true"/> and the parse buffer is cleared once
        /// the token has been processed. The current read position will then indicate where the next
        /// token must begin.
        /// </remarks>
        private void _continueNumGroup2Token() {

            char c = m_str[m_strpos];

            if (c >= '0' && c <= '9') {
                _writeCharToBuffer(c);
                m_strpos++;
                return;
            }

            if (c == '/') {
                if (_getLastCharFromBuffer() == '.') {
                    m_isValid = false;
                }
                else if (m_curTokenType == TokenType.NUMGROUP_SLASH2) {
                    _writeCharToBuffer('.');
                    m_strpos++;
                    m_curTokenType = TokenType.NUMGROUP_SLASH3;
                }
                else {
                    _writeToken();
                    m_strpos++;
                }
            }
            else if (c == '-') {
                char lastChar = _getLastCharFromBuffer();

                if (lastChar == '.') {
                    _writeCharToBuffer(c);
                    m_strpos++;
                }
                else if (lastChar == '-' || lastChar == '+') {
                    m_isValid = false;
                }
                else if (m_curTokenType == TokenType.NUMGROUP_DASH2) {
                    _writeCharToBuffer('.');
                    m_strpos++;
                    m_curTokenType = TokenType.NUMGROUP_DASH3;
                }
                else {
                    _writeToken();
                }
            }
            else if (c == '.' || c == ':') {
                if (_getLastCharFromBuffer() == '.') {
                    m_isValid = false;
                }
                else if (m_curTokenType == TokenType.NUMGROUP_DOT2) {
                    _writeCharToBuffer('.');
                    m_strpos++;
                    m_curTokenType = (c == '.') ? TokenType.NUMGROUP_DOT3 : TokenType.NUMGROUP_COLONDOT3;
                }
                else if (m_curTokenType == TokenType.NUMGROUP_COLONDOT2) {
                    _writeCharToBuffer('.');
                    m_strpos++;
                    m_curTokenType = TokenType.NUMGROUP_COLONDOT3;
                }
                else {
                    _writeToken();
                    m_strpos++;
                }
            }
            else if (c == '+') {
                if (_getLastCharFromBuffer() == '.') {
                    _writeCharToBuffer(c);
                    m_strpos++;
                }
                else {
                    _writeToken();
                }
            }
            else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) {
                // Before beginning a new word, ensure than any separator at the end of the
                // buffer is removed and the token type changed if needed.
                if (_getLastCharFromBuffer() == '.') {
                    m_bufferpos--;
                    m_curTokenType = TokenType.NUMBER;
                }
                _writeToken();
            }
            else {
                _writeToken();
            }

        }

        /// <summary>
        /// Reads a character from the input string with the current token type as a 3-number group.
        /// </summary>
        ///
        /// <remarks>
        /// If the end of the token has been reached, the <see cref="m_curTokenType"/> field is set
        /// to <see cref="TokenType.NONE" qualifyHint="true"/> and the parse buffer is cleared once
        /// the token has been processed. The current read position will then indicate where the next
        /// token must begin.
        /// </remarks>
        private void _continueNumGroup3Token() {

            char c = m_str[m_strpos];

            if (c >= '0' && c <= '9') {
                _writeCharToBuffer(c);
                m_strpos++;
                return;
            }

            if (c == '/') {
                if (_getLastCharFromBuffer() == '.') {
                    m_isValid = false;
                }
                else {
                    _writeToken();
                    m_strpos++;
                }
            }
            else if (c == '-') {
                char lastChar = _getLastCharFromBuffer();

                if (lastChar == '.') {
                    _writeCharToBuffer(c);
                    m_strpos++;
                }
                else if (lastChar == '-' || lastChar == '+') {
                    m_isValid = false;
                }
                else {
                    _writeToken();
                }
            }
            else if (c == '.' || c == ':') {
                // Number groups with colons and/or dots can have a maximum of four
                // numbers, to represent millisecond-precise times. (Groups with
                // slashes or dashes, on the other hand, can have only upto three
                // numbers, since they are only used for dates)

                if (_getLastCharFromBuffer() == '.') {
                    m_isValid = false;
                }
                else if (m_curTokenType == TokenType.NUMGROUP_DOT3) {
                    _writeCharToBuffer('.');
                    m_strpos++;
                    m_curTokenType = (c == '.') ? TokenType.NUMGROUP_DOT4 : TokenType.NUMGROUP_COLONDOT4;
                }
                else if (m_curTokenType == TokenType.NUMGROUP_COLONDOT3) {
                    _writeCharToBuffer('.');
                    m_strpos++;
                    m_curTokenType = TokenType.NUMGROUP_COLONDOT4;
                }
                else {
                    _writeToken();
                    m_strpos++;
                }
            }
            else if (c == '+') {
                if (_getLastCharFromBuffer() == '.') {
                    _writeCharToBuffer(c);
                    m_strpos++;
                }
                else {
                    _writeToken();
                }

            }
            else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) {
                if (_getLastCharFromBuffer() == '.') {
                    m_bufferpos--;

                    switch (m_curTokenType) {
                        case TokenType.NUMGROUP_DASH3:
                            m_curTokenType = TokenType.NUMGROUP_DASH2;
                            break;
                        case TokenType.NUMGROUP_SLASH3:
                            m_curTokenType = TokenType.NUMGROUP_SLASH2;
                            break;
                        case TokenType.NUMGROUP_DOT3:
                            m_curTokenType = TokenType.NUMGROUP_DOT2;
                            break;
                        case TokenType.NUMGROUP_COLONDOT3:
                            m_curTokenType = TokenType.NUMGROUP_COLONDOT2;
                            break;
                    }
                }
                _writeToken();
            }
            else {
                _writeToken();
            }

        }

        /// <summary>
        /// Reads a character from the input string with the current token type as a 4-number group.
        /// </summary>
        ///
        /// <remarks>
        /// If the end of the token has been reached, the <see cref="m_curTokenType"/> field is set
        /// to <see cref="TokenType.NONE" qualifyHint="true"/> and the parse buffer is cleared once
        /// the token has been processed. The current read position will then indicate where the next
        /// token must begin.
        /// </remarks>
        private void _continueNumGroup4Token() {

            char c = m_str[m_strpos];

            if (c >= '0' && c <= '9') {
                _writeCharToBuffer(c);
                m_strpos++;
                return;
            }

            if (c == '.' || c == ':' || c == '/') {
                if (_getLastCharFromBuffer() == '.') {
                    m_isValid = false;
                }
                else {
                    _writeToken();
                    m_strpos++;
                }
            }
            else if (c == '-') {
                char lastChar = _getLastCharFromBuffer();

                if (lastChar == '.') {
                    _writeCharToBuffer(c);
                    m_strpos++;
                }
                else if (lastChar == '-' || lastChar == '+') {
                    m_isValid = false;
                }
                else {
                    _writeToken();
                }
            }
            else if (c == '+') {
                if (_getLastCharFromBuffer() == '.') {
                    _writeCharToBuffer(c);
                    m_strpos++;
                }
                else {
                    _writeToken();
                }
            }
            else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) {
                if (_getLastCharFromBuffer() == '.') {
                    m_bufferpos--;

                    if (m_curTokenType == TokenType.NUMGROUP_DOT4)
                        m_curTokenType = TokenType.NUMGROUP_DOT3;
                    else if (m_curTokenType == TokenType.NUMGROUP_COLONDOT4)
                        m_curTokenType = TokenType.NUMGROUP_COLONDOT3;
                }
                _writeToken();
            }
            else {
                _writeToken();
            }

        }

        /// <summary>
        /// Writes the current token into the token list using the data in the parse buffer, and
        /// clears the parse buffer so that a new token can be started.
        /// </summary>
        private void _writeToken() {

            TokenType tkType = m_curTokenType;

            if (tkType == TokenType.NONE || m_bufferpos == 0) {
                m_curTokenType = TokenType.NONE;
                return;
            }

            switch (tkType) {

                case TokenType.NUMBER: {
                    int bufReadPos = 0;
                    int data = _parsePackedInt(ref bufReadPos);
                    if (!m_isValid)
                        return;

                    m_tokenTypes.add(tkType);
                    m_tokenData.add(data);
                    break;
                }

                case TokenType.NUMGROUP_COLONDOT2:
                case TokenType.NUMGROUP_DASH2:
                case TokenType.NUMGROUP_DOT2:
                case TokenType.NUMGROUP_SLASH2:
                    m_tokenTypes.add(tkType);
                    _writeNumGroupTokenData(2);
                    break;

                case TokenType.NUMGROUP_COLONDOT3:
                case TokenType.NUMGROUP_DASH3:
                case TokenType.NUMGROUP_DOT3:
                case TokenType.NUMGROUP_SLASH3:
                    m_tokenTypes.add(tkType);
                    _writeNumGroupTokenData(3);
                    break;

                case TokenType.NUMGROUP_COLONDOT4:
                case TokenType.NUMGROUP_DOT4:
                    m_tokenTypes.add(tkType);
                    _writeNumGroupTokenData(4);
                    break;

                case TokenType.WORD:
                    _writeWordToken();
                    break;

            }

            m_curTokenType = TokenType.NONE;
            m_bufferpos = 0;

        }

        /// <summary>
        /// Parses a PackedInt from the parse buffer, starting at the given position.
        /// </summary>
        /// <param name="bufReadPos">The position in the parse buffer at which to start reading. This
        /// will be incremented by the number of characters read from the buffer.</param>
        /// <returns>The encoded PackedInt. Use the <see cref="PackedInt(Int32)"/> constructor to
        /// decode it.</returns>
        private int _parsePackedInt(ref int bufReadPos) {

            char[] buf = m_buffer;
            int bufSize = m_bufferpos;
            int readPos = bufReadPos;

            if (readPos >= bufSize) {
                m_isValid = false;
                return 0;
            }

            int sign;
            switch (buf[readPos]) {
                case '+':
                    readPos++;
                    sign = 1;
                    break;
                case '-':
                    readPos++;
                    sign = -1;
                    break;
                default:
                    sign = 0;
                    break;
            }

            if (readPos >= bufSize) {
                // Anything with only a sign isn't valid.
                m_isValid = false;
                return 0;
            }

            int val = 0;
            int startPos = readPos;

            while (readPos < bufSize) {
                char c = buf[readPos];
                if (c == '.')
                    break;

                val = val * 10 + (c - '0');
                if (val > PackedInt.MAX_MAGNITUDE) {
                    m_isValid = false;
                    return 0;
                }

                readPos++;
            }

            bufReadPos = readPos;
            return (new PackedInt(sign, val, readPos - startPos)).encode();

        }

        /// <summary>
        /// Writes the token data for a numeric group. The parse buffer must contain the numeric
        /// strings for the group items, separated by '.'.
        /// </summary>
        /// <param name="nGroupItems">The number of items in the numeric group.</param>
        private void _writeNumGroupTokenData(int nGroupItems) {
            int bufReadPos = 0, bufSize = m_bufferpos;

            for (int i = 0; i < nGroupItems; i++) {
                int packedInt = _parsePackedInt(ref bufReadPos);
                if (!m_isValid)
                    break;
                m_tokenData.add(packedInt);
                if (bufReadPos < bufSize)
                    bufReadPos++;   // Skip the separator
            }
        }

        /// <summary>
        /// Writes a word token to the token list. The word token is converted into an appropriate
        /// token type based on the word's meaning (month name, weekday name etc.).
        /// </summary>
        private void _writeWordToken() {

            char[] buf = m_buffer;
            int bufSize = m_bufferpos;

            TokenType tkType = TokenType.INVALID_WORD;
            int tkData = -1;

            if (bufSize == 1) {
                if (buf[0] == 't')
                    tkType = TokenType.EXPECT_TIME;
                else if (buf[0] == 'z')
                    tkType = TokenType.UTC;
            }
            else if (bufSize == 2 && buf[1] == 'm') {
                if (buf[0] == 'a')
                    tkType = TokenType.AM;
                else if (buf[0] == 'p')
                    tkType = TokenType.PM;
            }
            else if (bufSize == 3
                && ((buf[0] == 'u' && buf[1] == 't' && buf[2] == 'c')
                    || (buf[0] == 'g' && buf[1] == 'm' && buf[2] == 't')))
            {
                // UTC/GMT
                tkType = TokenType.UTC;
            }
            else if (_matchBufferToWords(s_shortMonthNames, out tkData)
                || _matchBufferToWords(s_fullMonthNames, out tkData))
            {
                tkType = TokenType.MONTH;
            }
            else if (_matchBufferToWords(s_shortWeekdayNames, out tkData)
                || _matchBufferToWords(s_fullWeekdayNames, out tkData))
            {
                tkType = TokenType.WEEKDAY;
            }

            m_tokenTypes.add(tkType);
            if (tkType != TokenType.INVALID_WORD && tkData != -1)
                m_tokenData.add(tkData);

        }

        /// <summary>
        /// Checks if the contents of the current parse buffer are equal to one of the strings in the
        /// given array.
        /// </summary>
        /// <param name="arr">The array.</param>
        /// <param name="index">If a match was found, the index of the matching string in the array is
        /// written here.</param>
        /// <returns>True if a match was found, false otherwise.</returns>
        private bool _matchBufferToWords(string[] arr, out int index) {
            ReadOnlySpan<char> bufferSpan = m_buffer.AsSpan(0, m_bufferpos);

            for (int i = 0; i < arr.Length; i++) {
                if (bufferSpan.Equals(arr[i], StringComparison.Ordinal)) {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        /// <summary>
        /// Process the parsed tokens are returns the timestamp.
        /// </summary>
        /// <returns>The timestamp created from the tokens.</returns>
        private long _processTokensAndGetTimestamp() {
            if (m_tokenTypes.length == 0) {
                m_isValid = false;
                return 0;
            }

            var tokenTypes = m_tokenTypes.asSpan();

            for (int i = 0; i < tokenTypes.Length && m_isValid; i++) {
                TokenType tkType = tokenTypes[i];

                switch (tkType) {
                    case TokenType.MONTH:
                        if ((m_setFlags & SetFlags.MONTH) != 0 || m_timeExpected) {
                            m_isValid = false;
                        }
                        else {
                            m_month = m_tokenData[m_tokenDataReadPtr++];
                            m_setFlags |= SetFlags.MONTH;
                        }
                        break;

                    case TokenType.WEEKDAY:
                        if ((m_setFlags & SetFlags.WEEKDAY) != 0 || m_timeExpected) {
                            m_isValid = false;
                        }
                        else {
                            // Ignore weekday specifiers.
                            m_tokenDataReadPtr++;
                            m_setFlags |= SetFlags.WEEKDAY;
                        }
                        break;

                    case TokenType.UTC:
                        if ((m_setFlags & (SetFlags.UTC | SetFlags.TIMEZONE)) != 0) {
                            m_isValid = false;
                        }
                        else {
                            m_setFlags |= SetFlags.UTC;
                            m_timeZoneOffset = 0;
                        }
                        break;

                    case TokenType.EXPECT_TIME:
                        if (m_timeExpected)
                            m_isValid = false;
                        else
                            m_timeExpected = true;
                        break;

                    case TokenType.AM:
                    case TokenType.PM:
                        if ((m_setFlags & (SetFlags.AM | SetFlags.PM)) != 0 || (m_setFlags & SetFlags.HOUR) == 0)
                            m_isValid = false;
                        else
                            m_setFlags |= (tkType == TokenType.AM) ? SetFlags.AM : SetFlags.PM;
                        break;

                    case TokenType.NUMBER:
                        _readNumberToken(i);
                        break;

                    case TokenType.NUMGROUP_DOT2:
                    case TokenType.NUMGROUP_COLONDOT2:
                    case TokenType.NUMGROUP_SLASH2:
                    case TokenType.NUMGROUP_DASH2:
                        _readNumGroup2Token(i);
                        break;

                    case TokenType.NUMGROUP_DOT3:
                    case TokenType.NUMGROUP_COLONDOT3:
                    case TokenType.NUMGROUP_SLASH3:
                    case TokenType.NUMGROUP_DASH3:
                        _readNumGroup3Token(i);
                        break;

                    case TokenType.NUMGROUP_DOT4:
                    case TokenType.NUMGROUP_COLONDOT4:
                        _readNumGroup4Token(i);
                        break;

                    default:
                        m_isValid = false;
                        break;
                }
            }

            const SetFlags dmySetMask = SetFlags.YEAR | SetFlags.MONTH | SetFlags.DAY,
                           hourMinSetMask = SetFlags.HOUR | SetFlags.MINUTE;

            if ((m_setFlags & dmySetMask) != dmySetMask) {
                // Date, month and year are all required.
                m_isValid = false;
                return 0;
            }

            if ((m_setFlags & hourMinSetMask) != hourMinSetMask) {
                // If exactly one of hour and minute are set, the date
                // is invalid. If neither of them are set, they default to zero.
                if ((m_setFlags & hourMinSetMask) != 0) {
                    m_isValid = false;
                    return 0;
                }
                m_hour = 0;
                m_minute = 0;
            }

            // Second and millisecond default to zero if not set.
            if ((m_setFlags & SetFlags.SECOND) == 0)
                m_second = 0;
            if ((m_setFlags & SetFlags.MILLI) == 0)
                m_millisec = 0;

            // Adjust the hour if AM/PM is present

            if ((m_setFlags & SetFlags.AM) != 0) {
                if ((uint)m_hour > 12) {
                    m_isValid = false;
                    return 0;
                }
                else if (m_hour == 12) {
                    m_hour = 0;
                }
            }
            else if ((m_setFlags & SetFlags.PM) != 0) {
                if ((uint)m_hour > 12) {
                    m_isValid = false;
                    return 0;
                }
                else if (m_hour < 12) {
                    m_hour += 12;
                }
            }

            bool isLocalTime = (m_setFlags & (SetFlags.UTC | SetFlags.TIMEZONE)) == 0;
            long timestamp = DateHelper.createTimestamp(m_year, m_month, m_day - 1, m_hour, m_minute, m_second, m_millisec, isLocalTime);

            if (!isLocalTime && m_timeZoneOffset != 0)
                timestamp -= (long)m_timeZoneOffset * 60000L;

            return timestamp;
        }

        /// <summary>
        /// Processes a number token.
        /// </summary>
        /// <param name="index">The index of the token in the <see cref="m_tokenTypes"/>
        /// list.</param>
        private void _readNumberToken(int index) {
            const SetFlags dmySetMask = SetFlags.DAY | SetFlags.MONTH | SetFlags.YEAR,
                           dmSetMask = SetFlags.DAY | SetFlags.MONTH,
                           hourMinSetMask = SetFlags.HOUR | SetFlags.MINUTE;

            PackedInt packedInt = new PackedInt(m_tokenData[m_tokenDataReadPtr++]);

            // A number token is interpreted as a timezone offset if both of the following hold:
            // (1) It has an explicit sign
            // (2) It follows a GMT/UTC/Z, or the hour and minute have been set already and the
            //     timezone has not yet been set.

            if (packedInt.sign != 0
                && ((index != 0 && m_tokenTypes[index - 1] == TokenType.UTC)
                    || ((m_setFlags & hourMinSetMask) == hourMinSetMask && (m_setFlags & SetFlags.TIMEZONE) == 0)))
            {
                if ((m_setFlags & SetFlags.TIMEZONE) != 0) {
                    m_isValid = false;
                    return;
                }

                if (packedInt.nDigits > 4) {
                    m_isValid = false;
                    return;
                }

                int offsetHour, offsetMin;
                if (packedInt.nDigits > 2) {
                    offsetHour = packedInt.value / 100;
                    offsetMin = packedInt.value - offsetHour * 100;
                }
                else {
                    offsetHour = packedInt.value;
                    offsetMin = 0;
                }

                m_setFlags |= SetFlags.TIMEZONE;
                m_timeZoneOffset = offsetHour * 60 + offsetMin;
                if (packedInt.sign == -1)
                    m_timeZoneOffset = -m_timeZoneOffset;

                return;
            }

            // Any number more than 6 digits long is always a year.
            if (packedInt.nDigits > 6) {
                if ((m_setFlags & SetFlags.YEAR) != 0 || m_timeExpected) {
                    m_isValid = false;
                }
                else {
                    m_year = packedInt.signedValue;
                    m_setFlags |= SetFlags.YEAR;
                }
                return;
            }

            // If a number is at least 3 digits long, and the year has not been set,
            // interpret it as a year.
            if (packedInt.nDigits >= 3 && (m_setFlags & SetFlags.YEAR) == 0) {
                if (m_timeExpected) {
                    m_isValid = false;
                }
                else {
                    m_year = packedInt.signedValue;
                    m_setFlags |= SetFlags.YEAR;
                }
                return;
            }

            // If a number is between 5 and 6 digits, and the year has already been set,
            // interpret it as a time as HHMMSS.
            if (packedInt.nDigits >= 5) {
                if (packedInt.sign != 0 ||
                    (m_setFlags & (SetFlags.HOUR | SetFlags.MINUTE | SetFlags.SECOND)) != 0)
                {
                    m_isValid = false;
                }
                else {
                    int hour = packedInt.value / 10000;
                    int minsec = packedInt.value - hour * 10000;
                    m_hour = hour;
                    m_minute = minsec / 100;
                    m_second = minsec - (m_minute * 100);
                    m_setFlags |= SetFlags.HOUR | SetFlags.MINUTE | SetFlags.SECOND;
                }
                return;
            }

            // If a number is between 3 and 4 digits, and the year has already been set,
            // interpret it as a time as HHMM.
            if (packedInt.nDigits >= 3) {
                if (packedInt.sign != 0 ||
                    (m_setFlags & (SetFlags.HOUR | SetFlags.MINUTE)) != 0)
                {
                    m_isValid = false;
                }
                else {
                    m_hour = packedInt.value / 100;
                    m_minute = packedInt.value - m_hour * 100;
                    m_setFlags |= SetFlags.HOUR | SetFlags.MINUTE;
                }
                return;
            }

            // For numbers less than 3 digits
            if (packedInt.sign != 0 || m_timeExpected || (m_setFlags & dmySetMask) == dmySetMask) {
                m_isValid = false;
            }
            else if ((m_setFlags & dmSetMask) == dmSetMask) {
                // If day and month are set, interpret as a year offset from 1900.
                m_year = packedInt.value + 1900;
                m_setFlags |= SetFlags.YEAR;
            }
            else if ((m_setFlags & SetFlags.MONTH) != 0
                || (index != m_tokenTypes.length - 1 && m_tokenTypes[index + 1] == TokenType.MONTH))
            {
                // If month is not set or the following token is a month name, interpret the number as a day.
                m_day = packedInt.value;
                m_setFlags |= SetFlags.DAY;
            }
            else {
                // In all other cases, interpret the number as a month.
                m_month = packedInt.value - 1;
                m_setFlags |= SetFlags.MONTH;
            }
        }

        /// <summary>
        /// Processes a 2-member numeric group token.
        /// </summary>
        /// <param name="index">The index of the token in the <see cref="m_tokenTypes"/>
        /// list.</param>
        private void _readNumGroup2Token(int index) {
            const SetFlags dmySetMask = SetFlags.DAY | SetFlags.MONTH | SetFlags.YEAR;
            const SetFlags hourMinSetMask = SetFlags.HOUR | SetFlags.MINUTE;

            TokenType tkType = m_tokenTypes[index];
            PackedInt packedInt1 = new PackedInt(m_tokenData[m_tokenDataReadPtr]),
                      packedInt2 = new PackedInt(m_tokenData[m_tokenDataReadPtr + 1]);

            m_tokenDataReadPtr += 2;

            bool isDateTokenType = tkType == TokenType.NUMGROUP_DASH2 || tkType == TokenType.NUMGROUP_SLASH2;

            if (m_timeExpected && isDateTokenType) {
                m_isValid = false;
                return;
            }

            if (packedInt1.sign != 0
                && ((index != 0 && m_tokenTypes[index - 1] == TokenType.UTC)
                    || ((m_setFlags & hourMinSetMask) == hourMinSetMask && (m_setFlags & SetFlags.TIMEZONE) == 0)))
            {
                // Interpret as timezone offset

                if ((m_setFlags & SetFlags.TIMEZONE) != 0 || isDateTokenType || packedInt2.sign != 0) {
                    m_isValid = false;
                    return;
                }

                m_timeZoneOffset = packedInt1.value * 60 + packedInt2.value;
                if (packedInt1.sign == -1)
                    m_timeZoneOffset = -m_timeZoneOffset;

                m_setFlags |= SetFlags.TIMEZONE;
                return;
            }

            if (m_timeExpected || tkType == TokenType.NUMGROUP_COLONDOT2
                || (m_setFlags & dmySetMask) == dmySetMask)
            {
                // Interpret as HH:MM if the separator is a colon, the token follows 'T', or the date,
                // month and year are all set.
                if ((m_setFlags & (SetFlags.HOUR | SetFlags.MINUTE)) != 0 || isDateTokenType
                    || packedInt1.sign != 0 || packedInt2.sign != 0)
                {
                    m_isValid = false;
                    return;
                }
                m_hour = packedInt1.value;
                m_minute = packedInt2.value;
                m_setFlags |= SetFlags.HOUR | SetFlags.MINUTE;
            }
            else if ((m_setFlags & SetFlags.MONTH) != 0) {
                // Interpret as day:year if the month is set.
                if (packedInt1.sign != 0) {
                    m_isValid = false;
                    return;
                }
                m_day = packedInt1.value;
                m_year = packedInt2.signedValue;
                m_setFlags |= SetFlags.DAY | SetFlags.YEAR;
            }
            else {
                // In all other cases interpret as day:month.
                if (packedInt1.sign != 0 || packedInt2.sign != 0) {
                    m_isValid = false;
                    return;
                }
                m_month = packedInt1.value;
                m_day = packedInt2.value;
                m_setFlags |= SetFlags.DAY | SetFlags.MONTH;
            }
        }

        /// <summary>
        /// Processes a 3-member numeric group token.
        /// </summary>
        /// <param name="index">The index of the token in the <see cref="m_tokenTypes"/>
        /// list.</param>
        private void _readNumGroup3Token(int index) {
            TokenType tkType = m_tokenTypes[index];
            Span<int> data = m_tokenData.asSpan(m_tokenDataReadPtr, 3);

            PackedInt packedInt1 = new PackedInt(data[0]),
                      packedInt2 = new PackedInt(data[1]),
                      packedInt3 = new PackedInt(data[2]);

            m_tokenDataReadPtr += 3;

            bool isDateTokenType = tkType == TokenType.NUMGROUP_DASH3 || tkType == TokenType.NUMGROUP_SLASH3;

            if (m_timeExpected && isDateTokenType) {
                m_isValid = false;
                return;
            }

            if (m_timeExpected || tkType == TokenType.NUMGROUP_COLONDOT3
                || (m_setFlags & (SetFlags.DAY | SetFlags.MONTH | SetFlags.YEAR)) != 0)
            {
                // Interpret as HH:MM:SS if the token contains a colon separator, the token follows 'T', or the date,
                // month and year are all set.
                if ((m_setFlags & (SetFlags.HOUR | SetFlags.MINUTE | SetFlags.SECOND)) != 0
                    || packedInt1.sign != 0 || packedInt2.sign != 0 || packedInt3.sign != 0
                    || isDateTokenType)
                {
                    m_isValid = false;
                    return;
                }
                m_hour = packedInt1.value;
                m_minute = packedInt2.value;
                m_second = packedInt3.value;
                m_setFlags |= SetFlags.HOUR | SetFlags.MINUTE | SetFlags.SECOND;
            }
            else {
                // Interpret as year:month:day if the first number has more than two digits, otherwise
                // month:day:year.
                if (packedInt1.sign != 0 || packedInt1.nDigits > 2) {
                    if (packedInt2.sign != 0 || packedInt3.sign != 0) {
                        m_isValid = false;
                        return;
                    }
                    m_year = packedInt1.signedValue;
                    m_month = packedInt2.value;
                    m_day = packedInt3.value;
                }
                else {
                    if (packedInt1.sign != 0 || packedInt2.sign != 0) {
                        m_isValid = false;
                        return;
                    }
                    m_month = packedInt1.value;
                    m_day = packedInt2.value;
                    m_year = packedInt3.signedValue;
                    if (m_year >= 0 && m_year < 100)
                        m_year += 1900;
                }
                m_setFlags |= SetFlags.DAY | SetFlags.MONTH | SetFlags.YEAR;
            }
        }

        /// <summary>
        /// Processes a 4-member numeric group token.
        /// </summary>
        /// <param name="index">The index of the token in the <see cref="m_tokenTypes"/>
        /// list.</param>
        private void _readNumGroup4Token(int index) {
            Span<int> data = m_tokenData.asSpan(m_tokenDataReadPtr, 4);
            PackedInt packedInt1 = new PackedInt(data[0]),
                      packedInt2 = new PackedInt(data[1]),
                      packedInt3 = new PackedInt(data[2]),
                      packedInt4 = new PackedInt(data[3]);

            m_tokenDataReadPtr += 4;

            if (packedInt1.sign != 0 || packedInt2.sign != 0
                || packedInt3.sign != 0 || packedInt4.sign != 0)
            {
                m_isValid = false;
                return;
            }

            if ((m_setFlags & (SetFlags.HOUR | SetFlags.MINUTE | SetFlags.SECOND | SetFlags.MILLI)) != 0) {
                m_isValid = false;
                return;
            }

            m_hour = packedInt1.value;
            m_minute = packedInt2.value;
            m_second = packedInt3.value;
            m_millisec = packedInt4.value;
            m_setFlags |= SetFlags.HOUR | SetFlags.MINUTE | SetFlags.SECOND | SetFlags.MILLI;
        }

    }
}
