using System;
using System.Collections.Generic;
using System.Globalization;
using Mariana.AVM2.Native;

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
        /// Used for directly sorting objects in their internal representation in an Array.
        /// </summary>
        private class ArrayValueInternalComparer : IComparer<Value> {
            public IComparer<ASAny> baseComparer;
            public int Compare(Value x, Value y) => baseComparer.Compare(x.toAny(), y.toAny());
        }

        [ThreadStatic]
        private static ArrayValueInternalComparer s_threadStaticValueComparer;

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
        /// <term>RangeError #1005</term>
        /// <description><paramref name="length"/> is not a positive integer.</description>
        /// </list>
        /// </exception>
        public ASArray(int length) {
            if (length < 0)
                throw ErrorHelper.createError(ErrorCode.ARRAY_LENGTH_NOT_POSITIVE_INTEGER, length);

            m_values = new Value[Math.Max(length, 4)];
            m_length = (uint)length;
            m_nonEmptyCount = 0;
            m_totalCount = 0;
        }

        /// <summary>
        /// Creates a new Array with the specified initial length.
        /// </summary>
        /// <param name="length">The initial length of the array.</param>
        public ASArray(uint length) {
            m_values = (length < (uint)Int32.MaxValue) ? new Value[Math.Max((int)length, 4)] : Array.Empty<Value>();
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
        /// <term>RangeError #1005</term>
        /// <description>There is only one argument which is of a numeric type but not a positive
        /// integer.</description>
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
                double arg1_d = (double)args[0];
                uint arg1_u = (uint)arg1_d;

                if ((double)arg1_u != arg1_d)
                    throw ErrorHelper.createError(ErrorCode.ARRAY_LENGTH_NOT_POSITIVE_INTEGER, arg1_d);

                m_values = (arg1_u < (uint)Int32.MaxValue) ? new Value[(int)arg1_u] : Array.Empty<Value>();
                m_length = arg1_u;
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
        /// Creates a new <see cref="ASArray"/> instance using values from the given span.
        /// </summary>
        ///
        /// <param name="span">A span containing the elements of the array to be created.</param>
        /// <typeparam name="T">The type of the elements in the span.</typeparam>
        /// <returns>The created array.</returns>
        ///
        /// <remarks>
        /// If the element type is <see cref="ASObject"/> or a subclass of it, use of
        /// the faster <see cref="fromObjectSpan{T}"/> method is recommended.
        /// </remarks>
        public static ASArray fromSpan<T>(ReadOnlySpan<T> span) {
            ASArray array = new ASArray(span.Length);
            Span<Value> values = array.m_values.AsSpan(0, span.Length);
            var converter = GenericTypeConverter<T, ASAny>.instance;

            for (int i = 0; i < values.Length; i++)
                values[i] = Value.fromAny(converter.convert(span[i]));

            array.m_nonEmptyCount = span.Length;
            array.m_totalCount = span.Length;

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
        /// Use of this method is recommended instead <see cref="fromSpan{T}"/> when the
        /// element type is <see cref="ASObject"/> or a subclass of it, as it has better
        /// performance
        /// </remarks>
        public static ASArray fromObjectSpan<T>(ReadOnlySpan<T> span) where T : ASObject {
            ASArray array = new ASArray(span.Length);
            Span<Value> values = array.m_values.AsSpan(0, span.Length);

            for (int i = 0; i < values.Length; i++)
                values[i] = Value.fromObject(span[i]);

            array.m_nonEmptyCount = span.Length;
            array.m_totalCount = span.Length;

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
        /// A value less than the length of the <see cref="m_values"/> array of this instance, from
        /// which <see cref="m_totalCount"/> must be computed. Callers must ensure that all elements
        /// in <see cref="m_values"/> at indices not less than this value are null; this is not
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
            var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);
            HashLink[] hashLinks = new HashLink[m_values.Length];

            m_hashEmptyChainHead = -1;

            for (int i = 0; i < hashLinks.Length; i++)
                hashLinks[i].headOfChain = -1;

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
            if (newLength >= m_length) {
                m_length = newLength;
                return;
            }

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
                _updateArrayStorage(true);
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
                _updateArrayStorage(true);
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
            return (index >= 0.0)
                ? (uint)Math.Min(index, (double)length)
                : (uint)Math.Max(index + (double)length, 0.0);
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
                uint srcEndIndex = Math.Min(srcIndex + (uint)dest.Length, (uint)m_totalCount);
                if (srcEndIndex <= srcIndex)
                    return;

                var valuesSpan = new ReadOnlySpan<Value>(m_values, (int)srcIndex, (int)(srcEndIndex - srcIndex));

                for (int i = 0; i < valuesSpan.Length; i++)
                    dest[i] = converter.convert(valuesSpan[i].toAny());

                if (dest.Length > valuesSpan.Length)
                    dest.Slice(valuesSpan.Length).Fill(converter.convert(default(ASAny)));
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
        /// If the length of the array is greater than 2^31-1, only elements upto that index will be
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
                for (int i = 0; i < m_totalCount; i++)
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

        private ASAny _getElementFallback(string indexStr) {
            var name = QName.publicName(indexStr);
            BindStatus bindStatus = base.AS_tryGetProperty(name, out ASAny value);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return value;
            throw ErrorHelper.createBindingError(AS_class.name, name, bindStatus);
        }

        private void _setElementFallback(string indexStr, ASAny value) {
            var name = QName.publicName(indexStr);
            BindStatus bindStatus = base.AS_trySetProperty(name, value);
            if (bindStatus != BindStatus.SUCCESS && bindStatus != BindStatus.SOFT_SUCCESS)
                throw ErrorHelper.createBindingError(AS_class.name, name, bindStatus);
        }

        private bool _hasElementFallback(string indexStr) => base.AS_hasProperty(QName.publicName(indexStr));

        private bool _deleteElementFallback(string indexStr) => base.AS_deleteProperty(QName.publicName(indexStr));

        /// <summary>
        /// Gets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The value of the element, or undefined if no element exists at the given
        /// index.</returns>
        public ASAny AS_getElement(uint index) {
            Value val = _getValueAt(index);
            return !val.isEmpty ? val.toAny() : _getElementFallback(ASuint.AS_convertString(index));
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
            return !val.isEmpty || _hasElementFallback(ASuint.AS_convertString(index));
        }

        /// <summary>
        /// Sets the value of the element at the given index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value of the element.</param>
        public void AS_setElement(uint index, ASAny value) {
            if (index == UInt32.MaxValue) {  // Not a valid array index
                _setElementFallback(ASuint.AS_convertString(index), value);
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
            }
            else {
                _hashSetValue(index, Value.fromAny(value), out bool newHashSlotCreated);

                // If an element is added to the hash table (and it did not overwrite an
                // existing slot), check whether the hash table's load factor has crossed
                // the dense array threshold and convert to a dense array if that is the case.
                if (newHashSlotCreated)
                    _updateArrayStorage();
            }

            m_length = Math.Max(m_length, index + 1);
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
                return _deleteElementFallback(ASuint.AS_convertString(index));

            if (_isDenseArray()) {
                ref Value slot = ref m_values[(int)index];

                if (index >= (uint)m_totalCount || slot.isEmpty)
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

            _updateArrayStorage(true);
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
            (index >= 0) ? AS_getElement((uint)index) : _getElementFallback(ASint.AS_convertString(index));

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
            (index >= 0) ? AS_hasElement((uint)index) : _hasElementFallback(ASint.AS_convertString(index));

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
                _setElementFallback(ASint.AS_convertString(index), value);
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
            (index >= 0) ? AS_deleteElement((uint)index) : _deleteElementFallback(ASint.AS_convertString(index));

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
                : _getElementFallback(ASNumber.AS_convertString(index));
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
                : _hasElementFallback(ASNumber.AS_convertString(index));
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
                _setElementFallback(ASNumber.AS_convertString(index), value);
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
                : _deleteElementFallback(ASNumber.AS_convertString(index));
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

        #region BindingMethodOverrides

        /// <inheritdoc/>
        public override bool AS_hasProperty(
            in QName name,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, false, out uint index)
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
                && NumberFormatHelper.parseArrayIndex(name, false, out uint index)
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
                && NumberFormatHelper.parseArrayIndex(name.localName, false, out uint index))
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
                && NumberFormatHelper.parseArrayIndex(name, false, out uint index))
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
                && NumberFormatHelper.parseArrayIndex(name.localName, false, out uint index)
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
                && NumberFormatHelper.parseArrayIndex(name, false, out uint index)
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
                : AS_trySetProperty(QName.publicName(ASAny.AS_convertString(key)), value, options);
        }

        /// <inheritdoc/>
        public override bool AS_deleteProperty(
            in QName name, BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, false, out uint index)
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
                && NumberFormatHelper.parseArrayIndex(name, false, out uint index)
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
                : AS_deleteProperty(QName.publicName(ASAny.AS_convertString(key)), options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryCallProperty(
            in QName name, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && name.ns.isPublic
                && NumberFormatHelper.parseArrayIndex(name.localName, false, out uint index))
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
                && NumberFormatHelper.parseArrayIndex(name, false, out uint index))
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
                && NumberFormatHelper.parseArrayIndex(name.localName, false, out uint index))
            {
                Value v = _getValueAt(index);
                if (!v.isEmpty) {
                    return v.toAny().AS_tryConstruct(args, out result)
                        ? BindStatus.SUCCESS
                        : BindStatus.FAILED_NOTCONSTRUCTOR;
                }
            }
            return base.AS_tryCallProperty(name, args, out result, options);
        }

        /// <inheritdoc/>
        public override BindStatus AS_tryConstructProperty(
            string name, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic
                && NumberFormatHelper.parseArrayIndex(name, false, out uint index))
            {
                Value v = _getValueAt(index);
                if (!v.isEmpty) {
                    return v.toAny().AS_tryConstruct(args, out result)
                        ? BindStatus.SUCCESS
                        : BindStatus.FAILED_NOTCONSTRUCTOR;
                }
            }
            return base.AS_tryCallProperty(name, nsSet, args, out result, options);
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
            do {
                index++;
            } while (index <= m_totalCount && m_values[index - 1].isEmpty);

            if (index <= m_totalCount)
                return index;

            return base.AS_nextIndex(index - m_totalCount);
        }

        /// <summary>
        /// Gets the name of the dynamic property at the given index.
        /// </summary>
        /// <param name="index">The one-based index of the property. This index is usually obtained
        /// from the <see cref="AS_nextIndex"/> method.</param>
        /// <returns>The property name.</returns>
        public override ASAny AS_nameAtIndex(int index) =>
            (index <= m_totalCount) ? (ASAny)(index - 1) : base.AS_nameAtIndex(index - m_totalCount);

        /// <summary>
        /// Gets the value of the dynamic property at the given index.
        /// </summary>
        /// <param name="index">The one-based index of the property. This index is usually returned
        /// from the <see cref="AS_nextIndex"/> method.</param>
        /// <returns>The property value.</returns>
        public override ASAny AS_valueAtIndex(int index) =>
            (index <= m_totalCount) ? m_values[index - 1].toAny() : base.AS_valueAtIndex(index - m_totalCount);

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
            int newNonEmptyCount = m_nonEmptyCount;
            uint newLength = m_length;

            // Determine the amount of space that needs to be allocated for the result array
            // and its load factor (which determines whether the result array storage will be dense
            // or a hash table). Overflow checking is done here.

            for (int i = 0; i < nArgs; i++) {
                ASObject arg = args[i].value;

                if (arg is ASArray argArray) {
                    newNonEmptyCount = checked(newNonEmptyCount + argArray.m_nonEmptyCount);
                    newLength = checked(newLength + argArray.m_length);
                }
                else if (arg is ASVectorAny argVector) {
                    int vecLength = argVector.length;
                    newNonEmptyCount = checked(newNonEmptyCount + vecLength);
                    newLength = checked(newLength + (uint)vecLength);
                }
                else {
                    newNonEmptyCount = checked(newNonEmptyCount + 1);
                    newLength = checked(newLength + 1);
                }
            }

            // Create the result array.

            bool newArrayUseHash = !_canUseDenseArray(newNonEmptyCount, newLength);

            ASArray newArray;
            if (newArrayUseHash) {
                newArray = new ASArray(newNonEmptyCount);
                newArray._denseArrayToHash();
            }
            else {
                newArray = new ASArray((int)newLength);
            }

            uint curIndex = 0;

            // Copy current array contents.
            newArray._internalConcatArray(this, ref curIndex);

            // Copy contents from arguments

            for (int i = 0; i < nArgs; i++) {
                ASAny arg = args[i];

                if (arg.value is ASArray argArray) {
                    newArray._internalConcatArray(argArray, ref curIndex);
                }
                else if (arg.value is ASVectorAny argVector) {
                    newArray._internalConcatVector(argVector, ref curIndex);
                }
                else if (newArrayUseHash) {
                    newArray._hashSetValue(curIndex, Value.fromAny(arg));
                }
                else {
                    newArray.m_values[(int)curIndex] = Value.fromAny(arg);
                    newArray.m_nonEmptyCount++;
                    newArray.m_totalCount++;
                    curIndex++;
                }
            }

            if (newArray._isDenseArray())
                newArray._setDenseArrayTotalCount(newArray.m_totalCount);

            newArray.m_length = newLength;
            return newArray;
        }

        private void _internalConcatArray(ASArray srcArr, ref uint curIndex) {
            // We don't need to check the additions or int-uint casts here
            // for overflow since the checks done in the concat() method ensure
            // that it will never happen.

            // Four cases need to be handled here:
            // (1) Source and destination storage is array.
            // (2) Source storage is array, destination is hash table
            // (3) Source storage is hash table, destination is array
            // (4) Source and destination storage is hash table.

            var srcValues = new ReadOnlySpan<Value>(srcArr.m_values, 0, srcArr.m_totalCount);
            HashLink[] srcHashLinks = srcArr.m_hashLinks;

            if (_isDenseArray()) {
                if (srcArr._isDenseArray()) {
                    // Case (1)
                    srcValues.CopyTo(m_values.AsSpan((int)curIndex, srcValues.Length));
                }
                else {
                    // Case (2)
                    for (int i = 0; i < srcValues.Length; i++) {
                        Value val = srcValues[i];
                        if (!val.isEmpty)
                            m_values[(int)(curIndex + srcHashLinks[i].key)] = val;
                    }
                }

                m_nonEmptyCount += srcArr.m_nonEmptyCount;
                m_totalCount = (int)(curIndex + srcArr.m_length);
            }
            else {
                // Case (3) and (4)
                bool srcIsHash = !srcArr._isDenseArray();

                for (int i = 0; i < srcValues.Length; i++) {
                    Value val = srcValues[i];
                    if (!val.isEmpty) {
                        uint key = srcIsHash ? srcHashLinks[i].key : (uint)i;
                        _hashSetValue(curIndex + key, val);
                    }
                }
            }

            curIndex += srcArr.m_length;
        }

        private void _internalConcatVector(ASVectorAny srcVec, ref uint curIndex) {
            int vecLength = srcVec.length;

            if (_isDenseArray()) {
                var valuesSpan = m_values.AsSpan((int)curIndex, vecLength);
                for (int i = 0; i < valuesSpan.Length; i++)
                    valuesSpan[i] = Value.fromObject(srcVec.AS_getElement(i));

                m_nonEmptyCount += vecLength;
                m_totalCount += vecLength;
            }
            else {
                for (int i = 0; i < vecLength; i++)
                    _hashSetValue(curIndex + (uint)i, Value.fromObject(srcVec.AS_getElement(i)));
            }

            curIndex += (uint)vecLength;
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
        /// third is a reference to the Array instance that called this method. If the callback
        /// function returns a non-Boolean value, it will be converted to a Boolean. If this is null,
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
        /// <term>TypeError #1510</term>
        /// <description><paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null or undefined.</description>
        /// </item>
        /// <item>
        /// <term>Any error</term>
        /// <description>The callback function throws an exception.</description>
        /// </item>
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
                //throw AVM2Errors.createError<ASTypeError>(2007, "callback");

            if (thisObject.value != null && callback is ASMethodClosure)
                throw ErrorHelper.createError(ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL);

            ASAny[] cbArgsArray = new ASAny[3];
            cbArgsArray[2] = this;

            ReadOnlySpan<ASAny> cbArgs = cbArgsArray.AsSpan(0, Math.Min(cbArgsArray.Length, callback.length));

            if (_isDenseArrayWithoutEmptySlots()) {
                var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);
                for (int i = 0; i < valuesSpan.Length; i++) {
                    cbArgsArray[0] = valuesSpan[i].toAny();
                    cbArgsArray[1] = (uint)i;
                    if (!(bool)callback.AS_invoke(thisObject, cbArgs))
                        return false;
                }
            }
            else {
                for (uint i = 0, n = m_length; i < n; i++) {
                    cbArgsArray[0] = AS_getElement(i);
                    cbArgsArray[1] = i;
                    if (!(bool)callback.AS_invoke(thisObject, cbArgs))
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
        /// third is a reference to the Array instance that called this method. If the callback
        /// function returns a non-Boolean value, it will be converted to a Boolean. If this is null,
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
        /// <term>TypeError #1510</term>
        /// <description><paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null or undefined.</description>
        /// </item>
        /// <item>
        /// <term>Any error</term>
        /// <description>The callback function throws an exception.</description>
        /// </item>
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
                // AVM2Errors.createError<ASTypeError>(2007, "callback");

            if (thisObject.value != null && callback is ASMethodClosure)
                throw ErrorHelper.createError(ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL);

            ASArray resultArray = new ASArray();
            uint resultCount = 0;

            ASAny[] cbArgsArray = new ASAny[3];
            cbArgsArray[2] = this;

            ReadOnlySpan<ASAny> cbArgs = cbArgsArray.AsSpan(0, Math.Min(cbArgsArray.Length, callback.length));

            void check(uint ind, ASAny val, in ReadOnlySpan<ASAny> _cbArgs) {
                cbArgsArray[0] = val;
                cbArgsArray[1] = ind;

                if ((bool)callback.AS_invoke(thisObject, _cbArgs)) {
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
        /// <term>TypeError #1510</term>
        /// <description><paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null or undefined.</description>
        /// </item>
        /// <item>
        /// <term>Any error</term>
        /// <description>The callback function throws an exception.</description>
        /// </item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual void forEach(ASFunction callback, ASAny thisObject = default(ASAny)) {
            if (callback == null)
                return;
                // throw AVM2Errors.createError<ASTypeError>(2007, "callback");

            if (thisObject.value != null && callback is ASMethodClosure)
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
            uint fromIndexU = _normalizeIndex((double)fromIndex, m_length);

            if (_isDenseArrayWithoutEmptySlots()) {
                if (fromIndexU >= (uint)m_totalCount)
                    return -1.0;

                var valuesSpan = new ReadOnlySpan<Value>(m_values, (int)fromIndexU, m_totalCount - (int)fromIndexU);
                for (int i = 0; i < valuesSpan.Length; i++) {
                    if (ASAny.AS_strictEq(searchElement, valuesSpan[i].toAny()))
                        return (double)i;
                }
            }
            else {
                for (uint i = 0, n = m_length; i < n; i++) {
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
            ASAny searchElement, [ParamDefaultValue(Int32.MaxValue)] ASAny fromIndex)
        {
            uint fromIndexU = _normalizeIndex((double)fromIndex, m_length);

            if (_isDenseArrayWithoutEmptySlots()) {
                var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, (int)Math.Min(fromIndexU, (uint)m_totalCount));
                for (int i = valuesSpan.Length - 1; i >= 0; i--) {
                    if (ASAny.AS_strictEq(searchElement, valuesSpan[i].toAny()))
                        return (double)i;
                }
            }
            else {
                for (uint i = m_length; i >= 0; i--) {
                    if (ASAny.AS_strictEq(searchElement, AS_getElement(i)))
                        return (double)i;
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
        /// <term>TypeError #1510</term>
        /// <description><paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null or undefined.</description>
        /// </item>
        /// <item>
        /// <term>Any error</term>
        /// <description>The callback function throws an exception.</description>
        /// </item>
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

            if (thisObject.value != null && callback is ASMethodClosure)
                throw ErrorHelper.createError(ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL);

            int length = (int)Math.Min(m_length, (uint)Int32.MaxValue);

            ASArray resultArray = new ASArray(length);
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
                _updateArrayStorage(true);

                return popped.toAny();
            }
            else {
                Value popped = _hashDeleteAndGet(m_length - 1);
                m_length--;
                _updateArrayStorage(true);
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
                    Array.Resize(ref m_values, Math.Max(curLen + argCount, m_values.Length * 2));

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
                _updateArrayStorage(true);
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
                _updateArrayStorage(true);

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
            uint startIndexU = _normalizeIndex((double)startIndex, m_length);
            uint endIndexU = _normalizeIndex((double)endIndex, m_length);

            if (endIndexU <= startIndexU)
                return new ASArray();

            // slice() always returns a dense array, so the slice length is limited
            // to the max dense array size.
            if (endIndexU - startIndexU > (uint)Int32.MaxValue)
                endIndexU = startIndexU + (uint)Int32.MaxValue;

            int sliceLength = (int)(endIndexU - startIndexU);
            return _internalSlice(startIndexU, sliceLength);
        }

        private ASArray _internalSlice(uint startIndex, int sliceLength) {
            if (sliceLength == 0)
                return new ASArray();

            ASArray sliceArray = new ASArray(sliceLength);
            Span<Value> sliceValues = sliceArray.m_values.AsSpan(0, sliceLength);

            if (_isDenseArrayWithoutEmptySlots()) {
                int sliceLengthInArray = (startIndex >= (uint)m_values.Length)
                    ? 0
                    : Math.Min(sliceLength, m_values.Length - (int)startIndex);

                if (sliceLengthInArray > 0) {
                    ReadOnlySpan<Value> src = m_values.AsSpan((int)startIndex, sliceLengthInArray);
                    Span<Value> dest = sliceValues.Slice(0, sliceLengthInArray);
                    for (int i = 0; i < src.Length ; i++)
                        dest[i] = src[i].isEmpty ? Value.undef : src[i];
                }

                if (sliceLengthInArray < sliceLength)
                    sliceValues.Slice(sliceLengthInArray).Fill(Value.undef);
            }
            else {
                for (int i = 0; i < sliceValues.Length; i++)
                    sliceValues[i] = Value.fromAny(AS_getElement(startIndex + (uint)i));
            }

            sliceArray.m_nonEmptyCount = sliceLength;
            sliceArray.m_totalCount = sliceLength;

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
        /// third is a reference to the Array instance that called this method. If the callback
        /// function returns a non-Boolean value, it will be converted to a Boolean. If this is null,
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
        /// <term>TypeError #1510</term>
        /// <description><paramref name="callback"/> is a method closure, and
        /// <paramref name="thisObject"/> is not null or undefined.</description>
        /// </item>
        /// <item>
        /// <term>Any error</term>
        /// <description>The callback function throws an exception.</description>
        /// </item>
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

            if (thisObject.value != null && callback is ASMethodClosure)
                throw ErrorHelper.createError(ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL);

            ASAny[] cbArgsArray = new ASAny[3];
            cbArgsArray[2] = this;

            ReadOnlySpan<ASAny> cbArgs = cbArgsArray.AsSpan(0, Math.Min(cbArgsArray.Length, callback.length));

            if (_isDenseArrayWithoutEmptySlots()) {
                var valuesSpan = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);
                for (int i = 0; i < valuesSpan.Length; i++) {
                    cbArgsArray[0] = m_values[i].toAny();
                    cbArgsArray[1] = (uint)i;
                    if ((bool)callback.AS_invoke(thisObject, cbArgs))
                        return true;
                }
            }
            else {
                for (uint i = 0, n = m_length; i < n; i++) {
                    cbArgsArray[0] = AS_getElement(i);
                    cbArgsArray[1] = i;
                    if ((bool)callback.AS_invoke(thisObject, cbArgs))
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
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual ASAny sort(RestParam args = default) {
            ASObject arg1 = null, arg2 = null;
            if (args.length >= 1)
                arg1 = args[0].value;
            if (args.length >= 2)
                arg2 = args[1].value;

            _prepareArrayForSorting(out int sortElementCount);

            int flags;
            bool success = false;
            ASArray returnArray = null;

            if (arg1 is ASFunction func) {
                IComparer<ASAny> cmp = GenericComparer<ASAny>.getComparer(func);
                flags = (int)arg2;

                ArrayValueInternalComparer valueComparer = s_threadStaticValueComparer;
                if (valueComparer == null)
                    valueComparer = s_threadStaticValueComparer = new ArrayValueInternalComparer();

                valueComparer.baseComparer = cmp;
                try {
                    if ((flags & RETURNINDEXEDARRAY) != 0) {
                        int[] perm = ArraySortHelper.getSortedPermutation(
                            m_values, valueComparer, (flags & UNIQUESORT) != 0, sortElementCount);

                        if (perm != null) {
                            success = true;
                            returnArray = fromSpan<int>(perm);
                        }
                    }
                    else {
                        success = ArraySortHelper.sort(m_values, valueComparer, (flags & UNIQUESORT) != 0, sortElementCount);
                    }
                }
                finally {
                    valueComparer.baseComparer = null;
                }
            }
            else {
                flags = (int)arg1;

                var values = new ReadOnlySpan<Value>(m_values, 0, sortElementCount);
                bool uniqueSort = (flags & UNIQUESORT) != 0;
                bool returnIndexedArray = (flags & RETURNINDEXEDARRAY) != 0;

                if ((flags & NUMERIC) != 0) {
                    double[] keys = new double[values.Length];
                    IComparer<double> cmp = GenericComparer<double>.defaultComparer;

                    for (int i = 0; i < values.Length; i++)
                        keys[i] = (double)values[i].toAny();

                    success = _internalSortWithKeys(keys, cmp, uniqueSort, returnIndexedArray, out returnArray);
                }
                else {
                    IComparer<string> cmp = GenericComparer<string>.getComparer(
                        ((flags & CASEINSENSITIVE) != 0) ? GenericComparerType.STRING_IGNORECASE : GenericComparerType.DEFAULT
                    );

                    string[] keys = new string[values.Length];
                    for (int i = 0; i < values.Length; i++)
                        keys[i] = ASAny.AS_convertString(values[i].toAny());

                    success = _internalSortWithKeys(keys, cmp, uniqueSort, returnIndexedArray, out returnArray);
                }
            }

            if (!success)
                return 0;

            returnArray = returnArray ?? this;

            if ((flags & DESCENDING) != 0)
                Array.Reverse(returnArray.m_values, 0, returnArray.m_totalCount);

            return returnArray;
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
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual ASAny sortOn(
            ASAny names,
            [ParamDefaultValue(0)] ASAny options,
            RestParam reserved = default)
        {
            QName[] propNames;
            int[] propFlags;

            if (names.value is ASArray namesArray) {
                if (namesArray.length == 0)
                    return this;

                propNames = new QName[(int)Math.Min(namesArray.length, (uint)Int32.MaxValue)];

                for (int i = 0; i < propNames.Length; i++)
                    propNames[i] = QName.publicName((string)namesArray[(uint)i]);

                propFlags = new int[propNames.Length];

                if (options.value is ASArray optionsArray) {
                    // The options array must only be used if its length is the same as that of
                    // the names array. Otherwise all options are zero.
                    if ((int)optionsArray.length == propNames.Length) {
                        for (int i = 0; i < propFlags.Length; i++)
                            propFlags[i] = (int)namesArray[(uint)i];
                    }
                }
                else {
                    int optionsValue = (int)options;
                    for (int i = 0; i < propFlags.Length; i++)
                        propFlags[i] = optionsValue;
                }
            }
            else {
                propNames = new[] {QName.publicName((string)names.value)};
                propFlags = new[] {(int)options};
            }

            _prepareArrayForSorting(out int sortElementCount);

            ASArray returnArray;
            bool success;
            bool reverse;

            if (propNames.Length == 1) {
                // Special case for single name.
                QName propName = propNames[0];

                var values = new ReadOnlySpan<Value>(m_values, 0, sortElementCount);
                bool uniqueSort = (propFlags[0] & UNIQUESORT) != 0;
                bool returnIndexedArray = (propFlags[0] & RETURNINDEXEDARRAY) != 0;
                reverse = (propFlags[0] & DESCENDING) != 0;

                if ((propFlags[0] & NUMERIC) != 0) {
                    double[] keys = new double[values.Length];

                    for (int i = 0; i < values.Length; i++)
                        keys[i] = (double)values[i].toAny().AS_getProperty(propName);

                    IComparer<double> cmp = GenericComparer<double>.defaultComparer;
                    success = _internalSortWithKeys(keys, cmp, uniqueSort, returnIndexedArray, out returnArray);
                }
                else {
                    string[] keys = new string[values.Length];

                    for (int i = 0; i < values.Length; i++)
                        keys[i] = ASAny.AS_convertString(values[i].toAny().AS_getProperty(propName));

                    IComparer<string> cmp = GenericComparer<string>.getComparer(
                        ((propFlags[0] & CASEINSENSITIVE) != 0)
                            ? GenericComparerType.STRING_IGNORECASE
                            : GenericComparerType.DEFAULT
                    );

                    success = _internalSortWithKeys(keys, cmp, uniqueSort, returnIndexedArray, out returnArray);
                }
            }
            else {
                // More than one name.

                // In this case UNIQUESORT and RETURNINDEXEDARRAY are taken from the first element
                // in the flags array.
                bool uniqueSort = (propFlags[0] & UNIQUESORT) != 0;
                bool returnIndexedArray = (propFlags[0] & RETURNINDEXEDARRAY) != 0;

                _internalCreateSortOnKeysAndComparer(
                    propNames, propFlags, sortElementCount, out int[] keys, out IComparer<int> comparer, out reverse);

                success = _internalSortWithKeys(keys, comparer, uniqueSort, returnIndexedArray, out returnArray);
            }

            if (!success)
                return 0;

            if (reverse)
                Array.Reverse(m_values, 0, m_totalCount);

            return returnArray;
        }

        private void _prepareArrayForSorting(out int sortElementCount) {
            // Partition the array so that all values that are not empty or undefined
            // come first, followed by all undefined values and then all empty values.
            // Undefined and empty values should not be involved in sorting comparisons.

            Span<Value> values = m_values.AsSpan(0, m_totalCount);

            int i, j;

            // First partition into (defined, undefOrEmpty)
            for (i = 0, j = 0; i < values.Length; i++) {
                ref Value vali = ref values[i];
                if (vali.isEmptyOrUndefined)
                    continue;

                if (i != j) {
                    ref Value valj = ref values[j];
                    (vali, valj) = (valj, vali);
                }
                j++;
            }

            sortElementCount = j;
            Span<Value> undefOrEmpty = values.Slice(j);

            // Partition undefOrEmpty into (undefined, empty)
            for (i = 0, j = 0; i < undefOrEmpty.Length; i++) {
                ref Value vali = ref undefOrEmpty[i];
                if (vali.isEmpty)
                    continue;

                if (i != j) {
                    ref Value valj = ref values[j];
                    (vali, valj) = (valj, vali);
                }
                j++;
            }

            m_totalCount = m_nonEmptyCount;
            m_hashLinks = null;
            m_hashEmptyChainHead = -1;
        }

        /// <summary>
        /// Sorts the array using the given keys.
        /// </summary>
        /// <param name="keys">The keys to be used for sorting the array. The size of the keys array
        /// must be equal to the size of this array being sorted.</param>
        /// <param name="comparer">The comparer to use for sorting.</param>
        /// <param name="uniqueSort">If this is true, and two keys compare as equal, the array is
        /// restored to its original order and this method returns false.</param>
        /// <param name="returnIndexedArray">If this is true, create a permutation array containing
        /// the indices of the elements in sorted order and do not mutate this array.</param>
        /// <param name="result">If <paramref name="returnIndexedArray"/> is true, this argument will
        /// be set to the created permutation array, otherwise it will be set to this array.</param>
        /// <returns>If <paramref name="uniqueSort"/> is true and two keys compare as equal, returns
        /// false. Otherwise returns true.</returns>
        private bool _internalSortWithKeys<T>(
            T[] keys, IComparer<T> comparer, bool uniqueSort, bool returnIndexedArray, out ASArray result)
        {
            result = this;

            if (returnIndexedArray) {
                int[] permutation = ArraySortHelper.getSortedPermutation(keys, comparer, uniqueSort, m_totalCount);
                if (permutation == null)
                    return false;

                result = fromSpan<int>(permutation);
                return true;
            }
            else {
                return ArraySortHelper.sortPair(keys, m_values, comparer, uniqueSort, keys.Length);
            }
        }

        /// <summary>
        /// Creates the sorting keys and comparer for the sortOn method.
        /// </summary>
        /// <param name="propNames">The names of the properties to be used for sorting the array.</param>
        /// <param name="propFlags">The sorting flags for each property.</param>
        /// <param name="elementCount">The number of elements to be sorted.</param>
        /// <param name="keys">The sorting keys.</param>
        /// <param name="comparer">The sorting comparer.</param>
        /// <param name="reverse">If this is set to true, the array must be reversed after sorting.</param>
        private void _internalCreateSortOnKeysAndComparer(
            QName[] propNames, int[] propFlags, int elementCount, out int[] keys, out IComparer<int> comparer, out bool reverse)
        {
            int nameCount = propNames.Length;

            keys = new int[elementCount];
            for (int i = 0, curKey = 0; i < keys.Length; i++, curKey = checked(curKey + nameCount))
                keys[i] = curKey;

            ReadOnlySpan<Value> values = new ReadOnlySpan<Value>(m_values, 0, elementCount);

            bool hasCommonFlags = true;
            int commonFlags = propFlags[0] & (NUMERIC | CASEINSENSITIVE | DESCENDING);

            for (int i = 1; i < propFlags.Length && hasCommonFlags; i++)
                hasCommonFlags &= (propFlags[i] & (NUMERIC | CASEINSENSITIVE | DESCENDING)) == commonFlags;

            if (hasCommonFlags) {
                // If the numeric, caseinsensitive and descending flags are the same for all
                // properties then the type-converted property values can be precomputed.

                if ((commonFlags & NUMERIC) != 0) {
                    double[] propValues = new double[m_totalCount * nameCount];

                    for (int i = 0, propIndex = 0; i < values.Length; i++) {
                        ASAny obj = values[i].toAny();
                        for (int j = 0; j < nameCount; j++)
                            propValues[propIndex++] = (double)obj.AS_getProperty(propNames[j]);
                    }

                    comparer = new ArraySortHelper.NumericBlockComparer(propValues, nameCount);
                }
                else {
                    string[] propValues = new string[m_totalCount * nameCount];

                    for (int i = 0, propIndex = 0; i < values.Length; i++) {
                        ASAny obj = m_values[i].toAny();
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

                ASAny[] propValues = new ASAny[m_totalCount * nameCount];

                for (int i = 0, propIndex = 0; i < values.Length; i++) {
                    ASAny obj = m_values[i].toAny();
                    for (int j = 0; j < nameCount; j++)
                        propValues[propIndex++] = obj.AS_getProperty(propNames[j]);
                }

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

                comparer = new ArraySortHelper.GenericBlockComparer<ASAny>(propValues, nameCount, propComparers, descendingFlags);

                // The comparer takes the descending flags into account here, so no reversing after sorting.
                reverse = false;
            }
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
        /// <item>If no arguments are passed, the array is not modified and an empty array is
        /// returned.</item>
        /// <item>
        /// The first argument, <c>startIndex</c>, is the index from which elements should be
        /// removed and included in the returned array. If this is negative, the length of the Array
        /// is added to it; if it is still negative after adding the length, it is set to zero. If
        /// this is greater than the length of the array, it is set to the length of the array.
        /// </item>
        /// <item>The second argument, <c>deleteCount</c>, is the number of elements that must be
        /// removed. If this is negative, it is set to zero; if <c>startIndex + deleteCount</c> is
        /// greater than the array's length, or only one argument is passed, this is set to
        /// <c>length - startIndex</c>.</item>
        /// <item>If there are any passed arguments following the second, they will be considered as a
        /// list of values to be inserted into the array at <c>startIndex</c> after removing
        /// <c>deleteCount</c> elements.</item>
        /// </list>
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual ASArray splice(RestParam args = default) {
            if (args.length == 0)
                return new ASArray();

            uint startIndex, deleteCount, endIndex;

            startIndex = _normalizeIndex((double)args[0], m_length);

            if (args.length == 1)
                deleteCount = m_length - startIndex;
            else
                deleteCount = (uint)Math.Min((double)(m_length - startIndex), Math.Max((double)args[1], 0.0));

            deleteCount = Math.Min(deleteCount, (uint)Int32.MaxValue);

            endIndex = startIndex + deleteCount;

            int nExtraArgs = Math.Max(args.length - 2, 0);
            if (nExtraArgs > deleteCount)
                nExtraArgs = (int)Math.Min((uint)nExtraArgs, UInt32.MaxValue - m_length + deleteCount);

            ReadOnlySpan<ASAny> newValues = args.getSpan().Slice(2, nExtraArgs);
            ASArray spliceArray;

            if (_isDenseArray() && deleteCount == nExtraArgs && endIndex <= (uint)m_values.Length) {
                // Special case: Dense array with delete count same as replacement count.
                // No shifting in this case.

                spliceArray = new ASArray((int)deleteCount);
                Span<Value> values = m_values.AsSpan((int)startIndex, (int)deleteCount);
                Span<Value> spliceValues = spliceArray.m_values.AsSpan(0, (int)deleteCount);

                for (int i = 0; i < values.Length; i++) {
                    ref Value val = ref values[i];
                    if (val.isEmpty) {
                        spliceValues[i] = Value.fromAny(AS_getElement((uint)i));
                        m_nonEmptyCount++;
                    }
                    else {
                        spliceValues[i] = val;
                    }
                    val = Value.fromAny(newValues[i]);
                }

                m_totalCount = Math.Max(m_totalCount, (int)endIndex);
                m_length = Math.Max(m_length, endIndex);

                spliceArray.m_nonEmptyCount = (int)deleteCount;
                spliceArray.m_totalCount = (int)deleteCount;
            }
            else {
                spliceArray = _internalSlice(startIndex, (int)deleteCount);
                _internalSpliceReplaceRange(startIndex, (int)deleteCount, newValues);
            }

            return spliceArray;
        }

        private void _internalSpliceReplaceRange(uint startIndex, int deleteCount, ReadOnlySpan<ASAny> newValues) {
            int sizeDelta = newValues.Length - deleteCount;
            uint delEndIndex = startIndex + (uint)deleteCount;
            uint newArrLength = m_length + (uint)sizeDelta;
            bool denseConvertedToHash = false;

            if (_isDenseArray()) {
                // Empty out all deleted elements and update the nonEmptyCount.
                if (startIndex <= (uint)m_totalCount) {
                    Span<Value> values = m_values.AsSpan((int)startIndex, Math.Min(deleteCount, m_totalCount - (int)startIndex));
                    for (int i = 0; i < values.Length; i++) {
                        if (!values[i].isEmpty) {
                            values[i] = Value.empty;
                            m_nonEmptyCount--;
                        }
                    }
                }

                if (deleteCount > newValues.Length && !_canUseDenseArray(m_nonEmptyCount + newValues.Length, newArrLength)) {
                    // The spliced array should use hash table storage.
                    denseConvertedToHash = true;
                    _denseArrayToHash();
                }
            }

            if (_isDenseArray()) {
                Value[] values = m_values;

                if (newArrLength <= (uint)values.Length && newArrLength >= (uint)(values.Length >> 2)) {
                    // Shift the elements after the deleted range to their correct position in place.
                    uint shiftBegin = startIndex + (uint)deleteCount;

                    if (shiftBegin < (uint)m_totalCount) {
                        values.AsSpan((int)shiftBegin, m_totalCount - (int)shiftBegin)
                            .CopyTo(values.AsSpan((int)shiftBegin + sizeDelta));

                        if (sizeDelta < 0)
                            values.AsSpan(m_totalCount + sizeDelta, -sizeDelta).Clear();
                    }
                }
                else {
                    // Allocate a new array and copy elements, shifting those after the deleted range
                    // to their correct position.
                    int newDenseArrayLen = (newArrLength > (uint)values.Length)
                        ? Math.Max((int)newArrLength, values.Length * 2)
                        : Math.Max((int)newArrLength, values.Length >> 1);

                    var newArrayValues = new Value[newDenseArrayLen];
                    values.AsSpan(0, (int)Math.Min(startIndex, (uint)m_totalCount)).CopyTo(newArrayValues);

                    uint shiftBegin = startIndex + (uint)deleteCount;
                    if (shiftBegin < (uint)m_totalCount) {
                        values.AsSpan((int)shiftBegin, m_totalCount - (int)shiftBegin)
                            .CopyTo(newArrayValues.AsSpan((int)shiftBegin + sizeDelta));
                    }

                    m_values = newArrayValues;
                }

                m_totalCount = (int)newArrLength;
            }
            else {
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
                        key += (uint)sizeDelta;
                    }
                }

                if (!denseConvertedToHash && _canUseDenseArray(m_nonEmptyCount + newValues.Length, newArrLength)) {
                    // Converting to dense array doesn't depend on chain correctness, so safe to call here.
                    _hashToDenseArray((int)newArrLength);
                }
                else {
                    _resetHashTableChains();
                }
            }

            if (_isDenseArray()) {
                var values = m_values.AsSpan((int)startIndex, newValues.Length);
                for (int i = 0; i < newValues.Length; i++)
                    values[i] = Value.fromAny(newValues[i]);

                _setDenseArrayTotalCount((int)newArrLength);
                m_nonEmptyCount += newValues.Length;
            }
            else {
                for (int i = 0; i < newValues.Length; i++)
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
            int argCount = (int)Math.Min((uint)args.length, UInt32.MaxValue - m_length);

            if (argCount == 0)
                return m_length;

            uint newArrLength = m_length + (uint)argCount;
            bool denseConvertedToHash = false;

            if (_isDenseArray() && m_length > (uint)m_totalCount
                && !_canUseDenseArray(m_nonEmptyCount + argCount, newArrLength))
            {
                denseConvertedToHash = true;
                _denseArrayToHash();
            }

            if (_isDenseArray()) {
                if (m_totalCount + argCount > m_values.Length) {
                    Value[] newValues = new Value[Math.Max(m_totalCount + argCount, m_values.Length * 2)];
                    m_values.AsSpan(0, m_totalCount).CopyTo(newValues.AsSpan(argCount));
                    m_values = newValues;
                }
                else {
                    m_values.AsSpan(0, m_totalCount).CopyTo(m_values.AsSpan(argCount));
                }
                m_totalCount += argCount;
            }
            else {
                var values = new ReadOnlySpan<Value>(m_values, 0, m_totalCount);
                HashLink[] hashLinks = m_hashLinks;

                // Shift hash keys and then rebuild chains.
                for (int i = 0; i < values.Length; i++) {
                    if (!values[i].isEmpty)
                        hashLinks[i].key += (uint)argCount;
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
        [AVM2ExportPrototypeMethod]
        public override bool propertyIsEnumerable(ASAny name = default(ASAny)) {
            if (ASObject.AS_isUint(name.value) && !_getValueAt((uint)name).isEmpty)
                return true;

            string str = (string)name;
            if (NumberFormatHelper.parseArrayIndex(str, false, out uint index) && !_getValueAt(index).isEmpty)
                return true;

            return base.propertyIsEnumerable(name);
        }

        /// <exclude/>
        ///
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABCIL compiler to invoke the ActionScript Array class constructor. This must not be called
        /// by outside .NET code. Array objects constructed from .NET code must use the constructor
        /// defined on the <see cref="ASArray"/> type.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) => new ASArray(new RestParam(args));

    }

}
