using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Mariana.Common {

    /// <summary>
    /// A hash set which compares objects by reference instead of using an equality comparer. This
    /// improves performance when using keys which do not need value comparisons as it eliminates
    /// the calls made to the virtual <see cref="Object.GetHashCode" qualifyHint="true"/> and
    /// <see cref="Object.Equals(Object)" qualifyHint="true"/> methods done by the .NET
    /// Framework's HashSet class. This is similar to the
    /// <see cref="ReferenceDictionary{TKey, TValue}"/> class, except that it does not associate
    /// keys with values.
    /// </summary>
    ///
    /// <typeparam name="T">The type of the values in the set. This must be a class (reference)
    /// type.</typeparam>
    public sealed class ReferenceSet<T> : IEnumerable<T> where T : class {

        private struct Slot {
            internal T? value;        // Object key of the slot
            internal int next;       // Next slot in linked list
            internal bool hasValue;  // True if the slot is used, otherwise false
        }

        private const int DEFAULT_INITIAL_CAPACITY = 4;

        private const int HASH_CODE_MASK = 0x7FFFFFFF;

        private int[] m_chains;

        private Slot[] m_slots;

        private int m_count;

        private int m_emptyChain = -1;

        private int m_emptyCount;

        /// <summary>
        /// Creates a new instance of the ReferenceSet class.
        /// </summary>
        /// <param name="initialCapacity">The initial size of the set.</param>
        public ReferenceSet(int initialCapacity = DEFAULT_INITIAL_CAPACITY) {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            m_slots = new Slot[DataStructureUtil.nextPrime(initialCapacity)];
            m_chains = new int[m_slots.Length];
            m_chains.AsSpan().Fill(-1);
        }

        /// <summary>
        /// Returns the number of elements in the set.
        /// </summary>
        public int count => m_count - m_emptyCount;

        /// <summary>
        /// Returns true if the set contains the given item, otherwise returns false.
        /// </summary>
        /// <param name="item">The item to find in the set.</param>
        /// <returns>True if the set contains the given item, false otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
        public bool find(T item) {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            int chain = (RuntimeHelpers.GetHashCode(item) & HASH_CODE_MASK) % m_chains.Length;
            int i = m_chains[chain];

            while (i != -1) {
                ref Slot slot = ref m_slots[i];
                if (slot.value == item)
                    return true;
                i = slot.next;
            }

            return false;
        }

        /// <summary>
        /// Adds a new item to the set.
        /// </summary>
        /// <param name="item">The item to add to the set. This must be a non-null object.</param>
        /// <returns>True if the entry was added; false if the item already exists in the set (or is
        /// null).</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
        public bool add(T item) {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            int hash = RuntimeHelpers.GetHashCode(item) & HASH_CODE_MASK;
            int chain = hash % m_chains.Length;
            int i = m_chains[chain];

            while (i != -1) {
                ref Slot slot = ref m_slots[i];
                if (slot.value == item)
                    return false;
                i = slot.next;
            }

            int newIndex;
            if (m_emptyCount > 0) {
                newIndex = m_emptyChain;
                m_emptyChain = m_slots[newIndex].next;
                m_emptyCount--;
            }
            else {
                if (m_count == m_slots.Length) {
                    int newSize = DataStructureUtil.nextPrime(checked(m_count * 2));

                    int[] newChains = new int[newSize];
                    newChains.AsSpan().Fill(-1);

                    Slot[] newSlots = new Slot[newSize];
                    m_slots.AsSpan(0, m_count).CopyTo(newSlots);

                    for (int j = 0, n = m_count; j < n; j++) {
                        int newChain = (RuntimeHelpers.GetHashCode(newSlots[j].value) & HASH_CODE_MASK) % newSize;
                        newSlots[j].next = newChains[newChain];
                        newChains[newChain] = j;
                    }

                    m_chains = newChains;
                    m_slots = newSlots;
                    chain = hash % m_chains.Length;
                }
                newIndex = m_count++;
            }

            ref Slot newSlot = ref m_slots[newIndex];
            ref int chainStart = ref m_chains[chain];

            newSlot.next = chainStart;
            newSlot.value = item;
            newSlot.hasValue = true;
            chainStart = newIndex;

            return true;
        }

        /// <summary>
        /// Removes an item from the set.
        /// </summary>
        /// <param name="item">The item to remove from the set.</param>
        /// <returns>True if the item was deleted, false if the item does not exist in the
        /// set.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
        public bool delete(T item) {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            int hash = RuntimeHelpers.GetHashCode(item) & HASH_CODE_MASK;
            int path = hash % m_chains.Length;
            int prev = -1;
            int i = m_chains[path];

            while (i != -1) {
                ref Slot slot = ref m_slots[i];

                if (slot.value == item) {
                    if (prev == -1)
                        m_chains[path] = slot.next;
                    else
                        m_slots[prev].next = slot.next;

                    slot.value = null;
                    slot.next = m_emptyChain;
                    slot.hasValue = false;
                    m_emptyChain = i;
                    m_emptyCount++;
                    return true;
                }

                prev = i;
                i = slot.next;
            }

            return false;
        }

        /// <summary>
        /// Removes all items from the set.
        /// </summary>
        public void clear() {
            if (m_count == 0)
                return;

            m_slots.AsSpan(0, m_count).Clear();
            m_chains.AsSpan().Fill(-1);
            m_count = 0;
            m_emptyChain = -1;
            m_emptyCount = 0;
        }

        /// <summary>
        /// Sets the current set to the result of the union of it with another set. This method adds
        /// all elements in <paramref name="otherSet"/> that do not already exist in the current
        /// set.
        /// </summary>
        ///
        /// <param name="otherSet">The set to take the union with the current set.</param>
        /// <exception cref="ArgumentNullException"><paramref name="otherSet"/> is null.</exception>
        public void unionWith(ReferenceSet<T> otherSet) {
            if (otherSet == null)
                throw new ArgumentNullException(nameof(otherSet));

            if (otherSet == this)
                return;

            Span<Slot> otherSlots = otherSet.m_slots.AsSpan(0, otherSet.m_count);

            for (int i = 0; i < otherSlots.Length; i++) {
                ref Slot otherSlot = ref otherSlots[i];
                if (otherSlot.hasValue)
                    add(otherSlot.value!);
            }
        }

        /// <summary>
        /// Sets the current set to the result of the intersection of it with another set. This
        /// deletes all elements in the current set that do not exist in
        /// <paramref name="otherSet"/>.
        /// </summary>
        ///
        /// <param name="otherSet">The set to take the intersection with the current set.</param>
        /// <exception cref="ArgumentNullException"><paramref name="otherSet"/> is null.</exception>
        public void intersectWith(ReferenceSet<T> otherSet) {
            if (otherSet == null)
                throw new ArgumentNullException(nameof(otherSet));

            if (otherSet == this)
                return;

            Slot[] slots = m_slots;
            int[] chains = m_chains;

            for (int i = 0; i < chains.Length; i++) {
                int j = chains[i];
                int prev = -1;

                while (j != -1) {
                    ref Slot slot = ref slots[j];

                    int next = slot.next;
                    if (!otherSet.find(slot.value!)) {
                        if (prev == -1)
                            chains[i] = next;
                        else
                            slots[prev].next = next;

                        slot.value = null;
                        slot.hasValue = false;
                        slot.next = m_emptyChain;

                        m_emptyChain = j;
                        m_emptyCount++;
                    }
                    else {
                        prev = j;
                    }

                    j = next;
                }
            }
        }

        /// <summary>
        /// Returns an array containing all the items in the set. The order in which the set elements
        /// appear in the array is undefined.
        /// </summary>
        /// <returns>An array containing all the items in the set.</returns>
        public T[] toArray() {
            var list = new DynamicArray<T>(m_count - m_emptyCount);
            for (int i = 0, n = m_count; i < n; i++) {
                if (m_slots[i].hasValue)
                    list.add(m_slots[i].value!);
            }
            return list.toArray();
        }

        /// <summary>
        /// Gets an enumerator for this <see cref="ReferenceSet{T}"/> instance.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate over the key-value pairs of
        /// this dictionary.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Gets an enumerator for this <see cref="ReferenceSet{T}"/> instance.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate over the key-value pairs of
        /// this dictionary.</returns>
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Gets an enumerator for this <see cref="ReferenceSet{T}"/> instance.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate over the key-value pairs of
        /// this dictionary.</returns>
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// An implementation of <see cref="IEnumerator{T}"/> that can
        /// be used to iterate over the elements of a <see cref="ReferenceSet{T}"/>
        /// </summary>
        public struct Enumerator : IEnumerator<T> {

            private Slot[] m_slots;
            private int m_index;

            internal Enumerator(ReferenceSet<T> set) {
                m_slots = set.m_slots;
                m_index = set.m_count;
            }

            /// <summary>
            /// Moves the iterator to the next position.
            /// </summary>
            /// <returns>False if the end of the iteration has been reached, otherwise true.</returns>
            public bool MoveNext() {
                m_index--;

                while (m_index >= 0 && !m_slots[m_index].hasValue)
                    m_index--;

                return m_index >= 0;
            }

            /// <summary>
            /// Returns the key-value pair at the iterator's current position.
            /// </summary>
            /// <value>The key-value pair of the dictionary at the iterator's current position.</value>
            public T Current => m_slots[m_index].value!;

            /// <exclude/>
            public void Reset() => throw new NotImplementedException();

            /// <exclude/>
            object IEnumerator.Current => Current;

            /// <exclude/>
            public void Dispose() { }

        }

    }
}
