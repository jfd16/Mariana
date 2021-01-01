using System;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Converts AS3 regular expressions into equivalent .NET regular expressions.
    /// </summary>
    ///
    /// <remarks>
    /// <para>The regex parser and transpiler performs the following tasks:</para>
    /// <list type="bullet">
    /// <item>Detects syntax errors and throws the appropriate exception.</item>
    /// <item>Converts octal escape sequences to hexadecimal escape sequences to avoid
    /// octal-backreference ambiguity.</item>
    /// <item>Resolves octal vs. backreference ambiguities in the original regex, and converts
    /// backreferences to the <c>\k&lt;N&gt;</c> syntax of .NET regex.</item>
    /// <item>Escapes certain characters which are not escaped in the original regex, but are
    /// literal in their context. (e.g. <c>[[]</c> to <c>[\[]</c>)</item>
    /// <item>Strips nonsignificant whitespace, if extended mode is enabled.</item>
    /// <item>Replaces dots with equivalent expressions, if dotall mode is enabled.</item>
    /// <item>Creates a group number to name map, if named groups are present.</item>
    /// </list>
    /// </remarks>
    internal struct RegexTranspiler {

        private enum Error {
            UNBALANCED_PAREN,
            LONE_BACKSLASH,
            NO_CHARS_AFTER_X,
            NO_CHARS_AFTER_U,
            INVALID_HEX_ESCAPE,
            UNEXPECTED_QUANT,
            INVALID_NUMERIC_QUANT,
            ILLEGAL_CHAR_AFTER_SPECIAL,
            INVALID_GROUP_NAME,
            UNTERMINATED_GROUP_NAME,
            GROUP_LIMIT_EXCEEDED,
            UNTERMINATED_CHAR_SET,
            EMPTY_CHAR_SET,
            CHAR_SET_REVERSE_RANGE,
        }

        /// <summary>
        /// The error messages used by the regex preprocessor.
        /// </summary>
        private static DynamicArray<string> s_errorMessages = new DynamicArray<string>(15, true) {
            [(int)Error.UNBALANCED_PAREN] =
                @"Unbalanced grouping parentheses.",
            [(int)Error.LONE_BACKSLASH] =
                @"Unpaired '\' at end of pattern.",
            [(int)Error.NO_CHARS_AFTER_X] =
                @"At least 2 characters expected after '\x'.",
            [(int)Error.NO_CHARS_AFTER_U] =
                @"At least 4 characters expected after '\u'.",
            [(int)Error.INVALID_HEX_ESCAPE] =
                @"Illegal character in hexadecimal escape sequence.",
            [(int)Error.UNEXPECTED_QUANT] =
                @"Unexpected quantifier character '*', '+' or '?'.",
            [(int)Error.INVALID_NUMERIC_QUANT] =
                @"Invalid numeric quantifier.",
            [(int)Error.ILLEGAL_CHAR_AFTER_SPECIAL] =
                @"Illegal characters after '(?' construct: expecting '=', '!', '<=', '<!', ':' or 'P<...>'.",
            [(int)Error.INVALID_GROUP_NAME] =
                @"Invalid group name. Names can contain only alphabets (A-Z and a-z) and digits (0-9), and must not start with a digit.",
            [(int)Error.UNTERMINATED_GROUP_NAME] =
                @"Unterminated group name construct (?P<...>).",
            [(int)Error.GROUP_LIMIT_EXCEEDED] =
                @"Capturing group limit of 999 exceeded.",
            [(int)Error.UNTERMINATED_CHAR_SET] =
                @"Character set missing closing ']'.",
            [(int)Error.EMPTY_CHAR_SET] =
                @"Illegal empty character set.",
            [(int)Error.CHAR_SET_REVERSE_RANGE] =
                @"Illegal reverse range in character set.",
        };

        /// <summary>
        /// A numeric reference patch is emitted when parsing a regular expression when a numeric
        /// escape sequence (such as <c>\123</c>) is encountered, which is later converted into a
        /// backreference or octal escape at the end of parsing depending on the number of capturing
        /// groups.
        /// </summary>
        private struct NumRefPatch {
            /// <summary>
            /// The position in the (unpatched) regex string at which the expression for the patch must be
            /// inserted.
            /// </summary>
            public int position;

            /// <summary>
            /// The number of digits of the numeric reference. (Maximum 3)
            /// </summary>
            public byte nDigits;

            /// <summary>
            /// The value of the numeric reference, encoded in binary coded decimal.
            /// </summary>
            public ushort bcdDigits;
        }

        /// <summary>
        /// The pattern being parsed.
        /// </summary>
        private string m_source;

        /// <summary>
        /// The current index of the parser read pointer in the pattern string.
        /// </summary>
        private int m_srcpos;

        /// <summary>
        /// The buffer into which the translated regex is written to.
        /// </summary>
        private char[] m_buffer;

        /// <summary>
        /// The current write position in the <see cref="m_buffer"/> buffer.
        /// </summary>
        private int m_bufpos;

        /// <summary>
        /// A list of numeric reference patches generated during parsing.
        /// </summary>
        private DynamicArray<NumRefPatch> m_numRefPatches;

        /// <summary>
        /// The number of parentheses in the pattern which are not closed yet.
        /// </summary>
        private int m_parenBalance;

        /// <summary>
        /// A Boolean value indicating whether the parser is currently in a character set region.
        /// </summary>
        private bool m_isInCharSet;

        /// <summary>
        /// The previous character that was parsed in a character set region. This is used for
        /// checking reverse ranges such as <c>[9-0]</c> (which are invalid).
        /// </summary>
        private char m_charSetLastChar;

        /// <summary>
        /// The translated .NET regex string, once parsing is complete.
        /// </summary>
        private string m_transpiledRegex;

        /// <summary>
        /// A list of group names indexed by the corresponding group number. Null values are used for
        /// unnamed groups. In this table, 0 corresponds to the first group, not 1 (as in the case of
        /// backreferences and replace strings)
        /// </summary>
        private DynamicArray<string> m_groupNames;

        /// <summary>
        /// Gets the translated .NET regex string of the pattern transpiled by
        /// <see cref="transpile"/>.
        /// </summary>
        public string transpiledPattern => m_transpiledRegex;

        /// <summary>
        /// Gets an array mapping group numbers to their names in the pattern parsed by
        /// <see cref="transpile"/>
        /// </summary>
        /// <returns>An array containing the group names corresponding to each group number, with the
        /// first group as number 0. If there are no named groups in the pattern, returns
        /// null.</returns>
        public string[] getGroupNames() => (m_groupNames.length == 0) ? null : m_groupNames.toArray();

        /// <summary>
        /// Gets the number of capturing groups in the parsed regular expression.
        /// </summary>
        public int groupCount => m_groupNames.length;

        /// <summary>
        /// Parses and transpiles an AS3 regex pattern.
        /// </summary>
        /// <param name="pattern">The regex pattern string.</param>
        /// <param name="dotall">Set to true if dotall mode is being used.</param>
        /// <param name="extended">Set to true if extended mode is being used.</param>
        public void transpile(string pattern, bool dotall, bool extended) {

            m_buffer = new char[pattern.Length];
            m_bufpos = 0;
            m_source = pattern;
            m_srcpos = 0;
            m_groupNames.clear();
            m_parenBalance = 0;
            m_isInCharSet = false;
            m_transpiledRegex = null;

            int strlen = pattern.Length;

            if (extended)
                _goToNextNonSpace();

            bool quantifierNotExpected = true;  // Regex cannot begin with a quantifier.

            while (m_srcpos < strlen) {
                char c = pattern[m_srcpos];

                switch (c) {
                    case '\\':
                        _readEscapeSequence();
                        quantifierNotExpected = false;
                        break;

                    case '+':
                    case '*':
                    case '?':
                        if (quantifierNotExpected)
                            throw _error(Error.UNEXPECTED_QUANT);

                        _readQuantifier();
                        quantifierNotExpected = true;
                        break;

                    case '{':
                        if (quantifierNotExpected) {
                            // If there is an opening curly brace where a quantifier is not
                            // expected, it is a literal and not an error.
                            _writeCharPair('\\', '{');
                            m_srcpos++;
                        }
                        else {
                            quantifierNotExpected = _readNumericQuantifier();
                        }
                        break;

                    case '}':
                        _writeCharPair('\\', '}');
                        m_srcpos++;
                        quantifierNotExpected = false;
                        break;

                    case '.':
                        if (dotall)
                            _writeDotAll();
                        else
                            _writeChar('.');

                        m_srcpos++;
                        quantifierNotExpected = false;
                        break;

                    case '(':
                        _readGroupStart();
                        quantifierNotExpected = true;
                        break;

                    case ')':
                        _writeChar(')');
                        m_parenBalance--;
                        if (m_parenBalance < 0)
                            throw _error(Error.UNBALANCED_PAREN);

                        m_srcpos++;
                        quantifierNotExpected = false;
                        break;

                    case '[':
                        _readCharSet();
                        quantifierNotExpected = false;
                        break;

                    default:
                        _writeChar(c);
                        m_srcpos++;
                        quantifierNotExpected = false;
                        break;
                }

                if (extended)
                    _goToNextNonSpace();
            }

            if (m_parenBalance != 0)
                throw _error(Error.UNBALANCED_PAREN);

            _patchNumericReferences();

            m_transpiledRegex = new string(m_buffer, 0, m_bufpos);
            m_buffer = null;
        }

        /// <summary>
        /// Reads a backslashed character or character sequence from the source pattern.
        /// </summary>
        private void _readEscapeSequence() {

            int charsLeft = m_source.Length - m_srcpos;
            if (charsLeft < 2)
                throw _error(Error.LONE_BACKSLASH);

            char escapeChar = m_source[m_srcpos + 1];
            m_srcpos += 2;

            if (!m_isInCharSet) {
                switch (escapeChar) {
                    case 'w':
                    case 'd':
                    case 's':
                    case 'W':
                    case 'D':
                    case 'S':
                        // These are special metasequences allowed in AS3 regex syntax, so backslash them.
                        // Character classes handle their parsing differently, so parse them only in non-class
                        // regions
                        _writeCharPair('\\', escapeChar);
                        return;
                }
            }

            switch (escapeChar) {
                case 'b':
                    if (m_isInCharSet)  // '\b' has different meaning inside and outside a character set.
                        _writeHexEscape(0x08);
                    else
                        _writeCharPair('\\', 'b');
                    break;

                case 'f':
                    _writeHexEscape(0x12);
                    break;
                case 'n':
                    _writeHexEscape(0x10);
                    break;
                case 'r':
                    _writeHexEscape(0x13);
                    break;
                case 't':
                    _writeHexEscape(0x09);
                    break;
                case 'v':
                    _writeHexEscape(0x11);
                    break;

                case '(':
                case ')':
                case '{':
                case '}':
                case '[':
                case ']':
                case '\\':
                case '?':
                case '*':
                case '+':
                case '.':
                case '^':
                case '$':
                case '|':
                case '-':
                    // These must be escaped
                    _writeCharPair('\\', escapeChar);
                    break;

                case 'x': {
                    if (charsLeft < 4)
                        throw _error(Error.NO_CHARS_AFTER_X);

                    char digit1 = m_source[m_srcpos],
                         digit2 = m_source[m_srcpos + 1];

                    if (!_isHexChar(digit1) || !_isHexChar(digit2))
                        throw _error(Error.INVALID_HEX_ESCAPE);

                    _writeHexEscapeFromDigits(digit1, digit2);
                    m_srcpos += 2;
                    break;
                }

                case 'u': {
                    if (charsLeft < 6)
                        throw _error(Error.NO_CHARS_AFTER_U);

                    char digit1 = m_source[m_srcpos], digit2 = m_source[m_srcpos + 1],
                         digit3 = m_source[m_srcpos + 2], digit4 = m_source[m_srcpos + 3];

                    if (!_isHexChar(digit1) || !_isHexChar(digit2) || !_isHexChar(digit3) || !_isHexChar(digit4))
                        throw _error(Error.INVALID_HEX_ESCAPE);

                    _writeHexEscapeFromDigits(digit1, digit2, digit3, digit4);
                    m_srcpos += 4;
                    break;
                }

                case '0': {
                    if (charsLeft == 2) {
                        // No additional characters, so this is always a null character
                        _writeHexEscape(0x00);
                        break;
                    }

                    char digit1, digit2;

                    if (charsLeft == 3) {
                        // One additional character, check for an octal digit.
                        digit1 = m_source[m_srcpos];
                        if (digit1 >= '0' && digit1 <= '7') {
                            _writeHexEscape((byte)(digit1 - '0'));
                            m_srcpos++;
                        }
                        else {
                            _writeHexEscape(0x00);  // Write a null character
                        }
                        break;
                    }

                    // More than one additional character available.
                    digit1 = m_source[m_srcpos];
                    digit2 = m_source[m_srcpos + 1];

                    if (digit1 >= '0' && digit1 <= '7') {
                        if (digit2 >= '0' && digit2 <= '7') {
                            // Two-digit octal
                            _writeHexEscape((byte)((digit2 - '0') | (digit1 - '0') << 3));
                            m_srcpos += 2;
                        }
                        else {
                            // One-digit octal
                            _writeHexEscape((byte)(digit1 - '0'));  // One-digit octal
                            m_srcpos++;
                        }
                    }
                    else {
                        _writeHexEscape(0x00);  // null character
                    }

                    break;
                }

                default:
                    if (escapeChar < '1' || escapeChar > '9') {
                        // Don't backslash anything other than digits or the special
                        // characters handled earlier. (In particular, we shouldn't escape
                        // spaces in extended mode since the extended mode .NET regex option
                        // is not used by the AVM2 and spaces are stripped in the transpile
                        // step.
                        _writeChar(escapeChar);
                        break;
                    }

                    // Numeric characters escaped with a backslash can represent backreferences, literals
                    // or octal escapes - depending on the number of capture groups in the entire
                    // regex. Such escapes are not written to the buffer at this point. Instead, they are
                    // stored as numeric reference patches to be resolved once the entire regex is parsed.

                    // However, in character classes, these cannot resolve to backreferences and hence
                    // are always interpreted as octal escape sequences (or literal digits, if the
                    // digit(s) are not valid octal). This is implemented in the _readNumericReference method.

                    if (charsLeft == 2) {
                        // No additional characters.
                        _readNumericReference(escapeChar);
                    }
                    else if (charsLeft == 3) {
                        // One additional character. If this is a digit, write a two-digit numeric
                        // reference, otherwise it is a one-digit reference followed by a literal.
                        char digit = m_source[m_srcpos];
                        if (digit >= '0' && digit <= '9') {
                            _readNumericReference(escapeChar, digit);
                            m_srcpos++;
                        }
                        else {
                            _readNumericReference(escapeChar);
                        }
                    }
                    else {
                        // More than one additional character.
                        // The capturing groups cannot have numbers more than three digits, as these groups
                        // are limited to 999 per regex.

                        char digit1 = m_source[m_srcpos];
                        char digit2 = m_source[m_srcpos + 1];

                        if (digit1 >= '0' && digit1 <= '9') {
                            if (digit2 >= '0' && digit2 <= '9') {
                                _readNumericReference(escapeChar, digit1, digit2);
                                m_srcpos += 2;
                            }
                            else {
                                _readNumericReference(escapeChar, digit1);
                                m_srcpos++;
                            }
                        }
                        else {
                            _readNumericReference(escapeChar);
                        }
                    }
                    break;
            }

        }

        /// <summary>
        /// Reads a quantifier from the source pattern.
        /// </summary>
        private void _readQuantifier() {
            if (m_srcpos == m_source.Length - 1) {
                _writeChar(m_source[m_srcpos]);
                m_srcpos++;
                return;
            }

            char nextChar = m_source[m_srcpos + 1];
            if (nextChar == '?') {
                _writeCharPair(m_source[m_srcpos], '?');
                m_srcpos += 2;
            }
            else {
                _writeChar(m_source[m_srcpos]);
                m_srcpos++;
            }
        }

        /// <summary>
        /// Reads a numeric (curly-braced) quantifier from the source pattern.
        /// </summary>
        /// <returns>True if the a valid quantifier was parsed, false otherwise.</returns>
        ///
        /// <remarks>
        /// If the quantifier contains invalid characters, a literal '{' is written to the buffer and
        /// this method returns false. Characters following the opening brace should then be
        /// interpreted as normal regex syntax.
        /// </remarks>
        private bool _readNumericQuantifier() {

            string source = m_source;
            int srcpos = m_srcpos + 1;
            int srclen = m_source.Length;
            bool hasComma = false;
            int charCount = 1;
            int firstNum = -1, secondNum = -1;
            int currentValue = 0, curDigitCount = 0;

            while (srcpos < srclen) {
                char c = source[srcpos];

                if (c >= '0' && c <= '9') {
                    if (currentValue > (Int32.MaxValue / 10))
                        throw _error(Error.INVALID_NUMERIC_QUANT);
                    currentValue *= 10;
                    if (currentValue > Int32.MaxValue - (c - '0'))
                        throw _error(Error.INVALID_NUMERIC_QUANT);

                    currentValue += c - '0';
                    curDigitCount++;
                    charCount++;
                }
                else if (c == ',' && !hasComma) {
                    if (curDigitCount == 0)  // '{,' is literal
                        break;

                    charCount++;
                    firstNum = currentValue;
                    currentValue = 0;
                    curDigitCount = 0;
                    hasComma = true;
                }
                else if (c == '}') {
                    if (charCount == 1)   // An empty pair of braces '{}' is literal.
                        break;

                    if (curDigitCount == 0)
                        currentValue = -1;

                    if (hasComma)
                        secondNum = currentValue;
                    else
                        firstNum = currentValue;

                    if (secondNum != -1 && secondNum < firstNum)
                        throw _error(Error.INVALID_NUMERIC_QUANT);

                    _copyToBuffer(m_source.AsSpan(m_srcpos, charCount + 1));
                    m_srcpos = srcpos + 1;
                    return true;
                }
                else {
                    // Invalid character found - in this case, write an escaped '{' and let
                    // the characters succeeding it be treated as general syntax.
                    break;
                }

                srcpos++;
            }

            _writeCharPair('\\', '{');
            m_srcpos++;
            return false;

        }

        /// <summary>
        /// Returns a Boolean value indicating whether the given character is a valid hexadecimal
        /// digit.
        /// </summary>
        /// <param name="c">The character to check.</param>
        /// <returns>True, if the character is a hexadecimal digit, false otherwise.</returns>
        private static bool _isHexChar(char c) =>
            (uint)(c - '0') <= 9 || (uint)(c - 'A') <= 5 || (uint)(c - 'a') <= 5;

        /// <summary>
        /// Returns the hexadecimal value of a character. This method assumes that the given character
        /// is a valid hexadecimal digit and does not do any validation.
        /// </summary>
        /// <param name="c">The character.</param>
        /// <returns>The hexadecimal value of the character.</returns>
        private static int _charToHex(char c) {
            if (c >= 'a')
                return c - 'a' + 10;
            if (c >= 'A')
                return c - 'A' + 10;
            return c - '0';
        }

        /// <summary>
        /// Writes a character into the buffer.
        /// </summary>
        /// <param name="value">The character to write to the token stream.</param>
        private void _writeChar(char value) {
            if (m_buffer.Length == m_bufpos)
                DataStructureUtil.resizeArray(ref m_buffer, m_buffer.Length, m_bufpos + 1, false);
            m_buffer[m_bufpos++] = value;
            m_charSetLastChar = value;
        }

        /// <summary>
        /// Writes a pair of characters into the buffer.
        /// </summary>
        /// <param name="c1">The first character.</param>
        /// <param name="c2">The second character.</param>
        private void _writeCharPair(char c1, char c2) {
            if (m_buffer.Length - m_bufpos < 2)
                DataStructureUtil.resizeArray(ref m_buffer, m_buffer.Length, m_bufpos + 2, false);
            m_buffer[m_bufpos] = c1;
            m_buffer[m_bufpos + 1] = c2;
            m_bufpos += 2;
            m_charSetLastChar = c2;
        }

        /// <summary>
        /// Copies the characters from the given span into the buffer.
        /// </summary>
        /// <param name="span">The span whose contents to copy into the buffer.</param>
        ///
        /// <remarks>
        /// This method does not update the <see cref="m_charSetLastChar"/> field, even when inside
        /// a character set.
        /// </remarks>
        private void _copyToBuffer(ReadOnlySpan<char> span) {
            if (m_buffer.Length - m_bufpos < span.Length)
                DataStructureUtil.resizeArray(ref m_buffer, m_buffer.Length, m_bufpos + span.Length, false);

            span.CopyTo(m_buffer.AsSpan(m_bufpos));
            m_bufpos += span.Length;
        }

        /// <summary>
        /// Writes the two-hex-digit escape sequence of the given character into the buffer.
        /// </summary>
        /// <param name="code">The character value whose hex sequence to write.</param>
        private void _writeHexEscape(byte code) {
            if (m_buffer.Length - m_bufpos < 4)
                DataStructureUtil.resizeArray(ref m_buffer, m_buffer.Length, m_bufpos + 4, false);

            int low = code & 15, high = (code >> 4) & 15;

            var span = m_buffer.AsSpan(m_bufpos, 4);
            m_bufpos += 4;

            span[0] = '\\';
            span[1] = 'x';
            span[2] = (high > 9) ? (char)(high + 'A' - 10) : (char)(high + '0');
            span[3] = (low > 9) ? (char)(low + 'A' - 10) : (char)(low + '0');

            m_charSetLastChar = (char)code;
        }

        /// <summary>
        /// Writes a hexadecimal escape sequence into the buffer for a character whose value is given
        /// by two hex digits as characters.
        /// </summary>
        /// <param name="c1">The first hexadecimal character, representing the high 4 bits of the
        /// character value.</param>
        /// <param name="c2">The first hexadecimal character, representing the low 4 bits of the
        /// character value.</param>
        private void _writeHexEscapeFromDigits(char c1, char c2) {
            if (m_buffer.Length - m_bufpos < 4)
                DataStructureUtil.resizeArray(ref m_buffer, m_buffer.Length, m_bufpos + 4, false);

            var span = m_buffer.AsSpan(m_bufpos, 4);
            m_bufpos += 4;

            span[0] = '\\';
            span[1] = 'x';
            span[2] = c1;
            span[3] = c2;

            if (m_isInCharSet)
                // Since deducing the actual character from the hex code is required only
                // for the purpose of validating ranges in character classes, do not do
                // this outside a class region.
                m_charSetLastChar = (char)(_charToHex(c2) | _charToHex(c1) << 4);
        }

        /// <summary>
        /// Writes the four-hex-digit Unicode escape sequence into the buffer of the character whose
        /// value is given by four hex digits given as characters.
        /// </summary>
        ///
        /// <param name="c1">The first hexadecimal character, representing bits 13 to 16 of the code
        /// point.</param>
        /// <param name="c2">The second hexadecimal character, representing bits 9 to 12 of the code
        /// point.</param>
        /// <param name="c3">The third hexadecimal character, representing bits 5 to 8 of the code
        /// point.</param>
        /// <param name="c4">The fourth hexadecimal character, representing bits 1 to 4 of the code
        /// point.</param>
        private void _writeHexEscapeFromDigits(char c1, char c2, char c3, char c4) {
            if (m_buffer.Length - m_bufpos < 6)
                DataStructureUtil.resizeArray(ref m_buffer, m_buffer.Length, m_bufpos + 6, false);

            var span = m_buffer.AsSpan(m_bufpos, 6);
            m_bufpos += 6;

            span[0] = '\\';
            span[1] = 'u';
            span[2] = c1;
            span[3] = c2;
            span[4] = c3;
            span[5] = c4;

            if (m_isInCharSet)
                m_charSetLastChar = (char)(_charToHex(c4) | _charToHex(c3) << 4 | _charToHex(c2) << 8 | _charToHex(c1) << 12);
        }

        /// <summary>
        /// Writes a capturing group reference into the buffer whose group number is given by three
        /// decimal digits.
        /// </summary>
        ///
        /// <param name="digit1">The first digit of the capturing group number. Set this to zero for a
        /// one- or two-digit group number.</param>
        /// <param name="digit2">The second digit of the capturing group number. Set this (and
        /// <paramref name="digit1"/>) to zero for a one-digit group number.</param>
        /// <param name="digit3">The third digit of the capturing group number.</param>
        ///
        /// <remarks>
        /// The group number is calculated from the parameters as: <paramref name="digit1"/> * 100 +
        /// <paramref name="digit2"/> * 10 + <paramref name="digit3"/>.
        /// </remarks>
        private void _writeGroupReference(int digit1, int digit2, int digit3) {
            // Use the syntax \k<n> instead of \n to avoid ambiguities with octal escape sequences
            if (m_buffer.Length - m_bufpos < 7)
                DataStructureUtil.resizeArray(ref m_buffer, m_buffer.Length, m_bufpos + 7, false);

            var span = m_buffer.AsSpan(m_bufpos, 7);

            span[0] = '\\';
            span[1] = 'k';
            span[2] = '<';

            if (digit1 != 0) {
                span[3] = (char)(digit1 + '0');
                span[4] = (char)(digit2 + '0');
                span[5] = (char)(digit3 + '0');
                span[6] = '>';
                m_bufpos += 7;
            }
            else if (digit2 != 0) {
                span[3] = (char)(digit2 + '0');
                span[4] = (char)(digit3 + '0');
                span[5] = '>';
                m_bufpos += 6;
            }
            else {
                span[3] = (char)(digit3 + '0');
                span[4] = '>';
                m_bufpos += 5;
            }
        }

        /// <summary>
        /// Parses an octal escape sequence and writes the appropriate regex into the buffer.
        /// </summary>
        ///
        /// <param name="c1">The first digit character of the escape sequence.</param>
        /// <param name="c2">The second digit character of the escape sequence. If the escape sequence
        /// has only one character, set this (and <paramref name="c3"/>) to null.</param>
        /// <param name="c3">The third digit character of the escape sequence. If the escape sequence
        /// has only one or two characters, set this to null.</param>
        ///
        /// <remarks>
        /// The digit arguments must be passed to this method as digit characters, not their values
        /// (e.g. '1' or 0x31 instead of 1).
        /// </remarks>
        private void _writeOctal(char c1, char c2 = '\0', char c3 = '\0') {
            if (c1 > '7') {
                // If the first digit is not a valid octal digit, all digits are literal
                _writeChar(c1);
                if (c2 != 0)
                    _writeChar(c2);
                if (c3 != 0)
                    _writeChar(c3);
                return;
            }

            if (c2 == '\0') {
                // Only one octal digit
                _writeHexEscape((byte)(c1 - '0'));
                return;
            }

            if (c3 == '\0') {
                // Two octal digits - if the second one is not a valid octal digit, the first digit is
                // interpreted as an octal escape and the second as a literal.
                if (c2 > '7') {
                    _writeHexEscape((byte)(c1 - '0'));
                    _writeChar(c2);
                }
                else {
                    _writeHexEscape((byte)((c2 - '0') | (c1 - '0') << 3));
                }
                return;
            }

            // Three octal digits

            if (c2 > '7') {
                // Second digit invalid: Both second and third digits are literal.
                _writeHexEscape((byte)(c1 - '0'));
                _writeChar(c2);
                _writeChar(c3);
                return;
            }

            if (c1 > '3' || c3 > '7') {
                // Third digit is invalid, or first is 4-7: Two digit octal and third digit
                // is literal.
                _writeHexEscape((byte)((c2 - '0') | (c1 - '0') << 3));
                _writeChar(c3);
                return;
            }

            // Three-digit octal
            _writeHexEscape((byte)((c3 - '0') | (c2 - '0') << 3 | (c1 - '0') << 6));
        }

        /// <summary>
        /// Parses a numeric reference escape sequence and interprets it as an octal escape or emits a
        /// numeric reference patch (which may resolve to a backreference) depending on the current
        /// context.
        /// </summary>
        ///
        /// <param name="c1">The first digit character of the escape sequence.</param>
        /// <param name="c2">The second digit character of the escape sequence. If the escape sequence
        /// has only one character, set this (and <paramref name="c3"/>) to the null
        /// character.</param>
        /// <param name="c3">The third digit character of the escape sequence. If the escape sequence
        /// has only one or two characters, set this to the null character.</param>
        ///
        /// <remarks>
        /// The digit arguments must be passed to this method as digit characters, not their values
        /// (e.g. '1' or 0x31 instead of 1).
        /// </remarks>
        private void _readNumericReference(char c1, char c2 = '\0', char c3 = '\0') {
            if (m_isInCharSet) {
                // In character set, there cannot be any backreferences. Only octals.
                _writeOctal(c1, c2, c3);
                return;
            }

            NumRefPatch patch;
            patch.position = m_bufpos;

            if (c2 == '\0' && c3 == '\0') {
                patch.nDigits = 1;
                patch.bcdDigits = (ushort)(c1 - '0');
            }
            else if (c3 == '\0') {
                patch.nDigits = 2;
                patch.bcdDigits = (ushort)((c1 - '0') << 4 | (c2 - '0'));
            }
            else {
                patch.nDigits = 3;
                patch.bcdDigits = (ushort)((c1 - '0') << 8 | (c2 - '0') << 4 | (c3 - '0'));
            }

            m_numRefPatches.add(patch);
        }

        /// <summary>
        /// Advances the internal pattern pointer to the next non-whitespace character. This is used
        /// when parsing patterns with the extended flag set.
        /// </summary>
        private void _goToNextNonSpace() {
            string src = m_source;
            int pos = m_srcpos, len = src.Length;
            while (pos < len) {
                char c = src[pos];
                if (c != ' ' && (uint)(c - 9) > 4)
                    break;
                pos++;
            }
            m_srcpos = pos;
        }

        /// <summary>
        /// Writes the equivalent expression for a dot in dotall mode into the buffer.
        /// </summary>
        private void _writeDotAll() {
            // Although .NET regex has a Singleline option which is equivalent to the dotall mode, it is
            // not available when the ECMAScript option is also set (this implementation of RegExp
            // does use that option), so the dot must be substituted with an equivalent expression
            // during transpilation such as '[\s\S]'.

            if (m_buffer.Length - m_bufpos < 6)
                DataStructureUtil.resizeArray(ref m_buffer, m_buffer.Length, m_bufpos + 6, false);

            var span = m_buffer.AsSpan(m_bufpos, 6);
            m_bufpos += 6;

            "[\\s\\S]".AsSpan().CopyTo(span);
        }

        /// <summary>
        /// Reads the beginning of a parenthesized group (which may be a capturing group,
        /// non-capturing group, named group, lookahead or lookbehind)
        /// </summary>
        private void _readGroupStart() {

            string src = m_source;
            int srcpos = m_srcpos + 1;
            int srclen = m_source.Length;
            int charsLeft = src.Length - srcpos;

            if (charsLeft == 0) {
                // A lone opening parenthesis at the end of the pattern: this is not correct
                throw _error(Error.UNBALANCED_PAREN);
            }

            if (charsLeft == 1) {
                // If only one character remains, the only possibility is the empty group.
                if (src[srcpos] != ')')
                    throw _error(Error.UNBALANCED_PAREN);

                if (m_groupNames.length == 999)
                    throw _error(Error.GROUP_LIMIT_EXCEEDED);
                m_groupNames.add(null);

                _writeCharPair('(', ')');
                m_srcpos++;

                return;
            }

            _writeChar('(');
            m_parenBalance++;

            // All characters of a group start sequence must be continuous without spaces even
            // in extended mode, so we don't call _goToNextNonSpace() here.

            char nextChar = src[srcpos];

            if (nextChar == '?') {
                // Special group

                if (charsLeft == 2) {
                    // If there are only two characters left, only the question mark will be
                    // part of the group (if followed by a ')'), which is an error if not escaped.
                    if (src[srcpos + 1] == ')')
                        throw _error(Error.ILLEGAL_CHAR_AFTER_SPECIAL);
                    else
                        throw _error(Error.UNBALANCED_PAREN);
                }

                srcpos++;
                nextChar = src[srcpos];

                if (nextChar == '=' || nextChar == '!') {
                    // Positive/negative lookahead
                    _writeCharPair('?', nextChar);
                    m_srcpos = srcpos + 1;
                }
                else if (nextChar == '<') {
                    if (charsLeft == 3) {
                        // The expression '(?<)' is also not valid
                        if (src[srcpos + 1] == ')')
                            throw _error(Error.ILLEGAL_CHAR_AFTER_SPECIAL);
                        else
                            throw _error(Error.UNBALANCED_PAREN);
                    }

                    srcpos++;
                    nextChar = src[srcpos];

                    if (nextChar == '=' || nextChar == '!') {
                        // Positive/negative lookbehind
                        _writeCharPair('?', '<');
                        _writeChar(nextChar);
                    }
                    else {
                        throw _error(Error.ILLEGAL_CHAR_AFTER_SPECIAL);
                    }
                    m_srcpos = srcpos + 1;
                }
                else if (nextChar == ':') {
                    // No-capture group
                    _writeCharPair('?', ':');
                    m_srcpos = srcpos + 1;
                }
                else if (nextChar == 'P') {

                    // Named capture group.
                    // Valid group names must contain only the letters a-z (both uppercase and lowercase)
                    // and digits 0-9, and the first character must not be a digit.

                    if (charsLeft < 6) {
                        // There must be at least six characters after the opening '(' for the
                        // shortest valid named group construct: '(?P<a>)'
                        throw _error(Error.ILLEGAL_CHAR_AFTER_SPECIAL);
                    }

                    if (src[srcpos + 1] != '<')   // P is followed by something other than '<'
                        throw _error(Error.ILLEGAL_CHAR_AFTER_SPECIAL);

                    nextChar = src[srcpos + 2];
                    int nCharsInName = 1;

                    if (!(nextChar >= 'A' && nextChar <= 'Z') && !(nextChar >= 'a' && nextChar <= 'z'))
                        _error(Error.INVALID_GROUP_NAME);

                    srcpos += 3;

                    while (true) {
                        nextChar = src[srcpos];
                        if (nextChar == '>')    // End of group name
                            break;

                        if (!(nextChar >= 'A' && nextChar <= 'Z') && !(nextChar >= 'a' && nextChar <= 'z')
                            && !(nextChar >= '0' && nextChar <= '9'))
                        {
                            throw _error(Error.INVALID_GROUP_NAME);
                        }

                        srcpos++;
                        nCharsInName++;

                        if (srcpos == srclen)
                            throw _error(Error.UNTERMINATED_GROUP_NAME);
                    }

                    if (m_groupNames.length == 999) {
                        // Capturing groups (incuding named) are limited to 999 because the regex transpiler
                        // currently resolves backreferences of only up to three digits.
                        throw _error(Error.GROUP_LIMIT_EXCEEDED);
                    }

                    m_groupNames.add(src.Substring(srcpos - nCharsInName, nCharsInName));
                    m_srcpos = srcpos + 1;

                }
                else {
                    // Nothing else may appear after '?' except for the characters above
                    throw _error(Error.ILLEGAL_CHAR_AFTER_SPECIAL);
                }
            }
            else {
                // Unnamed capturing group.
                if (m_groupNames.length == 999)
                    throw _error(Error.GROUP_LIMIT_EXCEEDED);
                m_groupNames.add(null);
                m_srcpos++;
            }

        }

        /// <summary>
        /// Reads a character class from the source pattern.
        /// </summary>
        private void _readCharSet() {

            string src = m_source;
            int srcpos = m_srcpos + 1, srclen = src.Length;

            int charsLeft = srclen - srcpos;
            if (charsLeft == 0)     // Lone '[' at end of pattern
                throw _error(Error.UNTERMINATED_CHAR_SET);

            char c = src[srcpos];

            if (c == ']')   // Empty character sets ('[]') are not allowed
                throw _error(Error.EMPTY_CHAR_SET);

            if (charsLeft == 1)     // Only one character left: this indicates an unterminated set
                _error(Error.UNTERMINATED_CHAR_SET);

            if (c == '^') {
                if (src[srcpos + 1] == ']') {
                    // If '^' is the lone character of a set, it is literal, otherwise, if
                    // it is at the start of the set, it negates the set.
                    _writeCharPair('\\', '^');
                    m_srcpos += 3;
                    return;
                }
                m_isInCharSet = true;
                _writeCharPair('[', '^');
                srcpos++;
            }
            else {
                m_isInCharSet = true;
                _writeChar('[');
            }


            int setStartPos = srcpos;
            bool lastWasDash = false;
            bool lastWasSubclass = false;

            while (srcpos < srclen) {

                c = src[srcpos];

                if (c == '\\') {
                    if (srcpos == srclen - 1)
                        throw _error(Error.LONE_BACKSLASH);

                    char nextChar = src[srcpos + 1];
                    switch (nextChar) {
                        // These are special "subclasses" that can be used within classes. If they are preceded
                        // or succeeded by a dash character ('-'), the dash is literal.
                        case 'w':
                        case 'd':
                        case 's':
                        case 'W':
                        case 'D':
                        case 'S':
                            if (lastWasDash) {
                                _writeCharPair('\\', '-');
                                lastWasDash = false;
                            }
                            _writeCharPair('\\', nextChar);
                            lastWasSubclass = true;
                            srcpos += 2;
                            break;

                        default:
                            lastWasSubclass = false;

                            if (lastWasDash) {
                                // If the character is preceded by a dash character ('-'), a check has to be
                                // made to ensure that the character range is valid (i.e. it is not a reverse range)
                                char startOfRange = m_charSetLastChar;
                                _writeChar('-');

                                m_srcpos = srcpos;
                                _readEscapeSequence();
                                srcpos = m_srcpos;

                                if (m_charSetLastChar < startOfRange)
                                    throw _error(Error.CHAR_SET_REVERSE_RANGE);

                                lastWasDash = false;
                            }
                            else {
                                m_srcpos = srcpos;
                                _readEscapeSequence();
                                srcpos = m_srcpos;
                            }
                            break;
                    }
                }
                else if (c == ']') {
                    // End of class
                    if (lastWasDash)
                        _writeCharPair('\\', '-');

                    _writeChar(']');
                    m_srcpos = srcpos + 1;
                    m_isInCharSet = false;
                    return;
                }
                else if (c == '-' && !lastWasDash) {
                    if (lastWasSubclass || srcpos == setStartPos)
                        // If the '-' is preceded by a subclass (\w, \d etc.), or is at the
                        // beginning of the set (except for a possible '^'), it is literal.
                        _writeCharPair('\\', '-');
                    else
                        lastWasDash = true;

                    lastWasSubclass = false;
                    srcpos++;
                }
                else if (lastWasDash) {
                    if (c < m_charSetLastChar)
                        throw _error(Error.CHAR_SET_REVERSE_RANGE);

                    if (c == '-' || c == '[') {
                        // Escape these characters.
                        _writeChar('-');
                        _writeCharPair('\\', c);
                    }
                    else {
                        _writeCharPair('-', c);
                    }

                    lastWasDash = false;
                    lastWasSubclass = false;
                    srcpos++;
                }
                else {
                    if (c == '[')
                        _writeCharPair('\\', '[');
                    else
                        _writeChar(c);

                    srcpos++;
                    lastWasSubclass = false;
                }

            }

            // No ']', found
            throw _error(Error.UNTERMINATED_CHAR_SET);

        }

        /// <summary>
        /// Performs numeric reference patching once the regex string is parsed.
        /// </summary>
        private void _patchNumericReferences() {

            if (m_numRefPatches.length == 0)
                return;

            int groupCount = m_groupNames.length;

            char[] unpatchedBuffer = m_buffer;
            int unpatchedLength = m_bufpos;
            int lastPatchPosition = 0;

            // This is just an estimated length; we still need to check bounds on each
            // write and expand the buffer if needed.
            m_buffer = new char[checked(unpatchedLength + 6 * m_numRefPatches.length)];
            m_bufpos = 0;

            for (int i = 0, n = m_numRefPatches.length; i < n; i++) {

                NumRefPatch patch = m_numRefPatches[i];

                if (patch.position != lastPatchPosition) {
                    // Copy the data from the position of the last patch to this one.
                    _copyToBuffer(unpatchedBuffer.AsSpan(lastPatchPosition, patch.position - lastPatchPosition));
                    lastPatchPosition = patch.position;
                }

                if (patch.nDigits == 1) {
                    if (patch.bcdDigits <= groupCount) {
                        _writeGroupReference(0, 0, patch.bcdDigits);
                    }
                    else {
                        _writeOctal((char)(patch.bcdDigits + '0'), '\0', '\0');
                    }
                }
                else if (patch.nDigits == 2) {
                    int digit1 = patch.bcdDigits >> 4,
                        digit2 = patch.bcdDigits & 0xF;

                    if (digit1 * 10 + digit2 <= groupCount) {
                        _writeGroupReference(0, digit1, digit2);
                    }
                    else if (digit1 <= groupCount) {
                        _writeGroupReference(0, 0, digit1);
                        _writeChar((char)(digit2 + '0'));
                    }
                    else {
                        _writeOctal((char)(digit1 + '0'), (char)(digit2 + '0'), '\0');
                    }
                }
                else if (patch.nDigits == 3) {
                    int digit1 = patch.bcdDigits >> 8,
                        digit2 = (patch.bcdDigits >> 4) & 0xF,
                        digit3 = patch.bcdDigits & 0xF;

                    if (digit1 * 100 + digit2 * 10 + digit3 <= groupCount) {
                        _writeGroupReference(digit1, digit2, digit3);
                    }
                    else if (digit1 * 10 + digit2 <= groupCount) {
                        _writeGroupReference(0, digit1, digit2);
                        _writeChar((char)(digit3 + '0'));
                    }
                    else if (digit1 <= groupCount) {
                        _writeGroupReference(0, 0, digit1);
                        _writeCharPair((char)(digit2 + '0'), (char)(digit3 + '0'));
                    }
                    else {
                        _writeOctal((char)(digit1 + '0'), (char)(digit2 + '0'), (char)(digit3 + '0'));
                    }
                }

            }

            if (lastPatchPosition != unpatchedLength)
                // Copy any remaining characters from the unpatched regex.
                _copyToBuffer(unpatchedBuffer.AsSpan(lastPatchPosition, unpatchedLength - lastPatchPosition));

        }

        /// <summary>
        /// Returns an appropriate <see cref="AVM2Exception"/> for an error with the given code.
        /// </summary>
        /// <param name="errCode">The error code.</param>
        private AVM2Exception _error(Error errCode) {
            return ErrorHelper.createError(ErrorCode.MARIANA__REGEXP_PARSE_ERROR, s_errorMessages[(int)errCode]);
        }

    }
}
