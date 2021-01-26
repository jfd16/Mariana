using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Mariana.AVM2.Core;
using Mariana.AVM2.Native;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class ASErrorTest {

        private class DerivedError : ASError {}

        private static ApplicationDomain s_domainForStackTraceTest;
        private static object s_syncLock = new object();

        [Fact]
        public void shouldHaveDefaultNameIfNotSet() {
            Assert.Equal("Error", (new ASError()).name);
            Assert.Equal("Error", (new ASError("Hello")).name);
            Assert.Equal("Error", (new ASError("Hello", 123)).name);
            Assert.Equal("Error", (new DerivedError()).name);
        }

        [Fact]
        public void shouldGetMessage() {
            Assert.Equal("", (new ASError()).message);
            Assert.Equal("Hello", (new ASError("Hello")).message);
            Assert.Equal("Hello", (new ASError("Hello", 1234)).message);
            Assert.Null((new ASError(null)).message);
            Assert.Null((new ASError(null, 1234)).message);
        }

        [Fact]
        public void shouldGetErrorID() {
            Assert.Equal(0, (new ASError()).errorID);
            Assert.Equal(0, (new ASError("Hello")).errorID);
            Assert.Equal(1234, (new ASError("Hello", 1234)).errorID);
            Assert.Equal(0, (new ASError(null)).errorID);
            Assert.Equal(1234, (new ASError(null, 1234)).errorID);
        }

        [Fact]
        public void toString_shouldFormatObject() {
            var err = new ASError();

            err.message = null;
            Assert.Equal("Error", err.AS_toString());
            err.message = "";
            Assert.Equal("Error", err.AS_toString());
            err.message = "Hello";
            Assert.Equal("Error: Hello", err.AS_toString());

            err.name = "Error2";

            err.message = null;
            Assert.Equal("Error2", err.AS_toString());
            err.message = "";
            Assert.Equal("Error2", err.AS_toString());
            err.message = "Hello";
            Assert.Equal("Error2: Hello", err.AS_toString());

            err.name = "";

            err.message = null;
            Assert.Equal("", err.AS_toString());
            err.message = "";
            Assert.Equal("", err.AS_toString());
            err.message = "Hello";
            Assert.Equal(": Hello", err.AS_toString());

            err.name = null;

            err.message = null;
            Assert.Equal("null", err.AS_toString());
            err.message = "";
            Assert.Equal("null", err.AS_toString());
            err.message = "Hello";
            Assert.Equal("null: Hello", err.AS_toString());
        }

        [Fact]
        public void builtinErrors_shouldHaveNames() {
            Assert.Equal("ArgumentError", (new ASArgumentError()).name);
            Assert.Equal("DefinitionError", (new ASDefinitionError()).name);
            Assert.Equal("EvalError", (new ASEvalError()).name);
            Assert.Equal("RangeError", (new ASRangeError()).name);
            Assert.Equal("ReferenceError", (new ASReferenceError()).name);
            Assert.Equal("SecurityError", (new ASSecurityError()).name);
            Assert.Equal("SyntaxError", (new ASSyntaxError()).name);
            Assert.Equal("TypeError", (new ASTypeError()).name);
            Assert.Equal("URIError", (new ASURIError()).name);
            Assert.Equal("VerifyError", (new ASVerifyError()).name);
        }

        [Fact]
        public void getStackTrace_shouldGetStackTraceOfConstruction() {
            LazyInitializer.EnsureInitialized(ref s_domainForStackTraceTest, ref s_syncLock, () => {
                var domain = new ApplicationDomain();
                domain.loadNativeClass(typeof(ErrorStackTraceTestClass));
                domain.loadNativeModule(typeof(ErrorStackTraceTestModule));
                return domain;
            });

            ErrorStackTraceTestModule.method1();
            Assert.Equal(
                "Error: Hello\n" +
                "    at global/method1()",
                ErrorStackTraceTestClass.createdError.getStackTrace()
            );

            ErrorStackTraceTestModule.method4();
            Assert.Equal(
                "Error: Hello\n" +
                "    at global/method1()",
                ErrorStackTraceTestClass.createdError.getStackTrace()
            );

            ErrorStackTraceTestModule.method3();
            Assert.Equal(
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()\n" +
                "    at global/method3()",
                ErrorStackTraceTestClass.createdError.getStackTrace()
            );

            ErrorStackTraceTestClass.method4();
            Assert.Equal(
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()",
                ErrorStackTraceTestClass.createdError.getStackTrace()
            );

            (new ErrorStackTraceTestClass(null)).method2(0);
            Assert.Equal(
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()",
                ErrorStackTraceTestClass.createdError.getStackTrace()
            );

            (new ErrorStackTraceTestClass(null)).method2();
            Assert.Equal(
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()\n" +
                "    at abc::ErrorStackTraceTestClass/method2()",
                ErrorStackTraceTestClass.createdError.getStackTrace()
            );

            (new ErrorStackTraceTestClass(null)).method1();
            Assert.Equal(
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()\n" +
                "    at abc::ErrorStackTraceTestClass/method2()\n" +
                "    at abc::ErrorStackTraceTestClass/method1()",
                ErrorStackTraceTestClass.createdError.getStackTrace()
            );

            new ErrorStackTraceTestClass(0);
            Assert.Equal(
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()\n" +
                "    at abc::ErrorStackTraceTestClass/method2()\n" +
                "    at abc::ErrorStackTraceTestClass/method1()",
                ErrorStackTraceTestClass.createdError.getStackTrace()
            );

            new ErrorStackTraceTestClass();
            Assert.Equal(
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()\n" +
                "    at abc::ErrorStackTraceTestClass/method2()\n" +
                "    at abc::ErrorStackTraceTestClass/method1()\n" +
                "    at abc::ErrorStackTraceTestClass()",
                ErrorStackTraceTestClass.createdError.getStackTrace()
            );

            ErrorStackTraceTestModule.method2();
            Assert.Equal(
                "Error: Hello\n" +
                "    at abc::ErrorStackTraceTestClass/abc::__method4()\n" +
                "    at abc::ErrorStackTraceTestClass/method2()\n" +
                "    at abc::ErrorStackTraceTestClass/method1()\n" +
                "    at abc::ErrorStackTraceTestClass()\n" +
                "    at global/abc::__method2()",
                ErrorStackTraceTestClass.createdError.getStackTrace()
            );
        }

    }

    [AVM2ExportClass(nsUri = "abc")]
    public class ErrorStackTraceTestClass : ASObject {
        [AVM2ExportTrait]
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
