using System;
using System.Text;

namespace Mariana.Common {

    /// <summary>
    /// A static array pool for use in high performance applications to minimize GC load
    /// by recycling array memory.
    /// </summary>
    /// <remarks>
    /// Arrays allocated from a static array pool cannot be released individually, the only way to
    /// release the allocated memory (to make it available for reuse) is by clearing the entire pool.
    /// For an array pool that allows freeing and resizing individual arrays, use the
    /// <see cref="DynamicArrayPool{T}"/> class.
    /// </remarks>
    /// <typeparam name="T">The type of the array elements.</typeparam>
    public sealed class StaticArrayPool<T> {

        private DynamicArray<T> m_storage;

        private DynamicArray<int> m_indices;

        /// <summary>
        /// Creates a new instance of <see cref="StaticArrayPool{T}"/>.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the array pool.</param>
        public StaticArrayPool(int initialCapacity = 0) {
            m_storage = new DynamicArray<T>(initialCapacity);
            m_indices.add(0);
        }

        /// <summary>
        /// Allocates a new array from the array pool.
        /// </summary>
        /// <param name="length">The length of the array.</param>
        /// <returns>A <see cref="StaticArrayPoolToken{T}"/> that can be passed to the
        /// <see cref="getSpan"/> method to access the allocated memory.</returns>
        ///
        /// <remarks>
        /// The allocated memory is guaranteed to be zero-initialized if <typeparamref name="T"/> is
        /// a reference type or a value type containing references.
        /// </remarks>
        public StaticArrayPoolToken<T> allocate(int length) => allocate(length, out _);

        /// <summary>
        /// Allocates a new array from the array pool.
        /// </summary>
        /// <param name="length">The length of the array.</param>
        /// <param name="span">An output parameter to which a span providing access to the allocated
        /// memory will be written.</param>
        /// <returns>A <see cref="StaticArrayPoolToken{T}"/> that can be passed to the
        /// <see cref="getSpan"/> method to access the allocated memory.</returns>
        ///
        /// <remarks>
        /// The allocated memory is guaranteed to be zero-initialized if <typeparamref name="T"/> is
        /// a reference type or a value type containing references.
        /// </remarks>
        public StaticArrayPoolToken<T> allocate(int length, out Span<T> span) {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (length == 0) {
                span = default;
                return default;
            }

            int itemId = m_indices.length;
            int storageStart = m_indices[itemId - 1];
            m_indices.add(storageStart + length);
            m_storage.addUninitialized(length);

            span = m_storage.asSpan(storageStart, length);
            return new StaticArrayPoolToken<T>(itemId);
        }

        /// <summary>
        /// Returns a span that can be used to access an array allocated with the
        /// <see cref="allocate(Int32)"/> method.
        /// </summary>
        /// <param name="token">A <see cref="StaticArrayPoolToken{T}"/> that was obtained from
        /// a call to <see cref="allocate(Int32)"/>. If this is the default value of
        /// <see cref="StaticArrayPoolToken{T}"/>, an empty span is returned.</param>
        /// <returns>A <see cref="Span{T}"/> that provides access to the allocated memory.</returns>
        /// <remarks>
        /// If the token was obtained from a different <see cref="StaticArrayPool{T}"/>
        /// instance, or if the <see cref="clear"/> method was called after the allocation of
        /// the array represented by <paramref name="token"/>, the returned span may refer
        /// to the memory of some other allocated array in the pool or an exception may be thrown.
        /// </remarks>
        public Span<T> getSpan(StaticArrayPoolToken<T> token) {
            int id = token.m_id;
            if (id == 0)
                return Span<T>.Empty;

            int start = m_indices[id - 1];
            return m_storage.asSpan(start, m_indices[id] - start);
        }

        /// <summary>
        /// Returns the length of an array allocated with the <see cref="allocate(Int32)"/> method.
        /// </summary>
        /// <param name="token">A <see cref="StaticArrayPoolToken{T}"/> that was obtained from
        /// a call to <see cref="allocate(Int32)"/>. If this is the default value of
        /// <see cref="StaticArrayPoolToken{T}"/>, zero is returned.</param>
        /// <returns>The length of the array represented by <paramref name="token"/>.</returns>
        /// <remarks>
        /// If the token was obtained from a different <see cref="StaticArrayPool{T}"/>
        /// instance, or if the <see cref="clear"/> method was called after the allocation of
        /// the array represented by <paramref name="token"/>, an unspecified value may be
        /// returned or an exception may be thrown.
        /// </remarks>
        public int getLength(StaticArrayPoolToken<T> token) {
            int id = token.m_id;
            return (id != 0) ? m_indices[id] - m_indices[id - 1] : 0;
        }

        /// <summary>
        /// Clears the array pool. This allows the pool memory to be reused for future
        /// allocations.
        /// </summary>
        /// <remarks>
        /// Calling this method will invalidate all existing tokens obtained from the pool.
        /// </remarks>
        public void clear() {
            m_storage.clear();
            m_indices.clear();
            m_indices.add(0);
        }

        /// <summary>
        /// Returns a string representation of an array in the pool.
        /// </summary>
        /// <param name="item">A <see cref="StaticArrayPoolToken{T}"/> instance representing the
        /// allocated array for which to return a string representation.</param>
        /// <returns>A string representation of the array.</returns>
        public string arrayToString(StaticArrayPoolToken<T> item) {
            Span<T> span = getSpan(item);
            var sb = new StringBuilder();
            sb.Append('[');

            for (int i = 0; i < span.Length; i++) {
                if (i != 0)
                    sb.Append(',').Append(' ');
                sb.Append(span[i]);
            }

            sb.Append(']');
            return sb.ToString();
        }

    }

    /// <summary>
    /// A token that represents a segment of memory allocated from a <see cref="StaticArrayPool{T}"/>.
    /// </summary>
    /// <remarks>
    /// To access the allocated memory represented by a <see cref="StaticArrayPoolToken{T}"/>
    /// instance, call <see cref="StaticArrayPool{T}.getSpan"/> on the array pool from which
    /// it was allocated. The default value of this type when passed to
    /// <see cref="StaticArrayPool{T}.getSpan"/> will always result in an empty span
    /// being returned.
    /// </remarks>
    public readonly struct StaticArrayPoolToken<T> {

        internal readonly int m_id;
        internal StaticArrayPoolToken(int id) => m_id = id;

        /// <summary>
        /// Returns true if this instance is equal to the default value of
        /// <see cref="StaticArrayPoolToken{T}"/> (which always gives an empty span
        /// when passed to <see cref="StaticArrayPool{T}.getSpan"/>).
        /// </summary>
        public bool isDefault => m_id == 0;

    }

}
