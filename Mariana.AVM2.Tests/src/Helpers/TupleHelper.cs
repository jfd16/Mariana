using System;
using System.Collections.Generic;
using System.Linq;

namespace Mariana.AVM2.Tests.Helpers {

    internal static class TupleHelper {

        /// <summary>
        /// Creates an array of type Object with the single element <paramref name="item"/>.
        /// </summary>
        public static object[] toArray<T1>(T1 item) => new object[] {item};

        /// <summary>
        /// Creates an array of type Object whose elements are the same as those of <paramref name="tuple"/>.
        /// </summary>
        public static object[] toArray<T1, T2>((T1, T2) tuple) => new object[] {tuple.Item1, tuple.Item2};

        /// <summary>
        /// Creates an array of type Object whose elements are the same as those of <paramref name="tuple"/>.
        /// </summary>
        public static object[] toArray<T1, T2, T3>((T1, T2, T3) tuple) =>
            new object[] {tuple.Item1, tuple.Item2, tuple.Item3};

        /// <summary>
        /// Creates an array of type Object whose elements are the same as those of <paramref name="tuple"/>.
        /// </summary>
        public static object[] toArray<T1, T2, T3, T4>((T1, T2, T3, T4) tuple) =>
            new object[] {tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4};

        /// <summary>
        /// Creates an array of type Object whose elements are the same as those of <paramref name="tuple"/>.
        /// </summary>
        public static object[] toArray<T1, T2, T3, T4, T5>((T1, T2, T3, T4, T5) tuple) =>
            new object[] {tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5};

        /// <summary>
        /// Creates an array of type Object whose elements are the same as those of <paramref name="tuple"/>.
        /// </summary>
        public static object[] toArray<T1, T2, T3, T4, T5, T6>((T1, T2, T3, T4, T5, T6) tuple) =>
            new object[] {tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6};

        /// <summary>
        /// Converts each element in the given array to a single-element array of type Object having that element.
        /// </summary>
        public static IEnumerable<object[]> toArrays<T1>(params T1[] items) => items.Select(x => toArray(x));

        /// <summary>
        /// Converts each tuple in the given array to an of type Object containing the elements of the tuple.
        /// </summary>
        public static IEnumerable<object[]> toArrays<T1, T2>(params (T1, T2)[] items) => items.Select(x => toArray(x));

        /// <summary>
        /// Converts each tuple in the given array to an of type Object containing the elements of the tuple.
        /// </summary>
        public static IEnumerable<object[]> toArrays<T1, T2, T3>(params (T1, T2, T3)[] items) => items.Select(x => toArray(x));

        /// <summary>
        /// Converts each tuple in the given array to an of type Object containing the elements of the tuple.
        /// </summary>
        public static IEnumerable<object[]> toArrays<T1, T2, T3, T4>(params (T1, T2, T3, T4)[] items) => items.Select(x => toArray(x));

        /// <summary>
        /// Converts each tuple in the given array to an of type Object containing the elements of the tuple.
        /// </summary>
        public static IEnumerable<object[]> toArrays<T1, T2, T3, T4, T5>(params (T1, T2, T3, T4, T5)[] items) => items.Select(x => toArray(x));

        /// <summary>
        /// Converts each tuple in the given array to an of type Object containing the elements of the tuple.
        /// </summary>
        public static IEnumerable<object[]> toArrays<T1, T2, T3, T4, T5, T6>(params (T1, T2, T3, T4, T5, T6)[] items) => items.Select(x => toArray(x));

    }

}
