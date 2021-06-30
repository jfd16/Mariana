using System;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    internal static class CoreClasses {

        private static LazyInitObject<bool> m_lazyLoadClasses = new LazyInitObject<bool>(
            () => { _loadClasses(); return true; },
            recursionHandling: LazyInitRecursionHandling.RETURN_DEFAULT
        );

        private static LazyInitObject<bool> m_lazyLoadGlobals = new LazyInitObject<bool>(
            () => {
                NativeClass.createModule(typeof(ASGlobal), ApplicationDomain.systemDomain);
                return true;
            }
        );

        private static readonly Type[] s_coreClassTypes = {
            typeof(ASObject),
            typeof(ASNumber),
            typeof(ASint),
            typeof(ASuint),
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
        internal static void ensureClassesLoaded() => _ = m_lazyLoadClasses.value;

        /// <summary>
        /// Ensure that all core classes and global variables and functions are loaded.
        /// </summary>
        internal static void ensureGlobalsLoaded() => _ = m_lazyLoadClasses.value && m_lazyLoadGlobals.value;

        private static void _loadClasses() {
            var sysDomain = ApplicationDomain.systemDomain;

            // Since there is no way to define an undefined constant in ASGlobal, we have to do it here.
            sysDomain.tryDefineGlobalTrait(new ConstantTrait("undefined", declaringClass: null, sysDomain, ASAny.undefined));

            Type[] coreClassTypes = s_coreClassTypes;
            for (int i = 0; i < coreClassTypes.Length; i++)
                NativeClass.createClass(coreClassTypes[i], sysDomain);
        }

    }

}
