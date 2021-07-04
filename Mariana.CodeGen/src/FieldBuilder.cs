using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Mariana.CodeGen {

    /// <summary>
    /// Represents a field defined on a <see cref="TypeBuilder"/>.
    /// </summary>
    public sealed class FieldBuilder {

        private TypeBuilder m_declType;

        private string m_name;

        private FieldAttributes m_attrs;

        private EntityHandle m_handle;

        private StringHandle m_nameHandle;

        private BlobHandle m_sigHandle;

        private object? m_constantValue;

        internal FieldBuilder(
            TypeBuilder declaringType,
            string name,
            FieldAttributes attrs,
            EntityHandle handle,
            StringHandle nameHandle,
            BlobHandle sigHandle,
            object? constantValue
        ) {
            m_declType = declaringType;
            m_name = name;
            m_attrs = attrs;
            m_handle = handle;
            m_nameHandle = nameHandle;
            m_sigHandle = sigHandle;
            m_constantValue = constantValue;
        }

        /// <summary>
        /// Returns the name of the field.
        /// </summary>
        public string name => m_name;

        /// <summary>
        /// Returns the <see cref="FieldAttributes"/> flags that were provided when this field
        /// was defined with <see cref="TypeBuilder.defineField"/>.
        /// </summary>
        public FieldAttributes attributes => m_attrs;

        /// <summary>
        /// Returns the <see cref="TypeBuilder"/> on which this field is defined.
        /// </summary>
        public TypeBuilder declaringType => m_declType;

        /// <summary>
        /// Returns a handle that can be used to refer to this field definition
        /// in IL code and other metadata definitions in the same assembly.
        /// </summary>
        /// <remarks>
        /// The value of this handle will not necessarily be the same as that in the
        /// emitted PE file. Field definition handles are patched during serialization when
        /// the FieldDef table is sorted. Once the dynamic assembly has been emitted,
        /// the real handle for the field definition can be obtained from the
        /// assembly's token map (available through
        /// <see cref="AssemblyBuilderEmitResult.tokenMapping"/>).
        /// </remarks>
        public EntityHandle handle => m_handle;

        /// <summary>
        /// Returns a handle to the name of the field in metadata.
        /// </summary>
        internal StringHandle nameHandle => m_nameHandle;

        /// <summary>
        /// Returns a handle to the signature of the field in metadata.
        /// </summary>
        internal BlobHandle signatureHandle => m_sigHandle;

        /// <summary>
        /// Writes the field definition to the dynamic assembly metadata.
        /// </summary>
        /// <param name="builder">The <see cref="MetadataBuilder"/> into which to write the field
        /// definition.</param>
        internal void writeMetadata(MetadataBuilder builder) {
            var fieldHandle = builder.AddFieldDefinition(attributes, nameHandle, signatureHandle);

            if ((attributes & FieldAttributes.Literal) != 0)
                builder.AddConstant(fieldHandle, m_constantValue);
        }

    }

}
