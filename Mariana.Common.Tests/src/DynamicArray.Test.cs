using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Mariana.Common.Tests {

    public class DynamicArrayTest {

        private struct StructWithRef : IEquatable<StructWithRef> {
            public int p;
            public string q;
            public bool Equals(StructWithRef other) => p == other.p && q == other.q;
        }

        private void _checkCapacity(ref DynamicArray<int> arr, int expectedCapacity, bool clearAtEnd = true) {
            if (arr.length == 0)
                arr.add(-1);

            Assert.True(arr.length <= expectedCapacity);

            ref int firstElem = ref arr.asSpan()[0];
            for (int i = arr.length; i < expectedCapacity; i++) {
                arr.add(-1);
                // Assert that no reallocation has happened.
                Assert.True(Unsafe.AreSame(ref firstElem, ref arr.asSpan()[0]));
            }

            if (clearAtEnd)
                arr.clear();
        }

        [Fact]
        public void defaultValue_shouldBeEmptyArray() {
            DynamicArray<int> x = default;
            Assert.Equal(0, x.length);
            Assert.Equal(0, x.asSpan().Length);
        }

        [Fact]
        public void addTest() {
            DynamicArray<int> x = default;

            x.add(1);
            Assert.Equal(1, x.length);
            Assert.Equal<int>(new[] {1}, x.asSpan().ToArray());

            x.add(2);
            Assert.Equal(2, x.length);
            Assert.Equal<int>(new[] {1, 2}, x.asSpan().ToArray());

            x.add(3);
            x.add(4);
            x.add(5);
            x.add(6);
            Assert.Equal(6, x.length);
            Assert.Equal<int>(new[] {1, 2, 3, 4, 5, 6}, x.asSpan().ToArray());

            x = new DynamicArray<int>(new int[4], 2);
            x.add(2);
            x.add(2);
            Assert.Equal(4, x.length);
            Assert.Equal<int>(new[] {0, 0, 2, 2}, x.asSpan().ToArray());
            x.add(4);
            x.add(4);
            Assert.Equal(6, x.length);
            Assert.Equal<int>(new[] {0, 0, 2, 2, 4, 4}, x.asSpan().ToArray());
        }

        [Fact]
        public void addTest_withRef() {
            DynamicArray<int> x = default;
            int one = 1, two = 2;

            x.add(in one);
            Assert.Equal(1, x.length);
            Assert.Equal<int>(new[] {1}, x.asSpan().ToArray());

            x.add(in one);
            Assert.Equal(2, x.length);
            Assert.Equal<int>(new[] {1, 1}, x.asSpan().ToArray());

            x.add(in two);
            x.add(in two);
            x.add(in one);
            x.add(in one);
            Assert.Equal(6, x.length);
            Assert.Equal<int>(new[] {1, 1, 2, 2, 1, 1}, x.asSpan().ToArray());

            x = new DynamicArray<int>(new int[4], 2);
            x.add(in one);
            x.add(in one);
            Assert.Equal(4, x.length);
            Assert.Equal<int>(new[] {0, 0, 1, 1}, x.asSpan().ToArray());
            x.add(in two);
            x.add(in two);
            Assert.Equal(6, x.length);
            Assert.Equal<int>(new[] {0, 0, 1, 1, 2, 2}, x.asSpan().ToArray());
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(0, true)]
        [InlineData(1, false)]
        [InlineData(1, true)]
        [InlineData(4, false)]
        [InlineData(4, true)]
        [InlineData(50, false)]
        [InlineData(50, true)]
        public void constructorTest_withInitialCapacity(int initialCapacity, bool fillWithDefault) {
            var arr = new DynamicArray<int>(initialCapacity, fillWithDefault);

            if (initialCapacity == 0) {
                Assert.Equal(0, arr.length);
                Assert.Equal(0, arr.asSpan().Length);
            }
            else if (fillWithDefault) {
                Assert.Equal(initialCapacity, arr.length);
                Assert.Equal<int>(Enumerable.Repeat(0, initialCapacity), arr.asSpan().ToArray());
            }
            else {
                _checkCapacity(ref arr, initialCapacity);
            }
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 0)]
        [InlineData(1, 1)]
        [InlineData(4, 0)]
        [InlineData(4, 2)]
        [InlineData(4, 4)]
        [InlineData(100, 50)]
        [InlineData(100, 100)]
        public void constructorTest_withExistingArray(int capacity, int length) {
            var store = new int[capacity];
            var arr = new DynamicArray<int>(store, length);

            Assert.True(arr.asSpan() == store.AsSpan(0, length));

            for (int i = length; i < capacity; i++)
                arr.add(i);

            Assert.True(arr.asSpan() == store.AsSpan());
        }

        [Fact]
        public void constructorTest_withExistingArray_shouldClearExcessReferences() {
            var strArray = new string[] {"A", "B", "C", "D", "E", "F"};
            var valArray = new StructWithRef[] {
                new StructWithRef {p = 1, q = "A"},
                new StructWithRef {p = 2, q = "B"},
                new StructWithRef {p = 3, q = "C"},
            };

            new DynamicArray<string>(strArray, 6);
            Assert.Equal<string>(new string[] {"A", "B", "C", "D", "E", "F"}, strArray);

            new DynamicArray<string>(strArray, 4);
            Assert.Equal<string>(new string[] {"A", "B", "C", "D", null, null}, strArray);

            new DynamicArray<string>(strArray, 0);
            Assert.Equal<string>(Enumerable.Repeat<string>(null, 6), strArray);

            new DynamicArray<StructWithRef>(valArray, 3);
            Assert.Equal<StructWithRef>(
                new[] {
                    new StructWithRef {p = 1, q = "A"},
                    new StructWithRef {p = 2, q = "B"},
                    new StructWithRef {p = 3, q = "C"},
                },
                valArray
            );

            new DynamicArray<StructWithRef>(valArray, 1);
            Assert.Equal<StructWithRef>(new[] {new StructWithRef {p = 1, q = "A"}, default, default}, valArray);

            new DynamicArray<StructWithRef>(valArray, 0);
            Assert.Equal<StructWithRef>(new StructWithRef[] {default, default, default}, valArray);
        }

        [Fact]
        public void constructorTest_invalidArguments() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DynamicArray<int>(-1, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DynamicArray<int>(-1, true));
            Assert.Throws<ArgumentNullException>(() => new DynamicArray<int>(null, 0));
            Assert.Throws<ArgumentNullException>(() => new DynamicArray<int>(null, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DynamicArray<int>(new int[0], -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DynamicArray<int>(new int[0], 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DynamicArray<int>(new int[10], -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DynamicArray<int>(new int[10], 11));
        }

        [Fact]
        public void clearTest() {
            DynamicArray<int> arr = default;

            arr.clear();
            Assert.Equal(0, arr.length);
            Assert.Equal(0, arr.asSpan().Length);

            arr.add(1);
            arr.add(2);
            arr.add(3);
            arr.add(4);
            arr.add(5);

            Span<int> span = arr.asSpan();

            arr.clear();
            Assert.Equal(0, arr.length);
            Assert.Equal(0, arr.asSpan().Length);

            arr.clear();
            Assert.Equal(0, arr.length);
            Assert.Equal(0, arr.asSpan().Length);

            arr.add(1);
            arr.add(2);
            arr.add(3);
            arr.add(4);
            arr.add(5);
            Assert.True(span == arr.asSpan());

            int[] store = new int[10];
            arr = new DynamicArray<int>(store, 4);

            arr.clear();
            arr.add(1);
            arr.add(1);
            arr.add(1);
            arr.add(1);
            Assert.True(arr.asSpan() == store.AsSpan(0, 4));

            // clear should not reduce the capacity
            arr = new DynamicArray<int>(30);
            for (int i = 0; i < 30; i++)
                arr.add(i);

            arr.clear();
            _checkCapacity(ref arr, 30);
        }

        [Fact]
        public void clearTest_clearMemoryWithReferences() {
            DynamicArray<string> stringArr = new DynamicArray<string>();
            stringArr.add("1");
            stringArr.add("2");
            stringArr.add("3");
            stringArr.add("4");
            stringArr.add("5");

            var stringSpan = stringArr.asSpan();
            stringArr.clear();
            Assert.Equal<string>(Enumerable.Repeat<string>(null, 5), stringSpan.ToArray());

            DynamicArray<StructWithRef> valueArr = new DynamicArray<StructWithRef>();
            valueArr.add(new StructWithRef {p = 1, q = "1"});
            valueArr.add(new StructWithRef {p = 2, q = "2"});
            valueArr.add(new StructWithRef {p = 3, q = "3"});
            valueArr.add(new StructWithRef {p = 4, q = "4"});
            valueArr.add(new StructWithRef {p = 5, q = "5"});

            var valueSpan = valueArr.asSpan();
            valueArr.clear();
            Assert.Equal(Enumerable.Repeat(default(StructWithRef), 5), valueSpan.ToArray(), EqualityComparer<StructWithRef>.Default);
        }

        [Fact]
        public void getUnderlyingArrayTest() {
            DynamicArray<int> arr = default;
            Assert.Empty(arr.getUnderlyingArray());

            arr.add(1);
            arr.add(2);
            arr.add(3);
            arr.add(4);
            Assert.True(arr.asSpan() == arr.getUnderlyingArray().AsSpan(0, 4));

            int[] store = new int[10];
            arr = new DynamicArray<int>(store, 0);
            Assert.Same(store, arr.getUnderlyingArray());

            for (int i = 0; i < 10; i++)
                arr.add(i);

            Assert.Same(store, arr.getUnderlyingArray());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(12)]
        [InlineData(1, 1, 5)]
        [InlineData(1, 5, 1)]
        [InlineData(3, 3, 6, 11, 23)]
        [InlineData(20, 10, 10, 20, 20, 40, 40)]
        public void addDefaultTest(params int[] counts) {
            var intArray = new DynamicArray<int>();
            var strArray = new DynamicArray<string>();
            var valArray = new DynamicArray<StructWithRef>();

            int totalCount = 0;

            for (int i = 0; i < counts.Length; i++) {
                var intSpan = intArray.addDefault(counts[i]);
                var strSpan = strArray.addDefault(counts[i]);
                var valSpan = valArray.addDefault(counts[i]);

                totalCount += counts[i];

                Assert.Equal(totalCount, intArray.length);
                Assert.Equal(totalCount, strArray.length);
                Assert.Equal(totalCount, valArray.length);

                Assert.True(intSpan == intArray.asSpan().Slice(totalCount - counts[i]));
                Assert.True(strSpan == strArray.asSpan().Slice(totalCount - counts[i]));
                Assert.True(valSpan == valArray.asSpan().Slice(totalCount - counts[i]));

                Assert.Equal<int>(Enumerable.Repeat(0, counts[i]), intSpan.ToArray());
                Assert.Equal<string>(Enumerable.Repeat<string>(null, counts[i]), strSpan.ToArray());
                Assert.Equal(Enumerable.Repeat(default(StructWithRef), counts[i]), valSpan.ToArray(), EqualityComparer<StructWithRef>.Default);
            }
        }

        [Fact]
        public void addDefaultTest_invalidCount() {
            var intArray = new DynamicArray<int>();
            Assert.Throws<ArgumentOutOfRangeException>(() => intArray.addDefault(-1));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(12)]
        [InlineData(1, 1, 5)]
        [InlineData(1, 5, 1)]
        [InlineData(3, 3, 6, 11, 23)]
        [InlineData(20, 10, 10, 20, 20, 40, 40)]
        public void addUninitializedTest(params int[] counts) {
            var intArray = new DynamicArray<int>();
            var strArray = new DynamicArray<string>();
            var valArray = new DynamicArray<StructWithRef>();

            int totalCount = 0;

            for (int i = 0; i < counts.Length; i++) {
                var intSpan = intArray.addUninitialized(counts[i]);
                var strSpan = strArray.addUninitialized(counts[i]);
                var valSpan = valArray.addUninitialized(counts[i]);

                totalCount += counts[i];

                Assert.Equal(totalCount, intArray.length);
                Assert.Equal(totalCount, strArray.length);
                Assert.Equal(totalCount, valArray.length);

                Assert.True(intSpan == intArray.asSpan().Slice(totalCount - counts[i]));
                Assert.True(strSpan == strArray.asSpan().Slice(totalCount - counts[i]));
                Assert.True(valSpan == valArray.asSpan().Slice(totalCount - counts[i]));

                Assert.Equal<string>(Enumerable.Repeat<string>(null, counts[i]), strSpan.ToArray());
                Assert.Equal(Enumerable.Repeat(default(StructWithRef), counts[i]), valSpan.ToArray(), EqualityComparer<StructWithRef>.Default);
            }
        }

        [Fact]
        public void addUninitializedTest_invalidCount() {
            var intArray = new DynamicArray<int>();
            Assert.Throws<ArgumentOutOfRangeException>(() => intArray.addUninitialized(-1));
        }

        [Fact]
        public void indexerTest() {
            var arr = new DynamicArray<int>();

            arr.add(1);
            arr.add(2);
            arr.add(4);

            for (int i = 0; i < arr.length; i++)
                Assert.True(Unsafe.AreSame(ref arr[i], ref arr.asSpan()[i]));

            for (int i = arr.length; i < 60; i++) {
                arr.add(i);
                Assert.True(Unsafe.AreSame(ref arr[i], ref arr.asSpan()[i]));
            }

            arr.clear();
            arr.addDefault(6);

            for (int i = 0; i < arr.length; i++)
                Assert.True(Unsafe.AreSame(ref arr[i], ref arr.asSpan()[i]));
        }

        [Fact]
        public void setCapacityTest() {
            var arr = new DynamicArray<int>();

            arr.setCapacity(10);
            _checkCapacity(ref arr, 10);

            arr.setCapacity(30);
            _checkCapacity(ref arr, 30);

            arr.addDefault(10);
            arr.clear();

            arr.setCapacity(20);
            _checkCapacity(ref arr, 20);

            arr.setCapacity(20);
            _checkCapacity(ref arr, 20);

            arr.setCapacity(0);
            arr.add(0);
            arr.add(0);
            arr.add(0);
            arr.setCapacity(15);
            _checkCapacity(ref arr, 15);
        }

        [Fact]
        public void setCapacityTest_invalidArguments() {
            var arr = new DynamicArray<int>();
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.setCapacity(-1));

            arr.addDefault(10);
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.setCapacity(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.setCapacity(9));
        }

        [Fact]
        public void ensureCapacityTest() {
            var arr = new DynamicArray<int>();

            arr.ensureCapacity(10);
            _checkCapacity(ref arr, 10);

            arr.ensureCapacity(30);
            _checkCapacity(ref arr, 30);

            arr.ensureCapacity(20);
            _checkCapacity(ref arr, 30);

            arr.ensureCapacity(0);
            _checkCapacity(ref arr, 30);

            arr.ensureCapacity(40);
            _checkCapacity(ref arr, 40);

            arr.ensureCapacity(40);
            _checkCapacity(ref arr, 40);

            arr.ensureCapacity(39);
            _checkCapacity(ref arr, 40);

            arr.addDefault(10);
            arr.ensureCapacity(5);
            Assert.Equal(10, arr.length);
            _checkCapacity(ref arr, 40);
        }

        [Fact]
        public void ensureCapacityTest_invalidArguments() {
            var arr = new DynamicArray<int>();
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.ensureCapacity(-1));

            arr.addDefault(10);
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.ensureCapacity(-2));
        }

        [Fact]
        public void asSpanTest() {
            var arr = new DynamicArray<int>(30);
            arr.addDefault(30);

            Assert.True(arr.asSpan(0, 30) == arr.asSpan());
            Assert.True(arr.asSpan(1, 29) == arr.asSpan().Slice(1, 29));
            Assert.True(arr.asSpan(2, 0) == arr.asSpan().Slice(2, 0));
            Assert.True(arr.asSpan(5, 5) == arr.asSpan().Slice(5, 5));
        }

        [Fact]
        public void asReadOnlyArrayViewTest() {
            var arr = new DynamicArray<int>(30);
            Assert.Equal(0, arr.asReadOnlyArrayView().length);

            arr.addDefault(30);
            Assert.True(arr.asReadOnlyArrayView().asSpan() == arr.asSpan());

            arr.clear();
            arr.setCapacity(100);
            Assert.Equal(0, arr.asReadOnlyArrayView().length);

            arr.addDefault(50);
            Assert.True(arr.asReadOnlyArrayView().asSpan() == arr.asSpan());
        }

        [Fact]
        public void toArrayTest() {
            var arr = new DynamicArray<int>();
            Assert.Empty(arr.toArray());
            Assert.Empty(arr.toArray(true));

            for (int i = 0; i < 20; i++)
                arr.add(i);

            Assert.Equal<int>(arr.asSpan().ToArray(), arr.toArray());
            Assert.Equal<int>(arr.asSpan().ToArray(), arr.toArray(true));

            arr.clear();
            Assert.Empty(arr.toArray());
            Assert.Empty(arr.toArray(true));
        }

        [Fact]
        public void toArrayTest_createCopy() {
            int[] store = new int[20];
            for (int i = 0; i < 20; i++)
                store[i] = i;

            var arr = new DynamicArray<int>(store, 20);
            Assert.Equal<int>(store, arr.toArray());
            Assert.Equal<int>(store, arr.toArray(true));
            Assert.NotSame(store, arr.toArray(true));
        }

        [Fact]
        public void removeLastTest() {
            var intArr = new DynamicArray<int>();

            for (int i = 0; i < 10; i++)
                intArr.add(i);

            Span<int> intArrSpan = intArr.asSpan();

            intArr.removeLast();
            Assert.Equal(9, intArr.length);
            Assert.Equal<int>(Enumerable.Range(0, 9), intArr.asSpan().ToArray());

            intArr.removeLast();
            intArr.removeLast();
            Assert.Equal(7, intArr.length);
            Assert.Equal<int>(Enumerable.Range(0, 7), intArr.asSpan().ToArray());

            intArr.removeLast();
            intArr.removeLast();
            intArr.removeLast();
            intArr.removeLast();
            intArr.removeLast();
            intArr.removeLast();
            intArr.removeLast();
            Assert.Equal(0, intArr.length);

            for (int i = 0; i < 10; i++)
                intArr.add(i);

            Assert.True(intArrSpan == intArr.asSpan());
        }

        [Fact]
        public void removeLastTest_whenEmpty() {
            var intArr = new DynamicArray<int>();
            Assert.Throws<InvalidOperationException>(() => intArr.removeLast());

            intArr.add(1);
            intArr.add(2);
            intArr.removeLast();
            intArr.removeLast();
            Assert.Throws<InvalidOperationException>(() => intArr.removeLast());
        }

        [Fact]
        public void removeLastTest_shouldClearReferences() {
            var strArr = new DynamicArray<string>();
            strArr.add("A");
            strArr.add("B");
            strArr.add("C");
            var strArrSpan = strArr.asSpan();
            strArr.removeLast();
            strArr.removeLast();
            Assert.Equal<string>(new string[] {"A", null, null}, strArrSpan.ToArray());

            var valArr = new DynamicArray<StructWithRef>();
            valArr.add(new StructWithRef {q = "A"});
            valArr.add(new StructWithRef {q = "B"});
            valArr.add(new StructWithRef {q = "C"});
            var valArrSpan = valArr.asSpan();
            valArr.removeLast();
            valArr.removeLast();
            Assert.Equal(default(StructWithRef), valArrSpan[1]);
            Assert.Equal(default(StructWithRef), valArrSpan[2]);
        }

        [Fact]
        public void removeRangeTest() {
            var arr = new DynamicArray<int>();

            arr.removeRange(0, 0);
            Assert.Equal(0, arr.length);
            Assert.Equal(0, arr.asSpan().Length);

            for (int i = 0; i < 20; i++)
                arr.add(i);

            var originalSpan = arr.asSpan();

            arr.removeRange(10, 0);
            arr.removeRange(20, 0);
            Assert.Equal(20, arr.length);
            Assert.Equal<int>(Enumerable.Range(0, 20), arr.asSpan().ToArray());

            arr.removeRange(18, 2);
            arr.removeRange(17, 1);
            Assert.Equal(17, arr.length);
            Assert.Equal<int>(Enumerable.Range(0, 17), arr.asSpan().ToArray());

            Assert.True(Unsafe.AreSame(ref arr.asSpan()[0], ref originalSpan[0]));

            arr.removeRange(0, 5);
            Assert.Equal(12, arr.length);
            Assert.Equal<int>(Enumerable.Range(5, 12), arr.asSpan().ToArray());

            arr.add(10000);
            arr.add(20000);
            arr.add(30000);
            arr.removeRange(5, 8);

            Assert.Equal(7, arr.length);
            Assert.Equal<int>(new[] {5, 6, 7, 8, 9, 20000, 30000}, arr.asSpan().ToArray());

            Assert.True(Unsafe.AreSame(ref arr.asSpan()[0], ref originalSpan[0]));

            arr.removeRange(0, 7);
            Assert.Equal(0, arr.length);
            Assert.Equal(0, arr.asSpan().Length);

            arr.addDefault(5);
            Assert.True(Unsafe.AreSame(ref arr.asSpan()[0], ref originalSpan[0]));
        }

        [Fact]
        public void removeRangeTest_invalidArguments() {
            var arr = new DynamicArray<int>();

            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(-1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(-1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(1, 1));

            for (int i = 0; i < 10; i++)
                arr.add(i);

            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(-1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(-1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(10, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(11, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(11, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(9, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(0, 11));
            Assert.Throws<ArgumentOutOfRangeException>(() => arr.removeRange(-1, 12));
        }

        [Fact]
        public void removeRangeTest_shouldClearReferences() {
            var strArr = new DynamicArray<string>();
            var valArr = new DynamicArray<StructWithRef>();

            foreach (var s in new[] {"A", "B", "C", "D", "E", "F", "G", "H", "I"}) {
                strArr.add(s);
                valArr.add(new StructWithRef {p = s[0] - 'A', q = s});
            }

            var strSpan = strArr.asSpan();
            var valSpan = valArr.asSpan();

            strArr.removeRange(7, 2);
            strArr.removeRange(0, 3);
            strArr.removeRange(1, 2);

            Assert.Equal<string>(new[] {"D", "G", null, null, null, null, null, null, null}, strSpan.ToArray());

            valArr.removeRange(8, 1);
            valArr.removeRange(0, 2);
            valArr.removeRange(2, 3);

            Assert.Equal<int>(new[] {2, 3, 7, 0, 0, 0, 0, 0, 0}, valSpan.ToArray().Select(x => x.p));
            Assert.Equal<string>(new[] {"C", "D", "H", null, null, null, null, null, null}, valSpan.ToArray().Select(x => x.q));
        }

        [Fact]
        public void clearAndAddDefaultTest() {
            var intArr = new DynamicArray<int>();

            intArr.clearAndAddDefault(5);
            Assert.Equal(5, intArr.length);
            Assert.Equal<int>(Enumerable.Repeat(0, 5), intArr.asSpan().ToArray());

            intArr.clearAndAddDefault(50);
            Assert.Equal(50, intArr.length);
            Assert.Equal<int>(Enumerable.Repeat(0, 50), intArr.asSpan().ToArray());

            for (int i = 0; i < 10; i++)
                intArr.add(i);

            var span = intArr.asSpan();

            intArr.clearAndAddDefault(0);
            Assert.Equal(0, intArr.length);
            Assert.Equal(0, intArr.asSpan().Length);

            intArr.clearAndAddDefault(8);
            Assert.Equal(8, intArr.length);
            Assert.Equal<int>(Enumerable.Repeat(0, 8), intArr.asSpan().ToArray());

            Assert.True(Unsafe.AreSame(ref span[0], ref intArr.asSpan()[0]));
            _checkCapacity(ref intArr, 60, clearAtEnd: false);

            intArr.clearAndAddDefault(60);
            Assert.Equal(60, intArr.length);
            Assert.Equal<int>(Enumerable.Repeat(0, 60), intArr.asSpan().ToArray());
            Assert.True(Unsafe.AreSame(ref span[0], ref intArr.asSpan()[0]));

            intArr = new DynamicArray<int>(Enumerable.Repeat(-1, 10).ToArray(), 5);
            span = intArr.asSpan();
            intArr.clearAndAddDefault(9);
            Assert.Equal(9, intArr.length);
            Assert.Equal<int>(Enumerable.Repeat(0, 9), intArr.asSpan().ToArray());
            Assert.True(Unsafe.AreSame(ref span[0], ref intArr.asSpan()[0]));
        }

        [Fact]
        public void clearAndAddDefaultTest_invalidCount() {
            var intArray = new DynamicArray<int>();
            Assert.Throws<ArgumentOutOfRangeException>(() => intArray.clearAndAddDefault(-1));
        }

        [Fact]
        public void clearAndAddUninitializedTest() {
            var intArr = new DynamicArray<int>();

            intArr.clearAndAddUninitialized(5);
            Assert.Equal(5, intArr.length);

            intArr.clearAndAddUninitialized(50);
            Assert.Equal(50, intArr.length);

            for (int i = 0; i < 10; i++)
                intArr.add(i);

            var span = intArr.asSpan();

            intArr.clearAndAddUninitialized(0);
            Assert.Equal(0, intArr.length);
            Assert.Equal(0, intArr.asSpan().Length);

            intArr.clearAndAddUninitialized(8);
            Assert.Equal(8, intArr.length);

            Assert.True(Unsafe.AreSame(ref span[0], ref intArr.asSpan()[0]));
            _checkCapacity(ref intArr, 60, clearAtEnd: false);

            intArr.clearAndAddUninitialized(60);
            Assert.Equal(60, intArr.length);
            Assert.True(Unsafe.AreSame(ref span[0], ref intArr.asSpan()[0]));

            intArr = new DynamicArray<int>(Enumerable.Repeat(-1, 10).ToArray(), 5);
            span = intArr.asSpan();
            intArr.clearAndAddUninitialized(9);
            Assert.True(Unsafe.AreSame(ref span[0], ref intArr.asSpan()[0]));
        }

        [Fact]
        public void clearAndAddUninitializedTest_invalidCount() {
            var intArray = new DynamicArray<int>();
            Assert.Throws<ArgumentOutOfRangeException>(() => intArray.clearAndAddUninitialized(-1));
        }

        [Fact]
        public void clearAndAddUninitializedTest_shouldClearReferences() {
            var strArr = new DynamicArray<string>();
            var valArr = new DynamicArray<StructWithRef>();

            strArr.clearAndAddUninitialized(10);
            valArr.clearAndAddUninitialized(10);
            Assert.Equal<string>(Enumerable.Repeat<string>(null, 10), strArr.asSpan().ToArray());
            Assert.Equal<StructWithRef>(Enumerable.Repeat<StructWithRef>(default, 10), valArr.asSpan().ToArray());

            for (int i = 0; i < 10; i++) {
                strArr[i] = new string((char)('A' + i), 1);
                valArr[i] = new StructWithRef {p = i, q = strArr[i]};
            }

            var strSpan = strArr.asSpan();
            var valSpan = valArr.asSpan();

            strArr.clearAndAddUninitialized(0);
            valArr.clearAndAddUninitialized(0);
            Assert.Equal(0, strArr.asSpan().Length);
            Assert.Equal(0, valArr.asSpan().Length);
            Assert.Equal<string>(Enumerable.Repeat<string>(null, 10), strSpan.ToArray());
            Assert.Equal<StructWithRef>(Enumerable.Repeat<StructWithRef>(default, 10), valSpan.ToArray());

            for (int i = 0; i < 10; i++) {
                strArr.add(new string((char)('A' + i), 1));
                valArr.add(new StructWithRef {p = i, q = strArr[i]});
            }

            strArr.clearAndAddUninitialized(6);
            valArr.clearAndAddUninitialized(6);
            Assert.Equal<string>(Enumerable.Repeat<string>(null, 6), strArr.asSpan().ToArray());
            Assert.Equal<StructWithRef>(Enumerable.Repeat<StructWithRef>(default, 6), valArr.asSpan().ToArray());
            Assert.Equal<string>(Enumerable.Repeat<string>(null, 10), strSpan.ToArray());
            Assert.Equal<StructWithRef>(Enumerable.Repeat<StructWithRef>(default, 10), valSpan.ToArray());

            strArr.clear();
            valArr.clear();

            for (int i = 0; i < 10; i++) {
                strArr.add(new string((char)('A' + i), 1));
                valArr.add(new StructWithRef {p = i, q = strArr[i]});
            }

            strArr.clearAndAddUninitialized(10);
            valArr.clearAndAddUninitialized(10);
            Assert.True(strSpan == strArr.asSpan());
            Assert.True(valSpan == valArr.asSpan());
            Assert.Equal<string>(Enumerable.Repeat<string>(null, 10), strSpan.ToArray());
            Assert.Equal<StructWithRef>(Enumerable.Repeat<StructWithRef>(default, 10), valSpan.ToArray());

            strArr.clear();
            valArr.clear();

            for (int i = 0; i < 10; i++) {
                strArr.add(new string((char)('A' + i), 1));
                valArr.add(new StructWithRef {p = i, q = strArr[i]});
            }

            strArr.clearAndAddUninitialized(15);
            valArr.clearAndAddUninitialized(15);
            Assert.Equal<string>(Enumerable.Repeat<string>(null, 15), strArr.asSpan().ToArray());
            Assert.Equal<StructWithRef>(Enumerable.Repeat<StructWithRef>(default, 15), valArr.asSpan().ToArray());
        }

    }

}
