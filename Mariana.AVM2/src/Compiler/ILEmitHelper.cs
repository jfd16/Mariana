using System;
using System.Reflection;
using Mariana.CodeGen.IL;
using Mariana.AVM2.Core;
using System.Runtime.CompilerServices;

namespace Mariana.AVM2.Compiler {

    internal static class ILEmitHelper {

        private static readonly ConditionalWeakTable<Type, MethodInfo> s_objectCastMethods =
            new ConditionalWeakTable<Type, MethodInfo>();

        private static readonly ConditionalWeakTable<Type, MethodInfo> s_anyCastMethods =
            new ConditionalWeakTable<Type, MethodInfo>();

        private static readonly ConditionalWeakTable<Type, ConstructorInfo> s_optionalParamCtors =
            new ConditionalWeakTable<Type, ConstructorInfo>();

        private static readonly ConditionalWeakTable<Type, FieldInfo> s_optionalParamUnspecified =
            new ConditionalWeakTable<Type, FieldInfo>();

        private static readonly Class s_objectClass = Class.fromType(typeof(ASObject));

        /// <summary>
        /// Emits IL instructions to convert the value on top of the stack from one type to another.
        /// </summary>
        /// <param name="builder">The <see cref="ILBuilder"/> instance in which to emit the
        /// code.</param>
        /// <param name="fromType">The type of the value on top of the stack.</param>
        /// <param name="toType">The type to convert the value to.</param>
        public static void emitTypeCoerce(ILBuilder builder, Class fromType, Class toType) {
            if (fromType == toType)
                return;

            if (toType == null) {
                emitTypeCoerceToAny(builder, fromType);
                return;
            }

            switch (toType.tag) {
                case ClassTag.INT:
                    emitTypeCoerceToInt(builder, fromType);
                    return;
                case ClassTag.UINT:
                    emitTypeCoerceToUint(builder, fromType);
                    return;
                case ClassTag.NUMBER:
                    emitTypeCoerceToNumber(builder, fromType);
                    return;
                case ClassTag.BOOLEAN:
                    emitTypeCoerceToBoolean(builder, fromType);
                    return;
                case ClassTag.STRING:
                    emitTypeCoerceToString(builder, fromType);
                    return;
            }

            // Target type is an object type.

            if (fromType == null) {
                if (toType == s_objectClass) {
                    emitTypeCoerceToObject(builder, fromType);
                }
                else {
                    MethodInfo castMethod = getAnyCastMethod(toType);
                    builder.emit(ILOp.call, castMethod, 0);
                }
                return;
            }

            emitTypeCoerceToObject(builder, fromType);

            if (!fromType.canAssignTo(toType)) {
                MethodInfo castMethod = getObjectCastMethod(toType);
                builder.emit(ILOp.call, castMethod, 0);
            }
        }

        /// <summary>
        /// Emits IL instructions to convert the value on top of the stack to the "any" type.
        /// </summary>
        /// <param name="builder">The <see cref="ILBuilder"/> instance in which to emit the
        /// code.</param>
        /// <param name="fromType">The type of the value on top of the stack.</param>
        public static void emitTypeCoerceToAny(ILBuilder builder, Class fromType) {
            if (fromType == null)   // No-op
                return;

            switch (fromType.tag) {
                case ClassTag.INT:
                    builder.emit(ILOp.call, KnownMembers.intToAny, 0);
                    break;
                case ClassTag.UINT:
                    builder.emit(ILOp.call, KnownMembers.uintToAny, 0);
                    break;
                case ClassTag.NUMBER:
                    builder.emit(ILOp.call, KnownMembers.numberToAny, 0);
                    break;
                case ClassTag.STRING:
                    builder.emit(ILOp.call, KnownMembers.stringToAny, 0);
                    break;
                case ClassTag.BOOLEAN:
                    builder.emit(ILOp.call, KnownMembers.boolToAny, 0);
                    break;
                default:
                    if (fromType.isInterface)
                        builder.emit(ILOp.castclass, typeof(ASObject));
                    builder.emit(ILOp.call, KnownMembers.anyFromObject, 0);
                    break;
            }
        }

        /// <summary>
        /// Emits IL instructions to convert the value on top of the stack to the int type.
        /// </summary>
        /// <param name="builder">The <see cref="ILBuilder"/> instance in which to emit the
        /// code.</param>
        /// <param name="fromType">The type of the value on top of the stack.</param>
        public static void emitTypeCoerceToInt(ILBuilder builder, Class fromType) {
            if (fromType == null) {
                builder.emit(ILOp.call, KnownMembers.anyToInt, 0);
                return;
            }

            switch (fromType.tag) {
                case ClassTag.INT:
                case ClassTag.UINT:
                    // No-op
                    break;
                case ClassTag.BOOLEAN:
                    builder.emit(ILOp.ldc_i4_0);
                    builder.emit(ILOp.cgt_un);
                    break;
                case ClassTag.NUMBER:
                    builder.emit(ILOp.call, KnownMembers.numberToInt, 0);
                    break;
                case ClassTag.STRING:
                    builder.emit(ILOp.call, KnownMembers.stringToInt, 0);
                    break;
                default:
                    // Object type.
                    builder.emit(ILOp.call, KnownMembers.objectToInt, 0);
                    break;
            }
        }

        /// <summary>
        /// Emits IL instructions to convert the value on top of the stack to the uint type.
        /// </summary>
        /// <param name="builder">The <see cref="ILBuilder"/> instance in which to emit the
        /// code.</param>
        /// <param name="fromType">The type of the value on top of the stack.</param>
        public static void emitTypeCoerceToUint(ILBuilder builder, Class fromType) {
            if (fromType == null) {
                builder.emit(ILOp.call, KnownMembers.anyToUint, 0);
                return;
            }

            switch (fromType.tag) {
                case ClassTag.INT:
                case ClassTag.UINT:
                    // No-op
                    break;
                case ClassTag.BOOLEAN:
                    builder.emit(ILOp.ldc_i4_0);
                    builder.emit(ILOp.cgt_un);
                    break;
                case ClassTag.NUMBER:
                    builder.emit(ILOp.call, KnownMembers.numberToUint, 0);
                    break;
                case ClassTag.STRING:
                    builder.emit(ILOp.call, KnownMembers.stringToUint, 0);
                    break;
                default:
                    // Object type.
                    builder.emit(ILOp.call, KnownMembers.objectToUint, 0);
                    break;
            }
        }

        /// <summary>
        /// Emits IL instructions to convert the value on top of the stack to the Number type.
        /// </summary>
        /// <param name="builder">The <see cref="ILBuilder"/> instance in which to emit the
        /// code.</param>
        /// <param name="fromType">The type of the value on top of the stack.</param>
        public static void emitTypeCoerceToNumber(ILBuilder builder, Class fromType) {
            if (fromType == null) {
                builder.emit(ILOp.call, KnownMembers.anyToNumber, 0);
                return;
            }

            switch (fromType.tag) {
                case ClassTag.INT:
                    builder.emit(ILOp.conv_r8);
                    break;
                case ClassTag.UINT:
                    builder.emit(ILOp.conv_r_un);
                    break;
                case ClassTag.BOOLEAN:
                    builder.emit(ILOp.ldc_i4_0);
                    builder.emit(ILOp.cgt_un);
                    builder.emit(ILOp.conv_r8);
                    break;
                case ClassTag.NUMBER:
                    // No-op
                    break;
                case ClassTag.STRING:
                    builder.emit(ILOp.call, KnownMembers.stringToNumber, 0);
                    break;
                default:
                    // Object type.
                    builder.emit(ILOp.call, KnownMembers.objectToNumber, 0);
                    break;
            }
        }

        /// <summary>
        /// Emits IL instructions to convert the value on top of the stack to the Number type.
        /// </summary>
        ///
        /// <param name="builder">The <see cref="ILBuilder"/> instance in which to emit the
        /// code.</param>
        /// <param name="fromType">The type of the value on top of the stack.</param>
        /// <param name="convert">If this is true, convert null and undefined to their string
        /// representations "null" and "undefined" respectively (instead of the null string).</param>
        public static void emitTypeCoerceToString(ILBuilder builder, Class fromType, bool convert = false) {
            if (fromType == null) {
                builder.emit(ILOp.call, convert ? KnownMembers.anyToStringConvert : KnownMembers.anyToStringCoerce, 0);
                return;
            }

            switch (fromType.tag) {
                case ClassTag.INT:
                    builder.emit(ILOp.call, KnownMembers.intToString, 0);
                    break;
                case ClassTag.UINT:
                    builder.emit(ILOp.call, KnownMembers.uintToString, 0);
                    break;
                case ClassTag.BOOLEAN:
                    builder.emit(ILOp.call, KnownMembers.boolToString, 0);
                    break;
                case ClassTag.NUMBER:
                    builder.emit(ILOp.call, KnownMembers.numberToString, 0);
                    break;
                case ClassTag.STRING:
                    if (convert) {
                        var label = builder.createLabel();
                        builder.emit(ILOp.dup);
                        builder.emit(ILOp.brtrue, label);
                        builder.emit(ILOp.pop);
                        builder.emit(ILOp.ldstr, "null");
                        builder.markLabel(label);
                    }
                    break;
                default:
                    // Object type.
                    builder.emit(ILOp.call, convert ? KnownMembers.objectToStringConvert : KnownMembers.objectToStringCoerce, 0);
                    break;
            }
        }

        /// <summary>
        /// Emits IL instructions to convert the value on top of the stack to the Boolean type.
        /// </summary>
        /// <param name="builder">The <see cref="ILBuilder"/> instance in which to emit the
        /// code.</param>
        /// <param name="fromType">The type of the value on top of the stack.</param>
        public static void emitTypeCoerceToBoolean(ILBuilder builder, Class fromType) {
            if (fromType == null) {
                builder.emit(ILOp.call, KnownMembers.anyToBool, 0);
                return;
            }

            switch (fromType.tag) {
                case ClassTag.INT:
                case ClassTag.UINT:
                    builder.emit(ILOp.ldc_i4_0);
                    builder.emit(ILOp.cgt_un);
                    break;
                case ClassTag.BOOLEAN:
                    // No-op
                    break;
                case ClassTag.NUMBER:
                    builder.emit(ILOp.call, KnownMembers.numberToBool, 0);
                    break;
                case ClassTag.STRING:
                    builder.emit(ILOp.call, KnownMembers.stringToBool, 0);
                    break;
                default:
                    // Object type.
                    if (fromType.underlyingType == (object)typeof(ASObject)) {
                        builder.emit(ILOp.call, KnownMembers.objectToBool, 0);
                    }
                    else {
                        builder.emit(ILOp.ldnull);
                        builder.emit(ILOp.cgt_un);
                    }
                    break;
            }
        }

        /// <summary>
        /// Emits IL instructions to convert the value on top of the stack to the Object type.
        /// </summary>
        /// <param name="builder">The <see cref="ILBuilder"/> instance in which to emit the
        /// code.</param>
        /// <param name="fromType">The type of the value on top of the stack.</param>
        public static void emitTypeCoerceToObject(ILBuilder builder, Class fromType) {
            if (fromType == null) {
                // anyToObject requires an address, so use a temporary variable.
                var tempvar = builder.acquireTempLocal(typeof(ASAny));
                builder.emit(ILOp.stloc, tempvar);
                builder.emit(ILOp.ldloca, tempvar);
                builder.emit(ILOp.call, KnownMembers.anyGetObject, 0);
                builder.releaseTempLocal(tempvar);
                return;
            }

            switch (fromType.tag) {
                case ClassTag.INT:
                    builder.emit(ILOp.call, KnownMembers.intToObject, 0);
                    break;
                case ClassTag.UINT:
                    builder.emit(ILOp.call, KnownMembers.uintToObject, 0);
                    break;
                case ClassTag.NUMBER:
                    builder.emit(ILOp.call, KnownMembers.numberToObject, 0);
                    break;
                case ClassTag.STRING:
                    builder.emit(ILOp.call, KnownMembers.stringToObject, 0);
                    break;
                case ClassTag.BOOLEAN:
                    builder.emit(ILOp.call, KnownMembers.boolToObject, 0);
                    break;
                default:
                    if (fromType.isInterface)
                        // We assume that AS3 code doesn't need to handle non-AS objects implementing
                        // interfaces, so use castclass instead of calling AS_cast.
                        builder.emit(ILOp.castclass, typeof(ASObject));
                    break;
            }
        }

        /// <summary>
        /// Gets the <see cref="MethodInfo"/> representing the method that casts an instance of the
        /// <see cref="ASAny"/> type to an object (non-primitive) type.
        /// </summary>
        /// <param name="toType">The target type of the cast.</param>
        /// <returns>A <see cref="MethodInfo"/> representing the casting function.</returns>
        public static MethodInfo getAnyCastMethod(Class toType) {
            return s_anyCastMethods.GetValue(
                Class.getUnderlyingOrPrimitiveType(toType),
                x => KnownMembers.anyCast.MakeGenericMethod(x)
            );
        }

        /// <summary>
        /// Gets the <see cref="MethodInfo"/> representing the method that casts an instance of the
        /// <see cref="ASObject"/> type to an object (non-primitive) type.
        /// </summary>
        /// <param name="toType">The target type of the cast.</param>
        /// <returns>A <see cref="MethodInfo"/> representing the casting function.</returns>
        public static MethodInfo getObjectCastMethod(Class toType) {
            return s_objectCastMethods.GetValue(
                Class.getUnderlyingOrPrimitiveType(toType),
                x => KnownMembers.objectCast.MakeGenericMethod(x)
            );
        }

        /// <summary>
        /// Gets the <see cref="ConstructorInfo"/> representing the constructor that creates
        /// an instance of <see cref="OptionalParam{T}"/>.
        /// </summary>
        /// <param name="type">The type argument for the <see cref="OptionalParam{T}"/> instance.</param>
        /// <returns>A <see cref="ConstructorInfo"/> representing the constructor.</returns>
        public static ConstructorInfo getOptionalParamCtor(Class type) {
            return s_optionalParamCtors.GetValue(
                Class.getUnderlyingOrPrimitiveType(type),
                x => {
                    var optionalType = typeof(OptionalParam<>).MakeGenericType(x);
                    return Array.Find(
                        optionalType.GetConstructors(),
                        ctor => ctor.MetadataToken == KnownMembers.optionalParamCtor.MetadataToken
                    );
                }
            );
        }

        /// <summary>
        /// Gets the <see cref="FieldInfo"/> representing the field <see cref="OptionalParam{T}.missing"/>
        /// for some type T.
        /// </summary>
        /// <param name="type">The type argument for <see cref="OptionalParam{T}"/>.</param>
        /// <returns>A <see cref="FieldInfo"/> representing the field.</returns>
        public static FieldInfo getOptionalParamUnspecifiedField(Class type) {
            return s_optionalParamUnspecified.GetValue(
                Class.getUnderlyingOrPrimitiveType(type),
                x => {
                    var optionalType = typeof(OptionalParam<>).MakeGenericType(x);
                    return Array.Find(
                        optionalType.GetFields(),
                        f => f.MetadataToken == KnownMembers.optionalParamMissing.MetadataToken
                    );
                }
            );
        }

        /// <summary>
        /// Emits IL instructions to push the default value of the given type onto the stack.
        /// </summary>
        ///
        /// <param name="builder">The <see cref="ILBuilder"/> instance in which to emit the
        /// code.</param>
        /// <param name="type">The type. This may be an integral or floating-point type</param>
        /// <param name="useNaNForFloats">If this is true, use NaN instead of 0 for floating-point
        /// types.</param>
        public static void emitPushDefaultValueOfType(ILBuilder builder, Type type, bool useNaNForFloats = false) {
            switch (Type.GetTypeCode(type)) {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Char:
                case TypeCode.Boolean:
                    builder.emit(ILOp.ldc_i4_0);
                    break;

                case TypeCode.Int64:
                case TypeCode.UInt64:
                    builder.emit(ILOp.ldc_i4_0);
                    builder.emit(ILOp.conv_i8);
                    break;

                case TypeCode.Single:
                    if (useNaNForFloats) {
                        builder.emit(ILOp.ldc_r4, Single.NaN);
                    }
                    else {
                        builder.emit(ILOp.ldc_i4_0);
                        builder.emit(ILOp.conv_r4);
                    }
                    break;

                case TypeCode.Double:
                    if (useNaNForFloats) {
                        builder.emit(ILOp.ldc_r4, Single.NaN);
                    }
                    else {
                        builder.emit(ILOp.ldc_i4_0);
                        builder.emit(ILOp.conv_r8);
                    }
                    break;

                default:
                    if (type == (object)typeof(ASAny)) {
                        builder.emit(ILOp.ldsfld, KnownMembers.undefinedField);
                    }
                    else if (!type.IsValueType) {
                        builder.emit(ILOp.ldnull);
                    }
                    else {
                        var tempVar = builder.acquireTempLocal(type);
                        builder.emit(ILOp.ldloca, tempVar);
                        builder.emit(ILOp.initobj, type);
                        builder.emit(ILOp.ldloc, tempVar);
                        builder.releaseTempLocal(tempVar);
                    }
                    break;
            }
        }

        /// <summary>
        /// Emits IL instructions to push a constant value onto the stack.
        /// </summary>
        /// <param name="builder">The <see cref="ILBuilder"/> instance in which to emit the
        /// code.</param>
        /// <param name="value">The constant. This must be of one of the following types: int, uint,
        /// Number, String, Boolean or Namespace, or the value null or undefined.</param>
        public static void emitPushConstant(ILBuilder builder, ASAny value) {
            if (!value.isDefined) {
                builder.emit(ILOp.ldsfld, KnownMembers.undefinedField);
                return;
            }
            if (value.value == null) {
                builder.emit(ILOp.ldnull);
                return;
            }

            switch (value.value.AS_class.tag) {
                case ClassTag.INT:
                case ClassTag.UINT:
                    builder.emit(ILOp.ldc_i4, (int)value);
                    break;
                case ClassTag.NUMBER:
                    builder.emit(ILOp.ldc_r8, (double)value);
                    break;
                case ClassTag.BOOLEAN:
                    builder.emit(ILOp.ldc_i4, (bool)value ? 1 : 0);
                    break;
                case ClassTag.STRING:
                    builder.emit(ILOp.ldstr, (string)value);
                    break;
                case ClassTag.NAMESPACE:
                    builder.emit(ILOp.ldstr, ((ASNamespace)value).uri);
                    builder.emit(ILOp.newobj, KnownMembers.xmlNsCtorFromURI, 0);
                    break;
                default:
                    throw new ArgumentException("Invalid constant type.", nameof(value));
            }
        }

        /// <summary>
        /// Emits IL instructions to push a constant value onto the stack and box it into
        /// an instance of type <see cref="ASObject"/>.
        /// </summary>
        /// <param name="builder">The <see cref="ILBuilder"/> instance in which to emit the
        /// code.</param>
        /// <param name="value">The constant. This must be of one of the following types: int, uint,
        /// Number, String, Boolean or Namespace, or the value null or undefined.</param>
        public static void emitPushConstantAsObject(ILBuilder builder, ASAny value) {
            if (!value.isDefined || value.value == null) {
                builder.emit(ILOp.ldnull);
                return;
            }

            switch (value.value.AS_class.tag) {
                case ClassTag.INT:
                    builder.emit(ILOp.ldc_i4, (int)value);
                    builder.emit(ILOp.call, KnownMembers.intToObject, 0);
                    break;
                case ClassTag.UINT:
                    builder.emit(ILOp.ldc_i4, (int)value);
                    builder.emit(ILOp.call, KnownMembers.uintToObject, 0);
                    break;
                case ClassTag.NUMBER:
                    builder.emit(ILOp.ldc_r8, (double)value);
                    builder.emit(ILOp.call, KnownMembers.numberToObject, 0);
                    break;
                case ClassTag.BOOLEAN:
                    builder.emit(ILOp.ldc_i4, (bool)value ? 1 : 0);
                    builder.emit(ILOp.call, KnownMembers.boolToObject, 0);
                    break;
                case ClassTag.STRING:
                    builder.emit(ILOp.ldstr, (string)value);
                    builder.emit(ILOp.call, KnownMembers.stringToObject, 0);
                    break;
                case ClassTag.NAMESPACE:
                    // A Namespace is an Object, no need for any conversion.
                    builder.emit(ILOp.ldstr, ((ASNamespace)value).uri);
                    builder.emit(ILOp.newobj, KnownMembers.xmlNsCtorFromURI, 0);
                    break;
                default:
                    throw new ArgumentException("Invalid constant type.", nameof(value));
            }
        }

        /// <summary>
        /// Emits IL instructions to push a constant value onto the stack and box it into
        /// an instance of type <see cref="ASAny"/>.
        /// </summary>
        /// <param name="builder">The <see cref="ILBuilder"/> instance in which to emit the
        /// code.</param>
        /// <param name="value">The constant. This must be of one of the following types: int, uint,
        /// Number, String or Boolean, or the value null or undefined.</param>
        public static void emitPushConstantAsAny(ILBuilder builder, ASAny value) {
            if (!value.isDefined || value.value == null) {
                builder.emit(ILOp.ldsfld, KnownMembers.undefinedField);
                return;
            }

            switch (value.value.AS_class.tag) {
                case ClassTag.INT:
                    builder.emit(ILOp.ldc_i4, (int)value);
                    builder.emit(ILOp.call, KnownMembers.intToAny, 0);
                    break;
                case ClassTag.UINT:
                    builder.emit(ILOp.ldc_i4, (int)value);
                    builder.emit(ILOp.call, KnownMembers.uintToAny, 0);
                    break;
                case ClassTag.NUMBER:
                    builder.emit(ILOp.ldc_r8, (double)value);
                    builder.emit(ILOp.call, KnownMembers.numberToAny, 0);
                    break;
                case ClassTag.BOOLEAN:
                    builder.emit(ILOp.ldc_i4, (bool)value ? 1 : 0);
                    builder.emit(ILOp.call, KnownMembers.boolToAny, 0);
                    break;
                case ClassTag.STRING:
                    builder.emit(ILOp.ldstr, (string)value);
                    builder.emit(ILOp.call, KnownMembers.stringToAny, 0);
                    break;
                case ClassTag.NAMESPACE:
                    builder.emit(ILOp.ldstr, ((ASNamespace)value).uri);
                    builder.emit(ILOp.newobj, KnownMembers.xmlNsCtorFromURI, 0);
                    builder.emit(ILOp.call, KnownMembers.anyFromObject, 0);
                    break;
                default:
                    throw new ArgumentException("Invalid constant type.", nameof(value));
            }
        }

        /// <summary>
        /// Determines whether the given value is the value that the CLR would zero-initialize
        /// a field or variable of the given type to.
        /// </summary>
        /// <returns>True if a variable or field of the type <paramref name="type"/> will be implicitly
        /// initialized to the value <paramref name="value"/>, otherwise false.</returns>
        /// <param name="value">The value to check if it is the default for <paramref name="type"/>.</param>
        /// <param name="type">The type of the field or variable.</param>
        public static bool isImplicitDefault(ASAny value, Class type) {
            if (type == null)
                return !value.isDefined;

            switch (type.tag) {
                case ClassTag.INT:
                case ClassTag.UINT:
                    return (int)value == 0;
                case ClassTag.NUMBER:
                    return (double)value == 0.0;
                case ClassTag.BOOLEAN:
                    return (bool)value == false;
                default:
                    return value.value == null;
            }
        }

        /// <summary>
        /// Emits IL instructions to throw an exception from the given type, message and error code.
        /// </summary>
        ///
        /// <param name="builder">The <see cref="ILBuilder"/> instance into which to emit the code.</param>
        /// <param name="type">The type of the error to throw.</param>
        /// <param name="code">The error code.</param>
        /// <param name="message">The error message.</param>
        internal static void emitThrowError(ILBuilder builder, Type type, ErrorCode code, string message) {
            builder.emit(ILOp.ldtoken, type);
            builder.emit(ILOp.ldstr, message);
            builder.emit(ILOp.ldc_i4, (int)code);
            builder.emit(ILOp.call, KnownMembers.createExceptionFromCodeAndMsg, -2);
            builder.emit(ILOp.@throw);
        }

    }

}
