using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Mariana.Common;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class ASJSONTest {

        private static readonly ASAny NULL = ASAny.@null;
        private static readonly ASAny UNDEF = ASAny.undefined;
        private static readonly double NEG_ZERO = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000L));

        private static ASArray makeArray(params ASAny[] elements) => new ASArray(elements);
        private static ASVector<T> makeVector<T>(params T[] elements) => new ASVector<T>(elements);

        private static ASObject makeObject(params (string name, ASAny value)[] properties) {
            var obj = new ASObject();
            for (int i = 0; i < properties.Length; i++)
                obj.AS_dynamicProps.setValue(properties[i].name, properties[i].value);
            return obj;
        }

        private void validateStructuralEquality(
            ASAny expected,
            ASAny actual,
            bool roundtripMode = false,
            Func<ASAny, string, ASAny, ASAny, ASAny> unwrapper = null
        ) {
            if (expected.isUndefinedOrNull) {
                // undefined converts to null in roundtrip.
                AssertHelper.valueIdentical(roundtripMode ? NULL : expected, actual);
            }
            else if (ASObject.AS_isPrimitive(expected.value)) {
                if (!(roundtripMode && expected.value is ASNumber)) {
                    AssertHelper.valueIdentical(expected, actual);
                    return;
                }

                // If we are validating a roundtrip (stringify -> parse) result, NaNs must be converted
                // to null and negative zeros to positive zero.
                double expectedNum = (double)expected;
                if (!Double.IsFinite(expectedNum))
                    AssertHelper.identical(NULL, actual);
                else if (expectedNum == 0.0)
                    AssertHelper.valueIdentical(0.0, actual);
                else
                    AssertHelper.valueIdentical(expected, actual);
            }
            else if (expected.value is ASArray expectedArray) {
                ASArray actualArray = Assert.IsType<ASArray>(actual.value);
                Assert.Equal(expectedArray.length, actualArray.length);

                for (uint i = 0; i < expectedArray.length; i++) {
                    ASAny actualElement = actualArray[i];

                    if (unwrapper != null) {
                        actualElement = unwrapper(
                            actualArray, i.ToString(CultureInfo.InvariantCulture), actualArray[i], expectedArray[i]);
                    }

                    validateStructuralEquality(expectedArray[i], actualElement, roundtripMode, unwrapper);
                }
            }
            else if (roundtripMode && expected.value is ASVectorAny expectedVector) {
                // Vectors are converted to arrays when doing a JSON roundtrip.
                ASArray actualArray = Assert.IsType<ASArray>(actual.value);
                Assert.Equal((uint)expectedVector.length, actualArray.length);

                for (int i = 0; i < expectedVector.length; i++) {
                    ASAny actualElement = actualArray[i];

                    if (unwrapper != null) {
                        actualElement = unwrapper(
                            actualArray, i.ToString(CultureInfo.InvariantCulture), actualArray[i], expectedVector[i]);
                    }

                    validateStructuralEquality(expectedVector[i], actualElement, roundtripMode, unwrapper);
                }
            }
            else {
                if (roundtripMode && (expected.isUndefined || expected.value is ASFunction)) {
                    AssertHelper.identical(NULL, actual);
                    return;
                }

                ASObject expectedObject = expected.value;
                ASObject actualObject = Assert.IsType<ASObject>(actual.value);

                DynamicPropertyCollection expectedProps = expectedObject.AS_dynamicProps;
                DynamicPropertyCollection actualProps = actualObject.AS_dynamicProps;

                var propDict = new Dictionary<string, ASAny>(StringComparer.Ordinal);

                int curIndex = expectedProps.getNextIndex(-1);

                while (curIndex != -1) {
                    string propName = expectedProps.getNameFromIndex(curIndex);
                    ASAny propValue = expectedProps.getValueFromIndex(curIndex);
                    propDict.Add(propName, propValue);
                    curIndex = expectedProps.getNextIndex(curIndex);
                }

                curIndex = actualProps.getNextIndex(-1);

                while (curIndex != -1) {
                    string propName = actualProps.getNameFromIndex(curIndex);
                    Assert.Contains(propName, propDict.Keys);

                    ASAny propValue = actualProps.getValueFromIndex(curIndex);

                    if (unwrapper != null)
                        propValue = unwrapper(actualObject, propName, propValue, propDict[propName]);
                    else if (roundtripMode)
                        Assert.False(propDict[propName].value is ASFunction);

                    validateStructuralEquality(propDict[propName], propValue, roundtripMode, unwrapper);

                    propDict.Remove(propName);
                    curIndex = actualProps.getNextIndex(curIndex);
                }

                if (roundtripMode)
                    Assert.All(propDict.Values, v => Assert.True(v.isUndefined || v.value is ASFunction));
                else
                    Assert.Empty(propDict);
            }
        }

        public static IEnumerable<object[]> parseTestData_trueFalseNull = TupleHelper.toArrays<StringWrapper, ASAny>(
            ("null", NULL),
            ("null  ", NULL),
            ("  null", NULL),
            ("  null  ", NULL),
            (" \nnull\r\t", NULL),

            ("true", true),
            ("true  ", true),
            ("  true", true),
            ("  true  ", true),
            (" \ntrue\r\t", true),

            ("false", false),
            ("false  ", false),
            ("  false", false),
            ("  false  ", false),
            (" false\r\t", false)
        );

        [Theory]
        [MemberData(nameof(parseTestData_trueFalseNull))]
        public void parseTest_trueFalseNull(StringWrapper jsonStr, ASAny expectedValue) {
            AssertHelper.valueIdentical(expectedValue, ASJSON.parse(jsonStr.value));
        }

        public static IEnumerable<object[]> parseTestData_numbers = TupleHelper.toArrays<StringWrapper, ASAny>(
            ("0", 0),
            ("-0", NEG_ZERO),
            ("1", 1),
            ("-384", -384),
            ("1834722", 1834722),
            ("2147483647", 2147483647),
            ("-2147483648", -2147483648),
            ("3000000000", 3000000000),
            ("4294967295", 4294967295),
            ("9007199254740992", 9007199254740992),
            ("-288230376151711777", -288230376151711800),
            ("4329483243000493353200352", 4.3294832430004935e+24),
            ("17976931348623157" + new string('0', 292), Double.MaxValue),

            ("0.0", 0),
            ("-0.000000", NEG_ZERO),
            ("0.", 0),
            ("1834722.", 1834722),
            ("0.3421", 0.3421),
            ("0.000000000005433116", 0.000000000005433116),
            ("-1.3300034882", -1.3300034882),
            ("495831.59965", 495831.59965),
            ("-384546.99000033253", -384546.99000033253),
            ("9007199254740993.1", 9007199254740994),
            ("0." + new string('0', 323) + "5", Double.Epsilon),

            ("0e0", 0),
            ("0e100", 0),
            ("0e+1", 0),
            ("0e-100", 0),
            ("-0e+100", NEG_ZERO),
            ("0.0E+1000000000000", 0),

            ("1e0", 1),
            ("1e+0", 1),
            ("1E0", 1),

            ("2.445e+4", 2.445e+4),
            ("2.445e-4", 2.445e-4),
            ("2445.e-4", 0.2445),
            ("2445.e+4", 24450000),
            ("24450000000e+4", 2.445e+14),
            ("24450000000e-4", 2.445e+6),

            ("5.4332E+000000000000000000000000000000", 5.4332),
            ("-5.4332E+000000000000000000000000000004", -54332),

            ("9007.199254740991e+12", 9007199254740991),
            ("90071992547409930000000000000000001E-19", 9007199254740994),
            ("1.7976931348623157e+308", Double.MaxValue),
            ("-0." + new string('0', 200) + "17976931348623157e+509", -Double.MaxValue),
            ("2.2250738585072014e-308", 2.2250738585072014e-308),
            ("4.940656458412e-324", Double.Epsilon),

            ("2e308", Double.PositiveInfinity),
            ("1" + new string('0', 400), Double.PositiveInfinity),
            ("-1" + new string('0', 400), Double.NegativeInfinity),
            ("1e-324", 0),
            ("-1e-324", NEG_ZERO),
            ("0." + new string('0', 323) + "24", 0),

            ("123  ", 123),
            ("\t 123  \r\n  ", 123),
            ("\t -123  \r\n  ", -123),
            ("  -123  ", -123),
            ("2.445E6   ", 2.445e+6),
            ("    -0.0006849993 \n", -0.0006849993)
        );

        [Theory]
        [MemberData(nameof(parseTestData_numbers))]
        public void parseTest_numbers(StringWrapper jsonStr, ASAny expectedValue) {
            AssertHelper.valueIdentical(expectedValue, ASJSON.parse(jsonStr.value));
        }

        public static IEnumerable<object[]> parseTestData_strings = TupleHelper.toArrays<StringWrapper, ASAny>(
            ("\"\"", ""),
            ("\"abcd\"", "abcd"),
            ("\"  \"", "  "),
            ("\"/\"", "/"),
            (@"""\\\\\\""", @"\\\"),
            (@"""\/\\\""\b\f\n\r\t""", "/\\\"\b\f\n\r\t"),
            (@"""\u0000\u0001\u001f\u0045\u006C\u0037\u005c\u0020\u007E""", "\0\u0001\u001fEl7\\ ~"),
            (@"""abc\u00A0\u00f1123\/\u00E5\u00dC@\u00BE\n\u00Bb,\\""", "abc\u00a0\u00f1123/\u00e5\u00dc@\u00be\n\u00bb,\\"),
            (@"""\u0236\u0a3C\u0FFd\u309a\u4729\u88ED\uB3Aa\uCDDA\ud65e\uFFFF""", "\u0236\u0a3c\u0ffd\u309a\u4729\u88ed\ub3aa\ucdda\ud65e\uffff"),
            (@"""ab\ud801\udc2acd""", "ab\ud801\udc2acd"),
            (@"""\ud801""", "\ud801"),
            (@"""\udc2a""", "\udc2a"),
            (@"""ab\ud801cd""", "ab\ud801cd"),
            (@"""ab\ud801\udb01""", "ab\ud801\udb01"),
            (@"""ab\udc2a\ud801""", "ab\udc2a\ud801"),
            ("\"\ud801\\udd24\"", "\ud801\udd24"),

            ("  \" abc \" ", " abc "),
            (" \r \n \t \" \\r \\n \\t \\b \" \r \n \t ", " \r \n \t \b "),

            (
                "\"" + String.Concat(Enumerable.Repeat("\\u0061b\u0043d", 200)) + "\"",
                String.Concat(Enumerable.Repeat("abCd", 200))
            )
        );

        [Theory]
        [MemberData(nameof(parseTestData_strings))]
        public void parseTest_strings(StringWrapper jsonStr, ASAny expectedValue) {
            AssertHelper.valueIdentical(expectedValue, ASJSON.parse(jsonStr.value));
        }

        public static IEnumerable<object[]> parseTestData_simpleArrays = TupleHelper.toArrays<StringWrapper, ASAny[]>(
            ("[]", Array.Empty<ASAny>()),
            ("[  ]", Array.Empty<ASAny>()),

            ("[122]", new ASAny[] {122}),
            ("[-13.5562]", new ASAny[] {-13.5562}),
            ("[\"hello\"]", new ASAny[] {"hello"}),
            ("[true]", new ASAny[] {true}),
            ("[ -999938222 ]", new ASAny[] {-999938222}),
            ("[ \"hello\" ]", new ASAny[] {"hello"}),
            ("[ null ]", new ASAny[] {NULL}),

            ("[123,456,789]", new ASAny[] {123, 456, 789}),
            ("[123, 456, 789]", new ASAny[] {123, 456, 789}),
            ("[ 123, 456, true ]", new ASAny[] {123, 456, true}),

            (
                "[\"\",123,\"hello\",0.00493,true,null,\"Foobar\",\"\\\\\"]",
                new ASAny[] {"", 123, "hello", 0.00493, true, NULL, "Foobar", "\\"}
            ),
            (
                "[\"\", 123, \"hello\", 0.00493, true, null, \"Foobar\", \"\\\\\"]",
                new ASAny[] {"", 123, "hello", 0.00493, true, NULL, "Foobar", "\\"}
            ),

            (" \r [ \t\n ] \n", Array.Empty<ASAny>()),
            (
                " \r [ \t 123,\n \t \"hello\"\n, \r\n0.00493\n\n,true\t,\t null, \"Foobar\",\t\"\\\\\" ] \n",
                new ASAny[] {123, "hello", 0.00493, true, NULL, "Foobar", "\\"}
            )
        );

        [Theory]
        [MemberData(nameof(parseTestData_simpleArrays))]
        public void parseTest_simpleArrays(StringWrapper jsonStr, ASAny[] expectedArray) {
            ASObject parsedObject = ASJSON.parse(jsonStr.value);
            ASArray parsedArray = Assert.IsType<ASArray>(parsedObject);

            Assert.Equal((uint)expectedArray.Length, parsedArray.length);

            for (int i = 0; i < expectedArray.Length; i++)
                AssertHelper.valueIdentical(expectedArray[i], parsedArray[i]);
        }

        public static IEnumerable<object[]> parseTestData_simpleObjects = TupleHelper.toArrays<StringWrapper, (StringWrapper, ASAny)[]>(
            ("{}", Array.Empty<(StringWrapper, ASAny)>()),
            ("{  }", Array.Empty<(StringWrapper, ASAny)>()),

            (
                "{\"a\":1}",
                new (StringWrapper, ASAny)[] {("a", 1)}
            ),
            (
                "{ \"a\": 1 }",
                new (StringWrapper, ASAny)[] {("a", 1)}
            ),
            (
                "{\"\": \"\"}",
                new (StringWrapper, ASAny)[] {("", "")}
            ),
            (
                "{\" \\n\\u0002 \": true}",
                new (StringWrapper, ASAny)[] {(" \n\u0002 ", true)}
            ),
            (
                "{ \"12\": 12.4992e+07 }",
                new (StringWrapper, ASAny)[] {("12", 12.4992e+07)}
            ),
            (
                "{\"a\":1,\"b\":2,\"c\":3}",
                new (StringWrapper, ASAny)[] {("a", 1), ("b", 2), ("c", 3)}
            ),
            (
                "{ \"a\": 1, \"b\": 2, \"c\": 3 }",
                new (StringWrapper, ASAny)[] {("a", 1), ("b", 2), ("c", 3)}
            ),
            (
                "{\"0\":null, \"1\":true, \"2\": false, \"3\":-1938422, \"4\":\"\", \"5\":\"hello!\\n\"}",
                new (StringWrapper, ASAny)[] {("0", NULL), ("1", true), ("2", false), ("3", -1938422), ("4", ""), ("5", "hello!\n")}
            ),
            (
                "{ \"a\": 1, \"a\\u0000\": 2, \" a \": 3 }",
                new (StringWrapper, ASAny)[] {("a", 1), ("a\0", 2), (" a ", 3)}
            ),
            (
                "{\"\\ud801\": \"\\udf01\", \"\ud815\": \"\ud801\\udf01\"}",
                new (StringWrapper, ASAny)[] {("\ud801", "\udf01"), ("\ud815", "\ud801\udf01")}
            ),
            (
                "{\"abc\": true, \"abc\": false}",
                new (StringWrapper, ASAny)[] {("abc", false)}
            ),
            (
                "{\"abc\": 100, \"def\": 200, \"a\\u0062c\": 300, \"g\\u0068\\u0069\": \"400\"}",
                new (StringWrapper, ASAny)[] {("abc", 300), ("def", 200), ("ghi", "400")}
            ),
            (
                " \r {\n\"0\"\t:\nnull,\r\t\"1\"\n:true\r\n,\t\"2\":\nfalse,\"3\":\t \r  -1938422,\"4\"\n\n\n:\"\",\t\t\r \n\"5\":\"hello!\\n\"\n} \r",
                new (StringWrapper, ASAny)[] {("0", NULL), ("1", true), ("2", false), ("3", -1938422), ("4", ""), ("5", "hello!\n")}
            )
        );

        [Theory]
        [MemberData(nameof(parseTestData_simpleObjects))]
        public void parseTest_simpleObjects(StringWrapper jsonStr, (StringWrapper name, ASAny value)[] expectedProps) {
            ASObject parsedObject = ASJSON.parse(jsonStr.value);
            Assert.IsType<ASObject>(parsedObject);

            DynamicPropertyCollection dynProps = parsedObject.AS_dynamicProps;
            Assert.Equal(expectedProps.Length, dynProps.count);

            for (int i = 0; i < expectedProps.Length; i++) {
                var (name, value) = expectedProps[i];
                AssertHelper.valueIdentical(value, dynProps[name.value]);
                Assert.True(dynProps.isEnumerable(name.value));
            }
        }

        public static IEnumerable<object[]> parseTestData_complexObjects = TupleHelper.toArrays<StringWrapper, ASObject>(
            (
                "[[[]]]",
                makeArray(makeArray(makeArray()))
            ),
            (
                "[ [\n ]\t, [\r \n],\t [\n\n [ [\r ]]   ,\t[\n \n], [ \t\r\t]],[[\n[],\n []  ]\n, [] ] \r\r\n]  \n\t",
                makeArray(
                    makeArray(),
                    makeArray(),
                    makeArray(makeArray(makeArray()), makeArray(), makeArray()),
                    makeArray(makeArray(makeArray(), makeArray()), makeArray())
                )
            ),
            (
                "[[[1,2,3],[4,5,6],[7,8,9]],[[10,11,12],[13,14,15],[16,17,18]],[[19,20,21],[22,23,24],[25,26,27]]]",
                makeArray(
                    makeArray(makeArray(1, 2, 3), makeArray(4, 5, 6), makeArray(7, 8, 9)),
                    makeArray(makeArray(10, 11, 12), makeArray(13, 14, 15), makeArray(16, 17, 18)),
                    makeArray(makeArray(19, 20, 21), makeArray(22, 23, 24), makeArray(25, 26, 27))
                )
            ),
            (
                Enumerable.Range(0, 50).Aggregate(
                    "null",
                    (cur, val) => String.Format("[{0}, {1}]", val.ToString(CultureInfo.InvariantCulture), cur)
                ),
                Enumerable.Range(0, 50).Aggregate((ASObject)null, (cur, val) => makeArray(val, cur))
            ),
            (
                " [\n   [1, \"\", 2 ], [\"hello\", null, \t [1.00292, -5E+1000, \"3324\", [true, false, [null]], [] ], \"abc\", [] ], 34.38813, \n \"kk138&^32\\u218D\\n\", [  3, 4,  \"56\", true, [ null ], [false,1],\n null], true \r]\n",
                makeArray(
                    makeArray(1, "", 2),
                    makeArray(
                        "hello",
                        NULL,
                        makeArray(1.00292, Double.NegativeInfinity, "3324", makeArray(true, false, makeArray(NULL)), makeArray()),
                        "abc",
                        makeArray()
                    ),
                    34.38813,
                    "kk138&^32\u218D\n",
                    makeArray(3, 4, "56", true, makeArray(NULL), makeArray(false, 1), NULL),
                    true
                )
            ),
            (
                "{\"a\":{\"x\":1,\"y\":2,\"z\":3},\"b\":{\"x\":10,\"y\":20,\"z\":30},\"c\":{\"x\":100,\"y\":200,\"z\":300}}",
                makeObject(
                    ("a", makeObject(("x", 1), ("y", 2), ("z", 3))),
                    ("b", makeObject(("x", 10), ("y", 20), ("z", 30))),
                    ("c", makeObject(("x", 100), ("y", 200), ("z", 300)))
                )
            ),
            (
                "{\"a\":{},\"b\":{\"a\":{\"a\":{},\"b\":{\"a\":{}},\"c\":{}}},\"c\":{\"a\":{},\"b\":{},\"c\":{\"a\":{\"a\":{\"b\":{}}}}},\"1\":{}}",
                makeObject(
                    ("a", makeObject()),
                    ("b", makeObject(
                        ("a", makeObject(("a", makeObject()), ("b", makeObject(("a", makeObject()))), ("c", makeObject())))
                    )),
                    ("c", makeObject(
                        ("a", makeObject()),
                        ("b", makeObject()),
                        ("c", makeObject(("a", makeObject(("a", makeObject(("b", makeObject())))))))
                    )),
                    ("1", makeObject())
                )
            ),
            (
                " { \n\"a\" \r: {  }\t\n, \t\r\n\"b\"  :\t {\n\"a\"\r\t: {\n\"a\"\r:\n\n{},\n\"b\" : \r{\"a\":{\n }  }  \n , \"c\" \t: \n{  } }\r\n},\n\r \"c\"\t : \n{\"a\":{\n  \n},\"b\"\t:\n { }}\n}\n",
                makeObject(
                    ("a", makeObject()),
                    ("b", makeObject(
                        ("a", makeObject(("a", makeObject()), ("b", makeObject(("a", makeObject()))), ("c", makeObject())))
                    )),
                    ("c", makeObject(("a", makeObject()), ("b", makeObject())))
                )
            ),
            (
                Enumerable.Range(0, 50).Aggregate(
                    "null",
                    (cur, val) => String.Format("{{\"val\": {0}, \"next\": {1}}}", val.ToString(CultureInfo.InvariantCulture), cur)
                ),
                Enumerable.Range(0, 50).Aggregate((ASObject)null, (cur, val) => makeObject(("val", val), ("next", cur)))
            ),
            (
                @"[
                    [{ ""abc"": [100, 200], ""def"": ""gd7*\n6@#"" }, { ""j3v"": 2991023, "" j3v "": [null, {""a"": true, ""b"": []} ] }],
                    { ""4^#~"": ""\u67aa187&aQ"", ""gir8#$\uD80E"": [1, 183e-291, ""abc\u0000def"", false] },
                    ""fd89d7==d0/fg"",
                    { ""gf&*"": 1234, ""hg13"": [45, 12, true, {}, null], ""gf&*"": [null, null, -0, 123], ""fyr@\/"": ""9381938"" },
                    [12, {""qq"": 12, ""rr"": 12, ""ss"": [ ""____"" ], ""tt"": null, ""rr"": 17 }, [1,1,1]],
                    null,
                    true,
                    { ""<>??"": [[[]]], ""<>???"": [1,2,3], "" <>??"": [[4],[true],[{}, -4e30]] },
                    { ""abc\ndef"": 33, ""********************************************************************************"": 4444 }
                ]",
                makeArray(
                    makeArray(
                        makeObject(("abc", makeArray(100, 200)), ("def", "gd7*\n6@#")),
                        makeObject(("j3v", 2991023), (" j3v ", makeArray(NULL, makeObject(("a", true), ("b", makeArray())))))
                    ),
                    makeObject(("4^#~", "\u67aa187&aQ"), ("gir8#$\uD80E", makeArray(1, 183e-291, "abc\0def", false))),
                    "fd89d7==d0/fg",
                    makeObject(
                        ("gf&*", makeArray(NULL, NULL, NEG_ZERO, 123)),
                        ("hg13", makeArray(45, 12, true, makeObject(), NULL)),
                        ("fyr@/", "9381938")
                    ),
                    makeArray(
                        12,
                        makeObject(("qq", 12), ("rr", 17), ("ss", makeArray("____")), ("tt", NULL)),
                        makeArray(1, 1, 1)
                    ),
                    NULL,
                    true,
                    makeObject(
                        ("<>??", makeArray(makeArray(makeArray()))),
                        (" <>??", makeArray(makeArray(4), makeArray(true), makeArray(makeObject(), -4e+30))),
                        ("<>???", makeArray(1, 2, 3))
                    ),
                    makeObject(("abc\ndef", 33), ("********************************************************************************", 4444))
                )
            )
        );

        [Theory]
        [MemberData(nameof(parseTestData_complexObjects))]
        public void parseTest_complexObjects(StringWrapper jsonStr, ASObject expected) {
            validateStructuralEquality(expected, ASJSON.parse(jsonStr.value));
        }

        private class ReviverResult : ASObject {
            public readonly ASObject receiver;
            public readonly string key;
            public readonly ASObject value;

            public ReviverResult(ASObject receiver, string key, ASObject value) {
                this.receiver = receiver;
                this.key = key;
                this.value = value;
            }
        }

        public static IEnumerable<object[]> parseTestData_withReviver() {
            var testCases = new (StringWrapper, ASObject)[] {
                ("0", 0),
                ("123.456", 123.456),
                ("true", true),
                ("null", null),
                ("\"hello\"", "hello"),
                ("[]", makeArray()),
                ("[1,2,3,true,null,\"abcd\",0.0001]", makeArray(1, 2, 3, true, NULL, "abcd", 0.0001)),
                ("{}", makeObject()),
                ("{\"a\": 1, \"b\": true, \"c\": \"\"}", makeObject(("a", 1), ("b", true), ("c", "")))
            };

            return Enumerable.Empty<object[]>()
                .Concat(testCases.Select(x => TupleHelper.toArray(x)))
                .Concat(parseTestData_complexObjects);
        }

        [Theory]
        [MemberData(nameof(parseTestData_withReviver))]
        public void parseTest_withReviver(StringWrapper jsonStr, ASObject expected) {
            var reviver = new MockFunctionObject((obj, args) => {
                Assert.Equal(2, args.Length);
                Assert.IsType<ASString>(args[0].value);

                return new ReviverResult(obj.value, (string)args[0].value, args[1].value);
            });

            var rootResult = Assert.IsType<ReviverResult>(ASJSON.parse(jsonStr.value, reviver));

            Assert.Equal("", rootResult.key);
            Assert.Same(rootResult.value, rootResult.receiver.AS_getProperty("").value);

            validateStructuralEquality(
                expected,
                rootResult.value,
                unwrapper: (obj, key, value, expectedValue) => {
                    var reviverResult = Assert.IsType<ReviverResult>(value.value);
                    Assert.Same(obj.value, reviverResult.receiver);
                    Assert.Equal(key, reviverResult.key);
                    return reviverResult.value;
                }
            );
        }

        public static IEnumerable<object[]> parseTestData_deletePropsWithReviver = TupleHelper.toArrays<StringWrapper, string[], ASObject>(
            ("null", new[] {""}, null),
            ("true", new[] {""}, null),
            ("1234", new[] {""}, null),
            ("\"hello\"", new[] {""}, null),
            ("[1,2,3]", new[] {""}, null),
            ("{\"a\": 1, \"b\": 2}", new[] {""}, null),
            ("[[[]]]", new[] {""}, null),
            ("{\"a\":{\"a\":{\"a\":{}}}}", new[] {""}, null),

            ("null", new[] {"x"}, null),
            ("true", new[] {"x"}, true),
            ("1234", new[] {"x"}, 1234),
            ("\"hello\"", new[] {"x"}, "hello"),

            ("[1,2,3,4,5]", new[] {"0", "4"}, makeArray(NULL, 2, 3, 4, NULL)),
            ("[1,2,3,4,5]", new[] {"0", "1", "2", "3", "4", "5"}, makeArray(NULL, NULL, NULL, NULL, NULL)),
            ("[1,2,3,4,5]", new[] {"01"}, makeArray(1, 2, 3, 4, 5)),
            ("[1,2,3,4,5]", new[] {"x"}, makeArray(1, 2, 3, 4, 5)),

            ("{\"a\": 1, \"b\": 2, \"c\": 3}", new[] {"a"}, makeObject(("b", 2), ("c", 3))),
            ("{\"a\": 1, \"b\": 2, \"c\": 3}", new[] {"a", "c", "d"}, makeObject(("b", 2))),
            ("{\"a\": 1, \"b\": 2, \"c\": 3}", new[] {"a", "c", "b"}, makeObject()),

            (
                "[[1, 2], [null, 20], [30, 40, null], [[100], [200, 300], [400]]]",
                new[] {"0"},
                makeArray(NULL, makeArray(NULL, 20), makeArray(NULL, 40, NULL), makeArray(NULL, makeArray(NULL, 300), makeArray(NULL)))
            ),
            (
                "[[1, 2], [10, 20], [30, 40, 50], [[100], [200, 300], [400, 500, 600]]]",
                new[] {"2"},
                makeArray(makeArray(1, 2), makeArray(10, 20), NULL, makeArray(makeArray(100), makeArray(200, 300), NULL))
            ),

            (
                "{\"p1\": {\"ab\": 1, \"cd\": 2}, \"p2\": {\"ab\": 1, \"ef\": 2}, \"p3\": {\"cd\": 1, \"ef\": 2}}",
                new[] {"cd"},
                makeObject(
                    ("p1", makeObject(("ab", 1))),
                    ("p2", makeObject(("ab", 1), ("ef", 2))),
                    ("p3", makeObject(("ef", 2)))
                )
            ),
            (
                "{\"p1\": {\"ab\": null, \"cd\": 2}, \"p2\": {\"ab\": null, \"ef\": 2}, \"p3\": {\"cd\": 1, \"ef\": 2}}",
                new[] {"p2", "cd", "ef"},
                makeObject(("p1", makeObject(("ab", NULL))), ("p3", makeObject()))
            ),
            (
                @"[
                    {},
                    {""x"": 0},
                    {""y"": 0},
                    {""z"": 0},
                    {""x"": 0, ""y"": 0},
                    {""x"": 0, ""z"": 0},
                    {""y"": 0, ""z"": 0},
                    {""x"": [0, 0, 0], ""y"": [0, 0], ""z"": [0, 0, 0]}
                ]",
                new[] {"2", "z", "5"},
                makeArray(
                    makeObject(),
                    makeObject(("x", 0)),
                    NULL,
                    makeObject(),
                    makeObject(("x", 0), ("y", 0)),
                    NULL,
                    makeObject(("y", 0)),
                    makeObject(("x", makeArray(0, 0, NULL)), ("y", makeArray(0, 0)))
                )
            )
        );

        [Theory]
        [MemberData(nameof(parseTestData_deletePropsWithReviver))]
        public void parseTest_deletePropsWithReviver(StringWrapper jsonStr, string[] propsToDelete, ASObject expected) {
            var reviver = new MockFunctionObject((obj, args) => {
                var key = (string)args[0];
                return Array.FindIndex(propsToDelete, x => x == key) != -1 ? ASAny.undefined : args[1];
            });
            validateStructuralEquality(expected, ASJSON.parse(jsonStr.value, reviver));
        }

        [Fact]
        public void parseTest_deletePropsWithReviver_duplicatePropNames() {
            var jsonStr = "{\"a\": 123, \"b\": 456, \"a\": 789}";

            var reviver1 = new MockFunctionObject((obj, args) => ASAny.AS_strictEq(args[1], 789) ? ASAny.undefined : args[1]);
            var reviver2 = new MockFunctionObject((obj, args) => ASAny.AS_strictEq(args[1], 123) ? ASAny.undefined : args[1]);

            validateStructuralEquality(makeObject(("b", 456)), ASJSON.parse(jsonStr, reviver1));
            validateStructuralEquality(makeObject(("a", 789), ("b", 456)), ASJSON.parse(jsonStr, reviver2));
        }

        [Fact]
        public void parseTest_invalidNull() {
            AssertHelper.throwsErrorWithCode(ErrorCode.JSON_PARSE_INVALID_INPUT, () => ASJSON.parse(null));
        }

        public static IEnumerable<object[]> parseTestData_invalidEmptyString = TupleHelper.toArrays<StringWrapper>(
            "",
            "    ",
            " \t \n",
            "\n \r\t "
        );

        [Theory]
        [MemberData(nameof(parseTestData_invalidEmptyString))]
        public void parseTest_invalidEmptyString(StringWrapper jsonStr) {
            AssertHelper.throwsErrorWithCode(ErrorCode.JSON_PARSE_INVALID_INPUT, () => ASJSON.parse(jsonStr.value));
        }

        public static IEnumerable<object[]> parseTestData_invalidNumbers = TupleHelper.toArrays<StringWrapper>(
            "00",
            "+0",
            "01",
            "+1",
            "01234567",
            "-00",
            "-01",
            "-01234567",
            "+01234567",
            ".1",
            ".18377788391",
            "+.5043",
            "+0.5043",
            "-.5043",
            "0123e+4",
            "01.234E-7",
            "+1.234E-7",
            "+01.234E-7",
            "-01.234E-7",
            "-",
            "- 124",
            "1. 23",
            "1.23 e+5",
            " \n0124 ",
            " \r\t .488219",
            "  +232 ",
            "  -0232 ",
            "\n\n-.99999\n\n",
            "Infinity",
            "-Infinity",
            "NaN",
            "abcd",
            "0xabcd",
            "0x1234",
            "0x1234AB",
            "$12.34",
            "(12.34)"
        );

        [Theory]
        [MemberData(nameof(parseTestData_invalidNumbers))]
        public void parseTest_invalidNumbers(StringWrapper jsonStr) {
            AssertHelper.throwsErrorWithCode(ErrorCode.JSON_PARSE_INVALID_INPUT, () => ASJSON.parse(jsonStr.value));
        }

        public static IEnumerable<object[]> parseTestData_invalidStrings = TupleHelper.toArrays<StringWrapper>(
            "\"\0\"",
            "\"\u001F\"",
            "\"abcd\0\"",
            "\"abc\u0015d\"",
            "\"abcd\n\"",
            "\"abcd\r\"",
            "\"abc\tdef\"",
            "\"\\u0\"",
            "\"\\u12\"",
            "\"\\u1a5\"",
            "\"\\uG\"",
            "\"\\u1G\"",
            "\"\\u12g\"",
            "\"\\u1Ffg\"",
            "\"abc\\a\"",
            "\"abc\\v\"",
            "\"abc\\qd\"",
            "\"abc\\uG000d\"",
            "\"ABC\\n\\U123A\"",
            "\"",
            "\"abcd",
            "\"abc\\",
            "\"abc\\\"",
            " \n \"*&\\\"^(\\u0b4)\"  ",
            "\"abc\\\n\\",
            "\"abc\\\n\\\""
        );

        [Theory]
        [MemberData(nameof(parseTestData_invalidStrings))]
        public void parseTest_invalidStrings(StringWrapper jsonStr) {
            AssertHelper.throwsErrorWithCode(ErrorCode.JSON_PARSE_INVALID_INPUT, () => ASJSON.parse(jsonStr.value));
        }

        public static IEnumerable<object[]> parseTestData_invalidKeywords = TupleHelper.toArrays<StringWrapper>(
            "True",
            "TRUE",
            "False",
            "FALSE",
            "Null",
            "NULL",
            "undefined",
            "tru",
            "truee",
            "truf",
            "fals",
            "falsee",
            "falsg",
            "nul",
            "nulll",
            "nulk",
            "abc",
            "\t TRUE\n ",
            "\r nuLL  "
        );

        [Theory]
        [MemberData(nameof(parseTestData_invalidKeywords))]
        public void parseTest_invalidKeywords(StringWrapper jsonStr) {
            AssertHelper.throwsErrorWithCode(ErrorCode.JSON_PARSE_INVALID_INPUT, () => ASJSON.parse(jsonStr.value));
        }

        public static IEnumerable<object[]> parseTestData_invalidStringPropNames = TupleHelper.toArrays<StringWrapper>(
            "{\"\0\": 1}",
            "{\"a\": {\"a\": {\"\u001F\": null}}}",
            "{\"abcd\": 1, \"abcd\0\": 2}",
            "[[{\"abcd\": 1, \"abcd\n\": 2, \"_\": {\"abc\tdef\": 4}}]]",
            "{\"\\u0\": true}",
            "{\"\\u12\": true}",
            "{\"\\u1a5\": true}",
            "{\"\\uG\": true}",
            "{\"\\u1G\": true}",
            "{\"\\u12g\": true}",
            "{\"a\":{\"a\":{\"a\":{\"\\u1Ffg\": \"\"}}}}",
            "{\"a\":{\"a\":{\"a\":{\"abc\\v\": \"\"}}}}",
            "{\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\na\": true}"
        );

        [Theory]
        [MemberData(nameof(parseTestData_invalidStringPropNames))]
        public void parseTest_invalidStringPropNames(StringWrapper jsonStr) {
            AssertHelper.throwsErrorWithCode(ErrorCode.JSON_PARSE_INVALID_INPUT, () => ASJSON.parse(jsonStr.value));
        }

        public static IEnumerable<object[]> parseTestData_invalidComma = TupleHelper.toArrays<StringWrapper>(
            "[,]",
            "[,,,]",
            "[1, ]",
            "[,1]",
            "[1,2,3,]",
            "\n[1,\n 2,\n 3,\n]\n",
            "[,1,2,3]",
            "[1, 2, , 3]",
            "[[[[1],[2],[3]],[[4],[5],[6]],],]",
            "[[[[1],[2],[3]],[[4],[5,],[6]]]]",
            "[[[[1],[2,],[3]],[[4],[5],[6]]]]",
            "{,}",
            "{,,}",
            "{\"a\": 1,}",
            "{,\"a\": 1}",
            "{\"a\": 1, \"b\": 2, \"c\": 3,}",
            "\n{\"a\": 1,\n \"b\": 2,\n \"c\": 3,\n}\n",
            "{, \"a\": 1, \"b\": 2, \"c\": 3}",
            "{\"a\": 1, \"b\": 2, ,\"c\": 3}",
            "{\"a\": 1, \"b\": {\"a\": 1, \"b\": {\"a\": 1, \"b\": {\"a\": 1, \"b\": null}},}}",
            "{\"a\": 1, \"b\": [{\"a\": 1, \"b\": [{\"a\": 1, \"b\": [{\"a\": 1, \"b\": null},]}]}]}",
            "{\"a\": 1, \"b\": [{\"a\": 1, \"b\": [{\"a\": 1, \"b\": [{\"a\": 1, \"b\": null}]}],}]}",
            "{\"a\": 1, \"b\": [{\"a\": 1, \"b\": [{\"a\": 1, \"b\": [{\"a\": 1, \"b\": null}]}]},]}",
            "{\"a\": 1, \"b\": [{\"a\": 1, \"b\": [{\"a\": [1,], \"b\": [{\"a\": 1, \"b\": null}]}]}]},"
        );

        [Theory]
        [MemberData(nameof(parseTestData_invalidComma))]
        public void parseTest_invalidComma(StringWrapper jsonStr) {
            AssertHelper.throwsErrorWithCode(ErrorCode.JSON_PARSE_INVALID_INPUT, () => ASJSON.parse(jsonStr.value));
        }

        public static IEnumerable<object[]> parseTestData_invalidNesting = TupleHelper.toArrays<StringWrapper>(
            "[",
            "{",
            "]",
            "}",
            "[1,2,3",
            "[1,2,3,",
            "1,2,3]",
            "{\"a\":1,\"b\":2",
            "{\"a\":1,\"b\":2,",
            "\"a\":1,\"b\":2}",
            " [ 1, 2, 3  ",
            " 1, 2, 3 ]",
            " \"a\": 1 }",

            "[[[1,2,3],[4,5,6],[7,8,9]],[[10,11,12],[13,14,15],[16,17,18],[[19,20,21],[22,23,24],[25,26,27]]]",
            "[[[1,2,3],[4,5,6],[7,8,9]],[[10,11,12],[13,14,15],[16,17,18]],[[19,20,21],[22,23,24],[25,26,27]]",
            "[[[1,2,3],[4,5,6],[7,8,9,[[10,11,12],[13,14,15],[16,17,18]],[[19,20,21],[22,23,24],[25,26,27]]]",
            "[[[1,2,3],[4,5,6],[7,8,9]],[10,11,12],[13,14,15],[16,17,18]],[[19,20,21],[22,23,24],[25,26,27]]]",
            "[1,2,3],[4,5,6],[7,8,9]],[[10,11,12],[13,14,15],[16,17,18]],[[19,20,21],[22,23,24],[25,26,27]]]",
            "[[[1,2,3],[4,5,6],[7,8,9]],[[10,11,12],[13,14,15],[16,17,18]],[[19,20,21],[22,23,24],25,26,27]]]",

            "{\"a\":{},\"b\":{\"a\":{\"a\":{},\"b\":{\"a\":{}},\"c\":{}}},\"c\":{\"a\":{},\"b\":{},\"c\":{\"a\":{\"a\":{\"b\":{}}},\"1\":{}}",
            "{\"a\":{},\"b\":{\"a\":{\"a\":{},\"b\":{\"a\":{}},\"c\":{}}},\"c\":{\"a\":{},\"b\":1},\"c\":{\"a\":{\"a\":{\"b\":{}}}}},\"1\":{}}",

            "[}",
            "{]",
            "[1,2,3}",
            "{1,2,3]",
            "{\"a\": 1, \"b\": 2]",
            "[\"a\": 1, \"b\": 2}",
            "{\"a\": [1, 2, {\"b\": 3}}]",
            "{\"a\": [1, 2, {\"b\": 3]}}"
        );

        [Theory]
        [MemberData(nameof(parseTestData_invalidNesting))]
        public void parseTest_invalidNesting(StringWrapper jsonStr) {
            AssertHelper.throwsErrorWithCode(ErrorCode.JSON_PARSE_INVALID_INPUT, () => ASJSON.parse(jsonStr.value));
        }

        public static IEnumerable<object[]> parseTestData_invalidSyntax = TupleHelper.toArrays<StringWrapper>(
            "\"a\": 1",
            "[\"a\": 1]",
            "{1}",
            "{1, 2, 3}",
            "{\"a\"",
            "{\"a\"}",
            "{\"a\", \"b\", \"c\"}",
            "(1)",
            "(1, 2, 3)",
            "(\"a\": 1, \"b\": 2)",
            "[1, 2, 3)",
            "{\"a\": 1, \"b\": 2)",
            "[1, 2, /*???*/ 3]",
            "[1, 2, 3, //xyz\n 4]",
            "'hello'",
            "{'a': 1, 'b': 2, 'c': 3}",
            "{\"a\": 1, \"b: 2, \"c\": 3}",
            "???",
            "123 456",
            "123,456",
            "true, false",
            "[1, 2, 3] 4",
            "{\"a: 1, \"b\": 2}, {\"a\": 1, \"b\": 2}"
        );

        [Theory]
        [MemberData(nameof(parseTestData_invalidSyntax))]
        public void parseTest_invalidSyntax(StringWrapper jsonStr) {
            AssertHelper.throwsErrorWithCode(ErrorCode.JSON_PARSE_INVALID_INPUT, () => ASJSON.parse(jsonStr.value));
        }

        private static IEnumerable<ASObject> generateEquivalentSpaceArgs(string spaceStr) {
            var list = new List<ASObject>();

            list.Add(spaceStr);

            if (spaceStr.Length == 0) {
                list.Add(null);
                list.Add(0);
                list.Add(0.9);
                list.Add(-1);
                list.Add(Double.NaN);
                list.Add(new ASObject());
                return list;
            }

            if (spaceStr.Length == 10) {
                list.Add(spaceStr + " ");
                list.Add(spaceStr + new string('x', 20));
            }

            if (spaceStr.Any(x => x != ' '))
                return list;

            list.Add(spaceStr.Length);
            list.Add(spaceStr.Length + 0.6);

            if (spaceStr.Length > 10) {
                list.Add(spaceStr.Length + 1);
                list.Add((double)UInt32.MaxValue + 1.0);
                list.Add(Double.PositiveInfinity);
            }

            return list;
        }

        private static IEnumerable<ASArray> generateEquivalentFilterArgs(string[] filter) {
            var list = new List<ASArray>();

            list.Add(ASArray.fromEnumerable(filter));

            list.Add(ASArray.fromEnumerable(
                filter.Select(x =>
                    ASNumber.AS_convertString(ASString.AS_toNumber(x)) == x
                        ? (ASAny)ASString.AS_toNumber(x)
                        : (ASAny)x
                )
            ));

            list.Add(ASArray.fromEnumerable(
                filter.Select(x => (ASAny)x).Concat(new ASAny[] {
                    NULL,
                    UNDEF,
                    true,
                    new ASArray(new ASAny[] {"x"}),
                    new ConvertibleMockObject(stringValue: "x")
                })
            ));

            return list;
        }

        private const string JSON_STRING_REGEX_PATTERN = @"""(?>[^""\\]|\\[^u]|\\u(?!000[89acd])[0-9a-f]{4})*""";

        private static readonly Regex s_jsonSingleLineRegex = new Regex($@"^(?>{JSON_STRING_REGEX_PATTERN}|[^""\s]+)+$");

        private static readonly Regex s_jsonPrettyPrintLineRegex = new Regex(
            $@"^(?:
                (?:{JSON_STRING_REGEX_PATTERN}:\ )? (?: (?:{JSON_STRING_REGEX_PATTERN}|[0-9a-z\.\+\-]+|\[\]|\{{\}}) ,? | [\{{\[])
                | [\}}\]],?
            )$",
            RegexOptions.IgnorePatternWhitespace
        );

        private static string validateJSONFormatAndRemoveIndents(string jsonStr, string spaceStr) {
            if (spaceStr.Length == 0) {
                Assert.Matches(s_jsonSingleLineRegex, jsonStr);
                return jsonStr;
            }

            string[] lines = jsonStr.Split('\n');
            int indentLevel = 0;

            for (int i = 0; i < lines.Length; i++) {
                Assert.True(indentLevel >= 0);

                string indentStr = "";
                if (indentLevel > 0)
                    indentStr = String.Concat(Enumerable.Repeat(spaceStr, indentLevel));

                string line = lines[i];
                Assert.StartsWith(indentStr, line, StringComparison.Ordinal);

                string lineWithoutIndent = line.Substring(indentStr.Length);
                Assert.Matches(s_jsonPrettyPrintLineRegex, lineWithoutIndent);

                char lineLastChar = lineWithoutIndent[lineWithoutIndent.Length - 1];
                if (lineLastChar == '{' || lineLastChar == '[')
                    indentLevel++;
                else if (lineLastChar != ',')
                    indentLevel--;

                lines[i] = lineWithoutIndent;
            }

            return String.Concat(lines);
        }

        public static IEnumerable<object[]> stringifyTestData_primitives = TupleHelper.toArrays<ASObject, StringWrapper>(
            (null, "null"),
            (true, "true"),
            (false, "false"),

            (0, "0"),
            (NEG_ZERO, "0"),
            (123, "123"),
            (-492811, "-492811"),
            (1.349599216, "1.349599216"),
            (2147483647, "2147483647"),
            (3000000000u, "3000000000"),
            (4294967295u, "4294967295"),
            (9007199254740992, "9007199254740992"),
            (-288230376151711800, "-288230376151711800"),
            (0.0001243329, "0.0001243329"),
            (1E+20, "100000000000000000000"),
            (1E+21, "1e+21"),
            (-1E+21, "-1e+21"),
            (1E-6, "0.000001"),
            (4.5619E-6, "0.0000045619"),
            (1E-7, "1e-7"),
            (-3.556E-7, "-3.556e-7"),
            (Double.MaxValue, "1.7976931348623157e+308"),
            (-Double.Epsilon, "-4.9406564584124654e-324"),
            (Double.PositiveInfinity, "null"),
            (Double.NegativeInfinity, "null"),
            (Double.NaN, "null"),

            ("", "\"\""),
            ("abc", "\"abc\""),
            ("abc def", "\"abc def\""),
            ("abc\ndef", "\"abc\\ndef\""),
            ("abc\\ndef", "\"abc\\\\ndef\""),
            ("\"abc/def\\ghi'123", "\"\\\"abc/def\\\\ghi'123\""),
            ("\0\f\n\r\t\b\v\u001f\u007f\u00f3\u24d3", "\"\\u0000\\f\\n\\r\\t\\b\\u000b\\u001f\u007f\u00f3\u24d3\""),
            ("\ud800\udd8f\udc7a\uda24x\ud805y\udf3c\ud801aa", "\"\ud800\udd8f\udc7a\uda24x\ud805y\udf3c\ud801aa\""),

            (
                String.Concat(Enumerable.Repeat("aa\n25\0", 200)),
                "\"" + String.Concat(Enumerable.Repeat("aa\\n25\\u0000", 200)) + "\""
            ),

            (ASFunction.createEmpty(), "null")
        );

        [Theory]
        [MemberData(nameof(stringifyTestData_primitives))]
        public void stringifyTest_primitives(ASObject value, StringWrapper expectedJson) {
            var spaces = new string[] { "", "          " };
            foreach (ASObject spaceArg in spaces.SelectMany(generateEquivalentSpaceArgs))
                Assert.Equal(expectedJson.value, ASJSON.stringify(value, replacer: null, spaceArg));
        }

        public static IEnumerable<object[]> stringifyRoundTripTestData_simpleArrays() {
            var arrays = new ASObject[] {
                makeArray(),
                makeArray(123),
                makeArray(NULL),
                makeArray("&D8fd91182:'??DS&*@!))@((@9"),
                makeArray(1, 2, 3, 4, 5.03221, -1),
                makeArray("", "abc", "hello", "1234", "null", "true"),
                makeArray("abc\ndef\0g\t__\r\b8\f", "&*^^!@\v43\af"),
                makeArray(ASFunction.createEmpty()),
                makeArray(1, 2, NULL, "abc", ASFunction.createEmpty(), 6.93424, true, ASFunction.createEmpty()),

                makeArray(
                    1,
                    NULL,
                    true,
                    1.230021192,
                    Double.PositiveInfinity,
                    -4000000,
                    UNDEF,
                    false,
                    NULL,
                    "hello",
                    "19999999",
                    Double.NaN,
                    Double.MaxValue,
                    true,
                    Double.NegativeInfinity
                ),

                makeVector<int>(),
                makeVector<double>(),
                makeVector<ASObject>(),

                makeVector<int>(1),
                makeVector<uint>(2000),
                makeVector<int>(1, 2, 3, 4, 5, 6),
                makeVector<double>(1.0, -4.5, Double.PositiveInfinity, 6.3, 7.1, Double.NaN, 4.3321e-39),
                makeVector<string>("hello", "abc\ndef"),
                makeVector<bool>(true, false, false, true, false),

                makeVector<ASObject>(
                    1,
                    null,
                    true,
                    1.230021192,
                    Double.PositiveInfinity,
                    -4000000,
                    false,
                    null,
                    "hello",
                    "19999999",
                    Double.NaN,
                    Double.MaxValue,
                    true,
                    Double.NegativeInfinity
                )
            };

            var spaces = new string[] {"", " ", "    ", "\t", "####", "          "};

            foreach (var array in arrays) {
                foreach (var space in spaces)
                    yield return new object[] {array, space};
            }
        }

        public static IEnumerable<object[]> stringifyRoundTripTestData_simpleObjects() {
            var objects = new ASObject[] {
                makeObject(("a", 1)),
                makeObject(("", "")),
                makeObject((" \n\u0002 ", true)),
                makeObject(("12", 12.4992e+07)),
                makeObject(("a", 1), ("b", 2), ("c", 3)),
                makeObject(("0", NULL), ("1", true), ("2", false), ("3", -1938422), ("4", ""), ("5", "hello!\n")),
                makeObject(("a", 1), ("a\0", 2), (" a ", 3)),
                makeObject(("\ud801", "\udf01"), ("\ud815", "\ud801\udf01")),
                makeObject(("abc", false)),
                makeObject(("abc", 300), ("def", 200), ("ghi", "400")),
                makeObject(("0", NULL), ("1", true), ("2", false), ("3", -1938422), ("4", ""), ("5", "hello!\n")),
                makeObject(("x", ASFunction.createEmpty())),
                makeObject(("abc", 1), ("def", ASFunction.createEmpty()), ("ghi", 34), ("xyz", UNDEF)),
                makeObject(("a", 1), ("b", 2), ("c", Double.PositiveInfinity), ("d", 0.4432), ("e", Double.NegativeInfinity), ("g", Double.NaN)),
            };

            var spaces = new string[] {"", " ", "    ", "\t", "####", "          "};

            foreach (var obj in objects) {
                foreach (var space in spaces)
                    yield return new object[] {obj, space};
            }
        }

        public static IEnumerable<object[]> stringifyRoundTripTestData_complexObjects() {
            var objects = new ASObject[] {
                makeArray(makeArray(makeArray())),

                makeArray(
                    makeArray(),
                    makeArray(),
                    makeArray(makeArray(makeArray()), makeArray(), makeArray()),
                    makeArray(makeArray(makeArray(), makeArray()), makeArray())
                ),

                makeArray(
                    makeArray(makeArray(1, 2, 3), makeArray(4, 5, 6), makeArray(7, 8, 9)),
                    makeArray(makeArray(10, 11, 12), makeArray(13, 14, 15), makeArray(16, 17, 18)),
                    makeArray(makeArray(19, 20, 21), makeArray(22, 23, 24), makeArray(25, 26, 27))
                ),

                makeVector(
                    makeVector(makeVector<int>(1, 2, 3), makeVector<int>(4, 5, 6), makeVector<int>(7, 8, 9)),
                    makeVector(makeVector<int>(10, 11, 12), makeVector<int>(13, 14, 15), makeVector<int>(16, 17, 18)),
                    makeVector(makeVector<int>(19, 20, 21), makeVector<int>(22, 23, 24), makeVector<int>(25, 26, 27))
                ),

                makeArray(
                    makeArray(1, "", 2),
                    makeArray(
                        "hello",
                        NULL,
                        makeArray(1.00292, Double.NegativeInfinity, "3324", makeArray(true, false, makeArray(NULL)), makeArray()),
                        "abc",
                        makeArray()
                    ),
                    34.38813,
                    "kk138&^32\u218D\n",
                    makeArray(3, 4, "56", true, makeArray(NULL), makeArray(false, UNDEF, 1), NULL),
                    true
                ),

                makeObject(
                    ("a", makeObject(("x", 1), ("y", 2), ("z", 3))),
                    ("b", makeObject(("x", 10), ("y", 20), ("z", 30))),
                    ("c", makeObject(("x", 100), ("y", 200), ("z", 300)))
                ),

                makeObject(
                    ("a", makeObject()),
                    ("b", makeObject(
                        ("a", makeObject(("a", makeObject()), ("b", makeObject(("a", makeObject()))), ("c", makeObject())))
                    )),
                    ("c", makeObject(
                        ("a", makeObject()),
                        ("b", makeObject()),
                        ("c", makeObject(("a", makeObject(("a", makeObject(("b", makeObject(("a", UNDEF)))))))))
                    )),
                    ("1", makeObject())
                ),

                makeObject(
                    ("a", makeObject()),
                    ("b", makeObject(
                        ("a", makeObject(("a", makeObject()), ("b", makeObject(("a", makeObject()))), ("c", makeObject())))
                    )),
                    ("c", makeObject(("a", makeObject()), ("b", makeObject())))
                ),

                makeArray(
                    makeArray(
                        makeObject(
                            ("abc", makeArray(100, Double.PositiveInfinity, 200)),
                            ("def", "gd7*\n6@#"),
                            ("ghi", ASFunction.createEmpty())
                        ),
                        makeObject((
                            "j3v", 2991023),
                            (" j3v ", makeArray(NULL, makeObject(("a", true), ("b", UNDEF), ("c", makeArray()))))
                        )
                    ),
                    makeObject(
                        ("4^#~", "\u67aa187&aQ"),
                        ("gir8#$\uD80E", makeArray(1, 183e-291, "abc\0def", false)),
                        ("g%4&~:", Double.NaN)
                    ),
                    "fd89d7==d0/fg",
                    makeObject(
                        ("gf&*", makeArray(NULL, NULL, NEG_ZERO, 123)),
                        ("hg13", makeArray(45, 12, true, ASFunction.createEmpty(), UNDEF, makeObject(), NULL, 45.1, Double.NaN)),
                        ("fyr@/", "9381938")
                    ),
                    makeArray(
                        12,
                        makeObject(("qq", 12), ("rr", 17), ("ss", makeArray("____")), ("tt", NULL), ("uu", ASFunction.createEmpty())),
                        makeArray(1, 1, 1)
                    ),
                    NULL,
                    true,
                    makeObject(
                        ("<>??", makeArray(makeArray(makeArray()))),
                        (" <>??", makeArray(makeArray(4), makeArray(true), makeArray(makeObject(), -4e+30))),
                        ("<>???", makeArray(1, 2, 3)),
                        ("<??", UNDEF)
                    ),
                    makeObject(("abc\ndef", 33), ("********************************************************************************", 4444))
                ),

                makeArray(
                    makeVector(
                        makeArray(makeVector<int>(100, 200, -300), makeVector<double>(0.5, -1.03, 2.35e+40)),
                        makeArray(makeVector("abc", "hello", "\u2019\u2a6d"), makeVector<ASObject>(null, makeObject(), 4, true))
                    ),
                    makeVector<bool>(true, false, true, false),
                    makeArray(10, 93, 173, -193.21),
                    makeVector(
                        makeObject(("a", 1), ("b", 2), ("c", 6)),
                        makeObject(("a", 1), ("b", NULL), ("c", Double.NaN), ("d", UNDEF), ("g", new MockFunctionObject())),
                        new MockFunctionObject(),
                        makeObject(("a", makeVector<int>(1)), ("b", UNDEF), ("c", makeVector<int>(2, 3, 4)), ("d", makeArray())),
                        makeVector<ASVectorAny>(makeVector<int>(1, 2), makeVector("...", "???"), makeVector<double>(35.116, Double.NegativeInfinity))
                    )
                ),

                Enumerable.Range(0, 50).Aggregate((ASObject)null, (cur, val) => makeArray(val, cur)),
                Enumerable.Range(0, 50).Aggregate((ASObject)null, (cur, val) => makeObject(("val", val), ("next", cur))),
            };

            var spaces = new string[] {"", "    ", "\t", "1234", "          "};

            foreach (var obj in objects) {
                foreach (var space in spaces)
                    yield return new object[] {obj, space};
            }
        }

        [Theory]
        [MemberData(nameof(stringifyRoundTripTestData_simpleArrays))]
        [MemberData(nameof(stringifyRoundTripTestData_simpleObjects))]
        [MemberData(nameof(stringifyRoundTripTestData_complexObjects))]
        public void stringifyRoundTripTest(ASObject obj, string spaceStr) {
            foreach (ASObject spaceArg in generateEquivalentSpaceArgs(spaceStr)) {
                string json = ASJSON.stringify(obj, replacer: null, spaceArg);
                json = validateJSONFormatAndRemoveIndents(json, spaceStr);
                validateStructuralEquality(obj, ASJSON.parse(json), roundtripMode: true);
            }
        }

        public static IEnumerable<object[]> stringifyRoundTripTestData_objectsWithTraits() {
            var appDomain = new ApplicationDomain();

            var classA = new MockClass(
                name: "A",
                domain: appDomain,

                fields: new[] {
                    new MockFieldTrait(
                        name: "a12z9",
                        getValueFunc: obj => 149.322917,
                        setValueFunc: (obj, val) => throw new Exception("Field should not be written!")
                    ),
                    new MockFieldTrait(
                        name: "",
                        getValueFunc: obj => makeArray(1, 2, makeObject(("_X", 1), ("_Y", -29))),
                        setValueFunc: (obj, val) => throw new Exception("Field should not be written!")
                    ),

                    new MockFieldTrait(name: "__fgz!!", isReadOnly: true, getValueFunc: obj => "hello \"me\""),
                    new MockFieldTrait(name: "fg11*", isReadOnly: true, getValueFunc: obj => UNDEF),
                    new MockFieldTrait(name: "kk&d4", isReadOnly: true, getValueFunc: obj => ASFunction.createEmpty()),
                }
            );

            var classB = new MockClass(
                name: new QName(new Namespace(NamespaceKind.PACKAGE_INTERNAL, ""), "B"),
                domain: appDomain,

                properties: new[] {
                    new MockPropertyTrait(
                        name: "a12z9",
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => true)
                    ),
                    new MockPropertyTrait(
                        name: "&fyi<\u00D9\0",
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => -92443),
                        setter: new MockMethodTrait(invokeFunc: (obj, args) => throw new Exception("Setter should never be called!"))
                    ),
                    new MockPropertyTrait(
                        name: "hllq~",
                        setter: new MockMethodTrait(invokeFunc: (obj, args) => throw new Exception("Setter should never be called!"))
                    ),
                    new MockPropertyTrait(
                        name: "gt112",
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => ASFunction.createEmpty())
                    ),
                    new MockPropertyTrait(
                        name: "(ddfs)",
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => UNDEF)
                    ),
                    new MockPropertyTrait(
                        name: "gdd^^7",
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => Double.PositiveInfinity)
                    ),
                    new MockPropertyTrait(
                        name: "2311839",
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => makeVector<uint>(1, 3000, 3000000000)),
                        setter: new MockMethodTrait(invokeFunc: (obj, args) => throw new Exception("Setter should never be called!"))
                    ),

                    new MockPropertyTrait(
                        name: "599424",
                        isStatic: true,
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => makeArray(1, -1, 1, -1, 1, -1))
                    )
                }
            );

            var classC = new MockClass(
                name: "C",
                domain: appDomain,

                fields: new[] {
                    new MockFieldTrait(name: "abc_1", getValueFunc: obj => 1.38888193e+23),
                    new MockFieldTrait(name: new QName("__", "abc_2"), getValueFunc: obj => "!!!!"),

                    new MockFieldTrait(name: "abc_1", isStatic: true, getValueFunc: obj => 6533217),
                    new MockFieldTrait(name: "abc_2", isStatic: true, getValueFunc: obj => "H111"),
                    new MockFieldTrait(name: new QName("__", "abc_3"), isStatic: true, getValueFunc: obj => "_(^%$$$)@")
                },
                properties: new[] {
                    new MockPropertyTrait(
                        name: "abc_3",
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => makeArray(1, "44", false))
                    ),
                    new MockPropertyTrait(
                        name: "abc_4",
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => makeObject(("x_01", 113), ("x_02", 981)))
                    ),
                    new MockPropertyTrait(
                        name: new QName(new Namespace(NamespaceKind.EXPLICIT, "_a"), "abc_4"),
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => 0)
                    ),

                    new MockPropertyTrait(
                        name: "abc_5",
                        isStatic: true,
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => makeObject())
                    ),
                    new MockPropertyTrait(
                        name: new QName(new Namespace(NamespaceKind.STATIC_PROTECTED, "_a"), "abc_6"),
                        isStatic: true,
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => makeVector("", "a", "aa", "aaa"))
                    ),
                },
                methods: new[] {
                    new MockMethodTrait(
                        name: "foo_1",
                        invokeFunc: (obj, args) => throw new Exception("Method should not be called!")
                    ),
                    new MockMethodTrait(
                        name: new QName("_a", "foo_2"),
                        invokeFunc: (obj, args) => throw new Exception("Method should not be called!")
                    ),
                    new MockMethodTrait(
                        name: "foo_3",
                        isStatic: true,
                        invokeFunc: (obj, args) => throw new Exception("Method should not be called!")
                    ),
                }
            );

            classC.prototypeObject.AS_setProperty("proto_1", 1000);
            classC.prototypeObject.AS_setProperty("proto_2", 2000);

            var classD = new MockClass(
                name: "D",
                domain: appDomain,
                parent: classC,
                isDynamic: true,

                fields: new[] {
                    new MockFieldTrait(
                        name: "def_1",
                        isReadOnly: true,
                        getValueFunc: obj => makeVector(true, false, false, true, true, false)
                    ),
                    new MockFieldTrait(
                        name: new QName(new Namespace(NamespaceKind.PROTECTED, ""), "def_2"),
                        getValueFunc: obj => 923117
                    ),

                    new MockFieldTrait(
                        name: "abc_1",
                        isStatic: true,
                        getValueFunc: obj => makeVector<int>(-293881127, -29183812, -9338826)
                    ),
                    new MockFieldTrait(
                        name: "def_1",
                        isReadOnly: true,
                        isStatic: true,
                        getValueFunc: obj => "K8&D{://d99d8s"
                    ),
                    new MockFieldTrait(
                        name: new QName(new Namespace(NamespaceKind.PROTECTED, ""), "def_2"),
                        isStatic: true,
                        getValueFunc: obj => makeArray(1, "f", 2, "g", 3)
                    ),
                },
                properties: new[] {
                    new MockPropertyTrait(
                        name: "abc_3",
                        getter: new MockMethodTrait(isOverride: true, invokeFunc: (obj, args) => "ajqndyyassif8s77%S$S87ds")
                    ),
                    new MockPropertyTrait(
                        name: new QName(new Namespace(NamespaceKind.EXPLICIT, "_a"), "abc_4"),
                        getter: new MockMethodTrait(isOverride: true, invokeFunc: (obj, args) => "ajqndyyassif8s77%S$S87ds")
                    ),
                    new MockPropertyTrait(
                        name: "def_3",
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => makeArray(
                            new MockClassInstance(classB),
                            new MockClassInstance(classC),
                            true,
                            makeObject(("fd7d6", 1), ("jghd85", "()"))
                        ))
                    ),
                    new MockPropertyTrait(
                        name: new QName(new Namespace(NamespaceKind.PACKAGE_INTERNAL, ""), "def_4"),
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => false)
                    ),

                    new MockPropertyTrait(
                        name: "def_7",
                        isStatic: true,
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => new MockClassInstance(classA))
                    ),
                    new MockPropertyTrait(
                        name: new QName(new Namespace(NamespaceKind.PACKAGE_INTERNAL, ""), "def_4"),
                        getter: new MockMethodTrait(invokeFunc: (obj, args) => makeArray(0, 0, 0, 0, 0, 0, 0, 0))
                    )
                },
                methods: new[] {
                    new MockMethodTrait(
                        name: "foo_1",
                        isOverride: true,
                        invokeFunc: (obj, args) => throw new Exception("Method should not be called!")
                    ),
                    new MockMethodTrait(
                        name: "foo_3",
                        invokeFunc: (obj, args) => throw new Exception("Method should not be called!")
                    ),
                    new MockMethodTrait(
                        name: "foo_9",
                        isStatic: true,
                        invokeFunc: (obj, args) => throw new Exception("Method should not be called!")
                    )
                }
            );

            classD.prototypeObject.AS_setProperty("proto_2", 2491);
            classD.prototypeObject.AS_setProperty("proto_3", 4381);

            var classD_object = new MockClassInstance(classD);
            classD_object.AS_setProperty("qq1@67", 1938217);
            classD_object.AS_setProperty("qq1@54", new MockClassInstance(classA));

            appDomain.tryDefineGlobalTrait(classA);
            appDomain.tryDefineGlobalTrait(classB);
            appDomain.tryDefineGlobalTrait(classC);
            appDomain.tryDefineGlobalTrait(new ClassAlias("_B", declClass: null, appDomain, classB, metadata: null));

            var globalTraits = new Trait[] {
                new MockFieldTrait(name: "global_1", isStatic: true, getValueFunc: obj => makeObject(("psq", "rjkdds9"))),
                new MockFieldTrait(name: new QName("x", "global_2"), isStatic: true, getValueFunc: obj => makeObject(("afs", makeObject()))),

                new MockPropertyTrait(
                    name: "global_3",
                    isStatic: true,
                    getter: new MockMethodTrait(invokeFunc: (obj, args) => makeArray(193.84772, Double.NaN, ASFunction.createEmpty()))
                ),
                new MockPropertyTrait(
                    name: "global_4",
                    isStatic: true,
                    getter: new MockMethodTrait(invokeFunc: (obj, args) => makeObject(
                        ("_p_1", classD_object),
                        ("_p_2", new MockFunctionObject())
                    ))
                ),
                new MockPropertyTrait(
                    name: new QName(new Namespace(NamespaceKind.PACKAGE_INTERNAL, ""), "global_5"),
                    isStatic: true,
                    getter: new MockMethodTrait(invokeFunc: (obj, args) => 0)
                ),
            };

            for (int i = 0; i < globalTraits.Length; i++)
                appDomain.tryDefineGlobalTrait(globalTraits[i]);

            appDomain.globalObject.AS_setProperty("_df9s88*", makeArray(NULL, 1, 2, true, NULL, 99));

            var classA_roundtripResult = makeObject(
                ("a12z9", 149.322917),
                ("__fgz!!", "hello \"me\""),
                ("", makeArray(1, 2, makeObject(("_X", 1), ("_Y", -29))))
            );

            var classB_roundtripResult = makeObject(
                ("a12z9", true),
                ("&fyi<\u00D9\0", -92443),
                ("gdd^^7", NULL),
                ("2311839", makeArray(1, 3000, 3000000000))
            );

            var classC_roundtripResult = makeObject(
                ("abc_1", 1.38888193e+23),
                ("abc_3", makeArray(1, "44", false)),
                ("abc_4", makeObject(("x_01", 113), ("x_02", 981)))
            );

            var classD_roundtripResult = makeObject(
                ("abc_1", 1.38888193e+23),
                ("abc_3", "ajqndyyassif8s77%S$S87ds"),
                ("abc_4", makeObject(("x_01", 113), ("x_02", 981))),
                ("def_1", makeArray(true, false, false, true, true, false)),
                ("def_3", makeArray(classB_roundtripResult, classC_roundtripResult, true, makeObject(("fd7d6", 1), ("jghd85", "()")))),
                ("qq1@67", 1938217),
                ("qq1@54", classA_roundtripResult)
            );

            var classA_staticRoundtripResult = makeObject(
                ("prototype", makeObject())
            );

            var classB_staticRoundtripResult = makeObject(
                ("prototype", makeObject()),
                ("599424", makeArray(1, -1, 1, -1, 1, -1))
            );

            var classC_staticRoundtripResult = makeObject(
                ("prototype", makeObject(("proto_1", 1000), ("proto_2", 2000))),
                ("abc_1", 6533217),
                ("abc_2", "H111"),
                ("abc_5", makeObject())
            );

            var classD_staticRoundtripResult = makeObject(
                ("prototype", makeObject(("proto_2", 2491), ("proto_3", 4381))),
                ("abc_1", makeArray(-293881127, -29183812, -9338826)),
                ("def_1", "K8&D{://d99d8s"),
                ("def_7", makeObject(
                    ("a12z9", 149.322917),
                    ("__fgz!!", "hello \"me\""),
                    ("", makeArray(1, 2, makeObject(("_X", 1), ("_Y", -29))))
                ))
            );

            var globalRoundtripResult = makeObject(
                ("A", classA_staticRoundtripResult),
                ("_B", classB_staticRoundtripResult),
                ("C", classC_staticRoundtripResult),
                ("global_1", makeObject(("psq", "rjkdds9"))),
                ("global_3", makeArray(193.84772, NULL, NULL)),
                ("global_4", makeObject(("_p_1", classD_roundtripResult))),
                ("_df9s88*", makeArray(NULL, 1, 2, true, NULL, 99))
            );

            var testcases = new (ASObject obj, ASObject parsedObj)[] {
                (new MockClassInstance(classA), classA_roundtripResult),
                (new MockClassInstance(classB), classB_roundtripResult),
                (new MockClassInstance(classC), classC_roundtripResult),
                (classD_object, classD_roundtripResult),
                (classA.classObject, classA_staticRoundtripResult),
                (classB.classObject, classB_staticRoundtripResult),
                (classC.classObject, classC_staticRoundtripResult),
                (classD.classObject, classD_staticRoundtripResult),
                (appDomain.globalObject, globalRoundtripResult)
            };

            var spaces = new[] {"", "    ", "\t"};

            foreach (var (obj, parsedObj) in testcases) {
                foreach (string space in spaces)
                    yield return new object[] {obj, space, parsedObj};
            }
        }

        [Theory]
        [MemberData(nameof(stringifyRoundTripTestData_objectsWithTraits))]
        public void stringifyRoundTripTest_objectsWithTraits(ASObject obj, string spaceStr, ASObject parsedObj) {
            foreach (ASObject spaceArg in generateEquivalentSpaceArgs(spaceStr)) {
                string json = ASJSON.stringify(obj, replacer: null, spaceArg);
                json = validateJSONFormatAndRemoveIndents(json, spaceStr);
                validateStructuralEquality(parsedObj, ASJSON.parse(json), roundtripMode: true);
            }
        }

        public static IEnumerable<object[]> stringifyRoundTripTestData_toJSONOverriddenOnPrimitives = new[] {new object[] {new ASObject[] {
            null,
            0,
            18993u,
            1.39984,
            Double.PositiveInfinity,
            Double.NaN,
            true,
            "hello",
            makeArray(1, 2, 3, 4),
            makeObject(("x", 1), ("y", 2)),

            makeArray(makeArray(), makeArray(1, 2), makeArray()),
            makeObject(("x", makeObject(("p1", "1"), ("p2", "2"))), ("y", makeArray(makeObject(("p1", "k")), makeObject(("p2", true))))),

            makeObject(("x1", makeVector<int>(1, 2, 3, 4)), ("y1", makeVector("a", "b", "c", "d"))),

            ASFunction.createEmpty(),
            makeObject(("x", ASFunction.createEmpty()))
        }}};

        [Theory]
        [MemberData(nameof(stringifyRoundTripTestData_toJSONOverriddenOnPrimitives))]
        public void stringifyRoundTripTest_toJSONOverriddenOnPrimitives(ASObject[] objects) {
            var primitiveClasses = new Class[] {
                Class.fromType(typeof(ASNumber)),
                Class.fromType(typeof(ASString)),
                Class.fromType(typeof(ASBoolean)),
                Class.fromType(typeof(ASObject)),
                Class.fromType(typeof(ASArray)),
                Class.fromType(typeof(ASVectorAny)),
                Class.fromType(typeof(ASFunction))
            };

            using (var zone = new StaticZone())
                zone.enterAndRun(runTest);

            void runTest() {
                for (int i = 0; i < primitiveClasses.Length; i++) {
                    string name = (primitiveClasses[i].underlyingType == typeof(ASVectorAny))
                        ? "Vector"
                        : primitiveClasses[i].name.localName;

                    primitiveClasses[i].prototypeObject.AS_setProperty(
                        "toJSON",
                        new MockFunctionObject((r, args) => {
                            if (ASAny.AS_strictEq(args[0], "_value") || ASAny.AS_strictEq(args[0], "_type"))
                                return r;

                            return makeObject(("_type", name), ("_value", r));
                        })
                    );
                }

                for (int i = 0; i < objects.Length; i++) {
                    string jsonStr = ASJSON.stringify(objects[i], replacer: null, space: "");
                    jsonStr = validateJSONFormatAndRemoveIndents(jsonStr, "");

                    ASObject actualRoot = ASJSON.parse(jsonStr);
                    checkType(objects[i], actualRoot);

                    if (actualRoot == null)
                        continue;

                    if (objects[i] is ASFunction) {
                        Assert.False(actualRoot.AS_hasProperty("_value"));
                        continue;
                    }

                    validateStructuralEquality(
                        objects[i],
                        actualRoot.AS_getProperty("_value"),
                        roundtripMode: true,
                        unwrapper: (obj, key, value, expectedValue) => {
                            checkType(expectedValue, value);
                            if (expectedValue.value is ASFunction) {
                                Assert.False(value.AS_hasProperty("_value"));
                                return NULL;
                            }
                            return value.AS_getProperty("_value");
                        }
                    );
                }
            }

            void checkType(ASAny expectedValue, ASAny actualValue) {
                if (expectedValue.isUndefinedOrNull) {
                    AssertHelper.identical(NULL, actualValue);
                    return;
                }

                Assert.IsType<ASObject>(actualValue.value);
                string type = (string)actualValue.AS_getProperty("_type");

                if (expectedValue.value is ASVectorAny)
                    Assert.Equal("Vector", type);
                else if (expectedValue.value is ASFunction)
                    Assert.Equal("Function", type);
                else if (ASObject.AS_isNumeric(expectedValue.value))
                    Assert.Equal("Number", type);
                else
                    Assert.Equal(expectedValue.AS_class.name.localName, type);
            }
        }

        public static IEnumerable<object[]> stringifyRoundTripTestData_toJSONOverridden() {
            ASObject testCase1 = null;
            testCase1 = makeObject(
                ("x", 1),
                ("y", 2),
                ("toJSON", new MockFunctionObject((obj, args) => {
                    Assert.Same(testCase1, obj.value);
                    Assert.Single(args);
                    Assert.Equal("", (string)Assert.IsType<ASString>(args[0].value));
                    return "hello";
                }))
            );
            ASObject testCase1_result = "hello";

            ASObject testCase2 = makeObject(("x", 1), ("y", 2));
            testCase2.AS_setProperty("toJSON", MockFunctionObject.withReturn(testCase2));
            ASObject testCase2_result = makeObject(("x", 1), ("y", 2));

            ASObject testCase3 = makeArray(1, 2, 3, 4, 5);
            testCase3.AS_dynamicProps.setValue("toJSON", new MockFunctionObject((obj, args) => {
                Assert.Same(testCase3, obj.value);
                Assert.Single(args);
                Assert.Equal("", (string)Assert.IsType<ASString>(args[0].value));
                return makeArray(10, 20, 30);
            }));
            ASObject testCase3_result = makeArray(10, 20, 30);

            ASObject testCase4 = null;
            ASObject testCase4_toJSON = new MockFunctionObject((recv, args) => {
                Assert.Single(args);
                string key = (string)Assert.IsType<ASString>(args[0].value);

                if (key.StartsWith('_'))
                    return ASAny.undefined;

                return (double)recv.AS_getProperty("_v") * 10;
            });
            testCase4 = makeObject(
                ("x", makeObject(("_v", 100), ("toJSON", testCase4_toJSON))),
                ("y", makeObject(("_v", 200), ("toJSON", testCase4_toJSON))),
                ("_x", makeObject(("_v", 300), ("toJSON", testCase4_toJSON))),
                ("_y", makeObject(("_v", 400), ("toJSON", testCase4_toJSON)))
            );
            ASObject testCase4_result = makeObject(("x", 1000), ("y", 2000));

            ASObject testCase5 = null;
            ASObject testCase5_toJSON = new MockFunctionObject((recv, args) => {
                Assert.Single(args);
                string key = (string)Assert.IsType<ASString>(args[0].value);
                Assert.Matches("^[0-9]+$", key);

                if (ASString.AS_toInt(key) % 2 == 0)
                    return ASAny.undefined;

                return (double)recv.AS_getProperty("_v") * 10;
            });
            testCase5 = makeArray(
                makeObject(("_v", 100), ("toJSON", testCase5_toJSON)),
                makeObject(("_v", 200), ("toJSON", testCase5_toJSON)),
                makeObject(("_v", 300), ("toJSON", testCase5_toJSON)),
                makeObject(("_v", 400), ("toJSON", testCase5_toJSON))
            );
            ASObject testCase5_result = makeArray(NULL, 2000, NULL, 4000);

            ASObject testCase6 = makeObject(
                ("x", 1),
                ("y", 2),
                ("toJSON", MockFunctionObject.withReturn(makeArray(
                    1231,
                    5942,
                    makeObject(("toJSON", MockFunctionObject.withReturn(8491))),
                    makeObject(("toJSON", MockFunctionObject.withReturn(NULL))),
                    makeObject(("toJSON", MockFunctionObject.withReturn(Double.PositiveInfinity))),
                    makeObject(
                        ("p", 1),
                        ("q", 2),
                        ("toJSON", MockFunctionObject.withReturn(
                            makeObject(
                                ("p", 1),
                                ("q", makeObject(("toJSON", MockFunctionObject.withReturn(2)))),
                                ("r", makeObject(("toJSON", MockFunctionObject.withReturn(ASAny.undefined)))),
                                ("s", makeObject(("toJSON", MockFunctionObject.withReturn(ASFunction.createEmpty())))),
                                ("toJSON", MockFunctionObject.withReturn(makeObject(("p", true))))
                            )
                        ))
                    )
                )))
            );
            ASObject testCase6_result = makeArray(
                1231,
                5942,
                8491,
                NULL,
                NULL,
                makeObject(("p", 1), ("q", 2))
            );

            ASObject testCase7 = null;
            ASObject testCase7_inner = makeObject();
            testCase7 = makeObject(
                ("x", 1),
                ("y", makeArray(
                    NULL,
                    makeObject(("x", testCase7_inner), ("toJSON", MockFunctionObject.withReturn(makeArray()))),
                    100
                ))
            );
            testCase7_inner.AS_setProperty("x", testCase7);
            ASObject testCase7_result = makeObject(("x", 1), ("y", makeArray(NULL, makeArray(), 100)));

            ASObject testCase8 = ASObject.AS_createWithPrototype(
                makeObject(("toJSON", MockFunctionObject.withReturn("Hello, John")))
            );
            ASObject testCase8_result = "Hello, John";

            ASObject testCase9 = new MockClassInstance(
                new MockClass(methods: new[] {
                    new MockMethodTrait(name: "toJSON", invokeFunc: (obj, args) => makeArray(10, -10, 4, "A"))
                })
            );
            ASObject testCase9_result = makeArray(10, -10, 4, "A");

            MockClass testCase10_class = new MockClass(
                methods: new[] {
                    new MockMethodTrait(name: "toJSON", invokeFunc: (obj, args) => {
                        int index = (int)args[0];
                        MockClass innerClass = new MockClass(
                            fields: new[] {
                                new MockFieldTrait(name: "index", getValueFunc: innerObj => index)
                            },
                            methods: new[] {
                                new MockMethodTrait(
                                    name: "toJSON",
                                    invokeFunc: (innerObj, innerArgs) => throw new Exception("This should not be called!")
                                )
                            }
                        );
                        return new MockClassInstance(innerClass);
                    })
                }
            );
            ASObject testCase10 = makeArray(
                makeArray(new MockClassInstance(testCase10_class), new MockClassInstance(testCase10_class)),
                makeVector<ASObject>(new MockClassInstance(testCase10_class), new MockClassInstance(testCase10_class))
            );
            ASObject testCase10_result = makeArray(
                makeArray(makeObject(("index", 0)), makeObject(("index", 1))),
                makeArray(makeObject(("index", 0)), makeObject(("index", 1)))
            );

            ASObject testCase11 = makeObject(("x", true), ("y", "hello"), ("toJSON", "world"));
            ASObject testCase11_result = makeObject(("x", true), ("y", "hello"), ("toJSON", "world"));

            var testcases = new (ASObject, ASObject)[] {
                (testCase1, testCase1_result),
                (testCase2, testCase2_result),
                (testCase3, testCase3_result),
                (testCase4, testCase4_result),
                (testCase5, testCase5_result),
                (testCase6, testCase6_result),
                (testCase7, testCase7_result),
                (testCase8, testCase8_result),
                (testCase9, testCase9_result),
                (testCase10, testCase10_result),
                (testCase11, testCase11_result)
            };

            var spaces = new string[] {"", "  ", "aaa", "          "};

            foreach (var (obj, parsedObj) in testcases) {
                foreach (var space in spaces)
                    yield return new object[] {obj, space, parsedObj};
            }
        }

        [Theory]
        [MemberData(nameof(stringifyRoundTripTestData_toJSONOverridden))]
        public void stringifyRoundTripTest_toJSONOverridden(ASObject obj, string spaceStr, ASObject parsedObj) {
            foreach (ASObject spaceArg in generateEquivalentSpaceArgs(spaceStr)) {
                string json = ASJSON.stringify(obj, replacer: null, spaceArg);
                json = validateJSONFormatAndRemoveIndents(json, spaceStr);
                validateStructuralEquality(parsedObj, ASJSON.parse(json), roundtripMode: true);
            }
        }

        public static IEnumerable<object[]> stringifyRoundTripTestData_withReplacer() {
            var testCases = new List<(ASObject, ASFunction, ASObject)>();

            var testCases1_replacer = new MockFunctionObject((obj, args) => {
                Assert.IsType<ASObject>(obj.value);
                AssertHelper.strictEqual("", args[0]);
                AssertHelper.valueIdentical(args[1], obj.AS_getProperty(""));
                return args[1];
            });

            var testCases1 = new ASObject[] {null, 134.43, true, "hello", Double.PositiveInfinity};
            var testCases1_results = new ASObject[] {null, 134.43, true, "hello", null};

            for (int i = 0; i < testCases1.Length; i++)
                testCases.Add((testCases1[i], testCases1_replacer, testCases1_results[i]));

            var testCases2_replacer1 = new MockFunctionObject((obj, args) => {
                Assert.IsType<ASString>(args[0].value);
                string key = (string)args[0].value;
                AssertHelper.valueIdentical(args[1], obj.AS_getProperty(key));

                if (key == "_key" || key == "_val")
                    return args[1];

                return makeObject(("_key", key), ("_val", args[1]));
            });

            var testCases2_replacer2 = new MockFunctionObject((obj, args) => {
                string key = (string)args[0].value;

                if (key == "_key" || key == "_val")
                    return args[1];

                return new MockClassInstance(new MockClass(
                    properties: new[] {
                        new MockPropertyTrait(
                            name: "_key",
                            getter: new MockMethodTrait(invokeFunc: (_obj3, _args3) => key)
                        ),
                        new MockPropertyTrait(
                            name: "_val",
                            getter: new MockMethodTrait(invokeFunc: (_obj3, _args3) => args[1])
                        )
                    }
                ));
            });

            var testCases2_cachedArray = makeArray(1, 342, NULL);
            var testCases2_cachedObject = makeObject(("_a", 192));

            var testCases2 = new (ASObject obj, ASObject parsedObj)[] {
                (null, makeObject(("_key", ""), ("_val", NULL))),
                (134.43, makeObject(("_key", ""), ("_val", 134.43))),
                (true, makeObject(("_key", ""), ("_val", true))),
                ("hello", makeObject(("_key", ""), ("_val", "hello"))),

                (Double.NaN, makeObject(("_key", ""), ("_val", NULL))),
                (new MockFunctionObject(), makeObject(("_key", ""))),

                (
                    makeArray(1, 2, 3, "ABC"),
                    makeObject(("_key", ""), ("_val", makeArray(
                        makeObject(("_key", "0"), ("_val", 1)),
                        makeObject(("_key", "1"), ("_val", 2)),
                        makeObject(("_key", "2"), ("_val", 3)),
                        makeObject(("_key", "3"), ("_val", "ABC"))
                    )))
                ),
                (
                    makeArray(makeArray(10, 20, Double.PositiveInfinity), makeArray("A", "B", NULL, UNDEF)),
                    makeObject(("_key", ""), ("_val",  makeArray(
                        makeObject(("_key", "0"), ("_val",  makeArray(
                            makeObject(("_key", "0"), ("_val", 10)),
                            makeObject(("_key", "1"), ("_val", 20)),
                            makeObject(("_key", "2"), ("_val", NULL))
                        ))),
                        makeObject(("_key", "1"), ("_val",  makeArray(
                            makeObject(("_key", "0"), ("_val", "A")),
                            makeObject(("_key", "1"), ("_val", "B")),
                            makeObject(("_key", "2"), ("_val", NULL)),
                            makeObject(("_key", "3"))
                        )))
                    )))
                ),
                (
                    makeObject(
                        ("ab", makeObject(("x", 1), ("y", 2))),
                        ("AB", makeObject(("x", 4), ("y", 9), ("z", ASFunction.createEmpty()), ("w", UNDEF)))
                    ),
                    makeObject(("_key", ""), ("_val",  makeObject(
                        ("ab", makeObject(("_key", "ab"), ("_val",  makeObject(
                            ("x", makeObject(("_key", "x"), ("_val", 1))),
                            ("y", makeObject(("_key", "y"), ("_val", 2)))
                        )))),
                        ("AB", makeObject(("_key", "AB"), ("_val",  makeObject(
                            ("x", makeObject(("_key", "x"), ("_val", 4))),
                            ("y", makeObject(("_key", "y"), ("_val", 9))),
                            ("z", makeObject(("_key", "z"))),
                            ("w", makeObject(("_key", "w")))
                        ))))
                    )))
                ),
                (
                    makeVector<ASObject>(makeObject(("x", 1), ("y", 2)), makeObject(("x", 4), ("y", 9))),
                    makeObject(("_key", ""), ("_val", makeArray(
                        makeObject(("_key", "0"), ("_val",  makeObject(
                            ("x", makeObject(("_key", "x"), ("_val", 1))),
                            ("y", makeObject(("_key", "y"), ("_val", 2)))
                        ))),
                        makeObject(("_key", "1"), ("_val",  makeObject(
                            ("x", makeObject(("_key", "x"), ("_val", 4))),
                            ("y", makeObject(("_key", "y"), ("_val", 9)))
                        )))
                    )))
                ),
                (
                    new MockClassInstance(new MockClass(
                        name: "ABC",
                        fields: new[] {
                            new MockFieldTrait(name: "fg321", getValueFunc: obj => 123),
                            new MockFieldTrait(name: "hv426", getValueFunc: obj => UNDEF),
                            new MockFieldTrait(name: "kj764", getValueFunc: obj => testCases2_cachedArray),
                            new MockFieldTrait(name: "oq839", getValueFunc: obj => Double.NaN),
                            new MockFieldTrait(name: "pd091", getValueFunc: obj => testCases2_cachedObject)
                        }
                    )),
                    makeObject(("_key", ""), ("_val", makeObject(
                        ("fg321", makeObject(("_key", "fg321"), ("_val", 123))),
                        ("hv426", makeObject(("_key", "hv426"))),
                        ("kj764", makeObject(("_key", "kj764"), ("_val", makeArray(
                            makeObject(("_key", "0"), ("_val", 1)),
                            makeObject(("_key", "1"), ("_val", 342)),
                            makeObject(("_key", "2"), ("_val", NULL))
                        )))),
                        ("oq839", makeObject(("_key", "oq839"), ("_val", NULL))),
                        ("pd091", makeObject(("_key", "pd091"), ("_val", makeObject(
                            ("_a", makeObject(("_key", "_a"), ("_val", 192)))
                        ))))
                    )))
                )
            };

            for (int i = 0; i < testCases2.Length; i++) {
                testCases.Add((testCases2[i].obj, testCases2_replacer1, testCases2[i].parsedObj));
                testCases.Add((testCases2[i].obj, testCases2_replacer2, testCases2[i].parsedObj));
            }

            var testCases3_replacer = MockFunctionObject.withReturn(42);
            var testCases3 = new ASObject[] {
                null,
                0,
                42,
                "hello",
                true,
                makeArray(1, 2, 3, 4),
                makeVector<int>(1, 2, 3, 4),
                makeObject(("a", makeObject(("a", makeObject(("a", makeArray()))))))
            };

            for (int i = 0; i < testCases3.Length; i++)
                testCases.Add((testCases3[i], testCases3_replacer, 42));

            var testCases4_replacer = new MockFunctionObject((obj, args) => {
                if (args[1].isUndefinedOrNull || ASObject.AS_isPrimitive(args[1].value))
                    return ASAny.AS_convertString(args[1]);

                if (args[1].value is ASFunction)
                    return "function";

                return args[1];
            });

            var testCases4 = new (ASObject obj, ASObject parsedObj)[] {
                (null, "null"),
                (123, "123"),
                (false, "false"),
                (Double.NegativeInfinity, "-Infinity"),
                (new MockFunctionObject(), "function"),

                (
                    makeArray(12, "332", Double.NaN, NULL, UNDEF, true, new MockFunctionObject()),
                    makeArray("12", "332", "NaN", "null", "undefined", "true", "function")
                ),
                (
                    makeVector<double>(1.0, 5.0, -7.9, Double.PositiveInfinity, Double.NaN),
                    makeArray("1", "5", "-7.9", "Infinity", "NaN")
                ),
                (
                    makeObject(
                        ("x", NULL),
                        ("y", UNDEF),
                        ("z", Double.NaN),
                        ("w", makeArray(13, UNDEF, makeObject(("f", new MockFunctionObject()))))
                    ),
                    makeObject(
                        ("x", "null"),
                        ("y", "undefined"),
                        ("z", "NaN"),
                        ("w", makeArray("13", "undefined", makeObject(("f", "function"))))
                    )
                )
            };

            for (int i = 0; i < testCases4.Length; i++)
                testCases.Add((testCases4[i].obj, testCases4_replacer, testCases4[i].parsedObj));

            var testCase5_replacer = new MockFunctionObject((obj, args) => {
                string key = (string)args[0];
                return (key == "x") ? (ASAny)42 : args[1];
            });

            var testCase5_object = makeObject(("y", 45));
            testCase5_object.AS_dynamicProps.setValue("x", testCase5_object);

            var testCase5_result = makeObject(("x", 42), ("y", 45));

            testCases.Add((testCase5_object, testCase5_replacer, testCase5_result));

            var testCase6_object = makeObject(
                ("x", 142),
                ("toJSON", MockFunctionObject.withReturn(145))
            );
            var testCase6_replacer = new MockFunctionObject((obj, args) => {
                AssertHelper.strictEqual(145, args[1]);
                return 264;
            });

            testCases.Add((testCase6_object, testCase6_replacer, 264));

            var spaces = new[] {"", "  ", "aaaa", "          "};

            foreach (var (obj, replacer, parsedObj) in testCases) {
                foreach (var space in spaces)
                    yield return new object[] {obj, replacer, space, parsedObj};
            }
        }

        [Theory]
        [MemberData(nameof(stringifyRoundTripTestData_withReplacer))]
        public void stringifyRoundTripTest_withReplacer(ASObject obj, ASFunction replacer, string spaceStr, ASObject parsedObj) {
            foreach (ASObject spaceArg in generateEquivalentSpaceArgs(spaceStr)) {
                string json = ASJSON.stringify(obj, replacer, spaceArg);
                json = validateJSONFormatAndRemoveIndents(json, spaceStr);
                validateStructuralEquality(parsedObj, ASJSON.parse(json), roundtripMode: true);
            }
        }

        public static IEnumerable<object[]> stringifyRoundTripTestData_withReplacerArray() {
            var testCases = new (ASObject obj, string[] filter, ASObject parsedObj)[] {
                (null, Array.Empty<string>(), null),
                (134.2, Array.Empty<string>(), 134.2),
                ("ahfdg4", Array.Empty<string>(), "ahfdg4"),
                (true, Array.Empty<string>(), true),

                (null, new[] {"a"}, null),
                (134.2, new[] {"a"}, 134.2),
                ("ahfdg4", new[] {"a"}, "ahfdg4"),
                (true, new[] {"a"}, true),

                (makeArray(1, 2, 3, 4, 5), Array.Empty<string>(), makeArray(1, 2, 3, 4, 5)),
                (makeVector("a", "b", "c"), Array.Empty<string>(), makeArray("a", "b", "c")),
                (makeArray(1, 2, 3, 4, 5), new[] {"0", "1", "a"}, makeArray(1, 2, 3, 4, 5)),
                (makeVector("a", "b", "c"), new[] {"0", "1", "a"}, makeArray("a", "b", "c")),

                (
                    makeObject(("a", 1), ("b", 2), ("c", 3), ("d", 4), ("x", 5), ("z", 6)),
                    Array.Empty<string>(),
                    makeObject()
                ),
                (
                    makeObject(("a", 1), ("b", 2), ("c", 3), ("d", 4), ("x", 5), ("z", 6)),
                    new[] {"b", "d"},
                    makeObject(("b", 2), ("d", 4))
                ),
                (
                    makeObject(("a", 1), ("b", 2), ("c", 3), ("d", 4), ("x", 5), ("z", 6)),
                    new[] {"b", "c", "x", "y"},
                    makeObject(("b", 2), ("c", 3), ("x", 5))
                ),
                (
                    makeObject(("a", 1), ("b", 2), ("c", 3), ("d", 4), ("x", 5), ("z", 6)),
                    new[] {"b", "c", "x", "a", "z", "f", "d"},
                    makeObject(("a", 1), ("b", 2), ("c", 3), ("d", 4), ("x", 5), ("z", 6))
                ),
                (
                    makeObject(
                        ("x", makeObject(
                            ("x", makeObject(("x", makeObject()), ("y", makeObject()))),
                            ("y", makeObject(("x", makeObject()), ("y", makeObject())))
                        )),
                        ("x", makeObject(
                            ("x", makeObject(("x", makeObject()), ("y", makeObject()))),
                            ("y", makeObject(("x", makeObject()), ("y", makeObject())))
                        ))
                    ),
                    Array.Empty<string>(),
                    makeObject()
                ),
                (
                    makeObject(
                        ("x", makeObject(
                            ("x", makeObject(("x", makeObject()), ("y", makeObject()))),
                            ("y", makeObject(("x", makeObject()), ("y", makeObject())))
                        )),
                        ("y", makeObject(
                            ("x", makeObject(("x", makeObject()), ("y", makeObject()))),
                            ("y", makeObject(("x", makeObject()), ("y", makeObject())))
                        ))
                    ),
                    new[] {"x"},
                    makeObject(
                        ("x", makeObject(
                            ("x", makeObject(("x", makeObject())))
                        ))
                    )
                ),
                (
                    makeObject(
                        ("x", makeObject(
                            ("x", makeObject(("x", makeObject()), ("y", makeObject()), ("z", makeObject()))),
                            ("y", makeObject(("x", makeObject()), ("y", makeObject()), ("z", makeObject()))),
                            ("z", makeObject(("x", makeObject()), ("y", makeObject()), ("z", makeObject())))
                        )),
                        ("y", makeObject(
                            ("x", makeObject(("x", makeObject()), ("y", makeObject()), ("z", makeObject()))),
                            ("y", makeObject(("x", makeObject()), ("y", makeObject()), ("z", makeObject()))),
                            ("z", makeObject(("x", makeObject()), ("y", makeObject()), ("z", makeObject())))
                        )),
                        ("z", makeObject(
                            ("x", makeObject(("x", makeObject()), ("y", makeObject()), ("z", makeObject()))),
                            ("y", makeObject(("x", makeObject()), ("y", makeObject()), ("z", makeObject()))),
                            ("z", makeObject(("x", makeObject()), ("y", makeObject()), ("z", makeObject())))
                        ))
                    ),
                    new[] {"x", "z"},
                    makeObject(
                        ("x", makeObject(
                            ("x", makeObject(("x", makeObject()), ("z", makeObject()))),
                            ("z", makeObject(("x", makeObject()), ("z", makeObject())))
                        )),
                        ("z", makeObject(
                            ("x", makeObject(("x", makeObject()), ("z", makeObject()))),
                            ("z", makeObject(("x", makeObject()), ("z", makeObject())))
                        ))
                    )
                ),
                (
                    makeArray(
                        makeObject(
                            ("0", makeArray(
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0))
                            )),
                            ("1", makeArray(
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0))
                            )),
                            ("2", makeArray(
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0))
                            ))
                        ),
                        makeObject(
                            ("0", makeArray(
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0))
                            )),
                            ("1", makeArray(
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0))
                            )),
                            ("2", makeArray(
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0))
                            ))
                        )
                    ),
                    Array.Empty<string>(),
                    makeArray(makeObject(), makeObject())
                ),
                (
                    makeArray(
                        makeObject(
                            ("0", makeArray(
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("3", 0), ("y", 0))
                            )),
                            ("1", makeArray(
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("3", 0), ("y", 0))
                            )),
                            ("2", makeArray(
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("3", 0), ("y", 0))
                            ))
                        ),
                        makeObject(
                            ("0", makeArray(
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("3", 0), ("y", 0))
                            )),
                            ("1", makeArray(
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("3", 0), ("y", 0))
                            )),
                            ("2", makeArray(
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("2", 0), ("y", 0)),
                                makeObject(("x", 0), ("0", 0), ("3", 0), ("y", 0))
                            ))
                        )
                    ),
                    new[] {"y", "0", "3"},
                    makeArray(
                        makeObject(
                            ("0", makeArray(
                                makeObject(("0", 0), ("y", 0)),
                                makeObject(("0", 0), ("y", 0)),
                                makeObject(("0", 0), ("y", 0)),
                                makeObject(("0", 0), ("3", 0), ("y", 0))
                            ))
                        ),
                        makeObject(
                            ("0", makeArray(
                                makeObject(("0", 0), ("y", 0)),
                                makeObject(("0", 0), ("y", 0)),
                                makeObject(("0", 0), ("y", 0)),
                                makeObject(("0", 0), ("3", 0), ("y", 0))
                            ))
                        )
                    )
                ),
                (
                    new MockClassInstance(new MockClass(
                        fields: new[] {
                            new MockFieldTrait(name: "abc01", getValueFunc: obj => 0),
                            new MockFieldTrait(name: "abc02", getValueFunc: obj => 1),
                        },
                        properties: new[] {
                            new MockPropertyTrait(name: "abc03", getter: new MockMethodTrait(invokeFunc: (obj, args) => 2)),
                            new MockPropertyTrait(name: "abc04", getter: new MockMethodTrait(invokeFunc: (obj, args) => 3))
                        }
                    )),
                    new[] {"abc01", "abc04", "abc06"},
                    makeObject(("abc01", 0), ("abc04", 3))
                ),
                (
                    makeObject(("héllo", 0), ("he\u0301llo", 1), ("Héllo", 2), ("He\u0301llo", 3)),
                    new[] {"héllo", "He\u0301llo"},
                    makeObject(("héllo", 0), ("He\u0301llo", 3))
                )
            };

            var spaces = new[] {"", "  ", "abc", "          "};

            foreach (var (obj, filter, parsedObj) in testCases) {
                foreach (var space in spaces) {
                    yield return new object[] {obj, filter, space, parsedObj};
                }
            }
        }

        [Theory]
        [MemberData(nameof(stringifyRoundTripTestData_withReplacerArray))]
        public void stringifyRoundTripTest_withReplacerArray(
            ASObject obj, string[] filter, string spaceStr, ASObject parsedObj)
        {
            foreach (ASObject spaceArg in generateEquivalentSpaceArgs(spaceStr)) {
                foreach (ASArray filterArg in generateEquivalentFilterArgs(filter)) {
                    string json = ASJSON.stringify(obj, filterArg, spaceArg);
                    json = validateJSONFormatAndRemoveIndents(json, spaceStr);
                    validateStructuralEquality(parsedObj, ASJSON.parse(json), roundtripMode: true);
                }
            }
        }

        public static IEnumerable<object[]> stringifyTestData_cyclicStructures() {
            var testCase1 = makeArray(1, 2, 3, makeArray(1, 2, 3), 4, 5);
            testCase1[4] = testCase1;

            var testCase2 = makeObject(("x", 1), ("y", 2));
            testCase2.AS_setProperty("z", testCase2);

            var testCase3_inner = makeArray(1, 2, 3, 4);
            var testCase3_outer = makeArray(makeArray(1, 2, 3), makeArray(makeObject(), makeArray(testCase3_inner)), makeObject(("x", 1)));
            testCase3_inner[1] = testCase3_outer;
            var testCase3 = makeArray(makeArray(testCase3_inner), makeArray(testCase3_outer));

            var testCase4_inner = makeObject();
            var testCase4_outer = makeObject(("x1", makeObject()), ("x2", makeObject()), ("x3", makeObject(("p", testCase4_inner))));
            testCase4_inner.AS_setProperty("q", testCase4_outer);
            var testCase4 = makeObject(("a", makeObject(("b", testCase4_outer))));

            var testCase5_inner = makeArray(NULL, NULL, NULL);
            var testCase5_outer = makeObject(("x1", makeArray(1, 1, 1)), ("x2", makeArray(makeObject(("p", testCase5_inner)), 0)), ("x3", NULL));
            testCase5_inner[0] = testCase5_outer;
            var testCase5 = makeObject(("p", NULL), ("q", makeArray("", "abcd", testCase5_outer, "__")));

            var testCase6_inner1 = makeArray(NULL, NULL, NULL);
            var testCase6_inner2 = makeArray(NULL, NULL, NULL);
            var testCase6_outer1 = makeArray(NULL, testCase6_inner1, NULL);
            var testCase6_outer2 = makeArray(NULL, testCase6_inner2, NULL);
            testCase6_inner1[0] = testCase6_outer2;
            testCase6_inner2[2] = testCase6_outer1;
            var testCase6 = makeObject(("p1", testCase6_outer2), ("p2", testCase6_outer1));

            var testCase7_inner1 = makeObject();
            var testCase7_inner2 = makeObject();
            var testCase7_outer1 = makeObject(("p", makeArray(NULL, testCase7_inner1)));
            var testCase7_outer2 = makeObject(("q", makeArray(NULL, testCase7_inner2)));
            testCase7_inner1.AS_setProperty("r", testCase7_outer2);
            testCase7_inner2.AS_setProperty("s", testCase7_outer1);
            var testCase7 = makeArray(NULL, testCase7_outer1, testCase7_outer2);

            ASObject testCase8 = null;
            var testCase8_innerClass = new MockClass(
                fields: new[] {
                    new MockFieldTrait(name: "x", getValueFunc: obj => makeArray(testCase8))
                }
            );
            var testCase8_inner = new MockClassInstance(testCase8_innerClass);
            var testCase8_outerClass = new MockClass(
                properties: new[] {
                    new MockPropertyTrait(name: "y", getter: new MockMethodTrait(invokeFunc: (obj, args) => testCase8_inner))
                }
            );
            testCase8 = new MockClassInstance(testCase8_outerClass);

            ASObject testCase9 = null;
            var testCase9_inner = makeObject(
                ("toJSON", new MockFunctionObject((obj, args) => {
                    string key = (string)args[0];
                    return (key == "x") ? testCase9 : (ASAny)123;
                }))
            );
            testCase9 = makeObject(("x", testCase9_inner));

            var testCase10 = makeObject(("x", 123));
            var testCase10_replacer = new MockFunctionObject((obj, args) => {
                string key = (string)args[0];
                return (key == "x") ? testCase10 : args[1];
            });

            var testCase11 = makeVector<ASVectorAny>(null);
            testCase11[0] = testCase11;

            return TupleHelper.toArrays<ObjectWrapper<ASObject>, ASFunction>(
                (testCase1, null),
                (testCase2, null),
                (testCase3, null),
                (testCase4, null),
                (testCase5, null),
                (testCase6, null),
                (testCase7, null),
                (testCase8, null),
                (testCase9, null),
                (testCase10, testCase10_replacer),
                (testCase11, null)
            );
        }

        [Theory]
        [MemberData(nameof(stringifyTestData_cyclicStructures))]
        public void stringifyTest_cyclicStructures(ObjectWrapper<ASObject> obj, ASFunction replacer) {
            var spaces = new string[] {"", "          "};
            foreach (ASObject spaceArg in spaces.SelectMany(generateEquivalentSpaceArgs))
                AssertHelper.throwsErrorWithCode(ErrorCode.JSON_CYCLIC_STRUCTURE, () => ASJSON.stringify(obj.value, replacer, spaceArg));
        }

        public static IEnumerable<object[]> stringifyTestData_invalidReplacer() {
            var objects = new ASObject[] {
                null,
                12,
                "ab",
                true,
                makeArray(1, 2, "", 4, true, makeArray()),
                makeObject(("x", 1), ("y", makeArray()), ("z", makeObject(("x", makeObject()))))
            };

            var replacers = new ASObject[] {0, "x", true, new ASObject(), new ASVector<string>(new[] {"a"})};

            foreach (var obj in objects) {
                foreach (var replacer in replacers)
                    yield return new object[] {obj, replacer};
            }
        }

        [Theory]
        [MemberData(nameof(stringifyTestData_invalidReplacer))]
        public void stringifyTest_invalidReplacer(ASObject obj, ASObject replacer) {
            var spaces = new string[] {"", "          "};
            foreach (ASObject spaceArg in spaces.SelectMany(generateEquivalentSpaceArgs))
                AssertHelper.throwsErrorWithCode(ErrorCode.JSON_INVALID_REPLACER, () => ASJSON.stringify(obj, replacer, spaceArg));
        }

    }

}
