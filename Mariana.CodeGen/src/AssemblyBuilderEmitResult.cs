using System;
using System.Reflection;

namespace Mariana.CodeGen {

    /// <summary>
    /// Represents the result of serializing an <see cref="AssemblyBuilder"/>.
    /// </summary>
    public readonly struct AssemblyBuilderEmitResult {

        private readonly byte[] m_peFile;
        private readonly TokenMapping m_tokenMapping;

        internal AssemblyBuilderEmitResult(byte[] peFile, TokenMapping tokenMapping) {
            m_peFile = peFile;
            m_tokenMapping = tokenMapping;
        }

        /// <summary>
        /// Returns a byte array containing the Portable Executable (PE) image for the
        /// emitted dynamic assembly.
        /// </summary>
        /// <remarks>
        /// The image can be loaded into the runtime using <see cref="Assembly.Load(Byte[])"/>
        /// or saved to a .dll file on disk.
        /// </remarks>
        public byte[] peImageBytes => m_peFile;

        /// <summary>
        /// Returns the <see cref="TokenMapping"/> instance that maps virtual tokens of field
        /// and method definitions in the dynamic assembly to their actual tokens in the PE image.
        /// </summary>
        public TokenMapping tokenMapping => m_tokenMapping;

    }

}
