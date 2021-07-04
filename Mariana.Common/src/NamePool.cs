using System;

namespace Mariana.Common {

    /// <summary>
    /// A string pool that is used to ensure that equal strings reference the same string in
    /// memory. A common use of this is by parsers of text formats (such as JSON and XML).
    /// </summary>
    public sealed class NamePool {

        private struct Slot {
            internal string value;
            internal int hash;
            internal int next;
        }

        private const int DEFAULT_INITIAL_CAPACITY = 8;

        private int m_count;
        private Slot[] m_slots;
        private int[] m_chains;

        /// <summary>
        /// Creates a new instance of <see cref="NamePool"/>.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the name pool.</param>
        public NamePool(int initialCapacity = DEFAULT_INITIAL_CAPACITY) {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            m_slots = new Slot[DataStructureUtil.nextPrime(initialCapacity)];
            m_chains = new int[m_slots.Length];
            m_chains.AsSpan().Fill(-1);
        }

        /// <summary>
        /// Returns the string in the name pool having the same sequence of characters as the given span.
        /// If no such string exists, a new one is created from the span and added to the pool.
        /// </summary>
        ///
        /// <param name="span">A span to compare against the strings in the pool.</param>
        /// <returns>The string in the name pool equivalent to the span <paramref name="span"/>.</returns>
        public string getPooledValue(ReadOnlySpan<char> span) {
            int hash = _getHashCodeOfSpan(span) & 0x7FFFFFFF;
            int chain = hash % m_chains.Length;
            int i = m_chains[chain];

            while (i != -1) {
                ref Slot slot = ref m_slots[i];
                if (slot.hash == hash && span.Equals(slot.value, StringComparison.Ordinal))
                    return slot.value;

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
            newSlot.value = new string(span);
            newSlot.next = chainStart;
            chainStart = m_count;

            m_count++;
            return newSlot.value;
        }

        private static int _getHashCodeOfSpan(ReadOnlySpan<char> span) {
            int value = 1362874787;
            for (int i = 0; i < span.Length; i++)
                value = (value + span[i]) * 464739103;

            return value;
        }

    }

}
