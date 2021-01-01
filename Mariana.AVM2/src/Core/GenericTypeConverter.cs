using System;
using System.Reflection;
using System.Reflection.Emit;
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
        private static Lazy<GenericTypeConverter<TSource, TDest>> s_lazyInstance =
                new Lazy<GenericTypeConverter<TSource, TDest>>(_createInstance);

        /// <summary>
        /// Gets the type converter instance that converts from type <typeparamref name="TSource"/>
        /// to type <typeparamref name="TDest"/>.
        /// </summary>
        public static GenericTypeConverter<TSource, TDest> instance {
            get {
                return s_lazyInstance.Value;
            }
        }

        internal GenericTypeConverter() { }

        /// <summary>
        /// Converts the specified object from the source type to the destination type.
        /// </summary>
        /// <param name="o">The object to convert.</param>
        /// <returns>The object converted to the destination type.</returns>
        public abstract TDest convert(TSource o);

        /// <summary>
        /// Converts elements from the source span, and writes the converted objects to the destination span.
        /// </summary>
        /// <param name="src">The source span.</param>
        /// <param name="dst">The destination span. This must have the same length as <paramref name="src"/>.</param>
        public abstract void convertSpan(ReadOnlySpan<TSource> src, Span<TDest> dst);

        /// <summary>
        /// Converts elements from the source span, and returns an array containing the converted elements.
        /// </summary>
        /// <param name="src">The source span.</param>
        public TDest[] convertSpan(ReadOnlySpan<TSource> src) {
            TDest[] dst = new TDest[src.Length];
            convertSpan(src, dst);
            return dst;
        }

        /// <summary>
        /// Creates a <see cref="GenericTypeConverter{TSource, TDest}"/> instance for the specified
        /// source and destination type parameters and returns it.
        /// </summary>
        private static GenericTypeConverter<TSource, TDest> _createInstance() {
            Type tSource = typeof(TSource);
            Type tDest = typeof(TDest);
            object inst;

            if (tSource == tDest) {
                inst = new InternalGenericTypeConverters.Identity<TSource>();
            }
            else if (tSource == typeof(int)) {
                if (tDest == typeof(uint))
                    inst = new InternalGenericTypeConverters.Int2Uint();
                else if (tDest == typeof(double))
                    inst = new InternalGenericTypeConverters.Int2Number();
                else if (tDest == typeof(string))
                    inst = new InternalGenericTypeConverters.Int2String();
                else if (tDest == typeof(bool))
                    inst = new InternalGenericTypeConverters.Int2Boolean();
                else if (tDest == typeof(ASAny))
                    inst = new InternalGenericTypeConverters.Int2Any();
                else if (tDest == typeof(ASObject))
                    inst = new InternalGenericTypeConverters.Int2Object();
                else
                    inst = new InternalGenericTypeConverters.Invalid<TSource, TDest>();
            }
            else if (tSource == typeof(uint)) {
                if (tDest == typeof(int))
                    inst = new InternalGenericTypeConverters.Uint2Int();
                else if (tDest == typeof(double))
                    inst = new InternalGenericTypeConverters.Uint2Number();
                else if (tDest == typeof(string))
                    inst = new InternalGenericTypeConverters.Uint2String();
                else if (tDest == typeof(bool))
                    inst = new InternalGenericTypeConverters.Uint2Boolean();
                else if (tDest == typeof(ASAny))
                    inst = new InternalGenericTypeConverters.Uint2Any();
                else if (tDest == typeof(ASObject))
                    inst = new InternalGenericTypeConverters.Uint2Object();
                else
                    inst = new InternalGenericTypeConverters.Invalid<TSource, TDest>();
            }
            else if (tSource == typeof(double)) {
                if (tDest == typeof(int))
                    inst = new InternalGenericTypeConverters.Number2Int();
                else if (tDest == typeof(uint))
                    inst = new InternalGenericTypeConverters.Number2Uint();
                else if (tDest == typeof(string))
                    inst = new InternalGenericTypeConverters.Number2String();
                else if (tDest == typeof(bool))
                    inst = new InternalGenericTypeConverters.Number2Boolean();
                else if (tDest == typeof(ASAny))
                    inst = new InternalGenericTypeConverters.Number2Any();
                else if (tDest == typeof(ASObject))
                    inst = new InternalGenericTypeConverters.Number2Object();
                else
                    inst = new InternalGenericTypeConverters.Invalid<TSource, TDest>();
            }
            else if (tSource == typeof(string)) {
                if (tDest == typeof(int))
                    inst = new InternalGenericTypeConverters.String2Int();
                else if (tDest == typeof(uint))
                    inst = new InternalGenericTypeConverters.String2Uint();
                else if (tDest == typeof(double))
                    inst = new InternalGenericTypeConverters.String2Number();
                else if (tDest == typeof(bool))
                    inst = new InternalGenericTypeConverters.String2Boolean();
                else if (tDest == typeof(ASAny))
                    inst = new InternalGenericTypeConverters.String2Any();
                else if (tDest == typeof(ASObject))
                    inst = new InternalGenericTypeConverters.String2Object();
                else
                    inst = new InternalGenericTypeConverters.Invalid<TSource, TDest>();
            }
            else if (tSource == typeof(bool)) {
                if (tDest == typeof(int))
                    inst = new InternalGenericTypeConverters.Boolean2Int();
                else if (tDest == typeof(uint))
                    inst = new InternalGenericTypeConverters.Boolean2Uint();
                else if (tDest == typeof(double))
                    inst = new InternalGenericTypeConverters.Boolean2Number();
                else if (tDest == typeof(string))
                    inst = new InternalGenericTypeConverters.Boolean2String();
                else if (tDest == typeof(ASAny))
                    inst = new InternalGenericTypeConverters.Boolean2Any();
                else if (tDest == typeof(ASObject))
                    inst = new InternalGenericTypeConverters.Boolean2Object();
                else
                    inst = new InternalGenericTypeConverters.Invalid<TSource, TDest>();
            }
            else if (tSource == typeof(ASAny)) {
                if (tDest == typeof(int)) {
                    inst = new InternalGenericTypeConverters.Any2Int();
                }
                else if (tDest == typeof(uint)) {
                    inst = new InternalGenericTypeConverters.Any2Uint();
                }
                else if (tDest == typeof(double)) {
                    inst = new InternalGenericTypeConverters.Any2Number();
                }
                else if (tDest == typeof(string)) {
                    inst = new InternalGenericTypeConverters.Any2String();
                }
                else if (tDest == typeof(bool)) {
                    inst = new InternalGenericTypeConverters.Any2Boolean();
                }
                else if (typeof(ASObject).IsAssignableFrom(tDest)) {
                    inst = Activator.CreateInstance(
                        typeof(InternalGenericTypeConverters.Any2Object<>).MakeGenericType(tDest));
                }
                else {
                    inst = new InternalGenericTypeConverters.Invalid<TSource, TDest>();
                }
            }
            else if (tSource.IsInterface || typeof(ASObject).IsAssignableFrom(tSource)) {
                Type converterType = null;
                bool converterHasDest = false;
                inst = null;

                if (tDest == typeof(int)) {
                    converterType = typeof(InternalGenericTypeConverters.Object2Int<>);
                }
                else if (tDest == typeof(uint)) {
                    converterType = typeof(InternalGenericTypeConverters.Object2Uint<>);
                }
                else if (tDest == typeof(double)) {
                    converterType = typeof(InternalGenericTypeConverters.Object2Number<>);
                }
                else if (tDest == typeof(string)) {
                    converterType = typeof(InternalGenericTypeConverters.Object2String<>);
                }
                else if (tDest == typeof(bool)) {
                    converterType = typeof(InternalGenericTypeConverters.Object2Boolean<>);
                }
                else if (tDest == typeof(ASAny)) {
                    converterType = typeof(InternalGenericTypeConverters.Object2Any<>);
                }
                else if (tSource.IsInterface || tDest.IsInterface
                    || (tDest.IsAssignableFrom(tSource) && typeof(ASObject).IsAssignableFrom(tDest))
                    || tSource.IsAssignableFrom(tDest))
                {
                    converterType = typeof(InternalGenericTypeConverters.Object2Object<,>);
                    converterHasDest = true;
                }
                else {
                    inst = new InternalGenericTypeConverters.Invalid<TSource, TDest>();
                }

                if (inst == null) {
                    converterType = converterHasDest
                        ? converterType.MakeGenericType(tSource, tDest)
                        : converterType.MakeGenericType(tSource);
                    inst = Activator.CreateInstance(converterType);
                }
            }
            else {
                inst = new InternalGenericTypeConverters.Invalid<TSource, TDest>();
            }

            return (GenericTypeConverter<TSource, TDest>)inst;
        }

    }

    internal static class InternalGenericTypeConverters {

        #region ASAny => (TDest)

        internal sealed class Any2Int : GenericTypeConverter<ASAny, int> {
            public override int convert(ASAny o) => ASAny.AS_toInt(o);

            public override void convertSpan(ReadOnlySpan<ASAny> src, Span<int> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_toInt(src[i]);
            }
        }

        internal sealed class Any2Uint : GenericTypeConverter<ASAny, uint> {
            public override uint convert(ASAny o) => ASAny.AS_toUint(o);

            public override void convertSpan(ReadOnlySpan<ASAny> src, Span<uint> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_toUint(src[i]);
            }
        }

        internal sealed class Any2Number : GenericTypeConverter<ASAny, double> {
            public override double convert(ASAny o) => ASAny.AS_toNumber(o);

            public override void convertSpan(ReadOnlySpan<ASAny> src, Span<double> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_toNumber(src[i]);
            }
        }

        internal sealed class Any2String : GenericTypeConverter<ASAny, string> {
            public override string convert(ASAny o) => ASAny.AS_coerceString(o);

            public override void convertSpan(ReadOnlySpan<ASAny> src, Span<string> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_coerceString(src[i]);
            }
        }

        internal sealed class Any2Boolean : GenericTypeConverter<ASAny, bool> {
            public override bool convert(ASAny o) => ASAny.AS_toBoolean(o);

            public override void convertSpan(ReadOnlySpan<ASAny> src, Span<bool> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_toBoolean(src[i]);
            }
        }

        internal sealed class Any2Object<T> : GenericTypeConverter<ASAny, T> where T : class {
            public override T convert(ASAny o) => ASAny.AS_cast<T>(o);

            public override void convertSpan(ReadOnlySpan<ASAny> src, Span<T> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_cast<T>(src[i]);
            }
        }

        #endregion

        #region Boolean => (TDest)

        internal sealed class Boolean2Int : GenericTypeConverter<bool, int> {
            public override int convert(bool o) => o ? 1 : 0;

            public override void convertSpan(ReadOnlySpan<bool> src, Span<int> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = src[i] ? 1 : 0;
            }
        }

        internal sealed class Boolean2Uint : GenericTypeConverter<bool, uint> {
            public override uint convert(bool o) => o ? 1u : 0u;

            public override void convertSpan(ReadOnlySpan<bool> src, Span<uint> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = src[i] ? 1u : 0u;
            }
        }

        internal sealed class Boolean2Number : GenericTypeConverter<bool, double> {
            public override double convert(bool o) => o ? 1.0 : 0.0;

            public override void convertSpan(ReadOnlySpan<bool> src, Span<double> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = src[i] ? 1.0 : 0.0;
            }
        }

        internal sealed class Boolean2String : GenericTypeConverter<bool, string> {
            public override string convert(bool o) => ASBoolean.AS_convertString(o);

            public override void convertSpan(ReadOnlySpan<bool> src, Span<string> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASBoolean.AS_convertString(src[i]);
            }
        }

        internal sealed class Boolean2Object : GenericTypeConverter<bool, ASObject> {
            public override ASObject convert(bool o) => ASObject.AS_fromBoolean(o);

            public override void convertSpan(ReadOnlySpan<bool> src, Span<ASObject> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_fromBoolean(src[i]);
            }
        }

        internal sealed class Boolean2Any : GenericTypeConverter<bool, ASAny> {
            public override ASAny convert(bool o) => ASAny.AS_fromBoolean(o);

            public override void convertSpan(ReadOnlySpan<bool> src, Span<ASAny> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_fromBoolean(src[i]);
            }
        }

        #endregion

        #region int => (TDest)

        internal sealed class Int2Uint : GenericTypeConverter<int, uint> {
            public override uint convert(int o) => (uint)o;

            public override void convertSpan(ReadOnlySpan<int> src, Span<uint> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = (uint)src[i];
            }
        }

        internal sealed class Int2Number : GenericTypeConverter<int, double> {
            public override double convert(int o) => (double)o;

            public override void convertSpan(ReadOnlySpan<int> src, Span<double> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = (double)src[i];
            }
        }

        internal sealed class Int2String : GenericTypeConverter<int, string> {
            public override string convert(int o) => ASint.AS_convertString(o);

            public override void convertSpan(ReadOnlySpan<int> src, Span<string> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASint.AS_convertString(src[i]);
            }
        }

        internal sealed class Int2Boolean : GenericTypeConverter<int, bool> {
            public override bool convert(int o) => o != 0;

            public override void convertSpan(ReadOnlySpan<int> src, Span<bool> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = src[i] != 0;
            }
        }

        internal sealed class Int2Object : GenericTypeConverter<int, ASObject> {
            public override ASObject convert(int o) => ASObject.AS_fromInt(o);

            public override void convertSpan(ReadOnlySpan<int> src, Span<ASObject> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_fromInt(src[i]);
            }
        }

        internal sealed class Int2Any : GenericTypeConverter<int, ASAny> {
            public override ASAny convert(int o) => ASAny.AS_fromInt(o);

            public override void convertSpan(ReadOnlySpan<int> src, Span<ASAny> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_fromInt(src[i]);
            }
        }

        #endregion

        #region Number => (TDest)

        internal sealed class Number2Int : GenericTypeConverter<double, int> {
            public override int convert(double o) => ASNumber.AS_toInt(o);

            public override void convertSpan(ReadOnlySpan<double> src, Span<int> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASNumber.AS_toInt(src[i]);
            }
        }

        internal sealed class Number2Uint : GenericTypeConverter<double, uint> {
            public override uint convert(double o) => ASNumber.AS_toUint(o);

            public override void convertSpan(ReadOnlySpan<double> src, Span<uint> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASNumber.AS_toUint(src[i]);
            }
        }

        internal sealed class Number2String : GenericTypeConverter<double, string> {
            public override string convert(double o) => ASNumber.AS_convertString(o);

            public override void convertSpan(ReadOnlySpan<double> src, Span<string> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASNumber.AS_convertString(src[i]);
            }
        }

        internal sealed class Number2Boolean : GenericTypeConverter<double, bool> {
            public override bool convert(double o) => ASNumber.AS_toBoolean(o);

            public override void convertSpan(ReadOnlySpan<double> src, Span<bool> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASNumber.AS_toBoolean(src[i]);
            }
        }

        internal sealed class Number2Object : GenericTypeConverter<double, ASObject> {
            public override ASObject convert(double o) => ASObject.AS_fromNumber(o);

            public override void convertSpan(ReadOnlySpan<double> src, Span<ASObject> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_fromNumber(src[i]);
            }
        }

        internal sealed class Number2Any : GenericTypeConverter<double, ASAny> {
            public override ASAny convert(double o) => ASAny.AS_fromNumber(o);

            public override void convertSpan(ReadOnlySpan<double> src, Span<ASAny> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_fromNumber(src[i]);
            }
        }

        #endregion

        #region Object => (TDest)

        internal sealed class Object2Int<T> : GenericTypeConverter<T, int> where T : class {
            public override int convert(T o) => ASObject.AS_toInt((ASObject)(object)o);

            public override void convertSpan(ReadOnlySpan<T> src, Span<int> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_toInt((ASObject)(object)src[i]);
            }
        }

        internal sealed class Object2Uint<T> : GenericTypeConverter<T, uint> where T : class {
            public override uint convert(T o) => ASObject.AS_toUint((ASObject)(object)o);

            public override void convertSpan(ReadOnlySpan<T> src, Span<uint> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_toUint((ASObject)(object)src[i]);
            }
        }

        internal sealed class Object2Number<T> : GenericTypeConverter<T, double> where T : class {
            public override double convert(T o) => ASObject.AS_toNumber((ASObject)(object)o);

            public override void convertSpan(ReadOnlySpan<T> src, Span<double> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_toNumber((ASObject)(object)src[i]);
            }
        }

        internal sealed class Object2Boolean<T> : GenericTypeConverter<T, bool> where T : class {
            public override bool convert(T o) => ASObject.AS_toBoolean((ASObject)(object)o);

            public override void convertSpan(ReadOnlySpan<T> src, Span<bool> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_toBoolean((ASObject)(object)src[i]);
            }
        }

        internal sealed class Object2String<T> : GenericTypeConverter<T, string> where T : class {
            public override string convert(T o) => ASObject.AS_coerceString((ASObject)(object)o);

            public override void convertSpan(ReadOnlySpan<T> src, Span<string> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_coerceString((ASObject)(object)src[i]);
            }
        }

        internal sealed class Object2Object<TSource, TDest> : GenericTypeConverter<TSource, TDest>
            where TSource : class
            where TDest : class
        {
            private static readonly bool s_isUpCast = typeof(TDest).IsAssignableFrom(typeof(TSource));

            public override TDest convert(TSource o) => s_isUpCast ? (TDest)(object)o : ASObject.AS_cast<TDest>(o);

            public override void convertSpan(ReadOnlySpan<TSource> src, Span<TDest> dst) {
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
            public override ASAny convert(T o) => new ASAny((ASObject)(object)o);

            public override void convertSpan(ReadOnlySpan<T> src, Span<ASAny> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = new ASAny((ASObject)(object)src[i]);
            }
        }

        #endregion

        #region String => (TDest)

        internal sealed class String2Int : GenericTypeConverter<string, int> {
            public override int convert(string o) => ASString.AS_toInt(o);

            public override void convertSpan(ReadOnlySpan<string> src, Span<int> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASString.AS_toInt(src[i]);
            }
        }

        internal sealed class String2Uint : GenericTypeConverter<string, uint> {
            public override uint convert(string o) => ASString.AS_toUint(o);

            public override void convertSpan(ReadOnlySpan<string> src, Span<uint> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASString.AS_toUint(src[i]);
            }
        }

        internal sealed class String2Number : GenericTypeConverter<string, double> {
            public override double convert(string o) => ASString.AS_toNumber(o);

            public override void convertSpan(ReadOnlySpan<string> src, Span<double> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASString.AS_toNumber(src[i]);
            }
        }

        internal sealed class String2Boolean : GenericTypeConverter<string, bool> {
            public override bool convert(string o) => ASString.AS_toBoolean(o);

            public override void convertSpan(ReadOnlySpan<string> src, Span<bool> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASString.AS_toBoolean(src[i]);
            }
        }

        internal sealed class String2Object : GenericTypeConverter<string, ASObject> {
            public override ASObject convert(string o) => ASObject.AS_fromString(o);

            public override void convertSpan(ReadOnlySpan<string> src, Span<ASObject> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_fromString(src[i]);
            }
        }

        internal sealed class String2Any : GenericTypeConverter<string, ASAny> {
            public override ASAny convert(string o) => ASAny.AS_fromString(o);

            public override void convertSpan(ReadOnlySpan<string> src, Span<ASAny> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASAny.AS_fromString(src[i]);
            }
        }

        #endregion

        #region uint => (TDest)

        internal sealed class Uint2Int : GenericTypeConverter<uint, int> {
            public override int convert(uint o) => (int)o;

            public override void convertSpan(ReadOnlySpan<uint> src, Span<int> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = (int)src[i];
            }
        }

        internal sealed class Uint2Number : GenericTypeConverter<uint, double> {
            public override double convert(uint o) => (double)o;

            public override void convertSpan(ReadOnlySpan<uint> src, Span<double> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = (double)src[i];
            }
        }

        internal sealed class Uint2String : GenericTypeConverter<uint, string> {
            public override string convert(uint o) => ASuint.AS_convertString(o);

            public override void convertSpan(ReadOnlySpan<uint> src, Span<string> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASuint.AS_convertString(src[i]);
            }
        }

        internal sealed class Uint2Boolean : GenericTypeConverter<uint, bool> {
            public override bool convert(uint o) => o != 0u;

            public override void convertSpan(ReadOnlySpan<uint> src, Span<bool> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = src[i] != 0u;
            }
        }

        internal sealed class Uint2Object : GenericTypeConverter<uint, ASObject> {
            public override ASObject convert(uint o) => ASObject.AS_fromUint(o);

            public override void convertSpan(ReadOnlySpan<uint> src, Span<ASObject> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = ASObject.AS_fromUint(src[i]);
            }
        }

        internal sealed class Uint2Any : GenericTypeConverter<uint, ASAny> {
            public override ASAny convert(uint o) => ASAny.AS_fromUint(o);

            public override void convertSpan(ReadOnlySpan<uint> src, Span<ASAny> dst) {
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
            public override T convert(T o)  => o;
            public override void convertSpan(ReadOnlySpan<T> src, Span<T> dst) => src.CopyTo(dst.Slice(0, src.Length));
        }

        /// <summary>
        /// The GenericTypeConverter implementation for invalid conversions (this throws errors)
        /// </summary>
        internal sealed class Invalid<TSource, TDest> : GenericTypeConverter<TSource, TDest> {
            public override TDest convert(TSource o) =>
                throw ErrorHelper.createCastError(typeof(TSource), typeof(TDest));

            public override void convertSpan(ReadOnlySpan<TSource> src, Span<TDest> dst) =>
                throw ErrorHelper.createCastError(typeof(TSource), typeof(TDest));
        }

        /// <summary>
        /// A converter that converts between two types by converting to the Object type first, and
        /// then from the Object type to the destination type.
        /// </summary>
        internal sealed class GenericObject<TSource, TDest> : GenericTypeConverter<TSource, TDest> {
            private static readonly GenericTypeConverter<TSource, ASObject> m_srcToObj =
                GenericTypeConverter<TSource, ASObject>.instance;

            private static readonly GenericTypeConverter<ASObject, TDest> m_objToDst =
                GenericTypeConverter<ASObject, TDest>.instance;

            public override TDest convert(TSource o) => m_objToDst.convert(m_srcToObj.convert(o));

            public override void convertSpan(ReadOnlySpan<TSource> src, Span<TDest> dst) {
                for (int i = 0; i < src.Length; i++)
                    dst[i] = m_objToDst.convert(m_srcToObj.convert(src[i]));
            }
        }

        #endregion

    }

}
