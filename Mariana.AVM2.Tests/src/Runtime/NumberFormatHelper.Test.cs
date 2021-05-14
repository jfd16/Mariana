using System;
using System.Collections.Generic;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

using static Mariana.AVM2.Core.NumberFormatHelper;

namespace Mariana.AVM2.Tests {

    public class NumberFormatHelperTest {

        public static IEnumerable<object[]> intToString_shouldFormatIntInGivenRadix_data = TupleHelper.toArrays(
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
            (12464877, 2, "101111100011001011101101"),
            (46008307, 3, "10012120110112101"),
            (19383400, 5, "14430232100"),
            (784392, 8, "2774010"),
            (34378000, 10, "34378000"),
            (64738329, 16, "3dbd419"),
            (34443113, 26, "2n9h93"),
            (34224, 32, "11dg"),
            (95849993, 36, "1l2ebt"),
            (-45388274, 2, "-10101101001001000111110010"),
            (-3563757, 3, "-20201001120000"),
            (-45388274, 5, "-43104411044"),
            (-395933, 8, "-1405235"),
            (-45790000, 10, "-45790000"),
            (-755345, 16, "-b8691"),
            (-6574788, 26, "-ea20c"),
            (-124353444, 32, "-3miut4"),
            (2147483647, 2, "1111111111111111111111111111111"),
            (2147483647, 7, "104134211161"),
            (2147483647, 10, "2147483647"),
            (2147483647, 32, "1vvvvvv"),
            (2147483647, 36, "zik0zj"),
            (-2147483648, 2, "-10000000000000000000000000000000"),
            (-2147483648, 7, "-104134211162"),
            (-2147483648, 10, "-2147483648"),
            (-2147483648, 32, "-2000000"),
            (-2147483648, 36, "-zik0zk")
        );

        [Theory]
        [MemberData(nameof(intToString_shouldFormatIntInGivenRadix_data))]
        public void intToString_shouldFormatIntInGivenRadix(int value, int radix, string expected) {
            Assert.Equal(expected, intToString(value, radix));
        }

        public static IEnumerable<object[]> uintToString_shouldFormatUintInGivenRadix_data = TupleHelper.toArrays<uint, int, string>(
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
            (12464877, 2, "101111100011001011101101"),
            (46008307, 3, "10012120110112101"),
            (19383400, 5, "14430232100"),
            (784392, 8, "2774010"),
            (34378000, 10, "34378000"),
            (64738329, 16, "3dbd419"),
            (34443113, 26, "2n9h93"),
            (34224, 32, "11dg"),
            (95849993, 36, "1l2ebt"),
            (2147483647, 2, "1111111111111111111111111111111"),
            (2147483647, 7, "104134211161"),
            (2147483647, 10, "2147483647"),
            (2147483647, 16, "7fffffff"),
            (2147483647, 32, "1vvvvvv"),
            (2147483647, 36, "zik0zj"),
            (2147483648, 2, "10000000000000000000000000000000"),
            (2147483648, 7, "104134211162"),
            (2147483648, 10, "2147483648"),
            (2147483648, 16, "80000000"),
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
        [MemberData(nameof(uintToString_shouldFormatUintInGivenRadix_data))]
        public void uintToString_shouldFormatUintInGivenRadix(uint value, int radix, string expected) {
            Assert.Equal(expected, uintToString(value, radix));
        }

        public static IEnumerable<object[]> longToString_shouldFormatIntInGivenRadix_data = TupleHelper.toArrays(
            (0, 2, "0"),
            (0, 3, "0"),
            (12464877, 2, "101111100011001011101101"),
            (46008307, 3, "10012120110112101"),
            (19383400, 5, "14430232100"),
            (784392, 8, "2774010"),
            (34378000, 10, "34378000"),
            (5311850193172291, 2, "10010110111110001100101111111110111010011011101000011"),
            (11240691765545148, 3, "2000121001222122020010020000111220"),
            (49679888033209, 4, "23102330000011002112321"),
            (7368542529763683, 10, "7368542529763683"),
            (7826334501437058, 12, "611938085431856"),
            (20368263917282200, 16, "485cd3ff7b2398"),
            (764980928777168, 25, "80d9bm2gibi"),
            (13772703835078176, 32, "c7e6dvqvfh0"),
            (114946881852857168, 36, "vft9oj2nfbk"),
            (-7368542529587850, 2, "-11010001011011010011001111111111111010000101010001010"),
            (-9972151704164496, 3, "-1210102201110101121010220001002110"),
            (-49679885369321, 4, "-23102323333322313333221"),
            (-7368542526368592, 10, "-7368542526368592"),
            (-97165043983737, 12, "-aa932a0773b09"),
            (-20368263925211840, 16, "-485cd3fff422c0"),
            (-13878632757167282, 25, "-5kd504j88hg7"),
            (-13772703839184248, 32, "-c7e6dvuspbo"),
            (-114946881904076163, 36, "-vft9ojx585f"),
            (Int64.MaxValue, 2, "111111111111111111111111111111111111111111111111111111111111111"),
            (Int64.MaxValue, 10, "9223372036854775807"),
            (Int64.MaxValue, 14, "4340724c6c71dc7a7"),
            (Int64.MaxValue, 16, "7fffffffffffffff"),
            (Int64.MaxValue, 32, "7vvvvvvvvvvvv"),
            (-Int64.MaxValue, 2, "-111111111111111111111111111111111111111111111111111111111111111"),
            (-Int64.MaxValue, 10, "-9223372036854775807"),
            (-Int64.MaxValue, 14, "-4340724c6c71dc7a7"),
            (-Int64.MaxValue, 16, "-7fffffffffffffff"),
            (-Int64.MaxValue, 32, "-7vvvvvvvvvvvv")
        );

        [Theory]
        [MemberData(nameof(intToString_shouldFormatIntInGivenRadix_data))]
        [MemberData(nameof(longToString_shouldFormatIntInGivenRadix_data))]
        public void longToString_shouldFormatLongInGivenRadix(long value, int radix, string expected) {
            Assert.Equal(expected, longToString(value, radix));
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData("a", 0)]
        [InlineData("1", 0)]
        [InlineData(" ", 1)]
        [InlineData("\x1F", 0)]
        [InlineData("\x21", 0)]
        [InlineData("\x08", 0)]
        [InlineData("\n", 1)]
        [InlineData("\r", 1)]
        [InlineData("\f", 1)]
        [InlineData("\t", 1)]
        [InlineData("\v", 1)]
        [InlineData("\x0E", 0)]
        [InlineData("\xA0", 0)]
        [InlineData("\u1FFF", 0)]
        [InlineData("\u2000", 1)]
        [InlineData("\u2001", 1)]
        [InlineData("\u200A", 1)]
        [InlineData("\u200B", 1)]
        [InlineData("\u200C", 0)]
        [InlineData("\u2027", 0)]
        [InlineData("\u2028", 1)]
        [InlineData("\u2029", 1)]
        [InlineData("\u202A", 0)]
        [InlineData("\u205F", 1)]
        [InlineData("\u3000", 1)]
        [InlineData("   ", 3)]
        [InlineData("  A  ", 2)]
        [InlineData("\n\t  \u2001\v\u200B  \r\n", 11)]
        [InlineData("\n\t  \u2001\v\u200A-  \r\n", 7)]
        [InlineData("\n\t  \u2001\v \u3000\u200C  \r\n", 8)]
        public void indexOfFirstNonSpace_shouldFindFirstNonSpace(string str, int expected) {
            Assert.Equal(expected, indexOfFirstNonSpace(str));
        }

        private static readonly double NEG_ZERO = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000L));

        public static IEnumerable<object[]> doubleToString_shouldFormatScientific_data = TupleHelper.toArrays(
            (4.9406564584124654e-324, "4.9406564584124654e-324"),
            (9.881312916824931e-324, "9.8813129168249309e-324"),
            (1.9762625833649862e-323, "1.9762625833649862e-323"),
            (4.5437e-311, "4.5436999999998653e-311"),
            (-4.9406564584124654e-324, "-4.9406564584124654e-324"),
            (-9.881312916824931e-324, "-9.8813129168249309e-324"),
            (-1.9762625833649862e-323, "-1.9762625833649862e-323"),
            (-4.5437e-311, "-4.5436999999998653e-311"),
            (1e-308, "9.9999999999999991e-309"),
            (2e-308, "1.9999999999999998e-308"),
            (2.2250738585072014e-308, "2.2250738585072014e-308"),
            (-2.2250738585072014e-308, "-2.2250738585072014e-308"),
            (4.6746e-308, "4.6746e-308"),
            (4.6748e-308, "4.6748e-308"),
            (1e-307, "1e-307"),
            (1e-304, "1e-304"),
            (1e-254, "1e-254"),
            (-1e-34, "-1e-34"),
            (1e-36, "1e-36"),
            (-1e-37, "-1e-37"),
            (-1e-38, "-1e-38"),
            (1e-44, "1e-44"),
            (1e-7, "1e-7"),
            (1.0000000000000001e-7, "1.0000000000000001e-7"),
            (1.2508373855022015e-7, "1.2508373855022015e-7"),
            (5.750931471333307e-7, "5.750931471333307e-7"),
            (2.102840117458176e-7, "2.102840117458176e-7"),
            (9.898294840100714e-14, "9.898294840100714e-14"),
            (1.342535e-12, "1.342535e-12"),
            (3.48233e-10, "3.48233e-10"),
            (5.8372482e-7, "5.8372482e-7"),
            (1.567e-32, "1.567e-32"),
            (9.385e-141, "9.385e-141"),
            (8.47377e-293, "8.47377e-293"),
            (1.0542453230885948e-14, "1.0542453230885948e-14"),
            (1.6964421232914988e-31, "1.6964421232914988e-31"),
            (-4.1103931995575694e-30, "-4.1103931995575694e-30"),
            (4.110393199557569e-33, "4.110393199557569e-33"),
            (4.110393199557569e-49, "4.110393199557569e-49"),
            (1.2051775134745452e-165, "1.2051775134745452e-165"),
            (1.205177513474545e-236, "1.205177513474545e-236"),
            (1e+21, "1e+21"),
            (1.000000000000001e+21, "1.000000000000001e+21"),
            (4.235829543e+34, "4.235829543e+34"),
            (4.235e+36, "4.235e+36"),
            (3.42985724897392e+89, "3.42985724897392e+89"),
            (4.83e+112, "4.83e+112"),
            (5.89325729398423e+221, "5.89325729398423e+221"),
            (-4.24328e+271, "-4.24328e+271"),
            (1e+271, "1e+271"),
            (-1e+291, "-1e+291"),
            (1e+293, "1e+293"),
            (1e+294, "1e+294"),
            (1e+307, "1e+307"),
            (1e+308, "1e+308"),
            (1.79769313486231e+308, "1.79769313486231e+308"),
            (Double.MaxValue, "1.7976931348623157e+308"),
            (-Double.MaxValue, "-1.7976931348623157e+308")
        );

        [Theory]
        [MemberData(nameof(doubleToString_shouldFormatScientific_data))]
        public void doubleToString_shouldFormatScientific(double value, string expected) {
            Assert.Equal(expected, doubleToString(value));
        }

        public static IEnumerable<object[]> doubleToString_shouldFormatFixed_data = TupleHelper.toArrays(
            (0, "0"),
            (NEG_ZERO, "0"),
            (Double.NaN, "NaN"),
            (Double.PositiveInfinity, "Infinity"),
            (Double.NegativeInfinity, "-Infinity"),
            (1, "1"),
            (-1, "-1"),
            (10, "10"),
            (100, "100"),
            (1.25, "1.25"),
            (-32.43523, "-32.43523"),
            (8.432493014234021, "8.432493014234021"),
            (-9.717286403633267, "-9.717286403633267"),
            (9007199254740992, "9007199254740992"),
            (-18014398509481984, "-18014398509481984"),
            (144115188075855870, "144115188075855870"),
            (10000000000000000000, "10000000000000000000"),
            (24352350000000000000.0, "24352350000000000000"),
            (100000000000000000000.0, "100000000000000000000"),
            (-100000000000000000000.0, "-100000000000000000000"),
            (999999999999999900000.0, "999999999999999900000"),
            (-999999999999999900000.0, "-999999999999999900000"),
            (0.1, "0.1"),
            (0.1 + 0.2, "0.30000000000000004"),
            (0.1 + 0.7, "0.7999999999999999"),
            (0.1 + 0.8, "0.9"),
            (-0.04, "-0.04"),
            (0.0134254523, "0.0134254523"),
            (0.000033425, "0.000033425"),
            (0.000001, "0.000001"),
            (0.0000010000000000000002, "0.0000010000000000000002"),
            (0.000008, "0.000008")
        );

        [Theory]
        [MemberData(nameof(doubleToString_shouldFormatFixed_data))]
        public void doubleToString_shouldFormatFixed(double value, string expected) {
            Assert.Equal(expected, doubleToString(value));
        }

        public static IEnumerable<object[]> stringToDouble_shouldParse_zeroAndInfinity_data = TupleHelper.toArrays(
            ("0", 0),
            ("0.0", 0),
            ("+0", 0),
            ("+0.0", 0),
            ("000", 0),
            ("+000", 0),
            (".0", 0),
            ("+.0", 0),
            ("0.", 0),
            ("00.000", 0),
            ("0e0", 0),
            ("0e100", 0),
            ("0e+1", 0),
            ("+0e+100", 0),
            ("0e-100", 0),
            ("0.e0", 0),
            ("0.e100", 0),
            ("0.e+1", 0),
            ("0.e+100", 0),
            ("0.e-100", 0),
            (".0e0", 0),
            ("0.0e100", 0),
            (".0000e+10000", 0),
            ("000.e+100", 0),
            ("0000.0000e-500000", 0),
            ("0.0e+1000000000000", 0),

            ("-0", NEG_ZERO),
            ("-.0", NEG_ZERO),
            ("-0.", NEG_ZERO),
            ("-0.0", NEG_ZERO),
            ("-0e+2", NEG_ZERO),
            ("-.0e-20", NEG_ZERO),

            ("Infinity", Double.PositiveInfinity),
            ("+Infinity", Double.PositiveInfinity),
            ("-Infinity", Double.NegativeInfinity)
        );

        [Theory]
        [MemberData(nameof(stringToDouble_shouldParse_zeroAndInfinity_data))]
        public void stringToDouble_shouldParse_zeroAndInfinity(string str, double expected) {
            Assert.True(stringToDouble(str, out double value, out int charsRead));
            Assert.Equal(str.Length, charsRead);
            AssertHelper.floatIdentical(expected, value);
        }

        public static IEnumerable<object[]> stringToDouble_shouldParse_data_integers = TupleHelper.toArrays(
            ("1", 1),
            ("-1", -1),
            ("+1", 1),
            ("1458", 1458),
            ("9999999", 9999999),
            ("+2147483647", 2147483647),
            ("-4294967296", -4294967296),
            ("1000000", 1000000),
            ("1000200003", 1000200003),
            ("10002000030", 10002000030),
            ("000010002000030", 10002000030),
            ("-000010002000030", -10002000030),

            ("9007199254740991", 9007199254740991),
            ("00009007199254740991", 9007199254740991),
            ("9007199254740992", 9007199254740992),
            ("+9007199254740993", 9007199254740992),
            ("9007199254740994", 9007199254740994),
            ("9007199254740995", 9007199254740996),
            ("9007199254740996", 9007199254740996),
            ("9007199254740997", 9007199254740996),
            ("9007199254740998", 9007199254740998),
            ("288230376151711744", 288230376151711740),
            ("+288230376151711776", 288230376151711740),
            ("288230376151711777", 288230376151711800),
            ("288230376151711839", 288230376151711800),
            ("288230376151711840", 288230376151711900),
            ("288230376151711841", 288230376151711900),
            ("4611686018427388416", 4611686018427388000),
            ("4611686018427388417", 4611686018427389000),
            ("4611686018427388418", 4611686018427389000),

            ("100000000000000000002", 100000000000000000000.0),
            ("1000000000000000000001", 1e+21),
            ("1000000000000000000000000000000002", 1e+33),
            ("1" + new string('0', 50), 1e+50),
            ("10000000000000000" + new string('0', 44), 1e+60),
            ("10000000000000001" + new string('0', 44), 1.0000000000000001e+60),
            ("134142874634134", 134142874634134),
            ("1289137912837912", 1289137912837912),
            ("934823749382743000034", 934823749382743000000.0),
            ("438274347102391000", 438274347102391000),
            ("00004329483243000493353200352", 4.3294832430004935e+24),
            ("1" + new string('0', 308), 1e+308),
            ("+1" + new string('0', 308), 1e+308),
            ("17976931348623157" + new string('0', 292), Double.MaxValue),
            ("-17976931348623157" + new string('0', 292), -Double.MaxValue)
        );

        public static IEnumerable<object[]> stringToDouble_shouldParse_data_fractions = TupleHelper.toArrays(
            ("1.0", 1),
            ("1.00000", 1),
            ("+1.0", 1),
            ("-1.0", -1),
            ("-1.00000", -1),
            ("+1.", 1),
            ("-1.", -1),
            ("26453.0", 26453),
            ("26453.00000000000000000000000000000000000000", 26453),
            ("26453.", 26453),
            ("00026453.0000", 26453),
            ("+00026453.0000", 26453),
            ("-00026453.0000", -26453),

            ("0.1", 0.1),
            ("-0.1", -0.1),
            (".1", 0.1),
            ("-.1", -0.1),
            ("+.1", 0.1),
            (".0006849993", 0.0006849993),
            ("-.0006849993", -0.0006849993),
            ("1.6543", 1.6543),
            ("495831.59965", 495831.59965),

            ("9007199254740991.000", 9007199254740991),
            ("9007199254740991.", 9007199254740991),
            ("9007199254740991.0000000000000000000", 9007199254740991),
            ("9007199254740992.0000000000000000000", 9007199254740992),
            ("9007199254740993.1000000000000000000", 9007199254740994),
            ("9007199254740993.0000000000000000001", 9007199254740994),
            ("9007199254740994.0000000000000000000", 9007199254740994),
            ("9007199254740995.0000000000000000000", 9007199254740996),
            ("9007199254740996.0000000000000000000", 9007199254740996),
            ("9007199254740998.0000000000000000000", 9007199254740998),
            ("18014398509481986", 18014398509481984),
            ("18014398509481986.1", 18014398509481988),
            ("18014398509481986.01", 18014398509481988),
            ("18014398509481986.0000000000001", 18014398509481988),
            ("-18014398509481986.0000000000001", -18014398509481988),
            ("18014398509481986.2", 18014398509481988),
            ("72057594037927944", 72057594037927940),
            ("72057594037927944.0001", 72057594037927950),
            ("72057594037927945.0000", 72057594037927950),

            ("384546.990000332", 384546.990000332),
            ("384546.99000033207", 384546.99000033207),
            ("384546.9900003321", 384546.9900003321),
            ("384546.9900003322", 384546.9900003322),
            ("384546.99000033224", 384546.99000033224),
            ("384546.9900003323", 384546.9900003323),
            ("384546.99000033236", 384546.99000033236),
            ("384546.9900003324", 384546.9900003324),
            ("384546.9900003325", 384546.9900003325),
            ("384546.99000033253", 384546.99000033253),
            ("384546.9900003326", 384546.9900003326),
            ("384546.99000033265", 384546.99000033265),
            ("384546.9900003327", 384546.9900003327),
            ("384546.99000033276", 384546.99000033276),
            ("384546.9900003328", 384546.9900003328),
            ("384546.9900003329", 384546.9900003329),
            ("384546.99000033294", 384546.99000033294),
            ("384546.990000333", 384546.990000333),
            ("384546.99000033305", 384546.99000033305),
            ("384546.9900003331", 384546.9900003331),

            ("9.007199254740992", 9.007199254740993),
            ("9.007199254740994", 9.007199254740994),
            ("9.007199254740996", 9.007199254740996),
            ("9.007199254740998", 9.007199254740998),
            ("9.007199254741000", 9.007199254741),

            ("1.0000000000000002", 1.0000000000000002),
            ("1.000000000000000200000000000000001", 1.0000000000000002),
            ("10000000000000000.1", 10000000000000000),
            ("123" + new string('0', 25) + ".666666", 1.23e+27),
            ("+1" + new string('0', 308) + ".123456789", 1e+308),
            ("-1" + new string('0', 308) + ".123456789", -1e+308),
            ("." + new string('0', 307) + "2225073858507201", 2.225073858507201e-308),
            ("." + new string('0', 307) + "22250738585072014", 2.2250738585072014e-308),
            ("-0000." + new string('0', 307) + "22250738585072014", -2.2250738585072014e-308),
            ("0." + new string('0', 323) + "494065645841246", Double.Epsilon),
            ("0." + new string('0', 323) + "5", Double.Epsilon),
            ("0." + new string('0', 323) + "4", Double.Epsilon),
            ("0." + new string('0', 323) + "3", Double.Epsilon)
        );

        public static IEnumerable<object[]> stringToDouble_shouldParse_data_scientific = TupleHelper.toArrays(
            ("1e0", 1),
            ("1e+0", 1),
            ("1e-0", 1),
            ("1E0", 1),
            ("1E+0", 1),
            ("1E-0", 1),
            ("1.e0", 1),
            ("1.e+0", 1),
            ("1.0e+0", 1),
            ("1.0e-0", 1),

            ("-1e0", -1),
            ("-1e-0", -1),
            ("-1.e+0", -1),

            ("5.4332E+0000", 5.4332),
            ("5.4332E+000000000000000000000000000000", 5.4332),
            ("5.4332E-000000000000000000000000000000", 5.4332),
            ("-.4332E+000000000000000000000000000000", -0.4332),

            ("2.445e6", 2.445e+6),
            ("+2.445E6", 2.445e+6),
            ("2.445e+6", 2.445e+6),
            ("0.2445e+7", 2.445e+6),
            ("000.2445e+7", 2.445e+6),
            ("0.000002445e+12", 2.445e+6),
            ("0.000002445000000000000e+12", 2.445e+6),
            ("244.5e+4", 2.445e+6),
            ("2445e+3", 2.445e+6),
            ("24450000e-1", 2.445e+6),
            ("24450000000e-4", 2.445e+6),
            ("2445000" + new string('0', 400) + "e-400", 2.445e+6),
            ("0." + new string('0', 400) + "2445e+404", 2445),
            ("2.445E+006", 2.445e+6),
            ("2.445E+" + new string('0', 100) + "6", 2.445e+6),
            ("2.445E-" + new string('0', 100) + "6", 2.445e-6),

            ("2.147483648E9", 2147483648),

            ("900.7199254740991e+13", 9007199254740991),
            ("9007.199254740991e+12", 9007199254740991),
            ("9007199254740991000000e-6", 9007199254740991),
            ("0.009007199254740992e+18", 9007199254740992),
            ("90071992.54740993E+8", 9007199254740992),
            ("9007199254740993100000000000000E-15", 9007199254740994),
            ("90071992547.409931E+5", 9007199254740994),
            ("90071992547409930000000000000000001E-19", 9007199254740994),
            ("9007199254740994000000000000E-12", 9007199254740994),
            ("900719925474099500000E-5", 9007199254740996),
            ("9007199254740996000000E-6", 9007199254740996),
            ("900719925474099.8E1", 9007199254740998),
            ("18014398509481986E0", 18014398509481984),
            ("180143985094819861E-1", 18014398509481988),
            ("1801439850948198601E-2", 18014398509481988),
            ("180143985094819860000000000001E-13", 18014398509481988),
            ("-180143985094819860000000000001E-13", -18014398509481988),
            ("18014398509481.9862E+3", 18014398509481988),
            ("7.2057594037927944E+16", 72057594037927940),
            ("7.20575940379279440001E+16", 72057594037927950),
            ("7.20575940379279450000E+16", 72057594037927950),

            ("3.84546990000332e+5", 384546.990000332),
            ("3.8454699000033207e+5", 384546.99000033207),
            ("3.845469900003321e+5", 384546.9900003321),
            ("3.845469900003322e+5", 384546.9900003322),
            ("3.8454699000033224e+5", 384546.99000033224),
            ("3.845469900003323e+5", 384546.9900003323),
            ("3.8454699000033236e+5", 384546.99000033236),
            ("3.845469900003324e+5", 384546.9900003324),
            ("3.845469900003325e+5", 384546.9900003325),
            ("3.8454699000033253e+5", 384546.99000033253),
            ("3.845469900003326e+5", 384546.9900003326),
            ("3.8454699000033265e+5", 384546.99000033265),
            ("3.845469900003327e+5", 384546.9900003327),
            ("3.8454699000033276e+5", 384546.99000033276),
            ("3.845469900003328e+5", 384546.9900003328),
            ("3.845469900003329e+5", 384546.9900003329),
            ("3.8454699000033294e+5", 384546.99000033294),
            ("3.84546990000333e+5", 384546.990000333),
            ("3.8454699000033305e+5", 384546.99000033305),
            ("3.845469900003331e+5", 384546.9900003331),

            ("1.797693134862315e+308", 1.797693134862315e+308),
            ("1.7976931348623157e+308", Double.MaxValue),
            ("1797.6931348623157e+305", Double.MaxValue),
            ("0.17976931348623157e+309", Double.MaxValue),
            ("0." + new string('0', 200) + "17976931348623157e+509", Double.MaxValue),
            ("-." + new string('0', 200) + "17976931348623157e+509", -Double.MaxValue),
            ("17976931348623157" + new string('0', 492) + "E-200", Double.MaxValue),
            ("2.2250738585072014e-308", 2.2250738585072014e-308),
            ("222507385850720140000e-328", 2.2250738585072014e-308),
            ("5e-324", Double.Epsilon),
            ("500e-326", Double.Epsilon),
            ("5" + new string('0', 200) + "e-524", Double.Epsilon),
            ("0." + new string('0', 423) + "5e100", Double.Epsilon),
            ("4.940656458412e-324", Double.Epsilon),
            ("4.9406564584124e-324", Double.Epsilon),
            ("4.94065645841246e-324", Double.Epsilon),
            ("4.940656458412465e-324", Double.Epsilon),
            ("4.9406564584124654e-324", Double.Epsilon),
            ("4.94065645841246544e-324", Double.Epsilon),
            ("3e-324", Double.Epsilon),
            ("1e-323", Double.Epsilon * 2),
            ("3.5e-323", Double.Epsilon * 7),
            ("6.5e-323", Double.Epsilon * 13)
        );

        public static IEnumerable<object[]> stringToDouble_shouldParse_data_overflow = TupleHelper.toArrays(
            ("2e308", Double.PositiveInfinity),
            ("1e+309", Double.PositiveInfinity),
            ("1e+400", Double.PositiveInfinity),
            ("00001" + new string('0', 309), Double.PositiveInfinity),
            ("1" + new string('0', 400), Double.PositiveInfinity),
            ("-1" + new string('0', 400), Double.NegativeInfinity),
            ("1.797693134862316e+308", Double.PositiveInfinity),
            ("-1.797693134862316e+308", Double.NegativeInfinity),
            ("1e-324", 0),
            ("2e-324", 0),
            ("2.4e-324", 0),
            ("0.000000000024e-314", 0),
            ("0." + new string('0', 323) + "24", 0)
        );

        [Theory]
        [MemberData(nameof(stringToDouble_shouldParse_data_integers))]
        [MemberData(nameof(stringToDouble_shouldParse_data_fractions))]
        [MemberData(nameof(stringToDouble_shouldParse_data_scientific))]
        [MemberData(nameof(stringToDouble_shouldParse_data_overflow))]
        public void stringToDouble_shouldParse(string str, double expected) {
            Assert.True(stringToDouble(str, out double value, out int charsRead));
            Assert.Equal(str.Length, charsRead);
            Assert.Equal(expected, value);
        }

        public static IEnumerable<object[]> stringToDouble_shouldParseHex_data = TupleHelper.toArrays(
            ("0x0", 0),
            ("-0x0", NEG_ZERO),
            ("0x1", 1),
            ("0x123456789", 4886718345),
            ("0xabcdef", 11259375),
            ("0xABCDEF", 11259375),
            ("0X123456789", 4886718345),
            ("0Xabcdef", 11259375),
            ("0XABCDEF", 11259375),
            ("+0x1", 1),
            ("-0x1", -1),
            ("0x12345678900abc1d1ee1abd", 3.521251666818939e+26),
            ("-0x12345678900abc1d1ee1abd", -3.521251666818939e+26),
            ("0x12345678900ABC1d1ee1Abd", 3.521251666818939e+26),
            ("-0x12345678900abc1d1Ee1AbD", -3.521251666818939e+26),
            ("0xfffffffffffff8" + new string('0', 242), Double.MaxValue),
            ("0xfffffffffffff8" + new string('f', 242), Double.MaxValue),
            ("0xfffffffffffffb" + new string('0', 242), Double.MaxValue),
            ("0xfffffffffffffb" + new string('f', 242), Double.MaxValue),
            ("0xfffffffffffffc" + new string('0', 242), Double.PositiveInfinity),
            ("0xfffffffffffff8" + new string('0', 243), Double.PositiveInfinity),
            ("-0xfffffffffffff8" + new string('0', 242), -Double.MaxValue)
        );

        [Theory]
        [MemberData(nameof(stringToDouble_shouldParseHex_data))]
        public void stringToDouble_shouldParseHex(string str, double expected) {
            Assert.True(stringToDouble(str, out double value, out int charsRead, allowHex: true));
            Assert.Equal(str.Length, charsRead);
            Assert.Equal(expected, value);
            Assert.False(stringToDouble(str, out _, out _, allowHex: false));
        }

        public static IEnumerable<object[]> stringToDouble_shouldIgnoreLeadingSpaces_data = TupleHelper.toArrays(
            ("   0", 0),
            ("  \r\n\t \f\v 1.5", 1.5),
            ("  \u2000 \u200B  \u2003\u3000  \u205F\n  4.5888", 4.5888),
            ("   .1", 0.1),
            ("   +.1", 0.1),
            (" \n\n\n  -4333", -4333),
            ("     0x400", 1024),
            (" \n\n\n  -0X400", -1024),
            ("   Infinity", Double.PositiveInfinity),
            ("   -Infinity", Double.NegativeInfinity)
        );

        [Theory]
        [MemberData(nameof(stringToDouble_shouldIgnoreLeadingSpaces_data))]
        public void stringToDouble_shouldIgnoreLeadingSpaces(string str, double expected) {
            Assert.True(stringToDouble(str, out double value, out int charsRead, allowHex: true));
            Assert.Equal(str.Length, charsRead);
            Assert.Equal(expected, value);
        }

        public static IEnumerable<object[]> stringToDouble_shouldCheckTrailingSpacesIfStrict_data = TupleHelper.toArrays(
            ("123", 123, 3, true),
            ("123  ", 123, 3, true),
            ("\t 123  \r\n \u205F  ", 123, 5, true),
            ("123abc", 123, 3, false),
            ("\r\n\t123a", 123, 6, false),
            ("\r\n\t123    a", 123, 6, false),
            ("-123  ", -123, 4, true),
            ("\t -123  \r\n \u205F  ", -123, 6, true),
            ("-123abc", -123, 4, false),
            ("1.234e +1", 1.234, 5, false),
            ("1.234 e+1", 1.234, 5, false),
            ("300+", 300, 3, false),
            ("300-", 300, 3, false),
            ("300%", 300, 3, false),
            ("123.45.", 123.45, 6, false),
            ("123 .45", 123, 3, false),
            ("123. 45", 123, 4, false),
            ("123,456", 123, 3, false),
            ("123_456", 123, 3, false),
            ("123 456", 123, 3, false),
            ("1.2e", 1.2, 3, false),
            ("1.2e+", 1.2, 3, false),
            ("1.2e-", 1.2, 3, false),
            ("1.2e +1", 1.2, 3, false),
            ("1.2 e+1", 1.2, 3, false),
            ("1.2e3+1", 1200, 5, false),
            ("1.2e 1", 1.2, 3, false),
            ("1.2 e1", 1.2, 3, false),
            ("1.23f+1", 1.23, 4, false),
            ("1.23d+1", 1.23, 4, false),
            ("0 x1234", 0, 1, false),
            ("0 x 1234", 0, 1, false),
            ("0y1234", 0, 1, false),
            ("0w1234", 0, 1, false),
            ("0x1234.", 4660, 6, false),
            ("0x1234.567", 4660, 6, false),
            ("0x1234e+1", 74574, 7, false),
            ("0xabcdefg", 11259375, 8, false)
        );

        [Theory]
        [MemberData(nameof(stringToDouble_shouldCheckTrailingSpacesIfStrict_data))]
        public void stringToDouble_shouldCheckTrailingSpacesIfStrict(
            string str, double expectedValue, int expectedCharsReadNonStrict, bool strictAllowed)
        {
            double value;
            int charsRead;

            Assert.True(stringToDouble(str, out value, out charsRead, strict: false, allowHex: true));
            Assert.Equal(expectedCharsReadNonStrict, charsRead);
            Assert.Equal(expectedValue, value);

            Assert.Equal(strictAllowed, stringToDouble(str, out value, out charsRead, strict: true, allowHex: true));
            if (strictAllowed) {
                Assert.Equal(expectedValue, value);
                Assert.Equal(str.Length, charsRead);
            }
        }

        public static IEnumerable<object[]> stringToDouble_shouldAlwaysFail_data = TupleHelper.toArrays(
            "",
            "    ",
            "  \r\n  \t ",
            "a",
            "ffffff",
            "/",
            ":1234:",
            "$123456",
            "+",
            "-",
            ".",
            "+.",
            "-.",
            "e",
            ".e",
            "e+",
            "e+1000",
            ".e+1000",
            "+ 300",
            "- 300",
            "\x081234",
            "\x0E1234",
            "0xgabcdef",
            "0x@A`a",
            "0xÀÉ",
            "0x 1234",
            "0x+1234",
            "0x-1234",
            "0x.123456",
            "infinity",
            "INFINITY",
            "inf"
        );

        [Theory]
        [MemberData(nameof(stringToDouble_shouldAlwaysFail_data))]
        public void stringToDouble_shouldAlwaysFail(string str) {
            Assert.False(stringToDouble(str, out _, out _, strict: false, allowHex: false));
            Assert.False(stringToDouble(str, out _, out _, strict: false, allowHex: true));
            Assert.False(stringToDouble(str, out _, out _, strict: true, allowHex: false));
            Assert.False(stringToDouble(str, out _, out _, strict: true, allowHex: true));
        }

        public static IEnumerable<object[]> stringToDoubleIntPow2Radix_shouldParseString_data_base2 = TupleHelper.toArrays(
            ("0", 2, 0, 1),
            ("000000000", 2, 0, 9),
            ("1", 2, 1, 1),
            ("000001", 2, 1, 6),
            ("100", 2, 4, 3),
            ("100100100", 2, 292, 9),
            ("00011100110100010101111101111010111", 2, 3867868119, 35),
            ("1110011010001010111110111101011001110011110111100100000011110011111100111101100101001111111000000000000", 2, 9.132745381370342e+30, 103),
            (new string('1', 31), 2, 2147483647, 31),
            ("1" + new string('0', 31), 2, 2147483648, 32),
            (new string('1', 32), 2, 4294967295, 32),
            (new string('1', 53), 2, 9007199254740991, 53),
            ("1" + new string('0', 53), 2, 9007199254740992, 54),
            ("1" + new string('0', 51) + "01", 2, 9007199254740992, 54),
            ("1" + new string('0', 51) + "10", 2, 9007199254740994, 54),
            ("1" + new string('0', 51) + "11", 2, 9007199254740996, 54),
            ("1" + new string('0', 53) + "111011011111011", 2, 295147905179352830000.0, 69),
            ("1" + new string('0', 53) + "000111101110100", 2, 295147905179352830000.0, 69),
            ("1" + new string('0', 53) + "100000110111000", 2, 295147905179352830000.0, 69),
            ("1" + new string('0', 53) + "000000000001111", 2, 295147905179352830000.0, 69),
            ("1" + new string('0', 53) + "111111111111101", 2, 295147905179352830000.0, 69),
            ("1" + new string('0', 52) + "10000", 2, 144115188075855870.0, 58),
            ("1" + new string('0', 52) + "10001", 2, 144115188075855900.0, 58),
            (new string('1', 53) + "0", 2, 18014398509481982, 54),
            (new string('1', 53) + "1", 2, 18014398509481984, 54),
            (new string('1', 53) + new string('0', 1024 - 53), 2, Double.MaxValue, 1024),
            ("0000" + new string('1', 53) + new string('0', 1024 - 53), 2, Double.MaxValue, 1028),
            (new string('1', 53) + new string('0', 1023 - 53) + "1", 2, Double.MaxValue, 1024),
            (new string('1', 53) + "01" + new string('1', 1024 - 55), 2, Double.MaxValue, 1024),
            (new string('1', 54) + new string('0', 1024 - 54), 2, Double.PositiveInfinity, 1024),
            (new string('1', 1024), 2, Double.PositiveInfinity, 1024),
            ("1" + new string('0', 1024), 2, Double.PositiveInfinity, 1025),
            ("1" + new string('0', 1050), 2, Double.PositiveInfinity, 1051),
            ("", 2, 0, 0),
            ("11111111abc", 2, 255, 8),
            ("12345", 2, 1, 1),
            ("111   ", 2, 7, 3)
        );

        public static IEnumerable<object[]> stringToDoubleIntPow2Radix_shouldParseString_data_base4 = TupleHelper.toArrays(
            ("0", 4, 0, 1),
            ("000000000", 4, 0, 9),
            ("1", 4, 1, 1),
            ("3", 4, 3, 1),
            ("000001", 4, 1, 6),
            ("210", 4, 36, 3),
            ("0013320013032", 4, 2064846, 13),
            ("30011233320033322011201013321", 4, 217790730554708480.0, 29),
            ("001121223300321120001111000000022201011130333320313121033113121333000010301302231", 4, 1.2800101463424436e+47, 81),
            ("1" + new string('3', 15), 4, 2147483647, 16),
            (new string('3', 16), 4, 4294967295, 16),
            ("1" + new string('3', 26), 4, 9007199254740991, 27),
            ("2" + new string('0', 26), 4, 9007199254740992, 27),
            ("2" + new string('0', 25) + "1", 4, 9007199254740992, 27),
            ("2" + new string('0', 25) + "2", 4, 9007199254740994, 27),
            ("2" + new string('0', 25) + "3", 4, 9007199254740996, 27),
            ("2" + new string('0', 26) + "1203033", 4, 147573952589676410000.0, 34),
            ("2" + new string('0', 26) + "0021133", 4, 147573952589676410000.0, 34),
            ("2" + new string('0', 26) + "3333102", 4, 147573952589676410000.0, 34),
            ("2" + new string('0', 26) + "0000010", 4, 147573952589676410000.0, 34),
            ("2" + new string('0', 26) + "2222133", 4, 147573952589676410000.0, 34),
            ("2" + new string('0', 25) + "10000", 4, 2305843009213694000.0, 31),
            ("2" + new string('0', 25) + "10001", 4, 2305843009213694500.0, 31),
            (new string('3', 26) + "2", 4, 18014398509481982, 27),
            (new string('3', 26) + "3", 4, 18014398509481984, 27),
            (new string('3', 26) + "2" + new string('0', 512 - 27), 4, Double.MaxValue, 512),
            ("0000" + new string('3', 26) + "2" + new string('0', 512 - 27), 4, Double.MaxValue, 516),
            (new string('3', 26) + "2" + new string('0', 511 - 27) + "1", 4, Double.MaxValue, 512),
            (new string('3', 26) + "21" + new string('1', 512 - 28), 4, Double.MaxValue, 512),
            (new string('3', 26) + "3" + new string('0', 512 - 27), 4, Double.PositiveInfinity, 512),
            (new string('3', 512), 4, Double.PositiveInfinity, 512),
            ("1" + new string('0', 512), 4, Double.PositiveInfinity, 513),
            ("1" + new string('0', 600), 4, Double.PositiveInfinity, 601),
            ("", 4, 0, 0),
            ("13333abc", 4, 511, 5),
            ("12345", 4, 27, 3),
            ("333   ", 4, 63, 3)
        );

        public static IEnumerable<object[]> stringToDoubleIntPow2Radix_shouldParseString_data_base8 = TupleHelper.toArrays(
            ("0", 8, 0, 1),
            ("000000000", 8, 0, 9),
            ("1", 8, 1, 1),
            ("7", 8, 7, 1),
            ("000001", 8, 1, 6),
            ("3542", 8, 1890, 4),
            ("0047213600665", 8, 5271126453, 13),
            ("540075611000754122", 8, 12389143611168850.0, 18),
            ("11700334006471634000424233444210442355366145525411", 8, 2.20238575464427e+44, 50),
            ("1" + new string('7', 10), 8, 2147483647, 11),
            ("3" + new string('7', 10), 8, 4294967295, 11),
            (new string('7', 11), 8, 8589934591, 11),
            ("3" + new string('7', 17), 8, 9007199254740991, 18),
            ("4" + new string('0', 17), 8, 9007199254740992, 18),
            ("4" + new string('0', 16) + "1", 8, 9007199254740992, 18),
            ("4" + new string('0', 16) + "2", 8, 9007199254740994, 18),
            ("4" + new string('0', 16) + "3", 8, 9007199254740996, 18),
            ("4" + new string('0', 16) + "4", 8, 9007199254740996, 18),
            ("4" + new string('0', 16) + "5", 8, 9007199254740996, 18),
            ("4" + new string('0', 16) + "6", 8, 9007199254740998, 18),
            ("4" + new string('0', 16) + "7", 8, 9007199254741000, 18),
            ("4" + new string('0', 17) + "1", 8, 72057594037927940.0, 19),
            ("4" + new string('0', 17) + "3", 8, 72057594037927940.0, 19),
            ("4" + new string('0', 17) + "4", 8, 72057594037927940.0, 19),
            ("4" + new string('0', 17) + "6", 8, 72057594037927940.0, 19),
            ("4" + new string('0', 17) + "7", 8, 72057594037927940.0, 19),
            ("4" + new string('0', 16) + "100", 8, 576460752303423500.0, 20),
            ("4" + new string('0', 16) + "101", 8, 576460752303423600.0, 20),
            ("4" + new string('0', 16) + "105", 8, 576460752303423600.0, 20),
            (new string('7', 17) + "6", 8, 18014398509481982, 18),
            (new string('7', 17) + "7", 8, 18014398509481984, 18),
            ("1777777777777777774" + new string('0', 323), 8, Double.MaxValue, 342),
            ("00001777777777777777774" + new string('0', 323), 8, Double.MaxValue, 346),
            ("1777777777777777774" + new string('0', 322) + "1", 8, Double.MaxValue, 342),
            ("1777777777777777775" + new string('0', 323), 8, Double.MaxValue, 342),
            ("1777777777777777775" + new string('7', 323), 8, Double.MaxValue, 342),
            ("1777777777777777776" + new string('0', 323), 8, Double.PositiveInfinity, 342),
            ("1" + new string('7', 341), 8, Double.PositiveInfinity, 342),
            (new string('7', 342), 8, Double.PositiveInfinity, 342),
            ("1" + new string('0', 342), 8, Double.PositiveInfinity, 343),
            ("1" + new string('0', 380), 8, Double.PositiveInfinity, 381),
            ("", 8, 0, 0),
            ("14677abc", 8, 6591, 5),
            ("123456789", 8, 342391, 7),
            ("333   ", 8, 219, 3)
        );

        public static IEnumerable<object[]> stringToDoubleIntPow2Radix_shouldParseString_data_base16 = TupleHelper.toArrays(
            ("0", 16, 0, 1),
            ("000000000", 16, 0, 9),
            ("1", 16, 1, 1),
            ("3", 16, 3, 1),
            ("a", 16, 10, 1),
            ("f", 16, 15, 1),
            ("A", 16, 10, 1),
            ("C", 16, 12, 1),
            ("F", 16, 15, 1),
            ("000001", 16, 1, 6),
            ("396", 16, 918, 3),
            ("a7bfcd3ee224", 16, 184442224042532, 12),
            ("A7bfcD3eE224", 16, 184442224042532, 12),
            ("A7BFCD3EE224", 16, 184442224042532, 12),
            ("0023f667ea777451bb", 16, 2591372892322419000.0, 18),
            ("0023F667eA777451Bb", 16, 2591372892322419000.0, 18),
            ("4bc15543ee900039222cccc009837771113", 16, 4.124505799249946e+41, 35),
            ("7fffffff", 16, 2147483647, 8),
            ("ffffffff", 16, 4294967295, 8),
            ("1fffffffffffff", 16, 9007199254740991, 14),
            ("20000000000000", 16, 9007199254740992, 14),
            ("20000000000001", 16, 9007199254740992, 14),
            ("20000000000002", 16, 9007199254740994, 14),
            ("20000000000003", 16, 9007199254740996, 14),
            ("20000000000004", 16, 9007199254740996, 14),
            ("20000000000005", 16, 9007199254740996, 14),
            ("20000000000006", 16, 9007199254740998, 14),
            ("20000000000007", 16, 9007199254741000, 14),
            ("200000000000001cc4", 16, 590295810358705700000.0, 18),
            ("200000000000003abc", 16, 590295810358705700000.0, 18),
            ("200000000000008043", 16, 590295810358705700000.0, 18),
            ("20000000000000c32d", 16, 590295810358705700000.0, 18),
            ("20000000000000fff1", 16, 590295810358705700000.0, 18),
            ("20000000000001000", 16, 36893488147419103000.0, 17),
            ("20000000000001001", 16, 36893488147419110000.0, 17),
            ("fffffffffffff8" + new string('0', 242), 16, Double.MaxValue, 256),
            ("0000fffffffffffff8" + new string('0', 242), 16, Double.MaxValue, 260),
            ("fffffffffffff8" + new string('0', 241) + "1", 16, Double.MaxValue, 256),
            ("fffffffffffffb" + new string('f', 242), 16, Double.MaxValue, 256),
            ("fffffffffffffc" + new string('0', 242), 16, Double.PositiveInfinity, 256),
            ("ffffffffffffff" + new string('0', 242), 16, Double.PositiveInfinity, 256),
            (new string('f', 256), 16, Double.PositiveInfinity, 256),
            ("1" + new string('0', 256), 16, Double.PositiveInfinity, 257),
            ("1" + new string('0', 300), 16, Double.PositiveInfinity, 301),
            ("", 16, 0, 0),
            ("g", 16, 0, 0),
            ("176abcf!", 16, 24554447, 7),
            ("1236abcfgh", 16, 305572815, 8),
            ("333d   ", 16, 13117, 4)
        );

        public static IEnumerable<object[]> stringToDoubleIntPow2Radix_shouldParseString_data_base32 = TupleHelper.toArrays(
            ("0", 32, 0, 1),
            ("000000000", 32, 0, 9),
            ("1", 32, 1, 1),
            ("3", 32, 3, 1),
            ("a", 32, 10, 1),
            ("f", 32, 15, 1),
            ("v", 32, 31, 1),
            ("A", 32, 10, 1),
            ("H", 32, 17, 1),
            ("V", 32, 31, 1),
            ("000001", 32, 1, 6),
            ("396", 32, 3366, 3),
            ("3h7tbm40r", 32, 3891151966235, 9),
            ("e48j1ik89d7lp", 32, 16294693694636204000.0, 13),
            ("e48j1IK89d7Lp", 32, 16294693694636204000.0, 13),
            ("E48j1ik89d7lP", 32, 16294693694636204000.0, 13),
            ("E48J1IK89D7LP", 32, 16294693694636204000.0, 13),
            ("abcdefghij", 32, 364365110330963, 10),
            ("klmnopqrs", 32, 22736207145852, 9),
            ("tuv9876543", 32, 1054407230952579, 10),
            ("agf78v98300fdd321bbm6je", 32, 1.3649082425186979e+34, 23),
            ("7vvvvvvvvvv", 32, 9007199254740991, 11),
            ("80000000000", 32, 9007199254740992, 11),
            ("80000000001", 32, 9007199254740992, 11),
            ("80000000002", 32, 9007199254740994, 11),
            ("80000000003", 32, 9007199254740996, 11),
            ("80000000004", 32, 9007199254740996, 11),
            ("80000000005", 32, 9007199254740996, 11),
            ("80000000006", 32, 9007199254740998, 11),
            ("80000000007", 32, 9007199254741000, 11),
            ("800000000000", 32, 288230376151711740.0, 12),
            ("800000000001", 32, 288230376151711740.0, 12),
            ("80000000000a", 32, 288230376151711740.0, 12),
            ("80000000000v", 32, 288230376151711740.0, 12),
            ("8000000000100", 32, 9223372036854776000.0, 13),
            ("8000000000101", 32, 9223372036854778000.0, 13),
            ("fvvvvvvvvvu" + new string('0', 194), 32, Double.MaxValue, 205),
            ("0000fvvvvvvvvvu" + new string('0', 194), 32, Double.MaxValue, 209),
            ("fvvvvvvvvvu" + new string('0', 193) + "1", 32, Double.MaxValue, 205),
            ("fvvvvvvvvvu" + new string('1', 194), 32, Double.MaxValue, 205),
            ("fvvvvvvvvvv" + new string('0', 194), 32, Double.PositiveInfinity, 205),
            ("g0000000000" + new string('0', 194), 32, Double.PositiveInfinity, 205),
            (new string('v', 205), 32, Double.PositiveInfinity, 205),
            ("1" + new string('0', 205), 32, Double.PositiveInfinity, 206),
            ("1" + new string('0', 250), 32, Double.PositiveInfinity, 251),
            ("", 32, 0, 0),
            ("176b3t9!", 32, 1315278761, 7),
            ("1238tuvw", 32, 1144289247, 7),
            ("uvu1   ", 32, 1015745, 4)
        );

        [Theory]
        [MemberData(nameof(stringToDoubleIntPow2Radix_shouldParseString_data_base2))]
        [MemberData(nameof(stringToDoubleIntPow2Radix_shouldParseString_data_base4))]
        [MemberData(nameof(stringToDoubleIntPow2Radix_shouldParseString_data_base8))]
        [MemberData(nameof(stringToDoubleIntPow2Radix_shouldParseString_data_base16))]
        [MemberData(nameof(stringToDoubleIntPow2Radix_shouldParseString_data_base32))]
        public void stringToDoubleIntPow2Radix_shouldParseString(string str, int radix, double expectedValue, int expectedCharsRead) {
            double value = stringToDoubleIntPow2Radix(str, radix, out int charsRead);
            Assert.Equal(expectedValue, value);
            Assert.Equal(expectedCharsRead, charsRead);
        }

        public static IEnumerable<object[]> stringToDoubleIntRadix_shouldParseStringBase10_data = TupleHelper.toArrays<string, double, int?>(
            ("", 0, null),
            ("0", 0, null),
            ("2", 2, null),
            ("745361", 745361, null),
            ("123456789", 123456789, null),
            ("2147483647", 2147483647, null),
            ("2147483648", 2147483648, null),
            ("4294967295", 4294967295, null),
            ("4294967296", 4294967296, null),
            ("9007199254740991", 9007199254740991.0, null),
            ("9007199254740992", 9007199254740992.0, null),
            ("9007199254740993", 9007199254740992.0, null),
            ("9007199254740994", 9007199254740994.0, null),
            ("9007199254740995", 9007199254740996.0, null),
            ("9007199254740996", 9007199254740996.0, null),
            ("9007199254740997", 9007199254740996.0, null),
            ("9007199254740998", 9007199254740998.0, null),
            ("288230376151711744", 288230376151711740.0, null),
            ("288230376151711776", 288230376151711740.0, null),
            ("288230376151711777", 288230376151711800.0, null),
            ("288230376151711839", 288230376151711800.0, null),
            ("288230376151711840", 288230376151711900.0, null),
            ("288230376151711841", 288230376151711900.0, null),
            ("922337203685477580", 922337203685477580.0, null),
            ("922337203685477581", 922337203685477581.0, null),
            ("9223372036854775807", 9223372036854776000.0, null),
            ("9223372036854775808", 9223372036854776000.0, null),
            ("9223372036854775809", 9223372036854776000.0, null),
            ("9223372036854776832", 9223372036854776000.0, null),
            ("9223372036854776833", 9223372036854778000.0, null),
            ("1" + new string('0', 20), 1e+20, null),
            ("4" + new string('0', 100), 4e+100, null),
            ("1" + new string('0', 308), 1e+308, null),
            ("17976931348623157" + new string('0', 292), Double.MaxValue, null),
            ("17976931348623160" + new string('0', 292), Double.PositiveInfinity, null),
            ("1" + new string('0', 309), Double.PositiveInfinity, null),

            ("123456a", 123456, 6),
            ("123456   ", 123456, 6),
            ("17976931348623157" + new string('0', 292) + "abcdefg", Double.MaxValue, 309),

            ("abcd", 0, 0),
            ("+123", 0, 0),
            ("-123", 0, 0),
            (".345", 0, 0),
            ("0x1234", 0, 1),
            ("123.456", 123, 3),
            ("  123", 0, 0),
            ("123  ", 123, 3),
            ("1.234e+5", 1, 1),
            ("1234e+5", 1234, 4)
        );

        [Theory]
        [MemberData(nameof(stringToDoubleIntRadix_shouldParseStringBase10_data))]
        public void stringToDoubleIntRadix_shouldParseStringBase10(string str, double expectedValue, int? expectedCharsRead) {
            double value = stringToDoubleIntRadix(str, 10, out int charsRead);
            Assert.Equal(expectedValue, value);
            Assert.Equal(expectedCharsRead ?? str.Length, charsRead);
        }

        public static IEnumerable<object[]> stringToDoubleIntRadix_shouldParseStringNonBase10_data = TupleHelper.toArrays<string, int, double, int?>(
            ("", 2, 0, null),
            ("", 3, 0, null),
            ("", 5, 0, null),
            ("", 9, 0, null),
            ("", 13, 0, null),
            ("", 16, 0, null),
            ("", 27, 0, null),
            ("", 36, 0, null),

            ("0", 2, 0, null),
            ("0", 3, 0, null),
            ("0", 5, 0, null),
            ("0", 9, 0, null),
            ("0000", 13, 0, null),
            ("0000", 16, 0, null),
            ("0000", 27, 0, null),
            ("0000", 36, 0, null),

            ("1", 2, 1, null),
            ("1", 3, 1, null),
            ("1", 5, 1, null),
            ("1", 9, 1, null),
            ("1", 16, 1, null),
            ("1", 21, 1, null),
            ("1", 36, 1, null),

            ("1001", 2, 9, null),
            ("1001", 3, 28, null),
            ("1001", 5, 126, null),
            ("1001", 9, 730, null),
            ("1001", 16, 4097, null),
            ("1001", 21, 9262, null),
            ("1001", 36, 46657, null),

            ("00000000000000000000000000000000000000001001", 2, 9, null),
            ("00000000000000000000000000000000000000001001", 3, 28, null),
            ("00000000000000000000000000000000000000001001", 5, 126, null),
            ("00000000000000000000000000000000000000001001", 9, 730, null),
            ("00000000000000000000000000000000000000001001", 16, 4097, null),
            ("00000000000000000000000000000000000000001001", 21, 9262, null),
            ("00000000000000000000000000000000000000001001", 36, 46657, null),

            ("00011100110100010101111101111010111", 2, 3867868119, null),
            ("42300144331120003", 5, 689794395003, null),
            ("11700334006471634000424233444210442355366145525411", 8, 2.20238575464427e+44, null),
            ("109a4473cc37", 13, 1896031210080, null),
            ("f995783hie1i0", 22, 198329384347747838.0, null),
            ("7u003r1psb5633", 31, 194552809993208430000.0, null),
            ("7u003r1psb5633tp23km0087", 31, 1.5946098638203468e+35, null),
            ("akz239v44jp00q", 36, 1.8051993130217043e+21, null),
            ("abcdefghij", 36, 1047601316295595.0, null),
            ("klmnopqrst", 36, 2092218013456445.0, null),
            ("uvwxyz999", 36, 87134297481117.0, null),
            ("ABCDEFGHIJ", 36, 1047601316295595.0, null),
            ("KLMNOPQRST", 36, 2092218013456445.0, null),
            ("UVWXYZ999", 36, 87134297481117.0, null),

            ("f7ded8c9e1f8e", 17, 9007199254740991.0, null),
            ("f7ded8c9e1f8f", 17, 9007199254740992.0, null),
            ("f7ded8c9e1f8g", 17, 9007199254740992.0, null),
            ("f7ded8c9e1f90", 17, 9007199254740994.0, null),
            ("f7ded8c9e1f91", 17, 9007199254740996.0, null),
            ("f7ded8c9e1f92", 17, 9007199254740996.0, null),
            ("f7ded8c9e1f93", 17, 9007199254740996.0, null),
            ("f7ded8c9e1f94", 17, 9007199254740998.0, null),
            ("f7ded8c9e1f95", 17, 9007199254741000.0, null),
            ("21050010502502454110024", 6, 288230376151711740.0, null),
            ("21050010502502454110030", 6, 288230376151711740.0, null),
            ("21050010502502454110031", 6, 288230376151711740.0, null),
            ("21050010502502454110303", 6, 288230376151711800.0, null),
            ("21050010502502454110304", 6, 288230376151711900.0, null),
            ("21050010502502454110305", 6, 288230376151711900.0, null),
            ("28555h26e2b0aa0", 18, 922337203685477580.0, null),
            ("28555h26e2b0aa1", 18, 922337203685477581.0, null),
            ("bm03i95hia437", 31, 9223372036854776000.0, null),
            ("bm03i95hia438", 31, 9223372036854776000.0, null),
            ("bm03i95hia439", 31, 9223372036854776000.0, null),
            ("bm03i95hia559", 31, 9223372036854776000.0, null),
            ("bm03i95hia55a", 31, 9223372036854778000.0, null),
            ("67979g60f5428010", 17, 1.8446744073709552e+19, null),
            ("67979g60f5428011", 17, 1.8446744073709552e+19, null),
            ("67979g60f5428012", 17, 1.8446744073709552e+19, null),
            ("l12ee5fn0jhm58", 24, 1.844674407370955e+19, null),
            ("l12ee5fn0jhnnn", 24, 1.844674407370955e+19, null),
            ("l12ee5fn0ji000", 24, 1.8446744073709552e+19, null),
            ("l12ee5fn0ji001", 24, 1.8446744073709552e+19, null),
            ("l12ee5fn0jhig0", 24, 1.8446744073709548e+19, null),
            ("l12ee5fn0jhkag", 24, 1.8446744073709548e+19, null),
            ("l12ee5fn0jhkaf", 24, 1.8446744073709548e+19, null),
            ("l12ee5fn0jhkah", 24, 1.844674407370955e+19, null),
            ("2835gegdf3661a84", 19, 3.68934881474191e+19, null),
            ("2835gegdf3661g1i", 19, 3.68934881474191e+19, null),
            ("2835gegdf3661g20", 19, 3.6893488147419103e+19, null),
            ("2835gegdf3661g21", 19, 3.6893488147419103e+19, null),
            ("2835gegdf3660i1c", 19, 3.6893488147419095e+19, null),
            ("2835gegdf36614e7", 19, 3.6893488147419095e+19, null),
            ("2835gegdf36614e8", 19, 3.6893488147419095e+19, null),
            ("2835gegdf36614e9", 19, 3.68934881474191e+19, null),
            ("9j0pa3nipkcmnbd77c", 28, 3.868562622766813e+25, null),
            ("9j0pa3nipkcr857gfr", 28, 3.868562622766813e+25, null),
            ("9j0pa3nipkcr857gg0", 28, 3.8685626227668134e+25, null),
            ("9j0pa3nipkcr857gg1", 28, 3.8685626227668134e+25, null),
            ("9j0pa3nipkcdpnogi8", 28, 3.8685626227668125e+25, null),
            ("9j0pa3nipkciahipqn", 28, 3.8685626227668125e+25, null),
            ("9j0pa3nipkciahipqo", 28, 3.8685626227668125e+25, null),
            ("9j0pa3nipkciahipqp", 28, 3.868562622766813e+25, null),
            ("ja1mk7j9ncphimqeeo", 28, 7.737125245533626e+25, null),
            ("ja1mk7j9ncpqgaf53r", 28, 7.737125245533626e+25, null),
            ("ja1mk7j9ncpqgaf540", 28, 7.737125245533627e+25, null),
            ("ja1mk7j9ncpqgaf541", 28, 7.737125245533627e+25, null),
            ("ja1mk7j9ncornjl58g", 28, 7.737125245533625e+25, null),
            ("ja1mk7j9ncp8l79npj", 28, 7.737125245533625e+25, null),
            ("ja1mk7j9ncp8l79npk", 28, 7.737125245533625e+25, null),
            ("ja1mk7j9ncp8l79npl", 28, 7.737125245533626e+25, null),
            ("1ak3hcfajipn79hp11k", 28, 1.5474250491067252e+26, null),
            ("1ak3hcfajipnp4l2a7r", 28, 1.5474250491067252e+26, null),
            ("1ak3hcfajipnp4l2a80", 28, 1.5474250491067253e+26, null),
            ("1ak3hcfajipnp4l2a81", 28, 1.5474250491067253e+26, null),
            ("1ak3hcfajiplrjbeah4", 28, 1.547425049106725e+26, null),
            ("1ak3hcfajipmheejjnb", 28, 1.547425049106725e+26, null),
            ("1ak3hcfajipmheejjnc", 28, 1.547425049106725e+26, null),
            ("1ak3hcfajipmheejjnd", 28, 1.5474250491067252e+26, null),
            ("8kl7j2if734jrm9p800mkrrpe3hnb4gr24dijc", 28, 3.0649910817317774e+54, null),
            ("8kl7j2if734o0ddc08afqh67c7g22ljjjrk4fr", 28, 3.0649910817317774e+54, null),
            ("8kl7j2if734o0ddc08afqh67c7g22ljjjrk4g0", 28, 3.064991081731778e+54, null),
            ("8kl7j2if734o0ddc08afqh67c7g22ljjjrk4g1", 28, 3.064991081731778e+54, null),
            ("8kl7j2if734bqc2nnb989lf5hnl9rqbdme0iq8", 28, 3.064991081731777e+54, null),
            ("8kl7j2if734fr36afjj1falffrjgjfe6c974mn", 28, 3.064991081731777e+54, null),
            ("8kl7j2if734fr36afjj1falffrjgjfe6c974mo", 28, 3.064991081731777e+54, null),
            ("8kl7j2if734fr36afjj1falffrjgjfe6c974mp", 28, 3.0649910817317774e+54, null),
            ("8kl7j2if734jrm9p800mkrrpe3hnb61jpcil1k", 28, 3.0649910817317774e+54, null),
            ("8kl7j2if734o0ddc08afqh67c7g22n4cf7p6q8", 28, 3.064991081731778e+54, null),
            ("8kl7j2if734o0ddc08afqh67c7g22n4cf7p6q9", 28, 3.064991081731778e+54, null),
            ("8kl7j2if734bqc2nnb989lf5hnl9rro6hm5l8g", 28, 3.064991081731777e+54, null),
            ("8kl7j2if734fr36afjj1falffrjgjgqr7hc754", 28, 3.0649910817317774e+54, null),
            ("8kl7j2if734fr36afjj1falffrjgjgqr7hc755", 28, 3.0649910817317774e+54, null),
            ("hdefa592e69brgjmg01hdrrn077im95q48r9ao", 28, 6.129982163463555e+54, null),
            ("hdefa592e69k0qqo0gl3p6ceof445fbbbrc93r", 28, 6.129982163463555e+54, null),
            ("hdefa592e69k0qqo0gl3p6ceof445fbbbrc940", 28, 6.129982163463556e+54, null),
            ("hdefa592e69k0qqo0gl3p6ceof445fbbbrc941", 28, 6.129982163463556e+54, null),
            ("hdefa592e68noo5jimigjf2b7jejromrh019og", 28, 6.129982163463554e+54, null),
            ("hdefa592e693q6cl3ba32lf33rb5b30coie9hj", 28, 6.129982163463554e+54, null),
            ("hdefa592e693q6cl3ba32lf33rb5b30coie9hk", 28, 6.129982163463554e+54, null),
            ("hdefa592e693q6cl3ba32lf33rb5b30coie9hl", 28, 6.129982163463555e+54, null),

            ("1a1e4vngaiku6scyil2a1vcbg6qvbzjfseu5nty6qyr6ft0fmxyr3nmwlm21axdq6ed914edar7zmc0m6nphl75ran22ulsb7gk2x0w8eh76j4mr2dvcv9tlpr9qo3ap6my00o4k4hhs2393945uo1rspbz2qhhhvhwp0k4z956e50710y4rp9wvby29lpsvd8xlurk", 36, 1.7976931348623157e+308, null),
            ("1a1e4vngail3j61xbrj3wd5p3w12pe2v432gh4ox7crihu9ilb64v83i0odsm9li304s327g1d63y48rknvo48cu9okopejzfwqfliuei0wg8zlbh8iutgt9p0jh8owzotodh4y5899hlf9rrbefbjsb841uaf03zvwuxzunql7mtgi99hm5pr4mw9ulplvm95p8fzz", 36, 1.7976931348623157e+308, null),
            ("1a1e4vngail3j61xbrj3wd5p3w12pe2v432gh4ox7crihu9ilb64v83i0odsm9li304s327g1d63y48rknvo48cu9okopejzfwqfliuei0wg8zlbh8iutgt9p0jh8owzotodh4y5899hlf9rrbefbjsb841uaf03zvwuxzunql7mtgi99hm5pr4mw9ulplvm95p8g00", 36, Double.PositiveInfinity, null),
            ("1a1e4vngail3j61xbrj3wd5p3w12pe2v432gh4ox7crihu9ilb64v83i0odsm9li304s327g1d63y48rknvo48cu9okopejzfwqfliuei0wg8zlbh8iutgt9p0jh8owzotodh4y5899hlf9rrbefbjsb841uaf03zvwuxzunql7mtgi99hm5pr4mw9ulplvm95p8g01", 36, Double.PositiveInfinity, null),
            ("1a1e4vngaikbi0z0w84mcvpk4s6gl6gl52dk18gpu6qibqi9q7jzkipprheio8y6d6u6x8s7tjbqyrkbend4j4rlck0v508yqk7dk0zw7dsn3epm8okcyvu9r8q9iw2469h93qhdwxyczf7q8popd1qrnrtjmmg9mpwd5opma93ws3kkjv5zobhc7ahldxndlfecoao", 36, 1.7976931348623155e+308, null),
            ("1a1e4vngaikkuenzpelg7dixshgnyl00gqluuj7gakqudrrcokrdc36b6jq9zl5y9slpz6lak59vajsgsnjb25yobljgzt0mz0dq8iy2axhwt9o6nj7ux2txqi003hoeog7mk7az0pq2ir8eqwxa0jra6jwb6jyvr3wj34farp55gjvssendosp3rm9xhtq4hc5z9j3", 36, 1.7976931348623155e+308, null),
            ("1a1e4vngaikkuenzpelg7dixshgnyl00gqluuj7gakqudrrcokrdc36b6jq9zl5y9slpz6lak59vajsgsnjb25yobljgzt0mz0dq8iy2axhwt9o6nj7ux2txqi003hoeog7mk7az0pq2ir8eqwxa0jra6jwb6jyvr3wj34farp55gjvssendosp3rm9xhtq4hc5z9j4", 36, 1.7976931348623155e+308, null),
            ("1a1e4vngaikkuenzpelg7dixshgnyl00gqluuj7gakqudrrcokrdc36b6jq9zl5y9slpz6lak59vajsgsnjb25yobljgzt0mz0dq8iy2axhwt9o6nj7ux2txqi003hoeog7mk7az0pq2ir8eqwxa0jra6jwb6jyvr3wj34farp55gjvssendosp3rm9xhtq4hc5z9j5", 36, 1.7976931348623157e+308, null),
            ("2k2s9rawl15odkpx164k3qomwdhqnz2vktobbnwdhxicvm0v9vxi7b9t7842lurgcsqi28sqlifz8o18dbez6ebila45p7kmex45u1sgsyed299i4rqpqjn7fijhc6led9w01c948yzk46i6i8bpc3jleny5gyyzqzte149yiacsa0e21w9jejtqnw4j7flqqhv7pj4", 36, Double.PositiveInfinity, null),
            ("2k2s9rawl1672c3unj27sqbe7s25es5q864wy9duepj0zoj16mc9qg701crl8j70609k64ew2qc7w8hj5brc8gpojd5det3yvtgv71ot01swhz6myh1pmxmje12yhdtzdncqy9wagiiz6ujjimsun3kmg83oku07zrtpvzpbh6f9mx0iiz8bfi99sjp7f7r8ibegvzz", 36, Double.PositiveInfinity, null),
            ("2k2s9rawl1672c3unj27sqbe7s25es5q864wy9duepj0zoj16mc9qg701crl8j70609k64ew2qc7w8hj5brc8gpojd5det3yvtgv71ot01swhz6myh1pmxmje12yhdtzdncqy9wagiiz6ujjimsun3kmg83oku07zrtpvzpbh6f9mx0iiz8bfi99sjp7f7r8ibegw00", 36, Double.PositiveInfinity, null),
            ("2k2s9rawl1672c3unj27sqbe7s25es5q864wy9duepj0zoj16mc9qg701crl8j70609k64ew2qc7w8hj5brc8gpojd5det3yvtgv71ot01swhz6myh1pmxmje12yhdtzdncqy9wagiiz6ujjimsun3kmg83oku07zrtpvzpbh6f9mx0iiz8bfi99sjp7f7r8ibegw01", 36, Double.PositiveInfinity, null),
            ("2k2s9rawl14n01y1sg98prf49kcx6cx6a4r42gxfodh0nh0jgf3z51ffiyt1chwcqdoduhkfn2nhxj4mtaq929j6p41qa0hxh4er41zserla6tf8hd4pxrojihgj1s48ciyi7gyrtvwpyufghfdeq3hjbjn398wj9fsqbdf8ki7tk7553qbzcmyoekz6rvar6uspclc", 36, Double.PositiveInfinity, null),
            ("2k2s9rawl155otbzet6wer1vkyxbx600xh7pp2ewl5horjipd5iqo6cmd3gjz6bwjl7fyd6l4ajql3kxlb2m4bxcn72xzm19y0rgh1w4luztmjcdb2fpu5nvh0006zctcwf94ely1fg51igthtuk13ikd3smd3xri7t268uljeaax3rlktardle7j8juzng8yobyj27", 36, Double.PositiveInfinity, null),
            ("2k2s9rawl155otbzet6wer1vkyxbx600xh7pp2ewl5horjipd5iqo6cmd3gjz6bwjl7fyd6l4ajql3kxlb2m4bxcn72xzm19y0rgh1w4luztmjcdb2fpu5nvh0006zctcwf94ely1fg51igthtuk13ikd3smd3xri7t268uljeaax3rlktardle7j8juzng8yobyj28", 36, Double.PositiveInfinity, null),
            ("2k2s9rawl155otbzet6wer1vkyxbx600xh7pp2ewl5horjipd5iqo6cmd3gjz6bwjl7fyd6l4ajql3kxlb2m4bxcn72xzm19y0rgh1w4luztmjcdb2fpu5nvh0006zctcwf94ely1fg51igthtuk13ikd3smd3xri7t268uljeaax3rlktardle7j8juzng8yobyj29", 36, Double.PositiveInfinity, null),

            ("mpad3sm3s100ifmrsv2dwpbvj3534jkccd8133t98os5kag7so3gks3qjur7cscfwvhu1n1w35jokmrjj8kodbgrtes3vmf8g6hnc6uq2tibbu3vem050lwceabgg6edt0mdtpw013arxha1aanfn3q81d1830fx0f4q5mxlsoa7nxkmq2lrrsopt0d5ui9cqdmgwpexu", 34, 4.49423283715579e+307, null),
            ("mpad3sm3s100ifmrsv2dwpbvj3534jkccd8133t98os5kag7so3gks3qjur7cscfwvhu1n1w35jokmrjj8kodbgrtes3vmf8g6hnc6uq2tibbu3vem050lwceabgg6edt0mdtpw013arxha1aanfn3q81d1830fx0f4q5mxlsoa7nxkmq2lrrsopt0d5ui9cqdmgwpext", 34, 4.49423283715579e+307, null),
            ("1bgkq7na7m2012vblns4rvgnt46a6956ooqg267oihfmb6kwfne6x7m7j5rkepmovvt1q3c3u6b5f7bl54h7eqmxlotm7taugwd1codri5p2mnq7sta0a19uoskmwwcsro1arphu026llx0k2klcvc7ig2q2g60vw0u9ibbx9nekfdx7bi59llnfho0qbr2ipiraxvgtxq", 34, 8.98846567431158e+307, null),
            ("1bgkq7na7m2012vblns4rvgnt46a6956ooqg267oihfmb6kwfne6x7m7j5rkepmovvt1q3c3u6b5f7bl54h7eqmxlotm7taugwd1codri5p2mnq7sta0a19uoskmwwcsro1arphu026llx0k2klcvc7ig2q2g60vw0u9ibbx9nekfdx7bi59llnfho0qbr2ipiraxvgtxp", 34, 8.98846567431158e+307, null),
            ("2mx7ifckfa4025sn9dm9lsxdo8ckciadffiw4cff30vamd7uvcsdwfaf4bl6thbftto3i6o7qcmauen8a90etjbx9fpafolqxuq2perl2bg5bdifnok0k2jrfn7bvupnle2llh1q04d99w16578psof2w5i4wc1tu1qj2mnwjct6urwen2aj99cv1e1ink53h3klxsxpxi", 34, Double.PositiveInfinity, null),
            ("2mx7ifckfa4025sn9dm9lsxdo8ckciadffiw4cff30vamd7uvcsdwfaf4bl6thbftto3i6o7qcmauen8a90etjbx9fpafolqxuq2perl2bg5bdifnok0k2jrfn7bvupnle2llh1q04d99w16578psof2w5i4wc1tu1qj2mnwjct6urwen2aj99cv1e1ink53h3klxsxpxh", 34, Double.PositiveInfinity, null),

            ("123456789abcdefghijklmnopqrstuvwxyz", 2, 1, 1),
            ("123456789abcdefghijklmnopqrstuvwxyz", 7, 22875, 6),
            ("123456789abcdefghijklmnopqrstuvwxyz", 16, 81985529216486900.0, 15),
            ("123456789abcdefghijklmnopqrstuvwxyz", 24, 2.5212396537812554e+30, 23),
            ("123456789abcdefghijklmnopqrstuvwxyz", 35, 9.537262105139694e+50, 34),
            ("-1234", 7, 0, 0),
            ("bcd", 11, 0, 0),
            ("!!!", 36, 0, 0),

            ("123456     ", 7, 22875, 6),
            ("123456789abcdefghijklmn\t1234", 24, 2.5212396537812554e+30, 23)
        );

        [Theory]
        [MemberData(nameof(stringToDoubleIntRadix_shouldParseStringNonBase10_data))]
        public void stringToDoubleIntRadix_shouldParseStringNonBase10(
            string str, int radix, double expectedValue, int? expectedCharsRead)
        {
            double value = stringToDoubleIntRadix(str, radix, out int charsRead);
            Assert.Equal(expectedValue, value);
            Assert.Equal(expectedCharsRead ?? str.Length, charsRead);
        }

        public static IEnumerable<object[]> stringToIntUint_shouldParse_decimal_data = TupleHelper.toArrays(
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
            ("000000000000000000003333", 3333),
            ("123456789", 123456789),
            ("-123456789", -123456789),
            ("2147483647", 2147483647),
            ("-2147483647", -2147483647),
            ("2147483648", unchecked((int)2147483648u)),
            ("-2147483648", -2147483648),
            ("4294967295", unchecked((int)4294967295u)),
            ("-4294967295", 1),
            ("4294967296", 0),
            ("-4294967296", 0),
            ("8589934591", -1),
            ("8589934592", 0),
            ("8589934596", 4),
            ("9007199254740991", -1),
            ("-9007199254740991", 1),
            ("9007199254740992", 0),
            ("9007199255110380", 369388),
            ("-9007199255110380", -369388),
            ("9223372036854775808", 0),
            ("18446744073709551616", 0),
            ("18446744073709551626", 10),
            ("18446744073709551606", -10),
            ("129127208515967007305", 145993)
        );

        [Theory]
        [MemberData(nameof(stringToIntUint_shouldParse_decimal_data))]
        public void stringToIntUint_shouldParse_decimal(string str, int expectedValue) {
            int valueSigned;
            uint valueUnsigned;
            int charsRead;

            Assert.True(stringToInt(str, out valueSigned, out charsRead, allowHex: false));
            Assert.Equal(expectedValue, valueSigned);
            Assert.Equal(str.Length, charsRead);

            Assert.True(stringToUint(str, out valueUnsigned, out charsRead, allowHex: false));
            Assert.Equal((uint)expectedValue, valueUnsigned);
            Assert.Equal(str.Length, charsRead);
        }

        public static IEnumerable<object[]> stringToIntUint_shouldParse_hex_data = TupleHelper.toArrays(
            ("0x0", 0),
            ("0x00000", 0),
            ("-0x0", 0),
            ("+0x0", 0),
            ("0X0", 0),
            ("-0X0", 0),
            ("0x1", 1),
            ("-0x1", -1),
            ("0X1", 1),
            ("-0X1", -1),
            ("0x0001", 1),
            ("+0x0001", 1),
            ("-0x0001", -1),
            ("0x1b3c", 0x1b3c),
            ("0x12345678", 0x12345678),
            ("0X12345678", 0x12345678),
            ("0x000000000012345678", 0x12345678),
            ("-0x12345678", -0x12345678),
            ("-0x000000000012345678", -0x12345678),
            ("0x9abcdef", 0x9abcdef),
            ("-0x9ABCDEF", -0x9abcdef),
            ("0x7FfFfFfF", 0x7FFFFFFF),
            ("-0x7FfFfFfF", -0x7FFFFFFF),
            ("0xffffffff", -1),
            ("-0xffffffff", 1),
            ("0x1b3a6cc79", unchecked((int)0xb3a6cc79u)),
            ("0x166300c764421c0", 0x764421c0),
            ("0x1674aa6300c764421c0", 0x764421c0),
            ("-0x1674aa6300c764421c0", -0x764421c0),
            ("0x1674aa6300cfffffffd", -3),
            ("-0x1674aa6300cfffffffd", 3)
        );

        [Theory]
        [MemberData(nameof(stringToIntUint_shouldParse_hex_data))]
        public void stringToIntUint_shouldParse_hex(string str, int expectedValue) {
            int valueSigned;
            uint valueUnsigned;
            int charsRead;

            Assert.True(stringToInt(str, out valueSigned, out charsRead, allowHex: true));
            Assert.Equal(expectedValue, valueSigned);
            Assert.Equal(str.Length, charsRead);

            Assert.True(stringToUint(str, out valueUnsigned, out charsRead, allowHex: true));
            Assert.Equal((uint)expectedValue, valueUnsigned);
            Assert.Equal(str.Length, charsRead);

            Assert.False(stringToInt(str, out _, out _, allowHex: false));
            Assert.False(stringToUint(str, out _, out _, allowHex: false));
        }

        public static IEnumerable<object[]> stringToIntUint_shouldCheckLeadingAndTrailing_data = TupleHelper.toArrays(
            ("    0", 0, 5, true),
            ("    0   ", 0, 5, true),
            (" \n\t \u200b 30\f\v \u3000", 30, 8, true),
            (" \n\t \u200b +30\f\v \u3000", 30, 9, true),
            (" \n\t \u200b -30\f\v \u3000", -30, 9, true),
            ("  0x1333", 0x1333, 8, true),
            ("  0x1333\t\n", 0x1333, 8, true),
            ("  -0x1333", -0x1333, 9, true),
            ("  -0x1333\t\n", -0x1333, 9, true),
            ("  -0x1bcfff\t\n", -0x1bcfff, 11, true),
            ("0abc", 0, 1, false),
            ("   0  a", 0, 4, false),
            ("123456789:", 123456789, 9, false),
            ("123.456", 123, 3, false),
            ("   123.456", 123, 6, false),
            ("0.1456", 0, 1, false),
            ("123e+4", 123, 3, false),
            ("12 345", 12, 2, false),
            ("12,345,678", 12, 2, false),
            ("0xabcdefg", 0xabcdef, 8, false),
            ("0y1234", 0, 1, false),
            ("0x1234.56", 0x1234, 6, false)
        );

        [Theory]
        [MemberData(nameof(stringToIntUint_shouldCheckLeadingAndTrailing_data))]
        public void stringToIntUint_shouldCheckLeadingAndTrailing(
            string str, int expectedValue, int expectedCharsReadNonStrict, bool shouldPassStrict)
        {
            int valueSigned;
            uint valueUnsigned;
            int charsRead;

            Assert.True(stringToInt(str, out valueSigned, out charsRead, allowHex: true, strict: false));
            Assert.Equal(expectedValue, valueSigned);
            Assert.Equal(expectedCharsReadNonStrict, charsRead);

            Assert.True(stringToUint(str, out valueUnsigned, out charsRead, allowHex: true, strict: false));
            Assert.Equal((uint)expectedValue, valueUnsigned);
            Assert.Equal(expectedCharsReadNonStrict, charsRead);

            if (!shouldPassStrict) {
                Assert.False(stringToInt(str, out _, out _, allowHex: true, strict: true));
                Assert.False(stringToUint(str, out _, out _, allowHex: true, strict: true));
            }
            else {
                Assert.True(stringToInt(str, out valueSigned, out charsRead, allowHex: true, strict: true));
                Assert.Equal(expectedValue, valueSigned);
                Assert.Equal(str.Length, charsRead);

                Assert.True(stringToUint(str, out valueUnsigned, out charsRead, allowHex: true, strict: true));
                Assert.Equal((uint)expectedValue, valueUnsigned);
                Assert.Equal(str.Length, charsRead);
            }
        }

        public static IEnumerable<object[]> stringToIntUint_shouldAlwaysFail_data = TupleHelper.toArrays(
            "",
            "abc",
            "a123",
            "+",
            "-",
            ".",
            ".1234",
            "$1234",
            "0xg",
            "0x",
            "0x+1234",
            "0x-1234",
            "0x 1234",
            "+ 1234",
            "- 1234",
            "(1234)",
            "Infinity",
            "NaN"
        );

        [Theory]
        [MemberData(nameof(stringToIntUint_shouldAlwaysFail_data))]
        public void stringToIntUint_shouldAlwaysFail(string str) {
            Assert.False(stringToInt(str, out _, out _, strict: false, allowHex: false));
            Assert.False(stringToInt(str, out _, out _, strict: false, allowHex: true));
            Assert.False(stringToInt(str, out _, out _, strict: true, allowHex: false));
            Assert.False(stringToInt(str, out _, out _, strict: true, allowHex: true));
            Assert.False(stringToUint(str, out _, out _, strict: false, allowHex: false));
            Assert.False(stringToUint(str, out _, out _, strict: false, allowHex: true));
            Assert.False(stringToUint(str, out _, out _, strict: true, allowHex: false));
            Assert.False(stringToUint(str, out _, out _, strict: true, allowHex: true));
        }

        public static IEnumerable<object[]> parseArrayIndex_shouldParse_data = TupleHelper.toArrays<string, bool?, uint>(
            ("0", null, 0),
            ("1", null, 1),
            ("01", true, 1),
            ("12", null, 12),
            ("00012", true, 12),
            ("10000", null, 10000),
            ("123456789", null, 123456789),
            ("999999999", null, 999999999),
            ("2147483647", null, 2147483647),
            ("4294967294", null, 4294967294u),
            ("4294967295", null, 4294967295u),
            ("00004294967295", true, 4294967295u)
        );

        [Theory]
        [MemberData(nameof(parseArrayIndex_shouldParse_data))]
        public void parseArrayIndex_shouldParse(string str, bool? allowLeadingZeroes, uint expectedValue) {
            uint value;

            if (allowLeadingZeroes.HasValue) {
                Assert.True(parseArrayIndex(str, allowLeadingZeroes.Value, out value));
                Assert.Equal(expectedValue, value);
            }
            else {
                Assert.True(parseArrayIndex(str, false, out value));
                Assert.Equal(expectedValue, value);
                Assert.True(parseArrayIndex(str, true, out value));
                Assert.Equal(expectedValue, value);
            }
        }

        public static IEnumerable<object[]> parseArrayIndex_shouldFail_data = TupleHelper.toArrays<string, bool?>(
            (null, null),
            ("", null),
            ("01", false),
            ("00012", false),
            ("00004294967295", false),
            ("4294967296", null),
            ("4294967300", null),
            ("100000000000", null),
            ("+0", null),
            ("-0", null),
            ("+12", null),
            ("-12", null),
            ("0.1", null),
            ("1.1", null),
            ("1.", null),
            (" 0", null),
            ("0 ", null),
            ("  ", null),
            ("12 ", null),
            (" 12", null),
            (" 12 ", null),
            ("1e+2", null),
            ("1e2", null),
            ("0x12", null),
            ("123abc", null),
            ("199999999abc", null),
            ("1999999999abc", null)
        );

        [Theory]
        [MemberData(nameof(parseArrayIndex_shouldFail_data))]
        public void parseArrayIndex_shouldFail(string str, bool? allowLeadingZeroes) {
            if (allowLeadingZeroes.HasValue) {
                Assert.False(parseArrayIndex(str, allowLeadingZeroes.Value, out _));
            }
            else {
                Assert.False(parseArrayIndex(str, false, out _));
                Assert.False(parseArrayIndex(str, true, out _));
            }
        }

        public static IEnumerable<object[]> doubleToStringFixedNotation_shouldFormat_data = TupleHelper.toArrays(
            (0, 0, "0"),
            (NEG_ZERO, 0, "0"),
            (0, 2, "0.00"),
            (NEG_ZERO, 2, "0.00"),
            (0, 20, "0.00000000000000000000"),
            (0, 21, "0.000000000000000000000"),
            (1, 0, "1"),
            (-1, 0, "-1"),
            (1, 1, "1.0"),
            (-1, 3, "-1.000"),
            (1, 20, "1.00000000000000000000"),
            (-1, 20, "-1.00000000000000000000"),
            (135, 0, "135"),
            (135, 2, "135.00"),
            (135, 20, "135.00000000000000000000"),
            (4294967296, 2, "4294967296.00"),
            (9007199254740991, 5, "9007199254740991.00000"),
            (9007199254740992, 5, "9007199254740992.00000"),
            (72057594037927950, 0, "72057594037927952"),
            (72057594037927950, 5, "72057594037927952.00000"),
            (72057594037927950, 21, "72057594037927952.000000000000000000000"),
            (1000000000000000100, 0, "1000000000000000128"),
            (1000000000000000100, 3, "1000000000000000128.000"),
            (1.463728884739E+35, 0, "146372888473900007670749404302671872"),
            (1.463728884739E+35, 20, "146372888473900007670749404302671872.00000000000000000000"),

            (0.1, 0, "0"),
            (0.5, 0, "0"),
            (0.8, 0, "1"),
            (-0.1, 0, "-0"),
            (-0.0034, 2, "-0.00"),
            (-0.0034, 4, "-0.0034"),
            (1.475853, 0, "1"),
            (1.475853, 1, "1.5"),
            (1.475853, 2, "1.48"),
            (1.475853, 3, "1.476"),
            (1.475853, 4, "1.4759"),
            (1.475853, 5, "1.47585"),
            (1.475853, 6, "1.475853"),
            (1.475853, 7, "1.4758530"),
            (1.475853, 8, "1.47585300"),
            (1.475853, 9, "1.475853000"),
            (1.475853, 10, "1.4758530000"),
            (1.475853, 11, "1.47585300000"),
            (1.475853, 12, "1.475853000000"),
            (1.475853, 13, "1.4758530000000"),
            (1.475853, 14, "1.47585300000000"),
            (1.475853, 15, "1.475853000000000"),
            (1.475853, 16, "1.4758530000000001"),
            (1.475853, 17, "1.47585300000000008"),
            (1.475853, 18, "1.475853000000000081"),
            (1.475853, 19, "1.4758530000000000815"),
            (1.475853, 20, "1.47585300000000008147"),
            (1.475853, 21, "1.475853000000000081471"),
            (561774.475853, 0, "561774"),
            (561774.475853, 1, "561774.5"),
            (561774.475853, 2, "561774.48"),
            (561774.475853, 3, "561774.476"),
            (561774.475853, 4, "561774.4759"),
            (561774.475853, 5, "561774.47585"),
            (561774.475853, 6, "561774.475853"),
            (561774.475853, 7, "561774.4758530"),
            (561774.475853, 10, "561774.4758530000"),
            (561774.475853, 11, "561774.47585299995"),
            (561774.475853, 20, "561774.47585299995262175798"),
            (999999999999999.875, 20, "999999999999999.87500000000000000000"),
            (999999999999999.875, 21, "999999999999999.875000000000000000000"),
            (410834013012.54553, 20, "410834013012.54553222656250000000"),
            (0.00003767411, 4, "0.0000"),
            (0.00003767411, 5, "0.00004"),
            (0.00003767411, 6, "0.000038"),
            (0.00003767411, 9, "0.000037674"),
            (0.00003767411, 12, "0.000037674110"),
            (0.00003767411, 21, "0.000037674110000000000"),
            (1.478e-17, 18, "0.000000000000000015"),
            (1.478e-17, 20, "0.00000000000000001478"),
            (1e-18, 20, "0.00000000000000000100"),
            (1e-19, 20, "0.00000000000000000010"),
            (1e-20, 20, "0.00000000000000000001"),
            (1e-21, 20, "0.00000000000000000000"),

            (2.2250738585072014e-308, 0, "0"),
            (2.2250738585072014e-308, 21, "0.000000000000000000000"),

            (Double.MaxValue, 0, "179769313486231570814527423731704356798070567525844996598917476803157260780028538760589558632766878171540458953514382464234321326889464182768467546703537516986049910576551282076245490090389328944075868508455133942304583236903222948165808559332123348274797826204144723168738177180919299881250404026184124858368"),
            (Double.Epsilon, 0, "0"),
            (Double.Epsilon, 21, "0.000000000000000000000"),
            (Double.PositiveInfinity, 0, "Infinity"),
            (Double.PositiveInfinity, 21, "Infinity"),
            (Double.NegativeInfinity, 0, "-Infinity"),
            (Double.NegativeInfinity, 21, "-Infinity"),
            (Double.NaN, 0, "NaN")
        );

        [Theory]
        [MemberData(nameof(doubleToStringFixedNotation_shouldFormat_data))]
        public void doubleToStringFixedNotation_shouldFormat(double value, int precision, string expected) {
            Assert.Equal(expected, doubleToStringFixedNotation(value, precision));
        }

        public static IEnumerable<object[]> doubleToStringExpNotation_shouldFormat_data = TupleHelper.toArrays(
            (0, 0, "0e+0"),
            (NEG_ZERO, 0, "0e+0"),
            (0, 2, "0.00e+0"),
            (NEG_ZERO, 2, "0.00e+0"),
            (0, 20, "0.00000000000000000000e+0"),
            (1, 0, "1e+0"),
            (-1, 0, "-1e+0"),
            (1, 1, "1.0e+0"),
            (-1, 3, "-1.000e+0"),
            (1, 20, "1.00000000000000000000e+0"),
            (-1, 20, "-1.00000000000000000000e+0"),
            (135, 0, "1e+2"),
            (135, 2, "1.35e+2"),
            (135, 20, "1.35000000000000000000e+2"),
            (155, 0, "2e+2"),
            (155, 2, "1.55e+2"),
            (4294967296, 2, "4.29e+9"),
            (4294967296, 8, "4.29496730e+9"),
            (4294967296, 10, "4.2949672960e+9"),
            (9007199254740992, 5, "9.00720e+15"),
            (9007199254740992, 10, "9.0071992547e+15"),
            (9007199254740992, 15, "9.007199254740992e+15"),
            (9007199254740992, 20, "9.00719925474099200000e+15"),
            (72057594037927950, 0, "7e+16"),
            (72057594037927950, 5, "7.20576e+16"),
            (72057594037927950, 20, "7.20575940379279520000e+16"),
            (1000000000000000100, 20, "1.00000000000000012800e+18"),
            (1.4568e+99, 4, "1.4568e+99"),
            (1.4568e+100, 4, "1.4568e+100"),
            (1.4568e+300, 4, "1.4568e+300"),

            (0.1, 0, "1e-1"),
            (0.5, 0, "5e-1"),
            (0.8, 0, "8e-1"),
            (0.81, 0, "8e-1"),
            (0.85, 0, "8e-1"),
            (0.87, 0, "9e-1"),
            (0.81, 1, "8.1e-1"),
            (0.85, 1, "8.5e-1"),
            (0.87, 1, "8.7e-1"),
            (561774.475853, 0, "6e+5"),
            (561774.475853, 1, "5.6e+5"),
            (561774.475853, 2, "5.62e+5"),
            (561774.475853, 3, "5.618e+5"),
            (561774.475853, 4, "5.6177e+5"),
            (561774.475853, 5, "5.61774e+5"),
            (561774.475853, 6, "5.617745e+5"),
            (561774.475853, 7, "5.6177448e+5"),
            (561774.475853, 8, "5.61774476e+5"),
            (561774.475853, 9, "5.617744759e+5"),
            (561774.475853, 10, "5.6177447585e+5"),
            (561774.475853, 11, "5.61774475853e+5"),
            (561774.475853, 12, "5.617744758530e+5"),
            (561774.475853, 13, "5.6177447585300e+5"),
            (561774.475853, 14, "5.61774475853000e+5"),
            (561774.475853, 15, "5.617744758530000e+5"),
            (561774.475853, 16, "5.6177447585299995e+5"),
            (561774.475853, 17, "5.61774475852999953e+5"),
            (561774.475853, 18, "5.617744758529999526e+5"),
            (561774.475853, 19, "5.6177447585299995262e+5"),
            (561774.475853, 20, "5.61774475852999952622e+5"),
            (999999999999999.875, 20, "9.99999999999999875000e+14"),
            (410834013012.54553, 20, "4.10834013012545532227e+11"),
            (0.00003767411, 4, "3.7674e-5"),
            (0.00003767411, 6, "3.767411e-5"),
            (0.00003767411, 20, "3.76741099999999996947e-5"),
            (1.444e-99, 3, "1.444e-99"),
            (1.444e-100, 3, "1.444e-100"),
            (1.444e-300, 3, "1.444e-300"),
            (2.2250738585072014e-308, 16, "2.2250738585072014e-308"),
            (2.2250738585072014e-308, 20, "2.22507385850720138309e-308"),

            (Double.MaxValue, 0, "2e+308"),
            (Double.MaxValue, 4, "1.7977e+308"),
            (Double.MaxValue, 20, "1.79769313486231570815e+308"),
            (Double.MinValue, 20, "-1.79769313486231570815e+308"),
            (Double.Epsilon, 0, "5e-324"),
            (Double.Epsilon, 20, "4.94065645841246544177e-324"),
            (Double.Epsilon * 9, 2, "4.45e-323"),
            (Double.Epsilon * 9, 20, "4.44659081257121889759e-323"),
            (Double.PositiveInfinity, 0, "Infinity"),
            (Double.PositiveInfinity, 20, "Infinity"),
            (Double.NegativeInfinity, 0, "-Infinity"),
            (Double.NegativeInfinity, 20, "-Infinity"),
            (Double.NaN, 0, "NaN")
        );

        [Theory]
        [MemberData(nameof(doubleToStringExpNotation_shouldFormat_data))]
        public void doubleToStringExpNotation_shouldFormat(double value, int precision, string expected) {
            Assert.Equal(expected, doubleToStringExpNotation(value, precision));
        }

        public static IEnumerable<object[]> doubleToStringPrecision_shouldFormat_data = TupleHelper.toArrays(
            (0, 1, "0"),
            (NEG_ZERO, 1, "0"),
            (0, 2, "0.0"),
            (NEG_ZERO, 2, "0.0"),
            (0, 21, "0.00000000000000000000"),
            (1, 1, "1"),
            (-1, 1, "-1"),
            (1, 2, "1.0"),
            (-1, 5, "-1.0000"),
            (1, 21, "1.00000000000000000000"),
            (-1, 21, "-1.00000000000000000000"),
            (0.5, 1, "0.5"),
            (-0.5, 1, "-0.5"),
            (0.5, 5, "0.50000"),
            (0.5, 21, "0.500000000000000000000"),
            (0.0239410400390625, 15, "0.0239410400390625"),
            (0.0239410400390625, 21, "0.0239410400390625000000"),
            (-0.0239410400390625, 21, "-0.0239410400390625000000"),
            (0.004677, 4, "0.004677"),
            (-0.004677, 4, "-0.004677"),
            (0.004677, 21, "0.00467699999999999973394"),
            (135, 1, "1e+2"),
            (135, 2, "1.4e+2"),
            (135, 3, "135"),
            (135, 5, "135.00"),
            (135, 21, "135.000000000000000000"),
            (-155, 1, "-2e+2"),
            (-155, 3, "-155"),
            (-155, 8, "-155.00000"),
            (4294967296, 3, "4.29e+9"),
            (4294967296, 9, "4.29496730e+9"),
            (4294967296, 10, "4294967296"),
            (4294967296, 14, "4294967296.0000"),
            (9007199254740992, 6, "9.00720e+15"),
            (9007199254740992, 11, "9.0071992547e+15"),
            (9007199254740992, 16, "9007199254740992"),
            (9007199254740992, 20, "9007199254740992.0000"),
            (72057594037927950, 1, "7e+16"),
            (72057594037927950, 6, "7.20576e+16"),
            (72057594037927950, 20, "72057594037927952.000"),
            (1000000000000000100, 15, "1.00000000000000e+18"),
            (1000000000000000100, 17, "1.0000000000000001e+18"),
            (1000000000000000100, 18, "1.00000000000000013e+18"),
            (1000000000000000100, 19, "1000000000000000128"),

            (465057.8965517241, 1, "5e+5"),
            (465057.8965517241, 2, "4.7e+5"),
            (465057.8965517241, 3, "4.65e+5"),
            (465057.8965517241, 4, "4.651e+5"),
            (465057.8965517241, 5, "4.6506e+5"),
            (465057.8965517241, 6, "465058"),
            (465057.8965517241, 7, "465057.9"),
            (465057.8965517241, 8, "465057.90"),
            (465057.8965517241, 9, "465057.897"),
            (465057.8965517241, 10, "465057.8966"),
            (465057.8965517241, 11, "465057.89655"),
            (465057.8965517241, 12, "465057.896552"),
            (465057.8965517241, 13, "465057.8965517"),
            (465057.8965517241, 14, "465057.89655172"),
            (465057.8965517241, 15, "465057.896551724"),
            (465057.8965517241, 16, "465057.8965517241"),
            (465057.8965517241, 17, "465057.89655172412"),
            (465057.8965517241, 18, "465057.896551724116"),
            (465057.8965517241, 19, "465057.8965517241159"),
            (465057.8965517241, 20, "465057.89655172411585"),
            (465057.8965517241, 21, "465057.896551724115852"),

            (2.02493599329195405464e-8, 1, "0.00000002"),
            (2.02493599329195405464e-8, 2, "0.000000020"),
            (2.02493599329195405464e-8, 3, "0.0000000202"),
            (2.02493599329195405464e-8, 4, "0.00000002025"),
            (2.02493599329195405464e-8, 5, "0.000000020249"),
            (2.02493599329195405464e-8, 6, "0.0000000202494"),
            (2.02493599329195405464e-8, 7, "0.00000002024936"),
            (2.02493599329195405464e-8, 8, "0.000000020249360"),
            (2.02493599329195405464e-8, 9, "0.0000000202493599"),
            (2.02493599329195405464e-8, 10, "0.00000002024935993"),
            (2.02493599329195405464e-8, 11, "0.000000020249359933"),
            (2.02493599329195405464e-8, 12, "0.0000000202493599329"),
            (2.02493599329195405464e-8, 13, "0.00000002024935993292"),
            (2.02493599329195405464e-8, 14, "0.000000020249359932920"),
            (2.02493599329195405464e-8, 15, "0.0000000202493599329195"),
            (2.02493599329195405464e-8, 16, "0.00000002024935993291954"),
            (2.02493599329195405464e-8, 17, "0.000000020249359932919541"),
            (2.02493599329195405464e-8, 18, "0.0000000202493599329195405"),
            (2.02493599329195405464e-8, 19, "0.00000002024935993291954055"),
            (2.02493599329195405464e-8, 20, "0.000000020249359932919540546"),
            (2.02493599329195405464e-8, 21, "0.0000000202493599329195405464"),

            (1.40506819609812239058, 1, "1"),
            (1.40506819609812239058, 2, "1.4"),
            (1.40506819609812239058, 3, "1.41"),
            (1.40506819609812239058, 4, "1.405"),
            (1.40506819609812239058, 5, "1.4051"),
            (1.40506819609812239058, 6, "1.40507"),
            (1.40506819609812239058, 7, "1.405068"),
            (1.40506819609812239058, 8, "1.4050682"),
            (1.40506819609812239058, 9, "1.40506820"),
            (1.40506819609812239058, 10, "1.405068196"),
            (1.40506819609812239058, 11, "1.4050681961"),
            (1.40506819609812239058, 12, "1.40506819610"),
            (1.40506819609812239058, 13, "1.405068196098"),
            (1.40506819609812239058, 14, "1.4050681960981"),
            (1.40506819609812239058, 15, "1.40506819609812"),
            (1.40506819609812239058, 16, "1.405068196098122"),
            (1.40506819609812239058, 17, "1.4050681960981224"),
            (1.40506819609812239058, 18, "1.40506819609812239"),
            (1.40506819609812239058, 19, "1.405068196098122391"),
            (1.40506819609812239058, 20, "1.4050681960981223906"),
            (1.40506819609812239058, 21, "1.40506819609812239058"),

            (6.062535883705235e+43, 1, "6e+43"),
            (6.062535883705235e+43, 2, "6.1e+43"),
            (6.062535883705235e+43, 3, "6.06e+43"),
            (6.062535883705235e+43, 4, "6.063e+43"),
            (6.062535883705235e+43, 5, "6.0625e+43"),
            (6.062535883705235e+43, 6, "6.06254e+43"),
            (6.062535883705235e+43, 7, "6.062536e+43"),
            (6.062535883705235e+43, 8, "6.0625359e+43"),
            (6.062535883705235e+43, 9, "6.06253588e+43"),
            (6.062535883705235e+43, 10, "6.062535884e+43"),
            (6.062535883705235e+43, 11, "6.0625358837e+43"),
            (6.062535883705235e+43, 12, "6.06253588371e+43"),
            (6.062535883705235e+43, 13, "6.062535883705e+43"),
            (6.062535883705235e+43, 14, "6.0625358837052e+43"),
            (6.062535883705235e+43, 15, "6.06253588370523e+43"),
            (6.062535883705235e+43, 16, "6.062535883705235e+43"),
            (6.062535883705235e+43, 17, "6.0625358837052346e+43"),
            (6.062535883705235e+43, 18, "6.06253588370523461e+43"),
            (6.062535883705235e+43, 19, "6.062535883705234605e+43"),
            (6.062535883705235e+43, 20, "6.0625358837052346053e+43"),
            (6.062535883705235e+43, 21, "6.06253588370523460529e+43"),

            (999999999999.4999, 12, "999999999999"),
            (999999999999.5000, 12, "1.00000000000e+12"),
            (999999999999.9501, 12, "1.00000000000e+12"),
            (999999999999.9999, 12, "1.00000000000e+12"),
            (1e+12, 12, "1.00000000000e+12"),
            (9.999999999999999e+20, 21, "999999999999999868928"),
            (1e+21, 21, "1.00000000000000000000e+21"),
            (1e+23, 21, "9.99999999999999916114e+22"),
            (-1e+23, 21, "-9.99999999999999916114e+22"),

            (1e-79, 1, "0.0000000000000000000000000000000000000000000000000000000000000000000000000000001"),
            (1e-79, 5, "0.00000000000000000000000000000000000000000000000000000000000000000000000000000010000"),
            (1e-79, 17, "0.00000000000000000000000000000000000000000000000000000000000000000000000000000010000000000000000"),
            (1e-79, 21, "0.0000000000000000000000000000000000000000000000000000000000000000000000000000000999999999999999998879"),
            (1e-80, 1, "0.00000000000000000000000000000000000000000000000000000000000000000000000000000001"),
            (1e-80, 5, "0.000000000000000000000000000000000000000000000000000000000000000000000000000000010000"),
            (1e-80, 17, "0.0000000000000000000000000000000000000000000000000000000000000000000000000000000099999999999999996"),
            (1e-80, 21, "0.00000000000000000000000000000000000000000000000000000000000000000000000000000000999999999999999961425"),
            (1e-81, 1, "0.000000000000000000000000000000000000000000000000000000000000000000000000000000001"),
            (1e-81, 5, "0.0000000000000000000000000000000000000000000000000000000000000000000000000000000010000"),
            (1e-81, 17, "0.00000000000000000000000000000000000000000000000000000000000000000000000000000000099999999999999996"),
            (1e-81, 21, "0.000000000000000000000000000000000000000000000000000000000000000000000000000000000999999999999999961425"),
            (1e-82, 1, "0.0000000000000000000000000000000000000000000000000000000000000000000000000000000001"),
            (1e-82, 5, "0.00000000000000000000000000000000000000000000000000000000000000000000000000000000010000"),
            (1e-82, 17, "0.000000000000000000000000000000000000000000000000000000000000000000000000000000000099999999999999996"),
            (1e-82, 21, "0.0000000000000000000000000000000000000000000000000000000000000000000000000000000000999999999999999961425"),
            (1e-83, 1, "0.00000000000000000000000000000000000000000000000000000000000000000000000000000000001"),
            (1e-83, 5, "0.000000000000000000000000000000000000000000000000000000000000000000000000000000000010000"),
            (1e-83, 17, "0.000000000000000000000000000000000000000000000000000000000000000000000000000000000010000000000000000"),
            (1e-83, 21, "0.0000000000000000000000000000000000000000000000000000000000000000000000000000000000100000000000000003458"),

            (1.888e+99, 5, "1.8880e+99"),
            (1.888e+100, 5, "1.8880e+100"),
            (1.888e+200, 5, "1.8880e+200"),
            (1.888e+300, 5, "1.8880e+300"),
            (-1.888e+300, 5, "-1.8880e+300"),

            (Double.MaxValue, 1, "2e+308"),
            (Double.MaxValue, 5, "1.7977e+308"),
            (Double.MaxValue, 21, "1.79769313486231570815e+308"),
            (Double.MinValue, 1, "-2e+308"),
            (Double.MinValue, 21, "-1.79769313486231570815e+308"),

            (2.2250738585072014e-308, 1, "0." + new string('0', 307) + "2"),
            (2.2250738585072014e-308, 10, "0." + new string('0', 307) + "2225073859"),
            (2.2250738585072014e-308, 21, "0." + new string('0', 307) + "222507385850720138309"),
            (4.9406564584124654e-324, 1, "0." + new string('0', 323) + "5"),
            (4.9406564584124654e-324, 10, "0." + new string('0', 323) + "4940656458"),
            (4.9406564584124654e-324, 21, "0." + new string('0', 323) + "494065645841246544177"),
            (-4.9406564584124654e-324, 1, "-0." + new string('0', 323) + "5"),
            (-4.9406564584124654e-324, 21, "-0." + new string('0', 323) + "494065645841246544177"),
            (8.399115979301191e-323, 1, "0." + new string('0', 322) + "8"),
            (8.399115979301191e-323, 10, "0." + new string('0', 322) + "8399115979"),
            (8.399115979301191e-323, 21, "0." + new string('0', 322) + "839911597930119125100"),

            (Double.PositiveInfinity, 1, "Infinity"),
            (Double.PositiveInfinity, 21, "Infinity"),
            (Double.NegativeInfinity, 1, "-Infinity"),
            (Double.NegativeInfinity, 21, "-Infinity"),
            (Double.NaN, 1, "NaN"),
            (Double.NaN, 21, "NaN")
        );

        [Theory]
        [MemberData(nameof(doubleToStringPrecision_shouldFormat_data))]
        public void doubleToStringPrecision_shouldFormat(double value, int precision, string expected) {
            Assert.Equal(expected, doubleToStringPrecision(value, precision));
        }

        public static IEnumerable<object[]> doubleToStringPow2Radix_shouldFormat_data =
            TupleHelper.toArrays(
                (0.0, "0", "0", "0", "0", "0"),
                (NEG_ZERO, "0", "0", "0", "0", "0"),
                (1.0, "1", "1", "1", "1", "1"),
                (-1.0, "-1", "-1", "-1", "-1", "-1"),
                (2.0, "10", "2", "2", "2", "2"),
                (4.0, "100", "10", "4", "4", "4"),
                (8.0, "1000", "20", "10", "8", "8"),
                (16.0, "10000", "100", "20", "10", "g"),
                (31.0, "11111", "133", "37", "1f", "v"),
                (32.0, "100000", "200", "40", "20", "10"),
                (64.0, "1000000", "1000", "100", "40", "20"),
                (128.0, "10000000", "2000", "200", "80", "40"),
                (-128.0, "-10000000", "-2000", "-200", "-80", "-40"),
                (1356.0, "10101001100", "111030", "2514", "54c", "1ac"),
                (
                    745531.0,
                    "10110110000000111011",
                    "2312000323",
                    "2660073",
                    "b603b",
                    "mo1r"
                ),
                (
                    2147483647.0,
                    "1111111111111111111111111111111",
                    "1333333333333333",
                    "17777777777",
                    "7fffffff",
                    "1vvvvvv"
                ),
                (
                    2147483648.0,
                    "10000000000000000000000000000000",
                    "2000000000000000",
                    "20000000000",
                    "80000000",
                    "2000000"
                ),
                (
                    4294967295.0,
                    "11111111111111111111111111111111",
                    "3333333333333333",
                    "37777777777",
                    "ffffffff",
                    "3vvvvvv"
                ),
                (
                    4294967296.0,
                    "100000000000000000000000000000000",
                    "10000000000000000",
                    "40000000000",
                    "100000000",
                    "4000000"
                ),
                (
                    9007199254740991.0,
                    "11111111111111111111111111111111111111111111111111111",
                    "133333333333333333333333333",
                    "377777777777777777",
                    "1fffffffffffff",
                    "7vvvvvvvvvv"
                ),
                (
                    9007199254740992.0,
                    "100000000000000000000000000000000000000000000000000000",
                    "200000000000000000000000000",
                    "400000000000000000",
                    "20000000000000",
                    "80000000000"
                ),
                (
                    9007199254740994.0,
                    "100000000000000000000000000000000000000000000000000010",
                    "200000000000000000000000002",
                    "400000000000000002",
                    "20000000000002",
                    "80000000002"
                ),
                (
                    9007199254740996.0,
                    "100000000000000000000000000000000000000000000000000100",
                    "200000000000000000000000010",
                    "400000000000000004",
                    "20000000000004",
                    "80000000004"
                ),
                (
                    1.8014398509481982e+16,
                    "111111111111111111111111111111111111111111111111111110",
                    "333333333333333333333333332",
                    "777777777777777776",
                    "3ffffffffffffe",
                    "fvvvvvvvvvu"
                ),
                (
                    1.8014398509481984e+16,
                    "1000000000000000000000000000000000000000000000000000000",
                    "1000000000000000000000000000",
                    "1000000000000000000",
                    "40000000000000",
                    "g0000000000"
                ),
                (
                    1.8014398509481988e+16,
                    "1000000000000000000000000000000000000000000000000000100",
                    "1000000000000000000000000010",
                    "1000000000000000004",
                    "40000000000004",
                    "g0000000004"
                ),
                (
                    3.6028797018963976e+16,
                    "10000000000000000000000000000000000000000000000000001000",
                    "2000000000000000000000000020",
                    "2000000000000000010",
                    "80000000000008",
                    "100000000008"
                ),
                (
                    7.205759403792795e+16,
                    "100000000000000000000000000000000000000000000000000010000",
                    "10000000000000000000000000100",
                    "4000000000000000020",
                    "100000000000010",
                    "20000000000g"
                ),
                (
                    1.441151880758559e+17,
                    "1000000000000000000000000000000000000000000000000000100000",
                    "20000000000000000000000000200",
                    "10000000000000000040",
                    "200000000000020",
                    "400000000010"
                ),
                (
                    2.882303761517118e+17,
                    "10000000000000000000000000000000000000000000000000001000000",
                    "100000000000000000000000001000",
                    "20000000000000000100",
                    "400000000000040",
                    "800000000020"
                ),
                (
                    1.475739525896764e+20,
                    "1111111111111111111111111111111111111111111111111111100000000000000",
                    "1333333333333333333333333330000000",
                    "17777777777777777740000",
                    "7ffffffffffffc000",
                    "3vvvvvvvvvvg00"
                ),
                (
                    1.4757395258967641e+20,
                    "1" + new string('0', 67),
                    "2000000000000000000000000000000000",
                    "20000000000000000000000",
                    "80000000000000000",
                    "40000000000000"
                ),
                (
                    1.4757395258967645e+20,
                    "10000000000000000000000000000000000000000000000000001000000000000000",
                    "2000000000000000000000000020000000",
                    "20000000000000000100000",
                    "80000000000008000",
                    "40000000001000"
                ),
                (
                    -1.576649e+102,
                    "-1011010000110101100000001000110001111000110100011101" + new string('0', 288),
                    "-23100311200020301320310131" + new string('0', 144),
                    "-132065401061706435" + new string('0', 96),
                    "-b435808c78d1d" + new string('0', 72),
                    "-mgqo133oq78" + new string('0', 57)
                ),
                (
                    7.589337662678885e+256,
                    "10100001101111100110011101000111111111101001001010011" + new string('0', 801),
                    "220123321213101333322102212" + new string('0', 400),
                    "241574635077751123" + new string('0', 267),
                    "286f99d1ffa4a6" + new string('0', 200),
                    "a3fj78vv956" + new string('0', 160)
                ),
                (
                    8.988465674311579e+307,
                    "11111111111111111111111111111111111111111111111111111" + new string('0', 970),
                    "133333333333333333333333333" + new string('0', 485),
                    "777777777777777776" + new string('0', 323),
                    "7ffffffffffffc" + new string('0', 242),
                    "7vvvvvvvvvv" + new string('0', 194)
                ),
                (
                    1.34e+308,
                    "10111110110100101000010001111100111010011101011101011" + new string('0', 971),
                    "233231022010133032213113112" + new string('0', 485),
                    "1373224107635165654" + new string('0', 323),
                    "bed2847ce9d758" + new string('0', 242),
                    "btkk4fjktem" + new string('0', 194)
                ),
                (
                    1.7976931348623157e+308,
                    "11111111111111111111111111111111111111111111111111111" + new string('0', 971),
                    "333333333333333333333333332" + new string('0', 485),
                    "1777777777777777774" + new string('0', 323),
                    "fffffffffffff8" + new string('0', 242),
                    "fvvvvvvvvvu" + new string('0', 194)
                ),

                (0.5, "0.1", "0.2", "0.4", "0.8", "0.g"),
                (-0.5, "-0.1", "-0.2", "-0.4", "-0.8", "-0.g"),
                (0.25, "0.01", "0.1", "0.2", "0.4", "0.8"),
                (0.125, "0.001", "0.02", "0.1", "0.2", "0.4"),
                (0.0625, "0.0001", "0.01", "0.04", "0.1", "0.2"),
                (0.03125, "0.00001", "0.002", "0.02", "0.08", "0.1"),
                (0.015625, "0.000001", "0.001", "0.01", "0.04", "0.0g"),
                (0.0078125, "0.0000001", "0.0002", "0.004", "0.02", "0.08"),
                (-0.0078125, "-0.0000001", "-0.0002", "-0.004", "-0.02", "-0.08"),
                (10.5908203125, "1010.1001011101", "22.21131", "12.4564", "a.974", "a.it"),
                (-10.5908203125, "-1010.1001011101", "-22.21131", "-12.4564", "-a.974", "-a.it"),
                (
                    522.0323028715758,
                    "1000001010.0000100001000101000000000100000011111100001",
                    "20022.0020101100001000333002",
                    "1012.020424002017604",
                    "20a.08450040fc2",
                    "ga.112g0g7s4"
                ),
                (
                    5.016810647407878e-23,
                    "0." + new string('0', 74) + "111100101001100100011011100100100011",
                    "0." + new string('0', 37) + "330221210123210203",
                    "0.0000000000000000000000001712310671106",
                    "0.0000000000000000003ca646e48c",
                    "0.000000000000001skp3e93"
                ),
                (
                    -5.016810647407878e-23,
                    "-0." + new string('0', 74) + "111100101001100100011011100100100011",
                    "-0." + new string('0', 37) + "330221210123210203",
                    "-0.0000000000000000000000001712310671106",
                    "-0.0000000000000000003ca646e48c",
                    "-0.000000000000001skp3e93"
                ),
                (
                    2.2250738585072014e-308,
                    "0." + new string('0', 1021) + "1",
                    "0." + new string('0', 510) + "1",
                    "0." + new string('0', 340) + "2",
                    "0." + new string('0', 255) + "4",
                    "0." + new string('0', 204) + "8"
                ),
                (
                    Double.Epsilon,
                    "0." + new string('0', 1073) + "1",
                    "0." + new string('0', 536) + "1",
                    "0." + new string('0', 357) + "1",
                    "0." + new string('0', 268) + "4",
                    "0." + new string('0', 214) + "2"
                ),
                (
                    -Double.Epsilon,
                    "-0." + new string('0', 1073) + "1",
                    "-0." + new string('0', 536) + "1",
                    "-0." + new string('0', 357) + "1",
                    "-0." + new string('0', 268) + "4",
                    "-0." + new string('0', 214) + "2"
                ),
                (
                    Double.Epsilon * 43,
                    "0." + new string('0', 1068) + "101011",
                    "0." + new string('0', 534) + "223",
                    "0." + new string('0', 356) + "53",
                    "0." + new string('0', 267) + "ac",
                    "0." + new string('0', 213) + "2m"
                ),

                (Double.PositiveInfinity, "Infinity", "Infinity", "Infinity", "Infinity", "Infinity"),
                (Double.NegativeInfinity, "-Infinity", "-Infinity", "-Infinity", "-Infinity", "-Infinity"),
                (Double.NaN, "NaN", "NaN", "NaN", "NaN", "NaN")
            );

        [Theory]
        [MemberData(nameof(doubleToStringPow2Radix_shouldFormat_data))]
        public void doubleToStringPow2Radix_shouldFormat(
            double value, string expectedBase2, string expectedBase4, string expectedBase8, string expectedBase16, string expectedBase32)
        {
            Assert.Equal(expectedBase2, doubleToStringPow2Radix(value, 2));
            Assert.Equal(expectedBase4, doubleToStringPow2Radix(value, 4));
            Assert.Equal(expectedBase8, doubleToStringPow2Radix(value, 8));
            Assert.Equal(expectedBase16, doubleToStringPow2Radix(value, 16));
            Assert.Equal(expectedBase32, doubleToStringPow2Radix(value, 32));
        }

        public static IEnumerable<object[]> doubleIntegerToStringRadix_shouldFormat_data = TupleHelper.toArrays(
            (0.0, 2, "0"),
            (0.0, 15, "0"),
            (0.0, 20, "0"),
            (0.0, 28, "0"),
            (0.0, 30, "0"),
            (0.0, 33, "0"),
            (NEG_ZERO, 2, "0"),
            (NEG_ZERO, 15, "0"),
            (NEG_ZERO, 20, "0"),
            (NEG_ZERO, 28, "0"),
            (NEG_ZERO, 30, "0"),
            (NEG_ZERO, 33, "0"),
            (1.0, 5, "1"),
            (1.0, 10, "1"),
            (1.0, 18, "1"),
            (1.0, 19, "1"),
            (1.0, 27, "1"),
            (-1.0, 5, "-1"),
            (-1.0, 10, "-1"),
            (-1.0, 18, "-1"),
            (-1.0, 19, "-1"),
            (-1.0, 27, "-1"),
            (-1.0, 29, "-1"),
            (2.0, 2, "10"),
            (2.0, 4, "2"),
            (2.0, 7, "2"),
            (2.0, 8, "2"),
            (2.0, 20, "2"),
            (2.0, 34, "2"),
            (4.0, 3, "11"),
            (4.0, 6, "4"),
            (4.0, 10, "4"),
            (4.0, 14, "4"),
            (4.0, 18, "4"),
            (4.0, 35, "4"),
            (8.0, 2, "1000"),
            (8.0, 6, "12"),
            (8.0, 8, "10"),
            (8.0, 10, "8"),
            (8.0, 27, "8"),
            (8.0, 36, "8"),
            (16.0, 3, "121"),
            (16.0, 8, "20"),
            (16.0, 13, "13"),
            (16.0, 16, "10"),
            (16.0, 25, "g"),
            (16.0, 35, "g"),
            (31.0, 5, "111"),
            (31.0, 10, "31"),
            (31.0, 12, "27"),
            (31.0, 14, "23"),
            (31.0, 16, "1f"),
            (31.0, 27, "14"),
            (32.0, 7, "44"),
            (32.0, 13, "26"),
            (32.0, 21, "1b"),
            (32.0, 25, "17"),
            (32.0, 26, "16"),
            (32.0, 30, "12"),
            (1356.0, 3, "1212020"),
            (1356.0, 7, "3645"),
            (1356.0, 12, "950"),
            (1356.0, 17, "4bd"),
            (1356.0, 19, "3e7"),
            (1356.0, 26, "204"),
            (745531.0, 5, "142324111"),
            (745531.0, 11, "46a146"),
            (745531.0, 19, "5dd39"),
            (-745531.0, 20, "-4d3gb"),
            (-745531.0, 36, "-fz97"),
            (2147483647.0, 17, "53g7f548"),
            (2147483647.0, 18, "3928g3h1"),
            (2147483647.0, 20, "1db1f927"),
            (2147483647.0, 23, "ebelf95"),
            (2147483647.0, 28, "4clm98f"),
            (2147483647.0, 35, "15v22um"),
            (2147483648.0, 4, "2000000000000000"),
            (2147483648.0, 12, "4bb2308a8"),
            (2147483648.0, 14, "1652ca932"),
            (-2147483648.0, 16, "-80000000"),
            (-2147483648.0, 30, "-2sb6cs8"),
            (2147483648.0, 36, "zik0zk"),
            (4294967295.0, 5, "32244002423140"),
            (4294967295.0, 6, "1550104015503"),
            (4294967295.0, 9, "12068657453"),
            (4294967295.0, 21, "281d55i3"),
            (-4294967295.0, 30, "-5qmcpqf"),
            (-4294967295.0, 32, "-3vvvvvv"),
            (4294967296.0, 9, "12068657454"),
            (4294967296.0, 10, "4294967296"),
            (4294967296.0, 11, "1904440554"),
            (-4294967296.0, 14, "-2ca5b7464"),
            (4294967296.0, 17, "a7ffda91"),
            (4294967296.0, 19, "4f5aff66"),
            (9007199254740991.0, 5, "33421042423033203202431"),
            (9007199254740991.0, 16, "1fffffffffffff"),
            (9007199254740991.0, 19, "416210bi7ca49"),
            (-9007199254740991.0, 22, "-f92hf53a8cc7"),
            (9007199254740991.0, 28, "12bd1h7b56h3"),
            (9007199254740991.0, 33, "5t2d3e17rj7"),
            (-9007199254740992.0, 5, "-33421042423033203202432"),
            (9007199254740992.0, 6, "224404414114114022452"),
            (9007199254740992.0, 8, "400000000000000000"),
            (-9007199254740992.0, 15, "-4964cdca1dc7b2"),
            (9007199254740992.0, 22, "f92hf53a8cc8"),
            (9007199254740992.0, 29, "lbpf6d7shib"),
            (9007199254740994.0, 5, "33421042423033203202434"),
            (9007199254740994.0, 14, "b4c34aaccadc66"),
            (9007199254740994.0, 23, "9a9i7gmkbfj8"),
            (9007199254740994.0, 24, "5m1bec25hbda"),
            (-9007199254740994.0, 27, "-1gk4mmhm95ag"),
            (9007199254740994.0, 28, "12bd1h7b56h6"),
            (9007199254740996.0, 6, "224404414114114022500"),
            (9007199254740996.0, 12, "702273685b77a30"),
            (9007199254740996.0, 14, "b4c34aaccadc68"),
            (-9007199254740996.0, 22, "-f92hf53a8ccc"),
            (9007199254740996.0, 23, "9a9i7gmkbfja"),
            (9007199254740996.0, 28, "12bd1h7b56h8"),
            (-1.8014398509481982e+16, 3, "-10020111100200200022122200101202222"),
            (1.8014398509481982e+16, 14, "189a6977bb7dac6"),
            (1.8014398509481982e+16, 18, "fa5704b6gh1d8"),
            (1.8014398509481982e+16, 21, "299037b83fa5k"),
            (-1.8014398509481982e+16, 33, "-bp4q6s2fm5e"),
            (1.8014398509481982e+16, 33, "bp4q6s2fm5e"),
            (1.8014398509481984e+16, 3, "10020111100200200022122200101210001"),
            (-1.8014398509481984e+16, 4, "-1000000000000000000000000000"),
            (1.8014398509481984e+16, 6, "453213232232232045344"),
            (1.8014398509481984e+16, 9, "106440620278611701"),
            (1.8014398509481984e+16, 18, "fa5704b6gh1da"),
            (1.8014398509481984e+16, 26, "4nfmhd4egmmc"),
            (1.8014398509481988e+16, 3, "10020111100200200022122200101210012"),
            (1.8014398509481988e+16, 14, "189a6977bb7dacc"),
            (1.8014398509481988e+16, 18, "fa5704b6gh1de"),
            (1.8014398509481988e+16, 31, "luahljclc5k"),
            (1.8014398509481988e+16, 33, "bp4q6s2fm5k"),
            (1.8014398509481988e+16, 34, "8onkt2p4oq0"),
            (3.6028797018963976e+16, 3, "20110222201101100122022100210120101"),
            (-3.6028797018963976e+16, 14, "-3356d511991d7ba"),
            (3.6028797018963976e+16, 15, "137a46a5a7a5101"),
            (3.6028797018963976e+16, 18, "1d2ae094dfg39a"),
            (3.6028797018963976e+16, 23, "1eig47lmd0h89"),
            (3.6028797018963976e+16, 36, "9ur54ut49vs"),
            (1.8446744069414584e+19, 9, "145808576342137055303"),
            (1.8446744069414584e+19, 15, "2c1d56b62da5e0280"),
            (1.8446744069414584e+19, 19, "141c8786c571deab"),
            (1.8446744069414584e+19, 26, "7b7n2pc9lc30ik"),
            (1.8446744069414584e+19, 29, "1n3rsh0n8hnjb8"),
            (1.8446744069414584e+19, 32, "fvvvvvs000000"),
            (1.4757395258967641e+20, 14, "4ba882983c423c4c12"),
            (1.4757395258967641e+20, 16, "80000000000000000"),
            (1.4757395258967641e+20, 25, "3o0m015h77lfah3"),
            (1.4757395258967641e+20, 27, "19b5f5f5fpicclb"),
            (-1.4757395258967641e+20, 31, "-61b1qdmr2e73l4"),
            (1.4757395258967641e+20, 36, "v57488hd2bqbk"),
            (1.4757395259059972e+20, 7, "525132240051116416462054"),
            (1.4757395259059972e+20, 15, "17701d915710ab1194"),
            (-1.4757395259059972e+20, 18, "-13fd16g52746cace4"),
            (-1.4757395259059972e+20, 22, "-11fhja81bc0cag6c"),
            (1.4757395259059972e+20, 23, "cgi0l0lij19gfbb"),
            (1.4757395259059972e+20, 26, "27cb2nlpal8jp8k"),
            (
                1.576649e+102,
                2,
                "1011010000110101100000001000110001111000110100011101" + new string('0', 288)
            ),
            (
                1.576649e+102,
                3,
                "10201112011021221110012022222220010111010111200221010120222110122112122202200121101022120001022202202122100121001021121002110122001010000222011202002110002001220222112110121111211100010222112011021202120022202122200"
            ),
            (-1.576649e+102, 8, "-132065401061706435" + new string('0', 96)),
            (
                1.576649e+102,
                19,
                "f016gi7h52ah60db4hb62997a81655ef15h700c66959h5bf6fa5d742agh750bc20i293785db01ed4"
            ),
            (
                1.576649e+102,
                27,
                "3je47pc58qo3d3dip3fqchehkiga8f18kkh9g17g2ch130q4k2c21oqecgdm93qe47kf8khi"
            ),
            (
                1.576649e+102,
                30,
                "1qkj0s4e6ncnr7259shlqorf1b3gq926dfbe045at3tqjl5m79i4kkirao4dn00s9og09i"
            ),
            (
                7.589337662678885e+256,
                7,
                "6352543513441336216663564655355245011153503032343003610443053646412145036222335151041005603450260503054232032414602266635560315531416010434234243245041460550053565000243134106512202552631124123423510252630531436050211116115064423143616551662424410664256462145101114216433524141414661144602450610446454230"
            ),
            (7.589337662678885e+256, 8, "241574635077751123" + new string('0', 267)),
            (
                7.589337662678885e+256,
                9,
                "148136156718061817143654088737250246412733747774016657201554187125324421146571410066017231370470625652243153887652101434880283204281456568170532487383670025752265804486502451582710021882458623063801535566820562516306320264711872113450716482283044165720841184373658621386"
            ),
            (
                7.589337662678885e+256,
                14,
                "1593d77601cbb807383c1292908bc1419a302d5c525682b4325a1d7c224d00a8a5728623404b5133a8498ca48157573115c039cc724b994c26853c2b0c540590a532b80a4c6620733b694d150cc2b2bc606dd3d537668c1db4cb2b51300c6d2b238bcbd317675a82055810699cb9abcc0"
            ),
            (
                7.589337662678885e+256,
                17,
                "8e6g9155e9db1fd9c1geb1bb6d9cg9a91a030309f90cb15d82bf50c72351b65c9282f316974a0aca35ed399f97e202470egc99415fdg8f4c3g3da77381c6588271a383866f263de30377b7db72f20706934g55a3175fdd4d278c1b9cb21c5234egc2gbe31d561059a"
            ),
            (
                7.589337662678885e+256,
                18,
                "66e267d6eh1c3084afa53ee4d9h08a10eaa874h8e23fhaed8e942d1e7gc61e4238187e26h6ab572c61341ac6fag51ga12ca70d017b163g497hd1c29bg44e1446adhb1gg360c2103fb5f77274b4bh884d54h8hcc9a99a98a8b51hg35h157f9hd8f6b8a9c426gd6"
            ),
            (
                8.988465674311579e+307,
                2,
                "11111111111111111111111111111111111111111111111111111" + new string('0', 970)
            ),
            (
                8.988465674311579e+307,
                10,
                "89884656743115785407263711865852178399035283762922498299458738401578630390014269380294779316383439085770229476757191232117160663444732091384233773351768758493024955288275641038122745045194664472037934254227566971152291618451611474082904279666061674137398913102072361584369088590459649940625202013092062429184"
            ),
            (
                8.988465674311579e+307,
                11,
                "559348759a420a58a2556a05431a4767349aa860650267208124499a191a7817993501507812a7a3014671475725297593a3179a597557a373775a681927104044049496010272a0547835930123a5034857348a6358006291016477346113897528578672307016aa1099aa956431059057220206152a4a2424a73974391594a605328984096a8895a5063942a7333170964397"
            ),
            (
                8.988465674311579e+307,
                15,
                "9cd6b0aaa605b5008ba03db93e0bee11a49a8d130941ed68558950410c2098b32c357e3b21c88b3e429055a879d54b411c3a3784b26ac85c78dbc6820a1874c8917418b3b8c162b3b2a7b2505e18e65e8c6e9b9470739d760d26cabe0a5210a44c5d69c2c77cc2020abc40dc54a442d1e5422bd0cd0e61d557914ebcb9c7e4b8325774"
            ),
            (
                8.988465674311579e+307,
                28,
                "e9529a7fnrid07hl15mg0anp6m3e443j4icl1g65dr6fhm50phckriac91qhd7opkg0rrgngl3iife7m0db3229jl7hqncr9li6kblioicphl5m937k7fqhg2fcf3e6qfjiihcf0mlffrbf45p6n3kc65a155oi627eie87153gkio5kari5m95qbjappiblap4ahlp1ild7g12fegqlk"
            ),
            (
                8.988465674311579e+307,
                30,
                "54m0iodi97pgflqrnen2akcrgs8p3afqnil2b6o031dj50je310qmj788ar7pk2a2156k1337fbcnoegjgs3js0ms5f3bj47338i7tos8tfc2b7oc6k3ge49b7k9d2bqlnronafgn6bgpbh38t839lacnnl80d3cmfhirjns77khtqho3cas77n8njskdd55d5h8m8hl2n58g2gq4"
            ),
            (
                1.34e+308,
                2,
                "10111110110100101000010001111100111010011101011101011" + new string('0', 971)
            ),
            (
                1.34e+308,
                11,
                "8278a07847919238287365a7362362876070105029a24008a9953809012294a9432916903a216444967064455322aa938674342828343968729450485363aa4172943281a336775615362670940330611a7327875487614a1a746972457922883039284068a20122529462148a34567a1164230752013047778903711143534485a0184173a919709246954669a0aa91742775a7"
            ),
            (
                -1.34e+308,
                18,
                "-3f510457659g4g5bfa61ch629h3635f644ab0d8c7dadb6c5ha6621de03227be467fe9597a91406bf8c3aghdcdf0041813324d603138g410hh99hhch4h9gce875b3e0cfh999bd5e7gch54e5acgc7aa6f1a6h78bggabf5698aegbhcg9f4dbfb2607261676ehe10g86g5d4ebfe118g242c6c139gfegh17a63db697beg"
            ),
            (
                1.34e+308,
                22,
                "539ai71j2ik8k6i0b4d1ca4ahblj8097k42ehe974l16a2e7j9e3al368b645e9ic9leb3d6i5a3e757c4e28ajada79a71lif45j4kk51a1eglhf2jk04haka308k6a8fl871gie96820465g30gbe2c1c1i5g11kh2ldef35baf5f53c0a1abfeic80b903ccef01jjl2idl4111kjeh08k10lg7319fd6li"
            ),
            (
                1.34e+308,
                28,
                "la27jephr1o1c3k9nfpflpr895cq02a1najgfpg1nreckii39on6806pij9hj0d86na67oj3rqmmrki3dnbm3527q3259jl20mg11mh3fao2q3d0fh7pf4jm72nj2a511pcf491m992gbhddi0b8r31f6dpriq6rigjfmbm07eedomgpk05a7p4h2n3r6gga3j6gm9aoe6in36mcpfqa8"
            ),
            (
                1.34e+308,
                30,
                "7kk9k44smkpto18fhjhp8qoc3c5i2kk9ot4nifneq115ne8qja1a47ogeat69ga5c4c0ojhe54q16hotgje2c7356njkrqhke5esrcqal1pn4gni46f75tjraletitclo2bi060mjqrdg0kjp4ajtob0p3mjebq4l9gqhlhle4qj83rohq2oot104hrb8it9ajn7sb2m237gb6m7m"
            ),
            (
                1.7976931348623157e+308,
                4,
                "333333333333333333333333332" + new string('0', 485)
            ),
            (
                1.7976931348623157e+308,
                7,
                "42326224003605451310554404242624410603406055251244335360504122451145412556002204414340402213252061316665443530166425161224466044502022663322110623024535014021166542443601621160653643446062402342601354252363305333444143005032331550055004054110163453105261322444016604255115302134426043121536061643306155221452533556632312505522062644216061216064405622122411612120255"
            ),
            (
                -1.7976931348623157e+308,
                12,
                "-4a617b880b332814113338a4614899872806551a9a92b65b71b28b2553763564942526200150b66b24b93a4073075412a45283b4929878b97542b8aa25b38259973b0aa86026218649ba761251a384a28636776547b28321748713850304aa8b772702b27646849a1867b181883ba459219b5842a018a14b675b83145a053743554535a588255b3913568499aa20a8"
            ),
            (
                1.7976931348623157e+308,
                18,
                "52h011d73a63c1568a6cfa07c94ahh72f6c8d653bd79f95e4395ec65c2e593528ac5ch75664986ca3976dh2ga70dhec974a2c4530bc1aef11935g86fg22eff928811730eaa98d13d4a480hg88fab1880b5d8h8050ab1a43c025503g5e608f9a37h73gf1874b8gh867h3557gbg426h9b0fbh2d3fc7eb7gb11ffcf12"
            ),
            (
                -1.7976931348623157e+308,
                25,
                "-52f0dhiej0i37a9a034b5meakei53aagk0eoc21fone287827a110l6170gj4ggje656758d127ke50fa3b4mnd0hlh90c0517j87n2jfm5j7267nnja2geojibod7icjahf2cg8if6a6e2lajn5ig5eencf03id04ane6o81bn1127787f750jn910c5an2kb7kjj07ej4m717ac9bd10049fn9i"
            ),
            (
                1.7976931348623157e+308,
                30,
                "a9e17ir6ifl31dnpgtg4lapp3qhk6l1nh7c4mdi062r8a18s621nf8egglofla4k42ada266f0mphit393q79q1fqb06n88e66h6ftjqht0o4mfioda72s8imfaiq4nndhpjgl13gcn3kn46hsg6jckphhcg0q6pf157p9hqefb5tn5i6olqefghh9raqqaaqb4heh5c5gah253m8"
            ),

            (519072724966.0, 9, "1747731815244"),
            (1545166143127.0, 5, "200304000013040002"),
            (2821170326977.0, 36, "1000zz001"),
            (59401356089.0, 3, "12200022210010112200112"),
            (4177248169401010.0, 11, "aaaaaaaaaaa0000"),

            (0.1, 2, "0"),
            (0.3, 12, "0"),
            (0.7, 18, "0"),
            (0.000461, 23, "0"),
            (0.999999, 19, "0"),
            (-0.948113, 32, "0"),
            (-0.000581, 7, "0"),
            (Double.Epsilon, 4, "0"),
            (Double.Epsilon, 7, "0"),
            (-Double.Epsilon, 8, "0"),
            (-Double.Epsilon, 29, "0"),

            (1.2, 5, "1"),
            (1.7, 9, "1"),
            (-1.6, 13, "-1"),

            (745531.0000382, 2, "10110110000000111011"),
            (745531.8562113, 4, "2312000323"),
            (745531.7562114, 5, "142324111"),
            (745531.9483316, 8, "2660073"),
            (745531.0039210, 11, "46a146"),
            (-745531.6327455, 16, "-b603b"),
            (-745531.9974116, 19, "-5dd39"),
            (-745531.5732884, 20, "-4d3gb"),
            (-745531.9871123, 32, "-mo1r"),
            (-745531.6981994, 36, "-fz97"),

            (4294967295.65421, 5, "32244002423140"),
            (4294967295.09414, 6, "1550104015503"),
            (4294967295.78931, 9, "12068657453"),
            (4294967295.33672, 21, "281d55i3"),
            (-4294967295.00007, 30, "-5qmcpqf"),
            (-4294967295.99992, 32, "-3vvvvvv"),

            (Double.PositiveInfinity, 2, "Infinity"),
            (Double.PositiveInfinity, 5, "Infinity"),
            (Double.PositiveInfinity, 9, "Infinity"),
            (Double.PositiveInfinity, 16, "Infinity"),
            (Double.PositiveInfinity, 23, "Infinity"),
            (Double.PositiveInfinity, 36, "Infinity"),
            (Double.NegativeInfinity, 2, "-Infinity"),
            (Double.NegativeInfinity, 5, "-Infinity"),
            (Double.NegativeInfinity, 9, "-Infinity"),
            (Double.NegativeInfinity, 16, "-Infinity"),
            (Double.NegativeInfinity, 23, "-Infinity"),
            (Double.NegativeInfinity, 36, "-Infinity"),
            (Double.NaN, 2, "NaN"),
            (Double.NaN, 5, "NaN"),
            (Double.NaN, 9, "NaN"),
            (Double.NaN, 16, "NaN"),
            (Double.NaN, 23, "NaN"),
            (Double.NaN, 36, "NaN")
        );

        [Theory]
        [MemberData(nameof(doubleIntegerToStringRadix_shouldFormat_data))]
        public void doubleIntegerToStringRadix_shouldFormat(double value, int radix, string expected) {
            Assert.Equal(expected, doubleIntegerToStringRadix(value, radix));
        }

    }

}
