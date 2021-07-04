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

    /// <summary>
    /// Represents a tracked invocation of a <see cref="MockFunctionObject"/>.
    /// </summary>
    internal readonly struct MockCallRecord {

        private readonly ASObject m_receiver;
        private readonly ASAny[] m_args;
        private readonly ASAny m_retval;
        private readonly bool m_isConstruct;

        /// <summary>
        /// Creates a new instance of <see cref="MockCallRecord"/>.
        /// </summary>
        /// <param name="receiver">The "this" argument of the function call, or null for a
        /// constructor call.</param>
        /// <param name="args">The arguments passed to the function call.</param>
        /// <param name="retval">The return value of the function call.</param>
        /// <param name="isConstruct">True if the function was called as a constructor, otherwise false.</param>
        public MockCallRecord(ASObject receiver, ReadOnlySpan<ASAny> args, ASAny retval, bool isConstruct) {
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
        /// Returns a value indicating whether this <see cref="MockCallRecord"/> instance is equal
        /// to another <see cref="MockCallRecord"/> instance.
        /// </summary>
        /// <param name="other">The <see cref="MockCallRecord"/> instance to compare with this instance.</param>
        /// <returns>True if this instance is equal to <paramref name="other"/>, otherwise false.</returns>
        public bool isEqualTo(in MockCallRecord other) {
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

    /// <summary>
    /// A function object that tracks invocations.
    /// </summary>
    [AVM2ExportClass]
    public sealed class MockFunctionObject : ASFunction {

        static MockFunctionObject() => TestAppDomain.ensureClassesLoaded(typeof(MockFunctionObject));

        private Func<ASAny, ASAny[], ASAny> m_func;
        private ASObject m_storedReceiver;
        private int m_length;

        private DynamicArray<MockCallRecord> m_calls = new DynamicArray<MockCallRecord>();

        /// <summary>
        /// Creates a new instance of <see cref="MockFunctionObject"/>.
        /// </summary>
        /// <param name="func">A delegate that will be called when the function is invoked. If this is null,
        /// no actual function is invoked (but calls will still be tracked) and the return value of every
        /// call is assumed to be undefined.</param>
        /// <param name="storedReceiver">If the created function instance is to represent a method closure, set
        /// this to the method closure receiver. Otherwise set to null.</param>
        /// <param name="argCount">The number of arguments accepted by the function. This is used for
        /// the value of the <see cref="ASFunction.length"/> property.</param>
        public MockFunctionObject(Func<ASAny, ASAny[], ASAny> func = null, ASObject storedReceiver = null, int argCount = 0) {
            m_func = func;
            m_storedReceiver = storedReceiver;
            m_length = argCount;
        }

        /// <summary>
        /// Creates a new <see cref="MockFunctionObject"/> that returns the given value when called.
        /// </summary>
        /// <param name="returnVal">The value that the function must return.</param>
        /// <returns>The creates <see cref="MockFunctionObject"/>.</returns>
        public static MockFunctionObject withReturn(ASAny returnVal) => new MockFunctionObject((r, args) => returnVal);

        /// <summary>
        /// Creates a clone of this <see cref="MockFunctionObject"/> instance with an empty history.
        /// </summary>
        public MockFunctionObject clone() => new MockFunctionObject(m_func, m_storedReceiver, m_length);

        public override ASObject storedReceiver => m_storedReceiver;

        public override int length => m_length;

        public override bool isMethodClosure => m_storedReceiver != null;

        public override bool AS_tryInvoke(ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) {
            ASObject actualReceiver = m_storedReceiver ?? receiver.value;
            result = (m_func != null) ? m_func(actualReceiver, args.ToArray()) : default;
            m_calls.add(new MockCallRecord(actualReceiver, args, result, false));
            return true;
        }

        public override bool AS_tryConstruct(ReadOnlySpan<ASAny> args, out ASAny result) {
            if (isMethodClosure) {
                result = default;
                return false;
            }

            result = (m_func != null) ? m_func(default, args.ToArray()) : default;
            m_calls.add(new MockCallRecord(null, args, result, true));
            return true;
        }

        /// <summary>
        /// Returns a reference to a <see cref="MockCallRecord"/> representing the last
        /// invocation of the function. This should only be accessed after at least one
        /// invocation has been made.
        /// </summary>
        internal ref readonly MockCallRecord lastCall => ref m_calls[m_calls.length - 1];

        /// <summary>
        /// Returns a <see cref="ReadOnlySpan{MockCallRecord}"/> representing the history of all
        /// invocations made on this instance, in the order in which the invocations were made.
        /// </summary>
        internal ReadOnlySpan<MockCallRecord> getCallHistory() => m_calls.asSpan();

    }

    /// <summary>
    /// A mock <see cref="FieldTrait"/> that can be used for testing.
    /// </summary>
    internal sealed class MockFieldTrait : FieldTrait {

        private DynamicArray<MockCallRecord> m_records = new DynamicArray<MockCallRecord>();
        private Func<ASAny, ASAny> m_getValueFunc;
        private Action<ASAny, ASAny> m_setValueFunc;

        /// <summary>
        /// Creates a new instance of <see cref="MockFieldTrait"/>.
        /// </summary>
        /// <param name="name">The name of the field.</param>
        /// <param name="isStatic">True if this field should be a static field, otherwise false.</param>
        /// <param name="isReadOnly">True if the field should be read-only, otherwise false.</param>
        /// <param name="fieldType">The data type of the field.</param>
        /// <param name="getValueFunc">A function that is called when a get-value operation is
        /// attempted. The function takes one argument (the target object) and the value returned
        /// by the function is the result of the operation.</param>
        /// <param name="setValueFunc">A function that is called when a set-value operation is
        /// attempted. The function takes two argument (the target object and the field value)
        /// and must not return a value.</param>
        public MockFieldTrait(
            QName name = default,
            Class declClass = null,
            bool isStatic = false,
            bool isReadOnly = false,
            Class fieldType = null,
            Func<ASAny, ASAny> getValueFunc = null,
            Action<ASAny, ASAny> setValueFunc = null
        )
            : base(name, declClass, declClass?.applicationDomain ?? TestAppDomain.instance, isStatic)
        {
            setIsReadOnly(isReadOnly);
            setFieldType(fieldType);
            setUnderlyingFieldInfo(null);

            m_getValueFunc = getValueFunc;
            m_setValueFunc = setValueFunc;
        }

        public override BindStatus tryGetValue(ASAny target, out ASAny value) {
            value = (m_getValueFunc != null) ? m_getValueFunc(target) : default;
            m_records.add(new MockCallRecord(target.value, new[] {target}, value, isConstruct: false));
            return BindStatus.SUCCESS;
        }

        public override BindStatus trySetValue(ASAny target, ASAny value) {
            if (isReadOnly)
                return BindStatus.FAILED_WRITEONLY;

            m_setValueFunc?.Invoke(target, value);
            m_records.add(new MockCallRecord(target.value, new[] {target, value}, retval: default, isConstruct: false));

            return BindStatus.SUCCESS;
        }

        public override BindStatus tryInvoke(ASAny target, ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) {
            tryGetValue(target, out ASAny func);
            return func.AS_tryInvoke(receiver, args, out result) ? BindStatus.SUCCESS : BindStatus.FAILED_NOTFUNCTION;
        }

        public override BindStatus tryConstruct(ASAny target, ReadOnlySpan<ASAny> args, out ASAny result) {
            tryGetValue(target, out ASAny func);
            return func.AS_tryConstruct(args, out result) ? BindStatus.SUCCESS : BindStatus.FAILED_NOTCONSTRUCTOR;
        }

        /// <summary>
        /// Returns a history of getValue/setValue invocations on this field trait.
        /// </summary>
        /// <returns>A <see cref="ReadOnlySpan{MockCallRecord}"/> containing a history of
        /// getValue/setValue invocations.</returns>
        ///
        /// <remarks>
        /// <para>
        /// For each getValue invocation, the corresponding record in the history has one argument
        /// (the target object) and its return value is the value returned to the getValueFunc passed
        /// into the <see cref="MockFieldTrait"/> constructor, or undefined if no getValueFunc is given.
        /// </para>
        /// <para>
        /// For each setValue invocation, the corresponding record in the history has two arguments
        /// (the target object and the value) and its return value is undefined.
        /// </para>
        /// </remarks>
        internal ReadOnlySpan<MockCallRecord> getHistory() => m_records.asSpan();

        /// <summary>
        /// Returns a clone of this <see cref="MockFieldTrait"/> with an empty history.
        /// </summary>
        /// <param name="newDeclClass">The class to set as the declaring class of the cloned
        /// <see cref="MockFieldTrait"/>. If this is null, the declaring class of this trait
        /// is used.</param>
        /// <returns>A clone of this <see cref="MockFieldTrait"/>.</returns>
        internal MockFieldTrait clone(Class newDeclClass = null) =>
            new MockFieldTrait(name, newDeclClass ?? declaringClass, isStatic, isReadOnly, fieldType, m_getValueFunc, m_setValueFunc);

    }

    /// <summary>
    /// A mock <see cref="MethodTrait"/> that can be used for testing.
    /// </summary>
    internal sealed class MockMethodTrait : MethodTrait {

        private DynamicArray<MockCallRecord> m_records = new DynamicArray<MockCallRecord>();
        private Func<ASAny, ASAny[], ASAny> m_invokeFunc;
        private bool m_isFinal;

        /// <summary>
        /// Creates a new instance of <see cref="MockMethodTrait"/>.
        /// </summary>
        /// <param name="name">The name of the trait.</param>
        /// <param name="declClass">The class declaring the trait, or null for a global trait.</param>
        /// <param name="isStatic">True if this method is static, otherwise false.</param>
        /// <param name="isFinal">True if this method is final, otherwise false.</param>
        /// <param name="isOverride">True if this method is an override, otherwise false.</param>
        /// <param name="hasReturn">True if this method returns a value, otherwise false.</param>
        /// <param name="returnType">The return type of the method. Ignored if <paramref name="hasReturn"/>
        /// is set to false.</param>
        /// <param name="parameters">The formal parameters of this method trait.</param>
        /// <param name="hasRest">True if this method takes a "rest" argument, otherwise false.</param>
        /// <param name="invokeFunc">A delegate that is invoked when this method trait is invoked.
        /// It takes two arguments (the target object and an array containing the method call arguments)
        /// and its return value is the return value of the method invocation.</param>
        public MockMethodTrait(
            QName name = default,
            Class declClass = null,
            bool isStatic = false,
            bool isFinal = false,
            bool isOverride = false,
            bool hasReturn = false,
            Class returnType = null,
            ReadOnlySpan<MethodTraitParameter> parameters = default,
            bool hasRest = false,
            Func<ASAny, ASAny[], ASAny> invokeFunc = null
        )
            : base(name, declClass, declClass?.applicationDomain ?? TestAppDomain.instance, isStatic)
        {
            setIsOverride(isOverride);
            setUnderlyingMethodInfo(null);
            setSignature(hasReturn, returnType, parameters.ToArray(), hasRest);

            m_isFinal = isFinal;
            m_invokeFunc = invokeFunc;
        }

        public override bool isFinal => m_isFinal;

        public override BindStatus tryInvoke(ASAny target, ASAny receiver, ReadOnlySpan<ASAny> args, out ASAny result) {
            result = (m_invokeFunc != null) ? m_invokeFunc(receiver, args.ToArray()) : default;
            m_records.add(new MockCallRecord(target.value, args, result, isConstruct: false));
            return BindStatus.SUCCESS;
        }

        public override BindStatus tryConstruct(ASAny target, ReadOnlySpan<ASAny> args, out ASAny result) {
            result = (m_invokeFunc != null) ? m_invokeFunc(default, args.ToArray()) : default;
            m_records.add(new MockCallRecord(target.value, args, result, isConstruct: true));
            return BindStatus.SUCCESS;
        }

        /// <summary>
        /// Returns a history of invocations of this method trait.
        /// </summary>
        internal ReadOnlySpan<MockCallRecord> getHistory() => m_records.asSpan();

        /// <summary>
        /// Returns a clone of this <see cref="MockMethodTrait"/> with an empty history.
        /// </summary>
        /// <param name="newDeclClass">The class to set as the declaring class of the cloned
        /// <see cref="MockMethodTrait"/>. If this is null, the declaring class of this trait
        /// is used.</param>
        /// <returns>A clone of this <see cref="MockMethodTrait"/>.</returns>
        internal MockMethodTrait clone(Class newDeclClass = null) {
            return new MockMethodTrait(
                name,
                newDeclClass ?? declaringClass,
                isStatic,
                isFinal,
                isOverride,
                hasReturn,
                returnType,
                getParameters().asSpan(),
                hasRest,
                m_invokeFunc
            );
        }

    }

    /// <summary>
    /// A mock <see cref="PropertyTrait"/> that can be used for testing.
    /// </summary>
    internal sealed class MockPropertyTrait : PropertyTrait {

        /// <summary>
        /// Creates a new instance of <see cref="MockMethodTrait"/>.
        /// </summary>
        /// <param name="name">The name of the trait.</param>
        /// <param name="declClass">The class declaring the trait, or null for a global trait.</param>
        /// <param name="isStatic">True if this method is static, otherwise false.</param>
        /// <param name="isFinal">True if this method is final, otherwise false.</param>
        /// <param name="isOverride">True if this method is an override, otherwise false.</param>
        /// <param name="hasReturn">True if this method returns a value, otherwise false.</param>
        /// <param name="returnType">The return type of the method. Ignored if <paramref name="hasReturn"/>
        /// is set to false.</param>
        /// <param name="parameters">The formal parameters of this method trait.</param>
        /// <param name="hasRest">True if this method takes a "rest" argument, otherwise false.</param>
        /// <param name="invokeFunc">A delegate that is invoked when this method trait is invoked.
        /// It takes two arguments (the target object and an array containing the method call arguments)
        /// and its return value is the return value of the method invocation.</param>
        public MockPropertyTrait(
            QName name,
            Class declClass = null,
            bool isStatic = false,
            MockMethodTrait getter = null,
            MockMethodTrait setter = null
        )
            : base(name, declClass, declClass?.applicationDomain ?? TestAppDomain.instance, isStatic, getter, setter)
        {

        }

        /// <summary>
        /// Returns a history of invocations of the getter method of this property.
        /// </summary>
        internal ReadOnlySpan<MockCallRecord> getGetterHistory() => ((MockMethodTrait)getter).getHistory();

        /// <summary>
        /// Returns a history of invocations of the setter method of this property.
        /// </summary>
        internal ReadOnlySpan<MockCallRecord> getSetterHistory() => ((MockMethodTrait)setter).getHistory();

        /// <summary>
        /// Returns a clone of this <see cref="MockPropertyTrait"/> with an empty history.
        /// </summary>
        /// <param name="newDeclClass">The class to set as the declaring class of the cloned
        /// <see cref="MockPropertyTrait"/>. If this is null, the declaring class of this trait
        /// is used.</param>
        /// <returns>A clone of this <see cref="MockPropertyTrait"/>.</returns>
        internal MockPropertyTrait clone(Class newDeclClass = null) {
            return new MockPropertyTrait(
                name,
                newDeclClass ?? declaringClass,
                isStatic,
                (getter == null) ? null : ((MockMethodTrait)getter).clone(),
                (setter == null) ? null : ((MockMethodTrait)setter).clone()
            );
        }

    }

    /// <summary>
    /// A class constructor for a <see cref="MockClass"/>.
    /// </summary>
    internal sealed class MockClassConstructor : ClassConstructor {

        private DynamicArray<MockCallRecord> m_records = new DynamicArray<MockCallRecord>();
        private Func<ASAny[], ASAny> m_invokeFunc;

        public MockClassConstructor(
            Class declClass = null,
            ReadOnlySpan<MethodTraitParameter> parameters = default,
            bool hasRest = false,
            Func<ASAny[], ASAny> invokeFunc = null
        )
            : base(declClass)
        {
            setSignature(parameters.ToArray(), hasRest);
            m_invokeFunc = invokeFunc;
        }

        public override ASAny invoke(ReadOnlySpan<ASAny> args) {
            ASAny result = (m_invokeFunc != null) ? m_invokeFunc(args.ToArray()) : default;
            m_records.add(new MockCallRecord(default, args, result, isConstruct: true));
            return result;
        }

        /// <summary>
        /// Returns a history of invocations of this constructor.
        /// </summary>
        internal ReadOnlySpan<MockCallRecord> getHistory() => m_records.asSpan();

        /// <summary>
        /// Returns a clone of this <see cref="MockClassConstructor"/> with an empty history.
        /// </summary>
        /// <param name="newDeclClass">The class to set as the declaring class of the cloned
        /// <see cref="MockClassConstructor"/>. If this is null, the declaring class of this constructor
        /// is used.</param>
        /// <returns>A clone of this <see cref="MockClassConstructor"/>.</returns>
        internal MockClassConstructor clone(Class newDeclClass = null) =>
            new MockClassConstructor(newDeclClass ?? declaringClass, getParameters().asSpan(), hasRest, m_invokeFunc);

    }

    /// <summary>
    /// A mock implementation of <see cref="ClassImpl"/> that can be used for testing purposes.
    /// </summary>
    internal sealed class MockClass : ClassImpl {

        private bool m_isInterface;
        private bool m_isFinal;

        private bool m_initClassCalled = false;

        /// <summary>
        /// Creates an instance of <see cref="MockClass"/>.
        /// </summary>
        /// <param name="name">The name of the class.</param>
        /// <param name="domain">The application domain in which the class is defined.</param>
        /// <param name="parent">The parent class of this class. If this is null, the parent class is set
        /// to Object.</param>
        /// <param name="isFinal">True if this class is a final class.</param>
        /// <param name="isInterface">True if this class is an interface.</param>
        /// <param name="isDynamic">True if this class is dynamic.</param>
        /// <param name="canHideInheritedTraits">True if this class declares traits that may hide
        /// traits from its parent class with conflicting names.</param>
        /// <param name="interfaces">The interfaces implemented by this class.</param>
        /// <param name="fields">The field traits declared by this class. These will be cloned.</param>
        /// <param name="methods">The method traits declared by this class. These will be cloned.</param>
        /// <param name="properties">The property traits declared by this class. These will be cloned.</param>
        /// <param name="constants">The constant traits declared by this class. These will be cloned.</param>
        /// <param name="constructor">The constructor of this class. This will be cloned.</param>
        /// <param name="classSpecials">A <see cref="ClassSpecials"/> instance containing any special
        /// methods defined by this class.</param>
        public MockClass(
            QName name = default,
            ApplicationDomain domain = null,
            Class parent = null,
            bool isFinal = false,
            bool isInterface = false,
            bool isDynamic = false,
            bool canHideInheritedTraits = false,
            ReadOnlySpan<Class> interfaces = default,
            ReadOnlySpan<MockFieldTrait> fields = default,
            ReadOnlySpan<MockMethodTrait> methods = default,
            ReadOnlySpan<MockPropertyTrait> properties = default,
            ReadOnlySpan<ConstantTrait> constants = default,
            MockClassConstructor constructor = null,
            ClassSpecials classSpecials = null
        )
            : base(name, domain ?? TestAppDomain.instance)
        {
            setConstructor(constructor);
            setParent(parent ?? Class.fromType(typeof(ASObject)));
            setInterfaces(interfaces.ToArray());
            setIsDynamic(isDynamic);
            setIsHidingAllowed(canHideInheritedTraits);
            setClassSpecials(classSpecials);

            m_isInterface = isInterface;
            m_isFinal = isFinal;

            for (int i = 0; i < fields.Length; i++)
                tryDefineTrait(fields[i].clone(this));

            for (int i = 0; i < methods.Length; i++)
                tryDefineTrait(methods[i].clone(this));

            for (int i = 0; i < properties.Length; i++)
                tryDefineTrait(properties[i].clone(this));

            for (int i = 0; i < constants.Length; i++) {
                ConstantTrait constantTrait = constants[i];
                tryDefineTrait(new ConstantTrait(constantTrait.name, this, applicationDomain, constantTrait.constantValue));
            }
        }

        private protected override void initClass() => m_initClassCalled = true;

        public override bool isInterface => m_isInterface;
        public override bool isFinal => m_isFinal;

        private protected override Class createVectorClass() =>
            throw new InvalidOperationException("Cannot use a MockClass as the element type of a Vector.");

        /// <summary>
        /// Returns a value indicating whether the <c>initClass</c> method was called on
        /// this mock class.
        /// </summary>
        public bool initClassCalled => m_initClassCalled;

        /// <summary>
        /// Returns a <see cref="ReadOnlySpan{MockCallRecord}"/> representing a history of
        /// invocations of the constructor of this class.
        /// </summary>
        public ReadOnlySpan<MockCallRecord> getConstructorHistory() => ((MockClassConstructor)constructor).getHistory();

    }

    /// <summary>
    /// An instance of a <see cref="MockClass"/>.
    /// </summary>
    public class MockClassInstance : ASObject {

        /// <summary>
        /// Creates a new instance of <see cref="MockClassInstance"/>.
        /// </summary>
        /// <param name="klass">The <see cref="MockClass"/> that should be set as the class
        /// of the created instance.</param>
        /// <param name="proto">The prototype chain successor of this object. If this is null,
        /// the prototype of <paramref name="klass"/> is used.</param>
        internal MockClassInstance(MockClass klass, ASObject proto = null) : base(klass, proto) { }

    }

}
