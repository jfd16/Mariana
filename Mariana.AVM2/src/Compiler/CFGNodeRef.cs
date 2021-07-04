using System;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Represents a reference to a node in the control flow graph.
    /// </summary>
    internal readonly struct CFGNodeRef : IEquatable<CFGNodeRef> {

        private const int START_ID = unchecked((int)0xFFFFFFFFu);
        private const int ID_MASK = 0x7FFFFFFF;
        private const int CATCH_FLAG = unchecked((int)0x80000000u);

        private readonly int m_id;
        private CFGNodeRef(int id) => m_id = id;

        /// <summary>
        /// Represents the "start" node in the control flow graph, which is the
        /// ancestor of the first basic block of the method and all catch clauses.
        /// </summary>
        public static readonly CFGNodeRef start = new CFGNodeRef(START_ID);

        /// <summary>
        /// Gets the type of the node represented by this instance.
        /// </summary>
        public CFGNodeRefType type {
            get {
                if (m_id == START_ID)
                    return CFGNodeRefType.START;

                return ((m_id & CATCH_FLAG) != 0) ? CFGNodeRefType.CATCH : CFGNodeRefType.BLOCK;
            }
        }

        /// <summary>
        /// Returns true if this instance represents the "start" node, false otherwise.
        /// </summary>
        public bool isStart => m_id == START_ID;

        /// <summary>
        /// Returns true if this instance represents a basic block, false otherwise.
        /// </summary>
        public bool isBlock => (m_id & CATCH_FLAG) == 0;

        /// <summary>
        /// Returns true if this instance represents a catch entry node, false otherwise.
        /// </summary>
        public bool isCatch => (m_id & CATCH_FLAG) != 0 && m_id != START_ID;

        /// <summary>
        /// Gets the index of the basic block or exception handler referred to by this node.
        /// If this node is the start node, returns an unspecified value.
        /// </summary>
        public int id => m_id & ID_MASK;

        /// <summary>
        /// Creates a new <see cref="CFGNodeRef"/> instance for a basic block.
        /// </summary>
        /// <param name="block">The basic block for which to create a <see cref="CFGNodeRef"/>
        /// instance.</param>
        /// <returns>A <see cref="CFGNodeRef"/> instance.</returns>
        public static CFGNodeRef forBasicBlock(in BasicBlock block) => new CFGNodeRef(block.id);

        /// <summary>
        /// Creates a new <see cref="CFGNodeRef"/> instance for the basic block with the given id.
        /// </summary>
        /// <param name="blockId">The basic block id for which to create a <see cref="CFGNodeRef"/>
        /// instance.</param>
        /// <returns>A <see cref="CFGNodeRef"/> instance.</returns>
        public static CFGNodeRef forBasicBlock(int blockId) => new CFGNodeRef(blockId);

        /// <summary>
        /// Creates a new <see cref="CFGNodeRef"/> instance for the catch entry node of exception handler.
        /// </summary>
        /// <param name="excHandler">The exception handler for which to create a <see cref="CFGNodeRef"/>
        /// instance.</param>
        /// <returns>A <see cref="CFGNodeRef"/> instance.</returns>
        public static CFGNodeRef forExceptionHandler(in ExceptionHandler excHandler) => new CFGNodeRef(excHandler.id | CATCH_FLAG);

        /// <summary>
        /// Creates a new <see cref="CFGNodeRef"/> instance for the catch entry node of the
        /// exception handler with the given id.
        /// </summary>
        /// <param name="handlerId">The exception handler id for which to create a <see cref="CFGNodeRef"/>
        /// instance.</param>
        /// <returns>A <see cref="CFGNodeRef"/> instance.</returns>
        public static CFGNodeRef forCatch(int handlerId) => new CFGNodeRef(handlerId | CATCH_FLAG);

        /// <summary>
        /// Returns a value indicating whether <paramref name="obj"/> is equal to this instance.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>True if this instance is equal to <paramref name="obj"/>, otherwise false.</returns>
        public override bool Equals(object obj) => obj is CFGNodeRef node && node == this;

        /// <summary>
        /// Returns a value indicating whether <paramref name="other"/> is equal to this instance.
        /// </summary>
        /// <param name="other">The <see cref="CFGNodeRef"/> instance to compare with the
        /// current instance.</param>
        /// <returns>True if this instance is equal to <paramref name="other"/>, otherwise false.</returns>
        public bool Equals(CFGNodeRef other) => other.m_id == m_id;

        /// <summary>
        /// Gets a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this <see cref="CFGNodeRef"/> instance.</returns>
        public override int GetHashCode() => m_id << 1 | (int)((uint)m_id >> 31);

        /// <summary>
        /// Determines whether two <see cref="CFGNodeRef"/> instances are equal.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>True if <paramref name="x"/> is equal to <paramref name="y"/>, otherwise false.</returns>
        public static bool operator ==(CFGNodeRef x, CFGNodeRef y) => x.m_id == y.m_id;

        /// <summary>
        /// Determines whether two <see cref="CFGNodeRef"/> instances are not equal.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>True if <paramref name="x"/> is equal to <paramref name="y"/>, otherwise false.</returns>
        public static bool operator !=(CFGNodeRef x, CFGNodeRef y) => x.m_id != y.m_id;

        /// <summary>
        /// Returns a string representation of this <see cref="CFGNodeRef"/> instance.
        /// </summary>
        /// <returns>A string representation of this <see cref="CFGNodeRef"/> instance. </returns>
        public override string ToString() {
            var type = this.type;

            return type switch {
                CFGNodeRefType.START => "start",
                CFGNodeRefType.CATCH => "EH(" + ASint.AS_convertString(id) + ")",
                CFGNodeRefType.BLOCK => "BB(" + ASint.AS_convertString(id) + ")"
            };
        }

    }

    /// <summary>
    /// Specifies the type of a control flow graph node.
    /// </summary>
    internal enum CFGNodeRefType : byte {
        // Enumerator values are important here! Do not change them.

        /// <summary>
        /// The node is a basic block.
        /// </summary>
        BLOCK = 0,

        /// <summary>
        /// The node is the catch entry node for an exception handler.
        /// </summary>
        CATCH = 1,

        /// <summary>
        /// This value is reserved for <see cref="CFGNodeRef.start"/>.
        /// </summary>
        START = 2,
    }

}
