using System;
using System.Reflection;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Native {

    internal sealed class NativeClassConstant : ConstantTrait {
        internal NativeClassConstant(
            in QName name,
            Class declaringClass,
            ApplicationDomain appDomain,
            ASAny value,
            MetadataTagCollection metadata
        )
            : base(name, declaringClass, appDomain, value)
        {
            setMetadata(metadata);
        }
    }

    internal sealed class NativeClassField : FieldTrait {
        internal NativeClassField(
            in QName name,
            Class declaringClass,
            ApplicationDomain appDomain,
            FieldInfo underlyingFieldInfo,
            Class fieldType,
            MetadataTagCollection metadata
        )
            : base(name, declaringClass, appDomain, underlyingFieldInfo.IsStatic)
        {
            setIsReadOnly(underlyingFieldInfo.IsInitOnly);
            setUnderlyingFieldInfo(underlyingFieldInfo);
            setFieldType(fieldType);
            setMetadata(metadata);
        }
    }

    internal sealed class NativeClassMethod : MethodTrait {
        internal NativeClassMethod(
            in QName name,
            Class declaringClass,
            ApplicationDomain appDomain,
            MethodInfo underlyingMethodInfo,
            bool hasReturn,
            Class returnType,
            MethodTraitParameter[] parameters,
            bool hasRest,
            bool isOverride,
            MetadataTagCollection metadata
        )
            : base(name, declaringClass, appDomain, underlyingMethodInfo.IsStatic)
        {
            setUnderlyingMethodInfo(underlyingMethodInfo);
            setIsOverride(isOverride);
            setSignature(hasReturn, returnType, parameters, hasRest);
            setMetadata(metadata);
        }
    }

    internal sealed class NativeClassProperty : PropertyTrait {
        internal NativeClassProperty(
            in QName name,
            Class declaringClass,
            ApplicationDomain appDomain,
            bool isStatic,
            MethodTrait getter,
            MethodTrait setter,
            MetadataTagCollection metadata
        )
            : base(name, declaringClass, appDomain, isStatic, getter, setter)
        {
            setMetadata(metadata);
        }
    }

    internal sealed class NativeClassConstructor : ClassConstructor {
        internal NativeClassConstructor(
            Class declaringClass,
            ConstructorInfo underlyingCtorInfo,
            MethodTraitParameter[] parameters,
            bool hasRest,
            MetadataTagCollection metadata
        )
            : base(declaringClass)
        {
            setUnderlyingCtorInfo(underlyingCtorInfo);
            setSignature(parameters, hasRest);
            setMetadata(metadata);
        }
    }

}
