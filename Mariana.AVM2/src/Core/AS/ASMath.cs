using System;
using System.Runtime.CompilerServices;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The Math class contains static methods and properties for commonly used mathematical
    /// functions and constants.
    /// </summary>
    [AVM2ExportClass(name = "Math")]
    public sealed class ASMath : ASObject {

        private ASMath() { }

        /// <summary>
        /// The value of pi (π), which is the ratio of the circumference of a circle to its diameter.
        /// </summary>
        [AVM2ExportTrait]
        public const double PI = 3.141592653589793;

        /// <summary>
        /// The value of e, which is the base for the <see cref="exp"/> and <see cref="log"/>
        /// functions.
        /// </summary>
        [AVM2ExportTrait]
        public const double E = 2.718281828459045;

        /// <summary>
        /// The natural logarithm (base e) of 10. The natural logarithm of any number can be divided
        /// by this value to obtain its base 10 logarithm.
        /// </summary>
        [AVM2ExportTrait]
        public const double LN10 = 2.302585092994046;

        /// <summary>
        /// The natural logarithm (base e) of 2. The natural logarithm of any number can be divided by
        /// this value to obtain its base 2 logarithm.
        /// </summary>
        [AVM2ExportTrait]
        public const double LN2 = 0.6931471805599453;

        /// <summary>
        /// The base 10 logarithm of e. The natural logarithm of any number can be multiplied by this
        /// value to obtain its base 10 logarithm.
        /// </summary>
        [AVM2ExportTrait]
        public const double LOG10E = 0.4342944819032518;

        /// <summary>
        /// The base 2 logarithm of e. The natural logarithm of any number can be multiplied by this
        /// value to obtain its base 2 logarithm.
        /// </summary>
        [AVM2ExportTrait]
        public const double LOG2E = 1.442695040888963387;

        /// <summary>
        /// The value of the square root of 1/2 (0.5).
        /// </summary>
        [AVM2ExportTrait]
        public const double SQRT1_2 = 0.7071067811865476;

        /// <summary>
        /// The value of the square root of 2.
        /// </summary>
        [AVM2ExportTrait]
        public const double SQRT2 = 1.4142135623730951;

        /// <summary>
        /// Returns the absolute value (magnitude) of <paramref name="x"/>.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The absolute value of <paramref name="x"/>.</returns>
        [AVM2ExportTrait]
        public static double abs(double x) => Math.Abs(x);

        /// <summary>
        /// Returns the arc cosine of <paramref name="x"/>, i.e. the angle whose cosine equal to
        /// <paramref name="x"/>.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The angle in radians. The range of the values returned is [0, π].</returns>
        [AVM2ExportTrait]
        public static double acos(double x) => Math.Acos(x);

        /// <summary>
        /// Returns the arc sine of <paramref name="x"/>, i.e. the angle whose sine is equal to
        /// <paramref name="x"/>.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The angle in radians. The range of the values returned is [-π/2,
        /// π/2].</returns>
        [AVM2ExportTrait]
        public static double asin(double x) => Math.Asin(x);

        /// <summary>
        /// Returns the arc tangent of <paramref name="x"/>, i.e. the angle whose tangent is equal
        /// to <paramref name="x"/>.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The angle in radians. The range of the values returned is [-π/2,
        /// π/2].</returns>
        [AVM2ExportTrait]
        public static double atan(double x) => Math.Atan(x);

        /// <summary>
        /// Returns the angle in the Cartesian plane between the positive x-axis and the position
        /// vector of the point (<paramref name="x"/>, <paramref name="y"/>).
        /// </summary>
        /// <param name="y">The y-coordinate.</param>
        /// <param name="x">The x-coordinate.</param>
        /// <returns>The angle in radians.</returns>
        ///
        /// <remarks>
        /// The returned angle has a tangent equal to <paramref name="y"/>/<paramref name="x"/>
        /// and the quadrant in which it lies is determined by the signs of <paramref name="x"/> and
        /// <paramref name="y"/>. The range of the angle returned is (-π, π].
        /// </remarks>
        [AVM2ExportTrait]
        public static double atan2(double y, double x) => Math.Atan2(y, x);

        /// <summary>
        /// Returns the ceiling of <paramref name="x"/>, i.e. the least integer whose value is
        /// greater than or equal to <paramref name="x"/>.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The ceiling of <paramref name="x"/>.</returns>
        [AVM2ExportTrait]
        public static double ceil(double x) => Math.Ceiling(x);

        /// <summary>
        /// Returns the cosine of <paramref name="x"/>.
        /// </summary>
        /// <param name="x">The angle in radians.</param>
        /// <returns>The cosine of <paramref name="x"/>.</returns>
        [AVM2ExportTrait]
        public static double cos(double x) => Math.Cos(x);

        /// <summary>
        /// Returns the value of e raized to the power <paramref name="x"/>.
        /// </summary>
        /// <param name="x">The power.</param>
        /// <returns>The value of e raized to the power <paramref name="x"/>.</returns>
        [AVM2ExportTrait]
        public static double exp(double x) => Math.Exp(x);

        /// <summary>
        /// Returns the floor of <paramref name="x"/>, i.e. the greatest integer whose value is less
        /// than or equal to <paramref name="x"/>.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <returns>The floor of <paramref name="x"/>.</returns>
        [AVM2ExportTrait]
        public static double floor(double x) => Math.Floor(x);

        /// <summary>
        /// Returns the natural (base e) logarithm of <paramref name="x"/>.
        /// </summary>
        /// <param name="x">The value whose natural logarithm is to be obtained.</param>
        /// <returns>The natural logarithm of <paramref name="x"/>.</returns>
        [AVM2ExportTrait]
        public static double log(double x) => Math.Log(x);

        /// <summary>
        /// Returns the maximum value of the specified arguments.
        /// </summary>
        /// <param name="args">The values from which to return the maximum value.</param>
        /// <returns>The maximum value from the specified arguments. If no arguments are specified,
        /// returns negative infinity.</returns>
        [AVM2ExportTrait]
        public static double max(RestParam args = default) {
            var span = args.getSpan();
            if (span.Length == 0)
                return Double.NegativeInfinity;

            double currentMax = (double)span[0];
            if (Double.IsNaN(currentMax))
                return currentMax;

            for (int i = 1; i < span.Length; i++) {
                double arg = (double)span[i];
                if (Double.IsNaN(arg))
                    return arg;
                if (arg > currentMax || (currentMax == 0.0 && !Double.IsNegative(arg)))
                    currentMax = arg;
            }

            return currentMax;
        }

        /// <summary>
        /// Returns the minimum value of the specified arguments.
        /// </summary>
        /// <param name="args">The values from which to return the minimum value.</param>
        /// <returns>The minimum value from the specified arguments. If no arguments are specified,
        /// returns positive infinity.</returns>
        [AVM2ExportTrait]
        public static double min(RestParam args = default) {
            var span = args.getSpan();
            if (span.Length == 0)
                return Double.PositiveInfinity;

            double currentMin = (double)span[0];
            if (Double.IsNaN(currentMin))
                return currentMin;

            for (int i = 1; i < span.Length; i++) {
                double arg = (double)span[i];
                if (Double.IsNaN(arg))
                    return arg;
                if (arg < currentMin || (currentMin == 0.0 && Double.IsNegative(arg)))
                    currentMin = arg;
            }

            return currentMin;
        }

        /// <summary>
        /// Returns the value of <paramref name="x"/> raized to the power <paramref name="y"/>.
        /// </summary>
        /// <param name="x">The value to raise to the power <paramref name="y"/>.</param>
        /// <param name="y">The power.</param>
        /// <returns>The specified value raized to the specified power.</returns>
        [AVM2ExportTrait]
        public static double pow(double x, double y) => Math.Pow(x, y);

        /// <summary>
        /// Returns a pseudo-random number in the range [0, 1).
        /// </summary>
        /// <returns>A pseudo-random number in the range [0, 1).</returns>
        [AVM2ExportTrait]
        public static double random() => ThreadStaticRandom.instance.NextDouble();

        /// <summary>
        /// Rounds <paramref name="val"/> to the nearest integer. A number exactly halfway between
        /// two successive integers (i.e. having fractional part as 0.5) will be rounded towards
        /// positive infinity, i.e. to the integer which is greater than the given number.
        /// </summary>
        ///
        /// <param name="val">The value to round.</param>
        /// <returns>The rounded value.</returns>
        [AVM2ExportTrait]
        public static double round(double val) {
            if (!Double.IsFinite(val) || val == 0.0)
                return val;
            double frac = val % 1.0;
            if (frac > 0.0)
                return val - frac + ((frac < 0.5) ? 0.0 : 1.0);
            if (val >= -0.5)
                return -0.0;
            return val - frac - ((frac < -0.0) ? 1.0 : 0.0);
        }

        /// <summary>
        /// Returns the sine of <paramref name="x"/>.
        /// </summary>
        /// <param name="x">The angle in radians.</param>
        /// <returns>The sine of <paramref name="x"/>.</returns>
        [AVM2ExportTrait]
        public static double sin(double x) => Math.Sin(x);

        /// <summary>
        /// Returns the square root of <paramref name="x"/>.
        /// </summary>
        /// <param name="x">The number.</param>
        /// <returns>The square root of <paramref name="x"/>; NaN if <paramref name="x"/> is
        /// negative.</returns>
        [AVM2ExportTrait]
        public static double sqrt(double x) => Math.Sqrt(x);

        /// <summary>
        /// Returns the tangent of <paramref name="x"/>.
        /// </summary>
        /// <param name="x">The angle in radians.</param>
        /// <returns>The tangent of <paramref name="x"/>.</returns>
        [AVM2ExportTrait]
        public static double tan(double x) => Math.Tan(x);

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABCIL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) => throw ErrorHelper.createError(ErrorCode.MATH_NOT_FUNCTION);

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABCIL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_CONSTRUCT(ReadOnlySpan<ASAny> args) => throw ErrorHelper.createError(ErrorCode.MATH_NOT_CONSTRUCTOR);

    }

}
