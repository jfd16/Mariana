using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mariana.AVM2.Core;
using Mariana.Common;

namespace Mariana.AVM2.Native {

    internal sealed class NativeClass : ClassImpl {

        private const string SPECIAL_INVOKE_METHOD_NAME = "__AS_INVOKE";
        private const string SPECIAL_CONSTRUCT_METHOD_NAME = "__AS_CONSTRUCT";
        private const string CLASS_OBJ_INIT_METHOD_NAME = "__AS_INIT_CLASS";

        private static Assembly s_thisAssembly = typeof(NativeClass).Assembly;

        private bool m_containsProtoMethods;
        private bool m_containsIndexMethods;
        private Class? m_vectorElementType;
        private NativeClass m_classForInstProto;
        private DynamicArray<(string name, MethodTrait method)> m_prototypeMethods;
        private Action<ASClass>? m_classObjectInitializer;

        private NativeClass(
            QName name,
            ApplicationDomain domain,
            ClassTag tag,
            Type underlyingType,
            AVM2ExportClassAttribute classAttr,
            Class? vecElementType,
            bool canHideInheritedTraits,
            bool dontLoadParentAndInterfaces
        )
            : base(name, domain, tag)
        {
            _checkClass(classAttr, underlyingType);
            setUnderlyingType(underlyingType);

            if (!dontLoadParentAndInterfaces)
                _loadParentAndInterfaces();

            if (!underlyingType.IsGenericTypeDefinition) {
                setIsDynamic(classAttr.isDynamic);
                setIsHidingAllowed(canHideInheritedTraits);

                m_containsProtoMethods = classAttr.hasPrototypeMethods;
                m_containsIndexMethods = classAttr.hasIndexLookupMethods;
                m_vectorElementType = vecElementType;
            }

            setMetadata(_extractMetadata(underlyingType.GetCustomAttributes<TraitMetadataAttribute>()));
            m_classForInstProto = this;
        }

        private void _loadParentAndInterfaces() {
            if (!underlyingType.IsInterface && underlyingType != typeof(ASObject))
                setParent(_getDependentClass(underlyingType.BaseType, applicationDomain)!);

            setInterfaces(_makeInterfacesArray(underlyingType, applicationDomain));
        }

        /// <summary>
        /// Creates a new instance of <see cref="NativeClass"/> from the given underlying type.
        /// </summary>
        ///
        /// <param name="underlyingType">The underlying type of the class.</param>
        /// <param name="domain">The application domain into which the class will be loaded.</param>
        internal static NativeClass createClass(Type underlyingType, ApplicationDomain domain) =>
            _internalCreateClass(underlyingType, domain, isDependent: false);

        /// <summary>
        /// Gets the <see cref="Class"/> for a dependent type of a class being loaded (for
        /// example, a type used as the base type or in a method signature). If no class
        /// associated with the dependent type exists, a new class will be created.
        /// </summary>
        ///
        /// <param name="type">The underlying type of the class.</param>
        /// <param name="domain">The application domain into which the class will be loaded, if an
        /// existing class associated with <paramref name="type"/> does not exist and it needs to
        /// be created.</param>
        private static Class? _getDependentClass(Type type, ApplicationDomain domain) {
            if (type == (object)typeof(ASAny))
                return null;

            return ClassTypeMap.getOrCreateClass(type, t => _internalCreateClass(type, domain, isDependent: true));
        }

        private static NativeClass _internalCreateClass(
            Type underlyingType, ApplicationDomain domain, bool isDependent = false, bool dontLoadParentAndInterfaces = false)
        {
            domain ??= ApplicationDomain.systemDomain;

            // If isDependent is true, this call is a callback from ClassTypeMap.getOrCreateClass, which
            // calls this method only if the class has not been imported.
            // Otherwise, this call is from a public API such as ApplicationDomain.loadNativeClass, and
            // it is an error if the class has already been imported.

            if (!isDependent) {
                if (ClassTypeMap.getClass(underlyingType) != null)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_TYPE_EXISTS, underlyingType);
            }

            var classAttr = underlyingType.GetCustomAttribute<AVM2ExportClassAttribute>();
            if (classAttr == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_NO_EXPORT_ATTR, underlyingType);

            var classInternalAttr = underlyingType.GetCustomAttribute<AVM2ExportClassInternalAttribute>();

            Class? vecElementType = _getVectorElementClass(underlyingType, domain);
            QName name = _makeClassName(underlyingType, classAttr, vecElementType);

            ClassTag tag = (classInternalAttr != null) ? classInternalAttr.tag : ClassTag.OBJECT;
            bool canHideInheritedTraits = classInternalAttr != null && classInternalAttr.hidesInheritedTraits;

            if (vecElementType != null) {
                // Instantiations of Vector<T> should use the VECTOR tag, but Vector itself should
                // not, so this must be special-cased here.
                tag = ClassTag.VECTOR;
            }

            var createdClass = new NativeClass(
                name, domain, tag, underlyingType, classAttr, vecElementType, canHideInheritedTraits, dontLoadParentAndInterfaces);

            // Add the class to the application domain's global traits, unless it is declared as
            // hidden or is an instantiation of Vector.

            if (!classAttr.hiddenFromGlobal && vecElementType == null) {
                bool classWasAddedToDomain = domain.tryDefineGlobalTrait(createdClass);
                if (!classWasAddedToDomain)
                    throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, createdClass.name);
            }

            // If isDependent is true, this call is a callback from ClassTypeMap.getOrCreateClass, which
            // would add the created class returned by this call to the map.
            // Otherwise, the class must be added to the map explicitly.

            if (!isDependent)
                ClassTypeMap.addClass(underlyingType, createdClass);

            if (classInternalAttr != null) {
                if (classInternalAttr.primitiveType != null) {
                    // The created class represents a primitive type. Map it to the primitive type in
                    // addition to the underlying type (which is the boxed object type). This allows
                    // for example `Class.fromType(typeof(int))` to return the class that represents
                    // the AS3 int type.
                    ClassTypeMap.addClass(classInternalAttr.primitiveType, createdClass);
                }

                if (classInternalAttr.usePrototypeOf != null && classInternalAttr.usePrototypeOf != underlyingType) {
                    // usePrototypeOf is used by classes for which instances must have the prototype of
                    // another class. The class still has its own prototype, accessible from the class
                    // object as its `prototype` property, but instances don't see it. This is currently
                    // used by int and uint which use the prototype of Number.
                    createdClass.m_classForInstProto = (NativeClass)_getDependentClass(classInternalAttr.usePrototypeOf, domain)!;
                }
            }

            return createdClass;
        }

        /// <summary>
        /// Creates a <see cref="QName"/> to be used as the name of a loaded class.
        /// </summary>
        /// <param name="underlyingType">The <see cref="Type"/> representing the underlying type of the class.</param>
        /// <param name="classAttr">The <see cref="AVM2ExportClassAttribute"/> attribute instance declared by
        /// <paramref name="underlyingType"/>.</param>
        /// <param name="vecElementType">If <paramref name="underlyingType"/> is an instantiation of the
        /// <see cref="ASVector{T}"/> type, the <see cref="Class"/> representing the element type. Otherwise
        /// set this to null.</param>
        /// <returns>A <see cref="QName"/> to be used as the name of the loaded class.</returns>
        private static QName _makeClassName(Type underlyingType, AVM2ExportClassAttribute classAttr, Class? vecElementType) {
            Namespace ns;
            string localName;

            if (classAttr.nsKind == NamespaceKind.PRIVATE) {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__NATIVE_CLASS_NAME_INVALID_NS_KIND, underlyingType);
            }

            if (classAttr.nsUri == null)
                ns = Namespace.@public;
            else if (classAttr.nsKind == NamespaceKind.ANY)
                ns = new Namespace(classAttr.nsUri);
            else
                ns = new Namespace(classAttr.nsKind, classAttr.nsUri);

            localName = classAttr.name ?? underlyingType.Name;
            if (vecElementType != null)
                localName += ".<" + vecElementType.name.ToString() + ">";

            // Don't intern synthesized names for vector instantiations.
            if (vecElementType == null)
                localName = String.Intern(localName);

            if (ns.isPublic)
                return QName.publicName(localName);

            return new QName(ns, localName);
        }

        private static void _checkClass(AVM2ExportClassAttribute attr, Type underlyingType) {
            bool isInterface = underlyingType.IsInterface;

            if (!isInterface && underlyingType != typeof(ASObject) && !underlyingType.IsSubclassOf(typeof(ASObject)))
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_NOT_EXTEND_ASOBJECT, underlyingType);

            if (underlyingType.IsNested)
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_NESTED_TYPE, underlyingType);

            if ((underlyingType.Attributes & TypeAttributes.Public) == 0)
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_NONPUBLIC, underlyingType);

            if (!isInterface && underlyingType.IsAbstract)
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_ABSTRACT, underlyingType);

            // Generic types are not allowed, with the special exception of Vector.
            if ((underlyingType.IsGenericTypeDefinition && underlyingType != typeof(ASVector<>))
                || (underlyingType.IsConstructedGenericType && underlyingType.GetGenericTypeDefinition() != typeof(ASVector<>)))
            {
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_GENERIC, underlyingType);
            }

            if (isInterface) {
                if (attr.hasPrototypeMethods)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_INTERFACE_PROTOTYPE, underlyingType);

                if (attr.isDynamic)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_INTERFACE_DYNAMIC, underlyingType);
            }
        }

        private Class[] _makeInterfacesArray(Type underlyingType, ApplicationDomain domain) {
            Type[] interfaceTypes = underlyingType.GetInterfaces();
            DynamicArray<Class> interfaceList = new DynamicArray<Class>(interfaceTypes.Length);

            for (int i = 0; i < interfaceTypes.Length; i++) {
                Type interfaceType = interfaceTypes[i];

                if (!interfaceType.IsDefined(typeof(AVM2ExportClassAttribute))) {
                    // An exported interface cannot extend an interface that is not exported, otherwise
                    // it would be impossible for an AS3 class to implement it.
                    // However, a class is allowed to implement a non-exported interface.

                    if (underlyingType.IsInterface) {
                        throw ErrorHelper.createError(
                            ErrorCode.MARIANA__NATIVE_CLASS_INTERFACE_EXTENDS_UNEXPORTED, name, interfaceType);
                    }
                    continue;
                }

                Class? interfaceClass = _getDependentClass(interfaceType, domain);
                if (interfaceClass != null)
                    interfaceList.add(interfaceClass);
            }

            return interfaceList.toArray();
        }

        /// <summary>
        /// Returns true if the given <see cref="Type"/> instance is the AVM2 boxed equivalent of a primitive type.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if <paramref name="type"/> is the AVM2 boxed equivalent of any primitive type,
        /// otherwise false.</returns>
        private static bool _isBoxedType(Type type) {
            return type == (object)typeof(ASint)
                || type == (object)typeof(ASuint)
                || type == (object)typeof(ASNumber)
                || type == (object)typeof(ASBoolean)
                || type == (object)typeof(ASString);
        }

        /// <summary>
        /// Throws an error if a type in a field, property or method signature is a boxed primitive type.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <param name="traitName">The name of the trait to include in the error message.</param>
        /// <param name="traitDeclClass">The class declaring the trait, to include in the error message.
        /// Null if the trait is global.</param>
        private static void _throwIfBoxedPrimitive(Type type, in QName traitName, Class? traitDeclClass) {
            if (!_isBoxedType(type))
                return;

            throw ErrorHelper.createError(
                ErrorCode.MARIANA__NATIVE_CLASS_BOXED_PRIMITIVE, traitName, _getClassNameForErrorMsg(traitDeclClass));
        }

        /// <summary>
        /// Returns the name of a class to display in an error message.
        /// </summary>
        /// <param name="klass">The class.</param>
        /// <returns>A name for <paramref name="klass"/> that can be displayed in an error message.</returns>
        private static string _getClassNameForErrorMsg(Class? klass) => (klass == null) ? "<global>" : klass.name.ToString();

        private static Class? _getVectorElementClass(Type underlyingType, ApplicationDomain domain) {
            if (!underlyingType.IsConstructedGenericType)
                return null;

            // Since _checkClass rules out non-Vector generic types, we don't need to check
            // for that case here.

            Type elementType = underlyingType.GetGenericArguments()[0];
            if (_isBoxedType(elementType))
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_VECTOR_BOXED);
            else if (elementType == typeof(ASAny))
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_VECTOR_ANY);
            else if (elementType == typeof(ASVector<>))
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_VECTOR_VECTOR);

            return _getDependentClass(elementType, domain);
        }

        public override bool isVectorInstantiation => m_vectorElementType != null;

        public override Class? vectorElementType => m_vectorElementType;

        internal override ASObject prototypeForInstance => m_classForInstProto.prototypeObject;

        protected private override void initClass() {
            if (underlyingType == typeof(ASVector<>)) {
                // We don't want any traits on the uninstantiated Vector class
                // (It is a class that's only meant to be instantiated, nothing else)
                return;
            }

            _internalInitTraits();
            _internalInitClassSpecials();

            if (underlyingType.Assembly == s_thisAssembly) {
                // For internal types, check if an __AS_INIT_CLASS magic method exists.
                MethodInfo classObjInitMethod = underlyingType.GetMethod(
                    CLASS_OBJ_INIT_METHOD_NAME,
                    BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] {typeof(ASClass)},
                    modifiers: null
                );

                if (classObjInitMethod != null)
                    m_classObjectInitializer = (Action<ASClass>)classObjInitMethod.CreateDelegate(typeof(Action<ASClass>));
            }
        }

        private protected override void initPrototypeObject(ASObject prototypeObject) {
            if (!m_containsProtoMethods)
                return;

            var protoMethods = m_prototypeMethods.asSpan();
            for (int i = 0; i < protoMethods.Length; i++) {
                prototypeObject.AS_dynamicProps!.setValue(
                    protoMethods[i].name, protoMethods[i].method.createFunctionClosure(), isEnum: false);
            }
        }

        private protected override void initClassObject(ASClass classObject) =>
            m_classObjectInitializer?.Invoke(classObject);

        private void _internalInitTraits() {
            bool isInterface = this.isInterface;

            BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance;

            if (!isInterface)
                bindingAttr |= BindingFlags.Static;

            bool memberFilter(MemberInfo member) {
                if (member.IsDefined(typeof(AVM2ExportTraitAttribute), inherit: false))
                    return true;

                if (m_containsProtoMethods && member.IsDefined(typeof(AVM2ExportPrototypeMethodAttribute), inherit: false))
                    return true;

                return false;
            }

            const MemberTypes searchMemberTypes =
                MemberTypes.Field | MemberTypes.Method | MemberTypes.Property | MemberTypes.Constructor;

            MemberInfo[] members = underlyingType.FindMembers(searchMemberTypes, bindingAttr, (m, fc) => memberFilter(m), null);

            ClassConstructor? classCtor = null;

            // This contains all trait-exported methods and property accessors. This will be
            // used to check for unexported methods on interfaces or unexported interface
            // implementations in classes.
            var exportedMethodSet = new ReferenceSet<MethodInfo>();

            for (int i = 0; i < members.Length; i++) {
                object[] memberAttrs = members[i].GetCustomAttributes(inherit: false);

                var traitAttr = memberAttrs.OfType<AVM2ExportTraitAttribute>().FirstOrDefault();
                var metadataAttrs = memberAttrs.OfType<TraitMetadataAttribute>();

                Trait? createdTrait = null;

                MemberTypes memberType = members[i].MemberType;

                if (memberType == MemberTypes.Field) {
                    if (traitAttr == null)
                        continue;

                    FieldInfo fieldInfo = (FieldInfo)members[i];
                    if (fieldInfo.IsLiteral)
                        createdTrait = _makeConstantTrait(fieldInfo, traitAttr, this, applicationDomain, metadataAttrs);
                    else
                        createdTrait = _makeFieldTrait(fieldInfo, traitAttr, this, applicationDomain, metadataAttrs);
                }
                else if (memberType == MemberTypes.Property) {
                    if (traitAttr == null)
                        continue;

                    PropertyTrait propTrait = _makePropertyTrait(
                        (PropertyInfo)members[i], traitAttr, this, applicationDomain, metadataAttrs);

                    createdTrait = propTrait;

                    if (propTrait.getter != null)
                        exportedMethodSet.add(propTrait.getter.underlyingMethodInfo);
                    if (propTrait.setter != null)
                        exportedMethodSet.add(propTrait.setter.underlyingMethodInfo);
                }
                else if (memberType == MemberTypes.Method) {
                    // A method could be exported as a trait, a prototype method, or both.
                    MethodInfo methodInfo = (MethodInfo)members[i];

                    // A method is exported as a prototype method if the AVM2ExportClass attribute on the
                    // class has containsProtoMethods = true and the method has the AVM2ExportPrototypeMethod
                    // attribute.
                    AVM2ExportPrototypeMethodAttribute? protoMethodAttr = null;
                    if (m_containsProtoMethods)
                        protoMethodAttr = memberAttrs.OfType<AVM2ExportPrototypeMethodAttribute>().FirstOrDefault();

                    if (traitAttr == null && protoMethodAttr == null) {
                        // The method is not exported as a trait or prototype method, so skip.
                        continue;
                    }

                    MethodTrait? methodTrait = null;

                    if (traitAttr != null) {
                        methodTrait = _makeMethodTrait(methodInfo, traitAttr, this, applicationDomain, metadataAttrs);
                        exportedMethodSet.add(methodInfo);
                        createdTrait = methodTrait;
                    }

                    if (protoMethodAttr != null) {
                        string protoMethodName = protoMethodAttr.name ?? String.Intern(methodInfo.Name);

                        // If this method is exported only as a prototype method, create a new MethodTrait using
                        // the prototype export name. If it is also exported as a trait, a MethodTrait has already
                        // been created and we can use the same one to create the prototype function closure.
                        if (methodTrait == null)
                            methodTrait = _makeMethodTrait(methodInfo, QName.publicName(protoMethodName), this, applicationDomain);

                        m_prototypeMethods.add((protoMethodName, methodTrait));
                    }
                }
                else if (memberType == MemberTypes.Constructor) {
                    ConstructorInfo ctorInfo = (ConstructorInfo)members[i];
                    if (traitAttr == null || ctorInfo.IsStatic)
                        continue;

                    if (classCtor != null)
                        throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_MULTIPLE_CTORS, name);

                    classCtor = _makeClassConstructor(ctorInfo, traitAttr, metadataAttrs);
                    setConstructor(classCtor);
                }

                if (createdTrait != null) {
                    bool traitWasAddedToClass = tryDefineTrait(createdTrait);
                    if (!traitWasAddedToClass)
                        throw ErrorHelper.createError(ErrorCode.MARIANA__CLASS_TRAIT_ALREADY_EXISTS, createdTrait.name, name);
                }
            }

            if (isInterface) {
                // All instance methods of an interface must be exported (either as methods,
                // or accessors of exported properties)

                MemberInfo[] unexportedMethods = underlyingType.FindMembers(
                    MemberTypes.Method,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                    (m, fc) => !m.IsDefined(typeof(AVM2ExportTraitAttribute), inherit: false),
                    null
                );

                for (int i = 0; i < unexportedMethods.Length; i++) {
                    if (!exportedMethodSet.find((MethodInfo)unexportedMethods[i])) {
                        throw ErrorHelper.createError(
                            ErrorCode.MARIANA__NATIVE_CLASS_INTERFACE_UNEXPORTED_METHOD,
                            name,
                            unexportedMethods[i].Name
                        );
                    }
                }
            }
            else {
                // All class methods that implement interface methods of exported interfaces must be exported.

                var interfaces = getImplementedInterfaces();
                for (int i = 0; i < interfaces.length; i++) {
                    if (parent != null && parent.canAssignTo(interfaces[i])) {
                        // No need to check interfaces implemented by the base class.
                        continue;
                    }

                    InterfaceMapping interfaceMap = underlyingType.GetInterfaceMap(interfaces[i].underlyingType);
                    MethodInfo[] targetMethods = interfaceMap.TargetMethods;

                    for (int j = 0; j < targetMethods.Length; j++) {
                        if (!exportedMethodSet.find(targetMethods[j])) {
                            throw ErrorHelper.createError(
                                ErrorCode.MARIANA__NATIVE_CLASS_INTERFACE_IMPL_UNEXPORTED,
                                name,
                                targetMethods[j].Name,
                                interfaces[i].name
                            );
                        }
                    }
                }
            }
        }

        private static QName _makeTraitName(
            string localName, NamespaceKind nsKind, string? nsUri, Class? declClass)
        {
            Namespace ns;

            if (nsKind == NamespaceKind.PRIVATE) {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__NATIVE_CLASS_TRAIT_INVALID_NS_KIND,
                    _getClassNameForErrorMsg(declClass)
                );
            }

            if (nsUri == null)
                ns = Namespace.@public;
            else if (nsKind == NamespaceKind.ANY)
                ns = new Namespace(nsUri);
            else
                ns = new Namespace(nsKind, nsUri);

            if (ns.isPublic)
                return QName.publicName(localName);

            return new QName(ns, localName);
        }

        private static MetadataTagCollection _extractMetadata(IEnumerable<TraitMetadataAttribute>? attrs) {
            if (attrs == null)
                return MetadataTagCollection.empty;

            var parser = new MetadataParser();
            DynamicArray<MetadataTag> tagList = new DynamicArray<MetadataTag>();

            foreach (var attr in attrs)
                tagList.add(parser.createTag(attr.m_data));

            return new MetadataTagCollection(tagList.asSpan());
        }

        private static ConstantTrait _makeConstantTrait(
            FieldInfo fieldInfo,
            AVM2ExportTraitAttribute attr,
            NativeClass? declClass,
            ApplicationDomain domain,
            IEnumerable<TraitMetadataAttribute>? metadataAttrs
        ) {
            string localName = attr.name ?? String.Intern(fieldInfo.Name);

            QName traitName = _makeTraitName(localName, attr.nsKind, attr.nsUri, declClass);
            ASAny value = ASAny.AS_fromBoxed(fieldInfo.GetValue(null));

            return new NativeClassConstant(traitName, declClass, domain, value, _extractMetadata(metadataAttrs));
        }

        private static FieldTrait _makeFieldTrait(
            FieldInfo fldInfo,
            AVM2ExportTraitAttribute attr,
            NativeClass? declClass,
            ApplicationDomain domain,
            IEnumerable<TraitMetadataAttribute>? metadataAttrs
        ) {
            string localName = attr.name ?? String.Intern(fldInfo.Name);
            QName traitName = _makeTraitName(localName, attr.nsKind, attr.nsUri, declClass);

            _throwIfBoxedPrimitive(fldInfo.FieldType, traitName, declClass);

            Class? fieldType = _getDependentClass(fldInfo.FieldType, domain);
            MetadataTagCollection metadata = _extractMetadata(metadataAttrs);

            return new NativeClassField(traitName, declClass, domain, fldInfo, fieldType, metadata);
        }

        private static MethodTrait _makeMethodTrait(
            MethodInfo methodInfo,
            AVM2ExportTraitAttribute attr,
            NativeClass? declClass,
            ApplicationDomain domain,
            IEnumerable<TraitMetadataAttribute>? metadataAttrs
        ) {
            string localName = attr.name ?? String.Intern(methodInfo.Name);

            QName traitName = _makeTraitName(localName, attr.nsKind, attr.nsUri, declClass);
            return _makeMethodTrait(methodInfo, traitName, declClass, domain, metadataAttrs);
        }

        private static MethodTrait _makeMethodTrait(
            MethodInfo methodInfo,
            QName traitName,
            NativeClass? declClass,
            ApplicationDomain domain,
            IEnumerable<TraitMetadataAttribute>? metadataAttrs = null,
            bool isStandalone = false
         ) {
            if (methodInfo.ContainsGenericParameters || (!isStandalone && methodInfo.IsGenericMethod)) {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__NATIVE_CLASS_GENERIC_METHOD, traitName, _getClassNameForErrorMsg(declClass));
            }

            bool isOverride =
                methodInfo.IsVirtual
                && methodInfo.GetBaseDefinition().DeclaringType != methodInfo.DeclaringType;

            bool hasReturn;
            Class? returnType;

            if (methodInfo.ReturnType == (object)typeof(void)) {
                hasReturn = false;
                returnType = null;
            }
            else {
                hasReturn = true;
                _throwIfBoxedPrimitive(methodInfo.ReturnType, traitName, declClass);
                returnType = _getDependentClass(methodInfo.ReturnType, domain);
            }

            _makeMethodParams(
                methodInfo,traitName,
                declClass,
                domain,
                out MethodTraitParameter[] parameters,
                out bool hasRest
            );

            MetadataTagCollection metadata = _extractMetadata(metadataAttrs);

            return new NativeClassMethod(
                traitName, declClass, domain, methodInfo, hasReturn, returnType, parameters, hasRest, isOverride, metadata);
        }

        private static PropertyTrait _makePropertyTrait(
            PropertyInfo propInfo,
            AVM2ExportTraitAttribute attr,
            NativeClass? declClass,
            ApplicationDomain domain,
            IEnumerable<TraitMetadataAttribute>? metadataAttrs
        ) {
            string localName = attr.name ?? String.Intern(propInfo.Name);
            QName traitName = _makeTraitName(localName, attr.nsKind, attr.nsUri, declClass);

            MethodTrait? getter = null, setter = null;

            MethodInfo? getterMethodInfo = propInfo.GetGetMethod();
            MethodInfo? setterMethodInfo = propInfo.GetSetMethod();

            bool isStatic = (getterMethodInfo == null) ? setterMethodInfo.IsStatic : getterMethodInfo.IsStatic;

            if (getterMethodInfo != null) {
                getter = _makeMethodTrait(
                    getterMethodInfo,
                    new QName(traitName.ns, "get{" + traitName.localName + "}"),
                    declClass,
                    domain,
                    metadataAttrs: null
                );

                if (getter.paramCount != 0 || getter.hasRest || !getter.hasReturn) {
                    throw ErrorHelper.createError(
                        ErrorCode.MARIANA__NATIVE_CLASS_PROP_METHOD_SIG, traitName, _getClassNameForErrorMsg(declClass));
                }
            }

            if (setterMethodInfo != null) {
                setter = _makeMethodTrait(
                    setterMethodInfo,
                    new QName(traitName.ns, "set{" + traitName.localName + "}"),
                    declClass,
                    domain,
                    metadataAttrs: null
                );

                if (setter.paramCount != 1 || setter.hasRest || setter.hasReturn
                    || (getter != null && getter.returnType != setter.getParameters()[0].type))
                {
                    throw ErrorHelper.createError(
                        ErrorCode.MARIANA__NATIVE_CLASS_PROP_METHOD_SIG, traitName, _getClassNameForErrorMsg(declClass));
                }
            }

            return new NativeClassProperty(
                traitName, declClass, domain, isStatic, getter, setter, _extractMetadata(metadataAttrs));
        }

        private ClassConstructor _makeClassConstructor(
            ConstructorInfo ctorInfo, AVM2ExportTraitAttribute attr, IEnumerable<TraitMetadataAttribute>? metadataAttrs)
        {
            _makeMethodParams(
                ctorInfo,
                QName.publicName("<constructor>"),
                this,
                applicationDomain,
                out MethodTraitParameter[] parameters,
                out bool hasRest
            );
            return new NativeClassConstructor(this, ctorInfo, parameters, hasRest, _extractMetadata(metadataAttrs));
        }

        private static void _makeMethodParams(
            MethodBase methodInfo,
            QName traitName,
            NativeClass? declClass,
            ApplicationDomain domain,
            out MethodTraitParameter[] paramList,
            out bool hasRest
        ) {
            ParameterInfo[] paramInfos = methodInfo.GetParameters();
            int paramCount = paramInfos.Length;

            hasRest = false;

            if (paramInfos.Length != 0 && paramInfos[paramCount - 1].ParameterType == typeof(RestParam)) {
                hasRest = true;
                paramCount--;
            }

            paramList = (paramCount == 0) ? Array.Empty<MethodTraitParameter>() : new MethodTraitParameter[paramCount];

            int firstOptionalParamIndex = -1;

            for (int i = 0; i < paramCount; i++) {
                ParameterInfo paramInfo = paramInfos[i];

                Type paramInfoType = paramInfo.ParameterType;

                Class? paramTypeClass;
                bool isOptional = false;
                bool hasDefault = false;
                ASAny defaultVal = default;

                if (paramInfoType.IsConstructedGenericType && paramInfoType.GetGenericTypeDefinition() == typeof(OptionalParam<>)) {
                    isOptional = true;
                    paramInfoType = paramInfoType.GetGenericArguments()[0];
                }

                _throwIfBoxedPrimitive(paramInfoType, traitName, declClass);

                if (paramInfoType == typeof(RestParam)) {
                    // RestParam can only be the last parameter of the method, which was checked earlier.
                    throw ErrorHelper.createError(
                        ErrorCode.MARIANA__NATIVE_CLASS_METHOD_REST_PARAM,
                        traitName,
                        _getClassNameForErrorMsg(declClass)
                    );
                }

                paramTypeClass = _getDependentClass(paramInfoType, domain);

                if (!isOptional) {
                    // If this is not an OptionalParam<T>, check if it has a default value.
                    hasDefault = _tryGetParamDefaultValue(paramInfo, paramTypeClass, traitName, declClass, out defaultVal);
                    isOptional = hasDefault;
                }

                if (isOptional && firstOptionalParamIndex == -1) {
                    firstOptionalParamIndex = i;
                }
                else if (firstOptionalParamIndex != -1 && !isOptional) {
                    // A required parameter cannot be after an optional one.
                    throw ErrorHelper.createError(
                        ErrorCode.MARIANA__NATIVE_CLASS_METHOD_OPTIONAL_PARAMS, traitName, _getClassNameForErrorMsg(declClass));
                }

                // Parameter names do not need interning, as parameters are almost never looked up by name.
                string paramName = paramInfos[i].Name;

                paramList[i] = new MethodTraitParameter(paramName, paramTypeClass, isOptional, hasDefault, defaultVal);
            }
        }

        private static bool _tryGetParamDefaultValue(
            ParameterInfo paramInfo, Class? paramType, QName traitName, Class? declClass, out ASAny defaultVal)
        {
            var defaultValueAttr =
                paramInfo.GetCustomAttribute(typeof(ParamDefaultValueAttribute)) as ParamDefaultValueAttribute;

            if (defaultValueAttr != null) {
                // ParamDefaultValueAttribute takes priority over default value in ParameterInfo.

                if (paramType != null && paramType.tag == ClassTag.NAMESPACE) {
                    // If the parameter type is a namespace, the default value must be a string
                    // that will be used as the URI to construct the default namespace value.

                    if (defaultValueAttr.m_value is not string defaultValueURI) {
                        throw ErrorHelper.createError(
                            ErrorCode.MARIANA__NATIVE_CLASS_METHOD_INVALID_DEFAULT,
                            traitName,
                            _getClassNameForErrorMsg(declClass)
                        );
                    }
                    defaultVal = new ASNamespace(defaultValueURI);
                }
                else {
                    defaultVal = ASAny.AS_fromBoxed(defaultValueAttr.m_value);
                }
            }
            else if (paramInfo.IsOptional) {
                if (paramType == null) {
                    // Special case needed here because paramInfo.DefaultValue will throw.
                    defaultVal = default(ASAny);
                }
                else {
                    defaultVal = ASAny.AS_fromBoxed(paramInfo.DefaultValue);
                }
            }
            else {
                // No default value.
                defaultVal = default(ASAny);
                return false;
            }

            // We don't want to call AS_coerceType from here because it accesses the AS_class
            // property, which triggers a class initialization (which may lead to recursion)

            bool mustCoerceToParamType = paramType != null
                && (defaultVal.isUndefinedOrNull || defaultVal.value!.GetType() != paramType.underlyingType);

            if (mustCoerceToParamType) {
                if (defaultVal.isUndefined) {
                    defaultVal = ASAny.@null;
                }
                else {
                    defaultVal = paramType!.tag switch {
                        ClassTag.INT => (int)defaultVal,
                        ClassTag.UINT => (uint)defaultVal,
                        ClassTag.NUMBER => (double)defaultVal,
                        ClassTag.STRING => (string?)defaultVal,
                        ClassTag.BOOLEAN => (bool)defaultVal,
                        _ => (defaultVal.isNull || paramType.isObjectClass) ? defaultVal : throwInvalidDefaultError()
                    };
                }
            }

            return true;

            ASAny throwInvalidDefaultError() {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__NATIVE_CLASS_METHOD_INVALID_DEFAULT,
                    traitName,
                    _getClassNameForErrorMsg(declClass)
                );
            }
        }

        private void _internalInitClassSpecials() {
            if (isInterface)
                return;

            Type underlyingType = this.underlyingType;
            MethodInfo? specialInvoke = null, specialConstruct = null;

            if (underlyingType.Assembly == s_thisAssembly) {
                // Only consider the __AS_INVOKE and __AS_CONSTRUCT magic methods for
                // types in this assembly.

                const BindingFlags bindingFlags =
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

                specialInvoke = underlyingType.GetMethod(
                    SPECIAL_INVOKE_METHOD_NAME, 0, bindingFlags, null, new[] {typeof(ReadOnlySpan<ASAny>)}, null);

                specialConstruct = underlyingType.GetMethod(
                    SPECIAL_CONSTRUCT_METHOD_NAME, 0, bindingFlags, null, new[] {typeof(ReadOnlySpan<ASAny>)}, null);
            }

            IndexProperty? intIndex = null, uintIndex = null, doubleIndex = null;
            if (m_containsIndexMethods) {
                intIndex = _getIndexLookupMethods(typeof(int));
                uintIndex = _getIndexLookupMethods(typeof(uint));
                doubleIndex = _getIndexLookupMethods(typeof(double));
            }

            if (specialInvoke != null || specialConstruct != null
                || intIndex != null || uintIndex != null || doubleIndex != null)
            {
                setClassSpecials(new ClassSpecials(specialInvoke, specialConstruct, intIndex, uintIndex, doubleIndex));
            }
        }

        private IndexProperty? _getIndexLookupMethods(Type indexType) {
            Type underlyingType = this.underlyingType;

            MethodInfo? getMethod = underlyingType.GetMethod("AS_getElement", new[] {indexType});
            if (getMethod == null)
                return null;

            Type getMethodReturnType = getMethod.ReturnType;

            MethodInfo? setMethod = underlyingType.GetMethod("AS_setElement", new[] {indexType, getMethodReturnType});
            MethodInfo? hasMethod = underlyingType.GetMethod("AS_hasElement", new[] {indexType});
            MethodInfo? deleteMethod = underlyingType.GetMethod("AS_deleteElement", new[] {indexType});

            MethodTrait getMethodTrait = _makeMethodTrait(getMethod, QName.publicName(getMethod.Name), this, applicationDomain);

            MethodTrait? setMethodTrait = null;
            if (setMethod != null)
                setMethodTrait = _makeMethodTrait(setMethod, QName.publicName(setMethod.Name), this, applicationDomain);

            MethodTrait? hasMethodTrait = null;
            if (hasMethod != null)
                hasMethodTrait = _makeMethodTrait(hasMethod, QName.publicName(hasMethod.Name), this, applicationDomain);

            MethodTrait? deleteMethodTrait = null;
            if (deleteMethod != null)
                deleteMethodTrait = _makeMethodTrait(deleteMethod, QName.publicName(deleteMethod.Name), this, applicationDomain);

            return new IndexProperty(
                _getDependentClass(indexType, applicationDomain)!,
                _getDependentClass(getMethodReturnType, applicationDomain),
                getMethodTrait,
                setMethodTrait,
                hasMethodTrait,
                deleteMethodTrait
            );
        }

        /// <summary>
        /// Loads the traits of a module into an application domain.
        /// </summary>
        /// <param name="moduleType">The type to load as a module.</param>
        /// <param name="domain">The application domain in which the traits exported by the module
        /// should be defined.</param>
        internal static void createModule(Type moduleType, ApplicationDomain domain) {
            domain ??= ApplicationDomain.systemDomain;

            if (ApplicationDomain.getDomainOfModule(moduleType) != null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_MODULE_EXISTS, moduleType);

            if (!moduleType.IsDefined(typeof(AVM2ExportModuleAttribute)))
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_MODULE_NO_EXPORT_ATTR, moduleType);

            if (!moduleType.IsVisible)
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_NONPUBLIC, moduleType);

            if (moduleType.IsGenericType)
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_MODULE_GENERIC, moduleType);

            MemberInfo[] members = moduleType.FindMembers(
                memberType: MemberTypes.Field | MemberTypes.Method | MemberTypes.Property,
                bindingAttr: BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
                filter: (m, fc) => m.IsDefined(typeof(AVM2ExportTraitAttribute), inherit: false),
                filterCriteria: null
            );

            DynamicArray<Trait> moduleTraits = new DynamicArray<Trait>(members.Length);

            for (int i = 0; i < members.Length; i++) {
                MemberTypes memberType = members[i].MemberType;

                var traitAttr = members[i].GetCustomAttribute<AVM2ExportTraitAttribute>();
                var metadataAttrs = members[i].GetCustomAttributes<TraitMetadataAttribute>();

                if (memberType == MemberTypes.Field) {
                    FieldInfo fldInfo = (FieldInfo)members[i];

                    Trait fieldTrait = fldInfo.IsLiteral
                        ? _makeConstantTrait(fldInfo, traitAttr, declClass: null, domain, metadataAttrs)
                        : (Trait)_makeFieldTrait(fldInfo, traitAttr, declClass: null, domain, metadataAttrs);

                    moduleTraits.add(fieldTrait);
                }
                else if (memberType == MemberTypes.Property) {
                    PropertyInfo propInfo = (PropertyInfo)members[i];
                    moduleTraits.add(_makePropertyTrait(propInfo, traitAttr, declClass: null, domain, metadataAttrs));
                }
                else if (memberType == MemberTypes.Method) {
                    MethodInfo methodInfo = (MethodInfo)members[i];
                    moduleTraits.add(_makeMethodTrait(methodInfo, traitAttr, declClass: null, domain, metadataAttrs));
                }
            }

            domain.registerModule(moduleType);

            for (int i = 0; i < moduleTraits.length; i++) {
                bool addedToGlobals = domain.tryDefineGlobalTrait(moduleTraits[i]);
                if (!addedToGlobals)
                    throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, moduleTraits[i].name);
            }
        }

        /// <summary>
        /// Creates a stand-alone method that is not contained in a class or registered as a global method.
        /// </summary>
        /// <param name="methodInfo">The <see cref="MethodInfo"/> from which to create the
        /// standalone method.</param>
        /// <returns>The created <see cref="MethodTrait"/> for the standalone method.</returns>
        internal static MethodTrait internalCreateStandAloneMethod(MethodInfo methodInfo) {
            if (!methodInfo.IsStatic || methodInfo.ContainsGenericParameters)
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_STANDALONE_METHOD_INVALID);

            return _makeMethodTrait(
                methodInfo,
                QName.publicName(methodInfo.Name),
                declClass: null,
                domain: ApplicationDomain.systemDomain,
                metadataAttrs: null,
                isStandalone: true
            );
        }

        /// <summary>
        /// Loads all public types from the given array that declare the attributes
        /// <see cref="AVM2ExportClassAttribute"/> and <see cref="AVM2ExportModuleAttribute"/>
        /// as native classes and modules in the given application domain.
        /// </summary>
        /// <param name="types">A span of <see cref="Type"/> instances containing the types to load.</param>
        /// <param name="domain">The application domain in which to define the loaded classes and
        /// module traits.</param>
        internal static void createClassesAndModulesFromTypes(ReadOnlySpan<Type> types, ApplicationDomain domain) {
            var createdClasses = new DynamicArray<NativeClass>();

            for (int i = 0; i < types.Length; i++) {
                Type type = types[i];
                if ((type.Attributes & TypeAttributes.Public) == 0)
                    continue;

                if (type.IsDefined(typeof(AVM2ExportClassAttribute), inherit: false)) {
                    // Don't load the base class or interfaces of the class as dependents because
                    // they may also be available in `types` and have not been loaded yet.
                    // Otherwise, a "class with the underlying type already exists" error may be
                    // thrown when the dependent class is loaded for the second time.
                    var klass = _internalCreateClass(type, domain, dontLoadParentAndInterfaces: true);
                    createdClasses.add(klass);
                }
                else if (type.IsDefined(typeof(AVM2ExportModuleAttribute), inherit: false)) {
                    createModule(type, domain);
                }
            }

            for (int i = 0; i < createdClasses.length; i++)
                createdClasses[i]._loadParentAndInterfaces();
        }

        /// <summary>
        /// Parses the data string of <see cref="TraitMetadataAttribute"/>.
        /// </summary>
        private struct MetadataParser {

            private string m_data;
            private int m_readPos;
            private char[] m_strBuffer;

            internal MetadataTag createTag(string data) {
                if (m_strBuffer == null)
                    m_strBuffer = new char[16];

                m_data = data;
                m_readPos = 0;

                char ch;

                if (!_readChar(out ch) || ch != '[')
                    throw _parseError();

                string tagName = _readString();
                var keys = new DynamicArray<string?>();
                var values = new DynamicArray<string>();

                if (!_readChar(out ch))
                    throw _parseError();

                if (ch == ']') {
                    if (!_readChar(out _)) {
                        // Tag with only a name.
                        return _makeTag(tagName, keys, values);
                    }
                    throw _parseError();
                }
                else if (ch != '(') {
                    throw _parseError();
                }

                while (true) {
                    string keyOrKeylessValue = _readString();

                    if (!_readChar(out ch))
                        throw _parseError();

                    switch (ch) {
                        case ',':
                        case ';':
                        case ')':
                            keys.add(null);
                            values.add(keyOrKeylessValue);
                            break;

                        case '=': {
                            string value = _readString();
                            if (!_readChar(out ch) || (ch != ',' && ch != ';' && ch != ')'))
                                throw _parseError();

                            keys.add(keyOrKeylessValue);
                            values.add(value);
                            break;
                        }

                        default:
                            throw _parseError();
                    }

                    if (ch == ')')
                        break;
                }

                if (!_readChar(out ch) || ch != ']')
                    throw _parseError();

                if (_readChar(out _))
                    // Junk characters after tag end
                    throw _parseError();

                return _makeTag(tagName, keys, values);
            }

            private AVM2Exception _parseError() =>
                ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_METADATA_INVALID);

            private MetadataTag _makeTag(string tagName, in DynamicArray<string?> keys, in DynamicArray<string> values) =>
                new MetadataTag(tagName, keys.asSpan(), values.asSpan());

            private bool _readChar(out char ch) {
                int pos = m_readPos;
                int len = m_data.Length;
                while (pos < len) {
                    char c = m_data[pos];
                    pos++;
                    if (c != ' ' && (uint)(c - 9) > 4) {
                        m_readPos = pos;
                        ch = c;
                        return true;
                    }
                }
                ch = '\0';
                return false;
            }

            private string _readString() {
                string data = m_data;
                int len = data.Length;
                char[] buffer = m_strBuffer;
                int bufPos = 0;

                if (!_readChar(out char ch))
                    throw _parseError();

                int pos = m_readPos;
                char quote;

                if (ch == '\'' || ch == '"') {
                    quote = ch;
                }
                else {
                    pos--;
                    quote = '\0';
                }

                bool end = false;
                char endCh = '\0';

                while (pos < len && !end) {
                    ch = data[pos];
                    pos++;

                    switch (ch) {
                        case '\'':
                        case '"':
                            if (quote != ch)
                                throw _parseError();
                            end = true;
                            endCh = ch;
                            break;

                        case '\\': {
                            if (pos == len)
                                throw _parseError();

                            char nextch = data[pos];
                            if (nextch == '\\' || nextch == '\'' || nextch == '"') {
                                pos++;
                                ch = nextch;
                            }

                            goto default;
                        }

                        case ',':
                        case ';':
                        case '(':
                        case ')':
                        case '[':
                        case ']':
                        case '=':
                        case ' ':
                        case '\n':
                        case '\r':
                        case '\f':
                        case '\t':
                        case '\v':
                            if (quote == '\0') {
                                end = true;
                                endCh = ch;
                                pos--;
                                break;
                            }
                            goto default;

                        default:
                            if (bufPos == buffer.Length)
                                DataStructureUtil.expandArray(ref buffer);

                            buffer[bufPos++] = ch;
                            break;
                    }
                }

                if (quote != '\0' && endCh != quote)
                    throw _parseError();

                m_strBuffer = buffer;
                m_readPos = pos;
                return new string(buffer, 0, bufPos);
            }
        }
    }
}
