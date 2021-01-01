using System.Runtime.InteropServices;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// A union structure that can contain a single integer value, or a token for an integer
    /// array allocated from a static or dynamic array pool.
    /// </summary>
    /// <remarks>
    /// This is used as an optimization in cases where single-element arrays are common.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit)]
    internal struct IntSingleArrayUnion {

        /// <summary>
        /// A single integer value.
        /// </summary>
        [FieldOffset(0)]
        public int single;

        /// <summary>
        /// A token for an array allocated in a static array pool.
        /// </summary>
        [FieldOffset(0)]
        public StaticArrayPoolToken<int> staticToken;

        /// <summary>
        /// A token for an array allocated in a dynamic array pool.
        /// </summary>
        [FieldOffset(0)]
        public DynamicArrayPoolToken<int> dynamicToken;

    }

}
