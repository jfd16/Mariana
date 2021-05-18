using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mariana.AVM2.Core;
using Mariana.AVM2.Native;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class ASErrorTest {

        public static IEnumerable<object[]> namePropertyTest_data = TupleHelper.toArrays(
            (new ASError(), "Error"),
            (new ASError("Hello"), "Error"),
            (new ASError("Hello", 123), "Error"),
            (new ASErrorTest_DerivedError(), "Error"),

            (new ASArgumentError(), "ArgumentError"),
            (new ASDefinitionError(), "DefinitionError"),
            (new ASEvalError(), "EvalError"),
            (new ASRangeError(), "RangeError"),
            (new ASReferenceError(), "ReferenceError"),
            (new ASSecurityError(), "SecurityError"),
            (new ASSyntaxError(), "SyntaxError"),
            (new ASTypeError(), "TypeError"),
            (new ASURIError(), "URIError"),
            (new ASVerifyError(), "VerifyError")
        );

        [Theory]
        [MemberData(nameof(namePropertyTest_data))]
        public void namePropertyTest_noNameProvided(ASError obj, string expectedName) {
            Assert.Equal(expectedName, obj.name);
        }

        public static IEnumerable<object[]> messagePropertyTest_data = TupleHelper.toArrays(
            (new ASError(), ""),
            (new ASError("Hello"), "Hello"),
            (new ASErrorTest_DerivedError(), ""),
            (new ASError("Hello", 1234), "Hello"),
            (new ASError(null), null),
            (new ASError(null, 1234), null)
        );

        [Theory]
        [MemberData(nameof(messagePropertyTest_data))]
        public void messagePropertyTest(ASError obj, string message) {
            Assert.Equal(message, obj.message);
        }

        public static IEnumerable<object[]> errorIDPropertyTest_data = TupleHelper.toArrays(
            (new ASError(), 0),
            (new ASError("Hello"), 0),
            (new ASErrorTest_DerivedError(), 0),
            (new ASError("Hello", 1234), 1234),
            (new ASError(null), 0),
            (new ASError(null, 1234), 1234)
        );

        [Theory]
        [MemberData(nameof(errorIDPropertyTest_data))]
        public void errorIDPropertyTest(ASError obj, int expectedID) {
            Assert.Equal(expectedID, obj.errorID);
        }

        public static IEnumerable<object[]> toStringMethodTest_data = TupleHelper.toArrays(
            (new ASError(), "Error"),
            (new ASError(null), "Error: null"),
            (new ASError("Hello"), "Error: Hello"),

            (new ASError("Hello", 1234), "Error: Hello"),
            (new ASError(null, 1234), "Error: null"),
            (new ASError("", 1234), "Error"),

            (new ASError() {name = null}, "null"),
            (new ASError(null) {name = null}, "null: null"),
            (new ASError("Hello") {name = null}, "null: Hello"),

            (new ASError() {name = "Foo"}, "Foo"),
            (new ASError(null) {name = "Foo"}, "Foo: null"),
            (new ASError("Hello") {name = "Foo"}, "Foo: Hello"),

            (new ASErrorTest_DerivedError(), "Error"),
            (new ASErrorTest_DerivedError() {message = null}, "Error: null"),
            (new ASErrorTest_DerivedError() {message = "Hello"}, "Error: Hello"),
            (new ASErrorTest_DerivedError() {name = "Foo", message = "Hello"}, "Foo: Hello")
        );

        [Theory]
        [MemberData(nameof(toStringMethodTest_data))]
        public void toStringMethodTest(ASError error, string expected) {
            Assert.Equal(expected, error.ToString());
        }

        [Fact]
        public void toStringMethodTest_nameAndMessageChanged() {
            ASError err = new ASError();

            err.message = null;
            Assert.Equal("Error: null", err.AS_toString());
            err.message = "";
            Assert.Equal("Error", err.AS_toString());
            err.message = "Hello";
            Assert.Equal("Error: Hello", err.AS_toString());
            err.name = "Error2";
            Assert.Equal("Error2: Hello", err.AS_toString());
        }

        public static IEnumerable<object[]> getStackTraceMethodTest_data = TupleHelper.toArrays<Action, string>(
            (
                () => ErrorStackTraceTestClass.createdError = new ASError("Hello"),
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
            )
        );

        [Theory]
        [MemberData(nameof(getStackTraceMethodTest_data))]
        public void getStackTraceMethodTest(Action errorConstructor, string expectedStackTrace) {
            errorConstructor();
            Assert.Equal(expectedStackTrace, ErrorStackTraceTestClass.createdError.getStackTrace());
        }
    }

    [AVM2ExportClass]
    public class ASErrorTest_DerivedError : ASError {
        static ASErrorTest_DerivedError() => TestAppDomain.ensureClassesLoaded(typeof(ASErrorTest_DerivedError));
    }

    [AVM2ExportClass(nsUri = "abc")]
    public class ErrorStackTraceTestClass : ASObject {
        static ErrorStackTraceTestClass() => TestAppDomain.ensureClassesLoaded(typeof(ErrorStackTraceTestClass));

        [ThreadStatic]
        public static ASError createdError;

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
        public static void method4() => createdError = new ASError("Hello");
    }

    [AVM2ExportModule]
    public static class ErrorStackTraceTestModule {
        static ErrorStackTraceTestModule() => TestAppDomain.ensureClassesLoaded(typeof(ErrorStackTraceTestModule));

        [AVM2ExportTrait]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void method1() => ErrorStackTraceTestClass.createdError = new ASError("Hello");

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
