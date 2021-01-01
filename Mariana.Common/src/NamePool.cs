using System;

namespace Mariana.Common {

    /// <summary>
    /// A string pool that is used to ensure that equal strings reference the same string in
    /// memory. A common use of this is by parsers of text formats (such as JSON and XML).
    /// </summary>
    public sealed class NamePool {

        private struct Slot {
            internal string value;
            internal int next;
        }

        // This array consists of 53 chain start points. Chains are based on the first
        // character of each string: chains 0 to 25 are used for uppercase letters,
        // 26 to 51 are used for lowercase letters and path 52 is used
        // for strings starting with any character other than a letter.
        private int[] m_chains;

        private Slot[] m_slots;

        private int m_count;

        /// <summary>
        /// Creates a new NamePool instance.
        /// </summary>
        public NamePool() {
            m_slots = new Slot[8];
            m_chains = new int[53];
            Array.Fill(m_chains, -1);
        }

        private int _getChain(char c) {
            if (c >= 97 && c <= 122)
                return c - 71;
            if (c >= 65 && c <= 90)
                return c - 65;
            return 52;
        }

        /// <summary>
        /// Returns the string in the name pool having the same sequence of characters as the given string.
        /// If no such string exists, it is added to the pool.
        /// </summary>
        ///
        /// <param name="str">A string to compare against the strings in the pool.</param>
        /// <returns>The string in the name pool equivalent to the span <paramref name="str"/>.</returns>
        public string getPooledValue(string str) => _getPooledValueInternal(str, str);


        /// <summary>
        /// Returns the string in the name pool having the same sequence of characters as the given span.
        /// If no such string exists, a new one is created from the span and added to the pool.
        /// </summary>
        ///
        /// <param name="str">A span to compare against the strings in the pool.</param>
        /// <returns>The string in the name pool equivalent to the span <paramref name="str"/>.</returns>
        public string getPooledValue(ReadOnlySpan<char> str) => _getPooledValueInternal(str, null);

        private string _getPooledValueInternal(ReadOnlySpan<char> span, string originalString) {
            if (span.Length == 0)
                return "";

            int chain = _getChain(span[0]);

            for (int i = m_chains[chain]; i != -1; i = m_slots[i].next) {
                string slotval = m_slots[i].value;
                if (span.Equals(slotval, StringComparison.Ordinal))
                    return slotval;
            }

            Slot newSlot;
            newSlot.next = m_chains[chain];
            newSlot.value = originalString ?? new String(span);
            m_chains[chain] = m_count;

            if (m_slots.Length == m_count)
                DataStructureUtil.resizeArray(ref m_slots, m_count, m_count + 1, false);

            m_slots[m_count++] = newSlot;
            return newSlot.value;
        }

    }

}
