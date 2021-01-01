namespace Mariana.AVM2.Core {

    /// <summary>
    /// Contains values for the kinds of namespaces used in the AVM2.
    /// </summary>
    public enum NamespaceKind : byte {

        /// <summary>
        /// This value is reserved for the special "any" namespace. It cannot be used as the kind of
        /// any other namespace; doing so will throw an error.
        /// </summary>
        ANY,

        /// <summary>
        /// The namespace is an ordinary namespace.
        /// </summary>
        NAMESPACE,

        /// <summary>
        /// This kind of namespace is used in names of traits declared as "internal" in ActionScript
        /// 3.
        /// </summary>
        PACKAGE_INTERNAL,

        /// <summary>
        /// This kind of namespace is used in names of traits declared as "protected" in ActionScript
        /// 3.
        /// </summary>
        PROTECTED,

        /// <summary>
        /// The namespace is an explicit namespace.
        /// </summary>
        EXPLICIT,

        /// <summary>
        /// This kind of namespace is used in names of static traits declared as "protected" in
        /// ActionScript 3.
        /// </summary>
        STATIC_PROTECTED,

        /// <summary>
        /// This kind of namespace is used in names of traits declared as "private" in ActionScript 3.
        /// This is a reserved namespace kind and namespaces of this kind can only be created with the
        /// <see cref="Namespace.createPrivate()" qualifyHint="true"/> method. These namespaces have
        /// no names; instead, they have an internal identifier that is used to check equality.
        /// </summary>
        PRIVATE,

    }

}
