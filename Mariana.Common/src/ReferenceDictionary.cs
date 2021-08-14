using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Mariana.Common {

    /// <summary>
    /// A dictionary that compares class keys by reference instead of using an equality comparer.
    /// This improves performance when using keys that do not need value comparisons as it
    /// eliminates the calls made to the virtual
    /// <see cref="Object.GetHashCode" qualifyHint="true"/> and
    /// <see cref="Object.Equals(Object)" qualifyHint="true"/> methods done by the .NET
    /// Dictionary class.
    /// </summary>
    ///
    /// <typeparam name="TKey">The type of the keys in the dictionary. This must be a class
    /// (reference) type.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    public sealed class ReferenceDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> where TKey : class {

        private struct Slot {
            internal TKey? key;        // Key of the slot
            internal int next;         // Index of the next slot in a linked list
            internal TValue value;     // The value of the slot
            internal bool hasValue;    // True if the slot is used, otherwise false
        }

        /// <summary>
        /// The initial size of all new <see cref="ReferenceDictionary{TKey, TValue}"/> objects
        /// whose initial size is not specified in the constructor.
        /// </summary>
        private const int DEFAULT_INITIAL_CAPACITY = 4;

        private const int HASH_CODE_MASK = 0x7FFFFFFF;

        private int[] m_chains;

        private Slot[] m_slots;

        private int m_count;

        private int m_emptyChain = -1;

        private int m_emptyCount;

        /// <summary>
        /// Creates a new instance of the <see cref="ReferenceDictionary{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the dictionary.</param>
        public ReferenceDictionary(int initialCapacity = DEFAULT_INITIAL_CAPACITY) {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            m_slots = new Slot[DataStructureUtil.nextPrime(initialCapacity)];
            m_chains = new int[m_slots.Length];
            m_chains.AsSpan().Fill(-1);
        }

        /// <summary>
        /// Returns the number of entries in the dictionary.
        /// </summary>
        public int count => m_count - m_emptyCount;

        /// <summary>
        /// Gets or sets the value associated with the given key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <value>The value associated with <paramref name="key"/>.</value>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
        /// <exception cref="ArgumentException">No entry exists with the given key (getter only).</exception>
        public TValue this[TKey key] {
            get => getValueRef(key);
            set => getValueRef(key, true) = value;
        }

        /// <summary>
        /// Attempts to get the value of associated with the given key.
        /// </summary>
        /// <param name="key">The key for which to find the value.</param>
        /// <param name="value">The value of the entry with the given key, or the default value of
        /// the value type if no entry with that key exists.</param>
        /// <returns>True if an entry exists with the given key, false otherwise.</returns>
        public bool tryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            int chain = (RuntimeHelpers.GetHashCode(key) & HASH_CODE_MASK) % m_chains.Length;
            int i = m_chains[chain];

            while (i != -1) {
                ref Slot slot = ref m_slots[i];
                if (slot.key == key) {
                    value = slot.value;
                    return true;
                }
                i = slot.next;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Returns a boolean value indicating whether the given key exists in the dictionary.
        /// </summary>
        ///
        /// <param name="key">The key.</param>
        /// <returns>True if an entry in the dictionary exists whose key is <paramref name="key"/>,
        /// otherwise false.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
        public bool containsKey(TKey key) => tryGetValue(key, out _);

        /// <summary>
        /// Returns the value associated with the given key in the dictionary, or the default
        /// value of <typeparamref name="TValue"/> if no entry with the key exists.
        /// </summary>
        ///
        /// <param name="key">The key.</param>
        /// <returns>The value associated with the given key in the dictionary, or the default
        /// value of <typeparamref name="TValue"/> if no entry with the key exists.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
        public TValue? getValueOrDefault(TKey key) {
            tryGetValue(key, out TValue? value);
            return value;
        }

        /// <summary>
        /// Returns a reference to the value in the dictionary with the given key.
        /// </summary>
        ///
        /// <param name="key">The key.</param>
        /// <param name="createIfNotExists">If set to true, creates a new entry in the dictionary
        /// if no entry exists with the key <paramref name="key"/> and returns a reference to the
        /// value (initialized to the default value of <typeparamref name="TValue"/>). Otherwise, throws
        /// an exception if no entry exists.</param>
        ///
        /// <returns>A reference to the value associated with the given key. The reference is guaranteed
        /// to be valid as long as no entry is added to or removed from the dictionary.</returns>
        ///
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
        /// <exception cref="ArgumentException">No entry exists with the given key, and
        /// <paramref name="createIfNotExists"/> is false.</exception>
        public ref TValue getValueRef(TKey key, bool createIfNotExists = false) {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            int hash = RuntimeHelpers.GetHashCode(key) & HASH_CODE_MASK;
            int chain = hash % m_chains.Length;
            int i = m_chains[chain];

            while (i != -1) {
                ref Slot slot = ref m_slots[i];
                if (slot.key == key)
                    return ref slot.value;

                i = slot.next;
            }

            if (!createIfNotExists)
                throw new ArgumentException("The key '" + key.ToString() + "' does not exist in the dictionary.", nameof(key));

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
                        int newChain = (RuntimeHelpers.GetHashCode(newSlots[j].key) & HASH_CODE_MASK) % newSize;
                        newSlots[j].next = newChains[newChain];
                        newChains[newChain] = j;
                    }

                    m_chains = newChains;
                    m_slots = newSlots;
                    chain = hash % m_chains.Length;
                }

                newIndex = m_count;
                m_count++;
            }

            //Console.WriteLine($"Creating at index: {newIndex}, chain: {chain}");

            ref Slot newSlot = ref m_slots[newIndex];
            ref int chainStart = ref m_chains[chain];

            newSlot.next = chainStart;
            newSlot.key = key;
            newSlot.hasValue = true;
            chainStart = newIndex;

            return ref newSlot.value;
        }

        /// <summary>
        /// Deletes the entry with the given key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>True if the entry was deleted, false if an entry with the given key does not
        /// exist.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
        public bool delete(TKey key) {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            int hash = RuntimeHelpers.GetHashCode(key) & HASH_CODE_MASK;
            int path = hash % m_chains.Length;
            int prev = -1;
            int i = m_chains[path];

            while (i != -1) {
                ref Slot slot = ref m_slots[i];

                if (slot.key == key) {
                    if (prev == -1)
                        m_chains[path] = slot.next;
                    else
                        m_slots[prev].next = slot.next;

                    slot.key = null;
                    slot.next = m_emptyChain;
                    slot.hasValue = false;
                    slot.value = default(TValue)!;

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
        /// Removes all elements from the dictionary.
        /// </summary>
        public void clear() {
            m_slots.AsSpan(0, m_count).Clear();
            m_chains.AsSpan().Fill(-1);
            m_count = 0;
            m_emptyChain = -1;
            m_emptyCount = 0;
        }

        /// <summary>
        /// Gets an enumerator for this <see cref="ReferenceDictionary{TKey,TValue}"/> instance.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate over the key-value pairs of
        /// this dictionary.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Gets an enumerator for this <see cref="ReferenceDictionary{TKey,TValue}"/> instance.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate over the key-value pairs of
        /// this dictionary.</returns>
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// Gets an enumerator for this <see cref="ReferenceDictionary{TKey,TValue}"/> instance.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate over the key-value pairs of
        /// this dictionary.</returns>
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// An implementation of <see cref="IEnumerator{T}"/> that can
        /// be used to iterate over the key-value pairs of a <see cref="ReferenceDictionary{TKey,TValue}"/>
        /// </summary>
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>> {

            private Slot[] m_slots;
            private int m_index;

            internal Enumerator(ReferenceDictionary<TKey, TValue> dict) {
                m_slots = dict.m_slots;
                m_index = dict.m_count;
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
            public KeyValuePair<TKey, TValue> Current {
                get {
                    ref var slot = ref m_slots[m_index];
                    return new KeyValuePair<TKey, TValue>(slot.key!, slot.value);
                }
            }

            /// <exclude/>
            public void Reset() => throw new NotImplementedException();

            /// <exclude/>
            object IEnumerator.Current => Current;

            /// <exclude/>
            public void Dispose() { }

        }

    }
}
