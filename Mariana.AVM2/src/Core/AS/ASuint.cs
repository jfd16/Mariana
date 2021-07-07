using System;
using System.Globalization;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The uint class represents a 32-bit unsigned integer.
    /// </summary>
    ///
    /// <remarks>
    /// This is a boxed representation of the primitive type <see cref="UInt32"/>. It is only used
    /// when a boxing conversion is required. This type should not be used for any other purpose,
    /// other than for using its static properties or methods of for type checking of objects.
    /// Methods of the AS3 <c>uint</c> class are available as static methods taking a
    /// primitive <see cref="UInt32"/> argument.
    /// </remarks>
    [AVM2ExportClass(name = "uint", hasPrototypeMethods = true)]
    [AVM2ExportClassInternal(tag = ClassTag.UINT, primitiveType = typeof(uint), usePrototypeOf = typeof(ASNumber))]
    sealed public class ASuint : ASObject {

        /// <summary>
        /// The value of the "length" property of the AS3 uint class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// The maximum possible value for a 32-bit unsigned integer.
        /// </summary>
        [AVM2ExportTrait]
        public const uint MAX_VALUE = 4294967295;

        /// <summary>
        /// The minimum possible value for a 32-bit unsigned integer.
        /// </summary>
        [AVM2ExportTrait]
        public const uint MIN_VALUE = 0;

        /// <summary>
        /// The range of cached objects. Caches objects are used to avoid expensive boxing conversions
        /// for values within this range. Values from 0 to CACHE_RANGE will be cached.
        /// </summary>
        private const uint CACHE_RANGE = 256;

        /// <summary>
        /// The array containing the cached objects.
        /// </summary>
        private static LazyInitObject<ASObject[]> s_lazyCachedValues =
            new LazyInitObject<ASObject[]>(() => _prepareCachedValues());

        /// <summary>
        /// The range of cached strings used when converting integers to strings. Strings for the
        /// values from 0 to CACHE_RANGE will be cached.
        /// </summary>
        private const int STRING_CACHE_RANGE = 256;

        /// <summary>
        /// The array containing the cached strings returned by <see cref="AS_convertString"/>.
        /// </summary>
        private static readonly string[] s_cachedStrings = _prepareCachedStrings();

        private static LazyInitObject<Class> s_lazyClass = new LazyInitObject<Class>(
            () => Class.fromType(typeof(ASuint))!,
            recursionHandling: LazyInitRecursionHandling.RECURSIVE_CALL
        );

        private readonly uint m_value;

        private ASuint(uint v) : base(s_lazyClass.value) {
            m_value = v;
        }

        /// <summary>
        /// Converts the current instance to a Boolean value.
        /// </summary>
        /// <returns>The Boolean value</returns>
        protected private override bool AS_coerceBoolean() => m_value != 0;

        /// <summary>
        /// Converts the current instance to an integer value.
        /// </summary>
        /// <returns>The integer value.</returns>
        protected override int AS_coerceInt() => (int)m_value;

        /// <summary>
        /// Converts the current instance to a floating-point number value.
        /// </summary>
        /// <returns>The floating-point number value.</returns>
        protected override double AS_coerceNumber() => (double)m_value;

        /// <summary>
        /// Converts the current instance to a string value.
        /// </summary>
        /// <returns>The string value.</returns>
        protected override string AS_coerceString() => AS_convertString(m_value);

        /// <summary>
        /// Converts the current instance to an unsigned integer value.
        /// </summary>
        /// <returns>The unsigned integer value.</returns>
        protected override uint AS_coerceUint() => m_value;

        /// <summary>
        /// Returns the string representation of the number with a fixed number of significant digits.
        /// Numbers which are too large to represent in the given number of significant digits are
        /// represented in scientific notation.
        /// </summary>
        ///
        /// <param name="p">The number of significant digits (between 1 and 21 inclusive).</param>
        /// <returns>The string representation of the number with the specified number of significant
        /// digits.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1002: If the precision is less than 1 or greater than 21.</description></item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string toPrecision(int p = 0) => toPrecision(m_value, p);

        /// <summary>
        /// Returns the string representation of the number in fixed-point notation.
        /// </summary>
        /// <param name="p">The number of decimal places (between 0 and 20 inclusive).</param>
        /// <returns>The string representation of the number in fixed-point notation.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1002: If the precision is less than 0 or greater than 20.</description></item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string toFixed(int p = 0) => toFixed(m_value, p);

        /// <summary>
        /// Returns the string representation of the number in scientific (exponential) notation.
        /// </summary>
        /// <param name="p">The number of decimal places in the mantissa (between 0 and 20
        /// inclusive).</param>
        /// <returns>The string representation of the number in scientific notation.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1002: If the precision is less than 0 or greater than 20.</description></item>
        /// </list>
        /// </exception>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string toExponential(int p = 0) => toExponential(m_value, p);

        /// <summary>
        /// Returns a string representation of the integer value in the user's locale.
        /// </summary>
        /// <returns>A string representation of the integer value in the user's locale.</returns>
        //[AVM2ExportTrait(nsName = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public new string toLocaleString() => toLocaleString(m_value);

        /// <summary>
        /// Returns a string representation of the number in the given radix (base).
        /// </summary>
        /// <param name="radix">The base in which the string representation of the number must be
        /// returned. This must be a number from 2 to 36 inclusive.</param>
        /// <returns>The string representation of the number in the given base.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1003: If the radix is less than 2 or greater than 36.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method is exported to the AVM2 with the name <c>toString</c>, but must be called
        /// from .NET code with the name <c>AS_toString</c> to avoid confusion with the
        /// <see cref="Object.ToString" qualifyHint="true"/> method.
        /// </remarks>
        [AVM2ExportTrait(name = "toString", nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod(name = "toString")]
        public string AS_toString(int radix = 10) => toString(m_value, radix);

        /// <summary>
        /// Returns the primitive type representation of the object.
        /// </summary>
        /// <returns>A primitive representation of the object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public new uint valueOf() => m_value;

        /// <summary>
        /// Returns a string representation of the number in base 10.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <returns>The string representation of the number in base 10.</returns>
        public static string AS_convertString(uint num) {
            var cache = s_cachedStrings;
            return (num < (uint)cache.Length) ? cache[(int)num] : num.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Returns the string representation of the number in fixed-point notation.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <param name="precision">The number of decimal places (between 0 and 20
        /// inclusive).</param>
        /// <returns>The string representation of the number in fixed-point notation.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1002: If the precision is less than 0 or greater than 20.</description></item>
        /// </list>
        /// </exception>
        public static string toFixed(uint num, int precision) {
            if ((uint)precision > 20)
                throw ErrorHelper.createError(ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE);

            Span<char> buffer = stackalloc char[32];
            num.TryFormat(buffer, out int bufferLength, default, CultureInfo.InvariantCulture);

            if (precision > 0) {
                buffer[bufferLength] = '.';
                buffer.Slice(bufferLength + 1, precision).Fill('0');
                bufferLength += precision + 1;
            }

            return new string(buffer.Slice(0, bufferLength));
        }

        /// <summary>
        /// Returns the string representation of the number in scientific (exponential) notation.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <param name="precision">The number of decimal places in the mantissa (between 0 and 20
        /// inclusive).</param>
        /// <returns>The string representation of the number in scientific notation.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1002: If the precision is less than 0 or greater than 20.</description></item>
        /// </list>
        /// </exception>
        public static string toExponential(uint num, int precision) => ASNumber.toExponential(num, precision);

        /// <summary>
        /// Returns the string representation of the number with a fixed number of significant digits.
        /// Numbers that are too large to represent in the given number of significant digits are
        /// represented in scientific notation.
        /// </summary>
        ///
        /// <param name="num">The number.</param>
        /// <param name="precision">The number of significant digits (between 1 and 21
        /// inclusive).</param>
        /// <returns>The string representation of the number with the specified number of significant
        /// digits.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1002: If the precision is less than 1 or greater than 21.</description></item>
        /// </list>
        /// </exception>
        public static string toPrecision(uint num, int precision) => ASNumber.toPrecision(num, precision);

        /// <summary>
        /// Returns a string representation of the number in the given base.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <param name="radix">The base between 2 and 36.</param>
        /// <returns>The string representation of the number in the given base.</returns>
        public static string toString(uint num, int radix) {
            if (radix < 2 || radix > 36)
                throw ErrorHelper.createError(ErrorCode.NUMBER_RADIX_OUT_OF_RANGE, radix);
            return (radix == 10) ? AS_convertString(num) : NumberFormatHelper.uintToString(num, radix);
        }

        /// <summary>
        /// Returns the given argument. This is used by the ABC compiler for calls to the
        /// <c>valueOf</c> method on unsigned integer values.
        /// </summary>
        /// <param name="num">The argument.</param>
        /// <returns>The value of <paramref name="num"/>.</returns>
        public static uint valueOf(uint num) => num;

        /// <summary>
        /// Returns the string representation of a number in the user's locale.
        /// </summary>
        /// <param name="num">The number to convert to a string.</param>
        /// <returns>The string representation of the number in the user's locale.</returns>
        public static string toLocaleString(uint num) => num.ToString(CultureInfo.CurrentCulture);

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABC to IL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) =>
            ASAny.AS_fromUint((args.Length == 0) ? 0u : ASAny.AS_toUint(args[0]));

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABC to IL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_CONSTRUCT(ReadOnlySpan<ASAny> args) => __AS_INVOKE(args);

        /// <summary>
        /// Creates a boxed object from an unsigned integer value.
        /// </summary>
        /// <param name="x">The value to be boxed.</param>
        internal static ASObject box(uint x) {
            var cache = s_lazyCachedValues.value;
            return (x < (uint)cache.Length) ? cache[x] : new ASuint(x);
        }

        private static ASObject[] _prepareCachedValues() {
            ASObject[] cachedValues = new ASObject[CACHE_RANGE + 1];
            for (uint i = 0; i <= CACHE_RANGE; i++)
                cachedValues[i] = new ASuint(i);
            return cachedValues;
        }

        private static string[] _prepareCachedStrings() {
            string[] strs = new string[STRING_CACHE_RANGE + 1];
            for (uint i = 0; i <= STRING_CACHE_RANGE; i++)
                strs[i] = i.ToString(CultureInfo.InvariantCulture);
            return strs;
        }

    }

}
