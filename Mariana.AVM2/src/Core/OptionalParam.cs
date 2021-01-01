namespace Mariana.AVM2.Core {

    /// <summary>
    /// Represents an optional parameter in a method exported to the AVM2 that does not have
    /// a default value specified.
    /// </summary>
    /// <typeparam name="T">The type of the parameter.</typeparam>
    public readonly struct OptionalParam<T> {

        /// <summary>
        /// A <see cref="OptionalParam{T}"/> instance representing an argument that was not
        /// specified in a method call. This is the default value of this type.
        /// </summary>
        public static readonly OptionalParam<T> missing = default;

        /// <summary>
        /// True if the argument's value was specified in the method call, false otherwise.
        /// </summary>
        public readonly bool isSpecified;

        /// <summary>
        /// The value of the argument passed. If <see cref="isSpecified"/> is false, this is
        /// the default value of <typeparamref name="T"/>
        /// </summary>
        public readonly T value;

        /// <summary>
        /// Creates a new instance of <see cref="OptionalParam{T}"/> with a specified argument value.
        /// </summary>
        /// <param name="value">The argument value.</param>
        public OptionalParam(T value) {
            this.isSpecified = true;
            this.value = value;
        }

        /// <summary>
        /// Converts a value of type <typeparamref name="T"/> to an instance of
        /// <see cref="OptionalParam{T}"/>.
        /// </summary>
        /// <param name="value">The argument value.</param>
        public static implicit operator OptionalParam<T>(T value) => new OptionalParam<T>(value);

    }

}
