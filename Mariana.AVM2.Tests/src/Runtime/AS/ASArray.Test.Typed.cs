using System;
using System.Collections.Generic;
using System.Linq;
using Mariana.AVM2.Core;
using Mariana.AVM2.Native;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    using IndexDict = Dictionary<uint, ASAny>;

    [AVM2ExportClass]
    public interface ASArrayTest_IA {}

    [AVM2ExportClass]
    public class ASArrayTest_ClassA : ASObject {
        static ASArrayTest_ClassA() => TestAppDomain.ensureClassesLoaded(typeof(ASArrayTest_ClassA));
    }

    [AVM2ExportClass]
    public class ASArrayTest_ClassB : ASObject, ASArrayTest_IA {
        static ASArrayTest_ClassB() => TestAppDomain.ensureClassesLoaded(typeof(ASArrayTest_ClassB));
    }

    public partial class ASArrayTest {

        public static IEnumerable<object[]> fromObjectSpanTest_data = TupleHelper.toArrays(
            Array.Empty<ASObject>(),
            rangeSelect(0, 10, i => new ASObject()),
            rangeSelect(0, 1000, i => new ASObject()),
            rangeMultiSelect(0, 200, i => new[] {
                new ASObject(), new ASObject(), new ASArrayTest_ClassA(), new ASArrayTest_ClassB(), null
            })
        );

        [Theory]
        [MemberData(nameof(fromObjectSpanTest_data))]
        public void fromObjectSpanTest(ASObject[] data) {
            var instance = ASArray.fromObjectSpan<ASObject>(data);
            Assert.Equal(data.Length, (int)instance.length);

            for (int i = 0; i < data.Length; i++)
                AssertHelper.identical(data[i], instance.AS_getElement(i));
        }

        public static IEnumerable<object[]> fromObjectSpanTest_derivedClass_data = TupleHelper.toArrays(
            Array.Empty<ASArrayTest_ClassA>(),
            rangeSelect(0, 10, i => new ASArrayTest_ClassA()),
            rangeSelect(0, 1000, i => new ASArrayTest_ClassA()),
            rangeSelect(0, 1000, i => (i % 10 == 0) ? null : new ASArrayTest_ClassA())
        );

        [Theory]
        [MemberData(nameof(fromObjectSpanTest_derivedClass_data))]
        public void fromObjectSpanTest_derivedClass(ASArrayTest_ClassA[] data) {
            var instance = ASArray.fromObjectSpan<ASObject>(data);
            Assert.Equal(data.Length, (int)instance.length);

            for (int i = 0; i < data.Length; i++)
                AssertHelper.identical(data[i], instance.AS_getElement(i));
        }

        public static IEnumerable<object[]> fromArraySpanAndEnumerableTest_int_data = TupleHelper.toArrays(
            Array.Empty<int>(),
            new[] {0, 1, -2, 3, 4, 5, 6, -7, 8, 9, 10, 11, -12},
            rangeSelect(1, 1000, i => (int)(10000 + i))
        );

        [Theory]
        [MemberData(nameof(fromArraySpanAndEnumerableTest_int_data))]
        public void fromArraySpanAndEnumerableTest_int(int[] data) {
            test(ASArray.fromSpan<int>(data));
            test(ASArray.fromTypedArray(data));
            test(ASArray.fromEnumerable(data));
            test(ASArray.fromEnumerable(data.Select(x => x)));

            void test(ASArray instance) {
                Assert.Equal(data.Length, (int)instance.length);
                for (int i = 0; i < data.Length; i++) {
                    ASAny element = instance.AS_getElement(i);
                    Assert.IsType<ASint>(element.value);
                    Assert.Equal(data[i], (int)element);
                }
            }
        }

        public static IEnumerable<object[]> fromArraySpanAndEnumerableTest_uint_data = TupleHelper.toArrays(
            Array.Empty<uint>(),
            new uint[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12},
            rangeSelect(1000, 2000, i => UInt32.MaxValue - i)
        );

        [Theory]
        [MemberData(nameof(fromArraySpanAndEnumerableTest_uint_data))]
        public void fromArraySpanAndEnumerableTest_uint(uint[] data) {
            test(ASArray.fromSpan<uint>(data));
            test(ASArray.fromTypedArray(data));
            test(ASArray.fromEnumerable(data));
            test(ASArray.fromEnumerable(data.Select(x => x)));

            void test(ASArray instance) {
                Assert.Equal(data.Length, (int)instance.length);
                for (int i = 0; i < data.Length; i++) {
                    ASAny element = instance.AS_getElement(i);
                    Assert.IsType<ASuint>(element.value);
                    Assert.Equal(data[i], (uint)element);
                }
            }
        }

        public static IEnumerable<object[]> fromArraySpanAndEnumerableTest_number_data = TupleHelper.toArrays(
            Array.Empty<double>(),
            new double[] {
                0, 1, 2, -3, 4, 5, 6, 7, -8, 9, 0.5, -1.5, -2.5,
                Int32.MaxValue, Int32.MinValue, UInt32.MaxValue,
                Double.PositiveInfinity, Double.NegativeInfinity, Double.NaN
            },
            rangeSelect(1000, 2000, i => i * 2.434594881)
        );

        [Theory]
        [MemberData(nameof(fromArraySpanAndEnumerableTest_number_data))]
        public void fromArraySpanAndEnumerableTest_number(double[] data) {
            test(ASArray.fromSpan<double>(data));
            test(ASArray.fromTypedArray(data));
            test(ASArray.fromEnumerable(data));
            test(ASArray.fromEnumerable(data.Select(x => x)));

            void test(ASArray instance) {
                Assert.Equal(data.Length, (int)instance.length);
                for (int i = 0; i < data.Length; i++) {
                    ASAny element = instance.AS_getElement(i);
                    Assert.True(element.value is ASint || element.value is ASuint || element.value is ASNumber);
                    Assert.Equal(data[i], (double)element);
                }
            }
        }

        public static IEnumerable<object[]> fromArraySpanAndEnumerableTest_string_data() {
            var random = new Random(9822138);

            return TupleHelper.toArrays(
                Array.Empty<string>(),
                new string[] {"", "a", "b", "c", "d", "e", "f"},
                new string[] {"", null, "abc", "xyz", null, null, "abcz"},
                new string[] {null, null, null, null, null, null, null, null, null, null, "abc", null, null, null, null, null, "abc", null, null},
                rangeSelect(0, 999, i => RandomHelper.randomString(random, 0, 20))
            );
        }

        [Theory]
        [MemberData(nameof(fromArraySpanAndEnumerableTest_string_data))]
        public void fromArraySpanAndEnumerableTest_string(string[] data) {
            test(ASArray.fromSpan<string>(data));
            test(ASArray.fromTypedArray(data));
            test(ASArray.fromEnumerable(data));
            test(ASArray.fromEnumerable(data.Select(x => x)));

            void test(ASArray instance) {
                Assert.Equal(data.Length, (int)instance.length);
                for (int i = 0; i < data.Length; i++) {
                    ASAny element = instance.AS_getElement(i);
                    if (data[i] == null) {
                        AssertHelper.identical(ASAny.@null, element);
                    }
                    else {
                        Assert.IsType<ASString>(element.value);
                        Assert.Equal(data[i], (string)element);
                    }
                }
            }
        }

        public static IEnumerable<object[]> fromArraySpanAndEnumerableTest_bool_data = TupleHelper.toArrays(
            Array.Empty<bool>(),
            new bool[] {true, true, false, true, true, false, false, true, false, false, false, true, true, false, true, true},
            rangeSelect(0, 999, i => i % 2 == 0)
        );

        [Theory]
        [MemberData(nameof(fromArraySpanAndEnumerableTest_bool_data))]
        public void fromArraySpanAndEnumerableTest_bool(bool[] data) {
            test(ASArray.fromSpan<bool>(data));
            test(ASArray.fromTypedArray(data));
            test(ASArray.fromEnumerable(data));
            test(ASArray.fromEnumerable(data.Select(x => x)));

            void test(ASArray instance) {
                Assert.Equal(data.Length, (int)instance.length);
                for (int i = 0; i < data.Length; i++) {
                    ASAny element = instance.AS_getElement(i);
                    Assert.IsType<ASBoolean>(element.value);
                    Assert.Equal(data[i], (bool)element);
                }
            }
        }

        public static IEnumerable<object[]> fromArraySpanAndEnumerableTest_any_data = TupleHelper.toArrays(
            Array.Empty<ASAny>(),
            rangeSelect(1, 20, i => ASAny.undefined),
            new ASAny[] {
                new ASObject(),
                new ASObject(),
                ASAny.undefined,
                ASAny.@null,
                ASAny.undefined,
                new ASArrayTest_ClassA(),
                new ASArrayTest_ClassB(),
                ASAny.@null,
                ASAny.undefined,
                new ASObject(),
            },
            rangeSelect(0, 999, i => {
                switch (i % 5) {
                    case 0: return new ASObject();
                    case 1: return ASAny.undefined;
                    case 2: return new ASArrayTest_ClassA();
                    case 3: return ASAny.@null;
                    default: return new ASArrayTest_ClassB();
                }
            })
        );

        [Theory]
        [MemberData(nameof(fromArraySpanAndEnumerableTest_any_data))]
        public void fromArraySpanAndEnumerableTest_any(ASAny[] data) {
            test(ASArray.fromSpan<ASAny>(data));
            test(ASArray.fromTypedArray(data));
            test(ASArray.fromEnumerable(data));
            test(ASArray.fromEnumerable(data.Select(x => x)));

            void test(ASArray instance) {
                Assert.Equal(data.Length, (int)instance.length);
                for (int i = 0; i < data.Length; i++)
                    AssertHelper.identical(data[i], instance.AS_getElement(i));
            }
        }

        public static IEnumerable<object[]> fromArraySpanAndEnumerableTest_object_data = TupleHelper.toArrays(
            Array.Empty<ASObject>(),
            rangeSelect(1, 20, i => new ASObject()),
            rangeSelect<ASObject>(1, 20, i => null),
            new ASObject[] {
                new ASObject(),
                new ASObject(),
                null,
                null,
                new ASArrayTest_ClassA(),
                new ASArrayTest_ClassB(),
                null,
                new ASObject(),
            },
            rangeSelect(0, 999, i => {
                switch (i % 4) {
                    case 0: return new ASObject();
                    case 1: return null;
                    case 2: return new ASArrayTest_ClassA();
                    default: return new ASArrayTest_ClassB();
                }
            })
        );

        [Theory]
        [MemberData(nameof(fromArraySpanAndEnumerableTest_object_data))]
        public void fromArraySpanAndEnumerableTest_object(ASObject[] data) {
            test(ASArray.fromSpan<ASObject>(data));
            test(ASArray.fromTypedArray(data));
            test(ASArray.fromEnumerable(data));
            test(ASArray.fromEnumerable(data.Select(x => x)));

            void test(ASArray instance) {
                Assert.Equal(data.Length, (int)instance.length);
                for (int i = 0; i < data.Length; i++)
                    AssertHelper.identical(data[i], instance.AS_getElement(i));
            }
        }

        public static IEnumerable<object[]> fromArraySpanAndEnumerableTest_class_data = TupleHelper.toArrays(
            Array.Empty<ASArrayTest_ClassA>(),
            new ASArrayTest_ClassA[] {
                new ASArrayTest_ClassA(),
                new ASArrayTest_ClassA(),
                null,
                null,
                new ASArrayTest_ClassA(),
                null,
            },
            rangeSelect(0, 999, i => (i % 3 == 0) ? null : new ASArrayTest_ClassA())
        );

        [Theory]
        [MemberData(nameof(fromArraySpanAndEnumerableTest_class_data))]
        public void fromArraySpanAndEnumerableTest_class(ASArrayTest_ClassA[] data) {
            test(ASArray.fromSpan<ASArrayTest_ClassA>(data));
            test(ASArray.fromTypedArray(data));
            test(ASArray.fromEnumerable(data));
            test(ASArray.fromEnumerable(data.Select(x => x)));

            void test(ASArray instance) {
                Assert.Equal(data.Length, (int)instance.length);
                for (int i = 0; i < data.Length; i++)
                    AssertHelper.identical(data[i], instance.AS_getElement(i));
            }
        }

        public static IEnumerable<object[]> fromArraySpanAndEnumerableTest_interface_data = TupleHelper.toArrays(
            Array.Empty<ASArrayTest_IA>(),
            new ASArrayTest_IA[] {
                new ASArrayTest_ClassB(),
                new ASArrayTest_ClassB(),
                null,
                null,
                new ASArrayTest_ClassB(),
                null,
            },
            rangeSelect<ASArrayTest_IA>(0, 999, i => (i % 3 == 0) ? null : new ASArrayTest_ClassB())
        );

        [Theory]
        [MemberData(nameof(fromArraySpanAndEnumerableTest_interface_data))]
        public void fromArraySpanAndEnumerableTest_interface(ASArrayTest_IA[] data) {
            test(ASArray.fromSpan<ASArrayTest_IA>(data));
            test(ASArray.fromTypedArray(data));
            test(ASArray.fromEnumerable(data));
            test(ASArray.fromEnumerable(data.Select(x => x)));

            void test(ASArray instance) {
                Assert.Equal(data.Length, (int)instance.length);
                for (int i = 0; i < data.Length; i++)
                    AssertHelper.identical((ASObject)(object)data[i], instance.AS_getElement(i));
            }
        }

        private static void copyToSpanTestHelper<T>(
            ASArray array,
            uint startIndex,
            int length,
            IndexDict prototypeProperties,
            Action<ASAny, T> assertion,
            bool mustThrowCastError = false
        ) {
            setPrototypeProperties(prototypeProperties);

            try {
                uint arrayLength = array.length;
                T[] typedArray = new T[length];

                if (startIndex > arrayLength || (uint)length > arrayLength - startIndex) {
                    AssertHelper.throwsErrorWithCode(
                        ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE,
                        () => array.copyToSpan<T>(startIndex, typedArray)
                    );
                }
                else if (mustThrowCastError) {
                    AssertHelper.throwsErrorWithCode(
                        ErrorCode.TYPE_COERCION_FAILED,
                        () => array.copyToSpan<T>(startIndex, typedArray)
                    );
                }
                else {
                    array.copyToSpan<T>(startIndex, typedArray);
                    for (int i = 0; i < length; i++)
                        assertion(array.AS_getElement(startIndex + (uint)i), typedArray[i]);
                }
            }
            finally {
                resetPrototypeProperties();
            }
        }

        private static void toTypedArrayTestHelper<T>(
            ASArray array, IndexDict prototypeProperties, Action<ASAny, T> assertion, bool mustThrowCastError = false)
        {
            setPrototypeProperties(prototypeProperties);

            try {
                if (mustThrowCastError) {
                    AssertHelper.throwsErrorWithCode(
                        ErrorCode.TYPE_COERCION_FAILED,
                        () => array.toTypedArray<T>()
                    );
                }
                else {
                    T[] typedArray = array.toTypedArray<T>();
                    Assert.Equal(Math.Min(array.length, (uint)Int32.MaxValue), (uint)typedArray.Length);

                    for (int i = 0; i < typedArray.Length; i++)
                        assertion(array.AS_getElement(i), typedArray[i]);
                }
            }
            finally {
                resetPrototypeProperties();
            }
        }

        private static void asEnumerableTestHelper<T>(
            ASArray array, IndexDict prototypeProperties, Action<ASAny, T> assertion, bool mustThrowCastError = false)
        {
            setPrototypeProperties(prototypeProperties);

            bool castErrorThrown = false;

            try {
                uint curIndex = 0;
                foreach (T value in array.asEnumerable<T>()) {
                    assertion(array.AS_getElement(curIndex), value);
                    curIndex++;
                }
                Assert.Equal(array.length, curIndex);
            }
            catch (AVM2Exception e)
                when (mustThrowCastError
                    && e.thrownValue.value is ASError error
                    && error.errorID == (int)ErrorCode.TYPE_COERCION_FAILED)
            {
                castErrorThrown = true;
            }
            finally {
                resetPrototypeProperties();
            }

            if (mustThrowCastError)
                Assert.True(castErrorThrown, "Expected a type coercion failure.");
        }

        public static IEnumerable<object[]> copyToSpanTest_data() {
            var random = new Random(96500482);
            var testcases = new List<(ArrayWrapper, uint, int, IndexDict)>();

            void addTestCase(
                ASArray array, IEnumerable<Mutation> mutations = null, IEnumerable<(uint, int)> ranges = null, IndexDict prototype = null)
            {
                applyMutations(array, mutations);

                if (ranges == null)
                    testcases.Add((new ArrayWrapper(array), 0, checked((int)array.length), prototype));
                else
                    testcases.AddRange(ranges.Select(x => (new ArrayWrapper(array), x.Item1, x.Item2, prototype)));
            }

            ASObject randomObject() {
                return new ConvertibleMockObject(
                    intValue: random.Next(),
                    uintValue: (uint)random.Next(),
                    numberValue: random.NextDouble(),
                    stringValue: (random.Next(4) == 0) ? null : RandomHelper.randomString(random, 0, 20, 'a', 'z'),
                    boolValue: random.Next(2) == 0
                );
            }

            addTestCase(
                array: new ASArray(),
                ranges: new (uint, int)[] {
                    (0, 0),
                    (1, 0),
                    (UInt32.MaxValue, 0),
                    (0, 1),
                    (1, 1),
                    (0, 100),
                    (UInt32.MaxValue, 1),
                }
            );

            addTestCase(
                array: new ASArray(20),
                ranges: new (uint, int)[] {
                    (0, 0), (10, 0), (19, 0), (20, 0),
                    (0, 5), (0, 10), (0, 20),
                    (5, 5), (5, 15),
                    (19, 1),
                    (0, 21), (20, 1), (21, 0), (21, 1),
                    (5, 16), (5, 20),
                    (0, 100),
                    (UInt32.MaxValue, 0),
                    (UInt32.MaxValue, 100)
                }
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 20, i => randomObject())),
                ranges: new (uint, int)[] {
                    (0, 0), (10, 0), (19, 0), (20, 0),
                    (0, 5), (0, 10), (0, 20),
                    (5, 5), (5, 15),
                    (19, 1),
                    (0, 21), (20, 1), (21, 0), (21, 1),
                    (5, 16), (5, 20),
                    (0, 100),
                    (UInt32.MaxValue, 0),
                    (UInt32.MaxValue, 100)
                }
            );

            addTestCase(
                array: new ASArray(),
                mutations: rangeSelect(0, 49, i => mutSetUint(i, randomObject())),
                ranges: new (uint, int)[] {(0, 0), (20, 0), (50, 0), (0, 50), (20, 30), (40, 5)}
            );

            addTestCase(
                array: new ASArray(new[] {
                    ASAny.undefined,
                    ASAny.@null,
                    ASAny.undefined,
                    randomObject(),
                    randomObject(),
                    ASAny.@null,
                    ASAny.@null,
                    randomObject(),
                    ASAny.undefined
                }),
                ranges: new (uint, int)[] {(0, 9)}
            );

            addTestCase(
                array: new ASArray(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutPushOne(randomObject())),
                    rangeSelect(0, 19, i => mutUnshift(randomObject())),
                    mutPush(rangeSelect<ASAny>(0, 19, i => randomObject())),
                    mutPush(rangeSelect<ASAny>(0, 19, i => randomObject())),
                    mutDelUint(3),
                    mutDelUint(13),
                    mutDelUint(45),
                    mutDelUint(46),
                    mutDelUint(78),
                    mutDelUint(79)
                ),
                ranges: new (uint, int)[] {(0, 80), (0, 78), (70, 9), (70, 10), (78, 2), (78, 3)}
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 30, i => randomObject())),
                mutations: buildMutationList(
                    rangeSelect(29, 20, i => mutDelUint(i)),
                    mutSetLength(35)
                ),
                ranges: new (uint, int)[] {
                    (0, 0), (0, 15), (0, 20), (0, 25), (0, 30), (0, 32), (0, 35),
                    (10, 0), (10, 5), (10, 10), (10, 15), (10, 20), (10, 22), (10, 25),
                    (20, 0), (20, 5), (20, 10), (20, 12), (20, 15),
                    (25, 0), (25, 3), (25, 5), (25, 7), (25, 10),
                    (30, 0), (30, 2), (30, 5),
                    (33, 0), (33, 2),
                    (35, 0)
                }
            );

            addTestCase(
                array: new ASArray(UInt32.MaxValue),
                mutations: rangeSelect(0, 19, i => mutSetUint(i, randomObject())),
                ranges: new (uint, int)[] {
                    (0, 0), (10, 0), (19, 0), (20, 0),
                    (0, 5), (0, 10), (0, 20),
                    (5, 5), (5, 15),
                    (19, 1),
                    (0, 21), (20, 1), (21, 0), (21, 1),
                    (5, 16), (5, 20), (5, 100),
                    (UInt32.MaxValue / 2, 30),
                    (UInt32.MaxValue - 30, 30),
                    (UInt32.MaxValue, 0),
                    (UInt32.MaxValue, 15),
                    (UInt32.MaxValue - 20, 21),
                }
            );

            addTestCase(
                array: new ASArray(),
                mutations: buildMutationList(
                    rangeSelect(1, 200, i => mutSetUint((uint)random.Next(1000), randomObject())),
                    mutSetLength(1000)
                ),
                ranges: new (uint, int)[] {
                    (0, 0), (200, 0), (1000, 0),
                    (0, 100), (500, 100), (900, 100),
                    (0, 300), (300, 300), (600, 300),
                    (0, 500), (500, 500),
                    (0, 1000),
                    (999, 1),
                    (1000, 0),
                    (0, 1001), (1, 1000), (1000, 1), (1001, 0), (1001, 1),
                    (2000, 0), (2000, 2000),
                }
            );

            addTestCase(
                array: new ASArray(UInt32.MaxValue),
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutSetUint(i, randomObject())),
                    rangeSelect(0, 99, i => mutSetUint(i + 2147483630, randomObject())),
                    rangeSelect(0, 99, i => mutSetUint(i + 3473100483, randomObject())),
                    rangeSelect(0, 99, i => mutSetUint(UInt32.MaxValue - i - 1, randomObject()))
                ),
                ranges: new (uint, int)[] {
                    (0, 100), (0, 200), (50, 100),
                    (100, 100), (1000, 100),
                    (2147483620, 100), (2147483620, 110), (2147483620, 120),
                    (2147483630, 100), (2147483630, 120),
                    (2147483640, 80), (2147483640, 90), (2147483640, 100),
                    (2147483660, 60), (2147483660, 70), (2147483660, 80),
                    (2147483729, 1), (2147483729, 2),
                    (2147483730, 10), (2147483740, 10),
                    (3473100473, 100), (3473100473, 110), (3473100473, 120),
                    (3473100483, 100), (3473100483, 120),
                    (3473100493, 80), (3473100493, 90), (3473100493, 100),
                    (3473100513, 60), (3473100513, 70), (3473100513, 80),
                    (3473100582, 1), (2147483582, 2),
                    (3473100583, 10), (3473100593, 10),
                    (UInt32.MaxValue - 200, 100), (UInt32.MaxValue - 200, 150), (UInt32.MaxValue - 200, 200),
                    (UInt32.MaxValue - 100, 50), (UInt32.MaxValue - 100, 100),
                    (UInt32.MaxValue - 1, 1), (UInt32.MaxValue, 0),
                }
            );

            addTestCase(
                array: new ASArray(),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, new ASAny(randomObject())))),
                ranges: new (uint, int)[] {(0, 0), (0, 100)}
            );

            addTestCase(
                array: new ASArray(100),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, new ASAny(randomObject())))),
                ranges: new (uint, int)[] {(0, 100), (40, 30), (50, 10)}
            );

            addTestCase(
                array: new ASArray(),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, new ASAny(randomObject())))),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomObject())),
                ranges: new (uint, int)[] {(0, 100), (40, 30), (50, 10)}
            );

            addTestCase(
                array: new ASArray(),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, new ASAny(randomObject())))),
                mutations: buildMutationList(
                    rangeSelect(0, 52, i => mutSetUint(i, randomObject())),
                    rangeSelect(57, 99, i => mutSetUint(i, randomObject()))
                ),
                ranges: new (uint, int)[] {(0, 100), (40, 30), (50, 10), (53, 4)}
            );

            addTestCase(
                array: new ASArray(),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, new ASAny(randomObject())))),
                mutations: buildMutationList(
                    rangeSelect(0, 10, i => mutSetUint(i, randomObject())),
                    rangeSelect(90, 99, i => mutSetUint(i, randomObject()))
                ),
                ranges: new (uint, int)[] {(0, 100), (5, 90), (10, 80), (50, 10)}
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(copyToSpanTest_data))]
        public void copyToSpanTest_int(ArrayWrapper array, uint startIndex, int length, IndexDict prototype) {
            copyToSpanTestHelper<int>(array.instance, startIndex, length, prototype, (x, y) => Assert.Equal((int)x, y));
        }

        [Theory]
        [MemberData(nameof(copyToSpanTest_data))]
        public void copyToSpanTest_uint(ArrayWrapper array, uint startIndex, int length, IndexDict prototype) {
            copyToSpanTestHelper<uint>(array.instance, startIndex, length, prototype, (x, y) => Assert.Equal((uint)x, y));
        }

        [Theory]
        [MemberData(nameof(copyToSpanTest_data))]
        public void copyToSpanTest_number(ArrayWrapper array, uint startIndex, int length, IndexDict prototype) {
            copyToSpanTestHelper<double>(array.instance, startIndex, length, prototype, (x, y) => Assert.Equal((double)x, y));
        }

        [Theory]
        [MemberData(nameof(copyToSpanTest_data))]
        public void copyToSpanTest_string(ArrayWrapper array, uint startIndex, int length, IndexDict prototype) {
            copyToSpanTestHelper<string>(array.instance, startIndex, length, prototype, (x, y) => Assert.Equal((string)x, y));
        }

        [Theory]
        [MemberData(nameof(copyToSpanTest_data))]
        public void copyToSpanTest_bool(ArrayWrapper array, uint startIndex, int length, IndexDict prototype) {
            copyToSpanTestHelper<bool>(array.instance, startIndex, length, prototype, (x, y) => Assert.Equal((bool)x, y));
        }

        [Theory]
        [MemberData(nameof(copyToSpanTest_data))]
        public void copyToSpanTest_object(ArrayWrapper array, uint startIndex, int length, IndexDict prototype) {
            copyToSpanTestHelper<ASObject>(array.instance, startIndex, length, prototype, (x, y) => Assert.Same(x.value, y));
        }

        [Theory]
        [MemberData(nameof(copyToSpanTest_data))]
        public void copyToSpanTest_any(ArrayWrapper array, uint startIndex, int length, IndexDict prototype) {
            copyToSpanTestHelper<ASAny>(array.instance, startIndex, length, prototype, (x, y) => AssertHelper.identical(x, y));
        }

        public static IEnumerable<object[]> copyToSpanTest_class_data() {
            var testcases = new List<(ArrayWrapper, uint, int, IndexDict, bool)>();

            void addTestCase(
                ASArray array,
                IEnumerable<Mutation> mutations = null,
                IEnumerable<(uint, int)> ranges = null,
                IndexDict prototype = null,
                bool throwsCastError = false
            ) {
                applyMutations(array, mutations);

                if (ranges == null)
                    testcases.Add((new ArrayWrapper(array), 0, checked((int)array.length), prototype, throwsCastError));
                else
                    testcases.AddRange(ranges.Select(x => (new ArrayWrapper(array), x.Item1, x.Item2, prototype, throwsCastError)));
            }

            addTestCase(new ASArray());
            addTestCase(new ASArray(20));
            addTestCase(new ASArray(new[] {ASAny.undefined, ASAny.@null, ASAny.undefined, ASAny.@null}));
            addTestCase(new ASArray(rangeSelect<ASAny>(1, 20, i => new ASArrayTest_ClassA())));

            addTestCase(new ASArray(new[] {
                new ASArrayTest_ClassA(),
                ASAny.@null,
                ASAny.undefined,
                new ASArrayTest_ClassA(),
                ASAny.@null,
                ASAny.undefined,
            }));

            addTestCase(
                array: new ASArray(),
                mutations: rangeSelect(1, 50, i => mutSetUint(20 * i, new ASArrayTest_ClassA()))
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 20, i => new ASArrayTest_ClassB())),
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(new[] {
                    new ASArrayTest_ClassA(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassB(),
                    ASAny.@null,
                    ASAny.undefined,
                }),
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(new[] {
                    new ASArrayTest_ClassA(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassA(),
                    new ASArrayTest_ClassA(),
                    ASAny.undefined,
                    new ASArrayTest_ClassB(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassA(),
                    new ASArrayTest_ClassA(),
                }),
                ranges: new (uint, int)[] {(0, 6), (7, 4), (6, 0)}
            );

            addTestCase(
                array: new ASArray(new[] {
                    new ASArrayTest_ClassA(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassA(),
                    new ASArrayTest_ClassA(),
                    ASAny.undefined,
                    new ASArrayTest_ClassB(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassA(),
                    new ASArrayTest_ClassA(),
                }),
                ranges: new (uint, int)[] {(0, 11), (6, 1), (5, 3), (6, 5)},
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(),
                mutations: buildMutationList(
                    rangeSelect(1, 50, i => mutSetUint(20 * i, new ASArrayTest_ClassA())),
                    mutSetUint(508, new ASArrayTest_ClassB()),
                    mutSetUint(637, new ASObject())
                ),
                ranges: new (uint, int)[] {(0, 508), (509, 637 - 509), (638, 1001 - 638), (508, 0), (637, 0)}
            );

            addTestCase(
                array: new ASArray(),
                mutations: buildMutationList(
                    rangeSelect(1, 50, i => mutSetUint(20 * i, new ASArrayTest_ClassA())),
                    mutSetUint(508, new ASArrayTest_ClassB()),
                    mutSetUint(637, new ASObject())
                ),
                ranges: new (uint, int)[] {
                    (0, 1001), (0, 509), (0, 638),
                    (507, 3),
                    (508, 1), (508, 637 - 508), (508, 638 - 508), (508, 1001 - 508),
                    (636, 3),
                    (637, 1), (637, 1001 - 637)
                },
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(10),
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassA(),
                    [7] = ASAny.undefined,
                    [9] = new ASArrayTest_ClassA(),
                    [10] = new ASArrayTest_ClassB()
                }
            );

            addTestCase(
                array: new ASArray(11),
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassA(),
                    [7] = ASAny.undefined,
                    [9] = new ASArrayTest_ClassA(),
                    [10] = new ASArrayTest_ClassB()
                },
                ranges: new (uint, int)[] {(0, 10), (11, 0)}
            );

            addTestCase(
                array: new ASArray(11),
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassA(),
                    [7] = ASAny.undefined,
                    [9] = new ASArrayTest_ClassA(),
                    [10] = new ASArrayTest_ClassB()
                },
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 15, i => new ASArrayTest_ClassA())),
                mutations: new[] {mutSetLength(20)},
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassA(),
                    [7] = ASAny.undefined,
                    [9] = new ASArrayTest_ClassA(),
                    [10] = new ASArrayTest_ClassB(),
                    [17] = new ASArrayTest_ClassA(),
                }
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 15, i => new ASArrayTest_ClassA())),
                mutations: new[] {mutSetLength(50), mutPushOne(ASAny.undefined)},
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassA(),
                    [50] = new ASArrayTest_ClassB(),
                }
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 15, i => new ASArrayTest_ClassA())),
                mutations: new[] {mutSetLength(50), mutPushOne(ASAny.@null)},
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassA(),
                    [49] = new ASArrayTest_ClassB(),
                    [50] = new ASArrayTest_ClassB(),
                },
                throwsCastError: true
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(copyToSpanTest_class_data))]
        public void copyToSpanTest_class(ArrayWrapper array, uint startIndex, int length, IndexDict prototype, bool throwsCastError) {
            copyToSpanTestHelper<ASArrayTest_ClassA>(
                array.instance, startIndex, length, prototype, (x, y) => Assert.Same(x.value, y), throwsCastError);
        }

        public static IEnumerable<object[]> copyToSpanTest_interface_data() {
            var testcases = new List<(ArrayWrapper, uint, int, IndexDict, bool)>();

            void addTestCase(
                ASArray array,
                IEnumerable<Mutation> mutations = null,
                IEnumerable<(uint, int)> ranges = null,
                IndexDict prototype = null,
                bool throwsCastError = false
            ) {
                applyMutations(array, mutations);

                if (ranges == null)
                    testcases.Add((new ArrayWrapper(array), 0, checked((int)array.length), prototype, throwsCastError));
                else
                    testcases.AddRange(ranges.Select(x => (new ArrayWrapper(array), x.Item1, x.Item2, prototype, throwsCastError)));
            }

            addTestCase(new ASArray());
            addTestCase(new ASArray(20));
            addTestCase(new ASArray(new[] {ASAny.undefined, ASAny.@null, ASAny.undefined, ASAny.@null}));
            addTestCase(new ASArray(rangeSelect<ASAny>(1, 20, i => new ASArrayTest_ClassB())));

            addTestCase(new ASArray(new[] {
                new ASArrayTest_ClassB(),
                ASAny.@null,
                ASAny.undefined,
                new ASArrayTest_ClassB(),
                ASAny.@null,
                ASAny.undefined,
            }));

            addTestCase(
                array: new ASArray(),
                mutations: rangeSelect(1, 50, i => mutSetUint(20 * i, new ASArrayTest_ClassB()))
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 20, i => new ASArrayTest_ClassA())),
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(new[] {
                    new ASArrayTest_ClassB(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassA(),
                    ASAny.@null,
                    ASAny.undefined,
                }),
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(new[] {
                    new ASArrayTest_ClassB(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassB(),
                    new ASArrayTest_ClassB(),
                    ASAny.undefined,
                    new ASArrayTest_ClassA(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassB(),
                    new ASArrayTest_ClassB(),
                }),
                ranges: new (uint, int)[] {(0, 6), (7, 4), (6, 0)}
            );

            addTestCase(
                array: new ASArray(new[] {
                    new ASArrayTest_ClassB(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassB(),
                    new ASArrayTest_ClassB(),
                    ASAny.undefined,
                    new ASArrayTest_ClassA(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassB(),
                    new ASArrayTest_ClassB(),
                }),
                ranges: new (uint, int)[] {(0, 11), (6, 1), (5, 3), (6, 5)},
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(),
                mutations: buildMutationList(
                    rangeSelect(1, 50, i => mutSetUint(20 * i, new ASArrayTest_ClassB())),
                    mutSetUint(508, new ASArrayTest_ClassA()),
                    mutSetUint(637, new ASObject())
                ),
                ranges: new (uint, int)[] {(0, 508), (509, 637 - 509), (638, 1001 - 638), (508, 0), (637, 0)}
            );

            addTestCase(
                array: new ASArray(),
                mutations: buildMutationList(
                    rangeSelect(1, 50, i => mutSetUint(20 * i, new ASArrayTest_ClassB())),
                    mutSetUint(508, new ASArrayTest_ClassA()),
                    mutSetUint(637, new ASObject())
                ),
                ranges: new (uint, int)[] {
                    (0, 1001), (0, 509), (0, 638),
                    (507, 3),
                    (508, 1), (508, 637 - 508), (508, 638 - 508), (508, 1001 - 508),
                    (636, 3),
                    (637, 1), (637, 1001 - 637)
                },
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(10),
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassB(),
                    [7] = ASAny.undefined,
                    [9] = new ASArrayTest_ClassB(),
                    [10] = new ASArrayTest_ClassA()
                }
            );

            addTestCase(
                array: new ASArray(11),
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassB(),
                    [7] = ASAny.undefined,
                    [9] = new ASArrayTest_ClassB(),
                    [10] = new ASArrayTest_ClassA()
                },
                ranges: new (uint, int)[] {(0, 10), (11, 0)}
            );

            addTestCase(
                array: new ASArray(11),
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassB(),
                    [7] = ASAny.undefined,
                    [9] = new ASArrayTest_ClassB(),
                    [10] = new ASArrayTest_ClassA()
                },
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 15, i => new ASArrayTest_ClassB())),
                mutations: new[] {mutSetLength(20)},
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassB(),
                    [7] = ASAny.undefined,
                    [9] = new ASArrayTest_ClassB(),
                    [10] = new ASArrayTest_ClassA(),
                    [17] = new ASArrayTest_ClassB(),
                }
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 15, i => new ASArrayTest_ClassB())),
                mutations: new[] {mutSetLength(50), mutPushOne(ASAny.undefined)},
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassB(),
                    [50] = new ASArrayTest_ClassA(),
                }
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 15, i => new ASArrayTest_ClassB())),
                mutations: new[] {mutSetLength(50), mutPushOne(ASAny.@null)},
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassB(),
                    [49] = new ASArrayTest_ClassA(),
                    [50] = new ASArrayTest_ClassA(),
                },
                throwsCastError: true
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(copyToSpanTest_interface_data))]
        public void copyToSpanTest_interface(ArrayWrapper array, uint startIndex, int length, IndexDict prototype, bool throwsCastError) {
            copyToSpanTestHelper<ASArrayTest_IA>(
                array.instance, startIndex, length, prototype, (x, y) => Assert.Same(x.value, y), throwsCastError);
        }

        public static IEnumerable<object[]> toTypedArray_asEnumerable_testData() {
            var random = new Random(6773194);
            var testcases = new List<(ArrayWrapper, IndexDict)>();

            void addTestCase(
                ASArray array, IEnumerable<Mutation> mutations = null, IndexDict prototype = null)
            {
                applyMutations(array, mutations);
                testcases.Add((new ArrayWrapper(array), prototype));
            }

            ASObject randomObject() {
                return new ConvertibleMockObject(
                    intValue: random.Next(),
                    uintValue: (uint)random.Next(),
                    numberValue: random.NextDouble(),
                    stringValue: (random.Next(4) == 0) ? null : RandomHelper.randomString(random, 0, 20, 'a', 'z'),
                    boolValue: random.Next(2) == 0
                );
            }

            addTestCase(new ASArray());
            addTestCase(new ASArray(20));
            addTestCase(new ASArray(rangeSelect<ASAny>(1, 20, i => randomObject())));

            addTestCase(
                new ASArray(new[] {
                    ASAny.undefined,
                    ASAny.@null,
                    ASAny.undefined,
                    randomObject(),
                    randomObject(),
                    ASAny.@null,
                    ASAny.@null,
                    randomObject(),
                    ASAny.undefined
                })
            );

            addTestCase(
                array: new ASArray(),
                mutations: rangeSelect(0, 49, i => mutSetUint(i, randomObject()))
            );

            addTestCase(
                array: new ASArray(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutPushOne(randomObject())),
                    rangeSelect(0, 19, i => mutUnshift(randomObject())),
                    mutPush(rangeSelect<ASAny>(0, 19, i => randomObject())),
                    mutPush(rangeSelect<ASAny>(0, 19, i => randomObject())),
                    mutDelUint(3),
                    mutDelUint(13),
                    mutDelUint(45),
                    mutDelUint(46),
                    mutDelUint(78),
                    mutDelUint(79)
                )
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 30, i => randomObject())),
                mutations: buildMutationList(
                    rangeSelect(29, 20, i => mutDelUint(i)),
                    mutSetLength(35)
                )
            );

            addTestCase(
                array: new ASArray(),
                mutations: buildMutationList(
                    rangeSelect(1, 200, i => mutSetUint((uint)random.Next(1000), randomObject())),
                    mutSetLength(1000)
                )
            );

            addTestCase(
                array: new ASArray(),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, new ASAny(randomObject()))))
            );

            addTestCase(
                array: new ASArray(100),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, new ASAny(randomObject()))))
            );

            addTestCase(
                array: new ASArray(),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, new ASAny(randomObject())))),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomObject()))
            );

            addTestCase(
                array: new ASArray(),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, new ASAny(randomObject())))),
                mutations: buildMutationList(
                    rangeSelect(0, 52, i => mutSetUint(i, randomObject())),
                    rangeSelect(57, 99, i => mutSetUint(i, randomObject()))
                )
            );

            addTestCase(
                array: new ASArray(),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, new ASAny(randomObject())))),
                mutations: buildMutationList(
                    rangeSelect(0, 10, i => mutSetUint(i, randomObject())),
                    rangeSelect(90, 99, i => mutSetUint(i, randomObject()))
                )
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_testData))]
        public void toTypedArrayTest_int(ArrayWrapper array, IndexDict prototype) {
            toTypedArrayTestHelper<int>(array.instance, prototype, (x, y) => Assert.Equal((int)x, y));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_testData))]
        public void toTypedArrayTest_uint(ArrayWrapper array, IndexDict prototype) {
            toTypedArrayTestHelper<uint>(array.instance, prototype, (x, y) => Assert.Equal((uint)x, y));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_testData))]
        public void toTypedArrayTest_number(ArrayWrapper array, IndexDict prototype) {
            toTypedArrayTestHelper<double>(array.instance, prototype, (x, y) => Assert.Equal((double)x, y));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_testData))]
        public void toTypedArrayTest_string(ArrayWrapper array, IndexDict prototype) {
            toTypedArrayTestHelper<string>(array.instance, prototype, (x, y) => Assert.Equal((string)x, y));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_testData))]
        public void toTypedArrayTest_bool(ArrayWrapper array, IndexDict prototype) {
            toTypedArrayTestHelper<bool>(array.instance, prototype, (x, y) => Assert.Equal((bool)x, y));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_testData))]
        public void toTypedArrayTest_object(ArrayWrapper array, IndexDict prototype) {
            toTypedArrayTestHelper<ASObject>(array.instance, prototype, (x, y) => Assert.Same(x.value, y));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_testData))]
        public void toTypedArrayTest_any(ArrayWrapper array, IndexDict prototype) {
            toTypedArrayTestHelper<ASAny>(array.instance, prototype, (x, y) => AssertHelper.identical(x, y));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_testData))]
        public void asEnumerableTest_int(ArrayWrapper array, IndexDict prototype) {
            asEnumerableTestHelper<int>(array.instance, prototype, (x, y) => Assert.Equal((int)x, y));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_testData))]
        public void asEnumerableTest_uint(ArrayWrapper array, IndexDict prototype) {
            asEnumerableTestHelper<uint>(array.instance, prototype, (x, y) => Assert.Equal((uint)x, y));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_testData))]
        public void asEnumerableTest_number(ArrayWrapper array, IndexDict prototype) {
            asEnumerableTestHelper<double>(array.instance, prototype, (x, y) => Assert.Equal((double)x, y));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_testData))]
        public void asEnumerableTest_string(ArrayWrapper array, IndexDict prototype) {
            asEnumerableTestHelper<string>(array.instance, prototype, (x, y) => Assert.Equal((string)x, y));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_testData))]
        public void asEnumerableTest_bool(ArrayWrapper array, IndexDict prototype) {
            asEnumerableTestHelper<bool>(array.instance, prototype, (x, y) => Assert.Equal((bool)x, y));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_testData))]
        public void asEnumerableTest_object(ArrayWrapper array, IndexDict prototype) {
            asEnumerableTestHelper<ASObject>(array.instance, prototype, (x, y) => Assert.Same(x.value, y));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_testData))]
        public void asEnumerableTest_any(ArrayWrapper array, IndexDict prototype) {
            asEnumerableTestHelper<ASAny>(array.instance, prototype, (x, y) => AssertHelper.identical(x, y));
        }

        public static IEnumerable<object[]> toTypedArray_asEnumerable_class_testData() {
            var testcases = new List<(ArrayWrapper, IndexDict, bool)>();

            void addTestCase(
                ASArray array,
                IEnumerable<Mutation> mutations = null,
                IndexDict prototype = null,
                bool throwsCastError = false
            ) {
                applyMutations(array, mutations);
                testcases.Add((new ArrayWrapper(array), prototype, throwsCastError));
            }

            addTestCase(new ASArray());
            addTestCase(new ASArray(20));
            addTestCase(new ASArray(new[] {ASAny.undefined, ASAny.@null, ASAny.undefined, ASAny.@null}));
            addTestCase(new ASArray(rangeSelect<ASAny>(1, 20, i => new ASArrayTest_ClassA())));

            addTestCase(new ASArray(new[] {
                new ASArrayTest_ClassA(),
                ASAny.@null,
                ASAny.undefined,
                new ASArrayTest_ClassA(),
                ASAny.@null,
                ASAny.undefined,
            }));

            addTestCase(
                array: new ASArray(),
                mutations: rangeSelect(1, 50, i => mutSetUint(20 * i, new ASArrayTest_ClassA()))
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 20, i => new ASArrayTest_ClassB())),
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(new[] {
                    new ASArrayTest_ClassA(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassB(),
                    ASAny.@null,
                    ASAny.undefined,
                }),
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(new[] {
                    new ASArrayTest_ClassA(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassA(),
                    new ASArrayTest_ClassA(),
                    ASAny.undefined,
                    new ASArrayTest_ClassB(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassA(),
                    new ASArrayTest_ClassA(),
                }),
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(),
                mutations: buildMutationList(
                    rangeSelect(1, 50, i => mutSetUint(20 * i, new ASArrayTest_ClassA())),
                    mutSetUint(508, new ASArrayTest_ClassB()),
                    mutSetUint(637, new ASObject())
                ),
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(10),
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassA(),
                    [7] = ASAny.undefined,
                    [9] = new ASArrayTest_ClassA(),
                    [10] = new ASArrayTest_ClassB()
                }
            );

            addTestCase(
                array: new ASArray(11),
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassA(),
                    [7] = ASAny.undefined,
                    [9] = new ASArrayTest_ClassA(),
                    [10] = new ASArrayTest_ClassB()
                },
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 15, i => new ASArrayTest_ClassA())),
                mutations: new[] {mutSetLength(20)},
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassA(),
                    [7] = ASAny.undefined,
                    [9] = new ASArrayTest_ClassA(),
                    [10] = new ASArrayTest_ClassB(),
                    [17] = new ASArrayTest_ClassA(),
                }
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 15, i => new ASArrayTest_ClassA())),
                mutations: new[] {mutSetLength(50), mutPushOne(ASAny.undefined)},
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassA(),
                    [50] = new ASArrayTest_ClassB(),
                }
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 15, i => new ASArrayTest_ClassA())),
                mutations: new[] {mutSetLength(50), mutPushOne(ASAny.@null)},
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassA(),
                    [49] = new ASArrayTest_ClassB(),
                    [50] = new ASArrayTest_ClassB(),
                },
                throwsCastError: true
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_class_testData))]
        public void toTypedArrayTest_class(ArrayWrapper array, IndexDict prototype, bool throwsCastError) {
            toTypedArrayTestHelper<ASArrayTest_ClassA>(
                array.instance, prototype, (x, y) => Assert.Same(x.value, y), throwsCastError);
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_class_testData))]
        public void asEnumerableTest_class(ArrayWrapper array, IndexDict prototype, bool throwsCastError) {
            asEnumerableTestHelper<ASArrayTest_ClassA>(
                array.instance, prototype, (x, y) => Assert.Same(x.value, y), throwsCastError);
        }

        public static IEnumerable<object[]> toTypedArray_asEnumerable_interface_testData() {
            var testcases = new List<(ArrayWrapper, IndexDict, bool)>();

            void addTestCase(
                ASArray array,
                IEnumerable<Mutation> mutations = null,
                IndexDict prototype = null,
                bool throwsCastError = false
            ) {
                applyMutations(array, mutations);
                testcases.Add((new ArrayWrapper(array), prototype, throwsCastError));
            }

            addTestCase(new ASArray());
            addTestCase(new ASArray(20));
            addTestCase(new ASArray(new[] {ASAny.undefined, ASAny.@null, ASAny.undefined, ASAny.@null}));
            addTestCase(new ASArray(rangeSelect<ASAny>(1, 20, i => new ASArrayTest_ClassB())));

            addTestCase(new ASArray(new[] {
                new ASArrayTest_ClassB(),
                ASAny.@null,
                ASAny.undefined,
                new ASArrayTest_ClassB(),
                ASAny.@null,
                ASAny.undefined,
            }));

            addTestCase(
                array: new ASArray(),
                mutations: rangeSelect(1, 50, i => mutSetUint(20 * i, new ASArrayTest_ClassB()))
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 20, i => new ASArrayTest_ClassA())),
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(new[] {
                    new ASArrayTest_ClassB(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassA(),
                    ASAny.@null,
                    ASAny.undefined,
                }),
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(new[] {
                    new ASArrayTest_ClassB(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassB(),
                    new ASArrayTest_ClassB(),
                    ASAny.undefined,
                    new ASArrayTest_ClassA(),
                    ASAny.@null,
                    ASAny.undefined,
                    new ASArrayTest_ClassB(),
                    new ASArrayTest_ClassB(),
                }),
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(),
                mutations: buildMutationList(
                    rangeSelect(1, 50, i => mutSetUint(20 * i, new ASArrayTest_ClassB())),
                    mutSetUint(508, new ASArrayTest_ClassA()),
                    mutSetUint(637, new ASObject())
                ),
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(10),
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassB(),
                    [7] = ASAny.undefined,
                    [9] = new ASArrayTest_ClassB(),
                    [10] = new ASArrayTest_ClassA()
                }
            );

            addTestCase(
                array: new ASArray(11),
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassB(),
                    [7] = ASAny.undefined,
                    [9] = new ASArrayTest_ClassB(),
                    [10] = new ASArrayTest_ClassA()
                },
                throwsCastError: true
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 15, i => new ASArrayTest_ClassB())),
                mutations: new[] {mutSetLength(20)},
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassB(),
                    [7] = ASAny.undefined,
                    [9] = new ASArrayTest_ClassB(),
                    [10] = new ASArrayTest_ClassA(),
                    [17] = new ASArrayTest_ClassB(),
                }
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 15, i => new ASArrayTest_ClassB())),
                mutations: new[] {mutSetLength(50), mutPushOne(ASAny.undefined)},
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassB(),
                    [50] = new ASArrayTest_ClassA(),
                }
            );

            addTestCase(
                array: new ASArray(rangeSelect<ASAny>(1, 15, i => new ASArrayTest_ClassB())),
                mutations: new[] {mutSetLength(50), mutPushOne(ASAny.@null)},
                prototype: new IndexDict() {
                    [5] = new ASArrayTest_ClassB(),
                    [49] = new ASArrayTest_ClassA(),
                    [50] = new ASArrayTest_ClassA(),
                },
                throwsCastError: true
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_interface_testData))]
        public void toTypedArrayTest_interface(ArrayWrapper array, IndexDict prototype, bool throwsCastError) {
            toTypedArrayTestHelper<ASArrayTest_IA>(
                array.instance, prototype, (x, y) => Assert.Same(x.value, y), throwsCastError);
        }

        [Theory]
        [MemberData(nameof(toTypedArray_asEnumerable_interface_testData))]
        public void asEnumerableTest_interface(ArrayWrapper array, IndexDict prototype, bool throwsCastError) {
            asEnumerableTestHelper<ASArrayTest_IA>(
                array.instance, prototype, (x, y) => Assert.Same(x.value, y), throwsCastError);
        }

    }

}
