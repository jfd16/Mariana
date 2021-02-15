using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class ASNumberTest {

        private static readonly Class s_numberClass = Class.fromType<double>();

        private static readonly double NEG_ZERO = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000L));

        private static void assertNumberEqual(double expected, double actual) {
            if (Double.IsNaN(expected))
                Assert.True(Double.IsNaN(actual));
            else if (expected == 0.0)
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
            else
                Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> shouldBoxNumberValue_data = new double[] {
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
            Double.NaN,
        }.Select(x => new object[] {x});

        [Theory]
        [MemberData(nameof(shouldBoxNumberValue_data))]
        public void shouldBoxNumberValue(double value) {
            ASObject asObj = value;
            check(asObj);

            ASAny asAny = value;
            check(asAny.value);

            check(ASObject.AS_fromNumber(value));
            check(ASAny.AS_fromNumber(value).value);

            void check(ASObject o) {
                Assert.IsType<ASNumber>(o);
                Assert.Same(s_numberClass, o.AS_class);
                assertNumberEqual(value, ASObject.AS_toNumber(o));
                assertNumberEqual(value, ASAny.AS_toNumber(new ASAny(o)));
            }
        }

        [Fact]
        public void nullShouldConvertToZero() {
            assertNumberEqual(0.0, (double)(ASObject)null);
            assertNumberEqual(0.0, ASObject.AS_toNumber(null));
        }

        [Fact]
        public void undefinedShouldConvertToNaN() {
            assertNumberEqual(Double.NaN, (double)default(ASAny));
            assertNumberEqual(Double.NaN, ASAny.AS_toNumber(default));
        }

        [Fact]
        public void constantsShouldHaveCorrectValues() {
            assertNumberEqual(Double.MaxValue, ASNumber.MAX_VALUE);
            assertNumberEqual(Double.PositiveInfinity, ASNumber.POSITIVE_INFINITY);
            assertNumberEqual(Double.NegativeInfinity, ASNumber.NEGATIVE_INFINITY);
            assertNumberEqual(Double.NaN, ASNumber.NAN);

            Assert.True(ASNumber.MIN_VALUE > 0.0);
            Assert.Equal(0.0, ASNumber.MIN_VALUE / 2.0);
        }

        public static IEnumerable<object[]> shouldConvertToBoolean_data = new (double, bool)[] {
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
            (Double.NegativeInfinity, true),
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(shouldConvertToBoolean_data))]
        public void shouldConvertToBoolean(double value, bool expected) {
            Assert.Equal(expected, ASNumber.AS_toBoolean(value));
            Assert.Equal(expected, (bool)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_toBoolean(ASObject.AS_fromNumber(value)));
            Assert.Equal(expected, (bool)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_toBoolean(ASAny.AS_fromNumber(value)));
        }

        public static IEnumerable<object[]> shouldConvertToIntUint_data = new (double, int)[] {
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
            (Double.NaN, 0),
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(shouldConvertToIntUint_data))]
        public void shouldConvertToIntUint(double value, int expected) {
            Assert.Equal(expected, ASNumber.AS_toInt(value));
            Assert.Equal(expected, (int)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_toInt(ASObject.AS_fromNumber(value)));
            Assert.Equal(expected, (int)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_toInt(ASAny.AS_fromNumber(value)));

            Assert.Equal((uint)expected, ASNumber.AS_toUint(value));
            Assert.Equal((uint)expected, (uint)(ASObject)value);
            Assert.Equal((uint)expected, ASObject.AS_toUint(ASObject.AS_fromNumber(value)));
            Assert.Equal((uint)expected, (uint)(ASAny)value);
            Assert.Equal((uint)expected, ASAny.AS_toUint(ASAny.AS_fromNumber(value)));
        }

        [Theory]
        [MemberData(nameof(shouldBoxNumberValue_data))]
        public void valueOf_shouldReturnValue(double value) {
            assertNumberEqual(value, ASNumber.valueOf(value));
            assertNumberEqual(value, ((ASNumber)(ASObject)value).valueOf());
        }

        [Theory]
        [MemberData(nameof(shouldBoxNumberValue_data))]
        public void shouldConvertFromObject(double value) {
            ASObject obj = new SpyObjectWithConversions {numberValue = value};

            assertNumberEqual(value, (double)obj);
            assertNumberEqual(value, ASObject.AS_toNumber(obj));
            assertNumberEqual(value, (double)(ASAny)obj);
            assertNumberEqual(value, ASAny.AS_toNumber((ASAny)obj));
        }

        [Fact]
        public void classInvokeOrConstruct_shouldConvertFirstArg() {
            ASObject obj1 = new SpyObjectWithConversions {numberValue = 1.0};
            ASObject obj2 = new SpyObjectWithConversions {numberValue = Double.NaN};

            check(s_numberClass.invoke(new ASAny[] {}), 0);
            check(s_numberClass.invoke(new ASAny[] {default}), Double.NaN);
            check(s_numberClass.invoke(new ASAny[] {ASAny.@null}), 0);
            check(s_numberClass.invoke(new ASAny[] {obj1}), 1.0);
            check(s_numberClass.invoke(new ASAny[] {obj2}), Double.NaN);

            check(s_numberClass.invoke(new ASAny[] {default, obj1}), Double.NaN);
            check(s_numberClass.invoke(new ASAny[] {ASAny.@null, obj1}), 0);
            check(s_numberClass.invoke(new ASAny[] {obj1, obj2}), 1.0);
            check(s_numberClass.invoke(new ASAny[] {obj2, obj1}), Double.NaN);
            check(s_numberClass.invoke(new ASAny[] {obj1, obj2, default, obj1}), 1.0);

            check(s_numberClass.construct(new ASAny[] {}), 0);
            check(s_numberClass.construct(new ASAny[] {default}), Double.NaN);
            check(s_numberClass.construct(new ASAny[] {ASAny.@null}), 0);
            check(s_numberClass.construct(new ASAny[] {obj1}), 1.0);
            check(s_numberClass.construct(new ASAny[] {obj2}), Double.NaN);

            check(s_numberClass.construct(new ASAny[] {default, obj1}), Double.NaN);
            check(s_numberClass.construct(new ASAny[] {ASAny.@null, obj1}), 0);
            check(s_numberClass.construct(new ASAny[] {obj1, obj2}), 1.0);
            check(s_numberClass.construct(new ASAny[] {obj2, obj1}), Double.NaN);
            check(s_numberClass.construct(new ASAny[] {obj1, obj2, default, obj1}), 1.0);

            void check(ASAny o, double value) {
                Assert.IsType<ASNumber>(o.value);
                Assert.Same(s_numberClass, o.AS_class);
                assertNumberEqual(value, ASObject.AS_toNumber(o.value));
            }
        }

        public static IEnumerable<object[]> shouldConvertToString_data() {
            // Reuse test data from NumberFormatHelperTest
            return Enumerable.Concat(
                NumberFormatHelperTest.doubleToString_shouldFormatFixed_data,
                NumberFormatHelperTest.doubleToString_shouldFormatScientific_data
            );
        }

        [Theory]
        [MemberData(nameof(shouldConvertToString_data))]
        public void shouldConvertToString(double value, string expected) {
            Assert.Equal(expected, ASNumber.AS_convertString(value));
            Assert.Equal(expected, ASNumber.toString(value, 10));

            Assert.Equal(expected, (string)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_coerceString(ASObject.AS_fromNumber(value)));
            Assert.Equal(expected, ASObject.AS_convertString(ASObject.AS_fromNumber(value)));

            Assert.Equal(expected, (string)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_coerceString(ASAny.AS_fromNumber(value)));
            Assert.Equal(expected, ASAny.AS_convertString(ASAny.AS_fromNumber(value)));

            Assert.Equal(expected, ((ASNumber)(ASObject)value).AS_toString(10));
        }

        public static IEnumerable<object[]> toFixed_shouldFormatNumber_data() {
            // Reuse test data from NumberFormatHelperTest
            return NumberFormatHelperTest.doubleToStringFixedNotation_shouldFormat_data
                .Where(x => (int)x[1] <= 20);
        }

        [Theory]
        [MemberData(nameof(toFixed_shouldFormatNumber_data))]
        public void toFixed_shouldFormatNumber(double value, int precision, string expected) {
            Assert.Equal(expected, ASNumber.toFixed(value, precision));
            Assert.Equal(expected, ((ASNumber)(ASObject)value).toFixed(precision));
        }

        public static IEnumerable<object[]> toExponential_shouldFormatNumber_data() {
            // Reuse test data from NumberFormatHelperTest
            return NumberFormatHelperTest.doubleToStringExpNotation_shouldFormat_data
                .Where(x => (int)x[1] <= 20);
        }

        [Theory]
        [MemberData(nameof(toExponential_shouldFormatNumber_data))]
        public void toExponential_shouldFormatNumber(double value, int precision, string expected) {
            Assert.Equal(expected, ASNumber.toExponential(value, precision));
            Assert.Equal(expected, ((ASNumber)(ASObject)value).toExponential(precision));
        }

        public static IEnumerable<object[]> toPrecision_shouldFormatNumber_data() {
            // Reuse test data from NumberFormatHelperTest
            return NumberFormatHelperTest.doubleToStringPrecision_shouldFormat_data
                .Where(x => (int)x[1] <= 21);
        }

        [Theory]
        [MemberData(nameof(toPrecision_shouldFormatNumber_data))]
        public void toPrecision_shouldFormatNumber(double value, int precision, string expected) {
            Assert.Equal(expected, ASNumber.toPrecision(value, precision));
            Assert.Equal(expected, ((ASNumber)(ASObject)value).toPrecision(precision));
        }

        public static IEnumerable<object[]> toString_shouldFormatNumber_nonBase10_data() {
            // Reuse test data from NumberFormatHelperTest

            var dataSet1 = NumberFormatHelperTest.doubleIntegerToStringRadix_shouldFormat_data
                .Where(x => (int)x[1] != 10)
                .Select(x => ((double)x[0], (int)x[1], (string)x[2]));

            var dataSet2 = NumberFormatHelperTest.doubleToStringPow2Radix_shouldFormat_data
                .SelectMany(x => {
                    double val = (double)x[0];
                    if (Double.IsFinite(val) && val != Math.Truncate(val))
                        return Enumerable.Empty<(double, int, string)>();

                    return new (double, int, string)[] {
                        (val, 2, (string)x[1]),
                        (val, 4, (string)x[2]),
                        (val, 8, (string)x[3]),
                        (val, 16, (string)x[4]),
                        (val, 32, (string)x[5]),
                    };
                });

            return Enumerable.Concat(dataSet1, dataSet2).Distinct().Select(x => new object[] {x.Item1, x.Item2, x.Item3});
        }

        [Theory]
        [MemberData(nameof(toString_shouldFormatNumber_nonBase10_data))]
        public void toString_shouldFormatNumber_nonBase10(double value, int radix, string expected) {
            Assert.Equal(expected, ASNumber.toString(value, radix));
            Assert.Equal(expected, ((ASNumber)(ASObject)value).AS_toString(radix));
        }

        [Theory]
        [InlineData(0, "0")]
        [InlineData(1, "1")]
        [InlineData(-1, "~1")]
        [InlineData(17695, "17695")]
        [InlineData(-17695, "~17695")]
        [InlineData(0.5, "0_5")]
        [InlineData(-2.554, "~2_554")]
        [InlineData(1e+200, "1E#200")]
        [InlineData(1e-200, "1E~200")]
        [InlineData(Double.PositiveInfinity, "INF")]
        [InlineData(Double.NegativeInfinity, "~INF")]
        [InlineData(Double.NaN, "NAN")]
        public void toLocaleString_shouldFormatNumber(double value, string expected) {
            CultureInfo oldCulture = CultureInfo.CurrentCulture;
            try {
                CultureInfo.CurrentCulture = new CultureInfo("en-US") {
                    NumberFormat = new NumberFormatInfo() {
                        NegativeSign = "~",
                        PositiveSign = "#",
                        NumberDecimalSeparator = "_",
                        PositiveInfinitySymbol = "INF",
                        NegativeInfinitySymbol = "~INF",
                        NaNSymbol = "NAN",
                    }
                };
                Assert.Equal(expected, ASNumber.toLocaleString(value));
            }
            finally {
                CultureInfo.CurrentCulture = oldCulture;
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(Double.MaxValue)]
        [InlineData(Double.PositiveInfinity)]
        [InlineData(Double.NaN)]
        public void toFixed_toExponential_toPrecision_shouldThrowOnInvalidPrecision(double value) {
            check(() => ASNumber.toFixed(value, -1));
            check(() => ASNumber.toFixed(value, 21));
            check(() => ASNumber.toFixed(value, -1000));
            check(() => ASNumber.toFixed(value, 1000));

            check(() => ((ASNumber)(ASObject)value).toFixed(-1));
            check(() => ((ASNumber)(ASObject)value).toFixed(21));
            check(() => ((ASNumber)(ASObject)value).toFixed(-1000));
            check(() => ((ASNumber)(ASObject)value).toFixed(1000));

            check(() => ASNumber.toExponential(value, -1));
            check(() => ASNumber.toExponential(value, 21));
            check(() => ASNumber.toExponential(value, -1000));
            check(() => ASNumber.toExponential(value, 1000));

            check(() => ((ASNumber)(ASObject)value).toExponential(-1));
            check(() => ((ASNumber)(ASObject)value).toExponential(21));
            check(() => ((ASNumber)(ASObject)value).toExponential(-1000));
            check(() => ((ASNumber)(ASObject)value).toExponential(1000));

            check(() => ASNumber.toPrecision(value, 0));
            check(() => ASNumber.toPrecision(value, -1));
            check(() => ASNumber.toPrecision(value, 22));
            check(() => ASNumber.toPrecision(value, -1000));
            check(() => ASNumber.toPrecision(value, 1000));

            check(() => ((ASNumber)(ASObject)value).toPrecision(0));
            check(() => ((ASNumber)(ASObject)value).toPrecision(-1));
            check(() => ((ASNumber)(ASObject)value).toPrecision(22));
            check(() => ((ASNumber)(ASObject)value).toPrecision(-1000));
            check(() => ((ASNumber)(ASObject)value).toPrecision(1000));

            void check(Action f) => AssertHelper.throwsErrorWithCode(ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE, f);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(Double.MaxValue)]
        [InlineData(Double.PositiveInfinity)]
        [InlineData(Double.NaN)]
        public void toString_shouldThrowOnInvalidRadix(double value) {
            check(() => ASNumber.toString(value, -1));
            check(() => ASNumber.toString(value, 0));
            check(() => ASNumber.toString(value, 1));
            check(() => ASNumber.toString(value, 37));
            check(() => ASNumber.toString(value, -1000));
            check(() => ASNumber.toString(value, 1000));

            check(() => ((ASNumber)(ASObject)value).AS_toString(-1));
            check(() => ((ASNumber)(ASObject)value).AS_toString(0));
            check(() => ((ASNumber)(ASObject)value).AS_toString(1));
            check(() => ((ASNumber)(ASObject)value).AS_toString(37));
            check(() => ((ASNumber)(ASObject)value).AS_toString(-1000));
            check(() => ((ASNumber)(ASObject)value).AS_toString(1000));

            void check(Action f) => AssertHelper.throwsErrorWithCode(ErrorCode.NUMBER_RADIX_OUT_OF_RANGE, f);
        }

    }

}
