using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;

namespace Mariana.CodeGen.IL {

    /// <summary>
    /// Provides metadata tokens for referring to types, members and strings in
    /// emitted IL.
    /// </summary>
    public abstract class ILTokenProvider {

        /// <summary>
        /// Returns the metadata handle for the given type.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> instance representing the type for which
        /// to obtain a metadata handle to refer to it in emitted IL.</param>
        public abstract EntityHandle getHandle(Type type);

        /// <summary>
        /// Returns the metadata handle for the given field.
        /// </summary>
        /// <param name="field">The <see cref="FieldInfo"/> instance representing the field for which
        /// to obtain a metadata handle to refer to it in emitted IL.</param>
        public abstract EntityHandle getHandle(FieldInfo field);

        /// <summary>
        /// Returns the metadata handle for the given method.
        /// </summary>
        /// <param name="method">The <see cref="MethodBase"/> instance representing the method for which
        /// to obtain a metadata handle to refer to it in emitted IL.</param>
        public abstract EntityHandle getHandle(MethodBase method);

        /// <summary>
        /// Returns the metadata handle for the given string.
        /// </summary>
        /// <param name="str">The string for which to obtain a metadata handle to refer to it in emitted IL.</param>
        public abstract UserStringHandle getHandle(string str);

        /// <summary>
        /// Returns the metadata handle for the given type signature.
        /// </summary>
        /// <param name="type">The type signature for which to return obtain a metadata handle.</param>
        public abstract EntityHandle getHandle(in TypeSignature type);

        /// <summary>
        /// Returns the stack delta for the method with the given handle when used as the operand to the
        /// given opcode.
        /// </summary>
        /// <param name="handle">The method's metadata handle.</param>
        /// <param name="opcode">The opcode for which the method is used as an operand. This is
        /// one of the following: call, callvirt or newobj.</param>
        /// <returns>The stack delta for the method (that is, the change in the stack height when
        /// the instruction given by <paramref name="opcode"/> is used with the method).</returns>
        public abstract int getMethodStackDelta(EntityHandle handle, ILOp opcode);

        /// <summary>
        /// Returns true if the given metadata handle is a virtual handle which may need to
        /// be patched during code serialization.
        /// </summary>
        /// <param name="handle">A metadata handle.</param>
        /// <returns>True if the handle is a virtual handle, false otherwise.</returns>
        public abstract bool isVirtualHandle(EntityHandle handle);

        /// <summary>
        /// Returns the type signature for the given type.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> instance representing the type for which
        /// to obtain a type signature.</param>
        public abstract TypeSignature getTypeSignature(Type type);

        /// <summary>
        /// Returns true if local type signatures should be generated using <see cref="SignatureHelper"/>
        /// instead of generating the signature directly from type signatures provided by
        /// <see cref="getTypeSignature"/>.
        /// </summary>
        public abstract bool useLocalSigHelper { get; }

        /// <summary>
        /// Returns a handle for a method's local variable signature.
        /// </summary>
        /// <param name="localSig">The local signature for which to obtain a metadata handle.</param>
        public abstract StandaloneSignatureHandle getLocalSignatureHandle(ReadOnlySpan<byte> localSig);

    }

}
