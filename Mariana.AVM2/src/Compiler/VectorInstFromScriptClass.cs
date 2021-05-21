using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Mariana.AVM2.Core;
using System.Reflection;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Represents an instantiation of the Vector class whose element type is
    /// or contains a class currently being compiled.
    /// </summary>
    internal sealed class VectorInstFromScriptClass : ClassImpl {

        private static readonly Class s_vecAnyClass = Class.fromType<ASVectorAny>();

        // We use this class as a "marker" to identify the type argument of Vector in
        // field and method signatures so they can be substituted with the actual class.
        // The type T used as the marker must be chosen such that T or Vector<T> does
        // not appear in any field or method signature other than as the type argument
        // or a type dependent on it.
        private static readonly Class s_markerElement = Class.fromType<ASVector<ASVectorAny>>();

        /// <summary>
        /// Contains the Vector instantiations that use classes not yet fully compiled in
        /// their element types.
        /// </summary>
        /// <remarks>
        /// Such instantiations are called "incomplete" because, like the not-yet-compiled classes,
        /// the underlying Type, FieldInfo and MethodInfo instances corresponding to these classes
        /// and their traits are not available until they have been compiled (and the compiled
        /// assemblies loaded).
        /// </remarks>
        private static readonly ConcurrentBag<VectorInstFromScriptClass> s_incompleteInstances =
            new ConcurrentBag<VectorInstFromScriptClass>();

        private Class m_elementType;

        private Dictionary<int, Trait> m_incompleteTraitsByToken;

        private int m_constructorToken;

        internal VectorInstFromScriptClass(Class elementType)
            : base(
                new QName("__AS3__.vec", "Vector.<" + elementType.name.ToString() + ">"),
                s_vecAnyClass.applicationDomain,
                ClassTag.VECTOR
            )
        {
            Debug.Assert(elementType.underlyingType == null);

            m_elementType = elementType;
            setParent(s_vecAnyClass);
            setIsHidingAllowed(true);

            s_incompleteInstances.Add(this);
        }

        public override bool isFinal => true;

        public override bool isInterface => false;

        public override bool isVectorInstantiation => true;

        public override Class vectorElementType => m_elementType;

        protected private override Class createVectorClass() =>
            (underlyingType != null) ? base.createVectorClass() : new VectorInstFromScriptClass(this);

        private Class _substituteElement(Class type) {
            if (type == s_markerElement)
                return m_elementType;

            if (type.isVectorInstantiation)
                return _substituteElement(type.vectorElementType).getVectorClass();

            return type;
        }

        protected private override void initClass() {
            m_incompleteTraitsByToken = new Dictionary<int, Trait>();

            Class markerVector = s_markerElement.getVectorClass();

            setConstructor(new _Ctor(markerVector.constructor, this));
            m_constructorToken = constructor.underlyingConstructorInfo.MetadataToken;

            var traits = s_markerElement.getVectorClass().getTraits(TraitType.ALL, TraitScope.DECLARED);

            for (int i = 0; i < traits.length; i++) {
                var trait = traits[i];

                if (trait is FieldTrait field)
                    tryDefineTrait(new _Field(field, this));
                else if (trait is MethodTrait method)
                    tryDefineTrait(new _Method(method, this));
                else if (trait is PropertyTrait prop)
                    tryDefineTrait(new _Property(prop, this));
                else if (trait is ConstantTrait constant)
                    tryDefineTrait(new ConstantTrait(constant.name, this, applicationDomain, constant.constantValue));
            }

            var specials = markerVector.classSpecials;

            setClassSpecials(new ClassSpecials(
                // specialInvoke and specialConstruct are never used during compilation
                // (other than checking whether they are null or not) so the actual implementations
                // will be set once the underlying type is available.
                specialInvoke: specials.specialInvoke,
                specialConstruct: specials.specialConstruct,

                intIndexProperty: new IndexProperty(
                    m_elementType,
                    new _Method(specials.intIndexProperty.getMethod, this),
                    new _Method(specials.intIndexProperty.setMethod, this),
                    new _Method(specials.intIndexProperty.hasMethod, this),
                    new _Method(specials.intIndexProperty.deleteMethod, this)
                ),
                uintIndexProperty: new IndexProperty(
                    m_elementType,
                    new _Method(specials.uintIndexProperty.getMethod, this),
                    new _Method(specials.uintIndexProperty.setMethod, this),
                    new _Method(specials.uintIndexProperty.hasMethod, this),
                    new _Method(specials.uintIndexProperty.deleteMethod, this)
                ),
                numberIndexProperty: new IndexProperty(
                    m_elementType,
                    new _Method(specials.numberIndexProperty.getMethod, this),
                    new _Method(specials.numberIndexProperty.setMethod, this),
                    new _Method(specials.numberIndexProperty.hasMethod, this),
                    new _Method(specials.numberIndexProperty.deleteMethod, this)
                )
            ));

            // We don't need to handle prototype methods here as prototypes of ASVector<T>
            // instances use the inherited functions from ASVectorAny.
        }

        /// <summary>
        /// Completes the currently incomplete instances of <see cref="VectorInstFromScriptClass"/>
        /// and sets the <see cref="Class.underlyingType"/>, <see cref="FieldTrait.underlyingFieldInfo"/>,
        /// <see cref="MethodTrait.underlyingMethodInfo"/> and <see cref="ClassConstructor.underlyingConstructorInfo"/>
        /// properties of the classes and their traits to the corresponding instances that represent the
        /// compiled types and members. This should only be called once all classes used as element types
        /// for the Vector class have been fully compiled.
        /// </summary>
        internal static void completeInstances() {
            foreach (var inst in s_incompleteInstances)
                complete(inst);

            s_incompleteInstances.Clear();

            void complete(VectorInstFromScriptClass inst) {
                if (inst.underlyingType != null)
                    return;

                if (inst.m_elementType is VectorInstFromScriptClass elementVecInst)
                    complete(elementVecInst);

                Debug.Assert(inst.m_elementType.underlyingType != null);

                Type underlyingType = typeof(ASVector<>).MakeGenericType(new Type[] {inst.m_elementType.underlyingType});

                inst.setUnderlyingType(underlyingType);
                ClassTypeMap.addClass(underlyingType, inst);

                MethodInfo instSpecialInvoke = null, instSpecialConstruct = null;
                if (inst.classSpecials.specialInvoke != null) {
                    instSpecialInvoke = underlyingType.GetMethod(
                        inst.classSpecials.specialInvoke.Name,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
                    );
                }
                if (inst.classSpecials.specialConstruct != null) {
                    instSpecialConstruct = underlyingType.GetMethod(
                        inst.classSpecials.specialConstruct.Name,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
                    );
                }

                var newSpecials = new ClassSpecials(
                    specialInvoke: instSpecialInvoke,
                    specialConstruct: instSpecialConstruct,
                    intIndexProperty: inst.classSpecials.intIndexProperty,
                    uintIndexProperty: inst.classSpecials.uintIndexProperty,
                    numberIndexProperty: inst.classSpecials.numberIndexProperty
                );

                inst.setClassSpecials(newSpecials);

                MemberInfo[] members = underlyingType.GetMembers(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                for (int i = 0; i < members.Length; i++) {
                    int token = members[i].MetadataToken;

                    if (token == inst.m_constructorToken)
                        ((_Ctor)inst.constructor).setUnderlyingCtorInfo((ConstructorInfo)members[i]);

                    if (!inst.m_incompleteTraitsByToken.TryGetValue(members[i].MetadataToken, out Trait trait))
                        continue;

                    if (trait is _Field field)
                        field.setUnderlyingFieldInfo((FieldInfo)members[i]);
                    else if (trait is _Method method)
                        method.setUnderlyingMethodInfo((MethodInfo)members[i]);
                }
            }

            s_incompleteInstances.Clear();
        }

        private sealed class _Field : FieldTrait {
            public _Field(FieldTrait def, VectorInstFromScriptClass inst)
                : base(def.name, inst, inst.applicationDomain, def.isStatic)
            {
                setUnderlyingFieldInfo(def.underlyingFieldInfo);
                setIsReadOnly(def.isReadOnly);
                setFieldType(inst._substituteElement(def.fieldType));
                setMetadata(def.metadata);

                inst.m_incompleteTraitsByToken[def.underlyingFieldInfo.MetadataToken] = this;
            }

            internal new void setUnderlyingFieldInfo(FieldInfo fieldInfo) => base.setUnderlyingFieldInfo(fieldInfo);
        }

        private sealed class _Method : MethodTrait {
            public _Method(MethodTrait def, VectorInstFromScriptClass inst)
                : base(def.name, inst, inst.applicationDomain, def.isStatic)
            {
                var defParams = def.getParameters();
                var instParams = (defParams.length == 0)
                    ? Array.Empty<MethodTraitParameter>()
                    : new MethodTraitParameter[defParams.length];

                for (int i = 0; i < instParams.Length; i++) {
                    var defParam = defParams[i];
                    var instParamType = inst._substituteElement(defParam.type);
                    instParams[i] = new MethodTraitParameter(
                        defParam.name, instParamType, defParam.isOptional, defParam.hasDefault, defParam.defaultValue);
                }

                setIsOverride(def.isOverride);
                setSignature(def.hasReturn, def.hasReturn ? inst._substituteElement(def.returnType) : null, instParams, def.hasRest);
                setMetadata(def.metadata);
                setUnderlyingMethodInfo(def.underlyingMethodInfo);

                inst.m_incompleteTraitsByToken[def.underlyingMethodInfo.MetadataToken] = this;
            }

            internal new void setUnderlyingMethodInfo(MethodInfo methodInfo) => base.setUnderlyingMethodInfo(methodInfo);
        }

        private sealed class _Property : PropertyTrait {
            public _Property(PropertyTrait def, VectorInstFromScriptClass inst) : base(
                def.name,
                inst,
                inst.applicationDomain,
                def.isStatic,
                (def.getter != null) ? new _Method(def.getter, inst) : null,
                (def.setter != null) ? new _Method(def.setter, inst) : null
            ) {
                setMetadata(def.metadata);
            }
        }

        private sealed class _Ctor : ClassConstructor {
            public _Ctor(ClassConstructor def, VectorInstFromScriptClass inst) : base(inst) {
                var defParams = def.getParameters();
                var instParams = (defParams.length == 0)
                    ? Array.Empty<MethodTraitParameter>()
                    : new MethodTraitParameter[defParams.length];

                for (int i = 0; i < instParams.Length; i++) {
                    var defParam = defParams[i];
                    var instParamType = inst._substituteElement(defParam.type);
                    instParams[i] = new MethodTraitParameter(
                        defParam.name, instParamType, defParam.isOptional, defParam.hasDefault, defParam.defaultValue);
                }

                setSignature(instParams, def.hasRest);
                setMetadata(def.metadata);
                setUnderlyingCtorInfo(def.underlyingConstructorInfo);
            }

            internal new void setUnderlyingCtorInfo(ConstructorInfo ctorInfo) => base.setUnderlyingCtorInfo(ctorInfo);
        }

    }

}
