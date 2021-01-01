namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Specifies the situations in which the compiler should use integer arithmetic
    /// for certain floating-point arithmetic instructions where the operands are integers.
    /// </summary>
    /// <seealso cref="ScriptCompileOptions.integerArithmeticMode"/>
    public enum IntegerArithmeticMode : byte {

        /// <summary>
        /// The default level. Integer arithmetic is used when the operands are integers
        /// and the result is implicitly or explicitly converted to an integer type.
        /// </summary>
        DEFAULT,

        /// <summary>
        /// All arithmetic will be done in floating point except where integer arithmetic
        /// opcodes are explicitly used.
        /// </summary>
        EXPLICIT_ONLY,

        /// <summary>
        /// Always use integer arithmetic whenever the operands are integers.
        /// This may result in behaviour that does not conform to the ECMA-262 specification and/or
        /// is inconsistent with that of Flash Player and AIR.
        /// </summary>
        AGGRESSIVE,

    }

}
