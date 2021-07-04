using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Mariana.Common {

    /// <summary>
    /// A read-only wrapper for accessing an array or a continuous segment of an array.
    /// </summary>
    /// <typeparam name="T">The type of the array elements.</typeparam>
    public readonly struct ReadOnlyArrayView<T> : IReadOnlyList<T> {

        /// <summary>
        /// A <see cref="ReadOnlyArrayView{T}"/> instance representing an empty array. This is the
        /// default value of this type.
        /// </summary>
        public static readonly ReadOnlyArrayView<T> empty = default(ReadOnlyArrayView<T>);

        private readonly T[]? m_data;
        private readonly int m_start;
        private readonly int m_length;

        /// <summary>
        /// Creates a new <see cref="ReadOnlyArrayView{T}"/> for an array.
        /// </summary>
        /// <param name="array">The array. If this is null, creates a view over an empty array.</param>
        public ReadOnlyArrayView(T[]? array) {
            m_data = array;
            m_start = 0;
            m_length = (array == null) ? 0 : array.Length;
        }

        /// <summary>
        /// Creates a new <see cref="ReadOnlyArrayView{T}"/> for a segment of an array.
        /// </summary>
        /// <param name="array">The array.</param>
        /// <param name="start">The index of the first element of the range.</param>
        /// <param name="length">The number of elements in the range.</param>
        /// <exception cref="ArgumentException">The range defined by <paramref name="start"/> and
        /// <paramref name="length"/> is out of the bounds of <paramref name="array"/>, or
        /// <paramref name="array"/> is null and <paramref name="start"/> or <paramref name="length"/>
        /// is not zero.</exception>
        public ReadOnlyArrayView(T[]? array, int start, int length) {
            array ??= Array.Empty<T>();

            if ((uint)start > (uint)array.Length)
                throw new ArgumentOutOfRangeException(nameof(start));
            if ((uint)length > (uint)(array.Length - start))
                throw new ArgumentOutOfRangeException(nameof(length));

            (m_data, m_start, m_length) = (array, start, length);
        }

        /// <summary>
        /// Creates a new <see cref="ReadOnlyArrayView{T}"/> for a slice of another
        /// <see cref="ReadOnlyArrayView{T}"/>.
        /// </summary>
        ///
        /// <param name="view">The <see cref="ReadOnlyArrayView{T}"/> for which to create a slice.</param>
        /// <param name="start">The index of the first element of the slice range.</param>
        /// <param name="length">The number of elements in the slice range.</param>
        /// <exception cref="ArgumentException">The range defined by <paramref name="start"/> and
        /// <paramref name="length"/> is out of the bounds of this <see cref="ReadOnlyArrayView{T}"/>.</exception>
        public ReadOnlyArrayView(in ReadOnlyArrayView<T> view, int start, int length) {
            if ((uint)start > (uint)view.m_length)
                throw new ArgumentOutOfRangeException(nameof(start));
            if ((uint)length > (uint)(view.m_length - start))
                throw new ArgumentOutOfRangeException(nameof(length));

            (m_data, m_start, m_length) = (view.m_data, view.m_start + start, length);
        }

        /// <summary>
        /// Returns the number of elements in this <see cref="ReadOnlyArrayView{T}"/> instance.
        /// </summary>
        public int length => m_length;

        /// <summary>
        /// Returns a reference to the element at the given index.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of the bounds
        /// of this array.</exception>
        public ref readonly T this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if ((uint)index >= (uint)m_length)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return ref m_data![m_start + index];
            }
        }

        /// <summary>
        /// Returns a new array containing the elements of this <see cref="ReadOnlyArrayView{T}"/>
        /// instance.
        /// </summary>
        /// <returns>An array containing the elements of this <see cref="ReadOnlyArrayView{T}"/>
        /// instance.</returns>
        public T[] toArray() => (m_length == 0) ? Array.Empty<T>() : asSpan().ToArray();

        /// <summary>
        /// Returns a span that can be used to access the array view represented by this instance.
        /// </summary>
        public ReadOnlySpan<T> asSpan() => new ReadOnlySpan<T>(m_data, m_start, m_length);

        /// <summary>
        /// Returns a span that can be used to access the segment of the view represented
        /// by this instance from the given index until the end.
        /// </summary>
        ///
        /// <param name="start">The index of the first element of the view segment.</param>
        /// <returns>A <see cref="ReadOnlySpan{T}"/> that provides access to the segment
        /// of the current view starting at index <paramref name="start"/> and ending at
        /// the last element.</returns>
        ///
        /// <exception cref="ArgumentException"><paramref name="start"/> is greater than the number
        /// of elements in this <see cref="ReadOnlyArrayView{T}"/>.</exception>
        public ReadOnlySpan<T> asSpan(int start) {
            if ((uint)start > (uint)m_length)
                throw new ArgumentOutOfRangeException(nameof(start));

            return new ReadOnlySpan<T>(m_data, m_start + start, m_length - start);
        }

        /// <summary>
        /// Returns a span that can be used to access a segment of the view represented
        /// by this instance.
        /// </summary>
        ///
        /// <param name="start">The index of the first element of the view segment.</param>
        /// <param name="length">The length of the segment.</param>
        /// <returns>A <see cref="ReadOnlySpan{T}"/> that provides access to the segment
        /// of the current view starting at index <paramref name="start"/> and having
        /// <paramref name="length"/> number of elements.</returns>
        ///
        /// <exception cref="ArgumentOutOfRangeException">The range defined by <paramref name="start"/>
        /// and <paramref name="length"/> is not within the bounds of this <see cref="ReadOnlyArrayView{T}"/>.</exception>
        public ReadOnlySpan<T> asSpan(int start, int length) {
            if ((uint)start > (uint)m_length)
                throw new ArgumentOutOfRangeException(nameof(start));
            if ((uint)length > (uint)(m_length - start))
                throw new ArgumentOutOfRangeException(nameof(length));

            return new ReadOnlySpan<T>(m_data, m_start + start, length);
        }

        /// <summary>
        /// Creates a new <see cref="ReadOnlyArrayView{T}"/> for the segment of this
        /// <see cref="ReadOnlyArrayView{T}"/> from the given index until the end.
        /// </summary>
        /// <param name="start">The index of the first element of the slice range.</param>
        /// <returns>A <see cref="ReadOnlyArrayView{T}"/> that provides access to the segment
        /// of the current view starting at index <paramref name="start"/> and ending at
        /// the last element.</returns>
        ///
        /// <exception cref="ArgumentException"><paramref name="start"/> is greater than the number
        /// of elements in this <see cref="ReadOnlyArrayView{T}"/>.</exception>
        public ReadOnlyArrayView<T> slice(int start) => new ReadOnlyArrayView<T>(this, start, m_length - start);

        /// <summary>
        /// Creates a new <see cref="ReadOnlyArrayView{T}"/> for a segment of this
        /// <see cref="ReadOnlyArrayView{T}"/>.
        /// </summary>
        /// <param name="start">The index of the first element of the slice range.</param>
        /// <param name="length">The number of elements in the slice range.</param>
        /// <returns>A <see cref="ReadOnlyArrayView{T}"/> that provides access to the segment
        /// of the current view starting at index <paramref name="start"/> and having
        /// <paramref name="length"/> number of elements.</returns>
        ///
        /// <exception cref="ArgumentException">The range defined by <paramref name="start"/> and
        /// <paramref name="length"/> is not within the bounds of this <see cref="ReadOnlyArrayView{T}"/>.</exception>
        public ReadOnlyArrayView<T> slice(int start, int length) => new ReadOnlyArrayView<T>(this, start, length);

        /// <summary>
        /// Creates a <see cref="ReadOnlyArrayView{T}"/> for an array.
        /// </summary>
        /// <param name="arr">The array. If this is null, the created instance will reference
        /// an empty array.</param>
        public static implicit operator ReadOnlyArrayView<T>(T[] arr) => new ReadOnlyArrayView<T>(arr);

        /// <summary>
        /// Returns an enumerator for this <see cref="ReadOnlyArrayView{T}"/> instance.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate over the list elements.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Returns an enumerator for this <see cref="ReadOnlyArrayView{T}"/> instance.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate over the list elements.</returns>
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Returns an enumerator for this <see cref="ReadOnlyArrayView{T}"/> instance.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate over the list elements.</returns>
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Returns the value of the element in this <see cref="ReadOnlyArrayView{T}"/> at the given index.
        /// </summary>
        T IReadOnlyList<T>.this[int index] => this[index];

        /// <summary>
        /// Returns the number of elements in this <see cref="ReadOnlyArrayView{T}"/> instance.
        /// </summary>
        int IReadOnlyCollection<T>.Count => m_length;

        /// <summary>
        /// Enumerator for a <see cref="ReadOnlyArrayView{T}"/> instance.
        /// </summary>
        public struct Enumerator : IEnumerator<T> {

            private T[] m_data;
            private int m_current;
            private int m_end;

            internal Enumerator(in ReadOnlyArrayView<T> view) {
                m_data = view.m_data ?? Array.Empty<T>();
                m_current = view.m_start - 1;
                m_end = view.m_start + view.m_length;
            }

            /// <summary>
            /// Disposes this iterator. (Does nothing)
            /// </summary>
            public void Dispose() { }

            /// <summary>
            /// Moves the iterator to the next element of the array.
            /// </summary>
            /// <returns>True if the end of the array has not been reached, false otherwise.</returns>
            public bool MoveNext() {
                if (m_current >= m_end)
                    return false;

                m_current++;
                return m_current < m_end;
            }

            /// <summary>
            /// Returns the value of the element at the current position.
            /// </summary>
            public T Current => m_data[m_current];

            /// <summary>
            /// Returns the value of the element at the current position.
            /// </summary>
            object IEnumerator.Current => (object)m_data[m_current]!;

            /// <summary>
            /// This method is not supported (throws NotImplementedException).
            /// </summary>
            void IEnumerator.Reset() => throw new NotImplementedException();

        }

    }

}
