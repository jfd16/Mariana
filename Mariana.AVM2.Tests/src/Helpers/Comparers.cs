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

        public int GetHashCode(ASAny obj) => (obj.value == null) ? 0 : RuntimeHelpers.GetHashCode(obj.value);
        public bool Equals(ASAny x, ASAny y) => x == y;
    }

    /// <summary>
    /// An equality comparer for instances of pairs of <see cref="ASAny"/> that uses object reference equality.
    /// </summary>
    internal class ByRefPairEqualityComparer : IEqualityComparer<(ASAny, ASAny)> {
        /// <summary>
        /// A shared instance of <see cref="ByRefPairEqualityComparer"/>.
        /// </summary>
        public static readonly ByRefPairEqualityComparer instance = new ByRefPairEqualityComparer();

        public int GetHashCode((ASAny, ASAny) pair) {
            return ((pair.Item1.value == null) ? 0 : RuntimeHelpers.GetHashCode(pair.Item1.value))
                ^ ((pair.Item2.value == null) ? 0 : RuntimeHelpers.GetHashCode(pair.Item2.value));
        }

        public bool Equals((ASAny, ASAny) x, (ASAny, ASAny) y) =>
            x.Item1 == y.Item1 && x.Item2 == y.Item2;
    }

}
