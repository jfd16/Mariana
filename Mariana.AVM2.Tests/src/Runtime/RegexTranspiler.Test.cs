using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class RegexTranspilerTest {

        public readonly struct Result {
            public readonly string expectedPattern;
            public readonly string[] groupNames;
            public readonly bool isSyntaxError;

            public Result(string expectedPattern, int groupCount) : this(expectedPattern, new string[groupCount]) { }

            public Result(string expectedPattern, string[] groupNames) {
                this.expectedPattern = expectedPattern;
                this.groupNames = groupNames;
                this.isSyntaxError = false;
            }

            private Result(bool fIsSyntaxError) : this() {
                this.isSyntaxError = fIsSyntaxError;
            }

            public static readonly Result syntaxError = new Result(fIsSyntaxError: true);
        }

        public readonly struct TestCase {
            public readonly string pattern;
            public readonly string flags;
            public readonly Result result;

            public TestCase(string pattern) : this(pattern, pattern) { }

            public TestCase(string pattern, string transpiledPattern) : this(pattern, "", new Result(transpiledPattern, 0)) { }

            public TestCase(string pattern, Result result) : this(pattern, "", result) { }

            public TestCase(string pattern, string flags, string transpiledPattern) : this(pattern, flags, new Result(transpiledPattern, 0)) { }

            public TestCase(string pattern, string flags, Result result) {
                this.pattern = pattern;
                this.flags = flags;

                if (!result.isSyntaxError && result.expectedPattern == null)
                    this.result = new Result(pattern, result.groupNames);
                else
                    this.result = result;
            }

            public override string ToString() =>
                String.Format("(pattern: {0}, flags: {1})", pattern, (flags.Length == 0) ? "*" : flags);
        }

        public static IEnumerable<object[]> transpileTestData_noSpecialChars = TupleHelper.toArrays(
            new TestCase(""),
            new TestCase("abc"),
            new TestCase("hello"),
            new TestCase("~`!@%&-_=:;]'/\"<>,"),
            new TestCase("\x00\x01\x05\ud800\udfff\uffff")
        );

        [Theory]
        [MemberData(nameof(transpileTestData_noSpecialChars))]
        public void transpileTest_noSpecialChars(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withEscapedChars = TupleHelper.toArrays(
            new TestCase(@"\n\r\f\t\v\b\B"),
            new TestCase(@"\*\^\$\(\)\{\}\[\]\+\?\-\.\\"),
            new TestCase(@"\~\`\!\@\#\%\&\=\_\:\;\'\""\<\>\,", @"~`!@#%&=_:;'""<>,"),
            new TestCase("\\\n\\\r\\\t\\\f\\\v\\ ", "\n\r\t\f\v "),
            new TestCase(@"\a\c\e\g\h\i\j\l\m\o\p\q\y\z", "aceghijlmopqyz"),
            new TestCase(@"\A\C\E\F\G\H\I\J\K\L\M\N\O\P\Q\R\T\U\V\X\Y\Z", "ACEFGHIJKLMNOPQRTUVXYZ"),
            new TestCase(@"ab\c\*de\nfg\h\&i\$", @"abc\*de\nfgh&i\$"),
            new TestCase(@"\X33", "X33"),
            new TestCase(@"\U033A", "U033A")
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withEscapedChars))]
        public void transpileTest_withEscapedChars(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withQuantifiers = TupleHelper.toArrays(
            new TestCase(@"a*"),
            new TestCase(@"a+"),
            new TestCase(@"a?"),
            new TestCase(@"a*?"),
            new TestCase(@"a+?"),
            new TestCase(@"a??"),
            new TestCase(@"a*b"),
            new TestCase(@"a*b+&*?c"),
            new TestCase(@"\.*\n+\^*?q??\???"),

            new TestCase(@"a{1}"),
            new TestCase(@"a{1,}"),
            new TestCase(@"a{1,2}"),
            new TestCase(@"a{1}?"),
            new TestCase(@"a{1,}?"),
            new TestCase(@"a{1,2}?"),
            new TestCase(@"ab*c{10}d{6,400}e?f{7,}?gh{4,12}?ij"),
            new TestCase(@"a{0}"),
            new TestCase(@"a{0,2147483647}"),
            new TestCase(@"a{2147483647}"),
            new TestCase(@"a{2147483647,}"),
            new TestCase(@"a{2147483647,2147483647}")
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withQuantifiers))]
        public void transpileTest_withQuantifiers(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withStartAndEndAnchors = TupleHelper.toArrays(
            new TestCase("^", "!m", "^"),
            new TestCase("^", "m", @"(?<=[\r\n\u2028\u2029]|\A)"),
            new TestCase("$", "!m", @"\z"),
            new TestCase("abcd$", "!m", @"abcd\z"),
            new TestCase(@"abc^def$gh", "!m", @"abc^def\zgh"),
            new TestCase(@"^\^abc\w*?\$$", "!m", @"^\^abc\w*?\$\z"),

            new TestCase("^abcd", "!m", "^abcd"),
            new TestCase("^abcd", "m", @"(?<=[\r\n\u2028\u2029]|\A)abcd"),
            new TestCase("$", "m", @"(?=[\r\n\u2028\u2029]|\z)"),
            new TestCase("abcd$", "m", @"abcd(?=[\r\n\u2028\u2029]|\z)"),
            new TestCase(@"abc^def$gh", "m", @"abc(?<=[\r\n\u2028\u2029]|\A)def(?=[\r\n\u2028\u2029]|\z)gh"),
            new TestCase(@"^\^abc\w*?$\$$", "m", @"(?<=[\r\n\u2028\u2029]|\A)\^abc\w*?(?=[\r\n\u2028\u2029]|\z)\$(?=[\r\n\u2028\u2029]|\z)")
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withStartAndEndAnchors))]
        public void transpileTest_withStartAndEndAnchors(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withPipe = TupleHelper.toArrays(
            new TestCase("|"),
            new TestCase("a|"),
            new TestCase("|a"),
            new TestCase("||"),
            new TestCase("a|b"),
            new TestCase("a|b|c|d"),
            new TestCase(@"a*|b+?|cdef{1,2}|\r\w\S??"),
            new TestCase(@"^abc*|def?g+$|h", "!m", @"^abc*|def?g+\z|h"),
            new TestCase(@"^abc*|def?g+$|h", "m", @"(?<=[\r\n\u2028\u2029]|\A)abc*|def?g+(?=[\r\n\u2028\u2029]|\z)|h")
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withPipe))]
        public void transpileTest_withPipe(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withDot = TupleHelper.toArrays(
            new TestCase(".", "!s", @"[^\r\n\u2028\u2029]"),
            new TestCase(".", "s", @"[\s\S]"),
            new TestCase("abc...def.g", "!s", @"abc[^\r\n\u2028\u2029][^\r\n\u2028\u2029][^\r\n\u2028\u2029]def[^\r\n\u2028\u2029]g"),
            new TestCase("abc...def.g", "s", @"abc[\s\S][\s\S][\s\S]def[\s\S]g"),
            new TestCase(@".*.+.*?.+?.{1}.{1,}.{1,2}", "!s", @"[^\r\n\u2028\u2029]*[^\r\n\u2028\u2029]+[^\r\n\u2028\u2029]*?[^\r\n\u2028\u2029]+?[^\r\n\u2028\u2029]{1}[^\r\n\u2028\u2029]{1,}[^\r\n\u2028\u2029]{1,2}"),
            new TestCase(@".*.+.*?.+?.{1}.{1,}.{1,2}", "s", @"[\s\S]*[\s\S]+[\s\S]*?[\s\S]+?[\s\S]{1}[\s\S]{1,}[\s\S]{1,2}"),

            new TestCase(@"abc.*\n\[$", "!s,!m", @"abc[^\r\n\u2028\u2029]*\n\[\z"),
            new TestCase(@"abc.*\n\[$", "s,!m", @"abc[\s\S]*\n\[\z"),
            new TestCase(@"abc.*\n\[$", "s,m", @"abc[\s\S]*\n\[(?=[\r\n\u2028\u2029]|\z)"),

            new TestCase(@"^.$|ab.c|d", "!s,!m", @"^[^\r\n\u2028\u2029]\z|ab[^\r\n\u2028\u2029]c|d"),
            new TestCase(@"^.$|ab.c|d", "s,!m", @"^[\s\S]\z|ab[\s\S]c|d"),
            new TestCase(@"^.$|ab.c|d", "s,m", @"(?<=[\r\n\u2028\u2029]|\A)[\s\S](?=[\r\n\u2028\u2029]|\z)|ab[\s\S]c|d")
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withDot))]
        public void transpileTest_withDot(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withBuiltInClasses = TupleHelper.toArrays(
            new TestCase(@"\w"),
            new TestCase(@"\w\d\s\W\D\S"),
            new TestCase(@"\w{0}\s{5,}\d+\w{20,30}"),
            new TestCase(@"\w*\d+\s??abc\w|^\d*_*$", "!m", @"\w*\d+\s??abc\w|^\d*_*\z"),
            new TestCase(@"\w*\d+\s??abc\w|^\d*_*$", "m", @"\w*\d+\s??abc\w|(?<=[\r\n\u2028\u2029]|\A)\d*_*(?=[\r\n\u2028\u2029]|\z)")
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withBuiltInClasses))]
        public void transpileTest_withBuiltInClasses(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withHexEscapes = TupleHelper.toArrays(
            new TestCase(@"\x00"),
            new TestCase(@"\x09"),
            new TestCase(@"\x30"),
            new TestCase(@"\x3d"),
            new TestCase(@"\x3D"),
            new TestCase(@"\xa4"),
            new TestCase(@"\xA4"),
            new TestCase(@"\xff"),
            new TestCase(@"\xFF"),
            new TestCase(@"abc\x49+|\xa0*993\d"),
            new TestCase(@"\u0000"),
            new TestCase(@"\u0038"),
            new TestCase(@"\u0497"),
            new TestCase(@"\u8956"),
            new TestCase(@"\uaaaa"),
            new TestCase(@"\uffff"),
            new TestCase(@"\uAAAA"),
            new TestCase(@"\uFFFF"),
            new TestCase(@"\u9c3d"),
            new TestCase(@"\u9C3D"),
            new TestCase(@"\u9c3D"),
            new TestCase(@"\u9C3d"),
            new TestCase(@"\x20_*\u03fd+|\ud834\udf4B\x7c|\u3c3E{4,5}g")
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withHexEscapes))]
        public void transpileTest_withHexEscapes(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withOctalEscapes = TupleHelper.toArrays(
            new TestCase(@"\0", @"\x00"),
            new TestCase(@"\00", @"\x00"),
            new TestCase(@"\1", @"\x01"),
            new TestCase(@"\7", @"\x07"),
            new TestCase(@"\07", @"\x07"),
            new TestCase(@"\12", @"\x0A"),
            new TestCase(@"\17", @"\x0F"),
            new TestCase(@"\20", @"\x10"),
            new TestCase(@"\46", @"\x26"),
            new TestCase(@"\75", @"\x3D"),
            new TestCase(@"\77", @"\x3F"),
            new TestCase(@"\000", @"\x00"),
            new TestCase(@"\007", @"\x07"),
            new TestCase(@"\012", @"\x0A"),
            new TestCase(@"\046", @"\x26"),
            new TestCase(@"\077", @"\x3F"),
            new TestCase(@"\255", @"\xAD"),
            new TestCase(@"\377", @"\xFF"),
            new TestCase(@"\0\7abc*\46{2,4}\5??\377|\255-\4-\13!", @"\x00\x07abc*\x26{2,4}\x05??\xFF|\xAD-\x04-\x0B!"),
            new TestCase(@"\8", @"8"),
            new TestCase(@"\9", @"9"),
            new TestCase(@"\08", @"\x008"),
            new TestCase(@"\78", @"\x078"),
            new TestCase(@"\259", @"\x159"),
            new TestCase(@"\400", @"\x200"),
            new TestCase(@"\408", @"\x208"),
            new TestCase(@"\777", @"\x3F7"),
            new TestCase(@"\078", @"\x078"),
            new TestCase(@"\087", @"\x0087"),
            new TestCase(@"\287", @"\x0287"),
            new TestCase(@"\779", @"\x3F9"),
            new TestCase(@"\789", @"\x0789"),
            new TestCase(@"\2550", @"\xAD0"),
            new TestCase(@"\2590", @"\x1590"),
            new TestCase(@"\3777", @"\xFF7"),
            new TestCase(@"\800", @"800"),
            new TestCase(@"\967", @"967"),
            new TestCase(@"\0/", @"\x00/"),
            new TestCase(@"\0:", @"\x00:"),
            new TestCase(@"\2/", @"\x02/"),
            new TestCase(@"\2:", @"\x02:"),
            new TestCase(@"\0/\0:\07/\07:\066/\066:\4/\4:\46/\46:", @"\x00/\x00:\x07/\x07:\x36/\x36:\x04/\x04:\x26/\x26:"),
            new TestCase(@"abc\2d", @"abc\x02d"),
            new TestCase(@"abc\27d", @"abc\x17d"),
            new TestCase(@"\12\23+\406*\377+abc|\779??_", @"\x0A\x13+\x206*\xFF+abc|\x3F9??_")
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withOctalEscapes))]
        public void transpileTest_withOctalEscapes(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_curlyBracesNotInQuantifier = TupleHelper.toArrays(
            new TestCase(@"{", @"\{"),
            new TestCase(@"}", @"\}"),
            new TestCase(@"{{{{", @"\{\{\{\{"),
            new TestCase(@"{}", @"\{\}"),
            new TestCase(@"{}*", @"\{\}*"),
            new TestCase(@"abc{", @"abc\{"),
            new TestCase(@"abc}", @"abc\}"),
            new TestCase(@"abc{1", @"abc\{1"),
            new TestCase(@"abc1}", @"abc1\}"),
            new TestCase(@"abc{1>", @"abc\{1>"),
            new TestCase(@"abc{1}}def", @"abc{1}\}def"),
            new TestCase(@"{*", @"\{*"),
            new TestCase(@"{{2}", @"\{{2}"),
            new TestCase(@"{{{2,6}}}", @"\{\{{2,6}\}\}"),
            new TestCase(@"abc{}", @"abc\{\}"),
            new TestCase(@"abc{}def", @"abc\{\}def"),
            new TestCase(@"abc{,}def", @"abc\{,\}def"),
            new TestCase(@"abc{1-3}def", @"abc\{1-3\}def"),

            new TestCase(@"abc{ }def", "!x", @"abc\{ \}def"),
            new TestCase(@"abc{ 1 }def", "!x", @"abc\{ 1 \}def"),
            new TestCase(@"abc{1 }def", "!x", @"abc\{1 \}def"),
            new TestCase(@"abc{ 1}def", "!x", @"abc\{ 1\}def"),
            new TestCase(@"abc{1, }def", "!x", @"abc\{1, \}def"),
            new TestCase(@"abc{ 1,}def", "!x", @"abc\{ 1,\}def"),
            new TestCase(@"abc{1, 4}def", "!x",  @"abc\{1, 4\}def"),
            new TestCase(@"abc{1,4 }def", "!x", @"abc\{1,4 \}def"),
            new TestCase(@"abc{ 1,4}def", "!x", @"abc\{ 1,4\}def"),
            new TestCase(@"abc{ 1,4 }def", "!x", @"abc\{ 1,4 \}def"),
            new TestCase(@"abc{ 1, 4 }def", "!x", @"abc\{ 1, 4 \}def"),

            new TestCase(@"abc{a}def", @"abc\{a\}def"),
            new TestCase(@"abc{12a}def", @"abc\{12a\}def"),
            new TestCase(@"abc{a,1}def", @"abc\{a,1\}def"),
            new TestCase(@"abc{1,a}def", @"abc\{1,a\}def"),
            new TestCase(@"abc{1,23a}def", @"abc\{1,23a\}def"),
            new TestCase(@"a{1}b{2,}c*?{3,q}z", @"a{1}b{2,}c*?\{3,q\}z"),
            new TestCase(@"\p{A}", @"p\{A\}")
        );

        [Theory]
        [MemberData(nameof(transpileTestData_curlyBracesNotInQuantifier))]
        public void transpileTest_curlyBracesNotInQuantifier(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withCharClasses = TupleHelper.toArrays(
            new TestCase("[a]"),
            new TestCase("[abc]"),
            new TestCase("[aaa]"),
            new TestCase(@"[]", @"(?!)"),
            new TestCase(@"[^]", @"[\s\S]"),
            new TestCase(@"[\b]", @"[\x08]"),
            new TestCase(@"[\b\B]", @"[\x08B]"),
            new TestCase(@"[-ab][ab-]", @"[\-ab][ab\-]"),
            new TestCase(@"[^a]", @"[^a]"),
            new TestCase(@"[^-a]", @"[^\-a]"),
            new TestCase(@"[^-]", @"[^\-]"),
            new TestCase(@"[^abc]"),
            new TestCase(@"[abc^][ab^c][^ab^c]"),
            new TestCase(@"[^^]", @"[^^]"),
            new TestCase(@"[()$]", @"[()$]"),
            new TestCase(@"[^()$]", @"[^()$]"),
            new TestCase(@"[^[]", @"[^\[]"),
            new TestCase(@"[[][ab[c]", @"[\[][ab\[c]"),
            new TestCase(@"[^[][ab[c]", @"[^\[][ab\[c]"),
            new TestCase(@"[\w\d\s][\W\D\S]"),
            new TestCase(@"[^\w\d\s][^\W\D\S]"),
            new TestCase(@"[^\wx34\b\S\x12\u30ff]", @"[^\wx34\x08\S\x12\u30ff]"),
            new TestCase(@"[abc\0\1\34p\377q\407\9]", @"[abc\x00\x01\x1Cp\xFFq\x2079]"),
            new TestCase(@"[abc.def*gh?]"),
            new TestCase(@"[a-z]"),
            new TestCase(@"[a\-z]"),
            new TestCase(@"[a-a]"),
            new TestCase(@"[^a-z]"),
            new TestCase(@"[\x01-\xF2][\u1932-\uC678][\x35-\u0037\u0049-\x59][\xFF-\u25AB][\xC4-\u00c4\u00c4-\xc4]"),
            new TestCase(@"[^\x01-\xF2][^\u1932-\uC678][^\x35-\u0037\u0049-\x59][^\xFF-\u25AB][^\xC4-\u00c4\u00c4-\xc4]"),
            new TestCase(@"[A-\x60][\x40-a][A-\uFF01\u0006-a][\u2713-✔\u2714-✔✔-\u2716]"),
            new TestCase(@"[^A-\x60][^\x40-a][^A-\uFF01\u0006-a][^\u2713-✔\u2714-✔✔-\u2716]"),
            new TestCase(@"[abc-z05-9][5-7aq\x87-\xbd\xc8\u1137\u1334-\u1339]"),
            new TestCase(@"[\b-\x09\x07-\b\x08-\b][abc\be-g]", @"[\x08-\x09\x07-\x08\x08-\x08][abc\x08e-g]"),
            new TestCase(@"[^^-z][^!-^]"),
            new TestCase(@"[a-z-]", @"[a-z\-]"),
            new TestCase(@"[^a-z-]", @"[^a-z\-]"),
            new TestCase(@"[a-z-b]", @"[a-z\-b]"),
            new TestCase(@"[^a-z-b]", @"[^a-z\-b]"),
            new TestCase(@"[-a-z]", @"[\-a-z]"),
            new TestCase(@"[^-a-z]", @"[^\-a-z]"),
            new TestCase(@"[!----a]", @"[!-\-\--a]"),
            new TestCase(@"[!----a-b]", @"[!-\-\--a\-b]"),
            new TestCase(@"[-]", @"[\-]"),
            new TestCase(@"[AB-[C]]", @"[AB-\[C]]"),
            new TestCase(@"[A-Z-[G-H]]", @"[A-Z\-\[G-H]]"),
            new TestCase(@"[a-z-[g-h]]", @"[a-z\-\[g-h]]"),
            new TestCase(@"[a-z-[g-h\b]\b]", @"[a-z\-\[g-h\x08]\b]"),
            new TestCase(@"[-\x37][-\u1377][^-\x37][^-\u1377]", @"[\-\x37][\-\u1377][^\-\x37][^\-\u1377]"),
            new TestCase(@"[\w-\d-\s-\W-\D-\S-][-\w-\d-\s-\W-\D-\S][^\w-\d-\s]", @"[\w\-\d\-\s\-\W\-\D\-\S\-][\-\w\-\d\-\s\-\W\-\D\-\S][^\w\-\d\-\s]"),
            new TestCase(@"[\w-1\d-1\s-1\W-1\D-1\S-1][z-\wz-\dz-\sz-\Wz-\Dz-\S]", @"[\w\-1\d\-1\s\-1\W\-1\D\-1\S\-1][z\-\wz\-\dz\-\sz\-\Wz\-\Dz\-\S]"),
            new TestCase(@"abc[0-9pq]*[.?/{]{1,5}|[1-z]*?tu??v[<->-]{10,}|[^ab\w-d]", @"abc[0-9pq]*[.?/{]{1,5}|[1-z]*?tu??v[<->\-]{10,}|[^ab\w\-d]")
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withCharClasses))]
        public void transpileTest_withCharClasses(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withCaptureGroups = TupleHelper.toArrays(
            new TestCase(@"()", new Result(null, 1)),
            new TestCase(@"(a)", new Result(null, 1)),
            new TestCase(@"(\()", new Result(null, 1)),
            new TestCase(@"(\))", new Result(null, 1)),
            new TestCase(@"([(])", new Result(null, 1)),
            new TestCase(@"([)])", new Result(null, 1)),
            new TestCase(@"(123)", new Result(null, 1)),
            new TestCase(@"abc(12)+d", new Result(null, 1)),
            new TestCase(@"abc(12)", new Result(null, 1)),
            new TestCase(@"()abc", new Result(null, 1)),
            new TestCase(@"abc()", new Result(null, 1)),
            new TestCase(@"abcd()+e", new Result(null, 1)),
            new TestCase(@"(ab+[c-z]+)*?", new Result(null, 1)),
            new TestCase(@"(a)(a)(a)", new Result(null, 3)),
            new TestCase(@"(((a)))", new Result(null, 3)),
            new TestCase(@"((a))(b)", new Result(null, 3)),
            new TestCase(@"(a)((b))", new Result(null, 3)),
            new TestCase(@"ab(c|d)(e|f)g", new Result(null, 2)),

            new TestCase(@"(^)", "!m", new Result(@"(^)", 1)),
            new TestCase(@"(^)", "m", new Result(@"((?<=[\r\n\u2028\u2029]|\A))", 1)),
            new TestCase(@"($)", "!m", new Result(@"(\z)", 1)),
            new TestCase(@"($)", "m", new Result(@"((?=[\r\n\u2028\u2029]|\z))", 1)),

            new TestCase(@"ab(([0-9]+[.][0-9]+zzz|(pqr?){5,})*uv\(\)w{1,300}|foo)eee", new Result(null, 3)),

            new TestCase(
                @"ab((^[0-9]+[.][0-9]+zzz|(pqr?){5,})*.+u|foo$)eee",
                "!s,!m",
                new Result(@"ab((^[0-9]+[.][0-9]+zzz|(pqr?){5,})*[^\r\n\u2028\u2029]+u|foo\z)eee", 3)
            ),
            new TestCase(
                @"ab((^[0-9]+[.][0-9]+zzz|(pqr?){5,})*.+u|foo$)eee",
                "s,!m",
                new Result(@"ab((^[0-9]+[.][0-9]+zzz|(pqr?){5,})*[\s\S]+u|foo\z)eee", 3)
            ),
            new TestCase(
                @"ab((^[0-9]+[.][0-9]+zzz|(pqr?){5,})*.+u|foo$)eee",
                "!s,m",
                new Result(@"ab(((?<=[\r\n\u2028\u2029]|\A)[0-9]+[.][0-9]+zzz|(pqr?){5,})*[^\r\n\u2028\u2029]+u|foo(?=[\r\n\u2028\u2029]|\z))eee", 3)
            ),
            new TestCase(
                @"ab((^[0-9]+[.][0-9]+zzz|(pqr?){5,})*.+u|foo$)eee",
                "s,m",
                new Result(@"ab(((?<=[\r\n\u2028\u2029]|\A)[0-9]+[.][0-9]+zzz|(pqr?){5,})*[\s\S]+u|foo(?=[\r\n\u2028\u2029]|\z))eee", 3)
            ),

            new TestCase(String.Concat(Enumerable.Repeat("()", 999)), new Result(null, 999)),
            new TestCase(String.Concat(Enumerable.Repeat("([0-9]+)*", 999)), new Result(null, 999)),

            new TestCase(
                String.Concat(Enumerable.Repeat(@"((([0-9]+)*c)d$)", 333)),
                "!m",
                new Result(String.Concat(Enumerable.Repeat(@"((([0-9]+)*c)d\z)", 333)), 999)
            ),
            new TestCase(
                String.Concat(Enumerable.Repeat(@"((([0-9]+)*c)d$)", 333)),
                "m",
                new Result(String.Concat(Enumerable.Repeat(@"((([0-9]+)*c)d(?=[\r\n\u2028\u2029]|\z))", 333)), 999)
            )
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withCaptureGroups))]
        public void transpileTest_withCaptureGroups(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withNonCaptureGroups = TupleHelper.toArrays(
            new TestCase(@"(?:)", new Result(null, 0)),
            new TestCase(@"(?:a)", new Result(null, 0)),
            new TestCase(@"(?:123)", new Result(null, 0)),
            new TestCase(@"abc(?:12)+d", new Result(null, 0)),
            new TestCase(@"abc(?:12)", new Result(null, 0)),
            new TestCase(@"(?:)abc", new Result(null, 0)),
            new TestCase(@"abc(?:)", new Result(null, 0)),
            new TestCase(@"abcd(?:)+e", new Result(null, 0)),
            new TestCase(@"(?:ab+[c-z]+)*?", new Result(null, 0)),
            new TestCase(@"(?:a)(?:a)(?:a)", new Result(null, 0)),
            new TestCase(@"(a)(?:a)(?:a)", new Result(null, 1)),
            new TestCase(@"(?:a)(a)(?:a)", new Result(null, 1)),
            new TestCase(@"(?:a)(?:a)(a)", new Result(null, 1)),
            new TestCase(@"(a)(?:a)(a)", new Result(null, 2)),
            new TestCase(@"(?:a)(a)(a)", new Result(null, 2)),
            new TestCase(@"((?:))", new Result(null, 1)),
            new TestCase(@"(?:())", new Result(null, 1)),
            new TestCase(@"(?:((a)))", new Result(null, 2)),
            new TestCase(@"((?:(a)))", new Result(null, 2)),
            new TestCase(@"(((?:a)))", new Result(null, 2)),
            new TestCase(@"(?:(?:(a)))", new Result(null, 1)),
            new TestCase(@"(?:((?:a)))", new Result(null, 1)),
            new TestCase(@"(?:(?:(?:a)))", new Result(null, 0)),
            new TestCase(@"(?:(a))(b)", new Result(null, 2)),
            new TestCase(@"((?:a))(b)", new Result(null, 2)),
            new TestCase(@"((a))(?:b)", new Result(null, 2)),
            new TestCase(@"(?:a)((b))", new Result(null, 2)),
            new TestCase(@"(a)(?:(b))", new Result(null, 2)),
            new TestCase(@"(a)((?:b))", new Result(null, 2)),
            new TestCase(@"(?:(?:a))(b)", new Result(null, 1)),
            new TestCase(@"((?:a))(?:b)", new Result(null, 1)),
            new TestCase(@"(?:(a))(?:b)", new Result(null, 1)),
            new TestCase(@"(?:a)(?:(b))", new Result(null, 1)),
            new TestCase(@"(a)(?:(?:b))", new Result(null, 1)),
            new TestCase(@"(?:a)((?:b))", new Result(null, 1)),
            new TestCase(@"(?:a)(?:(?:b))", new Result(null, 0)),
            new TestCase(@"(?:(?:a))(?:b)", new Result(null, 0)),
            new TestCase(@"ab(c|d)(?:e|f)g", new Result(null, 1)),

            new TestCase(@"ab(?:(?:[0-9]+[.][0-9]+zzz|(?:pqr?){5,})*uv\(\)w{1,300}|foo)eee", new Result(null, 0)),
            new TestCase(@"ab(?:(?:[0-9]+[.][0-9]+zzz|(pqr?){5,})*uv\(\)w{1,300}|foo)eee", new Result(null, 1)),
            new TestCase(@"ab((?:[0-9]+[.][0-9]+zzz|(?:pqr?){5,})*uv\(\)w{1,300}|foo)eee", new Result(null, 1)),
            new TestCase(@"ab(?:([0-9]+[.][0-9]+zzz|(pqr?){5,})*uv\(\)w{1,300}|foo)eee", new Result(null, 2)),

            new TestCase(@"(?:^)", "!m", new Result(@"(?:^)", 0)),
            new TestCase(@"(?:^)", "m", new Result(@"(?:(?<=[\r\n\u2028\u2029]|\A))", 0)),
            new TestCase(@"(?:$)", "!m", new Result(@"(?:\z)", 0)),
            new TestCase(@"(?:$)", "m", new Result(@"(?:(?=[\r\n\u2028\u2029]|\z))", 0)),

            new TestCase(
                @"ab(?:(^[0-9]+[.][0-9]+zzz|(pqr?){5,})*.+u|foo$)eee",
                "!s,!m",
                new Result(@"ab(?:(^[0-9]+[.][0-9]+zzz|(pqr?){5,})*[^\r\n\u2028\u2029]+u|foo\z)eee", 2)
            ),
            new TestCase(
                @"ab(?:(^[0-9]+[.][0-9]+zzz|(pqr?){5,})*.+u|foo$)eee",
                "s,!m",
                new Result(@"ab(?:(^[0-9]+[.][0-9]+zzz|(pqr?){5,})*[\s\S]+u|foo\z)eee", 2)
            ),
            new TestCase(
                @"ab(?:(^[0-9]+[.][0-9]+zzz|(pqr?){5,})*.+u|foo$)eee",
                "!s,m",
                new Result(@"ab(?:((?<=[\r\n\u2028\u2029]|\A)[0-9]+[.][0-9]+zzz|(pqr?){5,})*[^\r\n\u2028\u2029]+u|foo(?=[\r\n\u2028\u2029]|\z))eee", 2)
            ),
            new TestCase(
                @"ab(?:(^[0-9]+[.][0-9]+zzz|(pqr?){5,})*.+u|foo$)eee",
                "s,m",
                new Result(@"ab(?:((?<=[\r\n\u2028\u2029]|\A)[0-9]+[.][0-9]+zzz|(pqr?){5,})*[\s\S]+u|foo(?=[\r\n\u2028\u2029]|\z))eee", 2)
            ),

            new TestCase(String.Concat(Enumerable.Repeat("(?:[0-9]+)*", 999)), new Result(null, 0)),
            new TestCase(String.Concat(Enumerable.Repeat("(?:[0-9]+)*", 1000)), new Result(null, 0)),
            new TestCase(String.Concat(Enumerable.Repeat("(a)(?:[0-9]+)*", 999)), new Result(null, 999)),
            new TestCase(String.Concat(Enumerable.Repeat("(a|b)+(?:[0-9]+)*", 999)), new Result(null, 999)),
            new TestCase(String.Concat(Enumerable.Repeat("((?:([0-9]+)*c)d)", 333)), new Result(null, 666)),

            new TestCase(
                String.Concat(Enumerable.Repeat(@"((([0-9]+)*c)d$)(?:e)", 333)),
                "!m",
                new Result(String.Concat(Enumerable.Repeat(@"((([0-9]+)*c)d\z)(?:e)", 333)), 999)
            ),
            new TestCase(
                String.Concat(Enumerable.Repeat(@"((([0-9]+)*c)d$)(?:e)", 333)),
                "m",
                new Result(String.Concat(Enumerable.Repeat(@"((([0-9]+)*c)d(?=[\r\n\u2028\u2029]|\z))(?:e)", 333)), 999)
            )
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withNonCaptureGroups))]
        public void transpileTest_withNonCaptureGroups(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withNamedCaptureGroups = TupleHelper.toArrays(
            new TestCase(
                @"(?P<a>)",
                new Result("()", groupNames: new[] {"a"})
            ),
            new TestCase(
                @"(?P<abcd>)",
                new Result("()", groupNames: new[] {"abcd"})
            ),
            new TestCase(
                @"(?P<a><)(?P<b>>)",
                new Result("(<)(>)", groupNames: new[] {"a", "b"})
            ),
            new TestCase(
                @"xxx(?P<abcd>)xxx",
                new Result("xxx()xxx", groupNames: new[] {"abcd"})
            ),
            new TestCase(
                @"(?P<a>a)",
                new Result("(a)", groupNames: new[] {"a"})
            ),
            new TestCase(
                @"(?P<abc>a)",
                new Result("(a)", groupNames: new[] {"abc"})
            ),
            new TestCase(
                @"(?P<A>a*|b*)",
                new Result("(a*|b*)", groupNames: new[] {"A"})
            ),
            new TestCase(
                @"(?P<Ab>a*|b*)",
                new Result("(a*|b*)", groupNames: new[] {"Ab"})
            ),
            new TestCase(
                @"(?P<Ab129_>a*|b*)",
                new Result("(a*|b*)", groupNames: new[] {"Ab129_"})
            ),
            new TestCase(
                @"(?P<ab129_>a*|b*)",
                new Result("(a*|b*)", groupNames: new[] {"ab129_"})
            ),
            new TestCase(
                @"(?P<_b129_>a*|b*)",
                new Result("(a*|b*)", groupNames: new[] {"_b129_"})
            ),
            new TestCase(
                @"abc(?P<a>12)+d",
                new Result("abc(12)+d", groupNames: new[] {"a"})
            ),
            new TestCase(
                @"(?P<a>)abc",
                new Result("()abc", groupNames: new[] {"a"})
            ),
            new TestCase(
                @"(?P<abcd>)abc",
                new Result("()abc", groupNames: new[] {"abcd"})
            ),
            new TestCase(
                @"abc(?P<a>)abc",
                new Result("abc()abc", groupNames: new[] {"a"})
            ),
            new TestCase(
                @"abc(?P<abcd>)abc",
                new Result("abc()abc", groupNames: new[] {"abcd"})
            ),
            new TestCase(
                @"(?P<x>ab+[c-z]+)*?",
                new Result("(ab+[c-z]+)*?", groupNames: new[] {"x"})
            ),
            new TestCase(
                @"(?P<x1>a)(?P<x2>a)(?P<x3>a)",
                new Result("(a)(a)(a)", groupNames: new[] {"x1", "x2", "x3"})
            ),
            new TestCase(
                @"(a)(?P<x2>a)(?P<x3>a)",
                new Result("(a)(a)(a)", groupNames: new[] {null, "x2", "x3"})
            ),
            new TestCase(
                @"(?P<x1>a)(a)(?P<x3>a)",
                new Result("(a)(a)(a)", groupNames: new[] {"x1", null, "x3"})
            ),
            new TestCase(
                @"(?P<x1>a)(?P<x2>a)(a)",
                new Result("(a)(a)(a)", groupNames: new[] {"x1", "x2", null})
            ),
            new TestCase(
                @"(a)(a)(?P<x3>a)",
                new Result("(a)(a)(a)", groupNames: new[] {null, null, "x3"})
            ),
            new TestCase(
                @"(a)(?P<x2>a)(a)",
                new Result("(a)(a)(a)", groupNames: new[] {null, "x2", null})
            ),
            new TestCase(
                @"(?P<x1>a)(?:a)(?P<x3>a)",
                new Result("(a)(?:a)(a)", groupNames: new[] {"x1", "x3"})
            ),
            new TestCase(
                @"(?:a)(a)(?P<x3>a)",
                new Result("(?:a)(a)(a)", groupNames: new[] {null, "x3"})
            ),
            new TestCase(
                @"(a)(?:a)(?P<x3>a)",
                new Result("(a)(?:a)(a)", groupNames: new[] {null, "x3"})
            ),
            new TestCase(
                @"(?:a)(?:a)(?P<x3>a)",
                new Result("(?:a)(?:a)(a)", groupNames: new[] {"x3"})
            ),
            new TestCase(
                @"(?:a)(?P<x2>a)(?:a)",
                new Result("(?:a)(a)(?:a)", groupNames: new[] {"x2"})
            ),
            new TestCase(
                @"(?P<x1>(?P<x2>(?P<x3>a)))",
                new Result("(((a)))", groupNames: new[] {"x1", "x2", "x3"})
            ),
            new TestCase(
                @"(?P<x1>(?P<x2>(a)))",
                new Result("(((a)))", groupNames: new[] {"x1", "x2", null})
            ),
            new TestCase(
                @"(?P<x1>((?P<x3>a)))",
                new Result("(((a)))", groupNames: new[] {"x1", null, "x3"})
            ),
            new TestCase(
                @"((?P<x2>(?P<x3>a)))",
                new Result("(((a)))", groupNames: new[] {null, "x2", "x3"})
            ),
            new TestCase(
                @"(?P<x1>((a)))",
                new Result("(((a)))", groupNames: new[] {"x1", null, null})
            ),
            new TestCase(
                @"((?P<x2>(a)))",
                new Result("(((a)))", groupNames: new[] {null, "x2", null})
            ),
            new TestCase(
                @"(((?P<x3>a)))",
                new Result("(((a)))", groupNames: new[] {null, null, "x3"})
            ),
            new TestCase(
                @"(?:((?P<x3>a)))",
                new Result("(?:((a)))", groupNames: new[] {null, "x3"})
            ),
            new TestCase(
                @"(?:(?:(?P<x3>a)))",
                new Result("(?:(?:(a)))", groupNames: new[] {"x3"})
            ),
            new TestCase(
                @"(?P<x1>(?P<x2>a))(?P<y>b)",
                new Result("((a))(b)", groupNames: new[] {"x1", "x2", "y"})
            ),
            new TestCase(
                @"(?P<x>a)(?P<y1>(?P<y2>b))",
                new Result("(a)((b))", groupNames: new[] {"x", "y1", "y2"})
            ),
            new TestCase(
                "(?P<abc>abc)(?P<Abc>def)",
                new Result("(abc)(def)", new[] {"abc", "Abc"})
            ),

            new TestCase(
                @"(?P<a>([0-9]){3}-)+(?P<b>(?:[0-9].){4,5})(?P<c>(foo)$)",
                "!s,!m",
                new Result(@"(([0-9]){3}-)+((?:[0-9][^\r\n\u2028\u2029]){4,5})((foo)\z)", groupNames: new[] {"a", null, "b", "c", null})
            ),
            new TestCase(
                @"(?P<a>([0-9]){3}-)+(?P<b>(?:[0-9].){4,5})(?P<c>(foo)$)",
                "!s,m",
                new Result(@"(([0-9]){3}-)+((?:[0-9][^\r\n\u2028\u2029]){4,5})((foo)(?=[\r\n\u2028\u2029]|\z))", groupNames: new[] {"a", null, "b", "c", null})
            ),
            new TestCase(
                @"(?P<a>([0-9]){3}-)+(?P<b>(?:[0-9].){4,5})(?P<c>(foo)$)",
                "s,!m",
                new Result(@"(([0-9]){3}-)+((?:[0-9][\s\S]){4,5})((foo)\z)", groupNames: new[] {"a", null, "b", "c", null})
            ),
            new TestCase(
                @"(?P<a>([0-9]){3}-)+(?P<b>(?:[0-9].){4,5})(?P<c>(foo)$)",
                "s,m",
                new Result(@"(([0-9]){3}-)+((?:[0-9][\s\S]){4,5})((foo)(?=[\r\n\u2028\u2029]|\z))", groupNames: new[] {"a", null, "b", "c", null})
            ),

            new TestCase(
                @"ab(?P<_1>(?P<_2>[0-9]+[.][0-9]+zzz|(?P<_3>pqr?){5,})*uv\(\)w{1,300}|foo)eee",
                new Result(@"ab(([0-9]+[.][0-9]+zzz|(pqr?){5,})*uv\(\)w{1,300}|foo)eee", groupNames: new[] {"_1", "_2", "_3"})
            ),
            new TestCase(
                @"ab(?P<_1>(?P<_2>[0-9]+[.][0-9]+(zz(?:\u43FD))|(?P<_3>pqr?){5,})*uv\(\)w{1,300}|\032)eee",
                new Result(@"ab(([0-9]+[.][0-9]+(zz(?:\u43FD))|(pqr?){5,})*uv\(\)w{1,300}|\x1A)eee", groupNames: new[] {"_1", "_2", null, "_3"})
            ),
            new TestCase(
                String.Concat(Enumerable.Range(0, 999).Select(i => $"(?P<a{i}>xyz)+")),
                new Result(
                    String.Concat(Enumerable.Range(0, 999).Select(i => "(xyz)+")),
                    groupNames: Enumerable.Range(0, 999).Select(i => "a" + i).ToArray()
                )
            ),
            new TestCase(
                String.Concat(Enumerable.Range(0, 333).Select(i => $"(?P<a{i}>(?P<b{i}>xyz))+([123]*)")),
                new Result(
                    String.Concat(Enumerable.Range(0, 333).Select(i => "((xyz))+([123]*)")),
                    groupNames: Enumerable.Range(0, 333).SelectMany(i => new[] {"a" + i, "b" + i, null}).ToArray()
                )
            )
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withNamedCaptureGroups))]
        public void transpileTest_withNamedCaptureGroups(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withLookarounds = TupleHelper.toArrays(
            new TestCase(@"(?=)"),
            new TestCase(@"(?!)"),
            new TestCase(@"(?<=)"),
            new TestCase(@"(?<!)"),
            new TestCase(@"(?=a)"),
            new TestCase(@"(?!a)"),
            new TestCase(@"(?<=a)"),
            new TestCase(@"(?<!a)"),
            new TestCase(@"a(?=a)b"),
            new TestCase(@"a(?!a)b"),
            new TestCase(@"a(?<=a)b"),
            new TestCase(@"a(?<!a)b"),
            new TestCase(@"(?=(?:ab*c|(d[\d_]+)z)?)", new Result(null, 1)),
            new TestCase(@"(?!(?:ab*c|(d[\d_]+)z)?)", new Result(null, 1)),
            new TestCase(@"(?<=(?:ab*c|(d[\d_]+)z)?)", new Result(null, 1)),
            new TestCase(@"(?<!(?:ab*c|(d[\d_]+)z)?)", new Result(null, 1)),
            new TestCase(@"(?=\()(?!\()(?<=\()(?<!\()"),
            new TestCase(@"(?=\))(?!\))(?<=\))(?<!\))"),
            new TestCase(@"_(?==)_(?!!)_(?<==)_(?<=<)_(?<!<)_(?<!!)_"),
            new TestCase(@"(?<=(?<=(?<=a)b)c)d"),
            new TestCase(@"(?<!(?<!(?<!a)b)c)d"),
            new TestCase(@"a(?=b(?=c(?=d)))"),
            new TestCase(@"a(?!b(?!c(?!d)))"),
            new TestCase(@"a(?=b)*(?<!abc){2,4}(?!(?=ab)+c*){8}"),

            new TestCase(@"(?=^)(?!^)(?<=^)(?<!^)", "!m", @"(?=^)(?!^)(?<=^)(?<!^)"),
            new TestCase(@"(?=^)(?!^)(?<=^)(?<!^)", "m", @"(?=(?<=[\r\n\u2028\u2029]|\A))(?!(?<=[\r\n\u2028\u2029]|\A))(?<=(?<=[\r\n\u2028\u2029]|\A))(?<!(?<=[\r\n\u2028\u2029]|\A))"),
            new TestCase(@"(?=(?<!(?:(^))))", "!m", new Result(@"(?=(?<!(?:(^))))", 1)),
            new TestCase(@"(?=(?<!(?:(^))))", "m", new Result(@"(?=(?<!(?:((?<=[\r\n\u2028\u2029]|\A)))))", 1)),

            new TestCase(@"(?=$)(?!$)(?<=$)(?<!$)", "!m", @"(?=\z)(?!\z)(?<=\z)(?<!\z)"),
            new TestCase(@"(?=$)(?!$)(?<=$)(?<!$)", "m", @"(?=(?=[\r\n\u2028\u2029]|\z))(?!(?=[\r\n\u2028\u2029]|\z))(?<=(?=[\r\n\u2028\u2029]|\z))(?<!(?=[\r\n\u2028\u2029]|\z))"),
            new TestCase(@"(?=(?<!(?:($))))", "!m", new Result(@"(?=(?<!(?:(\z))))", 1)),
            new TestCase(@"(?=(?<!(?:($))))", "m", new Result(@"(?=(?<!(?:((?=[\r\n\u2028\u2029]|\z)))))", 1)),

            new TestCase(
                @"(?P<x>abc*z|[0-9.]+(?=[a-z]{3,})|&+)[\w\d]*?\n(?!\s*[a-f])",
                new Result(@"(abc*z|[0-9.]+(?=[a-z]{3,})|&+)[\w\d]*?\n(?!\s*[a-f])", groupNames: new string[] {"x"})
            ),

            new TestCase(
                @"^(?<!\s*[a-f]$)(?P<x>abc*z|(?<=^[a-z]{3,}.*y*|$)[0-9.]+|&+)[\w\d]*?\n?",
                "!s,!m",
                new Result(@"^(?<!\s*[a-f]\z)(abc*z|(?<=^[a-z]{3,}[^\r\n\u2028\u2029]*y*|\z)[0-9.]+|&+)[\w\d]*?\n?", groupNames: new string[] {"x"})
            ),
            new TestCase(
                @"^(?<!\s*[a-f]$)(?P<x>abc*z|(?<=^[a-z]{3,}.*y*|$)[0-9.]+|&+)[\w\d]*?\n?",
                "s,!m",
                new Result(@"^(?<!\s*[a-f]\z)(abc*z|(?<=^[a-z]{3,}[\s\S]*y*|\z)[0-9.]+|&+)[\w\d]*?\n?", groupNames: new string[] {"x"})
            ),
            new TestCase(
                @"^(?<!\s*[a-f]$)(?P<x>abc*z|(?<=^[a-z]{3,}.*y*|$)[0-9.]+|&+)[\w\d]*?\n?",
                "!s,m",
                new Result(
                    @"(?<=[\r\n\u2028\u2029]|\A)(?<!\s*[a-f](?=[\r\n\u2028\u2029]|\z))(abc*z|(?<=(?<=[\r\n\u2028\u2029]|\A)[a-z]{3,}[^\r\n\u2028\u2029]*y*|(?=[\r\n\u2028\u2029]|\z))[0-9.]+|&+)[\w\d]*?\n?",
                    groupNames: new string[] {"x"}
                )
            ),
            new TestCase(
                @"^(?<!\s*[a-f]$)(?P<x>abc*z|(?<=^[a-z]{3,}.*y*|$)[0-9.]+|&+)[\w\d]*?\n?",
                "s,m",
                new Result(
                    @"(?<=[\r\n\u2028\u2029]|\A)(?<!\s*[a-f](?=[\r\n\u2028\u2029]|\z))(abc*z|(?<=(?<=[\r\n\u2028\u2029]|\A)[a-z]{3,}[\s\S]*y*|(?=[\r\n\u2028\u2029]|\z))[0-9.]+|&+)[\w\d]*?\n?",
                    groupNames: new string[] {"x"}
                )
            ),

            new TestCase(String.Concat(Enumerable.Repeat("(?=)", 999))),
            new TestCase(String.Concat(Enumerable.Repeat("(?!)", 999))),
            new TestCase(String.Concat(Enumerable.Repeat("(?<=)", 999))),
            new TestCase(String.Concat(Enumerable.Repeat("(?<!)", 999))),
            new TestCase(String.Concat(Enumerable.Repeat("(?=)", 1000))),
            new TestCase(String.Concat(Enumerable.Repeat("(?!)", 1000))),
            new TestCase(String.Concat(Enumerable.Repeat("(?<=)", 1000))),
            new TestCase(String.Concat(Enumerable.Repeat("(?<!)", 1000)))
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withLookarounds))]
        public void transpileTest_withLookarounds(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withBackreferences = TupleHelper.toArrays(
            new TestCase(@"()\1", new Result(@"()\k<1>", 1)),
            new TestCase(@"(a)\1", new Result(@"(a)\k<1>", 1)),
            new TestCase(@"\1(a)", new Result(@"\k<1>(a)", 1)),
            new TestCase(@"(a)\1\01\001", new Result(@"(a)\k<1>\x01\x01", 1)),
            new TestCase(@"(a)(a)(a)\1", new Result(@"(a)(a)(a)\k<1>", 3)),
            new TestCase(@"(a)(a)(a)\2", new Result(@"(a)(a)(a)\k<2>", 3)),
            new TestCase(@"(a)(a)(a)\3", new Result(@"(a)(a)(a)\k<3>", 3)),
            new TestCase(@"(a)(a)(a)\1\2\3", new Result(@"(a)(a)(a)\k<1>\k<2>\k<3>", 3)),
            new TestCase(@"\1\2\3(a)(a)(a)", new Result(@"\k<1>\k<2>\k<3>(a)(a)(a)", 3)),
            new TestCase(@"(a)\1(a)\2(a)\3", new Result(@"(a)\k<1>(a)\k<2>(a)\k<3>", 3)),
            new TestCase(@"(((a)))\1\2\3", new Result(@"(((a)))\k<1>\k<2>\k<3>", 3)),
            new TestCase(@"(\1(\3(a\2)))", new Result(@"(\k<1>(\k<3>(a\k<2>)))", 3)),
            new TestCase(@"\1(\1a)\1", new Result(@"\k<1>(\k<1>a)\k<1>", 1)),
            new TestCase(@"([a-z])999(?=\1+)", new Result(@"([a-z])999(?=\k<1>+)", 1)),
            new TestCase(@"(\w)\1{10,}", new Result(@"(\w)\k<1>{10,}", 1)),
            new TestCase(@"(a)(a)(a)\1\1\2\2\3\3", new Result(@"(a)(a)(a)\k<1>\k<1>\k<2>\k<2>\k<3>\k<3>", 3)),
            new TestCase(@"(\1(\3(\2a\2)\1)\3)", new Result(@"(\k<1>(\k<3>(\k<2>a\k<2>)\k<1>)\k<3>)", 3)),

            new TestCase(
                @"((ab)*\w+([0-9].{4,})-?[0-9]*\3|\2*pq(?:[|/?(])+?\1|(?<!\1)foo$)\2",
                "!s,!m",
                new Result(@"((ab)*\w+([0-9][^\r\n\u2028\u2029]{4,})-?[0-9]*\k<3>|\k<2>*pq(?:[|/?(])+?\k<1>|(?<!\k<1>)foo\z)\k<2>", 3)
            ),
            new TestCase(
                @"((ab)*\w+([0-9].{4,})-?[0-9]*\3|\2*pq(?:[|/?(])+?\1|(?<!\1)foo$)\2",
                "s,!m",
                new Result(@"((ab)*\w+([0-9][\s\S]{4,})-?[0-9]*\k<3>|\k<2>*pq(?:[|/?(])+?\k<1>|(?<!\k<1>)foo\z)\k<2>", 3)
            ),
            new TestCase(
                @"((ab)*\w+([0-9].{4,})-?[0-9]*\3|\2*pq(?:[|/?(])+?\1|(?<!\1)foo$)\2",
                "!s,m",
                new Result(@"((ab)*\w+([0-9][^\r\n\u2028\u2029]{4,})-?[0-9]*\k<3>|\k<2>*pq(?:[|/?(])+?\k<1>|(?<!\k<1>)foo(?=[\r\n\u2028\u2029]|\z))\k<2>", 3)
            ),
            new TestCase(
                @"((ab)*\w+([0-9].{4,})-?[0-9]*\3|\2*pq(?:[|/?(])+?\1|(?<!\1)foo$)\2",
                "s,m",
                new Result(@"((ab)*\w+([0-9][\s\S]{4,})-?[0-9]*\k<3>|\k<2>*pq(?:[|/?(])+?\k<1>|(?<!\k<1>)foo(?=[\r\n\u2028\u2029]|\z))\k<2>", 3)
            ),

            new TestCase(
                @"(?P<x>(?P<y>ab)*\w+(?P<z>^[0-9]{4,})-?[0-9]*\3|\2*pq(?:[|/?(])+?\1|(?<!\1)foo$)\2",
                "!m",
                new Result(
                    @"((ab)*\w+(^[0-9]{4,})-?[0-9]*\k<3>|\k<2>*pq(?:[|/?(])+?\k<1>|(?<!\k<1>)foo\z)\k<2>",
                    groupNames: new[] {"x", "y", "z"}
                )
            ),
            new TestCase(
                @"(?P<x>(?P<y>ab)*\w+(?P<z>^[0-9]{4,})-?[0-9]*\3|\2*pq(?:[|/?(])+?\1|(?<!\1)foo$)\2",
                "m",
                new Result(
                    @"((ab)*\w+((?<=[\r\n\u2028\u2029]|\A)[0-9]{4,})-?[0-9]*\k<3>|\k<2>*pq(?:[|/?(])+?\k<1>|(?<!\k<1>)foo(?=[\r\n\u2028\u2029]|\z))\k<2>",
                    groupNames: new[] {"x", "y", "z"}
                )
            ),
            new TestCase(
                String.Concat(Enumerable.Repeat("(a)", 10)) + @"\1\2\3\4\5\6\7\8\9\10",
                new Result(String.Concat(Enumerable.Repeat("(a)", 10)) + @"\k<1>\k<2>\k<3>\k<4>\k<5>\k<6>\k<7>\k<8>\k<9>\k<10>", 10)
            ),
            new TestCase(
                @"\1\2\3\4\5\6\7\8\9\10" + String.Concat(Enumerable.Repeat("(a)", 10)),
                new Result(@"\k<1>\k<2>\k<3>\k<4>\k<5>\k<6>\k<7>\k<8>\k<9>\k<10>" + String.Concat(Enumerable.Repeat("(a)", 10)), 10)
            ),
            new TestCase(
                String.Concat(Enumerable.Range(1, 99).Select(i => $"\\{i}(a)\\{i}")),
                new Result(String.Concat(Enumerable.Range(1, 99).Select(i => $"\\k<{i}>(a)\\k<{i}>")), 99)
            ),
            new TestCase(
                String.Concat(Enumerable.Range(1, 999).Select(i => $"\\{i}(a)\\{i}")),
                new Result(String.Concat(Enumerable.Range(1, 999).Select(i => $"\\k<{i}>(a)\\k<{i}>")), 999)
            ),

            new TestCase(@"(a)\0\1\2", new Result(@"(a)\x00\k<1>\x02", 1)),
            new TestCase(@"\0\1\2(a)", new Result(@"\x00\k<1>\x02(a)", 1)),
            new TestCase(@"(a)(a)(a)\0\1\2\3\4", new Result(@"(a)(a)(a)\x00\k<1>\k<2>\k<3>\x04", 3)),
            new TestCase(@"\0\1\2\3\4(a)(a)(a)", new Result(@"\x00\k<1>\k<2>\k<3>\x04(a)(a)(a)", 3)),

            new TestCase(
                @"(a)(a)(a)(a)(a)(a)(a)(a)(a)\1\2\3\4\5\6\7\8\9\10",
                new Result(@"(a)(a)(a)(a)(a)(a)(a)(a)(a)\k<1>\k<2>\k<3>\k<4>\k<5>\k<6>\k<7>\k<8>\k<9>\x08", 9)
            ),
            new TestCase(
                @"\67\68\69" + String.Concat(Enumerable.Repeat("(a)", 68)) + @"\67\68\69",
                new Result(@"\k<67>\k<68>\x069" + String.Concat(Enumerable.Repeat("(a)", 68)) + @"\k<67>\k<68>\x069", 68)
            ),
            new TestCase(
                @"\97\98\99" + String.Concat(Enumerable.Repeat("(a)", 98)) + @"\97\98\99",
                new Result(@"\k<97>\k<98>99" + String.Concat(Enumerable.Repeat("(a)", 98)) + @"\k<97>\k<98>99", 98)
            ),
            new TestCase(
                @"\99\100\101" + String.Concat(Enumerable.Repeat("(a)", 100)) + @"\99\100\101",
                new Result(@"\k<99>\k<100>\x41" + String.Concat(Enumerable.Repeat("(a)", 100)) + @"\k<99>\k<100>\x41", 100)
            ),
            new TestCase(
                @"\346\347\348\400" + String.Concat(Enumerable.Repeat("(a)", 346)) + @"\346\347\348\400",
                new Result(@"\k<346>\xE7\x1C8\x200" + String.Concat(Enumerable.Repeat("(a)", 346)) + @"\k<346>\xE7\x1C8\x200", 346)
            ),
            new TestCase(
                @"\618\619\620" + String.Concat(Enumerable.Repeat("(a)", 618)) + @"\618\619\620",
                new Result(@"\k<618>\x319\x320" + String.Concat(Enumerable.Repeat("(a)", 618)) + @"\k<618>\x319\x320", 618)
            ),
            new TestCase(
                @"\998\999" + String.Concat(Enumerable.Repeat("(a)", 998)) + @"\998\999",
                new Result(@"\k<998>999" + String.Concat(Enumerable.Repeat("(a)", 998)) + @"\k<998>999", 998)
            )
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withBackreferences))]
        public void transpileTest_withBackreferences(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withNamedBackreferences = TupleHelper.toArrays(
            new TestCase(
                @"(?P<a>)\k<a>",
                new Result(@"()\k<1>", groupNames: new[] {"a"})
            ),
            new TestCase(
                @"(?P<abcd>)\k<abcd>",
                new Result(@"()\k<1>", groupNames: new[] {"abcd"})
            ),
            new TestCase(
                @"\k<a>(?P<a>)",
                new Result(@"\k<1>()", groupNames: new[] {"a"})
            ),
            new TestCase(
                @"\k<abcd>(?P<abcd>)",
                new Result(@"\k<1>()", groupNames: new[] {"abcd"})
            ),
            new TestCase(
                @"(?P<a>a*|b*)\k<a>",
                new Result(@"(a*|b*)\k<1>", groupNames: new[] {"a"})
            ),
            new TestCase(
                @"(?P<ab>a*|b*)\k<ab>",
                new Result(@"(a*|b*)\k<1>", groupNames: new[] {"ab"})
            ),
            new TestCase(
                @"(?P<A>a*|b*)\k<A>",
                new Result(@"(a*|b*)\k<1>", groupNames: new[] {"A"})
            ),
            new TestCase(
                @"(?P<Ab>a*|b*)\k<Ab>",
                new Result(@"(a*|b*)\k<1>", groupNames: new[] {"Ab"})
            ),
            new TestCase(
                @"(?P<Ab129_>a*|b*)\k<Ab129_>",
                new Result(@"(a*|b*)\k<1>", groupNames: new[] {"Ab129_"})
            ),
            new TestCase(
                @"(?P<ab129_>a*|b*)\k<ab129_>",
                new Result(@"(a*|b*)\k<1>", groupNames: new[] {"ab129_"})
            ),
            new TestCase(
                @"(?P<_b129_>a*|b*)\k<_b129_>",
                new Result(@"(a*|b*)\k<1>", groupNames: new[] {"_b129_"})
            ),
            new TestCase(
                @"\k<x1>*\k<x2>*\k<x3>*(?P<x1>a)(?P<x2>b)(?P<x3>c)\k<x1>*\k<x2>*\k<x3>*",
                new Result(@"\k<1>*\k<2>*\k<3>*(a)(b)(c)\k<1>*\k<2>*\k<3>*", groupNames: new[] {"x1", "x2", "x3"})
            ),
            new TestCase(
                @"\1+\2+\3+(?P<x1>a)\3?(?P<x2>b)\2?(?P<x3>c)\1?\k<x1>*\k<x2>*\k<x3>*\3+\2+\1+",
                new Result(
                    @"\k<1>+\k<2>+\k<3>+(a)\k<3>?(b)\k<2>?(c)\k<1>?\k<1>*\k<2>*\k<3>*\k<3>+\k<2>+\k<1>+",
                    groupNames: new[] {"x1", "x2", "x3"}
                )
            ),
            new TestCase(
                @"\1+\2+\3+(?P<x1>a)\3?(b)\2?(?P<x3>c)\1?\k<x1>*\k<x3>*\3+\2+\1+",
                new Result(
                    @"\k<1>+\k<2>+\k<3>+(a)\k<3>?(b)\k<2>?(c)\k<1>?\k<1>*\k<3>*\k<3>+\k<2>+\k<1>+",
                    new[] {"x1", null, "x3"}
                )
            ),
            new TestCase(
                String.Concat(Enumerable.Range(1, 999).Select(i => $"\\{i}\\k<a{i}>(?P<a{i}>xyz)+\\{i}\\k<a{i}>")),
                new Result(
                    String.Concat(Enumerable.Range(1, 999).Select(i => $"\\k<{i}>\\k<{i}>(xyz)+\\k<{i}>\\k<{i}>")),
                    groupNames: Enumerable.Range(1, 999).Select(i => "a" + i).ToArray()
                )
            )
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withNamedBackreferences))]
        public void transpileTest_withNamedBackreferences(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_withExtendedModeWhiteSpaceAndComments = TupleHelper.toArrays(
            new TestCase("    ", "!x", null),
            new TestCase("    ", "x", ""),

            new TestCase(" \n\r\f\t\v", "!x", null),
            new TestCase(" \n\r\f\t\v ", "x", ""),

            new TestCase("  a  b  c  ", "!x", null),
            new TestCase("  a  b  c  ", "x", "abc"),

            new TestCase("  a *  b *? c +\nd +? e ? f\t?? g  {1} \r h \n {3,4} ", "!x", null),
            new TestCase("  a *  b *? c +\nd +? e ? f\t?? g  {1} \r h \n {3,4} ", "x", "a*b*?c+d+?e?f??g{1}h{3,4}"),

            new TestCase("  [0-9]* \\. [0-9] * \n\\. [0-9A-Za-z] {5,} ", "!x", null),
            new TestCase("  [0-9]* \\. [0-9] * \n\\. [0-9A-Za-z] {5,} ", "x", @"[0-9]*\.[0-9]*\.[0-9A-Za-z]{5,}"),

            new TestCase(" ( a  ( b  ( c ) d ) e ) f ", "!x", new Result(null, 3)),
            new TestCase(" ( a  ( b  ( c ) d ) e ) f ", "x", new Result("(a(b(c)d)e)f", 3)),

            new TestCase(" (?: a  (?: b ( c ) d ) e ) f ", "!x", new Result(null, 1)),
            new TestCase(" (?: a  (?: b ( c ) d ) e ) f ", "x", new Result("(?:a(?:b(c)d)e)f", 1)),

            new TestCase(" (?= a ) | (?! a ) | (?<= a ) | (?<! a ) ", "!x", null),
            new TestCase(" (?= a ) | (?! a ) | (?<= a ) | (?<! a ) ", "x", "(?=a)|(?!a)|(?<=a)|(?<!a)"),

            new TestCase(
                " (?P<x> a  (?P<y> b  (?P<z> c ) d ) e ) f ",
                "!x",
                new Result(" ( a  ( b  ( c ) d ) e ) f ", groupNames: new[] { "x", "y", "z" })
            ),
            new TestCase(
                " (?P<x> a  (?P<y> b  (?P<z> c ) d ) e ) f ",
                "x",
                new Result("(a(b(c)d)e)f", groupNames: new[] {"x", "y", "z"})
            ),

            new TestCase("\\ \\\n \\\r \\\t \\\f \\\v ", "!x", " \n \r \t \f \v "),
            new TestCase("\\ \\\n \\\r \\\t \\\f \\\v ", "x", " \n\r\t\f\v"),

            new TestCase("[ \n\r\f\t\v]", "x", "[ \n\r\f\t\v]"),

            new TestCase(@"abc\ * d", "!x", "abc * d"),
            new TestCase(@"abc\ * d", "x", "abc *d"),

            new TestCase(@"abc[ ]* d", "x", "abc[ ]*d"),

            new TestCase("#", "!x", "#"),
            new TestCase("#", "x", ""),

            new TestCase("abcd#", "!x", "abcd#"),
            new TestCase("abcd#", "x", "abcd"),

            new TestCase("abcd #", "!x", "abcd #"),
            new TestCase("abcd #", "x", "abcd"),

            new TestCase("abcd#\r", "!x", "abcd#\r"),
            new TestCase("abcd#\r", "x", "abcd"),
            new TestCase("abcd#\n", "!x", "abcd#\n"),
            new TestCase("abcd#\n", "x", "abcd"),
            new TestCase("abcd#\r\ne", "!x", "abcd#\r\ne"),
            new TestCase("abcd#\r\ne", "x", "abcde"),
            new TestCase("abcd\u2028efg", "x", "abcd\u2028efg"),
            new TestCase("abcd\u2029efg", "x", "abcd\u2029efg"),
            new TestCase("abcd#\u2028efg", "x", "abcdefg"),
            new TestCase("abcd#\u2029efg", "x", "abcdefg"),

            new TestCase("a #--- \r  b", "x", "ab"),
            new TestCase("a #--- \n  b", "x", "ab"),
            new TestCase("a [#bc] d", "x", "a[#bc]d"),
            new TestCase("a (  # ___ \n bc # ___ \n (d*) # *? \n ) #123 \r+", "x", new Result("a(bc(d*))+", 2)),

            new TestCase("abcd\\#ef#gh\nij\\#k", "!x", "abcd#ef#gh\nij#k"),
            new TestCase("abcd\\#ef#gh\nij\\#k", "x", "abcd#efij#k"),

            new TestCase("ab#(cd)\nef(gh)#(ij)", "!x", new Result("ab#(cd)\nef(gh)#(ij)", 3)),
            new TestCase("ab#(cd)\nef(gh)#(ij)", "x", new Result("abef(gh)", 1)),
            new TestCase("ab#(?P<_x>cd)\nef(?P<_y>gh)#(?P<_z>ij)", "!x", new Result("ab#(cd)\nef(gh)#(ij)", groupNames: new[] {"_x", "_y", "_z"})),
            new TestCase("ab#(?P<_x>cd)\nef(?P<_y>gh)#(?P<_z>ij)", "x", new Result("abef(gh)", groupNames: new[] {"_y"})),

            new TestCase(@"abc{ }def", "x", @"abc\{\}def"),
            new TestCase(@"abc{ 1 }def", "x", @"abc\{1\}def"),
            new TestCase(@"abc{1 }def", "x", @"abc\{1\}def"),
            new TestCase(@"abc{ 1}def", "x", @"abc\{1\}def"),
            new TestCase(@"abc{1, }def", "x", @"abc\{1,\}def"),
            new TestCase(@"abc{ 1,}def", "x", @"abc\{1,\}def"),
            new TestCase(@"abc{1, 4}def", "x",  @"abc\{1,4\}def"),
            new TestCase(@"abc{1,4 }def", "x", @"abc\{1,4\}def"),
            new TestCase(@"abc{ 1,4}def", "x", @"abc\{1,4\}def"),
            new TestCase(@"abc{ 1,4 }def", "x", @"abc\{1,4\}def"),
            new TestCase(@"abc{ 1, 4 }def", "x", @"abc\{1,4\}def"),
            new TestCase(@"abc{ 1,\ 4 }def", "x", @"abc\{1, 4\}def")
        );

        [Theory]
        [MemberData(nameof(transpileTestData_withExtendedModeWhiteSpaceAndComments))]
        public void transpileTest_withExtendedModeWhiteSpaceAndComments(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_invalidPattern = TupleHelper.toArrays(
            new TestCase(@"\", Result.syntaxError),
            new TestCase(@"\\\", Result.syntaxError),
            new TestCase(@"abcd\d\", Result.syntaxError),
            new TestCase(@"abcd[", Result.syntaxError),
            new TestCase(@"abcd[\", Result.syntaxError),
            new TestCase(@"abcd[^", Result.syntaxError),
            new TestCase(@"abcd(", Result.syntaxError),
            new TestCase(@"abcd(\", Result.syntaxError),
            new TestCase(@"abcd[pq][12\", Result.syntaxError),
            new TestCase(@"abcd[p-", Result.syntaxError),
            new TestCase(@"abcd[p-\", Result.syntaxError),
            new TestCase(@"abc\x", Result.syntaxError),
            new TestCase(@"abc\x1", Result.syntaxError),
            new TestCase(@"abc\u", Result.syntaxError),
            new TestCase(@"abc\u1", Result.syntaxError),
            new TestCase(@"abc\u12", Result.syntaxError),
            new TestCase(@"abc\u123", Result.syntaxError),
            new TestCase(@"abc\u1234\u", Result.syntaxError),
            new TestCase(@"*", Result.syntaxError),
            new TestCase(@"?", Result.syntaxError),
            new TestCase(@"+", Result.syntaxError),
            new TestCase(@"a**", Result.syntaxError),
            new TestCase(@"a++", Result.syntaxError),
            new TestCase(@"a*??", Result.syntaxError),
            new TestCase(@"(a*b)**", Result.syntaxError),
            new TestCase(@"[a-z]+*b", Result.syntaxError),
            new TestCase(@"(*abc)", Result.syntaxError),
            new TestCase(@"(?:*abc)", Result.syntaxError),
            new TestCase(@"(?=*abc)", Result.syntaxError),
            new TestCase(@"(?!+abc)", Result.syntaxError),
            new TestCase(@"(?<!?abc)", Result.syntaxError),
            new TestCase(@"(?<=+abc)", Result.syntaxError),
            new TestCase(@"(", Result.syntaxError),
            new TestCase(@")", Result.syntaxError),
            new TestCase(@"(abc(d)", Result.syntaxError),
            new TestCase(@"(abc(d)))", Result.syntaxError),
            new TestCase(@"(?)", Result.syntaxError),
            new TestCase(@"(?<)", Result.syntaxError),
            new TestCase(@"abc(?:", Result.syntaxError),
            new TestCase(@"abc(?<", Result.syntaxError),
            new TestCase(@"abc(?<=", Result.syntaxError),
            new TestCase(@"abc(?==", Result.syntaxError),
            new TestCase(@"abc(?=!", Result.syntaxError),
            new TestCase(@"abc(?*", Result.syntaxError),
            new TestCase(@"abc(?<:", Result.syntaxError),
            new TestCase(@"abc(?<abc>pq)", Result.syntaxError),
            new TestCase(@"abc(?>abc)", Result.syntaxError),
            new TestCase(@"abc(?(x)def)", Result.syntaxError),
            new TestCase(@"abc(?P<x>def)(?(x)ghi)", Result.syntaxError),
            new TestCase(@"(?P<abc", Result.syntaxError),
            new TestCase(@"(?P<abc)*", Result.syntaxError),
            new TestCase(@"(?p<abc>123)*", Result.syntaxError),
            new TestCase(@"(?P<>abc)", Result.syntaxError),
            new TestCase(@"(?P<1>abc)", Result.syntaxError),
            new TestCase(@"(?P<a-b>abc)", Result.syntaxError),
            new TestCase(@"(?abcd)", Result.syntaxError),
            new TestCase(@"(?;abcd)", Result.syntaxError),
            new TestCase(@"(? :a)", Result.syntaxError),
            new TestCase(@"(? = a)", Result.syntaxError),
            new TestCase(@"(?< =a)", Result.syntaxError),
            new TestCase(@"(? <!a)", Result.syntaxError),
            new TestCase(@"(?P <a> b)", Result.syntaxError),
            new TestCase(@"(? P<a> b)", Result.syntaxError),
            new TestCase(@"(?P< a >b)", Result.syntaxError),
            new TestCase(@"abc(?&'a'abcd)", Result.syntaxError),
            new TestCase(@"abc(?P'a'abcd)", Result.syntaxError),
            new TestCase(@"abc(?P", Result.syntaxError),
            new TestCase(@"abc(?P<", Result.syntaxError),
            new TestCase(@"abc(?P<a)", Result.syntaxError),
            new TestCase(@"abc(?P<a>", Result.syntaxError),
            new TestCase(@"abc(?P<>)", Result.syntaxError),
            new TestCase(@"abc(?P<a>d)(?P<a>e)", Result.syntaxError),
            new TestCase(@"abc(?P<a>d)(?P<b>e)(?P<a>f)", Result.syntaxError),
            new TestCase(@"abc\k", Result.syntaxError),
            new TestCase(@"abc\k1", Result.syntaxError),
            new TestCase(@"abc\k<abc", Result.syntaxError),
            new TestCase(@"abc\k<>", Result.syntaxError),
            new TestCase(@"abc\k<a-b>", Result.syntaxError),
            new TestCase(@"abc\k<a>", Result.syntaxError),
            new TestCase(@"abc\k<1>", Result.syntaxError),
            new TestCase(@"abc(?P<a>d)(?P<b>e)\k<c>", Result.syntaxError),
            new TestCase(@"abc\k<c>(?P<a>d)(?P<b>e)", Result.syntaxError),
            new TestCase(@"{1}", Result.syntaxError),
            new TestCase(@"{1,}", Result.syntaxError),
            new TestCase(@"{1,2}", Result.syntaxError),
            new TestCase(@"abc*{1}", Result.syntaxError),
            new TestCase(@"abc{1}{2}", Result.syntaxError),
            new TestCase(@"abc{2,1}", Result.syntaxError),
            new TestCase(@"abc{1000,999}", Result.syntaxError),
            new TestCase(@"abc{2147483648}", Result.syntaxError),
            new TestCase(@"abc{2147483650}", Result.syntaxError),
            new TestCase(@"abc{1,2147483648}", Result.syntaxError),
            new TestCase(@"abc{1,2147483650}", Result.syntaxError),
            new TestCase(@"abc{1}??", Result.syntaxError),
            new TestCase(@"abc{1,}??", Result.syntaxError),
            new TestCase(@"abc{1,2}??", Result.syntaxError),
            new TestCase(@"[z-a]", Result.syntaxError),
            new TestCase(@"[\x13-\x12]", Result.syntaxError),
            new TestCase(@"[\u3FAD-\u3FAC]", Result.syntaxError),
            new TestCase(@"[\u076B-\u0769]", Result.syntaxError),
            new TestCase(@"[9-\x38]", Result.syntaxError),
            new TestCase(@"[\x39-8]", Result.syntaxError),
            new TestCase(@"[✔-\u2713]", Result.syntaxError),
            new TestCase(@"[\u2715-✔]", Result.syntaxError),
            new TestCase(@"[ab-[c]]", Result.syntaxError),
            new TestCase(@"abc\u123G", Result.syntaxError),
            new TestCase(@"abc\u123h", Result.syntaxError),
            new TestCase(@"abc\xag", Result.syntaxError),
            new TestCase(@"abc\xAG", Result.syntaxError),
            new TestCase(@"abc\xga", Result.syntaxError),
            new TestCase(@"abc\xGA", Result.syntaxError),
            new TestCase(@"abc\x4:", Result.syntaxError),
            new TestCase(@"abc\x/4", Result.syntaxError),
            new TestCase(@"abc\x4@", Result.syntaxError),
            new TestCase(@"abc\x`4", Result.syntaxError),
            new TestCase(@"abc\u4g50", Result.syntaxError),
            new TestCase(@"abc\u4G50", Result.syntaxError),
            new TestCase(@"abc\u4:50", Result.syntaxError),
            new TestCase(@"abc\u45/0", Result.syntaxError),
            new TestCase(@"abc\u@45b", Result.syntaxError),
            new TestCase(@"abc\u45b`", Result.syntaxError),
            new TestCase(@"abc\u45g0", Result.syntaxError),
            new TestCase(@"abc\ug450", Result.syntaxError)
        );

        [Theory]
        [MemberData(nameof(transpileTestData_invalidPattern))]
        public void transpileTest_invalidPattern(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_invalidPattern_extendedModeOnly = TupleHelper.toArrays(
            new TestCase("  *  ", "!x", null),
            new TestCase("  *  ", "x", Result.syntaxError),

            new TestCase("  +  ", "!x", null),
            new TestCase("  +  ", "x", Result.syntaxError),

            new TestCase("a* *", "!x", null),
            new TestCase("a* *", "x", Result.syntaxError),

            new TestCase("a* ?", "!x", null),
            new TestCase("a* ?", "x", Result.syntaxError),

            new TestCase("a*\n\t*", "!x", null),
            new TestCase("a*\n\t*", "x", Result.syntaxError),

            new TestCase("a*#pq\n\t*", "!x", null),
            new TestCase("a*#pq\n\t*", "x", Result.syntaxError),

            new TestCase("a {1,2}  *", "!x", null),
            new TestCase("a {1,2}  *", "x", Result.syntaxError),

            new TestCase("( * )", "!x", new Result(null, 1)),
            new TestCase("( * )", "x", Result.syntaxError),

            new TestCase("( ?)", "!x", new Result(null, 1)),
            new TestCase("( ?)", "x", Result.syntaxError),

            new TestCase("(?: *)", "!x", null),
            new TestCase("(?: *)", "x", Result.syntaxError),

            new TestCase("( ?=a)", "!x", new Result(null, 1)),
            new TestCase("( ?=a)", "x", Result.syntaxError),

            new TestCase("( ?!a)", "!x", new Result(null, 1)),
            new TestCase("( ?!a)", "x", Result.syntaxError),

            new TestCase("( ?<=a)", "!x", new Result(null, 1)),
            new TestCase("( ?<=a)", "x", Result.syntaxError),

            new TestCase("( ?<!a)", "!x", new Result(null, 1)),
            new TestCase("( ?<!a)", "x", Result.syntaxError),

            new TestCase("( ?P<x>a)", "!x", new Result(null, 1)),
            new TestCase("( ?P<x>a)", "x", Result.syntaxError)
        );

        [Theory]
        [MemberData(nameof(transpileTestData_invalidPattern_extendedModeOnly))]
        public void transpileTest_invalidPattern_extendedModeOnly(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_invalidPattern_nonExtendedModeOnly = TupleHelper.toArrays(
            new TestCase("a*#(b", "!x", Result.syntaxError),
            new TestCase("a*#(b", "x", "a*"),

            new TestCase("a*#[q", "!x", Result.syntaxError),
            new TestCase("a*#[q", "x", "a*"),

            new TestCase("a*#[q-p]", "!x", Result.syntaxError),
            new TestCase("a*#[q-p]", "x", "a*"),

            new TestCase("a*#*+", "!x", Result.syntaxError),
            new TestCase("a*#*+", "x", "a*"),

            new TestCase("a*#b{10,8}", "!x", Result.syntaxError),
            new TestCase("a*#b{10,8}", "x", "a*"),

            new TestCase("a (  # ) \n bc # ) \n (d*) # *? \n ) #123 \r+", "!x", Result.syntaxError),
            new TestCase("a (  # ) \n bc # ) \n (d*) # *? \n ) #123 \r+", "x", new Result("a(bc(d*))+", 2))
        );

        [Theory]
        [MemberData(nameof(transpileTestData_invalidPattern_nonExtendedModeOnly))]
        public void transpileTest_invalidPattern_nonExtendedModeOnly(TestCase testCase) {
            verifyTestCase(testCase);
        }

        public static IEnumerable<object[]> transpileTestData_captureGroupLimitExceeded = TupleHelper.toArrays(
            new TestCase(String.Concat(Enumerable.Repeat("()", 1000)), Result.syntaxError),
            new TestCase(String.Concat(Enumerable.Repeat("(a)", 1000)), Result.syntaxError),
            new TestCase(String.Concat(Enumerable.Repeat("()", 2000)), Result.syntaxError),
            new TestCase(String.Concat(Enumerable.Repeat("(a)", 2000)), Result.syntaxError),
            new TestCase(String.Concat(Enumerable.Repeat("abc(a*)(?:a)", 1000)), Result.syntaxError),
            new TestCase(String.Concat(Enumerable.Repeat("abc(a*)(?:a)", 2000)), Result.syntaxError),
            new TestCase(String.Concat(Enumerable.Range(1, 1000).Select(i => $"(?P<a{i}>xyz)")), Result.syntaxError),
            new TestCase(String.Concat(Enumerable.Range(1, 2000).Select(i => $"(?P<a{i}>xyz)")), Result.syntaxError),
            new TestCase(String.Concat(Enumerable.Range(1, 500).Select(i => $"(?P<a{i}>xyz)__(xyz)")), Result.syntaxError),
            new TestCase(String.Concat(Enumerable.Range(1, 500).Select(i => $"(xyz)__(?P<a{i}>xyz)")), Result.syntaxError)
        );

        [Theory]
        [MemberData(nameof(transpileTestData_captureGroupLimitExceeded))]
        public void transpileTest_captureGroupLimitExceeded(TestCase testCase) {
            verifyTestCase(testCase);
        }

        private void verifyTestCase(TestCase testCase) {
            bool? multilineFlag = null;
            bool? extendedFlag = null;
            bool? dotallFlag = null;

            int indexOfFlagM = testCase.flags.IndexOf('m');
            int indexOfFlagS = testCase.flags.IndexOf('s');
            int indexOfFlagX = testCase.flags.IndexOf('x');

            if (indexOfFlagM != -1)
                multilineFlag = indexOfFlagM == 0 || testCase.flags[indexOfFlagM - 1] != '!';

            if (indexOfFlagS != -1)
                dotallFlag = indexOfFlagS == 0 || testCase.flags[indexOfFlagS - 1] != '!';

            if (indexOfFlagX != -1)
                extendedFlag = indexOfFlagX == 0 || testCase.flags[indexOfFlagX - 1] != '!';

            var transpiler = new RegexTranspiler();

            expand(multilineFlag, flagM => expand(dotallFlag, flagS => expand(extendedFlag, flagX => {
                if (testCase.result.isSyntaxError) {
                    AssertHelper.throwsErrorWithCode(
                        ErrorCode.MARIANA__REGEXP_PARSE_ERROR,
                        () => transpiler.transpile(testCase.pattern, flagM, flagS, flagX)
                    );
                    return;
                }

                transpiler.transpile(testCase.pattern, flagM, flagS, flagX);

                Assert.Equal(testCase.result.expectedPattern, transpiler.transpiledPattern);
                Assert.Equal(testCase.result.groupNames.Length, transpiler.groupCount);

                if (testCase.result.groupNames.All(x => x == null))
                    Assert.Null(transpiler.getGroupNames());
                else
                    Assert.Equal<string>(testCase.result.groupNames, transpiler.getGroupNames());

                // Ensure that constructing a Regex from the transpiled pattern does not throw.
                new Regex(testCase.result.expectedPattern, RegexOptions.ECMAScript | RegexOptions.CultureInvariant);
            })));

            void expand(bool? condition, Action<bool> action) {
                if (condition == null || condition.Value)
                    action(true);
                if (condition == null || !condition.Value)
                    action(false);
            }
        }

    }

}
