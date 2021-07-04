using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Mariana.AVM2.Core;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Represents a single-static assignment (SSA) definition for an element on the
    /// operand stack or scope stack or a local variable.
    /// </summary>
    internal struct DataNode {

        /// <summary>
        /// The unique id of the data node. A <see cref="DataNode"/> instance can be retrieved
        /// from its id using <see cref="MethodCompilation.getDataNode"/>.
        /// </summary>
        public int id;

        /// <summary>
        /// A <see cref="DataNodeSlot"/> representing the stack, scope or local slot occupied
        /// by this node.
        /// </summary>
        public DataNodeSlot slot;

        /// <summary>
        /// The data type of this node.
        /// </summary>
        public DataNodeType dataType;

        /// <summary>
        /// The data type that a stack node must be coerced to after it has been pushed.
        /// If there is no type coercion to be made after pushing, the value of this field
        /// is <see cref="DataNodeType.UNKNOWN"/>.
        /// </summary>
        public DataNodeType onPushCoerceType;

        /// <summary>
        /// A set of flags from the <see cref="DataNodeFlags"/> enum applicable for this node.
        /// </summary>
        public DataNodeFlags flags;

        /// <summary>
        /// Contains data flow (def-use) information for this node.
        /// </summary>
        public DataNodeDUInfo defUseInfo;

        /// <summary>
        /// Used by the compiler for storing constant data for a node, such as the value of a
        /// compile time constant or the class of an object node.
        /// </summary>
        public DataNodeConstant constant;

        /// <summary>
        /// True if this node holds a constant value, otherwise false.
        /// </summary>
        /// <remarks>
        /// Using this property will get or set the <see cref="DataNodeFlags.CONSTANT"/> flag.
        /// </remarks>
        public bool isConstant {
            readonly get => (flags & DataNodeFlags.CONSTANT) != 0;
            set => flags = value ? flags | DataNodeFlags.CONSTANT : flags & ~DataNodeFlags.CONSTANT;
        }

        /// <summary>
        /// True if this node is a phi-node, otherwise false.
        /// </summary>
        /// <remarks>
        /// Using this property will get or set the <see cref="DataNodeFlags.PHI"/> flag.
        /// </remarks>
        public bool isPhi {
            readonly get => (flags & DataNodeFlags.PHI) != 0;
            set => flags = value ? flags | DataNodeFlags.PHI : flags & ~DataNodeFlags.PHI;
        }

        /// <summary>
        /// True if this node represents an object value that is definitely
        /// not null or undefined, otherwise false.
        /// </summary>
        /// <remarks>
        /// Using this property will get or set the <see cref="DataNodeFlags.NOT_NULL"/> flag.
        /// </remarks>
        public bool isNotNull {
            readonly get => (flags & DataNodeFlags.NOT_NULL) != 0;
            set => flags = value ? flags | DataNodeFlags.NOT_NULL : flags & ~DataNodeFlags.NOT_NULL;
        }

        /// <summary>
        /// True if this node represents an object on the scope stack that was pushed with
        /// the <c>pushwith</c> instruction, otherwise false.
        /// </summary>
        /// <remarks>
        /// Using this property will get or set the <see cref="DataNodeFlags.WITH_SCOPE"/> flag.
        /// </remarks>
        public bool isWithScope {
            readonly get => (flags & DataNodeFlags.WITH_SCOPE) != 0;
            set => flags = value ? flags | DataNodeFlags.WITH_SCOPE : flags & ~DataNodeFlags.WITH_SCOPE;
        }

        /// <summary>
        /// True if this node represents an argument to the method call.
        /// </summary>
        /// <remarks>
        /// Using this property will get or set the <see cref="DataNodeFlags.ARGUMENT"/> flag.
        /// </remarks>
        public bool isArgument {
            readonly get => (flags & DataNodeFlags.ARGUMENT) != 0;
            set => flags = value ? flags | DataNodeFlags.ARGUMENT : flags & ~DataNodeFlags.ARGUMENT;
        }

        /// <summary>
        /// True if this node represents a value that will not be pushed onto the stack in
        /// the emitted IL, otherwise false.
        /// </summary>
        /// <remarks>
        /// Using this property will get or set the <see cref="DataNodeFlags.NO_PUSH"/> flag.
        /// </remarks>
        public bool isNotPushed {
            readonly get => (flags & DataNodeFlags.NO_PUSH) != 0;
            set => flags = value ? flags | DataNodeFlags.NO_PUSH : flags & ~DataNodeFlags.NO_PUSH;
        }

        /// <summary>
        /// Sets the data type of this node to the type represented by the given class.
        /// </summary>
        /// <param name="klass">A <see cref="Class"/> representing the class to be set as
        /// the type of this node.</param>
        /// <remarks>
        /// This method will also set or unset the <see cref="DataNodeFlags.NOT_NULL"/> flag depending
        /// on whether null or undefined is representable by <paramref name="klass"/>.
        /// </remarks>
        public void setDataTypeFromClass(Class klass) {
            dataType = DataNodeTypeHelper.getDataTypeOfClass(klass);

            if (dataType == DataNodeType.OBJECT)
                constant = new DataNodeConstant(klass);

            isNotNull = DataNodeTypeHelper.isNonNullable(dataType);
        }

    }

    /// <summary>
    /// An enumeration of bit flags representing attributes of data nodes stored in
    /// the <see cref="DataNode.flags"/> field.
    /// </summary>
    [Flags]
    internal enum DataNodeFlags : ushort {

        /// <summary>
        /// The node is a phi node.
        /// </summary>
        PHI = 1,

        /// <summary>
        /// The node contains a compile-time constant value.
        /// </summary>
        CONSTANT = 2,

        /// <summary>
        /// The node represents the exception caught in a catch clause. Such nodes have no
        /// sources.
        /// </summary>
        EXCEPTION = 4,

        /// <summary>
        /// The node represents an object value that the compiler has determined to be definitely
        /// not null or undefined.
        /// </summary>
        NOT_NULL = 8,

        /// <summary>
        /// The node has a single data flow definition.
        /// </summary>
        HAS_SINGLE_DEF = 16,

        /// <summary>
        /// The node has a single data flow use.
        /// </summary>
        HAS_SINGLE_USE = 32,

        /// <summary>
        /// The node represents an argument to the method.
        /// </summary>
        ARGUMENT = 64,

        /// <summary>
        /// The node is a scope stack node pushed using the "pushwith" instruction.
        /// </summary>
        WITH_SCOPE = 128,

        /// <summary>
        /// The node is a source for a phi node.
        /// </summary>
        PHI_SOURCE = 256,

        /// <summary>
        /// The node is a stack node that will not be pushed onto the runtime stack.
        /// (For example, it is a constant operand to an operation evaluated at compile time.)
        /// </summary>
        NO_PUSH = 512,

        /// <summary>
        /// The node is a stack node whose value must be wrapped in an <see cref="OptionalParam{T}"/>
        /// after being pushed.
        /// </summary>
        PUSH_OPTIONAL_PARAM = 1024,

        /// <summary>
        /// The node is a stack node that must be converted to a string using <c>convert_s</c> semantics
        /// after being pushed.
        /// </summary>
        PUSH_CONVERT_STRING = 2048,

        /// <summary>
        /// Indicates that a local variable assignment is eligible for write-through optimization.
        /// This is used for local nodes that are a source to a single phi node and have no other
        /// uses.
        /// </summary>
        LOCAL_WRITE_THROUGH = 4096,

        /// <summary>
        /// Indicates that trait property binding on the node where the name is a multiname with a
        /// namespace set must be deferred to runtime, even when the node type is known (unless it
        /// is a final class).
        /// </summary>
        LATE_MULTINAME_TRAIT_BINDING = 8192,

    }

    /// <summary>
    /// Contains use-def information for a data node.
    /// </summary>
    internal struct DataNodeDUInfo {

        [StructLayout(LayoutKind.Explicit)]
        private struct _ArrayUnion {
            [FieldOffset(0)] public DataNodeOrInstrRef single;
            [FieldOffset(0)] public DynamicArrayPoolToken<DataNodeOrInstrRef> array;
        }

        private _ArrayUnion m_defs;
        private _ArrayUnion m_uses;

        // Static methods are used here because instance fields cannot be returned by ref
        // from a struct, and we can't return by value because for the (common) case of
        // a single def or use, single-element spans are created directly from
        // references (using MemoryMarshal) to avoid additional memory allocations.

        /// <summary>
        /// Gets a reference to the single definition for the node.
        /// Only applicable if the <see cref="DataNodeFlags.HAS_SINGLE_DEF"/> flag is set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref DataNodeOrInstrRef singleDef(ref DataNodeDUInfo info) => ref info.m_defs.single;

        /// <summary>
        /// Gets a reference to the single definition for the node.
        /// Only applicable if the <see cref="DataNodeFlags.HAS_SINGLE_DEF"/> flag is set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly DataNodeOrInstrRef singleDefReadonly(in DataNodeDUInfo info) => ref info.m_defs.single;

        /// <summary>
        /// Gets a reference to a pool-allocated array containing the definitions for the node. Only
        /// applicable if the <see cref="DataNodeFlags.HAS_SINGLE_DEF"/> flag is not set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref DynamicArrayPoolToken<DataNodeOrInstrRef> defs(ref DataNodeDUInfo info) => ref info.m_defs.array;

        /// <summary>
        /// Gets a reference to a pool-allocated array containing the definitions for the node. Only
        /// applicable if the <see cref="DataNodeFlags.HAS_SINGLE_DEF"/> flag is not set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly DynamicArrayPoolToken<DataNodeOrInstrRef> defsReadonly(in DataNodeDUInfo info) => ref info.m_defs.array;

        /// <summary>
        /// Gets a reference to the single use for the node.
        /// Only applicable if the <see cref="DataNodeFlags.HAS_SINGLE_USE"/> flag is set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref DataNodeOrInstrRef singleUse(ref DataNodeDUInfo info) => ref info.m_uses.single;

        /// <summary>
        /// Gets a reference to the single use for the node.
        /// Only applicable if the <see cref="DataNodeFlags.HAS_SINGLE_USE"/> flag is set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly DataNodeOrInstrRef singleUseReadonly(in DataNodeDUInfo info) => ref info.m_uses.single;

        /// <summary>
        /// Gets a reference to a pool-allocated array containing the uses for the node. Only
        /// applicable if the <see cref="DataNodeFlags.HAS_SINGLE_USE"/> flag is not set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref DynamicArrayPoolToken<DataNodeOrInstrRef> uses(ref DataNodeDUInfo info) => ref info.m_uses.array;

        /// <summary>
        /// Gets a reference to a pool-allocated array containing the uses for the node. Only
        /// applicable if the <see cref="DataNodeFlags.HAS_SINGLE_USE"/> flag is not set.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly DynamicArrayPoolToken<DataNodeOrInstrRef> usesReadonly(in DataNodeDUInfo info) => ref info.m_uses.array;

    }

    /// <summary>
    /// An enumeration containing the possible types for the slot that a data node occupies.
    /// </summary>
    internal enum DataNodeSlotKind : byte {
        /// <summary>
        /// Indicates that a data node lives on the stack.
        /// </summary>
        STACK,

        /// <summary>
        /// Indicates that a data node lives on the scope stack.
        /// </summary>
        SCOPE,

        /// <summary>
        /// Indicates that a data node lives in a local variable slot.
        /// </summary>
        LOCAL,
    }

    /// <summary>
    /// Represents a slot occupied by a data node.
    /// </summary>
    internal readonly struct DataNodeSlot : IEquatable<DataNodeSlot> {

        // Slot id in upper 30 bits (stack/scope/local limits are u30 in ABC, so this can be done)
        // Slot kind is stored in low 2 bits.
        private readonly uint m_kindAndId;

        /// <summary>
        /// Creates a new <see cref="DataNodeSlot"/> instance.
        /// </summary>
        /// <param name="kind">The slot kind (stack, scope stack or local).</param>
        /// <param name="id">The slot index.</param>
        public DataNodeSlot(DataNodeSlotKind kind, int id) {
            // An id of -1 is used in "dummy" nodes created by the IL emitter when emitting certain
            // instructions.
            Debug.Assert((id & 0xC0000000) == 0 || id == -1);
            m_kindAndId = (uint)kind | (uint)id << 2;
        }

        /// <summary>
        /// Returns index of the stack, scope stack or local slot represented by this
        /// <see cref="DataNodeSlot"/> instance.
        /// </summary>
        public int id => (int)(m_kindAndId >> 2);

        /// <summary>
        /// Returns a value from the <see cref="DataNodeSlotKind"/> enumeration representing the
        /// kind of slot (stack, scope stack or local) represented by this <see cref="DataNodeSlot"/>
        /// instance.
        /// </summary>
        public DataNodeSlotKind kind => (DataNodeSlotKind)(m_kindAndId & 3);

        /// <summary>
        /// Returns a value indicating whether this <see cref="DataNodeSlot"/> is equal to
        /// another <see cref="DataNodeSlot"/> instance.
        /// </summary>
        /// <param name="other">The <see cref="DataNodeSlot"/> to check with this instance for equality.</param>
        /// <returns>True if this instance is equal to <paramref name="other"/>, otherwise false.</returns>
        public bool Equals(DataNodeSlot other) => m_kindAndId == other.m_kindAndId;

        /// <summary>
        /// Returns a value indicating whether this <see cref="DataNodeSlot"/> instance is equal to
        /// another object.
        /// </summary>
        /// <param name="other">The object to check with this instance for equality.</param>
        /// <returns>True if this instance is equal to <paramref name="other"/>, otherwise false.</returns>
        public override bool Equals(object other) => other is DataNodeSlot slot && m_kindAndId == slot.m_kindAndId;

        /// <summary>
        /// Returns a hash code for a <see cref="DataNodeSlot"/> instance.
        /// </summary>
        /// <returns>A hash code for a <see cref="DataNodeSlot"/> instance.</returns>
        public override int GetHashCode() => (int)m_kindAndId;

        /// <summary>
        /// Determines whether two <see cref="DataNodeSlot"/> instances are equal.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>True if <paramref name="x"/> is equal to <paramref name="y"/>, otherwise false.</returns>
        public static bool operator ==(DataNodeSlot x, DataNodeSlot y) => x.m_kindAndId == y.m_kindAndId;

        /// <summary>
        /// Determines whether two <see cref="DataNodeSlot"/> instances are not equal.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <returns>True if <paramref name="x"/> is not equal to <paramref name="y"/>, otherwise false.</returns>
        public static bool operator !=(DataNodeSlot x, DataNodeSlot y) => x.m_kindAndId != y.m_kindAndId;

        /// <summary>
        /// Returns a string representation of this <see cref="DataNodeOrInstrRef"/> instance.
        /// </summary>
        /// <returns>A string representation of this <see cref="DataNodeOrInstrRef"/> instance.</returns>
        public override string ToString() {
            string idStr = id.ToString(CultureInfo.InvariantCulture);
            if (kind == DataNodeSlotKind.STACK)
                return "stack(" + idStr + ")";
            if (kind == DataNodeSlotKind.STACK)
                return "scope(" + idStr + ")";
            if (kind == DataNodeSlotKind.STACK)
                return "local(" + idStr + ")";
            return "";
        }

    }

    /// <summary>
    /// Stores constant data for stack and local variable nodes. Constant data
    /// includes the values of constants, or the class of an object-typed node.
    /// </summary>
    /// <remarks>
    /// A <see cref="DataNodeConstant"/> instance can hold a value of one of the following types:
    /// integer, double, string, boolean, <see cref="Namespace"/>, <see cref="QName"/>,
    /// <see cref="Class"/> or <see cref="MethodTrait"/>.
    /// A <see cref="DataNodeConstant"/> instance does not contain any type information; this
    /// must be obtained from the node that contains the constant data.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit)]
    internal readonly struct DataNodeConstant : IEquatable<DataNodeConstant> {

        [FieldOffset(0)]
        private readonly int m_int32;

        [FieldOffset(0)]
        private readonly long m_int64;

        [FieldOffset(0)]
        private readonly double m_double;

        [FieldOffset(8)]
        private readonly object m_object;

        /// <summary>
        /// Creates a new instance of <see cref="DataNodeConstant"/> having an integer value.
        /// </summary>
        /// <param name="value">The value.</param>
        public DataNodeConstant(int value) : this() => m_int32 = value;

        /// <summary>
        /// Creates a new instance of <see cref="DataNodeConstant"/> having a floating-point value.
        /// </summary>
        /// <param name="value">The value.</param>
        public DataNodeConstant(double value) : this() => m_double = value;

        /// <summary>
        /// Creates a new instance of <see cref="DataNodeConstant"/> having a boolean value.
        /// </summary>
        /// <param name="value">The value.</param>
        public DataNodeConstant(bool value) : this() => m_int32 = value ? 1 : 0;

        /// <summary>
        /// Creates a new instance of <see cref="DataNodeConstant"/> having a string value.
        /// </summary>
        /// <param name="value">The value.</param>
        public DataNodeConstant(string value) : this() => m_object = value;

        /// <summary>
        /// Creates a new instance of <see cref="DataNodeConstant"/> having an instance of
        /// <see cref="Namespace"/> as its value.
        /// </summary>
        /// <param name="value">The value.</param>
        public DataNodeConstant(in Namespace value) : this() => m_object = (object)value;

        /// <summary>
        /// Creates a new instance of <see cref="DataNodeConstant"/> having an instance of
        /// <see cref="QName"/> as its value.
        /// </summary>
        /// <param name="value">The value.</param>
        public DataNodeConstant(in QName value) : this() => m_object = (object)value;

        /// <summary>
        /// Creates a new instance of <see cref="DataNodeConstant"/> having an instance of
        /// <see cref="Class"/> as its value.
        /// </summary>
        /// <param name="value">The value.</param>
        public DataNodeConstant(Class value) : this() => m_object = value;

        /// <summary>
        /// Creates a new instance of <see cref="DataNodeConstant"/> having an instance of
        /// <see cref="MethodTrait"/> as its value.
        /// </summary>
        /// <param name="value">The value.</param>
        public DataNodeConstant(MethodTrait value) : this() => m_object = value;

        /// <summary>
        /// Gets the integer value of the constant data.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int intValue => m_int32;

        /// <summary>
        /// Gets the boolean value of the constant data.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool boolValue => m_int32 != 0;

        /// <summary>
        /// Gets the floating-point value of the constant data.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public double doubleValue => m_double;

        /// <summary>
        /// Gets or sets the string value of the constant data.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string stringValue => (string)m_object;

        /// <summary>
        /// Gets or sets the <see cref="Namespace"/> value of the constant data.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Namespace namespaceValue => (Namespace)m_object;

        /// <summary>
        /// Gets or sets the <see cref="QName"/> value of the constant data.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public QName qnameValue => (QName)m_object;

        /// <summary>
        /// Gets or sets the <see cref="Class"/> value of the constant data.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Class classValue => (Class)m_object;

        /// <summary>
        /// Gets or sets the <see cref="MethodTrait"/> value of the constant data.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MethodTrait methodValue => (MethodTrait)m_object;

        /// <summary>
        /// Returns a value indicating whether this <see cref="DataNodeConstant"/> instance is
        /// equal to another <see cref="DataNodeConstant"/> instance.
        /// </summary>
        /// <param name="other">The <see cref="DataNodeConstant"/> to compare with this instance.</param>
        /// <returns>True if this instance is equal to <paramref name="other"/>, otherwise
        /// false.</returns>
        /// <remarks>
        /// Integer, double and boolean values are compared by value; all other types are compared
        /// by reference.
        /// </remarks>
        public bool Equals(DataNodeConstant other) => this == other;

        /// <summary>
        /// Returns a value indicating whether this <see cref="DataNodeConstant"/> instance is
        /// equal to another object.
        /// </summary>
        /// <param name="other">The object to compare with this <see cref="DataNodeConstant"/> instance.</param>
        /// <returns>True if this instance is equal to <paramref name="other"/>, otherwise
        /// false.</returns>
        /// <remarks>
        /// Integer, double and boolean values are compared by value; all other types are compared
        /// by reference.
        /// </remarks>
        public override bool Equals(object other) => other is DataNodeConstant imm && this == imm;

        /// <summary>
        /// Returns a hash code for this <see cref="DataNodeConstant"/> instance.
        /// </summary>
        /// <returns>A hash code for this <see cref="DataNodeConstant"/> instance.</returns>
        public override int GetHashCode() =>
            m_int64.GetHashCode() ^ ((m_object != null) ? RuntimeHelpers.GetHashCode(m_object) : 0);

        /// <summary>
        /// Returns a value indicating whether two <see cref="DataNodeConstant"/> instances are
        /// equal.
        /// </summary>
        /// <param name="x">The first <see cref="DataNodeConstant"/> instance.</param>
        /// <param name="y">The second <see cref="DataNodeConstant"/> instance.</param>
        /// <returns>True if <paramref name="x"/> is equal to <paramref name="y"/>, otherwise
        /// false.</returns>
        /// <remarks>
        /// Integer, double and boolean values are compared by value; all other types are compared
        /// by reference.
        /// </remarks>
        public static bool operator ==(in DataNodeConstant x, in DataNodeConstant y) =>
            x.m_int64 == y.m_int64 && x.m_object == y.m_object;

        /// <summary>
        /// Returns a value indicating whether two <see cref="DataNodeConstant"/> instances are
        /// not equal.
        /// </summary>
        /// <param name="x">The first <see cref="DataNodeConstant"/> instance.</param>
        /// <param name="y">The second <see cref="DataNodeConstant"/> instance.</param>
        /// <returns>True if <paramref name="x"/> is not equal to <paramref name="y"/>, otherwise
        /// false.</returns>
        /// <remarks>
        /// Integer, double and boolean values are compared by value; all other types are compared
        /// by reference.
        /// </remarks>
        public static bool operator !=(in DataNodeConstant x, in DataNodeConstant y) => !(x == y);

    }

}
