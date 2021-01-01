using System;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Mariana.CodeGen {

    /// <summary>
    /// Maps metadata handles assigned to fields and methods in a dynamic assembly during
    /// creation to the actual handle values based on their locations in the emitted PE file.
    /// </summary>
    public sealed class TokenMapping {

        private int[] m_fieldDefMap;
        private int[] m_methodDefMap;

        /// <summary>
        /// Creates a new instance of <see cref="TokenMapping"/>.
        /// </summary>
        /// <param name="nFieldDefs">The total number of field definitions in the assembly.</param>
        /// <param name="nMethodDefs">The total number of method definitions in the assembly.</param>
        internal TokenMapping(int nFieldDefs, int nMethodDefs) {
            // +1 because row indices are one-based.
            m_fieldDefMap = new int[nFieldDefs + 1];
            m_methodDefMap = new int[nMethodDefs + 1];
        }

        /// <summary>
        /// Maps a virtual row number of a field definition to its actual row number.
        /// </summary>
        /// <param name="row">The virtual row number assigned when the field definition was created.</param>
        /// <param name="mappedRow">The actual row number of the field definition in the PE file.</param>
        internal void mapFieldDef(int row, int mappedRow) => m_fieldDefMap[row] = mappedRow;

        /// <summary>
        /// Maps a virtual row number of a method definition to its actual row number.
        /// </summary>
        /// <param name="row">The virtual row number assigned when the method definition was created.</param>
        /// <param name="mappedRow">The actual row number of the method definition in the PE file.</param>
        internal void mapMethodDef(int row, int mappedRow) => m_methodDefMap[row] = mappedRow;

        /// <summary>
        /// Returns a <see cref="EntityHandle"/> instance representing the actual location of the
        /// field or method whose virtual handle (obtained from <see cref="FieldBuilder.handle"/>
        /// or <see cref="MethodBuilder.handle"/>) is given.
        /// </summary>
        /// <param name="handle">The virtual handle of the field or method definition.</param>
        /// <returns>If <paramref name="handle"/> represents a field or method definition,
        /// returns the real handle for that definition; otherwise returns <paramref name="handle"/>
        /// itself.</returns>
        public EntityHandle getMappedHandle(EntityHandle handle) => MetadataTokens.EntityHandle(getMappedToken(handle));

        /// <summary>
        /// Returns a metadata token representing the actual location of the
        /// field or method whose virtual handle (obtained from <see cref="FieldBuilder.handle"/>
        /// or <see cref="MethodBuilder.handle"/>) is given.
        /// </summary>
        /// <param name="handle">The virtual handle of the field or method definition.</param>
        /// <returns>If <paramref name="handle"/> represents a field or method definition,
        /// returns the real metadata token for that definition; otherwise returns the
        /// token for <paramref name="handle"/> itself.</returns>
        public int getMappedToken(EntityHandle handle) {
            int row = MetadataTokens.GetRowNumber(handle);

            if (handle.Kind == HandleKind.FieldDefinition && (uint)row < (uint)m_fieldDefMap.Length)
                return (int)TableIndex.Field << 24 | m_fieldDefMap[row];

            if (handle.Kind == HandleKind.MethodDefinition && (uint)row < (uint)m_methodDefMap.Length)
                return (int)TableIndex.MethodDef << 24 | m_methodDefMap[row];

            return MetadataTokens.GetToken(handle);
        }

        /// <summary>
        /// Replaces a virtual metadata token in memory with its mapped token.
        /// </summary>
        /// <param name="tokenSpan">The span containing the token to be replaced.</param>
        internal void patchToken(Span<byte> tokenSpan) {
            HandleKind kind = (HandleKind)tokenSpan[3];
            int newIndex;

            if (kind == HandleKind.FieldDefinition)
                newIndex = m_fieldDefMap[tokenSpan[0] | tokenSpan[1] << 8 | tokenSpan[2] << 16];
            else if (kind == HandleKind.MethodDefinition)
                newIndex = m_methodDefMap[tokenSpan[0] | tokenSpan[1] << 8 | tokenSpan[2] << 16];
            else
                return;

            tokenSpan[0] = (byte)newIndex;
            tokenSpan[1] = (byte)(newIndex >> 8);
            tokenSpan[2] = (byte)(newIndex >> 16);
        }

    }


}
