using System;
using Mariana.AVM2.Core;
using Xunit;

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
            var expectedType = ErrorHelper.createErrorObject(code, new object[9]).GetType();
            Assert.IsType(expectedType, ex.thrownValue.value);
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

        /// <summary>
        /// Asserts that two instances of the <see cref="ASAny"/> type are both undefined, both null
        /// or both containing the same object.
        /// </summary>
        /// <param name="expected">The expected <see cref="ASAny"/> instance.</param>
        /// <param name="actual">The actual instance to check against <see cref="expected"/>.</param>
        public static void identical(ASAny expected, ASAny actual) {
            if (expected.isUndefined)
                Assert.True(actual.isUndefined, $"Expected {actual} to be undefined.");
            else if (expected.isNull)
                Assert.Null(actual.value);
            else
                Assert.Same(expected.value, actual.value);
        }

        /// <summary>
        /// Asserts that two floating-point values are equal. This considers NaNs to be equal
        /// to each other, and the +0 and -0 floating-point values to be not equal.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        public static void floatIdentical(double expected, double actual) {
            if (Double.IsNaN(expected)) {
                Assert.True(Double.IsNaN(actual), $"Expected {actual} to be NaN.");
            }
            else if (expected == 0.0) {
                Assert.True(
                    actual == 0.0 && Double.IsNegative(expected) == Double.IsNegative(actual),
                    $"Expected {actual} to be {(Double.IsNegative(expected) ? "negative" : "positive")} zero."
                );
            }
            else {
                Assert.Equal(expected, actual);
            }
        }

        /// <summary>
        /// Asserts that two instances of the <see cref="ASAny"/> type are equal when compared
        /// using the strict equality operator.
        /// </summary>
        /// <param name="expected">The expected <see cref="ASAny"/> instance.</param>
        /// <param name="actual">The actual instance to check against <see cref="expected"/>.</param>
        public static void strictEqual(ASAny expected, ASAny actual) {
            Assert.True(ASAny.AS_strictEq(expected, actual), $"Expected {actual} to be strictly equal to {expected}.");
        }

        /// <summary>
        /// Asserts that two instances of the <see cref="ASAny"/> type are equal, using value equality
        /// for primitive types and reference equality for object types. This considers NaNs to be equal
        /// to each other, and the +0 and -0 floating-point values to be not equal.
        /// </summary>
        /// <param name="expected">The expected <see cref="ASAny"/> instance.</param>
        /// <param name="actual">The actual instance to check against <see cref="expected"/>.</param>
        public static void valueIdentical(ASAny expected, ASAny actual) {
            if (ASObject.AS_isNumeric(expected.value) && ASObject.AS_isNumeric(actual.value))
                floatIdentical((double)expected, (double)actual);
            else if (ASObject.AS_isPrimitive(expected.value) && ASObject.AS_isPrimitive(actual.value))
                strictEqual(expected, actual);
            else
                identical(expected, actual);
        }

    }

}
