using System;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// An actual AVM2 class implementation.
    /// </summary>
    internal abstract class ClassImpl : Class {

        private Type m_underlyingType;

        private ClassImpl m_parent;

        private Class[] m_interfaces;

        private bool m_isDynamic;

        private bool m_canHideInheritedTraits;

        private LazyInitObject<bool> m_lazyClassInit;

        private ASClass m_classObject;

        private ASObject m_prototypeObject;

        private ClassSpecials m_specials;

        private ClassTraitTable m_traitTable;

        private ClassConstructor m_constructor;

        internal ClassImpl(in QName name, ApplicationDomain appDomain, ClassTag tag = ClassTag.OBJECT)
            : base(name, null, appDomain, tag)
        {
            m_interfaces = Array.Empty<ClassImpl>();

            m_lazyClassInit = new LazyInitObject<bool>(
                delegate() {
                    _internalInitClass();
                    return true;
                },
                recursionHandling: LazyInitRecursionHandling.RETURN_DEFAULT
            );
        }

        public sealed override Type underlyingType => m_underlyingType;

        public sealed override bool isDynamic => m_isDynamic;

        public sealed override Class parent => m_parent;

        internal sealed override ClassImpl getClassImpl() => this;

        public override bool isVectorInstantiation => false;

        public override Class vectorElementType => null;

        protected private override Class createVectorClass() {
            Type vecType = typeof(ASVector<>).MakeGenericType(getUnderlyingOrPrimitiveType(this));
            Class klass = ClassTypeMap.getClass(vecType);

            if (klass == null)
                // This will add the created class to ClassTypeMap, but we don't have to
                // protect against race conditions as this method is always called from a
                // lazy initializer (which is thread safe), and there are no other code paths
                // that call NativeClass.createClass with an ASVector<T> instantiation.
                klass = NativeClass.createClass(vecType, ApplicationDomain.systemDomain);

            return klass;
        }

        internal sealed override ClassSpecials classSpecials {
            get {
                _ = m_lazyClassInit.value;
                return m_specials;
            }
        }

        public sealed override ClassConstructor constructor {
            get {
                _ = m_lazyClassInit.value;
                return m_constructor;
            }
        }

        public sealed override ASObject prototypeObject {
            get {
                _ = m_lazyClassInit.value;
                return m_prototypeObject;
            }
        }

        public sealed override ASClass classObject {
            get {
                _ = m_lazyClassInit.value;
                return m_classObject;
            }
        }

        public sealed override bool canAssignTo(Class klass) {
            if (klass == null || klass == this)
                return true;

            Type underlyingType = this.underlyingType;
            Type otherUnderlyingType = klass.underlyingType;

            if (underlyingType != null && otherUnderlyingType != null)
                return otherUnderlyingType.IsAssignableFrom(underlyingType);

            if (klass.isInterface) {
                ReadOnlySpan<Class> ifaces = getImplementedInterfaces().asSpan();
                for (int i = 0; i < ifaces.Length; i++) {
                    if (ifaces[i] == klass)
                        return true;
                }
            }
            else {
                for (Class p = this; p != null; p = p.parent) {
                    if (p == klass)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Sets the underlying type of this class.
        /// </summary>
        /// <param name="underlyingType">The underlying type of this class.</param>
        /// <remarks>
        /// When setting the underlying type of a class, ensure that <see cref="ClassTypeMap"/> is also
        /// updated.
        /// </remarks>
        protected private void setUnderlyingType(Type underlyingType) {
            m_underlyingType = underlyingType;
        }

        /// <summary>
        /// Sets the parent class of this class. Do not set this for interfaces!
        /// </summary>
        /// <param name="parent">The parent class of this class.</param>
        protected private void setParent(Class parent) {
            m_parent = parent.getClassImpl();
        }

        /// <summary>
        /// Sets the interfaces implemented by this class or interface.
        /// </summary>
        /// <param name="interfaces">An array of <see cref="Class"/> instances representing the
        /// interfaces implemented by this class or interface.</param>
        protected private void setInterfaces(Class[] interfaces) {
            bool mayHaveAlias = false;

            for (int i = 0; i < interfaces.Length && !mayHaveAlias; i++)
                mayHaveAlias = !(interfaces[i] is ClassImpl);

            if (!mayHaveAlias) {
                m_interfaces = interfaces;
                return;
            }

            Class[] classImpls = new Class[interfaces.Length];
            for (int i = 0; i < interfaces.Length; i++)
                classImpls[i] = interfaces[i].getClassImpl();

            m_interfaces = classImpls;
        }

        /// <summary>
        /// Sets whether this class is dynamic or not.
        /// </summary>
        /// <param name="isDynamic">Set to true if this class is dynamic, false otherwise.</param>
        protected private void setIsDynamic(bool isDynamic) {
            m_isDynamic = isDynamic;
        }

        /// <summary>
        /// Sets the constructor of this class.
        /// </summary>
        /// <param name="ctor">The constructor of this class.</param>
        protected private void setConstructor(ClassConstructor ctor) {
            m_constructor = ctor;
        }

        /// <summary>
        /// Sets the prototype object of this class.
        /// </summary>
        /// <param name="prototype">The prototype object of this class.</param>
        protected private void setPrototype(ASObject prototype) {
            m_prototypeObject = prototype;
        }

        /// <summary>
        /// Sets the <see cref="ClassSpecials"/> instance containing any special methods defined
        /// by this class.
        /// </summary>
        /// <param name="specials">A <see cref="ClassSpecials"/> instance.</param>
        protected private void setClassSpecials(ClassSpecials specials) {
            m_specials = specials;
        }

        /// <summary>
        /// Sets whether this class's declared instance traits can hide inherited traits having the
        /// same name (without overriding).
        /// </summary>
        /// <param name="isHidingAllowed">Set to true to allow hiding.</param>
        ///
        /// <remarks>
        /// Hiding is disabled by default. To allow hiding, this method must be called (with
        /// <paramref name="isHidingAllowed"/> set to true) before the the call to
        /// <see cref="initClass"/> finishes. (This is when the merging of inherited traits is
        /// done.)
        /// </remarks>
        protected private void setIsHidingAllowed(bool isHidingAllowed) {
            m_canHideInheritedTraits = isHidingAllowed;
        }

        /// <summary>
        /// A method that must be implemented by derived classes. This can be used for lazy
        /// initialisation (e.g. of class traits).
        /// </summary>
        ///
        /// <remarks>
        /// Overriders of this method should not be concerned about synchronisation or thread safety,
        /// or the initialisation of the class's parent or interfaces. All of this is handled by the
        /// initialisation code that calls this method.
        /// </remarks>
        protected private abstract void initClass();

        /// <summary>
        /// Adds a declared trait to this class definition.
        /// </summary>
        /// <param name="trait">The trait to add.</param>
        /// <returns>True if the trait was added, false if it could not (because of a name
        /// conflict).</returns>
        /// <remarks>
        /// Do not add inherited traits with this method. They are added automatically.
        /// </remarks>
        protected private bool tryDefineTrait(Trait trait) {
            if (m_traitTable == null)
                m_traitTable = new ClassTraitTable(this);
            return m_traitTable.tryAddTrait(trait, allowMergeProperties: false);
        }

        /// <summary>
        /// This method is called internally to define and close the class. It calls the virtual
        /// <see cref="initClass"/> method. This method also closes the class's trait table by
        /// copying parent traits.
        /// </summary>
        private void _internalInitClass() {

            // Ensure that this method is called on the parent and interfaces first.
            ClassImpl parent = m_parent;
            if (parent != null)
                _ = parent.m_lazyClassInit.value;

            var interfaces = m_interfaces;

            for (int i = 0; i < interfaces.Length; i++)
                _ = ((ClassImpl)interfaces[i]).m_lazyClassInit.value;

            // Create the class object and prototype object, if it has not been created already.
            if (m_classObject == null)
                m_classObject = new ASClass(this);

            if (m_prototypeObject == null) {
                m_prototypeObject = new ASObject();
                m_prototypeObject.AS_dynamicProps["constructor"] = m_classObject;
            }

            if (parent != null) {
                // Set the next object in the prototype chain of the class's prototype object to be that
                // of the parent class.
                m_prototypeObject.AS_proto = parent.prototypeObject;
            }
            else if (m_underlyingType == typeof(ASObject)) {
                // Special case: The protoype of Object does not have any protoype
                // chain.
                m_prototypeObject.AS_proto = null;
            }

            if (m_traitTable == null)
                m_traitTable = new ClassTraitTable(this, false);

            initClass();

            if (isInterface) {
                for (int i = 0; i < interfaces.Length; i++)
                    m_traitTable.mergeWithParentInterface(((ClassImpl)interfaces[i]).m_traitTable);
            }
            else if (m_parent != null) {
                m_traitTable.mergeWithParentClass(m_parent.m_traitTable, m_canHideInheritedTraits);
            }

            m_traitTable.seal();

            if (m_parent != null)
                m_specials = ClassSpecials.mergeWithParent(m_specials, m_parent.m_specials);

        }

        public sealed override ReadOnlyArrayView<Class> getImplementedInterfaces() => new ReadOnlyArrayView<Class>(m_interfaces);

        public sealed override Trait getTrait(in QName name) {
            _ = m_lazyClassInit.value;

            BindStatus bindStatus = m_traitTable.tryGetTrait(name, false, out Trait trait);

            if (bindStatus == BindStatus.NOT_FOUND)
                bindStatus = m_traitTable.tryGetTrait(name, true, out trait);
            if (trait == null)
                return null;
            if (bindStatus == BindStatus.AMBIGUOUS)
                throw ErrorHelper.createBindingError(this.name, name, bindStatus);

            return trait;
        }

        public sealed override Trait getTrait(in QName name, TraitType kinds, TraitScope scopes) {
            _ = m_lazyClassInit.value;

            Trait value = null;
            BindStatus bindStatus = BindStatus.NOT_FOUND;

            if ((scopes & TraitScope.INSTANCE) != 0)
                bindStatus = m_traitTable.tryGetTrait(name, false, out value);

            if ((scopes & TraitScope.STATIC) != 0 && bindStatus == BindStatus.NOT_FOUND)
                bindStatus = m_traitTable.tryGetTrait(name, true, out value);

            if (value == null)
                return null;

            if (bindStatus == BindStatus.AMBIGUOUS)
                throw ErrorHelper.createBindingError(this.name, name, bindStatus);

            if ((value.traitType & kinds) == 0
                || ((scopes & TraitScope.INSTANCE_INHERITED) == 0 && value.declaringClass != this))
            {
                return null;
            }
            return value;
        }

        public sealed override bool hasTrait(in QName name, TraitScope scopes) {
            _ = m_lazyClassInit.value;

            Trait trait = null;
            BindStatus bindStatus = BindStatus.NOT_FOUND;

            if ((scopes & TraitScope.INSTANCE) != 0)
                bindStatus = m_traitTable.tryGetTrait(name, false, out trait);

            if (bindStatus == BindStatus.SUCCESS
                && ((scopes & TraitScope.INSTANCE_INHERITED) != 0 || trait.declaringClass == this))
            {
                return true;
            }

            if (bindStatus == BindStatus.NOT_FOUND && (scopes & TraitScope.STATIC) != 0)
                bindStatus = m_traitTable.tryGetTrait(name, true, out _);

            return bindStatus == BindStatus.SUCCESS;
        }

        public sealed override Trait getTraitByFilter(Predicate<Trait> filter) {
            if (filter == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(filter));

            _ = m_lazyClassInit.value;
            return m_traitTable.getTraitByFilter(filter);
        }

        public sealed override ReadOnlyArrayView<Trait> getTraits() {
            _ = m_lazyClassInit.value;
            return m_traitTable.getTraits();
        }

        public sealed override ReadOnlyArrayView<Trait> getTraits(TraitType types, TraitScope scopes) {
            if ((scopes & TraitScope.ALL) == 0 || (types & TraitType.ALL) == 0)
                return ReadOnlyArrayView<Trait>.empty;

            _ = m_lazyClassInit.value;

            if ((types & TraitType.ALL) == TraitType.ALL) {
                return m_traitTable.getTraits(scopes);
            }

            DynamicArray<Trait> traitList = new DynamicArray<Trait>();
            m_traitTable.getTraits(types, scopes, ref traitList);
            return new ReadOnlyArrayView<Trait>(traitList.toArray());
        }

        public sealed override ReadOnlyArrayView<FieldTrait> getFields(TraitScope scopes) {
            _ = m_lazyClassInit.value;
            DynamicArray<FieldTrait> traitList = new DynamicArray<FieldTrait>();
            m_traitTable.getTraits(TraitType.FIELD, scopes, ref traitList);
            return new ReadOnlyArrayView<FieldTrait>(traitList.toArray());
        }

        public sealed override ReadOnlyArrayView<MethodTrait> getMethods(TraitScope scopes) {
            _ = m_lazyClassInit.value;
            DynamicArray<MethodTrait> traitList = new DynamicArray<MethodTrait>();
            m_traitTable.getTraits(TraitType.METHOD, scopes, ref traitList);
            return new ReadOnlyArrayView<MethodTrait>(traitList.toArray());
        }

        public sealed override ReadOnlyArrayView<PropertyTrait> getProperties(TraitScope scopes) {
            _ = m_lazyClassInit.value;
            DynamicArray<PropertyTrait> traitList = new DynamicArray<PropertyTrait>();
            m_traitTable.getTraits(TraitType.PROPERTY, scopes, ref traitList);
            return new ReadOnlyArrayView<PropertyTrait>(traitList.toArray());
        }

        public sealed override ReadOnlyArrayView<ConstantTrait> getConstants() {
            _ = m_lazyClassInit.value;
            DynamicArray<ConstantTrait> traitList = new DynamicArray<ConstantTrait>();
            m_traitTable.getTraits(TraitType.CONSTANT, TraitScope.STATIC, ref traitList);
            return new ReadOnlyArrayView<ConstantTrait>(traitList.toArray());
        }

        public sealed override BindStatus lookupTrait(in QName name, bool isStatic, out Trait trait) {
            _ = m_lazyClassInit.value;
            return m_traitTable.tryGetTrait(name, isStatic, out trait);
        }

        public sealed override BindStatus lookupTrait(string name, in NamespaceSet nsSet, bool isStatic, out Trait trait) {
            _ = m_lazyClassInit.value;
            return m_traitTable.tryGetTrait(name, nsSet, isStatic, out trait);
        }

        public sealed override ReadOnlyArrayView<Trait> getTraitsByFilter(Predicate<Trait> filter) {
            if (filter == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(filter));

            _ = m_lazyClassInit.value;
            DynamicArray<Trait> filteredList = new DynamicArray<Trait>();
            m_traitTable.getTraitsByFilter(filter, ref filteredList);
            return new ReadOnlyArrayView<Trait>(filteredList.toArray());
        }

        public sealed override BindStatus tryInvoke(ASAny target, ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) {
            if (m_specials?.specialInvokeDelegate != null) {
                result = m_specials.specialInvokeDelegate(args);
                return BindStatus.SUCCESS;
            }

            if (args.Length != 1)
                throw ErrorHelper.createError(ErrorCode.CLASS_COERCE_ARG_COUNT_MISMATCH, args.Length);

            ASAny obj = args[0];
            if (obj.isDefined && (obj.value == null || underlyingType.IsInstanceOfType(obj.value))) {
                result = obj;
                return BindStatus.SUCCESS;
            }
            throw ErrorHelper.createCastError(obj, this);
        }

        public sealed override BindStatus tryConstruct(ASAny target, ReadOnlySpan<ASAny> args, out ASAny result) {
            if (m_specials?.specialConstructDelegate != null) {
                result = m_specials.specialConstructDelegate(args);
                return BindStatus.SUCCESS;
            }

            ClassConstructor constructor = this.constructor;
            if (constructor == null)
                throw ErrorHelper.createError(ErrorCode.CLASS_CANNOT_BE_INSTANTIATED, name.ToString());

            result = constructor.invoke(args);
            return BindStatus.SUCCESS;
        }

    }
}
