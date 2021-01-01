using System;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Represents a reference to a data node or an instruction, used in the use-def
    /// info of a data node.
    /// </summary>
    internal readonly struct DataNodeOrInstrRef : IEquatable<DataNodeOrInstrRef> {

        private const int ID_MASK = 0x7FFFFFFF;
        private const int DATA_NODE_FLAG = unchecked((int)0x80000000u);

        private readonly int m_id;
        private DataNodeOrInstrRef(int id) => m_id = id;

        /// <summary>
        /// Creates a <see cref="DataNodeOrInstrRef"/> representing an instruction.
        /// </summary>
        /// <param name="instrId">The id of the instruction.</param>
        /// <returns>A <see cref="DataNodeOrInstrRef"/> representing the instruction.</returns>
        public static DataNodeOrInstrRef forInstruction(int instrId) => new DataNodeOrInstrRef(instrId);

        /// <summary>
        /// Creates a <see cref="DataNodeOrInstrRef"/> representing a data node.
        /// </summary>
        /// <param name="nodeId">The id of the data node.</param>
        /// <returns>A <see cref="DataNodeOrInstrRef"/> representing the data node.</returns>
        public static DataNodeOrInstrRef forDataNode(int nodeId) => new DataNodeOrInstrRef(nodeId | DATA_NODE_FLAG);

        /// <summary>
        /// Returns the index of the instruction or data node represented by this <see cref="DataNodeOrInstrRef"/>.
        /// </summary>
        public int instrOrNodeId => m_id & ID_MASK;

        /// <summary>
        /// Returns a value indicating whether this <see cref="DataNodeOrInstrRef"/> represents an instruction.
        /// </summary>
        /// <returns>True if this <see cref="DataNodeOrInstrRef"/> represents an instruction, otherwise false.</returns>
        public bool isInstruction => (m_id & DATA_NODE_FLAG) == 0;

        /// <summary>
        /// Returns a value indicating whether this <see cref="DataNodeOrInstrRef"/> represents a data node.
        /// </summary>
        /// <returns>True if this <see cref="DataNodeOrInstrRef"/> represents a data node, otherwise false.</returns>
        public bool isDataNode => (m_id & DATA_NODE_FLAG) != 0;

         /// <summary>
        /// Returns a value indicating whether <paramref name="other"/> is equal to this instance.
        /// </summary>
        /// <param name="other">The <see cref="DataNodeOrInstrRef"/> instance to compare with the
        /// current instance.</param>
        /// <returns>True if this instance is equal to <paramref name="other"/>, otherwise false.</returns>
        public bool Equals(DataNodeOrInstrRef other) => m_id == other.m_id;

        /// <summary>
        /// Returns a value indicating whether <paramref name="obj"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>True if this instance is equal to <paramref name="obj"/>, otherwise false.</returns>
        public override bool Equals(object obj) => obj is DataNodeOrInstrRef d && m_id == d.m_id;

        /// <summary>
        /// Gets a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this <see cref="DataNodeOrInstrRef"/> instance.</returns>
        public override int GetHashCode() => m_id << 1 | (int)((uint)m_id >> 31);

        /// <summary>
        /// Determines whether two <see cref="DataNodeOrInstrRef"/> instances are equal.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>True if <paramref name="x"/> is equal to <paramref name="y"/>, otherwise false.</returns>
        public static bool operator ==(DataNodeOrInstrRef x, DataNodeOrInstrRef y) => x.m_id == y.m_id;

        /// <summary>
        /// Determines whether two <see cref="DataNodeOrInstrRef"/> instances are not equal.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>True if <paramref name="x"/> is not equal to <paramref name="y"/>, otherwise false.</returns>
        public static bool operator !=(DataNodeOrInstrRef x, DataNodeOrInstrRef y) => x.m_id != y.m_id;

        /// <summary>
        /// Returns a string representation of this <see cref="DataNodeOrInstrRef"/> instance.
        /// </summary>
        /// <returns>A string representation of this <see cref="DataNodeOrInstrRef"/> instance.</returns>
        public override string ToString() {
            if (isInstruction)
                return "instr(" + ASint.AS_convertString(instrOrNodeId) + ")";

            if (isDataNode)
                return "node(" + ASint.AS_convertString(instrOrNodeId) + ")";

            return "";
        }

    }

}
