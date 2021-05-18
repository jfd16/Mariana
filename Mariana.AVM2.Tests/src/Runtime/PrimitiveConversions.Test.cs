using System;
using System.Collections.Generic;
using System.Linq;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class PrimitiveConversionsTest {

        private static readonly Class s_intClass = Class.fromType<int>();

        public static IEnumerable<object[]> intTestData = TupleHelper.toArrays(
            0, 1, -1, 2, -2,
            127, 128, 129, -127, -128, -129,
            255, 256, 257,
            4718, 9993,
            32767, 32768, 32769, -32767, -32768, -32769,
            65535, 65536, 65537,
            123456789, 193847113,
            Int32.MaxValue, Int32.MinValue, -Int32.MaxValue
        );

        [Theory]
        [MemberData(nameof(intTestData))]
        public void intObjectConvertRoundTripTest(int value) {
            ASObject asObj = value;
            check(asObj);

            ASAny asAny = value;
            check(asAny.value);

            check(ASObject.AS_fromInt(value));
            check(ASAny.AS_fromInt(value).value);

            void check(ASObject obj) {
                Assert.IsType<ASint>(obj);
                Assert.Same(s_intClass, obj.AS_class);

                Assert.Equal(value, ASObject.AS_toInt(obj));
                Assert.Equal(value, ASAny.AS_toInt(new ASAny(obj)));
            }
        }

        [Theory]
        [MemberData(nameof(intTestData))]
        public void intToUintConvertTest(int value) {
            Assert.Equal((uint)value, (uint)(ASObject)value);
            Assert.Equal((uint)value, ASObject.AS_toUint(ASObject.AS_fromInt(value)));
            Assert.Equal((uint)value, (uint)(ASAny)value);
            Assert.Equal((uint)value, ASAny.AS_toUint(ASAny.AS_fromInt(value)));
        }

        [Theory]
        [MemberData(nameof(intTestData))]
        public void intToNumberConvertTest(int value) {
            Assert.Equal((double)value, (double)(ASObject)value);
            Assert.Equal((double)value, ASObject.AS_toNumber(ASObject.AS_fromInt(value)));
            Assert.Equal((double)value, (double)(ASAny)value);
            Assert.Equal((double)value, ASAny.AS_toNumber(ASAny.AS_fromInt(value)));
        }

        [Theory]
        [MemberData(nameof(intTestData))]
        public void intToBooleanConvertTest(int value) {
            Assert.Equal(value != 0, (bool)(ASObject)value);
            Assert.Equal(value != 0, ASObject.AS_toBoolean(ASObject.AS_fromInt(value)));
            Assert.Equal(value != 0, (bool)(ASAny)value);
            Assert.Equal(value != 0, ASAny.AS_toBoolean(ASAny.AS_fromInt(value)));
        }

        public static IEnumerable<object[]> intToStringConvertTest_data = TupleHelper.toArrays(
            (0, "0"),
            (1, "1"),
            (2, "2"),
            (-1, "-1"),
            (-2, "-2"),
            (48, "48"),
            (-75, "-75"),
            (127, "127"),
            (128, "128"),
            (129, "129"),
            (-127, "-127"),
            (-128, "-128"),
            (-129, "-129"),
            (255, "255"),
            (256, "256"),
            (257, "257"),
            (32767, "32767"),
            (32768, "32768"),
            (32769, "32769"),
            (-32767, "-32767"),
            (-32768, "-32768"),
            (-32769, "-32769"),
            (65535, "65535"),
            (65536, "65536"),
            (65537, "65537"),
            (7564194, "7564194"),
            (1234567890, "1234567890"),
            (-2135497086, "-2135497086"),
            (Int32.MaxValue, "2147483647"),
            (Int32.MinValue, "-2147483648")
        );

        [Theory]
        [MemberData(nameof(intToStringConvertTest_data))]
        public void intToStringConvertTest(int value, string expected) {
            Assert.Equal(expected, ASint.AS_convertString(value));

            Assert.Equal(expected, (string)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_coerceString(ASObject.AS_fromInt(value)));
            Assert.Equal(expected, ASObject.AS_convertString(ASObject.AS_fromInt(value)));
            Assert.Equal(expected, (string)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_coerceString(ASAny.AS_fromInt(value)));
            Assert.Equal(expected, ASAny.AS_convertString(ASAny.AS_fromInt(value)));
        }

        public static IEnumerable<object[]> objectToIntConvertTest_data = TupleHelper.toArrays(
            (new ConvertibleMockObject(intValue: 1244), 1244),
            (null, 0)
        );

        [Theory]
        [MemberData(nameof(objectToIntConvertTest_data))]
        public void objectToIntConvertTest(ASObject obj, int expected) {
            Assert.Equal(expected, (int)obj);
            Assert.Equal(expected, ASObject.AS_toInt(obj));
        }

        public static IEnumerable<object[]> anyToIntConvertTest_data = TupleHelper.toArrays(
            (new ConvertibleMockObject(intValue: 1244), 1244),
            (ASAny.@null, 0),
            (ASAny.undefined, 0)
        );

        [Theory]
        [MemberData(nameof(anyToIntConvertTest_data))]
        public void anyToIntConvertTest(ASAny obj, int expected) {
            Assert.Equal(expected, (int)obj);
            Assert.Equal(expected, ASAny.AS_toInt(obj));
        }

        private static readonly Class s_uintClass = Class.fromType<uint>();

        public static IEnumerable<object[]> uintTestData = TupleHelper.toArrays<uint>(
            0, 1,
            127, 128, 129,
            255, 256, 257,
            4718, 9993,
            32767, 32768, 32769,
            65535, 65536, 65537,
            123456789, 193847113,
            (uint)Int32.MaxValue,
            (uint)Int32.MaxValue + 1,
            UInt32.MaxValue
        );

        [Theory]
        [MemberData(nameof(uintTestData))]
        public void uintObjectConvertRoundTripTest(uint value) {
            ASObject asObj = value;
            check(asObj);

            ASAny asAny = value;
            check(asAny.value);

            check(ASObject.AS_fromUint(value));
            check(ASAny.AS_fromUint(value).value);

            void check(ASObject obj) {
                Assert.IsType<ASuint>(obj);
                Assert.Same(s_uintClass, obj.AS_class);
                Assert.Equal(value, ASObject.AS_toUint(obj));
                Assert.Equal(value, ASAny.AS_toUint(new ASAny(obj)));
            }
        }

        [Theory]
        [MemberData(nameof(uintTestData))]
        public void uintToIntConvertTest(uint value) {
            Assert.Equal((int)value, (int)(ASObject)value);
            Assert.Equal((int)value, ASObject.AS_toInt(ASObject.AS_fromUint(value)));
            Assert.Equal((int)value, (int)(ASAny)value);
            Assert.Equal((int)value, ASAny.AS_toInt(ASAny.AS_fromUint(value)));
        }

        [Theory]
        [MemberData(nameof(uintTestData))]
        public void uintToNumberConvertTest(uint value) {
            Assert.Equal((double)value, (double)(ASObject)value);
            Assert.Equal((double)value, ASObject.AS_toNumber(ASObject.AS_fromUint(value)));
            Assert.Equal((double)value, (double)(ASAny)value);
            Assert.Equal((double)value, ASAny.AS_toNumber(ASAny.AS_fromUint(value)));
        }

        [Theory]
        [MemberData(nameof(uintTestData))]
        public void uintToBooleanConvertTest(uint value) {
            Assert.Equal(value != 0, (bool)(ASObject)value);
            Assert.Equal(value != 0, ASObject.AS_toBoolean(ASObject.AS_fromUint(value)));
            Assert.Equal(value != 0, (bool)(ASAny)value);
            Assert.Equal(value != 0, ASAny.AS_toBoolean(ASAny.AS_fromUint(value)));
        }

        public static IEnumerable<object[]> uintToStringConvertTest_data = TupleHelper.toArrays<uint, string>(
            (0, "0"),
            (1, "1"),
            (2, "2"),
            (48, "48"),
            (75, "75"),
            (127, "127"),
            (128, "128"),
            (129, "129"),
            (255, "255"),
            (256, "256"),
            (257, "257"),
            (32767, "32767"),
            (32768, "32768"),
            (32769, "32769"),
            (65535, "65535"),
            (65536, "65536"),
            (65537, "65537"),
            (7564194, "7564194"),
            (1234567890, "1234567890"),
            (Int32.MaxValue, "2147483647"),
            ((uint)Int32.MaxValue + 1, "2147483648"),
            (3980245617, "3980245617"),
            (UInt32.MaxValue, "4294967295")
        );

        [Theory]
        [MemberData(nameof(uintToStringConvertTest_data))]
        public void uintToStringConvertTest(uint value, string expected) {
            Assert.Equal(expected, ASuint.AS_convertString(value));

            Assert.Equal(expected, (string)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_coerceString(ASObject.AS_fromUint(value)));
            Assert.Equal(expected, ASObject.AS_convertString(ASObject.AS_fromUint(value)));
            Assert.Equal(expected, (string)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_coerceString(ASAny.AS_fromUint(value)));
            Assert.Equal(expected, ASAny.AS_convertString(ASAny.AS_fromUint(value)));
        }

        public static IEnumerable<object[]> objectToUintConvertTest_data = TupleHelper.toArrays(
            (new ConvertibleMockObject(uintValue: 1244), 1244u),
            (null, 0u)
        );

        [Theory]
        [MemberData(nameof(objectToUintConvertTest_data))]
        public void objectToUintConvertTest(ASObject obj, uint expected) {
            Assert.Equal(expected, (uint)obj);
            Assert.Equal(expected, ASObject.AS_toUint(obj));
        }

        public static IEnumerable<object[]> anyToUintConvertTest_data = TupleHelper.toArrays<ASAny, uint>(
            (new ConvertibleMockObject(uintValue: 1244), 1244u),
            (ASAny.@null, 0),
            (ASAny.undefined, 0)
        );

        [Theory]
        [MemberData(nameof(anyToUintConvertTest_data))]
        public void anyToUintConvertTest(ASAny obj, uint expected) {
            Assert.Equal(expected, (uint)obj);
            Assert.Equal(expected, ASAny.AS_toUint(obj));
        }

        private static readonly Class s_numberClass = Class.fromType<double>();

        private static readonly double NEG_ZERO = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000L));

        public static IEnumerable<object[]> numberToObjectConvertRoundTripTest_data = TupleHelper.toArrays(
            0.0,
            NEG_ZERO,
            1.0,
            -1.0,
            0.5,
            -0.5,
            1.5,
            -1.5,
            Double.Epsilon,
            -Double.Epsilon,
            Double.MaxValue,
            -Double.MaxValue,
            Double.PositiveInfinity,
            Double.NegativeInfinity,
            Double.NaN
        );

        [Theory]
        [MemberData(nameof(numberToObjectConvertRoundTripTest_data))]
        public void numberToObjectConvertRoundTripTest(double value) {
            ASObject asObj = value;
            check(asObj);

            ASAny asAny = value;
            check(asAny.value);

            check(ASObject.AS_fromNumber(value));
            check(ASAny.AS_fromNumber(value).value);

            void check(ASObject obj) {
                Assert.IsType<ASNumber>(obj);
                Assert.Same(s_numberClass, obj.AS_class);
                AssertHelper.floatIdentical(value, ASObject.AS_toNumber(obj));
                AssertHelper.floatIdentical(value, ASAny.AS_toNumber(new ASAny(obj)));
            }
        }

        public static IEnumerable<object[]> numberToBoolConvertTest_data = TupleHelper.toArrays(
            (0.0, false),
            (NEG_ZERO, false),
            (Double.NaN, false),

            (1.0, true),
            (-1.0, true),
            (0.5, true),
            (-0.5, true),
            (10.0, true),
            (-10.0, true),
            (Double.MaxValue, true),
            (Double.MinValue, true),
            (Double.Epsilon, true),
            (-Double.Epsilon, true),
            (Double.PositiveInfinity, true),
            (Double.NegativeInfinity, true)
        );

        [Theory]
        [MemberData(nameof(numberToBoolConvertTest_data))]
        public void shouldConvertToBoolean(double value, bool expected) {
            Assert.Equal(expected, ASNumber.AS_toBoolean(value));
            Assert.Equal(expected, (bool)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_toBoolean(ASObject.AS_fromNumber(value)));
            Assert.Equal(expected, (bool)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_toBoolean(ASAny.AS_fromNumber(value)));
        }

        public static IEnumerable<object[]> numberToIntUintConvertTest_data = TupleHelper.toArrays(
            (0.0, 0),
            (NEG_ZERO, 0),

            ( Double.Epsilon,           0),
            (-Double.Epsilon,           0),
            ( 2.2250738585072014e-308,  0),
            (-2.2250738585072014e-308,  0),
            ( 0.0048571143,             0),
            (-0.0048571143,             0),
            ( 0.4,                      0),
            (-0.4,                      0),
            ( 0.5,                      0),
            (-0.5,                      0),
            ( 0.75,                     0),
            (-0.75,                     0),
            ( Math.BitDecrement(1.0),   0),
            (-Math.BitDecrement(1.0),   0),

            ( 1.0,                      1),
            (-1.0,                     -1),
            ( Math.BitIncrement(1.0),   1),
            (-Math.BitIncrement(1.0),  -1),
            ( 1.4,                      1),
            (-1.4,                     -1),
            ( 1.5,                      1),
            (-1.5,                     -1),
            ( 1.6,                      1),
            (-1.6,                     -1),

            (Math.BitDecrement(2.0), 1),
            (2.0, 2),
            (Math.BitIncrement(2.0), 2),

            (1435.994821, 1435),
            (-488291456.3, -488291456),

            ( Math.Pow(2, 31) - 1.0,                    Int32.MaxValue),
            ( Math.BitIncrement(Math.Pow(2, 31) - 1.0), Int32.MaxValue),
            ( Math.Pow(2, 31) - 0.5,                    Int32.MaxValue),
            ( Math.BitDecrement(Math.Pow(2, 31)),       Int32.MaxValue),
            ( Math.Pow(2, 31),                          Int32.MinValue),
            ( Math.BitIncrement(Math.Pow(2, 31)),       Int32.MinValue),
            ( Math.Pow(2, 31) + 0.5,                    Int32.MinValue),
            ( Math.BitDecrement(Math.Pow(2, 31) + 1.0), Int32.MinValue),
            (-Math.Pow(2, 31),                          Int32.MinValue),
            (-Math.BitIncrement(Math.Pow(2, 31)),       Int32.MinValue),
            (-Math.Pow(2, 31) - 0.5,                    Int32.MinValue),
            (-Math.BitDecrement(Math.Pow(2, 31) + 1.0), Int32.MinValue),

            ( Math.Pow(2, 32) - 1.0,                    -1),
            ( Math.BitIncrement(Math.Pow(2, 32) - 1.0), -1),
            ( Math.Pow(2, 32) - 0.5,                    -1),
            ( Math.BitDecrement(Math.Pow(2, 32)),       -1),
            (-Math.Pow(2, 32) + 1.0,                     1),
            (-Math.BitIncrement(Math.Pow(2, 32) - 1.0),  1),
            (-Math.Pow(2, 32) + 0.5,                     1),
            (-Math.BitDecrement(Math.Pow(2, 32)),        1),
            ( Math.Pow(2, 32),                           0),
            ( Math.BitIncrement(Math.Pow(2, 32)),        0),
            (-Math.Pow(2, 32),                           0),
            (-Math.BitIncrement(Math.Pow(2, 32)),        0),
            ( Math.Pow(2, 32) + 1.0,                     1),
            (-Math.Pow(2, 32) - 1.0,                    -1),

            ( Math.Pow(2, 32) + 17.56,                  17),
            ( Math.Pow(2, 32) - 17.56,                 -18),
            (-Math.Pow(2, 32) + 17.56,                  18),
            (-Math.Pow(2, 32) - 17.56,                 -17),
            ( Math.Pow(2, 45) + 17.56,                  17),
            ( Math.Pow(2, 45) - 17.56,                 -18),
            (-Math.Pow(2, 45) + 17.56,                  18),
            (-Math.Pow(2, 45) - 17.56,                 -17),
            ( Math.Pow(2, 51) + 17.5,                   17),
            ( Math.Pow(2, 51) - 17.5,                  -18),
            (-Math.Pow(2, 51) + 17.5,                   18),
            (-Math.Pow(2, 51) - 17.5,                  -17),
            ( Math.Pow(2, 52) + 18.0,                   18),
            ( Math.Pow(2, 52) - 18.0,                  -18),
            (-Math.Pow(2, 52) + 18.0,                   18),
            (-Math.Pow(2, 52) - 18.0,                  -18),

            ( Math.Pow(2, 53) - 1.0,                    -1),
            (-Math.Pow(2, 53) + 1.0,                     1),
            ( Math.Pow(2, 53),                           0),
            (-Math.Pow(2, 53),                           0),
            ( Math.Pow(2, 53) + 12.0,                   12),
            (-Math.Pow(2, 53) + 12.0,                   12),
            (-Math.Pow(2, 53) - 12.0,                  -12),

            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 1),            -2),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 1),             2),
            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 3),            -8),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 3),             8),
            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 11),   -(1 << 11)),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 11),    (1 << 11)),
            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 20),   -(1 << 20)),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 20),    (1 << 20)),
            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 31),    (1 << 31)),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 31),    (1 << 31)),
            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 32),            0),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 32),            0),
            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 33),            0),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 33),            0),
            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 36),            0),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 36),            0),

            (Double.MaxValue, 0),
            (Double.MinValue, 0),
            (Double.PositiveInfinity, 0),
            (Double.NegativeInfinity, 0),
            (Double.NaN, 0)
        );

        [Theory]
        [MemberData(nameof(numberToIntUintConvertTest_data))]
        public void numberToIntConvertTest(double value, int expected) {
            Assert.Equal(expected, ASNumber.AS_toInt(value));
            Assert.Equal(expected, (int)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_toInt(ASObject.AS_fromNumber(value)));
            Assert.Equal(expected, (int)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_toInt(ASAny.AS_fromNumber(value)));
        }

        [Theory]
        [MemberData(nameof(numberToIntUintConvertTest_data))]
        public void numberToUintConvertTest(double value, int expected) {
            Assert.Equal((uint)expected, ASNumber.AS_toUint(value));
            Assert.Equal((uint)expected, (uint)(ASObject)value);
            Assert.Equal((uint)expected, ASObject.AS_toUint(ASObject.AS_fromNumber(value)));
            Assert.Equal((uint)expected, (uint)(ASAny)value);
            Assert.Equal((uint)expected, ASAny.AS_toUint(ASAny.AS_fromNumber(value)));
        }

        public static IEnumerable<object[]> numberToStringConvertTest_data() {
            // Reuse test data from NumberFormatHelperTest
            return Enumerable.Concat(
                NumberFormatHelperTest.doubleToStringMethodTest_data_fixedOutput,
                NumberFormatHelperTest.doubleToStringMethodTest_data_scientificOutput
            );
        }

        [Theory]
        [MemberData(nameof(numberToStringConvertTest_data))]
        public void numberToStringConvertTest(double value, string expected) {
            Assert.Equal(expected, ASNumber.AS_convertString(value));

            Assert.Equal(expected, (string)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_coerceString(ASObject.AS_fromNumber(value)));
            Assert.Equal(expected, ASObject.AS_convertString(ASObject.AS_fromNumber(value)));

            Assert.Equal(expected, (string)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_coerceString(ASAny.AS_fromNumber(value)));
            Assert.Equal(expected, ASAny.AS_convertString(ASAny.AS_fromNumber(value)));
        }

        public static IEnumerable<object[]> objectToNumberConvertTest_data = TupleHelper.toArrays(
            (null, 0.0),
            (new ConvertibleMockObject(numberValue: 0.0), 0.0),
            (new ConvertibleMockObject(numberValue: NEG_ZERO), NEG_ZERO),
            (new ConvertibleMockObject(numberValue: 1.0), 1.0),
            (new ConvertibleMockObject(numberValue: -1.0), -1.0),
            (new ConvertibleMockObject(numberValue: Double.PositiveInfinity), Double.PositiveInfinity),
            (new ConvertibleMockObject(numberValue: Double.NegativeInfinity), Double.NegativeInfinity),
            (new ConvertibleMockObject(numberValue: Double.NaN), Double.NaN)
        );

        [Theory]
        [MemberData(nameof(objectToNumberConvertTest_data))]
        public void objectToNumberConvertTest(ASObject obj, double expected) {
            AssertHelper.floatIdentical(expected, (double)obj);
            AssertHelper.floatIdentical(expected, ASObject.AS_toNumber(obj));
        }

        public static IEnumerable<object[]> anyToNumberConvertTest_data = TupleHelper.toArrays<ASAny, double>(
            (ASAny.@null, 0.0),
            (ASAny.undefined, Double.NaN),
            (new ConvertibleMockObject(numberValue: 0.0), 0.0),
            (new ConvertibleMockObject(numberValue: NEG_ZERO), NEG_ZERO),
            (new ConvertibleMockObject(numberValue: 1.0), 1.0),
            (new ConvertibleMockObject(numberValue: -1.0), -1.0),
            (new ConvertibleMockObject(numberValue: Double.PositiveInfinity), Double.PositiveInfinity),
            (new ConvertibleMockObject(numberValue: Double.NegativeInfinity), Double.NegativeInfinity),
            (new ConvertibleMockObject(numberValue: Double.NaN), Double.NaN)
        );

        [Theory]
        [MemberData(nameof(anyToNumberConvertTest_data))]
        public void anyToNumberConvertTest(ASAny obj, double expected) {
            AssertHelper.floatIdentical(expected, (double)obj);
            AssertHelper.floatIdentical(expected, ASAny.AS_toNumber(obj));
        }

        private static readonly Class s_stringClass = Class.fromType<string>();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abcde")]
        [InlineData("abc\0def\uFFFF")]
        public void stringObjectConvertRoundTripTest(string value) {
            ASObject asObj1 = value;
            ASObject asObj2 = ASObject.AS_fromString(value);

            ASAny asAny1 = value;
            ASAny asAny2 = ASAny.AS_fromString(value);

            if (value == null) {
                Assert.Null(asObj1);
                Assert.Null(asObj2);
                Assert.True(asAny1.isNull);
                Assert.True(asAny2.isNull);
            }
            else {
                check(asObj1);
                check(asObj2);
                check(asAny1.value);
                check(asAny2.value);
            }

            void check(ASObject obj) {
                Assert.IsType<ASString>(obj);
                Assert.Same(s_stringClass, obj.AS_class);
                Assert.Equal(value, ASObject.AS_coerceString(obj));
                Assert.Equal(value, ASObject.AS_convertString(obj));
                Assert.Equal(value, ASAny.AS_coerceString(new ASAny(obj)));
                Assert.Equal(value, ASAny.AS_convertString(new ASAny(obj)));
            }
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("0", true)]
        [InlineData("null", true)]
        [InlineData("false", true)]
        [InlineData("12345678", true)]
        [InlineData("abc\0def\uFFFF", true)]
        public void stringToBooleanConvertTest(string value, bool expected) {
            Assert.Equal(expected, ASString.AS_toBoolean(value));
            Assert.Equal(expected, (bool)(ASObject)value);
        }

        public static IEnumerable<object[]> stringToIntUintConvertTest_data = TupleHelper.toArrays(
            ("0", 0),
            ("+0", 0),
            ("-0", 0),
            ("0000", 0),
            ("1", 1),
            ("+1", 1),
            ("-1", -1),
            ("00001", 1),
            ("-00001", -1),
            ("100", 100),
            ("+3333", 3333),
            ("123456789", 123456789),
            ("-123456789", -123456789),
            ("2147483647", 2147483647),
            ("-2147483647", -2147483647),
            ("2147483648", unchecked((int)2147483648u)),
            ("-2147483648", -2147483648),
            ("4294967295", -1),
            ("-4294967295", 1),
            ("-4294967296", 0),
            ("8589934596", 4),
            ("9007199254740991", -1),
            ("9007199255110380", 369388),
            ("-129127208515967007305", -145993),

            ("0x0", 0),
            ("-0x0", 0),
            ("+0x0", 0),
            ("0X1", 1),
            ("-0X0001", -1),
            ("0x1b3c", 0x1b3c),
            ("0X12345678", 0x12345678),
            ("0x000000000012345678", 0x12345678),
            ("-0x000000000012345678", -0x12345678),
            ("0x9abcdef", 0x9abcdef),
            ("0x7FfFfFfF", 0x7FFFFFFF),
            ("-0x7FfFfFfF", -0x7FFFFFFF),
            ("0xffffffff", -1),
            ("0x1b3a6cc79", unchecked((int)0xb3a6cc79u)),
            ("0x166300c764421c0", 0x764421c0),

            ("-0x1674aa6300cfffffffd", 3),
            ("  17482  ", 17482),
            (" \n\t \u200b 30\f\v \u3000", 30),
            ("  -0x1333\t\n", -0x1333),
            ("\r\n    0x166300c764421c0   ", 0x764421c0),

            ("0.999999999999999", 0),
            ("0.9999999999999999999999999999999999", 1),
            ("-4775.039482", -4775),
            ("  -9007199255110380.4839488373 ", -369388),
            ("\t \u3000  2.381889E+24  \r\n ", -536870912),
            ("\t \u3000  -2.381889e24  \r\n ", 536870912),
            ("1.3e-4", 0),
            ("Infinity", 0),
            ("NaN", 0),

            ("", 0),
            ("abc", 0),
            ("a123", 0),
            ("+", 0),
            ("-", 0),
            (".", 0),
            (".1234", 0),
            ("$1234", 0),
            ("0xg", 0),
            ("0x", 0),
            ("0x+1234", 0),
            ("0x-1234", 0),
            ("0x 1234", 0),
            ("+ 1234", 0),
            ("- 1234", 0),
            ("(1234)", 0)
        );

        [Theory]
        [MemberData(nameof(stringToIntUintConvertTest_data))]
        public void stringToIntConvertTest(string value, int expected) {
            Assert.Equal(expected, ASString.AS_toInt(value));
            Assert.Equal(expected, (int)(ASObject)value);
        }

        [Theory]
        [MemberData(nameof(stringToIntUintConvertTest_data))]
        public void stringToUintConvertTest(string value, int expected) {
            Assert.Equal((uint)expected, ASString.AS_toUint(value));
            Assert.Equal((uint)expected, (uint)(ASObject)value);
        }

        public static IEnumerable<object[]> stringToNumberConvertTest_data = TupleHelper.toArrays(
            ("0", 0),
            ("0.0", 0),
            ("+0.0", 0),
            ("000", 0),
            (".0", 0),
            ("+.0", 0),
            ("0.", 0),
            ("00.000", 0),
            ("0e0", 0),
            ("0e100", 0),
            ("0e+1", 0),
            ("+0e+100", 0),
            ("0.0e100", 0),
            (".0000e+10000", 0),
            ("0000.0000e-500000", 0),

            ("-0", NEG_ZERO),
            ("-.0", NEG_ZERO),
            ("-0.", NEG_ZERO),

            ("1", 1),
            ("-1", -1),
            ("+0000001", 1),
            ("1458", 1458),
            ("9999999", 9999999),
            ("+2147483647", 2147483647),
            ("-4294967296", -4294967296),
            ("1000200003", 1000200003),
            ("10002000030", 10002000030),
            ("00009007199254740991", 9007199254740991),
            ("9007199254740997", 9007199254740996),
            ("1000000000000000000000000000000002", 1e+33),
            ("00004329483243000493353200352", 4.3294832430004935e+24),
            ("17976931348623157" + new string('0', 292), Double.MaxValue),

            ("1.0", 1),
            ("-1.00000", -1),
            ("26453.00000000000000000000000000000000000000", 26453),
            ("9007199254740993.0000000000000000001", 9007199254740994),
            ("384546.99000033207", 384546.99000033207),
            ("-.0006849993", -0.0006849993),
            ("495831.59965", 495831.59965),
            ("." + new string('0', 307) + "22250738585072014", 2.2250738585072014e-308),
            ("0." + new string('0', 323) + "494065645841246", Double.Epsilon),

            ("1e0", 1),
            ("1.e+0", 1),
            ("-1e-0", -1),
            ("+2.445E6", 2.445e+6),
            ("0.000002445000000000000e+12", 2.445e+6),
            ("2445000" + new string('0', 400) + "e-400", 2.445e+6),
            ("0.009007199254740992e+18", 9007199254740992),
            ("1.797693134862315e+308", 1.797693134862315e+308),
            ("3.8454699000033265e+5", 384546.99000033265),
            ("0." + new string('0', 200) + "17976931348623157e+509", Double.MaxValue),
            ("500e-326", Double.Epsilon),
            ("6.5e-323", Double.Epsilon * 13),
            ("1e+400", Double.PositiveInfinity),
            ("0.000000000024e-314", 0),

            ("0x0", 0),
            ("0X123456789", 4886718345),
            ("0XABCDEF", 11259375),
            ("-0x12345678900abc1d1ee1abd", -3.521251666818939e+26),
            ("0xfffffffffffffb" + new string('f', 242), Double.MaxValue),
            ("0xfffffffffffffc" + new string('0', 242), Double.PositiveInfinity),

            ("  \u2000 \u200B  \u2003\u3000  \u205F\n  4.5888", 4.5888),
            (" \n\n\n  -0X400", -1024),
            ("\t 123  \r\n \u205F  ", 123),

            ("Infinity", Double.PositiveInfinity),
            ("   Infinity", Double.PositiveInfinity),
            ("-Infinity", Double.NegativeInfinity),
            ("NaN", Double.NaN),

            ("", 0),
            ("      ", 0),
            ("  \r\n  \t ", 0),
            ("  \u2000 \u200B  \u2003\u3000  \u205F\n  ", 0),

            ("123,456", Double.NaN),
            ("123_456", Double.NaN),
            ("abcdef", Double.NaN),
            ("0xgabcdef", Double.NaN),
            ("$123456", Double.NaN),
            ("0x.123456", Double.NaN),
            ("0x1234e+1", Double.NaN),
            ("\x081234", Double.NaN),
            ("\t  \r\n 1839548a \u205F  ", Double.NaN),
            ("INFINITY", Double.NaN)
        );

        [Theory]
        [MemberData(nameof(stringToNumberConvertTest_data))]
        public void stringToNumberConvertTest(string value, double expected) {
            AssertHelper.floatIdentical(expected, ASString.AS_toNumber(value));
            AssertHelper.floatIdentical(expected, (double)(ASObject)value);
        }

        public static IEnumerable<object[]> objectToStringConvertTest_data = TupleHelper.toArrays(
            (null, null, "null"),
            (new ConvertibleMockObject(stringValue: ""), "", ""),
            (new ConvertibleMockObject(stringValue: "abcdef"), "abcdef", "abcdef"),
            (new ConvertibleMockObject(stringValue: "\u0000\ud800\udfff\ufffe\uffff"), "\u0000\ud800\udfff\ufffe\uffff", "\u0000\ud800\udfff\ufffe\uffff")
        );

        [Theory]
        [MemberData(nameof(objectToStringConvertTest_data))]
        public void objectToStringConvertTest(ASObject obj, string expectedCoerce, string expectedConvert) {
            Assert.Equal(expectedCoerce, (string)obj);
            Assert.Equal(expectedCoerce, ASObject.AS_coerceString(obj));
            Assert.Equal(expectedConvert, ASObject.AS_convertString(obj));
        }

        public static IEnumerable<object[]> anyToStringConvertTest_data = TupleHelper.toArrays<ASAny, string, string>(
            (ASAny.@null, null, "null"),
            (ASAny.undefined, null, "undefined"),
            (new ConvertibleMockObject(stringValue: ""), "", ""),
            (new ConvertibleMockObject(stringValue: "abcdef"), "abcdef", "abcdef"),
            (new ConvertibleMockObject(stringValue: "\u0000\ud800\udfff\ufffe\uffff"), "\u0000\ud800\udfff\ufffe\uffff", "\u0000\ud800\udfff\ufffe\uffff")
        );

        [Theory]
        [MemberData(nameof(anyToStringConvertTest_data))]
        public void anyToStringConvertTest(ASAny obj, string expectedCoerce, string expectedConvert) {
            Assert.Equal(expectedCoerce, (string)obj);
            Assert.Equal(expectedCoerce, ASAny.AS_coerceString(obj));
            Assert.Equal(expectedConvert, ASAny.AS_convertString(obj));
        }



        private static readonly Class s_booleanClass = Class.fromType<bool>();

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void boolToObjectConvetRoundTripTest(bool value) {
            ASObject asObj = value;
            check(asObj);

            ASAny asAny = value;
            check(asAny.value);

            check(ASObject.AS_fromBoolean(value));
            check(ASAny.AS_fromBoolean(value).value);

            void check(ASObject o) {
                Assert.IsType<ASBoolean>(o);
                Assert.Same(s_booleanClass, o.AS_class);
                Assert.Equal(value, ASObject.AS_toBoolean(o));
                Assert.Equal(value, ASAny.AS_toBoolean(new ASAny(o)));
            }
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(true, 1)]
        public void boolToIntConvertTest(bool value, int expected) {
            Assert.Equal(expected, (int)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_toInt(ASObject.AS_fromBoolean(value)));
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(true, 1)]
        public void boolToUintConvertTest(bool value, uint expected) {
            Assert.Equal(expected, (uint)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_toUint(ASAny.AS_fromBoolean(value)));
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(true, 1)]
        public void boolToNumberConvertTest(bool value, double expected) {
            Assert.Equal(expected, (double)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_toNumber(ASObject.AS_fromBoolean(value)));
            Assert.Equal(expected, (double)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_toNumber(ASAny.AS_fromBoolean(value)));
        }

        [Theory]
        [InlineData(false, "false")]
        [InlineData(true, "true")]
        public void boolToStringConvertTest(bool value, string expected) {
            Assert.Equal(expected, ASBoolean.AS_convertString(value));

            Assert.Equal(expected, (string)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_coerceString(ASObject.AS_fromBoolean(value)));
            Assert.Equal(expected, ASObject.AS_convertString(ASObject.AS_fromBoolean(value)));
            Assert.Equal(expected, (string)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_coerceString(ASAny.AS_fromBoolean(value)));
            Assert.Equal(expected, ASAny.AS_convertString(ASAny.AS_fromBoolean(value)));
        }

        public static IEnumerable<object[]> objectToBoolConvertTest_data = TupleHelper.toArrays(
            (new ConvertibleMockObject(boolValue: false), false),
            (new ConvertibleMockObject(boolValue: true), true),
            (null, false)
        );

        [Theory]
        [MemberData(nameof(objectToBoolConvertTest_data))]
        public void objectToBoolConvertTest(ASObject obj, bool expected) {
            Assert.Equal(expected, (bool)obj);
            Assert.Equal(expected, ASObject.AS_toBoolean(obj));
        }

        public static IEnumerable<object[]> anyToBoolConvertTest_data = TupleHelper.toArrays<ASAny, bool>(
            (new ConvertibleMockObject(boolValue: false), false),
            (new ConvertibleMockObject(boolValue: true), true),
            (ASAny.@null, false),
            (ASAny.undefined, false)
        );

        [Theory]
        [MemberData(nameof(anyToBoolConvertTest_data))]
        public void anyToBoolConvertTest(ASAny obj, bool expected) {
            Assert.Equal(expected, (bool)obj);
            Assert.Equal(expected, ASAny.AS_toBoolean(obj));
        }

        public static IEnumerable<object[]> objectToAnyConvertRoundTripTest_data = TupleHelper.toArrays<ASObject>(
            null,
            new ASObject(),
            new ASArray(),
            0,
            1u,
            1.5,
            "hello",
            true,
            new ConvertibleMockObject(intValue: 111)
        );

        [Theory]
        [MemberData(nameof(objectToAnyConvertRoundTripTest_data))]
        public void objectToAnyConvertRoundTripTest(ASObject obj) {
            ASAny any = obj;
            Assert.True(any.isDefined);
            Assert.Same(obj, any.value);
            Assert.Same(obj, (ASObject)any);

            any = new ASAny(obj);
            Assert.True(any.isDefined);
            Assert.Same(obj, any.value);
            Assert.Same(obj, (ASObject)any);
        }

    }

}
