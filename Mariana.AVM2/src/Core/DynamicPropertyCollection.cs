using System;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A collection for storing dynamic properties of AS3 objects that are instances of dynamic classes.
    /// </summary>
    /// <seealso cref="ASObject.AS_dynamicProps" qualifyHint="true" />
    public sealed class DynamicPropertyCollection {

        private struct Slot {
            internal string name;
            internal int next;
            internal int hash;
            internal ASAny value;
            internal bool isEnum;
        }

        // The initial table size for new instances.
        private const int INITIAL_SIZE = 5;

        private int m_emptyChainHead = -1;
        private int m_emptyCount;

        private int[] m_chainHeads;
        private Slot[] m_slots;
        private int m_count;

        /// <summary>
        /// Creates a new instance of <see cref="DynamicPropertyCollection"/>.
        /// </summary>
        internal DynamicPropertyCollection() {
            m_slots = new Slot[INITIAL_SIZE];
            m_chainHeads = new int[INITIAL_SIZE];
            m_chainHeads.AsSpan().Fill(-1);
        }

        /// <summary>
        /// Gets the number of properties in this instance.
        /// </summary>
        public int count => m_count - m_emptyCount;

        /// <summary>
        /// Gets or sets the value of the property with the specified name.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        public ASAny this[string name] {
            get => getValue(name);
            set => setValue(name, value);
        }

        /// <summary>
        /// Gets the value of the property with the given name.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <returns>The value of the property with the given name. If no property exists, returns
        /// undefined.</returns>
        public ASAny getValue(string name) {
            tryGetValue(name, out ASAny value);
            return value;
        }

        /// <summary>
        /// Gets the value of the property with the given name.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value of the property, or undefined if no property with the given
        /// name exists.</param>
        /// <returns>True, if the property with the given name exists, false otherwise.</returns>
        public bool tryGetValue(string name, out ASAny value) {
            if (name == null) {
                value = default;
                return false;
            }

            int hash = name.GetHashCode();
            int i = m_chainHeads[(hash & 0x7FFFFFFF) % m_chainHeads.Length];

            while (i != -1) {
                ref Slot slot = ref m_slots[i];
                if (slot.hash == hash && slot.name == name) {
                    value = slot.value;
                    return true;
                }
                i = slot.next;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Sets the value of the property with the given name.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="isEnum">True if the property's enumerable flag must be set, otherwise
        /// false.</param>
        public void setValue(string name, ASAny value, bool isEnum = true) {
            if (name == null)
                throw ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

            int hash = name.GetHashCode();
            int chain = (hash & 0x7FFFFFFF) % m_chainHeads.Length;
            int i = m_chainHeads[chain];

            while (i != -1) {
                ref Slot slot = ref m_slots[i];
                if (slot.hash == hash && slot.name == name) {
                    slot.value = value;
                    slot.isEnum = isEnum;
                    return;
                }
                i = slot.next;
            }

            int newIndex;

            if (m_emptyCount > 0) {
                newIndex = m_emptyChainHead;
                m_emptyChainHead = m_slots[newIndex].next;
                m_emptyCount--;
            }
            else {
                if (m_count == m_slots.Length) {
                    int newSize = DataStructureUtil.nextPrime(checked(m_count * 2));

                    int[] newChainHeads = new int[newSize];
                    newChainHeads.AsSpan().Fill(-1);

                    Slot[] newSlots = new Slot[newSize];
                    m_slots.AsSpan(0, m_count).CopyTo(newSlots);

                    for (int j = 0, n = m_count; j < n; j++) {
                        int newChain = (newSlots[j].hash & 0x7FFFFFFF) % newSize;
                        newSlots[j].next = newChainHeads[newChain];
                        newChainHeads[newChain] = j;
                    }

                    m_chainHeads = newChainHeads;
                    m_slots = newSlots;
                    chain = (hash & 0x7FFFFFFF) % newSize;
                }

                newIndex = m_count++;
            }

            ref Slot newSlot = ref m_slots[newIndex];
            ref int chainStart = ref m_chainHeads[chain];

            newSlot.hash = hash;
            newSlot.name = name;
            newSlot.value = value;
            newSlot.isEnum = isEnum;
            newSlot.next = chainStart;
            chainStart = newIndex;
        }

        /// <summary>
        /// Returns a Boolean value indicating whether a property with the given name exists.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <returns>True, if a property with the given name exists, false otherwise.</returns>
        public bool hasValue(string name) => getIndex(name) != -1;

        /// <summary>
        /// Returns the index of the property with the given name, or -1 if it does not exist. The
        /// index can be used with methods such as <see cref="getNameFromIndex"/>,
        /// <see cref="getValueFromIndex"/> and <see cref="getNextIndex"/>
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <returns>The index of the property, or -1 if a property with the given name does not
        /// exist.</returns>
        public int getIndex(string name) {
            if (name == null)
                return -1;

            int hash = name.GetHashCode();
            int i = m_chainHeads[(hash & 0x7FFFFFFF) % m_chainHeads.Length];

            while (i != -1) {
                ref Slot slot = ref m_slots[i];
                if (slot.hash == hash && slot.name == name)
                    return i;
                i = slot.next;
            }

            return -1;
        }

        /// <summary>
        /// Gets the index of the next enumerable property after the one at the specified index. If
        /// there are no more enumerable properties, returns -1.
        /// </summary>
        /// <param name="index">The index of the property from where to start searching. A value of -1
        /// will return the index of the first property.</param>
        /// <returns>The index of the next enumerable property after the specified one, or -1 if there
        /// are no more enumerable properties.</returns>
        /// <remarks>
        /// This method is used to implement ActionScript 3's for-in loops.
        /// </remarks>
        public int getNextIndex(int index) {
            for (int i = (index < 0) ? 0 : index + 1; i < m_count; i++) {
                if (m_slots[i].isEnum)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Gets the name of the property at the specified index.
        /// </summary>
        /// <param name="i">The index.</param>
        /// <returns>The name of the property.</returns>
        /// <remarks>
        /// This method is used to implement ActionScript 3's for-in loops.
        /// </remarks>
        public string getNameFromIndex(int i) => ((uint)i < (uint)m_count) ? m_slots[i].name : null;

        /// <summary>
        /// Gets the value of the property at the specified index.
        /// </summary>
        /// <param name="i">The index.</param>
        /// <returns>The value of the property.</returns>
        /// <remarks>
        /// This method is used to implement ActionScript 3's for-in loops.
        /// </remarks>
        public ASAny getValueFromIndex(int i) => ((uint)i < (uint)m_count) ? m_slots[i].value : default(ASAny);

        /// <summary>
        /// Returns a Boolean value indicating whether the property with the specified name is
        /// enumerable.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <returns>True if a property with the given name exists and is enumerable, otherwise
        /// false.</returns>
        public bool isEnumerable(string name) {
            int index = getIndex(name);
            return index != -1 && m_slots[index].isEnum;
        }

        /// <summary>
        /// Sets the enumerable flag of a property.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="isEnum">The value of the enumerable flag.</param>
        public void setEnumerable(string name, bool isEnum) {
            int index = getIndex(name);
            if (index != -1)
                m_slots[index].isEnum = isEnum;
        }

        /// <summary>
        /// Deletes the property with the specified name.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <returns>True if the property was deleted, false if it was not (such as if no property
        /// with the given name exists).</returns>
        public bool delete(string name) {
            if (name == null)
                return false;

            int hash = name.GetHashCode();
            int chain = (hash & 0x7FFFFFFF) % m_chainHeads.Length;
            int prev = -1;

            int i = m_chainHeads[chain];

            while (i != -1) {
                ref Slot slot = ref m_slots[i];

                if (slot.hash == hash && slot.name == name) {
                    if (prev == -1)
                        m_chainHeads[chain] = slot.next;
                    else
                        m_slots[prev].next = slot.next;

                    slot.name = null;
                    slot.hash = -1;
                    slot.value = default(ASAny);
                    slot.next = m_emptyChainHead;
                    slot.isEnum = false;

                    m_emptyChainHead = i;
                    m_emptyCount++;

                    return true;
                }

                prev = i;
                i = slot.next;
            }

            return false;
        }

        /// <summary>
        /// Performs a prototype chain search for dynamic properties on the given object.
        /// This will check the dynamic properties of the object (and all objects in its
        /// prototype chain) for a property with the given name.
        /// </summary>
        ///
        /// <param name="obj">The object on which to perform the search.</param>
        /// <param name="name">The name of the property to find.</param>
        /// <param name="value">The value of the property, if is is found. Otherwise, this is set to
        /// undefined.</param>
        /// <returns>True, if a property was found; false otherwise.</returns>
        internal static bool searchPrototypeChain(ASObject obj, string name, out ASAny value) {
            if (name == null) {
                value = default;
                return false;
            }

            int hash = name.GetHashCode();    // Compute the hash code only once.

            for (; obj != null; obj = obj.AS_proto) {
                DynamicPropertyCollection table = obj.AS_dynamicProps;
                if (table == null)
                    continue;

                Slot[] slots = obj.AS_dynamicProps.m_slots;
                int[] chainHeads = obj.AS_dynamicProps.m_chainHeads;

                int i = chainHeads[(hash & 0x7FFFFFFF) % chainHeads.Length];

                while (i != -1) {
                    ref Slot slot = ref slots[i];
                    if (slot.hash == hash && slot.name == name) {
                        value = slot.value;
                        return true;
                    }
                    i = slot.next;
                }
            }

            value = default(ASAny);
            return false;
        }

    }

}
