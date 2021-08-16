using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using Mariana.CodeGen.IL;
using Mariana.Common;

using ILEmitHelper = Mariana.AVM2.Compiler.ILEmitHelper;
using KnownMembers = Mariana.AVM2.Compiler.KnownMembers;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Generates dispatch stubs for dynamic trait invocation.
    /// </summary>
    internal static class RuntimeDispatch {

        /// <summary>
        /// A delegate for a runtime dispatch stub that gets or sets a field.
        /// </summary>
        /// <param name="receiver">The object on which to get/set the field, ignored for
        /// static fields.</param>
        /// <param name="value">The value to set to the field. Ignored for a get operation.</param>
        /// <param name="set">True to get the value, false to set.</param>
        /// <returns>The value of the field, or undefined if <paramref name="set"/> is true.</returns>
        public delegate ASAny FieldStub(ASAny receiver, ASAny value, bool set);

        /// <summary>
        /// A delegate for a runtime dispatch stub that calls a method or constructor.
        /// </summary>
        /// <param name="receiver">The object on which to call the method, ignored for
        /// static methods and constructors.</param>
        /// <param name="args">The arguments to pass to the method/constructor call.</param>
        /// <returns>The return value of the method or constructor call.</returns>
        public delegate ASAny MethodStub(ASAny receiver, ReadOnlySpan<ASAny> args);

        private static readonly IncrementCounter s_counter = new IncrementCounter();

        [ThreadStatic]
        private static DynamicMethodTokenProvider? s_threadTokenProvider;

        [ThreadStatic]
        private static ILBuilder? s_threadIlBuilder;

        // Mono's implementation of DynamicILInfo (as of version 6.12.0) is incomplete, so
        // check if we are running on a Mono runtime with an incomplete DynamicILInfo implementation
        // and fallback to ILGenerator in this case.

        private static readonly bool s_isDynamicILInfoFullyImplemented = _checkIfDynamicILInfoFullyImplemented();

        private static bool _checkIfDynamicILInfoFullyImplemented() {
            try {
                var dynamicMethod = new DynamicMethod("_", typeof(void), Array.Empty<Type>(), typeof(RuntimeDispatch));
                var dynamicILInfo = dynamicMethod.GetDynamicILInfo();

                // Should support getting tokens for methods on generic types
                var genericTypeMethod = typeof(Tuple<int, int>).GetProperty(nameof(Tuple<int, int>.Item1)).GetMethod;
                dynamicILInfo.GetTokenFor(genericTypeMethod.MethodHandle, genericTypeMethod.DeclaringType.TypeHandle);

                // Should support getting tokens for fields on generic types
                var genericTypeField = typeof(ValueTuple<int, int>).GetField(nameof(ValueTuple<int, int>.Item1));
                dynamicILInfo.GetTokenFor(genericTypeField.FieldHandle, genericTypeField.DeclaringType.TypeHandle);

                // Should support SetLocalSignature
                dynamicILInfo.SetLocalSignature(new byte[] {7, 0});

                return true;
            }
            catch (NotImplementedException) {
                return false;
            }
        }

        private static ILBuilder _getILBuilder(DynamicILInfo dynamicILInfo, int paramCount = 0) {
            Debug.Assert(s_isDynamicILInfoFullyImplemented);

            ref DynamicMethodTokenProvider? tokenProvider = ref s_threadTokenProvider;

            if (tokenProvider == null)
                tokenProvider = new DynamicMethodTokenProvider(dynamicILInfo);
            else
                tokenProvider.setDynamicILInfo(dynamicILInfo);

            if (paramCount > 10) {
                // We don't use the thread-static ILBuilder when there are a large number of
                // parameters so that it doesn't leak a large amount of memory. Functions with
                // these many parameters are usually rare so the additional allocations are not
                // much of an issue.
                return new ILBuilder(tokenProvider);
            }

            ref ILBuilder? builder = ref s_threadIlBuilder;
            if (builder == null)
                builder = new ILBuilder(tokenProvider);

            return builder;
        }

        public static FieldStub generateFieldStub(FieldTrait field) {
            string methodName = _createStubName(field.name, field.declaringClass);
            Type returnType = typeof(ASAny);
            Type[] paramTypes = new[] {typeof(ASAny), typeof(ASAny), typeof(bool)};

            var dynMethod = new DynamicMethod(methodName, returnType, paramTypes, field.underlyingFieldInfo.DeclaringType);

            if (s_isDynamicILInfoFullyImplemented) {
                var dynILInfo = dynMethod.GetDynamicILInfo();
                var ilBuilder = _getILBuilder(dynILInfo);
                _generateFieldStubInternal(field, ilBuilder);
                ilBuilder.createMethodBody().bindToDynamicILInfo(dynILInfo);
            }
            else {
                _generateFieldStubInternal(field, dynMethod.GetILGenerator());
            }

            return ReflectUtil.makeDelegate<FieldStub>(dynMethod);
        }

        private static void _generateFieldStubInternal(FieldTrait field, ILBuilder builder) {
            bool isReadOnly = field.underlyingFieldInfo.IsInitOnly;
            bool isStatic = field.isStatic;

            if (!isStatic) {
                // Push target object.
                builder.emit(ILOp.ldarg, 0);
                ILEmitHelper.emitTypeCoerce(builder, null, field.declaringClass);
            }

            // If the field is not read-only then we need to decide whether
            // to read or write depending on the read/write argument (the third one)
            ILBuilder.Label branchToSet = default;
            if (!isReadOnly) {
                branchToSet = builder.createLabel();
                builder.emit(ILOp.ldarg, 2);
                builder.emit(ILOp.brtrue, branchToSet);
            }

            if (isStatic)
                builder.emit(ILOp.ldsfld, field.underlyingFieldInfo);
            else
                builder.emit(ILOp.ldfld, field.underlyingFieldInfo);

            ILEmitHelper.emitTypeCoerceToAny(builder, field.fieldType);
            builder.emit(ILOp.ret);

            if (!isReadOnly) {
                builder.markLabel(branchToSet);
                builder.emit(ILOp.ldarg, 1);
                ILEmitHelper.emitTypeCoerce(builder, null, field.fieldType);

                if (isStatic)
                    builder.emit(ILOp.stsfld, field.underlyingFieldInfo);
                else
                    builder.emit(ILOp.stfld, field.underlyingFieldInfo);

                ILEmitHelper.emitPushConstant(builder, ASAny.undefined);
                builder.emit(ILOp.ret);
            }
        }

        private static void _generateFieldStubInternal(FieldTrait field, ILGenerator generator) {
            bool isReadOnly = field.underlyingFieldInfo.IsInitOnly;
            bool isStatic = field.isStatic;

            if (!isStatic) {
                // Push target object.
                generator.Emit(OpCodes.Ldarg_0);
                ILEmitHelper.emitTypeCoerce(generator, null, field.declaringClass);
            }

            // If the field is not read-only then we need to decide whether
            // to read or write depending on the read/write argument (the third one)
            Label branchToSet = default;
            if (!isReadOnly) {
                branchToSet = generator.DefineLabel();
                generator.Emit(OpCodes.Ldarg, 2);
                generator.Emit(OpCodes.Brtrue, branchToSet);
            }

            if (isStatic)
                generator.Emit(OpCodes.Ldsfld, field.underlyingFieldInfo);
            else
                generator.Emit(OpCodes.Ldfld, field.underlyingFieldInfo);

            ILEmitHelper.emitTypeCoerceToAny(generator, field.fieldType);
            generator.Emit(OpCodes.Ret);

            if (!isReadOnly) {
                generator.MarkLabel(branchToSet);
                generator.Emit(OpCodes.Ldarg, 1);
                ILEmitHelper.emitTypeCoerce(generator, null, field.fieldType);

                if (isStatic)
                    generator.Emit(OpCodes.Stsfld, field.underlyingFieldInfo);
                else
                    generator.Emit(OpCodes.Stfld, field.underlyingFieldInfo);

                ILEmitHelper.emitPushConstant(generator, ASAny.undefined);
                generator.Emit(OpCodes.Ret);
            }
        }

        public static MethodStub generateMethodStub(MethodTrait method) {
            string methodName = _createStubName(method.name, method.declaringClass);
            Type returnType = typeof(ASAny);
            Type[] paramTypes = new[] {typeof(ASAny), typeof(ReadOnlySpan<ASAny>)};

            var dynMethod = new DynamicMethod(methodName, returnType, paramTypes, method.underlyingMethodInfo.DeclaringType);

            Class? recvType = method.isStatic ? null : method.declaringClass;

            if (s_isDynamicILInfoFullyImplemented) {
                var dynILInfo = dynMethod.GetDynamicILInfo();
                var ilBuilder = _getILBuilder(dynILInfo, method.paramCount);

                _generateMethodStubInternal(
                    method.underlyingMethodInfo,
                    recvType,
                    method.hasReturn,
                    method.returnType,
                    method.getParameters(),
                    method.hasRest,
                    ilBuilder
                );
                ilBuilder.createMethodBody().bindToDynamicILInfo(dynILInfo);
            }
            else {
                _generateMethodStubInternal(
                    method.underlyingMethodInfo,
                    recvType,
                    method.hasReturn,
                    method.returnType,
                    method.getParameters(),
                    method.hasRest,
                    dynMethod.GetILGenerator()
                );
            }

            return ReflectUtil.makeDelegate<MethodStub>(dynMethod);
        }

        public static MethodStub generateCtorStub(ClassConstructor ctor) {
            string methodName = _createStubName(ctor.declaringClass.name, ctor.declaringClass);
            Type returnType = typeof(ASAny);
            Type[] paramTypes = new[] {typeof(ASAny), typeof(ReadOnlySpan<ASAny>)};

            var dynMethod = new DynamicMethod(methodName, returnType, paramTypes, ctor.declaringClass.underlyingType);

            if (s_isDynamicILInfoFullyImplemented) {
                var dynILInfo = dynMethod.GetDynamicILInfo();
                var ilBuilder = _getILBuilder(dynILInfo, ctor.paramCount);

                _generateMethodStubInternal(
                    ctor.underlyingConstructorInfo,
                    receiverType: null,
                    hasReturn: true,
                    returnType: ctor.declaringClass,
                    parameters: ctor.getParameters(),
                    hasRest: ctor.hasRest,
                    builder: ilBuilder
                );

                ilBuilder.createMethodBody().bindToDynamicILInfo(dynILInfo);
            }
            else {
                _generateMethodStubInternal(
                    ctor.underlyingConstructorInfo,
                    receiverType: null,
                    hasReturn: true,
                    returnType: ctor.declaringClass,
                    parameters: ctor.getParameters(),
                    hasRest: ctor.hasRest,
                    generator: dynMethod.GetILGenerator()
                );
            }

            return ReflectUtil.makeDelegate<MethodStub>(dynMethod);
        }

        private static void _generateMethodStubInternal(
            MethodBase methodOrCtor,
            Class? receiverType,
            bool hasReturn,
            Class? returnType,
            ReadOnlyArrayView<MethodTraitParameter> parameters,
            bool hasRest,
            ILBuilder builder
        ) {
            var argsLengthLocal = builder.declareLocal(typeof(int));
            builder.emit(ILOp.ldarga, 1);
            builder.emit(ILOp.call, KnownMembers.roSpanOfAnyLength, 0);
            builder.emit(ILOp.stloc, argsLengthLocal);

            int paramCount = parameters.length;

            if (receiverType != null) {
                // Not a static method or constructor.
                builder.emit(ILOp.ldarg_0);
                builder.emit(ILOp.call, ILEmitHelper.getAnyCastMethod(receiverType));
            }

            // Load formal arguments.
            for (int i = 0; i < paramCount; i++) {
                MethodTraitParameter p = parameters[i];

                if (!p.isOptional) {
                    builder.emit(ILOp.ldarga, 1);
                    builder.emit(ILOp.ldc_i4, i);
                    builder.emit(ILOp.call, KnownMembers.roSpanOfAnyGet, -1);
                    builder.emit(ILOp.ldobj, typeof(ASAny));
                    ILEmitHelper.emitTypeCoerce(builder, null, p.type);
                    continue;
                }

                var label1 = builder.createLabel();
                var label2 = builder.createLabel();

                // If i >= argsLengthLocal then goto label1
                builder.emit(ILOp.ldc_i4, i);
                builder.emit(ILOp.ldloc, argsLengthLocal);
                builder.emit(ILOp.bge, label1);

                // Push argument from arguments array
                builder.emit(ILOp.ldarga, 1);
                builder.emit(ILOp.ldc_i4, i);
                builder.emit(ILOp.call, KnownMembers.roSpanOfAnyGet, -1);
                builder.emit(ILOp.ldobj, typeof(ASAny));
                ILEmitHelper.emitTypeCoerce(builder, null, p.type);

                if (!p.hasDefault)
                    builder.emit(ILOp.newobj, ILEmitHelper.getOptionalParamCtor(p.type), 0);

                builder.emit(ILOp.br, label2);

                builder.markLabel(label1);

                // Push the default value (or OptionalParam.missing).
                if (p.hasDefault)
                    ILEmitHelper.emitPushConstantAsType(builder, p.defaultValue, p.type);
                else
                    builder.emit(ILOp.ldsfld, ILEmitHelper.getOptionalParamMissingField(p.type));

                builder.markLabel(label2);
            }

            if (hasRest) {
                // Push the rest argument.
                var label1 = builder.createLabel();
                var label2 = builder.createLabel();

                builder.emit(ILOp.ldloc, argsLengthLocal);
                builder.emit(ILOp.ldc_i4, paramCount);
                builder.emit(ILOp.ble_un, label1);

                builder.emit(ILOp.ldarga, 1);
                builder.emit(ILOp.ldc_i4, paramCount);
                builder.emit(ILOp.call, KnownMembers.roSpanOfAnySlice, -1);
                builder.emit(ILOp.newobj, KnownMembers.restParamFromSpan, 0);
                builder.emit(ILOp.br, label2);

                builder.markLabel(label1);
                builder.emit(ILOp.call, KnownMembers.roSpanOfAnyEmpty);

                builder.markLabel(label2);
            }

            // Call the method or constructor
            if (methodOrCtor is ConstructorInfo ctorInfo)
                builder.emit(ILOp.newobj, ctorInfo);
            else
                builder.emit((receiverType == null) ? ILOp.call : ILOp.callvirt, (MethodInfo)methodOrCtor);

            // Return.
            if (hasReturn)
                ILEmitHelper.emitTypeCoerceToAny(builder, returnType);
            else
                ILEmitHelper.emitPushConstant(builder, ASAny.undefined);

            builder.emit(ILOp.ret);
        }

        private static void _generateMethodStubInternal(
            MethodBase methodOrCtor,
            Class? receiverType,
            bool hasReturn,
            Class? returnType,
            ReadOnlyArrayView<MethodTraitParameter> parameters,
            bool hasRest,
            ILGenerator generator
        ) {
            var argsLengthLocal = generator.DeclareLocal(typeof(int));
            generator.Emit(OpCodes.Ldarga, 1);
            generator.Emit(OpCodes.Call, KnownMembers.roSpanOfAnyLength);
            generator.Emit(OpCodes.Stloc, argsLengthLocal);

            int paramCount = parameters.length;

            if (receiverType != null) {
                // Not a static method or constructor.
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Call, ILEmitHelper.getAnyCastMethod(receiverType));
            }

            // Load formal arguments.
            for (int i = 0; i < paramCount; i++) {
                MethodTraitParameter p = parameters[i];

                if (!p.isOptional) {
                    generator.Emit(OpCodes.Ldarga, 1);
                    generator.Emit(OpCodes.Ldc_I4, i);
                    generator.Emit(OpCodes.Call, KnownMembers.roSpanOfAnyGet);
                    generator.Emit(OpCodes.Ldobj, typeof(ASAny));
                    ILEmitHelper.emitTypeCoerce(generator, null, p.type);
                    continue;
                }

                var label1 = generator.DefineLabel();
                var label2 = generator.DefineLabel();

                // If i >= argsLengthLocal then goto label1
                generator.Emit(OpCodes.Ldc_I4, i);
                generator.Emit(OpCodes.Ldloc, argsLengthLocal);
                generator.Emit(OpCodes.Bge, label1);

                // Push argument from arguments array
                generator.Emit(OpCodes.Ldarga, 1);
                generator.Emit(OpCodes.Ldc_I4, i);
                generator.Emit(OpCodes.Call, KnownMembers.roSpanOfAnyGet);
                generator.Emit(OpCodes.Ldobj, typeof(ASAny));
                ILEmitHelper.emitTypeCoerce(generator, null, p.type);

                if (!p.hasDefault)
                    generator.Emit(OpCodes.Newobj, ILEmitHelper.getOptionalParamCtor(p.type));

                generator.Emit(OpCodes.Br, label2);

                generator.MarkLabel(label1);

                // Push the default value (or OptionalParam.missing).
                if (p.hasDefault)
                    ILEmitHelper.emitPushConstantAsType(generator, p.defaultValue, p.type);
                else
                    generator.Emit(OpCodes.Ldsfld, ILEmitHelper.getOptionalParamMissingField(p.type));

                generator.MarkLabel(label2);
            }

            if (hasRest) {
                // Push the rest argument.
                var label1 = generator.DefineLabel();
                var label2 = generator.DefineLabel();

                generator.Emit(OpCodes.Ldloc, argsLengthLocal);
                generator.Emit(OpCodes.Ldc_I4, paramCount);
                generator.Emit(OpCodes.Ble_Un, label1);

                generator.Emit(OpCodes.Ldarga, 1);
                generator.Emit(OpCodes.Ldc_I4, paramCount);
                generator.Emit(OpCodes.Call, KnownMembers.roSpanOfAnySlice);
                generator.Emit(OpCodes.Newobj, KnownMembers.restParamFromSpan);
                generator.Emit(OpCodes.Br, label2);

                generator.MarkLabel(label1);
                generator.Emit(OpCodes.Call, KnownMembers.roSpanOfAnyEmpty);

                generator.MarkLabel(label2);
            }

            // Call the method or constructor
            if (methodOrCtor is ConstructorInfo ctorInfo)
                generator.Emit(OpCodes.Newobj, ctorInfo);
            else
                generator.Emit((receiverType == null) ? OpCodes.Call : OpCodes.Callvirt, (MethodInfo)methodOrCtor);

            // Return.
            if (hasReturn)
                ILEmitHelper.emitTypeCoerceToAny(generator, returnType);
            else
                ILEmitHelper.emitPushConstant(generator, ASAny.undefined);

            generator.Emit(OpCodes.Ret);
        }

        private static string _createStubName(QName traitName, Class? declClass) {
            return "RuntimeDispatchStub"
                + s_counter.atomicNext().ToString(CultureInfo.InvariantCulture)
                + "{"
                + ((declClass != null) ? declClass.name.ToString() + "/" : "")
                + traitName.ToString()
                + "}";
        }

    }

}
