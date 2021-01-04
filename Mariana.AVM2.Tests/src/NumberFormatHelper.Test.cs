using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mariana.AVM2.Core;
using Xunit;

using static Mariana.AVM2.Core.NumberFormatHelper;

namespace Mariana.AVM2.Tests {

    public class NumberFormatHelperTest {

        [Fact]
        public void getToFixedFormatString_shouldGetFormatString() {
            for (int i = 0; i <= 20; i++)
                Assert.Equal("F" + i.ToString(CultureInfo.InvariantCulture), getToFixedFormatString(i));
        }

        [Fact]
        public void getToExponentialFormatString_shouldGetFormatString() {
            Assert.Equal("0e+0", getToExponentialFormatString(0));
            for (int i = 1; i <= 20; i++)
                Assert.Equal("0." + new string('0', i) + "e+0", getToExponentialFormatString(i));
        }

        public static IEnumerable<object[]> intToString_shouldFormatIntInGivenRadix_data = new (int, int, string)[] {
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
            (2147483647, 10, "2147483647"),
            (2147483647, 32, "1vvvvvv"),
            (2147483647, 36, "zik0zj"),
            (-2147483648, 2, "-10000000000000000000000000000000"),
            (-2147483648, 10, "-2147483648"),
            (-2147483648, 32, "-2000000"),
            (-2147483648, 36, "-zik0zk"),
        }.Select(x => new object[] {x.Item1, x.Item2, x.Item3});

        [Theory]
        [MemberData(nameof(intToString_shouldFormatIntInGivenRadix_data))]
        public void intToString_shouldFormatIntInGivenRadix(int value, int radix, string expected) {
            Assert.Equal(expected, intToString(value, radix));
        }

    }

}
