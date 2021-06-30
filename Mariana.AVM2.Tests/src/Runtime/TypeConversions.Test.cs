using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mariana.AVM2.Core;
using Mariana.AVM2.Native;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    [AVM2ExportClass]
    public interface TypeConversionsTest_IA { }

    [AVM2ExportClass]
    public interface TypeConversionsTest_IB { }

    [AVM2ExportClass]
    public interface TypeConversionsTest_IC : TypeConversionsTest_IA { }

    [AVM2ExportClass]
    public class TypeConversionsTest_CA : ASObject { }

    [AVM2ExportClass]
    public class TypeConversionsTest_CB : TypeConversionsTest_CA, TypeConversionsTest_IB { }

    [AVM2ExportClass]
    public sealed class TypeConversionsTest_CC : TypeConversionsTest_CA, TypeConversionsTest_IC { }

    [AVM2ExportClass]
    public sealed class TypeConversionsTest_CD : ASObject { }

    public class TypeConversionsTest {

        private static readonly MockClass mockClassA, mockClassB, mockClassC, mockClassD;

        static TypeConversionsTest() {
            TestAppDomain.ensureClassesLoaded(
                typeof(TypeConversionsTest_CA),
                typeof(TypeConversionsTest_CB),
                typeof(TypeConversionsTest_CC),
                typeof(TypeConversionsTest_CD)
            );

            mockClassA = new MockClass();
            mockClassA.prototypeObject.AS_setProperty("toString", MockFunctionObject.withReturn("Hello CA"));
            mockClassA.prototypeObject.AS_setProperty("valueOf", MockFunctionObject.withReturn(0));

            mockClassB = new MockClass(
                parent: mockClassA,
                methods: new[] {new MockMethodTrait("toString", invokeFunc: (obj, args) => "Hello CB")}
            );

            mockClassC = new MockClass(
                parent: mockClassA,
                methods: new[] {new MockMethodTrait("valueOf", invokeFunc: (obj, args) => 9999.36)}
            );

            mockClassD = new MockClass(
                methods: new[] {new MockMethodTrait("toString", invokeFunc: (obj, args) => new ASArray())}
            );
        }

        private static readonly Class s_intClass = Class.fromType(typeof(int));
        private static readonly Class s_uintClass = Class.fromType(typeof(uint));
        private static readonly Class s_numberClass = Class.fromType(typeof(double));
        private static readonly Class s_booleanClass = Class.fromType(typeof(bool));
        private static readonly Class s_stringClass = Class.fromType(typeof(string));
        private static readonly Class s_objectClass = Class.fromType(typeof(ASObject));

        private static readonly MethodInfo s_objectCastGenMethod =
            typeof(ASObject).GetMethod(nameof(ASObject.AS_cast), 1, new[] {typeof(ASObject)});

        private static readonly MethodInfo s_anyCastGenMethod =
            typeof(ASAny).GetMethod(nameof(ASAny.AS_cast), 1, new[] {typeof(ASAny)});

        private static ASObject objWithMethods(params (string name, ASAny returnVal)[] methods) {
            var obj = new ASObject();

            for (int i = 0; i < methods.Length; i++)
                obj.AS_setProperty(methods[i].name, MockFunctionObject.withReturn(methods[i].returnVal));

            return obj;
        }

        private static void assertObjIsInt(int expectedValue, ASObject obj) {
            Assert.IsType<ASint>(obj);
            Assert.Same(s_intClass, obj.AS_class);

            Assert.Equal(expectedValue, (int)obj);
            Assert.Equal(expectedValue, ASObject.AS_toInt(obj));
            Assert.Equal(expectedValue, (int)new ASAny(obj));
            Assert.Equal(expectedValue, ASAny.AS_toInt(new ASAny(obj)));
        }

        private static void assertObjIsInt(int expectedValue, ASAny obj) => assertObjIsInt(expectedValue, obj.value);

        private static void assertObjIsUint(uint expectedValue, ASObject obj) {
            Assert.IsType<ASuint>(obj);
            Assert.Same(s_uintClass, obj.AS_class);

            Assert.Equal(expectedValue, (uint)obj);
            Assert.Equal(expectedValue, ASObject.AS_toUint(obj));
            Assert.Equal(expectedValue, (uint)new ASAny(obj));
            Assert.Equal(expectedValue, ASAny.AS_toUint(new ASAny(obj)));
        }

        private static void assertObjIsUint(uint expectedValue, ASAny obj) => assertObjIsUint(expectedValue, obj.value);

        private static void assertObjIsNumber(double expectedValue, ASObject obj) {
            Assert.IsType<ASNumber>(obj);
            Assert.Same(s_numberClass, obj.AS_class);

            AssertHelper.floatIdentical(expectedValue, (double)obj);
            AssertHelper.floatIdentical(expectedValue, ASObject.AS_toNumber(obj));
            AssertHelper.floatIdentical(expectedValue, (double)new ASAny(obj));
            AssertHelper.floatIdentical(expectedValue, ASAny.AS_toNumber(new ASAny(obj)));
        }

        private static void assertObjIsNumber(double expectedValue, ASAny obj) => assertObjIsNumber(expectedValue, obj.value);

        private static void assertObjIsBoolean(bool expectedValue, ASObject obj) {
            Assert.IsType<ASBoolean>(obj);
            Assert.Same(s_booleanClass, obj.AS_class);

            Assert.Equal(expectedValue, (bool)obj);
            Assert.Equal(expectedValue, ASObject.AS_toBoolean(obj));
            Assert.Equal(expectedValue, (bool)new ASAny(obj));
            Assert.Equal(expectedValue, ASAny.AS_toBoolean(new ASAny(obj)));
        }

        private static void assertObjIsBoolean(bool expectedValue, ASAny obj) => assertObjIsBoolean(expectedValue, obj.value);

        private static void assertObjIsString(string expectedValue, ASObject obj) {
            if (expectedValue == null) {
                Assert.Null(obj);
                return;
            }

            Assert.IsType<ASString>(obj);
            Assert.Same(s_stringClass, obj.AS_class);

            Assert.Equal(expectedValue, (string)obj);
            Assert.Equal(expectedValue, ASObject.AS_coerceString(obj));
            Assert.Equal(expectedValue, ASObject.AS_convertString(obj));
            Assert.Equal(expectedValue, (string)new ASAny(obj));
            Assert.Equal(expectedValue, ASAny.AS_coerceString(new ASAny(obj)));
            Assert.Equal(expectedValue, ASAny.AS_convertString(new ASAny(obj)));
        }

        private static void assertObjIsString(string expectedValue, ASAny obj) {
            if (expectedValue == null)
                Assert.True(obj.isNull);
            else
                assertObjIsString(expectedValue, obj.value);
        }

        public static IEnumerable<object[]> intTestData = TupleHelper.toArrays(
            0, 1, -1, 2, -2,
            127, 128, 129, -127, -128, -129,
            255, 256, 257,
            4718, 9993,
            32767, 32768, 32769, -32767, -32768, -32769,
            65535, 65536, 65537,
            123456789, 193847113,
            Int32.MaxValue, Int32.MinValue, -Int32.MaxValue
        );

        [Theory]
        [MemberData(nameof(intTestData))]
        public void intObjectConvertRoundTripTest(int value) {
            ASObject asObj = value;
            assertObjIsInt(value, asObj);

            ASAny asAny = value;
            assertObjIsInt(value, asAny.value);

            assertObjIsInt(value, ASObject.AS_fromInt(value));
            assertObjIsInt(value, ASAny.AS_fromInt(value));

            assertObjIsInt(value, ASObject.AS_coerceType(ASObject.AS_fromInt(value), s_intClass));
            assertObjIsInt(value, ASAny.AS_coerceType(ASObject.AS_fromInt(value), s_intClass));
        }

        [Theory]
        [MemberData(nameof(intTestData))]
        public void intToUintConvertTest(int value) {
            Assert.Equal((uint)value, (uint)(ASObject)value);
            Assert.Equal((uint)value, ASObject.AS_toUint(ASObject.AS_fromInt(value)));
            Assert.Equal((uint)value, (uint)(ASAny)value);
            Assert.Equal((uint)value, ASAny.AS_toUint(ASAny.AS_fromInt(value)));

            assertObjIsUint((uint)value, ASObject.AS_coerceType(ASObject.AS_fromInt(value), s_uintClass));
            assertObjIsUint((uint)value, ASAny.AS_coerceType(ASAny.AS_fromInt(value), s_uintClass));
        }

        [Theory]
        [MemberData(nameof(intTestData))]
        public void intToNumberConvertTest(int value) {
            Assert.Equal((double)value, (double)(ASObject)value);
            Assert.Equal((double)value, ASObject.AS_toNumber(ASObject.AS_fromInt(value)));
            Assert.Equal((double)value, (double)(ASAny)value);
            Assert.Equal((double)value, ASAny.AS_toNumber(ASAny.AS_fromInt(value)));

            assertObjIsNumber((double)value, ASObject.AS_coerceType(ASObject.AS_fromInt(value), s_numberClass));
            assertObjIsNumber((double)value, ASAny.AS_coerceType(ASAny.AS_fromInt(value), s_numberClass));
        }

        [Theory]
        [MemberData(nameof(intTestData))]
        public void intToBooleanConvertTest(int value) {
            Assert.Equal(value != 0, (bool)(ASObject)value);
            Assert.Equal(value != 0, ASObject.AS_toBoolean(ASObject.AS_fromInt(value)));
            Assert.Equal(value != 0, (bool)(ASAny)value);
            Assert.Equal(value != 0, ASAny.AS_toBoolean(ASAny.AS_fromInt(value)));

            assertObjIsBoolean(value != 0, ASObject.AS_coerceType(ASObject.AS_fromInt(value), s_booleanClass));
            assertObjIsBoolean(value != 0, ASAny.AS_coerceType(ASAny.AS_fromInt(value), s_booleanClass));
        }

        public static IEnumerable<object[]> intToStringConvertTest_data = TupleHelper.toArrays(
            (0, "0"),
            (1, "1"),
            (2, "2"),
            (-1, "-1"),
            (-2, "-2"),
            (48, "48"),
            (-75, "-75"),
            (127, "127"),
            (128, "128"),
            (129, "129"),
            (-127, "-127"),
            (-128, "-128"),
            (-129, "-129"),
            (255, "255"),
            (256, "256"),
            (257, "257"),
            (32767, "32767"),
            (32768, "32768"),
            (32769, "32769"),
            (-32767, "-32767"),
            (-32768, "-32768"),
            (-32769, "-32769"),
            (65535, "65535"),
            (65536, "65536"),
            (65537, "65537"),
            (7564194, "7564194"),
            (1234567890, "1234567890"),
            (-2135497086, "-2135497086"),
            (Int32.MaxValue, "2147483647"),
            (Int32.MinValue, "-2147483648")
        );

        [Theory]
        [MemberData(nameof(intToStringConvertTest_data))]
        public void intToStringConvertTest(int value, string expected) {
            Assert.Equal(expected, ASint.AS_convertString(value));

            Assert.Equal(expected, (string)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_coerceString(ASObject.AS_fromInt(value)));
            Assert.Equal(expected, ASObject.AS_convertString(ASObject.AS_fromInt(value)));

            Assert.Equal(expected, (string)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_coerceString(ASAny.AS_fromInt(value)));
            Assert.Equal(expected, ASAny.AS_convertString(ASAny.AS_fromInt(value)));

            assertObjIsString(expected, ASObject.AS_coerceType(ASObject.AS_fromInt(value), s_stringClass));
            assertObjIsString(expected, ASAny.AS_coerceType(ASAny.AS_fromInt(value), s_stringClass));
        }

        public static IEnumerable<object[]> objectAnyToIntConvertTest_data() => TupleHelper.toArrays<ASAny, int>(
            (new ConvertibleMockObject(intValue: 1244), 1244),
            (ASAny.@null, 0),
            (ASAny.undefined, 0),

            (objWithMethods(("valueOf", 1234)), 1234),
            (objWithMethods(("valueOf", "1234.06")), 1234),
            (objWithMethods(("valueOf", "  -0x3Ac12 ")), -0x3AC12),
            (objWithMethods(("valueOf", "abc")), 0),
            (objWithMethods(("valueOf", true)), 1),
            (objWithMethods(("valueOf", ASAny.@null)), 0),
            (objWithMethods(("valueOf", ASAny.undefined)), 0),

            (objWithMethods(("toString", 1234)), 1234),
            (objWithMethods(("toString", "1234.06")), 1234),
            (objWithMethods(("toString", "  -0x3Ac12 ")), -0x3AC12),
            (objWithMethods(("toString", "abc")), 0),
            (objWithMethods(("toString", true)), 1),
            (objWithMethods(("toString", ASAny.@null)), 0),
            (objWithMethods(("toString", ASAny.undefined)), 0),

            (objWithMethods(("valueOf", 121), ("toString", 154)), 121),
            (objWithMethods(("valueOf", "-375.91"), ("toString", "abc")), -375),
            (objWithMethods(("valueOf", "abcd"), ("toString", 1234)), 0),
            (objWithMethods(("valueOf", ASAny.undefined), ("toString", "999")), 0),
            (objWithMethods(("valueOf", new ASObject()), ("toString", "999")), 999),

            (new MockClassInstance(mockClassA), 0),
            (new MockClassInstance(mockClassB), 0),
            (new MockClassInstance(mockClassC), 9999)
        );

        [Theory]
        [MemberData(nameof(objectAnyToIntConvertTest_data))]
        public void objectAnyToIntConvertTest(ASAny obj, int expected) {
            Assert.Equal(expected, (int)obj);
            Assert.Equal(expected, ASAny.AS_toInt(obj));

            assertObjIsInt(expected, ASAny.AS_coerceType(obj, s_intClass));

            if (obj.isDefined) {
                Assert.Equal(expected, (int)obj.value);
                Assert.Equal(expected, ASObject.AS_toInt(obj.value));

                assertObjIsInt(expected, ASObject.AS_coerceType(obj.value, s_intClass));
            }
        }

        public static IEnumerable<object[]> uintTestData = TupleHelper.toArrays<uint>(
            0, 1,
            127, 128, 129,
            255, 256, 257,
            4718, 9993,
            32767, 32768, 32769,
            65535, 65536, 65537,
            123456789, 193847113,
            (uint)Int32.MaxValue,
            (uint)Int32.MaxValue + 1,
            UInt32.MaxValue
        );

        [Theory]
        [MemberData(nameof(uintTestData))]
        public void uintObjectConvertRoundTripTest(uint value) {
            ASObject asObj = value;
            assertObjIsUint(value, asObj);

            ASAny asAny = value;
            assertObjIsUint(value, asAny.value);

            assertObjIsUint(value, ASObject.AS_fromUint(value));
            assertObjIsUint(value, ASAny.AS_fromUint(value).value);

            assertObjIsUint(value, ASObject.AS_coerceType(ASObject.AS_fromUint(value), s_uintClass));
            assertObjIsUint(value, ASAny.AS_coerceType(ASObject.AS_fromUint(value), s_uintClass));
        }

        [Theory]
        [MemberData(nameof(uintTestData))]
        public void uintToIntConvertTest(uint value) {
            Assert.Equal((int)value, (int)(ASObject)value);
            Assert.Equal((int)value, ASObject.AS_toInt(ASObject.AS_fromUint(value)));
            Assert.Equal((int)value, (int)(ASAny)value);
            Assert.Equal((int)value, ASAny.AS_toInt(ASAny.AS_fromUint(value)));

            assertObjIsInt((int)value, ASObject.AS_coerceType(ASObject.AS_fromUint(value), s_intClass));
            assertObjIsInt((int)value, ASAny.AS_coerceType(ASObject.AS_fromUint(value), s_intClass));
        }

        [Theory]
        [MemberData(nameof(uintTestData))]
        public void uintToNumberConvertTest(uint value) {
            Assert.Equal((double)value, (double)(ASObject)value);
            Assert.Equal((double)value, ASObject.AS_toNumber(ASObject.AS_fromUint(value)));
            Assert.Equal((double)value, (double)(ASAny)value);
            Assert.Equal((double)value, ASAny.AS_toNumber(ASAny.AS_fromUint(value)));

            assertObjIsNumber((double)value, ASObject.AS_coerceType(ASObject.AS_fromUint(value), s_numberClass));
            assertObjIsNumber((double)value, ASAny.AS_coerceType(ASObject.AS_fromUint(value), s_numberClass));
        }

        [Theory]
        [MemberData(nameof(uintTestData))]
        public void uintToBooleanConvertTest(uint value) {
            Assert.Equal(value != 0, (bool)(ASObject)value);
            Assert.Equal(value != 0, ASObject.AS_toBoolean(ASObject.AS_fromUint(value)));

            Assert.Equal(value != 0, (bool)(ASAny)value);
            Assert.Equal(value != 0, ASAny.AS_toBoolean(ASAny.AS_fromUint(value)));

            assertObjIsBoolean(value != 0, ASObject.AS_coerceType(ASObject.AS_fromUint(value), s_booleanClass));
            assertObjIsBoolean(value != 0, ASAny.AS_coerceType(ASAny.AS_fromUint(value), s_booleanClass));
        }

        public static IEnumerable<object[]> uintToStringConvertTest_data = TupleHelper.toArrays<uint, string>(
            (0, "0"),
            (1, "1"),
            (2, "2"),
            (48, "48"),
            (75, "75"),
            (127, "127"),
            (128, "128"),
            (129, "129"),
            (255, "255"),
            (256, "256"),
            (257, "257"),
            (32767, "32767"),
            (32768, "32768"),
            (32769, "32769"),
            (65535, "65535"),
            (65536, "65536"),
            (65537, "65537"),
            (7564194, "7564194"),
            (1234567890, "1234567890"),
            (Int32.MaxValue, "2147483647"),
            ((uint)Int32.MaxValue + 1, "2147483648"),
            (3980245617, "3980245617"),
            (UInt32.MaxValue, "4294967295")
        );

        [Theory]
        [MemberData(nameof(uintToStringConvertTest_data))]
        public void uintToStringConvertTest(uint value, string expected) {
            Assert.Equal(expected, ASuint.AS_convertString(value));

            Assert.Equal(expected, (string)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_coerceString(ASObject.AS_fromUint(value)));
            Assert.Equal(expected, ASObject.AS_convertString(ASObject.AS_fromUint(value)));

            Assert.Equal(expected, (string)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_coerceString(ASAny.AS_fromUint(value)));
            Assert.Equal(expected, ASAny.AS_convertString(ASAny.AS_fromUint(value)));

            assertObjIsString(expected, ASObject.AS_coerceType(ASObject.AS_fromUint(value), s_stringClass));
            assertObjIsString(expected, ASAny.AS_coerceType(ASObject.AS_fromUint(value), s_stringClass));
        }

        public static IEnumerable<object[]> objectAnyToUintConvertTest_data() => TupleHelper.toArrays<ASAny, uint>(
            (new ConvertibleMockObject(uintValue: 1244), 1244u),
            (ASAny.@null, 0u),
            (ASAny.undefined, 0u),

            (objWithMethods(("valueOf", 1234)), 1234),
            (objWithMethods(("valueOf", "1234.06")), 1234),
            (objWithMethods(("valueOf", "  -0x3Ac12 ")), unchecked((uint)(-0x3AC12))),
            (objWithMethods(("valueOf", "abc")), 0),
            (objWithMethods(("valueOf", true)), 1),
            (objWithMethods(("valueOf", ASAny.@null)), 0),
            (objWithMethods(("valueOf", ASAny.undefined)), 0),

            (objWithMethods(("toString", 1234)), 1234),
            (objWithMethods(("toString", "1234.06")), 1234),
            (objWithMethods(("toString", "  -0x3Ac12 ")), unchecked((uint)(-0x3AC12))),
            (objWithMethods(("toString", "abc")), 0),
            (objWithMethods(("toString", true)), 1),
            (objWithMethods(("toString", ASAny.@null)), 0),
            (objWithMethods(("toString", ASAny.undefined)), 0),

            (objWithMethods(("valueOf", 121), ("toString", 154)), 121),
            (objWithMethods(("valueOf", "-375.91"), ("toString", "abc")), unchecked((uint)(-375))),
            (objWithMethods(("valueOf", "abcd"), ("toString", 1234)), 0),
            (objWithMethods(("valueOf", ASAny.undefined), ("toString", "999")), 0),
            (objWithMethods(("valueOf", new ASObject()), ("toString", "999")), 999),

            (new MockClassInstance(mockClassA), 0),
            (new MockClassInstance(mockClassB), 0),
            (new MockClassInstance(mockClassC), 9999)
        );

        [Theory]
        [MemberData(nameof(objectAnyToUintConvertTest_data))]
        public void objectAnyToUintConvertTest(ASAny obj, uint expected) {
            Assert.Equal(expected, (uint)obj);
            Assert.Equal(expected, ASAny.AS_toUint(obj));

            assertObjIsUint(expected, ASAny.AS_coerceType(obj, s_uintClass));

            if (obj.isDefined) {
                Assert.Equal(expected, (uint)obj.value);
                Assert.Equal(expected, ASObject.AS_toUint(obj.value));

                assertObjIsUint(expected, ASObject.AS_coerceType(obj.value, s_uintClass));
            }
        }

        private static readonly double NEG_ZERO = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000L));

        public static IEnumerable<object[]> numberToObjectConvertRoundTripTest_data = TupleHelper.toArrays(
            0.0,
            NEG_ZERO,
            1.0,
            -1.0,
            0.5,
            -0.5,
            1.5,
            -1.5,
            Double.Epsilon,
            -Double.Epsilon,
            Double.MaxValue,
            -Double.MaxValue,
            Double.PositiveInfinity,
            Double.NegativeInfinity,
            Double.NaN
        );

        [Theory]
        [MemberData(nameof(numberToObjectConvertRoundTripTest_data))]
        public void numberToObjectConvertRoundTripTest(double value) {
            ASObject asObj = value;
            assertObjIsNumber(value, asObj);

            ASAny asAny = value;
            assertObjIsNumber(value, asAny.value);

            assertObjIsNumber(value, ASObject.AS_fromNumber(value));
            assertObjIsNumber(value, ASAny.AS_fromNumber(value).value);

            assertObjIsNumber(value, ASObject.AS_coerceType(ASObject.AS_fromNumber(value), s_numberClass));
            assertObjIsNumber(value, ASAny.AS_coerceType(ASAny.AS_fromNumber(value), s_numberClass));
        }

        public static IEnumerable<object[]> numberToBoolConvertTest_data = TupleHelper.toArrays(
            (0.0, false),
            (NEG_ZERO, false),
            (Double.NaN, false),

            (1.0, true),
            (-1.0, true),
            (0.5, true),
            (-0.5, true),
            (10.0, true),
            (-10.0, true),
            (Double.MaxValue, true),
            (Double.MinValue, true),
            (Double.Epsilon, true),
            (-Double.Epsilon, true),
            (Double.PositiveInfinity, true),
            (Double.NegativeInfinity, true)
        );

        [Theory]
        [MemberData(nameof(numberToBoolConvertTest_data))]
        public void numberToBoolConvertTest(double value, bool expected) {
            Assert.Equal(expected, ASNumber.AS_toBoolean(value));

            Assert.Equal(expected, (bool)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_toBoolean(ASObject.AS_fromNumber(value)));

            Assert.Equal(expected, (bool)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_toBoolean(ASAny.AS_fromNumber(value)));

            assertObjIsBoolean(expected, ASObject.AS_coerceType(ASObject.AS_fromNumber(value), s_booleanClass));
            assertObjIsBoolean(expected, ASAny.AS_coerceType(ASAny.AS_fromNumber(value), s_booleanClass));
        }

        public static IEnumerable<object[]> numberToIntUintConvertTest_data = TupleHelper.toArrays(
            (0.0, 0),
            (NEG_ZERO, 0),

            ( Double.Epsilon,           0),
            (-Double.Epsilon,           0),
            ( 2.2250738585072014e-308,  0),
            (-2.2250738585072014e-308,  0),
            ( 0.0048571143,             0),
            (-0.0048571143,             0),
            ( 0.4,                      0),
            (-0.4,                      0),
            ( 0.5,                      0),
            (-0.5,                      0),
            ( 0.75,                     0),
            (-0.75,                     0),
            ( Math.BitDecrement(1.0),   0),
            (-Math.BitDecrement(1.0),   0),

            ( 1.0,                      1),
            (-1.0,                     -1),
            ( Math.BitIncrement(1.0),   1),
            (-Math.BitIncrement(1.0),  -1),
            ( 1.4,                      1),
            (-1.4,                     -1),
            ( 1.5,                      1),
            (-1.5,                     -1),
            ( 1.6,                      1),
            (-1.6,                     -1),

            (Math.BitDecrement(2.0), 1),
            (2.0, 2),
            (Math.BitIncrement(2.0), 2),

            (1435.994821, 1435),
            (-488291456.3, -488291456),

            ( Math.Pow(2, 31) - 1.0,                    Int32.MaxValue),
            ( Math.BitIncrement(Math.Pow(2, 31) - 1.0), Int32.MaxValue),
            ( Math.Pow(2, 31) - 0.5,                    Int32.MaxValue),
            ( Math.BitDecrement(Math.Pow(2, 31)),       Int32.MaxValue),
            ( Math.Pow(2, 31),                          Int32.MinValue),
            ( Math.BitIncrement(Math.Pow(2, 31)),       Int32.MinValue),
            ( Math.Pow(2, 31) + 0.5,                    Int32.MinValue),
            ( Math.BitDecrement(Math.Pow(2, 31) + 1.0), Int32.MinValue),
            (-Math.Pow(2, 31),                          Int32.MinValue),
            (-Math.BitIncrement(Math.Pow(2, 31)),       Int32.MinValue),
            (-Math.Pow(2, 31) - 0.5,                    Int32.MinValue),
            (-Math.BitDecrement(Math.Pow(2, 31) + 1.0), Int32.MinValue),

            ( Math.Pow(2, 32) - 1.0,                    -1),
            ( Math.BitIncrement(Math.Pow(2, 32) - 1.0), -1),
            ( Math.Pow(2, 32) - 0.5,                    -1),
            ( Math.BitDecrement(Math.Pow(2, 32)),       -1),
            (-Math.Pow(2, 32) + 1.0,                     1),
            (-Math.BitIncrement(Math.Pow(2, 32) - 1.0),  1),
            (-Math.Pow(2, 32) + 0.5,                     1),
            (-Math.BitDecrement(Math.Pow(2, 32)),        1),
            ( Math.Pow(2, 32),                           0),
            ( Math.BitIncrement(Math.Pow(2, 32)),        0),
            (-Math.Pow(2, 32),                           0),
            (-Math.BitIncrement(Math.Pow(2, 32)),        0),
            ( Math.Pow(2, 32) + 1.0,                     1),
            (-Math.Pow(2, 32) - 1.0,                    -1),

            ( Math.Pow(2, 32) + 17.56,                  17),
            ( Math.Pow(2, 32) - 17.56,                 -18),
            (-Math.Pow(2, 32) + 17.56,                  18),
            (-Math.Pow(2, 32) - 17.56,                 -17),
            ( Math.Pow(2, 45) + 17.56,                  17),
            ( Math.Pow(2, 45) - 17.56,                 -18),
            (-Math.Pow(2, 45) + 17.56,                  18),
            (-Math.Pow(2, 45) - 17.56,                 -17),
            ( Math.Pow(2, 51) + 17.5,                   17),
            ( Math.Pow(2, 51) - 17.5,                  -18),
            (-Math.Pow(2, 51) + 17.5,                   18),
            (-Math.Pow(2, 51) - 17.5,                  -17),
            ( Math.Pow(2, 52) + 18.0,                   18),
            ( Math.Pow(2, 52) - 18.0,                  -18),
            (-Math.Pow(2, 52) + 18.0,                   18),
            (-Math.Pow(2, 52) - 18.0,                  -18),

            ( Math.Pow(2, 53) - 1.0,                    -1),
            (-Math.Pow(2, 53) + 1.0,                     1),
            ( Math.Pow(2, 53),                           0),
            (-Math.Pow(2, 53),                           0),
            ( Math.Pow(2, 53) + 12.0,                   12),
            (-Math.Pow(2, 53) + 12.0,                   12),
            (-Math.Pow(2, 53) - 12.0,                  -12),

            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 1),            -2),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 1),             2),
            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 3),            -8),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 3),             8),
            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 11),   -(1 << 11)),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 11),    (1 << 11)),
            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 20),   -(1 << 20)),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 20),    (1 << 20)),
            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 31),    (1 << 31)),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 31),    (1 << 31)),
            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 32),            0),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 32),            0),
            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 33),            0),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 33),            0),
            ( Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 36),            0),
            (-Math.ScaleB(Math.ScaleB(1, 53) - 1.0, 36),            0),

            (Double.MaxValue, 0),
            (Double.MinValue, 0),
            (Double.PositiveInfinity, 0),
            (Double.NegativeInfinity, 0),
            (Double.NaN, 0)
        );

        [Theory]
        [MemberData(nameof(numberToIntUintConvertTest_data))]
        public void numberToIntConvertTest(double value, int expected) {
            Assert.Equal(expected, ASNumber.AS_toInt(value));

            Assert.Equal(expected, (int)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_toInt(ASObject.AS_fromNumber(value)));

            Assert.Equal(expected, (int)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_toInt(ASAny.AS_fromNumber(value)));

            assertObjIsInt(expected, ASObject.AS_coerceType(ASObject.AS_fromNumber(value), s_intClass));
            assertObjIsInt(expected, ASAny.AS_coerceType(ASObject.AS_fromNumber(value), s_intClass));
        }

        [Theory]
        [MemberData(nameof(numberToIntUintConvertTest_data))]
        public void numberToUintConvertTest(double value, int expected) {
            Assert.Equal((uint)expected, ASNumber.AS_toUint(value));

            Assert.Equal((uint)expected, (uint)(ASObject)value);
            Assert.Equal((uint)expected, ASObject.AS_toUint(ASObject.AS_fromNumber(value)));

            Assert.Equal((uint)expected, (uint)(ASAny)value);
            Assert.Equal((uint)expected, ASAny.AS_toUint(ASAny.AS_fromNumber(value)));

            assertObjIsUint((uint)expected, ASObject.AS_coerceType(ASObject.AS_fromNumber(value), s_uintClass));
            assertObjIsUint((uint)expected, ASAny.AS_coerceType(ASAny.AS_fromNumber(value), s_uintClass));
        }

        public static IEnumerable<object[]> numberToStringConvertTest_data() {
            // Reuse test data from NumberFormatHelperTest
            return Enumerable.Concat(
                NumberFormatHelperTest.doubleToStringMethodTest_data_fixedOutput,
                NumberFormatHelperTest.doubleToStringMethodTest_data_scientificOutput
            );
        }

        [Theory]
        [MemberData(nameof(numberToStringConvertTest_data))]
        public void numberToStringConvertTest(double value, string expected) {
            Assert.Equal(expected, ASNumber.AS_convertString(value));

            Assert.Equal(expected, (string)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_coerceString(ASObject.AS_fromNumber(value)));
            Assert.Equal(expected, ASObject.AS_convertString(ASObject.AS_fromNumber(value)));

            Assert.Equal(expected, (string)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_coerceString(ASAny.AS_fromNumber(value)));
            Assert.Equal(expected, ASAny.AS_convertString(ASAny.AS_fromNumber(value)));

            assertObjIsString(expected, ASObject.AS_coerceType(ASObject.AS_fromNumber(value), s_stringClass));
            assertObjIsString(expected, ASAny.AS_coerceType(ASObject.AS_fromNumber(value), s_stringClass));
        }

        public static IEnumerable<object[]> objectAnyToNumberConvertTest_data() => TupleHelper.toArrays<ASAny, double>(
            (ASAny.@null, 0.0),
            (ASAny.undefined, Double.NaN),

            (new ConvertibleMockObject(numberValue: 0.0), 0.0),
            (new ConvertibleMockObject(numberValue: NEG_ZERO), NEG_ZERO),
            (new ConvertibleMockObject(numberValue: 1.0), 1.0),
            (new ConvertibleMockObject(numberValue: -1.0), -1.0),
            (new ConvertibleMockObject(numberValue: Double.PositiveInfinity), Double.PositiveInfinity),
            (new ConvertibleMockObject(numberValue: Double.NegativeInfinity), Double.NegativeInfinity),
            (new ConvertibleMockObject(numberValue: Double.NaN), Double.NaN),

            (objWithMethods(("valueOf", 1234)), 1234),
            (objWithMethods(("valueOf", "1234.06")), 1234.06),
            (objWithMethods(("valueOf", "  -0x3Ac12 ")), -0x3AC12),
            (objWithMethods(("valueOf", "abc")), Double.NaN),
            (objWithMethods(("valueOf", true)), 1),
            (objWithMethods(("valueOf", ASAny.@null)), 0),
            (objWithMethods(("valueOf", ASAny.undefined)), Double.NaN),

            (objWithMethods(("toString", 1234)), 1234),
            (objWithMethods(("toString", "1234.06")), 1234.06),
            (objWithMethods(("toString", "  -0x3Ac12 ")), -0x3AC12),
            (objWithMethods(("toString", "abc")), Double.NaN),
            (objWithMethods(("toString", true)), 1),
            (objWithMethods(("toString", ASAny.@null)), 0),
            (objWithMethods(("toString", ASAny.undefined)), Double.NaN),

            (objWithMethods(("valueOf", 121), ("toString", 154)), 121),
            (objWithMethods(("valueOf", "-375.91"), ("toString", "abc")), -375.91),
            (objWithMethods(("valueOf", "abcd"), ("toString", 1234)), Double.NaN),
            (objWithMethods(("valueOf", ASAny.undefined), ("toString", "999")), Double.NaN),
            (objWithMethods(("valueOf", new ASObject()), ("toString", "999")), 999),

            (new MockClassInstance(mockClassA), 0),
            (new MockClassInstance(mockClassB), 0),
            (new MockClassInstance(mockClassC), 9999.36)
        );

        [Theory]
        [MemberData(nameof(objectAnyToNumberConvertTest_data))]
        public void objectAnyToNumberConvertTest(ASAny obj, double expected) {
            AssertHelper.floatIdentical(expected, (double)obj);
            AssertHelper.floatIdentical(expected, ASAny.AS_toNumber(obj));

            assertObjIsNumber(expected, ASAny.AS_coerceType(obj, s_numberClass));

            if (obj.isDefined) {
                AssertHelper.floatIdentical(expected, (double)obj.value);
                AssertHelper.floatIdentical(expected, ASObject.AS_toNumber(obj.value));

                assertObjIsNumber(expected, ASAny.AS_coerceType(obj.value, s_numberClass));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abcde")]
        [InlineData("abc\0def\uFFFF")]
        public void stringObjectConvertRoundTripTest(string value) {
            ASObject asObj1 = value;
            ASObject asObj2 = ASObject.AS_fromString(value);

            ASAny asAny1 = value;
            ASAny asAny2 = ASAny.AS_fromString(value);

            assertObjIsString(value, asObj1);
            assertObjIsString(value, asObj2);
            assertObjIsString(value, asAny1);
            assertObjIsString(value, asAny2);

            assertObjIsString(value, ASObject.AS_coerceType(asObj1, s_stringClass));
            assertObjIsString(value, ASAny.AS_coerceType(asAny1, s_stringClass));
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("0", true)]
        [InlineData("null", true)]
        [InlineData("false", true)]
        [InlineData("12345678", true)]
        [InlineData("abc\0def\uFFFF", true)]
        public void stringToBooleanConvertTest(string value, bool expected) {
            Assert.Equal(expected, ASString.AS_toBoolean(value));
            Assert.Equal(expected, (bool)(ASObject)value);
            Assert.Equal(expected, (bool)(ASAny)value);

            assertObjIsBoolean(expected, ASObject.AS_coerceType((ASObject)value, s_booleanClass));
            assertObjIsBoolean(expected, ASAny.AS_coerceType((ASAny)value, s_booleanClass));
        }

        public static IEnumerable<object[]> stringToIntUintConvertTest_data = TupleHelper.toArrays(
            ("0", 0),
            ("+0", 0),
            ("-0", 0),
            ("0000", 0),
            ("1", 1),
            ("+1", 1),
            ("-1", -1),
            ("00001", 1),
            ("-00001", -1),
            ("100", 100),
            ("+3333", 3333),
            ("123456789", 123456789),
            ("-123456789", -123456789),
            ("2147483647", 2147483647),
            ("-2147483647", -2147483647),
            ("2147483648", unchecked((int)2147483648u)),
            ("-2147483648", -2147483648),
            ("4294967295", -1),
            ("-4294967295", 1),
            ("-4294967296", 0),
            ("8589934596", 4),
            ("9007199254740991", -1),
            ("9007199255110380", 369388),
            ("-129127208515967007305", -145993),

            ("0x0", 0),
            ("-0x0", 0),
            ("+0x0", 0),
            ("0X1", 1),
            ("-0X0001", -1),
            ("0x1b3c", 0x1b3c),
            ("0X12345678", 0x12345678),
            ("0x000000000012345678", 0x12345678),
            ("-0x000000000012345678", -0x12345678),
            ("0x9abcdef", 0x9abcdef),
            ("0x7FfFfFfF", 0x7FFFFFFF),
            ("-0x7FfFfFfF", -0x7FFFFFFF),
            ("0xffffffff", -1),
            ("0x1b3a6cc79", unchecked((int)0xb3a6cc79u)),
            ("0x166300c764421c0", 0x764421c0),

            ("-0x1674aa6300cfffffffd", 3),
            ("  17482  ", 17482),
            (" \n\t \u200b 30\f\v \u3000", 30),
            ("  -0x1333\t\n", -0x1333),
            ("\r\n    0x166300c764421c0   ", 0x764421c0),

            ("0.999999999999999", 0),
            ("0.9999999999999999999999999999999999", 1),
            ("-4775.039482", -4775),
            ("  -9007199255110380.4839488373 ", -369388),
            ("\t \u3000  2.381889E+24  \r\n ", -536870912),
            ("\t \u3000  -2.381889e24  \r\n ", 536870912),
            ("1.3e-4", 0),
            ("Infinity", 0),
            ("NaN", 0),

            ("", 0),
            ("abc", 0),
            ("a123", 0),
            ("+", 0),
            ("-", 0),
            (".", 0),
            (".1234", 0),
            ("$1234", 0),
            ("0xg", 0),
            ("0x", 0),
            ("0x+1234", 0),
            ("0x-1234", 0),
            ("0x 1234", 0),
            ("+ 1234", 0),
            ("- 1234", 0),
            ("(1234)", 0)
        );

        [Theory]
        [MemberData(nameof(stringToIntUintConvertTest_data))]
        public void stringToIntConvertTest(string value, int expected) {
            Assert.Equal(expected, ASString.AS_toInt(value));
            Assert.Equal(expected, (int)(ASObject)value);
            Assert.Equal(expected, (int)(ASAny)value);

            assertObjIsInt(expected, ASObject.AS_coerceType((ASObject)value, s_intClass));
            assertObjIsInt(expected, ASAny.AS_coerceType((ASAny)value, s_intClass));
        }

        [Theory]
        [MemberData(nameof(stringToIntUintConvertTest_data))]
        public void stringToUintConvertTest(string value, int expected) {
            Assert.Equal((uint)expected, ASString.AS_toUint(value));
            Assert.Equal((uint)expected, (uint)(ASObject)value);
            Assert.Equal((uint)expected, (uint)(ASAny)value);

            assertObjIsUint((uint)expected, ASObject.AS_coerceType((ASObject)value, s_uintClass));
            assertObjIsUint((uint)expected, ASAny.AS_coerceType((ASAny)value, s_uintClass));
        }

        public static IEnumerable<object[]> stringToNumberConvertTest_data = TupleHelper.toArrays(
            ("0", 0),
            ("0.0", 0),
            ("+0.0", 0),
            ("000", 0),
            (".0", 0),
            ("+.0", 0),
            ("0.", 0),
            ("00.000", 0),
            ("0e0", 0),
            ("0e100", 0),
            ("0e+1", 0),
            ("+0e+100", 0),
            ("0.0e100", 0),
            (".0000e+10000", 0),
            ("0000.0000e-500000", 0),

            ("-0", NEG_ZERO),
            ("-.0", NEG_ZERO),
            ("-0.", NEG_ZERO),

            ("1", 1),
            ("-1", -1),
            ("+0000001", 1),
            ("1458", 1458),
            ("9999999", 9999999),
            ("+2147483647", 2147483647),
            ("-4294967296", -4294967296),
            ("1000200003", 1000200003),
            ("10002000030", 10002000030),
            ("00009007199254740991", 9007199254740991),
            ("9007199254740997", 9007199254740996),
            ("1000000000000000000000000000000002", 1e+33),
            ("00004329483243000493353200352", 4.3294832430004935e+24),
            ("17976931348623157" + new string('0', 292), Double.MaxValue),

            ("1.0", 1),
            ("-1.00000", -1),
            ("26453.00000000000000000000000000000000000000", 26453),
            ("9007199254740993.0000000000000000001", 9007199254740994),
            ("384546.99000033207", 384546.99000033207),
            ("-.0006849993", -0.0006849993),
            ("495831.59965", 495831.59965),
            ("." + new string('0', 307) + "22250738585072014", 2.2250738585072014e-308),
            ("0." + new string('0', 323) + "494065645841246", Double.Epsilon),

            ("1e0", 1),
            ("1.e+0", 1),
            ("-1e-0", -1),
            ("+2.445E6", 2.445e+6),
            ("0.000002445000000000000e+12", 2.445e+6),
            ("2445000" + new string('0', 400) + "e-400", 2.445e+6),
            ("0.009007199254740992e+18", 9007199254740992),
            ("1.797693134862315e+308", 1.797693134862315e+308),
            ("3.8454699000033265e+5", 384546.99000033265),
            ("0." + new string('0', 200) + "17976931348623157e+509", Double.MaxValue),
            ("500e-326", Double.Epsilon),
            ("6.5e-323", Double.Epsilon * 13),
            ("1e+400", Double.PositiveInfinity),
            ("0.000000000024e-314", 0),

            ("0x0", 0),
            ("0X123456789", 4886718345),
            ("0XABCDEF", 11259375),
            ("-0x12345678900abc1d1ee1abd", -3.521251666818939e+26),
            ("0xfffffffffffffb" + new string('f', 242), Double.MaxValue),
            ("0xfffffffffffffc" + new string('0', 242), Double.PositiveInfinity),

            ("  \u2000 \u200B  \u2003\u3000  \u205F\n  4.5888", 4.5888),
            (" \n\n\n  -0X400", -1024),
            ("\t 123  \r\n \u205F  ", 123),

            ("Infinity", Double.PositiveInfinity),
            ("   Infinity", Double.PositiveInfinity),
            ("-Infinity", Double.NegativeInfinity),
            ("NaN", Double.NaN),

            ("", 0),
            ("      ", 0),
            ("  \r\n  \t ", 0),
            ("  \u2000 \u200B  \u2003\u3000  \u205F\n  ", 0),

            ("123,456", Double.NaN),
            ("123_456", Double.NaN),
            ("abcdef", Double.NaN),
            ("0xgabcdef", Double.NaN),
            ("$123456", Double.NaN),
            ("0x.123456", Double.NaN),
            ("0x1234e+1", Double.NaN),
            ("\x081234", Double.NaN),
            ("\t  \r\n 1839548a \u205F  ", Double.NaN),
            ("INFINITY", Double.NaN)
        );

        [Theory]
        [MemberData(nameof(stringToNumberConvertTest_data))]
        public void stringToNumberConvertTest(string value, double expected) {
            AssertHelper.floatIdentical(expected, ASString.AS_toNumber(value));
            AssertHelper.floatIdentical(expected, (double)(ASObject)value);

            assertObjIsNumber(expected, ASObject.AS_coerceType((ASObject)value, s_numberClass));
            assertObjIsNumber(expected, ASAny.AS_coerceType((ASAny)value, s_numberClass));
        }

        public static IEnumerable<object[]> objectAnyToStringConvertTest_data() => TupleHelper.toArrays<ASAny, string, string>(
            (ASAny.@null, null, "null"),
            (ASAny.undefined, null, "undefined"),

            (new ConvertibleMockObject(stringValue: ""), "", ""),
            (new ConvertibleMockObject(stringValue: "abcdef"), "abcdef", "abcdef"),
            (new ConvertibleMockObject(stringValue: "\u0000\ud800\udfff\ufffe\uffff"), "\u0000\ud800\udfff\ufffe\uffff", "\u0000\ud800\udfff\ufffe\uffff"),

            (objWithMethods(("toString", 1234)), "1234", "1234"),
            (objWithMethods(("toString", "abc")), "abc", "abc"),
            (objWithMethods(("toString", ASAny.@null)), "null", "null"),
            (objWithMethods(("toString", ASAny.undefined)), "undefined", "undefined"),

            (objWithMethods(("toString", 1234), ("valueOf", "abcd")), "1234", "1234"),
            (objWithMethods(("toString", "abc"), ("valueOf", "def")), "abc", "abc"),
            (objWithMethods(("toString", ASAny.undefined), ("valueOf", "abc")), "undefined", "undefined"),
            (objWithMethods(("toString", new ASObject()), ("valueOf", "abc")), "abc", "abc"),
            (objWithMethods(("toString", new ASObject()), ("valueOf", ASAny.@null)), "null", "null"),
            (objWithMethods(("toString", new ASObject()), ("valueOf", ASAny.undefined)), "undefined", "undefined"),

            (new MockClassInstance(mockClassA), "Hello CA", "Hello CA"),
            (new MockClassInstance(mockClassB), "Hello CB", "Hello CB"),
            (new MockClassInstance(mockClassC), "Hello CA", "Hello CA")
        );

        [Theory]
        [MemberData(nameof(objectAnyToStringConvertTest_data))]
        public void objectToStringConvertTest(ASAny obj, string expectedCoerce, string expectedConvert) {
            Assert.Equal(expectedCoerce, (string)obj);

            Assert.Equal(expectedCoerce, ASAny.AS_coerceString(obj));
            Assert.Equal(expectedConvert, ASAny.AS_convertString(obj));

            assertObjIsString(expectedCoerce, ASAny.AS_coerceType(obj, s_stringClass));

            if (obj.isDefined) {
                Assert.Equal(expectedCoerce, (string)obj.value);

                Assert.Equal(expectedCoerce, ASObject.AS_coerceString(obj.value));
                Assert.Equal(expectedConvert, ASObject.AS_convertString(obj.value));

                assertObjIsString(expectedCoerce, ASObject.AS_coerceType(obj.value, s_stringClass));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void boolToObjectConvetRoundTripTest(bool value) {
            ASObject asObj = value;
            assertObjIsBoolean(value, asObj);

            ASAny asAny = value;
            assertObjIsBoolean(value, asAny.value);

            assertObjIsBoolean(value, ASObject.AS_fromBoolean(value));
            assertObjIsBoolean(value, ASAny.AS_fromBoolean(value).value);

            assertObjIsBoolean(value, ASObject.AS_coerceType(ASObject.AS_fromBoolean(value), s_booleanClass));
            assertObjIsBoolean(value, ASAny.AS_coerceType(ASAny.AS_fromBoolean(value), s_booleanClass));
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(true, 1)]
        public void boolToIntConvertTest(bool value, int expected) {
            Assert.Equal(expected, (int)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_toInt(ASObject.AS_fromBoolean(value)));

            Assert.Equal(expected, (int)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_toInt(ASAny.AS_fromBoolean(value)));

            assertObjIsInt(expected, ASObject.AS_coerceType((ASObject)value, s_intClass));
            assertObjIsInt(expected, ASAny.AS_coerceType((ASAny)value, s_intClass));
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(true, 1)]
        public void boolToUintConvertTest(bool value, uint expected) {
            Assert.Equal(expected, (uint)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_toUint(ASObject.AS_fromBoolean(value)));

            Assert.Equal(expected, (uint)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_toUint(ASAny.AS_fromBoolean(value)));

            assertObjIsUint(expected, ASObject.AS_coerceType((ASObject)value, s_uintClass));
            assertObjIsUint(expected, ASAny.AS_coerceType((ASAny)value, s_uintClass));
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(true, 1)]
        public void boolToNumberConvertTest(bool value, double expected) {
            Assert.Equal(expected, (double)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_toNumber(ASObject.AS_fromBoolean(value)));

            Assert.Equal(expected, (double)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_toNumber(ASAny.AS_fromBoolean(value)));

            assertObjIsNumber(expected, ASObject.AS_coerceType((ASObject)value, s_numberClass));
            assertObjIsNumber(expected, ASAny.AS_coerceType((ASAny)value, s_numberClass));
        }

        [Theory]
        [InlineData(false, "false")]
        [InlineData(true, "true")]
        public void boolToStringConvertTest(bool value, string expected) {
            Assert.Equal(expected, ASBoolean.AS_convertString(value));

            Assert.Equal(expected, (string)(ASObject)value);
            Assert.Equal(expected, ASObject.AS_coerceString(ASObject.AS_fromBoolean(value)));
            Assert.Equal(expected, ASObject.AS_convertString(ASObject.AS_fromBoolean(value)));

            Assert.Equal(expected, (string)(ASAny)value);
            Assert.Equal(expected, ASAny.AS_coerceString(ASAny.AS_fromBoolean(value)));
            Assert.Equal(expected, ASAny.AS_convertString(ASAny.AS_fromBoolean(value)));

            assertObjIsString(expected, ASObject.AS_coerceType((ASObject)value, s_stringClass));
            assertObjIsString(expected, ASAny.AS_coerceType((ASAny)value, s_stringClass));
        }

        public static IEnumerable<object[]> objectAnyToBoolConvertTest_data() => TupleHelper.toArrays<ASAny, bool>(
            (ASAny.@null, false),
            (ASAny.undefined, false),

            (new ConvertibleMockObject(boolValue: false), false),
            (new ConvertibleMockObject(boolValue: true), true),

            (objWithMethods(("valueOf", true)), true),
            (objWithMethods(("valueOf", 1234)), true),
            (objWithMethods(("valueOf", "abc")), true),
            (objWithMethods(("valueOf", Double.NaN)), true),
            (objWithMethods(("valueOf", "")), true),
            (objWithMethods(("valueOf", ASAny.@null)), true),
            (objWithMethods(("valueOf", ASAny.undefined)), true),

            (objWithMethods(("toString", true)), true),
            (objWithMethods(("toString", 1234)), true),
            (objWithMethods(("toString", "abc")), true),
            (objWithMethods(("toString", Double.NaN)), true),
            (objWithMethods(("toString", "")), true),
            (objWithMethods(("toString", ASAny.@null)), true),
            (objWithMethods(("toString", ASAny.undefined)), true),

            (objWithMethods(("valueOf", true), ("toString", false)), true),
            (objWithMethods(("valueOf", 0), ("toString", "def")), true),
            (objWithMethods(("valueOf", ASAny.undefined), ("toString", "abc")), true),
            (objWithMethods(("valueOf", new ASObject()), ("toString", "abc")), true),
            (objWithMethods(("valueOf", new ASObject()), ("toString", "")), true)
        );

        [Theory]
        [MemberData(nameof(objectAnyToBoolConvertTest_data))]
        public void objectAnyToBoolConvertTest(ASAny obj, bool expected) {
            Assert.Equal(expected, (bool)obj);
            Assert.Equal(expected, ASAny.AS_toBoolean(obj));

            assertObjIsBoolean(expected, ASAny.AS_coerceType(obj, s_booleanClass));

            if (obj.isDefined) {
                Assert.Equal(expected, (bool)obj.value);
                Assert.Equal(expected, ASObject.AS_toBoolean(obj.value));

                assertObjIsBoolean(expected, ASObject.AS_coerceType(obj.value, s_booleanClass));
            }
        }

        public static IEnumerable<object[]> objectToAnyConvertRoundTripTest_data = TupleHelper.toArrays<ASObject>(
            null,
            new ASObject(),
            new ASArray(),
            0,
            1u,
            1.5,
            "hello",
            true,
            new ConvertibleMockObject(intValue: 111)
        );

        [Theory]
        [MemberData(nameof(objectToAnyConvertRoundTripTest_data))]
        public void objectToAnyConvertRoundTripTest(ASObject obj) {
            ASAny any = obj;
            Assert.True(any.isDefined);
            Assert.Same(obj, any.value);
            Assert.Same(obj, (ASObject)any);

            any = new ASAny(obj);
            Assert.True(any.isDefined);
            Assert.Same(obj, any.value);
            Assert.Same(obj, (ASObject)any);
        }

        public static IEnumerable<object[]> lateBoundConvertToObjectAndAnyTest_data = TupleHelper.toArrays<ASAny>(
            ASAny.undefined,
            ASAny.@null,
            new ASObject(),
            new ASArray(),
            0,
            1u,
            1.5,
            "hello",
            true,
            new ConvertibleMockObject(intValue: 111)
        );

        [Theory]
        [MemberData(nameof(lateBoundConvertToObjectAndAnyTest_data))]
        public void lateBoundConvertToObjectAndAnyTest(ASAny obj) {
            AssertHelper.identical(obj, ASAny.AS_coerceType(obj, null));
            AssertHelper.identical(obj.isUndefined ? ASAny.@null : obj, ASAny.AS_coerceType(obj, s_objectClass));

            if (!obj.isUndefined) {
                Assert.Same(obj.value, ASObject.AS_coerceType(obj.value, null));
                Assert.Same(obj.value, ASObject.AS_coerceType(obj.value, s_objectClass));
            }
        }

        private static IEnumerable<(ASObject obj, bool isInt, bool isUint, bool isNumber, bool isPrimitive)> isInt_isUint_isNumeric_isPrimitive_commonTestData() {
            var testcases = new List<(ASObject obj, bool isInt, bool isUint, bool isNumber, bool isPrimitive)>();

            void addTestCase(ASObject obj, bool isInt = false, bool isUint = false, bool isNumber = false, bool isPrimitive = false) {
                testcases.Add((
                    obj,
                    isInt,
                    isUint,
                    isNumber: isInt || isUint || isNumber,
                    isPrimitive: isInt || isUint || isNumber || isPrimitive
                ));
            }

            addTestCase(null);

            addTestCase(0, isInt: true, isUint: true);
            addTestCase(0u, isInt: true, isUint: true);
            addTestCase(0.0, isInt: true, isUint: true);
            addTestCase(1, isInt: true, isUint: true);
            addTestCase(1u, isInt: true, isUint: true);
            addTestCase(1.0, isInt: true, isUint: true);
            addTestCase(19645, isInt: true, isUint: true);
            addTestCase(19645u, isInt: true, isUint: true);
            addTestCase(19645.0, isInt: true, isUint: true);
            addTestCase(Int32.MaxValue, isInt: true, isUint: true);
            addTestCase((uint)Int32.MaxValue, isInt: true, isUint: true);
            addTestCase((double)Int32.MaxValue, isInt: true, isUint: true);

            addTestCase(-1, isInt: true);
            addTestCase(-1.0, isInt: true);
            addTestCase(-10000, isInt: true);
            addTestCase(-10000.0, isInt: true);
            addTestCase(Int32.MinValue, isInt: true);
            addTestCase((double)Int32.MinValue, isInt: true);

            addTestCase((uint)Int32.MaxValue + 1, isUint: true);
            addTestCase((double)Int32.MaxValue + 1.0, isUint: true);
            addTestCase(3401872993u, isUint: true);
            addTestCase(3401872993.0, isUint: true);
            addTestCase(UInt32.MaxValue, isUint: true);
            addTestCase((double)UInt32.MaxValue, isUint: true);

            addTestCase(Double.Epsilon, isNumber: true);
            addTestCase(0.001, isNumber: true);
            addTestCase(-0.999999, isNumber: true);
            addTestCase(Math.BitIncrement(1.0), isNumber: true);
            addTestCase(134457.56, isNumber: true);
            addTestCase(Math.BitDecrement((double)Int32.MaxValue), isNumber: true);
            addTestCase(3401872993.345, isNumber: true);
            addTestCase(Math.BitDecrement((double)UInt32.MaxValue), isNumber: true);
            addTestCase(Math.BitIncrement((double)UInt32.MaxValue), isNumber: true);
            addTestCase((double)UInt32.MaxValue + 0.4, isNumber: true);
            addTestCase((double)UInt32.MaxValue + 1.0, isNumber: true);
            addTestCase(Math.BitDecrement((double)Int32.MinValue), isNumber: true);
            addTestCase((double)Int32.MinValue - 1.0, isNumber: true);
            addTestCase(9007199254740991.0, isNumber: true);
            addTestCase(Double.MaxValue, isNumber: true);
            addTestCase(Double.MinValue, isNumber: true);
            addTestCase(Double.PositiveInfinity, isNumber: true);
            addTestCase(Double.NegativeInfinity, isNumber: true);
            addTestCase(Double.NaN, isNumber: true);

            addTestCase("", isPrimitive: true);
            addTestCase("1234", isPrimitive: true);
            addTestCase("hello", isPrimitive: true);
            addTestCase(true, isPrimitive: true);
            addTestCase(false, isPrimitive: true);

            addTestCase(new ASObject());
            addTestCase(new ASArray());
            addTestCase(ASXML.createNode(XMLNodeType.TEXT));
            addTestCase(new ASXMLList());
            addTestCase(new ASDate(0));
            addTestCase(ASFunction.createEmpty());

            return testcases;
        }

        public static IEnumerable<object[]> isIntTest_data =
            isInt_isUint_isNumeric_isPrimitive_commonTestData().Select(x => new object[] {x.obj, x.isInt});

        [Theory]
        [MemberData(nameof(isIntTest_data))]
        public void isIntTest(ASObject obj, bool expected) {
            Assert.Equal(expected, ASObject.AS_isInt(obj));
            Assert.Equal(expected, ASObject.AS_isType(obj, s_intClass.classObject));
            Assert.Same(expected ? obj : null, ASObject.AS_asType(obj, s_intClass.classObject));
        }

        public static IEnumerable<object[]> isUintTest_data =
            isInt_isUint_isNumeric_isPrimitive_commonTestData().Select(x => new object[] {x.obj, x.isUint});

        [Theory]
        [MemberData(nameof(isUintTest_data))]
        public void isUintTest(ASObject obj, bool expected) {
            Assert.Equal(expected, ASObject.AS_isUint(obj));
            Assert.Equal(expected, ASObject.AS_isType(obj, s_uintClass.classObject));
            Assert.Same(expected ? obj : null, ASObject.AS_asType(obj, s_uintClass.classObject));
        }

        public static IEnumerable<object[]> isNumericTest_data =
            isInt_isUint_isNumeric_isPrimitive_commonTestData().Select(x => new object[] {x.obj, x.isNumber});

        [Theory]
        [MemberData(nameof(isNumericTest_data))]
        public void isNumericTest(ASObject obj, bool expected) {
            Assert.Equal(expected, ASObject.AS_isNumeric(obj));
            Assert.Equal(expected, ASObject.AS_isType(obj, s_numberClass.classObject));
            Assert.Same(expected ? obj : null, ASObject.AS_asType(obj, s_numberClass.classObject));
        }

        public static IEnumerable<object[]> isPrimitiveTest_data =
            isInt_isUint_isNumeric_isPrimitive_commonTestData().Select(x => new object[] {x.obj, x.isPrimitive});

        [Theory]
        [MemberData(nameof(isPrimitiveTest_data))]
        public void isPrimitiveTest(ASObject obj, bool expected) {
            Assert.Equal(expected, ASObject.AS_isPrimitive(obj));
        }

        public static IEnumerable<object[]> isArrayLikeTest_data = TupleHelper.toArrays<ASObject, bool>(
            (null, false),
            (new ASObject(), false),
            (1, false),
            (1939u, false),
            (12.33, false),
            ("hello", false),
            (true, false),
            (new ASDate(0), false),
            (new TypeConversionsTest_CA(), false),
            (ASXML.createNode(XMLNodeType.TEXT), false),
            (new ASXMLList(), false),

            (new ASArray(), true),
            (new ASArray(new ASAny[] {1, 2, 3}), true),

            (new ASVector<int>(), true),
            (new ASVector<int>(new[] {1, 2, 3, 4}), true),
            (new ASVector<double>(), true),
            (new ASVector<string>(new[] {"abc", "def"}), true),
            (new ASVector<ASObject>(), true),
            (new ASVector<TypeConversionsTest_CA>(), true),
            (new ASVector<TypeConversionsTest_CA>(new[] {new TypeConversionsTest_CA()}), true),
            (new ASVector<TypeConversionsTest_IA>(), true),
            (new ASVector<TypeConversionsTest_IA>(new TypeConversionsTest_IA[] {new TypeConversionsTest_CC()}), true)
        );

        [Theory]
        [MemberData(nameof(isArrayLikeTest_data))]
        public void isArrayLikeTest(ASObject obj, bool expected) {
            Assert.Equal(expected, ASObject.AS_isArrayLike(obj));
        }

        public static IEnumerable<object[]> objectClassCastTest_data = TupleHelper.toArrays(
            #pragma warning disable 8123

            (obj: ASAny.undefined, toType: typeof(ASObject), isSuccess: true),
            (obj: ASAny.undefined, toType: typeof(ASArray), isSuccess: true),
            (obj: ASAny.undefined, toType: typeof(TypeConversionsTest_CA), isSuccess: true),
            (obj: ASAny.undefined, toType: typeof(TypeConversionsTest_CC), isSuccess: true),
            (obj: ASAny.undefined, toType: typeof(TypeConversionsTest_IA), isSuccess: true),
            (obj: ASAny.undefined, toType: typeof(TypeConversionsTest_IC), isSuccess: true),

            (obj: ASAny.@null, toType: typeof(ASObject), isSuccess: true),
            (obj: ASAny.@null, toType: typeof(ASArray), isSuccess: true),
            (obj: ASAny.@null, toType: typeof(TypeConversionsTest_CA), isSuccess: true),
            (obj: ASAny.@null, toType: typeof(TypeConversionsTest_CC), isSuccess: true),
            (obj: ASAny.@null, toType: typeof(TypeConversionsTest_IA), isSuccess: true),
            (obj: ASAny.@null, toType: typeof(TypeConversionsTest_IC), isSuccess: true),

            (obj: new ASObject(), toType: typeof(ASObject), isSuccess: true),
            (obj: new TypeConversionsTest_CA(), toType: typeof(ASObject), isSuccess: true),
            (obj: new TypeConversionsTest_CA(), toType: typeof(TypeConversionsTest_CA), isSuccess: true),
            (obj: new TypeConversionsTest_CB(), toType: typeof(ASObject), isSuccess: true),
            (obj: new TypeConversionsTest_CB(), toType: typeof(TypeConversionsTest_CB), isSuccess: true),
            (obj: new TypeConversionsTest_CB(), toType: typeof(TypeConversionsTest_CA), isSuccess: true),
            (obj: new TypeConversionsTest_CC(), toType: typeof(ASObject), isSuccess: true),
            (obj: new TypeConversionsTest_CC(), toType: typeof(TypeConversionsTest_CC), isSuccess: true),
            (obj: new TypeConversionsTest_CC(), toType: typeof(TypeConversionsTest_CA), isSuccess: true),

            (obj: new TypeConversionsTest_CB(), toType: typeof(TypeConversionsTest_IB), isSuccess: true),
            (obj: new TypeConversionsTest_CC(), toType: typeof(TypeConversionsTest_IA), isSuccess: true),
            (obj: new TypeConversionsTest_CC(), toType: typeof(TypeConversionsTest_IC), isSuccess: true),

            (obj: new ASObject(), toType: typeof(TypeConversionsTest_CA), isSuccess: false),
            (obj: new ASObject(), toType: typeof(TypeConversionsTest_IA), isSuccess: false),
            (obj: new TypeConversionsTest_CA(), toType: typeof(TypeConversionsTest_IA), isSuccess: false),
            (obj: new TypeConversionsTest_CA(), toType: typeof(TypeConversionsTest_IB), isSuccess: false),
            (obj: new TypeConversionsTest_CB(), toType: typeof(TypeConversionsTest_CC), isSuccess: false),
            (obj: new TypeConversionsTest_CB(), toType: typeof(TypeConversionsTest_IA), isSuccess: false),
            (obj: new TypeConversionsTest_CB(), toType: typeof(TypeConversionsTest_IC), isSuccess: false),
            (obj: new TypeConversionsTest_CC(), toType: typeof(ASArray), isSuccess: false),

            (obj: 1, toType: typeof(ASObject), isSuccess: true),
            (obj: "abcd", toType: typeof(ASObject), isSuccess: true),
            (obj: "abcd", toType: typeof(ASString), isSuccess: true),
            (obj: true, toType: typeof(ASObject), isSuccess: true),
            (obj: false, toType: typeof(ASBoolean), isSuccess: true),

            (obj: 1.2, toType: typeof(TypeConversionsTest_CA), isSuccess: false),
            (obj: "abcd", toType: typeof(TypeConversionsTest_CA), isSuccess: false),
            (obj: "abcd", toType: typeof(TypeConversionsTest_IA), isSuccess: false),

            (obj: ASXML.createNode(XMLNodeType.ELEMENT, new ASQName("a")), toType: typeof(ASXML), isSuccess: true),
            (obj: ASXML.createNode(XMLNodeType.ELEMENT, new ASQName("a")), toType: typeof(ASXMLList), isSuccess: false),
            (obj: new ASXMLList(new[] {ASXML.createNode(XMLNodeType.TEXT)}), toType: typeof(ASXMLList), isSuccess: true),
            (obj: new ASXMLList(new[] {ASXML.createNode(XMLNodeType.TEXT)}), toType: typeof(ASXML), isSuccess: false),

            (obj: new ASVector<ASObject>(), toType: typeof(ASVector<ASObject>), isSuccess: true),
            (obj: new ASVector<ASObject>(), toType: typeof(ASVectorAny), isSuccess: true),
            (obj: new ASVector<int>(), toType: typeof(ASVector<int>), isSuccess: true),
            (obj: new ASVector<int>(), toType: typeof(ASVectorAny), isSuccess: true),

            (obj: new ASVector<TypeConversionsTest_CA>(), toType: typeof(ASVector<TypeConversionsTest_CA>), isSuccess: true),
            (obj: new ASVector<TypeConversionsTest_CA>(), toType: typeof(ASVectorAny), isSuccess: true),
            (obj: new ASVector<TypeConversionsTest_IA>(), toType: typeof(ASVector<TypeConversionsTest_IA>), isSuccess: true),
            (obj: new ASVector<TypeConversionsTest_IA>(), toType: typeof(ASVectorAny), isSuccess: true),

            (obj: new ASVector<ASObject>(), toType: typeof(ASVector<TypeConversionsTest_CA>), isSuccess: false),
            (obj: new ASVector<ASObject>(new ASObject[] {new TypeConversionsTest_CA()}), toType: typeof(ASVector<TypeConversionsTest_CA>), isSuccess: false),
            (obj: new ASVector<TypeConversionsTest_CA>(), toType: typeof(ASVector<ASObject>), isSuccess: false),
            (obj: new ASVector<TypeConversionsTest_CA>(), toType: typeof(ASVector<TypeConversionsTest_CB>), isSuccess: false),
            (obj: new ASVector<TypeConversionsTest_CB>(), toType: typeof(ASVector<TypeConversionsTest_CA>), isSuccess: false),
            (obj: new ASVector<TypeConversionsTest_CB>(), toType: typeof(ASVector<TypeConversionsTest_CC>), isSuccess: false),
            (obj: new ASVector<TypeConversionsTest_CC>(), toType: typeof(ASVector<TypeConversionsTest_IB>), isSuccess: false),
            (obj: new ASVector<TypeConversionsTest_CC>(), toType: typeof(ASVector<TypeConversionsTest_IC>), isSuccess: false),
            (obj: new ASVector<TypeConversionsTest_IA>(), toType: typeof(ASVector<TypeConversionsTest_IC>), isSuccess: false),
            (obj: new ASVector<TypeConversionsTest_IC>(), toType: typeof(ASVector<TypeConversionsTest_IA>), isSuccess: false),
            (obj: new ASVector<TypeConversionsTest_IC>(), toType: typeof(ASVector<TypeConversionsTest_IB>), isSuccess: false)

            #pragma warning restore 8123
        );

        [Theory]
        [MemberData(nameof(objectClassCastTest_data))]
        public void objectClassCastTest(ASAny obj, Type toType, bool isSuccess) {
            MethodInfo objectCastMethod = s_objectCastGenMethod.MakeGenericMethod(toType);
            MethodInfo anyCastMethod = s_anyCastGenMethod.MakeGenericMethod(toType);

            Class toTypeClass = Class.fromType(toType, throwIfNotExists: true);

            if (isSuccess) {
                Assert.Same(obj.value, anyCastMethod.Invoke(null, new object[] {obj}));
                AssertHelper.identical(obj.value, ASAny.AS_coerceType(obj, toTypeClass));

                if (obj.isDefined) {
                    Assert.Same(obj.value, objectCastMethod.Invoke(null, new object[] {obj.value}));
                    Assert.Same(obj.value, ASObject.AS_coerceType(obj.value, toTypeClass));
                    Assert.Equal(obj.value != null, ASObject.AS_isType(obj.value, toTypeClass.classObject));
                    Assert.Same(obj.value, ASObject.AS_asType(obj.value, toTypeClass.classObject));
                }
            }
            else {
                throwsCastError(() => anyCastMethod.Invoke(null, new object[] {obj}));
                throwsCastError(() => ASAny.AS_coerceType(obj, toTypeClass));

                if (obj.isDefined) {
                    throwsCastError(() => objectCastMethod.Invoke(null, new object[] {obj.value}));
                    throwsCastError(() => ASObject.AS_coerceType(obj.value, toTypeClass));
                    Assert.False(ASObject.AS_isType(obj.value, toTypeClass.classObject));
                    Assert.Null(ASObject.AS_asType(obj.value, toTypeClass.classObject));
                }
            }

            void throwsCastError(Func<object> testCode) =>
                AssertHelper.throwsErrorWithCode(ErrorCode.TYPE_COERCION_FAILED, testCode);
        }

        public static IEnumerable<object[]> objectToPrimitiveConvertTest_shouldThrowError_data() => TupleHelper.toArrays<ASObject>(
            objWithMethods(("toString", new ASObject())),
            objWithMethods(("valueOf", new ASObject()), ("toString", new ASObject())),
            new MockClassInstance(mockClassD)
        );

        [Theory]
        [MemberData(nameof(objectToPrimitiveConvertTest_shouldThrowError_data))]
        public void objectToPrimitiveConvertTest_shouldThrowError(ASObject obj) {
            AssertHelper.throwsErrorWithCode(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE, () => ASObject.AS_toPrimitive(obj));
            AssertHelper.throwsErrorWithCode(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE, () => ASObject.AS_toPrimitiveNumHint(obj));
            AssertHelper.throwsErrorWithCode(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE, () => ASObject.AS_toPrimitiveStringHint(obj));

            AssertHelper.throwsErrorWithCode(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE, () => (int)obj);
            AssertHelper.throwsErrorWithCode(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE, () => (uint)obj);
            AssertHelper.throwsErrorWithCode(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE, () => (double)obj);
            AssertHelper.throwsErrorWithCode(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE, () => (string)obj);
            AssertHelper.throwsErrorWithCode(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE, () => ASObject.AS_convertString(obj));

            AssertHelper.throwsErrorWithCode(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE, () => (int)(ASAny)obj);
            AssertHelper.throwsErrorWithCode(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE, () => (uint)(ASAny)obj);
            AssertHelper.throwsErrorWithCode(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE, () => (double)(ASAny)obj);
            AssertHelper.throwsErrorWithCode(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE, () => (string)(ASAny)obj);
            AssertHelper.throwsErrorWithCode(ErrorCode.CANNOT_CONVERT_OBJECT_TO_PRIMITIVE, () => ASAny.AS_convertString((ASAny)obj));
        }

        [Fact]
        public void objectToPrimitive_shouldUseStringHintForDate() {
            var date = new ASDate(18392);
            AssertHelper.valueIdentical(date.AS_toString(), ASObject.AS_toPrimitive(date));
        }

        [Fact]
        public void objectToPrimitive_shouldUseNumberHintForNonDate() {
            AssertHelper.valueIdentical(9999.36, ASObject.AS_toPrimitive(new MockClassInstance(mockClassC)));
        }

        public static IEnumerable<object[]> isAsTypeTest_typeArgIsNotClass_data() {
            var function = ASFunction.createEmpty();

            return TupleHelper.toArrays(
                (null, null),
                (null, new ASObject()),
                (null, 123),

                (new ASObject(), null),
                (new ASObject(), 123),
                (new ASObject(), new ASObject()),
                (new ASObject(), Class.fromType(typeof(ASObject)).prototypeObject),

                (new TypeConversionsTest_CC(), new TypeConversionsTest_CA()),
                (new TypeConversionsTest_CC(), Class.fromType(typeof(TypeConversionsTest_CC)).prototypeObject),

                (ASObject.AS_createWithPrototype(function.prototype), function)
            );
        }

        [Theory]
        [MemberData(nameof(isAsTypeTest_typeArgIsNotClass_data))]
        public void isAsTypeTest_typeArgIsNotClass(ASObject obj, ASObject type) {
            AssertHelper.throwsErrorWithCode(ErrorCode.IS_AS_NOT_CLASS, () => ASObject.AS_isType(obj, type));
            AssertHelper.throwsErrorWithCode(ErrorCode.IS_AS_NOT_CLASS, () => ASObject.AS_asType(obj, type));
        }

        public static IEnumerable<object[]> objectToPrimitiveTest_withPrimitiveValueOrNull_data = TupleHelper.toArrays<ASObject>(
            0,
            123u,
            -193.439,
            Double.NaN,
            "",
            "hello",
            true,
            false,
            null
        );

        [Theory]
        [MemberData(nameof(objectToPrimitiveTest_withPrimitiveValueOrNull_data))]
        public void objectToPrimitiveTest_withPrimitiveValueOrNull(ASObject obj) {
            AssertHelper.identical(obj, ASObject.AS_toPrimitive(obj));
            AssertHelper.identical(obj, ASObject.AS_toPrimitiveNumHint(obj));
            AssertHelper.identical(obj, ASObject.AS_toPrimitiveStringHint(obj));
        }

        public static IEnumerable<object[]> boxedValueToObjectConvertTest_data() {
            var uniqueObject = new ASObject();
            var uniqueObjectCA = new TypeConversionsTest_CA();

            return TupleHelper.toArrays<object, ASAny>(
                (null, ASAny.@null),

                (ASAny.@null, ASAny.@null),
                (ASAny.undefined, ASAny.undefined),

                ((byte)0, 0),
                ((byte)255, 255),
                ((sbyte)1, 1),
                ((sbyte)(-1), -1),
                ((sbyte)127, 127),
                ((sbyte)(-128), -128),
                ((short)0, 0),
                ((short)32767, 32767),
                ((short)(-32768), -32768),
                ((ushort)0, 0),
                ((ushort)65535, 65535),

                (0, 0),
                (13432, 13432),
                (-1932, -1932),
                (Int32.MaxValue, Int32.MaxValue),
                (Int32.MinValue, Int32.MinValue),

                (0u, 0u),
                (18394u, 18394u),
                ((uint)Int32.MaxValue, (uint)Int32.MaxValue),
                (3473001938u, 3473001938u),
                (UInt32.MaxValue, UInt32.MaxValue),

                (0.0, 0.0),
                (0.0f, 0.0),
                (NEG_ZERO, NEG_ZERO),
                (BitConverter.Int32BitsToSingle(unchecked((int)0x80000000u)), NEG_ZERO),
                (1.5, 1.5),
                (1.5f, 1.5),
                (1.3f, (double)1.3f),
                (2147483648.0, 2147483648.0),
                (2147483648.0f, (double)2147483648.0f),
                (Single.MaxValue, (double)Single.MaxValue),
                (Double.MaxValue, Double.MaxValue),
                (Single.PositiveInfinity, Double.PositiveInfinity),
                (Double.PositiveInfinity, Double.PositiveInfinity),
                (Single.NegativeInfinity, Double.NegativeInfinity),
                (Double.NegativeInfinity, Double.NegativeInfinity),
                (Single.NaN, Double.NaN),
                (Double.NaN, Double.NaN),

                ("", ""),
                ("hello", "hello"),

                (true, true),
                (false, false),

                ((ASAny)123, 123),
                ((ASAny)193.42, 193.42),
                ((ASAny)true, true),
                ((ASAny)"hello", "hello"),

                (uniqueObject, uniqueObject),
                ((ASAny)uniqueObject, uniqueObject),
                (uniqueObjectCA, uniqueObjectCA),
                ((ASAny)uniqueObjectCA, uniqueObjectCA)
            );
        }

        [Theory]
        [MemberData(nameof(boxedValueToObjectConvertTest_data))]
        public void boxedValueToObjectConvertTest(object value, ASAny expected) {
            AssertHelper.valueIdentical(expected, ASAny.AS_fromBoxed(value));
            AssertHelper.valueIdentical(expected.value, ASObject.AS_fromBoxed(value));
        }

        public static IEnumerable<object[]> boxedValueToObjectConvertTest_shouldThrowError_data = TupleHelper.toArrays<object>(
            new object(),
            0L,
            UInt64.MaxValue,
            1.0m,
            new DateTime(59455540490013253L),
            'a',
            IntPtr.Zero,
            typeof(ASObject)
        );

        [Theory]
        [MemberData(nameof(boxedValueToObjectConvertTest_shouldThrowError_data))]
        public void boxedValueToObjectConvertTest_shouldThrowError(object value) {
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__OBJECT_FROMPRIMITIVE_INVALID, () => ASAny.AS_fromBoxed(value));
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__OBJECT_FROMPRIMITIVE_INVALID, () => ASObject.AS_fromBoxed(value));
        }

    }

}
