using System;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Converts XML objects to formatted XML strings.
    /// </summary>
    internal struct XMLWriter {

        private struct TagStackItem {
            public string prefix;
            public string localName;
            public int nsDeclStart;
            public uint tmpPrefixIdStart;
        }

        private bool m_prettyPrint;

        private string m_indent1, m_indent2, m_indent4;

        private ASXML.DescendantEnumerator m_iterator;

        private DynamicArray<string> m_parts;

        private DynamicArray<ASNamespace> m_nsInScope;

        private DynamicArray<TagStackItem> m_tagStack;

        private uint m_nextTempPrefixId;

        private char[] m_escBuffer;

        internal string makeString(ASXML node) {
            _init();
            _fetchAncestorNamespaces(node);
            _writeNode(node);
            return String.Join("", m_parts.getUnderlyingArray(), 0, m_parts.length);
        }

        internal string makeString(ASXMLList list) {
            _init();

            for (int i = 0, n = list.length(); i < n; i++) {
                ASXML cur = list[i];
                _fetchAncestorNamespaces(cur);
                _writeNode(cur);

                m_nsInScope.clear();
                m_tagStack.clear();
                m_nextTempPrefixId = 0;
            }

            return String.Join("", m_parts.getUnderlyingArray(), 0, m_parts.length);
        }

        private void _init() {
            m_prettyPrint = ASXML.prettyPrinting;

            if (m_prettyPrint) {
                int indentSize = ASXML.prettyIndent;
                m_indent1 = new string(' ', indentSize);
                m_indent2 = m_indent1 + m_indent1;
                m_indent4 = m_indent2 + m_indent2;
            }
            else {
                m_indent1 = null;
                m_indent2 = null;
                m_indent4 = null;
            }

            m_parts.clear();
            m_nsInScope.clear();
            m_tagStack.clear();

            m_nextTempPrefixId = 0;
        }

        private void _fetchAncestorNamespaces(ASXML node) {
            if (node.nodeType != XMLNodeType.ELEMENT)
                return;

            for (ASXML cur = node.parent(); cur != null; cur = cur.parent())
                cur.internalGetNamespaceDecls(ref m_nsInScope);

            if (m_nsInScope.length != 0)
                m_nsInScope.asSpan().Reverse();
        }

        private void _writeNode(ASXML node) {
            m_iterator = node.getDescendantEnumerator(true);

            while (m_iterator.MoveNext()) {
                while (m_tagStack.length != m_iterator.currentDepth)
                    _exitCurrentElement();

                ASXML cur = m_iterator.Current;
                if (cur.nodeType == XMLNodeType.ELEMENT) {
                    _enterElement(cur);
                    continue;
                }

                _writeIndent();

                switch (cur.nodeType) {
                    case XMLNodeType.TEXT:
                    case XMLNodeType.ATTRIBUTE:
                        _writeText(cur.nodeText);
                        break;

                    case XMLNodeType.COMMENT:
                        m_parts.add("<!--");
                        m_parts.add(cur.nodeText);
                        m_parts.add("-->");
                        break;

                    case XMLNodeType.PROCESSING_INSTRUCTION:
                        m_parts.add("<?");
                        m_parts.add(cur.name().localName);
                        m_parts.add(" ");
                        m_parts.add(cur.nodeText);
                        m_parts.add("?>");
                        break;

                    case XMLNodeType.CDATA: {
                        string text = cur.nodeText;
                        if (text.IndexOf("]]>", StringComparison.Ordinal) != -1) {
                            // If a CDATA node contains "]]>", it would be invalid XML when output as CDATA.
                            // So output it as an ordinary text node with proper escaping.
                            _writeText(text);
                        }
                        else {
                            m_parts.add("<![CDATA[");
                            m_parts.add(text);
                            m_parts.add("]]>");
                        }
                        break;
                    }
                }
            }

            while (m_tagStack.length != 0)
                _exitCurrentElement();
        }

        /// <summary>
        /// Writes a text value. The text is escaped and leading and trailing whitespace is
        /// removed if pretty printing is enabled.
        /// </summary>
        /// <param name="text">The text value to write.</param>
        private void _writeText(string text) {
            int start = 0, length = text.Length;

            if (m_prettyPrint) {
                XMLHelper.getStripWhitespaceBounds(text.AsSpan(start, length), out int nonWsStart, out length);
                start += nonWsStart;
            }

            if (length != 0)
                m_parts.add(XMLHelper.escape(text, start, length, ref m_escBuffer, false));
        }

        /// <summary>
        /// Writes a name.
        /// </summary>
        /// <param name="prefix">The prefix component of the name. If this is an empty string,
        /// no prefix is written.</param>
        /// <param name="local">The local component of the name.</param>
        private void _writeName(string prefix, string local) {
            if (prefix.Length != 0) {
                m_parts.add(prefix);
                m_parts.add(":");
            }
            m_parts.add(local);
        }

        /// <summary>
        /// Writes a new line and appropriate indentation, if pretty printing is enabled.
        /// </summary>
        private void _writeIndent() {
            if (!m_prettyPrint)
                return;

            if (m_parts.length != 0)
                m_parts.add("\n");

            int curDepth = m_tagStack.length;

            while (curDepth >= 4) {
                m_parts.add(m_indent4);
                curDepth -= 4;
            }
            if (curDepth >= 2) {
                m_parts.add(m_indent2);
                curDepth -= 2;
            }
            if (curDepth >= 1) {
                m_parts.add(m_indent1);
            }
        }

        private void _enterElement(ASXML elem) {
            _writeIndent();

            ASQName elemName = elem.internalGetName();
            TagStackItem stackItem = new TagStackItem();

            stackItem.tmpPrefixIdStart = m_nextTempPrefixId;
            stackItem.nsDeclStart = m_nsInScope.length;
            elem.internalGetNamespaceDecls(ref m_nsInScope);

            stackItem.localName = elemName.localName;
            stackItem.prefix = _getPrefix(elemName, false);

            m_parts.add("<");
            _writeName(stackItem.prefix, stackItem.localName);

            foreach (ASXML attr in elem.getAttributeEnumerator()) {
                ASQName attrName = attr.internalGetName();
                string attrValue = attr.nodeText;

                m_parts.add(" ");
                _writeName(_getPrefix(attrName, true), attrName.localName);
                m_parts.add("=\"");
                m_parts.add(XMLHelper.escape(attrValue, 0, attrValue.Length, ref m_escBuffer, true));
                m_parts.add("\"");
            }

            // If this is the root we must include the ancestor namespaces as well.
            int outNsDeclStart = (m_tagStack.length == 0) ? 0 : stackItem.nsDeclStart;
            for (int i = outNsDeclStart, n = m_nsInScope.length; i < n; i++) {
                ASNamespace nsdecl = m_nsInScope[i];

                if (nsdecl.prefix.Length != 0) {
                    m_parts.add(" xmlns:");
                    m_parts.add(nsdecl.prefix);
                    m_parts.add("=\"");
                }
                else {
                    m_parts.add(" xmlns=\"");
                }

                m_parts.add(XMLHelper.escape(nsdecl.uri, 0, nsdecl.uri.Length, ref m_escBuffer, true));
                m_parts.add("\"");
            }

            ASXML firstChild = elem.getChildAtIndex(0);

            if (firstChild == null) {
                // No children, so self close.
                m_parts.add("/>");
                m_nsInScope.removeRange(stackItem.nsDeclStart, m_nsInScope.length - stackItem.nsDeclStart);
                return;
            }

            if (firstChild.nodeType == XMLNodeType.TEXT && elem.getChildAtIndex(1) == null) {
                m_parts.add(">");
                _writeText(firstChild.nodeText);
                m_parts.add("</");
                _writeName(stackItem.prefix, stackItem.localName);
                m_parts.add(">");
                m_iterator.stepOverCurrentNode();
                return;
            }

            m_parts.add(">");
            m_tagStack.add(stackItem);
        }

        private void _exitCurrentElement() {
            TagStackItem stackItem = m_tagStack[m_tagStack.length - 1];
            m_tagStack.removeLast();

            _writeIndent();
            m_parts.add("</");
            _writeName(stackItem.prefix, stackItem.localName);
            m_parts.add(">");

            m_nextTempPrefixId = stackItem.tmpPrefixIdStart;
        }

        /// <summary>
        /// Gets a prefix from the current context for the given name.
        /// </summary>
        /// <returns>A namespace prefix for the name.</returns>
        /// <param name="qname">A qualified name.</param>
        /// <param name="isAttr">Set this to true if <paramref name="qname"/> is the name of
        /// an attribute.</param>
        private string _getPrefix(ASQName qname, bool isAttr) {
            if (qname.prefix != null)
                return qname.prefix;

            for (int i = m_nsInScope.length - 1; i >= 0; i--) {
                ASNamespace match = m_nsInScope[i];

                if (match.uri != qname.uri || (match.prefix.Length == 0 && isAttr))
                    continue;

                // Found a candidate prefix, check if it is hidden by another one declared
                // between the current element and the one declaring the prefix.

                bool isHidden = false;
                for (int j = m_nsInScope.length - 1; j > i; j--) {
                    if (match.prefix != m_nsInScope[j].prefix)
                        continue;

                    if (match.uri != m_nsInScope[j].uri) {
                        isHidden = true;
                        break;
                    }
                }

                if (isHidden)
                    continue;

                return match.prefix;
            }

            // No prefixes are available, so generate a temporary one.
            // The temporary prefix is only used in the output string, it does not get stored in the element.

            while (true) {
                string tmpPrefix = "_p" + ASuint.AS_convertString(m_nextTempPrefixId + 1);
                m_nextTempPrefixId++;

                bool mayConflict = false;
                for (int i = m_nsInScope.length - 1; i >= 0 && !mayConflict; i--)
                    mayConflict |= tmpPrefix == m_nsInScope[i].prefix;

                if (mayConflict)
                    continue;

                m_nsInScope.add(ASNamespace.unsafeCreate(tmpPrefix, qname.uri));
                return tmpPrefix;
            }
        }

    }
}
