using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mariana.AVM2.Core;
using Mariana.Common;

namespace Mariana.AVM2.Native {

    internal sealed class NativeClass : ClassImpl {

        private static Assembly s_thisAssembly = typeof(NativeClass).Assembly;

        private bool m_containsProtoMethods;
        private bool m_containsIndexMethods;
        private Class m_vectorElementType;

        public override bool isVectorInstantiation => m_vectorElementType != null;

        public override Class vectorElementType => m_vectorElementType;

        private NativeClass(
            QName name,
            ApplicationDomain domain,
            ClassTag tag,
            Type underlyingType,
            AVM2ExportClassAttribute classAttr,
            Class vecElementType,
            bool canHideInheritedTraits
        )
            : base(name, domain, tag)
        {
            _checkClass(classAttr, underlyingType);

            setUnderlyingType(underlyingType);

            if (!underlyingType.IsInterface && underlyingType != typeof(ASObject))
                setParent(_getDependentClass(underlyingType.BaseType, domain));

            if (!underlyingType.IsGenericTypeDefinition) {
                setInterfaces(_makeInterfacesArray(underlyingType, domain));
                setIsDynamic(classAttr.isDynamic);
                setIsHidingAllowed(canHideInheritedTraits);
                m_containsProtoMethods = classAttr.hasPrototypeMethods;
                m_containsIndexMethods = classAttr.hasIndexLookupMethods;
                m_vectorElementType = vecElementType;
            }

            setMetadata(_extractMetadata(underlyingType.GetCustomAttributes<TraitMetadataAttribute>()));
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
        private static Class _getDependentClass(Type type, ApplicationDomain domain) {
            if (type == (object)typeof(ASAny))
                return null;

            return ClassTypeMap.getOrCreateClass(type, t => _internalCreateClass(type, domain, isDependent: true));
        }

        private static NativeClass _internalCreateClass(Type underlyingType, ApplicationDomain domain, bool isDependent) {
            domain = domain ?? ApplicationDomain.systemDomain;

            // When loading a dependent class, we do not need to check if it has already
            // been loaded because that is done before calling this method (by ClassTypeMap.getOrCreateClass)
            if (!isDependent) {
                if (ClassTypeMap.getClass(underlyingType) != null)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_TYPE_EXISTS, underlyingType);
            }

            var classAttr = underlyingType.GetCustomAttribute<AVM2ExportClassAttribute>();
            if (classAttr == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_NO_EXPORT_ATTR, underlyingType);

            var classInternalAttr = underlyingType.GetCustomAttribute<AVM2ExportClassInternalAttribute>();

            Class vecElementType = _getVectorElementClass(underlyingType, domain);
            QName name = _makeClassName(underlyingType, classAttr, vecElementType);

            ClassTag tag = (classInternalAttr != null) ? classInternalAttr.tag : ClassTag.OBJECT;
            bool canHideInheritedTraits = classInternalAttr != null && classInternalAttr.hidesInheritedTraits;

            if (vecElementType != null)
                // Instantiations of Vector<T> should use the VECTOR tag, but Vector itself should
                // not, so this must be special-cased here.
                tag = ClassTag.VECTOR;

            var createdClass = new NativeClass(name, domain, tag, underlyingType, classAttr, vecElementType, canHideInheritedTraits);

            if (!classAttr.hiddenFromGlobal && vecElementType == null) {
                bool addedToGlobals = domain.tryDefineGlobalTrait(createdClass);
                if (!addedToGlobals)
                    throw ErrorHelper.createError(ErrorCode.ALREADY_DEFINED, createdClass.name);
            }

            if (!isDependent)
                ClassTypeMap.addClass(underlyingType, createdClass);

            if (classInternalAttr != null && classInternalAttr.primitiveType != null)
                ClassTypeMap.addClass(classInternalAttr.primitiveType, createdClass);

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
        private static QName _makeClassName(Type underlyingType, AVM2ExportClassAttribute classAttr, Class vecElementType) {
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

            if (isInterface && attr.hasPrototypeMethods)
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_INTERFACE_PROTOTYPE, underlyingType);
        }

        private Class[] _makeInterfacesArray(Type underlyingType, ApplicationDomain domain) {
            Type[] interfaceTypes = underlyingType.GetInterfaces();
            DynamicArray<Class> interfaceList = new DynamicArray<Class>(interfaceTypes.Length);

            for (int i = 0; i < interfaceTypes.Length; i++) {
                Type interfaceType = interfaceTypes[i];
                if (!interfaceType.IsDefined(typeof(AVM2ExportClassAttribute))) {
                    if (underlyingType.IsInterface) {
                        throw ErrorHelper.createError(
                            ErrorCode.MARIANA__NATIVE_CLASS_INTERFACE_EXTENDS_UNEXPORTED, name, interfaceType);
                    }
                    continue;
                }

                Class interfaceClass = _getDependentClass(interfaceType, domain);
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

        private static Class _getVectorElementClass(Type underlyingType, ApplicationDomain domain) {
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

        protected private override void initClass() {
            if (underlyingType == typeof(ASVector<>))
                // We don't want any traits on the unsintantiated Vector class
                // (It is a class that's only meant to be instantiated, nothing else)
                return;

            _internalInitTraits();
            _internalInitClassSpecials();
        }

        private void _internalInitTraits() {

            bool isInterface = this.isInterface;
            BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance;
            if (!isInterface)
                bindingAttr |= BindingFlags.Static;

            MemberInfo[] members = underlyingType.FindMembers(
                memberType: MemberTypes.Field | MemberTypes.Method
                    | MemberTypes.Property | MemberTypes.Constructor,
                bindingAttr: bindingAttr,
                filter: (MemberInfo m, object fc) => {
                    if (m.IsDefined(typeof(AVM2ExportTraitAttribute), false))
                        return true;
                    if (m_containsProtoMethods && m.IsDefined(typeof(AVM2ExportPrototypeMethodAttribute), false))
                        return true;
                    return false;
                },
                filterCriteria: null
            );

            ClassConstructor classCtor = null;
            DynamicPropertyCollection protoProperties = prototypeObject.AS_dynamicProps;

            // If this is an interface, we need to capture the accessor methods of exported
            // properties to ensure that they are not (erroneously) considered as unexported.
            ReferenceSet<MethodInfo> propAccessorSet = null;
            if (isInterface)
                propAccessorSet = new ReferenceSet<MethodInfo>();

            for (int i = 0; i < members.Length; i++) {
                object[] memberAttrs = members[i].GetCustomAttributes(false);
                var traitAttr = memberAttrs.OfType<AVM2ExportTraitAttribute>().FirstOrDefault();
                var metadataAttrs = memberAttrs.OfType<TraitMetadataAttribute>();

                Trait trait = null;

                MemberTypes memberType = members[i].MemberType;

                if (memberType == MemberTypes.Field) {
                    if (traitAttr == null)
                        continue;

                    FieldInfo fieldInfo = (FieldInfo)members[i];
                    if (fieldInfo.IsLiteral)
                        trait = _makeConstantTrait(fieldInfo, traitAttr, this, applicationDomain, metadataAttrs);
                    else
                        trait = _makeFieldTrait(fieldInfo, traitAttr, this, applicationDomain, metadataAttrs);
                }
                else if (memberType == MemberTypes.Property) {
                    if (traitAttr == null)
                        continue;

                    PropertyTrait propTrait = _makePropertyTrait(
                        (PropertyInfo)members[i], traitAttr, this, applicationDomain, metadataAttrs);

                    trait = propTrait;

                    if (propAccessorSet != null) {
                        if (propTrait.getter != null)
                            propAccessorSet.add(propTrait.getter.underlyingMethodInfo);
                        if (propTrait.setter != null)
                            propAccessorSet.add(propTrait.setter.underlyingMethodInfo);
                    }
                }
                else if (memberType == MemberTypes.Method) {
                    MethodInfo methodInfo = (MethodInfo)members[i];

                    AVM2ExportPrototypeMethodAttribute protoMethodAttr = null;
                    string protoMethodName = null;

                    if (m_containsProtoMethods) {
                        protoMethodAttr = memberAttrs.OfType<AVM2ExportPrototypeMethodAttribute>().FirstOrDefault();
                        if (protoMethodAttr != null)
                            protoMethodName = protoMethodAttr.name ?? String.Intern(methodInfo.Name);
                    }

                    MethodTrait methodTrait;
                    if (traitAttr != null) {
                        methodTrait = _makeMethodTrait(methodInfo, traitAttr, this, applicationDomain, metadataAttrs);
                        trait = methodTrait;
                    }
                    else if (protoMethodAttr != null) {
                        methodTrait = _makeMethodTrait(methodInfo, QName.publicName(protoMethodName), this, applicationDomain, null);
                    }
                    else {
                        continue;
                    }

                    if (protoMethodAttr != null) {
                        string protoName = protoMethodAttr.name;
                        protoProperties.setValue(protoMethodName, methodTrait.createFunctionClosure(), isEnum: false);
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

                if (trait != null) {
                    bool defined = tryDefineTrait(trait);
                    if (!defined)
                        throw ErrorHelper.createError(ErrorCode.MARIANA__CLASS_TRAIT_ALREADY_EXISTS, trait.name, name);
                }
            }

            if (isInterface) {
                // All instance methods of an interface must be exported.
                MemberInfo[] unexportedMethods = underlyingType.FindMembers(
                    MemberTypes.Method,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                    (m, fc) => !m.IsDefined(typeof(AVM2ExportTraitAttribute), false),
                    null
                );

                for (int i = 0; i < unexportedMethods.Length; i++) {
                    if (!propAccessorSet.find((MethodInfo)unexportedMethods[i]))
                        throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_INTERFACE_UNEXPORTED_METHODS, name);
                }
            }

        }

        private static QName _makeTraitName(
            string localName, NamespaceKind nsKind, string nsName, Class declClass)
        {
            Namespace ns;
            if (nsKind == NamespaceKind.PRIVATE) {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__NATIVE_CLASS_TRAIT_INVALID_NS_KIND,
                    (declClass != null) ? declClass.name.ToString() : "<global>");
            }

            if (nsName == null)
                ns = Namespace.@public;
            else if (nsKind == NamespaceKind.ANY)
                ns = new Namespace(nsName);
            else
                ns = new Namespace(nsKind, nsName);

            if (ns.isPublic)
                return QName.publicName(localName);

            return new QName(ns, localName);
        }

        private static MetadataTagCollection _extractMetadata(IEnumerable<TraitMetadataAttribute> attrs) {
            if (attrs == null)
                return MetadataTagCollection.empty;

            var parser = new MetadataParser();
            DynamicArray<MetadataTag> tagList = new DynamicArray<MetadataTag>();

            foreach (var attr in attrs)
                tagList.add(parser.createTag(attr.m_data));

            return new MetadataTagCollection(tagList.asSpan());
        }

        private static ConstantTrait _makeConstantTrait(
            FieldInfo fieldInfo, AVM2ExportTraitAttribute attr, NativeClass declClass, ApplicationDomain domain,
            IEnumerable<TraitMetadataAttribute> metadataAttrs)
        {
            string localName = attr.name ?? String.Intern(fieldInfo.Name);

            QName traitName = _makeTraitName(localName, attr.nsKind, attr.nsUri, declClass);
            ASAny value = ASAny.AS_fromBoxed(fieldInfo.GetValue(null));

            return new NativeClassConstant(traitName, declClass, domain, value, _extractMetadata(metadataAttrs));
        }

        private static FieldTrait _makeFieldTrait(
            FieldInfo fldInfo, AVM2ExportTraitAttribute attr, NativeClass declClass, ApplicationDomain domain,
            IEnumerable<TraitMetadataAttribute> metadataAttrs)
        {
            string localName = attr.name ?? String.Intern(fldInfo.Name);

            QName traitName = _makeTraitName(localName, attr.nsKind, attr.nsUri, declClass);

            if (_isBoxedType(fldInfo.FieldType)) {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__NATIVE_CLASS_BOXED_PRIMITIVE,
                    traitName, (declClass == null) ? "<global>" : declClass.name.ToString());
            }

            Class fieldType = _getDependentClass(fldInfo.FieldType, domain);
            MetadataTagCollection metadata = _extractMetadata(metadataAttrs);

            return new NativeClassField(traitName, declClass, domain, fldInfo, fieldType, metadata);
        }

        private static MethodTrait _makeMethodTrait(
            MethodInfo methodInfo, AVM2ExportTraitAttribute attr, NativeClass declClass, ApplicationDomain domain,
            IEnumerable<TraitMetadataAttribute> metadataAttrs)
        {
            string localName = attr.name ?? String.Intern(methodInfo.Name);

            QName traitName = _makeTraitName(localName, attr.nsKind, attr.nsUri, declClass);
            return _makeMethodTrait(methodInfo, traitName, declClass, domain, metadataAttrs);
        }

        private static MethodTrait _makeMethodTrait(
            MethodInfo methodInfo, QName traitName, NativeClass declClass, ApplicationDomain domain,
            IEnumerable<TraitMetadataAttribute> metadataAttrs)
        {
            if (methodInfo.IsGenericMethod) {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__NATIVE_CLASS_GENERIC_METHOD,
                    traitName, (declClass == null) ? "<global>" : declClass.name.ToString());
            }

            if (declClass != null && declClass.isInterface && !traitName.ns.isPublic) {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__NATIVE_CLASS_INTERFACE_TRAIT_NONPUBLIC,
                    traitName, (declClass == null) ? "<global>" : declClass.name.ToString());
            }

            bool isOverride =
                methodInfo.IsVirtual
                && methodInfo.GetBaseDefinition().DeclaringType != methodInfo.DeclaringType;

            bool hasReturn;
            Class returnType;

            if (methodInfo.ReturnType == (object)typeof(void)) {
                hasReturn = false;
                returnType = null;
            }
            else {
                hasReturn = true;

                if (_isBoxedType(methodInfo.ReturnType)) {
                    throw ErrorHelper.createError(
                        ErrorCode.MARIANA__NATIVE_CLASS_BOXED_PRIMITIVE,
                        traitName, (declClass == null) ? "<global>" : declClass.name.ToString());
                }
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
            PropertyInfo propInfo, AVM2ExportTraitAttribute attr, NativeClass declClass, ApplicationDomain domain,
            IEnumerable<TraitMetadataAttribute> metadataAttrs)
        {
            string localName = attr.name ?? String.Intern(propInfo.Name);
            QName traitName = _makeTraitName(localName, attr.nsKind, attr.nsUri, declClass);

            if (declClass != null && declClass.isInterface && !traitName.ns.isPublic) {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__NATIVE_CLASS_INTERFACE_TRAIT_NONPUBLIC,
                    traitName, (declClass == null) ? "<global>" : declClass.name.ToString());
            }

            MethodTrait getter = null, setter = null;

            MethodInfo getterMethodInfo = propInfo.GetGetMethod();
            MethodInfo setterMethodInfo = propInfo.GetSetMethod();
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
                        ErrorCode.MARIANA__NATIVE_CLASS_PROP_METHOD_SIG,
                        traitName, (declClass == null) ? "<global>" : declClass.name.ToString());
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
                        ErrorCode.MARIANA__NATIVE_CLASS_PROP_METHOD_SIG,
                        traitName, (declClass == null) ? "<global>" : declClass.name.ToString());
                }
            }

            return new NativeClassProperty(
                traitName, declClass, domain, isStatic, getter, setter, _extractMetadata(metadataAttrs));
        }

        private ClassConstructor _makeClassConstructor(
            ConstructorInfo ctorInfo, AVM2ExportTraitAttribute attr, IEnumerable<TraitMetadataAttribute> metadataAttrs)
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
            MethodBase methodInfo, QName traitName, NativeClass declClass, ApplicationDomain domain,
            out MethodTraitParameter[] paramList, out bool hasRest)
        {

            ParameterInfo[] paramInfos = methodInfo.GetParameters();
            int numParams = paramInfos.Length;
            hasRest = false;

            if (paramInfos.Length != 0 && paramInfos[numParams - 1].ParameterType == typeof(RestParam)) {
                hasRest = true;
                numParams--;
            }

            paramList = new MethodTraitParameter[numParams];

            int firstOptionalParamIndex = -1;

            for (int i = 0; i < numParams; i++) {
                ParameterInfo paramInfo = paramInfos[i];
                Type paramInfoType = paramInfo.ParameterType;
                bool isOptional = false;

                if (paramInfoType.IsConstructedGenericType && paramInfoType.GetGenericTypeDefinition() == typeof(OptionalParam<>)) {
                    isOptional = true;
                    paramInfoType = paramInfoType.GetGenericArguments()[0];
                }

                if (_isBoxedType(paramInfoType)) {
                    throw ErrorHelper.createError(
                        ErrorCode.MARIANA__NATIVE_CLASS_BOXED_PRIMITIVE,
                        traitName, (declClass == null) ? "<global>" : declClass.name.ToString());
                }

                if (paramInfoType == typeof(RestParam)) {
                    throw ErrorHelper.createError(
                        ErrorCode.MARIANA__NATIVE_CLASS_METHOD_REST_PARAM,
                        traitName, (declClass == null) ? "<global>" : declClass.name.ToString());
                }

                Class paramType = _getDependentClass(paramInfoType, domain);
                bool hasDefault = false;
                ASAny defaultVal = default;

                if (!isOptional) {
                    hasDefault = _tryGetParamDefaultValue(paramInfo, paramType, traitName, declClass, out defaultVal);
                    isOptional = hasDefault;
                }

                if (isOptional && firstOptionalParamIndex == -1) {
                    firstOptionalParamIndex = i;
                }
                else if (firstOptionalParamIndex != -1 && !isOptional) {
                    throw ErrorHelper.createError(
                        ErrorCode.MARIANA__NATIVE_CLASS_METHOD_OPTIONAL_PARAMS,
                        traitName, (declClass == null) ? "<global>" : declClass.name.ToString());
                }

                // Parameter names do not need interning, as parameters are almost never looked up by name.
                paramList[i] = new MethodTraitParameter(paramInfos[i].Name, paramType, isOptional, hasDefault, defaultVal);
            }

        }

        private static bool _tryGetParamDefaultValue(
            ParameterInfo paramInfo, Class paramType, QName traitName, Class declClass, out ASAny defaultVal)
        {
            var attr = paramInfo.GetCustomAttribute(typeof(ParamDefaultValueAttribute));

            if (attr is ParamDefaultValueAttribute optionalAttr) {
                string namespaceURI = null;
                if (paramType != null && paramType.tag == ClassTag.NAMESPACE)
                    namespaceURI = optionalAttr.m_value as string;

                if (namespaceURI != null)
                    defaultVal = new ASNamespace(namespaceURI);
                else
                    defaultVal = ASAny.AS_fromBoxed(optionalAttr.m_value);
            }
            else if (paramInfo.IsOptional) {
                if (paramType == null)
                    // Special case needed here because paramInfo.DefaultValue will throw.
                    defaultVal = default(ASAny);
                else
                    defaultVal = ASAny.AS_fromBoxed(paramInfo.DefaultValue);
            }
            else {
                defaultVal = default(ASAny);
                return false;
            }

            // We don't want to call AS_coerceType from here because it accesses the AS_class
            // property, which triggers a class initialization (which may lead to recursion)

            bool mustCoerceToParamType = paramType != null
                && (defaultVal.value == null || defaultVal.value.GetType() != paramType.underlyingType);

            if (mustCoerceToParamType) {
                switch (paramType.tag) {
                    case ClassTag.INT:
                        defaultVal = (int)defaultVal;
                        break;
                    case ClassTag.UINT:
                        defaultVal = (uint)defaultVal;
                        break;
                    case ClassTag.NUMBER:
                        defaultVal = (double)defaultVal;
                        break;
                    case ClassTag.STRING:
                        defaultVal = (string)defaultVal;
                        break;
                    case ClassTag.BOOLEAN:
                        defaultVal = (bool)defaultVal;
                        break;
                    default:
                        if (!defaultVal.isDefined) {
                            defaultVal = ASAny.@null;
                        }
                        else if (defaultVal.value != null && paramType.underlyingType != typeof(ASObject)) {
                            throw ErrorHelper.createError(
                                ErrorCode.MARIANA__NATIVE_CLASS_METHOD_INVALID_DEFAULT,
                                traitName, (declClass == null) ? "<global>" : declClass.name.ToString());
                        }
                        break;
                }
            }

            return true;
        }

        private void _internalInitClassSpecials() {
            if (isInterface)
                return;

            Type underlyingType = this.underlyingType;
            MethodInfo specialInvoke = null, specialConstruct = null;

            if (underlyingType.Assembly == s_thisAssembly) {
                // Only consider the __AS_INVOKE and __AS_CONSTRUCT magic methods for
                // types in this assembly.

                const BindingFlags bindingFlags =
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

                specialInvoke = underlyingType.GetMethod("__AS_INVOKE", 0, bindingFlags, null, new[] {typeof(ReadOnlySpan<ASAny>)}, null);
                specialConstruct = underlyingType.GetMethod("__AS_CONSTRUCT", 0, bindingFlags, null, new[] {typeof(ReadOnlySpan<ASAny>)}, null);
            }

            IndexProperty intIndex = null, uintIndex = null, doubleIndex = null;
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

        private IndexProperty _getIndexLookupMethods(Type indexType) {
            Type underlyingType = this.underlyingType;

            MethodInfo getMethod = underlyingType.GetMethod("AS_getElement", new[] {indexType});
            if (getMethod == null)
                return null;

            Type getMethodReturnType = getMethod.ReturnType;

            MethodInfo setMethod = underlyingType.GetMethod("AS_setElement", new[] {indexType, getMethodReturnType});
            MethodInfo hasMethod = underlyingType.GetMethod("AS_hasElement", new[] {indexType});
            MethodInfo deleteMethod = underlyingType.GetMethod("AS_deleteElement", new[] {indexType});

            MethodTrait getMethodTrait = _makeMethodTrait(getMethod, QName.publicName(getMethod.Name), this, applicationDomain, null);

            MethodTrait setMethodTrait = null;
            if (setMethod != null)
                setMethodTrait = _makeMethodTrait(setMethod, QName.publicName(setMethod.Name), this, applicationDomain, null);

            MethodTrait hasMethodTrait = null;
            if (hasMethod != null)
                hasMethodTrait = _makeMethodTrait(hasMethod, QName.publicName(hasMethod.Name), this, applicationDomain, null);

            MethodTrait deleteMethodTrait = null;
            if (deleteMethod != null)
                deleteMethodTrait = _makeMethodTrait(deleteMethod, QName.publicName(deleteMethod.Name), this, applicationDomain, null);

            return new IndexProperty(
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
            domain = domain ?? ApplicationDomain.systemDomain;

            if (!moduleType.IsDefined(typeof(AVM2ExportModuleAttribute))) {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__NATIVE_CLASS_LOAD_MODULE_NO_EXPORT_ATTR, moduleType);
            }
            if (!moduleType.IsVisible) {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__NATIVE_CLASS_LOAD_NONPUBLIC, moduleType);
            }
            if (moduleType.IsGenericType) {
                throw ErrorHelper.createError(
                    ErrorCode.MARIANA__NATIVE_CLASS_LOAD_MODULE_GENERIC, moduleType);
            }

            MemberInfo[] members = moduleType.FindMembers(
                memberType: MemberTypes.Field | MemberTypes.Method | MemberTypes.Property,
                bindingAttr: BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
                filter: (m, fc) => m.IsDefined(typeof(AVM2ExportTraitAttribute), false),
                filterCriteria: null
            );

            DynamicArray<Trait> moduleTraits = new DynamicArray<Trait>(members.Length);

            for (int i = 0; i < members.Length; i++) {
                MemberTypes memberType = members[i].MemberType;
                var traitAttr = members[i].GetCustomAttribute<AVM2ExportTraitAttribute>();

                if (memberType == MemberTypes.Field) {
                    FieldInfo fldInfo = (FieldInfo)members[i];

                    Trait fieldTrait = fldInfo.IsLiteral
                        ? _makeConstantTrait(fldInfo, traitAttr, null, domain, null)
                        : (Trait)_makeFieldTrait(fldInfo, traitAttr, null, domain, null);

                    moduleTraits.add(fieldTrait);
                }
                else if (memberType == MemberTypes.Property) {
                    PropertyInfo propInfo = (PropertyInfo)members[i];
                    moduleTraits.add(_makePropertyTrait(propInfo, traitAttr, null, domain, null));
                }
                else if (memberType == MemberTypes.Method) {
                    MethodInfo methodInfo = (MethodInfo)members[i];
                    moduleTraits.add(_makeMethodTrait(methodInfo, traitAttr, null, domain, null));
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
            if (!methodInfo.IsStatic
                || !methodInfo.IsPublic
                || methodInfo.ContainsGenericParameters
                || (methodInfo.DeclaringType != null && !methodInfo.DeclaringType.IsVisible))
            {
                throw ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_STANDALONE_METHOD_INVALID);
            }
            return _makeMethodTrait(methodInfo, QName.publicName(methodInfo.Name), null, null, null);
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
                var indexed = new DynamicArray<string>();
                var keys = new DynamicArray<string>();
                var values = new DynamicArray<string>();

                if (!_readChar(out ch))
                    throw _parseError();

                if (ch == ']') {
                    if (!_readChar(out _)) {
                        // Tag with only a name.
                        return _makeTag(tagName, ref indexed, ref keys, ref values);
                    }
                    throw _parseError();
                }
                else if (ch != '(') {
                    throw _parseError();
                }

                while (true) {
                    string indexedOrKey = _readString();

                    if (!_readChar(out ch))
                        throw _parseError();

                    switch (ch) {
                        case ',':
                        case ';':
                        case ')':
                            indexed.add(indexedOrKey);
                            break;

                        case '=': {
                            string value = _readString();
                            if (!_readChar(out ch) || (ch != ',' && ch != ';' && ch != ')'))
                                throw _parseError();
                            keys.add(indexedOrKey);
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

                return _makeTag(tagName, ref indexed, ref keys, ref values);
            }

            private AVM2Exception _parseError() {
                return ErrorHelper.createError(ErrorCode.MARIANA__NATIVE_CLASS_LOAD_METADATA_INVALID);
            }

            private MetadataTag _makeTag(
                string tagName, ref DynamicArray<string> indexed, ref DynamicArray<string> keys,
                ref DynamicArray<string> values)
            {
                return new MetadataTag(tagName, indexed.asSpan(), keys.asSpan(), values.asSpan());
            }

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
                                DataStructureUtil.resizeArray(ref buffer, bufPos, bufPos + 1, false);
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
