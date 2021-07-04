using System;
using System.Runtime.CompilerServices;

namespace Mariana.Common {

    /// <summary>
    /// A structure wrapping an object that provides reference equality semantics.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    public readonly struct RefWrapper<T> : IEquatable<RefWrapper<T>>, IEquatable<T> where T : class? {

        /// <summary>
        /// A <see cref="RefWrapper{T}"/> that holds a null reference.
        /// </summary>
        public static readonly RefWrapper<T?> nullRef = default;

        /// <summary>
        /// The reference to the object contained in this <see cref="RefWrapper{T}"/> instance.
        /// </summary>
        public readonly T value;

        /// <summary>
        /// Creates a new <see cref="RefWrapper{T}"/> instance.
        /// </summary>
        /// <param name="value">The object reference from which to create a <see cref="RefWrapper{T}"/>.</param>
        public RefWrapper(T value) => this.value = value;

        /// <summary>
        /// Converts an object reference to a <see cref="RefWrapper{T}"/> instance.
        /// </summary>
        /// <param name="value">The object reference from which to create a <see cref="RefWrapper{T}"/>.</param>
        public static implicit operator RefWrapper<T>(T value) => new RefWrapper<T>(value);

        /// <summary>
        /// Returns a value indicating whether this <see cref="RefWrapper{T}"/> instance
        /// is equal to another <see cref="RefWrapper{T}"/> instance.
        /// </summary>
        /// <param name="other">The <see cref="RefWrapper{T}"/> to compare with this instance.</param>
        /// <returns>True if this instance is equal to <paramref name="other"/>, otherwise
        /// false.</returns>
        /// <remarks>Two <see cref="RefWrapper{T}"/> instances are equal if and only if they
        /// contain references to the same object.</remarks>
        public bool Equals(RefWrapper<T> other) => value == other.value;

        /// <summary>
        /// Returns a value indicating whether this <see cref="RefWrapper{T}"/> instance
        /// is equal to another object of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="other">The object to compare with this instance.</param>
        /// <returns>True if this instance is equal to <paramref name="other"/>, otherwise
        /// false.</returns>
        /// <remarks>This method returns true if <paramref name="other"/> is the reference
        /// contained in this instance.</remarks>
        public bool Equals(T other) => value == other;

        /// <summary>
        /// Returns a value indicating whether this <see cref="RefWrapper{T}"/> instance
        /// is equal to another object.
        /// </summary>
        /// <param name="other">The object to compare with this instance.</param>
        /// <returns>True if this instance is equal to <paramref name="other"/>, otherwise
        /// false.</returns>
        /// <remarks>This method returns true if <paramref name="other"/> is the reference
        /// contained in this instance, or is a boxed <see cref="RefWrapper{T}"/> that contains
        /// the same reference as this instance.</remarks>
        public override bool Equals(object other) {
            if (other is RefWrapper<T> refWrapper)
                return value == refWrapper.value;

            return value == other;
        }

        /// <summary>
        /// Returns a hash code for a <see cref="RefWrapper{T}"/> instance.
        /// </summary>
        /// <returns>A hash code for a <see cref="RefWrapper{T}"/> instance.</returns>
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(value);

        /// <summary>
        /// Returns a value indicating whether two <see cref="RefWrapper{T}"/> instances are equal.
        /// </summary>
        /// <param name="x">The first <see cref="RefWrapper{T}"/> instance.</param>
        /// <param name="y">The second <see cref="RefWrapper{T}"/> instance.</param>
        /// <returns>True if <paramref name="x"/> is equal to <paramref name="y"/>,
        /// otherwise false.</returns>
        /// <remarks>Two <see cref="RefWrapper{T}"/> instances are equal if and only if they
        /// contain references to the same object.</remarks>
        public static bool operator ==(RefWrapper<T> x, RefWrapper<T> y) => x.value == y.value;

        /// <summary>
        /// Returns a value indicating whether two <see cref="RefWrapper{T}"/> instances are not equal.
        /// </summary>
        /// <param name="x">The first <see cref="RefWrapper{T}"/> instance.</param>
        /// <param name="y">The second <see cref="RefWrapper{T}"/> instance.</param>
        /// <returns>True if <paramref name="x"/> is not equal to <paramref name="y"/>,
        /// otherwise false.</returns>
        /// <remarks>Two <see cref="RefWrapper{T}"/> instances are equal if and only if they
        /// contain references to the same object.</remarks>
        public static bool operator !=(RefWrapper<T> x, RefWrapper<T> y) => x.value != y.value;

    }

}
