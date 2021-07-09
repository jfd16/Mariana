namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Used by the compiler to represent the type of a comparison operation to be
    /// performed with respect to the types of its operands.
    /// </summary>
    internal enum ComparisonType : byte {

        /// <summary>
        /// The comparison type has not yet been determined.
        /// </summary>
        NONE,

        /// <summary>
        /// The comparison is an integer comparison. Operands must be converted to the
        /// signed integer type.
        /// </summary>
        INT,

        /// <summary>
        /// The comparison is an unsigned integer comparison. Operands must be converted to the
        /// unsigned integer type.
        /// </summary>
        UINT,

        /// <summary>
        /// The comparison is a floating-point numeric comparison. Operands must be converted to the
        /// Number type.
        /// </summary>
        NUMBER,

        /// <summary>
        /// The comparison is a string comparison. Operands must be converted to strings.
        /// </summary>
        STRING,

        /// <summary>
        /// The comparison is an equality or inequality involving Namespace operands.
        /// </summary>
        NAMESPACE,

        /// <summary>
        /// The comparison is an equality or inequality involving QName operands.
        /// </summary>
        QNAME,

        /// <summary>
        /// Operands must be coerced to the Object type and compared by reference.
        /// </summary>
        OBJ_REF,

        /// <summary>
        /// Operands must be coerced to the Object type and the runtime comparison function
        /// must be invoked.
        /// </summary>
        OBJECT,

        /// <summary>
        /// Operands must be coerced to the "any" type and the runtime comparison function
        /// must be invoked.
        /// </summary>
        ANY,

        /// <summary>
        /// An equality or inequality comparison involving integer operands with constant zero
        /// as the left operand.
        /// </summary>
        INT_ZERO_L,

        /// <summary>
        /// An equality or inequality comparison of an integer with constant zero
        /// as the right operand.
        /// </summary>
        INT_ZERO_R,

        /// <summary>
        /// A reference equality or inequality comparison of objects or strings with constant null as
        /// the left operand.
        /// </summary>
        OBJ_NULL_L,

        /// <summary>
        /// A reference equality or inequality comparison of objects or strings with constant null as
        /// the right operand.
        /// </summary>
        OBJ_NULL_R,

        /// <summary>
        /// A reference equality or inequality comparison of objects of the "any" type with
        /// a constant undefined as the left operand.
        /// </summary>
        ANY_UNDEF_L,

        /// <summary>
        /// A reference equality or inequality comparison of objects of the "any" type with
        /// a constant undefined as the left operand.
        /// </summary>
        ANY_UNDEF_R,

        /// <summary>
        /// An equality or inequality comparison of boolean values with the left operand being a constant.
        /// </summary>
        BOOL_CONST_L,

        /// <summary>
        /// An equality or inequality comparison of boolean values with the right operand being a constant.
        /// </summary>
        BOOL_CONST_R,

        /// <summary>
        /// Used for the intrinsic expression patterns
        /// <c>int op str.charCodeAt(int)</c> and <c>const_str op str.charAt(int)</c>
        /// </summary>
        STR_CHARAT_L,

        /// <summary>
        /// Used for the intrinsic expression patterns
        /// <c>str.charCodeAt(int) op int</c> and <c>str.charAt(int) op const_str</c>
        /// </summary>
        STR_CHARAT_R,

        /// <summary>
        /// The comparison involves constant operands and has been evaluated at compile time.
        /// </summary>
        CONSTANT,

    }

}
