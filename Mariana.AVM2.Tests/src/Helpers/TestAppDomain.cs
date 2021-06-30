using System;
using Mariana.AVM2.Core;
using Mariana.Common;

namespace Mariana.AVM2.Tests.Helpers {

    /// <summary>
    /// An application domain into which any AVM2 classes needed for tests should be loaded.
    /// </summary>
    internal static class TestAppDomain {

        private static ApplicationDomain s_domain = new ApplicationDomain();
        private static ReferenceSet<Type> s_loadedClasses = new ReferenceSet<Type>();
        private static object s_lock = new object();

        /// <summary>
        /// Gets the instance of the testing application domain.
        /// </summary>
        public static ApplicationDomain instance => s_domain;

        /// <summary>
        /// Ensures that the given classes are loaded into the test domain.
        /// This method is thread safe and has no effect when all the types have already been loaded.
        /// </summary>
        /// <param name="types">The types to load as AVM2 native classes and modules into the test domain.</param>
        public static void ensureClassesLoaded(params Type[] types) {
            lock (s_lock) {
                bool isLoaded = true;

                for (int i = 0; i < types.Length && isLoaded; i++)
                    isLoaded &= s_loadedClasses.find(types[i]);

                if (isLoaded)
                    return;

                s_domain.loadNativeClasses(types);

                for (int i = 0; i < types.Length; i++)
                    s_loadedClasses.add(types[i]);
            }
        }

    }

}
