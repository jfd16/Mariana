using System;
using System.Collections.Generic;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Used by the compiler for indexing class traits by slot index and methods by disp_id.
    /// </summary>
    internal sealed class SlotMap {

        // Since slot indices are 30-bit values in ABC, we can use two bits in the key
        // for distinguishing between instance and static traits, and slot ids from
        // method disp_ids.
        private const uint KEY_STATIC_BIT = 1;
        private const uint KEY_METHOD_BIT = 2;

        private Dictionary<uint, Trait> m_dict = new Dictionary<uint, Trait>();

        /// <summary>
        /// Returns the dictionary key for a trait at the given slot index.
        /// </summary>
        /// <param name="slotId">The slot index for the trait.</param>
        /// <param name="isStatic">True if the trait is static or global, false for instance traits.</param>
        /// <returns>The dictionary key corresponding to the given slot index.</returns>
        private static uint _keyForSlotId(int slotId, bool isStatic) =>
            (uint)(slotId - 1) << 2 | (isStatic ? KEY_STATIC_BIT : 0);

        /// <summary>
        /// Returns the dictionary key for a method whose disp_id is given.
        /// </summary>
        /// <param name="dispId">The disp_id for the method.</param>
        /// <param name="isStatic">True if the method is static or global, false for instance method.</param>
        /// <returns>The dictionary key corresponding to the given disp_id.</returns>
        private static uint _keyForDispId(int dispId, bool isStatic) =>
            (uint)(dispId - 1) << 2 | KEY_METHOD_BIT | (isStatic ? KEY_STATIC_BIT : 0);

        /// <summary>
        /// Associates a trait with the given slot index.
        /// </summary>
        /// <param name="slotId">The slot index. This must be greater than 0.</param>
        /// <param name="trait">The trait to associate with the slot at <paramref name="slotId"/>.</param>
        /// <returns>True if the trait was successfully mapped to <paramref name="slotId"/>, false
        /// otherwise (for example, if another trait is already mapped to that slot).</returns>
        public bool tryAddSlot(int slotId, Trait trait) => m_dict.TryAdd(_keyForSlotId(slotId, trait.isStatic), trait);

        /// <summary>
        /// Associates a method trait with a disp_id value.
        /// </summary>
        /// <param name="dispId">The disp_id for the method. This must be greater than 0.</param>
        /// <param name="method">The method trait to associate with <paramref name="dispId"/>.</param>
        /// <returns>True if the method was successfully mapped to <paramref name="dispId"/>, false
        /// otherwise (for example, if another method already has that disp_id).</returns>
        public bool tryAddMethod(int dispId, MethodTrait method) => m_dict.TryAdd(_keyForDispId(dispId, method.isStatic), method);

        /// <summary>
        /// Returns the trait at the given slot index.
        /// </summary>
        /// <param name="slotId">The slot index.</param>
        /// <param name="isStatic">Set to true for static or global traits, false for instance traits.</param>
        /// <returns>A <see cref="Trait"/> instance representing the trait at <paramref name="slotId"/>,
        /// or null if no trait is mapped to that slot index.</returns>
        public Trait? getSlot(int slotId, bool isStatic) {
            m_dict.TryGetValue(_keyForSlotId(slotId, isStatic), out Trait trait);
            return trait;
        }

        /// <summary>
        /// Returns the method associated with the given disp_id.
        /// </summary>
        /// <param name="dispId">The disp_id of the method.</param>
        /// <param name="isStatic">Set to true for static or global methods, false for instance methods.</param>
        /// <returns>A <see cref="MethodTrait"/> instance representing the method whose disp_id is,
        /// given, or null if no method is associated with that disp_id.</returns>
        public MethodTrait? getMethodByDispId(int dispId, bool isStatic) {
            m_dict.TryGetValue(_keyForDispId(dispId, isStatic), out Trait trait);
            return trait as MethodTrait;
        }

        /// <summary>
        /// Adds the slot definitions from a parent class to this slot map.
        /// </summary>
        /// <param name="parent">The slot map for the parent class.</param>
        public void addParentSlots(SlotMap parent) {
            foreach (var parentEntry in parent.m_dict) {
                if ((parentEntry.Key & KEY_STATIC_BIT) == 0)
                    m_dict.TryAdd(parentEntry.Key, parentEntry.Value);
            }
        }

    }

}
