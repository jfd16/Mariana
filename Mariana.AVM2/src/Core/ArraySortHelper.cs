using System;
using System.Collections.Generic;

namespace Mariana.AVM2.Core {

    internal static class ArraySortHelper {

        /// <summary>
        /// Sorts the given array using the given comparer.
        /// </summary>
        /// <param name="arr">The array to sort.</param>
        /// <param name="comparer">The comparer to use for sorting.</param>
        /// <param name="failOnEqualElements">If this is true, restore the original order of
        /// <paramref name="arr"/> if two elements compare as equal.</param>
        /// <param name="length">If specified, only considers the first <paramref name="length"/>
        /// elements of the array.</param>
        /// <typeparam name="T">The type of the array.</typeparam>
        /// <returns>True if the array was sorted, false if it was not (because of elements comparing
        /// equal with <paramref name="failOnEqualElements"/> set to true.)</returns>
        public static bool sort<T>(
            T[] arr, IComparer<T> comparer, bool failOnEqualElements = false, int length = -1)
        {
            if (length == -1)
                length = arr.Length;

            if (length <= 1)
                return true;

            if (!failOnEqualElements) {
                Array.Sort(arr, 0, length, comparer);
                return true;
            }

            T[] tempCopy = arr.AsSpan(0, length).ToArray();
            Array.Sort(tempCopy, comparer);

            T prev = tempCopy[0];
            for (int i = 1; i < length; i++) {
                T cur = tempCopy[i];
                if (comparer.Compare(prev, cur) == 0)
                    return false;
                prev = cur;
            }

            tempCopy.AsSpan(0, length).CopyTo(arr);
            return true;
        }

        /// <summary>
        /// Sorts a pair of arrays using the given key comparer.
        /// </summary>
        /// <param name="keys">The keys array.</param>
        /// <param name="values">The values array. This will be sorted using the sort order
        /// of the <paramref name="keys"/> array.</param>
        /// <param name="comparer">The comparer to use for sorting.</param>
        /// <param name="failOnEqualElements">If this is true, the original order of the
        /// <paramref name="values"/> array is restored if two keys in <paramref name="keys"/>
        /// compare as equal.</param>
        /// <param name="length">If specified, only considers the first <paramref name="length"/>
        /// elements of the keys and values arrays.</param>
        /// <typeparam name="T">The element type of the keys array.</typeparam>
        /// <typeparam name="U">The element type of the values array.</typeparam>
        /// <returns>True if the values array was sorted, false if it was not (because of elements comparing
        /// equal with <paramref name="failOnEqualElements"/> set to true.)</returns>
        public static bool sortPair<T, U>(
            T[] keys, U[] values, IComparer<T> comparer, bool failOnEqualElements = false, int length = -1)
        {
            if (length == -1)
                length = keys.Length;

            if (length <= 1)
                return true;

            if (!failOnEqualElements) {
                Array.Sort(keys, values, 0, length, comparer);
                return true;
            }

            U[] tempValues = values.AsSpan(0, length).ToArray();
            Array.Sort(keys, tempValues, 0, length, comparer);

            T prev = keys[0];
            for (int i = 1; i < length; i++) {
                T cur = keys[i];
                if (comparer.Compare(prev, cur) == 0)
                    return false;
                prev = cur;
            }

            tempValues.AsSpan(0, length).CopyTo(values);
            return true;
        }

        /// <summary>
        /// Returns a permutation array that represents the sorted order of the given array.
        /// The array is not mutated.
        /// </summary>
        /// <param name="arr">The array whose sorted permutation is to be obtained.</param>
        /// <param name="comparer">The comparer to use for sorting.</param>
        /// <param name="failOnEqualElements">If this is true, return null if two elements in
        /// <paramref name="arr"/> compare as equal.</param>
        /// <param name="length">If specified, only considers the first <paramref name="length"/>
        /// elements of the array.</param>
        /// <typeparam name="T">The type of the array.</typeparam>
        /// <returns>A permutation array representing the sorted order of <paramref name="arr"/>.</returns>
        public static int[] getSortedPermutation<T>(
            T[] arr, IComparer<T> comparer, bool failOnEqualElements = false, int length = -1)
        {
            if (length == -1)
                length = arr.Length;

            int[] perm = new int[length];
            for (int i = 0; i < length; i++)
                perm[i] = i;

            Array.Sort(perm, 0, length, new PermutationComparer<T>(arr, comparer));

            if (failOnEqualElements && length > 1) {
                T prev = arr[perm[0]];
                for (int i = 1; i < perm.Length; i++) {
                    T cur = arr[perm[i]];
                    if (comparer.Compare(prev, cur) == 0)
                        return null;
                    prev = cur;
                }
            }

            return perm;
        }


        private class PermutationComparer<T> : IComparer<int> {
            private T[] m_map;
            private IComparer<T> m_comparer;

            public PermutationComparer(T[] map, IComparer<T> comparer) {
                m_map = map;
                m_comparer = comparer;
            }

            public int Compare(int x, int y) => m_comparer.Compare(m_map[x], m_map[y]);
        }

        /// <summary>
        /// A comparer for use with the <see cref="ASArray.sortOn" qualifyHint="true"/>
        /// method for string comparisons.
        /// </summary>
        public sealed class StringBlockComparer : IComparer<int> {

            private string[] m_keys;
            private int m_blockSize;
            private StringComparison m_compareType;

            /// <summary>
            /// Creates a new instance of <see cref="StringBlockComparer"/>.
            /// </summary>
            ///
            /// <param name="keys">The sorting keys to use for comparison. The length of this array must
            /// be equal to the length of the array being sorted, multiplied by the block size (number of
            /// properties).</param>
            /// <param name="blockSize">The size of a block in the keys array. This is the number of
            /// properties to consider for sorting.</param>
            /// <param name="ignoreCase">If set to true, ignore the case of strings for
            /// comparison.</param>
            public StringBlockComparer(string[] keys, int blockSize, bool ignoreCase) {
                m_compareType = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                m_keys = keys;
                m_blockSize = blockSize;
            }

            /// <summary>
            /// Compares two blocks and returns a value less than, equal to or greater than 0 if
            /// <paramref name="x"/> is less than, equal to or greater than <paramref name="y"/>,
            /// respectively.
            /// </summary>
            ///
            /// <param name="x">The start index of the first block in the <see cref="m_keys"/>
            /// array.</param>
            /// <param name="y">The start index of the second block in the <see cref="m_keys"/>
            /// array.</param>
            public int Compare(int x, int y) {
                var xSpan = m_keys.AsSpan(x, m_blockSize);
                var ySpan = m_keys.AsSpan(y, xSpan.Length);
                StringComparison cmpType = m_compareType;

                for (int i = 0; i < xSpan.Length; i++) {
                    int result = String.Compare(xSpan[i], ySpan[i], cmpType);
                    if (result != 0)
                        return result;
                }
                return 0;
            }

        }

        /// <summary>
        /// A comparer intended for use with the <see cref="ASArray.sortOn" qualifyHint="true"/>
        /// method for numeric comparisons.
        /// </summary>
        public sealed class NumericBlockComparer : IComparer<int> {

            private double[] m_keys;
            private int m_blockSize;

            /// <summary>
            /// Creates a new instance of <see cref="NumericBlockComparer"/>.
            /// </summary>
            ///
            /// <param name="keys">The sorting keys to use for comparison. The length of this array must
            /// be equal to the length of the array being sorted, multiplied by the block size (number of
            /// properties).</param>
            /// <param name="blockSize">The size of a block in the keys array. This is the number of
            /// properties to consider for sorting.</param>
            public NumericBlockComparer(double[] keys, int blockSize) {
                m_keys = keys;
                m_blockSize = blockSize;
            }

            /// <summary>
            /// Compares two blocks and returns a value less than, equal to or greater than 0 if
            /// <paramref name="x"/> is less than, equal to or greater than <paramref name="y"/>,
            /// respectively.
            /// </summary>
            ///
            /// <param name="x">The start index of the first block in the <see cref="m_keys"/>
            /// array.</param>
            /// <param name="y">The start index of the second block in the <see cref="m_keys"/>
            /// array.</param>
            public int Compare(int x, int y) =>
                m_keys.AsSpan(x, m_blockSize).SequenceCompareTo(m_keys.AsSpan(y, m_blockSize));

        }

        /// <summary>
        /// A generic version of the sortOn comparer which can be used with any type.
        /// </summary>
        public sealed class GenericBlockComparer<T> : IComparer<int> {

            private T[] m_keys;
            private int m_blockSize;
            private IComparer<T>[] m_comparers;
            private bool[] m_descendingFlags;

            /// <summary>
            /// Creates a new instance of <see cref="GenericBlockComparer{T}"/>.
            /// </summary>
            ///
            /// <param name="keys">The sorting keys to use for comparison. The length of this array must
            /// be equal to the length of the array being sorted, multiplied by the block size (number of
            /// properties).</param>
            /// <param name="blockSize">The size of a block in the keys array. This is the number of
            /// properties to consider for sorting.</param>
            /// <param name="comparers">The comparers used by each element in a block. The length of this
            /// array must be equal to <paramref name="blockSize"/></param>
            /// <param name="descending">An array of Boolean values indicating whether to use descending
            /// sort for each element in a block. The length of this array must be equal to
            /// <paramref name="blockSize"/>.</param>
            public GenericBlockComparer(T[] keys, int blockSize, IComparer<T>[] comparers, bool[] descending) {
                m_keys = keys;
                m_blockSize = blockSize;
                m_comparers = comparers;
                m_descendingFlags = descending;
            }

            /// <summary>
            /// Compares two blocks and returns a value less than, equal to or greater than 0 if
            /// <paramref name="x"/> is less than, equal to or greater than <paramref name="y"/>,
            /// respectively.
            /// </summary>
            ///
            /// <param name="x">The starting index of the first block.</param>
            /// <param name="y">The starting index of the second block.</param>
            public int Compare(int x, int y) {
                var xSpan = m_keys.AsSpan(x, m_blockSize);
                var ySpan = m_keys.AsSpan(y, xSpan.Length);

                for (int i = 0; i < xSpan.Length; i++) {
                    int result = m_comparers[i].Compare(xSpan[i], ySpan[i]);
                    if (result != 0)
                        return m_descendingFlags[i] ? -result : result;
                }
                return 0;
            }

        }

    }
}
