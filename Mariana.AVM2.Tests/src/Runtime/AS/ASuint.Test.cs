using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.TestDoubles;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class ASuintTest {

        private static readonly Class s_uintClass = Class.fromType<uint>();

        public static IEnumerable<object[]> s_uintTestData = new uint[] {
            0, 1,
            127, 128, 129,
            255, 256, 257,
            4718, 9993,
            32767, 32768, 32769,
            65535, 65536, 65537,
            123456789, 193847113,
            (uint)Int32.MaxValue,
            (uint)Int32.MaxValue + 1,
            UInt32.MaxValue,
        }.Select(x => new object[] {x});

        [Theory]
        [MemberData(nameof(s_uintTestData))]
        public void shouldBoxUIntegerValue(uint value) {
            ASObject asObj = value;
            check(asObj);

            ASAny asAny = value;
            check(asAny.value);

            check(ASObject.AS_fromUint(value));
            check(ASAny.AS_fromUint(value).value);

            void check(ASObject o) {
                Assert.IsType<ASuint>(o);
                Assert.Same(s_uintClass, o.AS_class);
                Assert.Equal(value, ASObject.AS_toUint(o));
                Assert.Equal(value, ASAny.AS_toUint(new ASAny(o)));
            }
        }

        [Fact]
        public void nullAndUndefinedShouldConvertToZero() {
            Assert.Equal(0u, (uint)(ASObject)null);
            Assert.Equal(0u, ASObject.AS_toUint(null));
            Assert.Equal(0u, (uint)default(ASAny));
            Assert.Equal(0u, ASAny.AS_toUint(default));
        }

        [Fact]
        public void constantsShouldHaveCorrectValues() {
            Assert.Equal(UInt32.MinValue, ASuint.MIN_VALUE);
            Assert.Equal(UInt32.MaxValue, ASuint.MAX_VALUE);
        }

        [Theory]
        [MemberData(nameof(s_uintTestData))]
        public void shouldConvertToUintNumberBoolean(uint value) {
            Assert.Equal((int)value, (int)(ASObject)value);
            Assert.Equal((int)value, ASObject.AS_toInt(ASObject.AS_fromUint(value)));
            Assert.Equal((int)value, (int)(ASAny)value);
            Assert.Equal((int)value, ASAny.AS_toInt(ASAny.AS_fromUint(value)));

            Assert.Equal((double)value, (double)(ASObject)value);
            Assert.Equal((double)value, ASObject.AS_toNumber(ASObject.AS_fromUint(value)));
            Assert.Equal((double)value, (double)(ASAny)value);
            Assert.Equal((double)value, ASAny.AS_toNumber(ASAny.AS_fromUint(value)));

            Assert.Equal(value != 0, (bool)(ASObject)value);
            Assert.Equal(value != 0, ASObject.AS_toBoolean(ASObject.AS_fromUint(value)));
            Assert.Equal(value != 0, (bool)(ASAny)value);
            Assert.Equal(value != 0, ASAny.AS_toBoolean(ASAny.AS_fromUint(value)));
        }

        [Theory]
        [MemberData(nameof(s_uintTestData))]
        public void valueOf_shouldReturnValue(uint value) {
            Assert.Equal(value, ASuint.valueOf(value));
            Assert.Equal(value, ((ASuint)(ASObject)value).valueOf());
        }

        public static IEnumerable<object[]> shouldConvertToString_data = new (uint, string)[] {
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
            (UInt32.MaxValue, "4294967295"),
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(shouldConvertToString_data))]
        public void shouldConvertToString(uint value, string expected) {
            Assert.Equal(expected, (string)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_coerceString(ASObject.AS_fromUint(value)));
            Assert.Equal(expected, ASObject.AS_convertString(ASObject.AS_fromUint(value)));
            Assert.Equal(expected, (string)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_coerceString(ASAny.AS_fromUint(value)));
            Assert.Equal(expected, ASAny.AS_convertString(ASAny.AS_fromUint(value)));

            Assert.Equal(expected, ASuint.AS_convertString(value));
            Assert.Equal(expected, ASuint.toString(value, 10));
            Assert.Equal(expected, ((ASuint)(ASObject)value).AS_toString());
            Assert.Equal(expected, ((ASuint)(ASObject)value).AS_toString(10));
        }

        public static IEnumerable<object[]> toString_shouldConvertToString_nonBase10_data = new (uint, int, string)[] {
            (0, 2, "0"),
            (0, 3, "0"),
            (0, 16, "0"),
            (0, 36, "0"),
            (1, 2, "1"),
            (1, 32, "1"),
            (2, 2, "10"),
            (6, 6, "10"),
            (35, 36, "z"),
            (37, 36, "11"),
            (12464877, 2, "101111100011001011101101"),
            (19383400, 5, "14430232100"),
            (784392, 8, "2774010"),
            (64738329, 16, "3dbd419"),
            (34443113, 26, "2n9h93"),
            (34224, 32, "11dg"),
            (95849993, 36, "1l2ebt"),
            (3563757, 3, "20201001120000"),
            (395933, 8, "1405235"),
            (755345, 16, "b8691"),
            (6574788, 26, "ea20c"),
            (124353444, 32, "3miut4"),
            (2147483647, 2, "1111111111111111111111111111111"),
            (2147483647, 7, "104134211161"),
            (2147483647, 32, "1vvvvvv"),
            (2147483647, 36, "zik0zj"),
            (2147483648, 2, "10000000000000000000000000000000"),
            (2147483648, 7, "104134211162"),
            (2147483648, 32, "2000000"),
            (2147483648, 36, "zik0zk"),
            (3000000005, 2, "10110010110100000101111000000101"),
            (3000000005, 7, "134225402462"),
            (3000000005, 16, "b2d05e05"),
            (3000000005, 32, "2pd0ng5"),
            (3000000005, 36, "1dm4eth"),
            (4294967295, 2, "11111111111111111111111111111111"),
            (4294967295, 7, "211301422353"),
            (4294967295, 16, "ffffffff"),
            (4294967295, 32, "3vvvvvv"),
            (4294967295, 36, "1z141z3"),
        }.Select(x => new object[] {x.Item1, x.Item2, x.Item3});

        [Theory]
        [MemberData(nameof(toString_shouldConvertToString_nonBase10_data))]
        public void toString_shouldConvertToString_nonBase10(uint value, int radix, string expected) {
            Assert.Equal(expected, ASuint.toString(value, radix));
            Assert.Equal(expected, ((ASuint)(ASObject)value).AS_toString(radix));
        }

        public static IEnumerable<object[]> toFixed_toExponential_shouldFormatNumber_data = new (uint, int, string, string)[] {
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
            (UInt32.MaxValue, 20, "4294967295.00000000000000000000", "4.29496729500000000000e+9"),
        }.Select(x => new object[] {x.Item1, x.Item2, x.Item3, x.Item4});

        [Theory]
        [MemberData(nameof(toFixed_toExponential_shouldFormatNumber_data))]
        public void toFixed_toExponential_shouldFormatNumber(uint value, int precision, string expectedFixed, string expectedExponential) {
            Assert.Equal(expectedFixed, ASuint.toFixed(value, precision));
            Assert.Equal(expectedFixed, ((ASuint)(ASObject)value).toFixed(precision));
            Assert.Equal(expectedExponential, ASuint.toExponential(value, precision));
            Assert.Equal(expectedExponential, ((ASuint)(ASObject)value).toExponential(precision));
        }

        public static IEnumerable<object[]> toPrecision_shouldFormatNumber_data = new (uint, int, string)[] {
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
            (UInt32.MaxValue, 21, "4294967295.00000000000"),
        }.Select(x => new object[] {x.Item1, x.Item2, x.Item3});

        [Theory]
        [MemberData(nameof(toPrecision_shouldFormatNumber_data))]
        public void toPrecision_shouldFormatNumber(uint value, int precision, string expected) {
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
        public void toLocaleString_shouldFormatNumber(uint value, string expected) {
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
        public void toFixed_toExponential_toPrecision_shouldThrowOnInvalidPrecision(uint value) {
            check(() => ASuint.toFixed(value, -1));
            check(() => ASuint.toFixed(value, 21));
            check(() => ASuint.toFixed(value, -1000));
            check(() => ASuint.toFixed(value, 1000));

            check(() => ((ASuint)(ASObject)value).toFixed(-1));
            check(() => ((ASuint)(ASObject)value).toFixed(21));
            check(() => ((ASuint)(ASObject)value).toFixed(-1000));
            check(() => ((ASuint)(ASObject)value).toFixed(1000));

            check(() => ASuint.toExponential(value, -1));
            check(() => ASuint.toExponential(value, 21));
            check(() => ASuint.toExponential(value, -1000));
            check(() => ASuint.toExponential(value, 1000));

            check(() => ((ASuint)(ASObject)value).toExponential(-1));
            check(() => ((ASuint)(ASObject)value).toExponential(21));
            check(() => ((ASuint)(ASObject)value).toExponential(-1000));
            check(() => ((ASuint)(ASObject)value).toExponential(1000));

            check(() => ASuint.toPrecision(value, 0));
            check(() => ASuint.toPrecision(value, -1));
            check(() => ASuint.toPrecision(value, 22));
            check(() => ASuint.toPrecision(value, -1000));
            check(() => ASuint.toPrecision(value, 1000));

            check(() => ((ASuint)(ASObject)value).toPrecision(0));
            check(() => ((ASuint)(ASObject)value).toPrecision(-1));
            check(() => ((ASuint)(ASObject)value).toPrecision(22));
            check(() => ((ASuint)(ASObject)value).toPrecision(-1000));
            check(() => ((ASuint)(ASObject)value).toPrecision(1000));

            void check(Action f) {
                var exc = Assert.Throws<AVM2Exception>(f);
                Assert.Equal(ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE, (ErrorCode)((ASError)exc.thrownValue).errorID);
            }
        }


        [Theory]
        [InlineData(0)]
        [InlineData(100)]
        [InlineData(Int32.MaxValue)]
        [InlineData(UInt32.MaxValue)]
        public void toString_shouldThrowOnInvalidRadix(uint value) {
            check(() => ASuint.toString(value, -1));
            check(() => ASuint.toString(value, 0));
            check(() => ASuint.toString(value, 1));
            check(() => ASuint.toString(value, 37));
            check(() => ASuint.toString(value, -1000));
            check(() => ASuint.toString(value, 1000));

            check(() => ((ASuint)(ASObject)value).AS_toString(-1));
            check(() => ((ASuint)(ASObject)value).AS_toString(0));
            check(() => ((ASuint)(ASObject)value).AS_toString(1));
            check(() => ((ASuint)(ASObject)value).AS_toString(37));
            check(() => ((ASuint)(ASObject)value).AS_toString(-1000));
            check(() => ((ASuint)(ASObject)value).AS_toString(1000));

            void check(Action f) {
                var exc = Assert.Throws<AVM2Exception>(f);
                Assert.Equal(ErrorCode.NUMBER_RADIX_OUT_OF_RANGE, (ErrorCode)((ASError)exc.thrownValue).errorID);
            }
        }

        [Fact]
        public void shouldConvertFromObject() {
            ASObject obj = new SpyObjectWithConversions() {uintValue = 1244};

            Assert.Equal(1244u, (uint)obj);
            Assert.Equal(1244u, ASObject.AS_toUint(obj));
            Assert.Equal(1244u, (uint)(ASAny)obj);
            Assert.Equal(1244u, ASAny.AS_toUint((ASAny)obj));
        }

        [Fact]
        public void classInvokeOrConstruct_shouldConvertFirstArg() {
            ASObject obj = new SpyObjectWithConversions() {uintValue = 1244};
            ASObject obj2 = new SpyObjectWithConversions() {uintValue = 35667};

            check(s_uintClass.invoke(new ASAny[] {}), 0);
            check(s_uintClass.invoke(new ASAny[] {default}), 0);
            check(s_uintClass.invoke(new ASAny[] {ASAny.@null}), 0);
            check(s_uintClass.invoke(new ASAny[] {obj}), 1244);
            check(s_uintClass.invoke(new ASAny[] {obj2}), 35667);

            check(s_uintClass.invoke(new ASAny[] {default, obj}), 0);
            check(s_uintClass.invoke(new ASAny[] {ASAny.@null, obj}), 0);
            check(s_uintClass.invoke(new ASAny[] {obj, obj2}), 1244);
            check(s_uintClass.invoke(new ASAny[] {obj2, obj}), 35667);
            check(s_uintClass.invoke(new ASAny[] {obj, obj2, default, obj}), 1244);

            check(s_uintClass.construct(new ASAny[] {}), 0);
            check(s_uintClass.construct(new ASAny[] {default}), 0);
            check(s_uintClass.construct(new ASAny[] {ASAny.@null}), 0);
            check(s_uintClass.construct(new ASAny[] {obj}), 1244);
            check(s_uintClass.construct(new ASAny[] {obj2}), 35667);

            check(s_uintClass.construct(new ASAny[] {default, obj}), 0);
            check(s_uintClass.construct(new ASAny[] {ASAny.@null, obj}), 0);
            check(s_uintClass.construct(new ASAny[] {obj, obj2}), 1244);
            check(s_uintClass.construct(new ASAny[] {obj2, obj}), 35667);
            check(s_uintClass.construct(new ASAny[] {obj, obj2, default, obj}), 1244);

            void check(ASAny o, uint value) {
                Assert.IsType<ASuint>(o.value);
                Assert.Same(s_uintClass, o.AS_class);
                Assert.Equal(value, ASObject.AS_toUint(o.value));
            }
        }

    }

}
