using System;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The JSON class contains methods for converting strings in the JavaScript Object Notation
    /// (JSON) format to ActionScript 3 objects, and for serializing ActionScript 3 objects as
    /// JSON.
    /// </summary>
    [AVM2ExportClass(name = "JSON")]
    public sealed class ASJSON : ASObject {

        /// <exclude/>
        /// <summary>
        /// This constructor throws an exception to ensure that the class cannot be instantiated.
        /// </summary>
        [AVM2ExportTrait]
        public ASJSON(RestParam rest = default) {
            throw ErrorHelper.createError(ErrorCode.CLASS_CANNOT_BE_INSTANTIATED, "JSON");
        }

        /// <summary>
        /// Parses a JSON string and returns an ActionScript 3 object.
        /// </summary>
        ///
        /// <param name="str">The string to parse.</param>
        /// <param name="reviver">
        /// An optional reviver function. This function must take two arguments: a property name and a
        /// value. Each object, after being created, is passed to this function, along with the
        /// property name (or array index for array elements) that it will be set to. The
        /// return value of the function will be used in place of the created object. If the function
        /// returns undefined for any property, that property will not be set.
        /// </param>
        ///
        /// <returns>The object created from the JSON string.</returns>
        [AVM2ExportTrait]
        public static ASObject parse(string str, ASFunction reviver = null) {
            var parser = new Parser(reviver);
            return parser.parseString(str);
        }

        /// <summary>
        /// Creates a JSON string from the specified object.
        /// </summary>
        /// <param name="obj">The object to convert to a JSON string.</param>
        /// <param name="replacer">This may be a function or an array. (See remarks below)</param>
        /// <param name="space">This may be a string or an integer. (See remarks below)</param>
        /// <returns>The JSON string.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>TypeError #1131: <paramref name="replacer"/> is not an Array or Function.</description></item>
        /// <item><description>TypeError #1129: <paramref name="obj"/> contains a circular reference.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// <para>
        /// If the <paramref name="replacer"/> argument is an array, it acts as a property name
        /// filter. Only object properties whose names are in that array will be output. If it is a
        /// function, it must take two arguments: a property name and a value. Each value is passed to
        /// this function along with its property name (or numeric index, in case of array elements),
        /// and the return value is used in the output in its place. If the function returns undefined
        /// for any property or array element, it is excluded from the output. If this argument is of
        /// any other type, an error is thrown.
        /// </para>
        /// <para>
        /// If the <paramref name="space"/> argument is a string, it is used as an indent, and is
        /// repeated for each level of indentation. It must not be longer than 10 characters,
        /// otherwise only the first 10 characters are used. If this string consists of characters
        /// other than white space, invalid JSON may be produced. A null string disables indentation.
        /// If this parameter is an integer, it indicates the number of spaces to be used for
        /// indentation (clamped to a maximum of 10). If this parameter is neither a string nor of
        /// a numeric type, it is converted to a string and used as the indent.
        /// </para>
        /// <para>
        /// For any object other than Array, Vector, Boolean, Number, int, uint or String, the object
        /// will be represented by a JSON object in the output containing all the dynamic properties
        /// of the object (for instances of dynamic classes), along with all public fields and
        /// read-only and read-write properties declared by the object's class. Properties in the
        /// object's prototype chain are not considered. This behaviour can be overridden for any
        /// class by defining a <c>toJSON</c> method at the class or prototype level.
        /// </para>
        /// </remarks>
        [AVM2ExportTrait]
        public static string stringify(ASObject obj, ASObject replacer = null, ASObject space = null) {
            bool prettyPrint = false;
            string[] nameFilter = null;
            ASFunction replacerFunc = null;
            string spaceString = null;

            if (replacer is ASArray replacerArr) {
                nameFilter = replacerArr.toTypedArray<string>();
            }
            else {
                replacerFunc = replacer as ASFunction;
                if (replacerFunc == null && replacer != null)
                    throw ErrorHelper.createError(ErrorCode.JSON_INVALID_REPLACER);
            }

            if (ASObject.AS_isNumeric(space)) {
                int numSpaces = (int)space;
                if (numSpaces > 0) {
                    prettyPrint = true;
                    spaceString = new string(' ', Math.Min(numSpaces, 10));
                }
            }
            else {
                spaceString = (string)space;
                if (spaceString != null) {
                    if (spaceString.Length > 10)
                        spaceString = spaceString.Substring(0, 10);
                    prettyPrint = spaceString.Length != 0;
                }
            }

            var stringifier = new Stringifier(prettyPrint, spaceString, nameFilter, replacerFunc);
            return stringifier.makeJSONString(obj);
        }

        /// <summary>
        /// Parses JSON strings to native ActionScript objects.
        /// </summary>
        private struct Parser {

            /// <summary>
            /// The parser error codes.
            /// </summary>
            private enum _Error {
                EOS_EXPECTED,
                EOS_NOT_EXPECTED,
                WRONG_BRACE_ARRAY,
                WRONG_BRACE_OBJECT,
                EXPECT_CLOSE_BRACE_OR_NAME,
                EXPECT_CLOSE_BRACE_OR_COMMA,
                EXPECT_LITERAL_OR_OPEN_BRACE,
                INVALID_NUMBER,
                STRING_CONTROL_CHAR,
                STRING_UNICODE_ESCAPE,
                STRING_ESCAPE,
                NAME_EXPECTED,
                COLON_EXPECTED,
            }

            /// <summary>
            /// The error messages thrown by the _error() method.
            /// </summary>
            private static readonly DynamicArray<string> s_errorMessages = new DynamicArray<string>(15, true) {
                [(int)_Error.EOS_EXPECTED] =
                    "End of string expected.",
                [(int)_Error.EOS_NOT_EXPECTED] =
                    "End of string not expected.",
                [(int)_Error.WRONG_BRACE_ARRAY] =
                    "Expecting ']' to close array, found '}'.",
                [(int)_Error.WRONG_BRACE_OBJECT] =
                    "Expecting '}' to close object, found ']'.",
                [(int)_Error.EXPECT_CLOSE_BRACE_OR_NAME] =
                    "Expecting property name or '}'.",
                [(int)_Error.EXPECT_CLOSE_BRACE_OR_COMMA] =
                    "Expecting ',' or ']' or '}'.",
                [(int)_Error.EXPECT_LITERAL_OR_OPEN_BRACE] =
                    "Expecting 'true', 'false', 'null', number, string, '[' or '{'.",
                [(int)_Error.INVALID_NUMBER] =
                    "Malformatted number.",
                [(int)_Error.STRING_CONTROL_CHAR] =
                    "Illegal control character in string literal.",
                [(int)_Error.STRING_UNICODE_ESCAPE] =
                    "Invalid Unicode hexadecimal escape sequence.",
                [(int)_Error.STRING_ESCAPE] =
                    "Illegal escape sequence.",
                [(int)_Error.NAME_EXPECTED] =
                    "Property name expected.",
                [(int)_Error.COLON_EXPECTED] =
                    "Expecting ':' after property name.",
            };

            private enum _State {
                OBJECT,
                PROP_NAME,
                NEXT_PROP_OR_END,
                NEXT_ARRAY_ELEM_OR_END,
                FIRST_PROP_OR_END,
                FIRST_ARRAY_ELEM_OR_END,
                END_OF_STRING,
            }

            private struct _StackItem {
                public ASObject obj;
                public string propName;
                public int arrBufMark;
            }

            private string m_str;

            private int m_pos;

            private int m_curLine;

            private NamePool m_namePool;

            private char[] m_stringBuffer;

            private ASFunction m_reviver;

            private ASAny[] m_reviverArgs;

            private ASObject m_rootObject;

            private string m_curPropName;

            private bool m_curLevelIsArray;

            private DynamicArray<_StackItem> m_stack;

            private DynamicArray<ASObject> m_arrBuffer;

            private _State m_curState;

            internal Parser(ASFunction reviver = null) : this() {
                m_reviver = reviver;
                m_reviverArgs = new ASAny[2];
                m_stringBuffer = new char[128];
                m_namePool = new NamePool();
            }

            internal ASObject parseString(string str) {
                m_str = str;
                m_pos = 0;
                m_curLine = 1;
                m_curPropName = null;
                m_curLevelIsArray = false;

                if (str == null || str.Length == 0 || !_goToNextNonSpace())
                    // Null, empty or whitespace-only string.
                    return null;

                m_curState = _State.OBJECT;

                while (m_curState != _State.END_OF_STRING) {
                    if (!_goToNextNonSpace())
                        throw _error(_Error.EOS_NOT_EXPECTED);

                    switch (m_curState) {
                        case _State.OBJECT:
                            _state_object();
                            break;
                        case _State.PROP_NAME:
                            _state_propName();
                            break;
                        case _State.FIRST_PROP_OR_END:
                            _state_firstPropOrEnd();
                            break;
                        case _State.FIRST_ARRAY_ELEM_OR_END:
                            _state_firstArrayElemOrEnd();
                            break;
                        case _State.NEXT_PROP_OR_END:
                            _state_nextPropOrEnd();
                            break;
                        case _State.NEXT_ARRAY_ELEM_OR_END:
                            _state_nextArrayElemOrEnd();
                            break;
                    }
                }

                if (_goToNextNonSpace())
                    // Junk after root object.
                    throw _error(_Error.EOS_EXPECTED);

                return m_rootObject;
            }

            private void _state_object() {
                char ch = m_str[m_pos];

                if (ch == '{') {
                    m_stack.add(new _StackItem {obj = new ASObject(), propName = m_curPropName, arrBufMark = -1});
                    m_curLevelIsArray = false;
                    m_curState = _State.FIRST_PROP_OR_END;
                    m_pos++;
                }
                else if (ch == '[') {
                    m_stack.add(new _StackItem {obj = null, propName = m_curPropName, arrBufMark = m_arrBuffer.length});
                    m_curLevelIsArray = true;
                    m_curState = _State.FIRST_ARRAY_ELEM_OR_END;
                    m_pos++;
                }
                else if (ch == '"') {
                    _acceptObject(_readString());
                }
                else if ((uint)(ch - '0') <= 9 || ch == '-') {
                    _acceptObject(_readNumber());
                }
                else if (String.CompareOrdinal(m_str, m_pos, "false", 0, 5) == 0) {
                    m_pos += 5;
                    _acceptObject(false);
                }
                else if (String.CompareOrdinal(m_str, m_pos, "true", 0, 4) == 0) {
                    m_pos += 4;
                    _acceptObject(true);
                }
                else if (String.CompareOrdinal(m_str, m_pos, "null", 0, 4) == 0) {
                    m_pos += 4;
                    _acceptObject(null);
                }
                else {
                    throw _error(_Error.EXPECT_LITERAL_OR_OPEN_BRACE);
                }

                m_curPropName = null;
            }

            private void _state_propName() {
                const int POOL_MAX_LENGTH = 32;

                string str = m_str;
                char ch = str[m_pos];

                if (ch != '"' || m_str.Length - m_pos < 2)
                    throw _error(_Error.NAME_EXPECTED);

                int nameStartPos = m_pos + 1;
                string propName = null;

                // For short names without escape characters, a name pool is used to conserve memory.

                for (int i = 0, n = Math.Min(m_str.Length - nameStartPos, POOL_MAX_LENGTH); i < n; i++) {
                    ch = str[nameStartPos + i];
                    if (ch == '"') {
                        propName = m_namePool.getPooledValue(str.AsSpan(nameStartPos, i));
                        m_pos = nameStartPos + i + 1;
                        break;
                    }
                    else if (ch < 0x20 || ch == '\\') {
                        break;
                    }
                }

                if (propName == null)
                    propName = _readString();

                if (!_goToNextNonSpace() || m_str[m_pos] != ':')
                    throw _error(_Error.COLON_EXPECTED);

                m_pos++;
                m_curPropName = propName;
                m_curState = _State.OBJECT;
            }

            private void _state_firstPropOrEnd() {
                char ch = m_str[m_pos];

                if (ch == '}') {
                    m_pos++;
                    _endCurrentObject();
                }
                else if (ch == '"') {
                    _state_propName();
                }
                else if (ch == ']') {
                    throw _error(_Error.WRONG_BRACE_OBJECT);
                }
                else {
                    throw _error(_Error.EXPECT_CLOSE_BRACE_OR_NAME);
                }
            }

            private void _state_firstArrayElemOrEnd() {
                char ch = m_str[m_pos];

                if (ch == ']') {
                    m_pos++;
                    _endCurrentObject();
                }
                else if (ch == '}') {
                    throw _error(_Error.WRONG_BRACE_ARRAY);
                }
                else {
                    _state_object();
                }
            }

            private void _state_nextPropOrEnd() {
                char ch = m_str[m_pos];

                if (ch == '}') {
                    m_pos++;
                    _endCurrentObject();
                }
                else if (ch == ',') {
                    m_pos++;
                    m_curState = _State.PROP_NAME;
                }
                else if (ch == ']') {
                    throw _error(_Error.WRONG_BRACE_OBJECT);
                }
                else {
                    throw _error(_Error.EXPECT_CLOSE_BRACE_OR_COMMA);
                }
            }

            private void _state_nextArrayElemOrEnd() {
                char ch = m_str[m_pos];

                if (ch == ']') {
                    m_pos++;
                    _endCurrentObject();
                }
                else if (ch == ',') {
                    m_pos++;
                    m_curState = _State.OBJECT;
                }
                else if (ch == '}') {
                    throw _error(_Error.WRONG_BRACE_ARRAY);
                }
                else {
                    throw _error(_Error.EXPECT_CLOSE_BRACE_OR_COMMA);
                }
            }

            private void _acceptObject(ASObject obj) {
                bool discard = false;

                if (m_reviver != null) {
                    // Pass the object through the reviver.

                    string reviverKey;
                    if (m_stack.length == 0) {
                        // Root object
                        reviverKey = "";
                    }
                    else if (m_curLevelIsArray) {
                        int index = m_arrBuffer.length - m_stack[m_stack.length - 1].arrBufMark;
                        reviverKey = ASint.AS_convertString(index);
                    }
                    else {
                        reviverKey = m_curPropName;
                    }

                    m_reviverArgs[0] = reviverKey;
                    m_reviverArgs[1] = obj;
                    ASAny reviverReturn = m_reviver.AS_invoke(ASAny.@null, m_reviverArgs);

                    if (reviverReturn.isUndefined && !m_curLevelIsArray)
                        discard = true;

                    obj = reviverReturn.value;
                }

                if (m_stack.length == 0) {
                    if (!discard)
                        m_rootObject = obj;
                    m_curState = _State.END_OF_STRING;
                }
                else if (m_curLevelIsArray) {
                    // Array elements are never discarded.
                    m_arrBuffer.add(obj);
                    m_curState = _State.NEXT_ARRAY_ELEM_OR_END;
                }
                else {
                    if (!discard)
                        m_stack[m_stack.length - 1].obj.AS_dynamicProps[m_curPropName] = obj;
                    m_curState = _State.NEXT_PROP_OR_END;
                }
            }

            private void _endCurrentObject() {
                _StackItem stackItem = m_stack[m_stack.length - 1];
                m_stack.removeLast();

                ASObject obj;

                if (stackItem.arrBufMark != -1) {
                    // Current object is an array.
                    int arrLen = m_arrBuffer.length - stackItem.arrBufMark;
                    if (arrLen != 0) {
                        obj = ASArray.fromObjectSpan<ASObject>(m_arrBuffer.asSpan(stackItem.arrBufMark, arrLen));
                        m_arrBuffer.removeRange(stackItem.arrBufMark, arrLen);
                    }
                    else {
                        obj = new ASArray();
                    }
                }
                else {
                    // Object.
                    obj = stackItem.obj;
                }

                m_curPropName = stackItem.propName;
                m_curLevelIsArray = m_stack.length != 0 && m_stack[m_stack.length - 1].arrBufMark != -1;

                _acceptObject(obj);
            }

            /// <summary>
            /// Reads a numeric string and returns the parsed number.
            /// </summary>
            /// <returns>The number.</returns>
            private double _readNumber() {
                // JSON does not allow these in numbers:
                // - Leading zeroes
                // - Decimal point at the beginning (e.g. ".2")
                // - Positive sign
                // - Infinity/NaN (A number whose magnitude is greater than the maximum of the
                //   Number type is allowed though, and is replaced with an infinity).
                // There is no need to check all of these conditions, as this function is called
                // only when the character at the current position is a digit or a negative
                // sign.

                var span = m_str.AsSpan(m_pos);
                char ch = span[0];

                if (ch == '0' && span.Length > 1) {
                    if ((uint)(span[1] - '0') <= 9)
                        throw _error(_Error.INVALID_NUMBER);
                }
                else if (ch == '-') {
                    if (span.Length <= 1)
                        throw _error(_Error.INVALID_NUMBER);

                    ch = span[1];

                    if ((uint)(ch - '0') > 9)
                        throw _error(_Error.INVALID_NUMBER);
                    else if (ch == '0' && span.Length > 2 && (uint)(span[2] - '0') <= 9)
                        throw _error(_Error.INVALID_NUMBER);
                }

                bool isValidNumber = NumberFormatHelper.stringToDouble(
                    span, out double value, out int charsRead, strict: false, allowHex: false);

                if (!isValidNumber)
                    throw _error(_Error.INVALID_NUMBER);

                m_pos += charsRead;
                return value;
            }

            /// <summary>
            /// Reads a string, decodes any escape sequences and returns the decoded string.
            /// </summary>
            /// <returns>The decoded string.</returns>
            private string _readString() {
                char[] buffer = m_stringBuffer;
                int bufsize = buffer.Length;
                int bufpos = 0;
                var span = m_str.AsSpan(m_pos + 1);   // +1 for opening quote
                int totalCharsRead = 1;

                while (true) {
                    if ((uint)span.Length <= 0)
                        throw _error(_Error.EOS_NOT_EXPECTED);

                    char ch = span[0];
                    int charsRead = 1;

                    if (ch < 0x20)  // Control characters are not allowed.
                        throw _error(_Error.STRING_CONTROL_CHAR);

                    if (ch == '\\') {
                        if ((uint)span.Length <= 1)  // Lone backslash at the end of the string
                            throw _error(_Error.EOS_NOT_EXPECTED);

                        ch = span[1];
                        charsRead++;

                        switch (ch) {
                            // Special escape sequences
                            case 'b':
                                ch = '\b';
                                break;
                            case 'f':
                                ch = '\f';
                                break;
                            case 'n':
                                ch = '\n';
                                break;
                            case 'r':
                                ch = '\r';
                                break;
                            case 't':
                                ch = '\t';
                                break;

                            // Literal when escaped
                            case '\\':
                            case '"':
                            case '/':
                                break;

                            // Unicode hex escape sequence
                            case 'u': {
                                if (span.Length < 6)  // At least four characters required after 'u'
                                    throw _error(_Error.EOS_NOT_EXPECTED);

                                if (!URIUtil.hexToByte(span.Slice(2), out byte byte1)
                                    || !URIUtil.hexToByte(span.Slice(4), out byte byte2))
                                {
                                    throw _error(_Error.STRING_UNICODE_ESCAPE);
                                }

                                ch = (char)((byte1 << 8) | byte2);
                                charsRead += 4;
                                break;
                            }

                            default:
                                // Illegal escape sequence
                                throw _error(_Error.STRING_ESCAPE);
                        }
                    }
                    else if (ch == '"') {
                        // String terminates
                        m_stringBuffer = buffer;
                        m_pos += totalCharsRead;
                        return new string(buffer, 0, bufpos);
                    }

                    if (bufsize == bufpos) {
                        DataStructureUtil.resizeArray(ref buffer, bufsize, bufsize + 1, false);
                        bufsize = buffer.Length;
                    }

                    buffer[bufpos++] = ch;
                    span = span.Slice(charsRead);
                    totalCharsRead += charsRead;
                }
            }

            /// <summary>
            /// Moves the internal pointer to the next non-whitespace character.
            /// </summary>
            /// <returns>False if the end of the string has been reached, otherwise true.</returns>
            private bool _goToNextNonSpace() {
                const int offset = 9;
                const uint max = ' ' - offset;
                const int mask = 1 << (' ' - offset) | 1 << ('\n' - offset) | 1 << ('\r' - offset) | 1 << ('\t' - offset);

                ReadOnlySpan<char> span = m_str.AsSpan(m_pos);
                int i;
                for (i = 0; i < span.Length; i++) {
                    int c = span[i] - offset;
                    if ((uint)c > max || ((1 << c) & mask) == 0)
                        break;
                    if (c == '\n' - offset)
                        m_curLine++;
                }
                m_pos += i;
                return i < span.Length;
            }

            private AVM2Exception _error(_Error code) =>
                ErrorHelper.createError(ErrorCode.JSON_PARSE_INVALID_INPUT, s_errorMessages[(int)code], m_curLine);

        }

        /// <summary>
        /// Converts objects to JSON strings.
        /// </summary>
        private struct Stringifier {

            private enum _ObjType {
                OBJECT,
                ARRAY,
                VECTOR,
            }

            private enum _State {
                VISIT,
                ENTER,
                LEAVE,
                NEXT,
                END,
            }

            private struct _StackItem {
                public ASObject obj;
                public _ObjType objType;
                public int nPropsWritten;
                public ReadOnlyArrayView<Trait> traits;
                public DynamicPropertyCollection dynamicProps;
                public int arrayLength;
                public int curTraitIndex;
                public int curDynPropIndex;   // Array index if obj is an array/vector.
            }

            // The toJSON method name.
            private static readonly QName s_toJSONMethodName = QName.publicName("toJSON");

            private bool m_prettyPrint;

            private string m_indentString1, m_indentString2, m_indentString4;

            private DynamicArray<string> m_parts;

            private string[] m_nameFilter;

            private ASFunction m_replacer;

            private char[] m_stringBuffer;

            private ASAny[] m_toJSONArgs;

            private ASAny[] m_replacerArgs;

            private DynamicArray<_StackItem> m_stack;

            private _State m_curState;

            private string m_curPropName;

            private ASAny m_curObject;

            private ASObject[] m_primitveToJSONFuncs;

            internal Stringifier(bool prettyPrint, string indent, string[] nameFilter, ASFunction replacer) : this() {
                m_prettyPrint = prettyPrint;
                m_indentString1 = indent;
                m_indentString2 = indent + indent;
                m_indentString4 = m_indentString2 + m_indentString2;
                m_nameFilter = nameFilter;
                m_replacer = replacer;
                m_toJSONArgs = new ASAny[1];
                m_replacerArgs = new ASAny[2];

                // These toJSON functions are cached to improve performance.
                // toJSON methods for int and uint are not stored here because int
                // and uint use Number's prototype.
                m_primitveToJSONFuncs = new ASObject[] {
                    Class.fromType<double>().prototypeObject.AS_getProperty(s_toJSONMethodName).value,
                    Class.fromType<string>().prototypeObject.AS_getProperty(s_toJSONMethodName).value,
                    Class.fromType<bool>().prototypeObject.AS_getProperty(s_toJSONMethodName).value,
                };
            }

            internal string makeJSONString(ASObject obj) {
                m_curState = _State.VISIT;
                m_curObject = obj;
                m_curPropName = null;

                while (m_curState != _State.END) {
                    switch (m_curState) {
                        case _State.VISIT:
                            _state_visit();
                            break;
                        case _State.ENTER:
                            _state_enter();
                            break;
                        case _State.LEAVE:
                            _state_leave();
                            break;
                        case _State.NEXT:
                            _state_next();
                            break;
                    }
                }

                return String.Join("", m_parts.getUnderlyingArray(), 0, m_parts.length);
            }

            private string _getCurrentKeyString() {
                if (m_stack.length != 0 && m_stack[m_stack.length - 1].objType != _ObjType.OBJECT)
                    return ASint.AS_convertString(m_stack[m_stack.length - 1].curDynPropIndex);
                return m_curPropName;
            }

            private void _checkCyclicStructure() {
                for (int i = 0, n = m_stack.length; i < n; i++) {
                    if (m_curObject.value == m_stack[i].obj)
                        throw ErrorHelper.createError(ErrorCode.JSON_CYCLIC_STRUCTURE);
                }
            }

            private bool _isNameInFilter(string name) {
                var filter = m_nameFilter;
                for (int i = 0; i < filter.Length; i++) {
                    if (name == filter[i])
                        return true;
                }
                return false;
            }

            private Trait _moveNextJSONWritableTrait(ref _StackItem stackItem) {
                int traitCount = stackItem.traits.length;
                int curIndex;

                for (curIndex = stackItem.curTraitIndex + 1; curIndex < traitCount; curIndex++) {
                    Trait trait = stackItem.traits[curIndex];
                    if (!trait.name.ns.isPublic)
                        continue;

                    TraitType type = trait.traitType;

                    if ((type & (TraitType.FIELD | TraitType.CONSTANT)) != 0) {
                        stackItem.curTraitIndex = curIndex;
                        return trait;
                    }

                    if (type == TraitType.PROPERTY) {
                        PropertyTrait prop = (PropertyTrait)trait;
                        if (prop.getter != null) {
                            stackItem.curTraitIndex = curIndex;
                            return trait;
                        }
                    }
                }

                stackItem.curTraitIndex = curIndex;
                return null;
            }

            private void _state_visit() {
                bool parentIsArray = m_stack.length != 0 && m_stack[m_stack.length - 1].objType != _ObjType.OBJECT;

                // If a replacer is given, pass the current key-value pair through it.
                if (m_replacer != null) {
                    m_replacerArgs[0] = _getCurrentKeyString();
                    m_replacerArgs[1] = m_curObject;

                    ASObject parentObj = (m_stack.length == 0) ? null : m_stack[m_stack.length - 1].obj;
                    ASAny replaced = m_replacer.AS_invoke(parentObj, m_replacerArgs);

                    if (replaced.isUndefined && !parentIsArray) {
                        // Skip properties that return undefined from the replacer.
                        // Except when the parent is an array/vector, in which case null is written.
                        m_curState = _State.NEXT;
                        return;
                    }

                    m_curObject = replaced;
                }

                if (m_curObject.value != null) {
                    // Attempt to call toJSON.
                    if (_tryCallToJSON(out ASAny result)) {
                        if (result.isUndefined && !parentIsArray) {
                            m_curState = _State.NEXT;
                            return;
                        }
                        m_curObject = result;
                    }
                }

                ASObject curObj = m_curObject.value;

                if (curObj is ASFunction) {
                    // Functions must be excluded.
                    if (!parentIsArray) {
                        m_curState = _State.NEXT;
                        return;
                    }
                    curObj = null;
                }

                if (m_stack.length != 0) {
                    if (m_stack[m_stack.length - 1].nPropsWritten != 0)
                        m_parts.add(",");
                    m_stack[m_stack.length - 1].nPropsWritten++;
                }

                _writeIndent();

                if (!parentIsArray && m_stack.length != 0) {
                    _writeJSONEscapedString(m_curPropName);
                    m_parts.add(m_prettyPrint ? ": " : ":");
                }

                if (curObj == null) {
                    m_parts.add("null");
                    m_curState = _State.NEXT;
                    return;
                }

                switch (curObj.AS_class.tag) {
                    case ClassTag.INT:
                        m_parts.add(ASint.AS_convertString((int)curObj));
                        m_curState = _State.NEXT;
                        return;

                    case ClassTag.UINT:
                        m_parts.add(ASuint.AS_convertString((uint)curObj));
                        m_curState = _State.NEXT;
                        return;

                    case ClassTag.NUMBER: {
                        double val = (double)curObj;
                        m_parts.add(Double.IsFinite(val) ? ASNumber.AS_convertString(val) : "null");
                        m_curState = _State.NEXT;
                        return;
                    }

                    case ClassTag.BOOLEAN:
                        m_parts.add((bool)curObj ? "true" : "false");
                        m_curState = _State.NEXT;
                        return;

                    case ClassTag.STRING:
                        _writeJSONEscapedString((string)curObj);
                        m_curState = _State.NEXT;
                        return;
                }

                // We have an array or object that we need to recurse into.
                m_curState = _State.ENTER;
            }

            private bool _tryCallToJSON(out ASAny result) {
                ClassTag tag = m_curObject.AS_class.tag;
                ASObject func = null;

                if (ClassTagSet.numeric.contains(tag))
                    func = m_primitveToJSONFuncs[0];
                else if (tag == ClassTag.STRING)
                    func = m_primitveToJSONFuncs[1];
                else if (tag == ClassTag.BOOLEAN)
                    func = m_primitveToJSONFuncs[2];

                if (func != null) {
                    m_toJSONArgs[0] = _getCurrentKeyString();
                    return func.AS_tryInvoke(m_curObject, m_toJSONArgs, out result);
                }

                if (ClassTagSet.primitive.contains(tag)) {
                    result = default(ASAny);
                    return false;
                }

                m_toJSONArgs[0] = _getCurrentKeyString();
                BindStatus bindStatus =
                    m_curObject.value.AS_tryCallProperty(s_toJSONMethodName, m_toJSONArgs, out result);

                return bindStatus == BindStatus.SUCCESS;
            }

            private void _state_enter() {
                _StackItem stackItem;
                ASObject curObj = m_curObject.value;

                if (curObj is ASArray curObjAsArray) {
                    if (curObjAsArray.length == 0) {
                        // Empty array.
                        m_parts.add("[]");
                    }
                    else {
                        _checkCyclicStructure();
                        int arrlen = (int)Math.Min(curObjAsArray.length, (uint)Int32.MaxValue);
                        stackItem = _createStackItemForArray(curObjAsArray, _ObjType.ARRAY, arrlen);
                        m_stack.add(stackItem);
                        m_parts.add("[");
                    }
                    m_curState = _State.NEXT;
                    return;
                }

                if (curObj is ASVectorAny curObjAsVec) {
                    if (curObjAsVec.length == 0) {
                        // Empty vector.
                        m_parts.add("[]");
                    }
                    else {
                        _checkCyclicStructure();
                        stackItem = _createStackItemForArray(curObjAsVec, _ObjType.VECTOR, curObjAsVec.length);
                        m_stack.add(stackItem);
                        m_parts.add("[");
                    }
                    m_curState = _State.NEXT;
                    return;
                }

                stackItem = _createStackItemForObject(curObj);

                // Need to check whether the object is empty.
                bool isEmptyObject = true;
                if (curObj.AS_dynamicProps != null && curObj.AS_dynamicProps.count != 0) {
                    isEmptyObject = false;
                }
                else {
                    _moveNextJSONWritableTrait(ref stackItem);
                    if (stackItem.curTraitIndex < stackItem.traits.length) {
                        isEmptyObject = false;
                        // NEXT state handler will increment curTraitIndex, so decrement it here.
                        stackItem.curTraitIndex--;
                    }
                }

                if (isEmptyObject) {
                    m_parts.add("{}");
                }
                else {
                    _checkCyclicStructure();
                    m_stack.add(stackItem);
                    m_parts.add("{");
                }

                m_curState = _State.NEXT;
            }

            private _StackItem _createStackItemForArray(ASObject arrOrVec, _ObjType objType, int length) {
                _StackItem stackItem;
                stackItem.obj = arrOrVec;
                stackItem.objType = objType;
                stackItem.traits = ReadOnlyArrayView<Trait>.empty;
                stackItem.curTraitIndex = -1;
                stackItem.dynamicProps = null;
                stackItem.arrayLength = length;
                stackItem.curDynPropIndex = -1;
                stackItem.nPropsWritten = 0;
                return stackItem;
            }

            private _StackItem _createStackItemForObject(ASObject obj) {
                _StackItem stackItem;
                stackItem.objType = _ObjType.OBJECT;
                stackItem.obj = obj;
                stackItem.nPropsWritten = 0;
                stackItem.dynamicProps = obj.AS_dynamicProps;

                if (obj is ASClass objAsClass)
                    stackItem.traits = objAsClass.internalClass.getTraits(TraitType.ALL, TraitScope.STATIC);
                else
                    stackItem.traits = obj.AS_class.getTraits(TraitType.ALL, TraitScope.INSTANCE);

                stackItem.curTraitIndex = -1;
                stackItem.curDynPropIndex = -1;
                stackItem.arrayLength = -1;

                return stackItem;
            }

            private void _state_next() {
                if (m_stack.length == 0) {
                    m_curState = _State.END;
                    return;
                }

                ref _StackItem topOfStack = ref m_stack[m_stack.length - 1];
                _ObjType objType = topOfStack.objType;

                if (objType != _ObjType.OBJECT) {
                    int nextIndex = topOfStack.curDynPropIndex + 1;

                    if (nextIndex >= topOfStack.arrayLength) {
                        m_curState = _State.LEAVE;
                    }
                    else {
                        topOfStack.curDynPropIndex = nextIndex;
                        m_curState = _State.VISIT;

                        m_curObject = (objType == _ObjType.VECTOR)
                            ? ((ASVectorAny)topOfStack.obj).AS_getElement(nextIndex)
                            : ((ASArray)topOfStack.obj).AS_getElement((uint)nextIndex);
                    }
                    return;
                }

                ASObject obj = topOfStack.obj;
                int traitCount = topOfStack.traits.length;

                if (topOfStack.curTraitIndex < traitCount) {
                    Trait trait = _moveNextJSONWritableTrait(ref topOfStack);
                    if (trait != null) {
                        m_curPropName = trait.name.localName;
                        if (m_nameFilter != null && !_isNameInFilter(m_curPropName)) {
                            m_curState = _State.NEXT;
                        }
                        else {
                            m_curObject = trait.getValue(obj);
                            m_curState = _State.VISIT;
                        }
                        return;
                    }
                }

                DynamicPropertyCollection dynProps = topOfStack.dynamicProps;
                if (dynProps == null) {
                    m_curState = _State.LEAVE;
                    return;
                }

                int nextDynIndex = dynProps.getNextIndex(topOfStack.curDynPropIndex);
                if (nextDynIndex == -1) {
                    m_curState = _State.LEAVE;
                    return;
                }

                topOfStack.curDynPropIndex = nextDynIndex;
                m_curPropName = dynProps.getNameFromIndex(nextDynIndex);
                if (m_nameFilter != null && !_isNameInFilter(m_curPropName)) {
                    m_curState = _State.NEXT;
                }
                else {
                    m_curObject = dynProps.getValueFromIndex(nextDynIndex);
                    m_curState = _State.VISIT;
                }
            }

            private void _state_leave() {
                bool isArray = m_stack[m_stack.length - 1].objType != _ObjType.OBJECT;

                m_stack.removeLast();  // Pop the last item off the stack.

                if (m_stack.length != 0)
                    _writeIndent();
                else if (m_prettyPrint)
                    m_parts.add("\n");

                m_parts.add(isArray ? "]" : "}");

                m_curState = _State.NEXT;
            }

            /// <summary>
            /// Inserts indentation, if pretty printing is enabled.
            /// </summary>
            private void _writeIndent() {
                int level = m_stack.length;
                if (level == 0 || !m_prettyPrint)
                    return;

                m_parts.add("\n");
                if (m_indentString1 == null)
                    return;

                while (level >= 4) {
                    m_parts.add(m_indentString4);
                    level -= 4;
                }
                switch (level) {
                    case 1:
                        m_parts.add(m_indentString1);
                        break;
                    case 2:
                        m_parts.add(m_indentString2);
                        break;
                    case 3:
                        m_parts.add(m_indentString2);
                        m_parts.add(m_indentString1);
                        break;
                }
            }

            /// <summary>
            /// Writes an escaped and quoted JSON string into the parts list.
            /// </summary>
            /// <param name="str">The string to escape and write.</param>
            private void _writeJSONEscapedString(string str) {
                char[] buffer = m_stringBuffer;
                int bufSize = 0;
                int bufPos = 0;
                bool useBuffer = false;

                for (int i = 0; i < str.Length; i++) {
                    char c = str[i];
                    if (c < 0x20 || c == '"' || c == '\\' || c == '/') {
                        // Start writing the string into the temporary string buffer as soon as the first
                        // character that requires escaping is found. This ensures that if no character is to
                        // be escaped, the original string can be used and no memory needs to be allocated for
                        // a new string.

                        if (buffer == null) {
                            m_stringBuffer = new char[64];
                            buffer = m_stringBuffer;
                        }
                        bufSize = m_stringBuffer.Length;
                        str.CopyTo(0, buffer, 0, i);
                        bufPos = i;
                        useBuffer = true;
                        break;
                    }
                }

                if (!useBuffer) {
                    m_parts.add("\"");
                    m_parts.add(str);
                    m_parts.add("\"");
                    return;
                }

                for (int i = bufPos; i < str.Length; i++) {
                    char c = str[i];
                    bool isControl = c < 0x20;

                    if (isControl || c == '"' || c == '\\') {
                        // Character that must be escaped.
                        int maxEscapedSize = isControl ? 6 : 2;
                        if (bufSize - bufPos < maxEscapedSize)
                            DataStructureUtil.resizeArray(ref buffer, bufSize, bufPos + maxEscapedSize, false);

                        buffer[bufPos++] = '\\';
                        switch (c) {
                            case '\n':
                                buffer[bufPos++] = 'n';
                                break;
                            case '\r':
                                buffer[bufPos++] = 'r';
                                break;
                            case '\b':
                                buffer[bufPos++] = 'b';
                                break;
                            case '\t':
                                buffer[bufPos++] = 't';
                                break;
                            case '\f':
                                buffer[bufPos++] = 'f';
                                break;
                            default:
                                if (isControl) {
                                    // JSON has special escape sequences for only a few control characters,
                                    // but well-formed JSON must not have any (unescaped) control characters
                                    // in strings, so use Unicode escape sequences for these.
                                    buffer[bufPos] = 'u';
                                    buffer[bufPos + 1] = '0';
                                    buffer[bufPos + 2] = '0';
                                    buffer[bufPos + 3] = (char)((c >> 4) + '0');
                                    buffer[bufPos + 4] = (char)(((c & 15) < 10) ? (c & 15) + '0' : (c & 15) - 10 + 'a');
                                    bufPos += 5;
                                }
                                else {
                                    buffer[bufPos++] = c;
                                }
                                break;
                        }
                    }
                    else {
                        if (bufPos == bufSize) {
                            DataStructureUtil.resizeArray(ref buffer, bufSize, bufSize + 1, false);
                            bufSize = buffer.Length;
                        }
                        buffer[bufPos++] = c;
                    }
                }

                m_stringBuffer = buffer;
                m_parts.add("\"");
                m_parts.add(new string(buffer, 0, bufPos));
                m_parts.add("\"");
            }

        }

    }
}
