using System;
using System.Runtime.CompilerServices;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// The Function class represents a closure in ActionScript 3, which is a wrapper object for a
    /// method that can be invoked dynamically.
    /// </summary>
    ///
    /// <remarks>
    /// <para>Function objects can be obtained in the following ways:</para>
    /// <list type="bullet">
    /// <item><description>By creating a method closure using the
    /// <see cref="MethodTrait.createMethodClosure" qualifyHint="true"/> method. Method closures
    /// have a stored receiver associated with them and ignore any receiver passed at invocation
    /// time. They cannot be used as constructors for prototype inheritance.</description></item>
    /// <item><description>
    /// By creating a function closure using the
    /// <see cref="MethodTrait.createFunctionClosure" qualifyHint="true"/> method. A function
    /// closure does not have a stored receiver like a method closure and must take a receiver at
    /// call time. If a function closure is created from an instance method, the receiver is
    /// passed on as the receiver to the instance method; if created from a static method, the
    /// receiver is passed as the first argument. Function closures can have prototypes and can be
    /// used as constructors for prototyped objects.
    /// </description></item>
    /// </list>
    /// </remarks>
    [AVM2ExportClass(name = "Function", isDynamic = true, hasPrototypeMethods = true)]
    [AVM2ExportClassInternal(tag = ClassTag.FUNCTION)]
    public class ASFunction : ASObject {

        /// <summary>
        /// The value of the "length" property of the AS3 Function class.
        /// </summary>
        [AVM2ExportTrait(name = "length")]
        public new const int AS_length = 1;

        private ASObject m_prototype;

        protected private ASFunction() { }

        /// <summary>
        /// Gets the number of parameters accepted by the underlying method of the Function object.
        /// </summary>
        [AVM2ExportTrait]
        public virtual int length => 0;

        /// <summary>
        /// Gets or sets the prototype object of the Function object.
        /// </summary>
        ///
        /// <remarks>
        /// <para>The prototype object will be set in the prototype chain of all objects constructed
        /// by this Function object when it is called as a constructor.</para>
        /// <para>If this <see cref="ASFunction"/> instance represents a method closure, the value
        /// of this property is always null and setting it will throw an exception; method closures
        /// cannot have prototypes and cannot be used as constructors.</para>
        /// </remarks>
        [AVM2ExportTrait]
        public virtual ASObject prototype {
            get => m_prototype;
            set => m_prototype = value;
        }

        /// <summary>
        /// Gets the underlying method of the Function object.
        /// </summary>
        public virtual MethodTrait method => null;

        /// <summary>
        /// Returns true if this <see cref="ASFunction"/> instance is a method closure, otherwise false.
        /// </summary>
        public virtual bool isMethodClosure => false;

        /// <summary>
        /// Gets the stored receiver of a method closure created from an instance method. If this
        /// <see cref="ASFunction"/> instance is not a method closure or a method closure of a static
        /// or global method, the value of this property is null.
        /// </summary>
        public virtual ASObject storedReceiver => null;

        /// <summary>
        /// Creates a delegate from the underlying method of the Function object with the given
        /// receiver.
        /// </summary>
        ///
        /// <param name="receiver">The receiver ("this" parameter) in calls to the returned delegate.
        /// This is ignored for method closures, which always use their stored receiver. For function
        /// closures, the global object of the application domain of the function's underlying method
        /// is used as the receiver if this argument is null.</param>
        ///
        /// <typeparam name="T">The type of the delegate. The signature of this type must be
        /// compatible with that of the underlying method of the Function object.</typeparam>
        ///
        /// <returns>The created delegate; null if the signature of <typeparamref name="T"/>
        /// is not compatible with that of the underlying method of the Function object, or if
        /// <paramref name="receiver"/> is not of the correct type.</returns>
        public virtual T createDelegate<T>(ASObject receiver = null) where T : Delegate => null;

        /// <summary>
        /// Invokes the object as a function.
        /// </summary>
        /// <param name="receiver">The receiver of the call.</param>
        /// <param name="args">The arguments passed to the call.</param>
        /// <param name="result">The return value of the call.</param>
        /// <returns>True, if the call was successful, otherwise false.</returns>
        public override bool AS_tryInvoke(ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) {
            result = default(ASAny);
            return true;
        }

        /// <summary>
        /// Invokes the object as a constructor.
        /// </summary>
        /// <param name="args">The arguments passed to the call.</param>
        /// <param name="result">The object created by the constructor call.</param>
        /// <returns>True, if the call was successful, otherwise false.</returns>
        ///
        /// <remarks>
        /// <para>
        /// Invoking a Function object as a constructor will create a new object, set its prototype
        /// chain to the prototype of this Function instance, and call the function with the new
        /// object as the receiver. If the function does not return a value (or returns undefined),
        /// the constructor invocation returns the created object.
        /// </para>
        /// <para>This method always returns false if called on a method closure; Method closures
        /// cannot be used as constructors.</para>
        /// </remarks>
        public override bool AS_tryConstruct(ReadOnlySpan<ASAny> args, out ASAny result) {
            result = ASObject.AS_createWithPrototype(prototype);
            return true;
        }

        /// <summary>
        /// Calls the function with the specified receiver and arguments.
        /// </summary>
        /// <param name="receiver">The receiver of the function call. If this
        /// <see cref="ASFunction"/> instance represents a method closure, this argument is ignored
        /// and the stored receiver is always used.</param>
        /// <param name="args">The arguments of the function call.</param>
        /// <returns>The return value of the function call.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASAny call(ASAny receiver = default, RestParam args = default) =>
            AS_invoke(receiver, args.getSpan());

        /// <summary>
        /// Calls the function with the specified receiver and arguments given as an array.
        /// </summary>
        ///
        /// <param name="receiver">The receiver of the function call. If this
        /// <see cref="ASFunction"/> instance represents a method closure, this argument is ignored
        /// and the stored receiver is always used.</param>
        /// <param name="argArray">The arguments of the function call as an array. If this is null, no
        /// arguments are passed, i.e. it is equivalent to passing an empty array.</param>
        ///
        /// <returns>The return value of the function call.</returns>
        [AVM2ExportTrait(nsUri = "http://adobe.com/AS3/2006/builtin")]
        [AVM2ExportPrototypeMethod]
        public ASAny apply(ASAny receiver, ASArray argArray) {
            ASAny[] arr = (argArray == null) ? Array.Empty<ASAny>() : argArray.toTypedArray<ASAny>();
            return AS_invoke(receiver, arr);
        }

        /// <summary>
        /// Returns a string representation of this Function object.
        /// </summary>
        /// <returns>A string representation of this Function object.</returns>
        ///
        /// <remarks>
        /// This method is exported to the AVM2 with the name <c>toString</c>, but must be called
        /// from .NET code with the name <c>AS_toString</c> to avoid confusion with the
        /// <see cref="Object.ToString" qualifyHint="true"/> method.
        /// </remarks>
        [AVM2ExportPrototypeMethod(name = "toString")]
        public new string AS_toString() => "function Function() {}";

        /// <summary>
        /// Creates an empty function and returns it.
        /// </summary>
        /// <returns>An empty function.</returns>
        /// <remarks>
        /// An empty function has no underlying method, and calling it will not do anything. However,
        /// it can have a prototype and be used to construct objects with prototype inheritance.
        /// </remarks>
        public static ASFunction createEmpty() {
            var func = new ASFunction();

            func.m_prototype = new ASObject();
            func.m_prototype.AS_dynamicProps.setValue("constructor", func, isEnum: false);

            return func;
        }

        /// <summary>
        /// Checks if two <see cref="ASFunction"/> instances are equal. Two instances are equal if they are
        /// the same (by reference) or are both method closures for the same method with the same receiver.
        /// </summary>
        ///
        /// <param name="func1">The first <see cref="ASFunction"/> instance.</param>
        /// <param name="func2">The second <see cref="ASFunction"/> instance.</param>
        /// <returns>True if <paramref name="func1"/> and <paramref name="func2"/> are equal, otherwise
        /// false.</returns>
        ///
        /// <remarks>This method is called when Function objects are compared using the weak equality
        /// or strict equality operators.</remarks>
        internal static bool internalEquals(ASFunction func1, ASFunction func2) {
            if (func1 == func2)
                return true;

            return func1.isMethodClosure && func2.isMethodClosure
                && func1.method == func2.method
                && func1.storedReceiver == func2.storedReceiver;
        }

        /// <summary>
        /// Returns a hash code for this <see cref="ASFunction"/> instance that is consistent with
        /// the <see cref="internalEquals"/> method.
        /// </summary>
        ///
        /// <returns>A hash code for this <see cref="ASFunction"/> instance.</returns>
        ///
        /// <remarks>This is called from <see cref="ASObject.GetHashCode"/>.</remarks>
        internal int internalGetHashCode() =>
            RuntimeHelpers.GetHashCode(method) ^ RuntimeHelpers.GetHashCode(storedReceiver);

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABC to IL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_INVOKE(ReadOnlySpan<ASAny> args) {
            if (args.Length == 0)
                return createEmpty();
            throw ErrorHelper.createError(ErrorCode.FUNCTION_STRING_NOT_SUPPORTED);
        }

        /// <exclude/>
        /// <summary>
        /// This is a special method that is called from the AVM2 runtime and by code compiled by the
        /// ABC to IL compiler. It must not be called from outside code.
        /// </summary>
        internal static new ASAny __AS_CONSTRUCT(ReadOnlySpan<ASAny> args) => __AS_INVOKE(args);

    }

    /// <summary>
    /// A method closure is a Function object that can be used to dynamically call an instance or
    /// static method.
    /// </summary>
    ///
    /// <remarks>
    /// <para>Method closures have an internal receiver object that is always passed as the
    /// receiver to the underlying method, regardless of the receiver provided when the method is
    /// invoked. Method closures, unlike function closures, cannot be used as constructors and
    /// cannot have prototypes.</para>
    /// </remarks>
    internal sealed class ASMethodClosure : ASFunction {

        private readonly ASObject m_receiver;
        private readonly MethodTrait m_method;

        internal ASMethodClosure(MethodTrait method, ASObject receiver) {
            m_receiver = receiver;
            m_method = method;
        }

        public override ASObject storedReceiver => m_receiver;

        public override MethodTrait method => m_method;

        public override int length => m_method.paramCount;

        public override bool isMethodClosure => true;

        public override ASObject prototype {
            get => base.prototype;
            set => throw ErrorHelper.createError(ErrorCode.ILLEGAL_WRITE_READONLY, "prototype", "MethodClosure");
        }

        public override bool AS_tryInvoke(ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) =>
            m_method.tryInvoke(m_receiver, args, out result) == BindStatus.SUCCESS;

        public override bool AS_tryConstruct(ReadOnlySpan<ASAny> args, out ASAny result) {
            result = default(ASAny);
            return false;
        }

        public override T createDelegate<T>(ASObject receiver = null) => method.createDelegate<T>(m_receiver);

    }

    /// <summary>
    /// A function closure is a Function object that can be used to dynamically call a method or
    /// use it as a constructor.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Unlike a method closure, a function closure has no stored receiver; it uses the receiver
    /// supplied at call time (or the global object of the method's context if no receiver is
    /// supplied). If a function closure is created from a static method, the receiver will be
    /// passed as the first argument to the method, before all the supplied arguments.
    /// </para>
    /// </remarks>
    internal sealed class ASFunctionClosure : ASFunction {

        private readonly MethodTrait m_method;
        private object m_scope;

        internal ASFunctionClosure(MethodTrait method, object scope) {
            m_method = method;
            m_scope = scope;

            var protoObject = new ASObject();
            protoObject.AS_dynamicProps.setValue("constructor", this, isEnum: false);

            this.prototype = protoObject;
        }

        /// <summary>
        /// Prepares the arguments for a function closure call.
        /// </summary>
        /// <returns>The arguments span that must be passed to the function's underlying method.</returns>
        /// <param name="receiver">The receiver of the function call.</param>
        /// <param name="args">The arguments passed to the function call.</param>
        private ReadOnlySpan<ASAny> _prepareArguments(ASAny receiver, ReadOnlySpan<ASAny> args) {
            int argCount = args.Length;

            if (m_method.hasAllParamsUntyped) {
                // If the method does not have typed parameters, no error should be thrown
                // when the argument count and parameter count mismatches. Instead, excess arguments
                // should be discarded and missing ones filled with undefined (which is ECMAScript
                // behaviour). Do this by allocating an argument array of the right size.

                argCount = Math.Max(argCount, m_method.requiredParamCount);
                if (!m_method.hasRest)
                    argCount = Math.Min(argCount, m_method.paramCount);
            }

            if (m_method.isStatic)
                argCount++;

            if (argCount == args.Length)
                return args;

            ASAny[] newArgs = new ASAny[argCount];
            Span<ASAny> newArgsSpan = newArgs;

            if (m_method.isStatic) {
                if (m_scope != null)
                    receiver = new ScopedClosureReceiver(receiver.value, m_scope, this);

                newArgsSpan[0] = receiver;
                newArgsSpan = newArgsSpan.Slice(1);
            }

            if (args.Length <= newArgsSpan.Length)
                args.CopyTo(newArgsSpan);
            else
                args.Slice(0, newArgsSpan.Length).CopyTo(newArgsSpan);

            return newArgs;
        }

        public override MethodTrait method => m_method;

        public override int length => m_method.isStatic ? m_method.paramCount - 1 : m_method.paramCount;

        public override bool AS_tryInvoke(ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) {
            if (receiver.value == null)
                receiver = m_method.applicationDomain.globalObject;

            ReadOnlySpan<ASAny> argsForCall = _prepareArguments(receiver, args);

            if (m_method.isStatic)
                receiver = default;

            var status = m_method.tryInvoke(receiver, argsForCall, out result);
            return status == BindStatus.SUCCESS;
        }

        public override bool AS_tryConstruct(ReadOnlySpan<ASAny> args, out ASAny result) {
            ASObject newObj = ASObject.AS_createWithPrototype(this.prototype);

            ReadOnlySpan<ASAny> argsForCall = _prepareArguments(newObj, args);
            ASAny receiver = m_method.isStatic ? default : newObj;

            BindStatus bindStatus = m_method.tryInvoke(receiver, argsForCall, out ASAny returnValue);

            if (bindStatus != BindStatus.SUCCESS) {
                result = default;
                return false;
            }

            result = returnValue.isDefined ? returnValue : (ASAny)newObj;
            return true;
        }

        public override T createDelegate<T>(ASObject receiver = null) {
            receiver = receiver ?? m_method.applicationDomain.globalObject;
            if (m_scope != null)
                receiver = new ScopedClosureReceiver(receiver, m_scope, this);

            return ReflectUtil.makeDelegate<T>(m_method.underlyingMethodInfo, receiver);
        }

    }

}
