using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// Represents an exception thrown by ActionScript 3 code, or internally by the AVM2.
    /// </summary>
    public sealed class AVM2Exception : Exception {

        private static ConcurrentDictionary<RuntimeTypeHandle, ConstructorInfo> s_cachedConstructors =
            new ConcurrentDictionary<RuntimeTypeHandle, ConstructorInfo>();

        private ASAny m_thrownValue;

        /// <summary>
        /// Creates a new <see cref="AVM2Exception"/> instance.
        /// </summary>
        /// <param name="thrownValue">The error object to be thrown. This is usually an instance of
        /// the Error class (or a subclass of it), but any AS3 object is allowed to be thrown as an
        /// error.</param>
        public AVM2Exception(ASAny thrownValue) {
            m_thrownValue = thrownValue;
        }

        /// <summary>
        /// Returns the value of the exception thrown.
        /// </summary>
        public ASAny thrownValue => m_thrownValue;

        /// <summary>
        /// Gets the error message of this <see cref="AVM2Exception"/> instance.
        /// </summary>
        public override string Message {
            get {
                if (thrownValue.value is ASError err) {
                    return String.Format(
                        "{0} #{1}: {2}",
                        err.name.ToString(),
                        err.errorID.ToString(CultureInfo.InvariantCulture),
                        err.message.ToString()
                    );
                }

                return ASAny.AS_convertString(thrownValue);
            }
        }

        /// <summary>
        /// Creates an <see cref="AVM2Exception"/> object wrapping a new instance of the Error class
        /// or a subclass of it with the given message and code.
        /// </summary>
        /// <param name="message">The value to set to the message property of the created
        /// <see cref="ASError"/> instance.</param>
        /// <param name="code">The value to set to the code property of the created
        /// <see cref="ASError"/> instance.</param>
        /// <typeparam name="TError">The type of the error to create. This type must have a public
        /// constructor with the signature <c>TError(string message, int code)</c>.</typeparam>
        /// <returns>An <see cref="AVM2Exception"/> object wrapping the created instance of the
        /// given error type.</returns>
        public static AVM2Exception create<TError>(string message, int code) where TError : ASError =>
            create(typeof(TError).TypeHandle, message, code);

        /// <summary>
        /// Creates an <see cref="AVM2Exception"/> object wrapping a new instance of the Error class
        /// or a subclass of it with the given message and code.
        /// </summary>
        ///
        /// <param name="typeHandle">The <see cref="RuntimeTypeHandle"/> of the type of the error
        /// to create. This type must derive from <see cref="ASError"/> and have a public constructor
        /// with the signature <c>TError(string message, int code)</c>.</param>
        /// <param name="message">The value to set to the message property of the created
        /// <see cref="ASError"/> instance.</param>
        /// <param name="code">The value to set to the code property of the created
        /// <see cref="ASError"/> instance.</param>
        ///
        /// <returns>An <see cref="AVM2Exception"/> object wrapping the created instance of the
        /// given error type.</returns>
        public static AVM2Exception create(RuntimeTypeHandle typeHandle, string message, int code) {
            ConstructorInfo ctor = s_cachedConstructors.GetOrAdd(
                typeHandle,
                h => {
                    var newCtor = Type.GetTypeFromHandle(h).GetConstructor(new[] {typeof(string), typeof(int)});
                    if (newCtor == null) {
                        throw new ArgumentException(
                            "Error type does not have the required constructor.", nameof(typeHandle));
                    }
                    return newCtor;
                }
            );

            return new AVM2Exception((ASError)ctor.Invoke(new object[] {message, code}));
        }

        /// <summary>
        /// Creates an <see cref="AVM2Exception"/> object to throw as an exception when an
        /// incorrect number of arguments is passed to a method call.
        /// </summary>
        /// <param name="methodName">The name of the method to be included in the error message.</param>
        /// <param name="expected">The number of arguments expected by the method.</param>
        /// <param name="received">The number of arguments passed to the method call.</param>
        /// <returns>An <see cref="AVM2Exception"/> object that can be thrown.</returns>
        /// <remarks>
        /// Calls to this method are inserted into code generated by the IL compiler; this method
        /// should not be used in .NET code.
        /// </remarks>
        public static AVM2Exception createArgCountMismatchError(string methodName, int expected, int received) =>
            ErrorHelper.createError(ErrorCode.ARG_COUNT_MISMATCH, methodName, expected, received);

        /// <summary>
        /// Creates an <see cref="AVM2Exception"/> object to throw as an exception when a null
        /// value is dereferenced.
        /// </summary>
        /// <returns>An <see cref="AVM2Exception"/> object that can be thrown.</returns>
        /// <remarks>
        /// Calls to this method are inserted into code generated by the IL compiler; this method
        /// should not be used in .NET code.
        /// </remarks>
        public static AVM2Exception createNullReferenceError() => ErrorHelper.createError(ErrorCode.NULL_REFERENCE_ERROR);

        /// <summary>
        /// Creates an <see cref="AVM2Exception"/> to throw when a global memory instruction
        /// attempts to load or store at an out-of-bounds
        /// </summary>
        /// <returns>An <see cref="AVM2Exception"/> object that can be thrown.</returns>
        /// <remarks>
        /// Calls to this method are inserted into code generated by the IL compiler; this method
        /// should not be used in .NET code.
        /// </remarks>
        public static AVM2Exception createGlobalMemoryRangeCheckError() => ErrorHelper.createError(ErrorCode.RANGE_INVALID);

        /// <summary>
        /// Attempts to unwrap a caught exception and retrieve the thrown object if it should be
        /// handled by AS3 code.
        /// </summary>
        /// <param name="exception">The caught exception.</param>
        /// <param name="thrownValue">An output parameter where the object thrown will be written,
        /// if the exception should be handled by compiled ActionScript code.</param>
        /// <returns>True if the exception should be handled, false otherwise.</returns>
        /// <remarks>
        /// Calls to this method are inserted into code generated by the IL compiler; this method
        /// should not be used in .NET code.
        /// </remarks>
        public static bool tryUnwrapCaughtException(Exception exception, out ASAny thrownValue) {
            thrownValue = default;

            // Unwrap inner exceptions from TypeInitializationException and TargetInvocationException.
            while (exception is TypeInitializationException || exception is TargetInvocationException)
                exception = exception.InnerException;

            if (exception is AVM2Exception avm2Exception) {
                thrownValue = avm2Exception.thrownValue;
                return true;
            }

            if (exception is NullReferenceException) {
                thrownValue = ErrorHelper.createErrorObject(ErrorCode.NULL_REFERENCE_ERROR);
                return true;
            }

            return false;
        }

    }

}
