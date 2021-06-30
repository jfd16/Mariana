using System;
using System.Collections.Generic;
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

        private ASJSON() { }

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
            HashSet<string> nameFilter = null;
            ASFunction replacerFunc = null;
            string spaceString = null;

            if (replacer is ASArray replacerArr) {
                nameFilter = new HashSet<string>(StringComparer.Ordinal);

                for (uint i = 0; i < replacerArr.length; i++) {
                    ASObject prop = replacerArr[i].value;
                    if (prop is ASString || ASObject.AS_isNumeric(prop))
                        nameFilter.Add((string)prop);
                }
            }
            else {
                replacerFunc = replacer as ASFunction;
                if (replacerFunc == null && replacer != null)
                    throw ErrorHelper.createError(ErrorCode.JSON_INVALID_REPLACER);
            }

            if (ASObject.AS_isNumeric(space)) {
                double numSpaces = Math.Min((double)space, 10.0);
                if (numSpaces >= 1.0) {
                    prettyPrint = true;
                    spaceString = new string(' ', (int)numSpaces);
                }
            }
            else if (space is ASString) {
                spaceString = (string)space;
                if (spaceString.Length > 10)
                    spaceString = spaceString.Substring(0, 10);
                prettyPrint = spaceString.Length != 0;
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
                NULL_INPUT,
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
            private static readonly DynamicArray<string> s_errorMessages = new DynamicArray<string>(16, true) {
                [(int)_Error.NULL_INPUT] =
                    "Input string is null.",
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
                public string propName;
                public bool isArray;
                public int stackBaseIndex;
            }

            private struct _Property {
                public string name;
                public ASObject value;
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

            private DynamicArray<_StackItem> m_stack;

            private DynamicArray<ASObject> m_arrayStack;

            private DynamicArray<_Property> m_propertyStack;

            private _State m_curState;

            internal Parser(ASFunction reviver = null) : this() {
                m_reviver = reviver;
                m_reviverArgs = new ASAny[2];
                m_stringBuffer = new char[128];
                m_namePool = new NamePool();
            }

            internal ASObject parseString(string str) {
                if (str == null)
                    throw _error(_Error.NULL_INPUT);

                m_str = str;
                m_pos = 0;
                m_curLine = 1;
                m_curPropName = null;

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

                if (_goToNextNonSpace()) {
                    // Junk after root object.
                    throw _error(_Error.EOS_EXPECTED);
                }

                if (m_reviver != null) {
                    // Pass the root through the reviver
                    ASObject reviverThis = new ASObject();
                    reviverThis.AS_dynamicProps.setValue("", m_rootObject);
                    m_rootObject = _callReviver(reviverThis, "", m_rootObject).value;
                }

                return m_rootObject;
            }

            private void _state_object() {
                char ch = m_str[m_pos];

                if (ch == '{') {
                    m_stack.add(new _StackItem {propName = m_curPropName, isArray = false, stackBaseIndex = m_propertyStack.length});
                    m_curState = _State.FIRST_PROP_OR_END;
                    m_pos++;
                }
                else if (ch == '[') {
                    m_stack.add(new _StackItem {propName = m_curPropName, isArray = true, stackBaseIndex = m_arrayStack.length});
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
                if (m_stack.length == 0) {
                    m_rootObject = obj;
                    m_curState = _State.END_OF_STRING;
                }
                else if (m_stack[m_stack.length - 1].isArray) {
                    m_arrayStack.add(obj);
                    m_curState = _State.NEXT_ARRAY_ELEM_OR_END;
                }
                else {
                    m_propertyStack.add(new _Property {name = m_curPropName, value = obj});
                    m_curState = _State.NEXT_PROP_OR_END;
                }
            }

            private ASAny _callReviver(ASObject obj, string key, ASObject value) {
                m_reviverArgs[0] = key;
                m_reviverArgs[1] = value;
                return m_reviver.AS_invoke(obj, m_reviverArgs);
            }

            private void _endCurrentObject() {
                ref _StackItem topOfStack = ref m_stack[m_stack.length - 1];

                ASObject obj;

                if (topOfStack.isArray) {
                    int arrayLength = m_arrayStack.length - topOfStack.stackBaseIndex;
                    var arrayElements = m_arrayStack.asSpan(topOfStack.stackBaseIndex, arrayLength);
                    var array = ASArray.fromObjectSpan(arrayElements);

                    if (m_reviver != null) {
                        for (int i = 0; i < arrayLength; i++)
                            array[i] = _callReviver(array, ASint.AS_convertString(i), arrayElements[i]).value;
                    }

                    obj = array;
                    m_arrayStack.removeRange(topOfStack.stackBaseIndex, arrayLength);
                }
                else {
                    int propCount = m_propertyStack.length - topOfStack.stackBaseIndex;
                    var properties = m_propertyStack.asSpan(topOfStack.stackBaseIndex, propCount);

                    obj = new ASObject();
                    DynamicPropertyCollection objProps = obj.AS_dynamicProps;

                    for (int i = 0; i < properties.Length; i++)
                        objProps.setValue(properties[i].name, properties[i].value);

                    if (m_reviver != null) {
                        // We need to pass the object properties through the reviver, and delete the properties
                        // from the object for which the reviver returns undefined.

                        // To avoid any problems that may arise from deleting properties from the object
                        // while iterating over it, this is done in two passes: First, we overwrite
                        // the segment of the property buffer that held the parsed key-value pairs with those
                        // that are actually on the object (this excludes any properties that were not written
                        // because of repeated keys), and then we iterate over the properties in the buffer and
                        // mutate the object with the reviver results.

                        int curIndex = objProps.getNextIndex(-1);
                        int effectivePropCount = 0;

                        while (curIndex != -1) {
                            properties[effectivePropCount] = new _Property {
                                name = objProps.getNameFromIndex(curIndex),
                                value = objProps.getValueFromIndex(curIndex).value
                            };
                            effectivePropCount++;
                            curIndex = objProps.getNextIndex(curIndex);
                        }

                        properties = properties.Slice(0, effectivePropCount);

                        for (int i = 0; i < properties.Length; i++) {
                            _Property prop = properties[i];
                            ASAny reviverResult = _callReviver(obj, prop.name, prop.value);

                            if (reviverResult.isUndefined) {
                                objProps.delete(prop.name);
                            }
                            else if (reviverResult.value != prop.value) {
                                // Skip redundant writes when the reviver returns the same value.
                                objProps.setValue(prop.name, reviverResult.value);
                            }
                        }
                    }

                    m_propertyStack.removeRange(topOfStack.stackBaseIndex, propCount);
                }

                m_curPropName = topOfStack.propName;

                m_stack.removeLast();
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
                    if (span.IsEmpty)
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
                        m_pos += totalCharsRead + 1;   // +1 for closing quote

                        return new string(buffer, 0, bufpos);
                    }

                    if (bufsize == bufpos) {
                        DataStructureUtil.expandArray(ref buffer);
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

            private enum _State {
                VISIT,
                ENTER,
                LEAVE,
                NEXT,
                END,
            }

            private struct _StackItem {

                public readonly ASObject obj;
                public readonly ASArray array;
                public readonly ASVectorAny vector;

                public readonly ReadOnlyArrayView<Trait> traits;
                public readonly ReadOnlyArrayView<Trait> staticTraits;
                public readonly DynamicPropertyCollection dynamicProps;

                public readonly int arrayLength;

                public int propWrittenCount;
                public int curTraitIndex;
                public int curArrayOrDynPropIndex;

                public bool isArrayOrVector => arrayLength != -1;
                public int traitCount => traits.length + staticTraits.length;

                public _StackItem(ASObject obj) : this() {
                    this.obj = obj;
                    this.arrayLength = -1;
                    this.curTraitIndex = -1;
                    this.curArrayOrDynPropIndex = -1;

                    this.dynamicProps = obj.AS_dynamicProps;
                    this.traits = obj.AS_class.getTraits(TraitType.ALL, TraitScope.INSTANCE);

                    if (obj is ASClass classObj)
                        this.staticTraits = classObj.internalClass.getTraits(TraitType.ALL, TraitScope.STATIC);
                    else if (obj is ASGlobalObject globalObj)
                        this.staticTraits = globalObj.applicationDomain.getGlobalTraits(TraitType.ALL, noInherited: true);
                }

                public _StackItem(ASArray array) : this() {
                    this.obj = array;
                    this.array = array;
                    this.arrayLength = (int)Math.Min(array.length, (uint)Int32.MaxValue);
                    this.curTraitIndex = -1;
                    this.curArrayOrDynPropIndex = -1;
                }

                public _StackItem(ASVectorAny vector) : this() {
                    this.obj = vector;
                    this.vector = vector;
                    this.arrayLength = vector.length;
                    this.curTraitIndex = -1;
                    this.curArrayOrDynPropIndex = -1;
                }

                public Trait nextWritableTrait() {
                    int traitCount = this.traitCount;

                    int curIndex;
                    Trait curTrait = null;

                    for (curIndex = curTraitIndex + 1; curIndex < traitCount; curIndex++) {
                        Trait trait = (curIndex >= traits.length)
                            ? staticTraits[curIndex - traits.length]
                            : traits[curIndex];

                        if (!trait.name.ns.isPublic)
                            continue;

                        if ((trait.traitType & (TraitType.FIELD | TraitType.CONSTANT | TraitType.CLASS)) != 0
                            || (trait is PropertyTrait prop && prop.getter != null))
                        {
                            curTrait = trait;
                            break;
                        }
                    }

                    curTraitIndex = curIndex;
                    return curTrait;
                }

            }

            // The toJSON method name.
            private static readonly QName s_toJSONMethodName = QName.publicName("toJSON");

            private bool m_prettyPrint;

            private string m_indentString1, m_indentString2, m_indentString4;

            private DynamicArray<string> m_parts;

            private HashSet<string> m_nameFilter;

            private ASFunction m_replacer;

            private char[] m_stringBuffer;

            private ASAny[] m_toJSONArgs;

            private ASAny[] m_replacerArgs;

            private DynamicArray<_StackItem> m_stack;

            private ReferenceSet<ASObject> m_currentPathSet;

            private _State m_curState;

            private string m_curPropName;

            private ASAny m_curObject;

            private ASObject[] m_primitveToJSONFuncs;

            internal Stringifier(bool prettyPrint, string indent, HashSet<string> nameFilter, ASFunction replacer) : this() {
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
                    Class.fromType(typeof(double)).prototypeObject.AS_getProperty(s_toJSONMethodName).value,
                    Class.fromType(typeof(string)).prototypeObject.AS_getProperty(s_toJSONMethodName).value,
                    Class.fromType(typeof(bool)).prototypeObject.AS_getProperty(s_toJSONMethodName).value,
                };

                m_currentPathSet = new ReferenceSet<ASObject>();
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
                if (m_stack.length == 0)
                    return "";

                if (m_stack[m_stack.length - 1].isArrayOrVector)
                    return ASint.AS_convertString(m_stack[m_stack.length - 1].curArrayOrDynPropIndex);

                return m_curPropName;
            }

            private void _state_visit() {
                bool parentIsArray = m_stack.length != 0 && m_stack[m_stack.length - 1].isArrayOrVector;

                // Attempt to call toJSON.
                if (_tryCallToJSONOnCurrentObject(out ASAny toJSONResult))
                    m_curObject = toJSONResult;

                // If a replacer is given, pass the current key-value pair through it.
                if (m_replacer != null) {
                    m_replacerArgs[0] = _getCurrentKeyString();
                    m_replacerArgs[1] = m_curObject;

                    ASObject parentObj;
                    if (m_stack.length != 0) {
                        parentObj = m_stack[m_stack.length - 1].obj;
                    }
                    else {
                        parentObj = new ASObject();
                        parentObj.AS_dynamicProps.setValue("", m_curObject);
                    }

                    m_curObject = m_replacer.AS_invoke(parentObj, m_replacerArgs);
                }

                ASObject curObj = m_curObject.value;

                if (m_curObject.isUndefined || curObj is ASFunction) {
                    // If the current object is undefined or a function, skip it if it is an object property.
                    // If it is an array element, write null instead.
                    if (m_stack.length != 0 && !parentIsArray) {
                        m_curState = _State.NEXT;
                        return;
                    }
                    curObj = null;
                }

                if (m_stack.length != 0) {
                    if (m_stack[m_stack.length - 1].propWrittenCount != 0)
                        m_parts.add(",");

                    m_stack[m_stack.length - 1].propWrittenCount++;
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

            private bool _tryCallToJSONOnCurrentObject(out ASAny result) {
                if (m_curObject.isUndefinedOrNull) {
                    result = default;
                    return false;
                }

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
                if (!m_currentPathSet.add(m_curObject.value))
                    throw ErrorHelper.createError(ErrorCode.JSON_CYCLIC_STRUCTURE);

                ASObject curObj = m_curObject.value;

                if (curObj is ASArray curObjAsArray) {
                    m_stack.add(new _StackItem(curObjAsArray));
                    m_parts.add("[");
                }
                else if (curObj is ASVectorAny curObjAsVec) {
                    m_stack.add(new _StackItem(curObjAsVec));
                    m_parts.add("[");
                }
                else {
                    m_stack.add(new _StackItem(curObj));
                    m_parts.add("{");
                }

                m_curState = _State.NEXT;
            }

            private void _state_next() {
                if (m_stack.length == 0) {
                    m_curState = _State.END;
                    return;
                }

                ref _StackItem topOfStack = ref m_stack[m_stack.length - 1];

                if (topOfStack.isArrayOrVector) {
                    // The current object is an array or vector. Move to the next element, or
                    // leave if there are no more elements.

                    int nextIndex = topOfStack.curArrayOrDynPropIndex + 1;

                    if ((uint)nextIndex >= (uint)topOfStack.arrayLength) {
                        m_curState = _State.LEAVE;
                    }
                    else {
                        topOfStack.curArrayOrDynPropIndex = nextIndex;
                        m_curState = _State.VISIT;

                        m_curObject = (topOfStack.vector != null)
                            ? topOfStack.vector.AS_getElement(nextIndex)
                            : topOfStack.array.AS_getElement((uint)nextIndex);
                    }
                    return;
                }

                ASObject obj = topOfStack.obj;

                if (topOfStack.curTraitIndex < topOfStack.traitCount) {
                    // Check if there are any traits from the object's class that can be written.
                    Trait trait = topOfStack.nextWritableTrait();

                    if (trait != null) {
                        m_curPropName = trait.name.localName;

                        if (m_nameFilter != null && !m_nameFilter.Contains(m_curPropName)) {
                            m_curState = _State.NEXT;
                        }
                        else {
                            m_curObject = trait.getValue(trait.isStatic ? ASAny.undefined : obj);
                            m_curState = _State.VISIT;
                        }
                        return;
                    }
                }

                // No more traits, so move on to the object's dynamic properties.

                DynamicPropertyCollection dynProps = topOfStack.dynamicProps;
                int nextDynPropIndex = (dynProps != null) ? dynProps.getNextIndex(topOfStack.curArrayOrDynPropIndex) : -1;

                if (nextDynPropIndex == -1) {
                    // No more dynamic properties, so leave the current object.
                    m_curState = _State.LEAVE;
                    return;
                }

                topOfStack.curArrayOrDynPropIndex = nextDynPropIndex;
                m_curPropName = dynProps.getNameFromIndex(nextDynPropIndex);

                if (m_nameFilter != null && !m_nameFilter.Contains(m_curPropName)) {
                    m_curState = _State.NEXT;
                }
                else {
                    m_curObject = dynProps.getValueFromIndex(nextDynPropIndex);
                    m_curState = _State.VISIT;
                }
            }

            private void _state_leave() {
                ref _StackItem topOfStack = ref m_stack[m_stack.length - 1];

                bool isArray = topOfStack.isArrayOrVector;
                bool isEmpty = topOfStack.propWrittenCount == 0;

                // Pop the current item off the stack.
                m_currentPathSet.delete(topOfStack.obj);
                m_stack.removeLast();

                // Only write newline and indentation if the object written is not empty.
                if (!isEmpty) {
                    if (m_stack.length != 0)
                        _writeIndent();
                    else if (m_prettyPrint)
                        m_parts.add("\n");
                }

                m_parts.add(isArray ? "]" : "}");
                m_curState = _State.NEXT;
            }

            /// <summary>
            /// Inserts indentation, if pretty printing is enabled.
            /// </summary>
            private void _writeIndent() {
                int depth = m_stack.length;
                if (depth == 0 || !m_prettyPrint)
                    return;

                m_parts.add("\n");

                while (depth >= 4) {
                    m_parts.add(m_indentString4);
                    depth -= 4;
                }
                switch (depth) {
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
                    if (c < 0x20 || c == '"' || c == '\\') {
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
                    char ch = str[i];
                    bool isControl = ch < 0x20;
                    bool isEscaped = isControl || ch == '"' || ch == '\\';

                    int escapedCount = 1;
                    if (isEscaped)
                        escapedCount = isControl ? 6 : 2;

                    if (bufSize - bufPos < escapedCount) {
                        DataStructureUtil.expandArray(ref buffer, escapedCount);
                        bufSize = buffer.Length;
                    }

                    if (!isEscaped) {
                        buffer[bufPos++] = ch;
                        continue;
                    }

                    buffer[bufPos++] = '\\';

                    switch (ch) {
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
                                buffer[bufPos + 3] = (char)((ch >> 4) + '0');
                                buffer[bufPos + 4] = (char)(((ch & 15) < 10) ? (ch & 15) + '0' : (ch & 15) - 10 + 'a');
                                bufPos += 5;
                            }
                            else {
                                buffer[bufPos++] = ch;
                            }
                            break;
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
