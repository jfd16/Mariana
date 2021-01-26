using System;
using System.Threading;
using Mariana.AVM2.Core;
using Mariana.AVM2.Native;

namespace Mariana.AVM2.Tests.TestDoubles {

    public static class SpyObjects {
        private static bool s_init;
        private static object s_lock = new object();
        private static ApplicationDomain s_spyObjDomain;

        public static void ensureLoaded() {
            LazyInitializer.EnsureInitialized(ref s_spyObjDomain, ref s_init, ref s_lock, () => {
                var domain = new ApplicationDomain();

                var types = new Type[] {
                    typeof(SpyObjectWithConversions),
                    typeof(SpyObjectWithToStringTrait),
                    typeof(SpyObjectWithValueOfTrait),
                    typeof(SpyObjectWithToStringAndValueOfTrait),
                };

                for (int i = 0; i < types.Length; i++)
                    domain.loadNativeClass(types[i]);

                return domain;
            });
        }
    }

    [AVM2ExportClass]
    public class SpyObjectWithToStringTrait : ASObject {
        private ASAny m_val;
        public SpyObjectWithToStringTrait(ASAny val) => m_val = val;

        [AVM2ExportTrait(name = "toString")]
        public new ASAny AS_toString() => m_val;
    }

    [AVM2ExportClass]
    public class SpyObjectWithValueOfTrait : ASObject {
        private ASAny m_val;
        public SpyObjectWithValueOfTrait(ASAny val) => m_val = val;

        [AVM2ExportTrait]
        public new ASAny valueOf() => m_val;
    }

    [AVM2ExportClass]
    public class SpyObjectWithToStringAndValueOfTrait : ASObject {
        private ASAny m_toStringVal;
        private ASAny m_valueOfVal;

        public SpyObjectWithToStringAndValueOfTrait(ASAny toStringVal, ASAny valueOfVal) =>
            (m_toStringVal, m_valueOfVal) = (toStringVal, valueOfVal);

        [AVM2ExportTrait(name = "toString")]
        public new ASAny AS_toString() => m_toStringVal;

        [AVM2ExportTrait]
        public new ASAny valueOf() => m_valueOfVal;
    }

    [AVM2ExportClass]
    public class SpyObjectWithConversions : ASObject {
        public int intValue;
        public uint uintValue;
        public double numberValue;
        public string stringValue;
        public bool boolValue;

        protected override bool AS_coerceBoolean() => boolValue;
        protected override int AS_coerceInt() => intValue;
        protected override uint AS_coerceUint() => uintValue;
        protected override double AS_coerceNumber() => numberValue;
        protected override string AS_coerceString() => stringValue;
    }

    public static class SpyObjectWithDynamicMethod {
        private static readonly MethodTrait s_invokeMethodTrait = MethodTrait.createNativeMethod(
            typeof(SpyObjectWithDynamicMethod).GetMethod(nameof(__invoke))
        );

        public static ASObject createObjectWithOwnMethod(string methodName, ASAny returnValue) {
            var obj = new ASObject();
            obj.AS_dynamicProps["__returnval"] = returnValue;
            obj.AS_dynamicProps.setValue(methodName, s_invokeMethodTrait.createFunctionClosure(), false);
            return obj;
        }

        public static ASObject createObjectWithProtoMethod(string methodName, ASAny returnValue) {
            var proto = new ASObject();
            proto.AS_dynamicProps.setValue(methodName, s_invokeMethodTrait.createFunctionClosure(), false);

            var obj = ASObject.AS_createWithPrototype(proto);
            obj.AS_dynamicProps["__returnval"] = returnValue;

            return obj;
        }

        public static ASAny __invoke(ASObject obj) => obj.AS_dynamicProps["__returnval"];
    }

}
