using System;
using System.Collections.Generic;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class ASBooleanTest {

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void valueOfMethodTest(bool value) {
            Assert.Equal(value, ASBoolean.valueOf(value));
            Assert.Equal(value, ((ASBoolean)(ASObject)value).valueOf());
        }

        [Theory]
        [InlineData(false, "false")]
        [InlineData(true, "true")]
        public void toStringMethodTest(bool value, string expected) {
            Assert.Equal(expected, ((ASBoolean)(ASObject)value).AS_toString());
        }

        public static IEnumerable<object[]> booleanClassRuntimeInvokeAndConstructTest_data() {
            ASObject obj1 = new ConvertibleMockObject(boolValue: true);
            ASObject obj2 = new ConvertibleMockObject(boolValue: false);

            return TupleHelper.toArrays<ASAny[], bool>(
                (Array.Empty<ASAny>(), false),
                (new ASAny[] {default}, false),
                (new ASAny[] {ASAny.@null}, false),
                (new ASAny[] {obj1}, true),
                (new ASAny[] {obj2}, false),
                (new ASAny[] {default, obj1}, false),
                (new ASAny[] {ASAny.@null, obj1}, false),
                (new ASAny[] {obj1, obj2}, true),
                (new ASAny[] {obj2, obj1}, false),
                (new ASAny[] {obj1, obj2, default, obj1}, true)
            );
        }

        [Theory]
        [MemberData(nameof(booleanClassRuntimeInvokeAndConstructTest_data))]
        public void booleanClassRuntimeInvokeAndConstructTest(ASAny[] args, bool expected) {
            Class klass = Class.fromType(typeof(bool));

            check(klass.invoke(args));
            check(klass.construct(args));

            void check(ASAny result) {
                Assert.IsType<ASBoolean>(result.value);
                Assert.Same(klass, result.AS_class);
                Assert.Equal(expected, ASObject.AS_toBoolean(result.value));
            }
        }

    }

}
