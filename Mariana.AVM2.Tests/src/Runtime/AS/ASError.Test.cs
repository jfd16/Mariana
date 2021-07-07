using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mariana.AVM2.Core;
using Mariana.AVM2.Native;
using Mariana.AVM2.Tests.Helpers;
using Mariana.Common;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class ASErrorTest {

        internal static ZoneStaticData<ASError> stackTraceTestError = new ZoneStaticData<ASError>();

        public static IEnumerable<object[]> namePropertyTest_noNameProvided_data = TupleHelper.toArrays<ASError, ASAny>(
            (new ASError(""), "Error"),
            (new ASError("Hello"), "Error"),
            (new ASError("Hello", 123), "Error"),
            (new ASErrorTest_DerivedError(), "Error"),

            (new ASArgumentError(""), "ArgumentError"),
            (new ASDefinitionError(""), "DefinitionError"),
            (new ASEvalError(""), "EvalError"),
            (new ASRangeError(""), "RangeError"),
            (new ASReferenceError(""), "ReferenceError"),
            (new ASSecurityError(""), "SecurityError"),
            (new ASSyntaxError(""), "SyntaxError"),
            (new ASTypeError(""), "TypeError"),
            (new ASURIError(""), "URIError"),
            (new ASVerifyError(""), "VerifyError")
        );

        [Theory]
        [MemberData(nameof(namePropertyTest_noNameProvided_data))]
        public void namePropertyTest_noNameProvided(ASError obj, ASAny expectedName) {
            Assert.Equal(expectedName, obj.name);
        }

        public static IEnumerable<object[]> messagePropertyTest_data = TupleHelper.toArrays<ASError, ASAny>(
            (new ASError(""), ""),
            (new ASError("Hello"), "Hello"),
            (new ASErrorTest_DerivedError(), ""),
            (new ASError("Hello", 1234), "Hello"),

            (new ASError(ASAny.@null), ASAny.@null),
            (new ASError(ASAny.@null, 1234), ASAny.@null),
            (new ASError(ASAny.undefined), ASAny.undefined),
            (new ASError(ASAny.undefined, 1234), ASAny.undefined),
            (new ASError(1344), 1344),
            (new ASError(true), true)
        );

        [Theory]
        [MemberData(nameof(messagePropertyTest_data))]
        public void messagePropertyTest(ASError obj, ASAny message) {
            AssertHelper.valueIdentical(message, obj.message);
        }

        public static IEnumerable<object[]> errorIDPropertyTest_data = TupleHelper.toArrays(
            (new ASError(""), 0),
            (new ASError("Hello"), 0),
            (new ASErrorTest_DerivedError(), 0),
            (new ASError("Hello", 1234), 1234)
        );

        [Theory]
        [MemberData(nameof(errorIDPropertyTest_data))]
        public void errorIDPropertyTest(ASError obj, int expectedID) {
            Assert.Equal(expectedID, obj.errorID);
        }

        public static IEnumerable<object[]> toStringMethodTest_data = TupleHelper.toArrays(
            (new ASError(""), "Error"),
            (new ASError(ASAny.@null), "Error: null"),
            (new ASError("Hello"), "Error: Hello"),

            (new ASError("Hello", 1234), "Error: Hello"),
            (new ASError(ASAny.undefined, 1234), "Error: undefined"),
            (new ASError("", 1234), "Error"),

            (new ASError("") {name = ASAny.@null}, "null"),
            (new ASError(ASAny.@null) {name = ASAny.undefined}, "undefined: null"),
            (new ASError("Hello") {name = ASAny.@null}, "null: Hello"),
            (new ASError(ASAny.undefined) {name = 1354.8}, "1354.8: undefined"),

            (new ASError("") {name = "Foo"}, "Foo"),
            (new ASError(ASAny.@null) {name = "Foo"}, "Foo: null"),
            (new ASError("Hello") {name = "Foo"}, "Foo: Hello"),
            (new ASError(16533) {name = "Foo"}, "Foo: 16533"),

            (new ASError("") {name = new ConvertibleMockObject(stringValue: "Foo")}, "Foo"),
            (new ASError(ASAny.@null) {name = new ConvertibleMockObject(stringValue: "Foo")}, "Foo: null"),
            (new ASError("Hello") {name = new ConvertibleMockObject(stringValue: "Foo")}, "Foo: Hello"),
            (new ASError(new ConvertibleMockObject(stringValue: "Hello")) {name = new ConvertibleMockObject(stringValue: "Foo")}, "Foo: Hello"),

            (new ASErrorTest_DerivedError(), "Error"),
            (new ASErrorTest_DerivedError() {message = ASAny.undefined}, "Error: undefined"),
            (new ASErrorTest_DerivedError() {message = "Hello"}, "Error: Hello"),
            (new ASErrorTest_DerivedError() {name = "Foo", message = "Hello"}, "Foo: Hello"),
            (new ASErrorTest_DerivedError() {name = 1948, message = new ConvertibleMockObject(stringValue: "Hello")}, "1948: Hello")
        );

        [Theory]
        [MemberData(nameof(toStringMethodTest_data))]
        public void toStringMethodTest(ASError error, string expected) {
            Assert.Equal(expected, error.ToString());
        }

        [Fact]
        public void toStringMethodTest_nameAndMessageChanged() {
            ASError err = new ASError("");

            err.message = ASAny.@null;
            Assert.Equal("Error: null", err.AS_toString());

            err.message = "";
            Assert.Equal("Error", err.AS_toString());

            err.message = "Hello";
            Assert.Equal("Error: Hello", err.AS_toString());

            err.name = "Error2";
            Assert.Equal("Error2: Hello", err.AS_toString());

            err.name = new ConvertibleMockObject(stringValue: "Error3");
            err.message = new ConvertibleMockObject(stringValue: "World");
            Assert.Equal("Error3: World", err.AS_toString());
        }

        public static IEnumerable<object[]> getStackTraceMethodTest_data = TupleHelper.toArrays<Action, string>(
            (
                () => ASErrorTest.stackTraceTestError.value = new ASError("Hello"),
                "Error: Hello"
            ),
            (
                ErrorStackTraceTestModule.method1,
                "Error: Hello\n" +
                "    at global/method1()"
            ),
            (
                ErrorStackTraceTestModule.method4,
                "Error: Hello\n" +
                "    at global/method1()"
            ),
            (
                ErrorStackTraceTestModule.method3,
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()\n" +
                "    at global/method3()"
            ),
            (
                ErrorStackTraceTestClass.method4,
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()"
            ),
            (
                () => (new ErrorStackTraceTestClass(null)).method2(0),
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()"
            ),
            (
                () => (new ErrorStackTraceTestClass(null)).method2(),
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()\n" +
                "    at abc::ErrorStackTraceTestClass/method2()"
            ),
            (
                () => (new ErrorStackTraceTestClass(null)).method1(),
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()\n" +
                "    at abc::ErrorStackTraceTestClass/method2()\n" +
                "    at abc::ErrorStackTraceTestClass/method1()"
            ),
            (
                () => new ErrorStackTraceTestClass(0),
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()\n" +
                "    at abc::ErrorStackTraceTestClass/method2()\n" +
                "    at abc::ErrorStackTraceTestClass/method1()"
            ),
            (
                () => new ErrorStackTraceTestClass(),
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()\n" +
                "    at abc::ErrorStackTraceTestClass/method2()\n" +
                "    at abc::ErrorStackTraceTestClass/method1()\n" +
                "    at abc::ErrorStackTraceTestClass()"
            ),
            (
                ErrorStackTraceTestModule.method2,
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()\n" +
                "    at abc::ErrorStackTraceTestClass/method2()\n" +
                "    at abc::ErrorStackTraceTestClass/method1()\n" +
                "    at abc::ErrorStackTraceTestClass()\n" +
                "    at global/abc::__method2()"
            ),
            (
                () => new ErrorStackTraceTestClass2(),
                "Error: Hello\n" +
                "    at ErrorStackTraceTestClass2/method1()"
            )
        );

        [Theory]
        [MemberData(nameof(getStackTraceMethodTest_data))]
        public void getStackTraceMethodTest(Action errorConstructor, string expectedStackTrace) {
            using (StaticZone zone = new StaticZone()) {
                zone.enterAndRun(() => {
                    errorConstructor();
                    Assert.Equal(expectedStackTrace, stackTraceTestError.value.getStackTrace());
                });
            }
        }
    }

    [AVM2ExportClass]
    public class ASErrorTest_DerivedError : ASError {
        static ASErrorTest_DerivedError() => TestAppDomain.ensureClassesLoaded(typeof(ASErrorTest_DerivedError));
        public ASErrorTest_DerivedError() : base("") { }
    }

    [AVM2ExportClass(nsUri = "abc")]
    public class ErrorStackTraceTestClass : ASObject {
        static ErrorStackTraceTestClass() => TestAppDomain.ensureClassesLoaded(typeof(ErrorStackTraceTestClass));

        [AVM2ExportTrait]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ErrorStackTraceTestClass() : this(0) {}

        [MethodImpl(MethodImplOptions.NoInlining)]
        public ErrorStackTraceTestClass(int x) : this(x, 0) {}

        [MethodImpl(MethodImplOptions.NoInlining)]
        private ErrorStackTraceTestClass(int x, int y) => method1();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public ErrorStackTraceTestClass(object x) {}

        [AVM2ExportTrait]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void method1() => method2();

        [AVM2ExportTrait]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void method2() => method2(0);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void method2(int x) => method3();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void method3() => method4();

        [AVM2ExportTrait(name = "__method4", nsUri = "abc")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void method4() => ASErrorTest.stackTraceTestError.value = new ASError("Hello");
    }

    [AVM2ExportClass]
    public class ErrorStackTraceTestClass2 : ASObject {
        static ErrorStackTraceTestClass2() => TestAppDomain.ensureClassesLoaded(typeof(ErrorStackTraceTestClass2));

        public ErrorStackTraceTestClass2() => method1();

        [AVM2ExportTrait]
        public void method1() => ASErrorTest.stackTraceTestError.value = new ASError("Hello");
    }

    [AVM2ExportModule]
    public static class ErrorStackTraceTestModule {
        static ErrorStackTraceTestModule() => TestAppDomain.ensureClassesLoaded(typeof(ErrorStackTraceTestModule));

        [AVM2ExportTrait]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void method1() => ASErrorTest.stackTraceTestError.value = new ASError("Hello");

        [AVM2ExportTrait(name = "__method2", nsUri = "abc")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void method2() => new ErrorStackTraceTestClass();

        [AVM2ExportTrait(nsUri = "abc", nsKind = NamespaceKind.PACKAGE_INTERNAL)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void method3() => ErrorStackTraceTestClass.method4();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void method4() => method5();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void method5() => method1();
    }

}
