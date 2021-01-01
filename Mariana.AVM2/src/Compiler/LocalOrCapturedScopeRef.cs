using System;
using System.Globalization;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Represents a reference to a local or captured scope stack node.
    /// </summary>
    internal readonly struct LocalOrCapturedScopeRef : IEquatable<LocalOrCapturedScopeRef> {

        private const int ID_MASK = 0x7FFFFFFF;
        private const int CAPTURED_FLAG = unchecked((int)0x80000000u);
        private const int NULL_VALUE = unchecked((int)0xFFFFFFFFu);

        private readonly int m_id;
        private LocalOrCapturedScopeRef(int id) => m_id = id;

        /// <summary>
        /// A <see cref="LocalOrCapturedScopeRef"/> representing a "null" reference. This does not
        /// refer to any local or captured scope stack object.
        /// </summary>
        public static readonly LocalOrCapturedScopeRef nullRef = new LocalOrCapturedScopeRef(NULL_VALUE);

        /// <summary>
        /// Creates a <see cref="LocalOrCapturedScopeRef"/> representing a local scope stack node.
        /// </summary>
        /// <param name="nodeId">The data node id for the local scope stack node.</param>
        /// <returns>A <see cref="DataNodeOrInstrRef"/> representing the local scope stack node.</returns>
        public static LocalOrCapturedScopeRef forLocal(int nodeId) => new LocalOrCapturedScopeRef(nodeId);

        /// <summary>
        /// Creates a <see cref="LocalOrCapturedScopeRef"/> representing an object in the captured scope
        /// stack.
        /// </summary>
        /// <param name="height">The height of the object in the captured scope stack.</param>
        /// <returns>A <see cref="LocalOrCapturedScopeRef"/> representing the captured scope stack object.</returns>
        public static LocalOrCapturedScopeRef forCaptured(int height) => new LocalOrCapturedScopeRef(height | CAPTURED_FLAG);

        /// <summary>
        /// Returns the data node id of a local scope stack object or the height of a captured scope
        /// stack object. For the null reference, this property has an unspecified value.
        /// </summary>
        public int idOrCaptureHeight => m_id & ID_MASK;

        /// <summary>
        /// Returns a value indicating whether this <see cref="LocalOrCapturedScopeRef"/> represents a
        /// local scope stack object.
        /// </summary>
        /// <returns>True if this <see cref="LocalOrCapturedScopeRef"/> represents a local scope stack object,
        /// otherwise false.</returns>
        public bool isLocal => (m_id & CAPTURED_FLAG) == 0;

        /// <summary>
        /// Returns a value indicating whether this <see cref="LocalOrCapturedScopeRef"/> represents a captured
        /// scope stack object.
        /// </summary>
        /// <returns>True if this <see cref="LocalOrCapturedScopeRef"/> represents a captured scope stack object,
        /// otherwise false.</returns>
        public bool isCaptured => (m_id & CAPTURED_FLAG) != 0 && m_id != NULL_VALUE;

        /// <summary>
        /// Returns a value indicating whether this <see cref="LocalOrCapturedScopeRef"/> is the
        /// null reference.
        /// </summary>
        /// <returns>True if this <see cref="LocalOrCapturedScopeRef"/> is the null reference, otherwise
        /// false.</returns>
        public bool isNull => m_id == NULL_VALUE;

         /// <summary>
        /// Returns a value indicating whether <paramref name="other"/> is equal to this instance.
        /// </summary>
        /// <param name="other">The <see cref="LocalOrCapturedScopeRef"/> instance to compare with the
        /// current instance.</param>
        /// <returns>True if this instance is equal to <paramref name="other"/>, otherwise false.</returns>
        public bool Equals(LocalOrCapturedScopeRef other) => m_id == other.m_id;

        /// <summary>
        /// Returns a value indicating whether <paramref name="obj"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>True if this instance is equal to <paramref name="obj"/>, otherwise false.</returns>
        public override bool Equals(object obj) => obj is LocalOrCapturedScopeRef d && m_id == d.m_id;

        /// <summary>
        /// Gets a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this <see cref="LocalOrCapturedScopeRef"/> instance.</returns>
        public override int GetHashCode() => m_id << 1 | (int)((uint)m_id >> 31);

        /// <summary>
        /// Determines whether two <see cref="LocalOrCapturedScopeRef"/> instances are equal.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>True if <paramref name="x"/> is equal to <paramref name="y"/>, otherwise false.</returns>
        public static bool operator ==(LocalOrCapturedScopeRef x, LocalOrCapturedScopeRef y) => x.m_id == y.m_id;

        /// <summary>
        /// Determines whether two <see cref="LocalOrCapturedScopeRef"/> instances are not equal.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>True if <paramref name="x"/> is not equal to <paramref name="y"/>, otherwise false.</returns>
        public static bool operator !=(LocalOrCapturedScopeRef x, LocalOrCapturedScopeRef y) => x.m_id != y.m_id;

        /// <summary>
        /// Returns a string representation of this <see cref="LocalOrCapturedScopeRef"/> instance.
        /// </summary>
        /// <returns>A string representation of this <see cref="LocalOrCapturedScopeRef"/> instance.</returns>
        public override string ToString() {
            if (isNull)
                return "null";
            if (isLocal)
                return "local(#" + idOrCaptureHeight.ToString(CultureInfo.InvariantCulture) + ")";
            if (isCaptured)
                return "captured(" + idOrCaptureHeight.ToString(CultureInfo.InvariantCulture) + ")";
            return "";
        }

    }

}
