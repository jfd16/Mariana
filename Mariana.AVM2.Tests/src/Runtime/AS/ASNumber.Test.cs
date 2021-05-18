using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class ASNumberTest {

        private static readonly double NEG_ZERO = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000L));

        [Fact]
        public void constantsShouldHaveCorrectValues() {
            AssertHelper.floatIdentical(Double.MaxValue, ASNumber.MAX_VALUE);
            AssertHelper.floatIdentical(Double.PositiveInfinity, ASNumber.POSITIVE_INFINITY);
            AssertHelper.floatIdentical(Double.NegativeInfinity, ASNumber.NEGATIVE_INFINITY);
            AssertHelper.floatIdentical(Double.NaN, ASNumber.NAN);

            Assert.True(ASNumber.MIN_VALUE > 0.0);
            Assert.Equal(0.0, ASNumber.MIN_VALUE / 2.0);
        }

        public static IEnumerable<object[]> valueOfMethodTest_data = TupleHelper.toArrays(
            0.0, NEG_ZERO, 10.0, -10.0, 0.5, Double.Epsilon, Double.MinValue, Double.MaxValue, Double.PositiveInfinity, Double.NegativeInfinity, Double.NaN
        );

        [Theory]
        [MemberData(nameof(valueOfMethodTest_data))]
        public void valueOfMethodTest(double value) {
            AssertHelper.floatIdentical(value, ASNumber.valueOf(value));
            AssertHelper.floatIdentical(value, ((ASNumber)(ASObject)value).valueOf());
        }

        public static IEnumerable<object[]> toFixedMethodTest_data() {
            // Reuse test data from NumberFormatHelperTest
            return NumberFormatHelperTest.doubleToStringFixedNotationMethodTest_data
                .Where(x => (int)x[1] <= 20);
        }

        [Theory]
        [MemberData(nameof(toFixedMethodTest_data))]
        public void toFixedMethodTest(double value, int precision, string expected) {
            Assert.Equal(expected, ASNumber.toFixed(value, precision));
            Assert.Equal(expected, ((ASNumber)(ASObject)value).toFixed(precision));
        }

        public static IEnumerable<object[]> toExponentialMethodTest_data() {
            // Reuse test data from NumberFormatHelperTest
            return NumberFormatHelperTest.doubleToStringExpNotationMethodTest_data
                .Where(x => (int)x[1] <= 20);
        }

        [Theory]
        [MemberData(nameof(toExponentialMethodTest_data))]
        public void toExponentialMethodTest(double value, int precision, string expected) {
            Assert.Equal(expected, ASNumber.toExponential(value, precision));
            Assert.Equal(expected, ((ASNumber)(ASObject)value).toExponential(precision));
        }

        public static IEnumerable<object[]> toPrecisionMethodTest_data() {
            // Reuse test data from NumberFormatHelperTest
            return NumberFormatHelperTest.doubleToStringPrecisionMethodTest_data
                .Where(x => (int)x[1] <= 21);
        }

        [Theory]
        [MemberData(nameof(toPrecisionMethodTest_data))]
        public void toPrecisionMethodTest(double value, int precision, string expected) {
            Assert.Equal(expected, ASNumber.toPrecision(value, precision));
            Assert.Equal(expected, ((ASNumber)(ASObject)value).toPrecision(precision));
        }

        public static IEnumerable<object[]> toStringMethodTest_base10_data() {
            // Reuse test data from NumberFormatHelperTest
            return Enumerable.Empty<object[]>()
                .Concat(NumberFormatHelperTest.doubleToStringMethodTest_data_fixedOutput.Select(x => new[] {x[0], 10, x[1]}))
                .Concat(NumberFormatHelperTest.doubleToStringMethodTest_data_scientificOutput.Select(x => new[] {x[0], 10, x[1]}));
        }

        public static IEnumerable<object[]> toStringMethodTest_nonBase10_data() {
            // Reuse test data from NumberFormatHelperTest

            var dataSet1 = NumberFormatHelperTest.doubleIntegerToStringRadixMethodTest_data
                .Where(x => (int)x[1] != 10)
                .Select(x => ((double)x[0], (int)x[1], (string)x[2]));

            var dataSet2 = NumberFormatHelperTest.doubleToStringPow2RadixMethodTest_data
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

            return Enumerable.Concat(dataSet1, dataSet2).Distinct().Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(toStringMethodTest_base10_data))]
        [MemberData(nameof(toStringMethodTest_nonBase10_data))]
        public void toStringMethodTest(double value, int radix, string expected) {
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
        public void toLocaleStringMethodTest(double value, string expected) {
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
        public void toFixedMethodTest_invalidPrecision(double value) {
            int[] precisions = {-1, 21, -1000, 1000};
            const ErrorCode errCode = ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE;

            for (int i = 0; i < precisions.Length; i++) {
                AssertHelper.throwsErrorWithCode(errCode, () => ASNumber.toFixed(value, precisions[i]));
                AssertHelper.throwsErrorWithCode(errCode, () => ((ASNumber)(ASObject)value).toFixed(precisions[i]));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(Double.MaxValue)]
        [InlineData(Double.PositiveInfinity)]
        [InlineData(Double.NaN)]
        public void toExponentialMethodTest_invalidPrecision(double value) {
            int[] precisions = {-1, 21, -1000, 1000};
            const ErrorCode errCode = ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE;

            for (int i = 0; i < precisions.Length; i++) {
                AssertHelper.throwsErrorWithCode(errCode, () => ASNumber.toExponential(value, precisions[i]));
                AssertHelper.throwsErrorWithCode(errCode, () => ((ASNumber)(ASObject)value).toExponential(precisions[i]));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(Double.MaxValue)]
        [InlineData(Double.PositiveInfinity)]
        [InlineData(Double.NaN)]
        public void toPrecisionTest_invalidPrecision(double value) {
            int[] precisions = {-1, 0, 22, -1000, 1000};
            const ErrorCode errCode = ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE;

            for (int i = 0; i < precisions.Length; i++) {
                AssertHelper.throwsErrorWithCode(errCode, () => ASNumber.toPrecision(value, precisions[i]));
                AssertHelper.throwsErrorWithCode(errCode, () => ((ASNumber)(ASObject)value).toPrecision(precisions[i]));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(Double.MaxValue)]
        [InlineData(Double.PositiveInfinity)]
        [InlineData(Double.NaN)]
        public void toStringMethodTest_invalidRadix(double value) {
            int[] radices = {-1, 0, 1, 37, -1000, 1000};
            const ErrorCode errCode = ErrorCode.NUMBER_RADIX_OUT_OF_RANGE;

            for (int i = 0; i < radices.Length; i++) {
                AssertHelper.throwsErrorWithCode(errCode, () => ASNumber.toString(value, radices[i]));
                AssertHelper.throwsErrorWithCode(errCode, () => ((ASNumber)(ASObject)value).AS_toString(radices[i]));
            }
        }

        public static IEnumerable<object[]> numberClassRuntimeInvokeAndConstructTest_data() {
            ASObject obj1 = new ConvertibleMockObject(numberValue: 1.0);
            ASObject obj2 = new ConvertibleMockObject(numberValue: Double.NaN);

            return TupleHelper.toArrays(
                (Array.Empty<ASAny>(), 0),
                (new ASAny[] {default}, Double.NaN),
                (new ASAny[] {ASAny.@null}, 0),
                (new ASAny[] {obj1}, 1.0),
                (new ASAny[] {obj2}, Double.NaN),

                (new ASAny[] {default, obj1}, Double.NaN),
                (new ASAny[] {ASAny.@null, obj1}, 0),
                (new ASAny[] {obj1, obj2}, 1.0),
                (new ASAny[] {obj2, obj1}, Double.NaN),
                (new ASAny[] {obj1, obj2, default, obj1}, 1.0)
            );
        }

        [Theory]
        [MemberData(nameof(numberClassRuntimeInvokeAndConstructTest_data))]
        public void numberClassRuntimeInvokeAndConstructTest(ASAny[] args, double expected) {
            Class klass = Class.fromType(typeof(double));

            check(klass.invoke(args));
            check(klass.construct(args));

            void check(ASAny result) {
                Assert.IsType<ASNumber>(result.value);
                Assert.Same(klass, result.AS_class);
                AssertHelper.floatIdentical(expected, ASObject.AS_toNumber(result.value));
            }
        }

    }

}
