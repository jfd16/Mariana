using System;
using System.Linq;
using Xunit;
using Mariana.AVM2.Core;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Mariana.AVM2.Tests {

    public class RegexTranspilerTest {

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("hello")]
        [InlineData("~`!@%&-_=:;]'/\"<>,")]
        [InlineData("\x00\x01\x05\ud800\udfff\uffff")]
        public void shouldTranspilePatternWithoutSpecialChars(string pattern) {
            _verifyPatternFlagsInvariant(pattern);
        }

        [Theory]
        [InlineData(@"\n\r\f\t\v\b")]
        [InlineData(@"\*\^\$\(\)\{\}\[\]\+\?\-\.\\")]
        [InlineData(@"\~\`\!\@\#\%\&\=\_\:\;\'\""\<\>\,", @"~`!@#%&=_:;'""<>,")]
        [InlineData("\\\n\\\r\\\t\\\f\\\v\\ ", "\n\r\t\f\v ")]
        [InlineData(@"\a\c\e\g\h\i\j\l\m\o\p\q\y\z", "aceghijlmopqyz")]
        [InlineData(@"\A\C\E\F\G\H\I\J\K\L\M\N\O\P\Q\R\T\U\V\X\Y\Z", "ACEFGHIJKLMNOPQRTUVXYZ")]
        [InlineData(@"ab\c\*de\nfg\h\&i\$", @"abc\*de\nfgh&i\$")]
        [InlineData(@"\X33", "X33")]
        [InlineData(@"\U033A", "U033A")]
        public void shouldTranspilePatternWithEscapedChars(string pattern, string expected = null) {
            _verifyPatternFlagsInvariant(pattern, expected);
        }

        [Theory]
        [InlineData(@"a*")]
        [InlineData(@"a+")]
        [InlineData(@"a?")]
        [InlineData(@"a*?")]
        [InlineData(@"a+?")]
        [InlineData(@"a??")]
        [InlineData(@"a*b")]
        [InlineData(@"a*b+&*?c")]
        [InlineData(@"\.*\n+\^*?q??\???")]
        public void shouldTranspilePatternWithQuantifiers(string pattern) {
            _verifyPatternFlagsInvariant(pattern);
        }

        [Theory]
        [InlineData(@"a{1}")]
        [InlineData(@"a{1,}")]
        [InlineData(@"a{1,2}")]
        [InlineData(@"a{1}?")]
        [InlineData(@"a{1,}?")]
        [InlineData(@"a{1,2}?")]
        [InlineData(@"ab*c{10}d{6,400}e?f{7,}?gh{4,12}?ij")]
        [InlineData(@"a{0}")]
        [InlineData(@"a{0,2147483647}")]
        [InlineData(@"a{2147483647}")]
        [InlineData(@"a{2147483647,}")]
        [InlineData(@"a{2147483647,2147483647}")]
        public void shouldTranspilePatternWithNumericQuantifiers(string pattern) {
            _verifyPatternFlagsInvariant(pattern);
        }

        [Theory]
        [InlineData("^")]
        [InlineData("$")]
        [InlineData("^abcd")]
        [InlineData("abcd$")]
        [InlineData(@"abc^def$gh")]
        [InlineData(@"^\^abc\w*?\$$")]
        public void shouldTranspilePatternWithStartEndAnchors(string pattern) {
            _verifyPatternFlagsInvariant(pattern);
        }

        [Theory]
        [InlineData("|")]
        [InlineData("a|")]
        [InlineData("|a")]
        [InlineData("||")]
        [InlineData("a|b")]
        [InlineData("a|b|c|d")]
        [InlineData(@"a*|b+?|cdef{1,2}|\r\w\S??")]
        [InlineData(@"^abc*|def?g+$|h")]
        public void shouldTranspilePatternWithPipe(string pattern) {
            _verifyPatternFlagsInvariant(pattern);
        }

        [Theory]
        [InlineData(".", @"[\s\S]")]
        [InlineData("abc...def.g", @"abc[\s\S][\s\S][\s\S]def[\s\S]g")]
        [InlineData(@"abc.*\n\[$", @"abc[\s\S]*\n\[$")]
        [InlineData(@".*.+.*?.+?.{1}.{1,}.{1,2}", @"[\s\S]*[\s\S]+[\s\S]*?[\s\S]+?[\s\S]{1}[\s\S]{1,}[\s\S]{1,2}")]
        [InlineData(@"^.$|ab.c|d", @"^[\s\S]$|ab[\s\S]c|d")]
        public void shouldTranspilePatternWithDot(string pattern, string expectedInDotAllMode) {
            var t = new RegexTranspiler();

            t.transpile(pattern, false, false);
            Assert.Equal(pattern, t.transpiledPattern);
            Assert.Equal(0, t.groupCount);

            t.transpile(pattern, false, true);
            Assert.Equal(pattern, t.transpiledPattern);
            Assert.Equal(0, t.groupCount);

            t.transpile(pattern, true, false);
            Assert.Equal(expectedInDotAllMode, t.transpiledPattern);
            Assert.Equal(0, t.groupCount);

            t.transpile(pattern, true, true);
            Assert.Equal(expectedInDotAllMode, t.transpiledPattern);
            Assert.Equal(0, t.groupCount);

            new Regex(pattern, RegexOptions.ECMAScript | RegexOptions.CultureInvariant);
            new Regex(expectedInDotAllMode, RegexOptions.ECMAScript | RegexOptions.CultureInvariant);
        }

        [Theory]
        [InlineData(@"\w")]
        [InlineData(@"\w\d\s\W\D\S")]
        [InlineData(@"\w*\d+\s??abc\w|^\d*_*$")]
        [InlineData(@"\w{0}\s{5,}\d+\w{20,30}")]
        public void shouldTranspilePatternWithBuiltinClasses(string pattern) {
            _verifyPatternFlagsInvariant(pattern);
        }

        [Theory]
        [InlineData(@"\x00")]
        [InlineData(@"\x09")]
        [InlineData(@"\x30")]
        [InlineData(@"\x3d")]
        [InlineData(@"\x3D")]
        [InlineData(@"\xa4")]
        [InlineData(@"\xA4")]
        [InlineData(@"\xff")]
        [InlineData(@"\xFF")]
        [InlineData(@"abc\x49+|\xa0*993\d$")]
        [InlineData(@"\u0000")]
        [InlineData(@"\u0038")]
        [InlineData(@"\u0497")]
        [InlineData(@"\u8956")]
        [InlineData(@"\uaaaa")]
        [InlineData(@"\uffff")]
        [InlineData(@"\uAAAA")]
        [InlineData(@"\uFFFF")]
        [InlineData(@"\u9c3d")]
        [InlineData(@"\u9C3D")]
        [InlineData(@"\u9c3D")]
        [InlineData(@"\u9C3d")]
        [InlineData(@"\x20_*\u03fd+|\ud834\udf4B\x7c|\u3c3E{4,5}g$")]
        public void shouldTranspilePatternWithHexEscapes(string pattern) {
            _verifyPatternFlagsInvariant(pattern);
        }

        public static IEnumerable<object[]> shouldTranspilePatternWithOctalEscapes_data = new (string, string)[] {
            (@"\0", @"\x00"),
            (@"\00", @"\x00"),
            (@"\1", @"\x01"),
            (@"\7", @"\x07"),
            (@"\07", @"\x07"),
            (@"\12", @"\x0A"),
            (@"\17", @"\x0F"),
            (@"\20", @"\x10"),
            (@"\46", @"\x26"),
            (@"\75", @"\x3D"),
            (@"\77", @"\x3F"),
            (@"\000", @"\x00"),
            (@"\007", @"\x07"),
            (@"\012", @"\x0A"),
            (@"\046", @"\x26"),
            (@"\077", @"\x3F"),
            (@"\255", @"\xAD"),
            (@"\377", @"\xFF"),
            (@"\0\7abc*\46{2,4}\5??\377|\255-\4-\13!", @"\x00\x07abc*\x26{2,4}\x05??\xFF|\xAD-\x04-\x0B!"),
            (@"\8", @"8"),
            (@"\9", @"9"),
            (@"\08", @"\x008"),
            (@"\78", @"\x078"),
            (@"\259", @"\x159"),
            (@"\400", @"\x200"),
            (@"\408", @"\x208"),
            (@"\777", @"\x3F7"),
            (@"\078", @"\x078"),
            (@"\087", @"\x0087"),
            (@"\287", @"\x0287"),
            (@"\779", @"\x3F9"),
            (@"\789", @"\x0789"),
            (@"\2550", @"\xAD0"),
            (@"\2590", @"\x1590"),
            (@"\3777", @"\xFF7"),
            (@"\800", @"800"),
            (@"\967", @"967"),
            (@"\0/", @"\x00/"),
            (@"\0:", @"\x00:"),
            (@"\2/", @"\x02/"),
            (@"\2:", @"\x02:"),
            (@"\0/\0:\07/\07:\066/\066:\4/\4:\46/\46:", @"\x00/\x00:\x07/\x07:\x36/\x36:\x04/\x04:\x26/\x26:"),
            (@"abc\2d", @"abc\x02d"),
            (@"abc\27d", @"abc\x17d"),
            (@"\12\23+\406*\377+abc|\779??_$", @"\x0A\x13+\x206*\xFF+abc|\x3F9??_$"),
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(shouldTranspilePatternWithOctalEscapes_data))]
        public void shouldTranspilePatternWithOctalEscapes(string pattern, string expected) {
            _verifyPatternFlagsInvariant(pattern, expected);
        }

        public static IEnumerable<object[]> shouldEscapeCurlyBracesIfNotValidQuantifier_data = new (string, string)[] {
            (@"{", @"\{"),
            (@"}", @"\}"),
            (@"{{{{", @"\{\{\{\{"),
            (@"{}", @"\{\}"),
            (@"{}*", @"\{\}*"),
            (@"abc{", @"abc\{"),
            (@"abc}", @"abc\}"),
            (@"abc{1", @"abc\{1"),
            (@"abc1}", @"abc1\}"),
            (@"abc{1>", @"abc\{1>"),
            (@"abc{1}}def", @"abc{1}\}def"),
            (@"{*", @"\{*"),
            (@"{{2}", @"\{{2}"),
            (@"{{{2,6}}}", @"\{\{{2,6}\}\}"),
            (@"abc{}", @"abc\{\}"),
            (@"abc{}def", @"abc\{\}def"),
            (@"abc{,}def", @"abc\{,\}def"),
            (@"abc{ }def", @"abc\{ \}def"),
            (@"abc{ 1 }def", @"abc\{ 1 \}def"),
            (@"abc{1 }def", @"abc\{1 \}def"),
            (@"abc{ 1}def", @"abc\{ 1\}def"),
            (@"abc{1-3}def", @"abc\{1-3\}def"),
            (@"abc{1, }def", @"abc\{1, \}def"),
            (@"abc{ 1,}def", @"abc\{ 1,\}def"),
            (@"abc{1, 4}def", @"abc\{1, 4\}def"),
            (@"abc{1,4 }def", @"abc\{1,4 \}def"),
            (@"abc{ 1,4}def", @"abc\{ 1,4\}def"),
            (@"abc{ 1,4 }def", @"abc\{ 1,4 \}def"),
            (@"abc{ 1, 4 }def", @"abc\{ 1, 4 \}def"),
            (@"abc{a}def", @"abc\{a\}def"),
            (@"abc{12a}def", @"abc\{12a\}def"),
            (@"abc{a,1}def", @"abc\{a,1\}def"),
            (@"abc{1,a}def", @"abc\{1,a\}def"),
            (@"abc{1,23a}def", @"abc\{1,23a\}def"),
            (@"a{1}b{2,}c*?{3,q}z", @"a{1}b{2,}c*?\{3,q\}z"),
            (@"\p{A}", @"p\{A\}"),
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(shouldEscapeCurlyBracesIfNotValidQuantifier_data))]
        public void shouldEscapeCurlyBracesIfNotValidQuantifier(string pattern, string expected) {
            var t = new RegexTranspiler();
            t.transpile(pattern, false, false);
            Assert.Equal(expected, t.transpiledPattern);
            Assert.Equal(0, t.groupCount);
            new Regex(t.transpiledPattern, RegexOptions.ECMAScript | RegexOptions.CultureInvariant);
        }

        public static IEnumerable<object[]> shouldTranspilePatternWithCharSets_data = new (string, string)[] {
            ("[a]", null),
            ("[abc]", null),
            ("[aaa]", null),
            (@"[]", @"(?!)"),
            (@"[^]", @"[\s\S]"),
            (@"[\b]", @"[\x08]"),
            (@"[-ab][ab-]", @"[\-ab][ab\-]"),
            (@"[^a]", @"[^a]"),
            (@"[^-a]", @"[^\-a]"),
            (@"[^-]", @"[^\-]"),
            (@"[^abc]", null),
            (@"[abc^][ab^c][^ab^c]", null),
            (@"[^^]", @"[^^]"),
            (@"[()$]", @"[()$]"),
            (@"[^()$]", @"[^()$]"),
            (@"[^[]", @"[^\[]"),
            (@"[[][ab[c]", @"[\[][ab\[c]"),
            (@"[^[][ab[c]", @"[^\[][ab\[c]"),
            (@"[\w\d\s][\W\D\S]", null),
            (@"[^\w\d\s][^\W\D\S]", null),
            (@"[^\wx34\b\S\x12\u30ff]", @"[^\wx34\x08\S\x12\u30ff]"),
            (@"[abc\0\1\34p\377q\407\9]", @"[abc\x00\x01\x1Cp\xFFq\x2079]"),
            (@"[abc.def*gh?]", null),
            (@"[a-z]", null),
            (@"[a\-z]", null),
            (@"[a-a]", null),
            (@"[^a-z]", null),
            (@"[\x01-\xF2][\u1932-\uC678][\x35-\u0037\u0049-\x59][\xFF-\u25AB][\xC4-\u00c4\u00c4-\xc4]", null),
            (@"[^\x01-\xF2][^\u1932-\uC678][^\x35-\u0037\u0049-\x59][^\xFF-\u25AB][^\xC4-\u00c4\u00c4-\xc4]", null),
            (@"[A-\x60][\x40-a][A-\uFF01\u0006-a][\u2713-✔\u2714-✔✔-\u2716]", null),
            (@"[^A-\x60][^\x40-a][^A-\uFF01\u0006-a][^\u2713-✔\u2714-✔✔-\u2716]", null),
            (@"[abc-z05-9][5-7aq\x87-\xbd\xc8\u1137\u1334-\u1339]", null),
            (@"[\b-\x09\x07-\b\x08-\b][abc\be-g]", @"[\x08-\x09\x07-\x08\x08-\x08][abc\x08e-g]"),
            (@"[^^-z][^!-^]", null),
            (@"[a-z-]", @"[a-z\-]"),
            (@"[^a-z-]", @"[^a-z\-]"),
            (@"[a-z-b]", @"[a-z\-b]"),
            (@"[^a-z-b]", @"[^a-z\-b]"),
            (@"[-a-z]", @"[\-a-z]"),
            (@"[^-a-z]", @"[^\-a-z]"),
            (@"[!----a]", @"[!-\-\--a]"),
            (@"[!----a-b]", @"[!-\-\--a\-b]"),
            (@"[-]", @"[\-]"),
            (@"[AB-[C]]", @"[AB-\[C]]"),
            (@"[A-Z-[G-H]]", @"[A-Z\-\[G-H]]"),
            (@"[a-z-[g-h]]", @"[a-z\-\[g-h]]"),
            (@"[a-z-[g-h\b]\b]", @"[a-z\-\[g-h\x08]\b]"),
            (@"[-\x37][-\u1377][^-\x37][^-\u1377]", @"[\-\x37][\-\u1377][^\-\x37][^\-\u1377]"),
            (@"[\w-\d-\s-\W-\D-\S-][-\w-\d-\s-\W-\D-\S][^\w-\d-\s]", @"[\w\-\d\-\s\-\W\-\D\-\S\-][\-\w\-\d\-\s\-\W\-\D\-\S][^\w\-\d\-\s]"),
            (@"[\w-1\d-1\s-1\W-1\D-1\S-1][z-\wz-\dz-\sz-\Wz-\Dz-\S]", @"[\w\-1\d\-1\s\-1\W\-1\D\-1\S\-1][z\-\wz\-\dz\-\sz\-\Wz\-\Dz\-\S]"),
            (@"abc[0-9pq]*[.?/{]{1,5}|[1-z]*?tu??v[<->-]{10,}$|^[^ab\w-d]", @"abc[0-9pq]*[.?/{]{1,5}|[1-z]*?tu??v[<->\-]{10,}$|^[^ab\w\-d]"),
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(shouldTranspilePatternWithCharSets_data))]
        public void shouldTranspilePatternWithCharSets(string pattern, string expected = null) {
            _verifyPatternFlagsInvariant(pattern, expected);
        }

        public static IEnumerable<object[]> shouldTranspilePatternWithCaptureGroups_data = new (string, string, int)[] {
            (@"()", null, 1),
            (@"(a)", null, 1),
            (@"($)", null, 1),
            (@"(\()", null, 1),
            (@"(\))", null, 1),
            (@"([(])", null, 1),
            (@"([)])", null, 1),
            (@"(123)", null, 1),
            (@"abc(12)+d", null, 1),
            (@"abc(12)", null, 1),
            (@"()abc", null, 1),
            (@"abc()", null, 1),
            (@"abcd()+e", null, 1),
            (@"(ab+[c-z]+)*?", null, 1),
            (@"(a)(a)(a)", null, 3),
            (@"(((a)))", null, 3),
            (@"((a))(b)", null, 3),
            (@"(a)((b))", null, 3),
            (@"ab(c|d)(e|f)g", null, 2),
            (@"ab(([0-9]+[.][0-9]+zzz|(pqr?){5,})*uv\(\)w{1,300}|foo$)eee", null, 3),
            (String.Concat(Enumerable.Repeat("()", 999)), null, 999),
            (String.Concat(Enumerable.Repeat("([0-9]+)*", 999)), null, 999),
            (String.Concat(Enumerable.Repeat("((([0-9]+)*c)d$)", 333)), null, 999),
        }.Select(x => new object[] {x.Item1, x.Item2, x.Item3});

        [Theory]
        [MemberData(nameof(shouldTranspilePatternWithCaptureGroups_data))]
        public void shouldTranspilePatternWithCaptureGroups(string pattern, string expectedPattern, int numGroups) {
            _verifyPatternFlagsInvariant(pattern, expectedPattern, numGroups);
        }

        public static IEnumerable<object[]> shouldTranspilePatternWithNonCaptureGroups_data = new (string, string, int)[] {
            (@"(?:)", null, 0),
            (@"(?:a)", null, 0),
            (@"(?:$)", null, 0),
            (@"(?:123)", null, 0),
            (@"abc(?:12)+d", null, 0),
            (@"abc(?:12)", null, 0),
            (@"(?:)abc", null, 0),
            (@"abc(?:)", null, 0),
            (@"abcd(?:)+e", null, 0),
            (@"(?:ab+[c-z]+)*?", null, 0),
            (@"(?:a)(?:a)(?:a)", null, 0),
            (@"(a)(?:a)(?:a)", null, 1),
            (@"(?:a)(a)(?:a)", null, 1),
            (@"(?:a)(?:a)(a)", null, 1),
            (@"(a)(?:a)(a)", null, 2),
            (@"(?:a)(a)(a)", null, 2),
            (@"((?:))", null, 1),
            (@"(?:())", null, 1),
            (@"(?:((a)))", null, 2),
            (@"((?:(a)))", null, 2),
            (@"(((?:a)))", null, 2),
            (@"(?:(?:(a)))", null, 1),
            (@"(?:((?:a)))", null, 1),
            (@"(?:(?:(?:a)))", null, 0),
            (@"(?:(a))(b)", null, 2),
            (@"((?:a))(b)", null, 2),
            (@"((a))(?:b)", null, 2),
            (@"(?:a)((b))", null, 2),
            (@"(a)(?:(b))", null, 2),
            (@"(a)((?:b))", null, 2),
            (@"(?:(?:a))(b)", null, 1),
            (@"((?:a))(?:b)", null, 1),
            (@"(?:(a))(?:b)", null, 1),
            (@"(?:a)(?:(b))", null, 1),
            (@"(a)(?:(?:b))", null, 1),
            (@"(?:a)((?:b))", null, 1),
            (@"(?:a)(?:(?:b))", null, 0),
            (@"(?:(?:a))(?:b)", null, 0),
            (@"ab(c|d)(?:e|f)g", null, 1),
            (@"ab(?:(?:[0-9]+[.][0-9]+zzz|(?:pqr?){5,})*uv\(\)w{1,300}|foo$)eee", null, 0),
            (@"ab(?:(?:[0-9]+[.][0-9]+zzz|(pqr?){5,})*uv\(\)w{1,300}|foo$)eee", null, 1),
            (@"ab((?:[0-9]+[.][0-9]+zzz|(?:pqr?){5,})*uv\(\)w{1,300}|foo$)eee", null, 1),
            (@"ab(?:([0-9]+[.][0-9]+zzz|(pqr?){5,})*uv\(\)w{1,300}|foo$)eee", null, 2),
            (String.Concat(Enumerable.Repeat("(?:[0-9]+)*", 999)), null, 0),
            (String.Concat(Enumerable.Repeat("(?:[0-9]+)*", 1000)), null, 0),
            (String.Concat(Enumerable.Repeat("(a)(?:[0-9]+)*", 999)), null, 999),
            (String.Concat(Enumerable.Repeat("(a|b)+(?:[0-9]+)*", 999)), null, 999),
            (String.Concat(Enumerable.Repeat("((?:([0-9]+)*c)d$)", 333)), null, 666),
            (String.Concat(Enumerable.Repeat("((([0-9]+)*c)d$)(?:e)", 333)), null, 999),
        }.Select(x => new object[] {x.Item1, x.Item2, x.Item3});

        [Theory]
        [MemberData(nameof(shouldTranspilePatternWithNonCaptureGroups_data))]
        public void shouldTranspilePatternWithNonCaptureGroups(string pattern, string expectedPattern, int numGroups) {
            _verifyPatternFlagsInvariant(pattern, expectedPattern, numGroups);
        }

        public static IEnumerable<object[]> shouldTranspilePatternWithNamedCaptureGroups_data = new (string, string, string[])[] {
            (@"(?P<a>)", "()", new[] {"a"}),
            (@"(?P<abcd>)", "()", new[] {"abcd"}),
            (@"(?P<a><)(?P<b>>)", "(<)(>)", new[] {"a", "b"}),
            (@"xxx(?P<abcd>)xxx", "xxx()xxx", new[] {"abcd"}),
            (@"(?P<a>a)", "(a)", new[] {"a"}),
            (@"(?P<abc>a)", "(a)", new[] {"abc"}),
            (@"(?P<A>a*|b*)", "(a*|b*)", new[] {"A"}),
            (@"(?P<Ab>a*|b*)", "(a*|b*)", new[] {"Ab"}),
            (@"(?P<Ab129_>a*|b*)", "(a*|b*)", new[] {"Ab129_"}),
            (@"(?P<ab129_>a*|b*)", "(a*|b*)", new[] {"ab129_"}),
            (@"(?P<_b129_>a*|b*)", "(a*|b*)", new[] {"_b129_"}),
            (@"abc(?P<a>12)+d", "abc(12)+d", new[] {"a"}),
            (@"(?P<a>)abc", "()abc", new[] {"a"}),
            (@"(?P<abcd>)abc", "()abc", new[] {"abcd"}),
            (@"abc(?P<a>)abc", "abc()abc", new[] {"a"}),
            (@"abc(?P<abcd>)abc", "abc()abc", new[] {"abcd"}),
            (@"(?P<x>ab+[c-z]+)*?", "(ab+[c-z]+)*?", new[] {"x"}),
            (@"(?P<x1>a)(?P<x2>a)(?P<x3>a)", "(a)(a)(a)", new[] {"x1", "x2", "x3"}),
            (@"(a)(?P<x2>a)(?P<x3>a)", "(a)(a)(a)", new[] {null, "x2", "x3"}),
            (@"(?P<x1>a)(a)(?P<x3>a)", "(a)(a)(a)", new[] {"x1", null, "x3"}),
            (@"(?P<x1>a)(?P<x2>a)(a)", "(a)(a)(a)", new[] {"x1", "x2", null}),
            (@"(a)(a)(?P<x3>a)", "(a)(a)(a)", new[] {null, null, "x3"}),
            (@"(a)(?P<x2>a)(a)", "(a)(a)(a)", new[] {null, "x2", null}),
            (@"(?P<x1>a)(?:a)(?P<x3>a)", "(a)(?:a)(a)", new[] {"x1", "x3"}),
            (@"(?:a)(a)(?P<x3>a)", "(?:a)(a)(a)", new[] {null, "x3"}),
            (@"(a)(?:a)(?P<x3>a)", "(a)(?:a)(a)", new[] {null, "x3"}),
            (@"(?:a)(?:a)(?P<x3>a)", "(?:a)(?:a)(a)", new[] {"x3"}),
            (@"(?:a)(?P<x2>a)(?:a)", "(?:a)(a)(?:a)", new[] {"x2"}),
            (@"(?P<x1>(?P<x2>(?P<x3>a)))", "(((a)))", new[] {"x1", "x2", "x3"}),
            (@"(?P<x1>(?P<x2>(a)))", "(((a)))", new[] {"x1", "x2", null}),
            (@"(?P<x1>((?P<x3>a)))", "(((a)))", new[] {"x1", null, "x3"}),
            (@"((?P<x2>(?P<x3>a)))", "(((a)))", new[] {null, "x2", "x3"}),
            (@"(?P<x1>((a)))", "(((a)))", new[] {"x1", null, null}),
            (@"((?P<x2>(a)))", "(((a)))", new[] {null, "x2", null}),
            (@"(((?P<x3>a)))", "(((a)))", new[] {null, null, "x3"}),
            (@"(?:((?P<x3>a)))", "(?:((a)))", new[] {null, "x3"}),
            (@"(?:(?:(?P<x3>a)))", "(?:(?:(a)))", new[] {"x3"}),
            (@"(?P<x1>(?P<x2>a))(?P<y>b)", "((a))(b)", new[] {"x1", "x2", "y"}),
            (@"(?P<x>a)(?P<y1>(?P<y2>b))", "(a)((b))", new[] {"x", "y1", "y2"}),
            (
                @"(?P<a>([0-9]){3}-)+(?P<b>(?:[0-9]){4,5})(?P<c>(foo)$)",
                @"(([0-9]){3}-)+((?:[0-9]){4,5})((foo)$)",
                new[] {"a", null, "b", "c", null}
            ),
            (
                @"ab(?P<_1>(?P<_2>[0-9]+[.][0-9]+zzz|(?P<_3>pqr?){5,})*uv\(\)w{1,300}|foo$)eee",
                @"ab(([0-9]+[.][0-9]+zzz|(pqr?){5,})*uv\(\)w{1,300}|foo$)eee",
                new[] {"_1", "_2", "_3"}
            ),
            (
                @"ab(?P<_1>(?P<_2>[0-9]+[.][0-9]+(zz(?:\u43FD))|(?P<_3>pqr?){5,})*uv\(\)w{1,300}|\032$)eee",
                @"ab(([0-9]+[.][0-9]+(zz(?:\u43FD))|(pqr?){5,})*uv\(\)w{1,300}|\x1A$)eee",
                new[] {"_1", "_2", null, "_3"}
            ),
            (
                String.Concat(Enumerable.Range(0, 999).Select(i => $"(?P<a{i}>xyz)+")),
                String.Concat(Enumerable.Range(0, 999).Select(i => "(xyz)+")),
                Enumerable.Range(0, 999).Select(i => "a" + i).ToArray()
            ),
            (
                String.Concat(Enumerable.Range(0, 333).Select(i => $"(?P<a{i}>(?P<b{i}>xyz))+([123]*)")),
                String.Concat(Enumerable.Range(0, 333).Select(i => "((xyz))+([123]*)")),
                Enumerable.Range(0, 333).SelectMany(i => new[] {"a" + i, "b" + i, null}).ToArray()
            ),
        }.Select(x => new object[] {x.Item1, x.Item2, x.Item3});

        [Theory]
        [MemberData(nameof(shouldTranspilePatternWithNamedCaptureGroups_data))]
        public void shouldTranspilePatternWithNamedCaptureGroups(string pattern, string expectedPattern, string[] groupNames) {
            _verifyPatternFlagsInvariant(pattern, expectedPattern, groupNames.Length, groupNames);
        }

        public static IEnumerable<object[]> shouldTranspilePatternWithLookarounds_data = new (string, string, string[])[] {
            (@"(?=)", null, null),
            (@"(?!)", null, null),
            (@"(?<=)", null, null),
            (@"(?<!)", null, null),
            (@"(?=a)", null, null),
            (@"(?!a)", null, null),
            (@"(?<=a)", null, null),
            (@"(?<!a)", null, null),
            (@"a(?=a)b", null, null),
            (@"a(?!a)b", null, null),
            (@"a(?<=a)b", null, null),
            (@"a(?<!a)b", null, null),
            (@"(?=(?:ab*c|(d[\d_]+)z)?)", null, new string[] {null}),
            (@"(?!(?:ab*c|(d[\d_]+)z)?)", null, new string[] {null}),
            (@"(?<=(?:ab*c|(d[\d_]+)z)?)", null, new string[] {null}),
            (@"(?<!(?:ab*c|(d[\d_]+)z)?)", null, new string[] {null}),
            (@"(?=\()(?!\()(?<=\()(?<!\()", null, null),
            (@"(?=\))(?!\))(?<=\))(?<!\))", null, null),
            (@"_(?==)_(?!!)_(?<==)_(?<=<)_(?<!<)_(?<!!)_", null, null),
            (@"(?<=(?<=(?<=a)b)c)d", null, null),
            (@"(?<!(?<!(?<!a)b)c)d", null, null),
            (@"a(?=b(?=c(?=d)))", null, null),
            (@"a(?!b(?!c(?!d)))", null, null),
            (@"a(?=b)*(?<!abc){2,4}(?!(?=ab)+c*){8}", null, null),
            (
                @"^(?P<x>abc*z|[0-9.]+(?=[a-z]{3,})|&+)[\w\d]*?\n(?!\s*[a-f])",
                @"^(abc*z|[0-9.]+(?=[a-z]{3,})|&+)[\w\d]*?\n(?!\s*[a-f])",
                new string[] {"x"}
            ),
            (
                @"(?<!\s*[a-f])(?P<x>abc*z|(?<=[a-z]{3,}y*)[0-9.]+|&+)[\w\d]*?\n?$",
                @"(?<!\s*[a-f])(abc*z|(?<=[a-z]{3,}y*)[0-9.]+|&+)[\w\d]*?\n?$",
                new string[] {"x"}
            ),
            (String.Concat(Enumerable.Repeat("(?=)", 999)), null, null),
            (String.Concat(Enumerable.Repeat("(?!)", 999)), null, null),
            (String.Concat(Enumerable.Repeat("(?<=)", 999)), null, null),
            (String.Concat(Enumerable.Repeat("(?<!)", 999)), null, null),
            (String.Concat(Enumerable.Repeat("(?=)", 1000)), null, null),
            (String.Concat(Enumerable.Repeat("(?!)", 1000)), null, null),
            (String.Concat(Enumerable.Repeat("(?<=)", 1000)), null, null),
            (String.Concat(Enumerable.Repeat("(?<!)", 1000)), null, null),
        }.Select(x => new object[] {x.Item1, x.Item2, x.Item3});

        [Theory]
        [MemberData(nameof(shouldTranspilePatternWithLookarounds_data))]
        public void shouldTranspilePatternWithLookarounds(string pattern, string expectedPattern, string[] groupNames) {
            _verifyPatternFlagsInvariant(pattern, expectedPattern, (groupNames == null) ? 0 : groupNames.Length, groupNames);
        }

        public static IEnumerable<object[]> shouldTranspilePatternWithBackreferences_data = new (string, string, object)[] {
            (@"()\1", @"()\k<1>", 1),
            (@"(a)\1", @"(a)\k<1>", 1),
            (@"\1(a)", @"\k<1>(a)", 1),
            (@"(a)\1\01\001", @"(a)\k<1>\x01\x01", 1),
            (@"(a)(a)(a)\1", @"(a)(a)(a)\k<1>", 3),
            (@"(a)(a)(a)\2", @"(a)(a)(a)\k<2>", 3),
            (@"(a)(a)(a)\3", @"(a)(a)(a)\k<3>", 3),
            (@"(a)(a)(a)\1\2\3", @"(a)(a)(a)\k<1>\k<2>\k<3>", 3),
            (@"\1\2\3(a)(a)(a)", @"\k<1>\k<2>\k<3>(a)(a)(a)", 3),
            (@"(a)\1(a)\2(a)\3", @"(a)\k<1>(a)\k<2>(a)\k<3>", 3),
            (@"(((a)))\1\2\3", @"(((a)))\k<1>\k<2>\k<3>", 3),
            (@"(\1(\3(a\2)))", @"(\k<1>(\k<3>(a\k<2>)))", 3),
            (@"\1(\1a)\1", @"\k<1>(\k<1>a)\k<1>", 1),
            (@"([a-z])999(?=\1+)", @"([a-z])999(?=\k<1>+)", 1),
            (@"(\w)\1{10,}", @"(\w)\k<1>{10,}", 1),
            (@"(a)(a)(a)\1\1\2\2\3\3", @"(a)(a)(a)\k<1>\k<1>\k<2>\k<2>\k<3>\k<3>", 3),
            (@"(\1(\3(\2a\2)\1)\3)", @"(\k<1>(\k<3>(\k<2>a\k<2>)\k<1>)\k<3>)", 3),
            (
                @"((ab)*\w+([0-9]{4,})-?[0-9]*\3|\2*pq(?:[|/?(])+?\1|(?<!\1)foo$)\2",
                @"((ab)*\w+([0-9]{4,})-?[0-9]*\k<3>|\k<2>*pq(?:[|/?(])+?\k<1>|(?<!\k<1>)foo$)\k<2>",
                3
            ),
            (
                @"(?P<x>(?P<y>ab)*\w+(?P<z>[0-9]{4,})-?[0-9]*\3|\2*pq(?:[|/?(])+?\1|(?<!\1)foo$)\2",
                @"((ab)*\w+([0-9]{4,})-?[0-9]*\k<3>|\k<2>*pq(?:[|/?(])+?\k<1>|(?<!\k<1>)foo$)\k<2>",
                new[] {"x", "y", "z"}
            ),
            (
                String.Concat(Enumerable.Repeat("(a)", 10)) + @"\1\2\3\4\5\6\7\8\9\10",
                String.Concat(Enumerable.Repeat("(a)", 10)) + @"\k<1>\k<2>\k<3>\k<4>\k<5>\k<6>\k<7>\k<8>\k<9>\k<10>",
                10
            ),
            (
                @"\1\2\3\4\5\6\7\8\9\10" + String.Concat(Enumerable.Repeat("(a)", 10)),
                @"\k<1>\k<2>\k<3>\k<4>\k<5>\k<6>\k<7>\k<8>\k<9>\k<10>" + String.Concat(Enumerable.Repeat("(a)", 10)),
                10
            ),
            (
                String.Concat(Enumerable.Range(1, 99).Select(i => $"\\{i}(a)\\{i}")),
                String.Concat(Enumerable.Range(1, 99).Select(i => $"\\k<{i}>(a)\\k<{i}>")),
                99
            ),
            (
                String.Concat(Enumerable.Range(1, 999).Select(i => $"\\{i}(a)\\{i}")),
                String.Concat(Enumerable.Range(1, 999).Select(i => $"\\k<{i}>(a)\\k<{i}>")),
                999
            ),
            (@"(a)\0\1\2", @"(a)\x00\k<1>\x02", 1),
            (@"\0\1\2(a)", @"\x00\k<1>\x02(a)", 1),
            (@"(a)(a)(a)\0\1\2\3\4", @"(a)(a)(a)\x00\k<1>\k<2>\k<3>\x04", 3),
            (@"\0\1\2\3\4(a)(a)(a)", @"\x00\k<1>\k<2>\k<3>\x04(a)(a)(a)", 3),
            (
                @"(a)(a)(a)(a)(a)(a)(a)(a)(a)\1\2\3\4\5\6\7\8\9\10",
                @"(a)(a)(a)(a)(a)(a)(a)(a)(a)\k<1>\k<2>\k<3>\k<4>\k<5>\k<6>\k<7>\k<8>\k<9>\x08",
                9
            ),
            (
                @"\67\68\69" + String.Concat(Enumerable.Repeat("(a)", 68)) + @"\67\68\69",
                @"\k<67>\k<68>\x069" + String.Concat(Enumerable.Repeat("(a)", 68)) + @"\k<67>\k<68>\x069",
                68
            ),
            (
                @"\97\98\99" + String.Concat(Enumerable.Repeat("(a)", 98)) + @"\97\98\99",
                @"\k<97>\k<98>99" + String.Concat(Enumerable.Repeat("(a)", 98)) + @"\k<97>\k<98>99",
                98
            ),
            (
                @"\99\100\101" + String.Concat(Enumerable.Repeat("(a)", 100)) + @"\99\100\101",
                @"\k<99>\k<100>\x41" + String.Concat(Enumerable.Repeat("(a)", 100)) + @"\k<99>\k<100>\x41",
                100
            ),
            (
                @"\346\347\348\400" + String.Concat(Enumerable.Repeat("(a)", 346)) + @"\346\347\348\400",
                @"\k<346>\xE7\x1C8\x200" + String.Concat(Enumerable.Repeat("(a)", 346)) + @"\k<346>\xE7\x1C8\x200",
                346
            ),
            (
                @"\618\619\620" + String.Concat(Enumerable.Repeat("(a)", 618)) + @"\618\619\620",
                @"\k<618>\x319\x320" + String.Concat(Enumerable.Repeat("(a)", 618)) + @"\k<618>\x319\x320",
                618
            ),
            (
                @"\998\999" + String.Concat(Enumerable.Repeat("(a)", 998)) + @"\998\999",
                @"\k<998>999" + String.Concat(Enumerable.Repeat("(a)", 998)) + @"\k<998>999",
                998
            ),
        }.Select(x => new object[] {x.Item1, x.Item2, x.Item3});

        [Theory]
        [MemberData(nameof(shouldTranspilePatternWithBackreferences_data))]
        public void shouldTranspilePatternWithBackreferences(string pattern, string expectedPattern, object groupCountOrNames) {
            int groupCount;
            string[] groupNames;

            if (groupCountOrNames is int count)
                (groupCount, groupNames) = (count, null);
            else if (groupCountOrNames is string[] names)
                (groupCount, groupNames) = (names.Length, names);
            else
                (groupCount, groupNames) = (0, null);

            _verifyPatternFlagsInvariant(pattern, expectedPattern, groupCount, groupNames);
        }

        public static IEnumerable<object[]> shouldTranspilePatternWithNamedBackreferences_data = new (string, string, string[])[] {
            (@"(?P<a>)\k<a>", @"()\k<1>", new[] {"a"}),
            (@"(?P<abcd>)\k<abcd>", @"()\k<1>", new[] {"abcd"}),
            (@"\k<a>(?P<a>)", @"\k<1>()", new[] {"a"}),
            (@"\k<abcd>(?P<abcd>)", @"\k<1>()", new[] {"abcd"}),
            (@"(?P<a>a*|b*)\k<a>", @"(a*|b*)\k<1>", new[] {"a"}),
            (@"(?P<ab>a*|b*)\k<ab>", @"(a*|b*)\k<1>", new[] {"ab"}),
            (@"(?P<A>a*|b*)\k<A>", @"(a*|b*)\k<1>", new[] {"A"}),
            (@"(?P<Ab>a*|b*)\k<Ab>", @"(a*|b*)\k<1>", new[] {"Ab"}),
            (@"(?P<Ab129_>a*|b*)\k<Ab129_>", @"(a*|b*)\k<1>", new[] {"Ab129_"}),
            (@"(?P<ab129_>a*|b*)\k<ab129_>", @"(a*|b*)\k<1>", new[] {"ab129_"}),
            (@"(?P<_b129_>a*|b*)\k<_b129_>", @"(a*|b*)\k<1>", new[] {"_b129_"}),
            (
                @"\k<x1>*\k<x2>*\k<x3>*(?P<x1>a)(?P<x2>b)(?P<x3>c)\k<x1>*\k<x2>*\k<x3>*",
                @"\k<1>*\k<2>*\k<3>*(a)(b)(c)\k<1>*\k<2>*\k<3>*",
                new[] {"x1", "x2", "x3"}
            ),
            (
                @"\1+\2+\3+(?P<x1>a)\3?(?P<x2>b)\2?(?P<x3>c)\1?\k<x1>*\k<x2>*\k<x3>*\3+\2+\1+",
                @"\k<1>+\k<2>+\k<3>+(a)\k<3>?(b)\k<2>?(c)\k<1>?\k<1>*\k<2>*\k<3>*\k<3>+\k<2>+\k<1>+",
                new[] {"x1", "x2", "x3"}
            ),
            (
                @"\1+\2+\3+(?P<x1>a)\3?(b)\2?(?P<x3>c)\1?\k<x1>*\k<x3>*\3+\2+\1+",
                @"\k<1>+\k<2>+\k<3>+(a)\k<3>?(b)\k<2>?(c)\k<1>?\k<1>*\k<3>*\k<3>+\k<2>+\k<1>+",
                new[] {"x1", null, "x3"}
            ),
            (
                String.Concat(Enumerable.Range(1, 999).Select(i => $"\\{i}\\k<a{i}>(?P<a{i}>xyz)+\\{i}\\k<a{i}>")),
                String.Concat(Enumerable.Range(1, 999).Select(i => $"\\k<{i}>\\k<{i}>(xyz)+\\k<{i}>\\k<{i}>")),
                Enumerable.Range(1, 999).Select(i => "a" + i).ToArray()
            ),
        }.Select(x => new object[] {x.Item1, x.Item2, x.Item3});

        [Theory]
        [MemberData(nameof(shouldTranspilePatternWithNamedBackreferences_data))]
        public void shouldTranspilePatternWithNamedBackreferences(string pattern, string expectedPattern, string[] groupNames) {
            _verifyPatternFlagsInvariant(pattern, expectedPattern, groupNames.Length, groupNames);
        }

        public static IEnumerable<object[]> shouldStripWhiteSpaceAndCommentsInExtendedMode_data = new (string, string, string, object)[] {
            ("    ", null, "", 0),
            (" \n\r\f\t\v ", null, "", 0),
            ("  a  b  c  ", null, "abc", 0),
            ("  a *  b *? c +\nd +? e ? f\t?? g  {1} \r h \n {3,4} ", null, "a*b*?c+d+?e?f??g{1}h{3,4}", 0),
            ("  [0-9]* \\. [0-9] * \n\\. [0-9A-Za-z] {5,} ", null, @"[0-9]*\.[0-9]*\.[0-9A-Za-z]{5,}", 0),
            (" ( a  ( b  ( c ) d ) e ) f ", null, "(a(b(c)d)e)f", 3),
            (" (?: a  (?: b ( c ) d ) e ) f ", null, "(?:a(?:b(c)d)e)f", 1),
            (" (?= a ) | (?! a ) | (?<= a ) | (?<! a ) ", null, "(?=a)|(?!a)|(?<=a)|(?<!a)", 0),
            (
                " (?P<x> a  (?P<y> b  (?P<z> c ) d ) e ) f ",
                " ( a  ( b  ( c ) d ) e ) f ",
                "(a(b(c)d)e)f",
                new string[] {"x", "y", "z"}
            ),
            ("\\ \\\n \\\r \\\t \\\f \\\v ", " \n \r \t \f \v ", " \n\r\t\f\v", 0),
            ("[ \n\r\f\t\v]", null, "[ \n\r\f\t\v]", 0),
            (@"abc\ * d", "abc * d", "abc *d", 0),
            (@"abc[ ]* d", null, "abc[ ]*d", 0),
            ("#", null, "", 0),
            ("abcd#", null, "abcd", 0),
            ("abcd #", null, "abcd", 0),
            ("abcd#\r", null, "abcd", 0),
            ("abcd#\n", null, "abcd", 0),
            ("abcd#\re", null, "abcde", 0),
            ("abcd#\ne", null, "abcde", 0),
            ("a #--- \r  b", null, "ab", 0),
            ("a #--- \n  b", null, "ab", 0),
            ("a [#bc] d", null, "a[#bc]d", 0),
            ("a (  # ___ \n bc # ___ \n (d*) # *? \n ) #123 \r+", null, "a(bc(d*))+", 2),
        }.Select(x => new object[] {x.Item1, x.Item2, x.Item3, x.Item4});

        [Theory]
        [MemberData(nameof(shouldStripWhiteSpaceAndCommentsInExtendedMode_data))]
        public void shouldStripWhiteSpaceAndCommentsInExtendedMode(
            string pattern, string expectedNonExtended, string expectedExtended, object groupCountOrNames)
        {
            int groupCount;
            string[] groupNames;

            if (groupCountOrNames is int count)
                (groupCount, groupNames) = (count, null);
            else if (groupCountOrNames is string[] names)
                (groupCount, groupNames) = (names.Length, names);
            else
                (groupCount, groupNames) = (0, null);

            var t = new RegexTranspiler();

            if (groupNames == null && groupCount > 0)
                groupNames = new string[groupCount];

            t.transpile(pattern, false, false);
            Assert.Equal(expectedNonExtended ?? pattern, t.transpiledPattern);
            Assert.Equal(groupCount, t.groupCount);
            Assert.Equal(groupNames, t.getGroupNames());

            t.transpile(pattern, false, true);
            Assert.Equal(expectedExtended, t.transpiledPattern);
            Assert.Equal(groupCount, t.groupCount);
            Assert.Equal(groupNames, t.getGroupNames());

            t.transpile(pattern, true, false);
            Assert.Equal(expectedNonExtended ?? pattern, t.transpiledPattern);
            Assert.Equal(groupCount, t.groupCount);
            Assert.Equal(groupNames, t.getGroupNames());

            t.transpile(pattern, true, true);
            Assert.Equal(expectedExtended, t.transpiledPattern);
            Assert.Equal(groupCount, t.groupCount);
            Assert.Equal(groupNames, t.getGroupNames());

            new Regex(expectedNonExtended ?? pattern, RegexOptions.ECMAScript | RegexOptions.CultureInvariant);
            new Regex(expectedExtended, RegexOptions.ECMAScript | RegexOptions.CultureInvariant);
        }

        public static IEnumerable<object[]> shouldThrowErrorIfPatternInvalid_data = new string[] {
            @"\",
            @"\\\",
            @"abcd\d\",
            @"abcd[",
            @"abcd[\",
            @"abcd(",
            @"abcd(\",
            @"abcd[pq][12\",
            @"abcd[p-",
            @"abcd[p-\",
            @"abc\x",
            @"abc\x1",
            @"abc\u",
            @"abc\u1",
            @"abc\u12",
            @"abc\u123",
            @"abc\u1234\u",
            @"*",
            @"?",
            @"+",
            @"a**",
            @"a++",
            @"a*??",
            @"(a*b)**",
            @"[a-z]+*b",
            @"(*abc)",
            @"(?:*abc)",
            @"(?=*abc)",
            @"(?!+abc)",
            @"(?<!?abc)",
            @"(?<=+abc)",
            @"(",
            @")",
            @"(abc(d)",
            @"(abc(d)))",
            @"(?)",
            @"(?<)",
            @"abc(?:",
            @"abc(?<",
            @"abc(?<=",
            @"abc(?==",
            @"abc(?=!",
            @"abc(?*",
            @"abc(?<:",
            @"abc(?<abc>pq)",
            @"abc(?>abc)",
            @"abc(?(x)def)",
            @"abc(?P<x>def)(?(x)ghi)",
            @"(?P<abc",
            @"(?P<abc)*",
            @"(?p<abc>123)*",
            @"(?P<>abc)",
            @"(?P<1>abc)",
            @"(?P<a-b>abc)",
            @"(?abcd)",
            @"(?;abcd)",
            @"(? :a)",
            @"(? = a)",
            @"(?< =a)",
            @"(? <!a)",
            @"(?P <a> b)",
            @"(? P<a> b)",
            @"(?P< a >b)",
            @"abc(?&'a'abcd)",
            @"abc(?P'a'abcd)",
            @"abc(?P",
            @"abc(?P<",
            @"abc(?P<a)",
            @"abc(?P<a>",
            @"abc(?P<>)",
            @"abc(?P<a>d)(?P<a>e)",
            @"abc(?P<a>d)(?P<b>e)(?P<a>f)",
            @"abc\k",
            @"abc\k1",
            @"abc\k<abc",
            @"abc\k<>",
            @"abc\k<a-b>",
            @"abc\k<a>",
            @"abc\k<1>",
            @"abc(?P<a>d)(?P<b>e)\k<c>",
            @"abc\k<c>(?P<a>d)(?P<b>e)",
            @"{1}",
            @"{1,}",
            @"{1,2}",
            @"abc*{1}",
            @"abc{1}{2}",
            @"abc{2,1}",
            @"abc{1000,999}",
            @"abc{2147483648}",
            @"abc{2147483650}",
            @"abc{1,2147483648}",
            @"abc{1,2147483650}",
            @"abc{1}??",
            @"abc{1,}??",
            @"abc{1,2}??",
            @"[z-a]",
            @"[\x13-\x12]",
            @"[\u3FAD-\u3FAC]",
            @"[\u076B-\u0769]",
            @"[9-\x38]",
            @"[\x39-8]",
            @"[✔-\u2713]",
            @"[\u2715-✔]",
            @"[ab-[c]]",
            @"abc\u123G",
            @"abc\u123h",
            @"abc\xag",
            @"abc\xAG",
            @"abc\xga",
            @"abc\xGA",
            @"abc\x4:",
            @"abc\x/4",
            @"abc\x4@",
            @"abc\x`4",
            @"abc\u4g50",
            @"abc\u4G50",
            @"abc\u4:50",
            @"abc\u45/0",
            @"abc\u@45b",
            @"abc\u45b`",
            @"abc\u45g0",
            @"abc\ug450",
        }.Select(x => new object[] {x});

        [Theory]
        [MemberData(nameof(shouldThrowErrorIfPatternInvalid_data))]
        public void shouldThrowErrorIfPatternInvalid(string pattern) {
            _verifyPatternErrorFlagsInvariant(pattern);
        }

        [Theory]
        [InlineData("  *  ")]
        [InlineData("  +  ")]
        [InlineData("a* *")]
        [InlineData("a* ?")]
        [InlineData("a*\n\t*")]
        [InlineData("a*#pq\n\t*")]
        [InlineData("a {1,2}  *")]
        [InlineData("( * )")]
        [InlineData("( ?)")]
        [InlineData("(?: *)")]
        [InlineData("( ?=a)")]
        [InlineData("( ?!a)")]
        [InlineData("( ?<=a)")]
        [InlineData("( ?<!a)")]
        [InlineData("( ?P<x>a)")]
        public void shouldThrowErrorIfPatternInvalidExtended(string pattern) {
            var t = new RegexTranspiler();
            AVM2Exception exc;

            t.transpile(pattern, false, false);
            t.transpile(pattern, true, false);

            exc = Assert.Throws<AVM2Exception>(() => t.transpile(pattern, false, true));
            Assert.Equal(ErrorCode.MARIANA__REGEXP_PARSE_ERROR, (ErrorCode)((ASError)exc.thrownValue).errorID);

            exc = Assert.Throws<AVM2Exception>(() => t.transpile(pattern, true, true));
            Assert.Equal(ErrorCode.MARIANA__REGEXP_PARSE_ERROR, (ErrorCode)((ASError)exc.thrownValue).errorID);
        }

        [Theory]
        [InlineData("a*#(b")]
        [InlineData("a*#[q")]
        [InlineData("a*#[q-p]")]
        [InlineData("a*#*+")]
        [InlineData("a (  # ) \n bc # ) \n (d*) # *? \n ) #123 \r+")]
        public void shouldThrowErrorIfPatternInvalidNonExtended(string pattern) {
            var t = new RegexTranspiler();
            AVM2Exception exc;

            t.transpile(pattern, false, true);
            t.transpile(pattern, true, true);

            exc = Assert.Throws<AVM2Exception>(() => t.transpile(pattern, false, false));
            Assert.Equal(ErrorCode.MARIANA__REGEXP_PARSE_ERROR, (ErrorCode)((ASError)exc.thrownValue).errorID);

            exc = Assert.Throws<AVM2Exception>(() => t.transpile(pattern, true, false));
            Assert.Equal(ErrorCode.MARIANA__REGEXP_PARSE_ERROR, (ErrorCode)((ASError)exc.thrownValue).errorID);
        }

        public static IEnumerable<object[]> shouldThrowErrorIfGroupLimitExceeded_data = new string[] {
            String.Concat(Enumerable.Repeat("()", 1000)),
            String.Concat(Enumerable.Repeat("(a)", 1000)),
            String.Concat(Enumerable.Repeat("()", 2000)),
            String.Concat(Enumerable.Repeat("(a)", 2000)),
            String.Concat(Enumerable.Repeat("abc(a*)(?:a)", 1000)),
            String.Concat(Enumerable.Repeat("abc(a*)(?:a)", 2000)),
            String.Concat(Enumerable.Range(1, 1000).Select(i => $"(?P<a{i}>xyz)")),
            String.Concat(Enumerable.Range(1, 2000).Select(i => $"(?P<a{i}>xyz)")),
            String.Concat(Enumerable.Range(1, 500).Select(i => $"(?P<a{i}>xyz)__(xyz)")),
            String.Concat(Enumerable.Range(1, 500).Select(i => $"(xyz)__(?P<a{i}>xyz)")),
        }.Select(x => new object[] {x});

        [Theory]
        [MemberData(nameof(shouldThrowErrorIfGroupLimitExceeded_data))]
        public void shouldThrowErrorIfGroupLimitExceeded(string pattern) {
            _verifyPatternErrorFlagsInvariant(pattern);
        }

        private void _verifyPatternFlagsInvariant(
            string pattern,
            string expectedTranspiled = null,
            int groupCount = 0,
            string[] groupNames = null
        ) {
            var t = new RegexTranspiler();

            if (groupNames == null && groupCount > 0)
                groupNames = new string[groupCount];

            t.transpile(pattern, false, false);
            Assert.Equal(expectedTranspiled ?? pattern, t.transpiledPattern);
            Assert.Equal(groupCount, t.groupCount);
            Assert.Equal(groupNames, t.getGroupNames());

            t.transpile(pattern, false, true);
            Assert.Equal(expectedTranspiled ?? pattern, t.transpiledPattern);
            Assert.Equal(groupCount, t.groupCount);
            Assert.Equal(groupNames, t.getGroupNames());

            t.transpile(pattern, true, false);
            Assert.Equal(expectedTranspiled ?? pattern, t.transpiledPattern);
            Assert.Equal(groupCount, t.groupCount);
            Assert.Equal(groupNames, t.getGroupNames());

            t.transpile(pattern, true, true);
            Assert.Equal(expectedTranspiled ?? pattern, t.transpiledPattern);
            Assert.Equal(groupCount, t.groupCount);
            Assert.Equal(groupNames, t.getGroupNames());

            new Regex(expectedTranspiled ?? pattern, RegexOptions.ECMAScript | RegexOptions.CultureInvariant);
        }

        private void _verifyPatternErrorFlagsInvariant(string pattern) {
            var t = new RegexTranspiler();
            AVM2Exception exc;

            exc = Assert.Throws<AVM2Exception>(() => t.transpile(pattern, false, false));
            Assert.Equal(ErrorCode.MARIANA__REGEXP_PARSE_ERROR, (ErrorCode)((ASError)exc.thrownValue).errorID);

            exc = Assert.Throws<AVM2Exception>(() => t.transpile(pattern, false, true));
            Assert.Equal(ErrorCode.MARIANA__REGEXP_PARSE_ERROR, (ErrorCode)((ASError)exc.thrownValue).errorID);

            exc = Assert.Throws<AVM2Exception>(() => t.transpile(pattern, true, false));
            Assert.Equal(ErrorCode.MARIANA__REGEXP_PARSE_ERROR, (ErrorCode)((ASError)exc.thrownValue).errorID);

            exc = Assert.Throws<AVM2Exception>(() => t.transpile(pattern, true, true));
            Assert.Equal(ErrorCode.MARIANA__REGEXP_PARSE_ERROR, (ErrorCode)((ASError)exc.thrownValue).errorID);
        }

    }

}
