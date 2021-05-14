using System;
using System.Collections.Generic;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Specifies the type of comparisons that a GenericComparer returned by the
    /// <see cref="GenericComparer{T}.getComparer(GenericComparerType)" qualifyHint="true"/>
    /// method must perform.
    /// </summary>
    public enum GenericComparerType {

        /// <summary>
        /// The default comparison semantics for the type of the values being compared are used. For
        /// strings, the comparison is case-sensitive.
        /// </summary>
        DEFAULT,

        /// <summary>
        /// The values will always be compared numerically, regardless of the their type. Values of
        /// non-numeric types will be converted into numbers and compared.
        /// </summary>
        NUMERIC,

        /// <summary>
        /// The string representations of the values will be compared, regardless of the their type.
        /// If the type is not String, values will be converted to strings and compared. The
        /// comparison is case-sensitive.
        /// </summary>
        STRING,

        /// <summary>
        /// The string representations of the values will be compared, regardless of the their type.
        /// If the type is not String, values will be converted to strings and compared. The
        /// comparison is case-insensitive.
        /// </summary>
        STRING_IGNORECASE,

    }

    /// <summary>
    /// A GenericComparer is used for comparing values of a particular type (usually a type
    /// parameter from another generic type, where the actual type and hence the method of
    /// comparison is not known).
    /// </summary>
    ///
    /// <typeparam name="T">The type of the values to compare.</typeparam>
    public abstract class GenericComparer<T> : IComparer<T>, IEqualityComparer<T> {

        private struct ComparerSet {
            public GenericComparer<T> defaultCmp;
            public GenericComparer<T> numericCmp;
            public GenericComparer<T> stringCmp;
            public GenericComparer<T> stringCmpIgnoreCase;
        }

        private static LazyInitObject<ComparerSet> s_lazyComparerSet = new LazyInitObject<ComparerSet>(_createComparerSet);

        /// <summary>
        /// Gets the instance of the default <see cref="GenericComparer{T}"/> for the given type argument.
        /// </summary>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>Error #10049</term>
        /// <description><typeparamref name="T"/> is not a supported type.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// Only the following type arguments can be used with this property: <see cref="Int32"/>,
        /// <see cref="UInt32"/>, <see cref="Double"/>, <see cref="String"/>,
        /// <see cref="Boolean"/>, interface types, the <see cref="ASAny"/> type and any type
        /// inheriting from the <see cref="ASObject"/> class.
        /// </remarks>
        public static GenericComparer<T> defaultComparer {
            get {
                GenericComparer<T> cmp = s_lazyComparerSet.value.defaultCmp;
                if (cmp == null)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__GENERIC_COMPARER_INVALID_TYPE, typeof(T));
                return cmp;
            }
        }

        /// <summary>
        /// Gets the instance of <see cref="GenericComparer{T}"/> for the specified comparison type.
        /// </summary>
        /// <param name="type">The kind of comparisons that the returned
        /// <see cref="GenericComparer{T}"/> instance must perform.</param>
        /// <returns>The <see cref="GenericComparer{T}"/> instance.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #10049</term>
        /// <description><paramref name="type"/> is
        /// <see cref="GenericComparerType.DEFAULT" qualifyHint="true"/> and
        /// <typeparamref name="T"/> is not a supported type.</description>
        /// </item>
        /// <item>
        /// <term>TypeError #10061</term>
        /// <description><paramref name="type"/> is not a valid value.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// Only the following type arguments can be used with this method with
        /// <paramref name="type"/> set to
        /// <see cref="GenericComparerType.DEFAULT" qualifyHint="true"/>: <see cref="Int32"/>,
        /// <see cref="UInt32"/>, <see cref="Double"/>, <see cref="String"/>,
        /// <see cref="Boolean"/>, interface types, the <see cref="ASAny"/> type and any type
        /// inheriting from the <see cref="ASObject"/> class. For any other type, this method will
        /// throw an exception if <paramref name="type"/> is set to
        /// <see cref="GenericComparerType.DEFAULT" qualifyHint="true"/>. When
        /// <paramref name="type"/> is set to any other value and an unsupported type argument is
        /// used, an error is not thrown immediately; however, calling any methods on the returned
        /// comparer may throw invalid conversion errors.
        /// </remarks>
        public static GenericComparer<T> getComparer(GenericComparerType type) {
            switch (type) {
                case GenericComparerType.DEFAULT:
                    return defaultComparer;
                case GenericComparerType.NUMERIC:
                    return s_lazyComparerSet.value.numericCmp;
                case GenericComparerType.STRING:
                    return s_lazyComparerSet.value.stringCmp;
                case GenericComparerType.STRING_IGNORECASE:
                    return s_lazyComparerSet.value.stringCmpIgnoreCase;
                default:
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(type));
            }
        }

        /// <summary>
        /// Creates a <see cref="GenericComparer{T}"/> instance which compares using a callback
        /// function.
        /// </summary>
        ///
        /// <param name="compareFunc">The comparison callback function. This function must accept two
        /// arguments of type <typeparamref name="T"/>; and return a negative integer, zero or a
        /// positive integer if the first argument is less than, equal to or greater than the second
        /// argument, respectively.</param>
        /// <param name="createDelegateComparer">
        /// If set to true, attempts to create a delegate comparer. A delegate comparer does faster
        /// comparisons by eliminating the overhead of dynamic function calls (and hence is useful for
        /// a large number of comparisons, such as for sorting a large array), but is slower to
        /// create. If the attempt fails, or if this parameter is set to false, a general function
        /// comparer is created that dynamically invokes the function for each comparison.
        /// </param>
        ///
        /// <returns>A <see cref="GenericComparer{T}"/> instance that uses the callback function for
        /// comparison.</returns>
        ///
        /// <remarks>
        /// It is not recommended to use the comparers returned by this method for hash tables, as
        /// they cannot generate hash codes. (The implementation of
        /// <see cref="IEqualityComparer{T}.GetHashCode" qualifyHint="true"/> will always
        /// return 0.)
        /// </remarks>
        public static GenericComparer<T> getComparer(ASFunction compareFunc, bool createDelegateComparer = false) {
            if (compareFunc == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(compareFunc));

            if (createDelegateComparer) {
                Comparison<T> del = compareFunc.createDelegate<Comparison<T>>();
                if (del != null)
                    return new InternalGenericComparers.Delegate<T>(del);
            }

            return new InternalGenericComparers.DynamicFunc<T>(compareFunc);
        }

        internal GenericComparer() { }

        /// <summary>
        /// Returns a Boolean value indicating whether <paramref name="x"/> is equal to
        /// <paramref name="x"/>.
        /// </summary>
        /// <param name="x">The first value.</param>
        /// <param name="y">The second value.</param>
        /// <returns>True if <paramref name="x"/> is equal to <paramref name="y"/>, otherwise
        /// false.</returns>
        public abstract bool Equals(T x, T y);

        /// <summary>
        /// Compares <paramref name="x"/> and <paramref name="y"/> and returns an integer. The
        /// returned value is less than 0 if <paramref name="x"/> is less than
        /// <paramref name="y"/>, 0 if <paramref name="x"/> is equal to <paramref name="y"/>,
        /// and greater than 0 if <paramref name="x"/> is greater than <paramref name="y"/>.
        /// </summary>
        ///
        /// <param name="x">The first value.</param>
        /// <param name="y">The second value.</param>
        /// <returns>An integer value indicating the result of the comparison.</returns>
        public abstract int Compare(T x, T y);

        /// <summary>
        /// Gets a hash code for the specified object.
        /// </summary>
        /// <param name="x">The object.</param>
        /// <returns>A hash code for <paramref name="x"/>.</returns>
        public abstract int GetHashCode(T x);

        /// <summary>
        /// Returns the index of the first element in the given span whose value is equal to <paramref name="item"/>.
        /// </summary>
        /// <param name="span">The span in which to search.</param>
        /// <param name="item">The value to find in the span.</param>
        /// <returns>The index of the first element in the span whose value is equal to
        /// <paramref name="item"/>. If no element is found with that value, returns -1.</returns>
        public abstract int indexOf(ReadOnlySpan<T> span, T item);

        /// <summary>
        /// Returns the index of the last element in the given span whose value is equal to <paramref name="item"/>.
        /// </summary>
        /// <param name="span">The span in which to search.</param>
        /// <param name="item">The value to find in the span.</param>
        /// <returns>The index of the last element in the span whose value is equal to
        /// <paramref name="item"/>. If no element is found with that value, returns -1.</returns>
        public abstract int lastIndexOf(ReadOnlySpan<T> span, T item);

        /// <summary>
        /// Returns true if the given spans are equal. The spans are equal if they are of the same length
        /// and the elements at corresponding indices compare as equal.
        /// </summary>
        ///
        /// <param name="span1">The first span.</param>
        /// <param name="span2">The second span.</param>
        ///
        /// <returns>True, if the spans are equal, false otherwise.</returns>
        public abstract bool spansEqual(ReadOnlySpan<T> span1, ReadOnlySpan<T> span2);

        /// <summary>
        /// Returns true if the given arrays are equal. The arrays are equal if they are of the same length
        /// and the elements at corresponding indices compare as equal.
        /// </summary>
        ///
        /// <param name="arr1">The first array.</param>
        /// <param name="arr2">The second array.</param>
        ///
        /// <returns>True, if the arrays are equal, false otherwise.</returns>
        public bool arraysEqual(T[] arr1, T[] arr2) => arr1 == arr2 || spansEqual(arr1, arr2);

        private static ComparerSet _createComparerSet() {
            Type t = typeof(T);
            ComparerSet comparerSet = default;

            if (t == typeof(int)) {
                comparerSet.defaultCmp = (GenericComparer<T>)(object)new InternalGenericComparers.Int();
                comparerSet.numericCmp = comparerSet.defaultCmp;
            }
            else if (t == typeof(uint)) {
                comparerSet.defaultCmp = (GenericComparer<T>)(object)new InternalGenericComparers.Uint();
                comparerSet.numericCmp = comparerSet.defaultCmp;
            }
            else if (t == typeof(double)) {
                comparerSet.defaultCmp = (GenericComparer<T>)(object)new InternalGenericComparers.Number();
                comparerSet.numericCmp = comparerSet.defaultCmp;
            }
            else if (t == typeof(string)) {
                comparerSet.defaultCmp = (GenericComparer<T>)(object)new InternalGenericComparers.String(StringComparison.Ordinal);
                comparerSet.stringCmp = comparerSet.defaultCmp;
                comparerSet.stringCmpIgnoreCase = (GenericComparer<T>)(object)new InternalGenericComparers.String(StringComparison.OrdinalIgnoreCase);
            }
            else if (t == typeof(bool)) {
                comparerSet.defaultCmp = (GenericComparer<T>)(object)new InternalGenericComparers.Boolean();
            }
            else if (t == typeof(ASNamespace)) {
                comparerSet.defaultCmp = (GenericComparer<T>)(object)new InternalGenericComparers.Namespace();
            }
            else if (t == typeof(ASQName)) {
                comparerSet.defaultCmp = (GenericComparer<T>)(object)new InternalGenericComparers.QName();
            }
            else if (t == typeof(ASAny)) {
                comparerSet.defaultCmp = (GenericComparer<T>)(object)new InternalGenericComparers.AnyType();
            }
            else if (t.IsInterface) {
                comparerSet.defaultCmp =
                    (GenericComparer<T>)Activator.CreateInstance(typeof(InternalGenericComparers.ObjectInterface<>).MakeGenericType(t));
            }
            else if (typeof(ASObject).IsAssignableFrom(t)) {
                comparerSet.defaultCmp =
                    (GenericComparer<T>)Activator.CreateInstance(typeof(InternalGenericComparers.Object<>).MakeGenericType(t));
            }

            if (comparerSet.numericCmp == null)
                comparerSet.numericCmp = new InternalGenericComparers.GenericNum<T>();

            if (comparerSet.stringCmp == null)
                comparerSet.stringCmp = new InternalGenericComparers.GenericString<T>(StringComparison.Ordinal);

            if (comparerSet.stringCmpIgnoreCase == null)
                comparerSet.stringCmpIgnoreCase = new InternalGenericComparers.GenericString<T>(StringComparison.OrdinalIgnoreCase);

            return comparerSet;
        }

    }

    internal static class InternalGenericComparers {

        internal sealed class Int : GenericComparer<int> {
            public override bool Equals(int x, int y) => x == y;
            public override int Compare(int x, int y) => (x == y) ? 0 : ((x < y) ? -1 : 1);
            public override int GetHashCode(int x) => x;

            public override int indexOf(ReadOnlySpan<int> span, int item) => span.IndexOf(item);
            public override int lastIndexOf(ReadOnlySpan<int> span, int item) => span.LastIndexOf(item);

            public override bool spansEqual(ReadOnlySpan<int> span1, ReadOnlySpan<int> span2)
                => span1.SequenceEqual(span2);
        }

        internal sealed class Uint : GenericComparer<uint> {
            public override bool Equals(uint x, uint y) => x == y;
            public override int Compare(uint x, uint y) => (x == y) ? 0 : ((x < y) ? -1 : 1);
            public override int GetHashCode(uint x) => (int)x;

            public override int indexOf(ReadOnlySpan<uint> span, uint item) => span.IndexOf(item);
            public override int lastIndexOf(ReadOnlySpan<uint> span, uint item) => span.LastIndexOf(item);

            public override bool spansEqual(ReadOnlySpan<uint> span1, ReadOnlySpan<uint> span2)
                => span1.SequenceEqual(span2);
        }

        internal sealed class Number : GenericComparer<double> {
            public override bool Equals(double x, double y) => x == y;
            public override int GetHashCode(double x) => x.GetHashCode();

            public override int Compare(double x, double y) {
                if (x == y)
                    return 0;
                if (x < y)
                    return -1;
                if (Double.IsNaN(y))
                    return Double.IsNaN(x) ? 0 : -1;
                return 1;
            }

            public override int indexOf(ReadOnlySpan<double> span, double item) =>
                Double.IsNaN(item) ? -1 : span.IndexOf(item);

            public override int lastIndexOf(ReadOnlySpan<double> span, double item) =>
                Double.IsNaN(item) ? -1 : span.LastIndexOf(item);

            public override bool spansEqual(ReadOnlySpan<double> span1, ReadOnlySpan<double> span2) {
                // We can't use Span.SequenceEqual as it considers NaN == NaN.

                if (span1.Length != span2.Length)
                    return false;

                for (int i = 0; i < span1.Length; i++) {
                    if (span1[i] != span2[i])
                        return false;
                }

                return true;
            }

        }

        internal sealed class GenericNum<T> : GenericComparer<T> {

            private static readonly GenericTypeConverter<T, double> s_converter =
                    GenericTypeConverter<T, double>.instance;

            public override bool Equals(T x, T y) => s_converter.convert(x) == s_converter.convert(y);
            public override int GetHashCode(T x) => s_converter.convert(x).GetHashCode();

            public override int Compare(T x, T y) {
                double dx = s_converter.convert(x), dy = s_converter.convert(y);
                if (dx == dy)
                    return 0;
                if (dx < dy)
                    return -1;
                if (Double.IsNaN(dy))
                    return Double.IsNaN(dx) ? 0 : -1;
                return 1;
            }

            public override int indexOf(ReadOnlySpan<T> span, T item) {
                double itemNum = s_converter.convert(item);
                for (int i = 0; i < span.Length; i++) {
                    if (s_converter.convert(span[i]) == itemNum)
                        return i;
                }
                return -1;
            }

            public override int lastIndexOf(ReadOnlySpan<T> span, T item) {
                double itemNum = s_converter.convert(item);
                for (int i = span.Length - 1; i >= 0; i--) {
                    if (s_converter.convert(span[i]) == itemNum)
                        return i;
                }
                return -1;
            }

            public override bool spansEqual(ReadOnlySpan<T> span1, ReadOnlySpan<T> span2) {
                if (span1.Length != span2.Length)
                    return false;

                for (int i = 0; i < span1.Length; i++) {
                    if (s_converter.convert(span1[i]) != s_converter.convert(span2[i]))
                        return false;
                }

                return true;
            }

        }

        internal sealed class String : GenericComparer<string> {

            // The type of string comparison to use.
            private readonly StringComparison m_compareType;

            internal String(StringComparison compareType) {
                m_compareType = compareType;
            }

            public override bool Equals(string x, string y) => System.String.Equals(x, y, m_compareType);
            public override int Compare(string x, string y) => System.String.Compare(x, y, m_compareType);
            public override int GetHashCode(string x) => x.GetHashCode(m_compareType);

            public override int indexOf(ReadOnlySpan<string> span, string item) {
                for (int i = 0; i < span.Length; i++) {
                    if (System.String.Equals(span[i], item, m_compareType))
                        return i;
                }
                return -1;
            }

            public override int lastIndexOf(ReadOnlySpan<string> span, string item) {
                for (int i = span.Length - 1; i >= 0; i--) {
                    if (System.String.Equals(span[i], item, m_compareType))
                        return i;
                }
                return -1;
            }

            public override bool spansEqual(ReadOnlySpan<string> span1, ReadOnlySpan<string> span2) {
                if (span1.Length != span2.Length)
                    return false;

                for (int i = 0; i < span1.Length; i++) {
                    if (!System.String.Equals(span1[i], span2[i]))
                        return false;
                }

                return true;
            }

        }

        internal sealed class GenericString<T> : GenericComparer<T> {

            private static readonly GenericTypeConverter<T, string> s_converter =
                    GenericTypeConverter<T, string>.instance;

            private readonly StringComparison m_compareType;

            internal GenericString(StringComparison compareType) {
                m_compareType = compareType;
            }

            public override bool Equals(T x, T y) =>
                System.String.Equals(s_converter.convert(x), s_converter.convert(y), m_compareType);

            public override int Compare(T x, T y) =>
                System.String.Compare(s_converter.convert(x), s_converter.convert(y), m_compareType);

            public override int GetHashCode(T x) => s_converter.convert(x).GetHashCode(m_compareType);

            public override int indexOf(ReadOnlySpan<T> span, T item) {
                string itemStr = s_converter.convert(item);
                for (int i = 0; i < span.Length; i++) {
                    if (System.String.Equals(s_converter.convert(span[i]), itemStr))
                        return i;
                }
                return -1;
            }

            public override int lastIndexOf(ReadOnlySpan<T> span, T item) {
                string itemStr = s_converter.convert(item);
                for (int i = span.Length - 1; i >= 0; i--) {
                    if (System.String.Equals(s_converter.convert(span[i]), itemStr))
                        return i;
                }
                return -1;
            }

            public override bool spansEqual(ReadOnlySpan<T> span1, ReadOnlySpan<T> span2) {
                if (span1.Length != span2.Length)
                    return false;

                for (int i = 0; i < span1.Length; i++) {
                    if (!System.String.Equals(s_converter.convert(span1[i]), s_converter.convert(span2[i]), m_compareType))
                        return false;
                }

                return true;
            }
        }

        internal sealed class Boolean : GenericComparer<bool> {
            public override bool Equals(bool x, bool y) => x == y;
            public override int Compare(bool x, bool y) => x ? (y ? 0 : 1) : (y ? -1 : 0);
            public override int GetHashCode(bool x) => x.GetHashCode();

            public override int indexOf(ReadOnlySpan<bool> span, bool item) => span.IndexOf(item);
            public override int lastIndexOf(ReadOnlySpan<bool> span, bool item) => span.LastIndexOf(item);

            public override bool spansEqual(ReadOnlySpan<bool> span1, ReadOnlySpan<bool> span2)
                => span1.SequenceEqual(span2);
        }

        internal sealed class Namespace : GenericComparer<ASNamespace> {

            public override bool Equals(ASNamespace x, ASNamespace y) => ASNamespace.AS_equals(x, y);

            public override int Compare(ASNamespace x, ASNamespace y) =>
                System.String.Compare(x.AS_toString(), y.AS_toString(), StringComparison.Ordinal);

            public override int GetHashCode(ASNamespace x) => x.uri.GetHashCode();

            public override int indexOf(ReadOnlySpan<ASNamespace> span, ASNamespace item) {
                for (int i = 0; i < span.Length; i++) {
                    if (ASNamespace.AS_equals(span[i], item))
                        return i;
                }
                return -1;
            }

            public override int lastIndexOf(ReadOnlySpan<ASNamespace> span, ASNamespace item) {
                for (int i = span.Length - 1; i >= 0; i--) {
                    if (ASNamespace.AS_equals(span[i], item))
                        return i;
                }
                return -1;
            }

            public override bool spansEqual(ReadOnlySpan<ASNamespace> span1, ReadOnlySpan<ASNamespace> span2) {
                if (span1.Length != span2.Length)
                    return false;

                for (int i = 0; i < span1.Length; i++) {
                    if (!ASNamespace.AS_equals(span1[i], span2[i]))
                        return false;
                }

                return true;
            }

        }

        internal sealed class QName : GenericComparer<ASQName> {

            public override bool Equals(ASQName x, ASQName y) {
                return ASQName.AS_equals(x, y);
            }

            public override int Compare(ASQName x, ASQName y) {
                return System.String.Compare(x.AS_toString(), y.AS_toString(), StringComparison.Ordinal);
            }

            public override int GetHashCode(ASQName x) {
                return x.internalGetHashCode();
            }

            public override int indexOf(ReadOnlySpan<ASQName> span, ASQName item) {
                for (int i = 0; i < span.Length; i++) {
                    if (ASQName.AS_equals(span[i], item))
                        return i;
                }
                return -1;
            }

            public override int lastIndexOf(ReadOnlySpan<ASQName> span, ASQName item) {
                for (int i = span.Length - 1; i >= 0; i--) {
                    if (ASQName.AS_equals(span[i], item))
                        return i;
                }
                return -1;
            }

            public override bool spansEqual(ReadOnlySpan<ASQName> span1, ReadOnlySpan<ASQName> span2) {
                if (span1.Length != span2.Length)
                    return false;

                for (int i = 0; i < span1.Length; i++) {
                    if (!ASQName.AS_equals(span1[i], span2[i]))
                        return false;
                }

                return true;
            }

        }

        internal sealed class Object<T> : GenericComparer<T> where T : ASObject {

            private static readonly bool s_typeHasSpecialStrictEq =
                typeof(T) == typeof(ASObject) || typeof(ASFunction).IsAssignableFrom(typeof(T));

            public override bool Equals(T x, T y) => s_typeHasSpecialStrictEq ? x == y : ASObject.AS_strictEq(x, y);
            public override int Compare(T x, T y) => Equals(x, y) ? 0 : (ASObject.AS_lessThan(x, y) ? -1 : 1);
            public override int GetHashCode(T x) => x.GetHashCode();

            public override int indexOf(ReadOnlySpan<T> span, T item) {
                if (item == null
                    || !s_typeHasSpecialStrictEq
                    || !ClassTagSet.specialStrictEquality.contains(item.AS_class.tag))
                {
                    for (int i = 0; i < span.Length; i++) {
                        if (span[i] == item)
                            return i;
                    }
                }
                else if (ASObject.AS_isNumeric(item)) {
                    double itemVal = ASObject.AS_toNumber(item);
                    for (int i = 0; i < span.Length; i++) {
                        if (ASObject.AS_isNumeric(span[i]) && ASObject.AS_toNumber(span[i]) == itemVal)
                            return i;
                    }
                }
                else if (item is ASString) {
                    string itemVal = ASObject.AS_coerceString(item);
                    for (int i = 0; i < span.Length; i++) {
                        if (span[i] is ASString && ASObject.AS_coerceString(span[i]) == itemVal)
                            return i;
                    }
                }
                else {
                    for (int i = 0; i < span.Length; i++) {
                        if (ASObject.AS_strictEq(span[i], item))
                            return i;
                    }
                }
                return -1;
            }

            public override int lastIndexOf(ReadOnlySpan<T> span, T item) {
                if (item == null
                    || !s_typeHasSpecialStrictEq
                    || !ClassTagSet.specialStrictEquality.contains(item.AS_class.tag))
                {
                    for (int i = span.Length - 1; i >= 0; i--) {
                        if (span[i] == item)
                            return i;
                    }
                }
                else if (ASObject.AS_isNumeric(item)) {
                    double itemVal = ASObject.AS_toNumber(item);
                    for (int i = span.Length - 1; i >= 0; i--) {
                        if (ASObject.AS_isNumeric(span[i]) && ASObject.AS_toNumber(span[i]) == itemVal)
                            return i;
                    }
                }
                else if (item is ASString) {
                    string itemVal = ASObject.AS_coerceString(item);
                    for (int i = span.Length - 1; i >= 0; i--) {
                        if (span[i] is ASString && ASObject.AS_coerceString(span[i]) == itemVal)
                            return i;
                    }
                }
                else {
                    for (int i = span.Length - 1; i >= 0; i--) {
                        if (ASObject.AS_strictEq(span[i], item))
                            return i;
                    }
                }
                return -1;
            }

            public override bool spansEqual(ReadOnlySpan<T> span1, ReadOnlySpan<T> span2) {
                if (span1.Length != span2.Length)
                    return false;

                if (!s_typeHasSpecialStrictEq) {
                    for (int i = 0; i < span1.Length; i++) {
                        if (span1[i] != span2[i])
                            return false;
                    }
                }
                else {
                    for (int i = 0; i < span1.Length; i++) {
                        if (!ASObject.AS_strictEq(span1[i], span2[i]))
                            return false;
                    }
                }

                return true;
            }

        }

        /// <summary>
        /// The GenericComparer implementation for all interface types. This is used because such
        /// types have no implicit conversion from these types to the ASObject type, and the
        /// explicit conversions (which require two casts) are apparently slower than the implicit
        /// conversions, used by the Object comparer which constrains its type parameter to
        /// ASObject.
        /// </summary>
        internal sealed class ObjectInterface<T> : GenericComparer<T> where T : class {
            // We can use reference equality here since no types that have a special definition
            // of strict equality (the primitive types, Namespace and QName) implement any
            // interfaces.

            public override bool Equals(T x, T y) => x == y;

            public override int Compare(T x, T y) =>
                Equals(x, y) ? 0 : (ASObject.AS_lessThan((ASObject)(object)x, (ASObject)(object)y) ? -1 : 1);

            public override int GetHashCode(T x) => x.GetHashCode();

            public override int indexOf(ReadOnlySpan<T> span, T item) {
                for (int i = 0; i < span.Length; i++) {
                    if (span[i] == item)
                        return i;
                }
                return -1;
            }

            public override int lastIndexOf(ReadOnlySpan<T> span, T item) {
                for (int i = span.Length - 1; i >= 0; i--) {
                    if (span[i] == item)
                        return i;
                }
                return -1;
            }

            public override bool spansEqual(ReadOnlySpan<T> span1, ReadOnlySpan<T> span2) {
                if (span1.Length != span2.Length)
                    return false;

                for (int i = 0; i < span1.Length; i++) {
                    if (span1[i] != span2[i])
                        return false;
                }

                return true;
            }

        }

        internal sealed class AnyType : GenericComparer<ASAny> {

            public override bool Equals(ASAny x, ASAny y) => ASAny.AS_strictEq(x, y);
            public override int Compare(ASAny x, ASAny y) => ASAny.AS_strictEq(x, y) ? 0 : ASAny.AS_lessThan(x, y) ? -1 : 1;
            public override int GetHashCode(ASAny x) => x.GetHashCode();

            public override int indexOf(ReadOnlySpan<ASAny> span, ASAny item) {
                if (item.isUndefinedOrNull
                    || !ClassTagSet.specialStrictEquality.contains(item.AS_class.tag))
                {
                    for (int i = 0; i < span.Length; i++) {
                        if (span[i] == item)
                            return i;
                    }
                }
                else if (ASObject.AS_isNumeric(item.value)) {
                    double itemVal = (double)item.value;
                    for (int i = 0; i < span.Length; i++) {
                        if (ASObject.AS_isNumeric(span[i].value) && (double)span[i] == itemVal)
                            return i;
                    }
                }
                else if (item.value is ASString) {
                    string itemVal = (string)item.value;
                    for (int i = 0; i < span.Length; i++) {
                        if (span[i].value is ASString && (string)span[i] == itemVal)
                            return i;
                    }
                }
                else {
                    for (int i = 0; i < span.Length; i++) {
                        if (ASAny.AS_strictEq(span[i], item))
                            return i;
                    }
                }
                return -1;
            }

            public override int lastIndexOf(ReadOnlySpan<ASAny> span, ASAny item) {
                if (item.isUndefinedOrNull
                    || !ClassTagSet.specialStrictEquality.contains(item.AS_class.tag))
                {
                    for (int i = span.Length - 1; i >= 0; i--) {
                        if (span[i] == item)
                            return i;
                    }
                }
                else if (ASObject.AS_isNumeric(item.value)) {
                    double itemVal = (double)item.value;
                    for (int i = span.Length - 1; i >= 0; i--) {
                        if (ASObject.AS_isNumeric(span[i].value) && (double)span[i] == itemVal)
                            return i;
                    }
                }
                else if (item.value is ASString) {
                    string itemVal = (string)item.value;
                    for (int i = span.Length - 1; i >= 0; i--) {
                        if (span[i].value is ASString && (string)span[i] == itemVal)
                            return i;
                    }
                }
                else {
                    for (int i = span.Length - 1; i >= 0; i--) {
                        if (ASAny.AS_strictEq(span[i], item))
                            return i;
                    }
                }
                return -1;
            }

            public override bool spansEqual(ReadOnlySpan<ASAny> span1, ReadOnlySpan<ASAny> span2) {
                if (span1.Length != span2.Length)
                    return false;

                for (int i = 0; i < span1.Length; i++) {
                    if (!ASAny.AS_strictEq(span1[i], span2[i]))
                        return false;
                }

                return true;
            }

        }

        /// <summary>
        /// A comparer that uses a delegate for comparison.
        /// </summary>
        internal sealed class Delegate<T> : GenericComparer<T> {

            private readonly Comparison<T> m_delegate;

            internal Delegate(Comparison<T> del) {
                m_delegate = del;
            }

            public override bool Equals(T x, T y) => m_delegate(x, y) == 0;
            public override int Compare(T x, T y) => m_delegate(x, y);
            public override int GetHashCode(T x) => 0;

            public override int indexOf(ReadOnlySpan<T> span, T item) {
                for (int i = 0; i < span.Length; i++) {
                    if (m_delegate(span[i], item) == 0)
                        return i;
                }
                return -1;
            }

            public override int lastIndexOf(ReadOnlySpan<T> span, T item) {
                for (int i = span.Length - 1; i >= 0; i--) {
                    if (m_delegate(span[i], item) == 0)
                        return i;
                }
                return -1;
            }

            public override bool spansEqual(ReadOnlySpan<T> span1, ReadOnlySpan<T> span2) {
                if (span1.Length != span2.Length)
                    return false;

                for (int i = 0; i < span1.Length; i++) {
                    if (m_delegate(span1[i], span2[i]) != 0)
                        return false;
                }

                return true;
            }
        }

        /// <summary>
        /// This comparer uses a callback function to compare values. The callback is dynamically
        /// invoked for each comparison.
        /// </summary>
        internal sealed class DynamicFunc<T> : GenericComparer<T> {

            private static readonly GenericTypeConverter<T, ASAny> s_anyConverter =
                    GenericTypeConverter<T, ASAny>.instance;

            private ASFunction m_func;
            private ASAny[] m_argArray = new ASAny[2];

            internal DynamicFunc(ASFunction func) {
                m_func = func;
            }

            public override bool Equals(T x, T y) => Compare(x, y) == 0;
            public override int GetHashCode(T x) => 0;

            public override int Compare(T x, T y) {
                m_argArray[0] = s_anyConverter.convert(x);
                m_argArray[1] = s_anyConverter.convert(y);

                double result = (double)m_func.AS_invoke(ASAny.@null, m_argArray);
                return (result > 0) ? 1 : ((result < 0) ? -1 : 0);
            }

            public override int indexOf(ReadOnlySpan<T> span, T item) {
                for (int i = 0; i < span.Length; i++) {
                    if (Compare(span[i], item) == 0)
                        return i;
                }
                return -1;
            }

            public override int lastIndexOf(ReadOnlySpan<T> span, T item) {
                for (int i = span.Length - 1; i >= 0; i--) {
                    if (Compare(span[i], item) == 0)
                        return i;
                }
                return -1;
            }

            public override bool spansEqual(ReadOnlySpan<T> span1, ReadOnlySpan<T> span2) {
                if (span1.Length != span2.Length)
                    return false;

                for (int i = 0; i < span1.Length; i++) {
                    if (Compare(span1[i], span2[i]) != 0)
                        return false;
                }

                return true;
            }
        }

    }

}
