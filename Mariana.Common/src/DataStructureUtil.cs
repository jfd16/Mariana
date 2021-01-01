using System;
using System.Runtime.CompilerServices;

namespace Mariana.Common {

    /// <summary>
    /// Utility functions for working with data structures such as variable-length lists, hash
    /// tables and sorted lists.
    /// </summary>
    public static class DataStructureUtil {

        /// <summary>
        /// A list of prime numbers that are checked against the argument to <see cref="nextPrime"/>
        /// before calculating a prime number after it, which may be an expensive operation.
        /// </summary>
        private static readonly int[] s_primes = {
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631,
            761, 919, 1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103,
            12143, 14591, 17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631,
            130363, 156437, 187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403,
            968897, 1162687, 1395263, 1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559,
            5999471, 7199369
        };

        /// <summary>
        /// Returns a prime number greater than or equal to the given number.
        /// </summary>
        /// <param name="min">The number for which to return a prime number greater than or equal to
        /// it.</param>
        /// <returns>A prime number greater than or equal to <paramref name="min"/>. (It will not
        /// necessarily be the least such prime number)</returns>
        public static int nextPrime(int min) {
            var primes = s_primes.AsSpan();

            if (min <= primes[primes.Length - 1]) {
                int searchIndex = primes.BinarySearch(min);
                return (searchIndex >= 0) ? min : primes[~searchIndex];
            }

            for (int i = min | 1; i < Int32.MaxValue - 1; i += 2) {
                int j = 3;
                long jsq = 9;
                while (jsq <= (long)i) {
                    if (i % j == 0)
                        goto __nextNum;
                    j += 2;
                    jsq += (long)(j + 1) << 2;
                }
                return i;
            __nextNum:;
            }

            return min;
        }

        /// <summary>
        /// Returns the power of two that is greater than and closest to the given number.
        /// </summary>
        /// <param name="n">The number.</param>
        /// <returns>The power of two that is greater than and closest to the given number.</returns>
        public static int nextPowerOf2(int n) {
            n--;
            n |= n >> 1;
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;
            n++;
            return n;
        }

        /// <summary>
        /// Resizes the specified array to the new length.
        /// </summary>
        ///
        /// <param name="arr">The array. This must not be null.</param>
        /// <param name="length">The initial length of the array.</param>
        /// <param name="newLength">The new length to resize the array to.</param>
        /// <param name="exact">If set to true, use the exact value of <paramref name="newLength"/>
        /// as the new array size, otherwise a new array size is determined automatically (which is
        /// always greater than or equal to newLength).</param>
        ///
        /// <typeparam name="T">The type of the array.</typeparam>
        ///
        /// <remarks>
        /// <list type="bullet">
        /// <item>
        /// If <paramref name="newLength"/> is less than <paramref name="length"/>, all elements
        /// after the index <c>newLength - 1</c> are set to the default value of the array type.
        /// Note that this is different from the <see cref="Array.Resize" qualifyHint="true"/>
        /// method, which creates a new array of a smaller size.
        /// </item>
        /// <item>If <paramref name="newLength"/> is less than <c>arr.Length</c> but not less
        /// than <paramref name="length"/>, the method does not modify the array.</item>
        /// <item>
        /// Otherwise, a new array is created. If <paramref name="exact"/> is true, the length of
        /// the new array is equal to <paramref name="newLength"/>. If <paramref name="exact"/> is
        /// false, the length of the new array will be calculated using the
        /// <see cref="getNextArraySize"/> method. The elements of the <paramref name="arr"/>
        /// array will be copied to the new array. The new array is then assigned to the
        /// <paramref name="arr"/> parameter.
        /// </item>
        /// </list>
        /// </remarks>
        public static void resizeArray<T>(ref T[] arr, int length, int newLength, bool exact) {
            if (newLength < arr.Length) {
                if (newLength < length)
                    arr.AsSpan(newLength, length - newLength).Clear();
            }
            else {
                T[] temp = new T[exact ? newLength : getNextArraySize(length, newLength)];
                arr.AsSpan(0, length).CopyTo(temp);
                arr = temp;
            }
        }

        /// <summary>
        /// Returns a number greater than or equal to <paramref name="newSizeHint"/> suitable for
        /// use as the new length of an enlarged array of initial size
        /// <paramref name="currentSize"/>.
        /// </summary>
        ///
        /// <param name="currentSize">The initial length of the array to be enlarged.</param>
        /// <param name="newSizeHint">The minimum length of the enlarged array.</param>
        /// <returns>A number greater than or equal to <paramref name="newSizeHint"/> suitable for
        /// use as the new length of an enlarged array of initial size
        /// <paramref name="currentSize"/>.</returns>
        public static int getNextArraySize(int currentSize, int newSizeHint) {
            if (currentSize == 0)
                return Math.Max(newSizeHint, 4);

            int computedNewSize;
            if (currentSize > 0x40000000) {
                // Increase length linearly after 2^30 to reduce chance of hitting
                // maximum size (2^31-1) or out-of-memory error
                computedNewSize = currentSize + 0x8000000;
            }
            else {
                computedNewSize = currentSize;
                while (computedNewSize < newSizeHint)
                    computedNewSize *= 2;
            }

            return Math.Max(computedNewSize, newSizeHint);
        }

        /// <summary>
        /// Compacts an array by removing null values. The ordering of non-null elements
        /// in the array is preserved.
        /// </summary>
        /// <returns>The size of the array after compacting. This is the number of non-null elements.</returns>
        /// <param name="arr">The array to be compacted.</param>
        /// <param name="count">The number of elements of the range of <paramref name="arr"/>,
        /// starting at index 0, to be compacted.</param>
        /// <typeparam name="T">The type of the array. This must be a class type.</typeparam>
        public static int compactArray<T>(T[] arr, int count) where T : class {
            int newCount = 0;

            for (int i = 0; i < count; i++) {
                if (arr[i] == null)
                    continue;
                if (i != newCount)
                    arr[newCount] = arr[i];
                newCount++;
            }

            for (int i = newCount; i < count; i++)
                arr[i] = null;

            return newCount;
        }

        /// <summary>
        /// A comparer function used with the <see cref="sortSpan{T}"/> and <see cref="getSpanSortPermutation{T}"/>
        /// methods.
        /// </summary>
        /// <param name="x">The first argument.</param>
        /// <param name="y">The second argument.</param>
        /// <typeparam name="T">The type of the objects being compared.</typeparam>
        /// <returns>A negative value, zero or a positive value if <paramref name="x"/> is less than, equal
        /// to or greater than <paramref name="y"/> respectively.</returns>
        public delegate int SortComparer<T>(in T x, in T y);

        /// <summary>
        /// Sorts a span in place.
        /// </summary>
        /// <param name="span">The span to sort.</param>
        /// <param name="comparer">A comparison function to use for sorting.</param>
        /// <typeparam name="T">The element type of the span to sort.</typeparam>
        public static void sortSpan<T>(Span<T> span, SortComparer<T> comparer) {
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));

            worker(span, comparer, ThreadStaticRandom.instance);

            void worker(Span<T> _span, SortComparer<T> _comparer, Random rng) {
                void swap(ref T x, ref T y) => (x, y) = (y, x);

                if (_span.Length <= 16) {
                    // For small spans use insertion sort
                    for (int i = 1; i < _span.Length; i++) {
                        ref T y = ref _span[i];
                        int j = i;

                        while (j > 0) {
                            ref T x = ref _span[--j];
                            if (_comparer(x, y) > 0)
                                swap(ref x, ref y);
                            y = ref x;
                        }
                    }
                }
                else {
                    // Use randomized quicksort for larger spans

                    ref T pivot = ref _span[_span.Length - 1];
                    int pivotIndex = rng.Next(_span.Length);

                    if (pivotIndex != _span.Length - 1)
                        swap(ref _span[pivotIndex], ref pivot);

                    int j = 0;
                    for (int i = 0; i < _span.Length - 1; i++) {
                        ref T current = ref _span[i];
                        if (_comparer(current, pivot) >= 0)
                            continue;
                        if (i != j)
                            swap(ref current, ref _span[j]);
                        j++;
                    }

                    worker(_span.Slice(0, j), _comparer, rng);

                    if (j < _span.Length - 1) {
                        swap(ref pivot, ref _span[j]);
                        worker(_span.Slice(j + 1), _comparer, rng);
                    }
                }
            }
        }

        /// <summary>
        /// Computes the sorted permutation of a span.
        /// </summary>
        /// <param name="span">The span containing the elements for which to compute the sorted
        /// permutation.</param>
        /// <param name="permutation">A span into which the indices of the sorted permutation will
        /// be written. The length of this span must not be less than that of <paramref name="span"/>.</param>
        /// <param name="comparer">A comparison function to use for sorting.</param>
        /// <typeparam name="T">The element type of the span.</typeparam>
        public static void getSpanSortPermutation<T>(ReadOnlySpan<T> span, Span<int> permutation, SortComparer<T> comparer) {
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));

            permutation = permutation.Slice(0, span.Length);
            for (int i = 0; i < span.Length; i++)
                permutation[i] = i;

            worker(span, permutation, comparer, ThreadStaticRandom.instance);

            void worker(ReadOnlySpan<T> _span, Span<int> _perm, SortComparer<T> _comparer, Random rng) {
                void swap(ref int x, ref int y) => (x, y) = (y, x);

                if (_perm.Length <= 16) {
                    // For small spans use insertion sort
                    for (int i = 1; i < _perm.Length; i++) {
                        ref int y = ref _perm[i];
                        int j = i;

                        while (j > 0) {
                            ref int x = ref _perm[--j];
                            if (_comparer(in _span[x], in _span[y]) > 0)
                                swap(ref x, ref y);
                            y = ref x;
                        }
                    }
                }
                else {
                    // Use randomized quicksort for larger spans

                    ref int pivot = ref _perm[_perm.Length - 1];
                    int pivotIndex = rng.Next(_perm.Length);

                    if (pivotIndex != _perm.Length - 1)
                        swap(ref _perm[pivotIndex], ref pivot);

                    int j = 0;
                    for (int i = 0; i < _perm.Length - 1; i++) {
                        ref int current = ref _perm[i];
                        if (_comparer(in _span[current], in _span[pivot]) >= 0)
                            continue;
                        if (i != j)
                            swap(ref current, ref _perm[j]);
                        j++;
                    }

                    worker(_span, _perm.Slice(0, j), _comparer, rng);

                    if (j < _perm.Length - 1) {
                        swap(ref pivot, ref _perm[j]);
                        worker(_span, _perm.Slice(j + 1), _comparer, rng);
                    }
                }
            }
        }

    }
}
