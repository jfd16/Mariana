using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The String class represents a sequence of Unicode characters.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// This is a boxed representation of the primitive type <see cref="String"/>. It is
    /// only used when a boxing conversion is required. This type should not be used for any other
    /// purpose, other than for using its static properties or methods of for type checking of
    /// objects. This class contains static methods for all the string functions in AS3, taking a
    /// primitive string as their first argument.
    /// </para>
    /// </remarks>
    [AVM2ExportClass(name = "String", hasPrototypeMethods = true)]
    [AVM2ExportClassInternal(tag = ClassTag.STRING, primitiveType = typeof(string))]
    public sealed class ASString : ASObject {

        /// <summary>
        /// The maximum character code for which to cache values for single characters.
        /// </summary>
        private const int SINGLE_CHAR_CACHE_RANGE = 255;

        /// <summary>
        /// An array of cached one-character strings.
        /// </summary>
        private static readonly string[] s_singleCharCachedValues = _prepareSingleCharCachedValues();

        /// <summary>
        /// The boxed representation of the empty string.
        /// </summary>
        private static LazyInitObject<ASString> s_lazyEmptyString = new LazyInitObject<ASString>(() => new ASString(""));

        /// <summary>
        /// An array of cached one-character strings in boxed form.
        /// </summary>
        private static LazyInitObject<ASObject[]> s_lazySingleCharCachedObjects =
            new LazyInitObject<ASObject[]>(() => _prepareSingleCharCachedObjects());

        private static LazyInitObject<Class> s_lazyClass = new LazyInitObject<Class>(
            () => Class.fromType(typeof(ASString)),
            recursionHandling: LazyInitRecursionHandling.RECURSIVE_CALL
        );

        private readonly string m_value;

        private ASString(string value) : base(s_lazyClass.value) {
            m_value = value;
        }

        /// <summary>
        /// Converts the current instance to a Boolean value.
        /// </summary>
        /// <returns>The Boolean value.</returns>
        protected private override bool AS_coerceBoolean() => AS_toBoolean(m_value);

        /// <summary>
        /// Converts the current instance to a floating-point number value.
        /// </summary>
        /// <returns>The floating-point number value.</returns>
        protected override double AS_coerceNumber() => AS_toNumber(m_value);

        /// <summary>
        /// Converts the current instance to an integer value.
        /// </summary>
        /// <returns>The integer value.</returns>
        protected override int AS_coerceInt() => AS_toInt(m_value);

        /// <summary>
        /// Converts the current instance to an unsigned integer value.
        /// </summary>
        /// <returns>The unsigned integer value.</returns>
        protected override uint AS_coerceUint() => AS_toUint(m_value);

        /// <summary>
        /// Converts the current instance to a string value.
        /// </summary>
        /// <returns>The string value.</returns>
        protected override string AS_coerceString() => m_value;

        /// <summary>
        /// Concatenates two strings and returns the result.
        /// </summary>
        /// <param name="s1">The first string.</param>
        /// <param name="s2">The second string.</param>
        /// <returns>The result of the concatenation of the two strings. If any of the strings is
        /// null, the string "null" is used in its place. Note that the result is consistent with
        /// the semantics of ECMAScript addition operator only if at least one operand is not
        /// null.</returns>
        public static string AS_add(string s1, string s2) => (s1 ?? "null") + (s2 ?? "null");

        /// <summary>
        /// Returns a value indicating whether the first string is less than the second
        /// according to the semantics of the ECMAScript less-than operator.
        /// </summary>
        /// <param name="s1">The first string.</param>
        /// <param name="s2">The second string.</param>
        /// <returns>True if <paramref name="s1"/> is less than <paramref name="s2"/>, otherwise
        /// false.</returns>
        public static bool AS_lessThan(string s1, string s2) {
            if (s1 != null && s2 != null)
                return String.Compare(s1, s2, StringComparison.Ordinal) < 0;

            return ASObject.AS_lessThan(s1, s2);
        }

        /// <summary>
        /// Returns a value indicating whether the first string is less than or equal to the second
        /// according to the semantics of the ECMAScript less-than-or-equals operator.
        /// </summary>
        /// <param name="s1">The first string.</param>
        /// <param name="s2">The second string.</param>
        /// <returns>True if <paramref name="s1"/> is less than or equal to <paramref name="s2"/>,
        /// otherwise false.</returns>
        public static bool AS_lessEq(string s1, string s2) {
            if (s1 != null && s2 != null)
                return String.Compare(s1, s2, StringComparison.Ordinal) <= 0;

            return ASObject.AS_lessThan(s1, s2);
        }

        /// <summary>
        /// Returns a value indicating whether the first string is greater than the second
        /// according to the semantics of the ECMAScript greater-than operator.
        /// </summary>
        /// <param name="s1">The first string.</param>
        /// <param name="s2">The second string.</param>
        /// <returns>True if <paramref name="s1"/> is less than <paramref name="s2"/>, otherwise
        /// false.</returns>
        public static bool AS_greaterThan(string s1, string s2) => AS_lessThan(s2, s1);

        /// <summary>
        /// Returns a value indicating whether the first string is greater than or equal to the second
        /// according to the semantics of the ECMAScript greater-than-or-equals operator.
        /// </summary>
        /// <param name="s1">The first string.</param>
        /// <param name="s2">The second string.</param>
        /// <returns>True if <paramref name="s1"/> is greater than or equal to <paramref name="s2"/>,
        /// otherwise false.</returns>
        public static bool AS_greaterEq(string s1, string s2) => AS_lessEq(s2, s1);

        /// <summary>
        /// Creates a string from an array of character codes (Unicode code points).
        /// </summary>
        /// <param name="codes">The character codes from which to create the string. Characters with
        /// code points greater than 0xFFFF must be encoded in their UTF-16 representation, i.e. as a
        /// surrogate pair of two characters.</param>
        /// <returns>The string.</returns>
        [AVM2ExportTrait]
        public static string fromCharCode(RestParam codes = default) {
            var codesSpan = codes.getSpan();
            char[] buffer = new char[codesSpan.Length];

            for (int i = 0; i < codesSpan.Length; i++)
                buffer[i] = (char)(int)codesSpan[i];

            return new string(buffer);
        }

        /// <summary>
        /// Gets the number of characters in the string.
        /// </summary>
        [AVM2ExportTrait]
        public int length => m_value.Length;

        /// <summary>
        /// Returns the string representation of the character at the given position in the string.
        /// </summary>
        /// <param name="index">The position.</param>
        /// <returns>The string representation of the character at position <paramref name="index"/>
        /// in the string, or an empty string if <paramref name="index"/> is out of
        /// bounds.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string charAt(int index = 0) => charAt(m_value, index);

        /// <summary>
        /// Returns the code of the character at the given position in the string.
        /// </summary>
        /// <param name="index">The position.</param>
        /// <returns>The code of the character at position <paramref name="index"/> in the string,
        /// or NaN if <paramref name="index"/> is out of bounds.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public double charCodeAt(int index = 0) => charCodeAt(m_value, index);

        /// <summary>
        /// Converts the objects in an array to strings and concatenates them to the current string.
        /// </summary>
        /// <param name="args">The arguments to concatenate to the string.</param>
        /// <returns>The concatenated string.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string concat(RestParam args) => concat(m_value, args);

        /// <summary>
        /// Returns the first position of the string <paramref name="searchStr"/> in the string
        /// after <paramref name="startIndex"/>, or -1 if <paramref name="searchStr"/> is not
        /// found in the string.
        /// </summary>
        ///
        /// <param name="searchStr">The string to match.</param>
        /// <param name="startIndex">The position from where to start searching.</param>
        /// <returns>The first position of <paramref name="searchStr"/> in the string.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public int indexOf(string searchStr, int startIndex = 0) => indexOf(m_value, searchStr, startIndex);

        /// <summary>
        /// Returns the last position of the string <paramref name="searchStr"/> in the current
        /// string before <paramref name="startIndex"/>, or -1 if <paramref name="searchStr"/> is
        /// not found in the string.
        /// </summary>
        ///
        /// <param name="searchStr">The string to match.</param>
        /// <param name="startIndex">The position from where to start searching.</param>
        /// <returns>The first position of <paramref name="searchStr"/> in the string.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public int lastIndexOf(string searchStr, int startIndex = Int32.MaxValue) => lastIndexOf(m_value, searchStr, startIndex);

        /// <summary>
        /// Compares the string to <paramref name="other"/> using a locale-specific comparison.
        /// </summary>
        /// <param name="other">The string to compare with the current string.</param>
        /// <returns>A negative number if the string is less than <paramref name="other"/>, zero if
        /// it is equal to <paramref name="other"/> or a positive number if it is greater than
        /// <paramref name="other"/>.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public int localeCompare(string other) => localeCompare(m_value, other);

        /// <summary>
        /// Returns the matches of the given regular expression in the string.
        /// </summary>
        /// <param name="regExp">The regular expression to execute on the target string.</param>
        /// <returns>An array containing the matches of <paramref name="regExp"/> in the string. The
        /// contents of the array depends on whether <paramref name="regExp"/> has the global flag
        /// set or not. (See remarks)</returns>
        ///
        /// <remarks>
        /// If the regular expression's global flag is set to false, this method calls
        /// <c>p.exec(this)</c> and returns its result. If the global flag is set to true, this
        /// method returns an array containing all substrings matched by the entire regular expression
        /// in the target string, or an empty array if no match is found, and the
        /// <see cref="ASRegExp.lastIndex" qualifyHint="true"/> property of
        /// <paramref name="regExp"/> is set to zero. In both cases, the
        /// <see cref="ASRegExp.lastIndex" qualifyHint="true"/> field of <paramref name="regExp"/>
        /// is ignored and the match always starts from the beginning of the target string. If the
        /// given parameter is not a RegExp object, it is converted to a string, a new RegExp object
        /// is created with that string as the pattern and no flags set, and the created RegExp object
        /// is used for the match
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASArray match(ASAny regExp) => match(m_value, regExp);

        /// <summary>
        /// Returns a new string with matches of the <paramref name="search"/> string or RegExp
        /// replaced with a replacement string or strings returned by a callback function.
        /// </summary>
        /// <param name="search">This may be a string or a RegExp object. (See remarks below)</param>
        /// <param name="repl">This may be a string or a Function object. (See remarks below)</param>
        /// <returns>The resultant string after replacement.</returns>
        ///
        /// <remarks>
        /// <para>
        /// If <paramref name="search"/> is a RegExp object, the first match (or all matches if the
        /// RegExp object's global flag is set) of the regular expression in the input string is
        /// replaced. Otherwise, <paramref name="search"/> is converted to a string, and the first
        /// match of that string in the input string is replaced.
        /// </para>
        /// <para>
        /// If <paramref name="repl"/> is a function, the function is called each time a match is
        /// found, with three arguments: the first is the matched string, the second is the index of
        /// the first character of the matched string in the input string, and the third is the input
        /// string itself. The return value of the function is used as the replacement string for that
        /// match. If <paramref name="repl"/> is not a function, it is converted to a string and
        /// used as the replacement string.
        /// </para>
        /// <para>
        /// If <paramref name="search"/> is a RegExp and <paramref name="repl"/> is a string,
        /// instances of '$' followed by certain sequences of characters in the replacement string are
        /// substituted with the following for each match: $0 and $&amp; (matched string), $1-$99
        /// (string matching the capturing group with that number), $` (portion of the input string
        /// preceding the matched string), $' (portion of the input string following the matched
        /// string). For a literal dollar sign in the replacement string, use "$$".
        /// </para>
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string replace(ASAny search, ASAny repl) => replace(m_value, search, repl);

        /// <summary>
        /// Returns the position of the first match of the regular expression in the string.
        /// </summary>
        ///
        /// <param name="regExp">
        /// The regular expression to execute on the string. This method ignores the RegExp object's
        /// <see cref="ASRegExp.lastIndex" qualifyHint="true"/> property and starts the search from
        /// the beginning of the string. If this parameter is not a RegExp object, it is converted to
        /// a string, a new RegExp object is created with that string as the pattern and no flags set,
        /// and the created RegExp object is used in the search.
        /// </param>
        ///
        /// <returns>The position of the first match of the string or regular expression given by the
        /// <paramref name="regExp"/> parameter in the string, or -1 if no match is
        /// found.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public int search(ASAny regExp) => search(m_value, regExp);

        /// <summary>
        /// Returns a substring of the string starting at <paramref name="startIndex"/> and ending
        /// at the index preceding <paramref name="endIndex"/>.
        /// </summary>
        ///
        /// <param name="startIndex">
        /// The index where the returned substring starts. If this is negative, the string's length
        /// will be added to it; if it is still negative after adding the string's length, it will be
        /// set to zero. If this is greater than the string's length or greater than or equal to
        /// <paramref name="endIndex"/>, the empty string is returned.
        /// </param>
        /// <param name="endIndex">
        /// The index where the returned substring ends. (The character at this index is not
        /// included.) If this is negative, the string's length will be added to it; if it is still
        /// negative after adding the string's length, the empty string is returned. If this is
        /// greater than the string's length, it will be set to the string's length. If this is less
        /// than or equal to <paramref name="startIndex"/>, the empty string is returned.
        /// </param>
        ///
        /// <returns>The substring of the string starting at index <paramref name="startIndex"/> and
        /// ending at the index preceding <paramref name="endIndex"/>.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string slice(int startIndex = 0, int endIndex = Int32.MaxValue) => slice(m_value, startIndex, endIndex);

        /// <summary>
        /// Splits the string using a separator (or a regular expression that matches separators) and
        /// returns an array of the split strings.
        /// </summary>
        ///
        /// <param name="sep">
        /// A string or regular expression that is used to separate the string. If this is a RegExp
        /// object, matches of the RegExp in the input string are considered as separators. The
        /// lastIndex property of the RegExp is ignored; the search always starts at the beginning of
        /// the string. If this is not a RegExp, it is converted to a string, and that string is used
        /// as a separator to split the string.
        /// </param>
        /// <param name="limit">The maximum number of split strings, starting from the beginning of
        /// the input string, to be included in the returned array. If this is negative, there is no
        /// limit; the returned array will contain all possible split strings.</param>
        ///
        /// <returns>An array containing the split substrings.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASArray split(ASAny sep, int limit = -1) => split(m_value, sep, limit);

        /// <summary>
        /// Returns the substring of the string starting at <paramref name="startIndex"/> and having
        /// <paramref name="len"/> number of characters.
        /// </summary>
        ///
        /// <param name="startIndex">The start index. If this is negative, the string's length will be
        /// added to it; if it is still negative after adding the string's length, it will be set to
        /// zero. If this is greater than the string's length, the empty string is returned.</param>
        /// <param name="len">
        /// The number of characters to include in the substring. If this value is greater than the
        /// number of characters available in the string starting at the
        /// <paramref name="startIndex"/> position, the returned substring will contain all
        /// characters from the start position to the end of the string. If this is negative, the
        /// empty string is returned.
        /// </param>
        ///
        /// <returns>The substring of the string starting at <paramref name="startIndex"/> and
        /// having <paramref name="len"/> number of characters.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string substr(int startIndex = 0, int len = Int32.MaxValue) => substr(m_value, startIndex, len);

        /// <summary>
        /// Returns the substring of the string starting at index <paramref name="startIndex"/> and
        /// ending at the index preceding <paramref name="endIndex"/>.
        /// </summary>
        ///
        /// <param name="startIndex">
        /// The index where the returned substring starts. If this is negative, it is set to zero; if
        /// it is greater than the string's length, it is taken as the string's length. If this is
        /// equal to <paramref name="endIndex"/>, the empty string is returned; if it is greater
        /// than <paramref name="endIndex"/>, the start and end indices are swapped.
        /// </param>
        /// <param name="endIndex">
        /// The index where the returned substring ends. (The character at this index is not
        /// included.) If this is negative, it is set to zero; if it is greater than the string's
        /// length, it is taken as the string's length. If this is equal to
        /// <paramref name="startIndex"/>, the empty string is returned; if it is less than
        /// <paramref name="startIndex"/>, the start and end indices are swapped.
        /// </param>
        ///
        /// <returns>The substring of the string starting at index <paramref name="startIndex"/> and
        /// ending at the index preceding <paramref name="endIndex"/>.</returns>
        ///
        /// <remarks>
        /// This differs from <see cref="slice(Int32, Int32)"/> in how negative values of
        /// <paramref name="startIndex"/> and <paramref name="endIndex"/> are handled (they are
        /// set to 0, whereas in <see cref="slice(Int32, Int32)"/> they are taken as indices relative to
        /// the end of the string).
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string substring(int startIndex = 0, int endIndex = Int32.MaxValue) => substring(m_value, startIndex, endIndex);

        /// <summary>
        /// Converts all alphabetic characters in the string to lowercase in a locale-specific manner.
        /// </summary>
        /// <returns>The string with alphabetic characters converted to lowercase.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string toLocaleLowerCase() => toLocaleLowerCase(m_value);

        /// <summary>
        /// Converts all alphabetic characters in the string to uppercase in a locale-specific manner.
        /// </summary>
        /// <returns>Converts all alphabetic characters in the string to uppercase.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string toLocaleUpperCase() => toLocaleUpperCase(m_value);

        /// <summary>
        /// Converts all alphabetic characters in the string to lowercase.
        /// </summary>
        /// <returns>Converts all alphabetic characters in the string to lowercase.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string toLowerCase() => toLowerCase(m_value);

        /// <summary>
        /// Converts all alphabetic characters in the string to uppercase.
        /// </summary>
        /// <returns>Converts all alphabetic characters in the string to uppercase.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string toUpperCase() => toUpperCase(m_value);

        /// <summary>
        /// Returns the primitive string value contained in this <see cref="ASString"/> instance.
        /// </summary>
        /// <returns>The primitive string value contained in this <see cref="ASString"/>
        /// instance.</returns>
        ///
        /// <remarks>
        /// This method is exported to the AVM2 with the name <c>toString</c>, but must be called
        /// from .NET code with the name <c>AS_toString</c> to avoid confusion with the
        /// <see cref="Object.ToString" qualifyHint="true"/> method.
        /// </remarks>
        [AVM2ExportTrait(name = "toString", nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod(name = "toString")]
        public new string AS_toString() => m_value;

        /// <summary>
        /// Returns the primitive type representation of the object.
        /// </summary>
        /// <returns>A primitive representation of the object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public new string valueOf() => m_value;

        /// <summary>
        /// Converts a string to a Boolean value.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <returns>True if the string is null or empty, otherwise false.</returns>
        public static bool AS_toBoolean(string s) => s != null && s.Length != 0;

        /// <summary>
        /// Parses a string to a 64-bit IEEE 754 floating-point number.
        /// </summary>
        /// <param name="s">The string to parse. If this starts with '0x' or '0X', it is interpreted
        /// as a hexadecimal integer.</param>
        /// <returns>The floating-point number. If <paramref name="s"/> is null, empty or contains
        /// only whitespace, 0 is returned; if <paramref name="s"/> is an invalid string, NaN is
        /// returned.</returns>
        public static double AS_toNumber(string s) {
            if (s == null)
                return 0.0;

            if (NumberFormatHelper.stringToDouble(s, out double value, out _, strict: true))
                return value;

            return (NumberFormatHelper.indexOfFirstNonSpace(s) != s.Length) ? Double.NaN : 0.0;
        }

        /// <summary>
        /// Parses a string to an integer.
        /// </summary>
        /// <param name="s">The string to parse. If this starts with '0x' or '0X', it is interpreted
        /// as a hexadecimal integer.</param>
        /// <returns>The integer. If <paramref name="s"/> is null, empty or invalid, 0 is
        /// returned.</returns>
        public static int AS_toInt(string s) {
            if (s == null)
                return 0;

            if (NumberFormatHelper.stringToInt(s, out int value, out _, strict: true))
                return value;

            if (NumberFormatHelper.stringToDouble(s, out double dvalue, out _, strict: true))
                return ASNumber.AS_toInt(dvalue);

            return 0;
        }

        /// <summary>
        /// Parses a string to an unsigned integer.
        /// </summary>
        /// <param name="s">The string to parse. If this starts with '0x' or '0X', it is interpreted
        /// as a hexadecimal integer.</param>
        /// <returns>The unsigned integer. If <paramref name="s"/> is null, empty or invalid, 0 is
        /// returned.</returns>
        public static uint AS_toUint(string s) {
            if (s == null)
                return 0;

            if (NumberFormatHelper.stringToUint(s, out uint value, out _, strict: true))
                return value;

            if (NumberFormatHelper.stringToDouble(s, out double dvalue, out _, strict: true))
                return ASNumber.AS_toUint(dvalue);

            return 0;
        }

        /// <summary>
        /// Performs a string conversion on the string <paramref name="s"/>.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <returns>If <paramref name="s"/> is null, returns the string "null", otherwise returns
        /// <paramref name="s"/>.</returns>
        public static string AS_convertString(string s) => s ?? "null";

        /// <summary>
        /// Returns the string representation of the character at the given position in the string.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <param name="index">The position in the string at which to return the character.</param>
        /// <returns>The string representation of the character at position <paramref name="index"/>
        /// in the string, or the empty string if <paramref name="index"/> is out of
        /// bounds.</returns>
        public static string charAt(string s, int index = 0) {
            if ((uint)index >= (uint)s.Length)
                return "";

            char c = s[index];
            var cache = s_singleCharCachedValues;
            return ((uint)c < (uint)cache.Length) ? cache[(int)c] : new string(c, 1);
        }

        /// <summary>
        /// Returns the Unicode code point of the character at the given position in the string.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <param name="index">The position in the string at which to return the character.</param>
        /// <returns>The code point of the character at position <paramref name="index"/> in the
        /// string <paramref name="s"/>, or NaN if <paramref name="index"/> is out of
        /// bounds.</returns>
        public static double charCodeAt(string s, int index = 0) =>
            ((uint)index >= (uint)s.Length) ? Double.NaN : (double)s[index];

        /// <summary>
        /// Converts the objects in an array to strings and concatenates them to the first string.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <param name="args">The arguments to concatenate to the string.</param>
        /// <returns>The string created by concatenation of the string <paramref name="s"/> with the
        /// strings created from objects in the <paramref name="args"/> array.</returns>
        public static string concat(string s, RestParam args) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            var span = args.getSpan();
            string[] arr = new string[span.Length + 1];

            arr[0] = s;

            for (int i = 0; i < span.Length; i++)
                arr[i + 1] = ASAny.AS_convertString(span[i]);

            return String.Concat(arr);
        }

        /// <summary>
        /// Returns the first position of the string <paramref name="searchStr"/> in
        /// <paramref name="s"/> after <paramref name="startIndex"/>, or -1 if
        /// <paramref name="searchStr"/> is not found in <paramref name="s"/>.
        /// </summary>
        ///
        /// <param name="s">The string to search for <paramref name="searchStr"/>.</param>
        /// <param name="searchStr">The string to match in <paramref name="s"/>.</param>
        /// <param name="startIndex">The position from where to start searching.</param>
        /// <returns>The first position of <paramref name="searchStr"/> in
        /// <paramref name="s"/>.</returns>
        public static int indexOf(string s, string searchStr, int startIndex = 0) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            if (searchStr == null || startIndex >= s.Length)
                return -1;

            if (startIndex < 0)
                startIndex = 0;

            if (searchStr.Length == 1)
                return s.IndexOf(searchStr[0], startIndex);

            return s.IndexOf(searchStr, startIndex, StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns the last position of the string <paramref name="searchStr"/> in
        /// <paramref name="s"/> before <paramref name="startIndex"/>, or -1 if
        /// <paramref name="searchStr"/> is not found in <paramref name="s"/>.
        /// </summary>
        ///
        /// <param name="s">The string to search for <paramref name="searchStr"/>.</param>
        /// <param name="searchStr">The string to match in <paramref name="s"/>.</param>
        /// <param name="startIndex">The position in <paramref name="s"/> from where to start
        /// searching.</param>
        /// <returns>The first position of <paramref name="searchStr"/> in
        /// <paramref name="s"/>.</returns>
        public static int lastIndexOf(string s, string searchStr, int startIndex = Int32.MaxValue) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            if (searchStr == null || startIndex < 0)
                return -1;

            if (startIndex >= s.Length)
                startIndex = s.Length - 1;

            if (searchStr.Length == 1)
                return s.LastIndexOf(searchStr[0], startIndex);

            return s.LastIndexOf(searchStr, startIndex, StringComparison.Ordinal);
        }

        /// <summary>
        /// Compares two strings in a locale-specific manner.
        /// </summary>
        /// <param name="s">The first string.</param>
        /// <param name="other">The second string.</param>
        /// <returns>A negative number if <paramref name="s"/> is less than
        /// <paramref name="other"/>, zero if <paramref name="s"/> is equal to
        /// <paramref name="other"/> or a positive number if <paramref name="s"/> is greater than
        /// <paramref name="other"/>.</returns>
        public static int localeCompare(string s, string other) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);
            return (other == null) ? -1 : String.Compare(s, other, StringComparison.CurrentCulture);
        }

        /// <summary>
        /// Returns the matches of the given regular expression in a string.
        /// </summary>
        /// <param name="s">The target string.</param>
        /// <param name="regExp">The RegExp object representing the regular expression to execute on
        /// the target string. If this is not a RegExp, it is converted to a string and used as the
        /// regular expression pattern, with no flags set.</param>
        /// <returns>An array containing the matches of <paramref name="regExp"/> in the string
        /// <paramref name="s"/>. The contents of the array depends on whether
        /// <paramref name="regExp"/> has the global flag set or not. (See remarks)</returns>
        ///
        /// <remarks>
        /// If the regular expression's global flag is set to false, this method calls
        /// <c><paramref name="regExp"/>.exec(<paramref name="s"/>)</c> and returns its
        /// result. If the global flag is set to true, this method returns an array containing all
        /// substrings matched by the entire regular expression in the target string (or an empty
        /// array if no match is found), and the <see cref="ASRegExp.lastIndex" qualifyHint="true"/>
        /// property of <paramref name="regExp"/> is set to zero. In both cases, the
        /// <see cref="ASRegExp.lastIndex" qualifyHint="true"/> property of
        /// <paramref name="regExp"/> is ignored and the match always starts from the beginning of
        /// the target string.
        /// </remarks>
        public static ASArray match(string s, ASAny regExp) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            ASRegExp re = regExp.value as ASRegExp;
            if (re == null) {
                if (regExp.value == null)
                    return null;
                re = new ASRegExp((string)regExp.value);
            }

            if (re.global) {
                MatchCollection mc = re.getInternalRegex().Matches(s);
                var matchArray = new ASArray(mc.Count);

                for (int i = 0, n = mc.Count; i < n; i++)
                    matchArray.AS_setElement(i, mc[i].Value);

                re.lastIndex = 0;
                return matchArray;
            }
            else {
                int lastIndex = 0;
                return re.execInternal(s, ref lastIndex);
            }
        }

        /// <summary>
        /// Returns a new string with matches of the <paramref name="search"/> string or RegExp
        /// replaced with a replacement string or strings returned by a callback function.
        /// </summary>
        /// <param name="s">The source string.</param>
        /// <param name="search">This may be a string or a RegExp object. (See remarks below)</param>
        /// <param name="repl">This may be a string or a Function object. (See remarks below)</param>
        /// <returns>The resulting string after replacement.</returns>
        ///
        /// <remarks>
        /// <para>
        /// If <paramref name="search"/> is a RegExp object, the first match (or all matches if the
        /// RegExp object's global flag is set) of the regular expression in the input string is
        /// replaced. Otherwise, <paramref name="search"/> is converted to a string, and the first
        /// match of that string in the input string is replaced.
        /// </para>
        /// <para>
        /// If <paramref name="repl"/> is a function, the function is called each time a match is
        /// found, with three arguments: the first is the matched string, the second is the index of
        /// the first character of the matched string in the input string, and the third is the input
        /// string itself. The return value of the function is used as the replacement string for that
        /// match. If <paramref name="repl"/> is not a function, it is converted to a string and
        /// used as the replacement string.
        /// </para>
        /// <para>
        /// If <paramref name="search"/> is a RegExp and <paramref name="repl"/> is a string,
        /// instances of '$' followed by certain sequences of characters in the replacement string are
        /// substituted with the following for each match: $0 and $&amp; (matched string), $1-$99
        /// (string matching the capturing group with that number), $` (portion of the input string
        /// preceding the matched string), $' (portion of the input string following the matched
        /// string). For a literal dollar sign in the replacement string, use "$$".
        /// </para>
        /// </remarks>
        public static string replace(string s, ASAny search, ASAny repl) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            ASFunction replFunction = repl.value as ASFunction;
            ASRegExp searchRegex = search.value as ASRegExp;
            string searchString = null, replString = null;

            // If repl is not a function or search is not a RegExp, convert them to strings.
            if (replFunction == null)
                replString = ASAny.AS_convertString(repl);

            if (searchRegex == null) {
                searchString = (string)search;
                if (searchString == null || searchString.Length == 0)
                    return s;
            }

            // isGlobal is true if all matches in the input string are to be replaced.
            // Otherwise, only the first match is replaced.
            bool isGlobal = searchRegex != null && searchRegex.global;

            ASAny[] callbackArgs = null;

            if (replFunction != null)
                callbackArgs = new ASAny[] {searchString ?? default, default, s};

            // Determine the index of the first match. If the first match has not been found, return the input
            // string, as there are no replacements to be made.
            int matchIndex;
            Match regexMatch = null;

            if (searchRegex == null) {
                matchIndex = indexOf(s, searchString);
                if (matchIndex == -1)
                    return s;
            }
            else {
                regexMatch = searchRegex.getInternalRegex().Match(s);
                if (!regexMatch.Success)
                    return s;
                matchIndex = regexMatch.Index;
            }

            bool mustResolveReplString =
                searchRegex != null && replString != null && replString.IndexOf('$') != -1;

            int replLength = (replString != null && !mustResolveReplString) ? replString.Length : -1;

            char[] replBuffer = new char[s.Length];
            int replBufIndex = 0;
            int srcIndex = 0;

            do {
                string matchedString = (searchRegex != null) ? regexMatch.Value : searchString;
                string strForThisReplace;

                if (replFunction != null) {
                    if (searchRegex != null)
                        callbackArgs[0] = matchedString;
                    callbackArgs[1] = matchIndex;
                    strForThisReplace = ASAny.AS_convertString(replFunction.AS_invoke(ASAny.@null, callbackArgs));
                }
                else {
                    strForThisReplace = replString;
                }

                int midLength = matchIndex - srcIndex;

                // Check that the buffer has sufficient space for the intermediate portion
                // (and the replacement string, if it does not have any references to resolve)
                int bufferCapacityNeeded = (replLength == -1) ? midLength : midLength + replLength;
                if (replBuffer.Length - replBufIndex < bufferCapacityNeeded) {
                    DataStructureUtil.resizeArray(
                        ref replBuffer, replBufIndex, replBufIndex + bufferCapacityNeeded, false);
                }

                s.CopyTo(srcIndex, replBuffer, replBufIndex, midLength);
                replBufIndex += midLength;

                if (mustResolveReplString) {
                    _resolveRelpaceString(ref replBuffer, ref replBufIndex, s, strForThisReplace, regexMatch);
                }
                else {
                    strForThisReplace.CopyTo(0, replBuffer, replBufIndex, replLength);
                    replBufIndex += replLength;
                }

                srcIndex += midLength + matchedString.Length;

                if (!isGlobal)
                    // For a non-global replace, exit after the first match.
                    break;

                // Do the next match.
                if (searchRegex != null) {
                    regexMatch = regexMatch.NextMatch();
                    matchIndex = regexMatch.Success ? regexMatch.Index : -1;
                }
                else {
                    matchIndex = indexOf(s, searchString, srcIndex);
                }
            } while (matchIndex != -1);

            // Copy the remaining part of the source string, after the last replacement (the 'tail')
            int tailLength = s.Length - srcIndex;
            if (replBufIndex + tailLength > replBuffer.Length)
                DataStructureUtil.resizeArray(ref replBuffer, replBufIndex, replBufIndex + tailLength, true);

            s.CopyTo(srcIndex, replBuffer, replBufIndex, tailLength);
            return new string(replBuffer, 0, replBufIndex + tailLength);
        }

        /// <summary>
        /// Resolves all $-references in a string passed to the replace() function and writes the
        /// resolved string to the buffer.
        /// </summary>
        ///
        /// <param name="buffer">The buffer to write the resolved string to.</param>
        /// <param name="bufIndex">The index at which to start writing into the buffer. This will be
        /// changed to the index following what was written.</param>
        /// <param name="input">The input string passed to replace().</param>
        /// <param name="replaceString">The unresolved replacement string passed to
        /// replace().</param>
        /// <param name="matchData">The <see cref="Match"/> instance returned by the regular
        /// expression match.</param>
        private static void _resolveRelpaceString(
            ref char[] buffer, ref int bufIndex, string input, string replaceString, Match matchData)
        {
            GroupCollection groups = matchData.Groups;
            ReadOnlySpan<char> replStrSpan = replaceString;

            while (replStrSpan.Length > 0) {
                int substIndex = replStrSpan.IndexOf('$');
                ReadOnlySpan<char> preSubst, subst;

                if (substIndex == -1 || substIndex == replStrSpan.Length - 1) {
                    preSubst = replStrSpan;
                    subst = default;
                    replStrSpan = default;
                }
                else {
                    preSubst = replStrSpan.Slice(0, substIndex);

                    switch (replStrSpan[substIndex + 1]) {
                        case '$':
                            // Escape, literal dollar sign
                            subst = replStrSpan.Slice(substIndex, 1);
                            replStrSpan = replStrSpan.Slice(substIndex + 2);
                            break;

                        case '&':
                        case '0':
                            // Matched string
                            subst = matchData.Value;
                            replStrSpan = replStrSpan.Slice(substIndex + 2);
                            break;

                        case '`':
                            // String preceding the matched string
                            subst = input.AsSpan(0, matchData.Index);
                            replStrSpan = replStrSpan.Slice(substIndex + 2);
                            break;

                        case '\'':
                            // String following the matched string
                            subst = input.AsSpan(matchData.Index + matchData.Length);
                            replStrSpan = replStrSpan.Slice(substIndex + 2);
                            break;

                        default: {
                            // Try to find a capturing group number.
                            // If the group number is greater than the number of groups in the regex,
                            // interpret these references as literals.

                            int index = readGroupIndex(replStrSpan.Slice(substIndex + 1), groups.Count, out int charsRead);
                            if (index == -1) {
                                subst = replStrSpan.Slice(substIndex, 1);
                                replStrSpan = replStrSpan.Slice(substIndex + 1);
                            }
                            else {
                                subst = groups[index].Value ?? "";
                                replStrSpan = replStrSpan.Slice(substIndex + charsRead + 1);
                            }
                            break;
                        }
                    }
                }

                int totalLength = preSubst.Length + subst.Length;
                if (buffer.Length - bufIndex < totalLength)
                    DataStructureUtil.resizeArray(ref buffer, bufIndex, bufIndex + totalLength, false);

                preSubst.CopyTo(buffer.AsSpan(bufIndex));
                subst.CopyTo(buffer.AsSpan(bufIndex + preSubst.Length));
                bufIndex += totalLength;
            }

            int readGroupIndex(ReadOnlySpan<char> span, int groupCount, out int charsRead) {
                charsRead = 0;
                char first = span[0];

                if ((uint)(first - '0') > 9)
                    return -1;

                if (span.Length >= 2) {
                    // Attempt to find a two-digit number.
                    char second = span[1];
                    if ((uint)(second - '0') <= 9) {
                        int twoDigitIndex = (first - '0') * 10 + (second - '0');
                        if (twoDigitIndex < groupCount) {
                            charsRead = 2;
                            return twoDigitIndex;
                        }
                    }
                }

                int oneDigitIndex = first - '0';
                if (oneDigitIndex < groupCount) {
                    charsRead = 1;
                    return oneDigitIndex;
                }

                return -1;
            }
        }

        /// <summary>
        /// Returns the position of the first match of the regular expression in the string
        /// <paramref name="s"/>. This method ignores the
        /// <see cref="ASRegExp.lastIndex" qualifyHint="true"/> property of RegExp and starts the
        /// search from the beginning of the string.
        /// </summary>
        ///
        /// <param name="s">The input string.</param>
        /// <param name="p">
        /// The RegExp object representing the regular expression to execute on the target string. If
        /// this is not a RegExp, it is converted to a string and used as the regular expression
        /// pattern, with no flags set. The RegExp object's
        /// <see cref="ASRegExp.lastIndex" qualifyHint="true"/> property is ignored; the search
        /// always starts at the beginning of the string.
        /// </param>
        ///
        /// <returns>The position of the first match of the regular expression in the string, or -1 if
        /// no match is found.</returns>
        public static int search(string s, ASAny p) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            ASRegExp re = p.value as ASRegExp;
            if (re == null) {
                if (p.value == null)
                    return -1;
                re = new ASRegExp(ASObject.AS_coerceString(p.value));
            }

            Match m = re.getInternalRegex().Match(s);
            return m.Success ? m.Index : -1;
        }

        /// <summary>
        /// Returns the substring of <paramref name="s"/> starting at
        /// <paramref name="startIndex"/> and having <paramref name="length"/> number of
        /// characters.
        /// </summary>
        ///
        /// <param name="s">The string.</param>
        /// <param name="startIndex">The start index. If this is negative, the string's length will be
        /// added to it; if it is still negative after adding the string's length, it will be set to
        /// zero. If this is greater than the string's length, the empty string is returned.</param>
        /// <param name="length">
        /// The number of characters to include in the substring. If this value is greater than the
        /// number of characters available in the string starting at the
        /// <paramref name="startIndex"/> position, the returned substring will contain all
        /// characters from the start position to the end of the string. If this is negative, the
        /// empty string is returned.
        /// </param>
        ///
        /// <returns>The substring of <paramref name="s"/> starting at
        /// <paramref name="startIndex"/> and having <paramref name="length"/> number of
        /// characters.</returns>
        public static string substr(string s, int startIndex = 0, int length = Int32.MaxValue) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            int strlen = s.Length;

            if (startIndex < 0)
                startIndex = Math.Max(startIndex + strlen, 0);

            if ((uint)startIndex > (uint)strlen || (uint)length > (uint)(strlen - startIndex))
                length = strlen - startIndex;

            return (startIndex >= strlen || length < 0) ? "" : s.Substring(startIndex, length);
        }

        /// <summary>
        /// Returns the substring of <paramref name="s"/> starting at index
        /// <paramref name="startIndex"/> and ending at the index preceding
        /// <paramref name="endIndex"/>.
        /// </summary>
        ///
        /// <param name="s">The string.</param>
        /// <param name="startIndex">
        /// The index where the returned substring starts. If this is negative, the string's length
        /// will be added to it; if it is still negative after adding the string's length, it will be
        /// set to zero. If this is greater than the string's length or greater than or equal to
        /// <paramref name="endIndex"/>, the empty string is returned.
        /// </param>
        /// <param name="endIndex">
        /// The index where the returned substring ends. (The character at this index is not
        /// included.) If this is negative, the string's length will be added to it; if it is still
        /// negative after adding the string's length, the empty string is returned. If this is
        /// greater than the string's length, it will be taken a the string's length. If this is less
        /// than or equal to <paramref name="startIndex"/>, the empty string is returned.
        /// </param>
        ///
        /// <returns>The substring of <paramref name="s"/> starting at index
        /// <paramref name="startIndex"/> and ending at the index preceding
        /// <paramref name="endIndex"/>.</returns>
        public static string slice(string s, int startIndex = 0, int endIndex = Int32.MaxValue) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            int strLen = s.Length;

            if (startIndex < 0)
                startIndex = Math.Max(startIndex + strLen, 0);

            if (endIndex < 0)
                endIndex = Math.Min(endIndex + strLen, strLen);

            return (endIndex <= startIndex || startIndex >= strLen) ? "" : s.Substring(startIndex, endIndex - startIndex);
        }

        /// <summary>
        /// Returns the substring of <paramref name="s"/> starting at index
        /// <paramref name="startIndex"/> and ending at the index preceding
        /// <paramref name="endIndex"/>.
        /// </summary>
        ///
        /// <param name="s">The string.</param>
        /// <param name="startIndex">
        /// The index where the returned substring starts. If this is negative, it is set to zero; if
        /// it is greater than the string's length, it is taken as the string's length. If this is
        /// equal to <paramref name="endIndex"/>, the empty string is returned; if it is greater
        /// than <paramref name="endIndex"/>, the start and end indices are swapped.
        /// </param>
        /// <param name="endIndex">
        /// The index where the returned substring ends. (The character at this index is not
        /// included.) If this is negative, it is set to zero; if it is greater than the string's
        /// length, it is set to the string's length. If this is equal to
        /// <paramref name="startIndex"/>, the empty string is returned; if it is less than
        /// <paramref name="startIndex"/>, the start and end indices are swapped.
        /// </param>
        ///
        /// <returns>The substring of <paramref name="s"/> starting at index
        /// <paramref name="startIndex"/> and ending at the index preceding
        /// <paramref name="endIndex"/>.</returns>
        ///
        /// <remarks>
        /// This differs from <see cref="slice(String, Int32, Int32)"/> in how negative values of start
        /// or end are handled: They are set to 0, where as in <see cref="slice(String, Int32, Int32)"/>
        /// they are set to indices relative to the end of the string).
        /// </remarks>
        public static string substring(string s, int startIndex = 0, int endIndex = Int32.MaxValue) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            int length = s.Length;

            startIndex = Math.Min(Math.Max(startIndex, 0), length);
            endIndex = Math.Min(Math.Max(endIndex, 0), length);

            if (startIndex == endIndex)
                return "";

            if (endIndex < startIndex)
                (startIndex, endIndex) = (endIndex, startIndex);

            return s.Substring(startIndex, endIndex - startIndex);
        }

        /// <summary>
        /// Splits the string <paramref name="s"/> using a separator (or a regular expression that
        /// matches separators) and returns an array of the split strings.
        /// </summary>
        ///
        /// <param name="s">The string to split.</param>
        /// <param name="sep">
        /// A string or regular expression that is used to separate the string. If this is a RegExp
        /// object, matches of the RegExp in the input string are considered as separators. The
        /// <see cref="ASRegExp.lastIndex" qualifyHint="true"/> property of the RegExp is ignored;
        /// the search always starts at the beginning of the string. If this is not a RegExp, it is
        /// converted to a string, and that string is used as a separator to split the string.
        /// </param>
        /// <param name="limit">The maximum number of split strings, starting from the beginning of
        /// the input string, to be included in the returned array. If this is negative, there is no
        /// limit; the returned array will contain all possible split strings.</param>
        ///
        /// <returns>An array containing the split substrings.</returns>
        public static ASArray split(string s, ASAny sep, int limit = -1) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            DynamicArray<string> strList = new DynamicArray<string>();
            if (sep.value is ASRegExp re)
                _internalSplitByRegExp(s, re, limit, ref strList);
            else
                _internalSplitByString(s, AS_coerceString(sep.value), limit, ref strList);

            return ASArray.fromSpan<string>(strList.asSpan());
        }

        private static void _internalSplitByString(string s, string sep, int limit, ref DynamicArray<string> outList) {

            if (limit == 0)
                return;

            if (sep == null) {
                outList.add(s);
                return;
            }

            int strLen = s.Length, lastIndex = 0;
            bool hitLimit = false;

            if (sep.Length == 0) {
                // Empty-string separator
                int listSize = (limit > 0) ? Math.Min(limit, s.Length) : s.Length;
                outList.setCapacity(listSize);

                for (int i = 0; i < listSize; i++)
                    outList.add(new string(s[i], 1));
            }
            else if (sep.Length == 1) {
                // One-character separator
                char c = sep[0];
                outList.setCapacity((limit == -1) ? 20 : Math.Min(limit, 20));

                for (int i = 0; i < strLen; i++) {
                    if (s[i] != c)
                        continue;

                    outList.add(s.Substring(lastIndex, i - lastIndex));
                    if (hitLimit)
                        break;
                    lastIndex = i + 1;
                    hitLimit |= outList.length == limit - 1;
                }
            }
            else {
                outList.setCapacity((limit == -1) ? 16 : Math.Min(limit, 16));
                int cur = s.IndexOf(sep, 0, StringComparison.Ordinal);

                while (cur != -1) {
                    // For each occurence of the separator string
                    // Get the substring of the source string between the last (or beginning of the string
                    // for the first occurence) and current occurences of the separator string, excluding the
                    // separators themselves.
                    // Add this substring to the return array.
                    outList.add(s.Substring(lastIndex, cur - lastIndex));

                    if (hitLimit)
                        break;
                    hitLimit |= outList.length == limit - 1;

                    // Find the next occurence.
                    lastIndex = cur + sep.Length;
                    cur = s.IndexOf(sep, lastIndex, StringComparison.Ordinal);
                }
            }

            // If the end of the string has been reached without exceeding the limit, add the last split
            // string (between the final occurence of the separator and the end of the string) to the return
            // array.
            if (!hitLimit)
                outList.add((lastIndex == strLen) ? "" : s.Substring(lastIndex, strLen - lastIndex));
        }

        private static void _internalSplitByRegExp(string s, ASRegExp sep, int limit, ref DynamicArray<string> outList) {
            if (limit == 0)
                return;

            outList.setCapacity((limit == -1) ? 20 : Math.Min(limit, 20));
            int lastIndex = 0;
            bool hitLimit = false;

            Match m = sep.getInternalRegex().Match(s);
            if (m.Success && m.Index == 0 && m.Length == 0)
                // Ignore empty-string matches at the beginning of the string (otherwise, there will be an unwanted
                // empty string as the first element of the returned array.
                m = m.NextMatch();

            bool lastMatchEmpty = false;

            while (m.Success) {
                // Empty-string matches are to be ignored if they occur exactly where the previous match ended.
                if (m.Length == 0 && m.Index == lastIndex) {
                    lastMatchEmpty = true;
                    m = m.NextMatch();
                    continue;
                }
                // For each match:
                // Get the substring of the source string between the position of the last
                // match (or the beginning of the string, in case of the first match) and
                // the current match, excluding the matched substrings.
                outList.add(s.Substring(lastIndex, m.Index - lastIndex));
                if (hitLimit)
                    break;
                hitLimit |= outList.length == limit - 1;

                // Find the next match.
                lastIndex = m.Index + m.Length;
                lastMatchEmpty = m.Length == 0;
                m = m.NextMatch();
            }

            // If the end of the string has been reached without exceeding the limit, add the last split
            // string (between the final match and the end of the string) to the return array.
            if (!hitLimit && (lastIndex != s.Length || !lastMatchEmpty))
                outList.add((lastIndex == s.Length) ? "" : s.Substring(lastIndex, s.Length - lastIndex));
        }

        /// <summary>
        /// Converts all alphabetic characters in the string to lowercase.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <returns>The string with alphabetic characters converted to lowercase.</returns>
        public static string toLowerCase(string s) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);
            return s.ToLowerInvariant();
        }

        /// <summary>
        /// Converts all alphabetic characters in the string to lowercase in a locale-specific manner.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <returns>The string with alphabetic characters converted to lowercase.</returns>
        public static string toLocaleLowerCase(string s) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);
            return s.ToLower(CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Converts all alphabetic characters in the string to uppercase.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <returns>The string with alphabetic characters converted to uppercase.</returns>
        public static string toUpperCase(string s) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);
            return s.ToUpperInvariant();
        }

        /// <summary>
        /// Converts all alphabetic characters in the string to uppercase in a locale-specific manner.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <returns>The string with alphabetic characters converted to uppercase.</returns>
        public static string toLocaleUpperCase(string s) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);
            return s.ToUpper(CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Returns the given argument. This is used by the ABC compiler for calls to the
        /// <c>valueOf</c> method on String values.
        /// </summary>
        /// <param name="s">The argument.</param>
        /// <returns>The value of <paramref name="s"/>.</returns>
        [AVM2ExportPrototypeMethod]
        public static string valueOf(string s) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);
            return s;
        }

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABCIL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) {
            if (args.Length == 0)
                return ASAny.AS_fromString("");
            return ASAny.AS_fromString(ASAny.AS_convertString(args[0]));
        }

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABCIL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_CONSTRUCT(ReadOnlySpan<ASAny> args) {
            if (args.Length == 0)
                return ASAny.AS_fromString("");
            return ASAny.AS_fromString(ASAny.AS_convertString(args[0]));
        }

        private static string[] _prepareSingleCharCachedValues() {
            var cachedValues = new string[SINGLE_CHAR_CACHE_RANGE + 1];
            for (int i = 0; i <= SINGLE_CHAR_CACHE_RANGE; i++)
                cachedValues[i] = String.Intern(new string((char)i, 1));
            return cachedValues;
        }

        private static ASObject[] _prepareSingleCharCachedObjects() {
            var cachedObjects = new ASObject[SINGLE_CHAR_CACHE_RANGE + 1];
            for (int i = 0; i <= SINGLE_CHAR_CACHE_RANGE; i++)
                cachedObjects[i] = new ASString(String.Intern(new string((char)i, 1)));
            return cachedObjects;
        }

        /// <summary>
        /// Creates a boxed object from a string value.
        /// </summary>
        /// <param name="val">The value to be boxed.</param>
        internal static ASObject box(string val) {
            if (val == null)
                return null;

            if ((uint)val.Length <= 0)
                return s_lazyEmptyString.value;

            if (val.Length == 1) {
                char cv = val[0];
                var cache = s_lazySingleCharCachedObjects.value;
                if ((uint)cv < (uint)cache.Length)
                    return cache[cv];
            }

            return new ASString(val);
        }

    }

}
