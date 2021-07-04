using Mariana.AVM2.Core;
using Mariana.Common;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Enumerates the basic data types used for stack, scope stack and local variable nodes by
    /// the compiler.
    /// </summary>
    internal enum DataNodeType : byte {

        /// <summary>
        /// The element does not have a type assigned.
        /// </summary>
        UNKNOWN,

        /// <summary>
        /// The element is of the "any" type.
        /// </summary>
        ANY,

        /// <summary>
        /// The element is of the Object type.
        /// </summary>
        OBJECT,

        /// <summary>
        /// The element is of the integer type.
        /// </summary>
        INT,

        /// <summary>
        /// The element is of the unsigned integer type.
        /// </summary>
        UINT,

        /// <summary>
        /// The element is of the Number (floating point) type.
        /// </summary>
        NUMBER,

        /// <summary>
        /// The element is of the boolean type.
        /// </summary>
        BOOL,

        /// <summary>
        /// The element is of the string type.
        /// </summary>
        STRING,

        /// <summary>
        /// The element is a constant null.
        /// </summary>
        NULL,

        /// <summary>
        /// The element is a constant undefined.
        /// </summary>
        UNDEFINED,

        /// <summary>
        /// The element is of the Namespace type.
        /// </summary>
        NAMESPACE,

        /// <summary>
        /// The element is of the QName type.
        /// </summary>
        QNAME,

        /// <summary>
        /// The element is a class constant.
        /// </summary>
        CLASS,

        /// <summary>
        /// The element is a function constant.
        /// </summary>
        FUNCTION,

        /// <summary>
        /// The element is the constant global object.
        /// </summary>
        GLOBAL,

        /// <summary>
        /// The element is the "this" argument of the method.
        /// </summary>
        THIS,

        /// <summary>
        /// The element is the "rest" argument of the method.
        /// </summary>
        REST,

    }

    internal static class DataNodeTypeHelper {

        private const int INTEGER_MASK =
              (1 << (int)DataNodeType.INT)
            | (1 << (int)DataNodeType.UINT);

        private const int NUMERIC_MASK =
              (1 << (int)DataNodeType.INT)
            | (1 << (int)DataNodeType.UINT)
            | (1 << (int)DataNodeType.NUMBER);

        private const int PRIMITIVE_MASK =
              NUMERIC_MASK
            | (1 << (int)DataNodeType.NUMBER)
            | (1 << (int)DataNodeType.BOOL)
            | (1 << (int)DataNodeType.STRING);

        private const int NON_NULLABLE_MASK =
              NUMERIC_MASK
            | (1 << (int)DataNodeType.BOOL)
            | (1 << (int)DataNodeType.THIS)
            | (1 << (int)DataNodeType.REST)
            | (1 << (int)DataNodeType.CLASS)
            | (1 << (int)DataNodeType.GLOBAL);

        private const int ANY_UNDEFINED_MASK =
            (1 << (int)DataNodeType.ANY)
            | (1 << (int)DataNodeType.UNDEFINED);

        private const int CONSTANT_TYPE_MASK =
            (1 << (int)DataNodeType.NULL)
            | (1 << (int)DataNodeType.UNDEFINED)
            | (1 << (int)DataNodeType.CLASS)
            | (1 << (int)DataNodeType.FUNCTION)
            | (1 << (int)DataNodeType.GLOBAL);

        private const int OBJECT_TYPE_MASK =
            ~(PRIMITIVE_MASK | ANY_UNDEFINED_MASK | (1 << (int)DataNodeType.REST) | (1 << (int)DataNodeType.UNKNOWN));

        private const int STRING_NULL_MASK =
            (1 << (int)DataNodeType.STRING) | (1 << (int)DataNodeType.NULL);

        private static DynamicArray<Class> s_elementTypeToClassMap = new DynamicArray<Class>(32, true) {
            [(int)DataNodeType.ANY] = null,
            [(int)DataNodeType.UNDEFINED] = null,
            [(int)DataNodeType.NULL] = Class.fromType<ASObject>(),
            [(int)DataNodeType.OBJECT] = Class.fromType<ASObject>(),
            [(int)DataNodeType.BOOL] = Class.fromType<bool>(),
            [(int)DataNodeType.INT] = Class.fromType<int>(),
            [(int)DataNodeType.UINT] = Class.fromType<uint>(),
            [(int)DataNodeType.NUMBER] = Class.fromType<double>(),
            [(int)DataNodeType.STRING] = Class.fromType<string>(),
            [(int)DataNodeType.NAMESPACE] = Class.fromType<ASNamespace>(),
            [(int)DataNodeType.QNAME] = Class.fromType<ASQName>(),
            [(int)DataNodeType.CLASS] = Class.fromType<ASClass>(),
            [(int)DataNodeType.FUNCTION] = Class.fromType<ASFunction>(),
            [(int)DataNodeType.GLOBAL] = Class.fromType<ASObject>(),
            [(int)DataNodeType.THIS] = Class.fromType<ASObject>(),
            [(int)DataNodeType.REST] = Class.fromType<ASArray>(),
        };

        /// <summary>
        /// Returns a value indicating whether the given data type is an integral type.
        /// </summary>
        /// <param name="type">A value from the <see cref="DataNodeType"/> enumeration.</param>
        /// <returns>True if <paramref name="type"/> is an integral type, otherwise false.</returns>
        public static bool isInteger(DataNodeType type) => ((1 << (int)type) & INTEGER_MASK) != 0;

        /// <summary>
        /// Returns a value indicating whether the given data type is a numeric type.
        /// </summary>
        /// <param name="type">A value from the <see cref="DataNodeType"/> enumeration.</param>
        /// <returns>True if <paramref name="type"/> is a numeric type, otherwise false.</returns>
        public static bool isNumeric(DataNodeType type) => ((1 << (int)type) & NUMERIC_MASK) != 0;

        /// <summary>
        /// Returns a value indicating whether the given data type is a primitive type.
        /// </summary>
        /// <param name="type">A value from the <see cref="DataNodeType"/> enumeration.</param>
        /// <returns>True if <paramref name="type"/> is a primitive type, otherwise false.</returns>
        public static bool isPrimitive(DataNodeType type) => ((1 << (int)type) & PRIMITIVE_MASK) != 0;

        /// <summary>
        /// Returns a value indicating whether the given data type can never have a null or
        /// undefined value.
        /// </summary>
        /// <param name="type">A value from the <see cref="DataNodeType"/> enumeration.</param>
        /// <returns>True if <paramref name="type"/> is a non-nullable type, otherwise false.</returns>
        public static bool isNonNullable(DataNodeType type) => ((1 << (int)type) & NON_NULLABLE_MASK) != 0;

        /// <summary>
        /// Returns a value indicating whether the given data type is either <see cref="DataNodeType.ANY"/>
        /// or <see cref="DataNodeType.UNDEFINED"/>.
        /// </summary>
        /// <param name="type">A value from the <see cref="DataNodeType"/> enumeration.</param>
        /// <returns>True if <paramref name="type"/> is either <see cref="DataNodeType.ANY"/>
        /// or <see cref="DataNodeType.UNDEFINED"/>, otherwise false.</returns>
        public static bool isAnyOrUndefined(DataNodeType type) => ((1 << (int)type) & ANY_UNDEFINED_MASK) != 0;

        /// <summary>
        /// Returns a value indicating whether the given data type is an object type.
        /// </summary>
        /// <param name="type">A value from the <see cref="DataNodeType"/> enumeration.</param>
        /// <returns>True if <paramref name="type"/> is an object type, otherwise false.</returns>
        public static bool isObjectType(DataNodeType type) => ((1 << (int)type) & OBJECT_TYPE_MASK) != 0;

        /// <summary>
        /// Returns a value indicating whether the given data type can only hold constant values.
        /// </summary>
        /// <param name="type">A value from the <see cref="DataNodeType"/> enumeration.</param>
        /// <returns>True if <paramref name="type"/> is a constant type, otherwise false.</returns>
        public static bool isConstantType(DataNodeType type) => ((1 << (int)type) & CONSTANT_TYPE_MASK) != 0;

        /// <summary>
        /// Returns a value indicating whether the given data type is <see cref="DataNodeType.STRING"/>
        /// or <see cref="DataNodeType.NULL"/>.
        /// </summary>
        /// <param name="type">A value from the <see cref="DataNodeType"/> enumeration.</param>
        /// <returns>True if <paramref name="type"/> is <see cref="DataNodeType.STRING"/>
        /// or <see cref="DataNodeType.NULL"/>, otherwise false.</returns>
        public static bool isStringOrNull(DataNodeType type) => ((1 << (int)type) & STRING_NULL_MASK) != 0;

        /// <summary>
        /// Returns the class corresponding to the given data type.
        /// </summary>
        /// <param name="type">A value from the <see cref="DataNodeType"/> enumeration.</param>
        /// <returns>A <see cref="Class"/> instance representing the class corresponding to <paramref name="type"/>,
        /// or null if there is no corresponding class.</returns>
        public static Class getClass(DataNodeType type) =>
            ((uint)type < (uint)s_elementTypeToClassMap.length) ? s_elementTypeToClassMap[(int)type] : null;

        /// <summary>
        /// Returns the data type corresponding to the given class.
        /// </summary>
        /// <param name="klass">A <see cref="Class"/> instance representing the class.</param>
        /// <returns>A value from the <see cref="DataNodeType"/> enumeration that corresponds to
        /// the class represented by <paramref name="klass"/>.</returns>
        public static DataNodeType getDataTypeOfClass(Class klass) {
            if (klass == null)
                return DataNodeType.ANY;

            return klass.tag switch {
                ClassTag.INT => DataNodeType.INT,
                ClassTag.UINT => DataNodeType.UINT,
                ClassTag.NUMBER => DataNodeType.NUMBER,
                ClassTag.STRING => DataNodeType.STRING,
                ClassTag.BOOLEAN => DataNodeType.BOOL,
                ClassTag.NAMESPACE => DataNodeType.NAMESPACE,
                ClassTag.QNAME => DataNodeType.QNAME,
                _ => DataNodeType.OBJECT,
            };
        }

    }

}
