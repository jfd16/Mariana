using System;
using System.Collections.Generic;

namespace Mariana.AVM2.Core {

    internal static class ArraySortHelper {

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

                int result = 0;
                for (int i = 0; i < xSpan.Length && result == 0; i++)
                    result = String.Compare(xSpan[i], ySpan[i], cmpType);

                return result;
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
            public int Compare(int x, int y) {
                var xSpan = m_keys.AsSpan(x, m_blockSize);
                var ySpan = m_keys.AsSpan(y, xSpan.Length);
                int result = 0;

                for (int i = 0; i < xSpan.Length && result == 0; i++) {
                    double xi = xSpan[i], yi = ySpan[i];

                    if (xi == yi)
                        result = 0;
                    else if (xi < yi)
                        result = -1;
                    else if (Double.IsNaN(yi))
                        result = Double.IsNaN(xi) ? 0 : -1;
                    else
                        result = 1;
                }
                return result;
            }

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
                int result = 0;

                for (int i = 0; i < xSpan.Length && result == 0; i++) {
                    result = m_comparers[i].Compare(xSpan[i], ySpan[i]);
                    if (m_descendingFlags[i])
                        result = -result;
                }
                return result;
            }

        }

    }
}
