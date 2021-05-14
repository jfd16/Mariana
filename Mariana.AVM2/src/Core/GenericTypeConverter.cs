using System;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The GenericTypeConverter class is used for generic type conversions between AVM2 supported
    /// types.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDest">The destination type.</typeparam>
    ///
    /// <remarks>
    /// <para>
    /// GenericTypeConverter can be used in cases where an instance of a type represented by a
    /// type parameter in a generic type is to be converted to or from a known type or another
    /// type parameter, such as to pass generic objects to dynamic ActionScript function calls
    /// (which require arguments to be of the "any" type).
    /// </para>
    /// </remarks>
    public abstract class GenericTypeConverter<TSource, TDest> {

        /// <summary>
        /// The type converter instance for the current <typeparamref name="TSource"/> and
        /// <typeparamref name="TDest"/> types.
        /// </summary>
        private static LazyInitObject<GenericTypeConverter<TSource, TDest>> s_lazyInstance =
            new LazyInitObject<GenericTypeConverter<TSource, TDest>>(
                () => (GenericTypeConverter<TSource, TDest>)InternalGenericTypeConverters.createConverterInstance(typeof(TSource), typeof(TDest))
            );

        /// <summary>
        /// Gets the type converter instance that converts from type <typeparamref name="TSource"/>
        /// to type <typeparamref name="TDest"/>.
        /// </summary>
        public static GenericTypeConverter<TSource, TDest> instance => s_lazyInstance.value;

        protected private GenericTypeConverter() { }

        /// <summary>
        /// Converts the specified object from the source type to the destination type.
        /// </summary>
        /// <param name="value">The object to convert.</param>
        /// <returns>The object converted to the destination type.</returns>
        public abstract TDest convert(TSource value);

        /// <summary>
        /// Converts elements from the source span, and writes the converted objects to the destination span.
        /// </summary>
        /// <param name="src">The source span.</param>
        /// <param name="dst">The destination span. This must have the same length as <paramref name="src"/>.</param>
        ///
        /// <exception cref="AVM2Exception">ArgumentError #10061: <paramref name="dst"/> does not have the same
        /// length as <paramref name="src"/>.</exception>
        public void convertSpan(ReadOnlySpan<TSource> src, Span<TDest> dst) {
            if (dst.Length != src.Length)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(dst));
            convertSpanImpl(src, dst);
        }

        /// <summary>
        /// Converts elements from the source span, and returns an array containing the converted elements.
        /// </summary>
        /// <param name="src">The source span.</param>
        public TDest[] convertSpan(ReadOnlySpan<TSource> src) {
            if (src.IsEmpty)
                return Array.Empty<TDest>();

            TDest[] dst = new TDest[src.Length];
            convertSpanImpl(src, dst);
            return dst;
        }

        /// <summary>
        /// Converts elements from the source span, and writes the converted objects to the destination span.
        /// </summary>
        /// <param name="src">The source span.</param>
        /// <param name="dst">The destination span. This must have the same length as <paramref name="src"/>.</param>
        protected private abstract void convertSpanImpl(ReadOnlySpan<TSource> src, Span<TDest> dst);

    }

    internal static class InternalGenericTypeConverters {

        internal static object createConverterInstance(Type tSource, Type tDest) {
            object instance = null;
            Type converterType = null;
            bool converterHasSourceArg = false;

            if (tSource == tDest) {
                converterType = typeof(Identity<>);
            }
            else if (tSource == typeof(int)) {
                if (tDest == typeof(uint))
                    instance = new Int2Uint();
                else if (tDest == typeof(double))
                    instance = new Int2Number();
                else if (tDest == typeof(string))
                    instance = new Int2String();
                else if (tDest == typeof(bool))
                    instance = new Int2Boolean();
                else if (tDest == typeof(ASAny))
                    instance = new Int2Any();
                else if (tDest == typeof(ASObject))
                    instance = new Int2Object();
                else
                    converterType = typeof(Invalid<,>);
            }
            else if (tSource == typeof(uint)) {
                if (tDest == typeof(int))
                    instance = new Uint2Int();
                else if (tDest == typeof(double))
                    instance = new Uint2Number();
                else if (tDest == typeof(string))
                    instance = new Uint2String();
                else if (tDest == typeof(bool))
                    instance = new Uint2Boolean();
                else if (tDest == typeof(ASAny))
                    instance = new Uint2Any();
                else if (tDest == typeof(ASObject))
                    instance = new Uint2Object();
                else
                    converterType = typeof(Invalid<,>);
            }
            else if (tSource == typeof(double)) {
                if (tDest == typeof(int))
                    instance = new Number2Int();
                else if (tDest == typeof(uint))
                    instance = new Number2Uint();
                else if (tDest == typeof(string))
                    instance = new Number2String();
                else if (tDest == typeof(bool))
                    instance = new Number2Boolean();
                else if (tDest == typeof(ASAny))
                    instance = new Number2Any();
                else if (tDest == typeof(ASObject))
                    instance = new Number2Object();
                else
                    converterType = typeof(Invalid<,>);
            }
            else if (tSource == typeof(string)) {
                if (tDest == typeof(int))
                    instance = new String2Int();
                else if (tDest == typeof(uint))
                    instance = new String2Uint();
                else if (tDest == typeof(double))
                    instance = new String2Number();
                else if (tDest == typeof(bool))
                    instance = new String2Boolean();
                else if (tDest == typeof(ASAny))
                    instance = new String2Any();
                else if (tDest == typeof(ASObject))
                    instance = new String2Object();
                else if (!tDest.IsValueType)
                    converterType = typeof(InvalidExceptNull<,>);
                else
                    converterType = typeof(Invalid<,>);
            }
            else if (tSource == typeof(bool)) {
                if (tDest == typeof(int))
                    instance = new Boolean2Int();
                else if (tDest == typeof(uint))
                    instance = new Boolean2Uint();
                else if (tDest == typeof(double))
                    instance = new Boolean2Number();
                else if (tDest == typeof(string))
                    instance = new Boolean2String();
                else if (tDest == typeof(ASAny))
                    instance = new Boolean2Any();
                else if (tDest == typeof(ASObject))
                    instance = new Boolean2Object();
                else
                    converterType = typeof(Invalid<,>);
            }
            else if (tSource == typeof(ASAny)) {
                if (tDest == typeof(int))
                    instance = new Any2Int();
                else if (tDest == typeof(uint))
                    instance = new Any2Uint();
                else if (tDest == typeof(double))
                    instance = new Any2Number();
                else if (tDest == typeof(string))
                    instance = new Any2String();
                else if (tDest == typeof(bool))
                    instance = new Any2Boolean();
                else if (!tDest.IsValueType)
                    converterType = typeof(Any2Object<>);
                else
                    converterType = typeof(Invalid<,>);
            }
            else if (tSource.IsInterface || typeof(ASObject).IsAssignableFrom(tSource)) {
                if (tDest == typeof(int)) {
                    converterType = typeof(Object2Int<>);
                    converterHasSourceArg = true;
                }
                else if (tDest == typeof(uint)) {
                    converterType = typeof(Object2Uint<>);
                    converterHasSourceArg = true;
                }
                else if (tDest == typeof(double)) {
                    converterType = typeof(Object2Number<>);
                    converterHasSourceArg = true;
                }
                else if (tDest == typeof(string)) {
                    converterType = typeof(Object2String<>);
                    converterHasSourceArg = true;
                }
                else if (tDest == typeof(bool)) {
                    converterType = typeof(Object2Boolean<>);
                    converterHasSourceArg = true;
                }
                else if (tDest == typeof(ASAny)) {
                    converterType = typeof(Object2Any<>);
                    converterHasSourceArg = true;
                }
                else if (!tDest.IsValueType) {
                    converterType = typeof(Object2Object<,>);
                }
                else {
                    converterType = typeof(Invalid<,>);
                }
            }
            else if (!tSource.IsValueType && !tDest.IsValueType) {
                converterType = typeof(Object2Object<,>);
            }
            else {
                converterType = typeof(Invalid<,>);
            }

            if (instance == null) {
                if (converterType.GetGenericArguments().Length == 2)
                    instance = Activator.CreateInstance(converterType.MakeGenericType(tSource, tDest));
                else if (converterHasSourceArg)
                    instance = Activator.CreateInstance(converterType.MakeGenericType(tSource));
                else
                    instance = Activator.CreateInstance(converterType.MakeGenericType(tDest));
            }

            return instance;
        }

        #region ASAny => (TDest)

        internal sealed class Any2Int : GenericTypeConverter<ASAny, int> {
            public override int convert(ASAny value) => ASAny.AS_toInt(value);

            protected private override void convertSpanImpl(ReadOnlySpan<ASAny> src, Span<int> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_toInt(src[i]);
            }
        }

        internal sealed class Any2Uint : GenericTypeConverter<ASAny, uint> {
            public override uint convert(ASAny value) => ASAny.AS_toUint(value);

            protected private override void convertSpanImpl(ReadOnlySpan<ASAny> src, Span<uint> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_toUint(src[i]);
            }
        }

        internal sealed class Any2Number : GenericTypeConverter<ASAny, double> {
            public override double convert(ASAny value) => ASAny.AS_toNumber(value);

            protected private override void convertSpanImpl(ReadOnlySpan<ASAny> src, Span<double> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_toNumber(src[i]);
            }
        }

        internal sealed class Any2String : GenericTypeConverter<ASAny, string> {
            public override string convert(ASAny value) => ASAny.AS_coerceString(value);

            protected private override void convertSpanImpl(ReadOnlySpan<ASAny> src, Span<string> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_coerceString(src[i]);
            }
        }

        internal sealed class Any2Boolean : GenericTypeConverter<ASAny, bool> {
            public override bool convert(ASAny value) => ASAny.AS_toBoolean(value);

            protected private override void convertSpanImpl(ReadOnlySpan<ASAny> src, Span<bool> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_toBoolean(src[i]);
            }
        }

        internal sealed class Any2Object<T> : GenericTypeConverter<ASAny, T> where T : class {
            public override T convert(ASAny value) => ASAny.AS_cast<T>(value);

            protected private override void convertSpanImpl(ReadOnlySpan<ASAny> src, Span<T> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_cast<T>(src[i]);
            }
        }

        #endregion

        #region Boolean => (TDest)

        internal sealed class Boolean2Int : GenericTypeConverter<bool, int> {
            public override int convert(bool value) => value ? 1 : 0;

            protected private override void convertSpanImpl(ReadOnlySpan<bool> src, Span<int> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = src[i] ? 1 : 0;
            }
        }

        internal sealed class Boolean2Uint : GenericTypeConverter<bool, uint> {
            public override uint convert(bool value) => value ? 1u : 0u;

            protected private override void convertSpanImpl(ReadOnlySpan<bool> src, Span<uint> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = src[i] ? 1u : 0u;
            }
        }

        internal sealed class Boolean2Number : GenericTypeConverter<bool, double> {
            public override double convert(bool value) => value ? 1.0 : 0.0;

            protected private override void convertSpanImpl(ReadOnlySpan<bool> src, Span<double> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = src[i] ? 1.0 : 0.0;
            }
        }

        internal sealed class Boolean2String : GenericTypeConverter<bool, string> {
            public override string convert(bool value) => ASBoolean.AS_convertString(value);

            protected private override void convertSpanImpl(ReadOnlySpan<bool> src, Span<string> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASBoolean.AS_convertString(src[i]);
            }
        }

        internal sealed class Boolean2Object : GenericTypeConverter<bool, ASObject> {
            public override ASObject convert(bool value) => ASObject.AS_fromBoolean(value);

            protected private override void convertSpanImpl(ReadOnlySpan<bool> src, Span<ASObject> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_fromBoolean(src[i]);
            }
        }

        internal sealed class Boolean2Any : GenericTypeConverter<bool, ASAny> {
            public override ASAny convert(bool value) => ASAny.AS_fromBoolean(value);

            protected private override void convertSpanImpl(ReadOnlySpan<bool> src, Span<ASAny> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_fromBoolean(src[i]);
            }
        }

        #endregion

        #region int => (TDest)

        internal sealed class Int2Uint : GenericTypeConverter<int, uint> {
            public override uint convert(int value) => (uint)value;

            protected private override void convertSpanImpl(ReadOnlySpan<int> src, Span<uint> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = (uint)src[i];
            }
        }

        internal sealed class Int2Number : GenericTypeConverter<int, double> {
            public override double convert(int value) => (double)value;

            protected private override void convertSpanImpl(ReadOnlySpan<int> src, Span<double> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = (double)src[i];
            }
        }

        internal sealed class Int2String : GenericTypeConverter<int, string> {
            public override string convert(int value) => ASint.AS_convertString(value);

            protected private override void convertSpanImpl(ReadOnlySpan<int> src, Span<string> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASint.AS_convertString(src[i]);
            }
        }

        internal sealed class Int2Boolean : GenericTypeConverter<int, bool> {
            public override bool convert(int value) => value != 0;

            protected private override void convertSpanImpl(ReadOnlySpan<int> src, Span<bool> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = src[i] != 0;
            }
        }

        internal sealed class Int2Object : GenericTypeConverter<int, ASObject> {
            public override ASObject convert(int value) => ASObject.AS_fromInt(value);

            protected private override void convertSpanImpl(ReadOnlySpan<int> src, Span<ASObject> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_fromInt(src[i]);
            }
        }

        internal sealed class Int2Any : GenericTypeConverter<int, ASAny> {
            public override ASAny convert(int value) => ASAny.AS_fromInt(value);

            protected private override void convertSpanImpl(ReadOnlySpan<int> src, Span<ASAny> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_fromInt(src[i]);
            }
        }

        #endregion

        #region Number => (TDest)

        internal sealed class Number2Int : GenericTypeConverter<double, int> {
            public override int convert(double value) => ASNumber.AS_toInt(value);

            protected private override void convertSpanImpl(ReadOnlySpan<double> src, Span<int> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASNumber.AS_toInt(src[i]);
            }
        }

        internal sealed class Number2Uint : GenericTypeConverter<double, uint> {
            public override uint convert(double value) => ASNumber.AS_toUint(value);

            protected private override void convertSpanImpl(ReadOnlySpan<double> src, Span<uint> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASNumber.AS_toUint(src[i]);
            }
        }

        internal sealed class Number2String : GenericTypeConverter<double, string> {
            public override string convert(double value) => ASNumber.AS_convertString(value);

            protected private override void convertSpanImpl(ReadOnlySpan<double> src, Span<string> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASNumber.AS_convertString(src[i]);
            }
        }

        internal sealed class Number2Boolean : GenericTypeConverter<double, bool> {
            public override bool convert(double value) => ASNumber.AS_toBoolean(value);

            protected private override void convertSpanImpl(ReadOnlySpan<double> src, Span<bool> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASNumber.AS_toBoolean(src[i]);
            }
        }

        internal sealed class Number2Object : GenericTypeConverter<double, ASObject> {
            public override ASObject convert(double value) => ASObject.AS_fromNumber(value);

            protected private override void convertSpanImpl(ReadOnlySpan<double> src, Span<ASObject> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_fromNumber(src[i]);
            }
        }

        internal sealed class Number2Any : GenericTypeConverter<double, ASAny> {
            public override ASAny convert(double value) => ASAny.AS_fromNumber(value);

            protected private override void convertSpanImpl(ReadOnlySpan<double> src, Span<ASAny> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_fromNumber(src[i]);
            }
        }

        #endregion

        #region Object => (TDest)

        internal sealed class Object2Int<T> : GenericTypeConverter<T, int> where T : class {
            public override int convert(T value) => ASObject.AS_toInt((ASObject)(object)value);

            protected private override void convertSpanImpl(ReadOnlySpan<T> src, Span<int> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_toInt((ASObject)(object)src[i]);
            }
        }

        internal sealed class Object2Uint<T> : GenericTypeConverter<T, uint> where T : class {
            public override uint convert(T value) => ASObject.AS_toUint((ASObject)(object)value);

            protected private override void convertSpanImpl(ReadOnlySpan<T> src, Span<uint> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_toUint((ASObject)(object)src[i]);
            }
        }

        internal sealed class Object2Number<T> : GenericTypeConverter<T, double> where T : class {
            public override double convert(T value) => ASObject.AS_toNumber((ASObject)(object)value);

            protected private override void convertSpanImpl(ReadOnlySpan<T> src, Span<double> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_toNumber((ASObject)(object)src[i]);
            }
        }

        internal sealed class Object2Boolean<T> : GenericTypeConverter<T, bool> where T : class {
            public override bool convert(T value) => ASObject.AS_toBoolean((ASObject)(object)value);

            protected private override void convertSpanImpl(ReadOnlySpan<T> src, Span<bool> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_toBoolean((ASObject)(object)src[i]);
            }
        }

        internal sealed class Object2String<T> : GenericTypeConverter<T, string> where T : class {
            public override string convert(T value) => ASObject.AS_coerceString((ASObject)(object)value);

            protected private override void convertSpanImpl(ReadOnlySpan<T> src, Span<string> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_coerceString((ASObject)(object)src[i]);
            }
        }

        internal sealed class Object2Object<TSource, TDest> : GenericTypeConverter<TSource, TDest>
            where TSource : class
            where TDest : class
        {
            private static readonly bool s_isUpCast = typeof(TDest).IsAssignableFrom(typeof(TSource));

            public override TDest convert(TSource value) => s_isUpCast ? (TDest)(object)value : ASObject.AS_cast<TDest>(value);

            protected private override void convertSpanImpl(ReadOnlySpan<TSource> src, Span<TDest> dst) {
                if (s_isUpCast) {
                    for (int i = 0; i < src.Length; i++)
                        dst[i] = (TDest)(object)src[i];
                }
                else {
                    for (int i = 0; i < src.Length; i++)
                        dst[i] = ASObject.AS_cast<TDest>(src[i]);
                }
            }
        }

        internal sealed class Object2Any<T> : GenericTypeConverter<T, ASAny> where T : class {
            public override ASAny convert(T value) => new ASAny((ASObject)(object)value);

            protected private override void convertSpanImpl(ReadOnlySpan<T> src, Span<ASAny> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = new ASAny((ASObject)(object)src[i]);
            }
        }

        #endregion

        #region String => (TDest)

        internal sealed class String2Int : GenericTypeConverter<string, int> {
            public override int convert(string value) => ASString.AS_toInt(value);

            protected private override void convertSpanImpl(ReadOnlySpan<string> src, Span<int> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASString.AS_toInt(src[i]);
            }
        }

        internal sealed class String2Uint : GenericTypeConverter<string, uint> {
            public override uint convert(string value) => ASString.AS_toUint(value);

            protected private override void convertSpanImpl(ReadOnlySpan<string> src, Span<uint> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASString.AS_toUint(src[i]);
            }
        }

        internal sealed class String2Number : GenericTypeConverter<string, double> {
            public override double convert(string value) => ASString.AS_toNumber(value);

            protected private override void convertSpanImpl(ReadOnlySpan<string> src, Span<double> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASString.AS_toNumber(src[i]);
            }
        }

        internal sealed class String2Boolean : GenericTypeConverter<string, bool> {
            public override bool convert(string value) => ASString.AS_toBoolean(value);

            protected private override void convertSpanImpl(ReadOnlySpan<string> src, Span<bool> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASString.AS_toBoolean(src[i]);
            }
        }

        internal sealed class String2Object : GenericTypeConverter<string, ASObject> {
            public override ASObject convert(string value) => ASObject.AS_fromString(value);

            protected private override void convertSpanImpl(ReadOnlySpan<string> src, Span<ASObject> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_fromString(src[i]);
            }
        }

        internal sealed class String2Any : GenericTypeConverter<string, ASAny> {
            public override ASAny convert(string value) => ASAny.AS_fromString(value);

            protected private override void convertSpanImpl(ReadOnlySpan<string> src, Span<ASAny> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_fromString(src[i]);
            }
        }

        #endregion

        #region uint => (TDest)

        internal sealed class Uint2Int : GenericTypeConverter<uint, int> {
            public override int convert(uint value) => (int)value;

            protected private override void convertSpanImpl(ReadOnlySpan<uint> src, Span<int> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = (int)src[i];
            }
        }

        internal sealed class Uint2Number : GenericTypeConverter<uint, double> {
            public override double convert(uint value) => (double)value;

            protected private override void convertSpanImpl(ReadOnlySpan<uint> src, Span<double> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = (double)src[i];
            }
        }

        internal sealed class Uint2String : GenericTypeConverter<uint, string> {
            public override string convert(uint value) => ASuint.AS_convertString(value);

            protected private override void convertSpanImpl(ReadOnlySpan<uint> src, Span<string> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASuint.AS_convertString(src[i]);
            }
        }

        internal sealed class Uint2Boolean : GenericTypeConverter<uint, bool> {
            public override bool convert(uint value) => value != 0u;

            protected private override void convertSpanImpl(ReadOnlySpan<uint> src, Span<bool> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = src[i] != 0u;
            }
        }

        internal sealed class Uint2Object : GenericTypeConverter<uint, ASObject> {
            public override ASObject convert(uint value) => ASObject.AS_fromUint(value);

            protected private override void convertSpanImpl(ReadOnlySpan<uint> src, Span<ASObject> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_fromUint(src[i]);
            }
        }

        internal sealed class Uint2Any : GenericTypeConverter<uint, ASAny> {
            public override ASAny convert(uint value) => ASAny.AS_fromUint(value);

            protected private override void convertSpanImpl(ReadOnlySpan<uint> src, Span<ASAny> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_fromUint(src[i]);
            }
        }

        #endregion

        #region Special

        /// <summary>
        /// The GenericTypeConverter implementation for converting from a type to the same type.
        /// </summary>
        internal sealed class Identity<T> : GenericTypeConverter<T, T> {
            public override T convert(T value)  => value;
            protected private override void convertSpanImpl(ReadOnlySpan<T> src, Span<T> dst) => src.CopyTo(dst.Slice(0, src.Length));
        }

        /// <summary>
        /// The GenericTypeConverter implementation for invalid conversions except for null.
        /// </summary>
        internal sealed class InvalidExceptNull<TSource, TDest> : GenericTypeConverter<TSource, TDest>
            where TSource : class
            where TDest : class
        {
            public override TDest convert(TSource value) {
                if (value == null)
                    return null;
                throw ErrorHelper.createCastError(typeof(TSource), typeof(TDest));
            }

            protected private override void convertSpanImpl(ReadOnlySpan<TSource> src, Span<TDest> dst) {
                for (int i = 0; i < src.Length; i++) {
                    if (src[i] != null)
                        throw ErrorHelper.createCastError(typeof(TSource), typeof(TDest));
                    dst[i] = null;
                }
            }
        }

        /// <summary>
        /// The GenericTypeConverter implementation for invalid conversions (this throws errors)
        /// </summary>
        internal sealed class Invalid<TSource, TDest> : GenericTypeConverter<TSource, TDest> {
            public override TDest convert(TSource value) =>
                throw ErrorHelper.createCastError(typeof(TSource), typeof(TDest));

            protected private override void convertSpanImpl(ReadOnlySpan<TSource> src, Span<TDest> dst) {
                if (!src.IsEmpty)
                    throw ErrorHelper.createCastError(typeof(TSource), typeof(TDest));
            }
        }

        #endregion

    }

}
