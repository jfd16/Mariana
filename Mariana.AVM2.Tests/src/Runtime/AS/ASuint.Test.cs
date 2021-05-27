using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class ASuintTest {

        [Fact]
        public void constantsShouldHaveCorrectValues() {
            Assert.Equal(UInt32.MinValue, ASuint.MIN_VALUE);
            Assert.Equal(UInt32.MaxValue, ASuint.MAX_VALUE);
        }

        [Fact]
        public void instancesShouldUseNumberPrototype() {
            var instance = ASObject.AS_fromUint(UInt32.MaxValue);
            Assert.Same(Class.fromType(typeof(double)).prototypeObject, instance.AS_proto);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(103943)]
        [InlineData(UInt32.MaxValue)]
        public void valueOfMethodTest(uint value) {
            Assert.Equal(value, ASuint.valueOf(value));
            Assert.Equal(value, ((ASuint)(ASObject)value).valueOf());
        }

        public static IEnumerable<object[]> toStringMethodTest_data = TupleHelper.toArrays<uint, int, string>(
            (0, 2, "0"),
            (0, 3, "0"),
            (0, 10, "0"),
            (0, 16, "0"),
            (0, 36, "0"),
            (1, 2, "1"),
            (1, 32, "1"),
            (2, 2, "10"),
            (6, 6, "10"),
            (35, 36, "z"),
            (37, 36, "11"),
            (587, 10, "587"),
            (12464877, 2, "101111100011001011101101"),
            (19383400, 5, "14430232100"),
            (784392, 8, "2774010"),
            (183772, 10, "183772"),
            (64738329, 16, "3dbd419"),
            (34443113, 26, "2n9h93"),
            (34224, 32, "11dg"),
            (95849993, 36, "1l2ebt"),
            (3563757, 3, "20201001120000"),
            (395933, 8, "1405235"),
            (755345, 16, "b8691"),
            (6574788, 26, "ea20c"),
            (124353444, 32, "3miut4"),
            (1234567890, 10, "1234567890"),
            (2147483647, 2, "1111111111111111111111111111111"),
            (2147483647, 7, "104134211161"),
            (2147483647, 10, "2147483647"),
            (2147483647, 32, "1vvvvvv"),
            (2147483647, 36, "zik0zj"),
            (2147483648, 2, "10000000000000000000000000000000"),
            (2147483648, 7, "104134211162"),
            (2147483648, 10, "2147483648"),
            (2147483648, 32, "2000000"),
            (2147483648, 36, "zik0zk"),
            (3000000005, 2, "10110010110100000101111000000101"),
            (3000000005, 7, "134225402462"),
            (3000000005, 10, "3000000005"),
            (3000000005, 16, "b2d05e05"),
            (3000000005, 32, "2pd0ng5"),
            (3000000005, 36, "1dm4eth"),
            (4294967295, 2, "11111111111111111111111111111111"),
            (4294967295, 7, "211301422353"),
            (4294967295, 10, "4294967295"),
            (4294967295, 16, "ffffffff"),
            (4294967295, 32, "3vvvvvv"),
            (4294967295, 36, "1z141z3")
        );

        [Theory]
        [MemberData(nameof(toStringMethodTest_data))]
        public void toStringMethodTest(uint value, int radix, string expected) {
            Assert.Equal(expected, ASuint.toString(value, radix));
            Assert.Equal(expected, ((ASuint)(ASObject)value).AS_toString(radix));
        }

        public static (uint val, int precision, string tofixed, string toexp)[] toFixed_toExponential_testData = {
            (0, 0, "0", "0e+0"),
            (0, 2, "0.00", "0.00e+0"),
            (0, 20, "0.00000000000000000000", "0.00000000000000000000e+0"),

            (1, 0, "1", "1e+0"),
            (1, 4, "1.0000", "1.0000e+0"),
            (1, 20, "1.00000000000000000000", "1.00000000000000000000e+0"),

            (7, 0, "7", "7e+0"),
            (7, 4, "7.0000", "7.0000e+0"),
            (7, 20, "7.00000000000000000000", "7.00000000000000000000e+0"),

            (10, 0, "10", "1e+1"),
            (10, 4, "10.0000", "1.0000e+1"),
            (10, 20, "10.00000000000000000000", "1.00000000000000000000e+1"),

            (15, 0, "15", "2e+1"),
            (15, 4, "15.0000", "1.5000e+1"),
            (15, 20, "15.00000000000000000000", "1.50000000000000000000e+1"),

            (33, 0, "33", "3e+1"),
            (33, 4, "33.0000", "3.3000e+1"),
            (33, 20, "33.00000000000000000000", "3.30000000000000000000e+1"),

            (361528, 0, "361528", "4e+5"),
            (361528, 1, "361528.0", "3.6e+5"),
            (361528, 2, "361528.00", "3.62e+5"),
            (361528, 3, "361528.000", "3.615e+5"),
            (361528, 4, "361528.0000", "3.6153e+5"),
            (361528, 5, "361528.00000", "3.61528e+5"),
            (361528, 6, "361528.000000", "3.615280e+5"),
            (361528, 10, "361528.0000000000", "3.6152800000e+5"),
            (361528, 15, "361528.000000000000000", "3.615280000000000e+5"),
            (361528, 20, "361528.00000000000000000000", "3.61528000000000000000e+5"),

            (Int32.MaxValue, 0, "2147483647", "2e+9"),
            (Int32.MaxValue, 2, "2147483647.00", "2.15e+9"),
            (Int32.MaxValue, 7, "2147483647.0000000", "2.1474836e+9"),
            (Int32.MaxValue, 8, "2147483647.00000000", "2.14748365e+9"),
            (Int32.MaxValue, 9, "2147483647.000000000", "2.147483647e+9"),
            (Int32.MaxValue, 10, "2147483647.0000000000", "2.1474836470e+9"),
            (Int32.MaxValue, 13, "2147483647.0000000000000", "2.1474836470000e+9"),
            (Int32.MaxValue, 19, "2147483647.0000000000000000000", "2.1474836470000000000e+9"),
            (Int32.MaxValue, 20, "2147483647.00000000000000000000", "2.14748364700000000000e+9"),

            (3999999981, 0, "3999999981", "4e+9"),
            (3999999981, 6, "3999999981.000000", "4.000000e+9"),
            (3999999981, 7, "3999999981.0000000", "4.0000000e+9"),
            (3999999981, 8, "3999999981.00000000", "3.99999998e+9"),
            (3999999981, 9, "3999999981.000000000", "3.999999981e+9"),
            (3999999981, 10, "3999999981.0000000000", "3.9999999810e+9"),

            (UInt32.MaxValue, 0, "4294967295", "4e+9"),
            (UInt32.MaxValue, 2, "4294967295.00", "4.29e+9"),
            (UInt32.MaxValue, 7, "4294967295.0000000", "4.2949673e+9"),
            (UInt32.MaxValue, 8, "4294967295.00000000", "4.29496730e+9"),
            (UInt32.MaxValue, 9, "4294967295.000000000", "4.294967295e+9"),
            (UInt32.MaxValue, 10, "4294967295.0000000000", "4.2949672950e+9"),
            (UInt32.MaxValue, 13, "4294967295.0000000000000", "4.2949672950000e+9"),
            (UInt32.MaxValue, 19, "4294967295.0000000000000000000", "4.2949672950000000000e+9"),
            (UInt32.MaxValue, 20, "4294967295.00000000000000000000", "4.29496729500000000000e+9")
        };

        public static IEnumerable<object[]> toFixedMethodTest_data = toFixed_toExponential_testData.Select(x => new object[] {x.val, x.precision, x.tofixed});

        [Theory]
        [MemberData(nameof(toFixedMethodTest_data))]
        public void toFixedMethodTest(uint value, int precision, string expected) {
            Assert.Equal(expected, ASuint.toFixed(value, precision));
            Assert.Equal(expected, ((ASuint)(ASObject)value).toFixed(precision));
        }

        public static IEnumerable<object[]> toExponentialMethodTest_data = toFixed_toExponential_testData.Select(x => new object[] {x.val, x.precision, x.toexp});

        [Theory]
        [MemberData(nameof(toExponentialMethodTest_data))]
        public void toExponentialMethodTest(uint value, int precision, string expected) {
            Assert.Equal(expected, ASuint.toExponential(value, precision));
            Assert.Equal(expected, ((ASuint)(ASObject)value).toExponential(precision));
        }

        public static IEnumerable<object[]> toPrecisionMethodTest_data = TupleHelper.toArrays<uint, int, string>(
            (0, 1, "0"),
            (0, 2, "0.0"),
            (0, 5, "0.0000"),
            (0, 21, "0.00000000000000000000"),

            (1, 1, "1"),
            (1, 4, "1.000"),
            (1, 21, "1.00000000000000000000"),

            (7, 1, "7"),
            (7, 4, "7.000"),
            (7, 21, "7.00000000000000000000"),

            (10, 1, "1e+1"),
            (10, 2, "10"),
            (10, 3, "10.0"),
            (10, 5, "10.000"),
            (10, 21, "10.0000000000000000000"),

            (15, 1, "2e+1"),
            (15, 2, "15"),
            (15, 4, "15.00"),
            (15, 21, "15.0000000000000000000"),

            (33, 1, "3e+1"),
            (33, 2, "33"),
            (33, 3, "33.0"),
            (33, 8, "33.000000"),
            (33, 21, "33.0000000000000000000"),

            (361528, 1, "4e+5"),
            (361528, 2, "3.6e+5"),
            (361528, 3, "3.62e+5"),
            (361528, 4, "3.615e+5"),
            (361528, 5, "3.6153e+5"),
            (361528, 6, "361528"),
            (361528, 7, "361528.0"),
            (361528, 10, "361528.0000"),
            (361528, 15, "361528.000000000"),
            (361528, 21, "361528.000000000000000"),

            (Int32.MaxValue, 1, "2e+9"),
            (Int32.MaxValue, 3, "2.15e+9"),
            (Int32.MaxValue, 8, "2.1474836e+9"),
            (Int32.MaxValue, 9, "2.14748365e+9"),
            (Int32.MaxValue, 10, "2147483647"),
            (Int32.MaxValue, 11, "2147483647.0"),
            (Int32.MaxValue, 13, "2147483647.000"),
            (Int32.MaxValue, 20, "2147483647.0000000000"),
            (Int32.MaxValue, 21, "2147483647.00000000000"),

            (3999999981, 1, "4e+9"),
            (3999999981, 7, "4.000000e+9"),
            (3999999981, 8, "4.0000000e+9"),
            (3999999981, 9, "3.99999998e+9"),
            (3999999981, 10, "3999999981"),
            (3999999981, 11, "3999999981.0"),

            (UInt32.MaxValue, 1, "4e+9"),
            (UInt32.MaxValue, 3, "4.29e+9"),
            (UInt32.MaxValue, 8, "4.2949673e+9"),
            (UInt32.MaxValue, 9, "4.29496730e+9"),
            (UInt32.MaxValue, 10, "4294967295"),
            (UInt32.MaxValue, 11, "4294967295.0"),
            (UInt32.MaxValue, 13, "4294967295.000"),
            (UInt32.MaxValue, 20, "4294967295.0000000000"),
            (UInt32.MaxValue, 21, "4294967295.00000000000")
        );

        [Theory]
        [MemberData(nameof(toPrecisionMethodTest_data))]
        public void toPrecisionMethodTest(uint value, int precision, string expected) {
            Assert.Equal(expected, ASuint.toPrecision(value, precision));
            Assert.Equal(expected, ((ASuint)(ASObject)value).toPrecision(precision));
        }

        [Theory]
        [InlineData(0, "0")]
        [InlineData(1, "1")]
        [InlineData(17695, "17695")]
        [InlineData(1234567890, "1234567890")]
        [InlineData(Int32.MaxValue, "2147483647")]
        [InlineData(UInt32.MaxValue, "4294967295")]
        public void toLocaleStringMethodTest(uint value, string expected) {
            CultureInfo oldCulture = CultureInfo.CurrentCulture;
            try {
                CultureInfo.CurrentCulture = new CultureInfo("en-US") {
                    NumberFormat = new NumberFormatInfo() {NegativeSign = "~"}
                };
                Assert.Equal(expected, ASuint.toLocaleString(value));
                Assert.Equal(expected, ((ASuint)(ASObject)value).toLocaleString());
            }
            finally {
                CultureInfo.CurrentCulture = oldCulture;
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(100)]
        [InlineData(Int32.MaxValue)]
        [InlineData(UInt32.MaxValue)]
        public void toFixedMethodTest_invalidPrecision(uint value) {
            int[] precisions = {-1, 21, -1000, 1000};
            const ErrorCode errCode = ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE;

            for (int i = 0; i < precisions.Length; i++) {
                AssertHelper.throwsErrorWithCode(errCode, () => ASuint.toFixed(value, precisions[i]));
                AssertHelper.throwsErrorWithCode(errCode, () => ((ASuint)(ASObject)value).toFixed(precisions[i]));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(100)]
        [InlineData(Int32.MaxValue)]
        [InlineData(UInt32.MaxValue)]
        public void toExponentialMethodTest_invalidPrecision(uint value) {
            int[] precisions = {-1, 21, -1000, 1000};
            const ErrorCode errCode = ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE;

            for (int i = 0; i < precisions.Length; i++) {
                AssertHelper.throwsErrorWithCode(errCode, () => ASuint.toExponential(value, precisions[i]));
                AssertHelper.throwsErrorWithCode(errCode, () => ((ASuint)(ASObject)value).toExponential(precisions[i]));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(100)]
        [InlineData(Int32.MaxValue)]
        [InlineData(UInt32.MaxValue)]
        public void toPrecisionMethodTest_invalidPrecision(uint value) {
            int[] precisions = {0, -1, 22, -1000, 1000};
            const ErrorCode errCode = ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE;

            for (int i = 0; i < precisions.Length; i++) {
                AssertHelper.throwsErrorWithCode(errCode, () => ASuint.toPrecision(value, precisions[i]));
                AssertHelper.throwsErrorWithCode(errCode, () => ((ASuint)(ASObject)value).toPrecision(precisions[i]));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(100)]
        [InlineData(Int32.MaxValue)]
        [InlineData(UInt32.MaxValue)]
        public void toStringMethodTest_invalidRadix(uint value) {
            int[] radices = {-1, 0, 1, 37, -1000, 1000};
            const ErrorCode errCode = ErrorCode.NUMBER_RADIX_OUT_OF_RANGE;

            for (int i = 0; i < radices.Length; i++) {
                AssertHelper.throwsErrorWithCode(errCode, () => ASuint.toString(value, radices[i]));
                AssertHelper.throwsErrorWithCode(errCode, () => ((ASuint)(ASObject)value).AS_toString(radices[i]));
            }
        }

        public static IEnumerable<object[]> uintClassRuntimeInvokeAndConstructTest_data() {
            ASObject obj1 = new ConvertibleMockObject(uintValue: 1244);
            ASObject obj2 = new ConvertibleMockObject(uintValue: 35667);

            return TupleHelper.toArrays<ASAny[], uint>(
                (Array.Empty<ASAny>(), 0),
                (new ASAny[] {default}, 0),
                (new ASAny[] {ASAny.@null}, 0),
                (new ASAny[] {obj1}, 1244),
                (new ASAny[] {obj2}, 35667),
                (new ASAny[] {default, obj1}, 0),
                (new ASAny[] {ASAny.@null, obj1}, 0),
                (new ASAny[] {obj1, obj2}, 1244),
                (new ASAny[] {obj2, obj1}, 35667),
                (new ASAny[] {obj1, obj2, default, obj1}, 1244)
            );
        }

        [Theory]
        [MemberData(nameof(uintClassRuntimeInvokeAndConstructTest_data))]
        public void uintClassRuntimeInvokeAndConstructTest(ASAny[] args, uint expected) {
            Class klass = Class.fromType(typeof(uint));

            check(klass.invoke(args));
            check(klass.construct(args));

            void check(ASAny result) {
                Assert.IsType<ASuint>(result.value);
                Assert.Same(klass, result.AS_class);
                Assert.Equal(expected, ASObject.AS_toUint(result.value));
            }
        }

    }

}
