using System;
using System.Threading;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    internal static class CoreClasses {

        private static LazyInitObject<bool> m_lazyLoaded = new LazyInitObject<bool>(
            () => { _load(); return true; },
            recursionHandling: LazyInitRecursionHandling.RETURN_DEFAULT
        );

        private static readonly Type[] s_coreClassTypes = {
            typeof(ASObject),
            typeof(ASint),
            typeof(ASuint),
            typeof(ASNumber),
            typeof(ASBoolean),
            typeof(ASString),
            typeof(ASArray),
            typeof(ASVectorAny),
            typeof(ASVector<>),
            typeof(ASClass),
            typeof(ASFunction),
            typeof(ASMath),
            typeof(ASJSON),
            typeof(ASDate),
            typeof(ASNamespace),
            typeof(ASQName),
            typeof(ASRegExp),
            typeof(ASXML),
            typeof(ASXMLList),
            typeof(ASError),
            typeof(ASArgumentError),
            typeof(ASDefinitionError),
            typeof(ASEvalError),
            typeof(ASRangeError),
            typeof(ASReferenceError),
            typeof(ASSecurityError),
            typeof(ASSyntaxError),
            typeof(ASTypeError),
            typeof(ASURIError),
            typeof(ASVerifyError),
            typeof(ScopedClosureReceiver),
        };

        /// <summary>
        /// Ensure that all core classes are loaded.
        /// </summary>
        internal static void ensureLoaded() => _ = m_lazyLoaded.value;

        private static void _load() {
            var sysDomain = ApplicationDomain.systemDomain;

            // Since there is no way to define an undefined constant in ASGlobal, we have to do it here.
            sysDomain.tryDefineGlobalTrait(new ConstantTrait("undefined", null, sysDomain, ASAny.undefined));

            Type[] coreClassTypes = s_coreClassTypes;
            for (int i = 0; i < coreClassTypes.Length; i++)
                NativeClass.createClass(coreClassTypes[i], sysDomain);

            NativeClass.createModule(typeof(ASGlobal), sysDomain);
        }

    }

}
