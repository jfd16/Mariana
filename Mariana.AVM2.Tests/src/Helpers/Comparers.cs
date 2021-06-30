using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Tests.Helpers {

    /// <summary>
    /// An equality comparer for instances of <see cref="ASAny"/> that uses object reference equality.
    /// </summary>
    internal class ByRefEqualityComparer : IEqualityComparer<ASAny> {
        /// <summary>
        /// A shared instance of <see cref="ByRefEqualityComparer"/>.
        /// </summary>
        public static readonly ByRefEqualityComparer instance = new ByRefEqualityComparer();

        public int GetHashCode(ASAny obj) => RuntimeHelpers.GetHashCode(obj.value);
        public bool Equals(ASAny x, ASAny y) => x == y;
    }

    /// <summary>
    /// An equality comparer that uses delegates for the the <see cref="IEqualityComparer{T}.Equals"/>
    /// and <see cref="IEqualityComparer{T}.GetHashCode"/> methods.
    /// </summary>
    internal class FunctionEqualityComparer<T> : IEqualityComparer<T> {

        private Func<T, T, bool> m_equalsFunc;
        private Func<T, int> m_hashCodeFunc;

        /// <summary>
        /// Creates a new instance of <see cref="FunctionEqualityComparer{T}"/>.
        /// </summary>
        /// <param name="equalsFunc">The function that implements the <see cref="IEqualityComparer{T}.Equals"/> method.</param>
        /// <param name="hashCodeFunc">The function that implements the <see cref="IEqualityComparer{T}.GetHashCode"/> method.</param>
        public FunctionEqualityComparer(Func<T, T, bool> equalsFunc, Func<T, int> hashCodeFunc) {
            m_equalsFunc = equalsFunc;
            m_hashCodeFunc = hashCodeFunc;
        }

        public int GetHashCode(T value) => m_hashCodeFunc(value);
        public bool Equals(T x, T y) => m_equalsFunc(x, y);

    }

}
