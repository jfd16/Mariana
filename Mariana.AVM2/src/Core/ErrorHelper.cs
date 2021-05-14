using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    internal static class ErrorHelper {

        private struct _ErrInfo {
            public Type type;
            public string msg;
        }

        private static readonly Dictionary<ErrorCode, _ErrInfo> m_errorInfoDict = _loadErrorData();

        private static Dictionary<ErrorCode, _ErrInfo> _loadErrorData() {
            Dictionary<ErrorCode, _ErrInfo> errInfoDict = new Dictionary<ErrorCode, _ErrInfo>();

            Dictionary<string, Type> typeNameDict = new Dictionary<string, Type> {
                ["Error"] = typeof(ASError),
                ["ArgumentError"] = typeof(ASArgumentError),
                ["DefinitionError"] = typeof(ASDefinitionError),
                ["EvalError"] = typeof(ASEvalError),
                ["RangeError"] = typeof(ASRangeError),
                ["ReferenceError"] = typeof(ASReferenceError),
                ["SecurityError"] = typeof(ASSecurityError),
                ["TypeError"] = typeof(ASTypeError),
                ["SyntaxError"] = typeof(ASSyntaxError),
                ["URIError"] = typeof(ASURIError),
                ["VerifyError"] = typeof(ASVerifyError),
                ["NativeClassLoadError"] = typeof(NativeClassLoadError),
            };

            using (var stream = typeof(ErrorHelper).Assembly.GetManifestResourceStream("error_messages.txt")) {
                StreamReader reader = new StreamReader(stream);

                while (true) {
                    string line = reader.ReadLine();
                    if (line == null)
                        break;
                    if (line.Length == 0 || line[0] == '#')
                        continue;

                    NumberFormatHelper.stringToInt(line, out int errCode, out int charsRead, strict: false);

                    while (line[charsRead] == ' ' || line[charsRead] == '\t')
                        charsRead++;

                    int typeNameLength = -1;
                    for (int i = charsRead; i < line.Length; i++) {
                        if (line[i] == ' ' || line[i] == '\t') {
                            typeNameLength = i - charsRead;
                            break;
                        }
                    }

                    Type errType = typeNameDict[line.Substring(charsRead, typeNameLength)];
                    charsRead += typeNameLength;

                    while (line[charsRead] == ' ' || line[charsRead] == '\t')
                        charsRead++;

                    int endOfMessage = line.Length - 1;
                    while (line[endOfMessage] == ' ' || line[endOfMessage] == '\t')
                        endOfMessage--;

                    string errMsg = line.Substring(charsRead, endOfMessage - charsRead + 1);
                    errInfoDict[(ErrorCode)errCode] = new _ErrInfo {type = errType, msg = errMsg};
                }
            }

            return errInfoDict;
        }

        /// <summary>
        /// Creates an exception corresponding to the given error code. The type of the error object
        /// is determined from <paramref name="code"/>.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <param name="args">The placeholder arguments, if the error code has a formattable
        /// message.</param>
        /// <returns>An <see cref="AVM2Exception"/> instance that can be thrown.</returns>
        internal static AVM2Exception createError(ErrorCode code, params object[] args) =>
            new AVM2Exception(createErrorObject(code, args));

        /// <summary>
        /// Creates an Error object corresponding to the given error code. The type of the error
        /// object is determined from <paramref name="code"/>. Unlike <see cref="createError"/>,
        /// this does not wrap the Error object in an <see cref="AVM2Exception"/>.
        /// </summary>
        ///
        /// <param name="code">The error code.</param>
        /// <param name="args">The placeholder arguments, if the error code has a formattable
        /// message.</param>
        /// <returns>An <see cref="ASError"/> object.</returns>
        internal static ASError createErrorObject(ErrorCode code, params object[] args) {
            _ErrInfo errinfo = m_errorInfoDict[code];
            ConstructorInfo ctor = errinfo.type.GetConstructor(new[] {typeof(string), typeof(int)});
            return (ASError)ctor.Invoke(new object[] {_formatErrorMsg(errinfo.msg, args), (int)code});
        }

        /// <summary>
        /// Creates an <see cref="AVM2Exception"/> containing an error that can be thrown when a
        /// binding operation fails.
        /// </summary>
        ///
        /// <param name="className">The name of the class on which the property binding was
        /// attempted.</param>
        /// <param name="traitName">The name of the property whose binding was attempted.</param>
        /// <param name="bindStatus">The <see cref="BindStatus"/> indicating the failure code. Must
        /// not be <see cref="BindStatus.SUCCESS" qualifyHint="true"/> or
        /// <see cref="BindStatus.SOFT_SUCCESS" qualifyHint="true"/>.</param>
        ///
        /// <returns>An <see cref="AVM2Exception"/> instance that can be thrown.</returns>
        public static AVM2Exception createBindingError(QName className, QName traitName, BindStatus bindStatus) =>
            createBindingError(className.ToString(), traitName.ToString(), bindStatus);

        /// <summary>
        /// Creates an <see cref="AVM2Exception"/> containing an error that can be thrown when a
        /// binding operation fails.
        /// </summary>
        ///
        /// <param name="className">The name of the class on which the property binding was
        /// attempted.</param>
        /// <param name="traitName">The name of the property whose binding was attempted.</param>
        /// <param name="bindStatus">The <see cref="BindStatus"/> indicating the failure code. Must
        /// not be <see cref="BindStatus.SUCCESS" qualifyHint="true"/> or
        /// <see cref="BindStatus.SOFT_SUCCESS" qualifyHint="true"/>.</param>
        ///
        /// <returns>An <see cref="AVM2Exception"/> instance that can be thrown.</returns>
        public static AVM2Exception createBindingError(string className, string traitName, BindStatus bindStatus) {
            switch (bindStatus) {
                case BindStatus.AMBIGUOUS:
                    return createError(ErrorCode.AMBIGUOUS_NAME_MATCH, traitName);

                case BindStatus.FAILED_ASSIGNMETHOD:
                    return createError(ErrorCode.CANNOT_ASSIGN_TO_METHOD, traitName, className);

                case BindStatus.FAILED_CREATEDYNAMICNONPUBLIC:
                    return createError(ErrorCode.CANNOT_CREATE_PROPERTY, traitName, className);

                case BindStatus.FAILED_METHODCONSTRUCT:
                    return createError(ErrorCode.CANNOT_CALL_METHOD_AS_CTOR, traitName, className);

                case BindStatus.FAILED_NOTCONSTRUCTOR:
                    return createError(ErrorCode.INSTANTIATE_NON_CONSTRUCTOR);

                case BindStatus.FAILED_NOTFUNCTION:
                    return createError(ErrorCode.INVOKE_NON_FUNCTION, traitName);

                case BindStatus.FAILED_READONLY:
                    return createError(ErrorCode.ILLEGAL_WRITE_READONLY, traitName, className);

                case BindStatus.FAILED_WRITEONLY:
                    return createError(ErrorCode.ILLEGAL_READ_WRITEONLY, traitName, className);

                case BindStatus.NOT_FOUND:
                    return createError(ErrorCode.PROPERTY_NOT_FOUND, traitName, className);

                default:
                    return null;
            }
        }

        /// <summary>
        /// Creates an <see cref="AVM2Exception"/> object containing an exception to be thrown when
        /// an object cannot be cast to another type.
        /// </summary>
        ///
        /// <param name="objOrFromType">The object or source type of the attempted cast. If this is an
        /// instance of <see cref="Class"/> or <see cref="Type"/>, it is interpreted as a
        /// type.</param>
        /// <param name="toType">The target type of the attempted cast. This must be an instance of
        /// <see cref="Class"/> or <see cref="Type"/>.</param>
        ///
        /// <returns>An <see cref="AVM2Exception"/> instance that can be thrown.</returns>
        public static AVM2Exception createCastError(object objOrFromType, object toType) {
            string fromTypeStr, toTypeStr;

            if (objOrFromType is ASAny any) {
                if (any.isUndefinedOrNull)
                    fromTypeStr = any.ToString();
                else
                    fromTypeStr = any.AS_class.name.ToString();
            }
            else if (objOrFromType is ASObject obj) {
                fromTypeStr = obj.AS_class.name.ToString();
            }
            else if (objOrFromType is Type fromType) {
                Class fromTypeClass = Class.fromType(fromType);
                fromTypeStr = (fromTypeClass != null) ? fromTypeClass.name.ToString() : ReflectUtil.getFullTypeName(fromType);
            }
            else if (objOrFromType is Class fromClass) {
                fromTypeStr = fromClass.name.ToString();
            }
            else {
                fromTypeStr = (objOrFromType as string) ?? "null";
            }

            if (toType is Class toClass) {
                toTypeStr = toClass.name.ToString();
            }
            else if (toType is Type toTypeAsType) {
                Class toTypeClass = Class.fromType(toTypeAsType);
                toTypeStr = (toTypeClass != null) ? toTypeClass.name.ToString() : ReflectUtil.getFullTypeName(toTypeAsType);
            }
            else {
                toTypeStr = (toType as string) ?? "null";
            }

            return createError(ErrorCode.TYPE_COERCION_FAILED, fromTypeStr, toTypeStr);
        }

        /// <summary>
        /// Creates an <see cref="AVM2Exception"/> instance to throw as an exception when an
        /// incorrect number of arguments is passed to a method call.
        /// </summary>
        /// <param name="method">The method being called.</param>
        /// <param name="argCount">The number of arguments passed.</param>
        /// <returns>An <see cref="AVM2Exception"/> object that can be thrown.</returns>
        public static AVM2Exception createArgCountMismatchError(MethodTrait method, int argCount) {
            string methodName =
                ((method.declaringClass != null) ? method.declaringClass.name.ToString() + "/" : "")
                + method.name.ToString()
                + "()";
            return createArgCountMismatchError(methodName, method.requiredParamCount, argCount);
        }

        /// <summary>
        /// Creates an <see cref="AVM2Exception"/> instance to throw as an exception when an
        /// incorrect number of arguments is passed to a constructor call.
        /// </summary>
        /// <param name="ctor">The constructor being called.</param>
        /// <param name="argCount">The number of arguments passed.</param>
        /// <returns>An <see cref="AVM2Exception"/> object that can be thrown.</returns>
        public static AVM2Exception createArgCountMismatchError(ClassConstructor ctor, int argCount) {
            string methodName = ctor.declaringClass.name.ToString() + "()";
            return createArgCountMismatchError(methodName, ctor.requiredParamCount, argCount);
        }

        /// <summary>
        /// Creates an <see cref="AVM2Exception"/> instance to throw as an exception when an
        /// incorrect number of arguments is passed to a method call.
        /// </summary>
        /// <param name="methodName">The name of the method to be displayed in the error message.</param>
        /// <param name="expectedCount">The number of arguments expected by the method.</param>
        /// <param name="argCount">The number of arguments passed.</param>
        /// <returns>An <see cref="AVM2Exception"/> object that can be thrown.</returns>
        public static AVM2Exception createArgCountMismatchError(string methodName, int expectedCount, int argCount) =>
            createError(ErrorCode.ARG_COUNT_MISMATCH, methodName, expectedCount, argCount);

        /// <summary>
        /// Substitutes arguments in a formattable internal error message.
        /// </summary>
        /// <param name="message">The error message containing placeholders.</param>
        /// <param name="args">The arguments to substitute.</param>
        /// <returns>The error message, with arguments substituted.</returns>
        private static string _formatErrorMsg(string message, object[] args) {
            var sb = new StringBuilder();
            ReadOnlySpan<char> messageSpan = message;

            while (messageSpan.Length > 0) {
                int placeholderPos = messageSpan.IndexOf('$');
                if (placeholderPos == -1 || placeholderPos == messageSpan.Length - 1) {
                    sb.Append(messageSpan);
                    break;
                }

                int argIndex = messageSpan[placeholderPos + 1] - '0';
                if (argIndex <= 9) {
                    sb.Append(messageSpan.Slice(0, placeholderPos));
                    object arg = args[argIndex - 1];
                    sb.Append((arg != null) ? arg.ToString() : "null");
                }

                messageSpan = messageSpan.Slice(placeholderPos + 2);
            }

            return sb.ToString();
        }

    }

}
