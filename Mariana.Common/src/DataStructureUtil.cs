using System;
using System.Threading;

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
            // This table is taken from the .NET Core source
            // https://source.dot.net/#System.Collections/HashHelpers.cs
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
            1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
            17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
            187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369
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

            if (min <= primes[^1]) {
                int searchIndex = primes.BinarySearch(min);
                return (searchIndex >= 0) ? min : primes[~searchIndex];
            }

            for (int i = min | 1; (uint)i < (uint)Int32.MaxValue; i += 2) {
                int j = 3;
                long jsq = 9;
                bool isNonPrime = false;

                while (jsq <= (long)i) {
                    if (i % j == 0) {
                        isNonPrime = true;
                        break;
                    }
                    j += 2;
                    jsq += (long)(j + 1) << 2;
                }

                if (!isNonPrime)
                    return i;
            }

            return min;
        }

        /// <summary>
        /// Returns the power of two that is greater than and closest to the given number.
        /// </summary>
        /// <param name="n">The number.</param>
        /// <returns>The positive power of two that is greater than and closest to the given number.</returns>
        public static int nextPowerOf2(int n) {
            if (n < 1)
                return 1;

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
        /// <param name="array">The array. This must not be null. If a new array is allocated,
        /// it will be written to this argument.</param>
        /// <param name="length">The current length. This must be a positive value less
        /// than or equal to the length of <paramref name="array"/>.</param>
        /// <param name="newLength">The new length to resize the array to.</param>
        /// <param name="exact">If set to true (and <paramref name="newLength"/> is greater than the
        /// length of <paramref name="array"/>), use the exact value of <paramref name="newLength"/>
        /// as the length of the new array to be allocated.</param>
        ///
        /// <typeparam name="T">The type of the array.</typeparam>
        ///
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative or
        /// greater than the length of <paramref name="array"/>, or <paramref name="newLength"/>
        /// is negative.</exception>
        ///
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>
        /// If <paramref name="newLength"/> is less than <paramref name="length"/>, all elements
        /// in the range [<paramref name="newLength"/>, <paramref name="length"/> - 1] are set to the
        /// default value of <typeparamref name="T"/>.
        /// Note that this is different from the <see cref="Array.Resize" qualifyHint="true"/>
        /// method, which creates a new array of a smaller size.
        /// </description></item>
        /// <item><description>If <paramref name="newLength"/> is less than or equal to the length of <paramref name="array"/>
        /// but not less than <paramref name="length"/>, all elements in the range
        /// [<paramref name="length"/>, <paramref name="newLength"/> - 1] are set to the
        /// default value of <typeparamref name="T"/>.</description></item>
        /// <item><description>
        /// Otherwise, a new array is created. If <paramref name="exact"/> is true, the length of
        /// the new array is equal to <paramref name="newLength"/>. If <paramref name="exact"/> is
        /// false, the length of the new array is calculated using the <see cref="getNextArraySize"/> method.
        /// The elements of <paramref name="array"/> from index 0 to <paramref name="length"/> - 1 will be
        /// copied to the new array. The new array will be assigned to the <paramref name="array"/> argument.
        /// </description></item>
        /// </list>
        /// </remarks>
        public static void resizeArray<T>(ref T[] array, int length, int newLength, bool exact = false) {
            T[] currentArray = array;

            if (currentArray == null)
                throw new ArgumentNullException(nameof(array));

            if ((uint)length > (uint)currentArray.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (newLength < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (newLength <= currentArray.Length) {
                if (newLength < length)
                    currentArray.AsSpan(newLength, length - newLength).Clear();
                else
                    currentArray.AsSpan(length, newLength - length).Clear();
            }
            else {
                T[] newArray = new T[exact ? newLength : getNextArraySize(length, newLength)];
                (new ReadOnlySpan<T>(currentArray, 0, length)).CopyTo(newArray);
                array = newArray;
            }
        }

        /// <summary>
        /// Allocates a new array whose length is at least <paramref name="minExpandSize"/>
        /// greater than the length of <paramref name="array"/>, and copies the elements
        /// of <paramref name="array"/> to it.
        /// </summary>
        ///
        /// <param name="array">The array to expand. The newly allocated array will be written
        /// to this argument.</param>
        /// <param name="minExpandSize">The minimum number of elements that must be available in
        /// the expanded array after the existing elements of <paramref name="array"/>.</param>
        ///
        /// <typeparam name="T">The type of the array.</typeparam>
        ///
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="minExpandSize"/> is
        /// negative, or the sum of the length of <paramref name="array"/> and
        /// <paramref name="minExpandSize"/> is greater than the maximum value of
        /// <see cref="Int32"/>.</exception>
        ///
        /// <remarks>
        /// The elements of the expanded array after the existing elements of <paramref name="array"/>
        /// are guaranteed to be initialized to the default value of <typeparamref name="T"/>.
        /// </remarks>
        public static void expandArray<T>(ref T[] array, int minExpandSize = 1) {
            T[] currentArray = array;

            if (currentArray == null)
                throw new ArgumentNullException(nameof(array));

            if (minExpandSize == 0)
                return;

            int newLength = currentArray.Length + minExpandSize;
            if (minExpandSize < 0 || newLength < 0)
                throw new ArgumentOutOfRangeException(nameof(minExpandSize));

            T[] newArray = new T[getNextArraySize(array.Length, newLength)];
            (new ReadOnlySpan<T>(currentArray)).CopyTo(newArray);

            array = newArray;
        }

        /// <summary>
        /// Ensures that the length of the array at the given location it at least <paramref name="minLength"/>.
        /// If the array length is less than <paramref name="minLength"/>, a new array of length
        /// <paramref name="minLength"/> is allocated, all elements in the existing array copied to
        /// it, and the reference is updated to the new array.
        /// </summary>
        ///
        /// <param name="array">A reference to an array. If a new array is allocated, it will be
        /// written to this reference. If the array at this reference is null, it is considered
        /// to be equivalent to an array of length zero (that is, a new array is allocated if
        /// <paramref name="minLength"/> is not zero).</param>
        /// <param name="minLength">The minimum length of the array at the reference
        /// <paramref name="array"/> so that no expansion is needed. This must be greater
        /// than zero.</param>
        ///
        /// <returns>If a new array was allocated, returns that array; otherwise, returns the
        /// existing array in the <paramref name="array"/> reference.</returns>
        ///
        /// <typeparam name="T">The type of the array.</typeparam>
        ///
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="minLength"/> is negative
        /// or zero.</exception>
        ///
        /// <remarks>
        /// <para>
        /// If a new array is allocated, the elements of the array at indices greater than
        /// or equal to the length of the existing array are guaranteed to be initialized
        /// to the default value of <typeparamref name="T"/>.
        /// </para>
        /// <para>
        /// If a new array is allocated, it is guaranteed that a thread that reads from the
        /// reference <paramref name="array"/> concurrently while this method is executing
        /// will observe either the old array, or the new array with all existing elements
        /// from the old array copied. In particular, it will never observe the new array
        /// before all the elements from the old array have been copied to it.
        /// </para>
        /// </remarks>
        public static T[] volatileEnsureArraySize<T>(ref T[]? array, int minLength) {
            if (minLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(minLength));

            T[]? currentArray = Volatile.Read(ref array);

            int currentArrayLength = (currentArray == null) ? 0 : currentArray.Length;
            if (currentArrayLength >= minLength)
                return currentArray!;

            int newArrayLength = getNextArraySize(currentArrayLength, minLength);
            T[] newArray = new T[newArrayLength];
            (new ReadOnlySpan<T>(currentArray)).CopyTo(newArray);

            Volatile.Write(ref array, newArray);
            return newArray;
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
        ///
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="newSizeHint"/> is not
        /// greater than <paramref name="currentSize"/>, or <paramref name="currentSize"/> is
        /// a negative value.</exception>
        public static int getNextArraySize(int currentSize, int newSizeHint) {
            if (currentSize < 0)
                throw new ArgumentOutOfRangeException(nameof(currentSize));
            if (newSizeHint <= currentSize)
                throw new ArgumentOutOfRangeException(nameof(newSizeHint));

            if (currentSize == 0)
                return Math.Max(newSizeHint, 4);

            int computedNewSize;
            if (currentSize > 0x40000000) {
                // Increase length linearly after 2^30 to reduce chance of hitting
                // maximum size (2^31-1) or out-of-memory error
                computedNewSize = Math.Max(currentSize + 0x8000000, Int32.MaxValue);
            }
            else {
                computedNewSize = currentSize;
                while ((uint)computedNewSize < (uint)newSizeHint)
                    computedNewSize *= 2;
            }

            return Math.Max(computedNewSize, newSizeHint);
        }

        /// <summary>
        /// Compacts the given span of object references by removing nulls, preserving the order of non-null
        /// references.
        /// </summary>
        /// <returns>The compacted span. This will be a slice of <paramref name="span"/> that
        /// starts at the same location as <paramref name="span"/>.</returns>
        /// <param name="span">The span to be compacted.</param>
        /// <typeparam name="T">The element type of the span. This must be a class type.</typeparam>
        public static Span<T> compactNulls<T>(Span<T?> span) where T : class {
            int newCount = 0;

            for (int i = 0; i < span.Length; i++) {
                if (span[i] == null)
                    continue;
                if (i != newCount)
                    span[newCount] = span[i];
                newCount++;
            }

            span.Slice(newCount).Clear();
            return span.Slice(0, newCount)!;
        }

        /// <summary>
        /// A comparer function used with the <see cref="sortSpan{T}(Span{T}, SortComparerIn{T})"/>
        /// and <see cref="getSpanSortPermutation{T}"/> methods. This takes the arguments as in-parameters
        /// instead of value parameters and is suitable for large structs.
        /// </summary>
        /// <param name="x">The first argument.</param>
        /// <param name="y">The second argument.</param>
        /// <typeparam name="T">The type of the objects being compared.</typeparam>
        /// <returns>A negative value, zero or a positive value if <paramref name="x"/> is less than, equal
        /// to or greater than <paramref name="y"/> respectively.</returns>
        public delegate int SortComparerIn<T>(in T x, in T y);

        /// <summary>
        /// Sorts a span in place.
        /// </summary>
        /// <param name="span">The span to sort.</param>
        /// <param name="comparer">A comparison function to use for sorting.</param>
        /// <typeparam name="T">The element type of the span to sort.</typeparam>
        ///
        /// <exception cref="ArgumentNullException"><paramref name="comparer"/> is null.</exception>
        public static void sortSpan<T>(Span<T> span, Comparison<T> comparer) {
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));

            worker(span, comparer, ThreadStaticRandom.instance);

            static void worker(Span<T> _span, Comparison<T> _comparer, Random rng) {
                static void swap(ref T x, ref T y) => (x, y) = (y, x);

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

                    ref T pivot = ref _span[^1];
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
        /// Sorts a span in place.
        /// </summary>
        /// <param name="span">The span to sort.</param>
        /// <param name="comparer">A comparison function to use for sorting.</param>
        /// <typeparam name="T">The element type of the span to sort.</typeparam>
        ///
        /// <exception cref="ArgumentNullException"><paramref name="comparer"/> is null.</exception>
        public static void sortSpan<T>(Span<T> span, SortComparerIn<T> comparer) {
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));

            worker(span, comparer, ThreadStaticRandom.instance);

            static void worker(Span<T> _span, SortComparerIn<T> _comparer, Random rng) {
                static void swap(ref T x, ref T y) => (x, y) = (y, x);

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

                    ref T pivot = ref _span[^1];
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
        /// be written. The length of this span must be equal to that of <paramref name="span"/>.</param>
        /// <param name="comparer">A comparison function to use for sorting.</param>
        /// <typeparam name="T">The element type of the span.</typeparam>
        ///
        /// <exception cref="ArgumentNullException"><paramref name="comparer"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="permutation"/> does not have the same length
        /// as <paramref name="span"/>.</exception>
        public static void getSpanSortPermutation<T>(ReadOnlySpan<T> span, Span<int> permutation, SortComparerIn<T> comparer) {
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));
            if (permutation.Length != span.Length)
                throw new ArgumentException("The permutation span must have the same number of elements as the data span.", nameof(permutation));

            for (int i = 0; i < span.Length; i++)
                permutation[i] = i;

            worker(span, permutation, comparer, ThreadStaticRandom.instance);

            static void worker(ReadOnlySpan<T> _span, Span<int> _perm, SortComparerIn<T> _comparer, Random rng) {
                static void swap(ref int x, ref int y) => (x, y) = (y, x);

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

                    ref int pivot = ref _perm[^1];
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
