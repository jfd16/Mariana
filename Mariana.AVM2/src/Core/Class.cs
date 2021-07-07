using System;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Represents an AVM2 class.
    /// </summary>
    public abstract class Class : Trait {

        private ClassTag m_tag;

        private LazyInitObject<Class> m_lazyVectorClass;

        internal Class(in QName name, Class? declClass, ApplicationDomain appDomain, ClassTag tag = ClassTag.OBJECT)
            : base(name, declClass, appDomain, isStatic: true)
        {
            m_tag = tag;
            m_lazyVectorClass = new LazyInitObject<Class>(createVectorClass);
        }

        /// <summary>
        /// Gets the underlying .NET type of a class, returning a primitive type if available.
        /// </summary>
        /// <param name="klass">The class for which to return the underlying .NET type.</param>
        ///
        /// <returns>
        /// The underlying .NET type of class <paramref name="klass"/>. This value is usually the same
        /// as the <see cref="underlyingType"/> property of <paramref name="klass"/>, except for the
        /// <c>int</c>, <c>uint</c>, <c>Number</c> <c>Boolean</c> and
        /// <c>String</c> classes, which return their primitive type equivalents for example, the
        /// <see cref="Int32"/> type in case of the <c>int</c> class). If
        /// <paramref name="klass"/> is null, the <see cref="ASAny"/> type is returned.
        /// </returns>
        public static Type getUnderlyingOrPrimitiveType(Class? klass) {
            if (klass == null)
                return typeof(ASAny);

            return klass.tag switch {
                ClassTag.INT => typeof(int),
                ClassTag.UINT => typeof(uint),
                ClassTag.NUMBER => typeof(double),
                ClassTag.BOOLEAN => typeof(bool),
                ClassTag.STRING => typeof(string),
                _ => klass.underlyingType,
            };
        }

        /// <summary>
        /// Gets the <see cref="Class"/> instance for the class having the specified .NET type as
        /// the underlying type.
        /// </summary>
        ///
        /// <param name="type">The underlying type for which to obtain the <see cref="Class"/>
        /// instance.</param>
        /// <param name="throwIfNotExists">If this is true, an error is thrown if no class with the
        /// given underlying type is available.</param>
        ///
        /// <returns>The class whose underlying type is the specified type, or null if there is no such
        /// class (and <paramref name="throwIfNotExists"/> is false). If <paramref name="type"/>
        /// is the type <see cref="ASAny"/>, this method always returns null even if
        /// <paramref name="throwIfNotExists"/> is true.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>Error #10008: No class with the given underlying type exists, and
        /// <paramref name="throwIfNotExists"/> is true.</description>
        /// </item>
        /// </list>
        /// </exception>
        public static Class? fromType(Type type, bool throwIfNotExists = false) {
            if (type == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(type));

            if (type == typeof(ASAny))
                return null;

            CoreClasses.ensureClassesLoaded();

            Class? klass = ClassTypeMap.getClass(type);
            if (klass != null)
                return klass;

            if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(ASVector<>)) {
                Class? elementClass = fromType(type.GetGenericArguments()[0]);
                if (elementClass == null)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_VECTOR_ANY);

                klass = elementClass.getVectorClass();
            }

            if (klass == null && throwIfNotExists)
                throw ErrorHelper.createError(ErrorCode.MARIANA__NO_CLASS_WITH_UNDERLYING_TYPE, ReflectUtil.getFullTypeName(type));

            return klass;
        }

        /// <summary>
        /// Gets the <see cref="Class"/> instance for the class having the specified .NET type as
        /// the underlying type.
        /// </summary>
        ///
        /// <param name="throwIfNotExists">If this is true, an error is thrown if no class with the
        /// given underlying type is available.</param>
        /// <typeparam name="T">The underlying type for which to obtain the <see cref="Class"/>
        /// instance.</typeparam>
        ///
        /// <returns>The class whose underlying type is the specified type, or null if there is no such
        /// class (and <paramref name="throwIfNotExists"/> is false). If <typeparamref name="T"/>
        /// is the type <see cref="ASAny"/>, this method always returns null even if
        /// <paramref name="throwIfNotExists"/> is true.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>Error #10008: No class with the given underlying type exists, and
        /// <paramref name="throwIfNotExists"/> is true.</description>
        /// </item>
        /// </list>
        /// </exception>
        public static Class? fromType<T>(bool throwIfNotExists = false) => fromType(typeof(T), throwIfNotExists);

        /// <summary>
        /// Returns the <see cref="ClassImpl"/> that represents this class.
        /// </summary>
        /// <returns>The <see cref="ClassImpl"/> that represents this class.</returns>
        internal abstract ClassImpl getClassImpl();

        /// <inheritdoc/>
        public sealed override TraitType traitType => TraitType.CLASS;

        /// <summary>
        /// Gets the underlying .NET type of the class.
        /// </summary>
        public virtual Type underlyingType => getClassImpl().underlyingType;

        /// <summary>
        /// Returns a value indicating whether this class uses the given .NET type as its
        /// underlying type.
        /// </summary>
        /// <param name="type">A <see cref="Type"/> instance.</param>
        /// <returns>True if the underlying type of this class is <paramref name="type"/>, otherwise
        /// false.</returns>
        public virtual bool hasUnderlyingType(Type type) => getClassImpl().hasUnderlyingType(type);

        /// <summary>
        /// Gets a Boolean value indicating whether the class is an interface.
        /// </summary>
        public virtual bool isInterface => underlyingType.IsInterface;

        /// <summary>
        /// Gets a Boolean value indicating whether the class is final.
        /// </summary>
        public virtual bool isFinal => underlyingType.IsSealed;

        /// <summary>
        /// Gets a Boolean value indicating whether the class is dynamic (that is, whether instances
        /// can have dynamic properties).
        /// </summary>
        /// <remarks>
        /// The dynamic-ness of a class is not inherited. It is possible for a non-dynamic class to derive
        /// from a dynamic base class, and vice versa.
        /// </remarks>
        public virtual bool isDynamic => getClassImpl().isDynamic;

        /// <summary>
        /// Gets a value indicating whether the class is an instantiation of the Vector class.
        /// </summary>
        public virtual bool isVectorInstantiation => getClassImpl().isVectorInstantiation;

        /// <summary>
        /// Gets the base class of the class.
        /// </summary>
        public virtual Class? parent => getClassImpl().parent;

        /// <summary>
        /// Gets a value indicating whether this <see cref="Class"/> instance represents the
        /// Object class, which is the root of the class hierarchy in the AVM2 type system.
        /// </summary>
        public virtual bool isObjectClass => getClassImpl().isObjectClass;

        /// <summary>
        /// Gets a value indicating whether this <see cref="Class"/> instance represents a
        /// primitive type.
        /// </summary>
        public bool isPrimitiveClass => ClassTagSet.primitive.contains(tag);

        /// <summary>
        /// Gets the <see cref="ClassTag"/> value for this class.
        /// </summary>
        public ClassTag tag => m_tag;

        /// <summary>
        /// Gets the <see cref="ClassConstructor"/> object representing the class's instance
        /// constructor.
        /// </summary>
        ///
        /// <remarks>
        /// The value of this property is null for certain core classes whose construction is
        /// intrinsic to the AVM2. These classes include primitive classes such as <c>int</c> and
        /// <c>String</c>. These classes, however, can still be constructed by calling methods
        /// such as <see cref="tryConstruct"/> on the respective <see cref="Class"/> instance.
        /// </remarks>
        public virtual ClassConstructor? constructor => getClassImpl().constructor;

        /// <summary>
        /// Gets the prototype object for this class.
        /// </summary>
        /// <remarks>
        /// The prototype object for a class in the AVM2 is zone-local, so a class has one prototype
        /// object for each <see cref="StaticZone"/> created (this property returns the object associated
        /// with the current zone), in addition to a global prototype object that is returned by this
        /// property when not executing inside a <see cref="StaticZone"/>.
        /// </remarks>
        public virtual ASObject prototypeObject => getClassImpl().prototypeObject;

        /// <summary>
        /// Gets the ActionScript 3 <c>Class</c> object for this class.
        /// </summary>
        /// <remarks>
        /// The class object for a class in the AVM2 is zone-local, so a class has one class
        /// object for each <see cref="StaticZone"/> created (this property returns the object associated
        /// with the current zone), in addition to a global class object that is returned by this property
        /// when not executing inside a <see cref="StaticZone"/>.
        /// </remarks>
        public virtual ASClass classObject => getClassImpl().classObject;

        /// <summary>
        /// Gets the element type, if this <see cref="Class"/> object represents an instantiation of
        /// the AS3 Vector class. For any other class, the value of this property is null.
        /// </summary>
        public virtual Class? vectorElementType => getClassImpl().vectorElementType;

        /// <summary>
        /// Gets a <see cref="ClassSpecials"/> instance containing certain special properties
        /// of the class used internally by the AVM2 runtime (such as for array indexing).
        /// For classes without special internal properties, returns null.
        /// </summary>
        internal virtual ClassSpecials? classSpecials => getClassImpl().classSpecials;

        /// <summary>
        /// Gets an array containing the interfaces implemented by a class or extended by an
        /// interface.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{Class}"/> containing the interfaces implemented by
        /// this class, including interfaces transitively implemented or implemented by a base class. If this
        /// class is an interface, returns the interfaces that this interface extends, including those
        /// extended transitively.</returns>
        public virtual ReadOnlyArrayView<Class> getImplementedInterfaces() => getClassImpl().getImplementedInterfaces();

        /// <summary>
        /// Gets a Boolean value indicating whether an instance of this class can be assigned to a
        /// variable whose type is <paramref name="klass"/> with only a boxing conversion, or a
        /// reference conversion that will always succeed.
        /// </summary>
        ///
        /// <param name="klass">The type of the variable that an instance of this class is to be
        /// assigned to. If this is null, this method always returns true as null represents the "any"
        /// type which can be assigned from any other type.</param>
        ///
        /// <returns>True if an instance of this class can be assigned to a variable of type
        /// <paramref name="klass"/>, otherwise false.</returns>
        public virtual bool canAssignTo(Class? klass) => getClassImpl().canAssignTo(klass);

        /// <summary>
        /// Gets a Boolean value indicating whether the given object is an instance of this class.
        /// </summary>
        /// <param name="obj">An object.</param>
        ///
        /// <returns>True if <paramref name="obj"/> is not null and it is an instance of this
        /// class (or a subclass of it). If this class is an interface, returns true if the
        /// class of <paramref name="obj"/> implements the interface.</returns>
        public virtual bool isInstance(ASObject? obj) => getClassImpl().isInstance(obj);

        /// <summary>
        /// Returns the <see cref="Class"/> object representing the instantiation of the Vector
        /// class with this class as the element type.
        /// </summary>
        /// <returns>The <see cref="Class"/> object representing the instantiation of the Vector
        /// class with this element type.</returns>
        public Class getVectorClass() => m_lazyVectorClass.value;

        /// <summary>
        /// Returns the <see cref="Class"/> object representing the instantiation of the Vector
        /// class with this class as the element type.
        /// </summary>
        /// <returns>The <see cref="Class"/> object representing the instantiation of the Vector
        /// class with this element type.</returns>
        /// <remarks>
        /// This method is always called from a thread-safe lazy initializer, so overriders
        /// need not be concerned about thread safety.
        /// </remarks>
        protected private virtual Class createVectorClass() => getClassImpl().createVectorClass();

        /// <summary>
        /// Gets the <see cref="Trait"/> object representing the trait in the class with the given
        /// name.
        /// </summary>
        /// <param name="name">The name of the trait.</param>
        /// <returns>An <see cref="Trait"/> object representing the trait with the given name. If no
        /// trait exists, returns null.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1008: If the namespace of <paramref name="name"/> is the any namespace, and more
        /// than one trait with the local name of <paramref name="name"/> is found. (Ambiguous
        /// match)</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method searches the instance traits first, and then the static traits. So it both an
        /// instance trait and a static trait have the same name, the instance trait will be returned.
        /// </remarks>
        public virtual Trait? getTrait(in QName name) => getClassImpl().getTrait(name);

        /// <summary>
        /// Gets the <see cref="Trait"/> object representing the trait in the class with the given
        /// name, which must be one of the specified kinds and in one of the specified scopes.
        /// </summary>
        ///
        /// <param name="name">The name of the trait.</param>
        /// <param name="kinds"> A set of bit flags from the <see cref="TraitType"/> enumeration
        /// indicating the kind(s) of the traits to be returned.</param>
        /// <param name="scopes">A set of bit flags from the <see cref="TraitScope"/> enumeration
        /// indicating the scope(s) of the traits to be returned.</param>
        ///
        /// <returns>An <see cref="Trait"/> object representing the trait with the given name that
        /// is one of the kinds specified in <paramref name="kinds"/> and in a scope specified in
        /// <paramref name="scopes"/>. If no such trait exists, returns null.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1008: If the namespace of <paramref name="name"/> is the any namespace, and more
        /// than one trait with a matching name is found. (Ambiguous match)</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If <paramref name="scopes"/> has both the INSTANCE and STATIC fields set, and an instance
        /// trait and a static trait have the same name, the instance trait will be returned.
        /// </remarks>
        public virtual Trait? getTrait(in QName name, TraitType kinds, TraitScope scopes = TraitScope.ALL)
            => getClassImpl().getTrait(name, kinds, scopes);

        /// <summary>
        /// Gets the <see cref="FieldTrait"/> object representing the field in the class with the
        /// given name.
        /// </summary>
        /// <param name="name">The name of the field.</param>
        /// <returns>A <see cref="FieldTrait"/> object representing the field with the given name.
        /// If no trait exists or if the trait with the given name is not a field, returns
        /// null.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1008: If the namespace of <paramref name="name"/> is the any namespace, and more
        /// than one trait with the local name of <paramref name="name"/> is found. (Ambiguous
        /// match)</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method searches the instance traits first, and then the static traits. So if an
        /// instance trait and a static trait have the same name, the instance trait will be returned.
        /// </remarks>
        public FieldTrait? getField(in QName name) => getTrait(name) as FieldTrait;

        /// <summary>
        /// Gets the <see cref="FieldTrait"/> object representing the field in the class with the
        /// given name in one of the specified scopes.
        /// </summary>
        /// <param name="name">The name of the field.</param>
        /// <param name="scopes">The scopes in which to search for the trait, as a bit-field set from
        /// the <see cref="TraitScope"/> enumeration.</param>
        /// <returns>A <see cref="FieldTrait"/> object representing the field with the given name.
        /// If no trait exists or if the trait with the given name is not a field, returns
        /// null.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1008: If the namespace of <paramref name="name"/> is the any namespace, and more
        /// than one trait with the local name of <paramref name="name"/> is found. (Ambiguous
        /// match)</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method searches the instance traits first, and then the static traits. So if an
        /// instance trait and a static trait have the same name and <paramref name="scopes"/>
        /// contains both instance and static scopes, the instance trait will be returned.
        /// </remarks>
        public FieldTrait? getField(in QName name, TraitScope scopes) => getTrait(name, TraitType.FIELD, scopes) as FieldTrait;

        /// <summary>
        /// Gets the <see cref="MethodTrait"/> object representing the method in the class with the
        /// given name.
        /// </summary>
        /// <param name="name">The name of the method.</param>
        /// <returns>A <see cref="MethodTrait"/> object representing the method with the given name.
        /// If no trait exists or if the trait with the given name is not a method, returns
        /// null.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1008: If the namespace of <paramref name="name"/> is the any namespace, and more
        /// than one trait with the local name of <paramref name="name"/> is found. (Ambiguous
        /// match)</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method searches the instance traits first, and then the static traits. So if an
        /// instance trait and a static trait have the same name, the instance trait will be returned.
        /// </remarks>
        public MethodTrait? getMethod(in QName name) => getTrait(name) as MethodTrait;

        /// <summary>
        /// Gets the <see cref="MethodTrait"/> object representing the method in the class with the
        /// given name in one of the specified scopes.
        /// </summary>
        /// <param name="name">The name of the method.</param>
        /// <param name="scopes">The scopes in which to search for the trait, as a bit-field set from
        /// the <see cref="TraitScope"/> enumeration.</param>
        /// <returns>A <see cref="MethodTrait"/> object representing the method with the given name.
        /// If no trait exists or if the trait with the given name is not a method, returns
        /// null.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1008: If the namespace of <paramref name="name"/> is the any namespace, and more
        /// than one trait with the local name of <paramref name="name"/> is found. (Ambiguous
        /// match)</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method searches the instance traits first, and then the static traits. So if an
        /// instance trait and a static trait have the same name and <paramref name="scopes"/>
        /// contains both instance and static scopes, the instance trait will be returned.
        /// </remarks>
        public MethodTrait? getMethod(in QName name, TraitScope scopes) => getTrait(name, TraitType.METHOD, scopes) as MethodTrait;

        /// <summary>
        /// Gets the <see cref="PropertyTrait"/> object representing the property in the class with
        /// the given name.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <returns>A <see cref="MethodTrait"/> object representing the property with the given
        /// name. If no trait exists or if the trait with the given name is not a property, returns
        /// null.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1008: If the namespace of <paramref name="name"/> is the any namespace, and more
        /// than one trait with the local name of <paramref name="name"/> is found. (Ambiguous
        /// match)</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method searches the instance traits first, and then the static traits. So if an
        /// instance trait and a static trait have the same name, the instance trait will be returned.
        /// </remarks>
        public PropertyTrait? getProperty(in QName name) => getTrait(name) as PropertyTrait;

        /// <summary>
        /// Gets the <see cref="PropertyTrait"/> object representing the property in the class with
        /// the given name.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="scopes">The scopes in which to search for the trait, as a bit-field set from
        /// the <see cref="TraitScope"/> enumeration.</param>
        /// <returns>An <see cref="PropertyTrait"/> object representing the property with the given
        /// name. If no trait exists or if the trait with the given name is not a property, returns
        /// null.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1008: If the namespace of <paramref name="name"/> is the any namespace, and more
        /// than one trait with the local name of <paramref name="name"/> is found. (Ambiguous
        /// match)</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method searches the instance traits first, and then the static traits. So if an
        /// instance trait and a static trait have the same name and <paramref name="scopes"/>
        /// contains both instance and static scopes, the instance trait will be returned.
        /// </remarks>
        public PropertyTrait? getProperty(in QName name, TraitScope scopes) => getTrait(name, TraitType.PROPERTY, scopes) as PropertyTrait;

        /// <summary>
        /// Gets the <see cref="ConstantTrait"/> object representing the constant in the class with
        /// the given name.
        /// </summary>
        /// <param name="name">The name of the class.</param>
        /// <returns>A <see cref="ConstantTrait"/> object representing the class with the given
        /// name. If no trait exists or if the trait with the given name is not a constant, returns
        /// null.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1008: If the namespace of <paramref name="name"/> is the any namespace, and more
        /// than one trait with the local name of <paramref name="name"/> is found. (Ambiguous
        /// match)</description>
        /// </item>
        /// </list>
        /// </exception>
        public ConstantTrait? getConstant(in QName name) =>
            getTrait(name, TraitType.CONSTANT, TraitScope.STATIC) as ConstantTrait;

        /// <summary>
        /// Returns a Boolean value indicating whether a trait with the given name exists in the
        /// class in one of the specified scopes.
        /// </summary>
        /// <param name="name">The name of the trait.</param>
        /// <param name="scopes">The scopes in which to search for the trait, as a bit-field set from
        /// the <see cref="TraitScope"/> enumeration.</param>
        /// <returns>True, if a trait with the given name exists, false otherwise.</returns>
        public virtual bool hasTrait(in QName name, TraitScope scopes = TraitScope.ALL) => getClassImpl().hasTrait(name, scopes);

        /// <summary>
        /// Gets the <see cref="Trait"/> object for the trait for which the given filter
        /// function returns true.
        /// </summary>
        /// <param name="filter">The filter function. Each trait is passed to the function, and if the
        /// function returns true for any trait, that trait is returned.</param>
        /// <returns>The trait for which the filter function returns true, of null if there is no such
        /// trait.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>ArgumentError #10060: <paramref name="filter"/> is null.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// If there is more than one trait for which the filter function returns true, any one of
        /// them is returned (which one is unspecified). If no trait passed to the filter function
        /// returns true, this method returns null.
        /// </remarks>
        public virtual Trait? getTraitByFilter(Predicate<Trait> filter) => getClassImpl().getTraitByFilter(filter);

        /// <summary>
        /// Gets all the traits in the class.
        /// </summary>
        /// <returns>An array containing all the traits in the class.</returns>
        public virtual ReadOnlyArrayView<Trait> getTraits() => getClassImpl().getTraits();

        /// <summary>
        /// Gets all the traits in the class which are of the specified kinds and are in the specified scopes.
        /// </summary>
        ///
        /// <param name="types">
        /// A set of bit flags of the <see cref="TraitType"/> enumeration indicating the kinds of
        /// the traits to be returned.
        /// </param>
        /// <param name="scopes">The scopes in which to search for the traits, as a bit-field set from
        /// the <see cref="TraitScope"/> enumeration.</param>
        ///
        /// <returns>A <see cref="ReadOnlyArrayView{Trait}"/> instance containing all the traits
        /// in the class of the specified kinds and in the specified scopes.</returns>
        public virtual ReadOnlyArrayView<Trait> getTraits(TraitType types, TraitScope scopes = TraitScope.ALL)
            => getClassImpl().getTraits(types, scopes);

        /// <summary>
        /// Gets all the field traits in the class in the specified scopes.
        /// </summary>
        /// <param name="scopes">The scopes in which to search for the traits, as a bit-field set from
        /// the <see cref="TraitScope"/> enumeration.</param>
        /// <returns>A <see cref="ReadOnlyArrayView{FieldTrait}"/> instance containing the field traits
        /// in the class in the specified scopes.</returns>
        public virtual ReadOnlyArrayView<FieldTrait> getFields(TraitScope scopes = TraitScope.ALL)
            => getClassImpl().getFields(scopes);

        /// <summary>
        /// Gets all the method traits in the class in the specified scopes.
        /// </summary>
        /// <param name="scopes">The scopes in which to search for the traits, as a bit-field set from
        /// the <see cref="TraitScope"/> enumeration.</param>
        /// <returns>A <see cref="ReadOnlyArrayView{MethodTrait}"/> instance containing the method traits
        /// in the class in the specified scopes.</returns>
        public virtual ReadOnlyArrayView<MethodTrait> getMethods(TraitScope scopes = TraitScope.ALL)
            => getClassImpl().getMethods(scopes);

        /// <summary>
        /// Gets all the property traits in the class in the specified scopes.
        /// </summary>
        /// <param name="scopes">The scopes in which to search for the traits, as a bit-field set from
        /// the <see cref="TraitScope"/> enumeration.</param>
        /// <returns>A <see cref="ReadOnlyArrayView{PropertyTrait}"/> instance containing the property traits
        /// in the class in the specified scopes.</returns>
        public virtual ReadOnlyArrayView<PropertyTrait> getProperties(TraitScope scopes = TraitScope.ALL)
            => getClassImpl().getProperties(scopes);

        /// <summary>
        /// Gets all the constants defined in the class.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{ConstantTrait}"/> instance containing the constant traits
        /// defined in the class.</returns>
        public virtual ReadOnlyArrayView<ConstantTrait> getConstants() => getClassImpl().getConstants();

        /// <summary>
        /// Looks up a trait in the class.
        /// </summary>
        ///
        /// <param name="name">The qualified name of the trait.</param>
        /// <param name="isStatic">Set to true to search static traits, false for instance
        /// traits.</param>
        /// <param name="trait">If a trait is found, the <see cref="Trait"/> instance will be set to
        /// this argument.</param>
        ///
        /// <returns>A <see cref="BindStatus"/> indicating the result of the lookup.</returns>
        public virtual BindStatus lookupTrait(in QName name, bool isStatic, out Trait? trait)
            => getClassImpl().lookupTrait(name, isStatic, out trait);

        /// <summary>
        /// Looks up a trait in the class.
        /// </summary>
        ///
        /// <param name="name">The local name of the trait.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the trait.</param>
        /// <param name="isStatic">Set to true to search static traits, false for instance
        /// traits.</param>
        /// <param name="trait">If a trait is found, the <see cref="Trait"/> instance will be set to
        /// this argument.</param>
        ///
        /// <returns>A <see cref="BindStatus"/> indicating the result of the lookup.</returns>
        public virtual BindStatus lookupTrait(string name, in NamespaceSet nsSet, bool isStatic, out Trait? trait)
            => getClassImpl().lookupTrait(name, nsSet, isStatic, out trait);

        /// <summary>
        /// Returns an array containing all the traits defined in this class for which the
        /// <paramref name="filter"/> function returns true.
        /// </summary>
        /// <param name="filter">A filter function.</param>
        /// <returns>An array containing the <see cref="Trait"/> instances for all the traits in
        /// this class for which the <paramref name="filter"/> function returns true.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>ArgumentError #10060: <paramref name="filter"/> is null.</description></item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// This method always allocates a new array.
        /// </remarks>
        public virtual ReadOnlyArrayView<Trait> getTraitsByFilter(Predicate<Trait> filter)
            => getClassImpl().getTraitsByFilter(filter);

        /// <summary>
        /// Gets the value of the trait on a specified target object.
        /// </summary>
        /// <param name="target">The target object for which to get the value of the trait. This is
        /// ignored for static and global traits.</param>
        /// <param name="value">The value of the trait on the specified object.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public sealed override BindStatus tryGetValue(ASAny target, out ASAny value) {
            value = classObject;
            return BindStatus.SUCCESS;
        }

        /// <summary>
        /// Sets the value of the trait on a specified target object.
        /// </summary>
        /// <param name="target">The target object for which to set the value of the trait. This is
        /// ignored for static and global traits.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public sealed override BindStatus trySetValue(ASAny target, ASAny value) => BindStatus.FAILED_ASSIGNCLASS;

        /// <summary>
        /// Invokes the trait as a function on the specified target object.
        /// </summary>
        /// <param name="target">The target object on which to invoke the trait.</param>
        /// <param name="receiver">The receiver of the function call. (This argument is ignored
        /// when this method is called on an instance of <see cref="Class"/>.)</param>
        /// <param name="args">The arguments to the function call.</param>
        /// <param name="result">The return value of the function call.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public override BindStatus tryInvoke(ASAny target, ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result)
            => getClassImpl().tryInvoke(target, receiver, args, out result);

        /// <summary>
        /// Invokes the trait as a constructor on the specified target object.
        /// </summary>
        /// <param name="target">The target object on which to invoke the trait as a
        /// constructor.</param>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <param name="result">The object created by the constructor.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public override BindStatus tryConstruct(ASAny target, ReadOnlySpan<ASAny> args, out ASAny result)
            => getClassImpl().tryConstruct(target, args, out result);

        /// <summary>
        /// Returns a string representation of the current <see cref="Class"/>.
        /// </summary>
        /// <returns>A string representation of the current <see cref="Class"/>.</returns>
        public override string ToString() {
            var sb = new System.Text.StringBuilder();

            if (isDynamic)
                sb.Append("dynamic ");
            if (isFinal)
                sb.Append("final ");

            sb.Append(isInterface ? "interface " : "class ");
            sb.Append(name.ToString());

            if (parent != null && parent.underlyingType != typeof(ASObject)) {
                sb.Append(" extends ");
                sb.Append(parent.name.ToString());
            }

            ReadOnlyArrayView<Class> interfaces = getImplementedInterfaces();
            if (interfaces.length != 0) {
                sb.Append(" implements ");
                for (int i = 0; i < interfaces.length; i++) {
                    if (i != 0)
                        sb.Append(", ");
                    sb.Append(interfaces[i].name.ToString());
                }
            }

            return sb.ToString();
        }

    }

}
