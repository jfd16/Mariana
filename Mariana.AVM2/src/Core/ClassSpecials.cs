using System;
using System.Reflection;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Describes custom behaviour of certain classes for operations such as class invocation
    /// or construction or property access on an instance with a numeric index.
    /// </summary>
    internal sealed class ClassSpecials {

        public delegate ASAny SpecialInvoke(ReadOnlySpan<ASAny> args);

        private MethodInfo m_specialInvoke;
        private MethodInfo m_specialConstruct;
        private SpecialInvoke m_specialInvokeDelegate;
        private SpecialInvoke m_specialConstructDelegate;
        private IndexProperty m_intIndexProperty;
        private IndexProperty m_uintIndexProperty;
        private IndexProperty m_numberIndexProperty;

        /// <summary>
        /// The method that is called when the class is invoked as a function.
        /// This overrides the default behaviour (type casting).
        /// </summary>
        public MethodInfo specialInvoke => m_specialInvoke;

        /// <summary>
        /// The method that is called when the class is invoked as a constructor.
        /// This overrides the default behaviour (call the class constructor).
        /// </summary>
        public MethodInfo specialConstruct => m_specialConstruct;

        /// <summary>
        /// A delegate for the <see cref="specialInvoke"/> method.
        /// </summary>
        public SpecialInvoke specialInvokeDelegate => m_specialInvokeDelegate;

        /// <summary>
        /// A delegate for the <see cref="specialConstruct"/> method.
        /// </summary>
        public SpecialInvoke specialConstructDelegate => m_specialConstructDelegate;

        /// <summary>
        /// An <see cref="IndexProperty"/> describing any custom behaviour for an instance
        /// of the class on a property lookup with an integer key. Null if there is no special
        /// behaviour.
        /// </summary>
        public IndexProperty intIndexProperty => m_intIndexProperty;

        /// <summary>
        /// An <see cref="IndexProperty"/> describing any custom behaviour for an instance
        /// of the class on a property lookup with an unsigned integer key. Null if there is no special
        /// behaviour.
        /// </summary>
        public IndexProperty uintIndexProperty => m_uintIndexProperty;

        /// <summary>
        /// An <see cref="IndexProperty"/> describing any custom behaviour for an instance
        /// of the class on a property lookup with a Number key. Null if there is no special
        /// behaviour.
        /// </summary>
        public IndexProperty numberIndexProperty => m_numberIndexProperty;

        public ClassSpecials(
            MethodInfo specialInvoke, MethodInfo specialConstruct,
            IndexProperty intIndexProperty, IndexProperty uintIndexProperty, IndexProperty numberIndexProperty)
        {
            m_specialInvoke = specialInvoke;
            m_specialConstruct = specialConstruct;
            m_intIndexProperty = intIndexProperty;
            m_uintIndexProperty = uintIndexProperty;
            m_numberIndexProperty = numberIndexProperty;

            if (specialInvoke != null)
                m_specialInvokeDelegate = (SpecialInvoke)specialInvoke.CreateDelegate(typeof(SpecialInvoke));

            if (specialConstruct != null)
                m_specialConstructDelegate = (SpecialInvoke)specialConstruct.CreateDelegate(typeof(SpecialInvoke));
        }

        /// <summary>
        /// Merges the <see cref="ClassSpecials"/> of a child class with that of its parent class.
        /// </summary>
        ///
        /// <param name="child">The <see cref="ClassSpecials"/> for the child class, or null if it
        /// does not have any of the special functions described by <see cref="ClassSpecials"/>
        /// other than those that it should inherit from its parent.</param>
        /// <param name="parent">The <see cref="ClassSpecials"/> for the child class, or null if it
        /// does not have any of the special functions described by <see cref="ClassSpecials"/>.</param>
        ///
        /// <returns>If <paramref name="child"/> is not null then returns <paramref name="child"/>
        /// (any inherited properties will be added to it), otherwise returns a <see cref="ClassSpecials"/>
        /// containing the properties that the child class should inherit from the parent (which
        /// may be null, if the child class does not inherit anything).</returns>
        public static ClassSpecials mergeWithParent(ClassSpecials child, ClassSpecials parent) {
            if (parent == null)
                return child;

            if (child == null) {
                if (parent.m_specialInvoke == null && parent.m_specialConstruct == null)
                    return parent;

                if (parent.m_intIndexProperty == null && parent.m_uintIndexProperty == null && parent.m_numberIndexProperty == null)
                    return null;

                return new ClassSpecials(null, null, parent.m_intIndexProperty, parent.m_uintIndexProperty, parent.m_numberIndexProperty);
            }

            child.m_intIndexProperty = IndexProperty.mergeWithParent(child.m_intIndexProperty, parent.m_intIndexProperty);
            child.m_uintIndexProperty = IndexProperty.mergeWithParent(child.m_uintIndexProperty, parent.m_uintIndexProperty);
            child.m_numberIndexProperty = IndexProperty.mergeWithParent(child.m_numberIndexProperty, parent.m_numberIndexProperty);

            return child;
        }

    }

}
