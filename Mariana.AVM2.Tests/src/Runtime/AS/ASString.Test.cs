using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class ASStringTest {

        private const int MAXINT = Int32.MaxValue;
        private const double INFINITY = Double.PositiveInfinity;

        /// <summary>
        /// This is used to prevent test runner errors that happen when inputs contain certain Unicode characters.
        /// </summary>
        public readonly struct StringWrapper {
            public readonly string instance;
            public StringWrapper(string str) => instance = str;
            public static implicit operator StringWrapper(string str) => new StringWrapper(str);

            public override string ToString() {
                var sb = new StringBuilder();
                sb.Append('"');

                for (int i = 0; i < instance.Length; i++) {
                    char ch = instance[i];
                    if (ch == '"' || ch == '\\') {
                        sb.Append('\\').Append(ch);
                    }
                    else if (ch <= 0x7F && !Char.IsControl(ch)) {
                        sb.Append(ch);
                    }
                    else {
                        sb.AppendFormat("\\u{0:X4}", (int)ch);
                    }
                }

                sb.Append('"');
                return sb.ToString();
            }
        }

        private static void assertThrowsNullReferenceError(Func<object> testCode) {
            Exception caughtException = null;
            try {
                testCode();
            }
            catch (Exception ex) {
                caughtException = ex;
            }

            bool isNullReferenceError =
                caughtException is NullReferenceException
                || (caughtException is AVM2Exception avm2ex
                    && avm2ex.thrownValue.value is ASTypeError err
                    && err.errorID == (int)ErrorCode.NULL_REFERENCE_ERROR);

            Assert.True(isNullReferenceError, "Expected a null reference error to be thrown.");
        }

        private static IEnumerable<double> generateEquivalentIndices(
            int index, int length, bool negativeIsZero = false, bool nanIsInfinity = false)
        {
            if (index == -1)
                return new[] {-1.0, -1.5, -10000000.0, -INFINITY};

            var indices = new HashSet<double>();

            indices.Add(index);
            indices.Add(index + 0.3);
            indices.Add(index + 0.7);

            if (index == length) {
                indices.Add(index + 1.0);
                indices.Add(index + 10000000.0);
                indices.Add(index + 4294967295.0);
                indices.Add(INFINITY);

                if (nanIsInfinity)
                    indices.Add(Double.NaN);
            }

            if (index == 0) {
                indices.Add(-0.3);
                indices.Add(-0.9);
                if (!nanIsInfinity)
                    indices.Add(Double.NaN);
            }

            if (negativeIsZero && index == 0) {
                indices.Add(-1.0);
                indices.Add(-10000000.0);
                indices.Add(-4294967296.0);
                indices.Add(-INFINITY);
            }

            return indices;
        }

        public static IEnumerable<object[]> stringClassRuntimeInvokeConstructTest_data() {
            ASObject obj1 = new ConvertibleMockObject(stringValue: "");
            ASObject obj2 = new ConvertibleMockObject(stringValue: "hello");

            return TupleHelper.toArrays(
                (Array.Empty<ASAny>(), ""),
                (new ASAny[] {default}, "undefined"),
                (new ASAny[] {ASAny.@null}, "null"),
                (new ASAny[] {obj1}, ""),
                (new ASAny[] {obj2}, "hello"),

                (new ASAny[] {default, obj1}, "undefined"),
                (new ASAny[] {ASAny.@null, obj1}, "null"),
                (new ASAny[] {obj1, obj2}, ""),
                (new ASAny[] {obj2, obj1}, "hello"),
                (new ASAny[] {obj1, obj2, default, obj1}, "")
            );
        }

        [Theory]
        [MemberData(nameof(stringClassRuntimeInvokeConstructTest_data))]
        public void stringClassRuntimeInvokeConstructTest(ASAny[] args, string expected) {
            Class klass = Class.fromType(typeof(string));

            check(klass.invoke(args));
            check(klass.construct(args));

            void check(ASAny result) {
                Assert.IsType<ASString>(result.value);
                Assert.Same(klass, result.AS_class);
                Assert.Equal(expected, ASObject.AS_coerceString(result.value));
            }
        }

        public static IEnumerable<object[]> toString_valueOf_testData = TupleHelper.toArrays<StringWrapper>(
            null, "", "abcde", "abc\0def\uFFFF", "\u0301a\ud800\udc00", "\udfff\ud800"
        );

        [Theory]
        [MemberData(nameof(toString_valueOf_testData))]
        public void toStringMethodTest(StringWrapper value) {
            if (value.instance == null) {
                assertThrowsNullReferenceError(() => ASString.toString(value.instance));
            }
            else {
                Assert.Equal(value, ASString.toString(value.instance));
                Assert.Equal(value, ((ASString)(ASObject)value.instance).AS_toString());
            }
        }

        [Theory]
        [MemberData(nameof(toString_valueOf_testData))]
        public void valueOfMethodTest(StringWrapper value) {
            if (value.instance == null) {
                assertThrowsNullReferenceError(() => ASString.valueOf(value.instance));
            }
            else {
                Assert.Equal(value, ASString.valueOf(value.instance));
                Assert.Equal(value, ((ASString)(ASObject)value.instance).valueOf());
            }
        }

        public static IEnumerable<object[]> lengthPropertyTest_data = TupleHelper.toArrays<StringWrapper, int>(
            ("", 0),
            ("abcde", 5),
            ("abc\0def\uFFFF", 8),
            ("\u0301a\ud800\udc00", 4),
            ("\udfff\ud800", 2)
        );

        [Theory]
        [MemberData(nameof(lengthPropertyTest_data))]
        public void lengthPropertyTest(StringWrapper value, int expectedLength) {
            Assert.Equal(expectedLength, ((ASString)(ASObject)value.instance).length);
        }

        public static IEnumerable<object[]> fromCharCodeMethodTest_data = TupleHelper.toArrays<ASAny[], StringWrapper>(
            (Array.Empty<ASAny>(), ""),
            (new ASAny[] {65}, "A"),
            (new ASAny[] {0x83ed}, "\u83ed"),
            (new ASAny[] {97, 98, 99}, "abc"),
            (new ASAny[] {48, 49, 50, 51, 52, 53, 54, 55, 56, 57}, "0123456789"),
            (new ASAny[] {12, 0, 5, 9, 19, 29, 34, 0, 87, 38}, "\u000c\0\u0005\u0009\u0013\u001d\u0022\0\u0057\u0026"),
            (new ASAny[] {-1, 65535, 65536, 65537, -2147483646, 4294967295, 9007199254740989.0, INFINITY, Double.NaN}, "\uffff\uffff\0\u0001\u0002\uffff\ufffd\0\0"),
            (new ASAny[] {65.1, 67.9, 97.8, 98.9, 100.4}, "ACabd"),
            (new ASAny[] {"101", "102" }, "ef"),
            (new ASAny[] {0xD800, 0xDC01, 0xDBEE, 0xDF00, 0xDFEE, 0xDD99, 0xD7FF, 0xDC00}, "\ud800\udc01\udbee\udf00\udfee\udd99\ud7ff\udc00")
        );

        [Theory]
        [MemberData(nameof(fromCharCodeMethodTest_data))]
        public void fromCharCodeMethodTest(ASAny[] args, StringWrapper expected) {
            Assert.Equal(expected.instance, ASString.fromCharCode(new RestParam(args)));
        }

        public static IEnumerable<object[]> charAt_charCodeAt_testData = TupleHelper.toArrays<StringWrapper, int[]>(
            (null, new[] {-1, 0, 1, MAXINT - 1, MAXINT}),
            ("", new[] {-1, 0}),
            ("a", new[] {-1, 0, 1}),
            ("\0", new[] {-1, 0, 1}),
            ("hello", new[] {-1, 0, 1, 2, 3, 4, 5}),
            (new string('a', 200), new[] {0, 1, 50, 100, 190, 199, 200}),
            ("\0\u0003\u000a\u0015\u007f\0\u009d\u0344\u2f3d\u49f6\uba55", new[] {-1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10}),
            ("\ud800\udfff\uda84\uddbc\udf09\udf11\uf899\ud584\ude7b\uffff", new[] {-1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9})
        );

        public static IEnumerable<object[]> charAt_charCodeAt_integerIndex_testData = TupleHelper.toArrays<StringWrapper>(
            null,
            "",
            "a",
            "\0",
            "hello",
            new string('a', 200),
            "\0\u0003\u000a\u0015\u007f\0\u009d\u0344\u2f3d\u49f6\uba55",
            "\ud800\udfff\uda84\uddbc\udf09\udf11\uf899\ud584\ude7b\uffff"
        );

        [Theory]
        [MemberData(nameof(charAt_charCodeAt_testData))]
        public void charAtMethodTest(StringWrapper str, int[] indices) {
            ASString boxedStr = (ASString)(ASObject)str.instance;
            int length = (str.instance != null) ? str.instance.Length : MAXINT;

            for (int i = 0; i < indices.Length; i++) {
                int index = indices[i];
                string expected =
                    (str.instance == null || index == -1 || index == str.instance.Length) ? "" : str.instance[index].ToString();

                foreach (double dIndex in generateEquivalentIndices(index, length)) {
                    if (str.instance == null) {
                        assertThrowsNullReferenceError(() => ASString.charAt(str.instance, dIndex));
                    }
                    else {
                        Assert.Equal(expected, ASString.charAt(str.instance, dIndex));
                        Assert.Equal(expected, boxedStr.charAt(dIndex));
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(charAt_charCodeAt_integerIndex_testData))]
        public void charAtMethodTest_integerIndex(StringWrapper str) {
            if (str.instance == null) {
                int[] indices = {0, 1, -1, Int32.MinValue, MAXINT};
                for (int i = 0; i < indices.Length; i++)
                    assertThrowsNullReferenceError(() => ASString.charAt(str.instance, indices[i]));
            }
            else {
                Assert.Equal("", ASString.charAt(str.instance, -1));
                Assert.Equal("", ASString.charAt(str.instance, str.instance.Length));
                Assert.Equal("", ASString.charAt(str.instance, Int32.MinValue));
                Assert.Equal("", ASString.charAt(str.instance, MAXINT));

                for (int i = 0; i < str.instance.Length; i++)
                    Assert.Equal(str.instance[i].ToString(), ASString.charAt(str.instance, i));
            }
        }

        [Theory]
        [MemberData(nameof(charAt_charCodeAt_testData))]
        public void charCodeAtMethodTest(StringWrapper str, int[] indices) {
            ASString boxedStr = (ASString)(ASObject)str.instance;
            int length = (str.instance != null) ? str.instance.Length : MAXINT;

            for (int i = 0; i < indices.Length; i++) {
                int index = indices[i];
                double expected = (str.instance == null || index == -1 || index == str.instance.Length)
                    ? Double.NaN
                    : (double)str.instance[index];

                foreach (double dIndex in generateEquivalentIndices(index, length)) {
                    if (str.instance == null) {
                        assertThrowsNullReferenceError(() => ASString.charAt(str.instance, dIndex));
                    }
                    else {
                        AssertHelper.floatIdentical(expected, ASString.charCodeAt(str.instance, dIndex));
                        AssertHelper.floatIdentical(expected, boxedStr.charCodeAt(dIndex));
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(charAt_charCodeAt_integerIndex_testData))]
        public void charCodeAtMethodTest_integerIndex(StringWrapper str) {
            if (str.instance == null) {
                int[] indices = {0, 1, -1, Int32.MinValue, MAXINT};
                for (int i = 0; i < indices.Length; i++)
                    assertThrowsNullReferenceError(() => ASString.charAt(str.instance, indices[i]));
            }
            else {
                AssertHelper.floatIdentical(Double.NaN, ASString.charCodeAt(str.instance, -1));
                AssertHelper.floatIdentical(Double.NaN, ASString.charCodeAt(str.instance, str.instance.Length));
                AssertHelper.floatIdentical(Double.NaN, ASString.charCodeAt(str.instance, Int32.MinValue));
                AssertHelper.floatIdentical(Double.NaN, ASString.charCodeAt(str.instance, MAXINT));

                for (int i = 0; i < str.instance.Length; i++)
                    AssertHelper.floatIdentical(str.instance[i], ASString.charCodeAt(str.instance, i));
            }
        }

        public static IEnumerable<object[]> concatMethodTest_data = TupleHelper.toArrays<StringWrapper, ASAny[], StringWrapper>(
            (null, Array.Empty<ASAny>(), null),
            (null, new ASAny[] {"", "abc"}, null),

            ("", Array.Empty<ASAny>(), ""),
            ("", new ASAny[] {""}, ""),
            ("", new ASAny[] {"", "", "", "", "", "", "", "", "", ""}, ""),
            ("", new ASAny[] {"abc"}, "abc"),
            ("", new ASAny[] {"abc", "def", "ghi"}, "abcdefghi"),
            ("", new ASAny[] {"", "", "abc", "", "def", "", "", "ghi", ""}, "abcdefghi"),

            ("hello", Array.Empty<ASAny>(), "hello"),
            ("hello", new ASAny[] {"abc"}, "helloabc"),
            ("abc", new ASAny[] {""}, "abc"),
            ("abcdefghijkl", new ASAny[] {"", "", "", "", "", "", "", "", "", ""}, "abcdefghijkl"),
            ("abc", new ASAny[] {"1", "2", "34"}, "abc1234"),
            ("abc", new ASAny[] {"", "1", "", "", "2", "", "34", ""}, "abc1234"),

            ("qwerty", new ASAny[] {0, 1, "abc", 20000, "DEF", new ConvertibleMockObject(stringValue: "..."), 99}, "qwerty01abc20000DEF...99"),
            ("", new ASAny[] {ASAny.undefined}, "undefined"),
            ("", new ASAny[] {ASAny.@null}, "null"),
            ("abc", new ASAny[] {123, ASAny.@null, 456, ASAny.undefined, ASAny.@null, ASAny.undefined, Double.NaN, "!"}, "abc123null456undefinednullundefinedNaN!"),

            ("\ud823", new ASAny[] {"\udc84"}, "\ud823\udc84"),
            ("\ud903\udd4a\ud804", new ASAny[] {"\udff3\ud819", "\ude90\udc00", "!\ud98d"}, "\ud903\udd4a\ud804\udff3\ud819\ude90\udc00!\ud98d")
        );

        [Theory]
        [MemberData(nameof(concatMethodTest_data))]
        public void concatMethodTest(StringWrapper str, ASAny[] args, StringWrapper expectedResult) {
            if (str.instance == null) {
                assertThrowsNullReferenceError(() => ASString.concat(str.instance, new RestParam(args)));
                return;
            }

            ASString boxedStr = (ASString)(ASObject)str.instance;

            Assert.Equal(expectedResult, ASString.concat(str.instance, new RestParam(args)));
            Assert.Equal(expectedResult, boxedStr.concat(new RestParam(args)));
        }

        public static IEnumerable<object[]> indexOfMethodTest_data = TupleHelper.toArrays<StringWrapper, StringWrapper, (int, int)[]>(
            (null, null, new[] {(0, -1), (1, -1), (MAXINT, -1)}),
            (null, "", new[] {(0, -1), (1, -1), (MAXINT, -1)}),
            (null, "abc", new[] {(0, -1), (1, -1), (MAXINT, -1)}),

            ("", null, new[] {(0, -1)}),
            ("", "", new[] {(0, 0)}),
            ("", "a", new[] {(0, -1)}),

            ("abcdefghiabcdefghiabcdefghi", null, new[] {(0, -1), (10, -1), (27, -1)}),
            ("abcdefghiabcdefghiabcdefghi", "", new[] {(0, 0), (1, 1), (10, 10), (27, 27)}),
            ("abcdefghiabcdefghiaxydezghi", "a", new[] {(0, 0), (1, 9), (9, 9), (10, 18), (18, 18), (19, -1), (27, -1)}),
            ("abcdefghiabcdefghiaxydezghi", "c", new[] {(0, 2), (1, 2), (2, 2), (3, 11), (11, 11), (12, -1), (19, -1), (27, -1)}),
            ("abcdefghiabcdefghiaxydezghi", "e", new[] {(0, 4), (4, 4), (5, 13), (14, 22), (23, -1), (27, -1)}),
            ("abcdefghiabcdefghiaxydezghi", "abc", new[] {(0, 0), (1, 9), (7, 9), (9, 9), (10, -1), (18, -1), (27, -1)}),
            ("abcdefghiabcdefghiaxydezghi", "de", new[] {(0, 3), (3, 3), (4, 12), (11, 12), (13, 21), (21, 21), (22, -1), (27, -1)}),
            ("abcdefghiabcdefghiaxydezghi", "defghi", new[] {(0, 3), (4, 12), (13, -1), (21, -1), (27, -1)}),
            ("abcdefghiabcdefghiaxydezghi", "abcdefghiabcdefghiaxydezghi", new[] {(0, 0), (1, -1), (27, -1)}),
            ("abcdefghiabcdefghiaxydezghi", "abcdefghiabcdefghiaxydezghij", new[] {(0, -1), (1, -1), (27, -1)}),
            ("abcdefghiabcdefghiaxydezghi", "A", new[] {(0, -1), (1, -1), (9, -1), (10, -1), (18, -1), (19, -1), (27, -1)}),

            ("abcabcabcabcabcabcabcabcabc", "abc", new[] {(0, 0), (1, 3), (13, 15), (23, 24), (25, -1)}),
            ("abcabcabcabcabcabcabcabcabc", "bcabc", new[] {(0, 1), (2, 4), (4, 4), (5, 7), (9, 10), (11, 13), (18, 19), (20, 22), (23, -1)}),
            ("abcabcabcabcabcabcabcabcabc", "abcabcabcabcabcabcabcabc", new[] {(0, 0), (3, 3), (4, -1), (6, -1)}),
            ("abcabcabcabcabcabcabcabcabc", "abcabcabcabcabcabcabcabd", new[] {(0, -1), (3, -1), (4, -1), (6, -1), (27, -1)}),

            ("hello", "HELLO", new[] {(0, -1)}),
            ("héllo", "héllo", new[] {(0, 0)}),
            ("hello", "héllo", new[] {(0, -1)}),
            ("he\u0301llo", "he\u0301llo", new[] {(0, 0)}),
            ("héllo", "he\u0301llo", new[] {(0, -1)}),
            ("he\u0301llo", "héllo", new[] {(0, -1)}),
            ("encyclopaedia", "encyclopædia", new[] {(0, -1)}),

            ("ABCD\ud800\udfff\udf00\ud8ff", "ABCD", new[] {(0, 0)}),
            ("ABCD\ud800\udfff\udf00\ud8ff\ud800", "\ud800", new[] {(0, 4), (5, 8), (9, -1)}),
            ("ABCD\ud800\udfff\udf00\ud8ff\ud800", "\ud800\udfff", new[] {(0, 4), (5, -1)}),
            ("ABCD\ud800\udfff\udf00\ud8ff\ud800", "\udf00", new[] {(0, 6)}),
            ("ABCD\ud800\udfff\udf00\ud8ff\ud800", "\udf00\ud8ff", new[] {(0, 6)}),
            ("ABCD\ud800\udfff\udf00\ud8ff\ud800", "\udf00\ud8ff\ud800", new[] {(0, 6)}),

            ("ABCDE\0FG", "E", new[] {(0, 4)}),
            ("ABCDE\0FG", "G", new[] {(0, 7)}),
            ("ABCDE\0FG", "E\0", new[] {(0, 4)}),
            ("ABCDE\0FG", "G\0", new[] {(0, -1)}),
            ("ABCDE\0FG", "E\0FG", new[] {(0, 4)}),
            ("ABCDE\0FG", "E\0FG\0", new[] {(0, -1)})
        );

        [Theory]
        [MemberData(nameof(indexOfMethodTest_data))]
        public void indexOfMethodTest(StringWrapper str, StringWrapper searchStr, (int start, int result)[] queries) {
            ASString boxedStr = (ASString)(ASObject)str.instance;
            int length = (str.instance != null) ? str.instance.Length : MAXINT;

            for (int i = 0; i < queries.Length; i++) {
                var (startIndex, result) = queries[i];

                foreach (double dStartIndex in generateEquivalentIndices(startIndex, length, negativeIsZero: true)) {
                    if (str.instance == null) {
                        assertThrowsNullReferenceError(() => ASString.indexOf(str.instance, searchStr.instance, dStartIndex));
                    }
                    else {
                        Assert.Equal(result, ASString.indexOf(str.instance, searchStr.instance, dStartIndex));
                        Assert.Equal(result, boxedStr.indexOf(searchStr.instance, dStartIndex));
                    }
                }
            }
        }

        public static IEnumerable<object[]> lastIndexOfMethodTest_data = TupleHelper.toArrays<StringWrapper, StringWrapper, (int, int)[]>(
            (null, null, new[] {(-1, -1), (0, -1), (1, -1), (MAXINT, -1)}),
            (null, "", new[] {(-1, -1), (0, -1), (1, -1), (MAXINT, -1)}),
            (null, "abc", new[] {(-1, -1), (0, -1), (1, -1), (MAXINT, -1)}),

            ("", null, new[] {(-1, -1), (0, -1)}),
            ("", "", new[] {(-1, -1), (0, 0)}),
            ("", "a", new[] {(-1, -1), (0, -1)}),

            ("abcdefghiabcdefghiabcdefghi", null, new[] {(-1, -1), (0, -1), (10, -1), (27, -1)}),
            ("abcdefghiabcdefghiabcdefghi", "", new[] {(-1, -1), (0, 0), (1, 1), (10, 10), (27, 27)}),
            ("abcdefghiabcdefghiaxydezghi", "a", new[] {(-1, -1), (0, 0), (1, 0), (8, 0), (9, 9), (10, 9), (18, 18), (19, 18), (27, 18)}),
            ("abcdefghiabcdefghiaxydezghi", "c", new[] {(-1, -1), (0, -1), (1, -1), (2, 2), (3, 2), (9, 2), (11, 11), (12, 11), (27, 11)}),
            ("abcdefghiabcdefghiaxydezghi", "e", new[] {(-1, -1), (0, -1), (4, 4), (5, 4), (14, 13), (23, 22), (27, 22)}),
            ("abcdefghiabcdefghiaxydezghi", "abc", new[] {(-1, -1), (0, 0), (1, 0), (7, 0), (9, 9), (10, 9), (18, 9), (27, 9)}),
            ("abcdefghiabcdefghiaxydezghi", "de", new[] {(-1, -1), (0, -1), (3, 3), (4, 3), (11, 3), (13, 12), (20, 12), (21, 21), (22, 21), (27, 21)}),
            ("abcdefghiabcdefghiaxydezghi", "defghi", new[] {(-1, -1), (0, -1), (3, 3), (4, 3), (12, 12), (13, 12), (21, 12), (27, 12)}),
            ("abcdefghiabcdefghiaxydezghi", "abcdefghiabcdefghiaxydezghi", new[] {(-1, -1), (0, 0), (1, 0), (27, 0)}),
            ("abcdefghiabcdefghiaxydezghi", "abcdefghiabcdefghiaxydezghij", new[] {(-1, -1), (0, -1), (1, -1), (27, -1)}),
            ("abcdefghiabcdefghiaxydezghi", "A", new[] {(-1, -1), (0, -1), (1, -1), (9, -1), (10, -1), (18, -1), (19, -1), (27, -1)}),

            ("abcabcabcabcabcabcabcabcabc", "abc", new[] {(-1, -1), (0, 0), (1, 0), (13, 12), (23, 21), (25, 24), (27, 24)}),
            ("abcabcabcabcabcabcabcabcabc", "bcabc", new[] {(-1, -1), (0, -1), (2, 1), (4, 4), (9, 7), (11, 10), (18, 16), (20, 19), (24, 22), (26, 22), (27, 22)}),
            ("abcabcabcabcabcabcabcabcabc", "abcabcabcabcabcabcabcabc", new[] {(-1, -1), (0, 0), (2, 0), (3, 3), (4, 3), (27, 3)}),
            ("abcabcabcabcabcabcabcabcabc", "abcabcabcabcabcabcabcabd", new[] {(-1, -1), (0, -1), (3, -1), (4, -1), (6, -1), (27, -1)}),

            ("hello", "HELLO", new[] {(0, -1)}),
            ("héllo", "héllo", new[] {(0, 0)}),
            ("hello", "héllo", new[] {(0, -1)}),
            ("he\u0301llo", "he\u0301llo", new[] {(0, 0)}),
            ("héllo", "he\u0301llo", new[] {(0, -1)}),
            ("he\u0301llo", "héllo", new[] {(0, -1)}),
            ("encyclopaedia", "encyclopædia", new[] {(0, -1)}),

            ("ABCD\ud800\udfff\udf00\ud8ff", "ABCD", new[] {(0, 0), (8, 0)}),
            ("ABCD\ud800\udfff\udf00\ud8ff\ud800", "\ud800", new[] {(0, -1), (2, -1), (4, 4), (5, 4), (8, 8), (9, 8)}),
            ("ABCD\ud800\udfff\udf00\ud8ff\ud800", "\ud800\udfff", new[] {(0, -1), (4, 4), (8, 4), (9, 4)}),
            ("ABCD\ud800\udfff\udf00\ud8ff\ud800", "\udf00", new[] {(9, 6)}),
            ("ABCD\ud800\udfff\udf00\ud8ff\ud800", "\udf00\ud8ff", new[] {(9, 6)}),
            ("ABCD\ud800\udfff\udf00\ud8ff\ud800", "\udf00\ud8ff\ud800", new[] {(9, 6)}),

            ("ABCDE\0FG", "E", new[] {(8, 4)}),
            ("ABCDE\0FG", "G", new[] {(8, 7)}),
            ("ABCDE\0FG", "E\0", new[] {(8, 4)}),
            ("ABCDE\0FG", "G\0", new[] {(8, -1)}),
            ("ABCDE\0FG", "E\0FG", new[] {(8, 4)}),
            ("ABCDE\0FG", "E\0FG\0", new[] {(8, -1)})
        );

        [Theory]
        [MemberData(nameof(lastIndexOfMethodTest_data))]
        public void lastIndexOfMethodTest(StringWrapper str, StringWrapper searchStr, (int start, int result)[] queries) {
            ASString boxedStr = (ASString)(ASObject)str.instance;
            int length = (str.instance != null) ? str.instance.Length : MAXINT;

            for (int i = 0; i < queries.Length; i++) {
                var (startIndex, result) = queries[i];

                foreach (double dStartIndex in generateEquivalentIndices(startIndex, length, nanIsInfinity: true)) {
                    if (str.instance == null) {
                        assertThrowsNullReferenceError(() => ASString.lastIndexOf(str.instance, searchStr.instance, dStartIndex));
                    }
                    else {
                        Assert.Equal(result, ASString.lastIndexOf(str.instance, searchStr.instance, dStartIndex));
                        Assert.Equal(result, boxedStr.lastIndexOf(searchStr.instance, dStartIndex));
                    }
                }
            }
        }

        public static IEnumerable<object[]> substr_substring_slice_testData = TupleHelper.toArrays<StringWrapper, (int, int)[]>(
            (null, new[] {(0, 0), (0, 1), (MAXINT, 0), (0, MAXINT)}),
            ("", new[] {(0, 0)}),
            ("a", new[] {(0, 0), (0, 1), (1, 0)}),

            (
                "abcdefghijklmnopqrstuvwxyz",
                new[] {
                    (0, 0), (5, 0), (25, 0), (26, 0),
                    (0, 1), (5, 1), (10, 1), (25, 1),
                    (0, 5), (10, 5), (20, 5), (21, 5),
                    (0, 25), (1, 25),
                    (0, 26)
                }
            ),
            (
                RandomHelper.randomString(new Random(17385138), 1000, 1000),
                new[] {(0, 0), (1000, 0), (0, 500), (200, 500), (500, 500), (0, 990), (10, 990), (0, 1000)}
            ),
            (
                "ABCD\ud800\udfff\udf00\ud8ff\ud800",
                new[] {
                    (0, 0), (4, 0), (5, 0), (9, 0),
                    (0, 4), (0, 5), (0, 6), (0, 7), (0, 8), (0, 9),
                    (4, 1), (4, 2), (4, 3), (4, 4), (4, 5),
                    (5, 1), (5, 2), (5, 4),
                    (6, 3)
                }
            ),
            ("ABCDE\0FG", new[] {(0, 8), (4, 1), (4, 3), (5, 1), (5, 3)}),
            ("he\u0301llo", new[] {(0, 2), (0, 3), (1, 1), (1, 2), (1, 5), (2, 1), (2, 4)})
        );

        [Theory]
        [MemberData(nameof(substr_substring_slice_testData))]
        public void substrMethodTest(StringWrapper str, (int start, int length)[] ranges) {
            ASString boxedStr = (ASString)(ASObject)str.instance;
            int strLength = (str.instance != null) ? str.instance.Length : MAXINT;

            for (int i = 0; i < ranges.Length; i++) {
                var (startIndex, subLength) = ranges[i];
                string expectedResult = (str.instance == null) ? null : str.instance.Substring(startIndex, subLength);

                foreach (var (dStartIndex, dSubLength) in generateRanges(startIndex, subLength)) {
                    if (str.instance == null) {
                        assertThrowsNullReferenceError(() => ASString.substr(str.instance, dStartIndex, dSubLength));
                    }
                    else {
                        Assert.Equal(expectedResult, ASString.substr(str.instance, dStartIndex, dSubLength));
                        Assert.Equal(expectedResult, boxedStr.substr(dStartIndex, dSubLength));
                    }
                }
            }

            IEnumerable<(double, double)> generateRanges(int start, int subLength) {
                var gRanges = new HashSet<(double, double)>();

                // For substr(): The start index is relative to the end of the string if negative.
                // Negative length is taken as zero.

                gRanges.Add((start, subLength));
                gRanges.Add((start + 0.3, subLength + 0.3));
                gRanges.Add((start + 0.7, subLength + 0.9));

                if (start + subLength == strLength) {
                    gRanges.Add((start, subLength + 1.0));
                    gRanges.Add((start, subLength + 100000.0));
                    gRanges.Add((start, INFINITY));
                }

                if (start == strLength) {
                    gRanges.Add((start + 1.0, subLength));
                    gRanges.Add((start + 100000.0, subLength));
                    gRanges.Add((INFINITY, subLength));
                }
                else if (start < strLength) {
                    gRanges.Add((start - strLength, subLength));
                    gRanges.Add((start - strLength - 0.3, subLength));
                    gRanges.Add((start - strLength - 0.7, subLength));
                }

                if (start == 0) {
                    gRanges.Add((-0.8, subLength));
                    gRanges.Add((-strLength, subLength));
                    gRanges.Add((-strLength - 1.0, subLength));
                    gRanges.Add((-strLength - 100000.0, subLength));
                    gRanges.Add((-INFINITY, subLength));
                    gRanges.Add((Double.NaN, subLength));
                }

                if (subLength == 0) {
                    gRanges.Add((start, -0.5));
                    gRanges.Add((start, -1.0));
                    gRanges.Add((start, -100000.0));
                    gRanges.Add((start, -INFINITY));
                }

                if (start == 0 && subLength == 0) {
                    gRanges.Add((-strLength - 100000.0, -100000.0));
                    gRanges.Add((-INFINITY, -INFINITY));
                    gRanges.Add((Double.NaN, Double.NaN));
                }

                if (start == strLength && subLength == 0) {
                    gRanges.Add((INFINITY, INFINITY));
                }

                if (start == 0 && subLength == strLength) {
                    gRanges.Add((-strLength - 100000.0, strLength + 100000.0));
                    gRanges.Add((-INFINITY, INFINITY));
                }

                return gRanges;
            }
        }

        [Theory]
        [MemberData(nameof(substr_substring_slice_testData))]
        public void substringMethodTest(StringWrapper str, (int start, int length)[] ranges) {
            ASString boxedStr = (ASString)(ASObject)str.instance;
            int strLength = (str.instance != null) ? str.instance.Length : MAXINT;

            for (int i = 0; i < ranges.Length; i++) {
                var (startIndex, subLength) = ranges[i];
                string expectedResult = (str.instance == null) ? null : str.instance.Substring(startIndex, subLength);

                foreach (var (dStartIndex, dEndIndex) in generateRanges(startIndex, startIndex + subLength)) {
                    if (str.instance == null) {
                        assertThrowsNullReferenceError(() => ASString.substring(str.instance, dStartIndex, dEndIndex));
                    }
                    else {
                        Assert.Equal(expectedResult, ASString.substring(str.instance, dStartIndex, dEndIndex));
                        Assert.Equal(expectedResult, boxedStr.substring(dStartIndex, dEndIndex));
                    }
                }
            }

            IEnumerable<(double, double)> generateRanges(int start, int end) {
                var gRanges = new HashSet<(double, double)>();

                // For substring():
                // Negative values for both start and end are clamped to zero (not relative to the end, unlike slice/substr).
                // Positive values are clamped to the string length.

                gRanges.Add((start, end));
                gRanges.Add((start + 0.3, end + 0.3));
                gRanges.Add((start + 0.7, end + 0.9));

                if (start == 0) {
                    gRanges.Add((-0.1, end));
                    gRanges.Add((-1.0, end));
                    gRanges.Add((-1000000.0, end));
                    gRanges.Add((-INFINITY, end));
                    gRanges.Add((Double.NaN, end));
                }

                if (end == 0) {
                    gRanges.Add((start, -0.1));
                    gRanges.Add((start, -1.0));
                    gRanges.Add((start, -1000000.0));
                    gRanges.Add((start, -INFINITY));
                    gRanges.Add((start, Double.NaN));
                }

                if (start == 0 && end == 0) {
                    gRanges.Add((-100000.0, -200000.0));
                    gRanges.Add((-INFINITY, -INFINITY));
                    gRanges.Add((Double.NaN, Double.NaN));
                }

                if (start == strLength) {
                    gRanges.Add((start + 1.0, end));
                    gRanges.Add((start + 100000.0, end));
                    gRanges.Add((INFINITY, end));
                }

                if (end == strLength) {
                    gRanges.Add((start, end + 1.0));
                    gRanges.Add((start, end + 100000.0));
                    gRanges.Add((start, INFINITY));
                }

                if (start == 0 && end == strLength) {
                    gRanges.Add((start - 100000.0, end + 100000.0));
                    gRanges.Add((-INFINITY, INFINITY));
                }

                if (start == strLength && end == strLength) {
                    gRanges.Add((start + 100000.0, end + 200000.0));
                    gRanges.Add((INFINITY, INFINITY));
                }

                // substring() must give the same result when the start and end indices are swapped.
                // So for evert (s, e) in the set of ranges we also add (e, s)
                var gRangesCopy = gRanges.ToArray();
                foreach (var (s, e) in gRangesCopy)
                    gRanges.Add((e, s));

                return gRanges;
            }
        }

        [Theory]
        [MemberData(nameof(substr_substring_slice_testData))]
        public void sliceMethodTest(StringWrapper str, (int start, int length)[] ranges) {
            ASString boxedStr = (ASString)(ASObject)str.instance;
            int strLength = (str.instance != null) ? str.instance.Length : MAXINT;

            for (int i = 0; i < ranges.Length; i++) {
                var (startIndex, subLength) = ranges[i];
                string expectedResult = (str.instance == null) ? null : str.instance.Substring(startIndex, subLength);

                foreach (var (dStartIndex, dEndIndex) in generateRanges(startIndex, startIndex + subLength)) {
                    if (str.instance == null) {
                        assertThrowsNullReferenceError(() => ASString.slice(str.instance, dStartIndex, dEndIndex));
                    }
                    else {
                        Assert.Equal(expectedResult, ASString.slice(str.instance, dStartIndex, dEndIndex));
                        Assert.Equal(expectedResult, boxedStr.slice(dStartIndex, dEndIndex));
                    }
                }
            }

            IEnumerable<(double, double)> generateRanges(int start, int end) {
                var gRanges = new HashSet<(double, double)>();

                // For slice(): Negative indices are relative to the end of the string, for
                // both start and end. Any range (s, e) where e < s is equivalent to (s, s)
                // as both produce the empty string.

                gRanges.Add((start, end));
                gRanges.Add((start + 0.3, end + 0.3));
                gRanges.Add((start + 0.7, end + 0.9));

                if (start == end) {
                    gRanges.Add((start + 1.0, end));
                    gRanges.Add((start + 100000.0, end));
                    gRanges.Add((INFINITY, end));
                }

                if (start == 0) {
                    gRanges.Add((-0.5, end));
                    gRanges.Add((-(double)strLength - 1.0, end));
                    gRanges.Add((-(double)strLength - 100000.0, end));
                    gRanges.Add((-INFINITY, end));
                    gRanges.Add((Double.NaN, end));
                }

                if (start == strLength) {
                    gRanges.Add((start + 1.0, end));
                    gRanges.Add((start + 100000.0, end));
                    gRanges.Add((INFINITY, end));
                }

                if (end == 0) {
                    gRanges.Add((start, -0.5));
                    gRanges.Add((start, -(double)strLength - 1.0));
                    gRanges.Add((start, -(double)strLength - 100000.0));
                    gRanges.Add((start, -INFINITY));
                    gRanges.Add((start, Double.NaN));
                }

                if (end == strLength) {
                    gRanges.Add((start, end + 1.0));
                    gRanges.Add((start, end + 100000.0));
                    gRanges.Add((start, INFINITY));
                }

                if (start < strLength) {
                    gRanges.Add((-(double)(strLength - start), end));
                    gRanges.Add((-(double)(strLength - start) - 0.3, end));
                    gRanges.Add((-(double)(strLength - start) - 0.7, end));
                }

                if (end < strLength) {
                    gRanges.Add((start, -(double)(strLength - end)));
                    gRanges.Add((start, -(double)(strLength - end) - 0.3));
                    gRanges.Add((start, -(double)(strLength - end) - 0.7));
                }

                if (start < strLength && end < strLength) {
                    gRanges.Add((-(double)(strLength - start), -(double)(strLength - end)));
                    gRanges.Add((-(double)(strLength - start) - 0.5, -(double)(strLength - end) - 0.5));
                    gRanges.Add((-(double)(strLength - start) - 0.7, -(double)(strLength - end) - 0.3));
                }

                if (start == 0 && end == 0) {
                    gRanges.Add((-(double)strLength - 100000.0, -(double)strLength - 100000.0));
                    gRanges.Add((-(double)strLength - 100000.0, -(double)strLength - 200000.0));
                    gRanges.Add((-(double)strLength - 200000.0, -(double)strLength - 100000.0));
                    gRanges.Add((-INFINITY, -INFINITY));
                    gRanges.Add((Double.NaN, Double.NaN));
                }

                if (start == end && end > 0) {
                    gRanges.Add((start, end - 1.0));
                    gRanges.Add((start, 0.0));
                    gRanges.Add((start, -(double)(strLength - end) - 1.0));
                    gRanges.Add((start, -(double)strLength));
                    gRanges.Add((start, -INFINITY));
                }

                if (start == strLength && end == strLength) {
                    gRanges.Add((start + 100000.0, end + 100000.0));
                    gRanges.Add((start + 100000.0, end + 200000.0));
                    gRanges.Add((start + 200000.0, end + 100000.0));
                    gRanges.Add((INFINITY, INFINITY));
                }

                if (start == 0 && end == strLength) {
                    gRanges.Add((-INFINITY, INFINITY));
                }

                return gRanges;
            }
        }

        public static IEnumerable<object[]> localeCompareMethodTest_data = TupleHelper.toArrays<StringWrapper, ASAny, int>(
            (null, ASAny.@null, 0),
            (null, "abc", 0),

            ("", "", 0),
            ("", "a", -1),
            ("", ASAny.@null, -1),
            ("", ASAny.undefined, -1),

            ("null", ASAny.@null, 0),
            ("nul", ASAny.@null, -1),
            ("nulm", ASAny.@null, 1),

            ("undefined", ASAny.undefined, 0),
            ("undefine", ASAny.undefined, -1),
            ("undefinee", ASAny.undefined, 1),

            ("abc", "abc", 0),
            ("abc", "abcd", -1),
            ("abc", "ab", 1),
            ("abc", "abd", -1),
            ("abc", "abb", 1),
            ("bcd", "c", -1),
            ("bcd", "caa", -1),
            ("bcd", "a", 1),
            ("bcd", "azz", 1),

            ("encyclopædia", "encyclopaddia", 1),
            ("encyclopædia", "encyclopaedia", 0),
            ("encyclopædia", "encyclopafdia", -1),
            ("encyclopædia", "encyclopb", -1),

            ("d\u0301", "é", -1),
            ("e\u0301", "é", 0),
            ("f\u0301", "é", 1),
            ("héllo", "he\u0301llo", 0),

            ("abc", "ABC", -1),
            ("ABC", "abc", 1)
        );

        [Theory]
        [MemberData(nameof(localeCompareMethodTest_data))]
        public void localeCompareMethodTest(StringWrapper str, ASAny arg, int expectedResultSign) {
            CultureInfo oldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("en-US", false);

            try {
                if (str.instance == null) {
                    assertThrowsNullReferenceError(() => ASString.localeCompare(str.instance, arg));
                }
                else {
                    ASString boxedStr = (ASString)(ASObject)str.instance;
                    Assert.Equal(expectedResultSign, Math.Sign(ASString.localeCompare(str.instance, arg)));
                    Assert.Equal(expectedResultSign, Math.Sign(boxedStr.localeCompare(arg)));
                }
            }
            finally {
                CultureInfo.CurrentCulture = oldCulture;
            }
        }

        public static IEnumerable<object[]> splitMethodTest_noRegExp_data = TupleHelper.toArrays<StringWrapper, ASAny, int[], StringWrapper[]>(
            (null, "", null, null),
            (null, ASAny.undefined, null, null),

            ("", "", new[] {0, 1, MAXINT}, Array.Empty<StringWrapper>()),
            ("", "a", new[] {0, 1, MAXINT}, new StringWrapper[] {""}),
            ("", ASAny.undefined, new[] {0, 1, MAXINT}, new StringWrapper[] {""}),

            ("abcdef1234", "", new[] {0, 1, 4, 9, 10, 11, MAXINT}, new StringWrapper[] {"a", "b", "c", "d", "e", "f", "1", "2", "3", "4"}),

            ("abc-def-ghi-jkl-mno", "-", new[] {0, 1, 3, 5, 6, MAXINT}, new StringWrapper[] {"abc", "def", "ghi", "jkl", "mno"}),
            ("abc-def-ghi-jkl-mno", "~", new[] {0, 1, 2, 3, MAXINT}, new StringWrapper[] {"abc-def-ghi-jkl-mno"}),

            ("---abcde--f----ghijk--l---", "-", new[] {0, 1, 3, 12, 14, 15, 20, MAXINT}, new StringWrapper[] {"", "", "", "abcde", "", "f", "", "", "", "ghijk", "", "l", "", "", ""}),
            ("---abcde--f----ghijk--l---", "--", new[] {0, 1, 3, 4, 6, 7, MAXINT}, new StringWrapper[] {"", "-abcde", "f", "", "ghijk", "l", "-"}),
            ("---abcde--f----ghijk--l---", "---", new[] {0, 1, 3, 4, MAXINT}, new StringWrapper[] {"", "abcde--f", "-ghijk--l", ""}),
            ("---abcde--f----ghijk--l---", "-----", new[] {0, 1, 2, 3, MAXINT}, new StringWrapper[] {"---abcde--f----ghijk--l---"}),

            ("abcdnullefghnullijknull", ASAny.@null, null, new StringWrapper[] {"abcd", "efgh", "ijk", ""}),
            ("undefined1234undefined567UNDEFINED", ASAny.undefined, null, new StringWrapper[] {"", "1234", "567UNDEFINED"}),

            ("123a123A135a146A13a0", "a", null, new StringWrapper[] {"123", "123A135", "146A13", "0"}),
            ("123a123A135a146A13a0", "A", null, new StringWrapper[] {"123a123", "135a146", "13a0"}),

            ("aaaa", "a", null, new StringWrapper[] {"", "", "", "", ""}),
            ("aaaa", "aa", null, new StringWrapper[] {"", "", ""}),
            ("aaaaa", "aa", null, new StringWrapper[] {"", "", "a"}),

            ("abcd\0dabcd\0dabc", "", null, new StringWrapper[] {"a", "b", "c", "d", "\0", "d", "a", "b", "c", "d", "\0", "d", "a", "b", "c"}),
            ("abcd\0dabcd\0dabc", "\0", null, new StringWrapper[] {"abcd", "dabcd", "dabc"}),
            ("abcd\0dabcd\0dabc", "d\0d", null, new StringWrapper[] {"abc", "abc", "abc"}),
            ("abcd\0dabcd\0dabc\0", "\0", null, new StringWrapper[] {"abcd", "dabcd", "dabc", ""}),

            ("encyclopaedia encyclopædia", "ae", null, new StringWrapper[] {"encyclop", "dia encyclopædia"}),
            ("encyclopaedia encyclopædia", "æ", null, new StringWrapper[] {"encyclopaedia encyclop", "dia"}),
            ("he\u0301llo", "", null, new StringWrapper[] {"h", "e", "\u0301", "l", "l", "o"}),
            ("hello héllo he\u0301llo", "e", null, new StringWrapper[] {"h", "llo héllo h", "\u0301llo"}),
            ("hello héllo he\u0301llo", "é", null, new StringWrapper[] {"hello h", "llo he\u0301llo"}),
            ("hello héllo he\u0301llo", "e\u0301", null, new StringWrapper[] {"hello héllo h", "llo"}),

            ("\ud801\udeff", "", null, new StringWrapper[] {"\ud801", "\udeff"}),
            ("a\ud801b\udeffc", "", null, new StringWrapper[] {"a", "\ud801", "b", "\udeff", "c"}),
            ("\udd92\uda3c", "", null, new StringWrapper[] {"\udd92", "\uda3c"}),

            ("abcd\ud801\udeffabcd\ud801\udeff123", "\ud801", null, new StringWrapper[] {"abcd", "\udeffabcd", "\udeff123"}),
            ("abcd\ud801\udeffabcd\ud801\udeff123", "\udeff", null, new StringWrapper[] {"abcd\ud801", "abcd\ud801", "123"}),
            ("abcd\ud801\udeffabcd\ud801\udeff123", "\ud801\udeff", null, new StringWrapper[] {"abcd", "abcd", "123"}),

            ("123a123A135a146A13a0", new ConvertibleMockObject(stringValue: "a"), null, new StringWrapper[] {"123", "123A135", "146A13", "0"})
        );

        [Theory]
        [MemberData(nameof(splitMethodTest_noRegExp_data))]
        public void splitMethodTest_noRegExp(StringWrapper str, ASAny separator, int[] limits, StringWrapper[] expected) {
            if (limits == null)
                limits = new[] {0, 1, MAXINT};

            ASString boxedStr = (ASString)(ASObject)str.instance;

            for (int i = 0; i < limits.Length; i++) {
                foreach (var limitArg in generateLimits(limits[i])) {
                    if (str.instance == null) {
                        assertThrowsNullReferenceError(() => ASString.split(str.instance, separator, limitArg));
                    }
                    else {
                        checkResult(ASString.split(str.instance, separator, limitArg), limits[i]);
                        checkResult(boxedStr.split(separator, limitArg), limits[i]);
                    }
                }
            }

            void checkResult(ASArray result, int limit) {
                Assert.Equal((uint)Math.Min(limit, expected.Length), result.length);

                for (uint i = 0; i < result.length; i++)
                    AssertHelper.valueIdentical(expected[(int)i].instance, result.AS_getElement(i));
            }

            IEnumerable<ASAny> generateLimits(int limit) {
                var gLimits = new List<ASAny>();

                gLimits.Add(limit);
                gLimits.Add(limit + 0.7);
                gLimits.Add(limit + (double)(1L << 32));
                gLimits.Add(limit + (double)((1L << 32) * 37) + 0.5);
                gLimits.Add(-((double)((1L << 32) * 14) - limit) - 0.75);

                if (limit == MAXINT) {
                    gLimits.Add(ASAny.undefined);
                    gLimits.Add(UInt32.MaxValue);
                }

                if (limit == 0) {
                    gLimits.Add(Double.NaN);
                }

                return gLimits;
            }
        }

        public static IEnumerable<object[]> toLowerCaseMethodTest_data = TupleHelper.toArrays<StringWrapper, StringWrapper>(
            (null, null),
            ("", ""),
            ("abcd", "abcd"),
            ("ABCD", "abcd"),
            ("aBcDi", "abcdi"),
            ("Hello, World!!$", "hello, world!!$"),
            ("0123ab456DE789fg1932%H%?", "0123ab456de789fg1932%h%?"),
            ("aBcD\0eFgH", "abcd\0efgh"),
            ("\0\u0001\u0008\u000d\u0014\u001f", "\0\u0001\u0008\u000d\u0014\u001f"),
            ("ÀÁÅÆÉËÑØàáåæéëñø×÷ҚқѢѣѦѧΓγΘθΛλΝμ", "àáåæéëñøàáåæéëñø×÷ққѣѣѧѧγγθθλλνμ"),
            ("a\u0301E\u0301i\u0301", "a\u0301e\u0301i\u0301")
        );

        [Theory]
        [MemberData(nameof(toLowerCaseMethodTest_data))]
        public void toLowerCaseMethodTest(StringWrapper str, StringWrapper expected) {
            if (str.instance == null) {
                assertThrowsNullReferenceError(() => ASString.toLowerCase(str.instance));
            }
            else {
                ASString boxedStr = (ASString)(ASObject)str.instance;
                Assert.Equal(expected.instance, ASString.toLowerCase(str.instance));
                Assert.Equal(expected.instance, boxedStr.toLowerCase());
            }
        }

        public static IEnumerable<object[]> toUpperCaseMethodTest_data = TupleHelper.toArrays<StringWrapper, StringWrapper>(
            (null, null),
            ("", ""),
            ("abcd", "ABCD"),
            ("ABCD", "ABCD"),
            ("aBcDi", "ABCDI"),
            ("Hello, World!!$", "HELLO, WORLD!!$"),
            ("0123ab456DE789fg1932%H%?", "0123AB456DE789FG1932%H%?"),
            ("aBcD\0eFgH", "ABCD\0EFGH"),
            ("\0\u0001\u0008\u000d\u0014\u001f", "\0\u0001\u0008\u000d\u0014\u001f"),
            ("ÀÁÅÆÉËÑØàáåæéëñø×÷ҚқѢѣѦѧΓγΘθΛλΝμ", "ÀÁÅÆÉËÑØÀÁÅÆÉËÑØ×÷ҚҚѢѢѦѦΓΓΘΘΛΛΝΜ"),
            ("a\u0301E\u0301i\u0301", "A\u0301E\u0301I\u0301")
        );

        [Theory]
        [MemberData(nameof(toUpperCaseMethodTest_data))]
        public void toUpperCaseMethodTest(StringWrapper str, StringWrapper expected) {
            if (str.instance == null) {
                assertThrowsNullReferenceError(() => ASString.toUpperCase(str.instance));
            }
            else {
                ASString boxedStr = (ASString)(ASObject)str.instance;
                Assert.Equal(expected.instance, ASString.toUpperCase(str.instance));
                Assert.Equal(expected.instance, boxedStr.toUpperCase());
            }
        }

        public static IEnumerable<object[]> toLocaleLowerCaseMethodTest_data = TupleHelper.toArrays<StringWrapper, string, StringWrapper>(
            (null, "en-US", null),
            ("", "en-US", ""),
            ("abcd", "en-US", "abcd"),
            ("ABCD", "en-US", "abcd"),
            ("aBcDi", "en-US", "abcdi"),
            ("Hello, World!!$", "en-US", "hello, world!!$"),
            ("0123ab456DE789fg1932%H%?", "en-US", "0123ab456de789fg1932%h%?"),
            ("aBcD\0eFgH", "en-US", "abcd\0efgh"),
            ("\0\u0001\u0008\u000d\u0014\u001f", "en-US", "\0\u0001\u0008\u000d\u0014\u001f"),
            ("ÀÁÅÆÉËÑØàáåæéëñø×÷ҚқѢѣѦѧΓγΘθΛλΝμ", "en-US", "àáåæéëñøàáåæéëñø×÷ққѣѣѧѧγγθθλλνμ"),
            ("a\u0301E\u0301i\u0301", "en-US", "a\u0301e\u0301i\u0301"),
            ("Iiıİ", "en-US", "iiıi"),
            ("Iiıİ", "tr-TR", "ıiıi")
        );

        [Theory]
        [MemberData(nameof(toLocaleLowerCaseMethodTest_data))]
        public void toLocaleLowerCaseMethodTest(StringWrapper str, string culture, StringWrapper expected) {
            CultureInfo oldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(culture, false);

            try {
                if (str.instance == null) {
                    assertThrowsNullReferenceError(() => ASString.toLocaleLowerCase(str.instance));
                }
                else {
                    ASString boxedStr = (ASString)(ASObject)str.instance;
                    Assert.Equal(expected.instance, ASString.toLocaleLowerCase(str.instance));
                    Assert.Equal(expected.instance, boxedStr.toLocaleLowerCase());
                }
            }
            finally {
                CultureInfo.CurrentCulture = oldCulture;
            }
        }

        public static IEnumerable<object[]> toLocaleUpperCaseMethodTest_data = TupleHelper.toArrays<StringWrapper, string, StringWrapper>(
            (null, "en-US", null),
            ("", "en-US", ""),
            ("abcd", "en-US", "ABCD"),
            ("ABCD", "en-US", "ABCD"),
            ("aBcDi", "en-US", "ABCDI"),
            ("Hello, World!!$", "en-US", "HELLO, WORLD!!$"),
            ("0123ab456DE789fg1932%H%?", "en-US", "0123AB456DE789FG1932%H%?"),
            ("aBcD\0eFgH", "en-US", "ABCD\0EFGH"),
            ("\0\u0001\u0008\u000d\u0014\u001f", "en-US", "\0\u0001\u0008\u000d\u0014\u001f"),
            ("ÀÁÅÆÉËÑØàáåæéëñø×÷ҚқѢѣѦѧΓγΘθΛλΝμ", "en-US", "ÀÁÅÆÉËÑØÀÁÅÆÉËÑØ×÷ҚҚѢѢѦѦΓΓΘΘΛΛΝΜ"),
            ("a\u0301E\u0301i\u0301", "en-US", "A\u0301E\u0301I\u0301"),
            ("Iiıİ", "en-US", "IIIİ"),
            ("Iiıİ", "tr-TR", "IİIİ")
        );

        [Theory]
        [MemberData(nameof(toLocaleUpperCaseMethodTest_data))]
        public void toLocaleUpperCaseMethodTest(StringWrapper str, string culture, StringWrapper expected) {
            CultureInfo oldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(culture, false);

            try {
                if (str.instance == null) {
                    assertThrowsNullReferenceError(() => ASString.toLocaleUpperCase(str.instance));
                }
                else {
                    ASString boxedStr = (ASString)(ASObject)str.instance;
                    Assert.Equal(expected.instance, ASString.toLocaleUpperCase(str.instance));
                    Assert.Equal(expected.instance, boxedStr.toLocaleUpperCase());
                }
            }
            finally {
                CultureInfo.CurrentCulture = oldCulture;
            }
        }

        public static IEnumerable<object[]> replaceMethodTest_noRegExp_data = TupleHelper.toArrays<StringWrapper, ASAny, ASAny, StringWrapper>(
            (null, ASAny.undefined, ASAny.undefined, null),
            (null, "", "", null),

            ("", ASAny.undefined, "", ""),
            ("", "", "", ""),
            ("", "a", "b", ""),
            ("", "", "a", "a"),
            ("", "", ASAny.undefined, "undefined"),
            ("", "", ASAny.@null, "null"),
            ("", "", new ConvertibleMockObject(stringValue: "1234"), "1234"),

            ("abcd", "", "", "abcd"),
            ("abcd", "", "xyz", "xyzabcd"),

            ("abcdefghijklmnopqrst", "abc", "", "defghijklmnopqrst"),
            ("abcdefghijklmnopqrst", "abc", "abc", "abcdefghijklmnopqrst"),
            ("abcdefghijklmnopqrst", "abc", "xyz", "xyzdefghijklmnopqrst"),
            ("abcdefghijklmnopqrst", "abc", "xyzw", "xyzwdefghijklmnopqrst"),
            ("abcdefghijklmnopqrst", "abc", "xy", "xydefghijklmnopqrst"),
            ("abcdefghijklmnopqrst", "ghi", "", "abcdefjklmnopqrst"),
            ("abcdefghijklmnopqrst", "ghi", "ghi", "abcdefghijklmnopqrst"),
            ("abcdefghijklmnopqrst", "ghi", "xyz", "abcdefxyzjklmnopqrst"),
            ("abcdefghijklmnopqrst", "ghi", "xyzw", "abcdefxyzwjklmnopqrst"),
            ("abcdefghijklmnopqrst", "ghi", "xy", "abcdefxyjklmnopqrst"),
            ("abcdefghijklmnopqrst", "rst", "", "abcdefghijklmnopq"),
            ("abcdefghijklmnopqrst", "rst", "rst", "abcdefghijklmnopqrst"),
            ("abcdefghijklmnopqrst", "rst", "xyz", "abcdefghijklmnopqxyz"),
            ("abcdefghijklmnopqrst", "rst", "xyzw", "abcdefghijklmnopqxyzw"),
            ("abcdefghijklmnopqrst", "rst", "xy", "abcdefghijklmnopqxy"),

            ("abcdefghijklmnopqrst", "abcdefghijklmnopqrst", "", ""),
            ("abcdefghijklmnopqrst", "abcdefghijklmnopqrst", "abcdefghijklmnopqrst", "abcdefghijklmnopqrst"),
            ("abcdefghijklmnopqrst", "abcdefghijklmnopqrst", "ABCDEFGHIJKLMNOPQRST", "ABCDEFGHIJKLMNOPQRST"),
            ("abcdefghijklmnopqrst", "abcdefghijklmnopqrst", "ABCDEFGHIJKLMNOPQR", "ABCDEFGHIJKLMNOPQR"),
            ("abcdefghijklmnopqrst", "abcdefghijklmnopqrst", "ABCDEFGHIJKLMNOPQRSTUVW", "ABCDEFGHIJKLMNOPQRSTUVW"),

            ("aaaaaaaaa", "a", "", "aaaaaaaa"),
            ("aaaaaaaaa", "aaa", "b", "baaaaaa"),
            ("aaaaabbaabbaa", "bb", "cbc", "aaaaacbcaabbaa"),

            ("1234 null undefined hello 1234", ASAny.@null, "---", "1234 --- undefined hello 1234"),
            ("1234 null undefined hello 1234", ASAny.undefined, "---", "1234 null --- hello 1234"),
            ("1234 null undefined hello 1234", 1234, "---", "--- null undefined hello 1234"),
            ("1234 null undefined hello 1234", 5678, "---", "1234 null undefined hello 1234"),
            ("1234 null undefined hello 1234", new ConvertibleMockObject(stringValue: "hello"), "---", "1234 null undefined --- 1234"),

            ("Hello, xyz xyz!", "xyz", ASAny.@null, "Hello, null xyz!"),
            ("Hello, xyz xyz!", "xyz", ASAny.undefined, "Hello, undefined xyz!"),
            ("Hello, xyz xyz!", "xyz", 1938.44, "Hello, 1938.44 xyz!"),
            ("Hello, xyz xyz!", "xyz", new ConvertibleMockObject(stringValue: ""), "Hello,  xyz!"),

            ("hello HELLO", "hello", "?", "? HELLO"),
            ("hello HELLO", "HELLO", "?", "hello ?"),
            ("héllo world", "héllo", "??", "?? world"),
            ("héllo world", "h\u0301llo", "??", "héllo world"),
            ("he\u0301llo world", "héllo", "??", "he\u0301llo world"),
            ("he\u0301llo world", "he\u0301llo", "??", "?? world"),

            ("ABCDE\0FG", "\0", "", "ABCDEFG"),
            ("ABCDE\0FG", "E\0", "", "ABCDFG"),
            ("ABCDE\0FG", "E\0F", "", "ABCDG"),
            ("ABCDE\0FG", "G\0", "", "ABCDE\0FG"),

            ("\ud84e\udf37\udf43\udb0c", "\ud84e", "x", "x\udf37\udf43\udb0c"),
            ("\ud84e\udf37\udf43\udb0c", "\udf37", "x", "\ud84ex\udf43\udb0c"),
            ("\ud84e\udf37\udf43\udb0c", "\ud84e\udf37", "x", "x\udf43\udb0c"),
            ("\ud84e\udf37\udf43\udb0c", "\udf43", "x", "\ud84e\udf37x\udb0c"),
            ("\ud84e\udf37\udf43\udb0c", "\udf43\udb0c", "x", "\ud84e\udf37x"),
            ("\ud84e\udf37\udf43\udb0c", "\udf43\udb0c", "\udb0c\udf43", "\ud84e\udf37\udb0c\udf43"),

            ("abcdefg", "c", "\ud800", "ab\ud800defg"),
            ("abcdefg", "c", "\uddf3", "ab\uddf3defg"),
            ("abcdefg", "c", "\ud800\uddf3", "ab\ud800\uddf3defg")
        );

        [Theory]
        [MemberData(nameof(replaceMethodTest_noRegExp_data))]
        public void replaceMethodTest_noRegExp(
            StringWrapper str, ASAny searchStr, ASAny replaceStr, StringWrapper expectedResult)
        {
            if (str.instance == null) {
                assertThrowsNullReferenceError(() => ASString.replace(str.instance, searchStr, replaceStr));
                return;
            }

            ASString boxedStr = (ASString)(ASObject)str.instance;
            string result = ASString.replace(str.instance, searchStr, replaceStr);

            Assert.Equal(expectedResult.instance, result);
        }

        [Theory]
        [MemberData(nameof(replaceMethodTest_noRegExp_data))]
        public void replaceMethodTest_noRegExp_withCallback(
            StringWrapper str, ASAny searchStr, ASAny replaceStr, StringWrapper expectedResult)
        {
            var callback = new SpyFunctionObject((r, args) => replaceStr);

            if (str.instance == null) {
                assertThrowsNullReferenceError(() => ASString.replace(str.instance, searchStr, callback));
                return;
            }

            ASString boxedStr = (ASString)(ASObject)str.instance;
            string result = ASString.replace(str.instance, searchStr, callback);

            Assert.Equal(expectedResult.instance, result);

            var callRecords = callback.getCallRecords();

            if (callRecords.Length == 0) {
                Assert.Equal(str, result);
            }
            else {
                Assert.Equal(1, callRecords.Length);
                Assert.False(callRecords[0].isConstruct);

                var args = callRecords[0].getArguments();
                Assert.Equal(3, args.Length);
                AssertHelper.valueIdentical(ASAny.AS_convertString(searchStr), args[0]);
                AssertHelper.valueIdentical(str.instance, args[2]);

                AssertHelper.valueIdentical(
                    str.instance.IndexOf(ASAny.AS_convertString(searchStr), StringComparison.Ordinal),
                    args[1]
                );
            }
        }

    }

}
