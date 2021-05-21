using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mariana.AVM2.Compiler;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// An application domain in the AVM2 is a container for code definitions (such as
    /// classes and functions).
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Application domains in the AVM2 have a hierarchy; the system domain (accessible using
    /// <see cref="systemDomain"/>) is at the root and contains the AVM2 core classes and global
    /// functions and variables. Code loaded into an application domain can access all definitions
    /// defined in that domain or any of its ancestors in the domain hierarchy.
    /// </para>
    /// <para>
    /// Global traits can defined in an application domain can be accessed using
    /// methods such as <see cref="getGlobalTrait(in QName,Boolean)"/>. These methods search the domain
    /// hierarchy from the domain on which the method is called upto the system domain.
    /// Some methods accept an additional argument that can be used to ignore the domain
    /// hierarchy and only search in the domain on which the method is called.
    /// </para>
    /// <para>
    /// To load a type written in .NET code as an AVM2 native class or module into
    /// an application domain, use the <see cref="loadNativeClass"/> and <see cref="loadNativeModule"/>
    /// methods. To search an entire .NET assembly for types that can be loaded, use
    /// <see cref="loadNativeClassesFromAssembly"/>. To load and compile ActionScript bytecode
    /// into an application domain, obtain a <see cref="ScriptLoader"/> instance by calling the
    /// <see cref="createScriptLoader"/> method.
    /// </para>
    /// </remarks>
    public sealed class ApplicationDomain {

        // A dictionary mapping module classes to the domains to which they are registered.
        // Modules are registered using the registerModule method.
        private static ConditionalWeakTable<Type, ApplicationDomain> s_moduleToDomainMap =
            new ConditionalWeakTable<Type, ApplicationDomain>();

        private static ApplicationDomain s_systemDomain = new ApplicationDomain(null, true);

        private ApplicationDomain m_parent;

        private ClassTraitTable m_globalTraitTable = new ClassTraitTable(null, true);

        private ASObject m_globalObject;

        private byte[] m_globalMemory = Array.Empty<byte>();

        private int m_globalMemorySize;

        /// <summary>
        /// Creates a new AVM2 application domain.
        /// </summary>
        /// <param name="parent">The parent of the application domain. If this is null, the system
        /// domain is used as the parent.</param>
        public ApplicationDomain(ApplicationDomain parent = null) : this(parent, false) {}

        /// <summary>
        /// Creates a new AVM2 application domain.
        /// </summary>
        /// <param name="parent">The parent of the application domain.</param>
        /// <param name="isSystem">This is true for the system domain, and false otherwise.</param>
        private ApplicationDomain(ApplicationDomain parent, bool isSystem) {
            if (parent == null && !isSystem)
                parent = s_systemDomain;

            m_parent = parent;
            m_globalObject = new ASGlobalObject(this);
        }

        /// <summary>
        /// Gets the system application domain.
        /// </summary>
        ///
        /// <remarks>
        /// The system domain is the application domain where all the core classes and traits are
        /// defined. It is the implicit parent of all other application domains which do not have an
        /// explicit parent.
        /// </remarks>
        public static ApplicationDomain systemDomain => s_systemDomain;

        /// <summary>
        /// Returns the <see cref="ApplicationDomain"/> instance representing the application domain
        /// to which the caller of this method belongs.
        /// </summary>
        /// <param name="nonSystemOnly">If this is true, ignore any system-domain methods in the call
        /// stack and only return an application domain if it is not the system domain.</param>
        /// <returns>The <see cref="ApplicationDomain"/> instance representing the application
        /// domain of the calling code, or null if no application domain is associated to any method
        /// in the current call stack.</returns>
        /// <remarks>
        /// This method involves a call stack walk which may be expensive.
        /// </remarks>
        public static ApplicationDomain getCurrentDomain(bool nonSystemOnly = false) {
            StackTrace stackTrace = new StackTrace(1);

            for (int i = 0, n = stackTrace.FrameCount; i < n; i++) {
                StackFrame frame = stackTrace.GetFrame(i);
                ApplicationDomain domainOfFrameMethod = getDomainFromMember(frame.GetMethod());
                if (domainOfFrameMethod != null && !(nonSystemOnly && domainOfFrameMethod == s_systemDomain))
                    return domainOfFrameMethod;
            }

            return null;
        }

        /// <summary>
        /// Gets the <see cref="ApplicationDomain"/> associated with the given
        /// <see cref="MemberInfo"/>.
        /// </summary>
        /// <param name="memberInfo">The <see cref="MemberInfo"/> for which to obtain the associated
        /// application domain.</param>
        /// <returns>The <see cref="ApplicationDomain"/> associated with
        /// <paramref name="memberInfo"/>, or null if no associated domain was found.</returns>
        public static ApplicationDomain getDomainFromMember(MemberInfo memberInfo) {
            Type type = memberInfo as Type;
            if (type == null)
                type = memberInfo.DeclaringType;

            Class klass = Class.fromType(type);
            if (klass != null)
                return klass.applicationDomain;

            s_moduleToDomainMap.TryGetValue(type, out var domain);
            return domain;
        }

        /// <summary>
        /// Gets the parent application domain of this domain.
        /// </summary>
        public ApplicationDomain parent => m_parent;

        /// <summary>
        /// Gets the global object for this application domain.
        /// </summary>
        ///
        /// <remarks>
        /// The global object is an object that can be used to access the global traits defined in the
        /// application domain as is they were traits on an ordinary object. Global objects can also
        /// have dynamic properties defined.
        /// </remarks>
        public ASObject globalObject => m_globalObject;

        /// <summary>
        /// Defines a global trait in this domain.
        /// </summary>
        /// <param name="trait">The <see cref="Trait"/> to be added as a global trait to this
        /// domain.</param>
        /// <param name="canHideFromParent">If this is true, the trait can hide another trait
        /// defined on an ancestor of this domain with the same name. Otherwise, if a trait
        /// is defined on an ancestor domain with the same name, the trait is not defined
        /// and this method returns false.</param>
        /// <returns>True if the trait was added, false if it could not (for example, another trait
        /// with the same name already exists in this domain, or on one of its ancestors and
        /// <paramref name="canHideFromParent"/> is false).</returns>
        /// <remarks>
        /// If a non-class trait is being added, its containing module class must be registered using
        /// the <see cref="registerModule"/> method.
        /// </remarks>
        internal bool tryDefineGlobalTrait(Trait trait, bool canHideFromParent = false) {
            if (!canHideFromParent && m_parent != null) {
                if (m_parent.lookupGlobalTrait(trait.name, false, out _) == BindStatus.SUCCESS)
                    return false;
            }
            return m_globalTraitTable.tryAddTrait(trait);
        }

        /// <summary>
        /// Associates the given module (and any global traits defined inside it) with this
        /// application domain.
        /// </summary>
        /// <param name="moduleType">The <see cref="Type"/> representing the module class to be
        /// associated with this domain.</param>
        internal void registerModule(Type moduleType) => s_moduleToDomainMap.Add(moduleType, this);

        /// <summary>
        /// Attempts to obtain the global trait from this application domain with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the trait.</param>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <param name="trait">The trait with the given name.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation. If this is
        /// <see cref="BindStatus.SUCCESS" qualifyHint="true"/> the search was successful; if it is
        /// <see cref="BindStatus.NOT_FOUND" qualifyHint="true"/>, no trait with the name
        /// <paramref name="name"/> exists.</returns>
        ///
        /// <remarks>
        /// The search for the trait is done in a child-to-parent order, starting at the domain on
        /// which this method is called and ending at the system domain (which is the root of the
        /// application domain hierarchy). The search stops whenever a trait is found or it results in
        /// an ambiguous match, on any domain in the inheritance chain.
        /// </remarks>
        public BindStatus lookupGlobalTrait(in QName name, bool noInherited, out Trait trait) {
            ApplicationDomain curDomain = this;

            while (curDomain != null) {
                if (curDomain == s_systemDomain)
                    CoreClasses.ensureGlobalsLoaded();

                BindStatus bindStatus = curDomain.m_globalTraitTable.tryGetTrait(name, true, out trait);
                if (bindStatus != BindStatus.NOT_FOUND)
                    return bindStatus;

                if (noInherited)
                    break;

                curDomain = curDomain.m_parent;
            }

            trait = null;
            return BindStatus.NOT_FOUND;
        }

        /// <summary>
        /// Attempts to obtain the global trait from this application domain with the given name in
        /// one of the namespaces of the given set.
        /// </summary>
        /// <param name="name">The name of the trait.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the trait.</param>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <param name="trait">The trait with the given name.</param>
        ///
        /// <returns>
        /// A <see cref="BindStatus"/> indicating the result of the operation. If this is
        /// <see cref="BindStatus.SUCCESS" qualifyHint="true"/> the search was successful; if it is
        /// <see cref="BindStatus.NOT_FOUND" qualifyHint="true"/>, no trait with the name
        /// <paramref name="name"/> exists. <see cref="BindStatus.AMBIGUOUS" qualifyHint="true"/>
        /// is used to indicate that a trait with the name <paramref name="name"/> exists in two or
        /// more namespaces of <paramref name="nsSet"/>.
        /// </returns>
        ///
        /// <remarks>
        /// The search for the trait is done in a child-to-parent order, starting at the domain on
        /// which this method is called and ending at the system domain (which is the root of the
        /// application domain hierarchy). The search stops whenever a trait is found or it results in
        /// an ambiguous match, on any domain in the inheritance chain.
        /// </remarks>
        public BindStatus lookupGlobalTrait(string name, in NamespaceSet nsSet, bool noInherited, out Trait trait) {
            ApplicationDomain curDomain = this;

            while (curDomain != null) {
                BindStatus bindStatus = curDomain.m_globalTraitTable.tryGetTrait(name, nsSet, true, out trait);
                if (bindStatus != BindStatus.NOT_FOUND)
                    return bindStatus;

                if (noInherited)
                    break;

                curDomain = curDomain.m_parent;
            }

            trait = null;
            return BindStatus.NOT_FOUND;
        }

        /// <summary>
        /// Gets the global trait from this application domain with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the trait.</param>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <returns>The trait with the name <paramref name="name"/>, or null if no trait with that
        /// name exists.</returns>
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
        /// The search for the trait is done in a child-to-parent order, starting at the domain on
        /// which this method is called and ending at the system domain (which is the root of the
        /// application domain hierarchy). The search stops whenever a trait is found or it results in
        /// an ambiguous match, on any domain in the inheritance chain.
        /// </remarks>
        public Trait getGlobalTrait(in QName name, bool noInherited = false) {
            BindStatus bindStatus = lookupGlobalTrait(name, noInherited, out Trait trait);
            if (bindStatus == BindStatus.SUCCESS)
                return trait;
            if (bindStatus == BindStatus.AMBIGUOUS)
                throw ErrorHelper.createBindingError("global", name.ToString(), bindStatus);
            return null;
        }

        /// <summary>
        /// Gets the global trait from this application domain with the given name in one of the
        /// namespaces of the given set.
        /// </summary>
        /// <param name="name">The name of the trait.</param>
        /// <param name="nsSet">The set of namespaces in which to search for the trait.</param>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <returns>The trait with the name <paramref name="name"/>, or null if no trait with that
        /// name exists.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>TypeError #1008: Two or more traits are found with the name <paramref name="name"/> in
        /// different namespaces of <paramref name="nsSet"/> (Ambiguous match).</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// The search for the trait is done in a child-to-parent order, starting at the domain on
        /// which this method is called and ending at the system domain (which is the root of the
        /// application domain hierarchy). The search stops whenever a trait is found or it results in
        /// an ambiguous match, on any domain in the inheritance chain.
        /// </remarks>
        public Trait getGlobalTrait(string name, in NamespaceSet nsSet, bool noInherited = false) {
            BindStatus bindStatus = lookupGlobalTrait(name, nsSet, noInherited, out Trait trait);
            if (bindStatus == BindStatus.SUCCESS)
                return trait;
            if (bindStatus == BindStatus.AMBIGUOUS)
                throw ErrorHelper.createBindingError("global", name.ToString(), bindStatus);
            return null;
        }

        /// <summary>
        /// Gets the global class from this application domain with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the trait.</param>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <returns>The class with the name <paramref name="name"/>, or null if no trait with that
        /// name exists or the trait with that name is not a class.</returns>
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
        /// The search for the trait is done in a child-to-parent order, starting at the domain on
        /// which this method is called and ending at the system domain (which is the root of the
        /// application domain hierarchy). The search stops whenever a trait is found or it results in
        /// an ambiguous match, on any domain in the inheritance chain.
        /// </remarks>
        public Class getGlobalClass(in QName name, bool noInherited = false) =>
            getGlobalTrait(name, noInherited) as Class;

        /// <summary>
        /// Gets the global method from this application domain with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the trait.</param>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <returns>The method with the name <paramref name="name"/>, or null if no trait with that
        /// name exists or the trait with that name is not a method.</returns>
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
        /// The search for the trait is done in a child-to-parent order, starting at the domain on
        /// which this method is called and ending at the system domain (which is the root of the
        /// application domain hierarchy). The search stops whenever a trait is found or it results in
        /// an ambiguous match, on any domain in the inheritance chain.
        /// </remarks>
        public MethodTrait getGlobalMethod(in QName name, bool noInherited = false) =>
            getGlobalTrait(name, noInherited) as MethodTrait;

        /// <summary>
        /// Gets the global field from this application domain with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the trait.</param>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <returns>The field with the name <paramref name="name"/>, or null if no trait with that
        /// name exists or the trait with that name is not a field.</returns>
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
        /// The search for the trait is done in a child-to-parent order, starting at the domain on
        /// which this method is called and ending at the system domain (which is the root of the
        /// application domain hierarchy). The search stops whenever a trait is found or it results in
        /// an ambiguous match, on any domain in the inheritance chain.
        /// </remarks>
        public FieldTrait getGlobalField(in QName name, bool noInherited = false) =>
            getGlobalTrait(name, noInherited) as FieldTrait;

        /// <summary>
        /// Gets the global property from this application domain with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the trait.</param>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <returns>The class with the name <paramref name="name"/>, or null if no trait with that
        /// name exists or the trait with that name is not a property.</returns>
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
        /// The search for the trait is done in a child-to-parent order, starting at the domain on
        /// which this method is called and ending at the system domain (which is the root of the
        /// application domain hierarchy). The search stops whenever a trait is found or it results in
        /// an ambiguous match, on any domain in the inheritance chain.
        /// </remarks>
        public PropertyTrait getGlobalProperty(in QName name, bool noInherited = false) =>
            getGlobalTrait(name, noInherited) as PropertyTrait;

        /// <summary>
        /// Gets the global constant from this application domain with the given name.
        /// </summary>
        /// <param name="name">The qualified name of the trait.</param>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <returns>The constant with the name <paramref name="name"/>, or null if no trait with
        /// that name exists or the trait with that name is not a constant.</returns>
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
        /// The search for the trait is done in a child-to-parent order, starting at the domain on
        /// which this method is called and ending at the system domain (which is the root of the
        /// application domain hierarchy). The search stops whenever a trait is found or it results in
        /// an ambiguous match, on any domain in the inheritance chain.
        /// </remarks>
        public ConstantTrait getGlobalConstant(in QName name, bool noInherited = false) =>
            getGlobalTrait(name, noInherited) as ConstantTrait;

        /// <summary>
        /// Returns a <see cref="ReadOnlyArrayView{Trait}"/> containing all the global traits of the
        /// given types in this application domain.
        /// </summary>
        ///
        /// <param name="types">The types of traits to include in the returned array.</param>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        ///
        /// <returns>An array containing the traits defined in this domain of the given
        /// categories.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>ArgumentError #10061: <paramref name="types"/> has an invalid flag set.</description></item>
        /// </list>
        /// </exception>
        public ReadOnlyArrayView<Trait> getGlobalTraits(
            TraitType types = TraitType.ALL, bool noInherited = false)
        {
            if ((types & TraitType.ALL) == TraitType.ALL && (noInherited || m_parent == null))
                return m_globalTraitTable.getTraits();

            return new ReadOnlyArrayView<Trait>(_getGlobalTraitsInternal<Trait>(types, noInherited));
        }

        /// <summary>
        /// Returns an array containing all the global classes in this application domain.
        /// </summary>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <returns>An array containing the classes defined in this domain.</returns>
        /// <remarks>
        /// This method always allocates a new array.
        /// </remarks>
        public ReadOnlyArrayView<Class> getGlobalClasses(bool noInherited = false) {
            return new ReadOnlyArrayView<Class>(
                _getGlobalTraitsInternal<Class>(TraitType.CLASS, noInherited));
        }

        /// <summary>
        /// Returns an array containing all the global fields in this application domain.
        /// </summary>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <returns>An array containing the fields defined in this domain.</returns>
        /// <remarks>
        /// This method always allocates a new array.
        /// </remarks>
        public ReadOnlyArrayView<FieldTrait> getGlobalFields(bool noInherited = false) {
            return new ReadOnlyArrayView<FieldTrait>(
                _getGlobalTraitsInternal<FieldTrait>(TraitType.FIELD, noInherited));
        }

        /// <summary>
        /// Returns an array containing all the global methods in this application domain.
        /// </summary>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <returns>An array containing the methods defined in this domain.</returns>
        /// <remarks>
        /// This method always allocates a new array.
        /// </remarks>
        public ReadOnlyArrayView<MethodTrait> getGlobalMethods(bool noInherited = false) {
            return new ReadOnlyArrayView<MethodTrait>(
                _getGlobalTraitsInternal<MethodTrait>(TraitType.METHOD, noInherited));
        }

        /// <summary>
        /// Returns an array containing all the global properties in this application domain.
        /// </summary>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <returns>An array containing the properties defined in this domain.</returns>
        /// <remarks>
        /// This method always allocates a new array.
        /// </remarks>
        public ReadOnlyArrayView<PropertyTrait> getGlobalProperties(bool noInherited = false) {
            return new ReadOnlyArrayView<PropertyTrait>(
                _getGlobalTraitsInternal<PropertyTrait>(TraitType.PROPERTY, noInherited));
        }

        /// <summary>
        /// Returns an array containing all the global constants in this application domain.
        /// </summary>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <returns>An array containing the constants defined in this domain.</returns>
        /// <remarks>
        /// This method always allocates a new array.
        /// </remarks>
        public ReadOnlyArrayView<ConstantTrait> getGlobalConstants(bool noInherited = false) {
            return new ReadOnlyArrayView<ConstantTrait>(
                _getGlobalTraitsInternal<ConstantTrait>(TraitType.CONSTANT, noInherited));
        }

        private T[] _getGlobalTraitsInternal<T>(TraitType types, bool noInherited = false) where T : Trait {
            DynamicArray<T> traitList = new DynamicArray<T>();

            ApplicationDomain curDomain = this;
            while (curDomain != null) {
                if (curDomain == s_systemDomain)
                    CoreClasses.ensureGlobalsLoaded();

                curDomain.m_globalTraitTable.getTraits(types, TraitScope.STATIC, ref traitList);
                if (noInherited)
                    break;
                curDomain = curDomain.m_parent;
            }

            return traitList.toArray();
        }

        /// <summary>
        /// Returns the global trait in this domain for which the given filter function returns true.
        /// </summary>
        /// <param name="filter">A filter function.</param>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        ///
        /// <returns>
        /// The <see cref="Trait"/> instance representing a global trait in this domain for which
        /// the <paramref name="filter"/> function returns true. If <paramref name="filter"/>
        /// returns false for all traits, returns null. If there are two or more traits for which the
        /// filter returns true, any one of them will be returned. (Which one is unspecified.)
        /// </returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>ArgumentError #10060: <paramref name="filter"/> is null.</description></item>
        /// </list>
        /// </exception>
        public Trait getGlobalTraitByFilter(Predicate<Trait> filter, bool noInherited = false) {
            if (filter == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(filter));

            ApplicationDomain curDomain = this;
            while (curDomain != null) {
                if (curDomain == s_systemDomain)
                    CoreClasses.ensureGlobalsLoaded();

                Trait trait = curDomain.m_globalTraitTable.getTraitByFilter(filter);
                if (trait != null)
                    return trait;

                if (noInherited)
                    break;

                curDomain = curDomain.m_parent;
            }

            return null;
        }

        /// <summary>
        /// Returns an array containing all the global traits defined in this domain for which the
        /// <paramref name="filter"/> function returns true.
        /// </summary>
        /// <param name="filter">A filter function.</param>
        /// <param name="noInherited">If this is set to true, only consider traits defined
        /// directly in this application domain; do not consider any traits inherited from ancestor
        /// domains.</param>
        /// <returns>An array containing the <see cref="Trait"/> instances for all the global traits
        /// in this domain for which the <paramref name="filter"/> function returns true.</returns>
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
        public ReadOnlyArrayView<Trait> getGlobalTraitsByFilter(Predicate<Trait> filter, bool noInherited = false) {
            if (filter == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(filter));

            DynamicArray<Trait> traitList = new DynamicArray<Trait>();

            ApplicationDomain curDomain = this;
            while (curDomain != null) {
                if (curDomain == s_systemDomain)
                    CoreClasses.ensureGlobalsLoaded();

                curDomain.m_globalTraitTable.getTraitsByFilter(filter, ref traitList);
                if (noInherited)
                    break;
                curDomain = curDomain.m_parent;
            }

            return new ReadOnlyArrayView<Trait>(traitList.toArray());
        }

        /// <summary>
        /// Loads the given type as a native type into this domain.
        /// </summary>
        /// <param name="type">The type to load as a native type.</param>
        /// <returns>A <see cref="Class"/> instance representing the loaded class.</returns>
        ///
        /// <remarks>
        /// When a class is loaded, its ancestor class (from the parent class upto <see cref="ASObject"/>)
        /// and all implemented interfaces that are declared as exported will be automatically loaded if
        /// they have not been loaded yet. Thus, when loading multiple classes, ensure that they are loaded
        /// in topological order (that is, base classes before derived classes and interfaces before their
        /// implementers) to avoid a double load error. Or use one of the <see cref="loadNativeClasses"/> and
        /// <see cref="loadNativeClassesFromAssembly"/> methods, which are designed for loading multiple
        /// classes and handle the loading order properly.
        /// </remarks>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>ArgumentError #10060: <paramref name="type"/> is null.</description></item>
        /// <item>
        /// <description>NativeClassLoadError #10101: A class with the underlying type <paramref name="type"/> already
        /// exists.</description>
        /// </item>
        /// <item>
        /// <description>NativeClassLoadError #10102: <paramref name="type"/> is not an interface type or a class type deriving
        /// from <see cref="ASObject"/>.</description>
        /// </item>
        /// <item><description>NativeClassLoadError #10103: <paramref name="type"/> is a generic type.</description></item>
        /// <item>
        /// <description>NativeClassLoadError #10104: <paramref name="type"/> is a type with non-public
        /// visibility.</description>
        /// </item>
        /// <item>
        /// <description>NativeClassLoadError #10105: <paramref name="type"/> declares <see cref="AVM2ExportClassAttribute"/>
        /// with the <see cref="AVM2ExportClassAttribute.nsKind" qualifyHint="true"/> property
        /// set to <see cref="NamespaceKind.PRIVATE" qualifyHint="true"/>.</description>
        /// </item>
        /// <item><description>NativeClassLoadError #10106: <paramref name="type"/> does not declare <see cref="AVM2ExportClassAttribute"/>.</description></item>
        /// <item><description>NativeClassLoadError #10117: <paramref name="type"/> is an instantiation of <see cref="ASVector{T}"/>.</description></item>
        /// </list>
        /// </exception>
        public Class loadNativeClass(Type type) {
            if (type == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(type));

            if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(ASVector<>))
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_VECTOR_EXPLICIT);

            CoreClasses.ensureGlobalsLoaded();
            return NativeClass.createClass(type, this);
        }

        /// <summary>
        /// Loads the given type as a native module into this domain.
        /// </summary>
        /// <param name="moduleType">The type to load as a native module.</param>
        ///
        /// <remarks>
        /// Loading a type as a native module adds all its exported static traits into
        /// the global scope of this domain.
        /// </remarks>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>ArgumentError #10060: <paramref name="moduleType"/> is null.</description></item>
        /// <item><description>NativeClassLoadError #10103: <paramref name="moduleType"/> is a generic type.</description></item>
        /// <item>
        /// <description>NativeClassLoadError #10104: <paramref name="moduleType"/> is a type with non-public
        /// visibility.</description>
        /// </item>
        /// <item>
        /// <description>NativeClassLoadError #10108: <paramref name="moduleType"/> does not declare
        /// <see cref="AVM2ExportModuleAttribute"/>.</description>
        /// </item>
        /// </list>
        /// </exception>
        public void loadNativeModule(Type moduleType) {
            if (moduleType == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(moduleType));

            if (this == s_systemDomain)
                CoreClasses.ensureGlobalsLoaded();

            NativeClass.createModule(moduleType, this);
        }

        /// <summary>
        /// Loads all public types from the given array of types that declare the attributes
        /// <see cref="AVM2ExportClassAttribute"/> and <see cref="AVM2ExportModuleAttribute"/>
        /// as native classes and modules into this domain.
        /// </summary>
        /// <param name="types">A read-only span of <see cref="Type"/> instances containing the types to load.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>ArgumentError #10060: One of the types in <paramref name="types"/> is null.</description></item>
        /// <item><description>NativeClassLoadError: One of the types in <paramref name="types"/> is public and contains the
        /// <see cref="AVM2ExportClassAttribute"/> or <see cref="AVM2ExportModuleAttribute"/> attribute
        /// but cannot be loaded as a native class or module.</description></item>
        /// </list>
        /// </exception>
        public void loadNativeClasses(ReadOnlySpan<Type> types) {
            if (this == s_systemDomain)
                CoreClasses.ensureGlobalsLoaded();

            for (int i = 0; i < types.Length; i++) {
                if (types[i] == null)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, $"types[{i}]");
            }

            NativeClass.createClassesAndModulesFromTypes(types, this);
        }

        /// <summary>
        /// Loads all public types from the given assembly that declare the attributes
        /// <see cref="AVM2ExportClassAttribute"/> and <see cref="AVM2ExportModuleAttribute"/>
        /// as native classes and modules into this domain.
        /// </summary>
        /// <param name="assembly">The <see cref="Assembly"/> instance representing the
        /// assembly containing the types to load.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>ArgumentError #10060: <paramref name="assembly"/> is null.</description></item>
        /// <item><description>NativeClassLoadError: One of the types declared in <paramref name="assembly"/> is public and
        /// contains the <see cref="AVM2ExportClassAttribute"/> or <see cref="AVM2ExportModuleAttribute"/>
        /// attribute but cannot be loaded as a native class or module.</description></item>
        /// </list>
        /// </exception>
        public void loadNativeClassesFromAssembly(Assembly assembly) {
            if (assembly == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(assembly));

            if (this == s_systemDomain)
                CoreClasses.ensureGlobalsLoaded();

            NativeClass.createClassesAndModulesFromTypes(assembly.GetTypes(), this);
        }

        /// <summary>
        /// Creates an instance of <see cref="ScriptLoader"/> that can be used to load and
        /// compile ActionScript 3 bytecode into this domain.
        /// </summary>
        /// <param name="compileOptions">An instance of <see cref="ScriptCompileOptions"/> containing
        /// the compiler configuration options. If this is null, the default options are used.</param>
        /// <returns>A <see cref="ScriptLoader"/> instance.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>Error #10070: This method is called on the system domain.</description></item>
        /// </list>
        /// </exception>
        public ScriptLoader createScriptLoader(ScriptCompileOptions compileOptions = null) {
            if (this == s_systemDomain)
                throw ErrorHelper.createError(ErrorCode.MARIANA__LOAD_ABC_SYSTEM_DOMAIN);
            return new ScriptLoader(this, compileOptions ?? new ScriptCompileOptions());
        }

        /// <summary>
        /// Sets the global memory buffer that will be accessed by scripts loaded into this domain
        /// that use global memory manipulation (Alchemy) instructions.
        /// </summary>
        ///
        /// <param name="buffer">A byte array to be used as the global memory buffer for this domain.</param>
        ///
        /// <exception cref="AVM2Exception">ArgumentError #10060: <paramref name="buffer"/> is null.</exception>
        public void setGlobalMemory(byte[] buffer) => setGlobalMemory(buffer, buffer.Length);

        /// <summary>
        /// Sets the global memory buffer that will be accessed by scripts loaded into this domain
        /// that use global memory manipulation (Alchemy) instructions.
        /// </summary>
        ///
        /// <param name="buffer">A byte array to be used as the global memory buffer for this domain.</param>
        /// <param name="size">The size of the buffer that will be accessible to scripts. This must not
        /// be greater than the length of <paramref name="buffer"/>.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>ArgumentError #10060: <paramref name="buffer"/> is null.</description></item>
        /// <item><description>ArgumentError #10061: <paramref name="size"/> is negative or greater than the
        /// length of <paramref name="buffer"/>.</description></item>
        /// </list>
        /// </exception>
        public void setGlobalMemory(byte[] buffer, int size) {
            if (buffer == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(buffer));

            if ((uint)size > (uint)buffer.Length)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, nameof(size));

            m_globalMemory = buffer;
            m_globalMemorySize = size;
        }

        /// <summary>
        /// Returns a <see cref="Span{Byte}"/> that can be used to access this application
        /// domain's global memory.
        /// </summary>
        /// <returns>A <see cref="Span{Byte}"/> that can be used to access the global memory of
        /// this domain. If no global memory buffer has been provided, an empty span is returned.</returns>
        public Span<byte> getGlobalMemorySpan() => m_globalMemory.AsSpan(0, m_globalMemorySize);

    }

}
