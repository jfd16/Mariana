using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Mariana.Common.Tests {

    public class StaticArrayPoolTest {

        private struct StructWithRef : IEquatable<StructWithRef> {
            public int p;
            public string q;

            public bool Equals(StructWithRef other) => p == other.p && q == other.q;
        }

        [Fact]
        public void constructor_shouldThrowWhenCapacityIsNegative() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StaticArrayPool<int>(-1));
        }

        [Fact]
        public void defaultTokenValue_shouldHaveIsDefaultTrue() {
            Assert.True(default(StaticArrayPoolToken<int>).isDefault);
        }

        [Fact]
        public void defaultToken_shouldGetEmptySpanAndZeroLength() {
            var pool = new StaticArrayPool<int>();
            Assert.Equal(0, pool.getSpan(default).Length);
            Assert.Equal(0, pool.getLength(default));
        }

        public static IEnumerable<object[]> allocateTest_data = new (int, int[])[] {
            (0, new[] {0, 0, 0}),
            (0, new[] {5}),
            (0, new[] {1, 2, 4, 8, 16, 32}),
            (0, new[] {3, 3, 3, 3, 3, 3, 3, 3, 3, 3}),
            (0, new[] {2, 2, 4, 3, 5, 1, 3, 6, 7}),
            (0, new[] {14, 0, 99, 4, 8, 0, 0, 0, 184, 84, 13, 5, 238, 1, 49}),

            (32, new[] {0, 0, 0}),
            (32, new[] {5}),
            (32, new[] {1, 2, 4, 8, 16, 32}),
            (32, new[] {3, 3, 3, 3, 3, 3, 3, 3, 3, 3}),
            (32, new[] {2, 2, 4, 3, 5, 1, 3, 6, 7}),
            (32, new[] {14, 0, 99, 4, 8, 0, 0, 0, 184, 84, 13, 5, 238, 1, 49}),

            (64, new[] {1, 2, 4, 8, 16, 32}),
            (64, new[] {14, 0, 99, 4, 8, 0, 0, 0, 184, 84, 13, 5, 238, 1, 49}),

            (500, new[] {14, 0, 99, 4, 8, 0, 0, 0, 184, 84, 13, 5, 238, 1, 49}),
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(allocateTest_data))]
        public void allocateTest(int capacity, int[] sizes) {
            var pool = new StaticArrayPool<int>(capacity);
            _doAllocateTest(pool, sizes);
        }

        private void _doAllocateTest(StaticArrayPool<int> pool, int[] sizes) {
            var tokens = new StaticArrayPoolToken<int>[sizes.Length];

            for (int i = 0; i < sizes.Length; i++) {
                tokens[i] = pool.allocate(sizes[i], out Span<int> newSpan);
                newSpan.Fill(i * i);

                Assert.True(newSpan == pool.getSpan(tokens[i]));
                Assert.Equal(sizes[i], pool.getLength(tokens[i]));

                for (int j = 0; j < i; j++) {
                    Assert.Equal(sizes[j], pool.getLength(tokens[j]));

                    var span = pool.getSpan(tokens[j]);
                    Assert.Equal<int>(Enumerable.Repeat(j * (i - 1), sizes[j]), span.ToArray());
                    span.Fill(j * i);
                }
            }
        }

        [Theory]
        [MemberData(nameof(allocateTest_data))]
        public void allocateTest_noSpanOutArgument(int capacity, int[] sizes) {
            var pool = new StaticArrayPool<int>(capacity);
            var tokens = new StaticArrayPoolToken<int>[sizes.Length];

            for (int i = 0; i < sizes.Length; i++) {
                tokens[i] = pool.allocate(sizes[i]);
                var newSpan = pool.getSpan(tokens[i]);

                Assert.Equal(sizes[i], pool.getLength(tokens[i]));
                newSpan.Fill(i * i);

                for (int j = 0; j < i; j++) {
                    Assert.Equal(sizes[j], pool.getLength(tokens[j]));

                    var span = pool.getSpan(tokens[j]);
                    Assert.Equal<int>(Enumerable.Repeat(j * (i - 1), sizes[j]), span.ToArray());
                    span.Fill(j * i);
                }
            }
        }

        [Theory]
        [InlineData(4)]
        [InlineData(128)]
        [InlineData(59481)]
        public void allocateTest_shouldAllocateInitialCapacity(int capacity) {
            var pool = new StaticArrayPool<int>(capacity);

            var token1 = pool.allocate(capacity / 3, out Span<int> span1);
            var token2 = pool.allocate(capacity / 3, out Span<int> span2);
            var token3 = pool.allocate(capacity - (span1.Length + span2.Length), out Span<int> span3);

            Assert.True(span1 == pool.getSpan(token1));
            Assert.True(span2 == pool.getSpan(token2));
            Assert.True(span3 == pool.getSpan(token3));
        }

        [Fact]
        public void allocateTest_shouldReturnDefaultTokenIfLengthZero() {
            var pool = new StaticArrayPool<int>();
            Assert.True(pool.allocate(0).isDefault);
            Assert.True(pool.allocate(0, out _).isDefault);
        }

        [Fact]
        public void allocateTest_shouldThrowIfLengthNegative() {
            var pool = new StaticArrayPool<int>();
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.allocate(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.allocate(-1, out _));
        }

        [Fact]
        public void allocateTest_shouldClearIfTypeContainsRefs() {
            var stringPool = new StaticArrayPool<string>();
            var structPool = new StaticArrayPool<StructWithRef>();

            stringPool.allocate(10, out Span<string> stringSpan);
            Assert.Equal<string>(Enumerable.Repeat<string>(null, 10), stringSpan.ToArray());

            structPool.allocate(10, out Span<StructWithRef> structSpan);
            Assert.Equal<StructWithRef>(Enumerable.Repeat<StructWithRef>(default, 10), structSpan.ToArray());
        }

        [Fact]
        public void clearTest_shouldReuseMemory() {
            var pool = new StaticArrayPool<int>();

            pool.allocate(10, out Span<int> span1);
            pool.clear();
            pool.allocate(10, out Span<int> span2);

            Assert.True(span1 == span2);
        }

        [Fact]
        public void clearTest_shouldClearValuesIfTypeContainsRefs() {
            var stringPool = new StaticArrayPool<string>();
            var structPool = new StaticArrayPool<StructWithRef>();

            stringPool.allocate(10, out Span<string> stringSpan);
            structPool.allocate(10, out Span<StructWithRef> structSpan);

            for (int i = 0; i < 10; i++) {
                stringSpan[i] = "A";
                structSpan[i] = new StructWithRef {p = 1, q = "A"};
            }

            stringPool.clear();
            Assert.Equal<string>(Enumerable.Repeat<string>(null, 10), stringSpan.ToArray());

            structPool.clear();
            Assert.Equal<StructWithRef>(Enumerable.Repeat<StructWithRef>(default, 10), structSpan.ToArray());
        }

        [Theory]
        [InlineData(new int[] {4, 3, 5}, new int[] {2}, new int[] {10, 20, 40}, new int[] {15, 15, 15})]
        [InlineData(new int[] {1}, new int[] {4, 16}, new int[] {64}, new int[] {16, 16, 16, 16}, new int[] {5, 10})]
        public void allocateAndClearTest(params int[][] sizes) {
            var pool = new StaticArrayPool<int>();
            for (int i = 0; i < sizes.Length; i++) {
                pool.clear();
                _doAllocateTest(pool, sizes[i]);
            }
        }

        [Fact]
        public void arrayToStringTest() {
            var pool = new StaticArrayPool<int>();

            var token1 = pool.allocate(5, out Span<int> span1);
            for (int i = 0; i < span1.Length; i++)
                span1[i] = i;

            var token2 = pool.allocate(15, out Span<int> span2);
            for (int i = 0; i < span2.Length; i++)
                span2[i] = 100 + i;

            Assert.Equal("[]", pool.arrayToString(default));
            Assert.Equal("[0, 1, 2, 3, 4]", pool.arrayToString(token1));
            Assert.Equal("[" + String.Join(", ", Enumerable.Range(100, 15).Select(x => x.ToString())) + "]", pool.arrayToString(token2));
        }

    }

}
