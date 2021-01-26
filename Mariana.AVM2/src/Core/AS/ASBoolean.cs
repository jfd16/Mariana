using System;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A Boolean represents one of two possible values, true or false.
    /// </summary>
    ///
    /// <remarks>
    /// This is a boxed representation of the primitive type <see cref="System.Boolean"/>. It is
    /// only used when a boxing conversion is required. This type should not be used for any other
    /// purpose, other than for using its static properties or methods of for type checking of
    /// objects.
    /// </remarks>
    [AVM2ExportClass(name = "Boolean", hasPrototypeMethods = true)]
    [AVM2ExportClassInternal(tag = ClassTag.BOOLEAN, primitiveType = typeof(bool))]
    sealed public class ASBoolean : ASObject {

        /// <summary>
        /// The value of the "length" property of the AS3 Boolean class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        private const string TRUE_STRING = "true";
        private const string FALSE_STRING = "false";

        // The boxed instances of true and false
        internal static LazyInitObject<(ASBoolean, ASBoolean)> s_boxedValues =
            new LazyInitObject<(ASBoolean, ASBoolean)>(() => (new ASBoolean(false), new ASBoolean(true)));

        private static LazyInitObject<Class> s_lazyClass = new LazyInitObject<Class>(
            () => Class.fromType(typeof(ASBoolean)),
            recursionHandling: LazyInitRecursionHandling.RECURSIVE_CALL
        );

        private bool m_value;

        private ASBoolean(bool v) : base(s_lazyClass.value) {
            m_value = v;
        }

        /// <summary>
        /// Converts the current instance to a Boolean value.
        /// </summary>
        /// <returns>The Boolean value</returns>
        protected private override bool AS_coerceBoolean() => m_value;

        /// <summary>
        /// Converts the current instance to an integer value.
        /// </summary>
        /// <returns>The integer value.</returns>
        protected override int AS_coerceInt() => m_value ? 1 : 0;

        /// <summary>
        /// Converts the current instance to a floating-point value.
        /// </summary>
        /// <returns>The floating-point value.</returns>
        protected override double AS_coerceNumber() => m_value ? 1 : 0;

        /// <summary>
        /// Converts the current instance to a string value.
        /// </summary>
        /// <returns>The string value.</returns>
        protected override string AS_coerceString() => m_value ? TRUE_STRING : FALSE_STRING;

        /// <summary>
        /// Converts the current instance to an unsigned integer value.
        /// </summary>
        /// <returns>The unsigned integer value.</returns>
        protected override uint AS_coerceUint() => m_value ? 1u : 0u;

        /// <summary>
        /// Returns the string representation of the object.
        /// </summary>
        /// <returns>The string representation of the object.</returns>
        ///
        /// <remarks>
        /// This method is exported to the AVM2 with the name <c>toString</c>, but must be called
        /// from .NET code with the name <c>AS_toString</c> to avoid confusion with the
        /// <see cref="Object.ToString" qualifyHint="true"/> method.
        /// </remarks>
        [AVM2ExportTrait(name = "toString", nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod(name = "toString")]
        public new string AS_toString() => m_value ? TRUE_STRING : FALSE_STRING;

        /// <summary>
        /// Returns the primitive type representation of the object.
        /// </summary>
        /// <returns>A primitive representation of the object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public new bool valueOf() => m_value;

        /// <summary>
        /// Converts a Boolean value to a string.
        /// </summary>
        /// <param name="b">The Boolean value</param>
        /// <returns>A string representation of the boolean value.</returns>
        public static string AS_convertString(bool b) => b ? TRUE_STRING : FALSE_STRING;

        /// <summary>
        /// Returns the given argument. This is used by the ABC compiler for calls to the
        /// <c>valueOf</c> method on Boolean values.
        /// </summary>
        /// <param name="b">The argument.</param>
        /// <returns>The value of <paramref name="b"/>.</returns>
        [AVM2ExportPrototypeMethod]
        public static bool valueOf(bool b) => b;

        /// <summary>
        /// Creates a boxed object from a Boolean value.
        /// </summary>
        /// <param name="value">The value from which to create a boxed object.</param>
        /// <returns>The boxed <see cref="ASObject"/> instance representing the given Boolean value.</returns>
        internal static ASObject box(bool value) => value ? s_boxedValues.value.Item2 : s_boxedValues.value.Item1;

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABCIL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) =>
            ASAny.AS_fromBoolean(args.Length != 0 && ASAny.AS_toBoolean(args[0]));

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABCIL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_CONSTRUCT(ReadOnlySpan<ASAny> args) => __AS_INVOKE(args);

    }

}
