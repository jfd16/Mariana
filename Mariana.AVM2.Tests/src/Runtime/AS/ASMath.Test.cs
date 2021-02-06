using System;
using System.Threading;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.TestDoubles;
using Xunit;

using static Mariana.AVM2.Core.ASMath;
using Rest = Mariana.AVM2.Core.RestParam;

namespace Mariana.AVM2.Tests {

    public class ASMathTest {

        private const long NEG_ZERO_BITS = unchecked((long)0x8000000000000000uL);
        private static readonly double NEG_ZERO = BitConverter.Int64BitsToDouble(NEG_ZERO_BITS);

        private const double POS_INF = Double.PositiveInfinity;
        private const double NEG_INF = Double.NegativeInfinity;
        private const double POS_EPSILON = Double.Epsilon;
        private const double NEG_EPSILON = -Double.Epsilon;
        private const double NAN = Double.NaN;

        private static readonly double TEST_EPSILON = Math.ScaleB(1, -50);

        private static void assertPositiveZero(double value) => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value));
        private static void assertNegativeZero(double value) => Assert.Equal(NEG_ZERO_BITS, BitConverter.DoubleToInt64Bits(value));

        private static void assertPositiveInfinity(double value) => Assert.Equal(POS_INF, value);
        private static void assertNegativeInfinity(double value) => Assert.Equal(NEG_INF, value);

        private static void assertNaN(double value) => Assert.True(Double.IsNaN(value));

        private static void assertEpsilonEqual(double expected, double value) {
            Assert.True((expected < 0) == (value < 0),
                $"Expected {value:R} to have the same sign as {expected:R}.");

            Assert.True(Math.Abs(value - expected) <= TEST_EPSILON * Math.Abs(expected),
                $"Expected {value:R} to be close to {expected:R}.");
        }

        [Fact]
        public void absTest() {
            assertPositiveZero(abs(0.0));
            assertPositiveZero(abs(NEG_ZERO));

            Assert.Equal(1.0, abs(1.0));
            Assert.Equal(1.0, abs(-1.0));

            Assert.Equal(0.5, abs(0.5));
            Assert.Equal(0.5, abs(-0.5));

            Assert.Equal(375.1887884733216, abs(375.1887884733216));
            Assert.Equal(375.1887884733216, abs(-375.1887884733216));

            assertPositiveInfinity(abs(POS_INF));
            assertPositiveInfinity(abs(NEG_INF));

            assertNaN(abs(NAN));
        }

        [Fact]
        public void acosTest() {
            assertEpsilonEqual(PI, acos(-1.0));
            assertEpsilonEqual(PI / 2, acos(0.0));
            assertEpsilonEqual(1.0471975511965979, acos(0.5));
            assertEpsilonEqual(2.0943951023931957, acos(-0.5));

            assertPositiveZero(acos(1.0));

            assertNaN(acos(Math.BitIncrement(1.0)));
            assertNaN(acos(Math.BitDecrement(-1.0)));
            assertNaN(acos(2.0));
            assertNaN(acos(-2.0));
            assertNaN(acos(POS_INF));
            assertNaN(acos(NEG_INF));
            assertNaN(acos(NAN));
        }

        [Fact]
        public void asinTest() {
            assertEpsilonEqual(-PI / 2, asin(-1.0));
            assertEpsilonEqual(PI / 2, asin(1.0));
            assertEpsilonEqual(-0.5235987755982989, asin(-0.5));
            assertEpsilonEqual(0.5235987755982989, asin(0.5));

            assertPositiveZero(asin(0.0));
            assertNegativeZero(asin(NEG_ZERO));

            assertNaN(acos(Math.BitIncrement(1.0)));
            assertNaN(acos(Math.BitDecrement(-1.0)));
            assertNaN(acos(2.0));
            assertNaN(acos(-2.0));
            assertNaN(acos(POS_INF));
            assertNaN(acos(NEG_INF));
            assertNaN(acos(NAN));
        }

        [Fact]
        public void atanTest() {
            assertPositiveZero(atan(0.0));
            assertNegativeZero(atan(NEG_ZERO));

            assertEpsilonEqual(0.7853981633974483, atan(1.0));
            assertEpsilonEqual(-0.7853981633974483, atan(-1.0));
            assertEpsilonEqual(0.4636476090008061, atan(0.5));
            assertEpsilonEqual(-0.4636476090008061, atan(-0.5));
            assertEpsilonEqual(1.1071487177940904, atan(2.0));
            assertEpsilonEqual(-1.1071487177940904, atan(-2.0));

            assertEpsilonEqual(-PI / 2, atan(NEG_INF));
            assertEpsilonEqual(PI / 2, atan(POS_INF));

            assertNaN(atan(NAN));
        }

        [Fact]
        public void atan2Test() {
            assertNaN(atan2(NAN, 0.0));
            assertNaN(atan2(0.0, NAN));
            assertNaN(atan2(NAN, NAN));

            assertEpsilonEqual(PI / 2, atan2(POS_EPSILON, 0.0));
            assertEpsilonEqual(PI / 2, atan2(POS_EPSILON, NEG_ZERO));
            assertEpsilonEqual(PI / 2, atan2(1.0, 0.0));
            assertEpsilonEqual(PI / 2, atan2(1.0, NEG_ZERO));
            assertEpsilonEqual(PI / 2, atan2(POS_INF, 0.0));
            assertEpsilonEqual(PI / 2, atan2(POS_INF, NEG_ZERO));

            assertPositiveZero(atan2(0.0, POS_EPSILON));
            assertPositiveZero(atan2(0.0, 1.0));
            assertPositiveZero(atan2(0.0, POS_INF));

            assertPositiveZero(atan2(0.0, 0.0));

            assertEpsilonEqual(PI, atan2(0.0, NEG_ZERO));
            assertEpsilonEqual(PI, atan2(0.0, NEG_EPSILON));
            assertEpsilonEqual(PI, atan2(0.0, -1.0));
            assertEpsilonEqual(PI, atan2(0.0, NEG_INF));

            assertNegativeZero(atan2(NEG_ZERO, POS_EPSILON));
            assertNegativeZero(atan2(NEG_ZERO, 1.0));
            assertNegativeZero(atan2(NEG_ZERO, POS_INF));

            assertNegativeZero(atan2(NEG_ZERO, 0.0));

            assertEpsilonEqual(-PI, atan2(NEG_ZERO, NEG_ZERO));
            assertEpsilonEqual(-PI, atan2(NEG_ZERO, NEG_EPSILON));
            assertEpsilonEqual(-PI, atan2(NEG_ZERO, -1.0));
            assertEpsilonEqual(-PI, atan2(NEG_ZERO, NEG_INF));

            assertEpsilonEqual(-PI / 2, atan2(NEG_EPSILON, 0.0));
            assertEpsilonEqual(-PI / 2, atan2(NEG_EPSILON, NEG_ZERO));
            assertEpsilonEqual(-PI / 2, atan2(-1.0, 0.0));
            assertEpsilonEqual(-PI / 2, atan2(-1.0, NEG_ZERO));
            assertEpsilonEqual(-PI / 2, atan2(NEG_INF, 0.0));
            assertEpsilonEqual(-PI / 2, atan2(NEG_INF, NEG_ZERO));

            assertPositiveZero(atan2(POS_EPSILON, POS_INF));
            assertPositiveZero(atan2(1.0, POS_INF));
            assertEpsilonEqual(PI, atan2(POS_EPSILON, NEG_INF));
            assertEpsilonEqual(PI, atan2(1.0, NEG_INF));

            assertNegativeZero(atan2(NEG_EPSILON, POS_INF));
            assertNegativeZero(atan2(-1.0, POS_INF));
            assertEpsilonEqual(-PI, atan2(NEG_EPSILON, NEG_INF));
            assertEpsilonEqual(-PI, atan2(-1.0, NEG_INF));

            assertEpsilonEqual(PI / 2, atan2(POS_INF, 0.0));
            assertEpsilonEqual(PI / 2, atan2(POS_INF, NEG_ZERO));
            assertEpsilonEqual(PI / 2, atan2(POS_INF, POS_EPSILON));
            assertEpsilonEqual(PI / 2, atan2(POS_INF, NEG_EPSILON));
            assertEpsilonEqual(PI / 2, atan2(POS_INF, 1.0));
            assertEpsilonEqual(PI / 2, atan2(POS_INF, -1.0));

            assertEpsilonEqual(-PI / 2, atan2(NEG_INF, 0.0));
            assertEpsilonEqual(-PI / 2, atan2(NEG_INF, NEG_ZERO));
            assertEpsilonEqual(-PI / 2, atan2(NEG_INF, POS_EPSILON));
            assertEpsilonEqual(-PI / 2, atan2(NEG_INF, NEG_EPSILON));
            assertEpsilonEqual(-PI / 2, atan2(NEG_INF, 1.0));
            assertEpsilonEqual(-PI / 2, atan2(NEG_INF, -1.0));

            assertEpsilonEqual(PI / 4, atan2(POS_INF, POS_INF));
            assertEpsilonEqual(-PI / 4, atan2(NEG_INF, POS_INF));
            assertEpsilonEqual(PI * 0.75, atan2(POS_INF, NEG_INF));
            assertEpsilonEqual(-PI * 0.75, atan2(NEG_INF, NEG_INF));

            assertEpsilonEqual(0.7853981633974483, atan2(1.0, 1.0));
            assertEpsilonEqual(-0.7853981633974483, atan2(-1.0, 1.0));
            assertEpsilonEqual(2.356194490192345, atan2(1.0, -1.0));
            assertEpsilonEqual(-2.356194490192345, atan2(-1.0, -1.0));
        }

        [Fact]
        public void ceilTest() {
            assertPositiveZero(ceil(0.0));
            assertNegativeZero(ceil(NEG_ZERO));

            assertPositiveInfinity(ceil(POS_INF));
            assertNegativeInfinity(ceil(NEG_INF));

            assertNaN(ceil(NAN));

            Assert.Equal(1.0, ceil(POS_EPSILON));
            Assert.Equal(1.0, ceil(0.5));
            Assert.Equal(1.0, ceil(Math.BitDecrement(1.0)));
            Assert.Equal(1.0, ceil(1.0));
            Assert.Equal(2.0, ceil(Math.BitIncrement(1.0)));

            assertNegativeZero(ceil(Math.BitIncrement(-1.0)));
            assertNegativeZero(ceil(NEG_EPSILON));
            assertNegativeZero(ceil(-0.5));

            Assert.Equal(-1.0, ceil(-1.0));
            Assert.Equal(-1.0, ceil(Math.BitDecrement(-1.0)));
            Assert.Equal(-1.0, ceil(Math.BitIncrement(-2.0)));
            Assert.Equal(-2.0, ceil(-2.0));
        }

        [Fact]
        public void cosTest() {
            assertNaN(cos(NAN));
            assertNaN(cos(POS_INF));
            assertNaN(cos(NEG_INF));

            assertEpsilonEqual(1.0, cos(0.0));
            assertEpsilonEqual(1.0, cos(NEG_ZERO));

            assertEpsilonEqual(0.5403023058681398, cos(1.0));
            assertEpsilonEqual(0.5403023058681398, cos(-1.0));
            assertEpsilonEqual(-0.4161468365471424, cos(2.0));
            assertEpsilonEqual(-0.4161468365471424, cos(-2.0));

            assertEpsilonEqual(-1.0, cos(PI));
            assertEpsilonEqual(-1.0, cos(-PI));
        }

        [Fact]
        public void expTest() {
            assertNaN(exp(NAN));

            assertPositiveInfinity(exp(POS_INF));
            assertPositiveZero(exp(NEG_INF));

            assertEpsilonEqual(1.0, exp(0.0));
            assertEpsilonEqual(1.0, exp(NEG_ZERO));

            assertEpsilonEqual(E, exp(1.0));
            assertEpsilonEqual(1.0 / E, exp(-1.0));

            assertEpsilonEqual(-1.0, cos(PI));
            assertEpsilonEqual(-1.0, cos(-PI));
        }

        [Fact]
        public void floorTest() {
            assertPositiveZero(floor(0.0));
            assertNegativeZero(floor(NEG_ZERO));

            assertPositiveInfinity(floor(POS_INF));
            assertNegativeInfinity(floor(NEG_INF));

            assertNaN(floor(NAN));

            assertPositiveZero(floor(POS_EPSILON));
            assertPositiveZero(floor(0.5));
            assertPositiveZero(floor(Math.BitDecrement(1.0)));
            Assert.Equal(1.0, floor(1.0));
            Assert.Equal(1.0, floor(Math.BitIncrement(1.0)));

            Assert.Equal(-1.0, floor(Math.BitIncrement(-1.0)));
            Assert.Equal(-1.0, floor(NEG_EPSILON));
            Assert.Equal(-1.0, floor(-0.5));

            Assert.Equal(-1.0, floor(-1.0));
            Assert.Equal(-2.0, floor(Math.BitDecrement(-1.0)));
            Assert.Equal(-2.0, floor(Math.BitIncrement(-2.0)));
            Assert.Equal(-2.0, floor(-2.0));
        }

        [Fact]
        public void logTest() {
            assertNaN(log(NAN));
            assertPositiveInfinity(log(POS_INF));
            assertPositiveZero(log(1.0));
            assertNegativeInfinity(log(0.0));
            assertNegativeInfinity(log(NEG_ZERO));

            assertNaN(log(NEG_EPSILON));
            assertNaN(log(-1.0));
            assertNaN(log(NEG_INF));

            assertEpsilonEqual(1.0, log(E));
            assertEpsilonEqual(-1.0, log(1.0 / E));
            assertEpsilonEqual(LN2, log(2.0));
            assertEpsilonEqual(LN10, log(10.0));
        }

        [Fact]
        public void minTest() {
            assertPositiveInfinity(min());

            assertPositiveZero(min(new Rest(0.0)));
            assertNegativeZero(min(new Rest(NEG_ZERO)));
            Assert.Equal(1.0, min(new Rest(1.0)));
            Assert.Equal(-1.0, min(new Rest(-1.0)));
            Assert.Equal(POS_INF, min(new Rest(POS_INF)));
            Assert.Equal(NEG_INF, min(new Rest(NEG_INF)));
            Assert.Equal(NAN, min(new Rest(NAN)));

            assertPositiveZero(min(new Rest(0.0, 1.0)));
            assertPositiveZero(min(new Rest(1.0, 0.0)));
            assertPositiveZero(min(new Rest(0.0, 0.0)));
            assertPositiveZero(min(new Rest(1.0, 0.0, 1.0, 0.0)));

            assertNegativeZero(min(new Rest(NEG_ZERO, 0.0)));
            assertNegativeZero(min(new Rest(NEG_ZERO, NEG_ZERO)));
            assertNegativeZero(min(new Rest(NEG_ZERO, 1.0)));
            assertNegativeZero(min(new Rest(1.0, NEG_ZERO)));
            assertNegativeZero(min(new Rest(0.0, 0.0, NEG_ZERO, 0.0)));
            assertNegativeZero(min(new Rest(1.0, 1.0, NEG_ZERO, 0.0)));
            assertNegativeZero(min(new Rest(1.0, NEG_ZERO, 1.0, 0.0)));
            assertNegativeZero(min(new Rest(1.0, 1.0, 0.0, NEG_ZERO)));

            assertNaN(min(new Rest(NAN, 0.0)));
            assertNaN(min(new Rest(0.0, NAN)));
            assertNaN(min(new Rest(NAN, 1.0)));
            assertNaN(min(new Rest(NEG_INF, NAN)));
            assertNaN(min(new Rest(NAN, NEG_INF)));
            assertNaN(min(new Rest(NAN, NAN)));
            assertNaN(min(new Rest(0.0, 0.0, NAN, 0.0)));
            assertNaN(min(new Rest(NEG_INF, NEG_INF, NAN, NEG_INF)));
            assertNaN(min(new Rest(NAN, 0.0, 0.0, NEG_INF)));
            assertNaN(min(new Rest(NAN, 0.0, -1.0, -2.0)));

            assertPositiveInfinity(min(new Rest(POS_INF, POS_INF)));
            assertPositiveInfinity(min(new Rest(POS_INF, POS_INF, POS_INF, POS_INF)));

            assertNegativeInfinity(min(new Rest(NEG_INF, 0.0)));
            assertNegativeInfinity(min(new Rest(0.0, NEG_INF)));
            assertNegativeInfinity(min(new Rest(NEG_INF, 1.0)));
            assertNegativeInfinity(min(new Rest(1.0, NEG_INF)));
            assertNegativeInfinity(min(new Rest(NEG_INF, -1.0)));
            assertNegativeInfinity(min(new Rest(-1.0, NEG_INF)));
            assertNegativeInfinity(min(new Rest(NEG_INF, POS_INF)));
            assertNegativeInfinity(min(new Rest(NEG_INF, NEG_INF)));
            assertNegativeInfinity(min(new Rest(0.0, 0.0, NEG_INF, 0.0)));
            assertNegativeInfinity(min(new Rest(NEG_INF, NEG_INF, NEG_INF, NEG_INF)));
            assertNegativeInfinity(min(new Rest(NEG_INF, 0.0, 0.0, NEG_INF)));
            assertNegativeInfinity(min(new Rest(NEG_INF, 0.0, -1.0, -2.0)));

            Assert.Equal(1.0, min(new Rest(1.0, 2.0)));
            Assert.Equal(1.0, min(new Rest(2.0, 1.0)));
            Assert.Equal(-1.0, min(new Rest(-1.0, 1.0)));
            Assert.Equal(-1.0, min(new Rest(1.0, -1.0)));
            Assert.Equal(-2.0, min(new Rest(-1.0, -2.0)));
            Assert.Equal(-2.0, min(new Rest(-1.0, -2.0)));
            Assert.Equal(1.0, min(new Rest(1.0, 2.0, 3.0, 4.0)));
            Assert.Equal(1.0, min(new Rest(4.0, 3.0, 2.0, 1.0)));
            Assert.Equal(1.0, min(new Rest(2.0, 3.0, 1.0, 4.0)));

            Assert.Equal(1.0, min(new Rest(1.0, POS_INF)));
            Assert.Equal(1.0, min(new Rest(POS_INF, 1.0)));

            var obj1 = new SpyObjectWithConversions {numberValue = 1.0};
            var obj2 = new SpyObjectWithConversions {numberValue = 2.0};

            Assert.Equal(1.0, min(new Rest(obj1, obj2)));
            Assert.Equal(1.0, min(new Rest(obj2, obj1)));
            Assert.Equal(1.0, min(new Rest(obj1, 2.0)));
            Assert.Equal(1.0, min(new Rest(obj2, 1.0)));
        }

        [Fact]
        public void maxTest() {
            assertNegativeInfinity(max());

            assertPositiveZero(max(new Rest(0.0)));
            assertNegativeZero(max(new Rest(NEG_ZERO)));
            Assert.Equal(1.0, max(new Rest(1.0)));
            Assert.Equal(-1.0, max(new Rest(-1.0)));
            Assert.Equal(POS_INF, max(new Rest(POS_INF)));
            Assert.Equal(NEG_INF, max(new Rest(NEG_INF)));
            Assert.Equal(NAN, max(new Rest(NAN)));

            assertPositiveZero(max(new Rest(0.0, -1.0)));
            assertPositiveZero(max(new Rest(-1.0, 0.0)));
            assertPositiveZero(max(new Rest(0.0, 0.0)));
            assertPositiveZero(max(new Rest(-1.0, 0.0, -1.0, 0.0)));
            assertPositiveZero(max(new Rest(NEG_ZERO, 0.0)));
            assertPositiveZero(max(new Rest(0.0, 0.0, NEG_ZERO, 0.0)));
            assertPositiveZero(max(new Rest(-1.0, -1.0, NEG_ZERO, 0.0)));
            assertPositiveZero(max(new Rest(-1.0, NEG_ZERO, -1.0, 0.0)));
            assertPositiveZero(max(new Rest(-1.0, -1.0, 0.0, NEG_ZERO)));

            assertNegativeZero(max(new Rest(NEG_ZERO, -1.0)));
            assertNegativeZero(max(new Rest(-1.0, NEG_ZERO)));
            assertNegativeZero(max(new Rest(NEG_ZERO, NEG_ZERO)));
            assertNegativeZero(max(new Rest(-1.0, NEG_ZERO, -1.0, NEG_ZERO)));

            assertNaN(max(new Rest(NAN, 0.0)));
            assertNaN(max(new Rest(0.0, NAN)));
            assertNaN(max(new Rest(NAN, 1.0)));
            assertNaN(max(new Rest(NEG_INF, NAN)));
            assertNaN(max(new Rest(NAN, NEG_INF)));
            assertNaN(max(new Rest(NAN, NAN)));
            assertNaN(max(new Rest(0.0, 0.0, NAN, 0.0)));
            assertNaN(max(new Rest(NEG_INF, NEG_INF, NAN, NEG_INF)));
            assertNaN(max(new Rest(NAN, 0.0, 0.0, NEG_INF)));
            assertNaN(max(new Rest(NAN, 0.0, -1.0, -2.0)));

            assertNegativeInfinity(max(new Rest(NEG_INF, NEG_INF)));
            assertNegativeInfinity(max(new Rest(NEG_INF, NEG_INF, NEG_INF, NEG_INF)));

            assertPositiveInfinity(max(new Rest(POS_INF, 0.0)));
            assertPositiveInfinity(max(new Rest(0.0, POS_INF)));
            assertPositiveInfinity(max(new Rest(POS_INF, 1.0)));
            assertPositiveInfinity(max(new Rest(1.0, POS_INF)));
            assertPositiveInfinity(max(new Rest(POS_INF, -1.0)));
            assertPositiveInfinity(max(new Rest(-1.0, POS_INF)));
            assertPositiveInfinity(max(new Rest(NEG_INF, POS_INF)));
            assertPositiveInfinity(max(new Rest(POS_INF, POS_INF)));
            assertPositiveInfinity(max(new Rest(0.0, 0.0, POS_INF, 0.0)));
            assertPositiveInfinity(max(new Rest(POS_INF, POS_INF, POS_INF, POS_INF)));
            assertPositiveInfinity(max(new Rest(POS_INF, 0.0, 0.0, POS_INF)));
            assertPositiveInfinity(max(new Rest(POS_INF, 0.0, 0.0, NEG_INF)));
            assertPositiveInfinity(max(new Rest(POS_INF, 0.0, -1.0, -2.0)));

            Assert.Equal(2.0, max(new Rest(1.0, 2.0)));
            Assert.Equal(2.0, max(new Rest(2.0, 1.0)));
            Assert.Equal(1.0, max(new Rest(-1.0, 1.0)));
            Assert.Equal(1.0, max(new Rest(1.0, -1.0)));
            Assert.Equal(-1.0, max(new Rest(-1.0, -2.0)));
            Assert.Equal(-1.0, max(new Rest(-1.0, -2.0)));
            Assert.Equal(4.0, max(new Rest(1.0, 2.0, 3.0, 4.0)));
            Assert.Equal(4.0, max(new Rest(4.0, 3.0, 2.0, 1.0)));
            Assert.Equal(4.0, max(new Rest(3.0, 2.0, 4.0, 1.0)));

            Assert.Equal(1.0, max(new Rest(1.0, NEG_INF)));
            Assert.Equal(1.0, max(new Rest(NEG_INF, 1.0)));

            var obj1 = new SpyObjectWithConversions {numberValue = 1.0};
            var obj2 = new SpyObjectWithConversions {numberValue = 2.0};

            Assert.Equal(2.0, max(new Rest(obj1, obj2)));
            Assert.Equal(2.0, max(new Rest(obj2, obj1)));
            Assert.Equal(2.0, max(new Rest(obj1, 2.0)));
            Assert.Equal(2.0, max(new Rest(obj2, 1.0)));
        }

        [Fact]
        public void powTest() {
            assertNaN(pow(0.0, NAN));
            assertNaN(pow(1.0, NAN));
            assertNaN(pow(-1.0, NAN));
            assertNaN(pow(POS_INF, NAN));
            assertNaN(pow(NEG_INF, NAN));
            assertNaN(pow(NAN, NAN));

            Assert.Equal(1.0, pow(0.0, 0.0));
            Assert.Equal(1.0, pow(NEG_ZERO, 0.0));
            Assert.Equal(1.0, pow(1.0, 0.0));
            Assert.Equal(1.0, pow(-1.0, 0.0));
            Assert.Equal(1.0, pow(POS_INF, 0.0));
            Assert.Equal(1.0, pow(NEG_INF, 0.0));
            Assert.Equal(1.0, pow(NAN, 0.0));

            Assert.Equal(1.0, pow(0.0, NEG_ZERO));
            Assert.Equal(1.0, pow(NEG_ZERO, NEG_ZERO));
            Assert.Equal(1.0, pow(1.0, NEG_ZERO));
            Assert.Equal(1.0, pow(-1.0, NEG_ZERO));
            Assert.Equal(1.0, pow(POS_INF, NEG_ZERO));
            Assert.Equal(1.0, pow(NEG_INF, NEG_ZERO));
            Assert.Equal(1.0, pow(NAN, NEG_ZERO));

            assertNaN(pow(NAN, 1.0));
            assertNaN(pow(NAN, -1.0));
            assertNaN(pow(NAN, POS_EPSILON));
            assertNaN(pow(NAN, NEG_EPSILON));
            assertNaN(pow(NAN, POS_INF));
            assertNaN(pow(NAN, NEG_INF));

            assertPositiveInfinity(pow(Math.BitIncrement(1.0), POS_INF));
            assertPositiveInfinity(pow(POS_INF, POS_INF));
            assertPositiveInfinity(pow(-Math.BitIncrement(1.0), POS_INF));
            assertPositiveInfinity(pow(NEG_INF, POS_INF));

            assertPositiveZero(pow(Math.BitIncrement(1.0), NEG_INF));
            assertPositiveZero(pow(POS_INF, NEG_INF));
            assertPositiveZero(pow(-Math.BitIncrement(1.0), NEG_INF));
            assertPositiveZero(pow(NEG_INF, NEG_INF));

            assertNaN(pow(1.0, POS_INF));
            assertNaN(pow(1.0, NEG_INF));
            assertNaN(pow(-1.0, POS_INF));
            assertNaN(pow(-1.0, NEG_INF));

            assertPositiveZero(pow(Math.BitDecrement(1.0), POS_INF));
            assertPositiveZero(pow(POS_EPSILON, POS_INF));
            assertPositiveZero(pow(0.0, POS_INF));
            assertPositiveZero(pow(-Math.BitDecrement(1.0), POS_INF));
            assertPositiveZero(pow(NEG_EPSILON, POS_INF));
            assertPositiveZero(pow(NEG_ZERO, POS_INF));

            assertPositiveInfinity(pow(Math.BitDecrement(1.0), NEG_INF));
            assertPositiveInfinity(pow(POS_EPSILON, NEG_INF));
            assertPositiveInfinity(pow(0.0, NEG_INF));
            assertPositiveInfinity(pow(-Math.BitDecrement(1.0), NEG_INF));
            assertPositiveInfinity(pow(NEG_EPSILON, NEG_INF));
            assertPositiveInfinity(pow(NEG_ZERO, NEG_INF));

            assertPositiveInfinity(pow(POS_INF, POS_EPSILON));
            assertPositiveInfinity(pow(POS_INF, 1.0));

            assertPositiveZero(pow(POS_INF, NEG_EPSILON));
            assertPositiveZero(pow(POS_INF, -1.0));

            assertNegativeInfinity(pow(NEG_INF, 1.0));
            assertNegativeInfinity(pow(NEG_INF, 3.0));
            assertPositiveInfinity(pow(NEG_INF, 2.0));
            assertPositiveInfinity(pow(NEG_INF, 4.0));
            assertPositiveInfinity(pow(NEG_INF, 0.5));
            assertPositiveInfinity(pow(NEG_INF, 1.5));

            assertNegativeZero(pow(NEG_INF, -1.0));
            assertNegativeZero(pow(NEG_INF, -3.0));
            assertPositiveZero(pow(NEG_INF, -2.0));
            assertPositiveZero(pow(NEG_INF, -4.0));
            assertPositiveZero(pow(NEG_INF, -0.5));
            assertPositiveZero(pow(NEG_INF, -1.5));

            assertPositiveZero(pow(0.0, POS_EPSILON));
            assertPositiveZero(pow(0.0, 1.0));
            assertPositiveZero(pow(0.0, POS_INF));
            assertPositiveInfinity(pow(0.0, NEG_EPSILON));
            assertPositiveInfinity(pow(0.0, -1.0));
            assertPositiveInfinity(pow(0.0, NEG_INF));

            assertNegativeZero(pow(NEG_ZERO, 1.0));
            assertNegativeZero(pow(NEG_ZERO, 3.0));
            assertPositiveZero(pow(NEG_ZERO, 2.0));
            assertPositiveZero(pow(NEG_ZERO, 4.0));
            assertPositiveZero(pow(NEG_ZERO, 0.5));
            assertPositiveZero(pow(NEG_ZERO, 1.5));

            assertNegativeInfinity(pow(NEG_ZERO, -1.0));
            assertNegativeInfinity(pow(NEG_ZERO, -3.0));
            assertPositiveInfinity(pow(NEG_ZERO, -2.0));
            assertPositiveInfinity(pow(NEG_ZERO, -4.0));
            assertPositiveInfinity(pow(NEG_ZERO, -0.5));
            assertPositiveInfinity(pow(NEG_ZERO, -1.5));

            assertNaN(pow(NEG_EPSILON, POS_EPSILON));
            assertNaN(pow(NEG_EPSILON, 0.5));
            assertNaN(pow(NEG_EPSILON, 1.5));
            assertNaN(pow(-0.5, POS_EPSILON));
            assertNaN(pow(-0.5, 0.5));
            assertNaN(pow(-0.5, 1.5));
            assertNaN(pow(-1.5, POS_EPSILON));
            assertNaN(pow(-1.5, 0.5));
            assertNaN(pow(-1.5, 1.5));

            Assert.Equal(1.0, pow(1.0, 1.0));
            Assert.Equal(1.0, pow(1.0, -1.0));
            Assert.Equal(0.5, pow(0.5, 1.0));
            Assert.Equal(0.5, pow(2.0, -1.0));
            Assert.Equal(0.25, pow(0.5, 2.0));
            Assert.Equal(0.25, pow(2.0, -2.0));
            Assert.Equal(0.125, pow(0.5, 3.0));
            Assert.Equal(0.125, pow(2.0, -3.0));

            Assert.Equal(-1.0, pow(-1.0, 1.0));
            Assert.Equal(-1.0, pow(-1.0, -1.0));
            Assert.Equal(-0.5, pow(-0.5, 1.0));
            Assert.Equal(-0.5, pow(-2.0, -1.0));
            Assert.Equal(0.25, pow(-0.5, 2.0));
            Assert.Equal(0.25, pow(-2.0, -2.0));
            Assert.Equal(-0.125, pow(-0.5, 3.0));
            Assert.Equal(-0.125, pow(-2.0, -3.0));

            assertEpsilonEqual(SQRT2, pow(2.0, 0.5));
            assertEpsilonEqual(SQRT2, pow(0.5, -0.5));
            assertEpsilonEqual(SQRT1_2, pow(2.0, -0.5));
            assertEpsilonEqual(SQRT1_2, pow(0.5, 0.5));

            Assert.Equal(9007199254740992, pow(2, 53));
            Assert.Equal(8.98846567431158e+307, pow(2, 1023));
            Assert.Equal(-8.98846567431158e+307, pow(-2, 1023));

            Assert.Equal(POS_EPSILON, pow(2, -1074));
            Assert.Equal(POS_EPSILON, pow(0.5, 1074));
            Assert.Equal(POS_EPSILON, pow(-2, -1074));

            assertPositiveInfinity(pow(2, 1024));
            assertPositiveInfinity(pow(-2, 1024));
            assertNegativeInfinity(pow(-2, 1025));
        }

        [Fact]
        public void sinTest() {
            assertNaN(sin(NAN));
            assertNaN(sin(POS_INF));
            assertNaN(sin(NEG_INF));

            assertPositiveZero(sin(0.0));
            assertNegativeZero(sin(NEG_ZERO));

            assertEpsilonEqual(0.8414709848078965, sin(1.0));
            assertEpsilonEqual(-0.8414709848078965, sin(-1.0));
            assertEpsilonEqual(0.9092974268256817, sin(2.0));
            assertEpsilonEqual(-0.9092974268256817, sin(-2.0));

            assertEpsilonEqual(1.0, sin(PI / 2));
            assertEpsilonEqual(-1.0, sin(-PI / 2));
        }

        [Fact]
        public void randomTest() {
            double[] seq1 = new double[100];
            double[] seq2 = new double[100];

            void worker(object param) {
                double[] s = (double[])param;
                for (int i = 0; i < s.Length; i++)
                    s[i] = random();
            }

            Thread thread1 = new Thread(worker);
            Thread thread2 = new Thread(worker);
            thread1.Start(seq1);
            thread2.Start(seq2);
            thread1.Join();
            thread2.Join();

            for (int i = 0; i < seq1.Length; i++)
                Assert.True(seq1[i] >= 0 && seq1[i] < 1);

            for (int i = 0; i < seq2.Length; i++)
                Assert.True(seq2[i] >= 0 && seq2[i] < 1);

            Assert.NotEqual<double>(seq1, seq2);
        }

        [Fact]
        public void roundTest() {
            assertNaN(round(NAN));
            assertPositiveInfinity(round(POS_INF));
            assertNegativeInfinity(round(NEG_INF));

            assertPositiveZero(round(0.0));
            assertNegativeZero(round(-0.0));

            assertPositiveZero(round(POS_EPSILON));
            assertPositiveZero(round(0.25));
            assertPositiveZero(round(Math.BitDecrement(0.5)));
            Assert.Equal(1.0, round(0.5));
            Assert.Equal(1.0, round(Math.BitIncrement(0.5)));
            Assert.Equal(1.0, round(0.75));
            Assert.Equal(1.0, round(Math.BitDecrement(1.0)));

            assertNegativeZero(round(NEG_EPSILON));
            assertNegativeZero(round(-0.25));
            assertNegativeZero(round(-Math.BitDecrement(0.5)));
            assertNegativeZero(round(-0.5));
            Assert.Equal(-1.0, round(-Math.BitIncrement(0.5)));
            Assert.Equal(-1.0, round(-0.75));
            Assert.Equal(-1.0, round(-Math.BitDecrement(1.0)));

            Assert.Equal(1.0, round(1.0));
            Assert.Equal(1.0, round(Math.BitIncrement(1.0)));
            Assert.Equal(1.0, round(1.25));
            Assert.Equal(1.0, round(Math.BitDecrement(1.5)));
            Assert.Equal(2.0, round(1.5));
            Assert.Equal(2.0, round(Math.BitIncrement(1.5)));
            Assert.Equal(2.0, round(1.75));
            Assert.Equal(2.0, round(Math.BitDecrement(2.0)));
            Assert.Equal(2.0, round(2.0));
            Assert.Equal(3.0, round(2.5));
            Assert.Equal(3.0, round(Math.BitIncrement(2.5)));
            Assert.Equal(3.0, round(Math.BitDecrement(3.0)));
            Assert.Equal(3.0, round(3.0));

            Assert.Equal(-1.0, round(-1.0));
            Assert.Equal(-1.0, round(-Math.BitIncrement(1.0)));
            Assert.Equal(-1.0, round(-1.25));
            Assert.Equal(-1.0, round(-Math.BitDecrement(1.5)));
            Assert.Equal(-1.0, round(-1.5));
            Assert.Equal(-2.0, round(-Math.BitIncrement(1.5)));
            Assert.Equal(-2.0, round(-1.75));
            Assert.Equal(-2.0, round(-Math.BitDecrement(2.0)));
            Assert.Equal(-2.0, round(-2.0));
            Assert.Equal(-2.0, round(-2.5));
            Assert.Equal(-3.0, round(-Math.BitIncrement(2.5)));
            Assert.Equal(-3.0, round(-Math.BitDecrement(3.0)));
            Assert.Equal(-3.0, round(-3.0));

            Assert.Equal(pow(2, 52), round(pow(2, 52)));
            Assert.Equal(-pow(2, 52), round(-pow(2, 52)));
            Assert.Equal(pow(2, 53) - 1.0, round(pow(2, 53) - 1.0));
            Assert.Equal(-pow(2, 53) + 1.0, round(-pow(2, 53) + 1.0));
            Assert.Equal(pow(2, 53), round(pow(2, 53)));
            Assert.Equal(pow(2, 54), round(pow(2, 54)));
            Assert.Equal(-pow(2, 53), round(-pow(2, 53)));
            Assert.Equal(-pow(2, 54), round(-pow(2, 54)));
            Assert.Equal(Double.MaxValue, round(Double.MaxValue));
            Assert.Equal(-Double.MaxValue, round(-Double.MaxValue));
        }

        [Fact]
        public void sqrtTest() {
            assertNaN(sqrt(NAN));
            assertNaN(sqrt(NEG_EPSILON));
            assertNaN(sqrt(-1.0));
            assertNaN(sqrt(NEG_INF));

            assertPositiveZero(sqrt(0.0));
            assertNegativeZero(sqrt(NEG_ZERO));

            assertPositiveInfinity(sqrt(POS_INF));

            Assert.Equal(1.0, sqrt(1.0));
            Assert.Equal(SQRT2, sqrt(2.0));
            Assert.Equal(SQRT1_2, sqrt(0.5));

            Assert.Equal(2.0, sqrt(4.0));
            Assert.Equal(3.1622776601683795, sqrt(10.0));

            Assert.Equal(pow(2, 26), sqrt(pow(2, 52)));
            Assert.Equal(pow(2, 27), sqrt(pow(2, 54)));
            Assert.Equal(pow(2, 52), sqrt(pow(2, 104)));
            Assert.Equal(pow(2, 53), sqrt(pow(2, 106)));
            Assert.Equal(pow(2, 54), sqrt(pow(2, 108)));
            Assert.Equal(pow(2, 511), sqrt(pow(2, 1022)));

            Assert.Equal(1.3407807929942596e+154, sqrt(Double.MaxValue));
        }

        [Fact]
        public void tanTest() {
            assertNaN(tan(NAN));
            assertNaN(tan(POS_INF));
            assertNaN(tan(NEG_INF));

            assertPositiveZero(tan(0.0));
            assertNegativeZero(tan(NEG_ZERO));

            assertEpsilonEqual(1.5574077246549023, tan(1.0));
            assertEpsilonEqual(-1.5574077246549023, tan(-1.0));
            assertEpsilonEqual(-2.185039863261519, tan(2.0));
            assertEpsilonEqual(2.185039863261519, tan(-2.0));
        }

        [Fact]
        public void mathClass_shouldThrowOnInvokeOrConstruct() {
            var mathClass = Class.fromType<ASMath>();

            check(() => mathClass.invoke(default), ErrorCode.MATH_NOT_FUNCTION);
            check(() => mathClass.invoke(new ASAny[] {1}), ErrorCode.MATH_NOT_FUNCTION);

            check(() => mathClass.construct(default), ErrorCode.MATH_NOT_CONSTRUCTOR);
            check(() => mathClass.construct(new ASAny[] {1}), ErrorCode.MATH_NOT_CONSTRUCTOR);

            void check(Action func, ErrorCode code) {
                var exc = Assert.Throws<AVM2Exception>(func);
                Assert.Equal(code, (ErrorCode)((ASError)exc.thrownValue).errorID);
            }
        }

    }

}
