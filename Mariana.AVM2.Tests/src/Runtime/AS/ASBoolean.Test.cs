using System;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.TestDoubles;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class ASBooleanTest {

        private static readonly Class s_booleanClass = Class.fromType<bool>();

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void shouldBoxBooleanValue(bool value) {
            ASObject asObj = value;
            check(asObj);

            ASAny asAny = value;
            check(asAny.value);

            check(ASObject.AS_fromBoolean(value));
            check(ASAny.AS_fromBoolean(value).value);

            void check(ASObject o) {
                Assert.IsType<ASBoolean>(o);
                Assert.Same(s_booleanClass, o.AS_class);
                Assert.Equal(value, ASObject.AS_toBoolean(o));
                Assert.Equal(value, ASAny.AS_toBoolean(new ASAny(o)));
            }
        }

        [Fact]
        public void nullAndUndefinedShouldConvertToFalse() {
            Assert.False((bool)(ASObject)null);
            Assert.False(ASObject.AS_toBoolean(null));
            Assert.False((bool)default(ASAny));
            Assert.False(ASAny.AS_toBoolean(default));
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(true, 1)]
        public void shouldConvertToIntUintNumber(bool value, int expected) {
            Assert.Equal(expected, (int)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_toInt(ASObject.AS_fromBoolean(value)));
            Assert.Equal(expected, (int)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_toInt(ASAny.AS_fromBoolean(value)));

            Assert.Equal((uint)expected, (uint)(ASObject)value);
            Assert.Equal((uint)expected, ASObject.AS_toUint(ASObject.AS_fromBoolean(value)));
            Assert.Equal((uint)expected, (uint)(ASAny)value);
            Assert.Equal((uint)expected, ASAny.AS_toUint(ASAny.AS_fromBoolean(value)));

            Assert.Equal((double)expected, (double)(ASObject)value);
            Assert.Equal((double)expected, ASObject.AS_toNumber(ASObject.AS_fromBoolean(value)));
            Assert.Equal((double)expected, (double)(ASAny)value);
            Assert.Equal((double)expected, ASAny.AS_toNumber(ASAny.AS_fromBoolean(value)));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void valueOf_shouldReturnValue(bool value) {
            Assert.Equal(value, ASBoolean.valueOf(value));
            Assert.Equal(value, ((ASBoolean)(ASObject)value).valueOf());
        }

        [Theory]
        [InlineData(false, "false")]
        [InlineData(true, "true")]
        public void shouldConvertToString(bool value, string expected) {
            Assert.Equal(expected, (string)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_coerceString(ASObject.AS_fromBoolean(value)));
            Assert.Equal(expected, ASObject.AS_convertString(ASObject.AS_fromBoolean(value)));
            Assert.Equal(expected, (string)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_coerceString(ASAny.AS_fromBoolean(value)));
            Assert.Equal(expected, ASAny.AS_convertString(ASAny.AS_fromBoolean(value)));

            Assert.Equal(expected, ASBoolean.AS_convertString(value));
            Assert.Equal(expected, ((ASBoolean)(ASObject)value).AS_toString());
        }

        [Fact]
        public void shouldConvertFromObject() {
            ASObject falseObj = new SpyObjectWithConversions() {boolValue = false};
            ASObject trueObj = new SpyObjectWithConversions() {boolValue = true};

            Assert.False((bool)falseObj);
            Assert.False(ASObject.AS_toBoolean(falseObj));
            Assert.False((bool)(ASAny)falseObj);
            Assert.False(ASAny.AS_toBoolean((ASAny)falseObj));

            Assert.True((bool)trueObj);
            Assert.True(ASObject.AS_toBoolean(trueObj));
            Assert.True((bool)(ASAny)trueObj);
            Assert.True(ASAny.AS_toBoolean((ASAny)trueObj));
        }

        [Fact]
        public void classInvokeOrConstruct_shouldConvertFirstArg() {
            ASObject falseObj = new SpyObjectWithConversions() {boolValue = false};
            ASObject trueObj = new SpyObjectWithConversions() {boolValue = true};

            check(s_booleanClass.invoke(new ASAny[] {}), false);
            check(s_booleanClass.invoke(new ASAny[] {default}), false);
            check(s_booleanClass.invoke(new ASAny[] {ASAny.@null}), false);
            check(s_booleanClass.invoke(new ASAny[] {falseObj}), false);
            check(s_booleanClass.invoke(new ASAny[] {trueObj}), true);

            check(s_booleanClass.invoke(new ASAny[] {default, falseObj}), false);
            check(s_booleanClass.invoke(new ASAny[] {default, trueObj}), false);
            check(s_booleanClass.invoke(new ASAny[] {ASAny.@null, trueObj}), false);
            check(s_booleanClass.invoke(new ASAny[] {falseObj, falseObj}), false);
            check(s_booleanClass.invoke(new ASAny[] {falseObj, trueObj}), false);
            check(s_booleanClass.invoke(new ASAny[] {trueObj, falseObj}), true);
            check(s_booleanClass.invoke(new ASAny[] {trueObj, trueObj}), true);

            check(s_booleanClass.invoke(new ASAny[] {trueObj, falseObj, falseObj, trueObj}), true);
            check(s_booleanClass.invoke(new ASAny[] {falseObj, trueObj, trueObj, trueObj}), false);

            check(s_booleanClass.construct(new ASAny[] {}), false);
            check(s_booleanClass.construct(new ASAny[] {default}), false);
            check(s_booleanClass.construct(new ASAny[] {ASAny.@null}), false);
            check(s_booleanClass.construct(new ASAny[] {falseObj}), false);
            check(s_booleanClass.construct(new ASAny[] {trueObj}), true);

            check(s_booleanClass.construct(new ASAny[] {default, falseObj}), false);
            check(s_booleanClass.construct(new ASAny[] {default, trueObj}), false);
            check(s_booleanClass.construct(new ASAny[] {ASAny.@null, trueObj}), false);
            check(s_booleanClass.construct(new ASAny[] {falseObj, falseObj}), false);
            check(s_booleanClass.construct(new ASAny[] {falseObj, trueObj}), false);
            check(s_booleanClass.construct(new ASAny[] {trueObj, falseObj}), true);
            check(s_booleanClass.construct(new ASAny[] {trueObj, trueObj}), true);

            check(s_booleanClass.construct(new ASAny[] {trueObj, falseObj, falseObj, trueObj}), true);
            check(s_booleanClass.construct(new ASAny[] {falseObj, trueObj, trueObj, trueObj}), false);

            void check(ASAny o, bool value) {
                Assert.IsType<ASBoolean>(o.value);
                Assert.Same(s_booleanClass, o.value.AS_class);
                Assert.Equal(value, ASObject.AS_toBoolean(o.value));
            }
        }

    }

}
