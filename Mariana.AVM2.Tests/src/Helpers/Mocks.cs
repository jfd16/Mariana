using System;
using Mariana.AVM2.Core;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Tests.Helpers {

    /// <summary>
    /// An object that produces specified values when converted to a primitive type.
    /// </summary>
    [AVM2ExportClass]
    public class ConvertibleMockObject : ASObject {
        static ConvertibleMockObject() => TestAppDomain.ensureClassesLoaded(typeof(ConvertibleMockObject));

        private readonly int m_intValue;
        private readonly uint m_uintValue;
        private readonly double m_numberValue;
        private readonly string m_stringValue;
        private readonly bool m_boolValue;

        protected private override bool AS_coerceBoolean() => m_boolValue;
        protected override int AS_coerceInt() => m_intValue;
        protected override uint AS_coerceUint() => m_uintValue;
        protected override double AS_coerceNumber() => m_numberValue;
        protected override string AS_coerceString() => m_stringValue;

        /// <summary>
        /// Creates a new instance of <see cref="ConvertibleMockObject"/>.
        /// </summary>
        /// <param name="intValue">The value returned when the object is converted to the int type.</param>
        /// <param name="uintValue">The value returned when the object is converted to the uint type.</param>
        /// <param name="numberValue">The value returned when the object is converted to the Number type.</param>
        /// <param name="stringValue">The value returned when the object is converted to the String type.</param>
        /// <param name="boolValue">The value returned when the object is converted to the Boolean type.</param>
        public ConvertibleMockObject(
            int intValue = 0,
            uint uintValue = 0,
            double numberValue = 0.0,
            string stringValue = "",
            bool boolValue = false
        ) {
            m_intValue = intValue;
            m_uintValue = uintValue;
            m_numberValue = numberValue;
            m_stringValue = stringValue;
            m_boolValue = boolValue;
        }
    }

    public static class DynamicMethodMocker {
        static DynamicMethodMocker() => TestAppDomain.ensureClassesLoaded(typeof(DynamicMethodMocker));

        private static readonly MethodTrait s_invokeMethodTrait = MethodTrait.createNativeMethod(
            typeof(DynamicMethodMocker).GetMethod(nameof(__invoke))
        );

        public static ASObject createObjectWithOwnMethod(string methodName, ASAny returnValue) {
            var obj = new ASObject();
            obj.AS_dynamicProps["__returnval"] = returnValue;
            obj.AS_dynamicProps.setValue(methodName, s_invokeMethodTrait.createFunctionClosure(), false);
            return obj;
        }

        public static ASObject createObjectWithProtoMethod(string methodName, ASAny returnValue) {
            var proto = new ASObject();
            proto.AS_dynamicProps.setValue(methodName, s_invokeMethodTrait.createFunctionClosure(), false);

            var obj = ASObject.AS_createWithPrototype(proto);
            obj.AS_dynamicProps["__returnval"] = returnValue;

            return obj;
        }

        public static ASAny __invoke(ASObject obj) => obj.AS_dynamicProps["__returnval"];
    }

    /// <summary>
    /// A function object that tracks invocations.
    /// </summary>
    [AVM2ExportClass]
    public sealed class SpyFunctionObject : ASFunction {

        /// <summary>
        /// Represents a tracked invocation of a <see cref="SpyFunctionObject"/>.
        /// </summary>
        public readonly struct CallRecord {
            private readonly ASObject m_receiver;
            private readonly ASAny[] m_args;
            private readonly ASAny m_retval;
            private readonly bool m_isConstruct;

            /// <summary>
            /// Creates a new instance of <see cref="CallRecord"/>.
            /// </summary>
            /// <param name="receiver">The "this" argument of the function call, or null for a
            /// constructor call.</param>
            /// <param name="args">The arguments passed to the function call.</param>
            /// <param name="retval">The return value of the function call.</param>
            /// <param name="isConstruct">True if the function was called as a constructor, otherwise false.</param>
            public CallRecord(ASObject receiver, ReadOnlySpan<ASAny> args, ASAny retval, bool isConstruct) {
                m_receiver = receiver;
                m_args = args.ToArray();
                m_retval = retval;
                m_isConstruct = isConstruct;
            }

            /// <summary>
            /// Gets the "this" argument of the function call, or null for a constructor call.
            /// </summary>
            public ASObject receiver => m_receiver;

            /// <summary>
            /// Gets the value returned by the function.
            /// </summary>
            public ASAny returnValue => m_retval;

            /// <summary>
            /// Returns true if the function was invoked as a constructor, otherwise false.
            /// </summary>
            public bool isConstruct => m_isConstruct;

            /// <summary>
            /// Returns a <see cref="ReadOnlySpan{ASAny}"/> containing the arguments passed to the function call.
            /// </summary>
            public ReadOnlySpan<ASAny> getArguments() => m_args;

            /// <summary>
            /// Returns a value indicating whether this <see cref="CallRecord"/> instance is equal
            /// to another <see cref="CallRecord"/> instance.
            /// </summary>
            /// <param name="other">The <see cref="CallRecord"/> instance to compare with this instance.</param>
            /// <returns>True if this instance is equal to <paramref name="other"/>, otherwise false.</returns>
            public bool isEqualTo(in CallRecord other) {
                if (m_receiver != other.m_receiver
                    || m_retval != other.m_retval
                    || m_isConstruct != other.m_isConstruct
                    || m_args.Length != other.m_args.Length)
                {
                    return false;
                }

                for (int i = 0; i < m_args.Length; i++) {
                    if (m_args[i] != other.m_args[i])
                        return false;
                }

                return true;
            }

            public override string ToString() =>
                String.Format("{0}({1}) => {2}", isConstruct ? "new " : "", String.Join(", ", m_args), m_retval);
        }

        private Func<ASAny, ASAny[], ASAny> m_func;
        private DynamicArray<CallRecord> m_calls;
        private ASObject m_storedReceiver;
        private int m_length;

        /// <summary>
        /// Creates a new instance of <see cref="SpyFunctionObject"/>.
        /// </summary>
        /// <param name="func">A delegate that will be called when the function is invoked. If this is null,
        /// no actual function is invoked (but calls will still be tracked) and the return value of every
        /// call is assumed to be undefined.</param>
        /// <param name="storedReceiver">If the created function instance is to represent a method closure, set
        /// this to the method closure receiver. Otherwise set to null.</param>
        /// <param name="argCount">The number of arguments accepted by the function. This is used for
        /// the value of the <see cref="ASFunction.length"/> property.</param>
        public SpyFunctionObject(Func<ASAny, ASAny[], ASAny> func = null, ASObject storedReceiver = null, int argCount = 0) {
            m_func = func;
            m_storedReceiver = storedReceiver;
            m_length = argCount;
        }

        /// <summary>
        /// Creates a clone of this <see cref="SpyFunctionObject"/> instance with an empty call record list.
        /// </summary>
        public SpyFunctionObject clone() => new SpyFunctionObject(m_func, m_storedReceiver, m_length);

        public override ASObject storedReceiver => m_storedReceiver;

        public override int length => m_length;

        public override bool isMethodClosure => m_storedReceiver != null;

        public override bool AS_tryInvoke(ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) {
            ASObject actualReceiver = m_storedReceiver ?? receiver.value;
            result = (m_func != null) ? m_func(actualReceiver, args.ToArray()) : default;
            m_calls.add(new CallRecord(actualReceiver, args, result, false));
            return true;
        }

        public override bool AS_tryConstruct(ReadOnlySpan<ASAny> args, out ASAny result) {
            if (isMethodClosure) {
                result = default;
                return false;
            }

            result = (m_func != null) ? m_func(default, args.ToArray()) : default;
            m_calls.add(new CallRecord(null, args, result, true));
            return true;
        }

        /// <summary>
        /// Returns a reference to a <see cref="CallRecord"/> representing the last
        /// invocation of the function. This should only be accessed after at least one
        /// invocation has been made.
        /// </summary>
        public ref readonly CallRecord lastCall => ref m_calls[m_calls.length - 1];

        /// <summary>
        /// Returns a <see cref="ReadOnlySpan{CallRecord}"/> representing all the invocations
        /// made on this instance, in the order in which the invocations were made.
        /// </summary>
        public ReadOnlySpan<CallRecord> getCallRecords() => m_calls.asSpan();

    }

}
