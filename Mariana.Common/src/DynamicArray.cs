using System;

namespace Mariana.Common {

    /// <summary>
    /// A struct-based variable-size array.
    /// </summary>
    /// <typeparam name="T">The element type of the array.</typeparam>
    ///
    /// <remarks>
    /// This is a value type and is intended for internal use within a class or method. It should
    /// not be returned from a function or passed as a by-value parameter, as having copies of a
    /// <see cref="DynamicArray{T}"/> may result in unintended behaviour. Most use cases of this
    /// are for maintaining internal lists in a class which are not exposed to outside code, or
    /// for building arrays to be returned from functions.
    /// </remarks>
    public struct DynamicArray<T> {

        private T[] m_array;
        private int m_length;

        /// <summary>
        /// Creates a new <see cref="DynamicArray{T}"/> of the given initial capacity.
        /// </summary>
        ///
        /// <param name="initialCapacity">The initial capacity.</param>
        /// <param name="fillWithDefault">If this is true, fill all elements in the array up to
        /// <paramref name="initialCapacity"/> with the default value of <typeparamref name="T"/>.
        /// This will set the size of the array to <paramref name="initialCapacity"/>. Otherwise, the
        /// initial size of the array is zero.</param>
        public DynamicArray(int initialCapacity, bool fillWithDefault = false) {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            m_array = null;
            m_length = 0;

            if (initialCapacity != 0) {
                m_array = new T[initialCapacity];
                if (fillWithDefault)
                    m_length = initialCapacity;
            }
        }

        /// <summary>
        /// Creates a new <see cref="DynamicArray{T}"/> with the given underlying array.
        /// </summary>
        /// <param name="underlyingArray">The array that will be used as the storage for this
        /// <see cref="DynamicArray{T}"/>. The created instance holds a reference to the array,
        /// no copy is made. The reference to this array will be lost if a reallocation is
        /// triggered by the <see cref="setCapacity"/>, <see cref="add(T)"/>, <see cref="add(in T)"/> or
        /// <see cref="addDefault"/> methods.</param>
        /// <param name="initialSize">The initial size of the created <see cref="DynamicArray{T}"/>.
        /// This must not be negative, or greater than the length of <paramref name="underlyingArray"/>.
        /// </param>
        public DynamicArray(T[] underlyingArray, int initialSize) {
            if (underlyingArray == null)
                throw new ArgumentNullException(nameof(underlyingArray));
            if ((uint)initialSize > (uint)underlyingArray.Length)
                throw new ArgumentOutOfRangeException(nameof(initialSize));

            m_array = underlyingArray;
            m_length = initialSize;
        }

        /// <summary>
        /// Changes the capacity of the array.
        /// </summary>
        /// <param name="newCapacity">The new capacity. If this is less than the current size of the
        /// array, does nothing.</param>
        public void setCapacity(int newCapacity) {
            if (newCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(newCapacity));
            if (newCapacity < m_length || newCapacity == m_array.Length)
                return;
            _setCapacityInternal(newCapacity);
        }

        /// <summary>
        /// Changes the capacity of the array, if the given capacity is greater than the current
        /// capacity.
        /// </summary>
        /// <param name="capacity">The new capacity of the array. If this is not greater than the
        /// current capacity, this method does nothing.</param>
        public void ensureCapacity(int capacity) {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (capacity == 0 || (m_array != null && m_array.Length > capacity))
                return;
            _setCapacityInternal(capacity);
        }

        /// <summary>
        /// Removes all elements from the array.
        /// </summary>
        public void clear() {
            if (m_array != null && m_length != 0)
                asSpan().Clear();
            m_length = 0;
        }

        /// <summary>
        /// Adds an element to the array.
        /// </summary>
        /// <param name="item">The element to add.</param>
        public void add(T item) {
            if (m_array == null) {
                _setCapacityInternal(4);
            }
            else if (m_array.Length == m_length) {
                int newCapacity = DataStructureUtil.getNextArraySize(m_length, m_length + 1);
                _setCapacityInternal(newCapacity);
            }
            m_array[m_length++] = item;
        }

        /// <summary>
        /// Adds an element to the array.
        /// </summary>
        /// <param name="item">The element to add.</param>
        public void add(in T item) {
            if (m_array == null) {
                _setCapacityInternal(4);
            }
            else if (m_array.Length == m_length) {
                int newCapacity = DataStructureUtil.getNextArraySize(m_length, m_length + 1);
                _setCapacityInternal(newCapacity);
            }
            m_array[m_length++] = item;
        }

        /// <summary>
        /// Adds the given number of elements to the array, each set to the default value of
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <param name="count">The number of elements to add.</param>
        public void addDefault(int count) {
            if (count == 0)
                return;

            if (m_array == null) {
                _setCapacityInternal(count);
            }
            else if (m_array.Length - m_length < count) {
                int newCapacity = DataStructureUtil.getNextArraySize(m_length, m_length + count);
                _setCapacityInternal(newCapacity);
            }

            m_length += count;
        }

        /// <summary>
        /// Removes all existing elements from the array, then adds the given number of elements
        /// each set to the default value of <typeparamref name="T"/>.
        /// </summary>
        /// <param name="count">The number of elements to add, after removing the existing
        /// elements.</param>
        public void clearAndAddDefault(int count) {
            if (m_array == null || count > m_array.Length)
                m_array = new T[DataStructureUtil.getNextArraySize(m_length, count)];
            else
                asSpan().Clear();

            m_length = count;
        }

        /// <summary>
        /// Removes a range of elements from the array.
        /// </summary>
        /// <param name="start">The index of the first element in the range to be removed.</param>
        /// <param name="count">The number of elements to remove.</param>
        public void removeRange(int start, int count) {
            if ((uint)start > (uint)m_length)
                throw new ArgumentOutOfRangeException(nameof(start));
            if ((uint)count > (uint)(m_length - start))
                throw new ArgumentOutOfRangeException(nameof(count));

            if (m_array == null || m_length == 0)
                return;

            if (start + count != m_length)
                (new ReadOnlySpan<T>(m_array, start + count, m_length - start - count)).CopyTo(m_array.AsSpan(start));

            if (count == 1)
                m_array[m_length - 1] = default(T);
            else
                m_array.AsSpan(m_length - count, count).Clear();

            m_length -= count;
        }

        /// <summary>
        /// Removes the last element from the array.
        /// </summary>
        /// <remarks>
        /// This can be used as a "pop" function when using a <see cref="DynamicArray{T}"/> as a
        /// stack.
        /// </remarks>
        public void removeLast() => m_array[--m_length] = default(T);

        /// <summary>
        /// Gets a span that provides read-only access to the underlying memory of this array.
        /// </summary>
        ///
        /// <remarks>
        /// The size of the returned span will be set to the current array size. If the array size is
        /// reduced (for example, by calling <see cref="removeRange"/>), the values of elements accessed
        /// through the returned span at indices greater than or equal to the new array size are undefined.
        /// If a reallocation is triggered (for instance, with a call to <see cref="add(T)"/> or
        /// <see cref="setCapacity"/>), the span may no longer refer to the memory of the dynamic
        /// array.
        /// </remarks>
        public Span<T> asSpan() => new Span<T>(m_array, 0, m_length);

        /// <summary>
        /// Gets a span that provides read-only access to the underlying memory of a segment of this array.
        /// </summary>
        /// <param name="start">The index of the first element of the segment of the array that
        /// is to be accessible through the returned span.</param>
        /// <param name="length">The number of elements in the segment that should be accessible
        /// through the returned span.</param>
        ///
        /// <remarks>
        /// The size of the returned span will be set to the current array size. If the array size is
        /// reduced (for example, by calling <see cref="removeRange"/>), the values of elements accessed
        /// through the returned span at indices greater than or equal to the new array size are undefined.
        /// If a reallocation is triggered (for instance, with a call to <see cref="add(T)"/> or
        /// <see cref="setCapacity"/>), the span may no longer refer to the memory of the dynamic
        /// array.
        /// </remarks>
        public Span<T> asSpan(int start, int length) => new Span<T>(m_array, start, length);

        /// <summary>
        /// Gets the array used as the underlying storage for this <see cref="DynamicArray{T}"/> instance.
        /// </summary>
        ///
        /// <remarks>
        /// <para>This method should only be used when using a <see cref="DynamicArray{T}"/>
        /// with legacy APIs that don't accept spans.</para>
        /// <para>The underlying array may change when methods such as <see cref="add(T)"/> and
        /// <see cref="setCapacity"/> are called. Any assignment made to an element of this
        /// array (of any field of it, if <typeparamref name="T"/> is a value type) after that will
        /// not be visible in this <see cref="DynamicArray{T}"/>.</para>
        /// <para>If the array is empty, the value of this property may be null.</para>
        /// <para>The length of this array (if not null) is equal to the capacity of the dynamic
        /// array, which may be greater than its current size. However, any elements beyond the
        /// current dynamic array size must not be modified.</para>
        /// </remarks>
        public T[] getUnderlyingArray() => m_array;

        /// <summary>
        /// Gets a <see cref="ReadOnlyArrayView{T}"/> instance providing read-only access to
        /// the elements of this array.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{T}"/> instance providing read-only access to
        /// the array elements.</returns>
        /// <remarks>
        /// The size of the returned <see cref="ReadOnlyArrayView{T}"/> will be set to the current
        /// array size. If the array size is reduced (for example, by calling <see cref="removeRange"/>),
        /// the values of elements accessed through the returned <see cref="ReadOnlyArrayView{T}"/> at
        /// indices greater than or equal to the new array size are undefined. If a reallocation is
        /// triggered (for instance, with a call to <see cref="add(T)"/> or <see cref="setCapacity"/>),
        /// the returned <see cref="ReadOnlyArrayView{T}"/> may no longer refer to the memory of
        /// the dynamic array.
        /// </remarks>
        public ReadOnlyArrayView<T> asReadOnlyArrayView() =>
            (m_length == 0) ? ReadOnlyArrayView<T>.empty : new ReadOnlyArrayView<T>(m_array, 0, m_length);

        /// <summary>
        /// Gets a reference to the element in the array at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        ///
        /// <remarks>
        /// <para>The underlying memory of this array may change when methods such as <see cref="add(T)"/>
        /// and <see cref="setCapacity"/> are called. Any assignment made to a reference returned by
        /// this indexer after a change to the underlying memory will not be visible in the dynamic
        /// array.</para>
        /// <para>If <paramref name="index"/> is out of bounds, this indexer may or may not throw
        /// an exception.</para>
        /// </remarks>
        public ref T this[int index] => ref m_array[index];

        /// <summary>
        /// The current size of the array.
        /// </summary>
        public int length => m_length;

        /// <summary>
        /// Gets an array containing the elements of this <see cref="DynamicArray{T}"/>.
        /// </summary>
        ///
        /// <param name="alwaysCopy">If this is set to true, this method always allocates a new array
        /// and copies the current array elements into it, regardless of size. Otherwise, this
        /// method may return the underlying array of this <see cref="DynamicArray{T}"/>, if its
        /// length is equal to the value of <see cref="length"/>.</param>
        /// <returns>An array containing the elements of this <see cref="DynamicArray{T}"/>.</returns>
        public T[] toArray(bool alwaysCopy = false) {
            if (m_length == 0)
                return Array.Empty<T>();
            if (m_length == m_array.Length && !alwaysCopy)
                return m_array;

            return asSpan().ToArray();
        }

        private void _setCapacityInternal(int newCapacity) {
            T[] newArray = new T[newCapacity];
            if (m_array != null)
                (new ReadOnlySpan<T>(m_array, 0, m_length)).CopyTo(newArray);
            m_array = newArray;
        }

    }

}
