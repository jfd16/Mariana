using System;
using System.Runtime.CompilerServices;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The Object class is the base class of all ActionScript 3 objects.
    /// </summary>
    [AVM2ExportClass(name = "Object", isDynamic = true, hasPrototypeMethods = true)]
    public class ASObject : IEquatable<ASObject> {

        /// <summary>
        /// These objects are used to synchronize lazy initialisation of the <see cref="m_class"/>,
        /// <see cref="m_proto"/> and <see cref="m_dynProps"/> fields of objects. An object will
        /// take one of these locks for initializing these fields based on its reference hash code.
        /// </summary>
        private static object[] s_internalFieldInitLocks = _createInternalFieldInitLocks();

        private static object[] _createInternalFieldInitLocks() {
            object[] locks = new object[13];
            for (int i = 0; i < locks.Length; i++)
                locks[i] = new object();
            return locks;
        }

        private static LazyInitObject<ASObject> s_lazyNumberClassProto =
            new LazyInitObject<ASObject>(() => Class.fromType<ASNumber>().prototypeObject);

        /// <summary>
        /// The value of the "length" property of the AS3 Object class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public const int AS_length = 1;

        /// <summary>
        /// The <see cref="Class"/> object representing the AVM2 class of which this object is an
        /// instance. This is lazily initialized when the <see cref="AS_class"/>,
        /// <see cref="AS_proto"/> or <see cref="AS_dynamicProps"/> property is accessed.
        /// </summary>
        private volatile Class m_class;

        /// <summary>
        /// The next object in the prototype chain of this object.
        /// </summary>
        private ASObject m_proto;

        /// <summary>
        /// The dynamic property table for this object. This is null for instances of non-dynamic
        /// classes.
        /// </summary>
        private DynamicPropertyCollection m_dynProps;

        /// <summary>
        /// Creates a new ActionScript 3 object.
        /// </summary>
        public ASObject() { }

        /// <summary>
        /// Gets the <see cref="Class"/> instance representing the object's class.
        /// </summary>
        public Class AS_class {
            get {
                Class klass = m_class;
                if (klass == null) {
                    _initInternalFields();
                    klass = m_class;
                }
                return klass;
            }
        }

        /// <summary>
        /// Gets the dynamic property collection for the object.
        /// </summary>
        /// <value>A <see cref="DynamicPropertyCollection"/> that can be used to access or modify
        /// the object's dynamic properties. If this object is not an instance of a dynamic class,
        /// the value of this property is null.</value>
        public DynamicPropertyCollection AS_dynamicProps {
            get {
                if (m_class == null)
                    _initInternalFields();
                return m_dynProps;
            }
        }

        /// <summary>
        /// Gets or sets the next object in the object's prototype chain.
        /// </summary>
        public ASObject AS_proto {
            get {
                if (m_class == null)
                    _initInternalFields();
                return m_proto;
            }
            set {
                if (m_class == null)
                    _initInternalFields();
                m_proto = value;
            }
        }

        private void _initInternalFields() {
            // To initialize these fields in a thread-safe way, we could create a
            // lock for each individual object (which would increase the memory footprint
            // of ALL AS3 objects), lock on "this" itself (which could result in inadvertent
            // deadlocks if outside code uses the objects as locks), or use a single global
            // lock (which would lead to threads waiting for what is not really a
            // shared resource). This hashcode based approach allows thread-safe initialisation
            // without an added object memory footprint while allowing a reasonable amount of
            // concurrency (which depends on the number of locks used).

            Class klass = null;
            Type objType = GetType();

            do {
                if (objType.IsVisible) {
                    klass = Class.fromType(objType, throwIfNotExists: false);
                    if (klass != null)
                        break;
                }
                objType = objType.BaseType;
            } while (objType != null);

            if (klass == null)
                return;

            int lockIndex = RuntimeHelpers.GetHashCode(this) % s_internalFieldInitLocks.Length;

            lock (s_internalFieldInitLocks[lockIndex]) {
                if (m_class != null)
                    return;

                m_class = klass;
                if (klass.isDynamic)
                    m_dynProps = new DynamicPropertyCollection();

                if (ClassTagSet.integer.contains(klass.tag))
                    // int and uint objects are given the Number prototype.
                    m_proto = s_lazyNumberClassProto.value;
                else
                    m_proto = klass.prototypeObject;
            }
        }

        #region PropertyBinding

        /// <summary>
        /// Performs a trait lookup on the object.
        /// </summary>
        /// <param name="name">The name of the trait to find.</param>
        /// <param name="trait">The trait with the name <paramref name="name"/>, if one
        /// exists.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the lookup.</returns>
        internal virtual BindStatus AS_lookupTrait(in QName name, out Trait trait) =>
            AS_class.lookupTrait(name, false, out trait);

        /// <summary>
        /// Performs a trait lookup on the object.
        /// </summary>
        /// <param name="name">The name of the trait to find.</param>
        /// <param name="nsSet">A set of namespaces in which to search for the trait.</param>
        /// <param name="trait">The trait with the name <paramref name="name"/> in a namespace of
        /// <paramref name="nsSet"/>, if one exists.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the lookup.</returns>
        internal virtual BindStatus AS_lookupTrait(string name, in NamespaceSet nsSet, out Trait trait) =>
            AS_class.lookupTrait(name, nsSet, false, out trait);

        /// <summary>
        /// Returns true if a property with the given name exists.
        /// </summary>
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>True, if a property with the name exists, false otherwise.</returns>
        public virtual bool AS_hasProperty(
            in QName name,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (name.ns.kind == NamespaceKind.ANY || (options & BindOptions.ATTRIBUTE) != 0)
                return false;

            if ((options & BindOptions.SEARCH_TRAITS) != 0 && AS_lookupTrait(name, out _) == BindStatus.SUCCESS)
                return true;

            if (!name.ns.isPublic && (name.localName != null || (options & BindOptions.RUNTIME_NAME) != 0)) {
                string key = ASString.AS_convertString(name.localName);

                DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
                if ((options & BindOptions.SEARCH_DYNAMIC) != 0 && dynamicProperties != null) {
                    if ((options & BindOptions.SEARCH_PROTOTYPE) != 0)
                        return DynamicPropertyCollection.searchPrototypeChain(this, key, out _);
                    return dynamicProperties.getIndex(key) != -1;
                }

                if ((options & BindOptions.SEARCH_PROTOTYPE) != 0)
                    return DynamicPropertyCollection.searchPrototypeChain(AS_proto, key, out _);
            }

            return false;
        }

        /// <summary>
        /// Returns true if a property with the given name exists in one of the namespaces of the
        /// given set.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to find the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>True, if a property exists, false otherwise.</returns>
        public virtual bool AS_hasProperty(
            string name, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & BindOptions.ATTRIBUTE) != 0)
                return false;

            if ((options & BindOptions.SEARCH_TRAITS) != 0 && AS_lookupTrait(name, nsSet, out _) == BindStatus.SUCCESS)
                return true;

            if (nsSet.containsPublic && (name != null || (options & BindOptions.RUNTIME_NAME) != 0)) {
                string key = ASString.AS_convertString(name);

                DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
                if ((options & BindOptions.SEARCH_DYNAMIC) != 0 && dynamicProperties != null) {
                    if ((options & BindOptions.SEARCH_PROTOTYPE) != 0)
                        return DynamicPropertyCollection.searchPrototypeChain(this, key, out _);
                    return dynamicProperties.getIndex(key) != -1;
                }

                if ((options & BindOptions.SEARCH_PROTOTYPE) != 0)
                    return DynamicPropertyCollection.searchPrototypeChain(AS_proto, key, out _);
            }

            return false;
        }

        /// <summary>
        /// Gets the value of the property with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryGetProperty(
            in QName name, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {

            if (name.ns.kind == NamespaceKind.ANY || (options & BindOptions.ATTRIBUTE) != 0) {
                value = default(ASAny);
                return BindStatus.NOT_FOUND;
            }

            Trait trait = null;
            BindStatus bindStatus = ((options & BindOptions.SEARCH_TRAITS) != 0)
                ? AS_lookupTrait(name, out trait)
                : BindStatus.NOT_FOUND;

            if (bindStatus == BindStatus.SUCCESS)
                return trait.tryGetValue(this, out value);

            if (bindStatus == BindStatus.AMBIGUOUS) {
                value = default(ASAny);
                return BindStatus.AMBIGUOUS;
            }

            if (name.ns.isPublic && (name.localName != null || (options & BindOptions.RUNTIME_NAME) != 0)) {
                DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
                string key = ASString.AS_convertString(name.localName);
                bool found;

                if ((options & BindOptions.SEARCH_DYNAMIC) != 0 && dynamicProperties != null) {
                    if ((options & BindOptions.SEARCH_PROTOTYPE) != 0)
                        found = DynamicPropertyCollection.searchPrototypeChain(this, key, out value);
                    else
                        found = dynamicProperties.tryGetValue(key, out value);
                    return found ? BindStatus.SUCCESS : BindStatus.SOFT_SUCCESS;
                }

                if ((options & BindOptions.SEARCH_PROTOTYPE) != 0) {
                    return DynamicPropertyCollection.searchPrototypeChain(AS_proto, key, out value)
                        ? BindStatus.SUCCESS
                        : BindStatus.NOT_FOUND;
                }
            }

            value = default(ASAny);
            return BindStatus.NOT_FOUND;

        }

        /// <summary>
        /// Gets the value of the property with the given name in one of the namespaces of the given
        /// set.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryGetProperty(
            string name, in NamespaceSet nsSet, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & BindOptions.ATTRIBUTE) != 0) {
                value = default(ASAny);
                return BindStatus.NOT_FOUND;
            }

            Trait trait = null;
            BindStatus bindStatus = ((options & BindOptions.SEARCH_TRAITS) != 0)
                ? AS_lookupTrait(name, nsSet, out trait)
                : BindStatus.NOT_FOUND;

            if (bindStatus == BindStatus.SUCCESS)
                return trait.tryGetValue(this, out value);

            if (bindStatus == BindStatus.AMBIGUOUS) {
                value = default(ASAny);
                return BindStatus.AMBIGUOUS;
            }

            if (nsSet.containsPublic && (name != null || (options & BindOptions.RUNTIME_NAME) != 0)) {
                DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
                string key = ASString.AS_convertString(name);
                bool found;

                if ((options & BindOptions.SEARCH_DYNAMIC) != 0 && dynamicProperties != null) {
                    if ((options & BindOptions.SEARCH_PROTOTYPE) != 0)
                        found = DynamicPropertyCollection.searchPrototypeChain(this, key, out value);
                    else
                        found = dynamicProperties.tryGetValue(key, out value);
                    return found ? BindStatus.SUCCESS : BindStatus.SOFT_SUCCESS;
                }

                if ((options & BindOptions.SEARCH_PROTOTYPE) != 0) {
                    return DynamicPropertyCollection.searchPrototypeChain(AS_proto, key, out value)
                        ? BindStatus.SUCCESS
                        : BindStatus.NOT_FOUND;
                }
            }

            value = default(ASAny);
            return BindStatus.NOT_FOUND;
        }

        /// <summary>
        /// Sets the value of the property with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_trySetProperty(
            in QName name, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if (name.ns.kind == NamespaceKind.ANY || (options & BindOptions.ATTRIBUTE) != 0)
                return BindStatus.NOT_FOUND;

            Trait trait = null;
            BindStatus bindStatus = ((options & BindOptions.SEARCH_TRAITS) != 0)
                ? AS_lookupTrait(name, out trait)
                : BindStatus.NOT_FOUND;

            if (bindStatus == BindStatus.SUCCESS)
                return trait.trySetValue(this, value);

            if (bindStatus == BindStatus.AMBIGUOUS)
                return BindStatus.AMBIGUOUS;

            if (name.localName == null && (options & BindOptions.RUNTIME_NAME) == 0)
                return BindStatus.NOT_FOUND;

            if (name.ns.isPublic) {
                DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
                if ((options & BindOptions.SEARCH_DYNAMIC) != 0 && dynamicProperties != null) {
                    string key = ASString.AS_convertString(name.localName);
                    dynamicProperties[key] = value;
                    return BindStatus.SUCCESS;
                }
            }
            else if ((options & BindOptions.SEARCH_DYNAMIC) != 0) {
                return BindStatus.FAILED_CREATEDYNAMICNONPUBLIC;
            }

            return BindStatus.NOT_FOUND;
        }

        /// <summary>
        /// Sets the value of the property with the given name in one of the namespaces of the given
        /// set.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_trySetProperty(
            string name, in NamespaceSet nsSet, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & BindOptions.ATTRIBUTE) != 0)
                return BindStatus.NOT_FOUND;

            Trait trait = null;
            BindStatus bindStatus = ((options & BindOptions.SEARCH_TRAITS) != 0)
                ? AS_lookupTrait(name, nsSet, out trait)
                : BindStatus.NOT_FOUND;

            if (bindStatus == BindStatus.SUCCESS)
                return trait.trySetValue(this, value);

            if (bindStatus == BindStatus.AMBIGUOUS)
                return BindStatus.AMBIGUOUS;

            if (name == null && (options & BindOptions.RUNTIME_NAME) == 0)
                return BindStatus.NOT_FOUND;

            if (nsSet.containsPublic) {
                DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
                if ((options & BindOptions.SEARCH_DYNAMIC) != 0 && dynamicProperties != null) {
                    string key = ASString.AS_convertString(name);
                    dynamicProperties[key] = value;
                    return BindStatus.SUCCESS;
                }
            }
            else if ((options & BindOptions.SEARCH_DYNAMIC) != 0) {
                return BindStatus.FAILED_CREATEDYNAMICNONPUBLIC;
            }

            return BindStatus.NOT_FOUND;
        }

        /// <summary>
        /// Invokes the value of the property with the given name as a function.
        /// </summary>
        ///
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="args">The arguments passed to the function call.</param>
        /// <param name="result">The return value of the call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryCallProperty(
            in QName name, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (name.ns.kind == NamespaceKind.ANY || (options & BindOptions.ATTRIBUTE) != 0) {
                result = default(ASAny);
                return BindStatus.NOT_FOUND;
            }

            ASObject receiver = ((options & BindOptions.NULL_RECEIVER) != 0) ? null : this;

            Trait trait = null;
            BindStatus bindStatus = ((options & BindOptions.SEARCH_TRAITS) != 0)
                ? AS_lookupTrait(name, out trait)
                : BindStatus.NOT_FOUND;

            result = default(ASAny);

            if (bindStatus == BindStatus.SUCCESS)
                return trait.tryInvoke(this, receiver, args, out result);

            if (bindStatus == BindStatus.AMBIGUOUS) {
                result = default(ASAny);
                return BindStatus.AMBIGUOUS;
            }

            if (name.ns.isPublic && (name.localName != null || (options & BindOptions.RUNTIME_NAME) != 0)) {
                string key = ASString.AS_convertString(name.localName);
                ASAny funcToInvoke;

                DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
                if ((options & BindOptions.SEARCH_DYNAMIC) != 0 && dynamicProperties != null) {
                    if ((options & BindOptions.SEARCH_PROTOTYPE) != 0)
                        DynamicPropertyCollection.searchPrototypeChain(this, key, out funcToInvoke);
                    else
                        funcToInvoke = dynamicProperties[key];
                }
                else if ((options & BindOptions.SEARCH_PROTOTYPE) != 0) {
                    if (!DynamicPropertyCollection.searchPrototypeChain(AS_proto, key, out funcToInvoke))
                        return BindStatus.NOT_FOUND;
                }
                else {
                    return BindStatus.NOT_FOUND;
                }

                if (funcToInvoke.value != null) {
                    return funcToInvoke.value.AS_tryInvoke(receiver, args, out result)
                        ? BindStatus.SUCCESS
                        : BindStatus.FAILED_NOTFUNCTION;
                }

                return BindStatus.FAILED_NOTFUNCTION;
            }

            return BindStatus.NOT_FOUND;
        }

        /// <summary>
        /// Invokes the value of the property with the given name in one of the namespaces of the
        /// given set as a function.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments passed to the function call.</param>
        /// <param name="result">The return value of the call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryCallProperty(
            string name, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & BindOptions.ATTRIBUTE) != 0) {
                result = default(ASAny);
                return BindStatus.NOT_FOUND;
            }

            ASObject receiver = ((options & BindOptions.NULL_RECEIVER) != 0) ? null : this;

            Trait trait = null;
            BindStatus bindStatus = ((options & BindOptions.SEARCH_TRAITS) != 0)
                ? AS_lookupTrait(name, nsSet, out trait)
                : BindStatus.NOT_FOUND;

            result = default(ASAny);

            if (bindStatus == BindStatus.SUCCESS)
                return trait.tryInvoke(this, receiver, args, out result);

            if (bindStatus == BindStatus.AMBIGUOUS)
                return BindStatus.AMBIGUOUS;

            if (nsSet.containsPublic && (name != null || (options & BindOptions.RUNTIME_NAME) != 0)) {
                string key = ASString.AS_convertString(name);
                ASAny funcToInvoke;

                DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
                if ((options & BindOptions.SEARCH_DYNAMIC) != 0 && dynamicProperties != null) {
                    if ((options & BindOptions.SEARCH_PROTOTYPE) != 0)
                        DynamicPropertyCollection.searchPrototypeChain(this, key, out funcToInvoke);
                    else
                        funcToInvoke = dynamicProperties[key];
                }
                else if ((options & BindOptions.SEARCH_PROTOTYPE) != 0) {
                    if (!DynamicPropertyCollection.searchPrototypeChain(AS_proto, key, out funcToInvoke))
                        return BindStatus.NOT_FOUND;
                }
                else
                    return BindStatus.NOT_FOUND;

                if (funcToInvoke.value != null)
                    return funcToInvoke.value.AS_tryInvoke(receiver, args, out result)
                        ? BindStatus.SUCCESS
                        : BindStatus.FAILED_NOTFUNCTION;

                return BindStatus.FAILED_NOTFUNCTION;
            }

            return BindStatus.NOT_FOUND;
        }

        /// <summary>
        /// Invokes the value of the property with the given name as a constructor.
        /// </summary>
        ///
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="args">The arguments passed to the constructor call.</param>
        /// <param name="result">The object created by the constructor.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryConstructProperty(
            in QName name, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if (name.ns.kind == NamespaceKind.ANY || (options & BindOptions.ATTRIBUTE) != 0) {
                result = default(ASAny);
                return BindStatus.NOT_FOUND;
            }

            Trait trait = null;
            result = default(ASAny);
            BindStatus bindStatus = ((options & BindOptions.SEARCH_TRAITS) != 0)
                ? AS_lookupTrait(name, out trait)
                : BindStatus.NOT_FOUND;

            if (bindStatus == BindStatus.SUCCESS)
                return trait.tryConstruct(this, args, out result);

            if (bindStatus == BindStatus.AMBIGUOUS)
                return BindStatus.AMBIGUOUS;

            if (name.ns.isPublic && (name.localName != null || (options & BindOptions.RUNTIME_NAME) != 0)) {
                string key = ASString.AS_convertString(name.localName);
                ASAny funcToInvoke;

                DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
                if ((options & BindOptions.SEARCH_DYNAMIC) != 0 && dynamicProperties != null) {
                    if ((options & BindOptions.SEARCH_PROTOTYPE) != 0)
                        DynamicPropertyCollection.searchPrototypeChain(this, key, out funcToInvoke);
                    else
                        funcToInvoke = dynamicProperties[key];
                }
                else if ((options & BindOptions.SEARCH_PROTOTYPE) != 0) {
                    if (!DynamicPropertyCollection.searchPrototypeChain(AS_proto, key, out funcToInvoke))
                        return BindStatus.NOT_FOUND;
                }
                else {
                    return BindStatus.NOT_FOUND;
                }

                if (funcToInvoke.value != null)
                    return funcToInvoke.value.AS_tryConstruct(args, out result)
                        ? BindStatus.SUCCESS
                        : BindStatus.FAILED_NOTCONSTRUCTOR;

                return BindStatus.FAILED_NOTCONSTRUCTOR;
            }

            return BindStatus.NOT_FOUND;
        }

        /// <summary>
        /// Invokes the value of the property with the given name in one of the namespaces of the
        /// given set as a constructor.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments passed to the constructor call.</param>
        /// <param name="result">The object created by the constructor.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryConstructProperty(
            string name, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & BindOptions.ATTRIBUTE) != 0) {
                result = default(ASAny);
                return BindStatus.NOT_FOUND;
            }

            Trait trait = null;
            BindStatus b = ((options & BindOptions.SEARCH_TRAITS) != 0)
                ? AS_lookupTrait(name, nsSet, out trait)
                : BindStatus.NOT_FOUND;

            result = default(ASAny);

            if (b == BindStatus.SUCCESS)
                return trait.tryConstruct(this, args, out result);

            if (b == BindStatus.AMBIGUOUS)
                return BindStatus.AMBIGUOUS;

            if (nsSet.containsPublic && (name != null || (options & BindOptions.RUNTIME_NAME) != 0)) {
                string key = ASString.AS_convertString(name);
                ASAny funcToInvoke;

                DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
                if ((options & BindOptions.SEARCH_DYNAMIC) != 0 && dynamicProperties != null) {
                    if ((options & BindOptions.SEARCH_PROTOTYPE) != 0)
                        DynamicPropertyCollection.searchPrototypeChain(this, key, out funcToInvoke);
                    else
                        funcToInvoke = dynamicProperties[key];
                }
                else if ((options & BindOptions.SEARCH_PROTOTYPE) != 0) {
                    if (!DynamicPropertyCollection.searchPrototypeChain(AS_proto, key, out funcToInvoke))
                        return BindStatus.NOT_FOUND;
                }
                else
                    return BindStatus.NOT_FOUND;

                if (funcToInvoke.value != null)
                    return funcToInvoke.value.AS_tryConstruct(args, out result)
                        ? BindStatus.SUCCESS
                        : BindStatus.FAILED_NOTCONSTRUCTOR;

                return BindStatus.FAILED_NOTCONSTRUCTOR;
            }

            return BindStatus.NOT_FOUND;
        }

        /// <summary>
        /// Deletes a property from the object.
        /// </summary>
        /// <param name="name">The name of the property to delete.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>True, if the property was deleted, false otherwise.</returns>
        /// <remarks>
        /// If a derived class does not override this method to provide custom behaviour, this method
        /// only deletes dynamic properties. Traits declared by the object's class cannot be deleted.
        /// </remarks>
        public virtual bool AS_deleteProperty(
            in QName name,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if (!name.ns.isPublic || (name.localName == null && (options & BindOptions.RUNTIME_NAME) == 0))
                return false;
            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return false;
            DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
            return dynamicProperties != null && dynamicProperties.delete(ASString.AS_convertString(name.localName));
        }

        /// <summary>
        /// Deletes a property from the object.
        /// </summary>
        ///
        /// <param name="name">The name of the property to delete.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property to
        /// delete.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>True, if the property was deleted, false otherwise.</returns>
        /// <remarks>
        /// If a derived class does not override this method to provide custom behaviour, this method
        /// only deletes dynamic properties. Traits declared by the object's class cannot be deleted.
        /// </remarks>
        public virtual bool AS_deleteProperty(
            string name, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if (!nsSet.containsPublic || (name == null && (options & BindOptions.RUNTIME_NAME) == 0))
                return false;
            if ((options & BindOptions.SEARCH_DYNAMIC) == 0)
                return false;
            DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
            return dynamicProperties != null && dynamicProperties.delete(ASString.AS_convertString(name));
        }

        /// <summary>
        /// Invokes the object as a function.
        /// </summary>
        /// <param name="receiver">The receiver of the call.</param>
        /// <param name="args">The arguments passed to the call.</param>
        /// <param name="result">The return value of the call.</param>
        /// <returns>True, if the call was successful, false otherwise.</returns>
        public virtual bool AS_tryInvoke(ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) {
            result = default(ASAny);
            return false;
        }

        /// <summary>
        /// Invokes the object as a constructor.
        /// </summary>
        /// <param name="args">The arguments passed to the call.</param>
        /// <param name="result">The object created by the constructor call.</param>
        /// <returns>True, if the call was successful, false otherwise.</returns>
        public virtual bool AS_tryConstruct(ReadOnlySpan<ASAny> args, out ASAny result) {
            result = default(ASAny);
            return false;
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object with a qualified name.
        /// </summary>
        ///
        /// <param name="name">The name argument to the descendants operator.</param>
        /// <param name="result">The result of invoking the descendants operator on the
        /// object.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryGetDescendants(
            in QName name, out ASAny result, BindOptions options = BindOptions.SEARCH_DYNAMIC)
        {
            result = default(ASAny);
            return BindStatus.FAILED_DESCENDANTOP;
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object with a name and
        /// namespace set.
        /// </summary>
        ///
        /// <param name="name">The local name argument to the descendants operator.</param>
        /// <param name="nsSet">The namespace set argument to the descendants operator.</param>
        /// <param name="result">The result of invoking the descendants operator on the
        /// object.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryGetDescendants(
            string name, in NamespaceSet nsSet, out ASAny result, BindOptions options = BindOptions.SEARCH_DYNAMIC)
        {
            result = default(ASAny);
            return BindStatus.FAILED_DESCENDANTOP;
        }

        /// <summary>
        /// Gets the value of the property with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The value of the property.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be retrieved.
        /// </remarks>
        public ASAny AS_getProperty(
            in QName name,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            BindStatus bindStatus = AS_tryGetProperty(name, out ASAny value, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return value;
            throw ErrorHelper.createBindingError(AS_class.name, name, bindStatus);
        }

        /// <summary>
        /// Gets the value of the property with the given name in the given set of namespaces.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The value of the property.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be retrieved.
        /// </remarks>
        public ASAny AS_getProperty(
            string name, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            BindStatus bindStatus = AS_tryGetProperty(name, nsSet, out ASAny value, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return value;
            throw ErrorHelper.createBindingError(AS_class.name.ToString(), name, bindStatus);
        }

        /// <summary>
        /// Sets the value of the property with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be set.
        /// </remarks>
        public void AS_setProperty(
            in QName name, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            BindStatus bindStatus = AS_trySetProperty(name, value, options);
            if (bindStatus != BindStatus.SUCCESS && bindStatus != BindStatus.SOFT_SUCCESS)
                throw ErrorHelper.createBindingError(AS_class.name, name, bindStatus);
        }

        /// <summary>
        /// Sets the value of the property with the given name in the given set of namespaces.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be set.
        /// </remarks>
        public void AS_setProperty(
            string name, in NamespaceSet nsSet,
            ASAny value, BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            BindStatus bindStatus = AS_trySetProperty(name, nsSet, value, options);
            if (bindStatus != BindStatus.SUCCESS && bindStatus != BindStatus.SOFT_SUCCESS)
                throw ErrorHelper.createBindingError(AS_class.name.ToString(), name, bindStatus);
        }

        /// <summary>
        /// Calls the value of the property with the given name as a function.
        /// </summary>
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="args">The arguments to the function call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The return value of the function call.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not callable.
        /// </remarks>
        public ASAny AS_callProperty(
            in QName name, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            BindStatus bindStatus = AS_tryCallProperty(name, args, out ASAny value, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return value;
            throw ErrorHelper.createBindingError(AS_class.name, name, bindStatus);
        }

        /// <summary>
        /// Calls the value of the property with the given name in the given set of namespaces as a
        /// function.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments to the function call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>The return value of the function call.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not callable.
        /// </remarks>
        public ASAny AS_callProperty(
            string name, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            BindStatus bindStatus = AS_tryCallProperty(name, nsSet, args, out ASAny value, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return value;
            throw ErrorHelper.createBindingError(AS_class.name.ToString(), name, bindStatus);
        }

        /// <summary>
        /// Calls the value of the property with the given name as a constructor.
        /// </summary>
        /// <param name="name">The qualified name of the property.</param>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The object created by the constructor call.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not constructible.
        /// </remarks>
        public ASAny AS_constructProperty(
            in QName name, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            BindStatus bindStatus = AS_tryConstructProperty(name, args, out ASAny value, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return value;
            throw ErrorHelper.createBindingError(AS_class.name, name, bindStatus);
        }

        /// <summary>
        /// Calls the value of the property with the given name in the given set of namespaces as a
        /// constructor.
        /// </summary>
        ///
        /// <param name="name">The name of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>The object created by the constructor call.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not constructible.
        /// </remarks>
        public ASAny AS_constructProperty(
            string name, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_DYNAMIC)
        {
            BindStatus bindStatus = AS_tryConstructProperty(name, nsSet, args, out ASAny value, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return value;
            throw ErrorHelper.createBindingError(AS_class.name.ToString(), name, bindStatus);
        }

        /// <summary>
        /// Invokes the object as a function.
        /// </summary>
        /// <param name="receiver">The receiver of the function call.</param>
        /// <param name="args">The arguments of the function call.</param>
        /// <returns>The return value of the function call.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1006</term>
        /// <description>The object is not callable.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASAny AS_invoke(ASAny receiver, ReadOnlySpan<ASAny> args) {
            if (AS_tryInvoke(receiver, args, out ASAny value))
                return value;
            throw ErrorHelper.createError(ErrorCode.INVOKE_NON_FUNCTION, "value");
        }

        /// <summary>
        /// Invokes the object as a constructor.
        /// </summary>
        /// <param name="args">The arguments of the constructor call.</param>
        /// <returns>The object created by the constructor.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1006</term>
        /// <description>The object is not constructible.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASAny AS_construct(ReadOnlySpan<ASAny> args) {
            if (AS_tryConstruct(args, out ASAny value))
                return value;
            throw ErrorHelper.createError(ErrorCode.INSTANTIATE_NON_CONSTRUCTOR);
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object with a qualified name.
        /// </summary>
        /// <param name="name">The name argument to the descendants operator.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The result of invoking the descendants operator on the object.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1016</term>
        /// <description>The descendants operator is not supported on the object.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASAny AS_getDescendants(in QName name, BindOptions options = BindOptions.SEARCH_DYNAMIC) {
            BindStatus bindStatus = AS_tryGetDescendants(name, out ASAny value, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return value;
            throw ErrorHelper.createError(ErrorCode.DESCENDANTS_NOT_SUPPORTED, AS_class.name.ToString());
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object with a name and
        /// namespace set.
        /// </summary>
        /// <param name="name">The local name argument to the descendants operator.</param>
        /// <param name="nsSet">The namespace set argument to the descendants operator.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The result of invoking the descendants operator on the object.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1016</term>
        /// <description>The descendants operator is not supported on the object.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASAny AS_getDescendants(
            string name, in NamespaceSet nsSet, BindOptions options = BindOptions.SEARCH_DYNAMIC)
        {
            BindStatus bindStatus = AS_tryGetDescendants(name, nsSet, out ASAny value, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return value;
            throw ErrorHelper.createError(ErrorCode.DESCENDANTS_NOT_SUPPORTED, AS_class.name.ToString());
        }

        /// <summary>
        /// Returns the index property defined on the object's class that can be used for an
        /// index property lookup with the given key.
        /// </summary>
        /// <param name="key">The key to be used for the index property lookup.</param>
        /// <returns>An instance of <see cref="IndexProperty"/>, or null if the object's class
        /// does not define an index property compatible with the type of <paramref name="key"/>.</returns>
        private IndexProperty _getIndexPropertyForKey(ASAny key) {
            ClassSpecials classSpecials = AS_class.classSpecials;
            if (classSpecials == null)
                return null;

            ASObject keyObj = key.value;
            if (keyObj == null)
                return null;

            switch (keyObj.AS_class.tag) {
                case ClassTag.INT:
                    return classSpecials.intIndexProperty ?? classSpecials.numberIndexProperty;
                case ClassTag.UINT:
                    return classSpecials.uintIndexProperty ?? classSpecials.numberIndexProperty;
                case ClassTag.NUMBER:
                    return classSpecials.numberIndexProperty;
            }

            return null;
        }

        /// <summary>
        /// Checks if a property with the given object key exists.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>True, if a property with the key exists, false otherwise.</returns>
        public virtual bool AS_hasPropertyObj(
            ASAny key,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            MethodTrait indexMethod = _getIndexPropertyForKey(key)?.hasMethod;
            if (indexMethod != null)
                return (bool)indexMethod.invoke(this, new ASAny[] {key});

            return (key.value is ASQName qName)
                ? AS_hasProperty(QName.fromASQName(qName), options)
                : AS_hasProperty(QName.publicName(ASAny.AS_convertString(key)), options);
        }

        /// <summary>
        /// Checks if a property with the given object key exists in the given set of namespaces.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>True, if a property with the key exists, false otherwise.</returns>
        public virtual bool AS_hasPropertyObj(
            ASAny key, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if (nsSet.containsPublic) {
                MethodTrait indexMethod = _getIndexPropertyForKey(key)?.hasMethod;
                if (indexMethod != null)
                    return (bool)indexMethod.invoke(this, new ASAny[] {key});
            }

            return (key.value is ASQName qName)
                ? AS_hasProperty(QName.fromASQName(qName), options)
                : AS_hasProperty(ASAny.AS_convertString(key), nsSet, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryGetPropertyObj(
            ASAny key, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC) {
                MethodTrait indexMethod = _getIndexPropertyForKey(key)?.getMethod;
                if (indexMethod != null) {
                    value = indexMethod.invoke(this, new ASAny[] {key});
                    return BindStatus.SUCCESS;
                }
            }

            return (key.value is ASQName qName)
                ? AS_tryGetProperty(QName.fromASQName(qName), out value, options)
                : AS_tryGetProperty(QName.publicName(ASAny.AS_convertString(key)), out value, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key in the given set of namespaces.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryGetPropertyObj(
            ASAny key, in NamespaceSet nsSet, out ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic)
            {
                MethodTrait indexMethod = _getIndexPropertyForKey(key)?.getMethod;
                if (indexMethod != null) {
                    value = indexMethod.invoke(this, new ASAny[] {key});
                    return BindStatus.SUCCESS;
                }
            }

            return (key.value is ASQName qName)
                ? AS_tryGetProperty(QName.fromASQName(qName), out value, options)
                : AS_tryGetProperty(ASAny.AS_convertString(key), nsSet, out value, options);
        }

        /// <summary>
        /// Sets the value of the property with the given object key.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_trySetPropertyObj(
            ASAny key, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC) {
                MethodTrait indexMethod = _getIndexPropertyForKey(key)?.setMethod;
                if (indexMethod != null) {
                    indexMethod.invoke(this, new ASAny[] {key, value});
                    return BindStatus.SUCCESS;
                }
            }

            return (key.value is ASQName qName)
                ? AS_trySetProperty(QName.fromASQName(qName), value, options)
                : AS_trySetProperty(QName.publicName(ASAny.AS_convertString(key)), value, options);
        }

        /// <summary>
        /// Sets the value of the property with the given object key in the given set of namespaces.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_trySetPropertyObj(
            ASAny key, in NamespaceSet nsSet, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic)
            {
                MethodTrait indexMethod = _getIndexPropertyForKey(key)?.setMethod;
                if (indexMethod != null) {
                    indexMethod.invoke(this, new ASAny[] {key, value});
                    return BindStatus.SUCCESS;
                }
            }

            return (key.value is ASQName qName)
                ? AS_trySetProperty(QName.fromASQName(qName), value, options)
                : AS_trySetProperty(ASAny.AS_convertString(key), nsSet, value, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key and invokes it as a function.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="args">The arguments passed to the function call.</param>
        /// <param name="result">The return value of the call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryCallPropertyObj(
            ASAny key, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC) {
                MethodTrait indexMethod = _getIndexPropertyForKey(key)?.getMethod;
                if (indexMethod != null) {
                    ASAny func = indexMethod.invoke(this, new ASAny[] {key});
                    ASObject receiver = ((options & BindOptions.NULL_RECEIVER) != 0) ? null : this;
                    return func.AS_tryInvoke(receiver, args, out result) ? BindStatus.SUCCESS : BindStatus.FAILED_NOTFUNCTION;
                }
            }

            return (key.value is ASQName qName)
                ? AS_tryCallProperty(QName.fromASQName(qName), args, out result, options)
                : AS_tryCallProperty(QName.publicName(ASAny.AS_convertString(key)), args, out result, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key in the given set of namespaces
        /// and invokes it as a function.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments passed to the function call.</param>
        /// <param name="result">The return value of the call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryCallPropertyObj(
            ASAny key, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic)
            {
                MethodTrait indexMethod = _getIndexPropertyForKey(key)?.getMethod;
                if (indexMethod != null) {
                    ASAny func = indexMethod.invoke(this, new ASAny[] {key});
                    ASObject receiver = ((options & BindOptions.NULL_RECEIVER) != 0) ? null : this;
                    return func.AS_tryInvoke(receiver, args, out result) ? BindStatus.SUCCESS : BindStatus.FAILED_NOTFUNCTION;
                }
            }

            return (key.value is ASQName qName)
                ? AS_tryCallProperty(QName.fromASQName(qName), args, out result, options)
                : AS_tryCallProperty(ASAny.AS_convertString(key), nsSet, args, out result, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key and invokes it as a constructor.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <param name="result">The object created by the constructor.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryConstructPropertyObj(
            ASAny key, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC) {
                MethodTrait indexMethod = _getIndexPropertyForKey(key)?.getMethod;
                if (indexMethod != null) {
                    ASAny func = indexMethod.invoke(this, new ASAny[] {key});
                    return func.AS_tryConstruct(args, out result) ? BindStatus.SUCCESS : BindStatus.FAILED_NOTCONSTRUCTOR;
                }
            }

            return (key.value is ASQName qName)
                ? AS_tryConstructProperty(QName.fromASQName(qName), args, out result, options)
                : AS_tryConstructProperty(QName.publicName(ASAny.AS_convertString(key)), args, out result, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key in the given set of namespaces
        /// and invokes it as a constructor.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <param name="result">The object created by the constructor.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryConstructPropertyObj(
            ASAny key, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args, out ASAny result,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic)
            {
                MethodTrait indexMethod = _getIndexPropertyForKey(key)?.getMethod;
                if (indexMethod != null) {
                    ASAny func = indexMethod.invoke(this, new ASAny[] {key});
                    return func.AS_tryConstruct(args, out result) ? BindStatus.SUCCESS : BindStatus.FAILED_NOTCONSTRUCTOR;
                }
            }

            return (key.value is ASQName qName)
                ? AS_tryConstructProperty(QName.fromASQName(qName), args, out result, options)
                : AS_tryConstructProperty(ASAny.AS_convertString(key), nsSet, args, out result, options);
        }

        /// <summary>
        /// Deletes the property with the given object key.
        /// </summary>
        /// <param name="key">The object key to look up the property to delete.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>True, if the property was deleted, false otherwise.</returns>
        ///
        /// <remarks>
        /// <para>If a derived class does not override this method to provide custom behaviour, this
        /// method only deletes dynamic properties. Traits declared by the object's class cannot be
        /// deleted.</para>
        /// </remarks>
        public virtual bool AS_deletePropertyObj(
            ASAny key,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC) {
                MethodTrait indexMethod = _getIndexPropertyForKey(key)?.deleteMethod;
                if (indexMethod != null)
                    return (bool)indexMethod.invoke(new ASAny[] {key});
            }

            return (key.value is ASQName qName)
                ? AS_deleteProperty(QName.fromASQName(qName), options)
                : AS_deleteProperty(QName.publicName(ASAny.AS_convertString(key)), options);
        }

        /// <summary>
        /// Deletes the property with the given object key in the given set of namespaces.
        /// </summary>
        /// <param name="key">The object key to look up the property to delete.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>True, if the property was deleted, false otherwise.</returns>
        ///
        /// <remarks>
        /// <para>If a derived class does not override this method to provide custom behaviour, this
        /// method only deletes dynamic properties. Traits declared by the object's class cannot be
        /// deleted.</para>
        /// </remarks>
        public virtual bool AS_deletePropertyObj(
            ASAny key, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC)
        {
            if ((options & (BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE)) == BindOptions.SEARCH_DYNAMIC
                && nsSet.containsPublic)
            {
                MethodTrait indexMethod = _getIndexPropertyForKey(key)?.deleteMethod;
                if (indexMethod != null)
                    return (bool)indexMethod.invoke(new ASAny[] {key});
            }

            return (key.value is ASQName qName)
                ? AS_deleteProperty(QName.fromASQName(qName), options)
                : AS_deleteProperty(ASAny.AS_convertString(key), nsSet, options);
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object using an object key.
        /// </summary>
        ///
        /// <param name="key">The object key argument to the descendants operator.</param>
        /// <param name="result">The result of invoking the descendants operator on the
        /// object.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryGetDescendantsObj(
            ASAny key, out ASAny result, BindOptions options = BindOptions.SEARCH_DYNAMIC)
        {
            return (key.value is ASQName qName)
                ? AS_tryGetDescendants(QName.fromASQName(qName), out result, options)
                : AS_tryGetDescendants(QName.publicName(ASAny.AS_convertString(key)), out result, options);
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object using an object key and
        /// namespace set.
        /// </summary>
        ///
        /// <param name="key">The object key argument to the descendants operator.</param>
        /// <param name="nsSet">The namespace set argument to the descendants operator.</param>
        /// <param name="result">The result of invoking the descendants operator on the
        /// object.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>A value from the <see cref="BindStatus"/> enumeration indicating the result of
        /// the operation.</returns>
        public virtual BindStatus AS_tryGetDescendantsObj(
            ASAny key, in NamespaceSet nsSet, out ASAny result, BindOptions options = BindOptions.SEARCH_DYNAMIC)
        {
            return (key.value is ASQName qName)
                ? AS_tryGetDescendants(QName.fromASQName(qName), out result, options)
                : AS_tryGetDescendants(ASAny.AS_convertString(key), nsSet, out result, options);
        }

        /// <summary>
        /// Gets the value of the property with the given object key.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The value of the property.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be retrieved.
        /// </remarks>
        public ASAny AS_getPropertyObj(
            ASAny key,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            BindStatus bindStatus = AS_tryGetPropertyObj(key, out ASAny value, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return value;
            throw ErrorHelper.createBindingError(AS_class.name.ToString(), ASAny.AS_convertString(key), bindStatus);
        }

        /// <summary>
        /// Gets the value of the property with the given object key.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The value of the property.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be retrieved.
        /// </remarks>
        public ASAny AS_getPropertyObj(
            ASAny key, in NamespaceSet nsSet,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            BindStatus bindStatus = AS_tryGetPropertyObj(key, nsSet, out ASAny value, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return value;
            throw ErrorHelper.createBindingError(AS_class.name.ToString(), ASAny.AS_convertString(key), bindStatus);
        }

        /// <summary>
        /// Sets the value of the property with the given object key.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be set.
        /// </remarks>
        public void AS_setPropertyObj(
            ASAny key, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            BindStatus bindStatus = AS_trySetPropertyObj(key, value, options);
            if (bindStatus != BindStatus.SUCCESS && bindStatus != BindStatus.SOFT_SUCCESS)
                throw ErrorHelper.createBindingError(AS_class.name.ToString(), ASAny.AS_convertString(key), bindStatus);
        }

        /// <summary>
        /// Sets the value of the property with the given object key.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value cannot be set.
        /// </remarks>
        public void AS_setPropertyObj(
            ASAny key, in NamespaceSet nsSet, ASAny value,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            BindStatus bindStatus = AS_trySetPropertyObj(key, nsSet, value, options);
            if (bindStatus != BindStatus.SUCCESS && bindStatus != BindStatus.SOFT_SUCCESS)
                throw ErrorHelper.createBindingError(AS_class.name.ToString(), ASAny.AS_convertString(key), bindStatus);
        }

        /// <summary>
        /// Gets the value of the property with the given object key and invokes it as a function.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="args">The arguments passed to the function call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The return value of the function call.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not callable.
        /// </remarks>
        public ASAny AS_callPropertyObj(
            ASAny key, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            BindStatus bindStatus = AS_tryCallPropertyObj(key, args, out ASAny result, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return result;
            throw ErrorHelper.createBindingError(AS_class.name.ToString(), ASAny.AS_convertString(key), bindStatus);
        }

        /// <summary>
        /// Gets the value of the property with the object key and invokes it as a function.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments passed to the function call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>The return value of the function call.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not callable.
        /// </remarks>
        public ASAny AS_callPropertyObj(
            ASAny key, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            BindStatus bindStatus = AS_tryCallPropertyObj(key, nsSet, args, out ASAny result, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return result;
            throw ErrorHelper.createBindingError(AS_class.name.ToString(), ASAny.AS_convertString(key), bindStatus);
        }

        /// <summary>
        /// Gets the value of the property with the given object key and invokes it as a constructor.
        /// </summary>
        /// <param name="key">The object key of the property.</param>
        /// <param name="args">The arguments passed to the constructor call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The return value of the constructor call, i.e. the created object.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not constructible.
        /// </remarks>
        public ASAny AS_constructPropertyObj(
            ASAny key, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            BindStatus bindStatus = AS_tryConstructPropertyObj(key, args, out ASAny result, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return result;
            throw ErrorHelper.createBindingError(AS_class.name.ToString(), ASAny.AS_convertString(key), bindStatus);
        }

        /// <summary>
        /// Gets the value of the property with the given object key and invokes it as a constructor.
        /// </summary>
        ///
        /// <param name="key">The object key of the property.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the property.</param>
        /// <param name="args">The arguments passed to the constructor call.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        ///
        /// <returns>The return value of the constructor call, i.e. the created object.</returns>
        /// <remarks>
        /// This method throws an error (usually TypeError or ReferenceError) if the property does not
        /// exist or its value is not constructible.
        /// </remarks>
        public ASAny AS_constructPropertyObj(
            ASAny key, in NamespaceSet nsSet, ReadOnlySpan<ASAny> args,
            BindOptions options = BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE)
        {
            BindStatus bindStatus = AS_tryConstructPropertyObj(key, nsSet, args, out ASAny result, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return result;
            throw ErrorHelper.createBindingError(AS_class.name.ToString(), ASAny.AS_convertString(key), bindStatus);
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object with an object key.
        /// </summary>
        /// <param name="key">The object key argument to the descendants operator.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The result of invoking the descendants operator on the object.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1016</term>
        /// <description>The descendants operator is not supported on the object.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASAny AS_getDescendantsObj(ASAny key, BindOptions options = BindOptions.SEARCH_DYNAMIC) {
            BindStatus bindStatus = AS_tryGetDescendantsObj(key, out ASAny result, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return result;
            throw ErrorHelper.createBindingError(AS_class.name.ToString(), ASAny.AS_convertString(key), bindStatus);
        }

        /// <summary>
        /// Invokes the ActionScript 3 descendants operator (..) on the object using object key
        /// lookup. An error is thrown if the object does not implement this operator.
        /// </summary>
        /// <param name="key">The object key argument to the descendants operator.</param>
        /// <param name="nsSet">The namespace set argument to the descendants operator.</param>
        /// <param name="options">The binding options for the search, indicating which properties are
        /// to be searched.</param>
        /// <returns>The result of invoking the descendants operator on the object.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1016</term>
        /// <description>The descendants operator is not supported on the object.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASAny AS_getDescendantsObj(
            ASAny key, in NamespaceSet nsSet, BindOptions options = BindOptions.SEARCH_DYNAMIC)
        {
            BindStatus bindStatus = AS_tryGetDescendantsObj(key, nsSet, out ASAny value, options);
            if (bindStatus == BindStatus.SUCCESS || bindStatus == BindStatus.SOFT_SUCCESS)
                return value;
            throw ErrorHelper.createBindingError(AS_class.name.ToString(), ASAny.AS_convertString(key), bindStatus);
        }

        /// <summary>
        /// Gets the one-based index of the next enumerable dynamic property after the given index.
        /// </summary>
        /// <param name="index">The index of the property from where to search for the next property.
        /// A value of 0 will return the index of the first enumerable property.</param>
        /// <returns>The one-based index of the next enumerable property, or 0 if there are no more
        /// enumerable properties.</returns>
        ///
        /// <remarks>
        /// <para>This method (along with <see cref="AS_nameAtIndex"/> and
        /// <see cref="AS_valueAtIndex"/>) is used to iterate for-in loops in ActionScript 3.
        /// Subclasses can override these methods for custom for-in loop behaviour.</para>
        /// </remarks>
        public virtual int AS_nextIndex(int index) {
            DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
            return (dynamicProperties == null) ? 0 : dynamicProperties.getNextIndex(index - 1) + 1;
        }

        /// <summary>
        /// Gets the name of the dynamic property at the given index. If the object is not dynamic,
        /// undefined is returned, unless a subclass provides an overridden implementation.
        /// </summary>
        /// <param name="index">The one-based index of the property. This index is usually obtained
        /// from the <see cref="AS_nextIndex"/> method.</param>
        /// <returns>The property name.</returns>
        ///
        /// <remarks>
        /// This method (along with <see cref="AS_nextIndex"/> and <see cref="AS_valueAtIndex"/>)
        /// is used to iterate for-in loops in ActionScript 3. Subclasses can override these methods
        /// for custom for-in loop behaviour.
        /// </remarks>
        public virtual ASAny AS_nameAtIndex(int index) {
            DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
            return (dynamicProperties == null) ? default(ASAny) : dynamicProperties.getNameFromIndex(index - 1);
        }

        /// <summary>
        /// Gets the value of the dynamic property at the given index. If the object is not dynamic,
        /// undefined is returned, unless a subclass provides an overridden implementation.
        /// </summary>
        /// <param name="index">The one-based index of the property. This index is usually obtained
        /// from the <see cref="AS_nextIndex"/> method.</param>
        /// <returns>The property value.</returns>
        ///
        /// <remarks>
        /// This method (along with <see cref="AS_nextIndex"/> and <see cref="AS_nameAtIndex"/>)
        /// is used to iterate for-in loops in ActionScript 3. Subclasses can override these methods
        /// for custom for-in loop behaviour.
        /// </remarks>
        public virtual ASAny AS_valueAtIndex(int index) {
            DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
            return (dynamicProperties == null) ? default(ASAny) : dynamicProperties.getValueFromIndex(index - 1);
        }

        /// <summary>
        /// Gets the one-based index of the next enumerable dynamic property after the given index for
        /// the given object, traversing its prototype chain if necessary.
        /// </summary>
        /// <param name="obj">The object to search for the next enumerable property.</param>
        /// <param name="index">The index of the property from where to search for the next
        /// property.</param>
        /// <returns>True, if the given object (or any object in its prototype chain) has an
        /// enumerable dynamic property after the given index, false otherwise.</returns>
        ///
        /// <remarks>
        /// <para>
        /// <paramref name="obj"/> and <paramref name="index"/> are reference parameters. If the
        /// object does not have any enumerable properties after the given index, its prototype chain
        /// will be searched for properties. In this case, <paramref name="obj"/> will be set to the
        /// object in the prototype chain having an enumerable property, and
        /// <paramref name="index"/> will be set to the index of the first enumerable property on
        /// that object. If there are no more objects in the prototype chain of the object, or none of
        /// the objects in the prototype chain contain enumerable properties, <paramref name="obj"/>
        /// is set to null, <paramref name="index"/> is set to 0 and the method returns false.
        /// </para>
        /// <para>This method is used to implement the <c>hasnext2</c> opcode in the AVM2, which
        /// is used for for-in loops in AS3. Calls to this method are injected into code generated by
        /// the ABCIL compiler. There are very few (if any) uses of it in .NET code.</para>
        /// </remarks>
        public static bool AS_hasnext2(ref ASObject obj, ref int index) {
            if (obj == null)
                return false;
            index = obj.AS_nextIndex(index);
            while (index == 0) {
                obj = obj.AS_proto;
                if (obj == null)
                    return false;
                index = obj.AS_nextIndex(0);
            }
            return true;
        }

        #endregion

        #region Conversions

        /// <summary>
        /// Converts the current instance to a Boolean value.
        /// </summary>
        /// <returns>The Boolean value</returns>
        ///
        /// <remarks>
        /// This method should not be overridden by any type other than the primitive types (int,
        /// uint, Number, String and Boolean). The ABCIL compiler assumes that other types do not
        /// override this method, and overriding will result in inconsistencies between compile-time
        /// and run-time behaviour.
        /// </remarks>
        protected virtual bool AS_coerceBoolean() => true;

        /// <summary>
        /// Converts the current instance to a floating-point number value.
        /// </summary>
        /// <returns>The floating-point number value.</returns>
        protected virtual double AS_coerceNumber() => (double)AS_toPrimitiveNumHint(this);

        /// <summary>
        /// Converts the current instance to an integer value.
        /// </summary>
        /// <returns>The integer value.</returns>
        protected virtual int AS_coerceInt() => (int)AS_toPrimitiveNumHint(this);

        /// <summary>
        /// Converts the current instance to an unsigned integer value.
        /// </summary>
        /// <returns>The unsigned integer value.</returns>
        protected virtual uint AS_coerceUint() => (uint)AS_toPrimitiveNumHint(this);

        /// <summary>
        /// Converts the current instance to a string value.
        /// </summary>
        /// <returns>The string value.</returns>
        protected virtual string AS_coerceString() => ASAny.AS_convertString(AS_toPrimitiveStringHint(this));

        /// <summary>
        /// Converts a Boolean value to a boxed object.
        /// </summary>
        /// <param name="x">The Boolean value to convert to a boxed object.</param>
        /// <returns>The boxed object.</returns>
        public static implicit operator ASObject(bool x) => x ? ASBoolean.s_trueVal : ASBoolean.s_falseVal;

        /// <summary>
        /// Converts a Boolean value to a boxed object.
        /// </summary>
        /// <param name="x">The Boolean value to convert to a boxed object.</param>
        /// <returns>The boxed object.</returns>
        public static ASObject AS_fromBoolean(bool x) => x ? ASBoolean.s_trueVal : ASBoolean.s_falseVal;

        /// <summary>
        /// Converts an integer to a boxed object.
        /// </summary>
        /// <param name="x">The integer to convert to a boxed object.</param>
        /// <returns>The boxed object.</returns>
        public static implicit operator ASObject(int x) => ASint.box(x);

        /// <summary>
        /// Converts an integer to a boxed object.
        /// </summary>
        /// <param name="x">The integer to convert to a boxed object.</param>
        /// <returns>The boxed object.</returns>
        public static ASObject AS_fromInt(int x) => ASint.box(x);

        /// <summary>
        /// Converts an unsigned integer to a boxed object.
        /// </summary>
        /// <param name="x">The unsigned integer to convert to a boxed object.</param>
        /// <returns>The boxed object.</returns>
        public static implicit operator ASObject(uint x) => ASuint.box(x);

        /// <summary>
        /// Converts an unsigned integer to a boxed object.
        /// </summary>
        /// <param name="x">The unsigned integer to convert to a boxed object.</param>
        /// <returns>The boxed object.</returns>
        public static ASObject AS_fromUint(uint x) => ASuint.box(x);

        /// <summary>
        /// Converts a floating-point number to a boxed object.
        /// </summary>
        /// <param name="x">The number to convert to a boxed object.</param>
        /// <returns>The boxed object.</returns>
        public static implicit operator ASObject(double x) => ASNumber.box(x);

        /// <summary>
        /// Converts a floating-point number to a boxed object.
        /// </summary>
        /// <param name="x">The number to convert to a boxed object.</param>
        /// <returns>The boxed object.</returns>
        public static ASObject AS_fromNumber(double x) => ASNumber.box(x);

        /// <summary>
        /// Converts a string to a boxed object.
        /// </summary>
        /// <param name="x">The string to convert to a boxed object.</param>
        /// <returns>The boxed object.</returns>
        public static implicit operator ASObject(string x) => ASString.box(x);

        /// <summary>
        /// Converts a string to a boxed object.
        /// </summary>
        /// <param name="x">The string to convert to a boxed object.</param>
        /// <returns>The boxed object.</returns>
        public static ASObject AS_fromString(string x) => ASString.box(x);

        /// <summary>
        /// Converts the given object to a Boolean value.
        /// </summary>
        /// <param name="x">The object to convert.</param>
        /// <returns>The Boolean value.</returns>
        public static explicit operator bool(ASObject x) => x != null && x.AS_coerceBoolean();

        /// <summary>
        /// Converts the given object to a Boolean value.
        /// </summary>
        /// <param name="x">The object to convert.</param>
        /// <returns>The Boolean value.</returns>
        public static bool AS_toBoolean(ASObject x) => x != null && x.AS_coerceBoolean();

        /// <summary>
        /// Converts the given object to an integer value.
        /// </summary>
        /// <param name="x">The object to convert.</param>
        /// <returns>The integer value.</returns>
        public static explicit operator int(ASObject x) => (x == null) ? 0 : x.AS_coerceInt();

        /// <summary>
        /// Converts the given object to an integer value.
        /// </summary>
        /// <param name="x">The object to convert.</param>
        /// <returns>The integer value.</returns>
        public static int AS_toInt(ASObject x) => (x == null) ? 0 : x.AS_coerceInt();

        /// <summary>
        /// Converts the given object to an unsigned integer value.
        /// </summary>
        /// <param name="x">The object to convert.</param>
        /// <returns>The unsigned integer value.</returns>
        public static explicit operator uint(ASObject x) => (x == null) ? 0u : x.AS_coerceUint();

        /// <summary>
        /// Converts the given object to an unsigned integer value.
        /// </summary>
        /// <param name="x">The object to convert.</param>
        /// <returns>The unsigned integer value.</returns>
        public static uint AS_toUint(ASObject x) => (x == null) ? 0u : x.AS_coerceUint();

        /// <summary>
        /// Converts the given object to a floating-point number value.
        /// </summary>
        /// <param name="x">The object to convert.</param>
        /// <returns>The floating-point number value.</returns>
        public static explicit operator double(ASObject x) => (x == null) ? 0.0 : x.AS_coerceNumber();

        /// <summary>
        /// Converts the given object to a floating-point number value.
        /// </summary>
        /// <param name="x">The object to convert.</param>
        /// <returns>The floating-point number value.</returns>
        public static double AS_toNumber(ASObject x) => (x == null) ? 0.0 : x.AS_coerceNumber();

        /// <summary>
        /// Converts the given object to a string value. This operator uses the
        /// <see cref="AS_coerceString(ASObject)"/> method, which converts null to null.
        /// </summary>
        /// <param name="x">The object to convert.</param>
        /// <returns>The string value.</returns>
        public static explicit operator string(ASObject x) => x?.AS_coerceString();

        /// <summary>
        /// Converts the given object to a string value. The null object is converted to the string
        /// "null".
        /// </summary>
        /// <param name="x">The object to convert.</param>
        /// <returns>The string value.</returns>
        public static string AS_convertString(ASObject x) => (x == null) ? "null" : x.AS_coerceString();

        /// <summary>
        /// Converts the given object to a string value. This method differs from
        /// <see cref="AS_convertString"/> in that null is converted to the null string and not the
        /// string "null".
        /// </summary>
        ///
        /// <param name="x">The object to convert.</param>
        /// <returns>The string value.</returns>
        public static string AS_coerceString(ASObject x) => (x == null) ? null : x.AS_coerceString();

        /// <summary>
        /// Creates an <see cref="ASObject"/> from the given argument, of an object or boxed
        /// primitive type.
        /// </summary>
        /// <param name="obj">An instance of <see cref="ASObject"/> or a boxed primitive.</param>
        ///
        /// <returns>
        /// If <paramref name="obj"/> is an instance of <see cref="ASObject"/>, returns the object
        /// itself. If <paramref name="obj"/> is a (boxed) instance of <see cref="ASAny"/>, returns
        /// the value of its <see cref="ASAny.value" qualifyHint="true"/> property. If
        /// <paramref name="obj"/> is a boxed primitive, returns the AS3 boxed form of that primitive
        /// (e.g. <see cref="ASint"/> for the type <see cref="Int32"/>). If the primitive type of
        /// <paramref name="obj"/> is <see cref="Byte"/>, <see cref="SByte"/>,
        /// <see cref="Int16"/> or <see cref="UInt16"/>, it is widened to <see cref="Int32"/>; if
        /// it is <see cref="Single"/>, it is widened to <see cref="Double"/>.
        /// </returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #10058</term>
        /// <description><paramref name="obj"/> is not an object of type <see cref="ASObject"/> or a
        /// type deriving from it, a boxed instance of <see cref="ASAny"/>, or a boxed instance of a
        /// valid primitive type.</description>
        /// </item>
        /// </list>
        /// </exception>
        public static ASObject AS_fromBoxed(object obj) {
            ASObject asobj = obj as ASObject;
            if (asobj != null || obj == null)
                return asobj;

            if (obj is ASAny any)
                return any.value;

            switch (Type.GetTypeCode(obj.GetType())) {
                case TypeCode.Byte:
                    return AS_fromInt((byte)obj);
                case TypeCode.SByte:
                    return AS_fromInt((sbyte)obj);
                case TypeCode.Int16:
                    return AS_fromInt((short)obj);
                case TypeCode.UInt16:
                    return AS_fromInt((ushort)obj);
                case TypeCode.Int32:
                    return AS_fromInt((int)obj);
                case TypeCode.UInt32:
                    return AS_fromUint((uint)obj);
                case TypeCode.Single:
                    return AS_fromNumber((float)obj);
                case TypeCode.Double:
                    return AS_fromNumber((double)obj);
                case TypeCode.String:
                    return AS_fromString((string)obj);
                case TypeCode.Boolean:
                    return AS_fromBoolean((bool)obj);
            }

            throw ErrorHelper.createError(ErrorCode.MARIANA__OBJECT_FROMPRIMITIVE_INVALID);
        }

        /// <summary>
        /// Performs a class cast of object <paramref name="obj"/> to type
        /// <typeparamref name="T"/>. An error is thrown if the object is not of the specified type.
        /// </summary>
        /// <param name="obj">The object to cast.</param>
        /// <typeparam name="T">The type to cast the object to.</typeparam>
        /// <returns>The object <paramref name="obj"/> cast to type
        /// <typeparamref name="T"/>.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1034</term>
        /// <description>If the object is not of the type given as the type argument, a class derived
        /// from it, or an implementation (if the type argument is an interface).</description>
        /// </item>
        /// </list>
        /// </exception>
        public static T AS_cast<T>(object obj) where T : class {
            T x = obj as T;
            if (x == null && obj != null)
                throw ErrorHelper.createCastError(obj.GetType(), typeof(T));
            return x;
        }

        /// <summary>
        /// Coerces the given object to the given type. Use this method when the type to coerce to is
        /// not known at compile time.
        /// </summary>
        /// <param name="obj">The object to coerce.</param>
        /// <param name="toClass">The <see cref="Class"/> object representing the type to coerce the
        /// object to.</param>
        /// <returns>The object, coerced to the given type.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1034</term>
        /// <description>The type conversion is unsuccessful.</description>
        /// </item>
        /// </list>
        /// </exception>
        public static ASObject AS_coerceType(ASObject obj, Class toClass) {
            if (toClass == null || (obj != null && obj.AS_class == toClass))
                return obj;

            switch (toClass.tag) {
                case ClassTag.INT:
                    return (int)obj;
                case ClassTag.UINT:
                    return (uint)obj;
                case ClassTag.NUMBER:
                    return (double)obj;
                case ClassTag.BOOLEAN:
                    return (bool)obj;
                case ClassTag.STRING:
                    return (string)obj;
                default:
                    if (obj == null || obj.AS_class.canAssignTo(toClass))
                        return obj;
                    throw ErrorHelper.createCastError(obj, toClass);
            }
        }

        /// <summary>
        /// Converts the given object to a primitive object with no hint.
        /// </summary>
        /// <param name="obj">The object to convert.</param>
        /// <returns>The primitive object.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1050</term>
        /// <description>Neither the toString nor the valueOf method of the object returns a primitive
        /// object (an object of the int, uint, Boolean, Number or String type).</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// For Date objects, the string hint is used (i.e. <c>toString()</c> is called before
        /// <c>valueOf()</c>); for any other object, the number hint is used (<c>valueOf()</c>
        /// is called before <c>toString()</c>).
        /// </remarks>
        public static ASAny AS_toPrimitive(ASObject obj) {
            return (obj is ASDate) ? AS_toPrimitiveStringHint(obj) : AS_toPrimitiveNumHint(obj);
        }

        /// <summary>
        /// Converts the given object to a primitive object with string hint.
        /// </summary>
        /// <param name="obj">The object to convert.</param>
        /// <returns>The primitive object.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1050</term>
        /// <description>Neither the toString nor the valueOf method of the object returns a primitive
        /// object (an object of the int, uint, Boolean, Number or String type).</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// The string hint is used, i.e. the object's <c>toString()</c> method is called first,
        /// and its <c>valueOf()</c> method is called only if its <c>toString()</c> method
        /// does not return a primitive object.
        /// </remarks>
        public static ASAny AS_toPrimitiveStringHint(ASObject obj) {
            if (obj == null)
                return ASAny.@null;

            ASAny result;
            result = obj.AS_callProperty(QName.publicName("toString"), Array.Empty<ASAny>());
            if (result.value == null || ClassTagSet.primitive.contains(result.value.AS_class.tag))
                return result;

            result = obj.AS_callProperty(QName.publicName("valueOf"), Array.Empty<ASAny>());
            if (result.value == null || ClassTagSet.primitive.contains(result.value.AS_class.tag))
                return result;

            throw ErrorHelper.createError(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE);
        }

        /// <summary>
        /// Converts the given object to a primitive object with number hint.
        /// </summary>
        /// <param name="obj">The object to convert.</param>
        /// <returns>The primitive object.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1050</term>
        /// <description>Neither the toString nor the valueOf method of the object returns a primitive
        /// object (an object of the int, uint, Boolean, Number or String type).</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// The number hint is used, i.e. the object's <c>valueOf()</c> method is called first,
        /// and its <c>toString()</c> method is called only if its <c>valueOf()</c> method
        /// does not return a primitive object.
        /// </remarks>
        public static ASAny AS_toPrimitiveNumHint(ASObject obj) {
            if (obj == null)
                return ASAny.@null;

            ASAny result;
            result = obj.AS_callProperty(QName.publicName("valueOf"), Array.Empty<ASAny>());
            if (result.value == null || ClassTagSet.primitive.contains(result.value.AS_class.tag))
                return result;

            result = obj.AS_callProperty(QName.publicName("toString"), Array.Empty<ASAny>());
            if (result.value == null || ClassTagSet.primitive.contains(result.value.AS_class.tag))
                return result;

            throw ErrorHelper.createError(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE);
        }

        #endregion

        #region Operators

        /// <summary>
        /// Compares two objects using the definition of the weak equality operator (==) is
        /// ActionScript 3.
        /// </summary>
        /// <param name="x">The first object.</param>
        /// <param name="y">The second object.</param>
        /// <returns>True, if the two objects are equal, false otherwise.</returns>
        ///
        /// <remarks>
        /// <para>The comparison is done as follows (in order):</para>
        /// <list type="bullet">
        /// <item>If one of the objects is null, both objects are equal if and only if the other
        /// object is null.</item>
        /// <item>If both objects are equal by reference, they are considered equal.</item>
        /// <item>If one of the objects is of a numeric type (int, uint, Number) or Boolean, then both
        /// objects are converted to the Number type and the floating-point number values are
        /// compared.</item>
        /// <item>If both the objects are strings, the string values are compared. The comparison is
        /// based on character code points and is locale-independent.</item>
        /// <item>Two Namespace objects are equal if they have the same URI.</item>
        /// <item>Two QName objects are equal if they have the same URI and local name.</item>
        /// <item>If both the operands are of the XML or XMLList type, and both have simple content,
        /// they are converted to strings and the string values are compared.</item>
        /// <item>
        /// If both operands are of the XML type, they are equal if and only if they (i) have the same
        /// node type, (ii) have the same name, if they are elements or attributes, (iii) have the
        /// same text, if they are text nodes or attributes and (iv) have the same set of attributes
        /// and the same child nodes, if they are elements. (The comparison of child nodes is done
        /// recursively.)
        /// </item>
        /// <item>If both operands are of the XMLList type, they are equal if and only if they have
        /// the same number of items and the items at corresponding indices of both lists are
        /// equal.</item>
        /// <item>If one operand is XML and the other is XMLList, they are equal if and only if (i)
        /// the XMLList has only one item, and (ii) that item is equal to the other (XML)
        /// operand.</item>
        /// <item>If one of the objects is an XML object having simple content, both objects are
        /// converted to strings and a string comparison is done.</item>
        /// <item>Otherwise, the two objects are not equal.</item>
        /// </list>
        /// </remarks>
        public static bool AS_weakEq(ASObject x, ASObject y) {
            if (x == y)  // Equal by reference, or both null
                return true;

            ClassTagSet tagSet = default;
            if (x != null)
                tagSet.add(x.AS_class.tag);
            if (y != null)
                tagSet.add(y.AS_class.tag);

            if (ClassTagSet.xmlOrXmlList.containsAny(tagSet))
                // null equals a simple-content XML object with "null", so do this check first.
                return XMLHelper.weakEquals(x, y);

            if (x == null || y == null)
                return false;

            if (ClassTagSet.numericOrBool.containsAny(tagSet))
                return (double)x == (double)y;

            if (tagSet.isSingle(ClassTag.STRING))
                return (string)x == (string)y;

            if (tagSet.isSingle(ClassTag.QNAME)) {
                // We don't call ASQName.AS_equals here because it does the reference equal
                // and null checks, which are redundant at this point.
                ASQName qname1 = (ASQName)x, qname2 = (ASQName)y;
                return qname1.localName == qname2.localName && qname1.uri == qname2.uri;
            }

            if (tagSet.isSingle(ClassTag.NAMESPACE))
                return ((ASNamespace)x).uri == ((ASNamespace)y).uri;

            return false;
        }

        /// <summary>
        /// Compares two objects using the definition of the strict equality operator (===) is
        /// ActionScript 3.
        /// </summary>
        /// <param name="x">The first object.</param>
        /// <param name="y">The second object.</param>
        /// <returns>True, if the two objects are equal, false otherwise.</returns>
        ///
        /// <remarks>
        /// <para>The comparison is done as follows (in order):</para>
        /// <list type="bullet">
        /// <item>If one of the objects is null, the objects are equal if and only if the other object
        /// is null.</item>
        /// <item>If both objects are equal by reference, they are considered equal.</item>
        /// <item>If both the objects are of numeric types (int, uint or Number), then both objects
        /// are converted to the Number type and the floating-point number values are
        /// compared.</item>
        /// <item>If both the objects are Strings, the string values are compared. The comparison is
        /// based on character code points and is locale-independent.</item>
        /// <item>If both the objects are of the Namespace type, they are equal if they have the same
        /// URIs. If both are of the QName type, they are equal if their namespace URIs and local
        /// names are equal.</item>
        /// <item>Otherwise, the two objects are not equal. Unlike weak equality, strict equality
        /// considers XML and XMLList objects as ordinary objects and they are compared by reference
        /// only.</item>
        /// </list>
        /// </remarks>
        public static bool AS_strictEq(ASObject x, ASObject y) {
            if (x == y)  // Equal by reference
                return true;
            if (x == null || y == null)
                return false;

            var tagSet = new ClassTagSet(x.AS_class.tag, y.AS_class.tag);

            if (ClassTagSet.numeric.containsAll(tagSet))
                return (double)x == (double)y;

            if (tagSet.isSingle(ClassTag.STRING))
                return (string)x == (string)y;

            if (tagSet.isSingle(ClassTag.QNAME)) {
                ASQName qname1 = (ASQName)x, qname2 = (ASQName)y;
                return qname1.localName == qname2.localName && qname1.uri == qname2.uri;
            }

            if (tagSet.isSingle(ClassTag.NAMESPACE))
                return ((ASNamespace)x).uri == ((ASNamespace)y).uri;

            return false;
        }

        /// <summary>
        /// Compares two objects and returns true if the first object is less than the second.
        /// </summary>
        /// <param name="x">The first object.</param>
        /// <param name="y">The second object.</param>
        /// <returns>True, if <paramref name="x"/> is less than <paramref name="y"/>, false
        /// otherwise.</returns>
        ///
        /// <remarks>
        /// The comparison is done using the definition of the less-than operator in ActionScript 3.
        /// If both operands are strings, a string comparison is done. The string comparison is based
        /// on character code points and is locale-independent. Otherwise, the objects are converted
        /// to the Number type and their floating-point values are compared.
        /// </remarks>
        public static bool AS_lessThan(ASObject x, ASObject y) {
            if (x == y) // Equal by reference
                return false;

            if (x is ASString && y is ASString)
                return String.Compare(AS_coerceString(x), AS_coerceString(y)) < 0;

            return (double)x < (double)y;
        }

        /// <summary>
        /// Compares two objects and returns true if the first object is less than or equal to the
        /// second.
        /// </summary>
        /// <param name="x">The first object.</param>
        /// <param name="y">The second object.</param>
        /// <returns>True, if <paramref name="x"/> is less than or equal to
        /// <paramref name="y"/>, false otherwise.</returns>
        ///
        /// <remarks>
        /// The comparison is done using the definition of the less than or equal to operator in
        /// ActionScript 3. If both operands are strings, a string comparison is done. The string
        /// comparison is based on character code points and is locale-independent. Otherwise, the
        /// objects are converted to the Number type and their floating-point values are compared.
        /// </remarks>
        public static bool AS_lessEq(ASObject x, ASObject y) {
            if (x == y) // Equal by reference
                return true;

            if (x is ASString && y is ASString)
                return String.CompareOrdinal(AS_coerceString(x), AS_coerceString(y)) <= 0;

            return (double)x <= (double)y;
        }

        /// <summary>
        /// Compares two objects and returns true if the first object is greater than or equal to the
        /// second.
        /// </summary>
        ///
        /// <param name="x">The first object.</param>
        /// <param name="y">The second object.</param>
        /// <returns>True, if <paramref name="x"/> is greater than <paramref name="y"/>, false
        /// otherwise.</returns>
        ///
        /// <remarks>
        /// The comparison is done using the definition of the greater than or equal to operator in
        /// ActionScript 3. If both operands are non-null strings, a string comparison is done. The
        /// string comparison is based on character code points and is locale-independent. Otherwise,
        /// the objects are converted to the Number type and their floating-point values are compared.
        /// </remarks>
        public static bool AS_greaterEq(ASObject x, ASObject y) => AS_lessEq(y, x);

        /// <summary>
        /// Compares two objects and returns true if the first object is greater than the second. This
        /// is equivalent to <c>AS_lessThan(<paramref name="y"/>, <paramref name="x"/>)</c>.
        /// </summary>
        /// <param name="x">The first object.</param>
        /// <param name="y">The second object.</param>
        /// <returns>True, if <paramref name="x"/> is greater than <paramref name="y"/>, false
        /// otherwise.</returns>
        ///
        /// <remarks>
        /// The comparison is done using the definition of the greater than operator in ActionScript
        /// 3. If both operands are non-null strings, a string comparison is done. The string
        /// comparison is based on character code points and is locale-independent. Otherwise, the
        /// objects are converted to the Number type and their floating-point values are compared.
        /// </remarks>
        public static bool AS_greaterThan(ASObject x, ASObject y) => AS_greaterThan(y, x);

        /// <summary>
        /// Adds two objects using the definition of the addition (+) operator in ActionScript 3.
        /// </summary>
        /// <param name="x">The first object.</param>
        /// <param name="y">The second object.</param>
        /// <returns>The result of the addition.</returns>
        ///
        /// <remarks>
        /// <para>The definition of the addition operator is as follows:</para>
        /// <list type="bullet">
        /// <item>If both the objects are null, both are of numeric types (int, uint, Number) or
        /// Boolean, or one of the objects is null and the other is of a numeric type or Boolean, the
        /// objects are converted to the Number type and the floating-point number values are
        /// added.</item>
        /// <item>If one of the objects is a string or Date, both objects are converted to Strings and
        /// the string values are concatenated.</item>
        /// <item>If both objects are of the XML or XMLList types, the two objects are concatenated
        /// into an XMLList.</item>
        /// <item>Otherwise, the two objects are converted to primitive objects by calling their
        /// <c>valueOf</c> or <c>toString</c> methods (with no hint) and the primitive objects
        /// are added.</item>
        /// </list>
        /// </remarks>
        public static ASObject AS_add(ASObject x, ASObject y) {
            ClassTagSet tagSet = default;
            if (x != null)
                tagSet = tagSet.add(x.AS_class.tag);
            if (y != null)
                tagSet = tagSet.add(y.AS_class.tag);

            if (ClassTagSet.numericOrBool.containsAll(tagSet))
                return (double)x + (double)y;
            if (ClassTagSet.stringOrDate.containsAny(tagSet))
                return ASObject.AS_convertString(x) + ASObject.AS_convertString(y);

            if (ClassTagSet.xmlOrXmlList.containsAll(tagSet) && x != null && y != null)
                return XMLHelper.concatenateXMLObjects(x, y);

            ASAny prim1 = AS_toPrimitive(x);
            ASAny prim2 = AS_toPrimitive(y);

            tagSet = default;
            if (prim1.value != null)
                tagSet = tagSet.add(prim1.value.AS_class.tag);
            if (prim2.value != null)
                tagSet = tagSet.add(prim2.value.AS_class.tag);

            if (ClassTagSet.numericOrBool.containsAll(tagSet))
                return (double)prim1 + (double)prim2;

            return ASAny.AS_convertString(prim1) + ASAny.AS_convertString(prim1);
        }

        /// <summary>
        /// Returns a string indicating the type of the given object. This method is used to implement
        /// the AS3 <c>typeof</c> operator.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>A string indicating the type of the given object.</returns>
        ///
        /// <remarks>
        /// The strings returned for different types are as follows: "number" (for int, uint and
        /// Number types), "boolean" (for Boolean), "string" (for String), "function" (for Function
        /// objects), "xml" (for XML and XMLList objects) or "object" (for null or objects of any
        /// other type).
        /// </remarks>
        public static string AS_typeof(ASObject obj) {
            if (obj != null) {
                switch (obj.AS_class.tag) {
                    case ClassTag.NUMBER:
                    case ClassTag.INT:
                    case ClassTag.UINT:
                        return "number";
                    case ClassTag.BOOLEAN:
                        return "boolean";
                    case ClassTag.STRING:
                        return "string";
                    case ClassTag.FUNCTION:
                        return "function";
                    case ClassTag.XML:
                    case ClassTag.XML_LIST:
                        return "xml";
                }
            }
            return "object";
        }

        /// <summary>
        /// Checks whether an object has the prototype of a class or function in its prototype chain.
        /// </summary>
        /// <param name="obj">The object whose prototype chain to search.</param>
        /// <param name="classOrFunction">A class or function whose prototype to search for in the
        /// prototype chain of <paramref name="obj"/>.</param>
        /// <returns>True if the object has the prototype of a class or function in its prototype
        /// chain, false otherwise.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1040</term>
        /// <description>The <paramref name="classOrFunction"/> argument is not a class or
        /// function.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// <para>If <paramref name="classOrFunction"/> is a Class object that represents an
        /// interface, this method always returns false. This is because the prototype objects of
        /// interfaces are not part of the prototype chain of classes that implement them.</para>
        /// <para>For class-based type checking, use <see cref="AS_isType"/>.</para>
        /// </remarks>
        public static bool AS_instanceof(ASObject obj, ASObject classOrFunction) {
            if (obj == null)
                return false;

            ASObject protoObject;

            if (classOrFunction is ASClass classObj)
                protoObject = classObj.internalClass.prototypeObject;
            else if (classOrFunction is ASFunction func)
                protoObject = func.prototype;
            else
                throw ErrorHelper.createError(ErrorCode.INSTANCEOF_NOT_CLASS_OR_FUNCTION);

            obj = obj.AS_proto;
            while (obj != null) {
                if (protoObject == obj)
                    return true;
                obj = obj.AS_proto;
            }

            return false;
        }

        /// <summary>
        /// Returns a Boolean value indicating whether the given object <paramref name="obj"/> is an
        /// instance of the class <paramref name="typeObj"/>.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="typeObj">The <see cref="ASClass"/> object representing the class.</param>
        /// <returns>True, if <paramref name="obj"/> is an instance of <paramref name="typeObj"/> or
        /// any class derived from it, or if <paramref name="typeObj"/> is an interface, an
        /// implementation of it; false otherwise.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1040</term>
        /// <description>The <paramref name="typeObj"/> argument is not a class.</description>
        /// </item>
        /// </list>
        /// </exception>
        public static bool AS_isType(ASObject obj, ASObject typeObj) {
            if(!(typeObj is ASClass klass))
                throw ErrorHelper.createError(ErrorCode.IS_AS_NOT_CLASS);

            // The is and as operators are special-cased for numeric types: The is
            // operator returns true (and the as operator returns the first operand) if
            // the first operand is of a numeric type and can be round-trip converted to
            // the target numeric type.
            switch (klass.internalClass.tag) {
                case ClassTag.INT:
                    return AS_isInt(obj);
                case ClassTag.UINT:
                    return AS_isUint(obj);
                case ClassTag.NUMBER:
                    return AS_isNumeric(obj);
                default:
                    return obj != null && klass.internalClass.underlyingType.IsInstanceOfType(obj);
            }
        }

        /// <summary>
        /// Returns the object <paramref name="obj"/> if it is an instance of the class
        /// <paramref name="typeObj"/>, otherwise returns null.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="typeObj">The Class object representing the class.</param>
        /// <returns>The object <paramref name="obj"/> is an instance of <paramref name="typeObj"/> or
        /// any class derived from it, or if <paramref name="typeObj"/> is an interface, an
        /// implementation of it; otherwise null is returned.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1040</term>
        /// <description>The <paramref name="typeObj"/> argument is not a class.</description>
        /// </item>
        /// </list>
        /// </exception>
        public static ASObject AS_asType(ASObject obj, ASObject typeObj) => AS_isType(obj, typeObj) ? obj : null;

        /// <summary>
        /// Creates an implementation of the generic class definition <paramref name="def"/> with
        /// the given type paramters and returns the class object of the implementation.
        /// </summary>
        /// <param name="def">The Class object representing the generic class definition.</param>
        /// <param name="typeParams">An array of Class objects representing the type paramters to
        /// substitute into <paramref name="def"/>. Null represents the any (*) type.</param>
        /// <returns>The Class object representing the implementation of the generic class.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1127</term>
        /// <description>The class <paramref name="def"/> is not an uninstantiated generic class
        /// definition.</description>
        /// </item>
        /// <item>
        /// <term>TypeError #1128</term>
        /// <description>The length of the <paramref name="typeParams"/> array does not match the
        /// number of type parameters accepted by the class definition.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// Only the Vector class in the AVM2 is generic. If <paramref name="def"/> is any other
        /// class, an error is thrown.
        /// </remarks>
        public static ASObject AS_applyType(ASObject def, ReadOnlySpan<ASAny> typeParams) {
            if (!(def is ASClass defClass))
                throw ErrorHelper.createError(ErrorCode.MARIANA__APPLYTYPE_NON_CLASS);

            Type underlyingType = defClass.internalClass.underlyingType;

            if (underlyingType != (object)typeof(ASVector<>)) {
                throw ErrorHelper.createError(
                    ErrorCode.NONGENERIC_TYPE_APPLICATION, defClass.internalClass.name.ToString());
            }
            if (typeParams.Length != 1) {
                throw ErrorHelper.createError(
                    ErrorCode.TYPE_ARGUMENT_COUNT_INCORRECT,
                    defClass.internalClass.name.ToString(), 1, typeParams.Length);
            }

            if (typeParams[0] == ASAny.@null)
                return Class.fromType<ASVectorAny>().classObject;

            if (!(typeParams[0].value is ASClass paramClass))
                throw ErrorHelper.createError(ErrorCode.MARIANA__APPLYTYPE_NON_CLASS);

            return paramClass.internalClass.getVectorClass().classObject;
        }

        /// <summary>
        /// Checks if the ActionScript filter operator <c>obj.(<i>expr</i>)</c> can be invoked
        /// on the given object. If it cannot, an error is thrown.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <term>TypeError #1123</term>
        /// <description>The filter operator cannot be invoked on the given object.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// Only XML and XMLList objects can be used with the filter operator.
        /// </remarks>
        public static void AS_checkFilter(ASObject obj) {
            if (obj == null || !ClassTagSet.xmlOrXmlList.contains(obj.AS_class.tag)) {
                throw ErrorHelper.createError(
                    ErrorCode.FILTER_NOT_SUPPORTED, (obj == null) ? "null" : obj.AS_class.name.ToString());
            }
        }

        /// <summary>
        /// Returns a Boolean value indicating whether the given object is of a numeric type (int,
        /// uint or Number).
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>True, if <paramref name="obj"/> is of a numeric type, false
        /// otherwise.</returns>
        public static bool AS_isNumeric(ASObject obj) =>
            obj != null && ClassTagSet.numeric.contains(obj.AS_class.tag);

        /// <summary>
        /// Returns a Boolean value indicating whether the given object is of the integer type
        /// or of another numeric type with a value that is exactly representable as a signed
        /// integer.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>True, if <paramref name="obj"/> is is of the integer type or of another numeric
        /// type with a value that is exactly representable as a signed integer, otherwise
        /// false.</returns>
        public static bool AS_isInt(ASObject obj) {
            if (obj == null)
                return false;

            ClassTag tag = obj.AS_class.tag;
            if (tag == ClassTag.INT)
                return true;

            if (tag == ClassTag.UINT)
                return (int)obj >= 0;

            if (tag == ClassTag.NUMBER) {
                double val = (double)obj;
                return (double)(int)val == val;
            }

            return false;
        }

        /// <summary>
        /// Returns a Boolean value indicating whether the given object is of the unsigned integer type
        /// or of another numeric type with a value that is exactly representable as an unsigned
        /// integer.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>True, if <paramref name="obj"/> is is of the unsigned integer type or of another numeric
        /// type with a value that is exactly representable as an unsigned integer, otherwise
        /// false.</returns>
        public static bool AS_isUint(ASObject obj) {
            if (obj == null)
                return false;

            ClassTag tag = obj.AS_class.tag;
            if (tag == ClassTag.UINT)
                return true;

            if (tag == ClassTag.INT)
                return (int)obj >= 0;

            if (tag == ClassTag.NUMBER) {
                double val = (double)obj;
                return (double)(uint)val == val;
            }

            return false;
        }

        /// <summary>
        /// Returns a Boolean value indicating whether the given object is of a primitive type (int,
        /// uint, Number, String or Boolean).
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>True, if <paramref name="obj"/> is of a primitive type, false
        /// otherwise.</returns>
        public static bool AS_isPrimitive(ASObject obj) =>
            obj != null && ClassTagSet.primitive.contains(obj.AS_class.tag);

        /// <summary>
        /// Returns a Boolean value indicating whether the given object is of the Array or Vector
        /// type.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>True, if <paramref name="obj"/> is of the Array or Vector type, false
        /// otherwise.</returns>
        public static bool AS_isArrayLike(ASObject obj) =>
            obj != null && ClassTagSet.arrayLike.contains(obj.AS_class.tag);

        #endregion

        /// <summary>
        /// Returns a value indicating whether the object has an own property with the given name.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <returns>True, if an own property with the given name was found, false
        /// otherwise.</returns>
        ///
        /// <remarks>
        /// The "own properties" of an object include declared and inherited public traits of its
        /// class and the dynamic properties of the object itself. Properties in the object's
        /// prototype chain are not considered as "own properties".
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual bool hasOwnProperty(ASAny name = default) =>
            AS_hasPropertyObj(name, BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_DYNAMIC);

        /// <summary>
        /// Returns a value indicating whether the object is in the prototype chain of another object.
        /// </summary>
        /// <param name="otherObj">The object whose prototype chain to check for the caller
        /// object.</param>
        /// <returns>True, if the object is in the prototype chain of <paramref name="otherObj"/>,
        /// false otherwise.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual bool isPrototypeOf(ASAny otherObj = default) {
            if (otherObj.value == null)
                return false;

            ASObject p = otherObj.value.AS_proto;
            while (p != null) {
                if (this == p)
                    return true;
                p = p.AS_proto;
            }
            return false;
        }

        /// <summary>
        /// Returns the value of the enumerable flag of a dynamic property.
        /// </summary>
        /// <param name="name">The name of the property for which to return the enumerable flag's
        /// value.</param>
        /// <returns>True if the property with the name <paramref name="name"/> is enumerable,
        /// otherwise false.</returns>
        ///
        /// <remarks>
        /// A dynamic property whose enumerable flag is set to false will be excluded from the
        /// properties searched by for-in loops. If the object does not have dynamic properties, this
        /// method returns false.
        /// </remarks>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public virtual bool propertyIsEnumerable(ASAny name = default) {
            DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
            return dynamicProperties != null && dynamicProperties.isEnumerable(ASAny.AS_convertString(name));
        }

        /// <summary>
        /// Sets the value of the enumerable flag of a dynamic property.
        /// </summary>
        /// <param name="name">The name of the property for which to set the enumerable flag's
        /// value.</param>
        /// <param name="value">True if the property with the name <paramref name="name"/> must be enumerable,
        /// otherwise false.</param>
        ///
        /// <remarks>
        /// A dynamic property whose enumerable flag is set to false will be excluded from the
        /// properties searched by for-in loops. If the object does not have dynamic properties, this
        /// method does nothing.
        /// </remarks>
        //[AVM2ExportTrait(nsName = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public void setPropertyIsEnumerable(string name, bool value) {
            DynamicPropertyCollection dynamicProperties = AS_dynamicProps;
            if (dynamicProperties != null)
                dynamicProperties.setEnumerable(ASString.AS_convertString(name), value);
        }

        /// <summary>
        /// Returns a locale-specific string representation of the object.
        /// </summary>
        /// <returns>A locale-specific string representation of the object.</returns>
        /// <remarks>
        /// This method is intended to return a locale-specific string, but the default implementation
        /// for objects returns the same result as <c>toString()</c>.
        /// </remarks>
        //[AVM2ExportTrait(nsName = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public string toLocaleString() => AS_toString();

        /// <summary>
        /// Returns a string representation of the object.
        /// </summary>
        /// <returns>The string representation of the object.</returns>
        ///
        /// <remarks>
        /// <para>The default implementation returns a string containing the object's class name.
        /// Objects can redefine this method at the class or prototype level to return a string
        /// representation appropriate for the object.</para>
        /// <para>This method is exported to the AVM2 with the name <c>toString</c>, but must be
        /// called from .NET code with the name <c>AS_toString</c> to avoid confusion with the
        /// <see cref="Object.ToString" qualifyHint="true"/> method.</para>
        /// </remarks>
        //[AVM2ExportTrait(name = "toString", nsName = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod(name = "toString")]
        public string AS_toString() => "[object " + AS_class.name.localName + "]";

        /// <summary>
        /// Returns the primitive type representation of the object. The default implementation
        /// returns the object itself, but objects can redefine this method at the class or prototype
        /// level to return a primitive value.
        /// </summary>
        ///
        /// <returns>A primitive value representation of the object.</returns>
        //[AVM2ExportTrait(nsName = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASObject valueOf() => this;

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>True if the specified object is equal to the current object; otherwise
        /// false.</returns>
        /// <remarks>
        /// This method uses the definition of the strict equality operator (===) of AS3 for
        /// comparison. It is equivalent to the static <see cref="AS_strictEq"/> method.
        /// </remarks>
        public sealed override bool Equals(object obj) => AS_strictEq(this, obj as ASObject);

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>True if the specified object is equal to the current object; otherwise
        /// false.</returns>
        /// <remarks>
        /// This method uses the definition of the strict equality operator (===) of AS3 for
        /// comparison. It is equivalent to the static <see cref="AS_strictEq"/> method.
        /// </remarks>
        public bool Equals(ASObject obj) => AS_strictEq(this, obj);

        /// <summary>
        /// Serves as a hash function for an ActionScript 3 object. The hash function is consistent
        /// with the definition of AS3's strict equality (===) operator.
        /// </summary>
        /// <returns>A hash code for this instance that is suitable for use in hashing algorithms and
        /// data structures such as a hash table.</returns>
        public sealed override int GetHashCode() {
            switch (AS_class.tag) {
                case ClassTag.INT:
                case ClassTag.UINT:
                case ClassTag.NUMBER:
                    return AS_coerceNumber().GetHashCode();
                case ClassTag.STRING:
                    return AS_coerceString().GetHashCode();
                case ClassTag.NAMESPACE:
                    return ((ASNamespace)this).uri.GetHashCode();
                case ClassTag.QNAME:
                    return ((ASQName)this).internalGetHashCode();
                default:
                    return base.GetHashCode();
            }
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public sealed override string ToString() => AS_coerceString();

        /// <exclude/>
        ///
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABC-IL compiler to invoke the ActionScript Object class constructor. This must not be
        /// called by outside .NET code. Object objects constructed from .NET code must use the
        /// constructor defined on the <see cref="ASObject"/> type.
        /// </summary>
        internal static ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) {
            if (args.Length == 0 || args[0].value == null)
                return new ASObject();
            return args[0].value;
        }

        /// <exclude/>
        ///
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABCIL compiler to invoke the ActionScript Object class constructor. This must not be
        /// called by outside .NET code. Object objects constructed from .NET code must use the
        /// constructor defined on the <see cref="ASObject"/> type.
        /// </summary>
        internal static ASAny __AS_CONSTRUCT(ReadOnlySpan<ASAny> args) {
            if (args.Length == 0 || args[0].value == null)
                return new ASObject();
            return args[0].value;
        }

    }
}
