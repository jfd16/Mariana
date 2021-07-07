using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mariana.AVM2.Core;
using Mariana.AVM2.Native;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    [AVM2ExportClass]
    public interface GenericTypeConvertersTest_IA {}

    [AVM2ExportClass]
    public interface GenericTypeConvertersTest_IB {}

    [AVM2ExportClass]
    public interface GenericTypeConvertersTest_IC : GenericTypeConvertersTest_IA, GenericTypeConvertersTest_IB {}

    [AVM2ExportClass]
    public class GenericTypeConvertersTest_CA : ASObject {}

    [AVM2ExportClass]
    public class GenericTypeConvertersTest_CB : ASObject, GenericTypeConvertersTest_IA {
        protected private override bool AS_coerceBoolean() => false;
        protected override int AS_coerceInt() => 139453;
        protected override uint AS_coerceUint() => 394922;
        protected override double AS_coerceNumber() => 7.5999483;
        protected override string AS_coerceString() => "aiq83392gg";
    }

    [AVM2ExportClass]
    public class GenericTypeConvertersTest_CC : GenericTypeConvertersTest_CB {}

    [AVM2ExportClass]
    public class GenericTypeConvertersTest_CD : ASObject, GenericTypeConvertersTest_IC {}

    public class GenericTypeConvertersTest_NonASType1 { }

    public class GenericTypeConvertersTest_NonASType2 : GenericTypeConvertersTest_NonASType1, GenericTypeConvertersTest_IA { }

    public struct GenericTypeConvertersTest_NonASType3 { }

    public struct GenericTypeConvertersTest_NonASType4 { }

    public class GenericTypeConvertersTest {

        static GenericTypeConvertersTest() {
            TestAppDomain.ensureClassesLoaded(
                typeof(GenericTypeConvertersTest_CA),
                typeof(GenericTypeConvertersTest_CB),
                typeof(GenericTypeConvertersTest_CC),
                typeof(GenericTypeConvertersTest_CD)
            );
        }

        private static void runConvertSingleValueTest<T, U>(T[] testcases, Func<T, U> expectedConversion, Action<U, U> assertion = null) {
            var converter = GenericTypeConverter<T, U>.instance;

            for (int i = 0; i < testcases.Length; i++) {
                bool typeCoercionFailed = false;
                T input = testcases[i];
                U expectedOutput = default;

                try {
                    expectedOutput = expectedConversion(input);
                }
                catch (AVM2Exception e)
                    when (e.thrownValue.value is ASError err && err.errorID == (int)ErrorCode.TYPE_COERCION_FAILED)
                {
                    typeCoercionFailed = true;
                }

                if (typeCoercionFailed)
                    AssertHelper.throwsErrorWithCode(ErrorCode.TYPE_COERCION_FAILED, () => converter.convert(input));
                else if (assertion != null)
                    assertion(expectedOutput, converter.convert(input));
                else
                    Assert.Equal(expectedOutput, converter.convert(input));
            }
        }

        private static void runConvertArrayTest<T, U>(T[] testcases, Func<T, U> expectedConversion, Action<U, U> assertion = null) {
            if (assertion == null)
                assertion = (expected, actual) => Assert.Equal(expected, actual);

            var converter = GenericTypeConverter<T, U>.instance;

            U[] expectedOutputs = new U[testcases.Length];
            int typeCoercionFailedAt = -1;

            for (int i = 0; i < testcases.Length && typeCoercionFailedAt == -1; i++) {
                T input = testcases[i];
                try {
                    expectedOutputs[i] = expectedConversion(input);
                }
                catch (AVM2Exception e)
                    when (e.thrownValue.value is ASError err && err.errorID == (int)ErrorCode.TYPE_COERCION_FAILED)
                {
                    typeCoercionFailedAt = i;
                }
            }

            U[] tmpArr = new U[testcases.Length + 1];

            if (testcases.Length > 0) {
                AssertHelper.throwsErrorWithCode(
                    ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE,
                    () => converter.convertSpan(testcases, Span<U>.Empty)
                );
            }

            if (testcases.Length > 1) {
                AssertHelper.throwsErrorWithCode(
                    ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE,
                    () => converter.convertSpan(testcases, tmpArr.AsSpan(0, testcases.Length - 1))
                );
                for (int i = 0; i < tmpArr.Length; i++)
                    Assert.Equal(default(U), tmpArr[i]);
            }

            AssertHelper.throwsErrorWithCode(
                ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE,
                () => converter.convertSpan(testcases, tmpArr.AsSpan(0, testcases.Length + 1))
            );

            for (int i = 0; i < tmpArr.Length; i++)
                Assert.Equal(default(U), tmpArr[i]);

            void testFunc1() => converter.convertSpan(testcases, tmpArr.AsSpan(0, testcases.Length));

            if (typeCoercionFailedAt != -1)
                AssertHelper.throwsErrorWithCode(ErrorCode.TYPE_COERCION_FAILED, testFunc1);
            else
                testFunc1();

            for (int i = 0, n = (typeCoercionFailedAt != -1) ? typeCoercionFailedAt : testcases.Length; i < n; i++)
                assertion(expectedOutputs[i], tmpArr[i]);

            U[] convertSpanArrayResult = null;

            void testFunc2() => convertSpanArrayResult = converter.convertSpan(testcases);

            if (typeCoercionFailedAt != -1) {
                AssertHelper.throwsErrorWithCode(ErrorCode.TYPE_COERCION_FAILED, testFunc2);
            }
            else {
                testFunc2();
                Assert.Equal(testcases.Length, convertSpanArrayResult.Length);
                for (int i = 0; i < testcases.Length; i++)
                    assertion(expectedOutputs[i], convertSpanArrayResult[i]);
            }
        }

        private static U invalidConversion<T, U>(T value) {
            if (!typeof(T).IsValueType && !typeof(U).IsValueType && (object)value == null)
                return (U)(object)null;

            throw ErrorHelper.createError(ErrorCode.TYPE_COERCION_FAILED, "T", "U");
        }

        private static U referenceConversion<T, U>(T value) where T : class where U : class => ASObject.AS_cast<U>(value);

        public static IEnumerable<object[]> convertFromIntTest_data = TupleHelper.toArrays(
            Array.Empty<int>(),
            new int[] {0, 0, 1, 4, 0, -1, 10000, Int32.MaxValue, Int32.MinValue, -5893313}
        );

        [Theory]
        [MemberData(nameof(convertFromIntTest_data))]
        public void convertFromIntTest_single(int[] data) {
            runConvertSingleValueTest<int, int>(data, x => x);
            runConvertSingleValueTest<int, uint>(data, x => (uint)x);
            runConvertSingleValueTest<int, double>(data, x => (double)x, AssertHelper.floatIdentical);
            runConvertSingleValueTest<int, bool>(data, x => x != 0);
            runConvertSingleValueTest<int, string>(data, x => ASint.AS_convertString(x));

            runConvertSingleValueTest<int, ASObject>(data, x => x, (expected, actual) => {
                Assert.IsType<ASint>(actual);
                Assert.Equal((int)expected, (int)actual);
            });

            runConvertSingleValueTest<int, ASAny>(data, x => x, (expected, actual) => {
                Assert.IsType<ASint>(actual.value);
                Assert.Equal((int)expected, (int)actual);
            });

            runConvertSingleValueTest(data, invalidConversion<int, GenericTypeConvertersTest_CA>);
            runConvertSingleValueTest(data, invalidConversion<int, GenericTypeConvertersTest_IA>);
            runConvertSingleValueTest(data, invalidConversion<int, GenericTypeConvertersTest_NonASType1>);
        }

        [Theory]
        [MemberData(nameof(convertFromIntTest_data))]
        public void convertFromIntTest_array(int[] data) {
            runConvertArrayTest<int, int>(data, x => x);
            runConvertArrayTest<int, uint>(data, x => (uint)x);
            runConvertArrayTest<int, double>(data, x => (double)x, AssertHelper.floatIdentical);
            runConvertArrayTest<int, bool>(data, x => x != 0);
            runConvertArrayTest<int, string>(data, x => ASint.AS_convertString(x));

            runConvertArrayTest<int, ASObject>(data, x => x, (expected, actual) => {
                Assert.IsType<ASint>(actual);
                Assert.Equal((int)expected, (int)actual);
            });

            runConvertArrayTest<int, ASAny>(data, x => x, (expected, actual) => {
                Assert.IsType<ASint>(actual.value);
                Assert.Equal((int)expected, (int)actual);
            });

            runConvertArrayTest(data, invalidConversion<int, GenericTypeConvertersTest_CA>);
            runConvertArrayTest(data, invalidConversion<int, GenericTypeConvertersTest_IA>);
            runConvertArrayTest(data, invalidConversion<int, GenericTypeConvertersTest_NonASType1>);
        }

        public static IEnumerable<object[]> convertFromUintTest_data = TupleHelper.toArrays(
            Array.Empty<uint>(),
            new uint[] {0, 0, 1, 4, 0, 10000, UInt32.MaxValue, 549103, UInt32.MaxValue, 0, 3009401940}
        );

        [Theory]
        [MemberData(nameof(convertFromUintTest_data))]
        public void convertFromUintTest_single(uint[] data) {
            runConvertSingleValueTest<uint, int>(data, x => (int)x);
            runConvertSingleValueTest<uint, uint>(data, x => x);
            runConvertSingleValueTest<uint, double>(data, x => (double)x, AssertHelper.floatIdentical);
            runConvertSingleValueTest<uint, bool>(data, x => x != 0);
            runConvertSingleValueTest<uint, string>(data, x => ASuint.AS_convertString(x));

            runConvertSingleValueTest<uint, ASObject>(data, x => x, (expected, actual) => {
                Assert.IsType<ASuint>(actual);
                Assert.Equal((uint)expected, (uint)actual);
            });

            runConvertSingleValueTest<uint, ASAny>(data, x => x, (expected, actual) => {
                Assert.IsType<ASuint>(actual.value);
                Assert.Equal((uint)expected, (uint)actual);
            });

            runConvertSingleValueTest(data, invalidConversion<uint, GenericTypeConvertersTest_CA>);
            runConvertSingleValueTest(data, invalidConversion<uint, GenericTypeConvertersTest_IA>);
            runConvertSingleValueTest(data, invalidConversion<uint, GenericTypeConvertersTest_NonASType1>);
        }

        [Theory]
        [MemberData(nameof(convertFromUintTest_data))]
        public void convertFromUintTest_array(uint[] data) {
            runConvertArrayTest<uint, int>(data, x => (int)x);
            runConvertArrayTest<uint, uint>(data, x => x);
            runConvertArrayTest<uint, double>(data, x => (double)x, AssertHelper.floatIdentical);
            runConvertArrayTest<uint, bool>(data, x => x != 0);
            runConvertArrayTest<uint, string>(data, x => ASuint.AS_convertString(x));

            runConvertArrayTest<uint, ASObject>(data, x => x, (expected, actual) => {
                Assert.IsType<ASuint>(actual);
                Assert.Equal((uint)expected, (uint)actual);
            });

            runConvertArrayTest<uint, ASAny>(data, x => x, (expected, actual) => {
                Assert.IsType<ASuint>(actual.value);
                Assert.Equal((uint)expected, (uint)actual);
            });

            runConvertArrayTest(data, invalidConversion<uint, GenericTypeConvertersTest_CA>);
            runConvertArrayTest(data, invalidConversion<uint, GenericTypeConvertersTest_IA>);
            runConvertArrayTest(data, invalidConversion<uint, GenericTypeConvertersTest_NonASType1>);
        }

        public static IEnumerable<object[]> convertFromNumberTest_data = TupleHelper.toArrays(
            Array.Empty<double>(),
            new double[] {
                0, 1, 4, 0, 10000, 0.1, Int32.MaxValue, -0.0, 1349.2949, 549103, UInt32.MaxValue,
                0, -0.5, 3009401940, 85775847291843476, Int64.MaxValue, UInt64.MaxValue,
                84924.34199959832, -3049.399346435435, 203.3988,
                1e-6, 1e-7, 1e-20, 1.453298874e-300, Double.Epsilon,
                1e+19, 1e+20, 1e+21, 6.583492400e+204, Double.MaxValue,
                Double.PositiveInfinity, Double.NegativeInfinity, Double.NaN
            }
        );

        [Theory]
        [MemberData(nameof(convertFromNumberTest_data))]
        public void convertFromNumberTest_single(double[] data) {
            runConvertSingleValueTest<double, int>(data, x => ASNumber.AS_toInt(x));
            runConvertSingleValueTest<double, uint>(data, x => ASNumber.AS_toUint(x));
            runConvertSingleValueTest<double, double>(data, x => x, AssertHelper.floatIdentical);
            runConvertSingleValueTest<double, bool>(data, x => ASNumber.AS_toBoolean(x));
            runConvertSingleValueTest<double, string>(data, x => ASNumber.AS_convertString(x));

            runConvertSingleValueTest<double, ASObject>(data, x => x, (expected, actual) => {
                Assert.True(ASObject.AS_isNumeric(actual));
                AssertHelper.floatIdentical((double)expected, (double)actual);
            });

            runConvertSingleValueTest<double, ASAny>(data, x => x, (expected, actual) => {
                Assert.True(ASObject.AS_isNumeric(actual.value));
                AssertHelper.floatIdentical((double)expected, (double)actual);
            });

            runConvertSingleValueTest(data, invalidConversion<double, GenericTypeConvertersTest_CA>);
            runConvertSingleValueTest(data, invalidConversion<double, GenericTypeConvertersTest_IA>);
            runConvertSingleValueTest(data, invalidConversion<double, GenericTypeConvertersTest_NonASType1>);
        }

        [Theory]
        [MemberData(nameof(convertFromNumberTest_data))]
        public void convertFromNumberTest_array(double[] data) {
            runConvertArrayTest<double, int>(data, x => ASNumber.AS_toInt(x));
            runConvertArrayTest<double, uint>(data, x => ASNumber.AS_toUint(x));
            runConvertArrayTest<double, double>(data, x => x, AssertHelper.floatIdentical);
            runConvertArrayTest<double, bool>(data, x => ASNumber.AS_toBoolean(x));
            runConvertArrayTest<double, string>(data, x => ASNumber.AS_convertString(x));

            runConvertArrayTest<double, ASObject>(data, x => x, (expected, actual) => {
                Assert.True(ASObject.AS_isNumeric(actual));
                AssertHelper.floatIdentical((double)expected, (double)actual);
            });

            runConvertArrayTest<double, ASAny>(data, x => x, (expected, actual) => {
                Assert.True(ASObject.AS_isNumeric(actual.value));
                AssertHelper.floatIdentical((double)expected, (double)actual);
            });

            runConvertArrayTest(data, invalidConversion<double, GenericTypeConvertersTest_CA>);
            runConvertArrayTest(data, invalidConversion<double, GenericTypeConvertersTest_IA>);
            runConvertArrayTest(data, invalidConversion<double, GenericTypeConvertersTest_NonASType1>);
        }

        public static IEnumerable<object[]> convertFromBoolTest_data = TupleHelper.toArrays(
            Array.Empty<bool>(),
            new bool[] {false, true, true, false, true, true, true, false, false, true, false, false, false}
        );

        [Theory]
        [MemberData(nameof(convertFromBoolTest_data))]
        public void convertFromBoolTest_single(bool[] data) {
            runConvertSingleValueTest<bool, int>(data, x => x ? 1 : 0);
            runConvertSingleValueTest<bool, uint>(data, x => x ? 1u : 0u);
            runConvertSingleValueTest<bool, double>(data, x => x ? 1 : 0, AssertHelper.floatIdentical);
            runConvertSingleValueTest<bool, bool>(data, x => x);
            runConvertSingleValueTest<bool, string>(data, x => ASBoolean.AS_convertString(x));

            runConvertSingleValueTest<bool, ASObject>(data, x => x, (expected, actual) => {
                Assert.IsType<ASBoolean>(actual);
                Assert.Equal((bool)expected, (bool)actual);
            });

            runConvertSingleValueTest<bool, ASAny>(data, x => x, (expected, actual) => {
                Assert.IsType<ASBoolean>(actual.value);
                Assert.Equal((bool)expected, (bool)actual);
            });

            runConvertSingleValueTest(data, invalidConversion<bool, GenericTypeConvertersTest_CA>);
            runConvertSingleValueTest(data, invalidConversion<bool, GenericTypeConvertersTest_IA>);
            runConvertSingleValueTest(data, invalidConversion<bool, GenericTypeConvertersTest_NonASType1>);
        }

        [Theory]
        [MemberData(nameof(convertFromBoolTest_data))]
        public void convertFromBoolTest_array(bool[] data) {
            runConvertArrayTest<bool, int>(data, x => x ? 1 : 0);
            runConvertArrayTest<bool, uint>(data, x => x ? 1u : 0u);
            runConvertArrayTest<bool, double>(data, x => x ? 1 : 0, AssertHelper.floatIdentical);
            runConvertArrayTest<bool, bool>(data, x => x);
            runConvertArrayTest<bool, string>(data, x => ASBoolean.AS_convertString(x));

            runConvertArrayTest<bool, ASObject>(data, x => x, (expected, actual) => {
                Assert.IsType<ASBoolean>(actual);
                Assert.Equal((bool)expected, (bool)actual);
            });

            runConvertArrayTest<bool, ASAny>(data, x => x, (expected, actual) => {
                Assert.IsType<ASBoolean>(actual.value);
                Assert.Equal((bool)expected, (bool)actual);
            });

            runConvertArrayTest(data, invalidConversion<bool, GenericTypeConvertersTest_CA>);
            runConvertArrayTest(data, invalidConversion<bool, GenericTypeConvertersTest_IA>);
            runConvertArrayTest(data, invalidConversion<bool, GenericTypeConvertersTest_NonASType1>);
        }

        public static IEnumerable<object[]> convertFromStringTest_data = TupleHelper.toArrays(
            Array.Empty<string>(),
            new string[] {
                null, "", "abcd", "1234", null, "123.0493", "+29420", "  -13.3309887e203 ",
                "true", "false", null, null, "0", "+2.4578991e+307", "Infinity", "-Infinity",
                "NaN", "2147483647", "-2147483648", "  4292967295 ", "", "\t-.000000000004534\r\n",
                "0xabcdef", "  0x12435.998 ", "0x089aa8f7g8993", "128849e-1000", "9.040000934023903e-99",
                "&^#$@**&#^@#@$&@?<>>>{}", "0x847aa6fafdd676b76665130294cee984761"
            },
            new string[] {null, null, null, null, null}
        );

        [Theory]
        [MemberData(nameof(convertFromStringTest_data))]
        public void convertFromStringTest_single(string[] data) {
            runConvertSingleValueTest<string, int>(data, x => ASString.AS_toInt(x));
            runConvertSingleValueTest<string, uint>(data, x => ASString.AS_toUint(x));
            runConvertSingleValueTest<string, double>(data, x => ASString.AS_toNumber(x), AssertHelper.floatIdentical);
            runConvertSingleValueTest<string, bool>(data, x => ASString.AS_toBoolean(x));
            runConvertSingleValueTest<string, string>(data, x => x);

            runConvertSingleValueTest<string, ASObject>(data, x => x, (expected, actual) => {
                if (expected != null) {
                    Assert.IsType<ASString>(actual);
                    Assert.Equal((string)expected, (string)actual);
                }
                else {
                    Assert.Null(actual);
                }
            });

            runConvertSingleValueTest<string, ASAny>(data, x => x, (expected, actual) => {
                if (expected.value != null) {
                    Assert.IsType<ASString>(actual.value);
                    Assert.Equal((string)expected, (string)actual);
                }
                else {
                    AssertHelper.identical(ASAny.@null, actual);
                }
            });

            runConvertSingleValueTest(data, invalidConversion<string, GenericTypeConvertersTest_CA>);
            runConvertSingleValueTest(data, invalidConversion<string, GenericTypeConvertersTest_IA>);
            runConvertSingleValueTest(data, invalidConversion<string, GenericTypeConvertersTest_NonASType1>);
        }

        [Theory]
        [MemberData(nameof(convertFromStringTest_data))]
        public void convertFromStringTest_array(string[] data) {
            runConvertArrayTest<string, int>(data, x => ASString.AS_toInt(x));
            runConvertArrayTest<string, uint>(data, x => ASString.AS_toUint(x));
            runConvertArrayTest<string, double>(data, x => ASString.AS_toNumber(x), AssertHelper.floatIdentical);
            runConvertArrayTest<string, bool>(data, x => ASString.AS_toBoolean(x));
            runConvertArrayTest<string, string>(data, x => x);

            runConvertArrayTest<string, ASObject>(data, x => x, (expected, actual) => {
                if (expected != null) {
                    Assert.IsType<ASString>(actual);
                    Assert.Equal((string)expected, (string)actual);
                }
                else {
                    Assert.Null(actual);
                }
            });

            runConvertArrayTest<string, ASAny>(data, x => x, (expected, actual) => {
                if (expected.value != null) {
                    Assert.IsType<ASString>(actual.value);
                    Assert.Equal((string)expected, (string)actual);
                }
                else {
                    AssertHelper.identical(ASAny.@null, actual);
                }
            });

            runConvertArrayTest(data, invalidConversion<string, GenericTypeConvertersTest_CA>);
            runConvertArrayTest(data, invalidConversion<string, GenericTypeConvertersTest_IA>);
            runConvertArrayTest(data, invalidConversion<string, GenericTypeConvertersTest_NonASType1>);
        }

        public static IEnumerable<object[]> convertFromObjectToPrimitiveTest_data = TupleHelper.toArrays(
            Array.Empty<ASObject>(),
            new ASObject[] {
                new ASObject(), null, null, 0, 0u, 0.0, 1, -1, 1u,
                359425, 2758362194, -1049483453, Int32.MaxValue,
                UInt32.MaxValue, 213445554.0, -75843.50995, 0.0004583596,
                "hello", "-10.43509856", "NaN", 4.57783921e+19, "", Double.NaN,
                Double.MaxValue, -2147483648.0, "0x748aa67f3", "0x847aa6fafdd676b76665130294cee984761",
                Double.NegativeInfinity, "Infinity", true, 8574438u, "4294967295",
                "  193.500 ", " 305\n \t ", "123abcde", false, null, -0.0, "-0",
                new ASObject(), new GenericTypeConvertersTest_CA(), new GenericTypeConvertersTest_CB(),
                new ConvertibleMockObject(intValue: 103, uintValue: 1923, numberValue: 937.2039991, stringValue: "abcddedf", boolValue: true),
                new ConvertibleMockObject(intValue: -4938, uintValue: 64533, numberValue: 7.47371881e-50, stringValue: "ggq", boolValue: false),
            }
        );

        [Theory]
        [MemberData(nameof(convertFromObjectToPrimitiveTest_data))]
        public void convertFromObjectToPrimitiveTest_single(ASObject[] data) {
            runConvertSingleValueTest<ASObject, int>(data, x => (int)x);
            runConvertSingleValueTest<ASObject, uint>(data, x => (uint)x);
            runConvertSingleValueTest<ASObject, double>(data, x => (double)x, AssertHelper.floatIdentical);
            runConvertSingleValueTest<ASObject, bool>(data, x => (bool)x);
            runConvertSingleValueTest<ASObject, string>(data, x => (string)x);

            runConvertSingleValueTest<ASObject, ASObject>(data, x => x, Assert.Same);
            runConvertSingleValueTest<ASObject, ASAny>(data, x => x, AssertHelper.identical);
        }

        [Theory]
        [MemberData(nameof(convertFromObjectToPrimitiveTest_data))]
        public void convertFromObjectToPrimitiveTest_array(ASObject[] data) {
            runConvertArrayTest<ASObject, int>(data, x => (int)x);
            runConvertArrayTest<ASObject, uint>(data, x => (uint)x);
            runConvertArrayTest<ASObject, double>(data, x => (double)x, AssertHelper.floatIdentical);
            runConvertArrayTest<ASObject, bool>(data, x => (bool)x);
            runConvertArrayTest<ASObject, string>(data, x => (string)x);

            runConvertArrayTest<ASObject, ASObject>(data, x => x, Assert.Same);
            runConvertArrayTest<ASObject, ASAny>(data, x => x, AssertHelper.identical);
        }

        public static IEnumerable<object[]> convertFromAnyToPrimitiveTest_data = TupleHelper.toArrays(
            Array.Empty<ASAny>(),
            new ASAny[] {
                new ASObject(), ASAny.@null, ASAny.undefined, 0, 0u, 0.0, 1, -1, 1u,
                359425, 2758362194, -1049483453, Int32.MaxValue,
                UInt32.MaxValue, 213445554.0, -75843.50995, 0.0004583596,
                "hello", "-10.43509856", "NaN", 4.57783921e+19, "", Double.NaN,
                Double.MaxValue, -2147483648.0, "0x748aa67f3", "0x847aa6fafdd676b76665130294cee984761",
                Double.NegativeInfinity, "Infinity", true, 8574438u, "4294967295",
                "  193.500 ", " 305\n \t ", "123abcde", false, ASAny.@null, -0.0, "-0",
                ASAny.undefined, ASAny.undefined, ASAny.@null, new GenericTypeConvertersTest_CA(), new GenericTypeConvertersTest_CB(),
                new ConvertibleMockObject(intValue: 103, uintValue: 1923, numberValue: 937.2039991, stringValue: "abcddedf", boolValue: true),
                new ConvertibleMockObject(intValue: -4938, uintValue: 64533, numberValue: 7.47371881e-50, stringValue: "ggq", boolValue: false),
            }
        );

        [Theory]
        [MemberData(nameof(convertFromAnyToPrimitiveTest_data))]
        public void convertFromAnyToPrimitiveTest_single(ASAny[] data) {
            runConvertSingleValueTest<ASAny, int>(data, x => (int)x);
            runConvertSingleValueTest<ASAny, uint>(data, x => (uint)x);
            runConvertSingleValueTest<ASAny, double>(data, x => (double)x, AssertHelper.floatIdentical);
            runConvertSingleValueTest<ASAny, bool>(data, x => (bool)x);
            runConvertSingleValueTest<ASAny, string>(data, x => (string)x);

            runConvertSingleValueTest<ASAny, ASObject>(data, x => x.value, Assert.Same);
            runConvertSingleValueTest<ASAny, ASAny>(data, x => x, AssertHelper.identical);
        }

        [Theory]
        [MemberData(nameof(convertFromAnyToPrimitiveTest_data))]
        public void convertFromAnyToPrimitiveTest_array(ASAny[] data) {
            runConvertArrayTest<ASAny, int>(data, x => (int)x);
            runConvertArrayTest<ASAny, uint>(data, x => (uint)x);
            runConvertArrayTest<ASAny, double>(data, x => (double)x, AssertHelper.floatIdentical);
            runConvertArrayTest<ASAny, bool>(data, x => (bool)x);
            runConvertArrayTest<ASAny, string>(data, x => (string)x);

            runConvertArrayTest<ASAny, ASObject>(data, x => x.value, Assert.Same);
            runConvertArrayTest<ASAny, ASAny>(data, x => x, AssertHelper.identical);
        }

        public static IEnumerable<object[]> convertFromClassToPrimitiveTest_data = TupleHelper.toArrays(
            Array.Empty<GenericTypeConvertersTest_CB>(),
            new GenericTypeConvertersTest_CB[] {
                null,
                new GenericTypeConvertersTest_CB(),
                null,
                new GenericTypeConvertersTest_CB(),
                new GenericTypeConvertersTest_CB(),
            }
        );

        [Theory]
        [MemberData(nameof(convertFromClassToPrimitiveTest_data))]
        public void convertFromClassToPrimitiveTest_single(GenericTypeConvertersTest_CB[] data) {
            runConvertSingleValueTest<GenericTypeConvertersTest_CB, int>(data, x => (int)x);
            runConvertSingleValueTest<GenericTypeConvertersTest_CB, uint>(data, x => (uint)x);
            runConvertSingleValueTest<GenericTypeConvertersTest_CB, double>(data, x => (double)x, AssertHelper.floatIdentical);
            runConvertSingleValueTest<GenericTypeConvertersTest_CB, bool>(data, x => (bool)x);
            runConvertSingleValueTest<GenericTypeConvertersTest_CB, string>(data, x => (string)x);

            runConvertSingleValueTest<GenericTypeConvertersTest_CB, ASObject>(data, x => x, Assert.Same);
            runConvertSingleValueTest<GenericTypeConvertersTest_CB, ASAny>(data, x => x, AssertHelper.identical);
        }

        [Theory]
        [MemberData(nameof(convertFromClassToPrimitiveTest_data))]
        public void convertFromClassToPrimitiveTest_array(GenericTypeConvertersTest_CB[] data) {
            runConvertArrayTest<GenericTypeConvertersTest_CB, int>(data, x => (int)x);
            runConvertArrayTest<GenericTypeConvertersTest_CB, uint>(data, x => (uint)x);
            runConvertArrayTest<GenericTypeConvertersTest_CB, double>(data, x => (double)x, AssertHelper.floatIdentical);
            runConvertArrayTest<GenericTypeConvertersTest_CB, bool>(data, x => (bool)x);
            runConvertArrayTest<GenericTypeConvertersTest_CB, string>(data, x => (string)x);

            runConvertArrayTest<GenericTypeConvertersTest_CB, ASObject>(data, x => x, Assert.Same);
            runConvertArrayTest<GenericTypeConvertersTest_CB, ASAny>(data, x => x, AssertHelper.identical);
        }

        public static IEnumerable<object[]> convertFromInterfaceToPrimitiveTest_data = TupleHelper.toArrays(
            Array.Empty<GenericTypeConvertersTest_IA>(),
            new GenericTypeConvertersTest_IA[] {
                null,
                new GenericTypeConvertersTest_CB(),
                null,
                new GenericTypeConvertersTest_CB(),
                new GenericTypeConvertersTest_CB(),
            }
        );

        [Theory]
        [MemberData(nameof(convertFromInterfaceToPrimitiveTest_data))]
        public void convertFromInterfaceToPrimitiveTest_single(GenericTypeConvertersTest_IA[] data) {
            runConvertSingleValueTest<GenericTypeConvertersTest_IA, int>(data, x => (int)(ASObject)x);
            runConvertSingleValueTest<GenericTypeConvertersTest_IA, uint>(data, x => (uint)(ASObject)x);
            runConvertSingleValueTest<GenericTypeConvertersTest_IA, double>(data, x => (double)(ASObject)x, AssertHelper.floatIdentical);
            runConvertSingleValueTest<GenericTypeConvertersTest_IA, bool>(data, x => (bool)(ASObject)x);
            runConvertSingleValueTest<GenericTypeConvertersTest_IA, string>(data, x => (string)(ASObject)x);

            runConvertSingleValueTest<GenericTypeConvertersTest_IA, ASObject>(data, x => (ASObject)x, Assert.Same);
            runConvertSingleValueTest<GenericTypeConvertersTest_IA, ASAny>(data, x => (ASObject)x, AssertHelper.identical);
        }

        [Theory]
        [MemberData(nameof(convertFromInterfaceToPrimitiveTest_data))]
        public void convertFromInterfaceToPrimitiveTest_array(GenericTypeConvertersTest_IA[] data) {
            runConvertSingleValueTest<GenericTypeConvertersTest_IA, int>(data, x => (int)(ASObject)x);
            runConvertSingleValueTest<GenericTypeConvertersTest_IA, uint>(data, x => (uint)(ASObject)x);
            runConvertSingleValueTest<GenericTypeConvertersTest_IA, double>(data, x => (double)(ASObject)x, AssertHelper.floatIdentical);
            runConvertSingleValueTest<GenericTypeConvertersTest_IA, bool>(data, x => (bool)(ASObject)x);
            runConvertSingleValueTest<GenericTypeConvertersTest_IA, string>(data, x => (string)(ASObject)x);

            runConvertSingleValueTest<GenericTypeConvertersTest_IA, ASObject>(data, x => (ASObject)x, Assert.Same);
            runConvertSingleValueTest<GenericTypeConvertersTest_IA, ASAny>(data, x => (ASObject)x, AssertHelper.identical);
        }

        public static IEnumerable<object[]> convertClassAndInterfaceTest_data() {
            var testcases = new List<(Array, Type)>();

            void addTestCase(Array data, Type[] fromTypes, Type[] toTypes) {
                Array[] fromTypeArrays = new Array[fromTypes.Length];
                Type dataArrayType = data.GetType().GetElementType();

                for (int i = 0; i < fromTypes.Length; i++) {
                    if (fromTypes[i] == dataArrayType) {
                        fromTypeArrays[i] = data;
                    }
                    else {
                        fromTypeArrays[i] = Array.CreateInstance(fromTypes[i], data.Length);
                        for (int j = 0; j < data.Length; j++)
                            fromTypeArrays[i].SetValue(data.GetValue(j), j);
                    }
                }

                for (int i = 0; i < fromTypeArrays.Length; i++) {
                    for (int j = 0; j < toTypes.Length; j++)
                        testcases.Add((fromTypeArrays[i], toTypes[j]));
                }
            }

            addTestCase(
                data: Array.Empty<object>(),
                fromTypes: new[] {
                    typeof(ASObject),
                    typeof(GenericTypeConvertersTest_CA),
                    typeof(GenericTypeConvertersTest_CC),
                    typeof(GenericTypeConvertersTest_CD),
                    typeof(GenericTypeConvertersTest_IA),
                    typeof(GenericTypeConvertersTest_IC),
                    typeof(GenericTypeConvertersTest_NonASType1),
                    typeof(GenericTypeConvertersTest_NonASType2),
                },
                toTypes: new[] {
                    typeof(ASObject),
                    typeof(GenericTypeConvertersTest_CA),
                    typeof(GenericTypeConvertersTest_CB),
                    typeof(GenericTypeConvertersTest_CD),
                    typeof(GenericTypeConvertersTest_IB),
                    typeof(GenericTypeConvertersTest_IC),
                    typeof(GenericTypeConvertersTest_NonASType1),
                    typeof(GenericTypeConvertersTest_NonASType2),
                }
            );

            addTestCase(
                data: new object[4],
                fromTypes: new[] {
                    typeof(ASObject),
                    typeof(GenericTypeConvertersTest_CA),
                    typeof(GenericTypeConvertersTest_CC),
                    typeof(GenericTypeConvertersTest_CD),
                    typeof(GenericTypeConvertersTest_IA),
                    typeof(GenericTypeConvertersTest_IC),
                    typeof(GenericTypeConvertersTest_NonASType1),
                    typeof(GenericTypeConvertersTest_NonASType2),
                },
                toTypes: new[] {
                    typeof(ASObject),
                    typeof(GenericTypeConvertersTest_CA),
                    typeof(GenericTypeConvertersTest_CB),
                    typeof(GenericTypeConvertersTest_CD),
                    typeof(GenericTypeConvertersTest_IB),
                    typeof(GenericTypeConvertersTest_IC),
                    typeof(GenericTypeConvertersTest_NonASType1),
                    typeof(GenericTypeConvertersTest_NonASType2),
                }
            );

            addTestCase(
                data: new GenericTypeConvertersTest_CA[] {
                    new GenericTypeConvertersTest_CA(),
                    null, new GenericTypeConvertersTest_CA()
                },
                fromTypes: new[] {
                    typeof(ASObject),
                    typeof(GenericTypeConvertersTest_CA)
                },
                toTypes: new[] {
                    typeof(ASObject),
                    typeof(GenericTypeConvertersTest_CA),
                    typeof(GenericTypeConvertersTest_CB),
                    typeof(GenericTypeConvertersTest_IA)
                }
            );

            addTestCase(
                data: new GenericTypeConvertersTest_CB[] {
                    null,
                    new GenericTypeConvertersTest_CC(),
                    null,
                    new GenericTypeConvertersTest_CC(),
                    new GenericTypeConvertersTest_CB(),
                    null,
                    new GenericTypeConvertersTest_CB(),
                },
                fromTypes: new[] {
                    typeof(ASObject),
                    typeof(GenericTypeConvertersTest_CB),
                    typeof(GenericTypeConvertersTest_IA)
                },
                toTypes: new[] {
                    typeof(ASObject),
                    typeof(GenericTypeConvertersTest_CA),
                    typeof(GenericTypeConvertersTest_CB),
                    typeof(GenericTypeConvertersTest_CC),
                    typeof(GenericTypeConvertersTest_IA),
                    typeof(GenericTypeConvertersTest_IC)
                }
            );

            addTestCase(
                data: new GenericTypeConvertersTest_CC[] {
                    new GenericTypeConvertersTest_CC(),
                    new GenericTypeConvertersTest_CC(),
                },
                fromTypes: new[] {
                    typeof(GenericTypeConvertersTest_CB),
                    typeof(GenericTypeConvertersTest_CC),
                    typeof(GenericTypeConvertersTest_IA),
                },
                toTypes: new[] {
                    typeof(ASObject),
                    typeof(GenericTypeConvertersTest_CA),
                    typeof(GenericTypeConvertersTest_CB),
                    typeof(GenericTypeConvertersTest_CC),
                    typeof(GenericTypeConvertersTest_IA),
                    typeof(GenericTypeConvertersTest_IC)
                }
            );

            addTestCase(
                data: new GenericTypeConvertersTest_CD[] {
                    null,
                    new GenericTypeConvertersTest_CD(),
                    new GenericTypeConvertersTest_CD(),
                },
                fromTypes: new[] {
                    typeof(GenericTypeConvertersTest_IA),
                    typeof(GenericTypeConvertersTest_IB),
                    typeof(GenericTypeConvertersTest_IC),
                },
                toTypes: new[] {
                    typeof(GenericTypeConvertersTest_IA),
                    typeof(GenericTypeConvertersTest_IB),
                    typeof(GenericTypeConvertersTest_IC),
                }
            );

            addTestCase(
                data: new ASObject[] {
                    null,
                    new GenericTypeConvertersTest_CD(),
                    new GenericTypeConvertersTest_CD(),
                    new GenericTypeConvertersTest_CD(),
                    null,
                    null,
                    new GenericTypeConvertersTest_CC(),
                    new GenericTypeConvertersTest_CC(),
                    new GenericTypeConvertersTest_CB(),
                    new GenericTypeConvertersTest_CB(),
                    null,
                },
                fromTypes: new[] {
                    typeof(ASObject),
                    typeof(GenericTypeConvertersTest_IA),
                },
                toTypes: new[] {
                    typeof(GenericTypeConvertersTest_CB),
                    typeof(GenericTypeConvertersTest_CC),
                    typeof(GenericTypeConvertersTest_CD),
                    typeof(GenericTypeConvertersTest_IA),
                    typeof(GenericTypeConvertersTest_IB),
                    typeof(GenericTypeConvertersTest_IC),
                    typeof(GenericTypeConvertersTest_NonASType1),
                }
            );

            addTestCase(
                data: new object[] {
                    new GenericTypeConvertersTest_NonASType2(),
                    null,
                },
                fromTypes: new[] {
                    typeof(GenericTypeConvertersTest_NonASType1),
                    typeof(GenericTypeConvertersTest_NonASType2),
                    typeof(GenericTypeConvertersTest_IA)
                },
                toTypes: new[] {
                    typeof(GenericTypeConvertersTest_NonASType1),
                    typeof(GenericTypeConvertersTest_NonASType2),
                    typeof(ASObject),
                    typeof(GenericTypeConvertersTest_CA),
                    typeof(GenericTypeConvertersTest_IA),
                    typeof(GenericTypeConvertersTest_IB),
                }
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(convertClassAndInterfaceTest_data))]
        public void convertClassAndInterfaceTest(Array data, Type targetType) {
            Type fromType = data.GetType().GetElementType();

            MethodInfo runTestSingleMethod = typeof(GenericTypeConvertersTest)
                .GetMethod(nameof(runConvertSingleValueTest), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(fromType, targetType);

            MethodInfo runTestArrayMethod = typeof(GenericTypeConvertersTest)
                .GetMethod(nameof(runConvertArrayTest), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(fromType, targetType);

            object expectedConversion = typeof(GenericTypeConvertersTest)
                .GetMethod(nameof(referenceConversion), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(fromType, targetType)
                .CreateDelegate(typeof(Func<,>).MakeGenericType(fromType, targetType));

            object assertion = typeof(Assert).GetMethod(nameof(Assert.Same))
                .CreateDelegate(typeof(Action<,>).MakeGenericType(targetType, targetType));

            runTestSingleMethod.Invoke(this, new[] {data, expectedConversion, assertion});
            runTestArrayMethod.Invoke(this, new[] {data, expectedConversion, assertion});
        }

        public static IEnumerable<object[]> convertAnyToClassOrInterfaceTest_data() {
            var testcases = new List<(ASAny[], Type)>();

            void addTestCase(ASAny[] data, Type[] toTypes) {
                for (int j = 0; j < toTypes.Length; j++)
                    testcases.Add((data, toTypes[j]));
            }

            addTestCase(
                data: Array.Empty<ASAny>(),
                toTypes: new[] {
                    typeof(ASObject),
                    typeof(GenericTypeConvertersTest_CA),
                    typeof(GenericTypeConvertersTest_CB),
                    typeof(GenericTypeConvertersTest_CD),
                    typeof(GenericTypeConvertersTest_IB),
                    typeof(GenericTypeConvertersTest_IC),
                    typeof(GenericTypeConvertersTest_NonASType1),
                    typeof(GenericTypeConvertersTest_NonASType2),
                }
            );

            addTestCase(
                data: new ASAny[] {ASAny.undefined, ASAny.@null, ASAny.undefined, ASAny.@null},
                toTypes: new[] {
                    typeof(ASObject),
                    typeof(GenericTypeConvertersTest_CA),
                    typeof(GenericTypeConvertersTest_CB),
                    typeof(GenericTypeConvertersTest_CD),
                    typeof(GenericTypeConvertersTest_IB),
                    typeof(GenericTypeConvertersTest_IC),
                    typeof(GenericTypeConvertersTest_NonASType1),
                    typeof(GenericTypeConvertersTest_NonASType2),
                }
            );

            addTestCase(
                data: new ASAny[] {
                    new GenericTypeConvertersTest_CA(),
                    ASAny.@null,
                    ASAny.undefined,
                    new GenericTypeConvertersTest_CA()
                },
                toTypes: new[] {
                    typeof(ASObject),
                    typeof(GenericTypeConvertersTest_CA),
                    typeof(GenericTypeConvertersTest_CB),
                    typeof(GenericTypeConvertersTest_IA)
                }
            );

            addTestCase(
                data: new ASAny[] {
                    ASAny.@null,
                    ASAny.undefined,
                    new GenericTypeConvertersTest_CD(),
                    new GenericTypeConvertersTest_CD(),
                },
                toTypes: new[] {
                    typeof(GenericTypeConvertersTest_IA),
                    typeof(GenericTypeConvertersTest_IB),
                    typeof(GenericTypeConvertersTest_IC),
                }
            );

            addTestCase(
                data: new ASAny[] {
                    ASAny.@null,
                    ASAny.undefined,
                    new GenericTypeConvertersTest_CC(),
                    new GenericTypeConvertersTest_CC(),
                    new GenericTypeConvertersTest_CB(),
                    new GenericTypeConvertersTest_CB(),
                    ASAny.@null,
                    new GenericTypeConvertersTest_CD(),
                    new GenericTypeConvertersTest_CD(),
                    new GenericTypeConvertersTest_CD(),
                    ASAny.@null,
                    ASAny.undefined,
                    ASAny.undefined,
                },
                toTypes: new[] {
                    typeof(GenericTypeConvertersTest_CA),
                    typeof(GenericTypeConvertersTest_CB),
                    typeof(GenericTypeConvertersTest_CC),
                    typeof(GenericTypeConvertersTest_CD),
                    typeof(GenericTypeConvertersTest_IA),
                    typeof(GenericTypeConvertersTest_IB),
                    typeof(GenericTypeConvertersTest_IC),
                    typeof(GenericTypeConvertersTest_NonASType1),
                }
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(convertAnyToClassOrInterfaceTest_data))]
        public void convertAnyToClassOrInterfaceTest(ASAny[] data, Type targetType) {
            MethodInfo runTestSingleMethod = typeof(GenericTypeConvertersTest)
                .GetMethod(nameof(runConvertSingleValueTest), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(typeof(ASAny), targetType);

            MethodInfo runTestArrayMethod = typeof(GenericTypeConvertersTest)
                .GetMethod(nameof(runConvertArrayTest), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(typeof(ASAny), targetType);

            object expectedConversion = typeof(ASAny)
                .GetMethod(nameof(ASAny.AS_cast))
                .MakeGenericMethod(targetType)
                .CreateDelegate(typeof(Func<,>).MakeGenericType(typeof(ASAny), targetType));

            object assertion = typeof(Assert).GetMethod(nameof(Assert.Same))
                .CreateDelegate(typeof(Action<,>).MakeGenericType(targetType, targetType));

            runTestSingleMethod.Invoke(this, new[] {data, expectedConversion, assertion});
            runTestArrayMethod.Invoke(this, new[] {data, expectedConversion, assertion});
        }

        public static IEnumerable<object[]> convertInvalidValueTypeTest_data() {
            var testcases = new List<(Type, Type)>();

            Type[] legalTypes = {
                typeof(int),
                typeof(uint),
                typeof(double),
                typeof(string),
                typeof(bool),
                typeof(ASAny),
                typeof(ASObject),
                typeof(GenericTypeConvertersTest_CA),
                typeof(GenericTypeConvertersTest_IA),
            };

            for (int i = 0; i < legalTypes.Length; i++) {
                testcases.Add((legalTypes[i], typeof(GenericTypeConvertersTest_NonASType3)));
                testcases.Add((typeof(GenericTypeConvertersTest_NonASType3), legalTypes[i]));
            }

            testcases.Add((typeof(GenericTypeConvertersTest_NonASType3), typeof(GenericTypeConvertersTest_NonASType4)));
            testcases.Add((typeof(GenericTypeConvertersTest_NonASType4), typeof(GenericTypeConvertersTest_NonASType3)));

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(convertInvalidValueTypeTest_data))]
        public void convertInvalidValueTypeTest(Type fromType, Type targetType) {
            MethodInfo runTestSingleMethod = typeof(GenericTypeConvertersTest)
                .GetMethod(nameof(runConvertSingleValueTest), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(fromType, targetType);

            MethodInfo runTestArrayMethod = typeof(GenericTypeConvertersTest)
                .GetMethod(nameof(runConvertArrayTest), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(fromType, targetType);

            object expectedConversion = typeof(GenericTypeConvertersTest)
                .GetMethod(nameof(invalidConversion), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(fromType, targetType)
                .CreateDelegate(typeof(Func<,>).MakeGenericType(fromType, targetType));

            Array emptyArray = Array.CreateInstance(fromType, 0);
            Array array = Array.CreateInstance(fromType, 1);

            runTestSingleMethod.Invoke(this, new[] {array, expectedConversion, null});
            runTestArrayMethod.Invoke(this, new[] {emptyArray, expectedConversion, null});
            runTestArrayMethod.Invoke(this, new[] {array, expectedConversion, null});
        }

    }

}
