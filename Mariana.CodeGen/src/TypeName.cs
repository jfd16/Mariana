using System;

namespace Mariana.CodeGen {

    /// <summary>
    /// Represents the name of a type in a dynamically emitted assembly.
    /// </summary>
    public readonly struct TypeName : IEquatable<TypeName> {

        /// <summary>
        /// The namespace of the type name, or null if the type name does not include a
        /// namespace.
        /// </summary>
        public readonly string? ns;

        /// <summary>
        /// The type name.
        /// </summary>
        public readonly string name;

        /// <summary>
        /// Creates a new type name without a namespace.
        /// </summary>
        /// <param name="name">The type name. This must not be null.</param>
        public TypeName(string name) : this(null, name) {}

        /// <summary>
        /// Creates a new type name with a namespace.
        /// </summary>
        /// <param name="ns">The namespace of the type name.</param>
        /// <param name="name">The type name.</param>
        public TypeName(string? ns, string name) {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            this.ns = ns;
            this.name = name;
        }

        /// <summary>
        /// Returns a value indicating whether this <see cref="TypeName"/> instance is equal
        /// to <paramref name="other"/>.
        /// </summary>
        /// <param name="other">The object to compare with this <see cref="TypeName"/>.</param>
        public override bool Equals(object other) => other is TypeName typeName && Equals(typeName);

        /// <summary>
        /// Returns a value indicating whether this <see cref="TypeName"/> instance is equal
        /// to <paramref name="other"/>.
        /// </summary>
        /// <param name="other">The value to compare with this <see cref="TypeName"/>.</param>
        public bool Equals(TypeName other) => ns == other.ns && name == other.name;

        /// <summary>
        /// Converts a string to a <see cref="TypeName"/> instance.
        /// </summary>
        /// <param name="s">The string to convert to a <see cref="TypeName"/>.</param>
        /// <returns>A <see cref="TypeName"/> with name equal to <paramref name="s"/> and
        /// no namespace.</returns>
        public static implicit operator TypeName(string s) => new TypeName(s);

        /// <summary>
        /// Returns a hash code for this <see cref="TypeName"/> instance.
        /// </summary>
        public override int GetHashCode() => ((ns == null) ? 0 : ns.GetHashCode()) ^ name.GetHashCode();

        /// <summary>
        /// Returns a string representation for this <see cref="TypeName"/> instance.
        /// </summary>
        public override string ToString() => (ns == null) ? name : ns + "." + name;

    }

}
