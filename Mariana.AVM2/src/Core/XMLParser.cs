using System;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// An XML parser used for E4X.
    /// </summary>
    internal struct XMLParser {

        /// <summary>
        /// Represents a temporary attribute (whose name is not fully resolved).
        /// </summary>
        private struct UnresolvedAttribute {
            public string prefix;       // Namespace prefix of the attribute
            public string localName;    // Local name of the attribute
            public string value;        // Value of the attribute
            public int lineNumber;      // Line number on which the attribute is defined
        }

        private struct StackItem {
            public ASQName elementName;
            public ASXML[] attributes;
            public ASNamespace[] nsDecls;
            public int childrenStartAt;
        }

        private const int FLAG_IGNORE_SPACE = 1;
        private const int FLAG_IGNORE_COMMENTS = 2;
        private const int FLAG_IGNORE_PI = 4;
        private const int FLAG_USES_DEFAULT_NS = 8;
        private const int FLAG_USES_XML_NS = 16;

        /// <summary>
        /// The default namespace for the predefined prefix "xml".
        /// </summary>
        private static readonly ASNamespace s_namespaceForXml = new ASNamespace("xml", "http://www.w3.org/XML/1998/namespace");

        /// <summary>
        /// The character that results in the line number being incremented. On systems that use
        /// '\n' or '\r\n' as the new line, this is set to '\n'; on systems that use '\r' as the
        /// newline (some Mac OS systems), this is set to '\r'.
        /// </summary>
        private static readonly char s_newLineChar = (Environment.NewLine == "\r") ? '\r' : '\n';

        private string m_str;           // Source string.
        private int m_pos;              // Current read pointer index
        private int m_curLine;          // Current line number

        // The text buffer, used for holding the current string of text being parsed.
        private char[] m_buffer;

        private NamePool m_namePool;

        private DynamicArray<ASNamespace> m_nsInScope;

        private DynamicArray<int> m_nsInScopePtrs;

        private ASNamespace m_defaultNS;

        private DynamicArray<UnresolvedAttribute> m_unresolvedAttrs;

        private DynamicArray<StackItem> m_parserStack;

        private DynamicArray<ASXML> m_nodeStack;

        private int m_parserFlags;

        /// <summary>
        /// Parses the given XML string and returns an XMLList containing the parsed
        /// XML objects.
        /// </summary>
        /// <returns>An XMLList containing the XML objects parsed from <paramref name="str"/>.</returns>
        /// <param name="str">The XML string to parse.</param>
        public ASXMLList parseList(string str) {
            _init(str);

            DynamicArray<ASXML> list = new DynamicArray<ASXML>();
            while (m_pos < m_str.Length) {
                ASXML node = _readSingleNode();
                if (node != null)
                    list.add(node);
            }

            return new ASXMLList(list.getUnderlyingArray(), list.length, true);
        }

        /// <summary>
        /// Parses the given XML string and returns the parsed XML object.
        /// </summary>
        /// <returns>The XML object parsed from <paramref name="str"/>.</returns>
        /// <param name="str">The XML string to parse.</param>
        public ASXML parseSingleElement(string str) {
            _init(str);

            ASXML firstNode = _readSingleNode();

            if (m_pos == m_str.Length)
                return firstNode ?? ASXML.createNode(XMLNodeType.TEXT);

            if (firstNode != null
                && firstNode.nodeType != XMLNodeType.ELEMENT
                && (firstNode.nodeType != XMLNodeType.TEXT
                    || XMLHelper.isOnlyWhitespace(firstNode.nodeText)))
            {
                firstNode = null;
            }

            while (m_pos < m_str.Length) {
                ASXML curNode = _readSingleNode();
                if (curNode == null)
                    continue;

                if (curNode.nodeType != XMLNodeType.ELEMENT
                    && (curNode.nodeType != XMLNodeType.TEXT
                        || XMLHelper.isOnlyWhitespace(curNode.nodeText)))
                {
                    continue;
                }

                if (firstNode != null)
                    throw _error(ErrorCode.XML_MARKUP_AFTER_ROOT);

                firstNode = curNode;
            }

            return firstNode ?? ASXML.createNode(XMLNodeType.TEXT);
        }

        private void _init(string str) {
            m_str = str;
            m_pos = 0;
            m_curLine = 1;
            m_defaultNS = ASNamespace.getDefault();
            m_parserFlags = 0;

            if (ASXML.ignoreComments)
                m_parserFlags |= FLAG_IGNORE_COMMENTS;
            if (ASXML.ignoreProcessingInstructions)
                m_parserFlags |= FLAG_IGNORE_PI;
            if (ASXML.ignoreWhitespace)
                m_parserFlags |= FLAG_IGNORE_SPACE;

            m_nsInScope.clear();
            m_nsInScopePtrs.clear();
            m_parserStack.clear();
            m_nodeStack.clear();
            m_unresolvedAttrs.clear();

            if (m_buffer == null)
                m_buffer = new char[128];

            if (m_namePool == null)
                m_namePool = new NamePool();
        }

        /// <summary>
        /// Parses a single XML node, reading from the current position in the string.
        /// </summary>
        /// <returns>The created node as an XML object.</returns>
        private ASXML _readSingleNode() {

            m_parserFlags &= ~(FLAG_USES_XML_NS | FLAG_USES_DEFAULT_NS);

            while (true) {
                if ((m_parserFlags & FLAG_IGNORE_SPACE) != 0)
                    _goToNextNonSpace();

                if (m_pos == m_str.Length) {
                    // End of string reached.
                    if (m_parserStack.length != 0) {
                        throw _error(
                            ErrorCode.XML_ELEMENT_NOT_TERMINATED,
                            m_parserStack[m_parserStack.length - 1].elementName.AS_toString()
                        );
                    }

                    if (m_nodeStack.length == 0)
                        return null;

                    ASXML createdNode = m_nodeStack[0];
                    m_nodeStack.clear();
                    return createdNode;
                }

                if (m_parserStack.length == 0 && m_nodeStack.length != 0) {
                    ASXML createdNode = m_nodeStack[0];
                    m_nodeStack.clear();
                    return createdNode;
                }

                if (m_str[m_pos] != '<') {
                    // Text node.
                    _readText();
                    continue;
                }

                if (m_str.Length - m_pos < 2)
                    // There must be at least two characters after the opening '<'.
                    // One for the closing '>', and the other for the element name.
                    throw _error(ErrorCode.XML_PARSER_UNTERMINATED_ELEMENT);

                char ch = m_str[m_pos + 1];

                if (ch == '?') {
                    _readProcInstr();
                }
                else if (ch == '!') {
                    if (String.CompareOrdinal(m_str, m_pos + 2, "[CDATA[", 0, 7) == 0)
                        _readCDATA();
                    else if (m_str.Length - m_pos >= 4 && m_str[m_pos + 2] == '-' && m_str[m_pos + 3] == '-')
                        _readComment();
                    else
                        _readDoctype();
                }
                else if (ch == '/') {
                    _readEndTag();
                }
                else {
                    _readStartTag();
                }
            }

        }

        /// <summary>
        /// Moves the pointer to the next non-whitespace character.
        /// </summary>
        /// <returns>False if the end of the string has been reached, otherwise true.</returns>
        private bool _goToNextNonSpace() {
            ReadOnlySpan<char> span = m_str.AsSpan(m_pos);
            char newline = s_newLineChar;
            int i;
            for (i = 0; i < span.Length; i++) {
                char ch = span[i];
                if (!XMLHelper.isWhitespaceChar(ch))
                    break;
                if (ch == newline)
                    m_curLine++;
            }
            m_pos += i;
            return i < span.Length;
        }

        /// <summary>
        /// Reads an XML name or a prefix-name pair.
        /// </summary>
        /// <returns>True if a valid name was read, false otherwise.</returns>
        /// <param name="prefix">The namespace prefix, if any.</param>
        /// <param name="localName">The local name.</param>
        private bool _readName(out string prefix, out string localName) {
            prefix = null;
            localName = null;

            ReadOnlySpan<char> span  = m_str.AsSpan(m_pos);
            int nameLength = XMLHelper.getValidNamePrefixLength(span);
            if (nameLength == 0)
                return false;

            var nameSpan = span.Slice(0, nameLength);
            m_pos += nameLength;

            if (m_pos >= m_str.Length || m_str[m_pos] != ':') {
                localName = m_namePool.getPooledValue(nameSpan);
                return true;
            }

            prefix = m_namePool.getPooledValue(nameSpan);
            m_pos++;

            span  = m_str.AsSpan(m_pos);
            nameLength = XMLHelper.getValidNamePrefixLength(span);
            if (nameLength == 0)
                return false;

            localName = m_namePool.getPooledValue(span.Slice(0, nameLength));
            m_pos += nameLength;
            return true;
        }

        /// <summary>
        /// Reads an entity reference and returns its character value.
        /// </summary>
        private int _readEntity() {
            if (m_str.Length - m_pos < 3)
                // At least 3 characters required.
                throw _error(ErrorCode.MARIANA__XML_PARSER_UNTERMINATED_ENTITY);

            int startPos = m_pos;
            m_pos++;
            char ch = m_str[m_pos];

            if (ch == '#') {
                m_pos++;
                int numCode = _readNumericEntity();

                if (numCode == -1)
                    throw _error(ErrorCode.MARIANA__XML_PARSER_INVALID_ENTITY);
                if (numCode == -2)
                    throw _error(ErrorCode.MARIANA__XML_PARSER_UNTERMINATED_ENTITY);

                return numCode;
            }

            // Named entity reference
            if (!_readName(out string prefix, out string name))
                throw _error(ErrorCode.MARIANA__XML_PARSER_INVALID_ENTITY);

            if (m_pos == m_str.Length || m_str[m_pos] != ';')
                throw _error(ErrorCode.MARIANA__XML_PARSER_UNTERMINATED_ENTITY);

            m_pos++;

            if (prefix == null) {
                switch (name) {
                    case "lt":
                        return '<';
                    case "gt":
                        return '>';
                    case "amp":
                        return '&';
                    case "apos":
                        return '\'';
                    case "quot":
                        return '"';
                }
            }

            // This could be a custom entity defined in a DOCTYPE. Even though this
            // parser does not resolve them, we at least don't want such documents to
            // throw errors.
            m_pos = startPos + 1;
            return '&';
        }

        /// <summary>
        /// Reads a numeric entity and returns the character code point value.
        /// </summary>
        /// <returns>The code point for the numeric entity. If the numeric entity is
        /// invalid, -1 is returned; if no terminating semicolon was found, -2 is returned.</returns>
        private int _readNumericEntity() {
            int code = 0;
            int charsLeft = m_str.Length - m_pos;
            ReadOnlySpan<char> span = m_str.AsSpan(m_pos);

            if ((uint)span.Length <= 1)
                // At least two characters after # required
                return -2;

            char ch = span[0];

            if (ch == 'x') {
                // Hexadecimal
                span = span.Slice(1);

                for (int i = 0; i < span.Length; i++) {
                    ch = span[i];

                    if (ch == ';') {
                        // End of entity
                        if (i == 0)
                            return -1;

                        m_pos += charsLeft - span.Length + i + 1;
                        return code;
                    }

                    int hexdigit;
                    if ((uint)(ch - '0') <= 9)
                        hexdigit = ch - '0';
                    else if ((uint)(ch - 'A') <= 5)
                        hexdigit = ch - ('A' - 10);
                    else if ((uint)(ch - 'a') <= 5)
                        hexdigit = ch - ('a' - 10);
                    else
                        return -1;

                    code = (code << 4) | hexdigit;
                    if (code > 0x10FFFF)
                        return -1;
                }

                return -2;
            }
            else {
                // Decimal
                for (int i = 0; i < span.Length; i++) {
                    ch = span[i];

                    if (ch == ';') {
                        if (i == 0)
                            return -1;

                        m_pos += charsLeft - span.Length + i + 1;
                        return code;
                    }

                    if ((uint)(ch - '0') > 9)
                        return -1;

                    code = (code * 10) + (ch - '0');
                    if (code > 0x10FFFF)
                        return -1;
                }

                return -2;
            }
        }

        /// <summary>
        /// Reads a comment node and adds it as a child to the element currently being processed.
        /// </summary>
        private void _readComment() {
            m_pos += 4;   // For opening '<!--'

            ReadOnlySpan<char> span = m_str.AsSpan(m_pos);
            int charsLeft = m_str.Length - m_pos;
            char newline = s_newLineChar;
            ReadOnlySpan<char> searchChars = stackalloc char[] {'-', newline};

            int charsRead;

            while (true) {
                int index = span.IndexOfAny(searchChars);
                if (index == -1)
                    throw _error(ErrorCode.XML_PARSER_UNTERMINATED_COMMENT);

                char charAtIndex = span[index];

                if (charAtIndex == newline) {
                    m_curLine++;
                    span = span.Slice(index + 1);
                    continue;
                }

                if ((uint)(index + 2) >= (uint)span.Length)
                    throw _error(ErrorCode.XML_PARSER_UNTERMINATED_COMMENT);

                if (span[index + 1] == '-') {
                    if (span[index + 2] != '>')
                        throw _error(ErrorCode.MARIANA__XML_PARSER_COMMENT_INVALID_SEQUENCE);

                    charsRead = charsLeft - span.Length + index + 3;
                    break;
                }

                span = span.Slice(index + 1);
            }

            int startPos = m_pos;
            m_pos += charsRead;

            if ((m_parserFlags & FLAG_IGNORE_COMMENTS) != 0)
                return;

            m_nodeStack.add(ASXML.createNode(XMLNodeType.COMMENT, null, m_str.Substring(startPos, charsRead)));
        }

        /// <summary>
        /// Reads a processing instruction node and adds it as a child to the element currently
        /// being processed.
        /// </summary>
        private void _readProcInstr() {
            m_pos += 2;     // For opening '<?'

            if (!_readName(out string prefix, out string name) || prefix != null)
                throw _error(ErrorCode.MARIANA__XML_PARSER_INVALID_NAME, (prefix == null) ? name : prefix + ":" + name);

            _goToNextNonSpace();

            ReadOnlySpan<char> span = m_str.AsSpan(m_pos);
            int charsLeft = m_str.Length - m_pos;
            char newline = s_newLineChar;

            ReadOnlySpan<char> searchChars = stackalloc char[] {'?', newline};

            int charsRead;

            while (true) {
                int index = span.IndexOfAny(searchChars);
                if (index == -1)
                    throw _error(ErrorCode.XML_PARSER_UNTERMINATED_PI);

                char ch = span[index];
                if (ch == newline) {
                    m_curLine++;
                }
                else if ((uint)(index + 1) < (uint)span.Length && span[index + 1] == '>') {
                    charsRead = charsLeft - span.Length + index + 2;
                    break;
                }

                span = span.Slice(index + 1);
            }

            int startPos = m_pos;
            m_pos += charsRead;

            if ((m_parserFlags & FLAG_IGNORE_PI) != 0 || name == "xml")
                return;

            string text = m_str.Substring(startPos, charsRead - 2);
            m_nodeStack.add(ASXML.createNode(XMLNodeType.PROCESSING_INSTRUCTION, new ASQName("", name), text));
        }

        /// <summary>
        /// Writes the character or surrogate pair for the given code point into the buffer.
        /// </summary>
        /// <param name="buffer">The buffer into which to write the character(s). This
        /// may be reallocated if there is not enough space.</param>
        /// <param name="position">The position in <paramref name="buffer"/> into which to
        /// write the character(s). The number of characters written will be added.</param>
        /// <param name="value">A Unicode code point value.</param>
        private static void _writeCodePoint(ref char[] buffer, ref int position, int value) {
            if (buffer.Length - position < 2)
                DataStructureUtil.expandArray(ref buffer, 2);

            if (value > 0xFFFF) {
                // Split into surrogate pairs.
                int offsetValue = value - 0x10000;
                buffer[position] = (char)(0xD800 | (offsetValue >> 10));
                buffer[position + 1] = (char)(0xDC00 | (offsetValue & 0x3FF));
                position += 2;
            }
            else {
                buffer[position] = (char)value;
                position++;
            }
        }

        /// <summary>
        /// Reads a text node and adds it as a child to the element currently being processed.
        /// </summary>
        private void _readText() {
            ReadOnlySpan<char> span = m_str.AsSpan(m_pos);
            int charsLeft = m_str.Length - m_pos;
            char newline = s_newLineChar;
            ReadOnlySpan<char> searchChars = stackalloc char[3] {'<', '&', newline};

            bool mayHaveEntities = false;

            while (!span.IsEmpty) {
                int index = span.IndexOfAny(searchChars);
                char charAtIndex = (index == -1) ? '\0' : span[index];

                if (charAtIndex == '<') {
                    span = span.Slice(index);
                    break;
                }
                else if (charAtIndex == '&') {
                    span = span.Slice(index);
                    mayHaveEntities = true;
                    break;
                }
                else if (charAtIndex == newline) {
                    m_curLine++;
                    span = span.Slice(index + 1);
                }
                else {
                    span = default;
                }
            }

            int charsRead = charsLeft - span.Length;
            string text;

            if (!mayHaveEntities) {
                text = ((m_parserFlags & FLAG_IGNORE_SPACE) != 0)
                    ? XMLHelper.stripWhitespace(m_str, m_pos, charsRead)
                    : m_str.Substring(m_pos, charsRead);

                if (text.Length != 0)
                    m_nodeStack.add(ASXML.createNode(XMLNodeType.TEXT, null, text));

                m_pos += charsRead;
                return;
            }

            char[] textBuffer = m_buffer;
            if (charsRead > textBuffer.Length)
                DataStructureUtil.resizeArray(ref textBuffer, textBuffer.Length, charsRead);

            m_str.CopyTo(m_pos, textBuffer, 0, charsRead);
            int textBufPos = charsRead;
            m_pos += charsRead;

            while (true) {
                char ch = m_str[m_pos];
                if (ch == '&') {
                    int entityCode = _readEntity();
                    _writeCodePoint(ref textBuffer, ref textBufPos, entityCode);
                }
                else if (ch == '<') {
                    break;
                }
                else if (ch == newline) {
                    m_curLine++;
                    m_pos++;
                }

                span = m_str.AsSpan(m_pos);
                int nextIndex = span.IndexOfAny(searchChars);
                int charsToCopy = (nextIndex == -1) ? span.Length : nextIndex;

                if (textBuffer.Length - textBufPos < charsToCopy)
                    DataStructureUtil.resizeArray(ref textBuffer, textBufPos, textBufPos + charsToCopy);

                span.Slice(0, charsToCopy).CopyTo(textBuffer.AsSpan(textBufPos));
                textBufPos += charsToCopy;
                m_pos += charsToCopy;

                if (nextIndex == -1)
                    break;
            }

            m_buffer = textBuffer;

            if (textBufPos == 0)
                return;

            text = ((m_parserFlags & FLAG_IGNORE_SPACE) != 0)
                ? XMLHelper.stripWhitespace(textBuffer, 0, textBufPos)
                : new string(textBuffer, 0, textBufPos);

            if (text.Length != 0)
                m_nodeStack.add(ASXML.createNode(XMLNodeType.TEXT, null, text));
        }

        /// <summary>
        /// Reads an attribute value and returns it.
        /// </summary>
        private string _readAttributeValue() {
            char quote = m_str[m_pos];
            if (quote != '"' && quote != '\'')
                throw _error(ErrorCode.XML_PARSER_ELEMENT_MALFORMED);

            m_pos++;

            ReadOnlySpan<char> span = m_str.AsSpan(m_pos);
            int charsLeft = m_str.Length - m_pos;
            char newline = s_newLineChar;
            ReadOnlySpan<char> searchChars = stackalloc char[] {quote, '&', '<', newline};

            bool mayHaveEntities = false;

            while (true) {
                int index = span.IndexOfAny(searchChars);
                if (index == -1)
                    throw _error(ErrorCode.XML_PARSER_UNTERMINATED_ATTR);

                char charAtIndex = span[index];
                if (charAtIndex == quote) {
                    span = span.Slice(index);
                    break;
                }
                else if (charAtIndex == '&') {
                    span = span.Slice(index);
                    mayHaveEntities = true;
                    break;
                }
                else if (charAtIndex == newline) {
                    m_curLine++;
                    span = span.Slice(index + 1);
                }
                else {
                    // Attributes cannot contain '<'.
                    throw _error(ErrorCode.XML_PARSER_ELEMENT_MALFORMED);
                }
            }

            int charsRead = charsLeft - span.Length;

            if (!mayHaveEntities) {
                int startPos = m_pos;
                m_pos += charsRead + 1;     // +1 for the closing quote
                return m_str.Substring(startPos, charsRead);
            }

            char[] textBuffer = m_buffer;

            if (textBuffer.Length < charsRead)
                DataStructureUtil.resizeArray(ref textBuffer, textBuffer.Length, charsRead);

            m_str.CopyTo(m_pos, textBuffer, 0, charsRead);
            m_pos += charsRead;
            int textBufPos = charsRead;

            while (true) {
                char ch = m_str[m_pos];
                if (ch == '&') {
                    int entityCode = _readEntity();
                    _writeCodePoint(ref textBuffer, ref textBufPos, entityCode);
                }
                else if (ch == quote) {
                    break;
                }
                else if (ch == '<') {
                    throw _error(ErrorCode.XML_PARSER_ELEMENT_MALFORMED);
                }
                else if (ch == newline) {
                    m_curLine++;
                    m_pos++;
                }

                span = m_str.AsSpan(m_pos);
                int nextIndex = span.IndexOfAny(searchChars);

                if (nextIndex == -1)
                    throw _error(ErrorCode.XML_PARSER_UNTERMINATED_ATTR);

                if (textBuffer.Length - textBufPos < nextIndex)
                    DataStructureUtil.resizeArray(ref textBuffer, textBufPos, textBufPos + nextIndex);

                span.Slice(0, nextIndex).CopyTo(textBuffer.AsSpan(textBufPos));
                textBufPos += nextIndex;

                m_pos += nextIndex;
            }

            m_pos++;    // For closing quote
            m_buffer = textBuffer;

            return (textBufPos == 0) ? "" : new string(textBuffer, 0, textBufPos);
        }

        /// <summary>
        /// Reads a CDATA section and adds it as a text node child to the element currently being
        /// processed.
        /// </summary>
        private void _readCDATA() {
            m_pos += 9;

            ReadOnlySpan<char> span = m_str.AsSpan(m_pos);
            int charsLeft = m_str.Length - m_pos;
            char newline = s_newLineChar;
            ReadOnlySpan<char> searchChars = stackalloc char[] {']', newline};

            int charsRead;

            while (true) {
                int index = span.IndexOfAny(searchChars);
                if (index == -1)
                    throw _error(ErrorCode.XML_PARSER_UNTERMINATED_CDATA);

                char charAtIndex = span[index];

                if (charAtIndex == newline) {
                    m_curLine++;
                }
                else {
                    if ((uint)(index + 2) >= (uint)span.Length)
                        throw _error(ErrorCode.XML_PARSER_UNTERMINATED_COMMENT);

                    if (span[index + 1] == ']' && span[index + 2] != '>') {
                        charsRead = charsLeft - span.Length + index + 3;
                        break;
                    }
                }

                span = span.Slice(index + 1);
            }

            int startPos = m_pos;
            m_pos += charsRead;

            if (charsRead == 3)
                return;

            m_nodeStack.add(ASXML.createNode(XMLNodeType.CDATA, null, m_str.Substring(startPos, charsRead - 3)));
        }

        /// <summary>
        /// Reads the start tag of an element.
        /// </summary>
        private void _readStartTag() {
            char ch;

            m_pos++;
            if (!_readName(out string prefix, out string localName)) {
                throw _error(
                    ErrorCode.MARIANA__XML_PARSER_INVALID_NAME,
                    (prefix == null) ? localName : prefix + ":" + localName
                );
            }

            if (m_str.Length == m_pos)
                throw _error(ErrorCode.XML_PARSER_UNTERMINATED_ELEMENT);

            m_nsInScopePtrs.add(m_nsInScope.length);

            while (true) {
                if (!_goToNextNonSpace())
                    throw _error(ErrorCode.XML_PARSER_UNTERMINATED_ELEMENT);

                ch = m_str[m_pos];
                if (ch == '/' || ch == '>')
                    break;

                if (!XMLHelper.isWhitespaceChar(m_str[m_pos - 1]))
                    // An attribute must be preceded by at least one whitespace character
                    throw _error(ErrorCode.XML_PARSER_ELEMENT_MALFORMED);

                _readAttribute();
            }

            bool isSelfClosing = false;
            ch = m_str[m_pos];
            if (ch == '/') {
                isSelfClosing = true;
                m_pos++;
            }

            if (m_pos == m_str.Length || m_str[m_pos] != '>')
                throw _error(ErrorCode.XML_PARSER_UNTERMINATED_ELEMENT);

            m_pos++;

            StackItem parserStackItem = new StackItem();

            ASNamespace elementNS = _resolvePrefix(prefix);
            if (elementNS == null)
                throw _error(ErrorCode.XML_PREFIX_NOT_BOUND, prefix, localName);

            parserStackItem.elementName = new ASQName(elementNS, localName);
            parserStackItem.attributes = _resolveAttributes(parserStackItem.elementName);

            int nsDeclStart = m_nsInScopePtrs[m_nsInScopePtrs.length - 1];
            int nsDeclCount = m_nsInScope.length - nsDeclStart;

            parserStackItem.nsDecls = (nsDeclCount == 0)
                ? Array.Empty<ASNamespace>()
                : m_nsInScope.asSpan(nsDeclStart, nsDeclCount).ToArray();

            if (isSelfClosing) {
                if (m_parserStack.length == 0)
                    parserStackItem.nsDecls = _addImplicitNSDeclsToRoot(parserStackItem.nsDecls);

                ASXML element = ASXML.internalCreateElement(
                    parserStackItem.elementName,
                    parserStackItem.attributes,
                    ReadOnlySpan<ASXML>.Empty,
                    parserStackItem.nsDecls
                );

                m_nodeStack.add(element);

                m_nsInScopePtrs.removeLast();
                m_nsInScope.removeRange(nsDeclStart, nsDeclCount);
            }
            else {
                parserStackItem.childrenStartAt = m_nodeStack.length;
                m_parserStack.add(parserStackItem);
            }
        }

        /// <summary>
        /// Reads an attribute.
        /// </summary>
        private void _readAttribute() {
            int curLine = m_curLine;

            if (!_readName(out string prefix, out string localName)) {
                throw _error(
                    ErrorCode.MARIANA__XML_PARSER_INVALID_NAME,
                    (prefix == null) ? localName : prefix + ":" + localName
                );
            }

            if (m_str.Length - m_pos < 2 || m_str[m_pos] != '=')
                throw _error(ErrorCode.XML_PARSER_UNTERMINATED_ATTR);

            m_pos++;
            string attrValue = _readAttributeValue();

            if (prefix == null && localName == "xmlns") {
                _addNamespaceDecl(ASNamespace.unsafeCreate("", attrValue));
            }
            else if (prefix == "xmlns") {
                if (attrValue.Length == 0)
                    throw ErrorHelper.createError(ErrorCode.XML_ILLEGAL_PREFIX_PUBLIC_NAMESPACE, localName);
                _addNamespaceDecl(ASNamespace.unsafeCreate(localName, attrValue));
            }
            else {
                // Attribute nodes canot be created at this point; this is because their prefixes
                // can refer to xmlns declarations after it on the same element tag. So they
                // must be stored as UnresolvedAttribute until the entire start tag is parsed,
                // after which their namespace URIs can be resolved and the attribute nodes created.
                m_unresolvedAttrs.add(new UnresolvedAttribute {
                    lineNumber = curLine,
                    prefix = prefix,
                    localName = localName,
                    value = attrValue,
                });
            }
        }

        /// <summary>
        /// Reads an end tag. This completes the element currently being processed.
        /// </summary>
        private void _readEndTag() {
            if (m_parserStack.length == 0)
                throw _error(ErrorCode.XML_MARKUP_AFTER_ROOT);

            m_pos += 2;
            if (!_readName(out string prefix, out string localName)) {
                throw _error(
                    ErrorCode.MARIANA__XML_PARSER_INVALID_NAME,
                    (prefix == null) ? localName : prefix + ":" + localName);
            }

            if (!_goToNextNonSpace() || m_str[m_pos] != '>')
                throw _error(ErrorCode.XML_PARSER_ELEMENT_MALFORMED);

            m_pos++;

            StackItem parserStackItem = m_parserStack[m_parserStack.length - 1];

            // Check that the end tag matches the corresponding start tag.
            ASNamespace prefixNS = _resolvePrefix(prefix);
            if (parserStackItem.elementName.localName != localName
                || prefixNS == null
                || parserStackItem.elementName.uri != prefixNS.uri)
            {
                throw _error(ErrorCode.XML_ELEMENT_NOT_TERMINATED, parserStackItem.elementName.AS_toString());
            }

            if (m_parserStack.length == 1)
                parserStackItem.nsDecls = _addImplicitNSDeclsToRoot(parserStackItem.nsDecls);

            // Create the element.
            int childCount = m_nodeStack.length - parserStackItem.childrenStartAt;
            ASXML element = ASXML.internalCreateElement(
                parserStackItem.elementName,
                parserStackItem.attributes,
                m_nodeStack.asSpan(parserStackItem.childrenStartAt, childCount),
                parserStackItem.nsDecls
            );

            m_nodeStack.removeRange(parserStackItem.childrenStartAt, childCount);
            m_nodeStack.add(element);

            m_parserStack.removeLast();

            int nsDeclStart = m_nsInScopePtrs[m_nsInScopePtrs.length - 1];
            m_nsInScopePtrs.removeLast();
            m_nsInScope.removeRange(nsDeclStart, m_nsInScope.length - nsDeclStart);
        }

        /// <summary>
        /// Reads a DOCTYPE declaration.
        /// </summary>
        private void _readDoctype() {
            // Ensure that the DOCTYPE appears at the top level of the document.
            if (m_parserStack.length != 0)
                throw _error(ErrorCode.MARIANA__XML_PARSER_INVALID_DOCTYPE);

            m_pos += 2;
            if (String.Compare(m_str, m_pos, "doctype", 0, 7, StringComparison.OrdinalIgnoreCase) != 0)
                throw _error(ErrorCode.MARIANA__XML_PARSER_INVALID_DOCTYPE);
            m_pos += 7;
            if (!_goToNextNonSpace())
                throw _error(ErrorCode.XML_PARSER_UNTERMINATED_DOCTYPE);

            if (!XMLHelper.isWhitespaceChar(m_str[m_pos - 1]))
                // At least one whitespace must follow DOCTYPE
                throw _error(ErrorCode.MARIANA__XML_PARSER_INVALID_DOCTYPE);

            // We don't parse DTDs for now. Only check the balance of square brackets.

            ReadOnlySpan<char> span = m_str.AsSpan(m_pos);
            int charsLeft = m_str.Length - m_pos;
            char newline = s_newLineChar;
            ReadOnlySpan<char> searchChars = stackalloc char[] {'[', ']', '>', newline};

            int charsRead;
            int balance = 0;

            while (true) {
                int index = span.IndexOfAny(searchChars);
                if (index == -1)
                    throw _error(ErrorCode.XML_PARSER_UNTERMINATED_DOCTYPE);

                char charAtIndex = span[index];

                if (charAtIndex == newline) {
                    m_curLine++;
                }
                else if (charAtIndex == '>') {
                    if (balance != 0)
                        throw _error(ErrorCode.MARIANA__XML_PARSER_INVALID_DOCTYPE);

                    charsRead = charsLeft - span.Length + index + 1;
                    break;
                }
                else if (charAtIndex == '[') {
                    balance++;
                }
                else {
                    balance--;
                    if (balance < 0)
                        throw _error(ErrorCode.MARIANA__XML_PARSER_INVALID_DOCTYPE);
                }

                span = span.Slice(index + 1);
            }

            m_pos += charsRead;
        }

        /// <summary>
        /// Resolves a namespace prefix using the namespaces that are currently in scope.
        /// </summary>
        /// <returns>The resolved namespace. If there is no namespace in scope with the given
        /// prefix, returns null.</returns>
        /// <param name="prefix">The prefix to resolve.</param>
        private ASNamespace _resolvePrefix(string prefix) {
            for (int i = m_nsInScope.length - 1; i >= 0; i--) {
                string nsPrefix = m_nsInScope[i].prefix;
                if (nsPrefix == prefix || (nsPrefix.Length == 0 && prefix == null))
                    return m_nsInScope[i];
            }

            if (prefix == null || prefix.Length == 0) {
                if (m_defaultNS.uri.Length != 0)
                    m_parserFlags |= FLAG_USES_DEFAULT_NS;
                return m_defaultNS;
            }

            if (prefix == "xml") {
                m_parserFlags |= FLAG_USES_XML_NS;
                return s_namespaceForXml;
            }

            return null;
        }

        /// <summary>
        /// Adds a namespace declaration to the declarations currently in scope.
        /// </summary>
        /// <param name="ns">The namespace declaration to add.</param>
        private void _addNamespaceDecl(ASNamespace ns) {
            // Check if the namespace is declared by an ancestor.
            for (int i = m_nsInScopePtrs[m_nsInScopePtrs.length - 1] - 1; i >= 0; i--) {
                ASNamespace parentNs = m_nsInScope[i];
                if (parentNs.prefix != ns.prefix)
                    continue;
                if (parentNs.uri == ns.uri)
                    return;
                break;
            }

            // Check if there is a duplicate declaration on this node.
            for (int i = m_nsInScopePtrs[m_nsInScopePtrs.length - 1], n = m_nsInScope.length; i < n; i++) {
                if (ns.prefix == m_nsInScope[i].prefix)
                    throw _error(ErrorCode.MARIANA__XML_PARSER_DUPLICATE_NS_DECL);
            }

            m_nsInScope.add(ns);
        }

        /// <summary>
        /// Adds the implicit namespace declarations for the root element for the default
        /// namespace and the "xml" namespace, if they are used in the document.
        /// </summary>
        /// <returns>The namespace declaration array with the implicit declarations added.</returns>
        /// <param name="rootNSDecls">The namespace declarations to which to add the implicit
        /// declarations.</param>
        private ASNamespace[] _addImplicitNSDeclsToRoot(ASNamespace[] rootNSDecls) {
            int nsDeclSize = rootNSDecls.Length;
            if ((m_parserFlags & FLAG_USES_XML_NS) != 0)
                nsDeclSize++;
            if ((m_parserFlags & FLAG_USES_DEFAULT_NS) != 0)
                nsDeclSize++;

            if (nsDeclSize == rootNSDecls.Length)
                return rootNSDecls;

            var newNSDecls = new ASNamespace[nsDeclSize];
            int i = 0;

            for (; i < rootNSDecls.Length; i++)
                newNSDecls[i] = rootNSDecls[i];

            if ((m_parserFlags & FLAG_USES_XML_NS) != 0)
                newNSDecls[i++] = s_namespaceForXml;
            if ((m_parserFlags & FLAG_USES_DEFAULT_NS) != 0)
                newNSDecls[i++] = m_defaultNS;

            return newNSDecls;
        }

        /// <summary>
        /// Creates the attribute nodes for the current element and resolves attribute prefixes.
        /// </summary>
        /// <returns>An array containing the created attributes.</returns>
        /// <param name="elementName">The name of the element.</param>
        private ASXML[] _resolveAttributes(ASQName elementName) {
            if (m_unresolvedAttrs.length == 0)
                return Array.Empty<ASXML>();

            ASXML[] resolvedAttrs = new ASXML[m_unresolvedAttrs.length];

            for (int i = 0, n = m_unresolvedAttrs.length; i < n; i++) {
                string prefix = m_unresolvedAttrs[i].prefix;

                ASNamespace attrNS = (prefix == null) ? ASNamespace.@public : _resolvePrefix(prefix);
                if (attrNS == null) {
                    throw _error(
                        ErrorCode.XML_PREFIX_NOT_BOUND,
                        prefix,
                        m_unresolvedAttrs[i].localName,
                        line: m_unresolvedAttrs[i].lineNumber
                    );
                }

                ASQName attrName = new ASQName(attrNS, m_unresolvedAttrs[i].localName);

                // Check for duplicate attributes
                for (int j = 0; j < i; j++) {
                    if (ASQName.AS_equals(attrName, resolvedAttrs[j].name())) {
                        throw _error(
                            ErrorCode.XML_ATTRIBUTE_DUPLICATE,
                            attrName.AS_toString(),
                            elementName.AS_toString(),
                            line: m_unresolvedAttrs[i].lineNumber
                        );
                    }
                }

                resolvedAttrs[i] = ASXML.internalCreateAttribute(attrName, m_unresolvedAttrs[i].value);
            }

            m_unresolvedAttrs.clear();
            return resolvedAttrs;
        }

        /// <summary>
        /// Creates an exception object to be thrown from the given error code and arguments.
        /// </summary>
        /// <returns>The <see cref="AVM2Exception"/> instance.</returns>
        /// <param name="code">The error code.</param>
        /// <param name="arg1">The first argument in the error message.</param>
        /// <param name="arg2">The second argument in the error message.</param>
        /// <param name="line">The line number at which the error occurred. If not set, the
        /// current line number is used.</param>
        private AVM2Exception _error(ErrorCode code, string arg1 = null, string arg2 = null, int line = -1) {
            if (line == -1)
                line = m_curLine;

            object[] args;
            if (arg2 != null)
                args = new object[] {arg1, arg2, line};
            else if (arg1 != null)
                args = new object[] {arg1, line};
            else
                args = new object[] {line};

            return ErrorHelper.createError(code, args);
        }

    }
}
