using System;
using System.Text.RegularExpressions;
using System.Threading;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The RegExp class represents a regular expression which can be used to search for a
    /// particular pattern of text in a string.
    /// </summary>
    [AVM2ExportClass(name = "RegExp", isDynamic = true, hasPrototypeMethods = true)]
    [AVM2ExportClassInternal(tag = ClassTag.REGEXP)]
    public class ASRegExp : ASObject {

        /// <summary>
        /// The value of the "length" property of the AS3 RegExp class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        private const int AUX_FLAG_GLOBAL = 1;
        private const int AUX_FLAG_DOTALL = 2;
        private const int AUX_FLAG_EXTENDED = 4;
        private const int AUX_FLAG_MULTILINE = 8;

        // These are set to non-null values by _init
        private Regex m_internalRegex = null!;
        private string m_source = null!;

        private int m_auxFlags;
        private int m_lastIndex;
        private string[]? m_groupNames;
        private int m_groupCount;

        /// <summary>
        /// Creates a copy of the given RegExp object. All properties except <see cref="lastIndex"/>
        /// will be copied.
        /// </summary>
        /// <param name="re">The RegExp object of which to create a copy.</param>
        public ASRegExp(ASRegExp re) {
            m_internalRegex = re.m_internalRegex;
            m_auxFlags = re.m_auxFlags;
            m_groupNames = re.m_groupNames;
            m_groupCount = re.m_groupCount;
            m_source = re.m_source;
        }

        /// <summary>
        /// This constructor implements the ActionScript 3 RegExp constructor.
        /// </summary>
        /// <param name="rest">The constructor arguments. This may be a pattern string, a pattern
        /// string and a flags string, or another RegExp object.</param>
        [AVM2ExportTrait]
        public ASRegExp(RestParam rest) {
            if (rest.length >= 1 && rest[0].value is ASRegExp otherRegExp) {
                if (rest.length >= 2 && !rest[1].isUndefined)
                    throw ErrorHelper.createError(ErrorCode.REGEXP_CONSTRUCT_COPY_FLAGS);

                m_internalRegex = otherRegExp.m_internalRegex;
                m_auxFlags = otherRegExp.m_auxFlags;
                m_groupNames = otherRegExp.m_groupNames;
                m_groupCount = otherRegExp.m_groupCount;
                m_source = otherRegExp.m_source;
            }
            else {
                string pattern = (rest.length >= 1 && !rest[0].isUndefined) ? ASAny.AS_convertString(rest[0]) : "";
                string flags = (rest.length >= 2 && !rest[1].isUndefined) ? ASAny.AS_convertString(rest[1]) : "";
                _init(pattern, flags);
            }
        }

        /// <summary>
        /// Creates a new RegExp object from a regular expression pattern and flags.
        /// </summary>
        /// <param name="pattern">The pattern string of the regular expression.</param>
        /// <param name="flags">The flags for the regular expression this must be a string which may
        /// contain one or more of the following characters: m, i, g, x or s.</param>
        ///
        /// <remarks>
        /// <para>The characters in the <paramref name="flags"/> string are interpreted as
        /// follows:</para>
        /// <list type="bullet">
        /// <item><description>m (multiline): If this is set, the '^' and '$' characters in the pattern matches the
        /// beginning and end of lines in the target string respectively, instead of the beginning and
        /// end of the entire string.</description></item>
        /// <item><description>i (ignoreCase): If this is set, any alphabetic character in the pattern string
        /// matches both its uppercase and lowercase forms in the target string.</description></item>
        /// <item><description>
        /// g (global): If this is set, the
        /// <see cref="ASString.match(String, ASAny)" qualifyHint="true"/> method of the String
        /// class returns an array of all matches of the regular expression in the target string
        /// instead of only the first match, the
        /// <see cref="ASString.replace(String, ASAny, ASAny)" qualifyHint="true"/> method of the
        /// String class replaces all matches of the regular expression in the target string instead
        /// of replacing only the first match, and the <see cref="exec"/> method
        /// will set the <see cref="lastIndex"/> property to the index of the character immediately
        /// after the matched substring. All other native methods that accept RegExp arguments do not
        /// use this flag.
        /// </description></item>
        /// <item><description>s (dotall): If this is set, the '.' character in the pattern string matches any
        /// character in the target string, including newlines. If this is not set, the dot character
        /// does not match newlines.</description></item>
        /// <item><description>
        /// x (extended): If this is set, any white space and/or new lines in the pattern string are
        /// ignored; except in character classes, when escaped with a backslash, inside the curly
        /// braces of numeric quantifiers and before, in between or after the special character(s)
        /// that occur after the '(' in a special group construct (such as <c>'(?=...)'</c>).
        /// </description></item>
        /// </list>
        /// </remarks>
        public ASRegExp(string pattern, string flags = "") => _init(pattern, flags);

        /// <summary>
        /// Initializes the RegExp object using the given pattern and flags.
        /// </summary>
        /// <param name="pattern">The regular expression pattern string.</param>
        /// <param name="flags">The flags string of the regular expression.</param>
        private void _init(string? pattern, string? flags) {
            pattern = ASString.AS_convertString(pattern);
            flags = ASString.AS_convertString(flags);

            m_source = pattern;
            RegexOptions regexOptions = RegexOptions.CultureInvariant | RegexOptions.ECMAScript;

            for (int i = 0; i < flags.Length; i++) {
                switch (flags[i]) {
                    case 'i':
                        regexOptions |= RegexOptions.IgnoreCase;
                        break;
                    case 'x':
                        m_auxFlags |= AUX_FLAG_EXTENDED;
                        break;
                    case 'm':
                        m_auxFlags |= AUX_FLAG_MULTILINE;
                        break;
                    case 's':
                        m_auxFlags |= AUX_FLAG_DOTALL;
                        break;
                    case 'g':
                        m_auxFlags |= AUX_FLAG_GLOBAL;
                        break;
                }
            }

            var transpiler = new RegexTranspiler();
            transpiler.transpile(
                pattern,
                (m_auxFlags & AUX_FLAG_MULTILINE) != 0,
                (m_auxFlags & AUX_FLAG_DOTALL) != 0,
                (m_auxFlags & AUX_FLAG_EXTENDED) != 0
            );

            m_internalRegex = new Regex(transpiler.transpiledPattern, regexOptions);

            m_groupNames = transpiler.getGroupNames();
            m_groupCount = transpiler.groupCount;
        }

        /// <summary>
        /// Used for lazily constructing a RegExp instance with constant pattern and flags
        /// strings in code generated by the ABC compiler.
        /// </summary>
        ///
        /// <param name="pattern">The regular expression pattern string.</param>
        /// <param name="flags">The flags string of the regular expression.</param>
        /// <param name="location">A reference that holds the lazily constructed object.</param>
        ///
        /// <returns>If the value at <paramref name="location"/> is not null, returns a new
        /// <see cref="ASRegExp"/> that uses the same pattern and flags as that object.
        /// Otherwise, constructs a new instance from <paramref name="pattern"/> and
        /// <paramref name="flags"/>, stores it in <paramref name="location"/> and
        /// returns it.</returns>
        public static ASRegExp lazyConstructRegExp(string pattern, string flags, ref ASRegExp location) {
            ASRegExp value = Volatile.Read(ref location);

            if (value == null) {
                value = new ASRegExp(pattern, flags);
                Volatile.Write(ref location, value);
            }
            else {
                value = new ASRegExp(value);
            }

            return value;
        }

        /// <summary>
        /// Gets the internal <see cref="Regex"/> used by the current <see cref="ASRegExp"/>
        /// instance.
        /// </summary>
        internal Regex getInternalRegex() => m_internalRegex;

        /// <summary>
        /// Gets the number of capturing groups in the regular expression pattern.
        /// </summary>
        public int groupCount => m_groupCount;

        /// <summary>
        /// Gets the name of the capturing group with the given one-based index.
        /// </summary>
        /// <param name="index">The one-based index of the capturing group. This is determined by the
        /// position of its opening parenthesis in the pattern string with respect to the positions of
        /// the opening parentheses of all capturing groups.</param>
        /// <returns>The name of the group, of null if the group with the given index does not exist
        /// or has no name.</returns>
        public string? getGroupName(int index) {
            if (m_groupNames == null || index == 0 || (uint)index > (uint)m_groupCount)
                return null;

            return m_groupNames[index - 1];
        }

        /// <summary>
        /// Gets the source pattern string used to create the regular expression.
        /// </summary>
        [AVM2ExportTrait]
        public virtual string source => m_source;

        /// <summary>
        /// Gets the index in the string from where the next search starts.
        /// </summary>
        ///
        /// <remarks>
        /// This only affects the <see cref="test"/> and <see cref="exec"/> methods of a RegExp
        /// object with the global flag set. The <see cref="ASString.search(String, ASAny)" qualifyHint="true"/>,
        /// <see cref="ASString.match(String, ASAny)" qualifyHint="true"/> and
        /// <see cref="ASString.replace(String, ASAny, ASAny)" qualifyHint="true"/> methods of the
        /// String class and the <see cref="test"/> and <see cref="exec"/> methods on a RegExp instance
        /// without the global flag set ignore the value of this property and start all searches from
        /// the beginning. (However, if a RegExp object with the global flag set to true is passed to the
        /// <see cref="ASString.match(String, ASAny)" qualifyHint="true"/> or
        /// <see cref="ASString.replace(String, ASAny, ASAny)" qualifyHint="true"/> methods,
        /// this property is set to zero after the match.)
        /// </remarks>
        [AVM2ExportTrait]
        public virtual int lastIndex {
            get => m_lastIndex;
            set => m_lastIndex = value;
        }

        /// <summary>
        /// Gets a value indicating whether this RegExp object has the global ('g') flag set.
        /// </summary>
        ///
        /// <remarks>
        /// <para>If this flag is set:</para>
        /// <list type="bullet">
        /// <item><description>
        /// The <see cref="ASString.match(String, ASAny)" qualifyHint="true"/>
        /// method of the String class returns an array of all matches of the regular expression in
        /// the target string instead of only the first match, and sets the <see cref="lastIndex"/>
        /// property to 0.
        /// </description></item>
        /// <item><description>
        /// The <see cref="ASString.replace(String, ASAny, ASAny)" qualifyHint="true"/> method of the
        /// String class replaces all matches of the regular expression in the target string instead
        /// of replacing only the first match, and sets the <see cref="lastIndex"/> property to 0.
        /// </description></item>
        /// <item><description>
        /// The <see cref="exec"/> method will set the <see cref="lastIndex"/> property to the index
        /// of the character immediately after the matched substring.
        /// </description></item>
        /// <item><description>All other methods taking RegExp arguments are not affected.</description></item>
        /// </list>
        /// </remarks>
        [AVM2ExportTrait]
        public virtual bool global => (m_auxFlags & AUX_FLAG_GLOBAL) != 0;

        /// <summary>
        /// Gets a value indicating whether this RegExp object has the ignoreCase ('i') flag set.
        /// </summary>
        ///
        /// <remarks>
        /// If this is set, any alphabetic character in the pattern string matches both its uppercase
        /// and lowercase forms in the target string. This corresponds to the
        /// <see cref="RegexOptions.IgnoreCase" qualifyHint="true"/> option in .NET regex.
        /// </remarks>
        [AVM2ExportTrait]
        public virtual bool ignoreCase => (m_internalRegex.Options & RegexOptions.IgnoreCase) != 0;

        /// <summary>
        /// Gets a value indicating whether this RegExp object has the extended ('x') flag set.
        /// </summary>
        ///
        /// <remarks>
        /// If this is set, any white space (including new lines) in the pattern string are ignored,
        /// and the '#' character begins a line comment (anything from '#' to the end of the line is
        /// ignored). Whitespace characters and '#' will be treated as literal when escaped or when
        /// in a character class.
        /// </remarks>
        [AVM2ExportTrait]
        public virtual bool extended => (m_auxFlags & AUX_FLAG_EXTENDED) != 0;

        /// <summary>
        /// Gets a value indicating whether this RegExp object has the dotall ('s') flag set.
        /// </summary>
        ///
        /// <remarks>
        /// If this is set, the '.' character in the pattern string matches any character in the
        /// target string, including new lines. If this is not set, the dot character does not match
        /// new lines.
        /// </remarks>
        [AVM2ExportTrait]
        public virtual bool dotall => (m_auxFlags & AUX_FLAG_DOTALL) != 0;

        /// <summary>
        /// Gets a value indicating whether this RegExp object has the multiline ('m') flag set.
        /// </summary>
        ///
        /// <remarks>
        /// If this is set, the '^' and '$' characters in the pattern matches the beginning and end of
        /// lines in the target string respectively, instead of only the beginning and end of the
        /// entire string.
        /// </remarks>
        [AVM2ExportTrait]
        public virtual bool multiline => (m_auxFlags & AUX_FLAG_MULTILINE) != 0;

        /// <summary>
        /// Returns a Boolean value indicating whether the regular expression matches the given
        /// string.
        /// </summary>
        ///
        /// <param name="str">The target string to test against the regular expression.</param>
        /// <returns>A Boolean value indicating whether the regular expression matches the given
        /// string.</returns>
        ///
        /// <remarks>
        /// If the regular expression has the global flag set, the match starts at the index in the
        /// string given by the value of the <see cref="lastIndex"/> property, and that property is set
        /// to the index of the character immediately following the matched substring (or 0 if the
        /// match failed). If the global flag is not set, the match always starts at the beginning
        /// of the string and the <see cref="lastIndex"/> property is not changed.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual bool test(string str = "") {
            str = ASString.AS_convertString(str);

            if (!global)
                return m_internalRegex.IsMatch(str);

            if ((uint)m_lastIndex >= (uint)str.Length) {
                m_lastIndex = 0;
                return false;
            }

            Match match = m_internalRegex.Match(str, m_lastIndex);
            m_lastIndex = match.Success ? match.Index + match.Length : 0;
            return match.Success;
        }

        /// <summary>
        /// Executes the regular expression on the specified target string and returns an array
        /// containing information about the match. The match starts at the position in the target
        /// string given by the <see cref="lastIndex"/> property.
        /// </summary>
        ///
        /// <param name="str">The target string on which the regular expression must be
        /// executed.</param>
        /// <returns>An array containing information about the match. This includes the string matched
        /// by the entire pattern as well as each of the capturing groups, and the index in the target
        /// string at which the match was found. (See remarks below.) If the match fails, returns null.</returns>
        ///
        /// <remarks>
        /// <para>The returned array has the following:</para>
        /// <list type="bullet">
        /// <item><description>The substring of the target string matched by the entire regular expression pattern
        /// is stored at index 0 in the array.</description></item>
        /// <item><description>
        /// A substring matched by a capturing group in the regular expression pattern is stored at
        /// the one-based numeric index of that group in the array. The index of a group is determined
        /// by the position of its opening parenthesis in the pattern string.
        /// </description></item>
        /// <item><description>If a capturing group has a name, the substring matched by it is stored as a dynamic
        /// property in the returned array with that name.</description></item>
        /// <item><description>Two dynamic properties named <c>input</c> and <c>index</c> have the value of
        /// the target string (i.e., the <paramref name="str"/> argument) and the zero-based index
        /// of the substring matched by the whole pattern in the target string, respectively.</description></item>
        /// </list>
        /// <para>
        /// If the regular expression has the global flag set, the match starts at the index in the
        /// string given by the value of the <see cref="lastIndex"/> property, and that property is set
        /// to the index of the character immediately following the matched substring (or 0 if the
        /// match failed). If the global flag is not set, the match always starts at the beginning
        /// of the string and the <see cref="lastIndex"/> property is not changed.
        /// </para>
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual ASObject? exec(string str = "") {
            str = ASString.AS_convertString(str);

            bool global = this.global;
            int startIndex = global ? m_lastIndex : 0;

            execInternal(str, startIndex, out ASArray? result, out int newLastIndex);

            if (global)
                m_lastIndex = newLastIndex;

            return result;

            void execInternal(string s, int start, out ASArray? _result, out int _newLastIndex) {
                _result = null;
                _newLastIndex = 0;

                if ((uint)lastIndex >= (uint)str.Length)
                    return;

                Match match = m_internalRegex.Match(str, lastIndex);
                if (!match.Success)
                    return;

                _newLastIndex = match.Index + match.Length;

                // Create the group array
                GroupCollection groups = match.Groups;
                ASArray groupArray = new ASArray(groups.Count);
                DynamicPropertyCollection namedProps = groupArray.AS_dynamicProps!;

                for (int i = 0, n = groups.Count; i < n; i++) {
                    ASAny capturedValue = groups[i].Success ? (ASAny)groups[i].Value : ASAny.undefined;

                    groupArray[i] = capturedValue;
                    if (i != 0 && m_groupNames != null && m_groupNames[i - 1] != null)
                        namedProps[m_groupNames[i - 1]] = capturedValue;
                }

                namedProps["input"] = str;
                namedProps["index"] = match.Index;

                _result = groupArray;
            }
        }

        /// <inheritdoc/>
        public override bool AS_tryInvoke(ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) {
            // Calling a RegExp as a function calls exec()
            // See: https://github.com/adobe/avmplus/blob/master/core/RegExpObject.cpp#L208
            result = exec((args.Length == 0) ? "" : ASAny.AS_convertString(args[0]));
            return true;
        }

        /// <exclude/>
        ///
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABC to IL compiler to invoke the ActionScript RegExp class constructor. This must not be
        /// called by outside .NET code. RegExp objects constructed from .NET code must use the
        /// constructor defined on the <see cref="ASRegExp"/> type.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) {
            if (args.Length == 1 && args[0].value is ASRegExp re)
                return re;
            return new ASRegExp(new RestParam(args));
        }

    }

}
