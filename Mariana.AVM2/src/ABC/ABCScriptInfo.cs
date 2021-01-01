using System;
using Mariana.Common;

namespace Mariana.AVM2.ABC {

    /// <summary>
    /// Represents a script defined in an ABC file.
    /// </summary>
    public sealed class ABCScriptInfo {

        private int m_abcIndex;

        private ABCMethodInfo m_init;

        private ABCTraitInfo[] m_traits;

        /// <summary>
        /// Gets the index of the script entry in the ABC file metadata.
        /// </summary>
        public int abcIndex => m_abcIndex;

        /// <summary>
        /// Gets the <see cref="ABCMethodInfo"/> instance representing the script's initializer.
        /// </summary>
        /// <value>The <see cref="ABCMethodInfo"/> instance representing the script's initializer.
        /// This is the method that is called when the script is run.</value>
        public ABCMethodInfo initMethod => m_init;

        /// <summary>
        /// Gets a read-only array view containing <see cref="ABCTraitInfo"/> instances representing the traits
        /// defined by this script.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{ABCTraitInfo}"/> containing objects representing
        /// the traits defined by this script.</returns>
        public ReadOnlyArrayView<ABCTraitInfo> getTraits() => new ReadOnlyArrayView<ABCTraitInfo>(m_traits);

        internal ABCScriptInfo(int abcIndex, ABCMethodInfo init, ABCTraitInfo[] traits) {
            m_abcIndex = abcIndex;
            m_init = init;
            m_traits = traits;
        }

    }

}
