using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Mariana.Common.Tests {

    public class NamePoolTest {

        [Fact]
        public void constructor_shouldThrowWhenCapacityIsNegative() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NamePool(-1));
        }

        public static IEnumerable<object[]> getPooledValueTest_data = new (int, string[])[] {
            (0, new[] {"", "a", "b", "c"}),
            (5, new[] {"", "a", "b", "c", "abcd"}),
            (10, new[] {"", "a", "b", "c", "abcd"}),
            (8, Enumerable.Range(0, 1000).Select(x => new string('0', x)).ToArray()),
            (1000, Enumerable.Range(0, 1000).Select(x => new string('0', x)).ToArray()),
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(getPooledValueTest_data))]
        public void getPooledValueTest(int capacity, string[] strings) {
            var pool = new NamePool(capacity);

            var pooledStrings = new string[strings.Length];

            for (int i = 0; i < strings.Length; i++) {
                pooledStrings[i] = pool.getPooledValue(strings[i]);
                Assert.Equal(pooledStrings[i], strings[i]);
                Assert.Same(pooledStrings[i], pool.getPooledValue(strings[i]));
            }

            for (int i = 0; i < strings.Length; i++) {
                Assert.Same(pooledStrings[i], pool.getPooledValue(strings[i]));
                Assert.Same(pooledStrings[i], pool.getPooledValue(new string(strings[i].AsSpan())));
            }
        }

    }

}
