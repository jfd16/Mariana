using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Mariana.Common {

    /// <summary>
    /// An array pool for recycling arrays to minimize GC load in high performance applications.
    /// </summary>
    public sealed class DynamicArrayPool<T> {

        private struct Bucket {

            public readonly int segmentSize;

            private DynamicArray<T> m_storage;
            private DynamicArray<int> m_freeList;

            public Bucket(int segmentSize) : this() {
                this.segmentSize = segmentSize;
            }

            public Span<T> getSpan(int index, int length) => m_storage.asSpan(index, length);

            public T getItemAt(int index) => m_storage[index];

            public void setItemAt(int index, T value) => m_storage[index] = value;

            public int allocNewSegment() {
                int index;
                if (m_freeList.length > 0) {
                    index = m_freeList[m_freeList.length - 1];
                    m_freeList.removeLast();
                }
                else {
                    index = m_storage.length;
                    m_storage.addDefault(segmentSize);
                }

                return index;
            }

            public void freeSegment(int index) {
                if (index + segmentSize == m_storage.length) {
                    m_storage.removeRange(index, segmentSize);
                }
                else {
                    m_storage.asSpan(index, segmentSize).Clear();
                    m_freeList.add(index);
                }
            }

            public void clear() {
                m_storage.clear();
                m_freeList.clear();
            }

        }

        private struct Segment {
            public int bucketId;
            public int index;
            public int length;
        }

        private DynamicArray<Bucket> m_buckets;
        private DynamicArray<Segment> m_segments;
        private DynamicArray<int> m_freeTokens;

        /// <summary>
        /// Creates a new instance of <see cref="DynamicArrayPool{T}"/>.
        /// </summary>
        public DynamicArrayPool() {}

        /// <summary>
        /// Returns a span that can be used to access an array allocated with the
        /// <see cref="allocate(Int32)"/> method.
        /// </summary>
        /// <param name="token">A <see cref="DynamicArrayPoolToken{T}"/> that was obtained from
        /// a call to <see cref="allocate(Int32)"/>. If this is the default value of
        /// <see cref="DynamicArrayPoolToken{T}"/>, an empty span is returned.</param>
        /// <returns>A <see cref="Span{T}"/> that provides access to the allocated memory.</returns>
        /// <remarks>
        /// If the token was obtained from a different <see cref="DynamicArrayPoolToken{T}"/>
        /// instance, or if the memory backed by the token was released by calling <see cref="free"/>
        /// or <see cref="clear"/>, the returned span may refer to the memory of some other allocated
        /// array in the pool or an exception may be thrown.
        /// </remarks>
        public Span<T> getSpan(DynamicArrayPoolToken<T> token) {
            int id = token.m_id;
            if (id == 0)
                return Span<T>.Empty;

            ref Segment segment = ref m_segments[id - 1];
            return m_buckets[segment.bucketId].getSpan(segment.index, segment.length);
        }

        /// <summary>
        /// Returns the length of an array allocated with the <see cref="allocate(Int32)"/> method.
        /// </summary>
        /// <param name="token">A <see cref="DynamicArrayPoolToken{T}"/> that was obtained from
        /// a call to <see cref="allocate(Int32)"/>. If this is the default value of
        /// <see cref="DynamicArrayPoolToken{T}"/>, zero is returned.</param>
        /// <returns>The length of the array represented by <paramref name="token"/>.</returns>
        /// <remarks>
        /// If the token was obtained from a different <see cref="DynamicArrayPoolToken{T}"/>
        /// instance, or if the memory backed by the token was released by calling
        /// <see cref="free"/> or <see cref="clear"/>, an unspecified value may be returned
        /// or an exception may be thrown.
        /// </remarks>
        public int getLength(DynamicArrayPoolToken<T> token) {
            int id = token.m_id;
            return (id != 0) ? m_segments[id - 1].length : 0;
        }

        /// <summary>
        /// Returns the bucket index for an array of the given length.
        /// </summary>
        /// <param name="length">The length of the array to allocate.</param>
        /// <returns>The index of the bucket in which the array should be allocated.</returns>
        private int _getBucketIndex(int length) {
            int index = 0;
            while ((1 << index) < length)
                index++;

            int bucketCount = m_buckets.length;
            while (index >= bucketCount) {
                m_buckets.add(new Bucket(1 << bucketCount));
                bucketCount++;
            }

            return index;
        }

        /// <summary>
        /// Allocates an array of the given length from the pool.
        /// </summary>
        /// <returns>A <see cref="DynamicArrayPoolToken{T}"/> instance representing the allocated array.</returns>
        /// <param name="length">The size of the array required.</param>
        /// <param name="span">An output parameter into which a <see cref="Span{T}"/> that provides
        /// access to the allocated memory will be written.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than 0.</exception>
        public DynamicArrayPoolToken<T> allocate(int length, out Span<T> span) {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            int tokenId;
            if (m_freeTokens.length != 0) {
                tokenId = m_freeTokens[m_freeTokens.length - 1];
                m_freeTokens.removeLast();
            }
            else {
                tokenId = m_segments.length + 1;
                m_segments.add(new Segment());
            }

            int bucketId = _getBucketIndex(length);
            ref Segment segment = ref m_segments[tokenId - 1];
            ref Bucket bucket = ref m_buckets[bucketId];

            segment.bucketId = bucketId;
            segment.index = bucket.allocNewSegment();
            segment.length = length;

            span = bucket.getSpan(segment.index, length);
            return new DynamicArrayPoolToken<T>(tokenId);
        }

        /// <summary>
        /// Allocates an array of the given length from the pool.
        /// </summary>
        /// <returns>A <see cref="DynamicArrayPoolToken{T}"/> instance representing the allocated array.</returns>
        /// <param name="length">The size of the array required.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than 0.</exception>
        public DynamicArrayPoolToken<T> allocate(int length) => allocate(length, out _);

        /// <summary>
        /// Releases an array obtained from this <see cref="DynamicArrayPool{T}"/> instance so that
        /// its memory can be reused.
        /// </summary>
        /// <param name="token">A <see cref="DynamicArrayPoolToken{T}"/> instance representing the
        /// array whose memory is to be released.</param>
        /// <exception cref="ArgumentException"><paramref name="token"/> is the default value of
        /// <see cref="DynamicArrayPoolToken{T}"/>.</exception>
        public void free(DynamicArrayPoolToken<T> token) {
            if (token.m_id == 0)
                throw new ArgumentException("Token must not be the default value.", nameof(token));

            ref Segment segment = ref m_segments[token.m_id - 1];
            if (segment.bucketId == -1)
                return;

            m_buckets[segment.bucketId].freeSegment(segment.index);
            segment.bucketId = -1;
            m_freeTokens.add(token.m_id);
        }

        /// <summary>
        /// Resizes an already allocated array.
        /// </summary>
        /// <param name="token">A <see cref="DynamicArrayPoolToken{T}"/> instance representing the
        /// array to be resized.</param>
        /// <param name="newLength">The new length of the array.</param>
        /// <param name="span">An output parameter into which a <see cref="Span{T}"/> that provides
        /// access to the memory of the resized array will be written.</param>
        /// <exception cref="ArgumentException"><paramref name="token"/> is the default value of
        /// <see cref="DynamicArrayPoolToken{T}"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="newLength"/> is less than 0.</exception>
        public void resize(DynamicArrayPoolToken<T> token, int newLength, out Span<T> span) {
            if (token.m_id == 0)
                throw new ArgumentException("Token must not be the default value.", nameof(token));

            if (newLength < 0)
                throw new ArgumentOutOfRangeException(nameof(newLength));

            ref Segment segment = ref m_segments[token.m_id - 1];
            ref Bucket bucket = ref m_buckets[segment.bucketId];

            if (newLength == segment.length) {
                span = bucket.getSpan(segment.index, segment.length);
                return;
            }

            if (newLength < bucket.segmentSize && newLength >= (bucket.segmentSize >> 1)) {
                if (newLength < segment.length) {
                    span = bucket.getSpan(segment.index, segment.length);
                    span.Slice(newLength).Clear();
                    span = span.Slice(0, newLength);
                }
                else {
                    span = bucket.getSpan(segment.index, newLength);
                }

                segment.length = newLength;
            }
            else {
                int newBucketId = _getBucketIndex(newLength);
                ref Bucket newBucket = ref m_buckets[newBucketId];

                int newIndex = newBucket.allocNewSegment();
                span = newBucket.getSpan(newIndex, newLength);

                Span<T> data = bucket.getSpan(segment.index, Math.Min(segment.length, newLength));
                data.CopyTo(span.Slice(0, data.Length));

                bucket.freeSegment(segment.index);
                segment.bucketId = newBucketId;
                segment.index = newIndex;
                segment.length = newLength;
            }
        }

        /// <summary>
        /// Resizes an already allocated array.
        /// </summary>
        /// <param name="token">A <see cref="DynamicArrayPoolToken{T}"/> instance representing the
        /// array to be resized.</param>
        /// <param name="newLength">The new length of the array.</param>
        /// <exception cref="ArgumentException"><paramref name="token"/> is the default value of
        /// <see cref="DynamicArrayPoolToken{T}"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="newLength"/> is less than 0.</exception>
        public void resize(DynamicArrayPoolToken<T> token, int newLength) => resize(token, newLength, out _);

        /// <summary>
        /// Appends a value to an array allocated from this array pool. This increases the
        /// length of the array by one.
        /// </summary>
        /// <param name="token">A <see cref="DynamicArrayPoolToken{T}"/> instance representing the
        /// array to which to append <paramref name="value"/>.</param>
        /// <param name="value">The value to append to the array represented by <paramref name="token"/>.</param>
        /// <exception cref="ArgumentException"><paramref name="token"/> is the default value of
        /// <see cref="DynamicArrayPoolToken{T}"/>.</exception>
        public void append(DynamicArrayPoolToken<T> token, T value) {
            if (token.m_id == 0)
                throw new ArgumentException("Token must not be the default value.", nameof(token));

            ref Segment segment = ref m_segments[token.m_id - 1];
            ref Bucket bucket = ref m_buckets[segment.bucketId];
            int length = segment.length;

            if (length < bucket.segmentSize) {
                segment.length = length + 1;
                bucket.setItemAt(segment.index + length, value);
            }
            else {
                resize(token, length + 1, out Span<T> span);
                span[length] = value;
            }
        }

        /// <summary>
        /// Releases the memory of all arrays allocated from this array pool so that it
        /// can be reused for future allocations. This will invalidate all existing tokens
        /// obtained from this pool.
        /// </summary>
        public void clear() {
            for (int i = 0; i < m_buckets.length; i++)
                m_buckets[i].clear();

            m_segments.clear();
            m_freeTokens.clear();
        }

        /// <summary>
        /// Returns a string representation of an array in the pool.
        /// </summary>
        /// <param name="token">A <see cref="DynamicArrayPoolToken{T}"/> instance representing the
        /// allocated array for which to return a string representation.</param>
        /// <returns>A string representation of the array.</returns>
        public string arrayToString(DynamicArrayPoolToken<T> token) {
            Span<T> span = getSpan(token);
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
    /// A token that represents a segment of memory allocated from a <see cref="DynamicArrayPool{T}"/>.
    /// </summary>
    /// <remarks>
    /// To access the allocated memory represented by a <see cref="DynamicArrayPool{T}"/>
    /// instance, call <see cref="DynamicArrayPool{T}.getSpan"/> on the array pool from which
    /// it was allocated. The default value of this type when passed to
    /// <see cref="DynamicArrayPool{T}.getSpan"/> will always result in an empty span
    /// being returned.
    /// </remarks>
    public readonly struct DynamicArrayPoolToken<T> {

        internal readonly int m_id;
        internal DynamicArrayPoolToken(int id) => m_id = id;

        /// <summary>
        /// Returns true if this instance is equal to the default value of
        /// <see cref="DynamicArrayPoolToken{T}"/> (which always gives an empty span
        /// when passed to <see cref="DynamicArrayPool{T}.getSpan"/>).
        /// </summary>
        public bool isDefault => m_id == 0;

    }

}
