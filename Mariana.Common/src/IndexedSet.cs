using System;

namespace Mariana.Common {

    /// <summary>
    /// A hashtable based set that supports array-like indexing.
    /// </summary>
    /// <typeparam name="T">The element type of the set. This must implement the
    /// <see cref="IEquatable{T}"/> interface.</typeparam>
    ///
    /// <remarks>
    /// Elements once added to the set cannot be removed (except by clearing the entire set
    /// using the <see cref="clear"/> method).
    /// </remarks>
    public sealed class IndexedSet<T> where T : IEquatable<T> {

        private struct Slot {
            internal T value;
            internal int hash;
            internal int next;
        }

        private const int DEFAULT_INITIAL_CAPACITY = 4;

        private const int HASH_CODE_MASK = 0x7FFFFFFF;

        private int m_count;
        private Slot[] m_slots;
        private int[] m_chains;

        /// <summary>
        /// Creates a new instance of <see cref="IndexedSet{T}"/>.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the set.</param>
        public IndexedSet(int initialCapacity = DEFAULT_INITIAL_CAPACITY) {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            m_slots = new Slot[DataStructureUtil.nextPrime(initialCapacity)];
            m_chains = new int[m_slots.Length];
            m_chains.AsSpan().Fill(-1);
        }

        /// <summary>
        /// Gets a read-only reference to the element at the given index in the set.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <exception cref="ArgumentOutOfRangeException">The index is out of bounds.</exception>
        /// <remarks>
        /// The reference returned is guaranteed to be valid until a new element is added to this
        /// <see cref="IndexedSet{T}"/>, or the <see cref="clear"/> method is called.
        /// </remarks>
        public ref readonly T this[int index] {
            get {
                if ((uint)index >= (uint)m_count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return ref m_slots[index].value;
            }
        }

        /// <summary>
        /// Gets the number of elements in the set.
        /// </summary>
        public int count => m_count;

        /// <summary>
        /// Adds an element to the set.
        /// </summary>
        /// <param name="o">The element to add to the set.</param>
        /// <returns>False if the element already exists, true otherwise.</returns>
        public bool add(T o) {
            int lastIndex = m_count;
            return findOrAdd(o) == lastIndex;
        }

        /// <summary>
        /// Searches for the given element in the set, adding it if it does not exist, and returns the
        /// index of the element.
        /// </summary>
        /// <param name="obj">The element to find or add to the set.</param>
        /// <returns>The index of the element in the set.</returns>
        public int findOrAdd(T obj) {
            int hash = obj.GetHashCode() & HASH_CODE_MASK;
            int chain = hash % m_chains.Length;
            int i = m_chains[chain];

            while (i != -1) {
                ref Slot slot = ref m_slots[i];
                if (slot.hash == hash && slot.value.Equals(obj))
                    return i;
                i = slot.next;
            }

            if (m_count == m_slots.Length) {
                int newSize = DataStructureUtil.nextPrime(m_count * 2);

                Slot[] newSlots = new Slot[newSize];
                m_slots.AsSpan(0, m_count).CopyTo(newSlots);

                int[] newChains = new int[newSize];
                newChains.AsSpan().Fill(-1);

                for (int j = 0; j < m_count; j++) {
                    int newChain = newSlots[j].hash % newSize;
                    newSlots[j].next = newChains[newChain];
                    newChains[newChain] = j;
                }

                m_slots = newSlots;
                m_chains = newChains;
                chain = hash % newSize;
            }

            ref Slot newSlot = ref m_slots[m_count];
            ref int chainStart = ref m_chains[chain];

            newSlot.hash = hash;
            newSlot.value = obj;
            newSlot.next = chainStart;
            chainStart = m_count;

            return m_count++;
        }

        /// <summary>
        /// Searches for the given element in the set and returns its index.
        /// </summary>
        /// <param name="o">The element to find in the set.</param>
        /// <returns>The index of the element in the set, or -1 if it does not exist.</returns>
        public int find(T o) {
            int hash = o.GetHashCode() & HASH_CODE_MASK;
            int i = m_chains[hash % m_chains.Length];

            while (i != -1) {
                ref Slot slot = ref m_slots[i];
                if (slot.hash == hash && slot.value.Equals(o))
                    return i;
                i = slot.next;
            }

            return -1;
        }

        /// <summary>
        /// Copies the elements from the set to a span. The elements will be written in the same order as
        /// their indices in the set.
        /// </summary>
        /// <param name="span">The span to which to copy the set elements to. The length of this span
        /// must be equal to the number of elements in the set.</param>
        /// <exception cref="ArgumentException">The length of <paramref name="span"/> is not equal to
        /// the number of elements in this set.</exception>
        public void copyTo(Span<T> span) {
            if (span.Length != m_count)
                throw new ArgumentException("Length of the span must be the same as the number of items in the set.", nameof(span));

            Span<Slot> slots = m_slots.AsSpan(0, m_count);
            for (int i = 0; i < slots.Length; i++)
                span[i] = slots[i].value;
        }

        /// <summary>
        /// Converts the set to an array. The elements in the array will be in the same order as
        /// their indices in the set.
        /// </summary>
        /// <returns>The array.</returns>
        public T[] toArray() {
            T[] arr = new T[m_count];
            copyTo(arr);
            return arr;
        }

        /// <summary>
        /// Removes all the items from the set.
        /// </summary>
        public void clear() {
            m_chains.AsSpan().Fill(-1);
            m_slots.AsSpan(0, m_count).Clear();
            m_count = 0;
        }

    }

}
