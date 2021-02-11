using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

namespace Mariana.Common.Tests {

    public class IndexedSetTest {

        private readonly struct Key : IEquatable<Key> {
            public readonly int v;
            public Key(int v) => this.v = v;

            public bool Equals(Key other) => v == other.v;
            public override int GetHashCode() => v & 31;
        }

        [Fact]
        public void constructorTest_invalidArguments() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new IndexedSet<Key>(-1));
        }

        [Fact]
        public void countTest_zeroWhenEmpty() {
            var set = new IndexedSet<Key>();
            Assert.Equal(0, set.count);

            set = new IndexedSet<Key>(1000);
            Assert.Equal(0, set.count);
        }

        public static IEnumerable<object[]> findAndAddTest_data() {
            var testCases1 = Enumerable.Range(0, 16);
            var testCases2 = Enumerable.Range(0, 16).Select(x => x * 16);

            var testCases3 = Enumerable.Range(0, 128);
            var testCases4 = Enumerable.Range(0, 128).Select(x => x * 32);

            var testCases5 = new HashSet<int>();
            var random = new Random(187531336);
            while (testCases5.Count < 100) {
                bool added = false;
                while (!added)
                    added = testCases5.Add(random.Next(0, 100));
            }

            var testCases6 = new[] {1438, 3935, 1470, 3937, 3957, 4851, -928, 7183, -844, 4126, 3979, -960, -992, 4883, 6004};

            yield return new object[] {testCases1};
            yield return new object[] {testCases2};
            yield return new object[] {testCases3};
            yield return new object[] {testCases4};
            yield return new object[] {testCases5};
            yield return new object[] {testCases6};
        }

        [Theory]
        [MemberData(nameof(findAndAddTest_data))]
        public void findAndAddTest(IEnumerable<int> keys) {
            var set = new IndexedSet<Key>();
            _doFindAndAddTest(set, keys, false);

            set = new IndexedSet<Key>();
            _doFindAndAddTest(set, keys, true);

            set = new IndexedSet<Key>(keys.Count());
            _doFindAndAddTest(set, keys, false);
        }

        [Fact]
        public void findAndAddTest_withCapacity() {
            var set = new IndexedSet<Key>(8);

            set.add(new Key(0));
            ref readonly var key0 = ref set[0];
            set.add(new Key(1));
            ref readonly var key1 = ref set[1];
            set.add(new Key(2));
            ref readonly var key2 = ref set[2];
            set.add(new Key(3));
            ref readonly var key3 = ref set[3];
            set.add(new Key(4));
            ref readonly var key4 = ref set[4];
            set.add(new Key(5));
            ref readonly var key5 = ref set[5];
            set.add(new Key(6));
            ref readonly var key6 = ref set[6];
            set.add(new Key(7));
            ref readonly var key7 = ref set[7];
            set.add(new Key(8));
            ref readonly var key8 = ref set[8];

            bool refsSame(in Key x, in Key y) => Unsafe.AreSame(ref Unsafe.AsRef(in x), ref Unsafe.AsRef(in y));

            Assert.True(refsSame(key0, set[0]));
            Assert.True(refsSame(key1, set[1]));
            Assert.True(refsSame(key2, set[2]));
            Assert.True(refsSame(key3, set[3]));
            Assert.True(refsSame(key4, set[4]));
            Assert.True(refsSame(key5, set[5]));
            Assert.True(refsSame(key6, set[6]));
            Assert.True(refsSame(key7, set[7]));
        }

        [Theory]
        [MemberData(nameof(findAndAddTest_data))]
        public void findAddAndClearTest(IEnumerable<int> keys) {
            var set = new IndexedSet<Key>();
            _doFindAndAddTest(set, keys, false);

            set.clear();
            _doFindAndAddTest(set, keys, false);
        }

        private void _doFindAndAddTest(IndexedSet<Key> set, IEnumerable<int> keys, bool useAdd = false) {
            int expectedIndex = 0;
            var expectedKeys = new List<int>();

            foreach (var k in keys) {
                Assert.Equal(-1, set.find(new Key(k)));

                if (useAdd)
                    Assert.True(set.add(new Key(k)));
                else
                    Assert.Equal(expectedIndex, set.findOrAdd(new Key(k)));

                expectedKeys.Add(k);
                Assert.Equal(expectedKeys.Count, set.count);

                for (int i = 0; i < expectedKeys.Count; i++) {
                    Assert.Equal(expectedKeys[i], set[i].v);
                    Assert.Equal(i, set.find(new Key(expectedKeys[i])));
                    Assert.Equal(i, set.findOrAdd(new Key(expectedKeys[i])));
                    Assert.False(set.add(new Key(expectedKeys[i])));
                }

                expectedIndex++;
            }
        }

        public static IEnumerable<object[]> copyToTest_data = new (IEnumerable<int>, IEnumerable<int>)[] {
            (Enumerable.Empty<int>(), null),
            (Enumerable.Range(0, 16), null),
            (Enumerable.Range(0, 200), null),
            (Enumerable.Range(0, 32).Select(x => x * 16), null),
            (Enumerable.Range(0, 200).Select(x => x * 32), null),
            (Enumerable.Repeat(Enumerable.Range(0, 50), 4).SelectMany(x => x), Enumerable.Range(0, 50))
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(copyToTest_data))]
        public void copyToAndToArrayTest(IEnumerable<int> keys, IEnumerable<int> expected) {
            expected = expected ?? keys;
            Key[] expectedArray = expected.Select(x => new Key(x)).ToArray();

            var set = new IndexedSet<Key>();
            foreach (var k in keys)
                set.add(new Key(k));

            Assert.Equal<Key>(expectedArray, set.toArray());

            Key[] valueArray = new Key[set.count];
            set.copyTo(valueArray);

            Assert.Equal<Key>(expectedArray, valueArray);
        }

        [Fact]
        public void copyToTest_invalidSpanLength() {
            var set = new IndexedSet<Key>();

            Key[] dstArray = new Key[4];
            Assert.Throws<ArgumentException>(() => set.copyTo(dstArray));

            set.add(new Key(1));
            set.add(new Key(2));

            Assert.Throws<ArgumentException>(() => set.copyTo(dstArray.AsSpan(0, 1)));
            Assert.Throws<ArgumentException>(() => set.copyTo(dstArray));
        }

        [Fact]
        public void indexerTest_outOfRange() {
            var set = new IndexedSet<Key>();

            Assert.Throws<ArgumentOutOfRangeException>(() => set[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => set[1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => set[-1]);

            set.add(new Key(1));
            set.add(new Key(2));

            Assert.Throws<ArgumentOutOfRangeException>(() => set[2]);
            Assert.Throws<ArgumentOutOfRangeException>(() => set[5]);
            Assert.Throws<ArgumentOutOfRangeException>(() => set[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => set[Int32.MinValue]);
            Assert.Throws<ArgumentOutOfRangeException>(() => set[Int32.MaxValue]);
        }

    }

}
