namespace Mariana.AVM2.Core {

    /// <summary>
    /// Describes custom property lookup behaviour for numeric keys on classes that
    /// provide it (for example, Array and Vector).
    /// </summary>
    internal sealed class IndexProperty {

        private Class m_valueType;
        private MethodTrait m_getMethod;
        private MethodTrait m_setMethod;
        private MethodTrait m_hasMethod;
        private MethodTrait m_deleteMethod;

        public IndexProperty(
            Class valueType, MethodTrait getMethod, MethodTrait setMethod,
            MethodTrait hasMethod, MethodTrait deleteMethod)
        {
            m_valueType = valueType;
            m_getMethod = getMethod;
            m_setMethod = setMethod;
            m_hasMethod = hasMethod;
            m_deleteMethod = deleteMethod;
        }

        /// <summary>
        /// Returns the <see cref="Class"/> representing the type of the property value
        /// associated with a numeric index key.
        /// </summary>
        public Class valueType => m_valueType;

        /// <summary>
        /// Returns the <see cref="MethodTrait"/> representing the method called to get
        /// the value of the property at an index.
        /// </summary>
        public MethodTrait getMethod => m_getMethod;

        /// <summary>
        /// Returns the <see cref="MethodTrait"/> representing the method called to set
        /// the value of the property at an index.
        /// </summary>
        public MethodTrait setMethod => m_setMethod;

        /// <summary>
        /// Returns the <see cref="MethodTrait"/> representing the method called to check
        /// if a property at an index exists.
        /// </summary>
        public MethodTrait hasMethod => m_hasMethod;

        /// <summary>
        /// Returns the <see cref="MethodTrait"/> representing the method called to delete
        /// the value of the property at an index.
        /// </summary>
        public MethodTrait deleteMethod => m_deleteMethod;

        /// <summary>
        /// Merges an <see cref="IndexProperty"/> of a child class with the corresponding
        /// <see cref="IndexProperty"/> (for the same index type) of its parent class.
        /// </summary>
        ///
        /// <param name="child">The <see cref="IndexProperty"/> defined by the child class, or
        /// null if the child class should only inherit the index property definition from
        /// its parent. This may be modified to add any inherited methods from
        /// <paramref name="parent"/>.</param>
        /// <param name="parent">The corresponding <see cref="IndexProperty"/> defined by the
        /// parent class, or null if the parent class does not define it.</param>
        ///
        /// <returns>A <see cref="IndexProperty"/> representing the index property
        /// definition from the child class that includes any inherited methods from
        /// the corresponding index property definition in the parent class. If the child
        /// class does not declare or inherit the index property, returns null.</returns>
        public static IndexProperty mergeWithParent(IndexProperty child, IndexProperty parent) {
            if (child == null)
                return parent;
            if (parent == null || parent.m_valueType != child.m_valueType)
                return child;

            child.m_getMethod ??= parent.m_getMethod;
            child.m_setMethod ??= parent.m_setMethod;
            child.m_hasMethod ??= parent.m_hasMethod;
            child.m_deleteMethod ??= parent.m_deleteMethod;

            return child;
        }

    }

}
