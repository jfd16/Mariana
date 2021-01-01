using System;
using System.Globalization;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The int class represents a 32-bit integer.
    /// </summary>
    ///
    /// <remarks>
    /// This is a boxed representation of the primitive type <see cref="Int32"/>. It is only used
    /// when a boxing conversion is required. This type should not be used for any other purpose,
    /// other than for using its static properties or methods of for type checking of objects.
    /// Methods of the AS3 <c>int</c> class are available as static methods taking a primitive
    /// <see cref="Int32"/> argument.
    /// </remarks>
    [AVM2ExportClass(name = "int", hasPrototypeMethods = true)]
    [AVM2ExportClassInternal(tag = ClassTag.INT, primitiveType = typeof(int))]
    sealed public class ASint : ASObject {

        /// <summary>
        /// The value of the "length" property of the AS3 int class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// The maximum possible value for a 32-bit integer.
        /// </summary>
        [AVM2ExportTrait]
        public const int MAX_VALUE = 2147483647;

        /// <summary>
        /// The minimum possible value for a 32-bit integer.
        /// </summary>
        [AVM2ExportTrait]
        public const int MIN_VALUE = -2147483648;

        /// <summary>
        /// Contains cached boxed instances for common integer values.
        /// </summary>
        private static readonly ASObject[] s_cachedValues = _prepareCachedValues();

        /// <summary>
        /// The maximum absolute value for which cached boxes should be created.
        /// </summary>
        private const int CACHE_RANGE = 128;

        /// <summary>
        /// Contains cached strings for common values for use by <see cref="AS_convertString"/>.
        /// </summary>
        private static readonly string[] s_cachedStrings = _prepareCachedStrings();

        /// <summary>
        /// The maximum absolute value for which cached strings should be created.
        /// </summary>
        private const int STRING_CACHE_RANGE = 128;

        private readonly int m_value;

        private ASint(int v) {
            m_value = v;
        }

        /// <summary>
        /// Converts the current instance to a Boolean value.
        /// </summary>
        /// <returns>The Boolean value.</returns>
        protected override bool AS_coerceBoolean() => m_value != 0;

        /// <summary>
        /// Converts the current instance to an integer value.
        /// </summary>
        /// <returns>The integer value.</returns>
        protected override int AS_coerceInt() => m_value;

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
        protected override uint AS_coerceUint() => (uint)m_value;

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
        /// <item>
        /// <term>RangeError #1002</term>
        /// <description>If the precision is less than 1 or greater than 21.</description>
        /// </item>
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
        /// <item>
        /// <term>RangeError #1002</term>
        /// <description>If the precision is less than 0 or greater than 20.</description>
        /// </item>
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
        /// <item>
        /// <term>RangeError #1002</term>
        /// <description>If the precision is less than 0 or greater than 20.</description>
        /// </item>
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
        /// Returns a string representation of the number in the given radix (base). Very large or
        /// small numbers are represented in scientific notation.
        /// </summary>
        /// <param name="radix">The base in which the string representation of the number must be
        /// returned. This must be a number from 2 to 36 inclusive.</param>
        /// <returns>The string representation of the number in the given base.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>RangeError #1003</term>
        /// <description>If the radix is less than 2 or greater than 36.</description>
        /// </item>
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
        public new int valueOf() => m_value;

        /// <summary>
        /// Returns a string representation of the number in base 10.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <returns>The string representation of the number in base 10.</returns>
        public static string AS_convertString(int num) {
            var cache = s_cachedStrings;
            return ((uint)(num + STRING_CACHE_RANGE) < (uint)cache.Length)
                ? cache[num + STRING_CACHE_RANGE]
                : num.ToString(CultureInfo.InvariantCulture);
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
        /// <item>
        /// <term>RangeError #1002</term>
        /// <description>If the precision is less than 0 or greater than 20.</description>
        /// </item>
        /// </list>
        /// </exception>
        public static string toFixed(int num, int precision) {
            if (precision < 0 || precision > 20)
                throw ErrorHelper.createError(ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE);
            return num.ToString(NumberFormatHelper.getToFixedFormatString(precision), CultureInfo.InvariantCulture);
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
        /// <item>
        /// <term>RangeError #1002</term>
        /// <description>If the precision is less than 0 or greater than 20.</description>
        /// </item>
        /// </list>
        /// </exception>
        public static string toExponential(int num, int precision) {
            if (precision < 0 || precision > 20)
                throw ErrorHelper.createError(ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE);
            return num.ToString(NumberFormatHelper.getToExponentialFormatString(precision), CultureInfo.InvariantCulture);
        }

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
        /// <item>
        /// <term>RangeError #1002</term>
        /// <description>If the precision is less than 1 or greater than 21.</description>
        /// </item>
        /// </list>
        /// </exception>
        public static string toPrecision(int num, int precision) {
            if (precision < 1 || precision > 21)
                throw ErrorHelper.createError(ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE);

            string fmtString;
            if (num == 0) {
                fmtString = NumberFormatHelper.getToFixedFormatString(precision - 1);
            }
            else {
                int order = (int)Math.Log10(Math.Abs((double)num));
                fmtString = (order < precision)
                    ? NumberFormatHelper.getToFixedFormatString(precision - order - 1)
                    : NumberFormatHelper.getToExponentialFormatString(precision - 1);
            }

            return num.ToString(fmtString, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Returns a string representation of the number in the given base.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <param name="radix">The base between 2 and 36.</param>
        /// <returns>The string representation of the number in the given base.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>RangeError #1003</term>
        /// <description>If the radix is less than 2 or greater than 36.</description>
        /// </item>
        /// </list>
        /// </exception>
        public static string toString(int num, int radix) {
            if (radix < 2 || radix > 36)
                throw ErrorHelper.createError(ErrorCode.NUMBER_RADIX_OUT_OF_RANGE, radix);

            return (radix == 10)
                ? num.ToString(CultureInfo.InvariantCulture)
                : NumberFormatHelper.intToString(num, radix);
        }

        /// <summary>
        /// Returns the string representation of a number in the user's locale.
        /// </summary>
        /// <param name="num">The number to convert to a string.</param>
        /// <returns>The string representation of the number in the user's locale.</returns>
        public static string toLocaleString(int num) => num.ToString(CultureInfo.CurrentCulture);

        /// <summary>
        /// Returns the given argument. This is used by the ABC compiler for calls to the
        /// <c>valueOf</c> method on integer values.
        /// </summary>
        /// <param name="num">The argument.</param>
        /// <returns>The value of <paramref name="num"/>.</returns>
        public static int valueOf(int num) => num;

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABCIL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) {
            if (args.Length == 0)
                return ASAny.AS_fromInt(0);
            return ASAny.AS_fromInt(ASAny.AS_toInt(args[0]));
        }

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABCIL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_CONSTRUCT(ReadOnlySpan<ASAny> args) => __AS_INVOKE(args);

        /// <summary>
        /// Creates a boxed object from an integer value.
        /// </summary>
        /// <param name="x">The value to be boxed.</param>
        internal static ASObject box(int x) {
            var cache = s_cachedValues;
            return ((uint)(x + CACHE_RANGE) < (uint)cache.Length) ? cache[x + CACHE_RANGE] : new ASint(x);
        }

        private static ASObject[] _prepareCachedValues() {
            var cachedValues = new ASObject[CACHE_RANGE * 2 + 1];
            for (int i = 0; i < cachedValues.Length; i++)
                cachedValues[i] = new ASint(i - CACHE_RANGE);

            return cachedValues;
        }

        private static string[] _prepareCachedStrings() {
            var strs = new string[STRING_CACHE_RANGE * 2 + 1];
            for (int i = 0; i < strs.Length; i++)
                strs[i] = (i - CACHE_RANGE).ToString(CultureInfo.InvariantCulture);

            return strs;
        }

    }

}

