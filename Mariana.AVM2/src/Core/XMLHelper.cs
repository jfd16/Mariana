using System;
using System.Runtime.CompilerServices;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    internal static class XMLHelper {

        private static readonly char[] s_escapeElemChars = {'<', '>', '&', '"'};

        // IndexOfAny is optimized for <= 5 characters, so we split the attribute
        // escape chars (6) into two sets.
        private static readonly char[] s_escapeAttrChars1 = {'<', '&', '"'};
        private static readonly char[] s_escapeAttrChars2 = {'\n', '\r', '\t'};

        /// <summary>
        /// Checks if the given span contains a valid name for an XML element or attribute, or a namespace
        /// prefix.
        /// </summary>
        /// <param name="span">The span to check.</param>
        /// <returns>True if <paramref name="span"/> contains a valid name, otherwise false.</returns>
        public static bool isValidName(ReadOnlySpan<char> span) => getValidNamePrefixLength(span) == span.Length;

        /// <summary>
        /// Computes the length of the longest prefix of the given span that is a valid XML name.
        /// </summary>
        /// <returns>The length of the longest prefix of <paramref name="span"/> that is a valid
        /// XML name, or 0 if no prefix of that span is a valid XML name.</returns>
        /// <param name="span">The span containing an XML name as a prefix.</param>
        public static int getValidNamePrefixLength(ReadOnlySpan<char> span) {
            if (span.IsEmpty)
                return 0;

            int i = 0;
            bool isValid = true;

            while (isValid) {
                // Using unsigned comparisons for bounds-check elimination.
                if ((uint)i >= (uint)span.Length)
                    break;

                char ch = span[i];

                if ((ch & 0xFC00) == 0xD800) {
                    if ((uint)(i + 1) >= (uint)span.Length) {
                        isValid = false;
                    }
                    else {
                        char trail = span[i + 1];
                        // XML allows code points outside the BMP up to 0xEFFFF, which means that
                        // the upper 3 bits of the code point before the 0x10000 bias is added
                        // must not be all 1s.
                        isValid = (trail & 0xFC00) == 0xDC00 && (ch & 0x380) != 0x380;
                    }

                    if (isValid)
                        i += 2;
                }
                else {
                    isValid = _isValidChar(ch, i != 0);
                    if (isValid)
                        i++;
                }
            }

            return i;
        }

        /// <summary>
        /// Checks if the given character is valid in an XML name.
        /// </summary>
        /// <param name="c">The character.</param>
        /// <param name="notStart">For the first character in a name, set this to false. Otherwise,
        /// set this to true.</param>
        /// <returns>True if the character is valid in an XML name, otherwise false.</returns>
        private static bool _isValidChar(char c, bool notStart) {
            // See: https://www.w3.org/TR/REC-xml/#sec-common-syn

            if ((c & ~0xFF) == 0) {
                // Single-byte characters
                if ((uint)(c - 'a') <= ('z' - 'a') || (uint)(c - 'A') <= ('Z' - 'A') || c == '_')
                    return true;
                if (((uint)(c - '-') <= ('9' - '-') && c != '/') || c == 0xB7)
                    return notStart;
                return c >= 0xC0 && c != 0xD7 && c != 0xF7;
            }

            if ((c & ~0x1FFF) == 0) {
                if (c <= 0x02FF)
                    return true;
                if (c <= 0x036F)
                    return notStart;
                return c != 0x037E;
            }

            if (c <= 0xD7FF) {
                if (c == 0x200C || c == 0x200D)
                    return true;
                if (c == 0x203F || c == 0x2040)
                    return notStart;
                return (uint)(c - 0x2070) <= 0x218F - 0x2070 || (uint)(c - 0x2C00) <= 0x2FEF - 0x2C00 || c >= 0x3001;
            }

            if (c <= 0xDFFF)
                return false;

            return (uint)(c - 0xF900) <= 0xFDCF - 0xF900 || (uint)(c - 0xFDF0) <= 0xFFFD - 0xFDF0;
        }

        /// <summary>
        /// Returns true if the given character is an XML whitespace character.
        /// </summary>
        /// <param name="c">The character to check</param>
        /// <returns>True if <paramref name="c"/> is a whitespace character, otherwise false.</returns>
        /// <remarks>
        /// The following characters are considered white space by this function: space (0x20),
        /// newline (0x0A), carriage return (0x0D) and tab (0x09).
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool isWhitespaceChar(char c) {
            const int offset = 9;
            const uint max = ' ' - offset;
            const int mask = 1 << (' ' - offset) | 1 << ('\n' - offset) | 1 << ('\r' - offset) | 1 << ('\t' - offset);
            int d = c - offset;
            return (uint)d <= max && ((1 << d) & mask) != 0;
        }

        /// <summary>
        /// Escapes a substring of the given string for use as the value of an XML element or attribute.
        /// </summary>
        /// <param name="s">The string to escape.</param>
        /// <param name="start">The index of the first character of the substring of
        /// <paramref name="s"/> to be escaped.</param>
        /// <param name="length">The length of the substring of <paramref name="s"/> to
        /// be escaped.</param>
        /// <param name="isAttr">If set to true, use attribute escaping rules; otherwise, use text
        /// escaping rules.</param>
        /// <returns>The escaped form of the substring of <paramref name="s"/> defined by
        /// <paramref name="start"/> and <paramref name="length"/>.</returns>
        ///
        /// <remarks>
        /// <para>The following characters are escaped:</para>
        /// <list type="bullet">
        /// <item><description><c>&lt;</c> and <c>&amp;</c>, irrespective of the value of
        /// <paramref name="isAttr"/>.</description></item>
        /// <item><description><c>&gt;</c>, if <paramref name="isAttr"/> is false.</description></item>
        /// <item><description><c>&quot;</c>, 0x09 (tab), 0x0A (newline) and 0x0D (carriage return), if
        /// <paramref name="isAttr"/> is true.</description></item>
        /// </list>
        /// <para>If <paramref name="isAttr"/> is true, this function escapes strings for
        /// double-quoted attributes. (The <c>&apos;</c> character is not escaped)</para>
        /// </remarks>
        public static string escape(string s, int start, int length, bool isAttr) {
            char[] buffer = null;
            return escape(s, start, length, ref buffer, isAttr);
        }

        /// <summary>
        /// Escapes a string for use as the value of an XML element or attribute.
        /// </summary>
        ///
        /// <param name="str">The string to escape.</param>
        /// <param name="start">The index of the first character of the substring of
        /// <paramref name="str"/> to be escaped.</param>
        /// <param name="length">The length of the substring of <paramref name="str"/> to
        /// be escaped.</param>
        /// <param name="buffer">A character buffer for temporarily holding the escaped string during
        /// escaping. This is passed by the XML string formatter.</param>
        /// <param name="isAttr">If set to true, use attribute escaping rules; otherwise, use text
        /// escaping rules.</param>
        ///
        /// <returns>The escaped string.</returns>
        ///
        /// <remarks>
        /// <para>The following characters are escaped:</para>
        /// <list type="bullet">
        /// <item><description><c>&lt;</c> and <c>&amp;</c>, irrespective of the value of
        /// <paramref name="isAttr"/>.</description></item>
        /// <item><description><c>&gt;</c>, if <paramref name="isAttr"/> is false.</description></item>
        /// <item><description><c>&quot;</c>, 0x09 (tab), 0x0A (newline) and 0x0D (carriage return), if
        /// <paramref name="isAttr"/> is true.</description></item>
        /// </list>
        /// <para>If <paramref name="isAttr"/> is true, this function escapes strings for
        /// double-quoted attributes. (The <c>&apos;</c> character is not escaped)</para>
        /// </remarks>
        public static string escape(string str, int start, int length, ref char[] buffer, bool isAttr) {
            ReadOnlySpan<char> span = str.AsSpan(start, length);

            ReadOnlySpan<char> ec1 = isAttr ? s_escapeAttrChars1 : s_escapeElemChars;
            ReadOnlySpan<char> ec2 = isAttr ? s_escapeAttrChars2 : default;

            int findNextEscapeChar(
                in ReadOnlySpan<char> _span, in ReadOnlySpan<char> _ec1, in ReadOnlySpan<char> _ec2)
            {
                if (_ec2.IsEmpty)
                    return _span.IndexOfAny(_ec1);

                int index1 = _span.IndexOfAny(_ec1);
                if (index1 == -1)
                    return _span.IndexOfAny(_ec2);

                int index2 = _span.Slice(0, index1).IndexOfAny(_ec2);
                return (index2 == -1) ? index1 : Math.Min(index1, index2);
            }

            int indexOfFirstEscapeChar = findNextEscapeChar(span, ec1, ec2);

            if (indexOfFirstEscapeChar == -1)
                return (start == 0 && length == str.Length) ? str : str.Substring(start, length);

            int bufferInitLength = length + 24;

            if (buffer == null)
                buffer = new char[bufferInitLength];
            else if (buffer.Length < bufferInitLength)
                DataStructureUtil.resizeArray(ref buffer, buffer.Length, bufferInitLength);

            str.CopyTo(start, buffer, 0, indexOfFirstEscapeChar);

            int bufPos = indexOfFirstEscapeChar;
            int bufLen = buffer.Length;

            span = span.Slice(indexOfFirstEscapeChar);

            while (!span.IsEmpty) {
                string esc = null;
                switch (span[0]) {
                    case '&':
                        esc = "&amp;";
                        break;
                    case '<':
                        esc = "&lt;";
                        break;
                    case '>':
                        esc = "&gt;";
                        break;
                    case '"':
                        esc = "&quot;";
                        break;
                    case '\x09':
                        esc = "&#x9;";
                        break;
                    case '\x0A':
                        esc = "&#xA;";
                        break;
                    case '\x0D':
                        esc = "&#xD;";
                        break;
                }

                if (bufLen - bufPos < esc.Length) {
                    DataStructureUtil.expandArray(ref buffer, esc.Length);
                    bufLen = buffer.Length;
                }

                Span<char> bufferSpan = buffer.AsSpan(bufPos, esc.Length);
                for (int i = 0; i < esc.Length; i++)
                    bufferSpan[i] = esc[i];

                bufPos += esc.Length;

                // Find the next escape character

                span = span.Slice(1);
                if (span.IsEmpty)
                    break;

                int nextIndex = findNextEscapeChar(span, ec1, ec2);
                ReadOnlySpan<char> copyChars = (nextIndex == -1) ? span : span.Slice(0, nextIndex);

                if (bufLen - bufPos < copyChars.Length) {
                    DataStructureUtil.resizeArray(ref buffer, bufLen, bufPos + copyChars.Length);
                    bufLen = buffer.Length;
                }
                copyChars.CopyTo(buffer.AsSpan(bufPos));
                bufPos += copyChars.Length;

                if (nextIndex == -1)
                    break;

                span = span.Slice(nextIndex);
            }

            return new string(buffer, 0, bufPos);
        }

        /// <summary>
        /// Gets the start index and length of the substring contained within another substring
        /// of the given character span, with leading and trailing whitespace removed.
        /// </summary>
        /// <param name="span">The character span.</param>
        /// <param name="stripStart">The index of the first character of the substring of
        /// <paramref name="span"/> with no leading and trailing white space.</param>
        /// <param name="stripLength">The length of the substring of <paramref name="span"/>
        /// with no leading and trailing white space.</param>
        /// <remarks>
        /// The following characters are considered white space by this function: space (0x20),
        /// newline (0x0A), carriage return (0x0D) and tab (0x09).
        /// </remarks>
        public static void getStripWhitespaceBounds(
            ReadOnlySpan<char> span, out int stripStart, out int stripLength)
        {
            int tStart = -1, tEnd = -1;

            for (int i = 0; i < span.Length; i++) {
                if (!isWhitespaceChar(span[i])) {
                    tStart = i;
                    break;
                }
            }

            if (tStart == -1) {
                (stripStart, stripLength) = (0, 0);
                return;
            }

            for (int i = span.Length - 1; i >= 0; i--) {
                if (!isWhitespaceChar(span[i])) {
                    tEnd = i + 1;
                    break;
                }
            }

            stripStart = tStart;
            stripLength = tEnd - tStart;
        }

        /// <summary>
        /// Returns a substring of the given string with leading and trailing whitespace removed.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <param name="start">The index of the first character of the substring of <paramref name="s"/>
        /// from which to trim white space.</param>
        /// <param name="length">The length of the substring.</param>
        /// <returns>The substring of <paramref name="s"/>, starting at <paramref name="start"/> and having
        /// length <paramref name="length"/>, with leading and trailing white space removed.</returns>
        /// <remarks>
        /// The following characters are considered whitespace by this function: space (0x20),
        /// newline (0x0A), carriage return (0x0D) and tab (0x09).
        /// </remarks>
        public static string stripWhitespace(string s, int start, int length) {
            getStripWhitespaceBounds(s.AsSpan(start, length), out int stripStart, out int stripLength);
            if (stripLength == 0)
                return "";

            stripStart += start;
            return (stripStart == 0 && stripLength == s.Length) ? s : s.Substring(stripStart, stripLength);
        }

        /// <summary>
        /// Returns a substring of the given character array with leading and trailing whitespace removed.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <param name="start">The index of the first character of the substring of <paramref name="s"/>
        /// from which to trim white space.</param>
        /// <param name="length">The length of the substring.</param>
        /// <returns>The substring of <paramref name="s"/>, starting at <paramref name="start"/> and having
        /// length <paramref name="length"/>, with leading and trailing white space removed.</returns>
        /// <remarks>
        /// The following characters are considered whitespace by this function: space (0x20),
        /// newline (0x0A), carriage return (0x0D) and tab (0x09).
        /// </remarks>
        public static string stripWhitespace(char[] s, int start, int length) {
            getStripWhitespaceBounds(s.AsSpan(start, length), out int stripStart, out int stripLength);
            if (stripLength == 0)
                return "";

            return new string(s, start + stripStart, stripLength);
        }

        /// <summary>
        /// Returns a value indicating whether a given character span contains
        /// only white space.
        /// </summary>
        /// <returns>True if <paramref name="span"/> consists of only whitespace characters, otherwise
        /// false.</returns>
        /// <param name="span">The character span.</param>
        /// <remarks>
        /// The following characters are considered whitespace by this function: space (0x20),
        /// newline (0x0A), carriage return (0x0D) and tab (0x09).
        /// </remarks>
        public static bool isOnlyWhitespace(ReadOnlySpan<char> span) {
            for (int i = 0; i < span.Length; i++) {
                if (!isWhitespaceChar(span[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Converts an ActionScript object to a namespace for use by E4X functions.
        /// </summary>
        /// <returns>The namespace.</returns>
        /// <param name="obj">The <see cref="ASAny"/> instance to convert to a namespace.</param>
        public static ASNamespace objectToNamespace(ASAny obj) {
            var ns = obj.value as ASNamespace;
            if (ns == null && obj.value is ASQName qname)
                ns = qname.getNamespace();

            if (ns != null)
                return ns;

            string uri = ASAny.AS_convertString(obj);
            return (uri.Length == 0) ? ASNamespace.@public : new ASNamespace(uri);
        }

        /// <summary>
        /// Converts an ActionScript object to a QName for use by E4X functions.
        /// </summary>
        /// <returns>The QName object.</returns>
        /// <param name="obj">The <see cref="ASAny"/> instance to convert to a QName.</param>
        /// <param name="isAttr">Set this to true if the QName is to be used as an attribute name.</param>
        public static ASQName objectToQName(ASAny obj, bool isAttr) {
            if (obj.isUndefined)
                return new ASQName("");

            if (obj.value is ASQName qname)
                return qname;

            if (obj.value is ASNamespace nameSpace)
                return new ASQName(nameSpace, "");

            string localName = ASAny.AS_convertString(obj);

            if (localName.Length == 1 && localName[0] == '*')
                return ASQName.any;

            return new ASQName(isAttr ? ASNamespace.@public : ASNamespace.getDefault(), localName);
        }

        /// <summary>
        /// Converts an object to a string that must be set as the value of an attribute when
        /// the object is assigned to it.
        /// </summary>
        /// <returns>The string that is to be set as the attribute value.</returns>
        /// <param name="obj">The object value that is being assigned to the attribute.</param>
        public static string objectToAttrString(ASAny obj) {
            // See: ECMA-357, sec. 9.1.1.2, [[Put]], steps 6b and 6c

            if (!(obj.value is ASXMLList xmlList))
                return ASAny.AS_convertString(obj);

            int nListItems = xmlList.length();

            switch (nListItems) {
                case 0:
                    return "";
                case 1:
                    return (string)xmlList[0];
                case 2:
                    return (string)xmlList[0] + " " + (string)xmlList[1];
                default: {
                    var strs = new string[nListItems];
                    for (int i = 0; i < strs.Length; i++)
                        strs[i] = (string)xmlList[i];

                    return String.Join(" ", strs);
                }
            }
        }

        /// <summary>
        /// Returns the string value of an XML text node or a non-XML object.
        /// </summary>
        /// <param name="value">An </param>
        ///
        /// <returns>If <paramref name="value"/> is an XML text or attribute node or an XMLList containing
        /// one such node, returns the node's value. If <paramref name="value"/> is not an XML or XMLList,
        /// returns <paramref name="value"/> converted to a string. Otherwise, returns null.</returns>
        ///
        /// <remarks>This method does not consider CDATA nodes to be text nodes.</remarks>
        public static string tryGetStringFromObjectOrNode(ASAny value) {
            ASXML valueXml = value.value as ASXML;
            ASXMLList valueXmlList = value.value as ASXMLList;

            if (valueXmlList != null && valueXmlList.length() == 1)
                valueXml = valueXmlList[0];

            if (valueXml != null && (valueXml.isText || valueXml.isAttribute))
                return valueXml.nodeText;

            if (valueXml == null && valueXmlList == null)
                return ASAny.AS_convertString(value);

            return null;
        }

        /// <summary>
        /// Concatenates two XML and/or XMLList objects.
        /// </summary>
        /// <returns>The XMLList resulting from the concatenation of the two operands.</returns>
        /// <param name="o1">The first XML or XMLList object.</param>
        /// <param name="o2">The second XML or XMLList object.</param>
        public static ASXMLList concatenateXMLObjects(ASObject o1, ASObject o2) {
            ASXML xml1 = o1 as ASXML, xml2 = o2 as ASXML;

            if (xml1 != null && xml2 != null)
                return new ASXMLList(new ASXML[] {xml1, xml2}, 2, noCopy: true);

            ASXMLList xmlList1 = o1 as ASXMLList, xmlList2 = o2 as ASXMLList;

            int totalLength = 0;
            totalLength = checked(totalLength + ((xml1 != null) ? 1 : xmlList1.length()));
            totalLength = checked(totalLength + ((xml2 != null) ? 1 : xmlList2.length()));

            var newListItems = new DynamicArray<ASXML>(totalLength);

            if (xml1 != null) {
                newListItems.add(xml1);
            }
            else {
                for (int i = 0, n = xmlList1.length(); i < n; i++)
                    newListItems.add(xmlList1[i]);
            }

            if (xml2 != null) {
                newListItems.add(xml2);
            }
            else {
                for (int i = 0, n = xmlList2.length(); i < n; i++)
                    newListItems.add(xmlList2[i]);
            }

            return new ASXMLList(newListItems.getUnderlyingArray(), newListItems.length, noCopy: true);
        }

        /// <summary>
        /// Compares two objects using the definition of the weak equality operator,
        /// where at least one of the objects is an XML or XMLList.
        /// </summary>
        /// <returns>True if the two operands are equal, otherwise false.</returns>
        /// <param name="o1">The first operand.</param>
        /// <param name="o2">The second operand.</param>
        public static bool weakEquals(ASAny o1, ASAny o2) {
            ASXML xml1 = o1.value as ASXML, xml2 = o2.value as ASXML;

            if (xml1 != null && xml2 != null)
                return ASXML.AS_weakEq(xml1, xml2);

            ASXMLList xmlList1 = o1.value as ASXMLList, xmlList2 = o2.value as ASXMLList;
            if (xmlList1 != null && xmlList2 != null)
                return ASXMLList.AS_weakEq(xmlList1, xmlList2);

            if (xmlList1 != null) {
                if (xmlList1.length() == 0)
                    return o2.isUndefined;
                else if (xmlList1.length() == 1)
                    xml1 = xmlList1[0];
                else
                    return false;
            }
            else if (xmlList2 != null) {
                if (xmlList2.length() == 0)
                    return o1.isUndefined;
                else if (xmlList2.length() == 1)
                    xml2 = xmlList2[0];
                else
                    return false;
            }

            if (xml1 != null && xml2 != null)
                return ASXML.AS_weakEq(xml1, xml2);

            if (xml1 != null && xml1.hasSimpleContent())
                return xml1.internalSimpleToString() == ASAny.AS_convertString(o2);

            if (xml2 != null && xml2.hasSimpleContent())
                return xml2.internalSimpleToString() == ASAny.AS_convertString(o1);

            return false;
        }

    }
}
