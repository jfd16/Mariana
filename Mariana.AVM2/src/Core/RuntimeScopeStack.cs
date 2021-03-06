using System;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A RuntimeScopeStack object is used as a scope stack for runtime name resolution in
    /// situations when scope stack lookups in a method in ActionScript bytecode cannot be fully
    /// resolved at compile time. This class is intended for use only in code generated by the
    /// ABC-IL compiler.
    /// </summary>
    public sealed class RuntimeScopeStack {

        /// <summary>
        /// An item on the scope stack.
        /// </summary>
        private struct Item {
            /// <summary>
            /// The object on the stack.
            /// </summary>
            public ASObject obj;

            /// <summary>
            /// The binding options to use for a name lookup on the object <see cref="obj"/>.
            /// </summary>
            public BindOptions bindOptions;
        }

        /// <summary>
        /// The stack items.
        /// </summary>
        private DynamicArray<Item> m_items;

        /// <summary>
        /// The <see cref="RuntimeScopeStack"/> for the enclosing scope.
        /// </summary>
        private RuntimeScopeStack? m_parentScope;

        /// <summary>
        /// Creates a new instance of <see cref="RuntimeScopeStack"/>.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the stack.</param>
        /// <param name="parentScope">The scope stack which the new scope stack should extend,
        /// or null if the new scope stack should not extend an enclosing scope.</param>
        public RuntimeScopeStack(int initialCapacity = 0, RuntimeScopeStack? parentScope = null) {
            m_items = new DynamicArray<Item>(initialCapacity);
            m_parentScope = parentScope;
        }

        /// <summary>
        /// Pushes a new object onto the scope stack.
        /// </summary>
        /// <param name="scopeObject">The object to push onto the scope stack.</param>
        /// <param name="bindingOptions">The binding options to be used when the object is reached in
        /// a name lookup. The default value will only search the object's traits.</param>
        public void push(ASObject scopeObject, BindOptions bindingOptions = BindOptions.SEARCH_TRAITS) =>
            m_items.add(new Item {obj = scopeObject, bindOptions = bindingOptions});

        /// <summary>
        /// Discards the topmost object from the scope stack. If the scope stack is empty, this method
        /// has no effect.
        /// </summary>
        public void pop() {
            if (m_items.length != 0)
                m_items.removeLast();
        }

        /// <summary>
        /// Pops objects from the scope stack until it reaches the given size.
        /// </summary>
        /// <param name="targetSize">The size of the scope stack at which to stop removing
        /// objects.</param>
        public void clear(int targetSize = 0) {
            m_items.removeRange(targetSize, m_items.length - targetSize);
        }

        /// <summary>
        /// Creates a copy of this <see cref="RuntimeScopeStack"/> instance in its current state.
        /// </summary>
        /// <returns>A copy of this <see cref="RuntimeScopeStack"/> instance in its current state.</returns>
        /// <remarks>
        /// This method creates a shallow copy of the current state of the scope stack. If this
        /// scope stack extends an enclosing scope (that was passed to the
        /// <see cref="RuntimeScopeStack(Int32, RuntimeScopeStack)"/> constructor), this
        /// method does not copy the enclosing scope; the returned clone will share the
        /// same enclosing scope as this instance.
        /// </remarks>
        public RuntimeScopeStack clone() {
            var copy = new RuntimeScopeStack(m_items.length, m_parentScope);
            m_items.asSpan().CopyTo(copy.m_items.addDefault(m_items.length));
            return copy;
        }

        /// <summary>
        /// Performs a name lookup in the scope stack. If the given name is successfully resolved on
        /// an object in the scope stack, this method returns; otherwise, the stack is traversed
        /// downwards until the name resolves successfully or the bottom of the stack is reached.
        /// </summary>
        ///
        /// <param name="name">The qualified name of the property to find.</param>
        /// <param name="startLevel">The level of the object in the stack at which to start the lookup
        /// relative to the top of the stack. 0 is at the top and higher indices move towards the
        /// bottom.</param>
        /// <param name="obj">The object in which the property with the given name was found. If no
        /// such property was found, this is set to the bottom-most object in the stack.</param>
        /// <param name="value">The value of the property found on <paramref name="obj"/>, with the
        /// given name.</param>
        /// <param name="isAttribute">Set this to true to search for attributes in XML objects. For
        /// any other properties, this value must be set to false.</param>
        /// <param name="strict">If set to true, and no property with the given name was found, an
        /// appropriate binding result indicating a failure is returned. Otherwise,
        /// <see cref="BindStatus.SUCCESS" qualifyHint="true"/> is returned irrespective of whether
        /// a property was found or not.</param>
        ///
        /// <returns>A <see cref="BindStatus"/> indicating the result of the name lookup.</returns>
        public BindStatus tryFindProperty(
            in QName name,
            int startLevel,
            out ASObject? obj,
            out ASAny value,
            bool isAttribute = false,
            bool strict = false
        ) {
            BindStatus bindStatus = BindStatus.NOT_FOUND;
            value = default(ASAny);
            obj = null;

            var current = this;
            while (current != null) {
                ref var items = ref current.m_items;
                for (int i = items.length - startLevel - 1; i >= 0; i--) {
                    obj = items[i].obj;
                    BindOptions bindOptions = items[i].bindOptions | (isAttribute ? BindOptions.ATTRIBUTE : 0);

                    bindStatus = obj.AS_tryGetProperty(name, out value, bindOptions);
                    if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.AMBIGUOUS)
                        return bindStatus;
                }
                current = current.m_parentScope;
                startLevel = 0;
            }

            return strict ? bindStatus : BindStatus.SUCCESS;
        }

        /// <summary>
        /// Performs a name lookup in the scope stack. If the given name is successfully resolved on
        /// an object in the scope stack, this method returns; otherwise, the stack is traversed
        /// downwards until the name resolves successfully or the bottom of the stack is reached.
        /// </summary>
        ///
        /// <param name="name">The local name of the property to find.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="startLevel">The level of the object in the stack at which to start the lookup
        /// relative to the top of the stack. 0 is at the top and higher indices move towards the
        /// bottom.</param>
        /// <param name="obj">The object in which the property with the given name and in one of the
        /// namespaces of the given set was found. If no such property was found, this is set to the
        /// bottom-most object in the stack.</param>
        /// <param name="value">The value of the property found on <paramref name="obj"/>.</param>
        /// <param name="isAttribute">Set this to true to search for attributes in XML objects. For
        /// any other properties, this value must be set to false.</param>
        /// <param name="strict">If set to true, and no property was found, an appropriate binding
        /// result indicating a failure is returned. Otherwise,
        /// <see cref="BindStatus.SUCCESS" qualifyHint="true"/> is returned irrespective of whether
        /// a property was found or not.</param>
        ///
        /// <returns>A <see cref="BindStatus"/> indicating the result of the name lookup.</returns>
        public BindStatus tryFindProperty(
            string name,
            in NamespaceSet nsSet,
            int startLevel,
            out ASObject? obj,
            out ASAny value,
            bool isAttribute = false,
            bool strict = false
        ) {
            BindStatus bindStatus = BindStatus.NOT_FOUND;
            value = default(ASAny);
            obj = null;

            var current = this;
            while (current != null) {
                ref var items = ref current.m_items;
                for (int i = items.length - startLevel - 1; i >= 0; i--) {
                    obj = items[i].obj;
                    BindOptions bindOptions = items[i].bindOptions | (isAttribute ? BindOptions.ATTRIBUTE : 0);

                    bindStatus = obj.AS_tryGetProperty(name, nsSet, out value, bindOptions);
                    if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.AMBIGUOUS)
                        return bindStatus;
                }
                current = current.m_parentScope;
                startLevel = 0;
            }

            return strict ? bindStatus : BindStatus.SUCCESS;
        }

        /// <summary>
        /// Performs an object key lookup in the scope stack. If the given name is successfully
        /// resolved on an object in the scope stack, this method returns; otherwise, the stack is
        /// traversed downwards until the name resolves successfully or the bottom of the stack is
        /// reached.
        /// </summary>
        ///
        /// <param name="key">The object key for which to find a property.</param>
        /// <param name="startLevel">The level of the object in the stack at which to start the lookup
        /// relative to the top of the stack. 0 is at the top and higher indices move towards the
        /// bottom.</param>
        /// <param name="obj">The object in which the property with the given key was found. If no
        /// such property was found, this is set to the bottom-most object in the stack.</param>
        /// <param name="value">The value of the property found on <paramref name="obj"/>, with the
        /// given key.</param>
        /// <param name="isAttribute">Set this to true to search for attributes in XML objects. For
        /// any other properties, this value must be set to false.</param>
        /// <param name="strict">If set to true, and no property with the given key was found, an
        /// appropriate binding result indicating a failure is returned. Otherwise,
        /// <see cref="BindStatus.SUCCESS" qualifyHint="true"/> is returned irrespective of whether
        /// a property was found or not.</param>
        ///
        /// <returns>A <see cref="BindStatus"/> indicating the result of the name lookup.</returns>
        public BindStatus tryFindPropertyObj(
            ASAny key,
            int startLevel,
            out ASObject? obj,
            out ASAny value,
            bool isAttribute = false,
            bool strict = false
        ) {
            BindStatus bindStatus = BindStatus.NOT_FOUND;
            value = default(ASAny);
            obj = null;

            var current = this;
            while (current != null) {
                ref var items = ref current.m_items;
                for (int i = items.length - startLevel - 1; i >= 0; i--) {
                    obj = items[i].obj;
                    BindOptions bindOptions = items[i].bindOptions | (isAttribute ? BindOptions.ATTRIBUTE : 0);

                    bindStatus = obj.AS_tryGetPropertyObj(key, out value, bindOptions);
                    if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.AMBIGUOUS)
                        return bindStatus;
                }
                current = current.m_parentScope;
            }

            return strict ? bindStatus : BindStatus.SUCCESS;
        }

        /// <summary>
        /// Performs an object key lookup in the scope stack. If the given name is successfully
        /// resolved on an object in the scope stack, this method returns; otherwise, the stack is
        /// traversed downwards until the name resolves successfully or the bottom of the stack is
        /// reached.
        /// </summary>
        ///
        /// <param name="key">The object key for which to find a property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="startLevel">The level of the object in the stack at which to start the lookup
        /// relative to the top of the stack. 0 is at the top and higher indices move towards the
        /// bottom.</param>
        /// <param name="obj">The object in which the property with the given key was found. If no
        /// such property was found, this is set to the bottom-most object in the stack.</param>
        /// <param name="value">The value of the property found on <paramref name="obj"/>, with the
        /// given key.</param>
        /// <param name="isAttribute">Set this to true to search for attributes in XML objects. For
        /// any other properties, this value must be set to false.</param>
        /// <param name="strict">If set to true, and no property with the given key was found, an
        /// appropriate binding result indicating a failure is returned. Otherwise,
        /// <see cref="BindStatus.SUCCESS" qualifyHint="true"/> is returned irrespective of whether
        /// a property was found or not.</param>
        ///
        /// <returns>A <see cref="BindStatus"/> indicating the result of the name lookup.</returns>
        public BindStatus tryFindPropertyObj(
            ASAny key,
            in NamespaceSet nsSet,
            int startLevel,
            out ASObject? obj,
            out ASAny value,
            bool isAttribute = false,
            bool strict = false
        ) {
            BindStatus bindStatus = BindStatus.NOT_FOUND;
            value = default(ASAny);
            obj = null;

            var current = this;
            while (current != null) {
                ref var items = ref current.m_items;
                for (int i = items.length - startLevel - 1; i >= 0; i--) {
                    obj = items[i].obj;
                    BindOptions bindOptions = items[i].bindOptions | (isAttribute ? BindOptions.ATTRIBUTE : 0);

                    bindStatus = obj.AS_tryGetPropertyObj(key, nsSet, out value, bindOptions);
                    if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.AMBIGUOUS)
                        return bindStatus;
                }
                current = current.m_parentScope;
            }

            return strict ? bindStatus : BindStatus.SUCCESS;
        }

        /// <summary>
        /// Performs a name lookup in the scope stack. If the given name is successfully resolved on
        /// an object in the scope stack, this method returns that object; otherwise, the stack is
        /// traversed downwards until the name resolves successfully or the bottom of the stack is
        /// reached.
        /// </summary>
        ///
        /// <param name="name">The qualified name of the property to find.</param>
        /// <param name="startLevel">The level of the object in the stack at which to start the lookup
        /// relative to the top of the stack. 0 (the default value) is at the top and higher indices
        /// move towards the bottom.</param>
        /// <param name="isAttribute">Set this to true to search for attributes in XML objects. For
        /// any other properties, this value must be set to false.</param>
        /// <param name="strict">If set to true, and no property with the given name was found, an
        /// appropriate error is thrown. Otherwise, this method returns the bottom-most object in the
        /// stack if no property is found.</param>
        ///
        /// <returns>The object in which the property with the given name was found. If no such
        /// property was found and <paramref name="strict"/> is set to false, the bottom-most object
        /// in the stack is returned.</returns>
        public ASObject? findProperty(in QName name, int startLevel = 0, bool isAttribute = false, bool strict = false) {
            BindStatus bindStatus = tryFindProperty(
                name, startLevel, out ASObject? obj, out _, isAttribute, strict);

            if (bindStatus == BindStatus.NOT_FOUND || bindStatus == BindStatus.SOFT_SUCCESS)
                throw ErrorHelper.createError(ErrorCode.VARIABLE_NOT_DEFINED, name.ToString());
            if (bindStatus == BindStatus.AMBIGUOUS)
                throw ErrorHelper.createError(ErrorCode.AMBIGUOUS_NAME_MATCH, name.ToString());

            return obj;
        }

        /// <summary>
        /// Performs a name lookup in the scope stack. If the given name is successfully resolved on
        /// an object in the scope stack, this method returns that object; otherwise, the stack is
        /// traversed downwards until the name resolves successfully or the bottom of the stack is
        /// reached.
        /// </summary>
        ///
        /// <param name="name">The local name of the property to find.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="startLevel">The level of the object in the stack at which to start the lookup
        /// relative to the top of the stack. 0 (the default value) is at the top and higher indices
        /// move towards the bottom.</param>
        /// <param name="isAttribute">Set this to true to search for attributes in XML objects. For
        /// any other properties, this value must be set to false.</param>
        /// <param name="strict">If set to true, and no property with the given name was found, an
        /// appropriate error is thrown. Otherwise, this method returns the bottom-most object in the
        /// stack if no property is found.</param>
        ///
        /// <returns>The object in which the property with the given name was found. If no such
        /// property was found and <paramref name="strict"/> is set to false, the bottom-most object
        /// in the stack is returned.</returns>
        public ASObject? findProperty(
            string name, in NamespaceSet nsSet, int startLevel = 0, bool isAttribute = false, bool strict = false)
        {
            BindStatus bindStatus = tryFindProperty(
                name, nsSet, startLevel, out ASObject? obj, out _, isAttribute, strict);

            if (bindStatus == BindStatus.NOT_FOUND || bindStatus == BindStatus.SOFT_SUCCESS)
                throw ErrorHelper.createError(ErrorCode.VARIABLE_NOT_DEFINED, name);
            if (bindStatus == BindStatus.AMBIGUOUS)
                throw ErrorHelper.createError(ErrorCode.AMBIGUOUS_NAME_MATCH, name);

            return obj;
        }

        /// <summary>
        /// Performs an object key lookup in the scope stack. If the given name is successfully
        /// resolved on an object in the scope stack, this method returns that object; otherwise, the
        /// stack is traversed downwards until the name resolves successfully or the bottom of the
        /// stack is reached.
        /// </summary>
        ///
        /// <param name="key">The object key for which to find a property in the scope stack.</param>
        /// <param name="startLevel">The level of the object in the stack at which to start the lookup
        /// relative to the top of the stack. 0 (the default value) is at the top and higher indices
        /// move towards the bottom.</param>
        /// <param name="isAttribute">Set this to true to search for attributes in XML objects. For
        /// any other properties, this value must be set to false.</param>
        /// <param name="strict">If set to true, and no property with the given key was found, an
        /// appropriate error is thrown. Otherwise, this method returns the bottom-most object in the
        /// stack if no property is found.</param>
        ///
        /// <returns>The object in which the property with the given key was found. If no such
        /// property was found and <paramref name="strict"/> is set to false, the bottom-most object
        /// in the stack is returned.</returns>
        public ASObject? findPropertyObj(ASAny key, int startLevel = 0, bool isAttribute = false, bool strict = false) {
            BindStatus status = tryFindPropertyObj(
                key, startLevel, out ASObject? obj, out _, isAttribute, strict);

            if (status == BindStatus.NOT_FOUND || status == BindStatus.SOFT_SUCCESS)
                throw ErrorHelper.createError(ErrorCode.VARIABLE_NOT_DEFINED, key);
            if (status == BindStatus.AMBIGUOUS)
                throw ErrorHelper.createError(ErrorCode.AMBIGUOUS_NAME_MATCH, key);

            return obj;
        }

        /// <summary>
        /// Performs an object key lookup in the scope stack. If the given name is successfully
        /// resolved on an object in the scope stack, this method returns that object; otherwise, the
        /// stack is traversed downwards until the name resolves successfully or the bottom of the
        /// stack is reached.
        /// </summary>
        ///
        /// <param name="key">The object key for which to find a property in the scope stack.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="startLevel">The level of the object in the stack at which to start the lookup
        /// relative to the top of the stack. 0 (the default value) is at the top and higher indices
        /// move towards the bottom.</param>
        /// <param name="isAttribute">Set this to true to search for attributes in XML objects. For
        /// any other properties, this value must be set to false.</param>
        /// <param name="strict">If set to true, and no property with the given key was found, an
        /// appropriate error is thrown. Otherwise, this method returns the bottom-most object in the
        /// stack if no property is found.</param>
        ///
        /// <returns>The object in which the property with the given key was found. If no such
        /// property was found and <paramref name="strict"/> is set to false, the bottom-most object
        /// in the stack is returned.</returns>
        public ASObject? findPropertyObj(
            ASAny key, in NamespaceSet nsSet, int startLevel = 0, bool isAttribute = false, bool strict = false)
        {
            BindStatus status = tryFindPropertyObj(
                key, nsSet, startLevel, out ASObject? obj, out _, isAttribute, strict);

            if (status == BindStatus.NOT_FOUND || status == BindStatus.SOFT_SUCCESS)
                throw ErrorHelper.createError(ErrorCode.VARIABLE_NOT_DEFINED, key);
            if (status == BindStatus.AMBIGUOUS)
                throw ErrorHelper.createError(ErrorCode.AMBIGUOUS_NAME_MATCH, key);

            return obj;
        }

        /// <summary>
        /// Performs a name lookup in the scope stack. If the given name is successfully resolved on
        /// an object in the scope stack, this method returns the value of the resolved property;
        /// otherwise, the stack is traversed downwards until the name resolves successfully or the
        /// bottom of the stack is reached.
        /// </summary>
        ///
        /// <param name="name">The qualified name of the property to find.</param>
        /// <param name="startLevel">The level of the object in the stack at which to start the lookup
        /// relative to the top of the stack. 0 (the default value) is at the top and higher indices
        /// move towards the bottom.</param>
        /// <param name="isAttribute">Set this to true to search for attributes in XML objects. For
        /// any other properties, this value must be set to false.</param>
        /// <param name="strict">If set to true, and no property with the given name was found, an
        /// appropriate error is thrown. Otherwise, this method returns undefined if no property is
        /// found.</param>
        ///
        /// <returns>The value of the property with the given name. If no property was found and
        /// <paramref name="strict"/> is set to false, undefined is returned.</returns>
        public ASAny getProperty(in QName name, int startLevel = 0, bool isAttribute = false, bool strict = false) {
            BindStatus status = tryFindProperty(
                name, startLevel, out _, out ASAny value, isAttribute, strict);

            if (status == BindStatus.NOT_FOUND || status == BindStatus.SOFT_SUCCESS)
                throw ErrorHelper.createError(ErrorCode.VARIABLE_NOT_DEFINED, name.ToString());
            if (status == BindStatus.AMBIGUOUS)
                throw ErrorHelper.createError(ErrorCode.AMBIGUOUS_NAME_MATCH, name.ToString());

            return value;
        }

        /// <summary>
        /// Performs a name lookup in the scope stack. If the given name is successfully resolved on
        /// an object in the scope stack, this method returns the value of the resolved property;
        /// otherwise, the stack is traversed downwards until the name resolves successfully or the
        /// bottom of the stack is reached.
        /// </summary>
        ///
        /// <param name="name">The qualified name of the property to find.</param>
        /// <param name="nsSet">The set of namespaces in which the property's namespace must
        /// exist.</param>
        /// <param name="startLevel">The level of the object in the stack at which to start the lookup
        /// relative to the top of the stack. 0 (the default value) is at the top and higher indices
        /// move towards the bottom.</param>
        /// <param name="isAttribute">Set this to true to search for attributes in XML objects. For
        /// any other properties, this value must be set to false.</param>
        /// <param name="strict">If set to true, and no property with the given name was found, an
        /// appropriate error is thrown. Otherwise, this method returns undefined if no property is
        /// found.</param>
        ///
        /// <returns>The value of the property with the given name. If no property was found and
        /// <paramref name="strict"/> is set to false, undefined is returned.</returns>
        public ASAny getProperty(string name, in NamespaceSet nsSet, int startLevel = 0, bool isAttribute = false, bool strict = false) {
            BindStatus status = tryFindProperty(
                name, nsSet, startLevel, out _, out ASAny value, isAttribute, strict);

            if (status == BindStatus.NOT_FOUND || status == BindStatus.SOFT_SUCCESS)
                throw ErrorHelper.createError(ErrorCode.VARIABLE_NOT_DEFINED, name);
            if (status == BindStatus.AMBIGUOUS)
                throw ErrorHelper.createError(ErrorCode.AMBIGUOUS_NAME_MATCH, name);

            return value;
        }

        /// <summary>
        /// Performs an object key lookup in the scope stack. If the given name is successfully
        /// resolved on an object in the scope stack, this method returns the value of the resolved
        /// property; otherwise, the stack is traversed downwards until the name resolves successfully
        /// or the bottom of the stack is reached.
        /// </summary>
        ///
        /// <param name="key">The key for which to find a property in the scope stack.</param>
        /// <param name="startLevel">The level of the object in the stack at which to start the lookup
        /// relative to the top of the stack. 0 (the default value) is at the top and higher indices
        /// move towards the bottom.</param>
        /// <param name="isAttribute">Set this to true to search for attributes in XML objects. For
        /// any other properties, this value must be set to false.</param>
        /// <param name="strict">If set to true, and no property with the given name was found, an
        /// appropriate error is thrown. Otherwise, this method returns undefined if no property is
        /// found.</param>
        ///
        /// <returns>The object in which the property with the given key was found. If no such
        /// property was found and <paramref name="strict"/> is set to false, the bottom-most object
        /// in the stack is returned.</returns>
        public ASAny getPropertyObj(ASAny key, int startLevel = 0, bool isAttribute = false, bool strict = false) {
            BindStatus status = tryFindPropertyObj(
                key, startLevel, out _, out ASAny value, isAttribute, strict);

            if (status == BindStatus.NOT_FOUND || status == BindStatus.SOFT_SUCCESS)
                throw ErrorHelper.createError(ErrorCode.VARIABLE_NOT_DEFINED, key);
            if (status == BindStatus.AMBIGUOUS)
                throw ErrorHelper.createError(ErrorCode.AMBIGUOUS_NAME_MATCH, key);

            return value;
        }

        /// <summary>
        /// Performs an object key lookup in the scope stack. If the given name is successfully
        /// resolved on an object in the scope stack, this method returns the value of the resolved
        /// property; otherwise, the stack is traversed downwards until the name resolves successfully
        /// or the bottom of the stack is reached.
        /// </summary>
        ///
        /// <param name="key">The key for which to find a property in the scope stack.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="startLevel">The level of the object in the stack at which to start the lookup
        /// relative to the top of the stack. 0 (the default value) is at the top and higher indices
        /// move towards the bottom.</param>
        /// <param name="isAttribute">Set this to true to search for attributes in XML objects. For
        /// any other properties, this value must be set to false.</param>
        /// <param name="strict">If set to true, and no property with the given name was found, an
        /// appropriate error is thrown. Otherwise, this method returns undefined if no property is
        /// found.</param>
        ///
        /// <returns>The object in which the property with the given key was found. If no such
        /// property was found and <paramref name="strict"/> is set to false, the bottom-most object
        /// in the stack is returned.</returns>
        public ASAny getPropertyObj(
            ASAny key, in NamespaceSet nsSet, int startLevel = 0, bool isAttribute = false, bool strict = false)
        {
            BindStatus status = tryFindPropertyObj(
                key, nsSet, startLevel, out _, out ASAny value, isAttribute, strict);

            if (status == BindStatus.NOT_FOUND || status == BindStatus.SOFT_SUCCESS)
                throw ErrorHelper.createError(ErrorCode.VARIABLE_NOT_DEFINED, key);
            if (status == BindStatus.AMBIGUOUS)
                throw ErrorHelper.createError(ErrorCode.AMBIGUOUS_NAME_MATCH, key);

            return value;
        }

    }
}
