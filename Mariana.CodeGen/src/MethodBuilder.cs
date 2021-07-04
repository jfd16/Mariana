using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Mariana.CodeGen.IL;
using Mariana.Common;

namespace Mariana.CodeGen {

    /// <summary>
    /// Represents a method defined on a <see cref="TypeBuilder"/>.
    /// </summary>
    public sealed class MethodBuilder {

        private struct _Param {
            public ParameterAttributes attrs;
            public string? name;
            public object? defaultValue;
        }

        private struct _GenParam {
            public string name;
            public GenericParameterAttributes attrs;
            public EntityHandle[] constraints;
        }

        private TypeBuilder m_declType;

        private string m_name;

        private MethodAttributes m_attrs;

        private MethodImplAttributes m_implAttrs;

        private EntityHandle m_handle;

        private StringHandle m_nameHandle;

        private BlobHandle m_sigHandle;

        private int m_paramCount;

        private _Param[]? m_params;

        private _GenParam[] m_genParams;

        private int m_ilStreamLocation = -1;

        private object? m_ilMethodBody;

        private int[]? m_ilCodeVirtualTokenLocations;

        internal MethodBuilder(
            TypeBuilder declaringType,
            string name,
            MethodAttributes attrs,
            EntityHandle handle,
            StringHandle nameHandle,
            BlobHandle sigHandle,
            int paramCount,
            int genParamCount
        ) {
            m_declType = declaringType;
            m_name = name;
            m_attrs = attrs;
            m_handle = handle;
            m_nameHandle = nameHandle;
            m_sigHandle = sigHandle;
            m_paramCount = paramCount;
            m_genParams = (genParamCount != 0) ? new _GenParam[genParamCount] : Array.Empty<_GenParam>();
        }

        /// <summary>
        /// Returns the name of the method represented by this <see cref="MethodBuilder"/>.
        /// </summary>
        public string name => m_name;

        /// <summary>
        /// Returns the <see cref="MethodAttributes"/> flags that were provided when this method
        /// was defined with <see cref="TypeBuilder.defineMethod"/>.
        /// </summary>
        public MethodAttributes attributes => m_attrs;

        /// <summary>
        /// Gets or sets the <see cref="MethodImplAttributes"/> flags for the method represented
        /// by this <see cref="MethodBuilder"/>.
        /// </summary>
        public MethodImplAttributes implAttributes {
            get => m_implAttrs;
            set => m_implAttrs = value;
        }

        /// <summary>
        /// Returns a handle that can be used to refer to this method definition
        /// in IL code and other metadata definitions (such as properties and events)
        /// in the same assembly.
        /// </summary>
        /// <remarks>
        /// The value of this handle will not necessarily be the same as that in the
        /// emitted PE file. Method definition handles are patched during serialization when
        /// the MethodDef table is sorted. Once the dynamic assembly has been emitted,
        /// the real handle for the method definition can be obtained from the
        /// assembly's token map (available through
        /// <see cref="AssemblyBuilderEmitResult.tokenMapping"/>).
        /// </remarks>
        public EntityHandle handle => m_handle;

        /// <summary>
        /// Returns the <see cref="TypeBuilder"/> that defined this method.
        /// </summary>
        public TypeBuilder declaringType => m_declType;

        /// <summary>
        /// Returns a handle to the method's name in metadata.
        /// </summary>
        internal StringHandle nameHandle => m_nameHandle;

        /// <summary>
        /// Returns a handle to the method's signature in metadata.
        /// </summary>
        internal BlobHandle signatureHandle => m_sigHandle;

        /// <summary>
        /// Defines the name, attributes and constraints of a generic type parameter on the method
        /// represented by this <see cref="MethodBuilder"/>.
        /// </summary>
        /// <param name="position">The zero-based position of the type parameter.</param>
        /// <param name="name">The name of the type parameter at <paramref name="position"/>.</param>
        /// <param name="attributes">The attributes of the type parameter at <paramref name="position"/>,
        /// as a set of bit flags from <see cref="GenericParameterAttributes"/>.</param>
        /// <param name="constraints">The base class and/or interface constraints for the generic
        /// parameter at <paramref name="position"/>, as a span of handles to the constraint
        /// types. If there should not be any constraints on the type parameter, pass an empty
        /// span.</param>
        ///
        /// <exception cref="ArgumentException"><paramref name="name"/> is null or the empty string,
        /// or one of the handles in <paramref name="constraints"/> does not refer to a TypeDef,
        /// TypeRef or TypeSpec.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative, or
        /// greater than or equal to the number of generic parameters declared for this method
        /// (the type parameter count provided to <see cref="TypeBuilder.defineMethod"/>).</exception>
        public void defineGenericParameter(
            int position,
            string name,
            GenericParameterAttributes attributes = GenericParameterAttributes.None,
            ReadOnlySpan<EntityHandle> constraints = default
        ) {
            if ((uint)position >= (uint)m_genParams.Length)
                throw new ArgumentOutOfRangeException(nameof(position));

            if (name == null || name.Length == 0)
                throw new ArgumentException("Generic parameter name must not be null or an empty string.", nameof(name));

            for (int i = 0; i < constraints.Length; i++) {
                HandleKind handleKind = constraints[i].Kind;

                if (constraints[i].IsNil
                    || (handleKind != HandleKind.TypeDefinition
                        && handleKind != HandleKind.TypeReference
                        && handleKind != HandleKind.TypeSpecification))
                {
                    throw new ArgumentException(
                        "Generic parameter constraint handle must be a TypeDef, TypeRef or TypeSpec.", $"{nameof(constraints)}[{i}]");
                }
            }

            ref var genParam = ref m_genParams[position];
            genParam.name = name;
            genParam.attrs = attributes;
            genParam.constraints = constraints.IsEmpty ? Array.Empty<EntityHandle>() : constraints.ToArray();
        }

        /// <summary>
        /// Sets the name, attributes and/or the default value of a parameter of the method
        /// represented by this <see cref="MethodBuilder"/>.
        /// </summary>
        ///
        /// <param name="position">The zero-based index of the parameter, or -1 for
        /// the return parameter.</param>
        /// <param name="name">The name to set for the parameter at <paramref name="position"/>,
        /// or null if the parameter should not have a name.</param>
        /// <param name="attributes">The attributes to set for the parameter at <paramref name="position"/>.</param>
        /// <param name="defaultValue">The default value to set for the parameter at
        /// <paramref name="position"/>. Only applicable if <paramref name="attributes"/> has the
        /// <see cref="ParameterAttributes.HasDefault"/> flag set.</param>
        ///
        /// <exception cref="ArgumentException"><paramref name="name"/> is the empty string.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative, or
        /// greater than or equal to the parameter count in the method's signature.</exception>
        public void defineParameter(
            int position,
            string? name = null,
            ParameterAttributes attributes = ParameterAttributes.None,
            object? defaultValue = null
        ) {
            if ((uint)(position + 1) > (uint)m_paramCount)
                throw new ArgumentOutOfRangeException(nameof(position));

            if (name != null && name.Length == 0)
                throw new ArgumentException("Parameter name must not be an empty string.", nameof(name));

            if (name == null && attributes == ParameterAttributes.None)
                return;

            if (m_params == null)
                m_params = new _Param[m_paramCount + 1];

            ref var param = ref m_params[position + 1];
            param.name = name;
            param.attrs = attributes;
            param.defaultValue = defaultValue;
        }

        /// <summary>
        /// Sets the body of the method.
        /// </summary>
        ///
        /// <param name="body">The body of the method, in the format specified in the ECMA-335
        /// specification, II.25.4 (Common Intermediate Language physical layout).</param>
        /// <param name="virtualTokenLocations">A span containing the byte offsets in
        /// <paramref name="body"/> of virtual metadata tokens (such as those obtained
        /// from <see cref="FieldBuilder.handle" qualifyHint="true"/> or
        /// <see cref="MethodBuilder.handle" qualifyHint="true"/>)
        /// that need to be replaced with their corresponding real tokens during assembly
        /// serialization.</param>
        ///
        /// <exception cref="InvalidOperationException">This <see cref="MethodBuilder"/> represents
        /// an abstract method.</exception>
        public void setMethodBody(ReadOnlySpan<byte> body, ReadOnlySpan<int> virtualTokenLocations = default) {
            if ((attributes & (MethodAttributes.Abstract | MethodAttributes.PinvokeImpl)) != 0)
                throw new InvalidOperationException("Methods with the Abstract or PInvokeImpl flag cannot have bodies.");

            m_ilMethodBody = body.ToArray();
            m_ilCodeVirtualTokenLocations = virtualTokenLocations.ToArray();
        }

        /// <summary>
        /// Sets the body of the method.
        /// </summary>
        /// <param name="body">An <see cref="ILMethodBody"/> instance representing the method body.</param>
        ///
        /// <exception cref="ArgumentNullException"><paramref name="body"/> is null.</exception>
        /// <exception cref="InvalidOperationException">This <see cref="MethodBuilder"/> represents
        /// an abstract method.</exception>
        public void setMethodBody(ILMethodBody body) {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            if ((attributes & (MethodAttributes.Abstract | MethodAttributes.PinvokeImpl)) != 0)
                throw new InvalidOperationException("Methods with the Abstract or PInvokeImpl flag cannot have bodies.");

            m_ilMethodBody = body;
            m_ilCodeVirtualTokenLocations = null;
        }

        /// <summary>
        /// Writes the method definition (excluding generic parameters) to the dynamic assembly
        /// metadata.
        /// </summary>
        /// <param name="builder">The <see cref="MetadataBuilder"/> into which to write the method
        /// definition.</param>
        internal void writeMetadata(MetadataBuilder builder) {
            MetadataContext context = m_declType.metadataContext;

            builder.AddMethodDefinition(
                attributes, implAttributes, nameHandle, signatureHandle, m_ilStreamLocation,
                MetadataTokens.ParameterHandle(builder.GetRowCount(TableIndex.Param) + 1)
            );

            _Param[] parameters = m_params ?? Array.Empty<_Param>();

            for (int i = 0; i < parameters.Length; i++) {
                ref var p = ref parameters[i];
                if (p.name == null && p.attrs == ParameterAttributes.None)
                    continue;

                var paramHandle = builder.AddParameter(p.attrs, context.getStringHandle(p.name), i);

                if ((p.attrs & ParameterAttributes.HasDefault) != 0)
                    builder.AddConstant(paramHandle, p.defaultValue);
            }
        }

        /// <summary>
        /// Appends the definitions of the generic type parameters of this method definition
        /// to the given list.
        /// </summary>
        /// <param name="tokenMapping">The token mapping from which to obtain the real
        /// metadata handle for this method definition.</param>
        /// <param name="entriesList">The list to which to append the generic parameter entries
        /// for this method definition if it is generic.</param>
        internal void appendGenParamEntries(
            TokenMapping tokenMapping, ref DynamicArray<GenericParameter> entriesList)
        {
            _GenParam[] genParams = m_genParams;
            if (genParams.Length == 0)
                return;

            EntityHandle realMethodHandle = tokenMapping.getMappedHandle(m_handle);

            for (int i = 0; i < genParams.Length; i++) {
                ref var genParam = ref genParams[i];
                entriesList.add(new GenericParameter(realMethodHandle, i, genParam.name, genParam.attrs, genParam.constraints));
            }
        }

        /// <summary>
        /// Assigns the position at which this method's body (if any) should be written to
        /// the assembly's IL stream.
        /// </summary>
        /// <param name="currentAddress">The position at which this method's body should be written.</param>
        /// <returns>The position in the IL stream at which the next method body should be written.</returns>
        internal int assignMethodBodyAddress(int currentAddress) {
            if (m_ilMethodBody == null)
                return currentAddress;

            ILMethodBody? mb = m_ilMethodBody as ILMethodBody;
            byte[]? byteArray = m_ilMethodBody as byte[];

            if ((currentAddress & 3) != 0) {
                // Fat method header must start on a 4-byte boundary.
                bool hasFatHeader = (mb != null) ? mb.hasFatHeader : (byteArray![0] & 3) == 3;
                if (hasFatHeader)
                    currentAddress = (currentAddress & ~3) + 4;
            }

            m_ilStreamLocation = currentAddress;
            return currentAddress + ((mb != null) ? mb.byteLength : byteArray!.Length);
        }

        /// <summary>
        /// Writes the method's body to the given blob.
        /// </summary>
        /// <param name="blob">The <see cref="BlobBuilder"/> to which the method body should be
        /// written.</param>
        /// <param name="tokenMapping">The token mapping for patching virtual tokens in the IL
        /// code stream before writing it.</param>
        internal void writeMethodBody(BlobBuilder blob, TokenMapping tokenMapping) {
            if (m_ilMethodBody == null)
                return;

            // Write padding bytes.
            int currentAddress = blob.Count;
            while (currentAddress < m_ilStreamLocation) {
                blob.WriteByte(0);
                currentAddress++;
            }

            Debug.Assert(currentAddress == m_ilStreamLocation);

            if (m_ilMethodBody is ILMethodBody mb) {
                mb.writeToBlobBuilder(blob, tokenMapping);
            }
            else {
                var code = m_ilMethodBody as byte[];
                var vTokenLocations = m_ilCodeVirtualTokenLocations ?? Array.Empty<int>();

                for (int i = 0; i < vTokenLocations.Length; i++)
                    tokenMapping.patchToken(code.AsSpan(vTokenLocations[i]));

                blob.WriteBytes(code);
            }
        }

    }

}
