using System;
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
        private static DynamicMethodTokenProvider s_threadTokenProvider;

        [ThreadStatic]
        private static ILBuilder s_threadIlBuilder;

        private static ILBuilder _getILBuilder(DynamicILInfo dynamicILInfo) {
            ref DynamicMethodTokenProvider tokenProvider = ref s_threadTokenProvider;

            if (tokenProvider == null)
                tokenProvider = new DynamicMethodTokenProvider(dynamicILInfo);
            else
                tokenProvider.setDynamicILInfo(dynamicILInfo);

            ref ILBuilder builder = ref s_threadIlBuilder;
            if (builder == null)
                builder = new ILBuilder(tokenProvider);

            return builder;
        }

        public static FieldStub generateFieldStub(FieldTrait field) {
            string methodName = _createStubName(field.name, field.declaringClass);
            Type returnType = typeof(ASAny);
            Type[] paramTypes = new[] {typeof(ASAny), typeof(ASAny), typeof(bool)};

            var dynMethod = new DynamicMethod(methodName, returnType, paramTypes, field.underlyingFieldInfo.DeclaringType);
            var dynILInfo = dynMethod.GetDynamicILInfo();
            var ilWriter = _getILBuilder(dynILInfo);
            _generateFieldStubInternal(field, _getILBuilder(dynILInfo));
            ilWriter.createMethodBody().bindToDynamicILInfo(dynILInfo);

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

        public static MethodStub generateMethodStub(MethodTrait method) {
            string methodName = _createStubName(method.name, method.declaringClass);
            Type returnType = typeof(ASAny);
            Type[] paramTypes = new[] {typeof(ASAny), typeof(ReadOnlySpan<ASAny>)};

            var dynMethod = new DynamicMethod(methodName, returnType, paramTypes, method.underlyingMethodInfo.DeclaringType);
            var dynILInfo = dynMethod.GetDynamicILInfo();
            var ilBuilder = _getILBuilder(dynILInfo);

            Class recvType = method.isStatic ? null : method.declaringClass;

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
            return ReflectUtil.makeDelegate<MethodStub>(dynMethod);
        }

        public static MethodStub generateCtorStub(ClassConstructor ctor) {
            string methodName = _createStubName(ctor.declaringClass.name, ctor.declaringClass);
            Type returnType = typeof(ASAny);
            Type[] paramTypes = new[] {typeof(ASAny), typeof(ReadOnlySpan<ASAny>)};

            var dynMethod = new DynamicMethod(methodName, returnType, paramTypes, ctor.declaringClass.underlyingType);
            var dynILInfo = dynMethod.GetDynamicILInfo();
            var ilBuilder = _getILBuilder(dynILInfo);

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
            return ReflectUtil.makeDelegate<MethodStub>(dynMethod);
        }

        private static void _generateMethodStubInternal(
            MethodBase methodOrCtor,
            Class receiverType,
            bool hasReturn,
            Class returnType,
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
                ILEmitHelper.emitTypeCoerce(builder, null, receiverType);
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

                var lbl1 = builder.createLabel();
                var lbl2 = builder.createLabel();

                // If i >= argsLengthLocal then goto lbl1
                builder.emit(ILOp.ldc_i4, i);
                builder.emit(ILOp.ldloc, argsLengthLocal);
                builder.emit(ILOp.bge, lbl1);

                // Push argument from arguments array
                builder.emit(ILOp.ldarga, 1);
                builder.emit(ILOp.ldc_i4, i);
                builder.emit(ILOp.call, KnownMembers.roSpanOfAnyGet, -1);
                builder.emit(ILOp.ldobj, typeof(ASAny));
                ILEmitHelper.emitTypeCoerce(builder, null, p.type);

                if (!p.hasDefault)
                    builder.emit(ILOp.newobj, ILEmitHelper.getOptionalParamCtor(p.type), 0);

                builder.emit(ILOp.br, lbl2);

                builder.markLabel(lbl1);

                // Push the default value (or OptionalParam.unspecified).
                if (p.hasDefault) {
                    if (p.type == null)
                        ILEmitHelper.emitPushConstantAsAny(builder, p.defaultValue);
                    else if (p.type.underlyingType == (object)typeof(ASObject))
                        ILEmitHelper.emitPushConstantAsObject(builder, p.defaultValue);
                    else
                        ILEmitHelper.emitPushConstant(builder, p.defaultValue);
                }
                else {
                    builder.emit(ILOp.ldsfld, ILEmitHelper.getOptionalParamUnspecifiedField(p.type));
                }

                builder.markLabel(lbl2);
            }

            if (hasRest) {
                // Push the rest argument.
                var lbl1 = builder.createLabel();
                var lbl2 = builder.createLabel();

                builder.emit(ILOp.ldloc, argsLengthLocal);
                builder.emit(ILOp.ldc_i4, paramCount);
                builder.emit(ILOp.ble_un, lbl1);

                builder.emit(ILOp.ldarga, 1);
                builder.emit(ILOp.ldc_i4, paramCount);
                builder.emit(ILOp.call, KnownMembers.roSpanOfAnySlice, -1);
                builder.emit(ILOp.newobj, KnownMembers.restParamFromSpan, 0);
                builder.emit(ILOp.br, lbl2);

                builder.markLabel(lbl1);
                builder.emit(ILOp.call, KnownMembers.roSpanOfAnyEmpty);

                builder.markLabel(lbl2);
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

        private static string _createStubName(QName traitName, Class declClass) {
            return "RuntimeDispatchStub"
                + s_counter.atomicNext().ToString(CultureInfo.InvariantCulture)
                + "{"
                + ((declClass != null) ? declClass.name.ToString() + "/" : "")
                + traitName.ToString()
                + "}";
        }

    }

}
