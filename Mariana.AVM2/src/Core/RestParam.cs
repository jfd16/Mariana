using System;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Used by AS3 functions that declare a <c>...rest</c> parameter.
    /// </summary>
    /// <remarks>
    /// The default value of this type corresponds to a an empty rest array.
    /// </remarks>
    public readonly ref struct RestParam {

        private readonly ReadOnlySpan<ASAny> m_span;

        /// <summary>
        /// Creates a new <see cref="RestParam"/> instance.
        /// </summary>
        /// <param name="args">The captured arguments.</param>
        public RestParam(params ASAny[] args) => m_span = args;

        /// <summary>
        /// Creates a new <see cref="RestParam"/> instance.
        /// </summary>
        /// <param name="span">The span containing the captured arguments.</param>
        public RestParam(ReadOnlySpan<ASAny> span) => m_span = span;

        /// <summary>
        /// Gets the argument captured by the rest parameter at the given position.
        /// </summary>
        /// <param name="index">The index of the argument. (Here, zero corresponds to the first
        /// argument captured by the rest parameter, which is not necessarily the first argument of
        /// the function.)</param>
        public ASAny this[int index] => m_span[index];

        /// <summary>
        /// Gets the number of arguments captured by the rest parameter.
        /// </summary>
        public int length => m_span.Length;

        /// <summary>
        /// Returns a read-only span containing the arguments captured by the rest parameter.
        /// </summary>
        /// <returns>A read-only span containing the arguments captured by the rest parameter.</returns>
        public ReadOnlySpan<ASAny> getSpan() => m_span;

        /// <summary>
        /// Gets the argument captured by the rest parameter at the given position.
        /// </summary>
        /// <param name="index">The index of the argument. (Here, zero corresponds to the first
        /// argument captured by the rest parameter, which is not necessarily the first argument of
        /// the function.)</param>
        /// <returns>The argument captured at <paramref name="index"/>, or undefined if
        /// <paramref name="index"/> is negative or out of bounds.</returns>
        public ASAny AS_getElement(int index) =>
            (uint)index < (uint)m_span.Length ? m_span[index] : default;

        /// <summary>
        /// Gets the argument captured by the rest parameter at the given position.
        /// </summary>
        /// <param name="index">The index of the argument. (Here, zero corresponds to the first
        /// argument captured by the rest parameter, which is not necessarily the first argument of
        /// the function.)</param>
        /// <returns>The argument captured at <paramref name="index"/>, or undefined if
        /// <paramref name="index"/> is negative or out of bounds.</returns>
        public ASAny AS_getElement(uint index) =>
            index < (uint)m_span.Length ? m_span[(int)index] : default;

        /// <summary>
        /// Gets the argument captured by the rest parameter at the given position.
        /// </summary>
        /// <param name="index">The index of the argument. (Here, zero corresponds to the first
        /// argument captured by the rest parameter, which is not necessarily the first argument of
        /// the function.)</param>
        /// <returns>The argument captured at <paramref name="index"/>, or undefined if
        /// <paramref name="index"/> is not an integer, negative or out of bounds.</returns>
        public ASAny AS_getElement(double index) {
            int ival = (int)index;
            return ((double)ival == index) ? AS_getElement(ival) : default;
        }

    }

}
