using System;
using Xunit;
using Mariana.AVM2.Core;

namespace Mariana.AVM2.Tests.Helpers {

    internal static class AssertHelper {

        /// <summary>
        /// Asserts that a given test function throws an exception of type <see cref="AVM2Exception"/>
        /// whose value is an Error instance with the given error code.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <param name="testFunc">The function to test.</param>
        public static void throwsErrorWithCode(ErrorCode code, Action testFunc) {
            var ex = Assert.Throws<AVM2Exception>(testFunc);
            Assert.Equal(code, (ErrorCode)((ASError)ex.thrownValue).errorID);
        }

        /// <summary>
        /// Asserts that a given test function throws an exception of type <see cref="AVM2Exception"/>
        /// whose value is an Error instance with the given error code.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <param name="testFunc">The function to test.</param>
        public static void throwsErrorWithCode(ErrorCode code, Func<object> testFunc) {
            var ex = Assert.Throws<AVM2Exception>(testFunc);
            Assert.Equal(code, (ErrorCode)((ASError)ex.thrownValue).errorID);
        }

    }

}
