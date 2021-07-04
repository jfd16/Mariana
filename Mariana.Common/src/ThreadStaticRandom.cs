using System;

namespace Mariana.Common {

    /// <summary>
    /// Provides a thread-static global instance of <see cref="Random"/> that can be used
    /// to generate random numbers.
    /// </summary>
    public static class ThreadStaticRandom {

        private static Random s_seeder = new Random();

        [ThreadStatic]
        private static Random? s_instance;

        /// <summary>
        /// Returns a thread-static global instance of <see cref="Random"/>.
        /// </summary>
        public static Random instance {
            get {
                Random? inst = s_instance;

                if (inst == null) {
                    lock (s_seeder)
                        inst = s_instance = new Random(s_seeder.Next());
                }

                return inst;
            }
        }

    }

}
