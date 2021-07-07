using System;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using Mariana.AVM2.Core;
using Mariana.CodeGen;
using Mariana.CodeGen.IL;

namespace Mariana.AVM2.Compiler {

    /// <summary>
    /// Emits helper functions for creating arrays and objects for ABC instructions such as
    /// newarray and newobject, or for creating argument arrays for dynamic method calls.
    /// </summary>
    internal sealed class HelperEmitter {

        /// <summary>
        /// The maximum element count for the newarray instruction for which a helper can be used.
        /// </summary>
        private const int NEWARRAY_HELPER_MAX_SIZE = 32;

        /// <summary>
        /// The maximum property count for the newobject instruction for which a helper can be used.
        /// </summary>
        private const int NEWOBJECT_HELPER_MAX_SIZE = 16;

        /// <summary>
        /// The maximum element count for creating argument arrays for which a helper can be used.
        /// </summary>
        private const int ARG_ARRAY_HELPER_MAX_SIZE = 32;

        private AssemblyBuilder m_assemblyBuilder;
        private TypeBuilder? m_containerType;
        private ILBuilder m_ilBuilder;

        private MethodBuilder[] m_newArrayHelperMethods = new MethodBuilder[NEWARRAY_HELPER_MAX_SIZE + 1];
        private MethodBuilder[] m_newObjectHelperMethods = new MethodBuilder[NEWOBJECT_HELPER_MAX_SIZE + 1];
        private MethodBuilder[] m_argArrayHelperMethods = new MethodBuilder[ARG_ARRAY_HELPER_MAX_SIZE + 1];

        public HelperEmitter(AssemblyBuilder assemblyBuilder) {
            m_assemblyBuilder = assemblyBuilder;
            m_ilBuilder = new ILBuilder(assemblyBuilder.metadataContext.ilTokenProvider);
        }

        /// <summary>
        /// Returns the maximum size supported by <see cref="tryGetNewArrayHelper"/>.
        /// </summary>
        public int newArrayHelperMaxSize => NEWARRAY_HELPER_MAX_SIZE;

        /// <summary>
        /// Returns the maximum size supported by <see cref="tryGetNewObjectHelper"/>.
        /// </summary>
        public int newObjectHelperMaxSize => NEWOBJECT_HELPER_MAX_SIZE;

        public int argArrayHelperMaxSize => ARG_ARRAY_HELPER_MAX_SIZE;

        /// <summary>
        /// Returns a handle to a helper function for creating a new Array instance from the
        /// arguments on the stack. This is used for the newarray ABC instruction. The arguments
        /// on the stack must be of the "any" type.
        /// </summary>
        /// <param name="size">The size argument to the newarray instruction, i.e. the number of
        /// arguments on the stack.</param>
        /// <param name="handle">If a helper function is available or can be emitted, a handle to
        /// the function is set to this argument.</param>
        /// <returns>True if a helper function is available or can be created, otherwise false.</returns>
        public bool tryGetNewArrayHelper(int size, out EntityHandle handle) {
            handle = default;

            if (size > newArrayHelperMaxSize)
                return false;

            if (m_newArrayHelperMethods[size] != null) {
                handle = m_newArrayHelperMethods[size].handle;
                return true;
            }

            _createContainerTypeIfNotCreated();

            TypeSignature[] parameterTypes = new TypeSignature[size];
            parameterTypes.AsSpan().Fill(m_assemblyBuilder.metadataContext.getTypeSignature(typeof(ASAny)));

            string methodName = "newarray" + size.ToString(CultureInfo.InvariantCulture);

            var methodBuilder = m_containerType!.defineMethod(
                methodName,
                MethodAttributes.Public | MethodAttributes.Static,
                m_assemblyBuilder.metadataContext.getTypeSignature(typeof(ASArray)),
                parameterTypes
            );

            m_ilBuilder.reset();

            var tempVar = m_ilBuilder.declareLocal(typeof(ASArray));

            m_ilBuilder.emit(ILOp.ldc_i4, size);
            m_ilBuilder.emit(ILOp.newobj, KnownMembers.arrayCtorWithLength);
            m_ilBuilder.emit(ILOp.stloc, tempVar);

            for (int i = 0; i < size; i++) {
                m_ilBuilder.emit(ILOp.ldloc, tempVar);
                m_ilBuilder.emit(ILOp.ldc_i4, i);
                m_ilBuilder.emit(ILOp.ldarg, i);
                m_ilBuilder.emit(ILOp.call, KnownMembers.arraySetUintIndex, -3);
            }

            m_ilBuilder.emit(ILOp.ldloc, tempVar);
            m_ilBuilder.emit(ILOp.ret);

            methodBuilder.setMethodBody(m_ilBuilder.createMethodBody());

            m_newArrayHelperMethods[size] = methodBuilder;
            handle = methodBuilder.handle;

            return true;
        }

        /// <summary>
        /// Returns a handle to a helper function for creating a new Object instance from the
        /// keys and values on the stack. This is used for the newobject ABC instruction. The keys
        /// must be of the String type and the values must be of the "any" type.
        /// </summary>
        /// <param name="size">The size argument to the newarray instruction, i.e. the number of
        /// key-value pairs on the stack.</param>
        /// <param name="handle">If a helper function is available or can be emitted, a handle to
        /// the function is set to this argument.</param>
        /// <returns>True if a helper function is available or can be created, otherwise false.</returns>
        public bool tryGetNewObjectHelper(int size, out EntityHandle handle) {
            handle = default;

            if (size > newObjectHelperMaxSize)
                return false;

            if (m_newObjectHelperMethods[size] != null) {
                handle = m_newObjectHelperMethods[size].handle;
                return true;
            }

            _createContainerTypeIfNotCreated();

            TypeSignature[] parameterTypes = new TypeSignature[size * 2];

            var typeSigForString = TypeSignature.forPrimitiveType(PrimitiveTypeCode.String);
            var typeSigForAny = m_assemblyBuilder.metadataContext.getTypeSignature(typeof(ASAny));
            for (int i = 0; i < parameterTypes.Length; i++)
                parameterTypes[i] = ((i & 1) == 0) ? typeSigForString : typeSigForAny;

            string methodName = "newobject" + size.ToString(CultureInfo.InvariantCulture);

            var methodBuilder = m_containerType!.defineMethod(
                methodName,
                MethodAttributes.Public | MethodAttributes.Static,
                m_assemblyBuilder.metadataContext.getTypeSignature(typeof(ASObject)),
                parameterTypes
            );

            m_ilBuilder.reset();

            var objLocal = m_ilBuilder.declareLocal(typeof(ASObject));
            var dpLocal = m_ilBuilder.declareLocal(typeof(DynamicPropertyCollection));

            m_ilBuilder.emit(ILOp.newobj, KnownMembers.objectCtor);
            m_ilBuilder.emit(ILOp.dup);
            m_ilBuilder.emit(ILOp.call, KnownMembers.getObjectDynamicPropCollection, 0);
            m_ilBuilder.emit(ILOp.stloc, dpLocal);
            m_ilBuilder.emit(ILOp.stloc, objLocal);

            // Object properties are set in top-to-bottom stack order.
            // So for example if the arguments are "a", 1, "b", 2, "a", 3 then the value of the property
            // "a" in the created object is 1, not 3.

            for (int i = size - 1; i >= 0; i--) {
                m_ilBuilder.emit(ILOp.ldloc, dpLocal);
                m_ilBuilder.emit(ILOp.ldarg, 2 * i);
                m_ilBuilder.emit(ILOp.ldarg, 2 * i + 1);
                m_ilBuilder.emit(ILOp.ldc_i4_1);    // isEnum = true
                m_ilBuilder.emit(ILOp.call, KnownMembers.dynamicPropCollectionSet, -4);
            }

            m_ilBuilder.emit(ILOp.ldloc, objLocal);
            m_ilBuilder.emit(ILOp.ret);

            methodBuilder.setMethodBody(m_ilBuilder.createMethodBody());

            m_newObjectHelperMethods[size] = methodBuilder;
            handle = methodBuilder.handle;

            return true;
        }

        /// <summary>
        /// Returns a handle to a helper function for creating a dense array (<c>ASAny[]</c>)
        /// from the arguments on the stack. This is used when an array is needed, for example, for
        /// creating a rest argument or for a dynamic method invocation. The arguments on the stack
        /// must be of the "any" type.
        /// </summary>
        /// <param name="size">The the number of arguments on the stack from which to create an array.</param>
        /// <param name="handle">If a helper function is available or can be emitted, a handle to
        /// the function is set to this argument.</param>
        /// <returns>True if a helper function is available or can be created, otherwise false.</returns>
        public bool tryGetArgArrayHelper(int size, out EntityHandle handle) {
            handle = default;

            if (size > newArrayHelperMaxSize)
                return false;

            if (m_argArrayHelperMethods[size] != null) {
                handle = m_argArrayHelperMethods[size].handle;
                return true;
            }

            _createContainerTypeIfNotCreated();

            TypeSignature[] parameterTypes = new TypeSignature[size];
            parameterTypes.AsSpan().Fill(m_assemblyBuilder.metadataContext.getTypeSignature(typeof(ASAny)));

            string methodName = "argarray" + size.ToString(CultureInfo.InvariantCulture);

            var methodBuilder = m_containerType!.defineMethod(
                methodName,
                MethodAttributes.Public | MethodAttributes.Static,
                m_assemblyBuilder.metadataContext.getTypeSignature(typeof(ASAny[])),
                parameterTypes
            );

            m_ilBuilder.reset();

            var tempVar = m_ilBuilder.declareLocal(typeof(ASAny[]));

            m_ilBuilder.emit(ILOp.ldc_i4, size);
            m_ilBuilder.emit(ILOp.newarr, typeof(ASAny));
            m_ilBuilder.emit(ILOp.stloc, tempVar);

            for (int i = 0; i < size; i++) {
                m_ilBuilder.emit(ILOp.ldloc, tempVar);
                m_ilBuilder.emit(ILOp.ldc_i4, i);
                m_ilBuilder.emit(ILOp.ldarg, i);
                m_ilBuilder.emit(ILOp.stelem, typeof(ASAny));
            }

            m_ilBuilder.emit(ILOp.ldloc, tempVar);
            m_ilBuilder.emit(ILOp.ret);

            methodBuilder.setMethodBody(m_ilBuilder.createMethodBody());

            m_argArrayHelperMethods[size] = methodBuilder;
            handle = methodBuilder.handle;

            return true;
        }

        private void _createContainerTypeIfNotCreated() {
            if (m_containerType != null)
                return;

            m_containerType = m_assemblyBuilder.defineType(
                new TypeName(NameMangler.INTERNAL_NAMESPACE, "Helpers"),
                TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.NotPublic
            );
        }

    }

}
