using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// An internal integer set data structure used by the compiler that uses pooled arrays.
    /// </summary>
    internal struct PooledIntegerSet {

        private const int EMPTY_SLOT = Int32.MinValue;

        private DynamicArrayPool<int> m_pool;
        private DynamicArrayPoolToken<int> m_token;
        private int m_count;

        /// <summary>
        /// Creates a new instance of <see cref="PooledIntegerSet"/>.
        /// </summary>
        /// <param name="pool">A <see cref="DynamicArrayPool{Int32}"/> instance from which the memory
        /// for this set should be allocated.</param>
        /// <param name="capacity">The initial capacity to allocate for this set.</param>
        public PooledIntegerSet(DynamicArrayPool<int> pool, int capacity = 0) {
            m_pool = pool;
            m_token = pool.allocate((capacity > 0) ? DataStructureUtil.nextPowerOf2(capacity - 1) : 4);
            m_pool.getSpan(m_token).Fill(EMPTY_SLOT);
            m_count = 0;
        }

        /// <summary>
        /// Returns the number of elements in the set.
        /// </summary>
        public readonly int count => m_count;

        /// <summary>
        /// Releases any memory being used by the set back to its array pool. Once this method
        /// is called the set can no longer be used.
        /// </summary>
        /// <remarks>
        /// This method has no effect when called on the default value of <see cref="PooledIntegerSet"/>.
        /// </remarks>
        public void free() {
            if (m_pool != null) {
                m_pool.free(m_token);
                m_token = default;
            }
        }

        /// <summary>
        /// Removes all existing elements from the set.
        /// </summary>
        public void clear() {
            m_pool.getSpan(m_token).Fill(EMPTY_SLOT);
            m_count = 0;
        }

        /// <summary>
        /// Adds a new element to the set.
        /// </summary>
        /// <param name="value">The value to add. This must not be equal to the minimum
        /// value of the <see cref="Int32"/> type.</param>
        /// <returns>True if the value was added, false if it already exists.</returns>
        public bool add(int value) {
            _ensureCapacityFor(m_count + 1);

            bool added = _putValueInSlot(value, m_pool.getSpan(m_token));
            if (added)
                m_count++;
            return added;
        }

        /// <summary>
        /// Adds elements from the given span to the set.
        /// </summary>
        /// <param name="values">A span containing values to add. This must not contain an element whose
        /// value is the minimum value of the <see cref="Int32"/> type.</param>
        public void add(ReadOnlySpan<int> values) {
            if (values.Length == 0)
                return;

            _ensureCapacityFor(m_count + values.Length);
            Span<int> slots = m_pool.getSpan(m_token);
            int addedCount = 0;

            for (int i = 0; i < values.Length; i++) {
                bool added = _putValueInSlot(values[i], slots);
                if (added)
                    addedCount++;
            }

            m_count += addedCount;
        }

        /// <summary>
        /// Adds elements from the given set to the current set.
        /// </summary>
        /// <param name="otherSet">A <see cref="PooledIntegerSet"/> containing values to add.</param>
        public void add(in PooledIntegerSet otherSet) {
            if (otherSet.m_count == 0)
                return;

            _ensureCapacityFor(m_count + otherSet.m_count);

            Span<int> slots = m_pool.getSpan(m_token);
            Span<int> otherSlots = m_pool.getSpan(otherSet.m_token);
            int addedCount = 0;

            for (int i = 0; i < otherSlots.Length; i++) {
                int v = otherSlots[i];
                if (v == EMPTY_SLOT)
                    continue;

                bool added = _putValueInSlot(v, slots);
                if (added)
                    addedCount++;
            }

            m_count += addedCount;
        }

        /// <summary>
        /// Returns a value indicating whether the given value exists in the set.
        /// </summary>
        /// <param name="value">The value to check for existence.</param>
        /// <returns>True if <paramref name="value"/> exists in this set, otherwise false.</returns>
        public readonly bool contains(int value) {
            Debug.Assert(value != EMPTY_SLOT);

            Span<int> slots = m_pool.getSpan(m_token);
            int hashMask = slots.Length - 1;
            int hash = value & hashMask;

            for (int i = 0; i <= hashMask; i++) {
                int slotValue = slots[hash];

                if (slotValue == value)
                    return true;

                if (slotValue == EMPTY_SLOT)
                    return false;

                hash = (hash + i + 1) & hashMask;
            }

            return false;
        }

        /// <summary>
        /// Writes the values in this set to the given span.
        /// </summary>
        /// <param name="values">A span into which to write the values.</param>
        public readonly void writeValues(Span<int> values) {
            Span<int> slots = m_pool.getSpan(m_token);

            int valCount = 0;
            for (int i = 0; i < slots.Length; i++) {
                int v = slots[i];
                if (v != EMPTY_SLOT) {
                    values[valCount] = v;
                    valCount++;
                }
            }

            Debug.Assert(valCount == m_count);
        }

        /// <summary>
        /// Returns an array containing the values in this set.
        /// </summary>
        /// <returns>An array containing the values in this set.</returns>
        public readonly int[] toArray() {
            var arr = new int[count];
            writeValues(arr);
            return arr;
        }

        /// <summary>
        /// Returns a string representation of this set containing its elements.
        /// </summary>
        /// <returns>A string representation of this set.</returns>
        public readonly override string ToString() {
            var sb = new StringBuilder();
            sb.Append('{');

            Span<int> slots = m_pool.getSpan(m_token);
            for (int i = 0; i < slots.Length; i++) {
                if (slots[i] == EMPTY_SLOT)
                    continue;

                if (sb.Length > 1)
                    sb.Append(',').Append(' ');
                sb.Append(slots[i].ToString(CultureInfo.InvariantCulture));
            }

            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Returns an enumerator that can be used to enumerate the elements of this set using
        /// a foreach loop.
        /// </summary>
        /// <returns>An enumerator instance.</returns>
        public readonly Enumerator GetEnumerator() => new Enumerator(this);

        private static bool _putValueInSlot(int value, Span<int> slots) {
            Debug.Assert(value != EMPTY_SLOT);

            int hashMask = slots.Length - 1;
            int hash = value & hashMask;

            for (int i = 0; i <= hashMask; i++) {
                int slotValue = slots[hash];

                if (slotValue == value)
                    return false;

                if (slotValue == EMPTY_SLOT) {
                    slots[hash] = value;
                    return true;
                }

                hash = (hash + i + 1) & hashMask;
            }

            Debug.Assert(false);    // Should never reach here.
            return false;
        }

        private void _ensureCapacityFor(int expectedCount) {
            int currentCapacity = m_pool.getLength(m_token);

            // Limit load factor to 0.75
            int expectedCapacity = expectedCount + (expectedCount >> 1);
            if (expectedCapacity <= currentCapacity)
                return;

            var oldToken = m_token;
            m_token = m_pool.allocate(DataStructureUtil.nextPowerOf2(expectedCapacity), out Span<int> newSlots);
            newSlots.Fill(EMPTY_SLOT);

            var oldSlots = m_pool.getSpan(oldToken);
            for (int i = 0; i < oldSlots.Length; i++) {
                int v = oldSlots[i];
                if (v != EMPTY_SLOT)
                    _putValueInSlot(v, newSlots);
            }

            m_pool.free(oldToken);
        }

        public ref struct Enumerator {
            private Span<int> m_slots;
            private int m_current;

            internal Enumerator(in PooledIntegerSet set) {
                m_slots = set.m_pool.getSpan(set.m_token);
                m_current = -1;
            }

            public bool MoveNext() {
                while (m_current < m_slots.Length - 1) {
                    m_current++;
                    if (m_slots[m_current] != EMPTY_SLOT)
                        return true;
                }
                return false;
            }

            public int Current => m_slots[m_current];
        }

    }

}
