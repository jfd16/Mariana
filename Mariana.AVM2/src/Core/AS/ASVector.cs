using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A dense, strongly-typed array.
    /// </summary>
    /// <typeparam name="T">The type of the elements of the Vector.</typeparam>
    ///
    /// <remarks>
    /// <para>
    /// <list type="bullet">
    /// <item><description>
    /// A Vector, unlike an Array can store elements of only a single type, which is given as
    /// the type argument to the Vector class.
    /// </description></item>
    /// <item><description>A Vector is a dense array, meaning that no empty space can exist between two
    /// elements. For example, if a Vector has a length 3 (indices 0, 1 and 2 assigned), the index
    /// 3 must be assigned a value before any index greater than that can be assigned.</description></item>
    /// <item><description>
    /// An attempt to get the value of an element in a Vector at an index greater than the highest
    /// index (one less than the length of the Vector) results in a <see cref="ASRangeError"/>
    /// being thrown, where as in Arrays such accesses return undefined. Assigning a value to an
    /// index greater than the length of the Vector will also throw a RangeError; however, if the
    /// vector is a fixed-length vector, assigning to the index equal to the length will also
    /// throw an error (as that would change the length of the Vector).
    /// </description></item>
    /// <item><description>
    /// Vector instantiations can be loaded into the AVM2 only if the element type is one of the
    /// following: <see cref="Int32"/>, <see cref="UInt32"/>, <see cref="Double"/>,
    /// <see cref="String"/>, <see cref="Boolean"/> or any type deriving from
    /// <see cref="ASObject"/> except boxed primitive types. In particular, the
    /// <see cref="ASAny"/> type cannot be used. (<c>Vector.&lt;*&gt;</c> in the AVM2
    /// corresponds to <see cref="ASVectorAny"/>.)
    /// </description></item>
    /// </list>
    /// </para>
    /// </remarks>
    [AVM2ExportClass(
        name = "Vector",
        nsUri = "__AS3__.vec",
        hasIndexLookupMethods = true
    )]
    [AVM2ExportClassInternal(
        // ClassTag.VECTOR is added to instantiations by the class loader. We don't want it on the
        // Vector class itself!
        // tag = ClassTag.VECTOR,
        hidesInheritedTraits = true
    )]
    public sealed class ASVector<T> : ASVectorAny {

        // Initial allocated capacity of a Vector
        private const int INIT_CAPACITY = 4;

        private T[] m_data;
        private int m_length;
        private bool m_fixed;

        /// <summary>
        /// Creates a new Vector of the specified length.
        /// </summary>
        ///
        /// <param name="length">The initial length of the vector. If this is zero or negative, the
        /// default initial size is used.</param>
        /// <param name="isFixed">A Boolean value indicating whether the vector has a fixed length.
        /// Any attempt to add items to a fixed-length vector beyond the specified length (or change
        /// its <see cref="length"/> property directly) will result in an error.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1005: <see cref="length"/> is not a positive integer.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// All elements in the array will be initialized to their default values.
        /// </remarks>
        [AVM2ExportTrait]
        public ASVector(int length = 0, bool isFixed = false) {
            if (length < 0)
                throw ErrorHelper.createError(ErrorCode.ARRAY_LENGTH_NOT_POSITIVE_INTEGER, length);
            m_data = new T[Math.Max(length, INIT_CAPACITY)];
            m_length = length;
            m_fixed = isFixed;
        }

        /// <summary>
        /// Creates a new Vector with data from the given span.
        /// </summary>
        ///
        /// <param name="data">The span containing the elements of the <see cref="ASVector{T}"/> instance.</param>
        /// <param name="isFixed">A Boolean value indicating whether the vector has a fixed length. Any
        /// attempt to change the length of a fixed-length vector will result in an error.</param>
        public ASVector(ReadOnlySpan<T> data, bool isFixed = false) : this(data.Length, isFixed) {
            data.CopyTo(m_data);
        }

        /// <summary>
        /// Creates a new Vector with data from the given span.
        /// </summary>
        ///
        /// <param name="data">The span containing the elements of the <see cref="ASVector{T}"/> instance.</param>
        /// <param name="isFixed">A Boolean value indicating whether the vector has a fixed length. Any
        /// attempt to change the length of a fixed-length vector will result in an error.</param>
        public static ASVector<T> fromSpan(ReadOnlySpan<T> data, bool isFixed = false) =>
            new ASVector<T>(data, isFixed);

        /// <summary>
        /// Creates a new Vector with data from the given span, converting the span elements to
        /// the vector's element type.
        /// </summary>
        ///
        /// <param name="data">The span containing the elements of the <see cref="ASVector{T}"/> instance.</param>
        /// <param name="isFixed">A Boolean value indicating whether the vector has a fixed length. Any
        /// attempt to change the length of a fixed-length vector will result in an error.</param>
        /// <typeparam name="U">The type of the elements in <paramref name="data"/>.</typeparam>
        public static ASVector<T> fromSpan<U>(ReadOnlySpan<U> data, bool isFixed = false) {
            var vec = new ASVector<T>(data.Length, isFixed);
            GenericTypeConverter<U, T>.instance.convertSpan(data, vec.asSpan());
            return vec;
        }

        /// <summary>
        /// Creates a new Vector with data from the given span.
        /// </summary>
        ///
        /// <param name="data">The span containing the elements of the <see cref="ASVector{T}"/> instance.</param>
        /// <param name="isFixed">A Boolean value indicating whether the vector has a fixed length. Any
        /// attempt to change the length of a fixed-length vector will result in an error.</param>
        public static ASVector<T> fromSpan(Span<T> data, bool isFixed = false) => fromSpan((ReadOnlySpan<T>)data, isFixed);

        /// <summary>
        /// Creates a new Vector with data from the given span, converting the span elements to
        /// the vector's element type.
        /// </summary>
        ///
        /// <param name="data">The span containing the elements of the <see cref="ASVector{T}"/> instance.</param>
        /// <param name="isFixed">A Boolean value indicating whether the vector has a fixed length. Any
        /// attempt to change the length of a fixed-length vector will result in an error.</param>
        /// <typeparam name="U">The type of the elements in <paramref name="data"/>.</typeparam>
        public static ASVector<T> fromSpan<U>(Span<U> data, bool isFixed = false) => fromSpan((ReadOnlySpan<U>)data, isFixed);

        /// <summary>
        /// Creates a new Vector with data from the given array.
        /// </summary>
        ///
        /// <param name="data">The array containing the elements of the <see cref="ASVector{T}"/> instance
        /// to be created.</param>
        /// <param name="isFixed">A Boolean value indicating whether the vector has a fixed length. Any
        /// attempt to change the length of a fixed-length vector will result in an error.</param>
        public static ASVector<T> fromTypedArray(T[] data, bool isFixed = false) => fromSpan(new ReadOnlySpan<T>(data), isFixed);

        /// <summary>
        /// Creates a new Vector with data from the given typed array, converting the elements to
        /// the vector's element type.
        /// </summary>
        ///
        /// <param name="data">The array containing the elements of the <see cref="ASVector{T}"/> instance
        /// to be created.</param>
        /// <param name="isFixed">A Boolean value indicating whether the vector has a fixed length. Any
        /// attempt to change the length of a fixed-length vector will result in an error.</param>
        /// <typeparam name="U">The type of the elements in <paramref name="data"/>.</typeparam>
        public static ASVector<T> fromTypedArray<U>(U[] data, bool isFixed = false) {
            var vec = new ASVector<T>(data.Length, isFixed);
            GenericTypeConverter<U, T>.instance.convertSpan(data, vec.asSpan());
            return vec;
        }

        /// <summary>
        /// Creates a new Vector with data from the given enumerable.
        /// </summary>
        ///
        /// <param name="data">An <see cref="IEnumerable{T}"/> instance that enumerates the elements
        /// of the <see cref="ASVector{T}"/> instance.</param>
        /// <param name="isFixed">A Boolean value indicating whether the vector has a fixed length. Any
        /// attempt to change the length of a fixed-length vector will result in an error.</param>
        public static ASVector<T> fromEnumerable(IEnumerable<T> data, bool isFixed = false) {
            var vec = new ASVector<T>();
            foreach (T item in data)
                vec.push(item);
            vec.@fixed = isFixed;
            return vec;
        }

        /// <summary>
        /// Creates a new Vector with data from the given enumerable, converting the span elements to
        /// the vector's element type.
        /// </summary>
        ///
        /// <param name="data">An <see cref="IEnumerable{U}"/> instance that enumerates the elements
        /// of the <see cref="ASVector{T}"/> instance.</param>
        /// <param name="isFixed">A Boolean value indicating whether the vector has a fixed length. Any
        /// attempt to change the length of a fixed-length vector will result in an error.</param>
        /// <typeparam name="U">The type of the elements in <paramref name="data"/>.</typeparam>
        public static ASVector<T> fromEnumerable<U>(IEnumerable<U> data, bool isFixed = false) {
            var vec = new ASVector<T>(0, isFixed);
            var converter = GenericTypeConverter<U, T>.instance;
            foreach (U item in data)
                vec.push(converter.convert(item));
            return vec;
        }

        /// <summary>
        /// Converts an object into a Vector.
        /// </summary>
        /// <param name="obj">The object to convert to a Vector.</param>
        /// <returns>The given object converted to a Vector.</returns>
        ///
        /// <remarks>
        /// If <paramref name="obj"/> is a Vector of the same element type, the same object is
        /// returned (no copy is made). If it is an Array or a Vector of a different element type, a
        /// new Vector is created having the same length as that of <paramref name="obj"/> and
        /// elements from it are converted to the target element type <typeparamref name="T"/> and
        /// written to the new vector. For any other object, an error is thrown.
        /// </remarks>
        public static new ASVector<T> fromObject(ASObject obj) {
            ASVector<T> vecOfT;

            if (obj is ASVector<T> vectorOfSameType) {
                vecOfT = vectorOfSameType;
            }
            else if (obj is ASVectorAny vector) {
                vecOfT = new ASVector<T>(vector.length);
                vector.copyToSpan(0, vecOfT.asSpan());
            }
            else if (obj is ASArray array) {
                vecOfT = new ASVector<T>((int)array.length);
                array.copyToSpan(0, vecOfT.asSpan());
            }
            else {
                throw ErrorHelper.createCastError(obj, typeof(ASVector<T>));
            }

            return vecOfT;
        }

        /// <summary>
        /// Converts a vector of another element type to a vector of the element type
        /// <typeparamref name="T"/>.
        /// </summary>
        /// <param name="vector">The vector of the source element type.</param>
        /// <typeparam name="U">The source element type.</typeparam>
        /// <returns>A vector containing the elements from the source vector converted to the target
        /// type.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1034: The source vector is null, or an error occurred while converting one of the
        /// source elements to the target type.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the source element type <typeparamref name="U"/> is the same as
        /// <typeparamref name="T"/>, the source Vector instance is returned; no copy is made.
        /// Otherwise, a new Vector is created having the same length as the source vector and
        /// elements from it are converted to the target element type <typeparamref name="T"/> and
        /// written to the new vector.
        /// </remarks>
        public static ASVector<T> fromOtherVector<U>(ASVector<U> vector) {
            ASVector<T> vecOfT;

            if (vector is ASVector<T> vectorOfSameType) {
                vecOfT = vectorOfSameType;
            }
            else if (vector != null) {
                vecOfT = new ASVector<T>(vector.length);
                vector.copyToSpan(0, vecOfT.asSpan());
            }
            else {
                throw ErrorHelper.createCastError(vector, typeof(ASVector<T>));
            }

            return vecOfT;
        }

        /// <summary>
        /// Gets or sets the length of the Vector.
        /// </summary>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1005: This property is set to a negative value.</description></item>
        /// <item><description>RangeError #1126: This property is changed on a fixed-length vector.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If this length is set to a new value that is less than the existing value, the Vector will
        /// be truncated to the new length. If it is set to a value greater than the existing value,
        /// the Vector will be filled with elements of the default value of the element type to make
        /// its length equal to the new length.
        /// </remarks>
        [AVM2ExportTrait]
        public new int length {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_length;
            set {
                if (value == m_length)
                    return;
                if (m_fixed)
                    throw ErrorHelper.createError(ErrorCode.VECTOR_FIXED_LENGTH_CHANGE);
                if (value < 0)
                    throw ErrorHelper.createError(ErrorCode.ARRAY_LENGTH_NOT_POSITIVE_INTEGER);

                DataStructureUtil.resizeArray(ref m_data, m_length, value, exact: true);
                m_length = value;
            }
        }

        /// <summary>
        /// A Boolean value indicating whether the length of the vector is fixed. If the vector's
        /// length is fixed, changing it will throw an error.
        /// </summary>
        [AVM2ExportTrait]
        public new bool @fixed {
            get => m_fixed;
            set => m_fixed = value;
        }

        /// <summary>
        /// Returns a copy of the <see cref="ASVector{T}"/> instance.
        /// </summary>
        /// <returns>A copy of the <see cref="ASVector{T}"/> instance.</returns>
        public ASVector<T> clone() {
            ASVector<T> copy = new ASVector<T>(m_length, m_fixed);
            _internalArrayCopy(m_data, 0, copy.m_data, 0, m_length);
            return copy;
        }

        /// <summary>
        /// Returns a span that can be used to access the data of this Vector instance.
        /// </summary>
        /// <returns>A span that can be used to access the data of this Vector instance.</returns>
        /// <remarks>
        /// The returned span will refer to the underlying data of the vector as long as the length
        /// of the vector does not change. If this vector is not a fixed-size vector and a method is called
        /// that changes the vector's length, it is not guaranteed that the span will continue to refer
        /// to the vector's data as the vector may be given a new data store.
        /// </remarks>
        public Span<T> asSpan() => new Span<T>(m_data, 0, m_length);

        /// <summary>
        /// Returns a span that can be used to access the data of the segment of this Vector instance
        /// from the given index until the end.
        /// </summary>
        /// <param name="start">The index from which the span should start.</param>
        /// <returns>A span that can be used to access the data of the segment of this Vector
        /// instance starting at the <paramref name="start"/> index and ending at the last
        /// element of the vector.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>ArgumentError #10061: <paramref name="start"/> is greater than the length of the vector.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// The returned span will refer to the underlying data of the vector as long as the length
        /// of the vector does not change. If this vector is not a fixed-size vector and a method is called
        /// that changes the vector's length, it is not guaranteed that the span will continue to refer
        /// to the vector's data as the vector may be given a new data store.
        /// </remarks>
        public Span<T> asSpan(int start) {
            if ((uint)start > (uint)m_length)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(start));

            return new Span<T>(m_data, start, m_length - start);
        }

        /// <summary>
        /// Returns a span that can be used to access the data of a segment of this Vector instance.
        /// </summary>
        /// <param name="start">The index of the start of the segment.</param>
        /// <param name="length">The length of the segment.</param>
        /// <returns>A span that can be used to access the data of the segment of this Vector
        /// instance defined by <paramref name="start"/> and <paramref name="length"/>.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>ArgumentError #10061: The segment defined by <paramref name="start"/> and <paramref name="length"/>
        /// is not entirely within the bounds of the vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// The returned span will refer to the underlying data of the vector as long as the length
        /// of the vector does not change. If this vector is not a fixed-size vector and a method is called
        /// that changes the vector's length, it is not guaranteed that the span will continue to refer
        /// to the vector's data as the vector may be given a new data store.
        /// </remarks>
        public Span<T> asSpan(int start, int length) {
            if ((uint)start > (uint)m_length)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(start));
            if ((uint)length > (uint)(m_length - start))
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(length));

            return new Span<T>(m_data, start, length);
        }

        /// <summary>
        /// Returns a <see cref="ReadOnlyArrayView{T}"/> that can be used to access the data of this
        /// Vector instance.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{T}"/> that can be used to access the data of this
        /// Vector instance.</returns>
        /// <remarks>
        /// The returned view will refer to the underlying data of the vector as long as the length
        /// of the vector does not change. If this vector is not a fixed-size vector and a method is called
        /// that changes the vector's length, it is not guaranteed that the view will continue to refer
        /// to the vector's data as the vector may be given a new data store.
        /// </remarks>
        public ReadOnlyArrayView<T> asReadOnlyArrayView() => new ReadOnlyArrayView<T>(m_data, 0, m_length);

        /// <summary>
        /// Returns a <see cref="ReadOnlyArrayView{T}"/> that can be used to access the data of the segment
        /// of this Vector instance from the given index until the end.
        /// </summary>
        /// <param name="start">The index from which the returned view should start.</param>
        /// <returns>A <see cref="ReadOnlyArrayView{T}"/> that can be used to access the data of the segment
        /// of this Vector instance starting at the <paramref name="start"/> index and ending at the
        /// last element of the vector.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>ArgumentError #10061: <paramref name="start"/> is greater than the length of the vector.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// The returned view will refer to the underlying data of the vector as long as the length
        /// of the vector does not change. If this vector is not a fixed-size vector and a method is called
        /// that changes the vector's length, it is not guaranteed that the view will continue to refer
        /// to the vector's data as the vector may be given a new data store.
        /// </remarks>
        public ReadOnlyArrayView<T> asReadOnlyArrayView(int start) {
            if ((uint)start > (uint)m_length)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(start));

            return new ReadOnlyArrayView<T>(m_data, start, m_length - start);
        }

        /// <summary>
        /// Returns a <see cref="ReadOnlyArrayView{T}"/> that can be used to access the data of a segment
        /// of this Vector instance.
        /// </summary>
        /// <param name="start">The index of the start of the segment.</param>
        /// <param name="length">The length of the segment, or -1 if the segment should
        /// be from <paramref name="start"/> to the end of the vector.</param>
        /// <returns>A <see cref="ReadOnlyArrayView{T}"/> that can be used to access the data of the segment
        /// of this Vector instance defined by <paramref name="start"/> and <paramref name="length"/>.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>ArgumentError #10061: The segment defined by <paramref name="start"/> and <paramref name="length"/>
        /// is not entirely within the bounds of the vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// The returned view will refer to the underlying data of the vector as long as the length
        /// of the vector does not change. If this vector is not a fixed-size vector and a method is called
        /// that changes the vector's length, it is not guaranteed that the view will continue to refer
        /// to the vector's data as the vector may be given a new data store.
        /// </remarks>
        public ReadOnlyArrayView<T> asReadOnlyArrayView(int start, int length) {
            if ((uint)start > (uint)m_length)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(start));
            if ((uint)length > (uint)(m_length - start))
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(length));

            return new ReadOnlyArrayView<T>(m_data, start, length);
        }

        /// <summary>
        /// Returns an instance of <see cref="IEnumerable{T}"/> that can be used to enumerate the
        /// elements of the vector.
        /// </summary>
        public IEnumerable<T> asEnumerable() {
            T[] data = m_data;
            for (int i = 0, n = m_length; i < n; i++)
                yield return data[i];
        }

        /// <summary>
        ///  Returns an instance of <see cref="IEnumerable{T}"/> that can be used to enumerate the
        /// elements of the Vector, converted to the type <typeparamref name="U"/>.
        /// </summary>
        /// <typeparam name="U">The type to convert the Vector elements to.</typeparam>
        public new IEnumerable<U> asEnumerable<U>() {
            return (typeof(T) == typeof(U)) ? (IEnumerable<U>)asEnumerable() : iterator();

            IEnumerable<U> iterator() {
                T[] data = m_data;
                var converter = GenericTypeConverter<T, U>.instance;
                for (int i = 0, n = m_length; i < n; i++)
                    yield return converter.convert(data[i]);
            }
        }

        /// <summary>
        /// Copies a range elements from the source to the destination array.
        /// </summary>
        ///
        /// <param name="src">The source array.</param>
        /// <param name="srcIndex">The index in the source array of the first element of the range to
        /// be copied.</param>
        /// <param name="dst">The destination array.</param>
        /// <param name="dstIndex">The index in the destination array of the first element of the
        /// range in the destination array to which the source elements must be written.</param>
        /// <param name="length">The number of elements to copy.</param>
        private static void _internalArrayCopy(T[] src, int srcIndex, T[] dst, int dstIndex, int length) =>
            (new ReadOnlySpan<T>(src, srcIndex, length)).CopyTo(dst.AsSpan(dstIndex));

        /// <summary>
        /// Returns a Boolean value indicating whether the Vector has an element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>True, if the Vector has an element at the given index, false
        /// otherwise.</returns>
        /// <remarks>
        /// This method returns true if the index is a positive integer less than the length of the
        /// Vector. Otherwise, it returns false.
        /// </remarks>
        public new bool AS_hasElement(int index) => (uint)index < (uint)m_length;

        /// <summary>
        /// Gets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The value of the element.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>RangeError #1125: <paramref name="index"/> is negative or greater than the length of the
        /// Vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public new T AS_getElement(int index) {
            if ((uint)index >= (uint)m_length)
                _getOutOfBoundsIndex(index);
            return m_data[index];
        }

        /// <summary>
        /// Sets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value of the element.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>RangeError #1125: <paramref name="index"/> is negative or greater than the length of the
        /// Vector.</description>
        /// </item>
        /// <item>
        /// <description>RangeError #1126: <paramref name="index"/> is equal to the length of the vector, and
        /// this a fixed-length vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AS_setElement(int index, T value) {
            if ((uint)index >= (uint)m_length)
                _setOutOfBoundsIndex(index);

            m_data[index] = value;
        }

        /// <summary>
        /// This method always throws an error. It is called if an out-of-bounds index is passed to
        /// the <see cref="AS_getElement(Int32)"/> method.
        /// </summary>
        /// <param name="index">The index.</param>
        private void _getOutOfBoundsIndex(int index) =>
            throw ErrorHelper.createError(ErrorCode.VECTOR_INDEX_OUT_OF_RANGE, (uint)index, m_length);

        /// <summary>
        /// This method is called when an out-of-bounds index is passed to the
        /// <see cref="AS_setElement(Int32, T)"/> method.
        /// </summary>
        /// <param name="index">The index.</param>
        private void _setOutOfBoundsIndex(int index) {
            if (index != m_length)
                throw ErrorHelper.createError(ErrorCode.VECTOR_INDEX_OUT_OF_RANGE, (uint)index, m_length);
            if (m_fixed)
                throw ErrorHelper.createError(ErrorCode.VECTOR_FIXED_LENGTH_CHANGE);
            if (m_data.Length == m_length)
                DataStructureUtil.expandArray(ref m_data);
            m_length++;
        }

        /// <summary>
        /// Deletes the value of the element at the given index. For Vectors, this method has no
        /// effect and returns false.
        /// </summary>
        /// <param name="index">The index of the element to delete.</param>
        /// <returns>True if the property was deleted, false otherwise.</returns>
        public new bool AS_deleteElement(int index) => false;

        /// <summary>
        /// Returns a Boolean value indicating whether the current instance has an element at the
        /// given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>True, if the current instance has an element at the given index, otherwise
        /// false.</returns>
        /// <remarks>
        /// This method returns true if the index is a positive integer less than the length of the
        /// Vector. Otherwise, it returns false.
        /// </remarks>
        public new bool AS_hasElement(uint index) => index < (uint)m_length;

        /// <summary>
        /// Gets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The value of the element.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>RangeError #1125: <paramref name="index"/> is negative or greater than the length of the
        /// Vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public new T AS_getElement(uint index) {
            if (index >= (uint)m_length)
                _getOutOfBoundsIndex((int)index);
            return m_data[(int)index];
        }

        /// <summary>
        /// Sets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value of the element.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>RangeError #1125: <paramref name="index"/> is negative or greater than the length of the
        /// Vector.</description>
        /// </item>
        /// <item>
        /// <description>RangeError #1126: <paramref name="index"/> is equal to the length of the Vector, and
        /// this a fixed-length vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AS_setElement(uint index, T value) {
            if (index >= (uint)m_length)
                _setOutOfBoundsIndex((int)index);
            m_data[(int)index] = value;
        }

        /// <summary>
        /// Deletes the value of the element at the given index. For Vectors, this method has no
        /// effect and always returns false.
        /// </summary>
        /// <param name="index">The index of the element to delete.</param>
        /// <returns>True if the property was deleted, otherwise false.</returns>
        public new bool AS_deleteElement(uint index) => false;

        /// <summary>
        /// Returns a Boolean value indicating whether the current instance has an element at the
        /// given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>True, if the current instance has an element at the given index, otherwise
        /// false.</returns>
        /// <remarks>
        /// This method returns true if the index is a positive integer less than the length of the
        /// Vector. Otherwise, it returns false.
        /// </remarks>
        public new bool AS_hasElement(double index) {
            int intIndex = (int)index;
            return (double)intIndex == index && AS_hasElement(intIndex);
        }

        /// <summary>
        /// Gets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The value of the element.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>RangeError #1125: <paramref name="index"/> is negative or greater than the length of the
        /// Vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public new T AS_getElement(double index) {
            int intIndex = (int)index;
            if ((double)intIndex != index)
                throw ErrorHelper.createError(ErrorCode.VECTOR_INDEX_OUT_OF_RANGE, index, m_length);
            return AS_getElement(intIndex);
        }

        /// <summary>
        /// Sets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value of the element.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>RangeError #1125: <paramref name="index"/> is negative or greater than the length of the
        /// Vector.</description>
        /// </item>
        /// <item>
        /// <description>RangeError #1126: <paramref name="index"/> is equal to the length of the Vector, and
        /// this a fixed-length vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AS_setElement(double index, T value) {
            int intIndex = (int)index;
            if ((double)intIndex != index)
                throw ErrorHelper.createError(ErrorCode.VECTOR_INDEX_OUT_OF_RANGE, index, m_length);
            AS_setElement(intIndex, value);
        }

        /// <summary>
        /// Deletes the value of the element at the given index. For Vectors, this method has no
        /// effect and always returns false.
        /// </summary>
        /// <param name="index">The index of the element to delete.</param>
        /// <returns>True if the property was deleted, otherwise false.</returns>
        public new bool AS_deleteElement(double index) => false;

        /// <summary>
        /// Copies the contents of a subarray of this <see cref="ASVector{T}"/> into a span of the,
        /// type <typeparamref name="TDest"/>, converting the elements to that type.
        /// </summary>
        ///
        /// <param name="srcIndex">The index in this <see cref="ASVector{T}"/> instance of
        /// the beginning of the subarray to be copied.</param>
        /// <param name="dst">The span to copy the vector's contents to. The length of the subarray
        /// copied is the length of this span.</param>
        ///
        /// <typeparam name="TDest">The type of the destination span. If this is not the element type
        /// of the vector, the elements will be converted to the destination type.</typeparam>
        public new void copyToSpan<TDest>(int srcIndex, Span<TDest> dst) {
            int length = dst.Length;

            if ((uint)srcIndex > (uint)m_length)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(srcIndex));
            if ((uint)length > (uint)(m_length - srcIndex))
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(length));

            GenericTypeConverter<T, TDest>.instance.convertSpan(m_data.AsSpan(srcIndex, length), dst);
        }

        /// <summary>
        /// Gets or sets the value of the element at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>RangeError #1125: <paramref name="index"/> is negative or greater than the length of the
        /// vector (applies to both get and set methods) or equal to the length of the vector (get
        /// method only).</description>
        /// </item>
        /// <item>
        /// <description>RangeError #1126: An element at an index is equal to the length of the vector is set, and
        /// this a fixed-length vector.</description>
        /// </item>
        /// </list>
        /// </exception>
        public new T this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if ((uint)index >= (uint)m_length)
                    _getOutOfBoundsIndex(index);
                return m_data[index];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                if ((uint)index >= (uint)m_length)
                    _setOutOfBoundsIndex(index);
                m_data[index] = value;
            }
        }

        /// <summary>
        /// Appends an element to the Vector.
        /// </summary>
        /// <param name="item">The value of the element to add.</param>
        /// <returns>The new length of the Vector, after adding the element.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1126: This method is called on a fixed-length vector.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method is not exported to the AVM2 (the other overload,
        /// <see cref="push(RestParam)"/>, is exported). However, the ABC to IL compiler may insert
        /// calls to this method as an optimization when the <c>push</c> method is called on a
        /// vector with a single argument.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int push(T item) {
            int len = m_length;
            _setOutOfBoundsIndex(len);
            m_data[len] = item;
            return len;
        }

        /// <summary>
        /// Creates a copy of the vector and appends the elements of each of the <see cref="ASVector{T}"/>
        /// and/or <see cref="Array"/> instances given as arguments to it, in order.
        /// </summary>
        /// <param name="args">The vectors and/or arrays to concatenate to a copy of this
        /// vector.</param>
        /// <returns>A copy of this vector with the elements of all the arrays and/or vectors in
        /// <paramref name="args"/> concatenated to it.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>TypeError #1009: One of the arguments is null.</description></item>
        /// <item>
        /// <description>TypeError #1034: One of the arguments is not an Array or Vector, or an element of one of the
        /// Arrays or Vectors given as arguments cannot be converted to the type <typeparamref name="T"/>.
        /// </description>
        /// </item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new ASVector<T> concat(RestParam args = default) {
            int totalLength = m_length;
            var argsSpan = args.getSpan();

            for (int i = 0; i < argsSpan.Length; i++) {
                ASObject? arg = argsSpan[i].value;

                if (arg is ASVectorAny argAsVector)
                    totalLength = checked(totalLength + argAsVector.length);
                else if (arg is ASArray argAsArray)
                    totalLength = checked(totalLength + (int)argAsArray.length);
                else
                    throw ErrorHelper.createCastError(argsSpan[i], typeof(ASVector<T>));
            }

            var concatVec = new ASVector<T>(totalLength);
            asSpan().CopyTo(concatVec.asSpan(0, m_length));

            totalLength = m_length;

            for (int i = 0; i < argsSpan.Length; i++) {
                ASObject? arg = argsSpan[i].value;

                if (arg is ASVectorAny argAsVector) {
                    int argLength = argAsVector.length;
                    argAsVector.copyToSpan(0, concatVec.asSpan(totalLength, argLength));
                    totalLength += argLength;
                }
                else if (arg is ASArray argAsArray) {
                    int argLength = (int)argAsArray.length;
                    argAsArray.copyToSpan(0, concatVec.asSpan(totalLength, argLength));
                    totalLength += argLength;
                }
            }

            return concatVec;
        }

        /// <summary>
        /// Calls a specified function for each element in the Vector, until it returns false for any
        /// element, in which case this method returns false, or the function returns true for all
        /// elements in the vector, in which case the method returns true.
        /// </summary>
        ///
        /// <param name="callback">
        /// The callback function to execute for each element. It must take three arguments. The first
        /// argument is the element value, the second argument is the index of the element and the
        /// third is a reference to the <see cref="ASVector{T}"/> instance that called this method.
        /// </param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this parameter
        /// must be null, otherwise an error is thrown.</param>
        ///
        /// <returns>True if <paramref name="callback"/> returns true for all elements in the
        /// Vector, otherwise false. If <paramref name="callback"/> is null, returns
        /// true.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1510: <paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null.</description>
        /// </item>
        /// <item><description>The callback function throws an exception.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the callback function modifies the Vector, the behaviour of this method is undefined.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new bool every(ASFunction callback, ASObject? thisObject = null) {
            if (callback == null)
                return true;

            if (thisObject != null && callback.isMethodClosure)
                throw ErrorHelper.createError(ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL);

            var anyConverter = GenericTypeConverter<T, ASAny>.instance;

            ASAny[] cbArgsArray = new ASAny[3];
            cbArgsArray[2] = this;

            var span = asSpan();
            for (int i = 0; i < span.Length; i++) {
                cbArgsArray[0] = anyConverter.convert(span[i]);
                cbArgsArray[1] = i;

                ASAny cbResult = callback.AS_invoke(thisObject, cbArgsArray);
                if (!(cbResult.value is ASBoolean && (bool)cbResult))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Calls the specified callback function for each element in the <see cref="ASVector{T}"/>
        /// instance, and returns a vector containing all elements for which the callback function
        /// returns true.
        /// </summary>
        ///
        /// <param name="callback">
        /// The callback function to execute for each element. It must take three arguments. The first
        /// argument is the element value, the second argument is the index of the element and the
        /// third is a reference to the <see cref="ASVector{T}"/> instance that called this method.
        /// </param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this parameter
        /// must be null, otherwise an error is thrown.</param>
        ///
        /// <returns>A <see cref="ASVector{T}"/> instance containing all elements for which
        /// the callback function returns true. If <paramref name="callback"/> is null, an
        /// empty vector is returned.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1510: <paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null.</description>
        /// </item>
        /// <item><description>The callback function throws an exception.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the callback function modifies the Vector, the behaviour of this method is undefined.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new ASVector<T> filter(ASFunction callback, ASObject? thisObject = null) {
            if (callback == null)
                return new ASVector<T>();

            if (thisObject != null && callback.isMethodClosure)
                throw ErrorHelper.createError(ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL);

            var anyConverter = GenericTypeConverter<T, ASAny>.instance;
            ASVector<T> result = new ASVector<T>();

            ASAny[] cbArgsArray = new ASAny[3];
            cbArgsArray[2] = this;

            var span = asSpan();
            for (int i = 0; i < span.Length; i++) {
                cbArgsArray[0] = anyConverter.convert(span[i]);
                cbArgsArray[1] = i;

                ASAny cbReturn = callback.AS_invoke(thisObject, cbArgsArray);
                if (cbReturn.value is ASBoolean && (bool)cbReturn)
                    result.push(span[i]);
            }

            return result;
        }

        /// <summary>
        /// Executes the specified callback function for each element in the Vector.
        /// </summary>
        ///
        /// <param name="callback">
        /// The callback function to execute for each element. It must take three arguments. The first
        /// argument is the element value, the second argument is the index of the element and the
        /// third is a reference to the <see cref="ASVector{T}"/> instance that called this method. If the callback
        /// function returns a value, it is ignored. If this argument is null, this method does
        /// nothing.
        /// </param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this parameter
        /// must be null, otherwise an error is thrown.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1510: <paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null.</description>
        /// </item>
        /// <item><description>The callback function throws an exception.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the callback function modifies the Vector, the behaviour of this method is undefined.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new void forEach(ASFunction callback, ASObject? thisObject = null) {
            if (callback == null)
                return;

            if (thisObject != null && callback.isMethodClosure)
                throw ErrorHelper.createError(ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL);

            var anyConverter = GenericTypeConverter<T, ASAny>.instance;

            ASAny[] cbArgsArray = new ASAny[3];
            cbArgsArray[2] = this;

            var span = asSpan();
            for (int i = 0; i < span.Length; i++) {
                cbArgsArray[0] = anyConverter.convert(span[i]);
                cbArgsArray[1] = i;
                callback.AS_invoke(thisObject, cbArgsArray);
            }
        }

        /// <summary>
        /// Searches for the element whose value is <paramref name="searchElement"/> in the Vector,
        /// starting at the index <paramref name="fromIndex"/>, and returns the index of the first
        /// element with that value.
        /// </summary>
        ///
        /// <param name="searchElement">The value of the element to search in the Vector
        /// instance.</param>
        /// <param name="fromIndex">The index from where to start searching. If this greater than or
        /// equal to the length of the Vector, this method returns -1. If this is negative, the length
        /// of the Vector is added to it; if it is still negative after adding the length, it is set
        /// to 0.</param>
        ///
        /// <returns>The index of the first element, at or after <paramref name="fromIndex"/>, whose
        /// value is equal to <paramref name="searchElement"/>. If no element with that value is
        /// found, or if <paramref name="fromIndex"/> is equal to or greater than the length of the
        /// Vector, returns -1.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public int indexOf(T searchElement, int fromIndex = 0) {
            if (fromIndex < 0)
                fromIndex = Math.Max(fromIndex + m_length, 0);
            if ((uint)fromIndex >= (uint)m_length)
                return -1;
            return GenericComparer<T>.defaultComparer.indexOf(asSpan().Slice(fromIndex), searchElement);
        }

        /// <summary>
        /// Returns a string containing the string representations of all elements in the Vector
        /// concatenated with the specified separator string between values.
        /// </summary>
        /// <param name="sep">The separator string. If this is null, the default value "," is
        /// used.</param>
        /// <returns>A string containing the string representations of all elements in the Vector
        /// concatenated with <paramref name="sep"/> between values.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new string join(string sep = ",") {
            if (m_length == 0)
                return "";

            sep ??= "";
            string[] stringsToJoin = new string[m_length];
            GenericTypeConverter<T, string>.instance.convertSpan(asSpan(), stringsToJoin);

            for (int i = 0; i < stringsToJoin.Length; i++) {
                if (stringsToJoin[i] == null)
                    stringsToJoin[i] = "null";
            }

            return String.Join(sep, stringsToJoin);
        }

        /// <summary>
        /// Searches for the element whose value is <paramref name="searchElement"/> in the Vector,
        /// starting at the index <paramref name="fromIndex"/> and moving backwards, and returns the
        /// index of the first element with that value.
        /// </summary>
        ///
        /// <param name="searchElement">The value of the element to search in the Vector
        /// instance.</param>
        /// <param name="fromIndex">The index from where to start searching. If this is negative, the
        /// length of the Vector is added to it; if it is still negative after adding the length, it
        /// is set to 0. If it is greater than or equal to the length of the Vector, it is set to
        /// <c>length - 1</c>.</param>
        ///
        /// <returns>The index of the first element, at or before <paramref name="fromIndex"/>,
        /// whose value is equal to <paramref name="searchElement"/>. If no element with that value
        /// is found, returns -1.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public int lastIndexOf(T searchElement, int fromIndex = Int32.MaxValue) {
            if (m_length == 0)
                return -1;

            if (fromIndex < 0)
                fromIndex = Math.Max(fromIndex + m_length, 0);
            else if (fromIndex >= m_length)
                fromIndex = m_length - 1;

            return GenericComparer<T>.defaultComparer.lastIndexOf(asSpan().Slice(0, fromIndex + 1), searchElement);
        }

        /// <summary>
        /// Executes the specified callback function for each element in the Vector and returns a new
        /// Vector with each index holding the return value of the callback function for the element
        /// of the current Vector instance at that index. The returned vector will have the same
        /// length and element type as the current vector.
        /// </summary>
        ///
        /// <param name="callback">
        /// The callback function to execute for each element. It must take three arguments. The first
        /// argument is the element value, the second argument is the index of the element and the
        /// third is a reference to the <see cref="ASVector{T}"/> instance that called this method.
        /// The return value of the callback will be converted to the type <typeparamref name="T"/>.
        /// </param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this parameter
        /// must be null, otherwise an error is thrown.</param>
        ///
        /// <returns>A Vector instance containing the return values of the callback function for each
        /// element in the current instance. If <paramref name="callback"/> is null, an empty Vector
        /// is returned.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1510: <paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null.</description>
        /// </item>
        /// <item><description>The callback function throws an exception.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the callback function modifies the Vector, the behaviour of this method is undefined.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new ASVector<T> map(ASFunction callback, ASObject? thisObject = null) {
            if (callback == null)
                return new ASVector<T>(m_length);

            if (thisObject != null && callback.isMethodClosure)
                throw ErrorHelper.createError(ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL);

            var convTypeToAny = GenericTypeConverter<T, ASAny>.instance;
            var convAnyToType = GenericTypeConverter<ASAny, T>.instance;
            var result = new ASVector<T>(m_length);
            var resultSpan = result.asSpan();

            ASAny[] cbArgsArray = new ASAny[3];
            cbArgsArray[2] = this;

            var span = asSpan();
            for (int i = 0; i < span.Length; i++) {
                cbArgsArray[0] = convTypeToAny.convert(span[i]);
                cbArgsArray[1] = i;
                resultSpan[i] = convAnyToType.convert(callback.AS_invoke(thisObject, cbArgsArray));
            }

            return result;
        }

        /// <summary>
        /// Removes the last element from the Vector and returns the value of that element.
        /// </summary>
        /// <returns>The value of the last element in the Vector. If the Vector is empty, returns the
        /// default value of the element type.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1126: This method is called on a fixed-length vector.</description></item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new T? pop() {
            if (m_fixed)
                throw ErrorHelper.createError(ErrorCode.VECTOR_FIXED_LENGTH_CHANGE);

            if (m_length == 0)
                return default(T);

            T val = m_data[m_length - 1];
            m_data[--m_length] = default(T)!;

            return val;
        }

        /// <summary>
        /// Adds the values in the <paramref name="args"/> array to the end of the Vector, and
        /// returns the new length of the Vector.
        /// </summary>
        /// <param name="args">The values to add to the end of the Vector.</param>
        /// <returns>The new length of the Vector.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1126: This method is called on a fixed-length vector.</description></item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new int push(RestParam args = default) {
            var argsSpan = args.getSpan();
            int argCount = argsSpan.Length;

            if (argCount == 0)
                return m_length;

            if (m_fixed)
                throw ErrorHelper.createError(ErrorCode.VECTOR_FIXED_LENGTH_CHANGE);

            if (m_data.Length - m_length < argCount)
                DataStructureUtil.resizeArray(ref m_data, m_length, m_length + argCount);

            var converter = GenericTypeConverter<ASAny, T>.instance;

            if (argCount == 1) {
                m_data[length++] = converter.convert(argsSpan[0]);
            }
            else {
                m_length += argCount;
                converter.convertSpan(argsSpan, asSpan().Slice(m_length - argCount));
            }

            return m_length;
        }

        /// <summary>
        /// Reverses all elements in the current Vector.
        /// </summary>
        /// <returns>The current Vector.</returns>
        /// <remarks>
        /// This method does an in-place reverse; no copy of the vector is made.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new ASVector<T> reverse() {
            m_data.AsSpan(0, m_length).Reverse();
            return this;
        }

        /// <summary>
        /// Removes the first element from the Vector and returns the value of that element. All other
        /// elements are shifted backwards by one index.
        /// </summary>
        /// <returns>The value of the first element in the Vector. If the Vector is empty, returns the
        /// default value of the element type.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1126: This method is called on a fixed-length vector.</description></item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new T? shift() {
            if (m_fixed)
                throw ErrorHelper.createError(ErrorCode.VECTOR_FIXED_LENGTH_CHANGE);

            if (m_length == 0)
                return default(T);

            T val = m_data[0];
            _internalArrayCopy(m_data, 1, m_data, 0, m_length - 1);

            m_data[m_length - 1] = default(T)!;
            m_length--;

            return val;
        }

        /// <summary>
        /// Returns a Vector containing all elements of the current Vector from
        /// <paramref name="startIndex"/> up to (but not including) <paramref name="endIndex"/>.
        /// </summary>
        ///
        /// <param name="startIndex">
        /// The index from which elements should be included in the returned Vector. If this is
        /// negative, the length of the Vector is added to it; if it is still negative after adding
        /// the length, it is set to zero. If this is greater than the length of the Vector, it is set
        /// to its length. If this is greater than of equal to <paramref name="endIndex"/>, an empty
        /// Vector is returned.
        /// </param>
        /// <param name="endIndex">
        /// The index at which to stop adding elements to the returned Vector. If this is negative,
        /// the length of the Vector is added to it; if it is still negative after adding the length,
        /// it is set to zero. If this is greater than the length of the Vector, it is set to its
        /// length. If this is less than of equal to <paramref name="endIndex"/>, an empty array is
        /// returned. Elements up to, but not including, this index, will be included in the returned
        /// Vector.
        /// </param>
        ///
        /// <returns>A Vector containing all elements of the current Vector from
        /// <paramref name="startIndex"/> up to (but not including)
        /// <paramref name="endIndex"/>.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new ASVector<T> slice(int startIndex = 0, int endIndex = Int32.MaxValue) {
            // Normalize the start and end indices
            startIndex = (startIndex < 0) ? Math.Max(startIndex + m_length, 0) : Math.Min(startIndex, m_length);
            endIndex = (endIndex < 0) ? Math.Max(endIndex + m_length, 0) : Math.Min(endIndex, m_length);

            if (startIndex >= endIndex)
                return new ASVector<T>(0);

            return new ASVector<T>(m_data.AsSpan(startIndex, endIndex - startIndex));
        }

        /// <summary>
        /// Calls a specified function for each element in the Vector, until it returns true for any
        /// element, in which case this method returns true, or the function returns false for all
        /// elements in the Vector, in which case this method returns false.
        /// </summary>
        ///
        /// <param name="callback">
        /// The callback function to execute for each element. It must take three arguments. The first
        /// argument is the element value, the second argument is the index of the element and the
        /// third is a reference to the <see cref="ASVector{T}"/> instance that called this method.
        /// </param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this parameter
        /// must be null, otherwise an error is thrown.</param>
        ///
        /// <returns>True if <paramref name="callback"/> returns true for any element in the Vector,
        /// otherwise false. If <paramref name="callback"/> is null, returns false.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1510: <paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null.</description>
        /// </item>
        /// <item><description>The callback function throws an exception.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the callback function modifies the Vector, the behaviour of this method is undefined.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new bool some(ASFunction callback, ASObject? thisObject = null) {
            if (callback == null)
                return false;

            if (thisObject != null && callback.isMethodClosure)
                throw ErrorHelper.createError(ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL);

            var anyConverter = GenericTypeConverter<T, ASAny>.instance;
            ASAny[] cbArgsArray = new ASAny[3];
            cbArgsArray[2] = this;

            var span = asSpan();
            for (int i = 0; i < span.Length; i++) {
                cbArgsArray[0] = anyConverter.convert(span[i]);
                cbArgsArray[1] = i;

                ASAny cbResult = callback.AS_invoke(thisObject, cbArgsArray);
                if (cbResult.value is ASBoolean && (bool)cbResult)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Sorts the <see cref="ASVector{T}"/> instance.
        /// </summary>
        ///
        /// <param name="sortComparer">
        /// An object representing the kind of comparison used in the sort. If this is a function, the
        /// function will be used as a comparer function. In this case, the function must take two
        /// arguments of the vector's element type, and must return an integer which is less than,
        /// equal to or greater than zero if the first argument is less than, equal to or greater than
        /// the second argument respectively. If this is not a function, it is converted to an integer
        /// and treated as a set of bit flags represented by the <see cref="ASArray"/> sorting
        /// constants (NUMERIC, DESCENDING, CASEINSENSITIVE and UNIQUESORT). RETURNINDEXEDARRAY is not
        /// supported by the Vector class and has no effect.
        /// </param>
        ///
        /// <returns>The instance that called this method.</returns>
        /// <remarks>
        /// If the <paramref name="sortComparer"/> parameter is a callback function, and it throws
        /// an exception during the sort, the state of the Vector is undefined.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new ASVector<T> sort(ASObject? sortComparer) {
            if (sortComparer is ASFunction func) {
                // The Array.Sort methods in corelib throws ArgumentException for some ill-behaved
                // comparison functions, so use DataStructureUtil.sortSpan (which never throws)
                // when we are given a user-provided comparator.

                var compareDelegate = func.createDelegate<Comparison<T>>();
                if (compareDelegate == null)
                    compareDelegate = GenericComparer<T>.getComparer(func).Compare;

                DataStructureUtil.sortSpan(asSpan(), compareDelegate);
                return this;
            }

            if (!ASObject.AS_isNumeric(sortComparer))
                throw ErrorHelper.createCastError(sortComparer, "Function");

            int flags = (int)sortComparer;

            GenericComparerType comparerType;
            if ((flags & ASArray.NUMERIC) != 0)
                comparerType = GenericComparerType.NUMERIC;
            else if ((flags & ASArray.CASEINSENSITIVE) != 0)
                comparerType = GenericComparerType.STRING_IGNORECASE;
            else
                comparerType = GenericComparerType.STRING;

            var comparer = GenericComparer<T>.getComparer(comparerType);
            Array.Sort(m_data, 0, m_length, comparer);

            if ((flags & ASArray.UNIQUESORT) != 0) {
                for (int i = 0, n = m_length; i + 1 < n; i++) {
                    if (comparer.Compare(m_data[i], m_data[i + 1]) == 0)
                        return this;
                }
            }

            if ((flags & ASArray.DESCENDING) != 0)
                reverse();

            return this;
        }

        /// <summary>
        /// Replaces the specified number of elements in the Vector, starting at a given index with
        /// values from the <paramref name="newValues"/> array, and returns another Vector
        /// containing the values that have been deleted.
        /// </summary>
        ///
        /// <param name="startIndex">The index from which elements should be removed and included in
        /// the returned array. If this is negative, the length of the Vector is added to it; if it is
        /// still negative after adding the length, it is set to zero. If this is greater than the
        /// length of the Vector, it is set to its length.</param>
        /// <param name="deleteCount">
        /// The number of elements to be removed from the Vector. If this is negative, it is the
        /// number of elements to be retained starting from the end of the array and moving backwards,
        /// with all other elements starting at <paramref name="startIndex"/> being removed (in this
        /// case, its magnitude must not be greater than <c>length - startIndex</c>, where
        /// <c>length</c> is the length of the Vector; if it is greater than this value, it is set
        /// to zero). If <c>startIndex + deleteCount</c> is greater than the value of the
        /// <see cref="length"/> property, this value is set to <c>length - startIndex</c>.
        /// </param>
        /// <param name="newValues">
        /// The new values to be added to the Vector, starting at <paramref name="startIndex"/>, in
        /// place of the deleted elements. For fixed-length vectors, the number of elements in this
        /// array must be equal to <paramref name="deleteCount"/>, otherwise a RangeError is thrown.
        /// For Vectors whose <see cref="@fixed"/> value is set to false, if the length of this
        /// array is not equal to <paramref name="deleteCount"/>, elements after the index
        /// <c>deleteCount - 1</c> are shifted backwards or forwards so that they occur
        /// immediately after the elements inserted from this array.
        /// </param>
        ///
        /// <returns>A Vector containing the values that have been deleted. It contains
        /// <paramref name="deleteCount"/> elements from the Vector (prior to this method being
        /// called), starting at <paramref name="startIndex"/>.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>RangeError #1126: The Vector is a fixed-length Vector, and <paramref name="deleteCount"/> is
        /// not equal to the number of arguments in <paramref name="newValues"/>.</description>
        /// </item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new ASVector<T> splice(
            int startIndex, int deleteCount = Int32.MaxValue, RestParam newValues = default)
        {
            startIndex = (startIndex < 0)
                ? Math.Max(startIndex + m_length, 0)
                : Math.Min(startIndex, m_length);

            if (deleteCount < 0)
                // If deleteCount is negative, its absolute value indicates the number
                // of elements (starting from the end of the array) that must be
                // retained. So add length - startIndex to it. If it is still negative,
                // set it to zero.
                // Note: This is different from Array behaviour.
                deleteCount = Math.Max(deleteCount + m_length - startIndex, 0);

            if (startIndex + deleteCount > m_length || startIndex + deleteCount < startIndex)
                // Ensure that the startIndex and deleteCount values do not result in an out-of-bounds index
                deleteCount = m_length - startIndex;

            var spliced = new ASVector<T>(deleteCount);

            if (newValues.length == deleteCount) {
                // One special case is when deleteCount is equal to the number of values in the newValues
                // array, in which case, write the deleted elements to the return array, and then replace them
                // with values from the newValues array.
                // This is the only case where fixed-length vectors can be used.
                if (deleteCount != 0) {
                    var typeConverter = GenericTypeConverter<ASAny, T>.instance;
                    m_data.AsSpan(startIndex, deleteCount).CopyTo(spliced.m_data);
                    typeConverter.convertSpan(newValues.getSpan(), asSpan(startIndex, deleteCount));
                }
                return spliced;
            }

            if (m_fixed)
                // Except for the special case above, fixed-length vectors cannot use this method, as it would
                // change their length.
                throw ErrorHelper.createError(ErrorCode.VECTOR_FIXED_LENGTH_CHANGE);

            T[] newBackingArray;
            int delta = newValues.length - deleteCount;

            if (m_data.Length - m_length < delta) {
                // Not enough space in the array. Create a new one, and copy all existing elements
                // until but not including the startIndex index (elements after the new values will be copied
                // later)
                newBackingArray = new T[DataStructureUtil.getNextArraySize(m_length, m_length + delta)];
                _internalArrayCopy(m_data, 0, newBackingArray, 0, startIndex);
            }
            else {
                // Sufficient space in the backing array.
                newBackingArray = m_data;
            }

            if (deleteCount != 0)
                m_data.AsSpan(startIndex, deleteCount).CopyTo(spliced.m_data);

            if (startIndex + deleteCount != m_length) {
                _internalArrayCopy(
                    m_data,
                    startIndex + deleteCount,
                    newBackingArray,
                    startIndex + newValues.length,
                    m_length - startIndex - deleteCount
                );
            }
            if (delta < 0 && m_data == newBackingArray) {
                newBackingArray.AsSpan(m_length + delta, -delta).Clear();
            }
            if (newValues.length > 0) {
                var typeConverter = GenericTypeConverter<ASAny, T>.instance;
                typeConverter.convertSpan(newValues.getSpan(), newBackingArray.AsSpan(startIndex, newValues.length));
            }

            m_length += delta;
            m_data = newBackingArray;

            return spliced;
        }

        /// <summary>
        /// Returns a locale-specific string representation of the current Vector instance.
        /// </summary>
        /// <returns>A locale-specific string representation of the current vector.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new string toLocaleString() {
            if (m_length == 0)
                return "";

            int len = m_length;
            string[] stringsToJoin = new string[len];
            QName toLocaleStringName = QName.publicName("toLocaleString");

            var objConverter = GenericTypeConverter<T, ASObject>.instance;
            for (int i = 0; i < len; i++) {
                ASObject obj = objConverter.convert(m_data[i]);
                if (obj == null)
                    continue;

                ASAny result = obj.AS_callProperty(toLocaleStringName, Array.Empty<ASAny>());
                stringsToJoin[i] = ASAny.AS_convertString(result);
            }

            return String.Join(",", stringsToJoin);
        }

        /// <summary>
        /// Returns the string representation of the current instance.
        /// </summary>
        /// <returns>The string representation of the current instance.</returns>
        ///
        /// <remarks>
        /// This method is exported to the AVM2 with the name <c>toString</c>, but must be called
        /// from .NET code with the name <c>AS_toString</c> to avoid confusion with the
        /// <see cref="Object.ToString" qualifyHint="true"/> method.
        /// </remarks>
        [AVM2ExportTrait(name = "toString", nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod(name = "toString")]
        public new string AS_toString() => join();

        /// <summary>
        /// Adds the values in the <paramref name="args"/> array to the beginning of the Vector
        /// instance, and returns the new length of the Vector.
        /// </summary>
        /// <param name="args">The values to add to the beginning of the Vector.</param>
        /// <returns>The new length of the Vector.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1126: This method is called on a fixed-length vector.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// All existing elements of the Vector are shifted forwards by N indices, where N is the
        /// number of items in the <paramref name="args"/> array.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new int unshift(RestParam args = default) {
            var argsSpan = args.getSpan();
            int argCount = argsSpan.Length;

            if (argCount == 0)
                return m_length;

            if (m_fixed)
                throw ErrorHelper.createError(ErrorCode.VECTOR_FIXED_LENGTH_CHANGE);

            T[] newBackingArray = m_data;
            if (m_data.Length - m_length < argCount)
                newBackingArray = new T[DataStructureUtil.getNextArraySize(m_length, m_length + argCount)];

            _internalArrayCopy(m_data, 0, newBackingArray, argCount, m_length);

            var typeConverter = GenericTypeConverter<ASAny, T>.instance;
            if (argCount == 1)
                newBackingArray[0] = typeConverter.convert(argsSpan[0]);
            else
                typeConverter.convertSpan(argsSpan, newBackingArray.AsSpan(0, argCount));

            m_data = newBackingArray;
            m_length += argCount;

            return m_length;
        }

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABC to IL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) {
            if (args.Length != 1)
                throw ErrorHelper.createError(ErrorCode.CLASS_COERCE_ARG_COUNT_MISMATCH, args.Length);

            return fromObject(args[0].value!);
        }

        #region ASVectorAny overrides

        protected private override bool _VA_hasElement(int index) => AS_hasElement(index);
        protected private override bool _VA_hasElement(uint index) => AS_hasElement(index);
        protected private override bool _VA_hasElement(double index) => AS_hasElement(index);

        protected private override IEnumerable<U> _VA_asEnumerable<U>() => asEnumerable<U>();

        protected private override void _VA_copyToSpan<U>(int srcIndex, Span<U> dst) => copyToSpan(srcIndex, dst);

        protected private override ASObject _VA_getElement(int index) =>
            GenericTypeConverter<T, ASObject>.instance.convert(AS_getElement(index));

        protected private override ASObject _VA_getElement(uint index) =>
            GenericTypeConverter<T, ASObject>.instance.convert(AS_getElement(index));

        protected private override ASObject _VA_getElement(double index) =>
            GenericTypeConverter<T, ASObject>.instance.convert(AS_getElement(index));

        protected private override void _VA_setElement(int index, ASObject? value) =>
            AS_setElement(index, GenericTypeConverter<ASObject?, T>.instance.convert(value));

        protected private override void _VA_setElement(uint index, ASObject? value) =>
            AS_setElement(index, GenericTypeConverter<ASObject?, T>.instance.convert(value));

        protected private override void _VA_setElement(double index, ASObject? value) =>
            AS_setElement(index, GenericTypeConverter<ASObject?, T>.instance.convert(value));

        protected private override bool _VA_deleteElement(int index) => AS_deleteElement(index);
        protected private override bool _VA_deleteElement(uint index) => AS_deleteElement(index);
        protected private override bool _VA_deleteElement(double index) => AS_deleteElement(index);

        protected private override int _VA_length {
            get => length;
            set => length = value;
        }

        protected private override bool _VA_fixed {
            get => @fixed;
            set => @fixed = value;
        }

        protected private override bool _VA_some(ASFunction callback, ASObject? thisObject) =>
            some(callback, thisObject);

        protected private override bool _VA_every(ASFunction callback, ASObject? thisObject) =>
            every(callback, thisObject);

        protected private override void _VA_forEach(ASFunction callback, ASObject? thisObject) =>
            forEach(callback, thisObject);

        protected private override ASVectorAny _VA_map(ASFunction callback, ASObject? thisObject) =>
            map(callback, thisObject);

        protected private override ASVectorAny _VA_filter(ASFunction callback, ASObject? thisObject) =>
            filter(callback, thisObject);

        protected private override ASVectorAny _VA_concat(RestParam args) => concat(args);

        protected private override int _VA_indexOf(ASAny searchElement, int fromIndex) =>
            indexOf(GenericTypeConverter<ASAny, T>.instance.convert(searchElement), fromIndex);

        protected private override int _VA_lastIndexOf(ASAny searchElement, int fromIndex) =>
            lastIndexOf(GenericTypeConverter<ASAny, T>.instance.convert(searchElement), fromIndex);

        protected private override int _VA_push(RestParam args) => push(args);

        protected private override ASAny _VA_pop() => GenericTypeConverter<T?, ASAny>.instance.convert(pop());

        protected private override ASAny _VA_shift() => GenericTypeConverter<T?, ASAny>.instance.convert(shift());

        protected private override int _VA_unshift(RestParam args) => unshift(args);

        protected private override string _VA_join(string sep) => join(sep);

        protected private override ASVectorAny _VA_slice(int startIndex, int endIndex) => slice(startIndex, endIndex);

        protected private override ASVectorAny _VA_splice(int startIndex, int deleteCount, RestParam newValues) =>
            splice(startIndex, deleteCount, newValues);

        protected private override ASVectorAny _VA_sort(ASObject? sortComparer) => sort(sortComparer);

        protected private override ASVectorAny _VA_reverse() => reverse();

        protected private override string _VA_toLocaleString() => toLocaleString();

        protected private override string _VA_toString() => AS_toString();

        #endregion

    }

}
