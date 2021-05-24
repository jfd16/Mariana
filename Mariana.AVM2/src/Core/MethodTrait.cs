using System;
using System.Reflection;
using Mariana.AVM2.Native;
using Mariana.Common;

namespace Mariana.AVM2.Core {

    /// <summary>
    /// A trait object representing a method in a class, a global method or an inner closure
    /// method defined in an ABC script.
    /// </summary>
    public class MethodTrait : Trait {

        private MethodInfo m_underlyingMethodInfo;

        private bool m_hasReturn;

        private bool m_hasRest;

        private bool m_isOverride;

        private bool m_hasUntypedSig;

        private bool m_hasScope;

        private int m_requiredArgCount;

        private Class m_returnType;

        private MethodTraitParameter[] m_params;

        private LazyInitObject<RuntimeDispatch.MethodStub> m_lazyRuntimeDispatch;

        protected private MethodTrait(in QName name, Class declaringClass, ApplicationDomain appDomain, bool isStatic)
            : base(name, declaringClass, appDomain, isStatic)
        {
            m_lazyRuntimeDispatch = new LazyInitObject<RuntimeDispatch.MethodStub>(
                () => RuntimeDispatch.generateMethodStub(this)
            );
        }

        /// <inheritdoc/>
        public override TraitType traitType => TraitType.METHOD;

        /// <summary>
        /// Gets the underlying <see cref="MethodInfo"/> of this method trait.
        /// </summary>
        public MethodInfo underlyingMethodInfo => m_underlyingMethodInfo;

        /// <summary>
        /// Gets a Boolean value indicating whether this method has a return value.
        /// </summary>
        public bool hasReturn => m_hasReturn;

        /// <summary>
        /// Gets the return type of this method.
        /// </summary>
        /// <value>The return type of the method. If this method does not return a value, the
        /// value of this property is null.</value>
        public Class returnType => m_returnType;

        /// <summary>
        /// Gets a Boolean value indicating whether this method is final (cannot be overridden).
        /// </summary>
        public virtual bool isFinal {
            get {
                var attrs = m_underlyingMethodInfo.Attributes;
                return (attrs & (MethodAttributes.Final | MethodAttributes.Virtual)) != MethodAttributes.Virtual;
            }
        }

        /// <summary>
        /// Gets a Boolean value indicating whether this method is an override of a base class method.
        /// </summary>
        public bool isOverride => m_isOverride;

        /// <summary>
        /// Gets a Boolean value indicating whether this method has a "rest" parameter.
        /// </summary>
        public bool hasRest => m_hasRest;

        /// <summary>
        /// Gets the number of formal parameters (required and optional) declared by this method,
        /// excluding the "rest" parameter (if any).
        /// </summary>
        public int paramCount => m_params.Length;

        /// <summary>
        /// Gets the number of required formal parameters declared by this method.
        /// </summary>
        public int requiredParamCount => m_requiredArgCount;

        /// <summary>
        /// Returns a read-only array view containing <see cref="MethodTraitParameter"/> instances representing
        /// this method's declared formal parameters.
        /// </summary>
        /// <returns>A <see cref="ReadOnlyArrayView{MethodTraitParameter}"/> containing
        /// instances representing the method's declared formal parameters.</returns>
        public ReadOnlyArrayView<MethodTraitParameter> getParameters() => new ReadOnlyArrayView<MethodTraitParameter>(m_params);

        /// <summary>
        /// Gets a Boolean value indicating whether this method's signature is untyped.
        /// </summary>
        internal bool hasUntypedSignature => m_hasUntypedSig;

        /// <summary>
        /// Sets the underlying <see cref="MethodInfo"/> of the method trait represented by this
        /// instance.
        /// </summary>
        /// <param name="underlyingMethodInfo">The underlying <see cref="MethodInfo"/> for this method trait.</param>
        protected private void setUnderlyingMethodInfo(MethodInfo underlyingMethodInfo) {
            m_underlyingMethodInfo = underlyingMethodInfo;
        }

        /// <summary>
        /// Sets the signature of this method.
        /// </summary>
        ///
        /// <param name="hasReturn">Set this to true if the method returns a value, false if it does
        /// not.</param>
        /// <param name="returnType">The method's return type, null if <paramref name="hasReturn"/>
        /// is false.</param>
        /// <param name="parameters">An array of <see cref="MethodTraitParameter"/> instances
        /// representing the method's formal parameters.</param>
        /// <param name="hasRest">True if the method acccepts a "rest" parameter.</param>
        protected private void setSignature(
            bool hasReturn, Class returnType, MethodTraitParameter[] parameters, bool hasRest)
        {
            m_hasReturn = hasReturn;
            m_returnType = returnType;
            m_params = parameters;
            m_hasRest = hasRest;

            bool hasUntypedSig = true;
            int requiredArgCount = 0;

            if (!hasReturn || returnType != null) {
                hasUntypedSig = false;
            }

            for (int i = 0; i < parameters.Length; i++) {
                var param = parameters[i];
                if (param.type != null)
                    hasUntypedSig = false;
                if (!param.isOptional)
                    requiredArgCount = i + 1;
            }

            m_hasUntypedSig = hasUntypedSig;
            m_requiredArgCount = requiredArgCount;

            m_hasScope = isStatic && m_params.Length > 0
                && m_params[0].type?.underlyingType == typeof(ScopedClosureReceiver);
        }

        /// <summary>
        /// Sets a value indicating whether this method overrides a base class method.
        /// </summary>
        /// <param name="isOverride">True if this method overrides a base class method, otherwise false.</param>
        protected private void setIsOverride(bool isOverride) {
            m_isOverride = isOverride;
        }

        /// <summary>
        /// Gets the value of the trait on a specified target object.
        /// </summary>
        /// <param name="target">The target object for which to get the value of the trait. This is
        /// ignored for static and global traits.</param>
        /// <param name="value">The value of the trait on the specified object.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public override BindStatus tryGetValue(ASAny target, out ASAny value) {
            value = createMethodClosure(target.value);
            return BindStatus.SUCCESS;
        }

        /// <summary>
        /// Sets the value of the trait on a specified target object.
        /// </summary>
        /// <param name="target">The target object for which to set the value of the trait. This is
        /// ignored for static and global traits.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        public override BindStatus trySetValue(ASAny target, ASAny value) => BindStatus.FAILED_ASSIGNMETHOD;

        /// <summary>
        /// Invokes the trait as a function on the specified target object.
        /// </summary>
        /// <param name="target">The target object on which to invoke the trait.</param>
        /// <param name="receiver">The receiver of the function call. (This argument is ignored
        /// when this method is called on an instance of <see cref="MethodTrait"/>.)</param>
        /// <param name="args">The arguments to the function call.</param>
        /// <param name="result">The return value of the function call.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>Any error: An exception is thrown by the invoked method.</description></item>
        /// <item>
        /// <description>ArgumentError #1063: The length of <paramref name="args"/> is less than the number of required parameters
        /// declared by this method, or is greater than the number of parameters and the method does
        /// not have a "rest" parameter.</description>
        /// </item>
        /// </list>
        /// </exception>
        public override BindStatus tryInvoke(ASAny target, ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) {
            if (target.isUndefined && !isStatic)
                throw ErrorHelper.createError(ErrorCode.UNDEFINED_REFERENCE_ERROR);

            if (args.Length < m_requiredArgCount || (!m_hasRest && args.Length > m_params.Length))
                throw ErrorHelper.createArgCountMismatchError(this, args.Length);

            result = m_lazyRuntimeDispatch.value(target, args);
            return BindStatus.SUCCESS;
        }

        /// <summary>
        /// Invokes the trait as a constructor on the specified target object.
        /// </summary>
        /// <param name="target">The target object on which to invoke the trait as a
        /// constructor.</param>
        /// <param name="args">The arguments to the constructor call.</param>
        /// <param name="result">The object created by the constructor.</param>
        /// <returns>A <see cref="BindStatus"/> indicating the result of the operation.</returns>
        /// <exception cref="AVM2Exception">An error occurs during the operation.</exception>
        /// <remarks>
        /// The <see cref="MethodTrait"/> implementation simply returns
        /// <see cref="BindStatus.FAILED_METHODCONSTRUCT" qualifyHint="true"/>.
        /// </remarks>
        public override BindStatus tryConstruct(ASAny target, ReadOnlySpan<ASAny> args, out ASAny result) {
            result = default(ASAny);
            return BindStatus.FAILED_METHODCONSTRUCT;
        }

        /// <summary>
        /// Creates a delegate of the specified type from the method.
        /// </summary>
        /// <param name="receiver">The "this" argument to be passed to the method when the
        /// delegate is invoked, if it is an instance method. For a static method, this is
        /// ignored.</param>
        /// <param name="scope">The scope argument to pass to the method when the delegate is
        /// invoked, if it accepts one. For methods that do not accept scope arguments, this
        /// is ignored.</param>
        ///
        /// <typeparam name="TDelegate">The type of the delegate. The signature of this type must be
        /// compatible with that of the method.</typeparam>
        ///
        /// <returns>The created delegate; null if the signature of <typeparamref name="TDelegate"/>
        /// is not compatible with that of the method, or if <paramref name="receiver"/> is not of
        /// the correct type.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>Error #10039: The method takes a scope argument and <paramref name="scope"/>
        /// is null.</description>
        /// </item>
        /// <item>
        /// <description>Error #10040: The value of <paramref name="receiver"/> is null and the method is an
        /// instance method.</description>
        /// </item>
        /// </list>
        /// </exception>
        public TDelegate createDelegate<TDelegate>(ASObject receiver = null, ASObject scope = null) where TDelegate : Delegate {
            if (m_hasScope) {
                if (scope == null)
                    throw ErrorHelper.createError(ErrorCode.MARIANA__METHOD_SCOPE_ARG_REQUIRED, name.ToString());

                receiver = new ScopedClosureReceiver(receiver, scope, null);
            }

            if (!isStatic && receiver == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__METHOD_CLOSURE_INVALID_RECEIVER);

            return ReflectUtil.makeDelegate<TDelegate>(underlyingMethodInfo, receiver);
        }

        /// <summary>
        /// Creates a method closure of this method with the specified receiver and returns it.
        /// </summary>
        ///
        /// <param name="receiver">The receiver of the method closure. For a static method, this is
        /// ignored. For an instance method, this must be set to a non-null value. All calls to the
        /// returned method closure will always use this receiver, regardless of any receiver provided
        /// at invocation time.</param>
        ///
        /// <returns>The created method closure.</returns>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>Error #10041: This method takes a scope argument.</description></item>
        /// <item>
        /// <description>Error #10040: The value of <paramref name="receiver"/> is null and the method is an
        /// instance method.</description>
        /// </item>
        /// </list>
        /// </exception>
        public ASFunction createMethodClosure(ASObject receiver = null) {
            if (m_hasScope)
                throw ErrorHelper.createError(ErrorCode.MARIANA__METHOD_CLOSURE_SCOPE_ARG, name.ToString());
            if (!isStatic && receiver == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__METHOD_CLOSURE_INVALID_RECEIVER);

            return new ASMethodClosure(this, receiver);
        }

        /// <summary>
        /// Creates a function closure for this method and returns it.
        /// </summary>
        /// <param name="scope">The scope object that is to be passed to the method when the
        /// closure is invoked. If this method does not accept a scope argument, this is ignored.
        /// Otherwise, it must be an instance of the scope argument type accepted by the method.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item>
        /// <description>Error #10039: The method contains a scope argument, and <paramref name="scope"/>
        /// is null.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// <para>If the <paramref name="scope"/> object has a type which is different from the type
        /// of the scope argument accepted by the method, an invalid cast exception will be thrown
        /// when the method is called.</para>
        /// </remarks>
        public ASFunction createFunctionClosure(object scope = null) {
            if (m_hasScope && scope == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__METHOD_SCOPE_ARG_REQUIRED, name.ToString());

            return new ASFunctionClosure(this, m_hasScope ? scope : null);
        }

        /// <summary>
        /// Returns a string representation of the currend <see cref="MethodTrait"/>.
        /// </summary>
        /// <returns>A string representation of the currend <see cref="MethodTrait"/>.</returns>
        public override string ToString() {
            var sb = new System.Text.StringBuilder();

            if (isStatic)
                sb.Append("static ");
            if (isFinal && !isStatic)
                sb.Append("final ");
            if (isOverride)
                sb.Append("override ");

            sb.Append("function ");
            sb.Append(name.ToString());

            sb.Append('(');
            MethodTraitParameter.paramListToString(m_params, sb);

            if (hasRest) {
                if (paramCount != 0)
                    sb.Append(", ");
                sb.Append("...rest");
            }

            sb.Append("): ");

            if (hasReturn)
                sb.Append((returnType == null) ? "*" : returnType.name.ToString());
            else
                sb.Append("void");

            return sb.ToString();
        }

        /// <summary>
        /// Creates a standalone method trait from a <see cref="MethodInfo"/>. The created method trait
        /// will not be a part of any class.
        /// </summary>
        /// <returns>The created <see cref="MethodTrait"/>, which can be invoked to call the underlying
        /// method represented by <paramref name="methodInfo"/>.</returns>
        /// <param name="methodInfo">A <see cref="MethodInfo"/> instance representing the underlying method.
        /// This must represent a static public method that does not contain any generic parameters.</param>
        ///
        /// <exception cref="AVM2Exception">
        /// <list type="bullet">
        /// <item><description>ArgumentError #10060: <paramref name="methodInfo"/> is null.</description></item>
        /// <item>
        /// <description>NativeClassLoadError #10140: <paramref name="methodInfo"/> represents an instance method
        /// or a method that contains open generic parameters.</description>
        /// </item>
        /// </list>
        /// </exception>
        ///
        /// <remarks>
        /// The signature of the method represented by <paramref name="methodInfo"/> must satisfy
        /// the same requirements as those for methods of native classes exported to the AVM2. See
        /// notes on <see cref="AVM2ExportTraitAttribute"/> for further details. However, unlike
        /// exported methods on classes, methods used to create standalone <see cref="MethodTrait"/>
        /// instances can be non-public and can be (completely constructed) generic methods.
        /// </remarks>
        public static MethodTrait createNativeMethod(MethodInfo methodInfo) {
            if (methodInfo == null)
                throw ErrorHelper.createError(ErrorCode.MARIANA__ARGUMENT_NULL, nameof(methodInfo));

            CoreClasses.ensureClassesLoaded();
            return NativeClass.internalCreateStandAloneMethod(methodInfo);
        }

    }

}
