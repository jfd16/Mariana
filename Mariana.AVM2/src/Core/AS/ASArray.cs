using System;
using System.Collections.Generic;
using System.Globalization;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The Array class represents an ordered list of items which can be looked up by numeric
    /// indices.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// An Array is a sparse array, that is, it need not be populated in a sequential order (for
    /// example, code like <c>array[1000] = "some value"</c> is legal even for arrays whose
    /// length is less than 1000). It is a dynamic array, that is, it does not have a fixed size
    /// (unlike a C# array). An Array object is not a typed array. Elements can be of any type,
    /// and are internally stored as <see cref="ASAny"/> (the "any" type).
    /// </para>
    /// <para>The Array class is not recommend for performance critical operations. For typed
    /// and/or fixed length arrays (which perform faster), use the <see cref="ASVector{T}"/>
    /// class instead of the <see cref="ASArray"/> class.</para>
    /// </remarks>
    [AVM2ExportClass(name = "Array", isDynamic = true, hasPrototypeMethods = true, hasIndexLookupMethods = true)]
    [AVM2ExportClassInternal(tag = ClassTag.ARRAY)]
    public class ASArray : ASObject {

        /// <summary>
        /// Represents an element in an array. This can be empty, null, undefined
        /// or an object. The "empty" state is used to represent an element whose
        /// value does not exist, and is different from undefined (for example,
        /// hasProperty returns false for an empty element but true for an
        /// undefined element)
        /// </summary>
        private readonly struct Value {
            public static readonly Value @null = new Value(new ASObject());
            public static readonly Value undef = new Value(new ASObject());
            public static readonly Value empty = default(Value);

            private readonly ASObject m_obj;

            private Value(ASObject obj) => m_obj = obj;

            public static Value fromAny(ASAny val) =>
                val.isDefined ? new Value(val.value ?? @null.m_obj) : undef;

            public static Value fromObject(ASObject val) => new Value(val ?? @null.m_obj);

            public bool isEmpty => m_obj == null;

            public bool isEmptyOrUndefined => m_obj == null || m_obj == undef.m_obj;

            public bool isReferenceEqual(Value other) => m_obj == other.m_obj;

            public ASAny toAny() {
                if (m_obj == null || m_obj == undef.m_obj)
                    return default(ASAny);
                if (m_obj == @null.m_obj)
                    return ASAny.@null;
                return new ASAny(m_obj);
            }

            public override string ToString() => isEmpty ? "<empty>" : toAny().ToString();
        }

        // A slot in the hash table.
        private struct HashLink {
            public uint key;
            public int next;
            public int headOfChain;
        }

        /// <summary>
        /// The value of the "length" property of the AS3 Array class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// Indicates that strings must be compared in a case-insensitive manner when sorting the
        /// array or vector. This option is used as a bit flag for the
        /// <see cref="ASArray.sort" qualifyHint="true"/> and
        /// <see cref="ASVector{T}.sort" qualifyHint="true"/> methods.
        /// </summary>
        [AVM2ExportTrait]
        public const int CASEINSENSITIVE = 1;

        /// <summary>
        /// Specifies that the array must be sorted in descending order. (The default is ascending
        /// order) This option is used as a bit flag for the
        /// <see cref="ASArray.sort" qualifyHint="true"/> and
        /// <see cref="ASVector{T}.sort" qualifyHint="true"/> methods.
        /// </summary>
        [AVM2ExportTrait]
        public const int DESCENDING = 2;

        /// <summary>
        /// Specifies that all elements in the array or vector must be restored to their original
        /// positions if two elements compare as equal during the sort. This option is used as a bit
        /// flag for the <see cref="ASArray.sort" qualifyHint="true"/> and
        /// <see cref="ASVector{T}.sort" qualifyHint="true"/> methods.
        /// </summary>
        [AVM2ExportTrait]
        public const int UNIQUESORT = 4;

        /// <summary>
        /// Specifies that the array must not be modified during the sort; instead, an array of
        /// indices representing the order in which the elements would have been sorted must be
        /// returned. This option is used as a bit flag for the
        /// <see cref="ASArray.sort" qualifyHint="true"/> method.
        /// </summary>
        [AVM2ExportTrait]
        public const int RETURNINDEXEDARRAY = 8;

        /// <summary>
        /// Specifies that the elements array or vector must be sorted based on their numeric values
        /// (as opposed to their string values). This option is used as a bit flag for the
        /// <see cref="ASArray.sort" qualifyHint="true"/> and
        /// <see cref="ASVector{T}.sort" qualifyHint="true"/> methods.
        /// </summary>
        [AVM2ExportTrait]
        public const int NUMERIC = 16;

        /// <summary>
        /// The array length below which
        /// </summary>
        private const int DENSE_ARRAY_SMALL_SIZE = 16;

        /// <summary>
        /// The load factor threshold below which dense array storage is converted to a hash table.
        /// (This must be a value between 0 and 64)
        /// </summary>
        private const int DENSE_TO_HASH_LOAD_FACTOR = 32;

        /// <summary>
        /// The load factor threshold above which hash table storage is converted to a dense array.
        /// (This must be a value between 0 and 64)
        /// </summary>
        private const int HASH_TO_DENSE_LOAD_FACTOR = 36;

        /// <summary>
        /// The maximum array size that can be preallocated when constructing an Array with a constructor that
        /// takes a length argument.
        /// </summary>
        private const int MAX_PREALLOC_LENGTH = 1 << 20;

        /// <summary>
        /// Used for directly sorting objects in their internal representation in an Array.
        /// </summary>
        private class ArrayValueComparer : IComparer<Value> {
            private readonly IComparer<ASAny> m_baseComparer;
            public ArrayValueComparer(IComparer<ASAny> baseComparer) => m_baseComparer = baseComparer;
            public int Compare(Value x, Value y) => m_baseComparer.Compare(x.toAny(), y.toAny());
        }

        /// <summary>
        /// This is used for the Array.sort method with a user provided compare function.
        /// </summary>
        private class ArrayValueComparerWithUserFunc {
            private readonly ASFunction m_func;
            private readonly ASAny[] m_callArgs = new ASAny[2];
            private readonly Value[] m_indexArray;

            public ArrayValueComparerWithUserFunc(ASFunction compareFunc, Value[] indexArray = null) {
                m_func = compareFunc;
                m_indexArray = indexArray;
            }

            public int compareValues(Value x, Value y) {
                m_callArgs[0] = x.toAny();
                m_callArgs[1] = y.toAny();

                double result = (double)m_func.AS_invoke(ASAny.@null, m_callArgs);
                return (result > 0) ? 1 : ((result < 0) ? -1 : 0);
            }

            public int compareIndices(int x, int y) => compareValues(m_indexArray[x], m_indexArray[y]);
        }

        /// <summary>
        /// The value of the "length" property of the current array. The indices of all non-deleted
        /// elements in the array must be less than this value.
        /// </summary>
        ///
        /// <remarks>
        /// If the current storage is a dense array, the value of this field must be greater
        /// than or equal <see cref="m_totalCount"/>, and greater than or equal to the length of the
        /// <see cref="m_values"/> array.
        /// </remarks>
        private uint m_length;

        /// <summary>
        /// Stores the values of the array.
        /// </summary>
        ///
        /// <remarks>
        /// <para>If dense array storage is used, this array stores the elements using their actual
        /// array indices.</para>
        /// <para>If hash table storage is used, this array contains the values of elements
        /// corresponding to the indices in the <see cref="HashLink.key" qualifyHint="true"/> fields
        /// of the elements in the <see cref="m_hashLinks"/> array.</para>
        /// </remarks>
        private Value[] m_values;

        /// <summary>
        /// The number of elements in the array which contain an object (which may be null or
        /// undefined), i.e. those which are not deleted.
        /// </summary>
        private int m_nonEmptyCount;

        /// <summary>
        /// If the current storage is a dense array, this is equal to one plus the index of the last
        /// non-deleted element. If the current storage is a hash table, this is the number of hash
        /// table slots currently in use, including those in the deleted slot chain.
        /// </summary>
        ///
        /// <remarks>
        /// <para>For any storage, this value must be less than or equal to the length of the
        /// <see cref="m_values"/> array.</para>
        /// <para>For dense array storage, this value must be less than or equal to the value of
        /// <see cref="m_length"/>.</para>
        /// </remarks>
        private int m_totalCount;

        /// <summary>
        /// Contains hash table links, if the current storage is a hash table. If dense array storage
        /// is currently being used, this is null. If this is not null, its length must be the same as
        /// that of the <see cref="m_values"/> array.
        /// </summary>
        private HashLink[] m_hashLinks;

        /// <summary>
        /// The head of the hash table chain containing deleted elements, if hash table storage is
        /// currently being used.
        /// </summary>
        private int m_hashEmptyChainHead;

        /// <summary>
        /// Creates a new empty Array instance.
        /// </summary>
        public ASArray() {
            m_values = Array.Empty<Value>();
            m_length = 0;
            m_nonEmptyCount = 0;
            m_totalCount = 0;
        }

        /// <summary>
        /// Creates a new array with the given elements.
        /// </summary>
        /// <param name="data">The array elements to initialize the array with.</param>
        public ASArray(ReadOnlySpan<ASAny> data) {
            if (data.Length == 0) {
                m_values = Array.Empty<Value>();
                m_length = 0;
                m_nonEmptyCount = 0;
                m_totalCount = 0;
            }
            else {
                m_values = new Value[data.Length];
                for (int i = 0; i < data.Length; i++)
                    m_values[i] = Value.fromAny(data[i]);

                m_length = (uint)data.Length;
                m_nonEmptyCount = data.Length;
                m_totalCount = data.Length;
            }
        }

        /// <summary>
        /// Creates a new Array with the specified initial length.
        /// </summary>
        /// <param name="length">The initial length of the array.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1005: <paramref name="length"/> is not a positive integer.</description></item>
        /// </list>
        /// </exception>
        public ASArray(int length) {
            if (length < 0)
                throw ErrorHelper.createError(ErrorCode.ARRAY_LENGTH_NOT_POSITIVE_INTEGER, length);

            if (length <= MAX_PREALLOC_LENGTH)
                m_values = new Value[Math.Max(length, 4)];
            else
                m_values = Array.Empty<Value>();

            m_length = (uint)length;
            m_nonEmptyCount = 0;
            m_totalCount = 0;
        }

        /// <summary>
        /// Creates a new Array with the specified initial length.
        /// </summary>
        /// <param name="length">The initial length of the array.</param>
        public ASArray(uint length) {
            if (length <= (uint)MAX_PREALLOC_LENGTH)
                m_values = new Value[Math.Max((int)length, 4)];
            else
                m_values = Array.Empty<Value>();

            m_length = length;
            m_nonEmptyCount = 0;
            m_totalCount = 0;
        }

        /// <summary>
        /// This constructor implements the ActionScript 3 Array constructor.
        /// </summary>
        /// <param name="rest">The constructor arguments. If there is only one argument that is of a
        /// numeric type, the array's length is set to that value. Otherwise, the arguments will be
        /// set as elements of the array.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1005: There is only one argument which is of a numeric
        /// type but not a positive integer.</description></item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait]
        public ASArray(RestParam rest) {
            ReadOnlySpan<ASAny> args = rest.getSpan();

            m_nonEmptyCount = 0;
            m_totalCount = 0;

            if (args.Length == 0) {
                m_values = Array.Empty<Value>();
                m_length = 0;
            }
            else if (args.Length == 1 && AS_isNumeric(args[0].value)) {
                double dArg1 = (double)args[0];
                uint uArg1 = (uint)dArg1;

                if ((double)uArg1 != dArg1)
                    throw ErrorHelper.createError(ErrorCode.ARRAY_LENGTH_NOT_POSITIVE_INTEGER, dArg1);

                if (uArg1 <= (uint)MAX_PREALLOC_LENGTH)
                    m_values = new Value[Math.Max((int)uArg1, 4)];
                else
                    m_values = Array.Empty<Value>();

                m_length = uArg1;
            }
            else {
                m_values = new Value[args.Length];

                for (int i = 0; i < args.Length; i++)
                    m_values[i] = Value.fromAny(args[i]);

                m_length = (uint)args.Length;
                m_nonEmptyCount = args.Length;
                m_totalCount = args.Length;
            }
        }

        /// <summary>
        /// Creates a new <see cref="ASArray"/> instance using values from the given typed array.
        /// </summary>
        ///
        /// <param name="array">A typed array containing the elements of the <see cref="ASArray"/>
        /// instance to be created.</param>
        /// <typeparam name="T">The type of the elements in <paramref name="array"/>.</typeparam>
        /// <returns>The created array.</returns>
        public static ASArray fromTypedArray<T>(T[] array) => fromSpan(new ReadOnlySpan<T>(array));

        /// <summary>
        /// Creates a new <see cref="ASArray"/> instance using values from the given span.
        /// </summary>
        ///
        /// <param name="span">A span containing the elements of the array to be created.</param>
        /// <typeparam name="T">The type of the elements in the span.</typeparam>
        /// <returns>The created array.</returns>
        ///
        /// <remarks>
        /// If the element type is <see cref="ASObject"/> or a subclass of it, use of
        /// the faster <see cref="fromObjectSpan{T}(Span{T})"/> method is recommended.
        /// </remarks>
        public static ASArray fromSpan<T>(Span<T> span) => fromSpan((ReadOnlySpan<T>)span);

        /// <summary>
        /// Creates a new <see cref="ASArray"/> instance using values from the given span.
        /// </summary>
        ///
        /// <param name="span">A span containing the elements of the array to be created.</param>
        /// <typeparam name="T">The type of the elements in the span. This must be a
        /// subclass of <see cref="ASObject"/>.</typeparam>
        /// <returns>The created array.</returns>
        ///
        /// <remarks>
        /// Use of this method is recommended instead <see cref="fromSpan{T}(Span{T})"/> when the
        /// element type is <see cref="ASObject"/> or a subclass of it, as it has better
        /// performance
        /// </remarks>
        public static ASArray fromObjectSpan<T>(Span<T> span) where T : ASObject => fromObjectSpan((ReadOnlySpan<T>)span);

        /// <summary>
        /// Creates a new <see cref="ASArray"/> instance using values from the given span.
        /// </summary>
        ///
        /// <param name="span">A span containing the elements of the array to be created.</param>
        /// <typeparam name="T">The type of the elements in the span.</typeparam>
        /// <returns>The created array.</returns>
        ///
        /// <remarks>
        /// If the element type is <see cref="ASObject"/> or a subclass of it, use of
        /// the faster <see cref="fromObjectSpan{T}(ReadOnlySpan{T})"/> method is recommended.
        /// </remarks>
        public static ASArray fromSpan<T>(ReadOnlySpan<T> span) {
            ASArray array = new ASArray();
            array.m_values = span.IsEmpty ? Array.Empty<Value>() : new Value[span.Length];

            Span<Value> values = array.m_values.AsSpan(0, span.Length);
            var converter = GenericTypeConverter<T, ASAny>.instance;

            for (int i = 0; i < values.Length; i++)
                values[i] = Value.fromAny(converter.convert(span[i]));

            array.m_nonEmptyCount = span.Length;
            array.m_totalCount = span.Length;
            array.m_length = (uint)span.Length;

            return array;
        }

        /// <summary>
        /// Creates a new <see cref="ASArray"/> instance using values from the given span.
        /// </summary>
        ///
        /// <param name="span">A span containing the elements of the array to be created.</param>
        /// <typeparam name="T">The type of the elements in the span. This must be a
        /// subclass of <see cref="ASObject"/>.</typeparam>
        /// <returns>The created array.</returns>
        ///
        /// <remarks>
        /// Use of this method is recommended instead <see cref="fromSpan{T}(ReadOnlySpan{T})"/> when the
        /// element type is <see cref="ASObject"/> or a subclass of it, as it has better
        /// performance
        /// </remarks>
        public static ASArray fromObjectSpan<T>(ReadOnlySpan<T> span) where T : ASObject {
            ASArray array = new ASArray();
            array.m_values = span.IsEmpty ? Array.Empty<Value>() : new Value[span.Length];

            Span<Value> values = array.m_values;

            for (int i = 0; i < values.Length; i++)
                values[i] = Value.fromObject(span[i]);

            array.m_nonEmptyCount = span.Length;
            array.m_totalCount = span.Length;
            array.m_length = (uint)span.Length;

            return array;
        }

        /// <summary>
        /// Creates a new <see cref="ASArray"/> instance using values from the given enumerable.
        /// </summary>
        /// <param name="data">The enumerable from which to create the array.</param>
        /// <typeparam name="T">The type of the elements in the enumerable.</typeparam>
        /// <returns>The created array.</returns>
        public static ASArray fromEnumerable<T>(IEnumerable<T> data) {
            var converter = GenericTypeConverter<T, ASAny>.instance;
            var array = new ASArray();
            int index = 0;

            foreach (T item in data) {
                array.AS_setElement((uint)index, converter.convert(item));
                index++;
            }

            return array;
        }

        /// <summary>
        /// Returns a value indicating whether the current array is using dense array storage.
        /// </summary>
        /// <returns>True if the current storage of this Array instance is a dense array, false if the
        /// current storage is a hash table.</returns>
        private bool _isDenseArray() => m_hashLinks == null;

        /// <summary>
        /// Returns a value indicating whether the current array is using dense array storage
        /// and does not have any empty slots.
        /// </summary>
        /// <returns>True if the current storage of this Array instance is a dense array without
        /// any empty slots, otherwise false.</returns>
        private bool _isDenseArrayWithoutEmptySlots() =>
            m_hashLinks == null && m_nonEmptyCount == m_totalCount && m_length == (uint)m_totalCount;

        /// <summary>
        /// Gets the value of an element from the hash table with the specified key. Call this method
        /// only if hash table storage is currently in use.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The value of the object with the given key. If no value is associated with
        /// the key, returns <see cref="Value.empty"/>.</returns>
        private Value _hashGetValue(uint key) {
            HashLink[] hashLinks = m_hashLinks;
            int chain = ((int)key & 0x7FFFFFFF) % m_hashLinks.Length;
            int curIndex = hashLinks[chain].headOfChain;

            while (curIndex != -1) {
                ref HashLink link = ref hashLinks[curIndex];
                if (link.key == key)
                    return m_values[curIndex];
                curIndex = link.next;
            }

            return Value.empty;
        }

        /// <summary>
        /// Sets the value of an element in the hash table with the specified key. Call this method
        /// only if hash table storage is currently in use.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value to set..</param>
        private void _hashSetValue(uint key, Value value) => _hashSetValue(key, value, out _);

        /// <summary>
        /// Sets the value of an element in the hash table with the specified key. Call this method
        /// only if hash table storage is currently in use.
        /// </summary>
        ///
        /// <param name="key">The key.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="isNew">If a new hash table slot was created, this is set to true. If the
        /// value of an existing slot (with the same key) was overwritten, this is set to
        /// false.</param>
        private void _hashSetValue(uint key, Value value, out bool isNew) {
            int chain = 0;

            if (m_hashLinks.Length != 0)
                chain = ((int)key & 0x7FFFFFFF) % m_hashLinks.Length;

            if (key < m_length) {
                // Check for an existing element with the key.
                chain = ((int)key & 0x7FFFFFFF) % m_hashLinks.Length;

                int curIndex = m_hashLinks[chain].headOfChain;
                while (curIndex != -1) {
                    ref HashLink link = ref m_hashLinks[curIndex];
                    if (link.key == key) {
                        m_values[curIndex] = value;
                        isNew = false;
                        return;
                    }
                    curIndex = link.next;
                }
            }

            int newSlotIndex;

            if (m_totalCount > m_nonEmptyCount) {
                // If there are empty slots in the hash table, use one of them.
                newSlotIndex = m_hashEmptyChainHead;
                m_hashEmptyChainHead = m_hashLinks[newSlotIndex].next;
            }
            else {
                // No empty slots. Store the element in a new slot.
                if (m_totalCount == m_hashLinks.Length) {
                    // Resize the hash table.
                    int newSize = Math.Max(checked(m_hashLinks.Length * 2), 4);
                    HashLink[] newLinks = new HashLink[newSize];
                    m_hashLinks.CopyTo(newLinks.AsSpan());
                    m_hashLinks = newLinks;

                    Value[] newValues = new Value[newSize];
                    m_values.CopyTo(newValues.AsSpan());
                    m_values = newValues;

                    _resetHashTableChains();
                    chain = ((int)key & 0x7FFFFFFF) % m_hashLinks.Length;
                }
                newSlotIndex = m_totalCount;
                m_totalCount++;
            }

            ref HashLink newLink = ref m_hashLinks[newSlotIndex];
            ref int headOfChain = ref m_hashLinks[chain].headOfChain;

            newLink.next = headOfChain;
            newLink.key = key;
            headOfChain = newSlotIndex;

            m_values[newSlotIndex] = value;
            m_nonEmptyCount++;
            isNew = true;
        }

        /// <summary>
        /// Deletes the element with the key <paramref name="key"/> from the hash table and returns
        /// its value. Call this method only if hash table storage is currently in use.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The value of the deleted element. If the key does not exist, returns
        /// <see cref="Value.empty"/>.</returns>
        private Value _hashDeleteAndGet(uint key) {
            int chain = ((int)key & 0x7FFFFFFF) % m_hashLinks.Length;

            int curIndex = m_hashLinks[chain].headOfChain;
            int prevIndex = -1;

            while (curIndex != -1) {
                ref HashLink curLink = ref m_hashLinks[curIndex];
                if (curLink.key != key) {
                    prevIndex = curIndex;
                    curIndex = curLink.next;
                    continue;
                }

                ref int prevRef = ref ((prevIndex != -1) ? ref m_hashLinks[prevIndex].next : ref m_hashLinks[chain].headOfChain);
                prevRef = curLink.next;

                ref Value valRef = ref m_values[curIndex];
                Value val = valRef;
                valRef = Value.empty;

                curLink.key = 0;
                curLink.next = m_hashEmptyChainHead;
                m_hashEmptyChainHead = curIndex;
                m_nonEmptyCount--;

                return val;
            }

            return Value.empty;
        }

        /// <summary>
        /// Recomputes the chains in the hash table. This should be called after modifying the keys
        /// directly. Call this method only if hash table storage is currently in use.
        /// </summary>
        private void _resetHashTableChains() {
            HashLink[] hashLinks = m_hashLinks;

            for (int i = 0; i < hashLinks.Length; i++)
                hashLinks[i].headOfChain = -1;

            m_hashEmptyChainHead = -1;

            var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);

            for (int i = 0; i < valuesSpan.Length; i++) {
                ref HashLink link = ref hashLinks[i];

                if (!valuesSpan[i].isEmpty) {
                    int chain = ((int)link.key & 0x7FFFFFFF) % hashLinks.Length;
                    link.next = hashLinks[chain].headOfChain;
                    hashLinks[chain].headOfChain = i;
                }
                else {
                    link.key = 0;
                    link.next = m_hashEmptyChainHead;
                    m_hashEmptyChainHead = i;
                }
            }
        }

        /// <summary>
        /// Returns the value in the array at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The <see cref="Value"/> representing the value at the given index.</returns>
        private Value _getValueAt(uint index) {
            if (index == UInt32.MaxValue)   // Not a valid index
                return default;

            if (_isDenseArray())
                return (index < (uint)m_totalCount) ? m_values[(int)index] : default;

            return _hashGetValue(index);
        }

        /// <summary>
        /// Checks if the load factor of the array is above the given threshold.
        /// </summary>
        ///
        /// <param name="nonEmpty">The number of elements in the array that have non-empty
        /// values.</param>
        /// <param name="length">The total number of elements in the array, including
        /// empty values.</param>
        /// <param name="threshold">The load factor threshold to check. This must be an integer
        /// between 0 and 64.</param>
        ///
        /// <returns>True if the load factor of the array with <paramref name="length"/> elements
        /// and <paramref name="nonEmpty"/> objects is greater than or equal to
        /// <paramref name="threshold"/>, otherwise false.</returns>
        private static bool _isLoadFactorAboveThreshold(int nonEmpty, uint length, int threshold) =>
            ((long)nonEmpty << 6) >= (long)threshold * (long)length;

        /// <summary>
        /// Determines whether an array having <paramref name="length"/> elements and
        /// <paramref name="nonEmpty"/> objects (non-deleted elements) can be represented using
        /// dense array storage.
        /// </summary>
        ///
        /// <param name="nonEmpty">The number of elements in the array that have non-empty
        /// values.</param>
        /// <param name="length">The total number of elements in the array, including
        /// deleted.</param>
        /// <returns>True if dense array storage can be used for the array with the given number of
        /// elements and objects, otherwise false.</returns>
        ///
        /// <remarks>
        /// This method is used by Array class methods such as <see cref="concat"/>,
        /// <see cref="push(RestParam)"/> and <see cref="splice"/>, which need to make storage allocation
        /// decisions modifying an array with the new object count known.
        /// </remarks>
        private static bool _canUseDenseArray(int nonEmpty, uint length) {
            if (length > (uint)Int32.MaxValue)
                return false;
            if (length <= (uint)DENSE_ARRAY_SMALL_SIZE)
                return true;
            return _isLoadFactorAboveThreshold(nonEmpty, length, DENSE_TO_HASH_LOAD_FACTOR);
        }

        /// <summary>
        /// Sets the value of the <see cref="m_totalCount"/> field of the dense array to the value
        /// of <paramref name="totalCount"/>, after adjusting to ensure that the element at the
        /// index <paramref name="totalCount"/> - 1 is nonempty.
        /// </summary>
        ///
        /// <param name="totalCount">
        /// An upper bound for <see cref="m_totalCount"/>, which must be less than the length of
        /// the <see cref="m_values"/> array. Callers must ensure that all elements
        /// in <see cref="m_values"/> at indices not less than this value are empty; this is not
        /// checked in this method and a violation of this condition may break the ASArray class
        /// invariants.
        /// </param>
        private void _setDenseArrayTotalCount(int totalCount) {
            Value[] values = m_values;
            while (totalCount > 0 && values[totalCount - 1].isEmpty)
                totalCount--;
            m_totalCount = totalCount;
        }

        /// <summary>
        /// Transforms the underlying storage of this array from a dense array to a hash table.
        /// </summary>
        private void _denseArrayToHash() {
            if (m_values.Length == 0)
                m_values = new Value[4];

            HashLink[] hashLinks = new HashLink[m_values.Length];

            m_hashEmptyChainHead = -1;
            for (int i = 0; i < hashLinks.Length; i++)
                hashLinks[i].headOfChain = -1;

            var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);

            for (int i = 0; i < valuesSpan.Length; i++) {
                ref HashLink link = ref hashLinks[i];
                if (!valuesSpan[i].isEmpty) {
                    link.headOfChain = i;
                    link.key = (uint)i;
                    link.next = -1;
                }
                else {
                    link.key = 0;
                    link.next = m_hashEmptyChainHead;
                    m_hashEmptyChainHead = i;
                }
            }

            m_hashLinks = hashLinks;
        }

        /// <summary>
        /// Transforms the underlying storage of this array from a hash table to a dense array.
        /// </summary>
        /// <remarks>
        /// The conversion to a dense array by this method only uses the keys. The hash table chains
        /// need not be correct for the conversion to work.
        /// </remarks>
        private void _hashToDenseArray() => _hashToDenseArray((int)m_length);

        /// <summary>
        /// Transforms the underlying storage of this array from a hash table to a dense array,
        /// specifying the amount of memory to allocate for the dense array.
        /// </summary>
        /// <param name="newLength">The number of elements to allocate for the dense array. This must
        /// be greater than the largest key currently in the hash table.</param>
        /// <remarks>
        /// The conversion to a dense array by this method only uses the keys. The hash table chains
        /// need not be correct for the conversion to work.
        /// </remarks>
        private void _hashToDenseArray(int newLength) {
            Value[] newDenseArray = new Value[newLength];
            HashLink[] hashLinks = m_hashLinks;
            int newTotalCount = 0;

            for (int i = 0; i < hashLinks.Length; i++) {
                Value val = m_values[i];
                if (!val.isEmpty) {
                    int index = (int)hashLinks[i].key;
                    newDenseArray[index] = val;
                    newTotalCount = Math.Max(newTotalCount, index + 1);
                }
            }

            m_hashLinks = null;
            m_hashEmptyChainHead = -1;
            m_values = newDenseArray;
            m_totalCount = newTotalCount;
        }

        /// <summary>
        /// Converts the array storage from dense array to hash table (or vice versa) if needed, based
        /// on the current load factor.
        /// </summary>
        /// <param name="isDelete">Set this to true if this method is called after deleting elements
        /// from the array, otherwise set to false.</param>
        private void _updateArrayStorage(bool isDelete = false) {
            if (_isDenseArray()) {
                if (m_totalCount > DENSE_ARRAY_SMALL_SIZE
                    && !_isLoadFactorAboveThreshold(m_nonEmptyCount, (uint)m_totalCount, DENSE_TO_HASH_LOAD_FACTOR))
                {
                    _denseArrayToHash();
                    return;
                }

                if (!isDelete || m_totalCount > (m_values.Length >> 2))
                    return;

                int newArraySize = Math.Max(m_values.Length >> 1, 4);

                if (newArraySize <= DENSE_ARRAY_SMALL_SIZE
                    || _isLoadFactorAboveThreshold(m_nonEmptyCount, (uint)m_totalCount, DENSE_TO_HASH_LOAD_FACTOR))
                {
                    Array.Resize(ref m_values, newArraySize);
                }
                else {
                    _denseArrayToHash();
                }
            }
            else {
                if (m_length <= (uint)DENSE_ARRAY_SMALL_SIZE
                    || (m_length <= (uint)Int32.MaxValue
                        && _isLoadFactorAboveThreshold(m_nonEmptyCount, m_length, HASH_TO_DENSE_LOAD_FACTOR)))
                {
                    _hashToDenseArray();
                }
            }
        }

        /// <summary>
        /// This method is used when the <see cref="length"/> property is set to a value less than
        /// the current value. It truncates the array accordingly.
        /// </summary>
        /// <param name="newLength">The new value to be set to the <see cref="length"/>
        /// property.</param>
        private void _trimToLength(uint newLength) {
            if (_isDenseArray())
                trimDenseArray(newLength);
            else
                trimHash(newLength);

            // Dense array: Clear any elements with an index greater than or
            // equal to newLength.

            void trimDenseArray(uint newlen) {
                if (newlen >= m_totalCount) {
                    m_length = newlen;
                    return;
                }

                var clearSpan = m_values.AsSpan((int)newlen, m_totalCount - (int)newlen);
                for (int i = 0; i < clearSpan.Length; i++) {
                    if (!clearSpan[i].isEmpty) {
                        clearSpan[i] = default;
                        m_nonEmptyCount--;
                    }
                }

                m_length = newlen;
                m_totalCount = (int)newlen;
                _updateArrayStorage(isDelete: true);
            }

            // For hash table storage.

            void trimHash(uint newlen) {
                HashLink[] hashLinks = m_hashLinks;

                for (int chain = 0; chain < hashLinks.Length; chain++) {
                    int curIndex = m_hashLinks[chain].headOfChain;
                    int prevIndex = -1;

                    while (curIndex != -1) {
                        ref HashLink link = ref hashLinks[curIndex];
                        if (link.key < newLength) {
                            prevIndex = curIndex;
                            curIndex = link.next;
                            continue;
                        }

                        int nextIndex = link.next;
                        ref int prevRef = ref ((prevIndex != -1)
                            ? ref hashLinks[prevIndex].next
                            : ref hashLinks[chain].headOfChain
                        );
                        prevRef = nextIndex;

                        m_values[curIndex] = Value.empty;
                        link.key = 0;
                        link.next = m_hashEmptyChainHead;
                        m_hashEmptyChainHead = curIndex;
                        m_nonEmptyCount--;

                        curIndex = nextIndex;
                    }
                }

                m_length = newLength;
                _updateArrayStorage(isDelete: true);
            }
        }

        /// <summary>
        /// Gets an array index from an floating-point value given as a property name.
        /// </summary>
        /// <param name="key">The floating-point value given as the property name.</param>
        /// <param name="index">The array index obtained from the key.</param>
        /// <returns>True if an array index could be obtained, false otherwise.</returns>
        private static bool _tryGetIndexFromNumberKey(double key, out uint index) {
            index = (uint)key;
            return key == (double)index && index != UInt32.MaxValue;
        }

        /// <summary>
        /// This function is used to normalize the indices passed into methods such as
        /// <see cref="indexOf"/>, <see cref="lastIndexOf"/> and <see cref="slice"/>.
        /// </summary>
        /// <param name="index">The index to normalize.</param>
        /// <param name="length">The length of the array.</param>
        /// <returns>The normalized index.</returns>
        private static uint _normalizeIndex(double index, uint length) {
            if (Double.IsNaN(index))
                return 0;

            return (index <= -1.0)
                ? (uint)Math.Max(Math.Truncate(index) + (double)length, 0)
                : (uint)Math.Min(index, (double)length);
        }

        /// <summary>
        /// Copies the contents of the Array into a typed span. Each element to be copied will be
        /// converted to the type <typeparamref name="T"/>.
        /// </summary>
        ///
        /// <param name="srcIndex">The index of the first element of the range of the
        /// source Array to be copied to <paramref name="dest"/>.</param>
        /// <param name="dest">The span to copy the Array's contents to. The length of
        /// the range to be copied is the length of this span.</param>
        ///
        /// <typeparam name="T">The type of the destination span.</typeparam>
        public void copyToSpan<T>(uint srcIndex, Span<T> dest) {
            if (srcIndex > m_length || (uint)dest.Length > m_length - srcIndex)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(srcIndex));

            if (dest.Length == 0)
                return;

            var converter = GenericTypeConverter<ASAny, T>.instance;

            if (_isDenseArrayWithoutEmptySlots()) {
                var valuesSpan = new ReadOnlySpan<Value>(m_values, (int)srcIndex, dest.Length);
                for (int i = 0; i < valuesSpan.Length; i++)
                    dest[i] = converter.convert(valuesSpan[i].toAny());
            }
            else {
                for (int i = 0; i < dest.Length; i++)
                    dest[i] = converter.convert(AS_getElement(srcIndex + (uint)i));
            }
        }

        /// <summary>
        /// Creates a typed array of type <typeparamref name="T"/> containing all elements of this
        /// Array converted to that type.
        /// </summary>
        /// <typeparam name="T">The type of the array to return.</typeparam>
        /// <returns>A typed array.</returns>
        /// <remarks>
        /// If the length of the array is greater than 2^31-1, only elements until that index will be
        /// included in the returned array.
        /// </remarks>
        public T[] toTypedArray<T>() {
            T[] arr = new T[(int)Math.Min(m_length, (uint)Int32.MaxValue)];
            copyToSpan<T>(0, arr);
            return arr;
        }

        /// <summary>
        /// Returns an instance of <see cref="IEnumerable{T}"/> that enumerates the elements
        /// of this array, converting them to type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to which to convert the array elements to.</typeparam>
        public IEnumerable<T> asEnumerable<T>() {
            var converter = GenericTypeConverter<ASAny, T>.instance;
            uint length = this.length;

            if (_isDenseArrayWithoutEmptySlots()) {
                for (int i = 0; i < length; i++)
                    yield return converter.convert(m_values[i].toAny());
            }
            else {
                for (uint i = 0; i < length; i++)
                    yield return converter.convert(AS_getElement(i));
            }
        }

        /// <summary>
        /// Gets or sets the length of the array.
        /// </summary>
        ///
        /// <remarks>
        /// <para>When an index in the array, that is equal to or greater to this value is assigned
        /// to; this property is set to the assigned index plus one. Deleting any element from the
        /// array (including the one with the highest index), however, will not change the length
        /// property.</para>
        /// <para>If this property is explicitly set to a value less than the existing value, all
        /// elements whose indices are greater than or equal to the new length are deleted.</para>
        /// </remarks>
        [AVM2ExportTrait]
        public virtual uint length {
            get => m_length;
            set {
                if (value < m_length)
                    _trimToLength(value);
                else
                    m_length = value;
            }
        }

        private ASAny _getElementEmptyOrNonValidIndex(string indexStr) {
            var name = QName.publicName(indexStr);
            BindStatus bindStatus = base.AS_tryGetProperty(name, out ASAny value);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return value;
            throw ErrorHelper.createBindingError(AS_class.name, name, bindStatus);
        }

        private bool _hasElementEmptyOrNonValidIndex(string indexStr) =>
            base.AS_hasProperty(QName.publicName(indexStr));

        private void _setElementNonValidIndex(string indexStr, ASAny value) {
            var name = QName.publicName(indexStr);
            BindStatus bindStatus = base.AS_trySetProperty(name, value);
            if (bindStatus != BindStatus.SUCCESS && bindStatus != BindStatus.SOFT_SUCCESS)
                throw ErrorHelper.createBindingError(AS_class.name, name, bindStatus);
        }

        private bool _deleteElementNonValidIndex(string indexStr) =>
            base.AS_deleteProperty(QName.publicName(indexStr));

        /// <summary>
        /// Gets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The value of the element, or undefined if no element exists at the given
        /// index.</returns>
        public ASAny AS_getElement(uint index) {
            Value val = _getValueAt(index);
            return !val.isEmpty ? val.toAny() : _getElementEmptyOrNonValidIndex(ASuint.AS_convertString(index));
        }

        /// <summary>
        /// Returns a Boolean value indicating whether the current <see cref="ASArray"/> instance
        /// has an element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>True, if the current instance has an element at the given index, false
        /// otherwise.</returns>
        public bool AS_hasElement(uint index) {
            Value val = _getValueAt(index);
            return !val.isEmpty || _hasElementEmptyOrNonValidIndex(ASuint.AS_convertString(index));
        }

        /// <summary>
        /// Sets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value of the element.</param>
        public void AS_setElement(uint index, ASAny value) {
            if (index == UInt32.MaxValue) {  // Not a valid array index
                _setElementNonValidIndex(ASuint.AS_convertString(index), value);
                return;
            }

            if (_isDenseArray()) {
                if (index < (uint)m_values.Length) {
                    // If the new element can be inserted into the dense array without
                    // any reallocation, do it without any load factor check.
                    ref Value slot = ref m_values[(int)index];
                    if (slot.isEmpty)
                        m_nonEmptyCount++;
                    if (index >= (uint)m_totalCount)
                        m_totalCount = (int)index + 1;
                    slot = Value.fromAny(value);
                }
                else {
                    // If the element cannot be inserted, convert the storage to the hash
                    // table form if the index is greater than the maximum permitted dense
                    // array index, or if the load factor would go below a certain threshold.

                    if (!_canUseDenseArray(m_nonEmptyCount + 1, index + 1)) {
                        _denseArrayToHash();
                        _hashSetValue(index, Value.fromAny(value));
                    }
                    else {
                        Array.Resize(ref m_values, Math.Max(m_values.Length * 2, (int)index + 1));
                        m_values[(int)index] = Value.fromAny(value);
                        m_nonEmptyCount++;
                        m_totalCount = (int)index + 1;
                    }
                }

                m_length = Math.Max(m_length, index + 1);
            }
            else {
                _hashSetValue(index, Value.fromAny(value), out bool newHashSlotCreated);

                // If an element is added to the hash table (and it did not overwrite an
                // existing slot), check whether the hash table's load factor has crossed
                // the dense array threshold and convert to a dense array if that is the case.
                // This requires the array's length property to have a correct value, so update it first.
                m_length = Math.Max(m_length, index + 1);

                if (newHashSlotCreated)
                    _updateArrayStorage();
            }
        }

        /// <summary>
        /// Deletes the value of the element at the given index. If an element exists at the index and
        /// its value is not undefined, its value is set to undefined and this method returns true.
        /// Otherwise, this method returns false.
        /// </summary>
        ///
        /// <param name="index">The index of the element to delete.</param>
        /// <returns>True if the property was deleted, false otherwise.</returns>
        public bool AS_deleteElement(uint index) {
            if (index == UInt32.MaxValue)   // Not a valid array index
                return _deleteElementNonValidIndex(ASuint.AS_convertString(index));

            if (_isDenseArray()) {
                if (index >= (uint)m_totalCount)
                    return false;

                ref Value slot = ref m_values[(int)index];

                if (slot.isEmpty)
                    return false;

                slot = Value.empty;
                m_nonEmptyCount--;

                if ((int)index == m_totalCount - 1)
                    _setDenseArrayTotalCount(m_totalCount - 1);
            }
            else {
                Value deleted = _hashDeleteAndGet(index);
                if (deleted.isEmpty)
                    return false;
            }

            _updateArrayStorage(isDelete: true);
            return true;
        }

        /// <summary>
        /// Gets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The value of the element, or undefined if no element exists at the given
        /// index.</returns>
        ///
        /// <remarks>
        /// If the index is negative, it is converted to a string and the value of the dynamic
        /// property with the string as its name is returned. However, if this method is called on a
        /// subclass of Array that is non-dynamic, an error is thrown.
        /// </remarks>
        public ASAny AS_getElement(int index) =>
            (index >= 0) ? AS_getElement((uint)index) : _getElementEmptyOrNonValidIndex(ASint.AS_convertString(index));

        /// <summary>
        /// Returns a Boolean value indicating whether the current instance has an element at the
        /// given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>True, if the current instance has an element at the given index, false
        /// otherwise.</returns>
        ///
        /// <remarks>
        /// If the index is negative, it is converted to a string and the value of the dynamic
        /// property with the string as its name is checked. However, if this method is called on a
        /// subclass of Array that is non-dynamic, an error is thrown.
        /// </remarks>
        public bool AS_hasElement(int index) =>
            (index >= 0) ? AS_hasElement((uint)index) : _hasElementEmptyOrNonValidIndex(ASint.AS_convertString(index));

        /// <summary>
        /// Sets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value of the element.</param>
        ///
        /// <remarks>
        /// If the index is negative, it is converted to a string and the value of the dynamic
        /// property with the string as its name is set. However, if this method is called on a
        /// subclass of Array that is non-dynamic, an error is thrown.
        /// </remarks>
        public void AS_setElement(int index, ASAny value) {
            if (index >= 0)
                AS_setElement((uint)index, value);
            else
                _setElementNonValidIndex(ASint.AS_convertString(index), value);
        }

        /// <summary>
        /// Deletes the value of the element at the given index. If an element exists at the index,
        /// this method returns true. Otherwise, this method returns false.
        /// </summary>
        /// <param name="index">The index of the element to delete.</param>
        /// <returns>True if the property was deleted, false otherwise.</returns>
        ///
        /// <remarks>
        /// If the index is negative, it is converted to a string and the dynamic property with that
        /// string as its name is deleted. However, if this method is called on a subclass of Array
        /// that is non-dynamic, this method returns false.
        /// </remarks>
        public bool AS_deleteElement(int index) =>
            (index >= 0) ? AS_deleteElement((uint)index) : _deleteElementNonValidIndex(ASint.AS_convertString(index));

        /// <summary>
        /// Gets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The value of the element, or undefined if no element exists at the given
        /// index.</returns>
        ///
        /// <remarks>
        /// If the index is not valid, it is converted to a string and the value of the dynamic
        /// property with the string as its name is returned. However, if this method is called on a
        /// subclass of Array that is non-dynamic, an error is thrown.
        /// </remarks>
        public ASAny AS_getElement(double index) {
            return _tryGetIndexFromNumberKey(index, out uint uintIndex)
                ? AS_getElement(uintIndex)
                : _getElementEmptyOrNonValidIndex(ASNumber.AS_convertString(index));
        }

        /// <summary>
        /// Returns a Boolean value indicating whether the current instance has an element at the
        /// given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>True, if the current instance has an element at the given index, false
        /// otherwise.</returns>
        ///
        /// <remarks>
        /// If the index is not valid, it is converted to a string and the value of the dynamic
        /// property with the string as its name is checked. However, if this method is called on a
        /// subclass of Array that is non-dynamic, an error is thrown.
        /// </remarks>
        public bool AS_hasElement(double index) {
            return _tryGetIndexFromNumberKey(index, out uint uintIndex)
                ? AS_hasElement(uintIndex)
                : _hasElementEmptyOrNonValidIndex(ASNumber.AS_convertString(index));
        }

        /// <summary>
        /// Sets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value of the element.</param>
        ///
        /// <remarks>
        /// If the index is not valid, it is converted to a string and the value of the dynamic
        /// property with the string as its name is set. However, if this method is called on a
        /// subclass of Array that is non-dynamic, an error is thrown.
        /// </remarks>
        public void AS_setElement(double index, ASAny value) {
            if (_tryGetIndexFromNumberKey(index, out uint uintIndex))
                AS_setElement(uintIndex, value);
            else
                _setElementNonValidIndex(ASNumber.AS_convertString(index), value);
        }

        /// <summary>
        /// Deletes the value of the element at the given index. If an element exists at the index,
        /// this method returns true. Otherwise, this method returns false.
        /// </summary>
        /// <param name="index">The index of the element to delete.</param>
        /// <returns>True if the property was deleted, false otherwise.</returns>
        ///
        /// <remarks>
        /// If the index is not valid, it is converted to a string and the dynamic property with that
        /// string as its name is deleted. However, if this method is called on a subclass of Array
        /// that is non-dynamic, this method returns false.
        /// </remarks>
        public bool AS_deleteElement(double index) {
            return _tryGetIndexFromNumberKey(index, out uint uintIndex)
                ? AS_deleteElement(uintIndex)
                : _deleteElementNonValidIndex(ASNumber.AS_convertString(index));
        }

        /// <summary>
        /// Creates a shallow copy of the Array instance.
        /// </summary>
        /// <returns>A shallow copy of the Array instance.</returns>
        public ASArray clone() {
            ASArray cloneArray = new ASArray();

            cloneArray.m_length = m_length;
            cloneArray.m_nonEmptyCount = m_nonEmptyCount;
            cloneArray.m_totalCount = m_totalCount;
            cloneArray.m_values = m_values.AsSpan().ToArray();

            if (m_hashLinks != null) {
                cloneArray.m_hashLinks = m_hashLinks.AsSpan().ToArray();
                cloneArray.m_hashEmptyChainHead = m_hashEmptyChainHead;
            }

            return cloneArray;
        }

        /// <summary>
        /// Gets or sets the value of the element at the specified index.
        /// </summary>
        /// <param name="i">The index.</param>
        public ASAny this[uint i] {
            get => AS_getElement(i);
            set => AS_setElement(i, value);
        }

        /// <summary>
        /// Gets or sets the value of the element at the specified index.
        /// </summary>
        /// <param name="i">The index.</param>
        ///
        /// <exception cref="AVM2Exception">ArgumentError #10061: <paramref name="i"/> is less than 0.</exception>
        ///
        /// <remarks>
        /// This method throws an exception when the index is negative, unlike <see cref="AS_getElement(Int32)"/>
        /// which converts the negative index to a string and does a dynamic property lookup.
        /// </remarks>
        public ASAny this[int i] {
            get {
                if (i < 0)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(i));
                return AS_getElement((uint)i);
            }
            set {
                if (i < 0)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(i));
                AS_setElement(i, value);
            }
        }

        #region BindingMethodOverrides

        /// <inheritdoc/>
        public override bool AS_hasProperty(
            in QName name,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, allowLeadingZeroes: false, out uint index)
                && !_getValueAt(index).isEmpty)
            {
                return true;
            }

            return base.AS_hasProperty(name, options);
        }

        /// <inheritdoc/>
        public override bool AS_hasProperty(
            string name, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && NumberFormatHelper.parseArrayIndex(name, allowLeadingZeroes: false, out uint index)
                && !_getValueAt(index).isEmpty)
            {
                return true;
            }

            return base.AS_hasProperty(name, nsSet, options);
        }

        /// <inheritdoc/>
        public override bool AS_hasPropertyObj(
            ASAny key,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && AS_isUint(key.value) && !_getValueAt((uint)key).isEmpty)
            {
                return true;
            }

            return (key.value is ASQName qName)
                ? AS_hasProperty(QName.fromASQName(qName), options)
                : AS_hasProperty(QName.publicName(ASAny.AS_convertString(key)), options);
        }

        /// <inheritdoc/>
        public override bool AS_hasPropertyObj(
            ASAny key, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && AS_isUint(key.value) && !_getValueAt((uint)key).isEmpty)
            {
                return true;
            }

            return (key.value is ASQName qName)
                ? AS_hasProperty(QName.fromASQName(qName), options)
                : AS_hasProperty(ASAny.AS_convertString(key), nsSet, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryGetProperty(
            in QName name, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, allowLeadingZeroes: false, out uint index))
            {
                Value v = _getValueAt(index);
                if (!v.isEmpty) {
                    value = v.toAny();
                    return BindStatus.SUCCESS;
                }
            }
            return base.AS_tryGetProperty(name, out value, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryGetProperty(
            string name, in NamespaceSet nsSet, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && NumberFormatHelper.parseArrayIndex(name, allowLeadingZeroes: false, out uint index))
            {
                Value v = _getValueAt(index);
                if (!v.isEmpty) {
                    value = v.toAny();
                    return BindStatus.SUCCESS;
                }
            }
            return base.AS_tryGetProperty(name, nsSet, out value, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryGetPropertyObj(
            ASAny key, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && AS_isUint(key.value))
            {
                Value v = _getValueAt((uint)key);
                if (!v.isEmpty) {
                    value = v.toAny();
                    return BindStatus.SUCCESS;
                }
            }

            return (key.value is ASQName qName)
                ? AS_tryGetProperty(QName.fromASQName(qName), out value, options)
                : AS_tryGetProperty(QName.publicName(ASAny.AS_convertString(key)), out value, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryGetPropertyObj(
            ASAny key, in NamespaceSet nsSet, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && AS_isUint(key.value))
            {
                Value v = _getValueAt((uint)key);
                if (!v.isEmpty) {
                    value = v.toAny();
                    return BindStatus.SUCCESS;
                }
            }

            return (key.value is ASQName qName)
                ? AS_tryGetProperty(QName.fromASQName(qName), out value, options)
                : AS_tryGetProperty(ASAny.AS_convertString(key), nsSet, out value, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_trySetProperty(
            in QName name, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, allowLeadingZeroes: false, out uint index)
                && index != UInt32.MaxValue)
            {
                AS_setElement(index, value);
                return BindStatus.SUCCESS;
            }
            return base.AS_trySetProperty(name, value, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_trySetProperty(
            string name, in NamespaceSet nsSet, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && NumberFormatHelper.parseArrayIndex(name, allowLeadingZeroes: false, out uint index)
                && index != UInt32.MaxValue)
            {
                AS_setElement(index, value);
                return BindStatus.SUCCESS;
            }
            return base.AS_trySetProperty(name, nsSet, value, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_trySetPropertyObj(
            ASAny key, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && AS_isUint(key.value))
            {
                uint index = (uint)key;
                if (index != UInt32.MaxValue) {
                    AS_setElement(index, value);
                    return BindStatus.SUCCESS;
                }
            }

            return (key.value is ASQName qName)
                ? AS_trySetProperty(QName.fromASQName(qName), value, options)
                : AS_trySetProperty(QName.publicName(ASAny.AS_convertString(key)), value, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_trySetPropertyObj(
            ASAny key, in NamespaceSet nsSet, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && AS_isUint(key.value))
            {
                uint index = (uint)key;
                if (index != UInt32.MaxValue) {
                    AS_setElement(index, value);
                    return BindStatus.SUCCESS;
                }
            }

            return (key.value is ASQName qName)
                ? AS_trySetProperty(QName.fromASQName(qName), value, options)
                : AS_trySetProperty(ASAny.AS_convertString(key), nsSet, value, options);
        }

        /// <inheritdoc/>
        public override bool AS_deleteProperty(
            in QName name, BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, allowLeadingZeroes: false, out uint index)
                && index != UInt32.MaxValue)
            {
                return AS_deleteElement(index);
            }
            return base.AS_deleteProperty(name, options);
        }

        /// <inheritdoc/>
        public override bool AS_deleteProperty(
            string name, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && NumberFormatHelper.parseArrayIndex(name, allowLeadingZeroes: false, out uint index)
                && index != UInt32.MaxValue)
            {
                return AS_deleteElement(index);
            }
            return base.AS_deleteProperty(name, nsSet, options);
        }

        /// <inheritdoc/>
        public override bool AS_deletePropertyObj(
            ASAny key, BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && AS_isUint(key.value))
            {
                uint index = (uint)key;
                if (index != UInt32.MaxValue)
                    return AS_deleteElement(index);
            }

            return (key.value is ASQName qName)
                ? AS_deleteProperty(QName.fromASQName(qName), options)
                : AS_deleteProperty(QName.publicName(ASAny.AS_convertString(key)), options);
        }

        /// <inheritdoc/>
        public override bool AS_deletePropertyObj(
            ASAny key, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && AS_isUint(key.value))
            {
                uint index = (uint)key;
                if (index != UInt32.MaxValue)
                    return AS_deleteElement(index);
            }

            return (key.value is ASQName qName)
                ? AS_deleteProperty(QName.fromASQName(qName), options)
                : AS_deleteProperty(ASAny.AS_convertString(key), nsSet, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryCallProperty(
            in QName name, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, allowLeadingZeroes: false, out uint index))
            {
                Value v = _getValueAt(index);
                if (!v.isEmpty) {
                    return v.toAny().AS_tryInvoke(this, args, out result)
                        ? BindStatus.SUCCESS
                        : BindStatus.FAILED_NOTFUNCTION;
                }
            }
            return base.AS_tryCallProperty(name, args, out result, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryCallProperty(
            string name, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && NumberFormatHelper.parseArrayIndex(name, allowLeadingZeroes: false, out uint index))
            {
                Value v = _getValueAt(index);
                if (!v.isEmpty) {
                    return v.toAny().AS_tryInvoke(this, args, out result)
                        ? BindStatus.SUCCESS
                        : BindStatus.FAILED_NOTFUNCTION;
                }
            }
            return base.AS_tryCallProperty(name, nsSet, args, out result, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryCallPropertyObj(
            ASAny key, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && AS_isUint(key.value))
            {
                Value v = _getValueAt((uint)key);
                if (!v.isEmpty) {
                    return v.toAny().AS_tryInvoke(this, args, out result)
                        ? BindStatus.SUCCESS
                        : BindStatus.FAILED_NOTFUNCTION;
                }
            }

            return (key.value is ASQName qName)
                ? AS_tryCallProperty(QName.fromASQName(qName), args, out result, options)
                : AS_tryCallProperty(QName.publicName(ASAny.AS_convertString(key)), args, out result, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryCallPropertyObj(
            ASAny key, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && AS_isUint(key.value))
            {
                Value v = _getValueAt((uint)key);
                if (!v.isEmpty) {
                    return v.toAny().AS_tryInvoke(this, args, out result)
                        ? BindStatus.SUCCESS
                        : BindStatus.FAILED_NOTFUNCTION;
                }
            }

            return (key.value is ASQName qName)
                ? AS_tryCallProperty(QName.fromASQName(qName), args, out result, options)
                : AS_tryCallProperty(ASAny.AS_convertString(key), nsSet, args, out result, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryConstructProperty(
            in QName name, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, allowLeadingZeroes: false, out uint index))
            {
                Value v = _getValueAt(index);
                if (!v.isEmpty) {
                    return v.toAny().AS_tryConstruct(args, out result)
                        ? BindStatus.SUCCESS
                        : BindStatus.FAILED_NOTCONSTRUCTOR;
                }
            }
            return base.AS_tryConstructProperty(name, args, out result, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryConstructProperty(
            string name, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && NumberFormatHelper.parseArrayIndex(name, allowLeadingZeroes: false, out uint index))
            {
                Value v = _getValueAt(index);
                if (!v.isEmpty) {
                    return v.toAny().AS_tryConstruct(args, out result)
                        ? BindStatus.SUCCESS
                        : BindStatus.FAILED_NOTCONSTRUCTOR;
                }
            }
            return base.AS_tryConstructProperty(name, nsSet, args, out result, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryConstructPropertyObj(
            ASAny key, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && AS_isUint(key.value))
            {
                Value v = _getValueAt((uint)key);
                if (!v.isEmpty) {
                    return v.toAny().AS_tryConstruct(args, out result)
                        ? BindStatus.SUCCESS
                        : BindStatus.FAILED_NOTCONSTRUCTOR;
                }
            }

            return (key.value is ASQName qName)
                ? AS_tryConstructProperty(QName.fromASQName(qName), args, out result, options)
                : AS_tryConstructProperty(QName.publicName(ASAny.AS_convertString(key)), args, out result, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryConstructPropertyObj(
            ASAny key, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && AS_isUint(key.value))
            {
                Value v = _getValueAt((uint)key);
                if (!v.isEmpty) {
                    return v.toAny().AS_tryConstruct(args, out result)
                        ? BindStatus.SUCCESS
                        : BindStatus.FAILED_NOTCONSTRUCTOR;
                }
            }

            return (key.value is ASQName qName)
                ? AS_tryConstructProperty(QName.fromASQName(qName), args, out result, options)
                : AS_tryConstructProperty(ASAny.AS_convertString(key), nsSet, args, out result, options);
        }

        /// <summary>
        /// Gets the one-based index of the next enumerable dynamic property after the given index. If
        /// the object is not dynamic or has no enumerable dynamic properties after the given index, 0
        /// is returned. The index returned can be used with methods such as
        /// <see cref="AS_nameAtIndex"/> and <see cref="AS_valueAtIndex"/>.
        /// </summary>
        ///
        /// <param name="index">The index of the property from where to search for the next property.
        /// A value of 0 will return the index of the first enumerable property.</param>
        /// <returns>The one-based index of the next enumerable property, or 0 if there are no more
        /// enumerable properties.</returns>
        public override int AS_nextIndex(int index) {
            if (index < m_totalCount) {
                do {
                    index++;
                } while (index <= m_totalCount && m_values[index - 1].isEmpty);

                if (index <= m_totalCount)
                    return index;

                index = m_totalCount;
            }

            int baseNextIndex = base.AS_nextIndex(index - m_totalCount);
            return (baseNextIndex == 0) ? 0 : m_totalCount + baseNextIndex;
        }

        /// <summary>
        /// Gets the name of the dynamic property at the given index.
        /// </summary>
        /// <param name="index">The one-based index of the property. This index is usually obtained
        /// from the <see cref="AS_nextIndex"/> method.</param>
        /// <returns>The property name.</returns>
        public override ASAny AS_nameAtIndex(int index) {
            if (index > m_totalCount)
                return base.AS_nameAtIndex(index - m_totalCount);

            return _isDenseArray() ? (uint)(index - 1) : m_hashLinks[index - 1].key;
        }

        /// <summary>
        /// Gets the value of the dynamic property at the given index.
        /// </summary>
        /// <param name="index">The one-based index of the property. This index is usually returned
        /// from the <see cref="AS_nextIndex"/> method.</param>
        /// <returns>The property value.</returns>
        public override ASAny AS_valueAtIndex(int index) {
            if (index > m_totalCount)
                return base.AS_valueAtIndex(index - m_totalCount);

            return m_values[index - 1].toAny();
        }

        #endregion

        /// <summary>
        /// Creates a copy of the current Array, concatenates the given arguments to it and returns
        /// the created Array.
        /// </summary>
        /// <param name="args">The arguments to concatenate to the new Array. If any of the arguments
        /// is an Array or Vector, each element of that Array or Vector will be individually appended
        /// to the new Array (however, Arrays or Vectors within them are not flattened
        /// further).</param>
        /// <returns>An array containing the elements of this array, concatenated with the
        /// arguments.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual ASArray concat(RestParam args = default) {
            if (args.length == 0)
                return clone();

            int nArgs = args.length;
            int resultNonEmptyCount = m_nonEmptyCount;
            uint resultLength = m_length;

            // Determine the amount of space that needs to be allocated for the result array
            // and its load factor (which determines whether the result array storage will be dense
            // or a hash table).

            for (int i = 0; i < nArgs; i++) {
                ASObject arg = args[i].value;

                uint argLength;
                int argNonEmptyCount;

                if (arg is ASArray argArray) {
                    argLength = argArray.m_length;
                    argNonEmptyCount = argArray.m_nonEmptyCount;
                }
                else if (arg is ASVectorAny argVector) {
                    argLength = (uint)argVector.length;
                    argNonEmptyCount = (int)argLength;
                }
                else {
                    argLength = 1;
                    argNonEmptyCount = 1;
                }

                if (resultLength >= UInt32.MaxValue - argLength) {
                    // Don't use any more arguments when the maximum length is reached.
                    // In this case, we do not have the exact nonEmptyCount for the (prefix of) the last
                    // argument that will be appended, so use the minimum of the prefix length and
                    // the full nonEmptyCount of the argument as an approximate value.
                    // This is OK as we are using the result nonEmptyCount calculated here only to
                    // determine the storage type of the result; the exact count will be calculated
                    // when the result array is actually being constructed.

                    resultLength = UInt32.MaxValue;
                    resultNonEmptyCount += (int)Math.Min((uint)argNonEmptyCount, UInt32.MaxValue - argLength);
                    nArgs = i + 1;
                    break;
                }

                resultLength += argLength;
                resultNonEmptyCount += argNonEmptyCount;
            }

            if (resultNonEmptyCount < 0)
                throw new OutOfMemoryException();

            // Create the result array.

            bool resultIsHash = !_canUseDenseArray(resultNonEmptyCount, resultLength);

            ASArray resultArray = new ASArray();
            resultArray.m_length = resultLength;

            if (resultIsHash) {
                resultArray.m_values = new Value[resultNonEmptyCount];
                resultArray._denseArrayToHash();
            }
            else {
                resultArray.m_values = new Value[(int)resultLength];
            }

            uint resCurrentLength = 0;

            // Copy current array.
            appendArrayToResult(this);

            // Copy from arguments
            for (int i = 0; i < nArgs; i++) {
                ASAny arg = args[i];

                if (arg.value is ASArray argArray) {
                    appendArrayToResult(argArray);
                }
                else if (arg.value is ASVectorAny argVector) {
                    appendVectorToResult(argVector);
                }
                else if (resultIsHash) {
                    resultArray._hashSetValue(resCurrentLength, Value.fromAny(arg));
                    resCurrentLength++;
                }
                else {
                    resultArray.m_values[(int)resCurrentLength] = Value.fromAny(arg);
                    resultArray.m_nonEmptyCount++;
                    resultArray.m_totalCount++;
                    resCurrentLength++;
                }
            }

            if (resultArray._isDenseArray())
                resultArray._setDenseArrayTotalCount(resultArray.m_totalCount);

            return resultArray;

            void appendArrayToResult(ASArray argArray) {
                var argValues = new ReadOnlySpan<Value>(argArray.m_values, 0, argArray.m_totalCount);
                HashLink[] argHashLinks = argArray.m_hashLinks;
                Span<Value> resultValues = resultArray.m_values;

                if (resultArray._isDenseArray()) {
                    // If the result is a dense array, we do not have to handle the case where
                    // the last argument is truncated. This happens only when the result
                    // array length reaches the limit of 2^32 - 1, at which point it cannot use
                    // a dense array storage.

                    if (argArray._isDenseArray()) {
                        // Both argument and result are using dense array storage.
                        argValues.CopyTo(resultValues.Slice((int)resCurrentLength));
                    }
                    else {
                        // Argument is a hash table but the result is a dense array.
                        for (int i = 0; i < argValues.Length; i++) {
                            Value val = argValues[i];
                            if (!val.isEmpty) {
                                int index = (int)(resCurrentLength + argHashLinks[i].key);
                                resultValues[index] = val;
                            }
                        }
                    }

                    resultArray.m_nonEmptyCount += argArray.m_nonEmptyCount;
                    resCurrentLength += argArray.length;
                    resultArray.m_totalCount = (int)resCurrentLength;
                }
                else {
                    // Result is a hash table.
                    // We must handle the last argument truncated case here.

                    bool argIsHash = !argArray._isDenseArray();
                    uint appendLength = Math.Min(argArray.length, resultLength - resCurrentLength);

                    if (!argIsHash) {
                        if (appendLength < (uint)argValues.Length)
                            argValues = argValues.Slice(0, (int)appendLength);

                        for (int i = 0; i < argValues.Length; i++) {
                            Value val = argValues[i];
                            if (!val.isEmpty)
                                resultArray._hashSetValue(resCurrentLength + (uint)i, val);
                        }
                    }
                    else {
                        for (int i = 0; i < argValues.Length; i++) {
                            Value val = argValues[i];
                            uint key = argHashLinks[i].key;

                            if (!val.isEmpty && key < appendLength)
                                resultArray._hashSetValue(resCurrentLength + key, val);
                        }
                    }

                    resCurrentLength += appendLength;
                }
            }

            void appendVectorToResult(ASVectorAny argVector) {
                int vecLength = argVector.length;

                if (resultArray._isDenseArray()) {
                    // We do not have to handle the last argument truncated case when the result
                    // is a dense array. (See comments on appendArrayToResult)

                    var valuesSpan = resultArray.m_values.AsSpan((int)resCurrentLength, vecLength);
                    for (int i = 0; i < valuesSpan.Length; i++)
                        valuesSpan[i] = Value.fromObject(argVector.AS_getElement(i));

                    resultArray.m_nonEmptyCount += vecLength;
                    resultArray.m_totalCount += vecLength;
                    resCurrentLength += (uint)vecLength;
                }
                else {
                    uint appendLength = Math.Min((uint)vecLength, resultLength - resCurrentLength);
                    for (uint i = 0; i < appendLength; i++)
                        resultArray._hashSetValue(resCurrentLength + i, Value.fromObject(argVector.AS_getElement(i)));

                    resCurrentLength += appendLength;
                }
            }
        }

        /// <summary>
        /// Calls a specified function for each element in the Array, until it returns false for any
        /// element, in which case this method returns false, or the function returns true for all
        /// elements in the Array, in which case this method returns true.
        /// </summary>
        ///
        /// <param name="callback">
        /// The callback function to execute for each element. It must take three arguments. The first
        /// argument is the element value, the second argument is the index of the element and the
        /// third is a reference to the Array instance that called this method. If this is null,
        /// this method returns true.
        /// </param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this argument
        /// must be null or undefined, otherwise an error is thrown.</param>
        ///
        /// <returns>True if <paramref name="callback"/> returns true for all elements in the Array,
        /// otherwise false.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1510: <paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null or undefined.</description>
        /// </item>
        /// <item><description>Any error: The callback function throws an exception.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the callback function modifies the Array instance, the returned result and the
        /// state of the instance when this method returns is unspecified.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual bool every(ASFunction callback, ASAny thisObject = default) {
            if (callback == null)
                return true;

            if (!thisObject.isUndefinedOrNull && callback.isMethodClosure)
                throw ErrorHelper.createError(ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL);

            ASAny[] cbArgsArray = new ASAny[3];
            cbArgsArray[2] = this;

            ReadOnlySpan<ASAny> cbArgs = cbArgsArray.AsSpan(0, Math.Min(cbArgsArray.Length, callback.length));

            if (_isDenseArrayWithoutEmptySlots()) {
                var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);
                for (int i = 0; i < valuesSpan.Length; i++) {
                    cbArgsArray[0] = valuesSpan[i].toAny();
                    cbArgsArray[1] = (uint)i;

                    ASAny cbResult = callback.AS_invoke(thisObject, cbArgs);
                    if (!(cbResult.value is ASBoolean && (bool)cbResult))
                        return false;
                }
            }
            else {
                for (uint i = 0, n = m_length; i < n; i++) {
                    cbArgsArray[0] = AS_getElement(i);
                    cbArgsArray[1] = i;

                    ASAny cbResult = callback.AS_invoke(thisObject, cbArgs);
                    if (!(cbResult.value is ASBoolean && (bool)cbResult))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Calls the specified callback function for each element in the Array instance, and returns
        /// an Array containing all elements for which the callback function returns true.
        /// </summary>
        ///
        /// <param name="callback">
        /// The callback function to execute for each element. It must take three arguments. The first
        /// argument is the element value, the second argument is the index of the element and the
        /// third is a reference to the Array instance that called this method. If this is null,
        /// this method returns an empty Array.
        /// </param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this argument
        /// must be null or undefined, otherwise an error is thrown.</param>
        ///
        /// <returns>An Array containing all elements for which the callback function returns
        /// true.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1510: <paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null or undefined.</description>
        /// </item>
        /// <item><description>Any error: The callback function throws an exception.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the callback function modifies the Array instance, the returned result and the
        /// state of the instance when this method returns is unspecified.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual ASArray filter(ASFunction callback, ASAny thisObject = default) {
            if (callback == null)
                return new ASArray();

            if (!thisObject.isUndefinedOrNull && callback.isMethodClosure)
                throw ErrorHelper.createError(ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL);

            ASArray resultArray = new ASArray();
            uint resultCount = 0;

            ASAny[] cbArgsArray = new ASAny[3];
            cbArgsArray[2] = this;

            ReadOnlySpan<ASAny> cbArgs = cbArgsArray.AsSpan(0, Math.Min(cbArgsArray.Length, callback.length));

            void check(uint ind, ASAny val, in ReadOnlySpan<ASAny> _cbArgs) {
                cbArgsArray[0] = val;
                cbArgsArray[1] = ind;

                ASAny cbReturn = callback.AS_invoke(thisObject, _cbArgs);
                if (cbReturn.value is ASBoolean && (bool)cbReturn) {
                    resultArray.AS_setElement(resultCount, val);
                    resultCount++;
                }
            }

            if (_isDenseArrayWithoutEmptySlots()) {
                var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);
                for (int i = 0; i < valuesSpan.Length; i++)
                    check((uint)i, valuesSpan[i].toAny(), cbArgs);
            }
            else {
                for (uint i = 0, n = m_length; i < n; i++)
                    check(i, AS_getElement(i), cbArgs);
            }

            return resultArray;
        }

        /// <summary>
        /// Executes the specified callback function for each element in the Array.
        /// </summary>
        ///
        /// <param name="callback">
        /// The callback function to execute for each element. It must take three arguments. The first
        /// argument is the element value, the second argument is the index of the element and the
        /// third is a reference to the Array instance that called this method. The return value of
        /// the callback function, if any, is discarded.
        /// </param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this argument
        /// must be null or undefined, otherwise an error is thrown.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1510: <paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null or undefined.</description>
        /// </item>
        /// <item><description>Any error: The callback function throws an exception.</description></item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual void forEach(ASFunction callback, ASAny thisObject = default(ASAny)) {
            if (callback == null)
                return;

            if (!thisObject.isUndefinedOrNull && callback.isMethodClosure)
                throw ErrorHelper.createError(ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL);

            ASAny[] cbArgsArray = new ASAny[3];
            cbArgsArray[2] = this;

            ReadOnlySpan<ASAny> cbArgs = cbArgsArray.AsSpan(0, Math.Min(cbArgsArray.Length, callback.length));

            if (_isDenseArrayWithoutEmptySlots()) {
                var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);
                for (int i = 0; i < valuesSpan.Length; i++) {
                    cbArgsArray[0] = valuesSpan[i].toAny();
                    cbArgsArray[1] = (uint)i;
                    callback.AS_invoke(thisObject, cbArgs);
                }
            }
            else {
                for (uint i = 0, n = m_length; i < n; i++) {
                    cbArgsArray[0] = AS_getElement(i);
                    cbArgsArray[1] = i;
                    callback.AS_invoke(thisObject, cbArgs);
                }
            }
        }

        /// <summary>
        /// Searches for the element whose value is <paramref name="searchElement"/> in the Array
        /// using the rules of the strict equality operator (===), starting at the index
        /// <paramref name="fromIndex"/>, and returns the index of the first element with that
        /// value.
        /// </summary>
        ///
        /// <param name="searchElement">The value of the element to search in the Array instance.
        /// Comparison is done using the strict equality (===) operator.</param>
        /// <param name="fromIndex">The index from where to start searching. If this is greater than
        /// or equal to the length of the Array, this method returns -1. If this is negative, the
        /// length of the Array is added to it; if it is still negative after adding the length, it is
        /// set to 0. The fractional part, if any, will be ignored.</param>
        ///
        /// <returns>
        /// The index of the first element, at or after <paramref name="fromIndex"/>, whose value is
        /// equal to <paramref name="searchElement"/>. If no element with that value is found, if
        /// <paramref name="fromIndex"/> is greater than or equal to the length of the Array, or if
        /// <paramref name="searchElement"/> is undefined, returns -1.
        /// </returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual double indexOf(
            ASAny searchElement, [ParamDefaultValue(0)] ASAny fromIndex)
        {
            uint uFromIndex = _normalizeIndex((double)fromIndex, m_length);

            if (_isDenseArrayWithoutEmptySlots()) {
                if (uFromIndex >= m_length)
                    return -1.0;

                var valuesSpan = new ReadOnlySpan<Value>(m_values, (int)uFromIndex, (int)(m_length - uFromIndex));

                if (searchElement.isUndefinedOrNull
                    || !ClassTagSet.specialStrictEquality.contains(searchElement.AS_class.tag))
                {
                    Value searchVal = Value.fromAny(searchElement);
                    for (int i = 0; i < valuesSpan.Length; i++) {
                        if (valuesSpan[i].isReferenceEqual(searchVal))
                            return (double)((uint)i + uFromIndex);
                    }
                }
                else if (ASObject.AS_isNumeric(searchElement.value)) {
                    double searchVal = (double)searchElement;
                    for (int i = 0; i < valuesSpan.Length; i++) {
                        ASObject obj = valuesSpan[i].toAny().value;
                        if (ASObject.AS_isNumeric(obj) && (double)obj == searchVal)
                            return (double)((uint)i + uFromIndex);
                    }
                }
                else if (searchElement.value is ASString) {
                    string searchVal = (string)searchElement;
                    for (int i = 0; i < valuesSpan.Length; i++) {
                        ASObject strObj = valuesSpan[i].toAny().value as ASString;
                        if (strObj != null && (string)strObj == searchVal)
                            return (double)((uint)i + uFromIndex);
                    }
                }
                else {
                    for (int i = 0; i < valuesSpan.Length; i++) {
                        if (ASAny.AS_strictEq(searchElement, valuesSpan[i].toAny()))
                            return (double)((uint)i + uFromIndex);
                    }
                }
            }
            else {
                for (uint i = uFromIndex, n = m_length; i < n; i++) {
                    if (ASAny.AS_strictEq(searchElement, AS_getElement(i)))
                        return (double)i;
                }
            }

            return -1.0;
        }

        /// <summary>
        /// Returns a string containing the string representations of all elements in the Array
        /// concatenated with the specified separator string between values.
        /// </summary>
        /// <param name="sep">The separator string. If this is undefined (the default), the separator
        /// "," is used. If this is a non-string value other than undefined, it is converted to a
        /// string.</param>
        /// <returns>A string containing the string representations of all elements in the Array
        /// concatenated with <paramref name="sep"/> between values.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual string join(ASAny sep = default) {
            string sepStr = sep.isDefined ? ASAny.AS_convertString(sep) : ",";
            int length = (int)Math.Min(m_length, (uint)Int32.MaxValue);

            string[] strings = new string[length];

            if (_isDenseArrayWithoutEmptySlots()) {
                var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);
                for (int i = 0; i < valuesSpan.Length; i++)
                    strings[i] = ASAny.AS_coerceString(valuesSpan[i].toAny());
            }
            else {
                for (uint i = 0, n = m_length; i < n; i++)
                    strings[i] = ASAny.AS_coerceString(AS_getElement(i));
            }

            return String.Join(sepStr, strings);
        }

        /// <summary>
        /// Searches for the element whose value is <paramref name="searchElement"/> in the Array,
        /// starting at the index <paramref name="fromIndex"/> and moving backwards, using the rules
        /// of the strict equality operator (===), and returns the index of the first element with
        /// that value.
        /// </summary>
        ///
        /// <param name="searchElement">The value of the element to search in the Array instance.
        /// Comparison is done using the strict equality operator (===).</param>
        /// <param name="fromIndex">
        /// The index from where to start searching. If this is greater than or equal to the length of
        /// the Array, it is set to <c>length - 1</c>. If this is negative, the length of the
        /// Array is added to it; if it is still negative after adding the length, it is set to 0. The
        /// fractional part, if any, will be ignored.
        /// </param>
        ///
        /// <returns>The index of the first element, at or before <paramref name="fromIndex"/>,
        /// whose value is equal to <paramref name="searchElement"/>. If no element with that value
        /// is found, or if <paramref name="searchElement"/> is undefined, returns -1.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual double lastIndexOf(
            ASAny searchElement, [ParamDefaultValue(UInt32.MaxValue)] ASAny fromIndex)
        {
            uint uFromIndex = _normalizeIndex((double)fromIndex, m_length);

            if (_isDenseArrayWithoutEmptySlots()) {
                var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, (int)Math.Min(uFromIndex + 1, m_length));

                if (searchElement.isUndefinedOrNull
                    || !ClassTagSet.specialStrictEquality.contains(searchElement.AS_class.tag))
                {
                    Value searchVal = Value.fromAny(searchElement);
                    for (int i = valuesSpan.Length - 1; i >= 0; i--) {
                        if (valuesSpan[i].isReferenceEqual(searchVal))
                            return (double)i;
                    }
                }
                else if (ASObject.AS_isNumeric(searchElement.value)) {
                    double searchVal = (double)searchElement;
                    for (int i = valuesSpan.Length - 1; i >= 0; i--) {
                        ASObject obj = valuesSpan[i].toAny().value;
                        if (ASObject.AS_isNumeric(obj) && (double)obj == searchVal)
                            return (double)i;
                    }
                }
                else if (searchElement.value is ASString) {
                    string searchVal = (string)searchElement;
                    for (int i = valuesSpan.Length - 1; i >= 0; i--) {
                        ASObject strObj = valuesSpan[i].toAny().value as ASString;
                        if (strObj != null && (string)strObj == searchVal)
                            return (double)i;
                    }
                }
                else {
                    for (int i = valuesSpan.Length - 1; i >= 0; i--) {
                        if (ASAny.AS_strictEq(searchElement, valuesSpan[i].toAny()))
                            return (double)i;
                    }
                }
            }
            else {
                uint curIndex = (uFromIndex == m_length) ? m_length - 1 : uFromIndex;
                while (true) {
                    if (ASAny.AS_strictEq(searchElement, AS_getElement(curIndex)))
                        return (double)curIndex;
                    if (curIndex == 0)
                        break;
                    curIndex--;
                }
            }

            return -1.0;
        }

        /// <summary>
        /// Executes the specified callback function for each element in the Array and returns a new
        /// Array with each index holding the return value of the callback function for the element of
        /// the current Array instance at that index.
        /// </summary>
        ///
        /// <param name="callback">The callback function to execute for each element. It must take
        /// three arguments. The first argument is the element value, the second argument is the index
        /// of the element and the third is a reference to the Array instance that called this method.
        /// If this is null, this method returns an empty Array.</param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this argument
        /// must be null or undefined, otherwise an error is thrown.</param>
        ///
        /// <returns>An Array instance containing the return values of the callback function for each
        /// element in the current instance.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1510: <paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null or undefined.</description>
        /// </item>
        /// <item><description>Any error: The callback function throws an exception.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the callback function modifies the Array instance, the behaviour of this method is
        /// undefined.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual ASArray map(ASFunction callback, ASAny thisObject = default(ASAny)) {
            if (callback == null)
                return new ASArray();

            if (!thisObject.isUndefinedOrNull && callback.isMethodClosure)
                throw ErrorHelper.createError(ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL);

            int length = (int)Math.Min(m_length, (uint)Int32.MaxValue);

            ASArray resultArray = new ASArray();
            resultArray.m_values = new Value[length];
            resultArray.m_length = (uint)length;

            Span<Value> resultValues = resultArray.m_values;

            ASAny[] cbArgsArray = new ASAny[3];
            cbArgsArray[2] = this;

            ReadOnlySpan<ASAny> cbArgs = cbArgsArray.AsSpan(0, Math.Min(cbArgsArray.Length, callback.length));

            if (_isDenseArrayWithoutEmptySlots()) {
                var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, length);
                for (int i = 0; i < valuesSpan.Length; i++) {
                    cbArgsArray[0] = valuesSpan[i].toAny();
                    cbArgsArray[1] = (uint)i;
                    resultValues[i] = Value.fromAny(callback.AS_invoke(thisObject, cbArgs));
                }
            }
            else {
                for (uint i = 0; i < (uint)length; i++) {
                    cbArgsArray[0] = AS_getElement(i);
                    cbArgsArray[1] = i;
                    resultValues[(int)i] = Value.fromAny(callback.AS_invoke(thisObject, cbArgs));
                }
            }

            resultArray.m_nonEmptyCount = length;
            resultArray.m_totalCount = length;

            return resultArray;
        }

        /// <summary>
        /// Removes the last element from the Array and returns the value of that element. The length
        /// of the array is decreased by one.
        /// </summary>
        /// <returns>The value of the last element in the Array. For empty arrays, this method returns
        /// undefined.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual ASAny pop() {
            if (m_length == 0)
                return default(ASAny);

            if (_isDenseArray()) {
                if (m_length > (uint)m_totalCount) {
                    m_length--;
                    return default(ASAny);
                }

                int newTotalCount = m_totalCount - 1;
                Value popped = m_values[newTotalCount];

                if (!popped.isEmpty) {
                    m_values[newTotalCount] = default;
                    m_nonEmptyCount--;
                }

                _setDenseArrayTotalCount(newTotalCount);
                m_length--;
                _updateArrayStorage(isDelete: true);

                return popped.toAny();
            }
            else {
                Value popped = _hashDeleteAndGet(m_length - 1);
                m_length--;
                _updateArrayStorage(isDelete: true);
                return popped.toAny();
            }

        }

        /// <summary>
        /// Appends the given value to the Array and returns the new length of the Array.
        /// </summary>
        /// <param name="arg">The value to append to the array.</param>
        /// <returns>The new length of the array.</returns>
        public virtual uint push(ASAny arg) {
            // This method is used as a compiler intrinsic for a single-argument push() call
            if (m_length < UInt32.MaxValue)
                AS_setElement(m_length, arg);
            return m_length;
        }

        /// <summary>
        /// Adds the values in the <paramref name="args"/> array to the end of the Array, and
        /// returns the new length of the Array.
        /// </summary>
        /// <param name="args">The values to add to the end of the array.</param>
        /// <returns>The new length of the Array.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual uint push(RestParam args = default) {
            int argCount = (int)Math.Min((uint)args.length, UInt32.MaxValue - m_length);
            if (argCount == 0)
                return m_length;

            uint newArrLength = m_length + (uint)argCount;

            if (_isDenseArray() && m_length > (uint)m_totalCount
                && !_canUseDenseArray(m_nonEmptyCount + argCount, newArrLength))
            {
                _denseArrayToHash();
            }

            if (_isDenseArray()) {
                int curLen = (int)m_length;
                if (curLen + argCount > m_values.Length)
                    Array.Resize(ref m_values, Math.Max(Math.Max(curLen + argCount, m_values.Length * 2), 4));

                var span = m_values.AsSpan(curLen, argCount);
                for (int i = 0; i < argCount; i++)
                    span[i] = Value.fromAny(args[i]);

                m_nonEmptyCount += argCount;
                m_totalCount = curLen + argCount;
                m_length = (uint)(curLen + argCount);
            }
            else {
                for (int i = 0; i < argCount; i++)
                    _hashSetValue(m_length + (uint)i, Value.fromAny(args[i]));

                m_length += (uint)argCount;
                _updateArrayStorage();
            }

            return m_length;
        }

        /// <summary>
        /// Reverses all elements in the current Array instance in place.
        /// </summary>
        /// <returns>The instance on which the method is called.</returns>
        ///
        /// <remarks>
        /// This method modifies the current instance; it does not create a copy of it. The value of
        /// any element at index <c>i</c> before reversing will me moved to the index <c>length
        /// - i - 1</c> after reversing.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual ASArray reverse() {
            if (_isDenseArray() && m_length > (uint)m_totalCount && !_canUseDenseArray(m_nonEmptyCount, m_length))
                _denseArrayToHash();

            if (_isDenseArray()) {
                int curLen = (int)m_length;

                if (curLen <= m_values.Length) {
                    m_values.AsSpan(0, curLen).Reverse();
                }
                else {
                    // If the array needs to be enlarged, copy and reverse in a single loop.
                    var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);
                    Value[] newValues = new Value[curLen];
                    for (int i = 0; i < valuesSpan.Length; i++)
                        newValues[curLen - i - 1] = valuesSpan[i];

                    m_values = newValues;
                }

                _setDenseArrayTotalCount(curLen);
            }
            else {
                // For a hash table, reverse by modifying the keys and rebuilding the chains.
                var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);
                HashLink[] hashLinks = m_hashLinks;

                for (int i = 0; i < valuesSpan.Length; i++) {
                    if (!valuesSpan[i].isEmpty) {
                        ref uint key = ref hashLinks[i].key;
                        key = m_length - key - 1;
                    }
                }
                _resetHashTableChains();
            }

            return this;
        }

        /// <summary>
        /// Removes the first element from the Array and returns the value of that element. All other
        /// elements are shifted backwards by one index. The length of the array is decremented by
        /// one.
        /// </summary>
        ///
        /// <returns>The value of the first element in the Array. For empty arrays, this method
        /// returns undefined.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual ASAny shift() {
            if (m_length == 0)
                return default(ASAny);

            Value removed;

            if (_isDenseArray()) {
                if (m_totalCount == 0) {
                    m_length--;
                    return default(ASAny);
                }

                removed = m_values[0];
                m_values.AsSpan(1).CopyTo(m_values);
                m_values[m_totalCount - 1] = default;

                m_length--;
                if (!removed.isEmpty)
                    m_nonEmptyCount--;

                m_totalCount = (m_nonEmptyCount == 0) ? 0 : m_totalCount - 1;
                _updateArrayStorage(isDelete: true);
            }
            else {
                removed = _hashDeleteAndGet(0);

                var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);
                HashLink[] hashLinks = m_hashLinks;

                for (int i = 0; i < valuesSpan.Length; i++) {
                    if (!valuesSpan[i].isEmpty)
                        hashLinks[i].key--;
                }

                m_length--;

                // Since conversion of a hash table to dense array storage does not depend
                // on chain correctness, check whether the conversion can be done first and
                // rebuild the chains only if no conversion to a dense array was done.
                _updateArrayStorage(isDelete: true);

                if (!_isDenseArray())
                    _resetHashTableChains();
            }

            return removed.toAny();
        }

        /// <summary>
        /// Returns an Array containing all elements starting from <paramref name="startIndex"/> up
        /// to (but not including) the element at <paramref name="endIndex"/>.
        /// </summary>
        ///
        /// <param name="startIndex">
        /// The index from which elements should be included in the returned Array. If this is
        /// negative, the length of the Array is added to it; if it is still negative after adding the
        /// length, it is set to zero. If this is greater than the length of the Array, it is set to
        /// its length. If this is greater than or equal to <paramref name="endIndex"/>, an empty
        /// Array is returned.
        /// </param>
        /// <param name="endIndex">
        /// The index at which to stop adding elements to the returned Array. If this is negative, the
        /// length of the Array is added to it; if it is still negative after adding the length, it is
        /// set to zero. If this is greater than the length of the Array, it is set to its length. If
        /// this is less than or equal to <paramref name="endIndex"/>, an empty Array is returned.
        /// Elements up to, but not including, this index, will be included in the returned Array.
        /// </param>
        ///
        /// <returns>An array containing all elements starting from <paramref name="startIndex"/> up
        /// to (but not including) the element at <paramref name="endIndex"/>.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual ASArray slice(
            [ParamDefaultValue(0)] ASAny startIndex, [ParamDefaultValue(UInt32.MaxValue)] ASAny endIndex)
        {
            uint uStartIndex = _normalizeIndex((double)startIndex, m_length);
            uint uEndIndex = _normalizeIndex((double)endIndex, m_length);

            if (uEndIndex <= uStartIndex)
                return new ASArray();

            // slice() always returns a dense array, so the slice length is limited
            // to the max dense array size.
            if (uEndIndex - uStartIndex > (uint)Int32.MaxValue)
                uEndIndex = uStartIndex + (uint)Int32.MaxValue;

            int sliceLength = (int)(uEndIndex - uStartIndex);
            return _internalSlice(uStartIndex, sliceLength);
        }

        private ASArray _internalSlice(uint startIndex, int sliceLength) {
            ASArray sliceArray = new ASArray();
            sliceArray.m_values = new Value[sliceLength];

            Span<Value> sliceValues = sliceArray.m_values.AsSpan(0, sliceLength);

            if (_isDenseArrayWithoutEmptySlots()) {
                var srcValues = new ReadOnlySpan<Value>(m_values, (int)startIndex, sliceLength);
                srcValues.CopyTo(sliceValues);
            }
            else {
                for (int i = 0; i < sliceValues.Length; i++)
                    sliceValues[i] = Value.fromAny(AS_getElement(startIndex + (uint)i));
            }

            sliceArray.m_nonEmptyCount = sliceLength;
            sliceArray.m_totalCount = sliceLength;
            sliceArray.m_length = (uint)sliceLength;

            return sliceArray;
        }

        /// <summary>
        /// Calls a specified function for each element in the Array, until it returns true for any
        /// element, in which case this method returns true, or the function returns false for all
        /// elements in the Array, in which case this method returns false.
        /// </summary>
        ///
        /// <param name="callback">
        /// The callback function to execute for each element. It must take three arguments. The first
        /// argument is the element value, the second argument is the index of the element and the
        /// third is a reference to the Array instance that called this method. If this is null,
        /// this method returns false.
        /// </param>
        /// <param name="thisObject">The object to be used as the "this" object in calls to the
        /// callback function. If <paramref name="callback"/> is a method closure, this argument
        /// must be null or undefined, otherwise an error is thrown.</param>
        ///
        /// <returns>True if <paramref name="callback"/> returns true for any element in the Array,
        /// otherwise false.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1510: <paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null or undefined.</description>
        /// </item>
        /// <item><description>Any error: The callback function throws an exception.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If the callback function modifies the Array instance, the behaviour of this method is
        /// undefined.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual bool some(ASFunction callback, ASAny thisObject = default(ASAny)) {
            if (callback == null)
                return false;

            if (!thisObject.isUndefinedOrNull && callback.isMethodClosure)
                throw ErrorHelper.createError(ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL);

            ASAny[] cbArgsArray = new ASAny[3];
            cbArgsArray[2] = this;

            ReadOnlySpan<ASAny> cbArgs = cbArgsArray.AsSpan(0, Math.Min(cbArgsArray.Length, callback.length));

            if (_isDenseArrayWithoutEmptySlots()) {
                var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);
                for (int i = 0; i < valuesSpan.Length; i++) {
                    cbArgsArray[0] = m_values[i].toAny();
                    cbArgsArray[1] = (uint)i;

                    ASAny cbResult = callback.AS_invoke(thisObject, cbArgs);
                    if (cbResult.value is ASBoolean && (bool)cbResult)
                        return true;
                }
            }
            else {
                for (uint i = 0, n = m_length; i < n; i++) {
                    cbArgsArray[0] = AS_getElement(i);
                    cbArgsArray[1] = i;

                    ASAny cbResult = callback.AS_invoke(thisObject, cbArgs);
                    if (cbResult.value is ASBoolean && (bool)cbResult)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Sorts the array.
        /// </summary>
        /// <param name="args">The sort arguments. (See remarks below)</param>
        ///
        /// <returns>
        /// If the <see cref="UNIQUESORT"/> flag is set and two equal elements are found, returns 0;
        /// if the <see cref="RETURNINDEXEDARRAY"/> flag is set, returns a new array containing
        /// indices representing the sort order of the array (the array is not mutated in this case);
        /// otherwise, returns the array on which this method is called (and the array is sorted in
        /// place).
        /// </returns>
        ///
        /// <remarks>
        /// <para>
        /// If this first argument is a Function object, it is used as a comparer function. The
        /// comparer function must take two arguments, and return an integer that is negative, zero or
        /// positive if the first argument is less than, equal to or greater than the second argument
        /// respectively. In this case, the second argument is used for the sorting flags (of which
        /// <see cref="NUMERIC"/> and <see cref="CASEINSENSITIVE"/> are irrelevant and ignored).
        /// </para>
        /// <para>If the first argument is not a function, it is converted to an integer and treated
        /// as sorting flags (which are defined as constants in the Array class). The second argument
        /// is ignored in this case.</para>
        /// <para>Any arguments passed after the second are ignored.</para>
        /// <para>If a comparer function is passed to this method, and it throws an exception, the
        /// array will be left in an unspecified state.</para>
        /// <para>If the value of the <see cref="length"/> property of the array is greater than
        /// 2^32 - 1, the array will not be sorted and this method will always return the instance
        /// on which it was called (even if <see cref="RETURNINDEXEDARRAY"/> is set).</para>
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual ASAny sort(RestParam args = default) {
            ASFunction compareFunc = (args.length >= 1) ? args[0].value as ASFunction : null;
            int flags;

            if (compareFunc != null) {
                if (args.length < 2)
                    flags = 0;
                else if (!ASObject.AS_isNumeric(args[1].value))
                    throw ErrorHelper.createCastError(args[1], "Number");
                else
                    flags = (int)args[1];
            }
            else {
                if (args.length < 1)
                    flags = 0;
                else if (!ASObject.AS_isNumeric(args[0].value))
                    throw ErrorHelper.createCastError(args[0], "Function");
                else
                    flags = (int)args[0];
            }

            if (m_length > (uint)Int32.MaxValue) {
                // Arrays greater than this length can't be sorted.
                return this;
            }

            bool uniqueSort = (flags & UNIQUESORT) != 0;
            bool returnIndexedArray = (flags & RETURNINDEXEDARRAY) != 0;

            _getArrayValuesForSorting(flags, out Value[] valuesForSort, out bool isSortInPlace);

            int sortArrayLength = isSortInPlace ? m_totalCount : valuesForSort.Length;
            int sortArrAndUndefinedLength;
            int[] indexArray = null;

            if (returnIndexedArray) {
                indexArray = new int[checked((int)m_length)];
                for (int i = 0; i < indexArray.Length; i++)
                    indexArray[i] = i;

                _doPreSortPartitionIndexed(
                    valuesForSort.AsSpan(0, sortArrayLength),
                    indexArray.AsSpan(0, sortArrayLength),
                    separateNulls: false,
                    out sortArrAndUndefinedLength,
                    out int definedCount
                );
                sortArrayLength = definedCount;
            }
            else {
                _doPreSortPartition(
                    valuesForSort.AsSpan(0, sortArrayLength), separateNulls: false, out sortArrAndUndefinedLength, out int definedCount);

                sortArrayLength = definedCount;
            }

            // If using UNIQUESORT, we need to generate sort keys for the entire array for the uniqueness check.
            int sortKeyCount = uniqueSort ? (int)m_length : sortArrayLength;

            if (compareFunc != null) {
                // The Array.Sort methods in corelib throws ArgumentException for some ill-behaved
                // comparison functions, so use DataStructureUtil.sortSpan (which never throws)
                // when we are given a user-provided comparator.

                var comparer = new ArrayValueComparerWithUserFunc(compareFunc, valuesForSort);

                if (returnIndexedArray)
                    DataStructureUtil.sortSpan(indexArray.AsSpan(0, sortArrayLength), comparer.compareIndices);
                else
                    DataStructureUtil.sortSpan(valuesForSort.AsSpan(0, sortArrayLength), comparer.compareValues);

                if (uniqueSort && sortKeyCount > 0) {
                    Value prevValue = valuesForSort[returnIndexedArray ? indexArray[0] : 0];

                    for (int i = 1; i < sortKeyCount; i++) {
                        Value curValue = valuesForSort[returnIndexedArray ? indexArray[i] : i];
                        if (comparer.compareValues(prevValue, curValue) == 0)
                            return 0;

                        prevValue = curValue;
                    }
                }
            }
            else if ((flags & NUMERIC) != 0) {
                double[] keys = new double[sortKeyCount];
                var comparer = GenericComparer<double>.defaultComparer;

                for (int i = 0; i < keys.Length; i++) {
                    Value val = valuesForSort[returnIndexedArray ? indexArray[i] : i];
                    keys[i] = (double)val.toAny();
                }

                if (returnIndexedArray)
                    Array.Sort(keys, indexArray, 0, sortArrayLength, comparer);
                else
                    Array.Sort(keys, valuesForSort, 0, sortArrayLength, comparer);

                if (uniqueSort && !_doUniqueSortCheck(keys, comparer))
                    return 0;
            }
            else {
                var comparerType = ((flags & CASEINSENSITIVE) != 0) ? GenericComparerType.STRING_IGNORECASE : GenericComparerType.DEFAULT;
                var comparer = GenericComparer<string>.getComparer(comparerType);

                string[] keys = new string[sortKeyCount];
                for (int i = 0; i < keys.Length; i++) {
                    Value val = valuesForSort[returnIndexedArray ? indexArray[i] : i];
                    keys[i] = ASAny.AS_convertString(val.toAny());
                }

                if (returnIndexedArray)
                    Array.Sort(keys, indexArray, 0, sortArrayLength, comparer);
                else
                    Array.Sort(keys, valuesForSort, 0, sortArrayLength, comparer);

                if (uniqueSort && !_doUniqueSortCheck(keys, comparer))
                    return 0;
            }

            if (returnIndexedArray) {
                if ((flags & DESCENDING) != 0)
                    indexArray.AsSpan(0, sortArrayLength).Reverse();

                return fromTypedArray(indexArray);
            }

            // If the sorting was done on a separate array (because of holes, UNIQUESORT flag, etc.)
            // we need to write the sorted array back into this instance. We use one of three options here:
            //
            // - If the number of holes in the sorted array is not very large, swap the current
            //   backing array with the sorted one.
            // - Otherwise, if the current backing array can hold all the sorted values plus all the
            //   `undefined` values after the sorted values, copy from the sorted array to the current
            //   backing array.
            // - Otherwise, allocate a new backing array.

            if (!isSortInPlace) {
                if (sortArrAndUndefinedLength >= (valuesForSort.Length >> 1)) {
                    m_values = valuesForSort;
                }
                else if (sortArrAndUndefinedLength <= m_values.Length) {
                    valuesForSort.AsSpan(0, sortArrAndUndefinedLength).CopyTo(m_values);
                    if (sortArrAndUndefinedLength < m_totalCount)
                        m_values.AsSpan(sortArrAndUndefinedLength, m_totalCount - sortArrAndUndefinedLength).Clear();
                }
                else {
                    m_values = valuesForSort.AsSpan(0, sortArrAndUndefinedLength).ToArray();
                }

                m_totalCount = sortArrAndUndefinedLength;
                m_nonEmptyCount = sortArrAndUndefinedLength;
                m_hashLinks = null;
                m_hashEmptyChainHead = -1;
            }

            if ((flags & DESCENDING) != 0)
                m_values.AsSpan(0, sortArrayLength).Reverse();

            return this;
        }

        /// <summary>
        /// Sorts the array based on the values of properties of the array's elements.
        /// </summary>
        ///
        /// <param name="names">
        /// If this is a string, the array will be sorted using the value of the property of
        /// each element with the name as a sorting key, i.e. the comparison of any two elements will
        /// compare the values of the properties on both elements with the given name. If this is an
        /// array, it must contain only strings. In this case, comparison of two elements will compare
        /// the values of the properties on both elements whose name is at index
        /// 0 in the array; if they are equal, the values of properties with names at subsequent
        /// indices are compared, until two non-equal values are found (in which case the result of
        /// the comparison of the elements is the result of comparing those two values), or all names
        /// in the array have been used (in which case the two elements are considered as equal).
        /// </param>
        /// <param name="options">
        /// If this is anything other than an array, it is converted to an integer and used as the
        /// sorting flags (which must be a bitwise-OR combination of the sorting constants defined in
        /// the Array class). If this is an array, the <paramref name="names"/> parameter must also
        /// be an array, the length of this array must be equal to that of the
        /// <paramref name="names"/> array, and the array must contain integers. In this case, each
        /// element of the array is taken as the sorting flag combination for the corresponding
        /// property name in the <paramref name="names"/>. array, and the <see cref="UNIQUESORT"/>
        /// and <see cref="RETURNINDEXEDARRAY"/> flags are taken from the first element.
        /// </param>
        /// <param name="reserved">This argument is ignored and only present for signature
        /// compatibility when a subclass defined in a script overrides this method.</param>
        ///
        /// <returns>
        /// If the <see cref="UNIQUESORT"/> flag is set and two equal elements are found, returns 0;
        /// if the <see cref="RETURNINDEXEDARRAY"/> flag is set, returns a new array containing
        /// indices representing the sort order of the array (the array is not mutated in this case);
        /// otherwise, returns the array on which this method is called (in which case the array
        /// is sorted in place).
        /// </returns>
        ///
        /// <remarks>
        /// <para>If the value of the <see cref="length"/> property of the array is greater than
        /// 2^32 - 1, the array will not be sorted and this method will always return the instance
        /// on which it was called (even if <see cref="RETURNINDEXEDARRAY"/> is set).</para>
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual ASAny sortOn(ASAny names, ASAny options = default, RestParam reserved = default) {
            if (m_length > (uint)Int32.MaxValue) {
                // Arrays greater than this length cannot be sorted.
                return this;
            }

            QName[] propNames;
            int[] propFlags;

            if (names.value is ASArray namesArray) {
                propNames = new QName[(int)Math.Min(namesArray.length, (uint)Int32.MaxValue)];
                for (int i = 0; i < propNames.Length; i++)
                    propNames[i] = QName.publicName(ASAny.AS_convertString(namesArray[i]));
            }
            else {
                propNames = new QName[] {ASAny.AS_convertString(names)};
            }

            propFlags = new int[propNames.Length];
            bool uniqueSort = false, returnIndexedArray = false;

            if (options.value is ASArray optionsArray) {
                // The options array must only be used if its length is the same as that of
                // the names array. Otherwise all options are zero.
                if ((int)optionsArray.length == propNames.Length) {
                    for (int i = 0; i < propFlags.Length; i++)
                        propFlags[i] = (int)optionsArray[i];

                    // UNIQUESORT and RETURNINDEXEDARRAY are always taken from the first element in the flags array.
                    uniqueSort = (propFlags[0] & UNIQUESORT) != 0;
                    returnIndexedArray = (propFlags[0] & RETURNINDEXEDARRAY) != 0;
                }
            }
            else {
                int optionsValue = (int)options;
                for (int i = 0; i < propFlags.Length; i++)
                    propFlags[i] = optionsValue;

                uniqueSort = (optionsValue & UNIQUESORT) != 0;
                returnIndexedArray = (optionsValue & RETURNINDEXEDARRAY) != 0;
            }

            int globalFlags = 0;
            if (uniqueSort)
                globalFlags |= UNIQUESORT;
            if (returnIndexedArray)
                globalFlags |= RETURNINDEXEDARRAY;

            _getArrayValuesForSorting(globalFlags, out Value[] valuesForSort, out bool isSortInPlace);

            // Unlike sort(), null values here are also moved to the end of the array with undefined values.
            // So sortArrAndUndefinedLength includes nulls as well!

            int sortArrayLength = isSortInPlace ? m_totalCount : valuesForSort.Length;
            int sortArrAndUndefinedLength;
            int[] indexArray = null;

            if (returnIndexedArray) {
                indexArray = new int[checked((int)m_length)];
                for (int i = 0; i < indexArray.Length; i++)
                    indexArray[i] = i;

                _doPreSortPartitionIndexed(
                    valuesForSort.AsSpan(0, sortArrayLength),
                    indexArray.AsSpan(0, sortArrayLength),
                    separateNulls: true,
                    out sortArrAndUndefinedLength,
                    out int definedCount
                );
                sortArrayLength = definedCount;
            }
            else {
                _doPreSortPartition(
                    valuesForSort.AsSpan(0, sortArrayLength), separateNulls: true, out sortArrAndUndefinedLength, out int definedCount);

                sortArrayLength = definedCount;
            }

            if (uniqueSort && m_length > (uint)sortArrayLength + 1) {
                // UNIQUESORT is set and there is more than one value that is null, undefined or empty, so fail.
                // This is intentional, even when there is one null + one undefined value.
                return 0;
            }

            bool mustReverse;

            if (propNames.Length == 0) {
                // If there are no property names to sort on, it is the same as considering all objects
                // to be equal to each other. This means that if UNIQUESORT is set, the uniqueness check
                // succeeds if and only if there is no more than one object.

                if (uniqueSort && sortArrayLength > 1)
                    return 0;

                mustReverse = false;
            }
            else if (propNames.Length == 1) {
                // We use this fast path when there is exactly one property name.

                QName propName = propNames[0];
                mustReverse = (propFlags[0] & DESCENDING) != 0;

                if ((propFlags[0] & NUMERIC) != 0) {
                    double[] keys = new double[sortArrayLength];

                    for (int i = 0; i < keys.Length; i++) {
                        Value val = valuesForSort[returnIndexedArray ? indexArray[i] : i];
                        keys[i] = (double)val.toAny().AS_getProperty(propName);
                    }

                    var comparer = GenericComparer<double>.defaultComparer;

                    if (returnIndexedArray)
                        Array.Sort(keys, indexArray, 0, sortArrayLength, comparer);
                    else
                        Array.Sort(keys, valuesForSort, 0, sortArrayLength, comparer);

                    if (uniqueSort && !_doUniqueSortCheck(keys, comparer))
                        return 0;
                }
                else {
                    string[] keys = new string[sortArrayLength];

                    for (int i = 0; i < keys.Length; i++) {
                        Value val = valuesForSort[returnIndexedArray ? indexArray[i] : i];
                        keys[i] = ASAny.AS_convertString(val.toAny().AS_getProperty(propName));
                    }

                    var comparerType = ((propFlags[0] & CASEINSENSITIVE) != 0) ? GenericComparerType.STRING_IGNORECASE : GenericComparerType.DEFAULT;
                    IComparer<string> comparer = GenericComparer<string>.getComparer(comparerType);

                    if (returnIndexedArray)
                        Array.Sort(keys, indexArray, 0, sortArrayLength, comparer);
                    else
                        Array.Sort(keys, valuesForSort, 0, sortArrayLength, comparer);

                    if (uniqueSort && !_doUniqueSortCheck(keys, comparer))
                        return 0;
                }
            }
            else {
                // Use the slower path in other cases.

                _internalCreateSortOnKeysAndComparer(
                    objects: returnIndexedArray ? valuesForSort : valuesForSort.AsSpan(0, sortArrayLength),
                    useIndices: returnIndexedArray,
                    indices: returnIndexedArray ? indexArray.AsSpan(0, sortArrayLength) : default,
                    propNames,
                    propFlags,
                    out int[] keys,
                    out IComparer<int> comparer,
                    out mustReverse
                );

                if (returnIndexedArray)
                    Array.Sort(keys, indexArray, 0, sortArrayLength, comparer);
                else
                    Array.Sort(keys, valuesForSort, 0, sortArrayLength, comparer);

                if (uniqueSort && !_doUniqueSortCheck(keys, comparer))
                    return 0;
            }

            if (returnIndexedArray) {
                if (mustReverse)
                    indexArray.AsSpan(0, sortArrayLength).Reverse();

                return fromTypedArray(indexArray);
            }

            // If the sorting was done on a separate array (because of holes, UNIQUESORT flag, etc.)
            // we need to write the sorted array back into this instance. We use one of three options here:
            //
            // - If the number of holes in the sorted array is not very large, swap the current
            //   backing array with the sorted one.
            // - Otherwise, if the current backing array can hold all the sorted values plus all the
            //   `undefined` values after the sorted values, copy from the sorted array to the current
            //   backing array.
            // - Otherwise, allocate a new backing array.

            if (!isSortInPlace) {
                if (sortArrAndUndefinedLength >= (valuesForSort.Length >> 1)) {
                    m_values = valuesForSort;
                }
                else if (sortArrAndUndefinedLength <= m_values.Length) {
                    valuesForSort.AsSpan(0, sortArrAndUndefinedLength).CopyTo(m_values);
                    if (sortArrAndUndefinedLength < m_totalCount)
                        m_values.AsSpan(sortArrAndUndefinedLength, m_totalCount - sortArrAndUndefinedLength).Clear();
                }
                else {
                    m_values = valuesForSort.AsSpan(0, sortArrAndUndefinedLength).ToArray();
                }

                m_totalCount = sortArrAndUndefinedLength;
                m_nonEmptyCount = sortArrAndUndefinedLength;
                m_hashLinks = null;
                m_hashEmptyChainHead = -1;
            }

            if (mustReverse)
                m_values.AsSpan(0, sortArrayLength).Reverse();

            return this;
        }

        private void _getArrayValuesForSorting(int sortFlags, out Value[] valuesForSorting, out bool isSortInPlace) {
            if (_isDenseArrayWithoutEmptySlots()
                && ((sortFlags & UNIQUESORT) == 0 || (sortFlags & RETURNINDEXEDARRAY) != 0))
            {
                // If this is a dense array with no empty slots, we can do an in-place sort if
                // the UNIQUESORT flag is not set, or the RETURNINDEXEDARRAY flag is set. In
                // the latter case the sort is not strictly in-place (we still have to allocate
                // an index array), but we can use the backing array as the values array
                // as there are no holes for which we have to search the prototype.

                valuesForSorting = m_values;
                isSortInPlace = true;
                return;
            }

            Value[] vals = new Value[checked((int)m_length)];
            valuesForSorting = vals;

            isSortInPlace = false;

            if (_isDenseArrayWithoutEmptySlots()) {
                m_values.AsSpan(0, vals.Length).CopyTo(vals);
                return;
            }

            for (int i = 0; i < vals.Length; i++) {
                vals[i] = _getValueAt((uint)i);

                if (!vals[i].isEmpty)
                    continue;

                // If a prototype property with the same name as this index exists, fill the hole with it.
                BindStatus status = base.AS_tryGetProperty(
                    QName.publicName(ASint.AS_convertString(i)),
                    out ASAny prototypeVal,
                    BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE
                );

                if (status == BindStatus.SUCCESS)
                    vals[i] = Value.fromAny(prototypeVal);
            }
        }

        private static void _doPreSortPartition(
            Span<Value> values, bool separateNulls, out int nonEmptyValueCount, out int definedValueCount)
        {
            // Partition the values array so that all non-empty non-undefined (and non-null, if
            // separateNulls is true) values - the ones that will be sorted - come first, followed
            // by all undefined values, followed by all undefined values, followed by all empty
            // values (holes).

            int i, j;

            // First partition into (defined, undefOrEmpty)
            for (i = 0, j = 0; i < values.Length; i++) {
                ref Value vali = ref values[i];
                if (vali.isEmptyOrUndefined || (separateNulls && vali.toAny().isNull))
                    continue;

                if (i != j) {
                    ref Value valj = ref values[j];
                    (vali, valj) = (valj, vali);
                }
                j++;
            }

            definedValueCount = j;

            Span<Value> undefOrEmpty = values.Slice(j);

            // Partition undefOrEmpty into (undefined, empty)
            for (i = 0, j = 0; i < undefOrEmpty.Length; i++) {
                ref Value vali = ref undefOrEmpty[i];
                if (vali.isEmpty)
                    continue;

                if (i != j) {
                    ref Value valj = ref undefOrEmpty[j];
                    (vali, valj) = (valj, vali);
                }
                j++;
            }

            nonEmptyValueCount = definedValueCount + j;
        }

        private static void _doPreSortPartitionIndexed(
            ReadOnlySpan<Value> values, Span<int> indices, bool separateNulls, out int nonEmptyValueCount, out int definedValueCount)
        {
            // Do the same (defined, undefined, empty) partition, but on the indices array instead of
            // the values array.

            int i, j;

            // First partition into (defined, undefOrEmpty)
            for (i = 0, j = 0; i < indices.Length; i++) {
                ref int indexi = ref indices[i];
                Value vali = values[indexi];

                if (vali.isEmptyOrUndefined || (separateNulls && vali.toAny().isNull))
                    continue;

                if (i != j) {
                    ref int indexj = ref indices[j];
                    (indexi, indexj) = (indexj, indexi);
                }
                j++;
            }

            definedValueCount = j;

            Span<int> undefOrEmptyIndices = indices.Slice(j);

            // Partition undefOrEmpty into (undefined, empty)
            for (i = 0, j = 0; i < undefOrEmptyIndices.Length; i++) {
                ref int indexi = ref undefOrEmptyIndices[i];
                if (values[indexi].isEmpty)
                    continue;

                if (i != j) {
                    ref int indexj = ref undefOrEmptyIndices[j];
                    (indexi, indexj) = (indexj, indexi);
                }
                j++;
            }

            nonEmptyValueCount = definedValueCount + j;
        }

        /// <summary>
        /// Creates the sorting keys and comparer for the sortOn method.
        /// </summary>
        /// <param name="objects">The objects to be sorted.</param>
        /// <param name="useIndices">If this is true, use the <paramref name="indices"/> span for
        /// the initial ordering of the keys. Otherwise use the order of the objects in
        /// <paramref name="objects"/></param>
        /// <param name="indices">The indices in the <paramref name="objects"/> span representing the
        /// order in which to create the keys, if <paramref name="useIndices"/> is true.</param>
        /// <param name="propNames">The names of the properties to be used for sorting the array.</param>
        /// <param name="propFlags">The sorting flags for each property.</param>
        /// <param name="keys">The sorting keys.</param>
        /// <param name="comparer">The sorting comparer.</param>
        /// <param name="reverse">If this is set to true, the array must be reversed after sorting.</param>
        private static void _internalCreateSortOnKeysAndComparer(
            ReadOnlySpan<Value> objects,
            bool useIndices,
            ReadOnlySpan<int> indices,
            QName[] propNames,
            int[] propFlags,
            out int[] keys,
            out IComparer<int> comparer,
            out bool reverse
        ) {
            int nameCount = propNames.Length;
            bool hasCommonFlags = true;

            int commonFlags = propFlags[0] & (NUMERIC | CASEINSENSITIVE | DESCENDING);

            for (int i = 1; i < propFlags.Length && hasCommonFlags; i++)
                hasCommonFlags &= (propFlags[i] & (NUMERIC | CASEINSENSITIVE | DESCENDING)) == commonFlags;

            int objectCount = useIndices ? indices.Length : objects.Length;

            keys = new int[objectCount];
            int curKey = 0;

            for (int i = 0; i < keys.Length; i++) {
                keys[i] = curKey;
                curKey += nameCount;
            }

            if (hasCommonFlags) {
                // If the numeric, caseinsensitive and descending flags are the same for all
                // properties then the type-converted property values can be precomputed.

                if ((commonFlags & NUMERIC) != 0) {
                    double[] propValues = new double[checked(objectCount * nameCount)];

                    for (int i = 0, propIndex = 0; i < objectCount; i++) {
                        ASAny obj = objects[useIndices ? indices[i] : i].toAny();

                        for (int j = 0; j < nameCount; j++)
                            propValues[propIndex++] = (double)obj.AS_getProperty(propNames[j]);
                    }

                    comparer = new ArraySortHelper.NumericBlockComparer(propValues, nameCount);
                }
                else {
                    string[] propValues = new string[checked(objectCount * nameCount)];

                    for (int i = 0, propIndex = 0; i < objectCount; i++) {
                        ASAny obj = objects[useIndices ? indices[i] : i].toAny();

                        for (int j = 0; j < nameCount; j++)
                            propValues[propIndex++] = ASAny.AS_convertString(obj.AS_getProperty(propNames[j]));
                    }

                    comparer = new ArraySortHelper.StringBlockComparer(
                        propValues, nameCount, (commonFlags & CASEINSENSITIVE) != 0);
                }

                reverse = (commonFlags & DESCENDING) != 0;
            }
            else {
                // If the flags differ for each property then we can't type-convert
                // the precomputed property values, the conversions have to be done in the comparer.

                ASAny[] propValues = new ASAny[checked(objectCount * nameCount)];
                var propComparers = new IComparer<ASAny>[nameCount];
                var descendingFlags = new bool[nameCount];

                for (int i = 0; i < nameCount; i++) {
                    int flags = propFlags[i];
                    GenericComparerType comparerType;

                    if ((flags & NUMERIC) != 0)
                        comparerType = GenericComparerType.NUMERIC;
                    else if ((flags & CASEINSENSITIVE) != 0)
                        comparerType = GenericComparerType.STRING_IGNORECASE;
                    else
                        comparerType = GenericComparerType.STRING;

                    propComparers[i] = GenericComparer<ASAny>.getComparer(comparerType);
                    descendingFlags[i] = (flags & DESCENDING) != 0;
                }

                for (int i = 0, propIndex = 0; i < objectCount; i++) {
                    ASAny obj = objects[useIndices ? indices[i] : i].toAny();

                    for (int j = 0; j < nameCount; j++) {
                        ASAny propVal = obj.AS_getProperty(propNames[j]);

                        // If the property value is null or undefined and we are doing a string comparison,
                        // we need to convert the value to its string representation - this is because the
                        // comparer converts null and undefined to a null reference.

                        if (propVal.isUndefinedOrNull && (propFlags[j] & NUMERIC) == 0)
                            propVal = ASAny.AS_convertString(propVal);

                        propValues[propIndex++] = propVal;
                    }
                }

                comparer = new ArraySortHelper.GenericBlockComparer<ASAny>(propValues, nameCount, propComparers, descendingFlags);

                // The comparer takes the descending flags into account here, so no reversing after sorting.
                reverse = false;
            }
        }

        /// <summary>
        /// Checks if all keys are unique in a sorted span.
        /// </summary>
        /// <param name="sortedKeys">A span containing the keys to check. This must be sorted in the order
        /// defined by <paramref name="comparer"/>.</param>
        /// <param name="comparer">The <see cref="IComparer{T}"/> implementation that defines the ordering
        /// of the keys in <paramref name="sortedKeys"/>.</param>
        /// <typeparam name="T">The type of the keys.</typeparam>
        /// <returns>True if no two elements in <paramref name="sortedKeys"/> are equal, otherwise false.</returns>
        private static bool _doUniqueSortCheck<T>(ReadOnlySpan<T> sortedKeys, IComparer<T> comparer) {
            for (int i = 0; i + 1 < sortedKeys.Length; i++) {
                if (comparer.Compare(sortedKeys[i], sortedKeys[i + 1]) == 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Replaces the specified number of elements in the Array, starting at a given index with
        /// values from the given arguments, and returns another Array containing the values that have
        /// been deleted.
        /// </summary>
        ///
        /// <param name="args">See remarks.</param>
        /// <returns>An array containing the deleted range of elements.</returns>
        ///
        /// <remarks>
        /// <para>The arguments in the <paramref name="args"/> list are interpreted as
        /// follows:</para>
        /// <list type="bullet">
        /// <item><description>If no arguments are passed, the array is not modified and an empty array is
        /// returned.</description></item>
        /// <item><description>
        /// The first argument, <c>startIndex</c>, is the index from which elements should be
        /// removed and included in the returned array. If this is negative, the length of the Array
        /// is added to it; if it is still negative after adding the length, it is set to zero. If
        /// this is greater than the length of the array, it is set to the length of the array.
        /// </description></item>
        /// <item><description>The second argument, <c>deleteCount</c>, is the number of elements that must be
        /// removed. If this is negative, it is set to zero; if <c>startIndex + deleteCount</c> is
        /// greater than the array's length, or only one argument is passed, this is set to
        /// <c>length - startIndex</c>.</description></item>
        /// <item><description>If there are any passed arguments following the second, they will be considered as a
        /// list of values to be inserted into the array at <c>startIndex</c> after removing
        /// <c>deleteCount</c> elements.</description></item>
        /// </list>
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual ASArray splice(RestParam args = default) {
            if (args.length == 0)
                return new ASArray();

            uint startIndex = _normalizeIndex((double)args[0], m_length);
            int maxDeleteCount = (int)Math.Min(m_length - startIndex, (uint)Int32.MaxValue);

            int deleteCount;
            if (args.length < 2) {
                deleteCount = maxDeleteCount;
            }
            else {
                double dDeleteCount = (double)args[1];
                if (Double.IsNaN(dDeleteCount) || dDeleteCount < 0.0)
                    deleteCount = 0;
                else
                    deleteCount = (int)Math.Min(dDeleteCount, (double)maxDeleteCount);
            }

            uint endIndex = startIndex + (uint)deleteCount;
            int insertCount = Math.Max(args.length - 2, 0);

            ReadOnlySpan<ASAny> newValues = (insertCount > 0) ? args.getSpan().Slice(2, insertCount) : default;
            ASArray spliceArray;

            if (_isDenseArray() && deleteCount == insertCount && endIndex <= (uint)m_values.Length) {
                // Special case: Dense array with delete count same as insert count.
                // No shifting in this case.

                spliceArray = new ASArray();
                spliceArray.m_values = new Value[deleteCount];

                Span<Value> values = m_values.AsSpan((int)startIndex, deleteCount);
                Span<Value> spliceValues = spliceArray.m_values.AsSpan(0, deleteCount);

                for (int i = 0; i < values.Length; i++) {
                    ref Value val = ref values[i];
                    if (val.isEmpty) {
                        // Holes in the returned array must be filled with the prototype value, like in slice().
                        // Also increment nonEmptyCount because the hole in this array will be filled with the
                        // argument value for its position.
                        spliceValues[i] = Value.fromAny(AS_getElement((uint)i + startIndex));
                        m_nonEmptyCount++;
                    }
                    else {
                        spliceValues[i] = val;
                    }
                    val = Value.fromAny(newValues[i]);
                }

                m_totalCount = Math.Max(m_totalCount, (int)endIndex);
                m_length = Math.Max(m_length, endIndex);

                spliceArray.m_nonEmptyCount = deleteCount;
                spliceArray.m_totalCount = deleteCount;
                spliceArray.m_length = (uint)deleteCount;
            }
            else {
                spliceArray = _internalSlice(startIndex, deleteCount);
                _internalSpliceReplaceRange(startIndex, deleteCount, newValues);
            }

            return spliceArray;
        }

        private void _internalSpliceReplaceRange(uint startIndex, int deleteCount, ReadOnlySpan<ASAny> newValues) {
            int sizeDelta = newValues.Length - deleteCount;
            uint delEndIndex = startIndex + (uint)deleteCount;

            uint newArrLength;
            if (sizeDelta <= 0)
                newArrLength = m_length + (uint)sizeDelta;
            else
                newArrLength = m_length + Math.Min((uint)sizeDelta, UInt32.MaxValue - m_length);

            bool denseConvertedToHash = false;

            if (_isDenseArray()) {
                // Set all the slots that we are going to delete to empty.
                // Update the nonEmptyCount accordingly.
                // If the load factor gets too low (after accounting for the new items yet to be inserted),
                // switch to a hash table storage.

                if (startIndex <= (uint)m_totalCount) {
                    int deleteRangeLength = Math.Min(deleteCount, m_totalCount - (int)startIndex);
                    Span<Value> deleteRange = m_values.AsSpan((int)startIndex, deleteRangeLength);

                    for (int i = 0; i < deleteRange.Length; i++) {
                        if (!deleteRange[i].isEmpty) {
                            deleteRange[i] = Value.empty;
                            m_nonEmptyCount--;
                        }
                    }
                }

                if (!_canUseDenseArray(m_nonEmptyCount + newValues.Length, newArrLength)) {
                    denseConvertedToHash = true;
                    _denseArrayToHash();
                }
            }

            if (_isDenseArray()) {
                // This is a dense array and does not have to be converted to a hash table.
                // So shift the elements after the deleted range (startIndex + deleteCount) to their new positions.
                // If we need to grow or shrink the backing array, do the copying and shifting in one pass.

                Value[] values = m_values;
                uint shiftBegin = startIndex + (uint)deleteCount;

                if (newArrLength <= (uint)values.Length && newArrLength >= (uint)(values.Length >> 2)) {
                    if (shiftBegin < (uint)m_totalCount) {
                        values.AsSpan((int)shiftBegin, m_totalCount - (int)shiftBegin)
                            .CopyTo(values.AsSpan((int)shiftBegin + sizeDelta));

                        if (sizeDelta < 0)
                            values.AsSpan(m_totalCount + sizeDelta, -sizeDelta).Clear();
                    }
                }
                else {
                    int newDenseArrayLen = (newArrLength > (uint)values.Length)
                        ? Math.Max((int)newArrLength, values.Length * 2)
                        : Math.Max((int)newArrLength, values.Length >> 1);

                    var newArrayValues = new Value[newDenseArrayLen];
                    values.AsSpan(0, (int)Math.Min(startIndex, (uint)m_totalCount)).CopyTo(newArrayValues);

                    if (shiftBegin < (uint)m_totalCount) {
                        values.AsSpan((int)shiftBegin, m_totalCount - (int)shiftBegin)
                            .CopyTo(newArrayValues.AsSpan((int)shiftBegin + sizeDelta));
                    }

                    m_values = newArrayValues;
                }

                m_totalCount = (int)newArrLength;
            }
            else {
                // This Array instance is currently using hash table storage.
                // Delete all elements whose keys are in the delete range [startIndex, startIndex + deleteCount - 1],
                // and add the size difference to all keys >= startIndex + deleteCount.
                // In addition, check if the new load factor (after accounting for the elements yet to be inserted)
                // is greater than the dense array load factor threshold, and switch to a dense array if that is the case.

                Span<Value> values = m_values.AsSpan(0, m_totalCount);
                HashLink[] hashLinks = m_hashLinks;

                for (int i = 0; i < values.Length; i++) {
                    ref Value val = ref values[i];
                    if (val.isEmpty)
                        continue;

                    ref uint key = ref hashLinks[i].key;
                    if (key >= startIndex && key < delEndIndex) {
                        m_nonEmptyCount--;
                        val = default;   // This marks the hash slot for deletion when _resetHashTableChains is called.
                    }
                    else if (key >= delEndIndex) {
                        if (sizeDelta > 0 && key >= UInt32.MaxValue - (uint)sizeDelta)
                            val = default;  // Discard overflowing elements
                        else
                            key += (uint)sizeDelta;
                    }
                }

                if (!denseConvertedToHash
                    && _canUseDenseArray(m_nonEmptyCount + newValues.Length, newArrLength))
                {
                    // Converting to dense array doesn't depend on chain correctness, so safe to call here.
                    _hashToDenseArray((int)newArrLength);
                }
                else {
                    _resetHashTableChains();
                }
            }

            // Insert the new elements
            if (_isDenseArray()) {
                var values = m_values.AsSpan((int)startIndex, newValues.Length);
                for (int i = 0; i < newValues.Length; i++)
                    values[i] = Value.fromAny(newValues[i]);

                _setDenseArrayTotalCount((int)newArrLength);
                m_nonEmptyCount += newValues.Length;
            }
            else {
                int insertCount = (int)Math.Min((uint)newValues.Length, UInt32.MaxValue - startIndex);
                for (int i = 0; i < insertCount; i++)
                    _hashSetValue(startIndex + (uint)i, Value.fromAny(newValues[i]));
            }

            m_length = newArrLength;
        }

        /// <summary>
        /// Returns a locale-specific string representation of the current Array instance.
        /// </summary>
        /// <returns>A locale-specific string representation of the current array.</returns>
        //[AVM2ExportTrait(name = "toString", nsName = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod(name = "toLocaleString")]
        public new string toLocaleString() {
            // ECMA-262, 22.1.3.26
            // "The elements of the array are converted to Strings using their toLocaleString methods,
            //  and these Strings are then concatenated, separated by occurrences of a separator
            //  String that has been derived in an implementation-defined locale-specific way."
            //

            int len = (int)Math.Min(m_length, (uint)Int32.MaxValue);
            var strings = new string[len];

            string callToLocaleStringOnElement(ASAny elem) {
                if (elem.value == null)
                    return null;

                ASAny result = elem.value.AS_callProperty(QName.publicName("toLocaleString"), ReadOnlySpan<ASAny>.Empty);
                return ASAny.AS_convertString(result);
            }

            if (_isDenseArrayWithoutEmptySlots()) {
                var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);
                for (int i = 0; i < valuesSpan.Length; i++)
                    strings[i] = callToLocaleStringOnElement(valuesSpan[i].toAny());
            }
            else {
                for (int i = 0; i < len; i++)
                    strings[i] = callToLocaleStringOnElement(AS_getElement((uint)i));
            }

            return String.Join(CultureInfo.CurrentCulture.TextInfo.ListSeparator, strings);
        }

        /// <summary>
        /// Returns the string representation of the current instance. This method is equivalent to
        /// <c>join(",")</c>.
        /// </summary>
        /// <returns>The string representation of the current instance.</returns>
        ///
        /// <remarks>
        /// This method is exported to the AVM2 with the name <c>toString</c>, but must be called
        /// from .NET code with the name <c>AS_toString</c> to avoid confusion with the
        /// <see cref="Object.ToString" qualifyHint="true"/> method.
        /// </remarks>
        //[AVM2ExportTrait(name = "toString", nsName = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod(name = "toString")]
        public new string AS_toString() => join();

        /// <summary>
        /// Adds the values in the <paramref name="args"/> array to the beginning of the Array, and
        /// returns the new length of the Array. All existing elements in the array are shifted
        /// forward by the length of the <paramref name="args"/> array.
        /// </summary>
        ///
        /// <param name="args">The new values to add to the beginning of the Array.</param>
        /// <returns>The new length of the Array.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual uint unshift(RestParam args = default) {
            int argCount = args.length;

            if (argCount == 0)
                return m_length;

            uint newArrLength = m_length + Math.Min(UInt32.MaxValue - m_length, (uint)argCount);
            bool denseConvertedToHash = false;

            if (_isDenseArray() && !_canUseDenseArray(m_nonEmptyCount + argCount, newArrLength)) {
                denseConvertedToHash = true;
                _denseArrayToHash();
            }

            if (_isDenseArray()) {
                if (m_totalCount + argCount > m_values.Length) {
                    int newArraySize = Math.Max(Math.Max(m_totalCount + argCount, m_values.Length * 2), 4);
                    Value[] newValues = new Value[newArraySize];
                    m_values.AsSpan(0, m_totalCount).CopyTo(newValues.AsSpan(argCount));
                    m_values = newValues;
                }
                else {
                    m_values.AsSpan(0, m_totalCount).CopyTo(m_values.AsSpan(argCount));
                }
                m_totalCount += argCount;
            }
            else {
                var values = m_values.AsSpan(0, m_totalCount);
                HashLink[] hashLinks = m_hashLinks;

                // Shift hash keys and then rebuild chains.
                for (int i = 0; i < values.Length; i++) {
                    if (values[i].isEmpty)
                        continue;

                    ref uint key = ref hashLinks[i].key;
                    if (key < UInt32.MaxValue - (uint)argCount) {
                        key += (uint)argCount;
                    }
                    else {
                        // Discard elements that are shifted beyond the maximum array length.
                        values[i] = Value.empty;
                    }
                }

                if (!denseConvertedToHash && _canUseDenseArray(m_nonEmptyCount + argCount, newArrLength))
                    _hashToDenseArray((int)newArrLength);
                else
                    _resetHashTableChains();
            }

            if (_isDenseArray()) {
                var values = m_values.AsSpan(0, argCount);
                for (int i = 0; i < values.Length; i++)
                    values[i] = Value.fromAny(args[i]);

                m_nonEmptyCount += argCount;
            }
            else {
                for (int i = 0; i < argCount; i++)
                    _hashSetValue((uint)i, Value.fromAny(args[i]));
            }

            m_length = newArrLength;
            return newArrLength;
        }

        /// <inheritdoc/>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public override bool propertyIsEnumerable(ASAny name = default(ASAny)) {
            uint index = UInt32.MaxValue;

            if (ASObject.AS_isUint(name.value))
                index = (uint)name.value;
            else if (NumberFormatHelper.parseArrayIndex((string)name, allowLeadingZeroes: false, out uint parsedIndex))
                index = parsedIndex;

            if (index != UInt32.MaxValue)
                return !_getValueAt(index).isEmpty;

            return base.propertyIsEnumerable(name);
        }

        /// <inheritdoc/>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public override bool hasOwnProperty(ASAny name = default(ASAny)) {
            uint index = UInt32.MaxValue;

            if (ASObject.AS_isUint(name.value))
                index = (uint)name.value;
            else if (NumberFormatHelper.parseArrayIndex((string)name, allowLeadingZeroes: false, out uint parsedIndex))
                index = parsedIndex;

            if (index != UInt32.MaxValue)
                return !_getValueAt(index).isEmpty;

            return base.hasOwnProperty(name);
        }

        /// <exclude/>
        ///
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABC to IL compiler to invoke the ActionScript Array class constructor. This must not be called
        /// by outside .NET code. Array objects constructed from .NET code must use the constructor
        /// defined on the <see cref="ASArray"/> type.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) => new ASArray(new RestParam(args));

    }

}
