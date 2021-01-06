using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Mariana.Common.Tests {

    public sealed class ReadOnlyArrayViewTest {

        [Fact]
        public void defaultValueShouldHaveZeroLength() {
            Assert.Equal(0, default(ReadOnlyArrayView<int>).length);
            Assert.Equal(0, ReadOnlyArrayView<int>.empty.length);
        }

        [Fact]
        public void shouldCreateZeroLengthViewIfNullOrEmptyArrayGiven() {
            Assert.Equal(0, (new ReadOnlyArrayView<int>(null)).length);
            Assert.Equal(0, (new ReadOnlyArrayView<int>(new int[0])).length);
        }

        [Theory]
        [InlineData(new object[] {null})]
        [InlineData(new object[] {new int[] {}})]
        public void threeArgCtor_nullOrEmptyArray(int[] arg) {
            Assert.Equal(0, (new ReadOnlyArrayView<int>(arg, 0, 0)).length);

            Assert.Throws<ArgumentOutOfRangeException>(() => { new ReadOnlyArrayView<int>(arg, 1, 0); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { new ReadOnlyArrayView<int>(arg, -1, 0); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { new ReadOnlyArrayView<int>(arg, 0, 1); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { new ReadOnlyArrayView<int>(arg, 0, -1); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { new ReadOnlyArrayView<int>(arg, 1, -1); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { new ReadOnlyArrayView<int>(arg, -1, 1); });
        }

        [Fact]
        public void threeArgCtor_emptyView() {
            var empty = ReadOnlyArrayView<int>.empty;
            Assert.Equal(0, (new ReadOnlyArrayView<int>(empty, 0, 0)).length);

            Assert.Throws<ArgumentOutOfRangeException>(() => { new ReadOnlyArrayView<int>(empty, 1, 0); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { new ReadOnlyArrayView<int>(empty, -1, 0); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { new ReadOnlyArrayView<int>(empty, 0, 1); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { new ReadOnlyArrayView<int>(empty, 0, -1); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { new ReadOnlyArrayView<int>(empty, 1, -1); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { new ReadOnlyArrayView<int>(empty, -1, 1); });
        }

        [Theory]
        [InlineData(1, 1, 1)]
        [InlineData(1, 1, 2)]
        [InlineData(1, 2, 0)]
        [InlineData(1, 2, 1)]
        [InlineData(1, -1, 0)]
        [InlineData(1, -1, 1)]
        [InlineData(1, 0, -1)]
        [InlineData(1, 1, -1)]
        [InlineData(1, -1, -1)]
        [InlineData(1000, 500, 501)]
        public void threeArgCtor_shouldThrowOnInvalidBounds(int arrLen, int start, int length) {
            int[] arr = new int[arrLen];
            Assert.Throws<ArgumentOutOfRangeException>(() => {
                new ReadOnlyArrayView<int>(arr, start, length);
            });
            Assert.Throws<ArgumentOutOfRangeException>(() => {
                new ReadOnlyArrayView<int>(new ReadOnlyArrayView<int>(arr), start, length);
            });
        }

        [Fact]
        public void asSpan_shouldGetReadOnlySpanOverView() {
            int[] arr = new int[100];

            Assert.True((new ReadOnlyArrayView<int>(arr)).asSpan() == arr.AsSpan());
            Assert.True((new ReadOnlyArrayView<int>(arr)).asSpan(10) == arr.AsSpan(10));
            Assert.True((new ReadOnlyArrayView<int>(arr)).asSpan(10, 30) == arr.AsSpan(10, 30));
            Assert.True((new ReadOnlyArrayView<int>(arr)).asSpan(100).IsEmpty);
            Assert.True((new ReadOnlyArrayView<int>(arr)).asSpan(100, 0).IsEmpty);

            var subView = new ReadOnlyArrayView<int>(arr, 10, 30);

            Assert.True(subView.asSpan() == arr.AsSpan(10, 30));
            Assert.True(subView.asSpan(10) == arr.AsSpan(20, 20));
            Assert.True(subView.asSpan(10, 15) == arr.AsSpan(20, 15));
            Assert.True(subView.asSpan(30).IsEmpty);
            Assert.True(subView.asSpan(30, 0).IsEmpty);

            var subSubView = new ReadOnlyArrayView<int>(subView, 5, 20);

            Assert.True(subSubView.asSpan() == arr.AsSpan(15, 20));
            Assert.True(subSubView.asSpan(10) == arr.AsSpan(25, 10));
            Assert.True(subSubView.asSpan(10, 5) == arr.AsSpan(25, 5));
            Assert.True(subSubView.asSpan(20).IsEmpty);
            Assert.True(subSubView.asSpan(20, 0).IsEmpty);
        }

        [Fact]
        public void length_shouldGetViewLength() {
            int[] arr = new int[100];
            Assert.Equal(100, (new ReadOnlyArrayView<int>(arr)).length);
            var subView = new ReadOnlyArrayView<int>(arr, 10, 30);
            Assert.Equal(30, subView.length);
            var subSubView = new ReadOnlyArrayView<int>(subView, 5, 20);
            Assert.Equal(20, subSubView.length);
        }

        [Fact]
        public void indexer_shouldGetReadOnlyRefToElement() {
            int[] arr = new int[100];

            var view = new ReadOnlyArrayView<int>(arr);
            Assert.True(Unsafe.AreSame(ref arr[0], ref Unsafe.AsRef(in view[0])));
            Assert.True(Unsafe.AreSame(ref arr[99], ref Unsafe.AsRef(in view[99])));

            var subView = new ReadOnlyArrayView<int>(arr, 10, 30);
            Assert.True(Unsafe.AreSame(ref arr[10], ref Unsafe.AsRef(in subView[0])));
            Assert.True(Unsafe.AreSame(ref arr[20], ref Unsafe.AsRef(in subView[10])));

            var subSubView = new ReadOnlyArrayView<int>(subView, 5, 20);
            Assert.True(Unsafe.AreSame(ref arr[15], ref Unsafe.AsRef(in subSubView[0])));
            Assert.True(Unsafe.AreSame(ref arr[20], ref Unsafe.AsRef(in subSubView[5])));
        }

        [Fact]
        public void indexer_shouldThrowOnInvalidIndex() {
            int[] arr = new int[100];

            var view = new ReadOnlyArrayView<int>(arr);
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = view[100]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = view[101]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = view[-1]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = view[Int32.MaxValue]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = view[Int32.MinValue]; });

            var subView = new ReadOnlyArrayView<int>(arr, 10, 30);
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = subView[30]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = subView[31]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = subView[-1]; });

            var subSubView = new ReadOnlyArrayView<int>(subView, 5, 20);
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = subSubView[20]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = subSubView[21]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = subSubView[-2]; });

            var emptyView = ReadOnlyArrayView<int>.empty;
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = emptyView[0]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = emptyView[1]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = emptyView[-1]; });
        }

        [Fact]
        public void slice_shouldCreateSubView() {
            int[] arr = new int[100];

            Assert.True((new ReadOnlyArrayView<int>(arr)).slice(10).asSpan() == arr.AsSpan(10));
            Assert.True((new ReadOnlyArrayView<int>(arr)).slice(10, 30).asSpan() == arr.AsSpan(10, 30));
            Assert.Equal(0, (new ReadOnlyArrayView<int>(arr)).slice(100).length);
            Assert.Equal(0, (new ReadOnlyArrayView<int>(arr)).slice(100, 0).length);

            var subView = new ReadOnlyArrayView<int>(arr, 10, 30);

            Assert.True(subView.slice(10).asSpan() == arr.AsSpan(20, 20));
            Assert.True(subView.slice(10, 15).asSpan() == arr.AsSpan(20, 15));
            Assert.Equal(0, subView.slice(30).length);
            Assert.Equal(0, subView.slice(30, 0).length);

            var subSubView = new ReadOnlyArrayView<int>(subView, 5, 20);

            Assert.True(subSubView.slice(10).asSpan() == arr.AsSpan(25, 10));
            Assert.True(subSubView.slice(10, 5).asSpan() == arr.AsSpan(25, 5));
            Assert.Equal(0, subSubView.slice(20).length);
            Assert.Equal(0, subSubView.slice(20, 0).length);
        }

        [Fact]
        public void toArray_shouldCreateCopy() {
            int[] arr = new int[100];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = i;

            var view = new ReadOnlyArrayView<int>(arr);
            Assert.NotSame(arr, view.toArray());
            Assert.True(arr.AsSpan().SequenceEqual(view.toArray()));

            var subView = new ReadOnlyArrayView<int>(arr, 10, 30);
            Assert.True(arr.AsSpan(10, 30).SequenceEqual(subView.toArray()));

            var subSubView = new ReadOnlyArrayView<int>(subView, 5, 20);
            Assert.True(arr.AsSpan(15, 20).SequenceEqual(subSubView.toArray()));

            var emptyView = ReadOnlyArrayView<int>.empty;
            Assert.Empty(emptyView.toArray());
        }

        [Fact]
        public void implicitConversion_shouldCreateViewOverArray() {
            int[] arr = null;
            ReadOnlyArrayView<int> view = arr;
            Assert.Equal(0, view.length);

            arr = new int[100];
            view = arr;
            Assert.True(view.asSpan() == arr.AsSpan());
        }

        [Theory]
        [InlineData(new[] {1, 2, 3, 4, 5}, 0, 5)]
        [InlineData(new[] {1, 2, 3, 4, 5}, 1, 3)]
        [InlineData(new[] {1, 2, 3, 4, 5}, 3, 0)]
        [InlineData(new int[] {}, 0, 0)]
        public void enumerator_shouldEnumerateViewElements(int[] arr, int start, int length) {
            ReadOnlyArrayView<int>.Enumerator enumerator;
            IEnumerator<int> enumerator2;
            IEnumerator enumerator3;

            var view = new ReadOnlyArrayView<int>(arr, start, length);
            enumerator = view.GetEnumerator();
            enumerator2 = ((IEnumerable<int>)view).GetEnumerator();
            enumerator3 = ((IEnumerable)view).GetEnumerator();

            for (int i = 0; i < length; i++) {
                Assert.True(enumerator.MoveNext());
                Assert.Equal(arr[start + i], enumerator.Current);
                Assert.True(enumerator2.MoveNext());
                Assert.Equal(arr[start + i], enumerator2.Current);
                Assert.True(enumerator3.MoveNext());
                Assert.Equal(arr[start + i], enumerator3.Current);
            }

            Assert.False(enumerator.MoveNext());
            Assert.False(enumerator2.MoveNext());
            Assert.False(enumerator3.MoveNext());
            Assert.False(enumerator.MoveNext());
            Assert.False(enumerator2.MoveNext());
            Assert.False(enumerator3.MoveNext());

            Assert.Throws<NotImplementedException>(() => enumerator2.Reset());
            Assert.Throws<NotImplementedException>(() => enumerator3.Reset());

            enumerator2.Dispose();
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(1, -1)]
        [InlineData(1000, 1001)]
        public void asSpan_slice_start_shouldThrowOnInvalidBounds(int arrLen, int start) {
            int[] arr = new int[arrLen];
            Assert.Throws<ArgumentOutOfRangeException>(() => {
                (new ReadOnlyArrayView<int>(arr)).asSpan(start);
            });
            Assert.Throws<ArgumentOutOfRangeException>(() => {
                (new ReadOnlyArrayView<int>(arr)).slice(start);
            });
        }

        [Theory]
        [InlineData(1, 1, 1)]
        [InlineData(1, 1, 2)]
        [InlineData(1, 2, 0)]
        [InlineData(1, 2, 1)]
        [InlineData(1, -1, 0)]
        [InlineData(1, -1, 1)]
        [InlineData(1, 0, -1)]
        [InlineData(1, 1, -1)]
        [InlineData(1, -1, -1)]
        [InlineData(1000, 500, 501)]
        public void asSpan_slice_startAndLength_shouldThrowOnInvalidBounds(int arrLen, int start, int length) {
            int[] arr = new int[arrLen];
            Assert.Throws<ArgumentOutOfRangeException>(() => {
                (new ReadOnlyArrayView<int>(arr)).asSpan(start, length);
            });
            Assert.Throws<ArgumentOutOfRangeException>(() => {
                (new ReadOnlyArrayView<int>(arr)).slice(start, length);
            });
        }

        [Fact]
        public void interface_IReadOnlyList_shouldGetCountAndElements() {
            int[] arr = new int[100];
            for (int i = 0; i < 100; i++)
                arr[i] = i * i + 4 * i + 7;

            IReadOnlyList<int> view = new ReadOnlyArrayView<int>(arr);
            Assert.Equal(100, view.Count);
            Assert.Equal(arr[0], view[0]);
            Assert.Equal(arr[2], view[2]);
            Assert.Equal(arr[99], view[99]);

            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = view[100]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = view[101]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = view[-1]; });
        }

    }

}
