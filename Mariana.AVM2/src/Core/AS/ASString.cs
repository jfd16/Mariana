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
        private const int SINGLE_CHAR_CACHE_RANGE = 127;

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
        ///
        /// <returns>The result of the concatenation of the two strings. If any of the strings is
        /// null, the string "null" is used in its place. Note that the result is consistent with
        /// the semantics of the ECMAScript addition operator if and only if at least one operand
        /// is not null.</returns>
        public static string AS_add(string s1, string s2) => (s1 ?? "null") + (s2 ?? "null");

        /// <summary>
        /// Concatenates three strings and returns the result.
        /// </summary>
        /// <param name="s1">The first string.</param>
        /// <param name="s2">The second string.</param>
        /// <param name="s3">The third string.</param>
        ///
        /// <returns>The result of the concatenation of the three strings. If any of the strings is
        /// null, the string "null" is used in its place.</returns>
        public static string AS_add(string s1, string s2, string s3) =>
            String.Concat(s1 ?? "null", s2 ?? "null", s3 ?? "null");

        /// <summary>
        /// Concatenates four strings and returns the result.
        /// </summary>
        /// <param name="s1">The first string.</param>
        /// <param name="s2">The second string.</param>
        /// <param name="s3">The third string.</param>
        /// <param name="s4">The fourth string.</param>
        ///
        /// <returns>The result of the concatenation of the four strings. If any of the strings is
        /// null, the string "null" is used in its place.</returns>
        public static string AS_add(string s1, string s2, string s3, string s4) {
            s1 = s1 ?? "null";
            s2 = s2 ?? "null";
            s3 = s3 ?? "null";
            s4 = s4 ?? "null";

            int totalLength = checked(s1.Length + s2.Length + s3.Length + s4.Length);

            return String.Create(totalLength, (s1, s2, s3, s4), (dest, state) => {
                Span<char> remaining = dest;

                state.s1.AsSpan().CopyTo(remaining);
                remaining = remaining.Slice(state.s1.Length);
                state.s2.AsSpan().CopyTo(remaining);
                remaining = remaining.Slice(state.s2.Length);
                state.s3.AsSpan().CopyTo(remaining);
                remaining = remaining.Slice(state.s3.Length);
                state.s4.AsSpan().CopyTo(remaining);
            });
        }

        /// <summary>
        /// Concatenates an array of strings and returns the result.
        /// </summary>
        /// <param name="strings">An array of strings.</param>
        ///
        /// <returns>The result of the concatenation of the strings in the array. If any of the strings
        /// is null, the string "null" is used in its place.</returns>
        public static string AS_add(string[] strings) {
            int totalLength = 0;

            for (int i = 0; i < strings.Length; i++) {
                string s = strings[i];
                totalLength = checked(totalLength + ((s != null) ? s.Length : 4));
            }

            return String.Create(totalLength, strings, (dest, _strings) => {
                Span<char> remaining = dest;
                for (int i = 0; i < _strings.Length; i++) {
                    string s = _strings[i] ?? "null";
                    s.AsSpan().CopyTo(remaining);
                    remaining = remaining.Slice(s.Length);
                }
            });
        }

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

            return ASObject.AS_lessEq(s1, s2);
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
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
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
        /// <param name="index">The position of the character.</param>
        /// <returns>The string representation of the character at position <paramref name="index"/>
        /// in the string, or an empty string if <paramref name="index"/> is out of
        /// bounds.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public string charAt(double index = 0) => charAt(m_value, index);

        /// <summary>
        /// Returns the code of the character at the given position in the string.
        /// </summary>
        /// <param name="index">The position of the character.</param>
        /// <returns>The code of the character at position <paramref name="index"/> in the string,
        /// or NaN if <paramref name="index"/> is out of bounds.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public double charCodeAt(double index = 0) => charCodeAt(m_value, index);

        /// <summary>
        /// Converts the objects in an array to strings and concatenates them to the current string.
        /// </summary>
        /// <param name="args">The arguments to concatenate to the string.</param>
        /// <returns>The concatenated string.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
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
        public int indexOf(string searchStr, double startIndex = 0) => indexOf(m_value, searchStr, startIndex);

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
        public int lastIndexOf(string searchStr, double startIndex = Int32.MaxValue) => lastIndexOf(m_value, searchStr, startIndex);

        /// <summary>
        /// Compares the string to <paramref name="other"/> using a locale-specific comparison.
        /// </summary>
        /// <param name="other">The string to compare with the current string.</param>
        /// <returns>A negative number if the string is less than <paramref name="other"/>, zero if
        /// it is equal to <paramref name="other"/> or a positive number if it is greater than
        /// <paramref name="other"/>.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public int localeCompare(ASAny other = default) => localeCompare(m_value, other);

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
        public ASArray match(ASAny regExp = default) => match(m_value, regExp);

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
        public string replace(ASAny search = default, ASAny repl = default) => replace(m_value, search, repl);

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
        public int search(ASAny regExp = default) => search(m_value, regExp);

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
        public string slice(double startIndex = 0, double endIndex = Int32.MaxValue) => slice(m_value, startIndex, endIndex);

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
        /// the input string, to be included in the returned array. If this is undefined, there is no
        /// limit.</param>
        ///
        /// <returns>An array containing the split substrings.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public ASArray split(ASAny sep, ASAny limit = default) => split(m_value, sep, limit);

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
        public string substr(double startIndex = 0, double len = Int32.MaxValue) => substr(m_value, startIndex, len);

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
        /// This differs from <see cref="slice(Double, Double)"/> in how negative values of
        /// <paramref name="startIndex"/> and <paramref name="endIndex"/> are handled (they are
        /// set to 0, but in <see cref="slice(Double, Double)"/> they are taken as indices
        /// relative to the end of the string).
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public string substring(double startIndex = 0, double endIndex = Int32.MaxValue) => substring(m_value, startIndex, endIndex);

        /// <summary>
        /// Converts all alphabetic characters in the string to lowercase in a locale-specific manner.
        /// </summary>
        /// <returns>The string with alphabetic characters converted to lowercase.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public string toLocaleLowerCase() => toLocaleLowerCase(m_value);

        /// <summary>
        /// Converts all alphabetic characters in the string to uppercase in a locale-specific manner.
        /// </summary>
        /// <returns>Converts all alphabetic characters in the string to uppercase.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public string toLocaleUpperCase() => toLocaleUpperCase(m_value);

        /// <summary>
        /// Converts all alphabetic characters in the string to lowercase.
        /// </summary>
        /// <returns>Converts all alphabetic characters in the string to lowercase.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public string toLowerCase() => toLowerCase(m_value);

        /// <summary>
        /// Converts all alphabetic characters in the string to uppercase.
        /// </summary>
        /// <returns>Converts all alphabetic characters in the string to uppercase.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
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
        public new string AS_toString() => m_value;

        /// <summary>
        /// Returns the primitive type representation of the object.
        /// </summary>
        /// <returns>A primitive representation of the object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
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
        /// Converts a floating-point index passed in to some String instance methods to an integer index.
        /// </summary>
        /// <param name="index">The floating-point index.</param>
        /// <returns>The integer index computed from <paramref name="index"/>. If the integer index is
        /// negative, returns -1. If the integer index is greater than the maximum value for the
        /// <see cref="Int32"/> type, returns the maximum value.</returns>
        private static int _indexToInteger(double index) {
            if (Double.IsNaN(index))
                return 0;

            if (index <= -1.0)
                return -1;

            if (index >= (double)Int32.MaxValue)
                return Int32.MaxValue;

            return (int)index;
        }

        /// <summary>
        /// Converts a floating-point index used in some String instance methods to an integer index,
        /// treating negative indices as relative to the end of the string.
        /// </summary>
        /// <param name="index">The floating-point index.</param>
        /// <param name="stringLength">The length of the string relative to which negative indices are
        /// to be converted, and to which positive indices are to be bounded.</param>
        /// <returns>The integer index. This is always non-negative and not greater than
        /// <paramref name="stringLength"/></returns>
        private static int _relativeIndexToInteger(double index, int stringLength) {
            if (Double.IsNaN(index))
                return 0;

            if (index > -1.0)
                return (int)Math.Min(index, (double)stringLength);

            return (int)Math.Max(Math.Truncate(index) + (double)stringLength, 0.0);
        }

        /// <summary>
        /// Returns the string representation of the character at the given position in the string.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <param name="index">The position in the string at which to return the character.</param>
        /// <returns>The string representation of the character at position <paramref name="index"/>
        /// in the string, or the empty string if <paramref name="index"/> is out of
        /// bounds.</returns>
        [AVM2ExportPrototypeMethod]
        public static string charAt(string s, double index = 0) => charAt(s, _indexToInteger(index));

        /// <summary>
        /// Returns the string representation of the character at the given position in the string.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <param name="index">The position in the string at which to return the character.</param>
        /// <returns>The string representation of the character at position <paramref name="index"/>
        /// in the string, or the empty string if <paramref name="index"/> is out of
        /// bounds.</returns>
        public static string charAt(string s, int index) {
            if ((uint)index >= (uint)s.Length)
                return "";

            int ch = s[index];
            var cache = s_singleCharCachedValues;
            return ((uint)ch < (uint)cache.Length) ? cache[ch] : new string((char)ch, 1);
        }

        /// <summary>
        /// Returns the Unicode code point of the character at the given position in the string.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <param name="index">The position in the string at which to return the character.</param>
        /// <returns>The code point of the character at position <paramref name="index"/> in the
        /// string <paramref name="s"/>, or NaN if <paramref name="index"/> is out of
        /// bounds.</returns>
        [AVM2ExportPrototypeMethod]
        public static double charCodeAt(string s, double index = 0) => charCodeAt(s, _indexToInteger(index));

        /// <summary>
        /// Returns the Unicode code point of the character at the given position in the string.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <param name="index">The position in the string at which to return the character.</param>
        /// <returns>The code point of the character at position <paramref name="index"/> in the
        /// string <paramref name="s"/>, or NaN if <paramref name="index"/> is out of
        /// bounds.</returns>
        public static double charCodeAt(string s, int index) =>
            (uint)index >= (uint)s.Length ? Double.NaN : (double)s[index];

        /// <summary>
        /// Converts the objects in an array to strings and concatenates them to the first string.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <param name="args">The arguments to concatenate to the string.</param>
        /// <returns>The string created by concatenation of the string <paramref name="s"/> with the
        /// strings created from objects in the <paramref name="args"/> array.</returns>
        [AVM2ExportPrototypeMethod]
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
        /// Returns the index of the first occurence of the string <paramref name="searchStr"/> in
        /// <paramref name="s"/> at or after <paramref name="startIndex"/>, or -1 if
        /// <paramref name="searchStr"/> is not found in <paramref name="s"/>.
        /// </summary>
        ///
        /// <param name="s">The string to search for <paramref name="searchStr"/>.</param>
        /// <param name="searchStr">The string to match in <paramref name="s"/>.</param>
        /// <param name="startIndex">The position from where to start searching.</param>
        /// <returns>The first position of <paramref name="searchStr"/> in
        /// <paramref name="s"/>.</returns>
        [AVM2ExportPrototypeMethod]
        public static int indexOf(string s, string searchStr, double startIndex = 0) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            if (searchStr == null)
                return -1;

            int iStartIndex = Math.Min(Math.Max(_indexToInteger(startIndex), 0), s.Length);

            if (iStartIndex > s.Length - searchStr.Length)
                return -1;

            if (searchStr.Length == 0)
                return iStartIndex;

            if (searchStr.Length == 1)
                return s.IndexOf(searchStr[0], iStartIndex);

            return s.IndexOf(searchStr, iStartIndex, StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns the index of the last occurence of the string <paramref name="searchStr"/> in
        /// <paramref name="s"/> at or before <paramref name="startIndex"/>, or -1 if
        /// <paramref name="searchStr"/> is not found in <paramref name="s"/>.
        /// </summary>
        ///
        /// <param name="s">The string to search for <paramref name="searchStr"/>.</param>
        /// <param name="searchStr">The string to match in <paramref name="s"/>.</param>
        /// <param name="startIndex">The position in <paramref name="s"/> from where to start
        /// searching. For a match to be valid, the index of the first character of the substring
        /// of <paramref name="s"/> that matches <paramref name="searchStr"/> must be not greater
        /// than this index.</param>
        /// <returns>The first position of <paramref name="searchStr"/> in
        /// <paramref name="s"/>.</returns>
        [AVM2ExportPrototypeMethod]
        public static int lastIndexOf(string s, string searchStr, double startIndex = Int32.MaxValue) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            if (searchStr == null)
                return -1;

            // For lastIndexOf, startIndex = NaN should be taken as infinity (not zero) as per ECMA-262 spec
            int iStartIndex = Double.IsNaN(startIndex) ? s.Length : _indexToInteger(startIndex);
            iStartIndex = Math.Min(iStartIndex, s.Length - searchStr.Length);

            if (iStartIndex < 0)
                return -1;

            if (searchStr.Length == 0)
                return iStartIndex;

            if (searchStr.Length == 1)
                return s.LastIndexOf(searchStr[0], iStartIndex);

            // String.LastIndexOf in .NET takes startIndex to be the maximum index of the last character of
            // the match, not the first character as in ECMAScript/AS3. So we need to adjust the startIndex.
            return s.LastIndexOf(searchStr, iStartIndex + searchStr.Length - 1, StringComparison.Ordinal);
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
        [AVM2ExportPrototypeMethod]
        public static int localeCompare(string s, ASAny other = default) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);
            return String.Compare(s, ASAny.AS_convertString(other), StringComparison.CurrentCulture);
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
        [AVM2ExportPrototypeMethod]
        public static ASArray match(string s, ASAny regExp) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            if (!(regExp.value is ASRegExp re))
                re = new ASRegExp(ASAny.AS_convertString(regExp.value));

            if (re.global) {
                MatchCollection mc = re.getInternalRegex().Matches(s);
                var matchArray = new ASArray(mc.Count);

                for (int i = 0, n = mc.Count; i < n; i++)
                    matchArray.AS_setElement(i, mc[i].Value);

                re.lastIndex = 0;
                return matchArray;
            }
            else {
                return (ASArray)re.exec(s);
            }
        }

        /// <summary>
        /// Returns a new string with matches of the <paramref name="search"/> string or RegExp
        /// replaced with a replacement string or strings returned by a callback function.
        /// </summary>
        /// <param name="input">The source string.</param>
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
        [AVM2ExportPrototypeMethod]
        public static string replace(string input, ASAny search, ASAny repl) {
            if (input == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            var replFunction = repl.value as ASFunction;
            var searchRegex = search.value as ASRegExp;
            string searchString = null, replString = null;

            // If repl is not a function, convert to a string.
            if (replFunction == null)
                replString = ASAny.AS_convertString(repl);

            // If search is not a RegExp, convert to a string.
            if (searchRegex == null)
                searchString = ASAny.AS_convertString(search);

            // isGlobal is true if all matches in the input string are to be replaced.
            // Otherwise, only the first match is replaced.
            bool isGlobal = searchRegex != null && searchRegex.global;

            ASAny[] callbackArgs = null;

            if (replFunction != null) {
                callbackArgs = new ASAny[3 + ((searchRegex != null) ? searchRegex.groupCount : 0)];
                callbackArgs[callbackArgs.Length - 1] = input;
            }

            // Determine the index of the first match. If the first match has not been found, return the input
            // string, as there are no replacements to be made.
            int matchIndex;
            Match regexMatch = null;

            if (searchRegex == null) {
                matchIndex = input.IndexOf(searchString, StringComparison.Ordinal);
                if (matchIndex == -1)
                    return input;
            }
            else {
                regexMatch = searchRegex.getInternalRegex().Match(input);
                if (!regexMatch.Success)
                    return input;
                matchIndex = regexMatch.Index;
            }

            ParsedReplaceString parsedReplString = default;

            if (searchRegex != null && replString != null)
                parsedReplString = new ParsedReplaceString(replString, searchRegex.groupCount);

            int srcLastIndex = 0;
            var resultBuilder = new ReplaceResultBuilder(input, replString);

            while (true) {
                int matchLength = (searchRegex != null) ? regexMatch.Length : searchString.Length;
                resultBuilder.addSourceStringSpan(srcLastIndex, matchIndex - srcLastIndex);

                if (replFunction != null)
                    resultBuilder.addOtherString(getCurrentReplaceStringFromCallback());
                else if (searchRegex != null)
                    parsedReplString.resolveWithMatch(regexMatch, ref resultBuilder);
                else
                    resultBuilder.addReplaceStringSpan(0, replString.Length);

                srcLastIndex = matchIndex + matchLength;

                if (!isGlobal) {
                    // For a non-global replace, exit after the first match.
                    break;
                }

                // Do the next match.
                if (searchRegex != null) {
                    regexMatch = regexMatch.NextMatch();
                    if (!regexMatch.Success)
                        break;

                    matchIndex = regexMatch.Index;
                }
                else {
                    int nextMatchStart = matchIndex + Math.Max(matchLength, 1);
                    matchIndex = -1;

                    if (nextMatchStart <= searchString.Length)
                        matchIndex = input.IndexOf(searchString, nextMatchStart, StringComparison.Ordinal);

                    if (matchIndex == -1)
                        break;
                }
            }

            resultBuilder.addSourceStringSpan(srcLastIndex, input.Length - srcLastIndex);
            return resultBuilder.makeResult();

            string getCurrentReplaceStringFromCallback() {
                if (regexMatch == null) {
                    callbackArgs[0] = searchString;
                }
                else {
                    GroupCollection groups = regexMatch.Groups;
                    for (int i = 0; i < groups.Count; i++)
                        callbackArgs[i] = groups[i].Success ? groups[i].Value : ASAny.undefined;
                }
                callbackArgs[callbackArgs.Length - 2] = matchIndex;
                return ASAny.AS_convertString(replFunction.AS_invoke(ASAny.@null, callbackArgs));
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
        /// <param name="regExp">
        /// The RegExp object representing the regular expression to execute on the target string. If
        /// this is not a RegExp, it is converted to a string and used as the regular expression
        /// pattern, with no flags set. The RegExp object's
        /// <see cref="ASRegExp.lastIndex" qualifyHint="true"/> property is ignored; the search
        /// always starts at the beginning of the string.
        /// </param>
        ///
        /// <returns>The position of the first match of the regular expression in the string, or -1 if
        /// no match is found.</returns>
        [AVM2ExportPrototypeMethod]
        public static int search(string s, ASAny regExp) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            if (!(regExp.value is ASRegExp re))
                re = new ASRegExp(ASAny.AS_convertString(regExp));

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
        [AVM2ExportPrototypeMethod]
        public static string substr(string s, double startIndex = 0, double length = Int32.MaxValue) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            int iStartIndex = _relativeIndexToInteger(startIndex, s.Length);

            int iLength = Math.Max(_indexToInteger(length), 0);
            iLength = Math.Min(iLength, s.Length - iStartIndex);

            return s.Substring(iStartIndex, iLength);
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
        [AVM2ExportPrototypeMethod]
        public static string slice(string s, double startIndex = 0, double endIndex = Int32.MaxValue) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            int iStartIndex = _relativeIndexToInteger(startIndex, s.Length);
            int iEndIndex = _relativeIndexToInteger(endIndex, s.Length);

            if (iStartIndex > iEndIndex)
                return "";

            return s.Substring(iStartIndex, iEndIndex - iStartIndex);
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
        /// This differs from <see cref="slice(String, Double, Double)"/> in how negative values of start
        /// or end index are handled: They are set to 0, where as in <see cref="slice(String, Double, Double)"/>
        /// they are treated as indices relative to the end of the string).
        /// </remarks>
        [AVM2ExportPrototypeMethod]
        public static string substring(string s, double startIndex = 0, double endIndex = Int32.MaxValue) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            int iStartIndex = Math.Min(Math.Max(_indexToInteger(startIndex), 0), s.Length);
            int iEndIndex = Math.Min(Math.Max(_indexToInteger(endIndex), 0), s.Length);

            if (iStartIndex == iEndIndex)
                return "";

            if (iEndIndex < iStartIndex)
                (iStartIndex, iEndIndex) = (iEndIndex, iStartIndex);

            return s.Substring(iStartIndex, iEndIndex - iStartIndex);
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
        /// the input string, to be included in the returned array. If this is undefined,
        /// there is no limit.</param>
        ///
        /// <returns>An array containing the split substrings.</returns>
        [AVM2ExportPrototypeMethod]
        public static ASArray split(string s, ASAny sep, ASAny limit = default) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            uint uLimit = limit.isUndefined ? UInt32.MaxValue : (uint)limit;
            uLimit = Math.Min(uLimit, (uint)Int32.MaxValue);

            if (sep.value is ASRegExp re)
                return _internalSplitByRegExp(s, re, (int)uLimit);
            else
                return _internalSplitByString(s, ASAny.AS_convertString(sep), (int)uLimit);
        }

        private static ASArray _internalSplitByString(string s, string sep, int limit) {
            ASArray result = new ASArray();

            if (limit == 0)
                return result;

            if (s.Length == 0) {
                // From ECMA-262:
                // If the this object is (or converts to) the empty string, the result depends on whether separator
                // can match the empty string. If it can, the result array contains no elements. Otherwise,
                // the result array contains one element, which is the empty string.
                if (sep.Length > 0)
                    result.push("");

                return result;
            }

            if (sep.Length == 0) {
                // Empty-string separator, split into single characters.
                ReadOnlySpan<char> span = s.AsSpan(0, Math.Min(limit, s.Length));

                ASObject[] singleCharCache = s_lazySingleCharCachedObjects.value;

                for (int i = 0; i < span.Length; i++) {
                    int ch = span[i];
                    ASObject charStrObj = ((uint)ch < (uint)singleCharCache.Length)
                        ? singleCharCache[ch]
                        : ASObject.AS_fromString(new string((char)ch, 1));

                    result.push(charStrObj);
                }
            }
            else if (sep.Length == 1) {
                // One-character separator

                char sepChar = sep[0];
                int lastIndex = 0;

                while (result.length < (uint)limit) {
                    int nextIndex = s.IndexOf(sepChar, lastIndex);

                    if (nextIndex == -1) {
                        // No more separators found. This correctly handles the case where the separator is
                        // the last character in the string, adding an empty string at the end.
                        result.push(s.Substring(lastIndex));
                        break;
                    }
                    else {
                        result.push(s.Substring(lastIndex, nextIndex - lastIndex));
                        lastIndex = nextIndex + 1;
                    }
                }
            }
            else {
                int lastIndex = 0;

                while (result.length < limit) {
                    int nextIndex = s.IndexOf(sep, lastIndex, StringComparison.Ordinal);

                    if (nextIndex == -1) {
                        // No more separators found. This correctly handles the case where the separator is
                        // the last character in the string, adding an empty string at the end.
                        result.push(s.Substring(lastIndex));
                        break;
                    }
                    else {
                        result.push(s.Substring(lastIndex, nextIndex - lastIndex));
                        lastIndex = nextIndex + sep.Length;
                    }
                }
            }

            return result;
        }

        private static ASArray _internalSplitByRegExp(string s, ASRegExp sep, int limit) {
            ASArray result = new ASArray();

            if (limit == 0)
                return result;

            if (s.Length == 0) {
                // From ECMA-262:
                // If the this object is (or converts to) the empty string, the result depends on whether separator
                // can match the empty string. If it can, the result array contains no elements. Otherwise,
                // the result array contains one element, which is the empty string.
                if (!sep.getInternalRegex().IsMatch(s))
                    result.push("");

                return result;
            }

            int lastIndex = 0;
            Match currentMatch = null;

            while (result.length < limit) {
                if (currentMatch == null)
                    currentMatch = sep.getInternalRegex().Match(s);
                else
                    currentMatch = currentMatch.NextMatch();

                if (!currentMatch.Success) {
                    result.push(s.Substring(lastIndex));
                    break;
                }

                int matchIndex = currentMatch.Index;
                int matchLength = currentMatch.Length;

                if (matchLength == 0 && (matchIndex == lastIndex || matchIndex == s.Length)) {
                    // From ECMA-262:
                    // separator does not match the empty substring at the beginning or end of the input string,
                    // nor does it match the empty substring at the end of the previous separator match
                    continue;
                }

                result.push(s.Substring(lastIndex, matchIndex - lastIndex));

                // From ECMA-262:
                // If separator is a regular expression that contains capturing parentheses, then each time
                // separator is matched the results (including any undefined results) of the capturing
                // parentheses are spliced into the output array.
                GroupCollection matchGroups = currentMatch.Groups;
                for (int i = 1; i < matchGroups.Count; i++) {
                    Group group = matchGroups[i];
                    result.push(group.Success ? group.Value : ASAny.undefined);
                }

                lastIndex = matchIndex + matchLength;
            }

            return result;
        }

        /// <summary>
        /// Converts all alphabetic characters in the string to lowercase.
        /// </summary>
        /// <param name="s">The string.</param>
        /// <returns>The string with alphabetic characters converted to lowercase.</returns>
        [AVM2ExportPrototypeMethod]
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
        [AVM2ExportPrototypeMethod]
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
        [AVM2ExportPrototypeMethod]
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
        [AVM2ExportPrototypeMethod]
        public static string toLocaleUpperCase(string s) {
            if (s == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);
            return s.ToUpper(CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Returns the given argument. This is used by the ABC compiler for calls to the
        /// <c>toString</c> method on String values.
        /// </summary>
        /// <param name="s">The argument.</param>
        /// <returns>The value of <paramref name="s"/>.</returns>
        [AVM2ExportPrototypeMethod]
        public static string toString(string s) => valueOf(s);

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
        /// ABC to IL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) =>
            ASAny.AS_fromString((args.Length != 0) ? ASAny.AS_convertString(args[0]) : "");

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABC to IL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_CONSTRUCT(ReadOnlySpan<ASAny> args) => __AS_INVOKE(args);

        internal static void __AS_CLASS_LOADED(ClassImpl klass) {
            klass.classObject.AS_dynamicProps.setValue(
                nameof(fromCharCode),
                klass.getMethod(new QName(Namespace.AS3, nameof(fromCharCode)), TraitScope.STATIC).createMethodClosure()
            );
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
                int cv = val[0];
                var cache = s_lazySingleCharCachedObjects.value;
                if ((uint)cv < (uint)cache.Length)
                    return cache[cv];
            }

            return new ASString(val);
        }

        /// <summary>
        /// Helper for constructing replacement strings for the replace() method with minimal intermediate
        /// allocations.
        /// </summary>
        private struct ReplaceResultBuilder {

            private struct Slice {
                public int id;
                public int start;
                public int length;
            }

            private const int SOURCE_STR_ID = -1;
            private const int REPL_STR_ID = -2;

            private string m_sourceString;
            private string m_replaceString;
            private int m_resultLength;

            private DynamicArray<Slice> m_resultSlices;
            private DynamicArray<string> m_otherStrings;

            public ReplaceResultBuilder(string sourceStr, string replaceStr) {
                m_sourceString = sourceStr;
                m_replaceString = replaceStr;
                m_resultSlices = new DynamicArray<Slice>();
                m_otherStrings = new DynamicArray<string>();
                m_resultLength = 0;
            }

            public void addSourceStringSpan(int start) => addSourceStringSpan(start, m_sourceString.Length - start);

            public void addSourceStringSpan(int start, int length) {
                if (length == 0)
                    return;

                m_resultSlices.add(new Slice {id = SOURCE_STR_ID, start = start, length = length});
                m_resultLength += length;
            }

            public void addReplaceStringSpan(int start, int length) {
                if (length == 0)
                    return;

                m_resultSlices.add(new Slice {id = REPL_STR_ID, start = start, length = length});
                m_resultLength += length;
            }

            public void addOtherString(string str) => addOtherString(str, 0, str.Length);

            public void addOtherString(string str, int start, int length) {
                if (length == 0)
                    return;

                m_resultSlices.add(new Slice {id = m_otherStrings.length, start = start, length = length});
                m_otherStrings.add(str);
                m_resultLength += length;
            }

            public string makeResult() => String.Create(m_resultLength, this, _internalWriteResult);

            private static void _internalWriteResult(Span<char> resultSpan, ReplaceResultBuilder builder) {
                ReadOnlySpan<Slice> slices = builder.m_resultSlices.asSpan();
                Span<char> remaining = resultSpan;

                for (int i = 0; i < slices.Length; i++) {
                    ref readonly Slice slice = ref slices[i];

                    string sliceStr;
                    if (slice.id == SOURCE_STR_ID)
                        sliceStr = builder.m_sourceString;
                    else if (slice.id == REPL_STR_ID)
                        sliceStr = builder.m_replaceString;
                    else
                        sliceStr = builder.m_otherStrings[slice.id];

                    sliceStr.AsSpan(slice.start, slice.length).CopyTo(remaining);
                    remaining = remaining.Slice(slice.length);
                }
            }

        }

        /// <summary>
        /// Represents a parsed replacement string containing references to capturing groups that
        /// must be substituted during replacement.
        /// </summary>
        private struct ParsedReplaceString {

            private enum TokenType : byte {
                LITERAL,
                MATCH,
                GROUP,
                BEFORE,
                AFTER,
            }

            private readonly struct Token {
                public readonly TokenType type;
                public readonly int startIndexOrGroup;
                public readonly int length;

                public Token(TokenType type, int startIndexOrGroup = 0, int length = 0) {
                    this.type = type;
                    this.startIndexOrGroup = startIndexOrGroup;
                    this.length = length;
                }
            }

            private DynamicArray<Token> m_tokens;

            public ParsedReplaceString(string replString, int groupCount) {
                m_tokens = new DynamicArray<Token>();

                int curIndex = 0;

                while (curIndex < replString.Length) {
                    int placeholderIndex = replString.IndexOf('$', curIndex);

                    if (placeholderIndex == -1 || placeholderIndex == replString.Length - 1) {
                        m_tokens.add(new Token(TokenType.LITERAL, curIndex, replString.Length - curIndex));
                        break;
                    }

                    switch (replString[placeholderIndex + 1]) {
                        case '$':
                            // Escape, literal dollar sign
                            m_tokens.add(new Token(TokenType.LITERAL, curIndex, placeholderIndex - curIndex + 1));
                            curIndex = placeholderIndex + 2;
                            break;

                        case '&':
                            // Matched string
                            if (placeholderIndex != curIndex)
                                m_tokens.add(new Token(TokenType.LITERAL, curIndex, placeholderIndex - curIndex));
                            m_tokens.add(new Token(TokenType.MATCH));
                            curIndex = placeholderIndex + 2;
                            break;

                        case '`':
                        case '\'':
                            // String preceding/following the matched string
                            if (placeholderIndex != curIndex)
                                m_tokens.add(new Token(TokenType.LITERAL, curIndex, placeholderIndex - curIndex));
                            m_tokens.add(new Token((replString[placeholderIndex + 1] == '`') ? TokenType.BEFORE : TokenType.AFTER));
                            curIndex = placeholderIndex + 2;
                            break;

                        default: {
                            // Try to find a capturing group number.
                            // If the group number is 0 or greater than the number of groups in the regex,
                            // interpret these references as literals.

                            int groupNumber = _readGroupNumber(replString.AsSpan(placeholderIndex + 1), groupCount, out int charsRead);

                            if (groupNumber == -1 || groupNumber == 0) {
                                m_tokens.add(new Token(TokenType.LITERAL, curIndex, placeholderIndex - curIndex + 1));
                                curIndex = placeholderIndex + 1;
                            }
                            else {
                                if (placeholderIndex != curIndex)
                                    m_tokens.add(new Token(TokenType.LITERAL, curIndex, placeholderIndex - curIndex));

                                m_tokens.add(new Token(TokenType.GROUP, groupNumber));
                                curIndex = placeholderIndex + charsRead + 1;
                            }
                            break;
                        }
                    }
                }
            }

            private static int _readGroupNumber(ReadOnlySpan<char> span, int groupCount, out int charsRead) {
                charsRead = 0;
                char first = span[0];

                if ((uint)(first - '0') > 9)
                    return -1;

                if (span.Length >= 2) {
                    // Attempt to find a two-digit number.
                    char second = span[1];
                    if ((uint)(second - '0') <= 9) {
                        int twoDigitIndex = (first - '0') * 10 + (second - '0');
                        if (twoDigitIndex <= groupCount) {
                            charsRead = 2;
                            return twoDigitIndex;
                        }
                    }
                }

                int oneDigitNum = first - '0';
                if (oneDigitNum <= groupCount) {
                    charsRead = 1;
                    return oneDigitNum;
                }

                return -1;
            }

            public void resolveWithMatch(Match match, ref ReplaceResultBuilder resultBuilder) {
                var tokens = m_tokens.asSpan();

                for (int i = 0; i < tokens.Length; i++) {
                    ref readonly Token tk = ref tokens[i];

                    switch (tk.type) {
                        case TokenType.LITERAL:
                            resultBuilder.addReplaceStringSpan(tk.startIndexOrGroup, tk.length);
                            break;
                        case TokenType.MATCH:
                            resultBuilder.addSourceStringSpan(match.Index, match.Length);
                            break;
                        case TokenType.BEFORE:
                            resultBuilder.addSourceStringSpan(0, match.Index);
                            break;
                        case TokenType.AFTER:
                            resultBuilder.addSourceStringSpan(match.Index + match.Length);
                            break;

                        case TokenType.GROUP: {
                            Group group = match.Groups[tk.startIndexOrGroup];
                            if (group.Success)
                                resultBuilder.addSourceStringSpan(group.Index, group.Length);
                            break;
                        }
                    }
                }
            }

        }

    }

}
