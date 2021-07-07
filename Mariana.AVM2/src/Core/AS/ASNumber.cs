using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The Number class represents a 64-bit IEEE 754 floating-point number.
    /// </summary>
    ///
    /// <remarks>
    /// This is a boxed representation of the primitive type <see cref="Double"/>. It is only
    /// used when a boxing conversion is required. This type should not be used for any other
    /// purpose, other than for using its static properties or methods of for type checking of
    /// objects. Methods of the AS3 <c>Number</c> class are available as static methods taking
    /// a primitive <see cref="Double"/> argument.
    /// </remarks>
    [AVM2ExportClass(name = "Number", hasPrototypeMethods = true)]
    [AVM2ExportClassInternal(tag = ClassTag.NUMBER, primitiveType = typeof(double))]
    sealed public class ASNumber : ASObject {

        // The prototype methods of this class (unlike most other classes) are static,
        // because they are also used by int and uint objects. (Static methods are allowed
        // to be exported as prototype methods, where the receiver is passed on to the first
        // argument)

        /// <summary>
        /// The value of the "length" property of the AS3 Number class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        /// <summary>
        /// The maximum possible finite value for a Number.
        /// </summary>
        [AVM2ExportTrait]
        public const double MAX_VALUE = Double.MaxValue;

        /// <summary>
        /// The minimum possible value for a Number that is greater than zero.
        /// </summary>
        [AVM2ExportTrait]
        public static readonly double MIN_VALUE = _getDoubleEpsilon();

        /// <summary>
        /// The value NaN (Not a number), which indicates the result of certain invalid operations
        /// (such as the division 0/0, or the square root of a negative number).
        /// </summary>
        [AVM2ExportTrait]
        public const double NAN = Double.NaN;

        /// <summary>
        /// The value of positive infinity. This indicates that the result of an operation is positive
        /// and either infinite or too large in magnitude to be represented as a finite number.
        /// </summary>
        [AVM2ExportTrait]
        public const double POSITIVE_INFINITY = Double.PositiveInfinity;

        /// <summary>
        /// The value of negative infinity, which indicates that the result of an operation is
        /// negative and either infinite or too large in magnitude to be represented as a finite
        /// number.
        /// </summary>
        [AVM2ExportTrait]
        public const double NEGATIVE_INFINITY = Double.NegativeInfinity;

        private static LazyInitObject<Class> s_lazyClass = new LazyInitObject<Class>(
            () => Class.fromType(typeof(ASNumber))!,
            recursionHandling: LazyInitRecursionHandling.RECURSIVE_CALL
        );

        private readonly double m_value;

        private ASNumber(double v) : base(s_lazyClass.value) {
            m_value = v;
        }

        /// <summary>
        /// Converts the uint instance to a Boolean value.
        /// </summary>
        /// <returns>The Boolean value</returns>
        protected private override bool AS_coerceBoolean() => AS_toBoolean(m_value);

        /// <summary>
        /// Converts the current instance to an integer value.
        /// </summary>
        /// <returns>The integer value.</returns>
        protected override int AS_coerceInt() => AS_toInt(m_value);

        /// <summary>
        /// Converts the current instance to a floating-point number value.
        /// </summary>
        /// <returns>The floating-point number value.</returns>
        protected override double AS_coerceNumber() => m_value;

        /// <summary>
        /// Converts the current instance to a string value.
        /// </summary>
        /// <returns>The string value.</returns>
        protected override string AS_coerceString() => AS_convertString(m_value);

        /// <summary>
        /// Converts the current instance to an unsigned integer value.
        /// </summary>
        /// <returns>The unsigned integer value.</returns>
        protected override uint AS_coerceUint() => AS_toUint(m_value);

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
        public string toExponential(int p = 0) => toExponential(m_value, p);

        /// <summary>
        /// Returns a string representation of the number in the given base. For base 10, very large
        /// or small numbers are represented in scientific notation. For other bases, the returned
        /// string is always in fixed-point notation.
        /// </summary>
        ///
        /// <param name="radix">The base. This must be between 2 and 36.</param>
        /// <returns>The string representation of the number in the given base.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1003: If the radix is less than 2 or greater than 36.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method is exported to the AVM2 with the name <c>toString</c>, but must be
        /// called from .NET code with the name <c>AS_toString</c> to avoid confusion with the
        /// <see cref="Object.ToString" qualifyHint="true"/> method.
        /// </remarks>
        [AVM2ExportTrait(name = "toString", nsUri = "http://adobe.com/AS3/2006/builtin")]
        public string AS_toString(int radix = 10) => toString(m_value, radix);

        /// <summary>
        /// Returns the primitive type representation of the object.
        /// </summary>
        /// <returns>A primitive representation of the object.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        public new double valueOf() => m_value;

        /// <summary>
        /// Returns a string representation of the number in base 10. Very large or small numbers are
        /// represented in scientific notation.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <returns>The string representation of the number in base 10.</returns>
        public static string AS_convertString(double num) => NumberFormatHelper.doubleToString(num);

        /// <summary>
        /// Converts a floating-point number to a 32-bit signed integer.
        /// </summary>
        /// <param name="num">The number to convert.</param>
        /// <returns>The integer. If the number is infinity or NaN, 0 is returned; otherwise the
        /// number is cast to an integer with its decimal point truncated. For integers outside the
        /// 32-bit range, the overflowing bits are masked out.</returns>
        public static int AS_toInt(double num) {
            // We can't use the native conversion here as ECMAScript requires infinities
            // and NaNs to convert to zero, and numbers outside the int range to be
            // reduced modulo 2^31. These are both unspecified conversions in the CLR.

            long bits = BitConverter.DoubleToInt64Bits(num);
            int sign = (int)(bits >> 63);
            int exponent = ((int)(bits >> 52) & 0x7FF) - 1023;
            if ((uint)exponent >= 84u)
                return 0;
            bits = (bits & 0xFFFFFFFFFFFFFL) | 0x10000000000000L;
            int abs = (exponent <= 52) ? (int)(bits >> (52 - exponent)) : (int)(bits << (exponent - 52));
            return (abs + sign) ^ sign;
        }

        /// <summary>
        /// Converts a floating-point number to a 32-bit unsigned integer.
        /// </summary>
        /// <param name="num">The number to convert.</param>
        /// <returns>The integer. If the number is infinity or NaN, 0 is returned; otherwise the
        /// number is cast to an unsigned integer with its decimal point truncated. For integers
        /// outside the 32-bit range, the overflowing bits are masked out.</returns>
        public static uint AS_toUint(double num) {
            long bits = BitConverter.DoubleToInt64Bits(num);
            uint sign = (uint)(int)(bits >> 63);
            int exponent = ((int)(bits >> 52) & 0x7FF) - 1023;
            if ((uint)exponent >= 84u)
                return 0;
            bits = (bits & 0xFFFFFFFFFFFFFL) | 0x10000000000000L;
            int abs = (exponent <= 52) ? (int)(bits >> (52 - exponent)) : (int)(bits << (exponent - 52));
            return ((uint)abs + sign) ^ sign;
        }

        /// <summary>
        /// Converts a floating-point value to a Boolean value.
        /// </summary>
        /// <param name="d">The floating-point value.</param>
        /// <returns>False if the value is 0 or NaN, otherwise true.</returns>
        public static bool AS_toBoolean(double d) => !Double.IsNaN(d) && d != 0.0;

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
        [AVM2ExportPrototypeMethod]
        public static string toFixed(double num, int precision = 0) {
            if ((uint)precision > 20)
                throw ErrorHelper.createError(ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE);
            return NumberFormatHelper.doubleToStringFixedNotation(num, precision);
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
        [AVM2ExportPrototypeMethod]
        public static string toExponential(double num, int precision = 0) {
            if ((uint)precision > 20)
                throw ErrorHelper.createError(ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE);
            return NumberFormatHelper.doubleToStringExpNotation(num, precision);
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
        /// <item><description>RangeError #1002: If the precision is less than 1 or greater than 21.</description></item>
        /// </list>
        /// </exception>
        [AVM2ExportPrototypeMethod]
        public static string toPrecision(double num, int precision = 0) {
            if (precision < 1 || precision > 21)
                throw ErrorHelper.createError(ErrorCode.NUMBER_PRECISION_OUT_OF_RANGE);
            return NumberFormatHelper.doubleToStringPrecision(num, precision);
        }

        /// <summary>
        /// Returns a string representation of the number in the given base.
        /// </summary>
        /// <param name="num">The number.</param>
        /// <param name="radix">The base. This must be between 2 and 36.</param>
        /// <returns>The string representation of the number in the given base.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>RangeError #1003: If the radix is less than 2 or greater than 36.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// For base 10, scientific notation is used for numbers whose magnitude is less than 10^-6
        /// or greater than or equal to 10^21. For other bases, the number is truncated to an integer
        /// and scientific notation is never used.
        /// </remarks>
        [AVM2ExportPrototypeMethod]
        public static string toString(double num, int radix = 10) {
            if (radix < 2 || radix > 36)
                throw ErrorHelper.createError(ErrorCode.NUMBER_RADIX_OUT_OF_RANGE, radix);

            if (radix == 10)
                return NumberFormatHelper.doubleToString(num);

            return NumberFormatHelper.doubleIntegerToStringRadix(num, radix);
        }

        /// <summary>
        /// Returns the string representation of a number in the user's locale.
        /// </summary>
        /// <param name="num">The number to convert to a string.</param>
        /// <returns>The string representation of the number in the user's locale.</returns>
        [AVM2ExportPrototypeMethod]
        public static string toLocaleString(double num) => num.ToString(CultureInfo.CurrentCulture);

        /// <summary>
        /// Returns the given argument. This is used by the ABC compiler for calls to the
        /// <c>valueOf</c> method on Number values.
        /// </summary>
        /// <param name="num">The argument.</param>
        /// <returns>The value of <paramref name="num"/>.</returns>
        [AVM2ExportPrototypeMethod]
        public static double valueOf(double num) => num;

        /// <summary>
        /// Returns the positive 64-bit floating point number with the smallest representable nonzero
        /// magnitude on the current platform.
        /// </summary>
        /// <returns>The positive 64-bit floating point number with the smallest representable nonzero
        /// magnitude on the current platform. For platforms that support denormalized numbers, this is
        /// equal to the value of <see cref="Double.Epsilon" qualifyHint="true"/>.</returns>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static double _getDoubleEpsilon() {
            double eps = BitConverter.Int64BitsToDouble(1L);
            if (eps != 0.0)   // Are denormals supported on this system?
                return eps;
            return BitConverter.Int64BitsToDouble(0x0010000000000000L);
        }

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABC to IL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) =>
            ASAny.AS_fromNumber((args.Length == 0) ? 0.0 : ASAny.AS_toNumber(args[0]));

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABC to IL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_CONSTRUCT(ReadOnlySpan<ASAny> args) => __AS_INVOKE(args);

        /// <summary>
        /// Creates a boxed object from a floating-point value.
        /// </summary>
        /// <param name="x">The value to be boxed.</param>
        internal static ASObject box(double x) => new ASNumber(x);

    }

}
