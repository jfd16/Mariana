using System;
using System.Runtime.CompilerServices;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Maintains a global mapping from <see cref="Type"/> instances representing types used as underlying
    /// types of AVM2 classes to the corresponding <see cref="Class"/> instances. This is used by methods
    /// such as <see cref="Class.fromType"/>.
    /// </summary>
    internal static class ClassTypeMap {

        private static ConditionalWeakTable<Type, Class> m_table = new ConditionalWeakTable<Type, Class>();
        private static object m_getOrCreateLock = new object();

        /// <summary>
        /// Returns the <see cref="Class"/> associated with a <see cref="Type"/>.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> for which to get the associated <see cref="Class"/>.</param>
        /// <returns>The <see cref="Class"/> associated with <paramref name="type"/>, or null if no
        /// such class exists.</returns>
        public static Class? getClass(Type type) => m_table.TryGetValue(type, out Class klass) ? klass : null;

        /// <summary>
        /// Returns the <see cref="Class"/> associated with a <see cref="Type"/>, or calls a callback function
        /// to create the class if it does not exist.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> for which to get the associated <see cref="Class"/>.</param>
        /// <param name="createCallback">A callback function that is executed if no class associated with
        /// <paramref name="type"/> exists. The <see cref="Class"/> returned by the function is added to
        /// the mapping with the key <paramref name="type"/>. The callback is guaranteed to be called at
        /// most once, even when this method is called from multiple threads simultaneously with the
        /// same value for <paramref name="type"/>.</param>
        /// <returns>The <see cref="Class"/> associated with <paramref name="type"/> in the global map.</returns>
        public static Class getOrCreateClass(Type type, ConditionalWeakTable<Type, Class>.CreateValueCallback createCallback) {
            // GetValue calls the callback function outside the table's internal lock,
            // so we need to use another lock to ensure that the callback is called
            // only once.
            lock (m_getOrCreateLock) {
                return m_table.GetValue(type, createCallback);
            }
        }

        /// <summary>
        /// Registers the given class as associated with a <see cref="Type"/> as its underlying
        /// type in the global map.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> to associate with <paramref name="klass"/> as its
        /// underlying type in the global map.</param>
        /// <param name="klass">A <see cref="Class"/> instance.</param>
        public static void addClass(Type type, Class klass) => m_table.Add(type, klass);

    }

}
