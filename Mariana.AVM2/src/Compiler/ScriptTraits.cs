using System;
using System.Reflection;
using Mariana.AVM2.Core;
using Mariana.AVM2.ABC;

namespace Mariana.AVM2.Compiler {

    internal sealed class ScriptField : FieldTrait {
        internal ScriptField(
            ABCTraitInfo traitInfo, Class declClass, ApplicationDomain domain, bool isStatic, Class fieldType)
            : base(traitInfo.name, declClass, domain, isStatic)
        {
            setIsReadOnly(traitInfo.kind == ABCTraitFlags.Const);
            setMetadata(traitInfo.metadata);
            setFieldType(fieldType);
        }

        internal ScriptField(
            in QName name, Class declClass, ApplicationDomain domain, bool isStatic, Class fieldType, bool isReadOnly)
            : base(name, declClass, domain, isStatic)
        {
            setIsReadOnly(isReadOnly);
            setFieldType(fieldType);
        }

        internal new void setUnderlyingFieldInfo(FieldInfo fieldInfo) => base.setUnderlyingFieldInfo(fieldInfo);
    }

    internal sealed class ScriptConstant : ConstantTrait {
        internal ScriptConstant(ABCTraitInfo traitInfo, Class declClass, ApplicationDomain domain)
            : base(traitInfo.name, declClass, domain, traitInfo.fieldDefaultValue)
        {
            setMetadata(traitInfo.metadata);
        }
    }

    internal sealed class ScriptProperty : PropertyTrait {
        internal ScriptProperty(in QName name, Class declClass, ApplicationDomain domain, bool isStatic)
            : base(name, declClass, domain, isStatic, getter: null, setter: null)
        {

        }

        internal new void setAccessors(MethodTrait getter, MethodTrait setter) => base.setAccessors(getter, setter);
    }

    internal sealed class ScriptMethod : MethodTrait {

        private ABCMethodInfo m_abcMethodInfo;
        private bool m_isFinal;

        internal ScriptMethod(
            ABCMethodInfo methodInfo,
            in QName name,
            Class declClass,
            ApplicationDomain domain,
            bool isStatic,
            bool isFinal,
            bool isOverride,
            MetadataTagCollection metadata
        )
            : base(name, declClass, domain, isStatic)
        {
            m_abcMethodInfo = methodInfo;
            m_isFinal = isStatic || (isFinal && !declClass.isInterface) || declClass.isFinal;
            setIsOverride(isOverride);
            setMetadata(metadata);
        }

        internal ScriptMethod(
            ABCTraitInfo traitInfo, Class declClass, ApplicationDomain domain, bool isStatic
        )
            : this(
                traitInfo.methodInfo,
                traitInfo.name,
                declClass,
                domain,
                isStatic,
                (traitInfo.flags & ABCTraitFlags.ATTR_Final) != 0,
                (traitInfo.flags & ABCTraitFlags.ATTR_Override) != 0,
                traitInfo.metadata
            )
        { }

        internal ABCMethodInfo abcMethodInfo => m_abcMethodInfo;

        internal new void setUnderlyingMethodInfo(MethodInfo mi) => base.setUnderlyingMethodInfo(mi);

        internal new void setSignature(
            bool hasReturn, Class returnType, MethodTraitParameter[] parameters, bool hasRest)
        {
            base.setSignature(hasReturn, returnType, parameters, hasRest);
        }

        public override bool isFinal => m_isFinal;

    }

    internal sealed class ScriptClassConstructor : ClassConstructor {

        private ABCMethodInfo m_abcMethodInfo;

        internal ScriptClassConstructor(ABCMethodInfo methodInfo, Class declClass) : base(declClass) {
            m_abcMethodInfo = methodInfo;
        }

        internal ABCMethodInfo abcMethodInfo => m_abcMethodInfo;

        internal new void setSignature(MethodTraitParameter[] parameters, bool hasRest) =>
            base.setSignature(parameters, hasRest);

        internal new void setUnderlyingCtorInfo(ConstructorInfo ctorInfo) => base.setUnderlyingCtorInfo(ctorInfo);

    }

}
