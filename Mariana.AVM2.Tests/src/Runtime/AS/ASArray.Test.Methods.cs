using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Mariana.Common;
using Xunit;

namespace Mariana.AVM2.Tests {

    using IndexDict = Dictionary<uint, ASAny>;

    public partial class ASArrayTest {

        public static IEnumerable<object[]> everySomeMethodTest_data() {
            setRandomSeed(669194);

            var testcases = new List<(ArrayWrapper, Image, IndexDict, SpyFunctionObject)>();

            void addTestCase(
                (ASArray arr, Image img)[] testInstances,
                (Func<ASAny[], ASAny> func, int argCount)[] functions,
                IndexDict prototype = null
            ) {
                foreach (var (curArr, img) in testInstances) {
                    Image curImg = img;

                    foreach (var (func, argCount) in functions) {
                        SpyFunctionObject spyFunction = null;
                        if (func != null) {
                            var closedReceiver = (randomIndex(4) == 0) ? new ASObject() : null;
                            spyFunction = new SpyFunctionObject((r, args) => func(args), closedReceiver, argCount);
                        }
                        testcases.Add((new ArrayWrapper(curArr), curImg, prototype, spyFunction));
                    }
                }
            }

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(),
                    makeEmptyArrayAndImage(20),
                    makeArrayAndImageWithValues(true),
                    makeArrayAndImageWithValues(false),
                    makeArrayAndImageWithValues(0.0),
                    makeArrayAndImageWithValues("12345"),
                },
                functions: new (Func<ASAny[], ASAny>, int argCount)[] {
                    (null, argCount: 0),
                    (a => true, argCount: 0),
                    (a => false, argCount: 0),
                    (a => ASAny.undefined, argCount: 0),
                    (a => a[0], argCount: 1),
                    (a => a[0], argCount: 2),
                    (a => (bool)a[0], argCount: 3),
                }
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithValues(false, 0, Double.NaN, 0.0, "", 0u, false, Double.NaN, ASAny.@null, ASAny.undefined),
                    makeArrayAndImageWithValues(true, 1, 1.0, Double.PositiveInfinity, "a", 100000u, "bcd"),
                    makeArrayAndImageWithValues(true, 1, 1.0, false, Double.PositiveInfinity, "a", 100000u, "", ASAny.undefined, ASAny.@null, Double.NaN),
                },
                functions: new (Func<ASAny[], ASAny> func, int argCount)[] {
                    (null, argCount: 0),
                    (a => a[0], argCount: 1),
                    (a => (bool)a[0], argCount: 3),
                }
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 19, i => false)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 19, i => true)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 19, i => i == 19)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 19, i => i != 19)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 19, i => i == 0)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 19, i => i != 0)),
                },
                functions: new (Func<ASAny[], ASAny> func, int argCount)[] {
                    (null, argCount: 0),
                    (a => (bool)a[0], argCount: 1),
                    (a => !(bool)a[0], argCount: 1),
                }
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithValues(
                        rangeSelect<ASAny>(0, 49, i => new ConvertibleMockObject(intValue: (int)i, boolValue: true))
                    ),
                    makeArrayAndImageWithValues(
                        rangeSelect<ASAny>(0, 49, i => new ConvertibleMockObject(intValue: 10001 + (int)i, boolValue: false))
                    ),
                    makeArrayAndImageWithValues(
                        rangeSelect<ASAny>(0, 49, i => new ConvertibleMockObject(intValue: 10000, boolValue: i < 30))
                    ),
                    makeArrayAndImageWithValues(
                        rangeSelect<ASAny>(0, 499, i => new ConvertibleMockObject(intValue: 40 * (int)i, boolValue: i % 2 == 0))
                    )
                },
                functions: new (Func<ASAny[], ASAny> func, int argCount)[] {
                    (a => (bool)a[0], argCount: 1),
                    (a => !(bool)a[0], argCount: 1),
                    (a => (int)a[0] < 10000, argCount: 1),
                    (a => (int)a[0] > 10000, argCount: 1),
                    (a => (int)a[1] < 100, argCount: 2),
                    (a => (int)a[1] > 100, argCount: 3),
                }
            );

            var commonFunctions = new (Func<ASAny[], ASAny> func, int argCount)[] {
                (null, argCount: 0),
                (a => (bool)a[0], argCount: 1),
                (a => !(bool)a[0], argCount: 1),
                (a => !a[0].isUndefined, argCount: 3),
                (a => 1, argCount: 1),
            };

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 49, i => mutPushOne(true)),
                        mutSetLength(60)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 49, i => mutPushOne(false)),
                        mutSetLength(60)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 9, i => mutSetUint(i, true)),
                        rangeSelect(100, 109, i => mutSetUint(i, true))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutPush(rangeSelect<ASAny>(0, 49, i => true)),
                        mutDelUint(10),
                        mutDelUint(20),
                        mutDelUint(30)
                    ))
                },
                functions: commonFunctions
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 49, i => mutPushOne(true)),
                        mutSetLength(60)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 49, i => mutPushOne(false)),
                        mutSetLength(60)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 49, i => mutPushOne(false)),
                        mutSetLength(60),
                        rangeSelect(0, 9, i => mutPushOne(false))
                    )),
                },
                functions: commonFunctions,
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, (ASAny)1)))
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 9, i => mutSetUint(i, true)),
                        rangeSelect(100, 109, i => mutSetUint(i, true))
                    ))
                },
                functions: commonFunctions,
                prototype: new IndexDict(rangeSelect(10, 99, i => KeyValuePair.Create(i, (ASAny)false)))
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 9, i => mutSetUint(i, true)),
                        rangeSelect(100, 109, i => mutSetUint(i, true))
                    ))
                },
                functions: commonFunctions,
                prototype: new IndexDict(rangeSelect(10, 99, i => KeyValuePair.Create(i, (ASAny)10001)))
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 9, i => mutSetUint(i, false)),
                        rangeSelect(100, 109, i => mutSetUint(i, false))
                    ))
                },
                prototype: new IndexDict() {
                    [5] = true, [10] = false, [20] = false, [99] = false, [100] = true, [109] = true, [110] = true
                },
                functions: commonFunctions
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutPush(rangeSelect<ASAny>(0, 49, i => true)),
                        mutDelUint(10),
                        mutDelUint(20),
                        mutDelUint(30)
                    ))
                },
                functions: commonFunctions,
                prototype: new IndexDict() {[10] = true, [20] = true, [30] = true}
            );

            addTestCase(
                testInstances: new[] {makeEmptyArrayAndImage(10)},
                functions: commonFunctions,
                prototype: new IndexDict(rangeSelect(0, 9, i => KeyValuePair.Create(i, (ASAny)true)))
            );

            addTestCase(
                testInstances: new[] {makeEmptyArrayAndImage(10)},
                functions: commonFunctions,
                prototype: new IndexDict {[5] = true}
            );

            foreach (var (array, image, prototype, spyFunc) in testcases) {
                yield return new object[] {array, image, spyFunc, null, prototype};
                if (spyFunc != null)
                    yield return new object[] {array, image, spyFunc.clone(), new ASObject(), prototype};
            }
        }

        [Theory]
        [MemberData(nameof(everySomeMethodTest_data))]
        public void everyMethodTest(ArrayWrapper array, Image image, SpyFunctionObject function, ASObject thisObj, IndexDict prototype) {
            setRandomSeed(199328);
            setPrototypeProperties(prototype);

            try {
                if (function != null && function.isMethodClosure && thisObj != null) {
                    AssertHelper.throwsErrorWithCode(
                        ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL, () => array.instance.every(function, thisObj));

                    Assert.Equal(0, function.getCallRecords().Length);
                    verifyArrayMatchesImage(array.instance, image);
                    return;
                }

                bool result = array.instance.every(function, thisObj);

                // Ensure that the array is not mutated
                verifyArrayMatchesImage(array.instance, image);

                if (function == null) {
                    Assert.True(result);
                    return;
                }

                var callRecords = function.getCallRecords();

                var arrayElements = getArrayElementsFromImage(image);
                Assert.True(callRecords.Length <= arrayElements.Length);

                for (int i = 0; i < arrayElements.Length; i++) {
                    ref readonly var call = ref callRecords[i];

                    Assert.Same(function.isMethodClosure ? function.storedReceiver : thisObj, call.receiver);
                    Assert.False(call.isConstruct);

                    var args = call.getArguments();
                    Assert.Equal(Math.Min(function.length, 3), args.Length);

                    if (args.Length >= 1)
                        AssertHelper.identical(arrayElements[i], args[0]);
                    if (args.Length >= 2)
                        AssertHelper.strictEqual(i, args[1]);
                    if (args.Length >= 3)
                        AssertHelper.identical(array.instance, args[2]);

                    if (!(call.returnValue.value is ASBoolean && (bool)call.returnValue)) {
                        Assert.False(result);
                        Assert.Equal(i + 1, callRecords.Length);
                        return;
                    }
                }

                Assert.True(result);
            }
            finally {
                resetPrototypeProperties();
            }
        }

        [Theory]
        [MemberData(nameof(everySomeMethodTest_data))]
        public void someMethodTest(ArrayWrapper array, Image image, SpyFunctionObject function, ASObject thisObj, IndexDict prototype) {
            setRandomSeed(25419);
            setPrototypeProperties(prototype);

            try {
                if (function != null && function.isMethodClosure && thisObj != null) {
                    AssertHelper.throwsErrorWithCode(
                        ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL, () => array.instance.some(function, thisObj));

                    Assert.Equal(0, function.getCallRecords().Length);
                    verifyArrayMatchesImage(array.instance, image);
                    return;
                }

                bool result = array.instance.some(function, thisObj);

                // Ensure that the array is not mutated
                verifyArrayMatchesImage(array.instance, image);

                if (function == null) {
                    Assert.False(result);
                    return;
                }

                var callRecords = function.getCallRecords();

                var arrayElements = getArrayElementsFromImage(image);
                Assert.True(callRecords.Length <= arrayElements.Length);

                for (int i = 0; i < arrayElements.Length; i++) {
                    ref readonly var call = ref callRecords[i];

                    Assert.Same(function.isMethodClosure ? function.storedReceiver : thisObj, call.receiver);
                    Assert.False(call.isConstruct);

                    var args = call.getArguments();
                    Assert.Equal(Math.Min(function.length, 3), args.Length);

                    if (args.Length >= 1)
                        AssertHelper.identical(arrayElements[i], args[0]);
                    if (args.Length >= 2)
                        AssertHelper.strictEqual(i, args[1]);
                    if (args.Length >= 3)
                        AssertHelper.identical(array.instance, args[2]);

                    if (call.returnValue.value is ASBoolean && (bool)call.returnValue) {
                        Assert.True(result);
                        Assert.Equal(i + 1, callRecords.Length);
                        return;
                    }
                }

                Assert.False(result);
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> forEachMethodTest_data() {
            setRandomSeed(4995);

            ASAny[] valueDomain = makeUniqueValues(50);
            valueDomain[6] = ASAny.undefined;
            valueDomain[14] = ASAny.undefined;
            valueDomain[28] = ASAny.undefined;
            valueDomain[32] = ASAny.@null;
            valueDomain[41] = ASAny.@null;

            var testcases = new List<(ArrayWrapper, Image, IndexDict, SpyFunctionObject)>();

            void addTestCase(
                (ASArray arr, Image img)[] initialStates,
                (Func<ASAny[], ASAny> func, int argCount)[] functions,
                IEnumerable<Mutation> mutations = null,
                IndexDict prototype = null
            ) {
                foreach (var (curArr, img) in initialStates) {
                    Image curImg = img;

                    applyMutations(curArr, mutations);
                    applyMutations(ref curImg, mutations);

                    foreach (var (func, argCount) in functions) {
                        SpyFunctionObject spyFunction = null;
                        if (func != null) {
                            var closedReceiver = (randomIndex(4) == 0) ? new ASObject() : null;
                            spyFunction = new SpyFunctionObject((r, args) => func(args), closedReceiver, argCount);
                        }
                        testcases.Add((new ArrayWrapper(curArr), curImg, prototype, spyFunction));
                    }
                }
            }

            var commonFunctions = new (Func<ASAny[], ASAny> func, int argCount)[] {
                (null, argCount: 0),
                (args => ASAny.undefined, argCount: 0),
                (args => true, argCount: 1),
                (args => false, argCount: 2),
                (args => args[0], argCount: 3),
            };

            addTestCase(
                initialStates: new[] {
                    makeEmptyArrayAndImage(),
                    makeEmptyArrayAndImage(50),
                    makeRandomDenseArrayAndImage(50, valueDomain),
                },
                functions: commonFunctions
            );

            addTestCase(
                initialStates: new[] {makeRandomDenseArrayAndImage(50, valueDomain)},
                mutations: new[] {mutSetLength(120)},
                functions: commonFunctions
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                mutations: rangeSelect(0, 499, i => mutSetUint(i, randomSample(valueDomain))),
                functions: commonFunctions
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                mutations: rangeSelect(0, 99, i => mutSetUint(i * 3, randomSample(valueDomain))),
                functions: commonFunctions
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(0, 9, i => mutDelUint(i))
                ),
                functions: commonFunctions
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(0, 59, i => mutDelUint(i))
                ),
                functions: commonFunctions
            );

            addTestCase(
                initialStates: new[] {makeRandomDenseArrayAndImage(50, valueDomain)},
                prototype: makeIndexDict(20, 40, 50, 60, 65, 70),
                functions: commonFunctions
            );

            addTestCase(
                initialStates: new[] {makeRandomDenseArrayAndImage(50, valueDomain)},
                mutations: new[] {mutSetLength(60)},
                prototype: makeIndexDict(20, 40, 50, 60, 65, 70),
                functions: commonFunctions
            );

            addTestCase(
                initialStates: new[] {makeRandomDenseArrayAndImage(50, valueDomain)},
                mutations: new[] {mutSetLength(120), mutPushOne(ASAny.undefined)},
                prototype: makeIndexDict(20, 40, 50, 60, 65, 70, 100, 119, 120, 121, 10000),
                functions: commonFunctions
            );

            foreach (var (array, image, prototype, spyFunc) in testcases) {
                yield return new object[] {array, image, spyFunc, null, prototype};
                if (spyFunc != null)
                    yield return new object[] {array, image, spyFunc.clone(), new ASObject(), prototype};
            }
        }

        [Theory]
        [MemberData(nameof(forEachMethodTest_data))]
        public void forEachMethodTest(ArrayWrapper array, Image image, SpyFunctionObject function, ASObject thisObj, IndexDict prototype) {
            setRandomSeed(247388);
            setPrototypeProperties(prototype);

            try {
                if (function != null && function.isMethodClosure && thisObj != null) {
                    AssertHelper.throwsErrorWithCode(
                        ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL, () => array.instance.forEach(function, thisObj));

                    Assert.Equal(0, function.getCallRecords().Length);
                    verifyArrayMatchesImage(array.instance, image);
                    return;
                }

                array.instance.forEach(function, thisObj);

                // Ensure that the array is not mutated
                verifyArrayMatchesImage(array.instance, image);

                if (function == null)
                    return;

                var callRecords = function.getCallRecords();

                var arrayElements = getArrayElementsFromImage(image);
                Assert.True(callRecords.Length == arrayElements.Length);

                for (int i = 0; i < arrayElements.Length; i++) {
                    ref readonly var call = ref callRecords[i];

                    Assert.Same(function.isMethodClosure ? function.storedReceiver : thisObj, call.receiver);
                    Assert.False(call.isConstruct);

                    var args = call.getArguments();
                    Assert.Equal(Math.Min(function.length, 3), args.Length);

                    if (args.Length >= 1)
                        AssertHelper.identical(arrayElements[i], args[0]);
                    if (args.Length >= 2)
                        AssertHelper.strictEqual(i, args[1]);
                    if (args.Length >= 3)
                        AssertHelper.identical(array.instance, args[2]);
                }
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> filterMethodTest_data() {
            setRandomSeed(71192);

            var testcases = new List<(ArrayWrapper, Image, IndexDict, SpyFunctionObject)>();

            void addTestCase(
                (ASArray arr, Image img)[] initialStates,
                (Func<ASAny[], ASAny> func, int argCount)[] functions,
                IEnumerable<Mutation> mutations = null,
                IndexDict prototype = null
            ) {
                foreach (var (curArr, img) in initialStates) {
                    Image curImg = img;

                    applyMutations(curArr, mutations);
                    applyMutations(ref curImg, mutations);

                    foreach (var (func, argCount) in functions) {
                        SpyFunctionObject spyFunction = null;
                        if (func != null) {
                            var closedReceiver = (randomIndex(4) == 0) ? new ASObject() : null;
                            spyFunction = new SpyFunctionObject((r, args) => func(args), closedReceiver, argCount);
                        }
                        testcases.Add((new ArrayWrapper(curArr), curImg, prototype, spyFunction));
                    }
                }
            }

            addTestCase(
                initialStates: new[] {
                    makeEmptyArrayAndImage(),
                    makeEmptyArrayAndImage(20),
                    makeArrayAndImageWithValues(true),
                    makeArrayAndImageWithValues(false),
                    makeArrayAndImageWithValues(0.0),
                    makeArrayAndImageWithValues("12345"),
                },
                functions: new (Func<ASAny[], ASAny>, int argCount)[] {
                    (null, argCount: 0),
                    (a => true, argCount: 0),
                    (a => false, argCount: 0),
                    (a => ASAny.undefined, argCount: 0),
                    (a => a[0], argCount: 1),
                    (a => a[0], argCount: 2),
                    (a => (bool)a[0], argCount: 3),
                }
            );

            addTestCase(
                initialStates: new[] {
                    makeArrayAndImageWithValues(false, 0, Double.NaN, 0.0, "", 0u, false, Double.NaN, ASAny.@null, ASAny.undefined),
                    makeArrayAndImageWithValues(true, 1, 1.0, Double.PositiveInfinity, "a", 100000u, "bcd"),
                    makeArrayAndImageWithValues(true, 1, 1.0, false, Double.PositiveInfinity, "a", 100000u, "", ASAny.undefined, ASAny.@null, Double.NaN),
                },
                functions: new (Func<ASAny[], ASAny> func, int argCount)[] {
                    (null, argCount: 0),
                    (a => a[0], argCount: 1),
                    (a => !(bool)a[0], argCount: 1),
                    (a => ASObject.AS_isNumeric(a[0].value), argCount: 3),
                }
            );

            addTestCase(
                initialStates: new[] {
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 99, i => false)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 99, i => true)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 99, i => i >= 30 && i < 60)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 99, i => randomIndex(2) == 0)),
                },
                functions: new (Func<ASAny[], ASAny> func, int argCount)[] {
                    (null, argCount: 0),
                    (a => (bool)a[0], argCount: 1),
                    (a => !(bool)a[0], argCount: 1),
                    (a => (int)a[1] % 3 == 1, argCount: 2),
                }
            );

            addTestCase(
                initialStates: new[] {
                    makeArrayAndImageWithValues(
                        rangeSelect<ASAny>(0, 99, i => new ConvertibleMockObject(intValue: (int)i, boolValue: true))
                    ),
                    makeArrayAndImageWithValues(
                        rangeSelect<ASAny>(0, 99, i => new ConvertibleMockObject(intValue: 10001 + (int)i, boolValue: false))
                    ),
                    makeArrayAndImageWithValues(
                        rangeSelect<ASAny>(0, 99, i => new ConvertibleMockObject(intValue: 10000, boolValue: i < 50))
                    ),
                    makeArrayAndImageWithValues(
                        rangeSelect<ASAny>(0, 499, i => new ConvertibleMockObject(intValue: (int)randomIndex(30000), boolValue: randomIndex(2) == 0))
                    )
                },
                functions: new (Func<ASAny[], ASAny> func, int argCount)[] {
                    (a => (bool)a[0], argCount: 1),
                    (a => !(bool)a[0], argCount: 1),
                    (a => (int)a[0] < 10000, argCount: 1),
                    (a => (int)a[0] > 10000, argCount: 1),
                    (a => (int)a[1] < 100, argCount: 2),
                    (a => (int)a[1] > 100, argCount: 3),
                }
            );

            var commonFunctions = new (Func<ASAny[], ASAny> func, int argCount)[] {
                (null, argCount: 0),
                (a => (bool)a[0], argCount: 1),
                (a => !(bool)a[0], argCount: 1),
                (a => !a[0].isUndefined, argCount: 3),
                (a => 1, argCount: 1),
            };

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                mutations: buildMutationList(
                    rangeSelect(0, 49, i => mutPushOne(true)),
                    mutSetLength(60)
                )
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                mutations: buildMutationList(
                    rangeSelect(0, 49, i => mutPushOne(false)),
                    mutSetLength(60)
                )
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, (ASAny)1))),
                mutations: buildMutationList(
                    rangeSelect(0, 49, i => mutPushOne(true)),
                    mutSetLength(60)
                )
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, (ASAny)1))),
                mutations: buildMutationList(
                    rangeSelect(0, 49, i => mutPushOne(false)),
                    mutSetLength(60),
                    rangeSelect(0, 9, i => mutPushOne(false))
                )
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                mutations: buildMutationList(
                    rangeSelect(0, 49, i => mutPushOne(false)),
                    mutSetLength(60)
                )
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                mutations: buildMutationList(
                    rangeSelect(0, 9, i => mutSetUint(i, true)),
                    rangeSelect(100, 109, i => mutSetUint(i, true))
                )
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                prototype: new IndexDict(rangeSelect(10, 99, i => KeyValuePair.Create(i, (ASAny)10000))),
                mutations: buildMutationList(
                    rangeSelect(0, 9, i => mutSetUint(i, true)),
                    rangeSelect(100, 109, i => mutSetUint(i, true))
                )
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                prototype: new IndexDict(rangeSelect(15, 95, i => KeyValuePair.Create(i, (ASAny)randomIndex(2)))),
                mutations: buildMutationList(
                    rangeSelect(0, 9, i => mutSetUint(i, true)),
                    rangeSelect(100, 109, i => mutSetUint(i, true))
                )
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                prototype: new IndexDict() {[5] = true, [10] = false, [20] = false, [99] = false, [100] = true, [109] = true, [110] = true},
                mutations: buildMutationList(
                    rangeSelect(0, 9, i => mutSetUint(i, false)),
                    rangeSelect(100, 109, i => mutSetUint(i, false))
                )
            );

            addTestCase(
                initialStates: new[] {makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 49, i => true))},
                functions: commonFunctions,
                mutations: new[] {mutDelUint(10), mutDelUint(20), mutDelUint(30)}
            );

            addTestCase(
                initialStates: new[] {makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 49, i => true))},
                functions: commonFunctions,
                mutations: new[] {mutDelUint(10), mutDelUint(20), mutDelUint(30)},
                prototype: new IndexDict() {[10] = true, [20] = true, [30] = true}
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage(10)},
                functions: commonFunctions,
                prototype: new IndexDict(rangeSelect(0, 9, i => KeyValuePair.Create(i, (ASAny)true)))
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage(10)},
                functions: commonFunctions,
                prototype: new IndexDict {[5] = true}
            );

            foreach (var (array, image, prototype, spyFunc) in testcases) {
                yield return new object[] {array, image, spyFunc, null, prototype};
                if (spyFunc != null)
                    yield return new object[] {array, image, spyFunc.clone(), new ASObject(), prototype};
            }
        }

        [Theory]
        [MemberData(nameof(filterMethodTest_data))]
        public void filterMethodTest(ArrayWrapper array, Image image, SpyFunctionObject function, ASObject thisObj, IndexDict prototype) {
            setRandomSeed(301609);
            setPrototypeProperties(prototype);

            try {
                if (function != null && function.isMethodClosure && thisObj != null) {
                    AssertHelper.throwsErrorWithCode(
                        ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL, () => array.instance.filter(function, thisObj));

                    Assert.Equal(0, function.getCallRecords().Length);
                    verifyArrayMatchesImage(array.instance, image);
                    return;
                }

                ASArray result = array.instance.filter(function, thisObj);
                Assert.NotSame(array.instance, result);

                if (function == null) {
                    Assert.Equal(0u, result.length);
                    return;
                }

                var arrayElements = getArrayElementsFromImage(image);
                var callRecords = function.getCallRecords();

                Assert.True(callRecords.Length <= arrayElements.Length);
                Assert.True(result.length <= (uint)arrayElements.Length);

                // Ensure that the array is not mutated
                verifyArrayMatchesImage(array.instance, image);

                // Fill up the prototype with a dummy object to detect holes in the returned array.
                ASAny hole = new ASObject();
                setPrototypeProperties(new IndexDict(rangeSelect(1, result.length, i => KeyValuePair.Create(i, hole))));

                uint resultIndex = 0;

                for (int i = 0; i < arrayElements.Length; i++) {
                    ref readonly var call = ref callRecords[i];

                    Assert.Same(function.isMethodClosure ? function.storedReceiver : thisObj, call.receiver);
                    Assert.False(call.isConstruct);

                    var args = call.getArguments();
                    Assert.Equal(Math.Min(function.length, 3), args.Length);

                    if (args.Length >= 1)
                        AssertHelper.identical(arrayElements[i], args[0]);
                    if (args.Length >= 2)
                        AssertHelper.strictEqual(i, args[1]);
                    if (args.Length >= 3)
                        AssertHelper.identical(array.instance, args[2]);

                    if (call.returnValue.value is ASBoolean && (bool)call.returnValue) {
                        Assert.True(resultIndex < result.length);
                        Assert.True(result.AS_hasElement(resultIndex));
                        AssertHelper.identical(result.AS_getElement(resultIndex), arrayElements[i]);
                        resultIndex++;
                    }
                }

                Assert.Equal(resultIndex, result.length);
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> mapMethodTest_data() {
            setRandomSeed(54831900);

            var testcases = new List<(ArrayWrapper, Image, IndexDict, SpyFunctionObject)>();

            void addTestCase(
                (ASArray arr, Image img)[] initialStates,
                (Func<ASAny[], ASAny> func, int argCount)[] functions,
                IEnumerable<Mutation> mutations = null,
                IndexDict prototype = null
            ) {
                foreach (var (curArr, img) in initialStates) {
                    Image curImg = img;

                    applyMutations(curArr, mutations);
                    applyMutations(ref curImg, mutations);

                    foreach (var (func, argCount) in functions) {
                        SpyFunctionObject spyFunction = null;
                        if (func != null) {
                            var closedReceiver = (randomIndex(4) == 0) ? new ASObject() : null;
                            spyFunction = new SpyFunctionObject((r, args) => func(args), closedReceiver, argCount);
                        }
                        testcases.Add((new ArrayWrapper(curArr), curImg, prototype, spyFunction));
                    }
                }
            }

            addTestCase(
                initialStates: new[] {
                    makeEmptyArrayAndImage(),
                    makeEmptyArrayAndImage(20),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 49, i => new ASObject()))
                },
                functions: new (Func<ASAny[], ASAny> func, int argCount)[] {
                    (null, argCount: 0),
                    (a => new ASObject(), argCount: 0),
                    (a => a[0], argCount: 1),
                    (a => a[1], argCount: 2),
                    (a => a[2], argCount: 3),
                    (a => ASAny.undefined, argCount: 3),
                }
            );

            addTestCase(
                initialStates: new[] {
                    makeArrayAndImageWithValues(12, 1, 1.0, 15.6, -45, 193u, UInt32.MaxValue, Double.NaN, "1234", "0x47ff", "", "145.663"),
                    makeArrayAndImageWithValues(ASAny.undefined, ASAny.@null, true, false, 1, 0, 1u, 1.0, -1.0, Double.PositiveInfinity, 7.9964e+43, "hello"),
                },
                functions: new (Func<ASAny[], ASAny> func, int argCount)[] {
                    (null, argCount: 0),
                    (a => a[0], argCount: 1),
                    (a => (bool)a[0], argCount: 1),
                    (a => (int)a[0], argCount: 1),
                    (a => (int)a[0] + (int)a[1], argCount: 2),
                    (a => (double)a[0], argCount: 1),
                    (a => (string)a[0], argCount: 1),
                    (a => (ASObject)a[0], argCount: 1),
                }
            );

            addTestCase(
                initialStates: new[] {
                    makeArrayAndImageWithValues(
                        rangeSelect<ASAny>(0, 499, i => new ConvertibleMockObject(intValue: (int)randomIndex(30000), boolValue: randomIndex(2) == 0))
                    ),
                    makeArrayAndImageWithValues(
                        rangeSelect<ASAny>(0, 499, i => new ConvertibleMockObject(intValue: (int)randomIndex(30000), boolValue: randomIndex(2) == 0))
                    )
                },
                functions: new (Func<ASAny[], ASAny> func, int argCount)[] {
                    (a => (bool)a[0], argCount: 1),
                    (a => (int)a[0], argCount: 1),
                }
            );

            var commonFunctions = new (Func<ASAny[], ASAny> func, int argCount)[] {
                (null, argCount: 0),
                (a => a[0], argCount: 1),
                (a => a[1], argCount: 3),
                (a => (bool)a[0], argCount: 1),
                (a => (int)a[0], argCount: 1),
            };

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                mutations: buildMutationList(
                    rangeSelect(0, 49, i => mutPushOne(i)),
                    mutSetLength(60)
                )
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, (ASAny)(i - 50)))),
                mutations: buildMutationList(
                    rangeSelect(0, 49, i => mutPushOne(i)),
                    mutSetLength(60)
                )
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, (ASAny)i))),
                mutations: buildMutationList(
                    rangeSelect(0, 49, i => mutPushOne(0)),
                    mutSetLength(60),
                    rangeSelect(0, 9, i => mutPushOne(0))
                )
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                mutations: buildMutationList(
                    rangeSelect(0, 9, i => mutSetUint(i, randomIndex())),
                    rangeSelect(100, 109, i => mutSetUint(i, randomIndex()))
                )
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                prototype: new IndexDict(rangeSelect(10, 99, i => KeyValuePair.Create(i, (ASAny)randomIndex()))),
                mutations: buildMutationList(
                    rangeSelect(0, 9, i => mutSetUint(i, "11111")),
                    rangeSelect(100, 109, i => mutSetUint(i, "hello"))
                )
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                prototype: new IndexDict(rangeSelect(15, 95, i => KeyValuePair.Create(i, (ASAny)randomIndex()))),
                mutations: buildMutationList(
                    rangeSelect(0, 9, i => mutSetUint(i, "1384933")),
                    rangeSelect(100, 109, i => mutSetUint(i, "0x88abc"))
                )
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage()},
                functions: commonFunctions,
                prototype: new IndexDict() {[5] = true, [10] = false, [20] = false, [99] = false, [100] = true, [109] = true, [110] = true},
                mutations: buildMutationList(
                    rangeSelect(0, 9, i => mutSetUint(i, false)),
                    rangeSelect(100, 109, i => mutSetUint(i, false))
                )
            );

            addTestCase(
                initialStates: new[] {makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 49, i => randomIndex()))},
                functions: commonFunctions,
                mutations: new[] {mutDelUint(10), mutDelUint(20), mutDelUint(30)}
            );

            addTestCase(
                initialStates: new[] {makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 49, i => randomIndex()))},
                functions: commonFunctions,
                mutations: new[] {mutDelUint(10), mutDelUint(20), mutDelUint(30)},
                prototype: new IndexDict() {[10] = 1, [20] = 2, [30] = 3}
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage(10)},
                functions: commonFunctions,
                prototype: new IndexDict(rangeSelect(0, 9, i => KeyValuePair.Create(i, (ASAny)randomIndex())))
            );

            addTestCase(
                initialStates: new[] {makeEmptyArrayAndImage(10)},
                functions: commonFunctions,
                prototype: new IndexDict {[5] = false}
            );

            foreach (var (array, image, prototype, spyFunc) in testcases) {
                yield return new object[] {array, image, spyFunc, null, prototype};
                if (spyFunc != null)
                    yield return new object[] {array, image, spyFunc.clone(), new ASObject(), prototype};
            }
        }

        [Theory]
        [MemberData(nameof(mapMethodTest_data))]
        public void mapMethodTest(ArrayWrapper array, Image image, SpyFunctionObject function, ASObject thisObj, IndexDict prototype) {
            setRandomSeed(644397);
            setPrototypeProperties(prototype);

            try {
                if (function != null && function.isMethodClosure && thisObj != null) {
                    AssertHelper.throwsErrorWithCode(
                        ErrorCode.CALLBACK_METHOD_THIS_NOT_NULL, () => array.instance.map(function, thisObj));

                    Assert.Equal(0, function.getCallRecords().Length);
                    verifyArrayMatchesImage(array.instance, image);
                    return;
                }

                ASArray result = array.instance.map(function, thisObj);
                Assert.NotSame(array.instance, result);

                if (function == null) {
                    Assert.Equal(0u, result.length);
                    return;
                }

                var arrayElements = getArrayElementsFromImage(image);
                var callRecords = function.getCallRecords();

                Assert.True(callRecords.Length == arrayElements.Length);
                Assert.Equal(result.length, (uint)arrayElements.Length);

                // Ensure that the array is not mutated
                verifyArrayMatchesImage(array.instance, image);

                // Fill up the prototype with a dummy object to detect holes in the returned array.
                ASAny hole = new ASObject();
                setPrototypeProperties(new IndexDict(rangeSelect(1, result.length, i => KeyValuePair.Create(i, hole))));

                for (int i = 0; i < arrayElements.Length; i++) {
                    ref readonly var call = ref callRecords[i];

                    Assert.Same(function.isMethodClosure ? function.storedReceiver : thisObj, call.receiver);
                    Assert.False(call.isConstruct);

                    var args = call.getArguments();
                    Assert.Equal(Math.Min(function.length, 3), args.Length);

                    if (args.Length >= 1)
                        AssertHelper.identical(arrayElements[i], args[0]);
                    if (args.Length >= 2)
                        AssertHelper.strictEqual(i, args[1]);
                    if (args.Length >= 3)
                        AssertHelper.identical(array.instance, args[2]);

                    Assert.True(result.AS_hasElement(i));
                    AssertHelper.identical(result.AS_getElement(i), call.returnValue);
                }
            }
            finally {
                resetPrototypeProperties();
            }
        }

        private static void verifySlice(ASArray slice, Image originalImage, uint startIndex, uint endIndex) {
            if (endIndex <= startIndex) {
                Assert.Equal(0u, slice.length);
                return;
            }

            Assert.Equal(endIndex - startIndex, slice.length);

            for (uint i = startIndex; i < endIndex; i++) {
                Assert.True(slice.AS_hasElement(i - startIndex));
                ASAny expectedValue;

                if (originalImage.elements.TryGetValue(i, out expectedValue)
                    || (s_currentProtoProperties != null && s_currentProtoProperties.TryGetValue(i, out expectedValue)))
                {
                    AssertHelper.identical(expectedValue, slice.AS_getElement(i - startIndex));
                }
                else {
                    AssertHelper.identical(ASAny.undefined, slice.AS_getElement(i - startIndex));
                }
            }
        }

        public static IEnumerable<object[]> sliceMethodTest_data() {
            setRandomSeed(499939);

            ASAny[] valueDomain = makeUniqueValues(50);
            valueDomain[6] = ASAny.undefined;
            valueDomain[14] = ASAny.undefined;
            valueDomain[28] = ASAny.undefined;
            valueDomain[32] = ASAny.@null;
            valueDomain[41] = ASAny.@null;

            const uint maxLength = UInt32.MaxValue;

            var testcases = new List<(ArrayWrapper, Image, (uint, uint)[], IndexDict)>();

            void addTestCase(
                (ASArray arr, Image img) initialState,
                IEnumerable<Mutation> mutations = null,
                (uint, uint)[] ranges = null,
                IndexDict prototype = null
            ) {
                var (arr, img) = initialState;
                applyMutations(arr, mutations);
                applyMutations(ref img, mutations);

                testcases.Add((new ArrayWrapper(arr), img, ranges, prototype));
            }

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                ranges: new (uint, uint)[] {(0, 0)}
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(20),
                ranges: new (uint, uint)[] {
                    (0, 0), (10, 10),
                    (19, 19), (20, 20),
                    (0, 5), (0, 10), (0, 19), (0, 20),
                    (10, 10), (10, 15), (10, 19), (10, 20),
                    (19, 20), (20, 20)
                }
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(20, valueDomain),
                ranges: new (uint, uint)[] {
                    (0, 0), (10, 10), (19, 19), (20, 20),
                    (0, 5), (0, 10), (0, 19), (0, 20),
                    (10, 10), (10, 15), (10, 19), (10, 20),
                    (19, 20), (20, 20)
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 49, i => mutSetUint(i, randomSample(valueDomain))),
                ranges: new (uint, uint)[] {(0, 0), (20, 20), (50, 50), (0, 50), (20, 40), (20, 50), (40, 45), (40, 50)}
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(0, 19, i => mutUnshift(randomSample(valueDomain))),
                    mutPush(rangeSelect<ASAny>(0, 19, i => randomSample(valueDomain))),
                    mutPush(rangeSelect<ASAny>(0, 19, i => randomSample(valueDomain))),
                    mutDelUint(3),
                    mutDelUint(13),
                    mutDelUint(45),
                    mutDelUint(46),
                    mutDelUint(78),
                    mutDelUint(79)
                ),
                ranges: new (uint, uint)[] {(0, 80), (0, 78), (70, 79), (70, 80), (78, 80)}
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(30, valueDomain),
                mutations: buildMutationList(
                    rangeSelect(29, 20, i => mutDelUint(i)),
                    mutSetLength(35)
                ),
                ranges: new (uint, uint)[] {
                    (0, 0), (0, 15), (0, 20), (0, 25), (0, 30), (0, 32), (0, 35),
                    (10, 10), (10, 15), (10, 20), (10, 25), (10, 30), (10, 32), (10, 35),
                    (20, 20), (20, 25), (20, 30), (20, 32), (20, 35),
                    (25, 25), (25, 28), (25, 30), (25, 32), (25, 35),
                    (30, 30), (30, 32), (30, 35),
                    (33, 33), (33, 35),
                    (35, 35)
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: rangeSelect(0, 19, i => mutSetUint(i, randomSample(valueDomain))),
                ranges: new (uint, uint)[] {
                    (0, 0), (10, 10), (19, 19), (20, 20),
                    (0, 5), (0, 10), (0, 20),
                    (5, 10), (5, 20),
                    (19, 20),
                    (0, 21), (20, 21), (21, 21), (21, 22),
                    (5, 21), (5, 25), (5, 100),
                    (maxLength / 2, maxLength / 2 + 30),
                    (maxLength - 30, maxLength),
                    (maxLength, maxLength),
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(1, 200, i => mutSetUint(randomIndex(1000), randomSample(valueDomain))),
                    mutSetLength(1000)
                ),
                ranges: new (uint, uint)[] {
                    (0, 0), (200, 0), (1000, 0),
                    (0, 100), (500, 600), (900, 1000),
                    (0, 300), (300, 600), (600, 900),
                    (0, 500), (500, 1000),
                    (0, 1000),
                    (999, 1000),
                    (1000, 1000),
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(0, 99, i => mutSetUint(i + 2147483630, randomSample(valueDomain))),
                    rangeSelect(0, 99, i => mutSetUint(i + 3473100483, randomSample(valueDomain))),
                    rangeSelect(0, 99, i => mutSetUint(maxLength - i - 1, randomSample(valueDomain)))
                ),
                ranges: new (uint, uint)[] {
                    (0, 100), (0, 200), (50, 150),
                    (100, 200), (1000, 1100),
                    (2147483620u, 2147483620u + 100), (2147483620u, 2147483620u + 110), (2147483620u, 2147483620u + 120),
                    (2147483630u, 2147483630u + 100), (2147483630u, 2147483630u + 120),
                    (2147483640u, 2147483640u + 80), (2147483640u, 2147483640u + 90), (2147483640u, 2147483640u + 100),
                    (2147483660u, 2147483660u + 60), (2147483660u, 2147483660u + 70), (2147483660u, 2147483660u + 80),
                    (2147483729u, 2147483729u + 1), (2147483729u, 2147483729u + 2),
                    (2147483730u, 2147483730u + 10), (2147483740u, 2147483740u + 10),
                    (3473100473u, 3473100473u + 100), (3473100473u, 3473100473u + 110), (3473100473u, 3473100473u + 120),
                    (3473100483u, 3473100483u + 100), (3473100483u, 3473100483u + 120),
                    (3473100493u, 3473100493u + 80), (3473100493u, 3473100493u + 90), (3473100493u, 3473100493u + 100),
                    (3473100513u, 3473100513u + 60), (3473100513u, 3473100513u + 70), (3473100513u, 3473100513u + 80),
                    (3473100582u, 3473100582u + 1), (2147483582u, 2147483582u + 2),
                    (3473100583u, 3473100583u + 10), (3473100593u, 3473100593u + 10),
                    (maxLength - 200, 100), (maxLength - 200, 150), (maxLength - 200, 200),
                    (maxLength - 100, 50), (maxLength - 100, 100),
                    (maxLength - 1, 1), (maxLength, 0),
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, randomSample(valueDomain)))),
                ranges: new (uint, uint)[] {(0, 0)}
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(100),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, randomSample(valueDomain)))),
                ranges: new (uint, uint)[] {(0, 100), (40, 70), (50, 60)}
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, randomSample(valueDomain)))),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                ranges: new (uint, uint)[] {(0, 100), (40, 70), (50, 60)}
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, randomSample(valueDomain)))),
                mutations: buildMutationList(
                    rangeSelect(0, 52, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(57, 99, i => mutSetUint(i, randomSample(valueDomain)))
                ),
                ranges: new (uint, uint)[] {(0, 100), (40, 70), (50, 60), (53, 57)}
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                prototype: new IndexDict(rangeSelect(50, 59, i => KeyValuePair.Create(i, randomSample(valueDomain)))),
                mutations: buildMutationList(
                    rangeSelect(0, 10, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(90, 99, i => mutSetUint(i, randomSample(valueDomain)))
                ),
                ranges: new (uint, uint)[] {(0, 100), (5, 95), (10, 90), (50, 60)}
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(sliceMethodTest_data))]
        public void sliceMethodTest(ArrayWrapper array, Image image, (uint, uint)[] ranges, IndexDict prototype) {
            setRandomSeed(4758118);
            setPrototypeProperties(prototype);

            try {
                foreach (var (start, end) in ranges) {
                    foreach (var (s, e) in generateEquivalentStartEndRanges(start, end, array.instance.length)) {
                        ASArray slice = array.instance.slice(s, e);
                        verifySlice(slice, image, start, end);
                    }
                }
                verifyArrayMatchesImage(array.instance, image);
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> indexOf_lastIndexOf_testData() {
            setRandomSeed(665144);

            var testcases = new List<(ArrayWrapper, Image, (ASAny, uint)[], IndexDict)>();

            void addTestCase(
                (ASArray arr, Image img) initialState,
                IEnumerable<Mutation> mutations = null,
                IEnumerable<(uint, uint[])> indexQueries = null,
                IEnumerable<(ASAny, uint[])> objectQueries = null,
                IndexDict prototype = null
            ) {
                var (arr, img) = initialState;
                applyMutations(arr, mutations);
                applyMutations(ref img, mutations);

                var queries = new List<(ASAny, uint)>();

                if (indexQueries != null) {
                    foreach (var (targetIndex, startIndices) in indexQueries) {
                        ASAny value;
                        if (!img.elements.TryGetValue(targetIndex, out value)
                            && (prototype == null || !prototype.TryGetValue(targetIndex, out value)))
                        {
                            value = ASAny.undefined;
                        }

                        foreach (uint startIndex in startIndices.Distinct())
                            queries.Add((value, startIndex));
                    }
                }

                if (objectQueries != null) {
                    foreach (var (target, startIndices) in objectQueries) {
                        foreach (uint startIndex in startIndices.Distinct())
                            queries.Add((target, startIndex));
                    }
                }

                testcases.Add((new ArrayWrapper(arr), img, queries.ToArray(), prototype));
            }

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                objectQueries: new (ASAny, uint[])[] {
                    (ASAny.undefined, new uint[] {0}),
                    (ASAny.@null, new uint[] {0}),
                    (0, new uint[] {0}),
                    (0u, new uint[] {0}),
                    (0.0, new uint[] {0}),
                    ("", new uint[] {0}),
                    (true, new uint[] {0}),
                    (new ASObject(), new uint[] {0}),
                    (new ASArrayTest_ClassA(), new uint[] {0})
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(20),
                objectQueries: new (ASAny, uint[])[] {
                    (ASAny.undefined, new uint[] {0, 10, 20}),
                    (ASAny.@null, new uint[] {0, 10, 20}),
                    (0, new uint[] {0, 10, 20}),
                    (0u, new uint[] {0, 10, 20}),
                    (0.0, new uint[] {0, 10, 20}),
                    ("", new uint[] {0, 10, 20}),
                    (true, new uint[] {0, 10, 20}),
                    (new ASObject(), new uint[] {0, 10, 20}),
                }
            );

            addTestCase(
                initialState: makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 49, i => new ASObject())),
                indexQueries: rangeSelect(0, 49, i => (i, new uint[] {0, (i > 0) ? i - 1 : 0, i, i + 1, 49, 50})),
                objectQueries: new (ASAny, uint[])[] {
                    (new ASObject(), new uint[] {0, 20, 49, 50}),
                    (ASAny.undefined, new uint[] {0, 20, 49, 50}),
                    (ASAny.@null, new uint[] {0, 20, 49, 50}),
                    ("", new uint[] {0, 20, 49, 50}),
                    (false, new uint[] {0, 20, 49, 50}),
                }
            );

            addTestCase(
                initialState: makeArrayAndImageWithValues(
                    rangeSelect<ASAny>(0, 499, i => (randomIndex(10) == 0) ? ASAny.undefined : new ASObject())
                ),
                indexQueries: rangeSelect(
                    0, 99, i => (randomIndex(500), rangeSelect(0, 14, j => randomIndex(500)))
                ),
                objectQueries: new (ASAny, uint[])[] {
                    (new ASObject(), rangeSelect(0, 4, j => randomIndex(500))),
                    (ASAny.undefined, rangeSelect(0, 4, j => randomIndex(500))),
                    (ASAny.@null, rangeSelect(0, 4, j => randomIndex(500))),
                    (0, rangeSelect(0, 4, j => randomIndex(500))),
                    (true, rangeSelect(0, 4, j => randomIndex(500))),
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutPushOne(new ASObject())),
                    rangeSelect(0, 19, i => mutUnshift(new ASObject())),
                    mutPush(rangeSelect<ASAny>(0, 19, i => new ASObject())),
                    mutUnshift(rangeSelect<ASAny>(0, 19, i => new ASObject())),
                    mutDelUint(3),
                    mutSetUint(8, ASAny.@null),
                    mutDelUint(13),
                    mutSetUint(45, ASAny.@null),
                    mutDelUint(46),
                    mutDelUint(78),
                    mutSetUint(79, ASAny.@null),
                    mutSetLength(90)
                ),
                indexQueries: new (uint, uint[])[] {
                    (0, new uint[] {0, 1, 78, 80, 90}),
                    (26, new uint[] {0, 1, 25, 26, 27, 78, 80, 90}),
                    (44, new uint[] {0, 1, 43, 44, 45, 78, 80, 90}),
                },
                objectQueries: new (ASAny, uint[])[] {
                    (ASAny.undefined, new uint[] {0, 3, 8, 10, 13, 35, 45, 46, 47, 63, 77, 78, 79, 80, 89, 90}),
                    (ASAny.@null, new uint[] {0, 3, 8, 10, 13, 35, 45, 46, 47, 63, 77, 78, 79, 80, 89, 90}),
                    (false, new uint[] {0, 3, 8, 10, 13, 35, 45, 46, 47, 63, 77, 78, 79, 80, 89, 90}),
                }
            );

            addTestCase(
                initialState: makeArrayAndImageWithValues(
                    0, 0u, 0.0, 100, 200.0, 0.3, 0.1 + 0.2, 200u, Double.PositiveInfinity, Double.NegativeInfinity,
                    Int32.MinValue, Int32.MaxValue, 0u, 100, -20, -20.0, Double.NaN, (double)Int32.MaxValue, 200, 0.3,
                    50000.0, -50000.0, 0, 200, 200.1, 50000, -20, -1, UInt32.MaxValue, -50000.1
                ),
                objectQueries: new (ASAny, uint[])[] {
                    (0, new uint[] {0, 1, 2, 10, 12, 15, 22, 30}),
                    (99, new uint[] {0, 2, 3, 10, 13, 15, 22, 30}),
                    (100, new uint[] {0, 2, 3, 10, 13, 15, 22, 30}),
                    (200, new uint[] {0, 2, 4, 6, 7, 15, 18, 22, 23, 24, 26, 30}),
                    (201, new uint[] {0, 2, 4, 6, 7, 15, 18, 22, 23, 24, 26, 30}),
                    (0u, new uint[] {0, 1, 2, 10, 12, 15, 22, 30}),
                    (100u, new uint[] {0, 2, 3, 10, 13, 15, 22, 30}),
                    (200u, new uint[] {0, 2, 4, 6, 7, 15, 18, 22, 23, 24, 26, 30}),
                    (205u, new uint[] {0, 2, 4, 6, 7, 15, 18, 22, 23, 24, 26, 30}),
                    (0.0, new uint[] {0, 1, 2, 10, 12, 15, 22, 30}),
                    (-0.0, new uint[] {0, 1, 2, 10, 12, 15, 22, 30}),
                    (100.0, new uint[] {0, 2, 3, 10, 13, 15, 22, 30}),
                    (100.1, new uint[] {0, 2, 3, 10, 13, 15, 22, 30}),
                    (200.0, new uint[] {0, 2, 4, 6, 7, 15, 18, 22, 23, 24, 26, 30}),
                    (200.1, new uint[] {0, 2, 4, 6, 7, 15, 18, 22, 23, 24, 26, 30}),
                    (200.2, new uint[] {0, 2, 4, 6, 7, 15, 18, 22, 23, 24, 26, 30}),
                    (-1, new uint[] {0, 16, 27, 28, 29, 30}),
                    (-1.0, new uint[] {0, 16, 27, 28, 29, 30}),
                    (50000, new uint[] {0, 10, 20, 21, 25, 26, 29, 30}),
                    (50000u, new uint[] {0, 10, 20, 21, 25, 26, 29, 30}),
                    (50000.0, new uint[] {0, 10, 20, 21, 25, 26, 29, 30}),
                    (-50000, new uint[] {0, 10, 20, 21, 25, 26, 29, 30}),
                    (-50000.0, new uint[] {0, 10, 20, 21, 25, 26, 29, 30}),
                    (-50000.1, new uint[] {0, 10, 20, 21, 25, 26, 29, 30}),
                    (50001, new uint[] {0, 10, 20, 21, 25, 26, 29, 30}),
                    (50001u, new uint[] {0, 10, 20, 21, 25, 26, 29, 30}),
                    (-50001, new uint[] {0, 10, 20, 21, 25, 26, 29, 30}),
                    (0.3, new uint[] {0, 5, 6, 19, 30}),
                    (0.1 + 0.2, new uint[] {0, 5, 6, 19, 30}),
                    (Int32.MaxValue, new uint[] {0, 10, 11, 16, 28, 29, 30}),
                    ((double)Int32.MaxValue, new uint[] {0, 10, 11, 16, 28, 29, 30}),
                    (UInt32.MaxValue, new uint[] {0, 10, 11, 16, 28, 29, 30}),
                    ((double)UInt32.MaxValue, new uint[] {0, 10, 11, 16, 28, 29, 30}),
                    (Double.PositiveInfinity, new uint[] {0, 30}),
                    (Double.NegativeInfinity, new uint[] {0, 30}),
                    (Double.NaN, new uint[] {0, 30}),
                }
            );

            addTestCase(
                initialState: makeArrayAndImageWithValues(
                    12,
                    ASAny.@null,
                    "hello",
                    134.78,
                    Double.NaN,
                    12.0,
                    -44448,
                    new ASNamespace("foo"),
                    new ASNamespace("bar"),
                    13,
                    Double.NegativeInfinity,
                    Double.NaN,
                    134,
                    -44448.0,
                    "foo",
                    new ASQName("", "hello"),
                    new ASNamespace("a", "foo"),
                    12u,
                    "hello",
                    new ASQName("abc", "hello"),
                    new ASQName("p", "abc", "hello"),
                    "12",
                    "NaN",
                    true,
                    "0",
                    "",
                    false,
                    new ASNamespace("b", "foo"),
                    ASAny.@null,
                    ASAny.undefined,
                    "-44448",
                    1,
                    new ConvertibleMockObject(intValue: 12),
                    new ConvertibleMockObject(stringValue: "hello")
                ),
                indexQueries: rangeSelect(0, 31, i => (i, new uint[] {0, i, 32})),
                objectQueries: new (ASAny, uint[])[] {
                    (12, new uint[] {0, 3, 5, 11, 17, 21, 28, 34}),
                    ("12", new uint[] {0, 3, 5, 11, 17, 21, 28, 34}),
                    (134.78, new uint[] {0, 3, 5, 11, 17, 21, 28, 34}),
                    ("134.78", new uint[] {0, 3, 5, 11, 17, 21, 28, 34}),
                    (new ConvertibleMockObject(intValue: 12), new uint[] {0, 3, 5, 11, 17, 21, 28, 34}),
                    ("hello", new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (new ConvertibleMockObject(stringValue: "hello"), new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    ("foo", new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    ("bar", new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    ("baz", new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (new ASNamespace("hello"), new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (new ASNamespace("foo"), new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (new ASNamespace("bar"), new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (new ASNamespace("baz"), new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (new ASNamespace("a", "foo"), new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (new ASNamespace("d", "bar"), new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (new ASNamespace("d", "baz"), new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (new ASQName("", "hello"), new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (new ASQName("abc", "hello"), new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (new ASQName("def", "hello"), new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (new ASQName("", "abc", "hello"), new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (new ASQName("d", "abc", "hello"), new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (new ASQName("d", "abc", "foo"), new uint[] {0, 2, 4, 7, 8, 12, 14, 15, 16, 17, 18, 19, 20, 24, 27, 34}),
                    (-44448, new uint[] {0, 10, 20, 34}),
                    ("-44448", new uint[] {0, 10, 20, 34}),
                    (new ConvertibleMockObject(numberValue: -44448.0, intValue: -44448), new uint[] {0, 10, 20, 34}),
                    (true, new uint[] {0, 12, 22, 23, 24, 25, 26, 27, 31, 34}),
                    (false, new uint[] {0, 12, 22, 23, 24, 25, 26, 27, 31, 34}),
                    (new ConvertibleMockObject(boolValue: true), new uint[] {0, 12, 22, 23, 24, 25, 26, 27, 31, 34}),
                    (0, new uint[] {0, 12, 22, 23, 24, 25, 26, 27, 31, 34}),
                    (1, new uint[] {0, 12, 22, 23, 24, 25, 26, 27, 31, 34}),
                    ("0", new uint[] {0, 12, 22, 23, 24, 25, 26, 27, 31, 34}),
                    ("1", new uint[] {0, 12, 22, 23, 24, 25, 26, 27, 31, 34}),
                    ("", new uint[] {0, 12, 22, 23, 24, 25, 26, 27, 31, 34}),
                    ("true", new uint[] {0, 12, 22, 23, 24, 25, 26, 27, 31, 34}),
                    ("false", new uint[] {0, 12, 22, 23, 24, 25, 26, 27, 31, 34}),
                    (ASAny.@null, new uint[] {0, 14, 27, 28, 29, 33}),
                    (ASAny.undefined, new uint[] {0, 14, 27, 28, 29, 33}),
                }
            );

            addTestCase(
                initialState: makeArrayAndImageWithValues(
                    "encyclopædia", "Archæology", "ARCHÆOLOGY", "encyclopaedia", "ARCHAEOLOGY"
                ),
                objectQueries: new (ASAny, uint[])[] {
                    ("encyclopædia", new uint[] {0, 4}),
                    ("Archæology", new uint[] {0, 4}),
                    ("ARCHÆOLOGY", new uint[] {0, 4}),
                    ("encyclopaedia", new uint[] {0, 4}),
                    ("ARCHAEOLOGY", new uint[] {0, 4}),
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    repeat(mutPushOne(new ASObject()), 5),
                    repeat(mutPushOne(new ASObject()), 5)
                ),
                indexQueries: new (uint, uint[])[] {
                    (0, rangeSelect(0, 10, i => i)),
                    (5, rangeSelect(0, 10, i => i)),
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutSetUint(i * 10, new ASObject())),
                    rangeSelect(20, 39, i => mutSetUint(i * 10, i - 20)),
                    mutSetLength(500)
                ),
                indexQueries: rangeSelect(1, 19, i => (i, new uint[] {0, i * 10 - 5, i * 10, i * 10 + 5, 500})),
                objectQueries: new (ASAny, uint[])[] {
                    (0, new uint[] {100, 200, 205, 300, 500}),
                    (4u, new uint[] {195, 230, 235, 240, 265, 380, 405, 499}),
                    (19.0, new uint[] {247, 316, 387, 397, 400, 458}),
                    (20, new uint[] {356, 395, 401, 489}),
                    ("7", new uint[] {200, 255, 270, 298, 307, 500}),
                    (ASAny.undefined, new uint[] {0, 5, 95, 100, 195, 200, 305, 395, 400, 450, 499, 500})
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(UInt32.MaxValue),
                mutations: buildMutationList(
                    mutSetUint(UInt32.MaxValue - 7, 1),
                    mutSetUint(UInt32.MaxValue - 6, 2),
                    mutSetUint(UInt32.MaxValue - 5, 1),
                    mutSetUint(UInt32.MaxValue - 4, 5),
                    mutSetUint(UInt32.MaxValue - 3, 1),
                    mutSetUint(UInt32.MaxValue - 2, 2),
                    mutSetUint(UInt32.MaxValue - 1, 1)
                ),
                objectQueries: new (ASAny, uint[])[] {
                    (1, rangeSelect(UInt32.MaxValue - 7, UInt32.MaxValue, i => i)),
                    (2, rangeSelect(UInt32.MaxValue - 6, UInt32.MaxValue, i => i)),
                    (5, rangeSelect(UInt32.MaxValue - 4, UInt32.MaxValue, i => i)),
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                prototype: new IndexDict() {[0] = 10000},
                objectQueries: new (ASAny, uint[])[] {
                    (10000, new uint[] {0})
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(20),
                prototype: new IndexDict() {[2] = 10000, [5] = 30000, [7] = 30000, [10] = "10000", [15] = 20000, [20] = 30000},
                objectQueries: new (ASAny, uint[])[] {
                    (10000, new uint[] {0, 2, 5, 7, 10, 12, 18, 19, 20}),
                    (20000, new uint[] {0, 2, 5, 7, 10, 12, 18, 19, 20}),
                    (30000, new uint[] {0, 2, 5, 7, 10, 12, 18, 19, 20}),
                    ("10000", new uint[] {0, 2, 5, 7, 10, 12, 18, 19, 20}),
                    (40000, new uint[] {0, 2, 5, 7, 10, 12, 18, 19, 20}),
                    (ASAny.undefined, new uint[] {0, 2, 5, 7, 10, 12, 18, 19, 20})
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                prototype: new IndexDict(rangeSelect(10, 19, i => KeyValuePair.Create(i, new ASAny(new ASObject())))),
                mutations: buildMutationList(
                    rangeSelect(0, 9, i => mutSetUint(i, new ASObject())),
                    mutSetLength(20)
                ),
                indexQueries: rangeSelect(0, 19, i => (i, new uint[] {0, i, 20})),
                objectQueries: new[] {(ASAny.undefined, new uint[] {0, 20})}
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    mutSetUint(100, "hello"),
                    mutSetUint(150, "foo"),
                    mutSetUint(200, "bar"),
                    mutSetUint(226, "abcdef")
                ),
                prototype: new IndexDict() {
                    [49] = "aa112",
                    [134] = "qwerty",
                    [178] = "l938113",
                    [200] = "a__3bf",
                    [225] = "!!!!",
                    [226] = "pqrst()",
                    [227] = "wxyz"
                },
                objectQueries: new (ASAny, uint[])[] {
                    ("hello", new uint[] {0, 227}),
                    ("foo", new uint[] {0, 100, 150, 204, 227}),
                    ("abcdef", new uint[] {0, 134, 166, 204, 227}),
                    ("pqrst()", new uint[] {0, 134, 166, 204, 227}),
                    ("qwerty", new uint[] {0, 79, 134, 193, 227}),
                    ("!!!!", new uint[] {0, 79, 134, 193, 227}),
                }
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(indexOf_lastIndexOf_testData))]
        public void indexOfMethodTest(ArrayWrapper array, Image image, (ASAny, uint)[] queries, IndexDict prototype) {
            setRandomSeed(47633019);
            setPrototypeProperties(prototype);

            try {
                foreach (var (target, startIndex) in queries) {
                    double expectedIndex = -1;

                    for (uint i = startIndex; i < array.instance.length; i++) {
                        ASAny elementValue;
                        bool matches = false;

                        if (image.elements.TryGetValue(i, out elementValue)
                            || (s_currentProtoProperties != null && s_currentProtoProperties.TryGetValue(i, out elementValue)))
                        {

                            matches = ASAny.AS_strictEq(elementValue, target);
                        }
                        else {
                            matches = target.isUndefined;
                        }

                        if (matches) {
                            expectedIndex = i;
                            break;
                        }
                    }

                    foreach (var s in generateEquivalentIndices(startIndex, array.instance.length))
                        Assert.Equal(expectedIndex, array.instance.indexOf(target, s));
                }

                verifyArrayMatchesImage(array.instance, image);
            }
            finally {
                resetPrototypeProperties();
            }
        }

        [Theory]
        [MemberData(nameof(indexOf_lastIndexOf_testData))]
        public void lastIndexOfMethodTest(ArrayWrapper array, Image image, (ASAny, uint)[] queries, IndexDict prototype) {
            setRandomSeed(47633023);
            setPrototypeProperties(prototype);

            try {
                foreach (var (target, startIndex) in queries) {
                    double expectedIndex = -1;

                    if (array.instance.length > 0) {
                        uint curIndex = Math.Min(startIndex, array.instance.length - 1);

                        while (true) {
                            ASAny elementValue;
                            bool matches = false;

                            if (image.elements.TryGetValue(curIndex, out elementValue)
                                || (s_currentProtoProperties != null && s_currentProtoProperties.TryGetValue(curIndex, out elementValue)))
                            {
                                matches = ASAny.AS_strictEq(elementValue, target);
                            }
                            else {
                                matches = target.isUndefined;
                            }

                            if (matches) {
                                expectedIndex = curIndex;
                                break;
                            }

                            if (curIndex == 0)
                                break;

                            curIndex--;
                        }
                    }

                    foreach (var s in generateEquivalentIndices(startIndex, array.instance.length))
                        Assert.Equal(expectedIndex, array.instance.lastIndexOf(target, s));
                }

                verifyArrayMatchesImage(array.instance, image);
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> join_toString_testData() {
            var random = new Random(880389);

            var testcases = new List<(ArrayWrapper, Image, IndexDict)>();

            void addTestCase(
                (ASArray arr, Image img) initialState,
                IEnumerable<Mutation> mutations = null,
                IndexDict prototype = null
            ) {
                var (arr, img) = initialState;
                applyMutations(arr, mutations);
                applyMutations(ref img, mutations);

                testcases.Add((new ArrayWrapper(arr), img, prototype));
            }

            addTestCase(initialState: makeEmptyArrayAndImage());
            addTestCase(initialState: makeEmptyArrayAndImage(20));

            addTestCase(
                initialState: makeArrayAndImageWithValues(
                    rangeSelect<ASAny>(0, 99, i => RandomHelper.randomString(random, 0, 30))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 49, i => mutSetUint(i, random.Next())),
                    rangeSelect(50, 99, i => mutSetUint(i, RandomHelper.randomString(random, 0, 50))),
                    rangeSelect(100, 109, i => mutSetUint(i, true)),
                    rangeSelect(110, 119, i => mutSetUint(i, false)),
                    rangeSelect(120, 129, i => mutSetUint(i, ASAny.@null)),
                    rangeSelect(130, 139, i => mutSetUint(i, ASAny.undefined)),
                    rangeSelect(140, 149, i => mutSetUint(i, random.NextDouble())),
                    rangeSelect(150, 199, i => mutSetUint(i, new ConvertibleMockObject(stringValue: "abcdef")))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutPushOne("abcabcabc")),
                    rangeSelect(0, 49, i => mutUnshift("defdefdef")),
                    rangeSelect(0, 19, i => mutDelUint((uint)random.Next(150))),
                    mutSetLength(170)
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    mutSetUint(1, "abc1"),
                    mutSetUint(12, Double.NaN),
                    mutSetUint(13, 4775),
                    mutSetUint(19, ASAny.@null),
                    mutSetUint(28, 183.1993884),
                    mutSetUint(49, "hello0"),
                    mutSetUint(73, new ConvertibleMockObject(stringValue: "77!@rtt")),
                    mutSetUint(75, new ConvertibleMockObject(stringValue: "fhggaaq", numberValue: 1123)),
                    mutSetUint(84, "ann847;;"),
                    mutSetUint(97, 1111),
                    mutSetLength(94),
                    mutSetUint(103, "qqiiii"),
                    mutSetUint(104, true)
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                prototype: new IndexDict(rangeSelect(0, 21, i => KeyValuePair.Create(i, (ASAny)"a")))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(20),
                prototype: new IndexDict(rangeSelect(0, 21, i => KeyValuePair.Create(i, (ASAny)"a")))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutSetUint(i, "abc")),
                    mutSetLength(30)
                ),
                prototype: new IndexDict(rangeSelect(23, 29, i => KeyValuePair.Create(i, (ASAny)"def")))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    mutSetUint(3, "......"),
                    mutSetUint(7, 144.34),
                    mutSetUint(17, "abcc"),
                    mutSetUint(24, true),
                    mutSetUint(28, "177312")
                ),
                prototype: new IndexDict(rangeSelect(0, 30, i => KeyValuePair.Create(i, (ASAny)"a")))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    mutSetUint(3, "......"),
                    mutSetUint(7, 144.34),
                    mutSetUint(17, "abcc"),
                    mutSetUint(24, true),
                    mutSetUint(28, "177312")
                ),
                prototype: new IndexDict() {
                    [2] = "a",
                    [5] = "a",
                    [7] = "a",
                    [12] = "a",
                    [17] = "a",
                    [18] = "a",
                    [20] = 11345,
                    [24] = 14893,
                    [27] = 29485,
                    [28] = ".",
                    [29] = "999"
                }
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(join_toString_testData))]
        public void join_toString_methodTest(ArrayWrapper array, Image image, IndexDict prototype) {
            setRandomSeed(4635521);
            setPrototypeProperties(prototype);

            try {
                ASAny[] elements = getArrayElementsFromImage(image);

                Assert.Equal(buildString(elements, ","), array.instance.join());
                Assert.Equal(buildString(elements, ","), array.instance.AS_toString());
                Assert.Equal(buildString(elements, ","), array.instance.join(","));
                Assert.Equal(buildString(elements, ""), array.instance.join(""));
                Assert.Equal(buildString(elements, "abcdef"), array.instance.join("abcdef"));
                Assert.Equal(buildString(elements, "     "), array.instance.join(new ConvertibleMockObject(stringValue: "     ")));

                verifyArrayMatchesImage(array.instance, image);
            }
            finally {
                resetPrototypeProperties();
            }

            string buildString(ASAny[] elements, string separator) {
                var sb = new StringBuilder();
                for (int i = 0; i < elements.Length; i++) {
                    if (i > 0)
                        sb.Append(separator);
                    if (!elements[i].isUndefinedOrNull)
                        sb.Append((string)elements[i]);
                }
                return sb.ToString();
            }
        }

        public static IEnumerable<object[]> toLocaleStringMethodTest_data = TupleHelper.toArrays(
            (
                new string[] {},
                null
            ),
            (
                new string[] {"a", "b", "c", "", "d", "e", "f", "", "ghi", "123", ";;"},
                null
            ),
            (
                new string[] {null, "abc", null, null, "abc", null, "def", null, null, "ghi"},
                null
            ),
            (
                new string[] {},
                new string[] {"b", "b", "b", "b"}
            ),
            (
                new string[] {"a", "a", "a", "a", "a"},
                new string[] {"b", "b", "b", "b"}
            ),
            (
                new string[] {"a", null, null, "a", "a", null},
                new string[] {"b", "b", "b", "b", null, "b", null, null}
            ),
            (
                new string[] {"a", null, null, "a", "a", null, null, "c", "d", null},
                new string[] {"b", "b", "b", "b", null, "b", null, null}
            ),
            (
                rangeSelect(0, 49, i => ((i >= 10 && i < 19) || i == 49) ? "abc" : null),
                rangeSelect(0, 24, i => (i >= 16) ? "def" : null)
            )
        );

        [Theory]
        [MemberData(nameof(toLocaleStringMethodTest_data))]
        public void toLocaleStringMethodTest(string[] elementStrings, string[] prototypeStrings) {
            elementStrings = elementStrings ?? Array.Empty<string>();
            prototypeStrings = prototypeStrings ?? Array.Empty<string>();

            var array = new ASArray();
            var prototype = new IndexDict();

            for (int i = 0; i < elementStrings.Length; i++) {
                if (elementStrings[i] != null)
                    array.AS_setElement(i, DynamicMethodMocker.createObjectWithOwnMethod("toLocaleString", elementStrings[i]));
            }

            array.length = (uint)elementStrings.Length;

            for (int i = 0; i < prototypeStrings.Length; i++) {
                if (prototypeStrings[i] != null)
                    prototype[(uint)i] = DynamicMethodMocker.createObjectWithOwnMethod("toLocaleString", prototypeStrings[i]);
            }

            string separator = "...";
            var expectedString = new StringBuilder();

            for (int i = 0; i < elementStrings.Length; i++) {
                if (i > 0)
                    expectedString.Append(separator);

                if (elementStrings[i] != null)
                    expectedString.Append(elementStrings[i]);
                else if (i < prototypeStrings.Length && prototypeStrings[i] != null)
                    expectedString.Append(prototypeStrings[i]);
            }

            setPrototypeProperties(prototype);

            try {
                var oldCulture = CultureInfo.CurrentCulture;
                CultureInfo.CurrentCulture = new CultureInfo("en-US");
                try {
                    CultureInfo.CurrentCulture.TextInfo.ListSeparator = separator;
                    Assert.Equal(expectedString.ToString(), array.toLocaleString());
                }
                finally {
                    CultureInfo.CurrentCulture = oldCulture;
                }
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> hasOwnProperty_propertyIsEnumerable_testData() {
            setRandomSeed(57732);

            ASAny[] valueDomain = makeUniqueValues(50);
            valueDomain[6] = ASAny.undefined;
            valueDomain[14] = ASAny.undefined;
            valueDomain[28] = ASAny.undefined;
            valueDomain[32] = ASAny.@null;
            valueDomain[41] = ASAny.@null;

            var testcases = new List<(ArrayWrapper, Image, IndexDict)>();

            void addTestCase(
                (ASArray arr, Image img) initialState,
                IEnumerable<Mutation> mutations = null,
                IndexDict prototype = null
            ) {
                var (arr, img) = initialState;
                applyMutations(arr, mutations);
                applyMutations(ref img, mutations);

                testcases.Add((new ArrayWrapper(arr), img, prototype));
            }

            addTestCase(initialState: makeEmptyArrayAndImage());
            addTestCase(initialState: makeEmptyArrayAndImage(20));
            addTestCase(initialState: makeRandomDenseArrayAndImage(50, valueDomain));

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetUint(randomIndex(), randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(40),
                prototype: new IndexDict(rangeSelect(0, 29, i => KeyValuePair.Create(i, randomSample(valueDomain))))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                prototype: new IndexDict(rangeSelect(60, 129, i => KeyValuePair.Create(i, randomSample(valueDomain))))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    mutSetUint(3, randomSample(valueDomain)),
                    mutSetUint(7, randomSample(valueDomain)),
                    mutSetUint(14, randomSample(valueDomain)),
                    mutSetUint(19, randomSample(valueDomain)),
                    mutSetUint(34, randomSample(valueDomain)),
                    mutSetUint(49, randomSample(valueDomain)),
                    mutSetUint(56, randomSample(valueDomain)),
                    mutSetUint(95, randomSample(valueDomain))
                ),
                prototype: new IndexDict() {
                    [5] = randomSample(valueDomain),
                    [16] = randomSample(valueDomain),
                    [19] = randomSample(valueDomain),
                    [43] = randomSample(valueDomain),
                    [56] = randomSample(valueDomain),
                    [102] = randomSample(valueDomain)
                }
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(hasOwnProperty_propertyIsEnumerable_testData))]
        public void hasOwnProperty_propertyIsEnumerable_methodTest(ArrayWrapper array, Image image, IndexDict prototype) {
            var prototypeDPs = new Dictionary<string, (ASAny, bool isEnum)> {
                ["+9"] = (new ASObject(), isEnum: true),
                ["6.0"] = (new ASObject(), isEnum: true),
                [" 8 "] = (new ASObject(), isEnum: false),
                ["4294967295"] = (new ASObject(), isEnum: false),
                ["NaN"] = (new ASObject(), isEnum: true),
                ["def"] = (new ASObject(), isEnum: true),
                ["ghi"] = (new ASObject(), isEnum: false),
            };

            var arrayDPs = new Dictionary<string, (ASAny, bool isEnum)> {
                ["+5"] = (new ASObject(), isEnum: true),
                [" 8"] = (new ASObject(), isEnum: false),
                [" 8 "] = (new ASObject(), isEnum: true),
                ["-1"] = (new ASObject(), isEnum: true),
                ["-001"] = (new ASObject(), isEnum: true),
                ["-10000"] = (default, isEnum: true),
                ["010"] = (new ASObject(), isEnum: true),
                ["+0010"] = (new ASObject(), isEnum: false),
                ["4."] = (new ASObject(), isEnum: true),
                ["4.0"] = (default, isEnum: false),
                ["4.3"] = (new ASObject(), isEnum: true),
                ["6.0"] = (new ASObject(), isEnum: false),
                ["1e+1"] = (new ASObject(), isEnum: true),
                ["4294967295"] = (new ASObject(), isEnum: false),
                ["6781000394"] = (default, isEnum: true),
                ["Infinity"] = (new ASObject(), isEnum: false),
                ["NaN"] = (new ASObject(), isEnum: true),
                ["abc"] = (new ASObject(), isEnum: false),
                ["def"] = (new ASObject(), isEnum: true),
            };

            setPrototypeProperties(prototype);
            var arrayProto = s_arrayClassPrototype;

            try {
                foreach (var (key, (val, isEnum)) in arrayDPs)
                    array.instance.AS_dynamicProps.setValue(key, val, isEnum);

                foreach (var (key, (val, isEnum)) in prototypeDPs)
                    arrayProto.AS_dynamicProps.setValue(key, val, isEnum);

                IEnumerable<uint> indices = image.elements.Keys;
                if (prototype != null)
                    indices = indices.Union(prototype.Keys);

                foreach (uint index in indices) {
                    bool hasOwnProp = image.elements.ContainsKey(index);

                    Assert.Equal(hasOwnProp, array.instance.propertyIsEnumerable(index));
                    Assert.Equal(hasOwnProp, array.instance.propertyIsEnumerable((double)index));
                    Assert.Equal(hasOwnProp, array.instance.propertyIsEnumerable(indexToString(index)));
                    Assert.Equal(hasOwnProp, array.instance.propertyIsEnumerable(new ASQName("", indexToString(index))));

                    Assert.Equal(hasOwnProp, array.instance.hasOwnProperty(index));
                    Assert.Equal(hasOwnProp, array.instance.hasOwnProperty((double)index));
                    Assert.Equal(hasOwnProp, array.instance.hasOwnProperty(indexToString(index)));
                    Assert.Equal(hasOwnProp, array.instance.hasOwnProperty(new ASQName("", indexToString(index))));

                    if (index <= (uint)Int32.MaxValue) {
                        Assert.Equal(hasOwnProp, array.instance.propertyIsEnumerable((int)index));
                        Assert.Equal(hasOwnProp, array.instance.hasOwnProperty((int)index));
                    }
                }

                IEnumerable<string> strKeys = arrayDPs.Keys.Union(prototypeDPs.Keys).Concat(new[] {"", "wxyz"});

                foreach (string key in strKeys) {
                    bool hasOwnProp = arrayDPs.ContainsKey(key);
                    bool isEnumerable = hasOwnProp && arrayDPs[key].isEnum;

                    Assert.Equal(hasOwnProp, array.instance.hasOwnProperty(key));
                    Assert.Equal(hasOwnProp, array.instance.hasOwnProperty(new ASQName("", key)));
                    Assert.Equal(isEnumerable, array.instance.propertyIsEnumerable(key));
                    Assert.Equal(isEnumerable, array.instance.propertyIsEnumerable(new ASQName("", key)));
                }
            }
            finally {
                foreach (string key in prototypeDPs.Keys)
                    arrayProto.AS_dynamicProps.delete(key);

                resetPrototypeProperties();
            }
        }

        public readonly struct ConcatTest_Arg {
            public readonly ASAny value;
            public readonly Image image;

            public ConcatTest_Arg(ASAny value, Image image) =>
                (this.value, this.image) = (value, image);

            public override string ToString() {
                if (value.value is ASArray array)
                    return "[Array (length = " + array.length.ToString() + ")]";
                if (value.value is ASVectorAny vec)
                    return "[Vector (length = " + vec.length.ToString() + ")]";
                return value.ToString();
            }
        }

        public static IEnumerable<object[]> concatMethodTest_data() {
            setRandomSeed(797382);

            ASAny[] valueDomain = makeUniqueValues(50);
            valueDomain[6] = ASAny.undefined;
            valueDomain[14] = ASAny.undefined;
            valueDomain[28] = ASAny.undefined;
            valueDomain[32] = ASAny.@null;
            valueDomain[41] = ASAny.@null;

            const uint maxLength = UInt32.MaxValue;

            var testcases = new List<(ConcatTest_Arg[], IndexDict)>();

            void addTestCase(ConcatTest_Arg[] args, int[][] permutations = null, IndexDict prototype = null) {
                if (permutations != null)
                    testcases.AddRange(permutations.Select(p => (p.Select(i => args[i]).ToArray(), prototype)));
                else
                    testcases.Add((args, prototype));
            }

            ConcatTest_Arg makeSingleArg(ASAny value) => new ConcatTest_Arg(value, makeImageWithValues(value));

            ConcatTest_Arg makeArrayArg((ASArray arr, Image img) initialState, IEnumerable<Mutation> mutations = null) {
                var (arr, img) = initialState;
                applyMutations(arr, mutations);
                applyMutations(ref img, mutations);

                return new ConcatTest_Arg(arr, img);
            }

            ConcatTest_Arg makeVectorArg<T>(params T[] elements) {
                return new ConcatTest_Arg(
                    new ASVector<T>(elements),
                    makeImageWithValues(GenericTypeConverter<T, ASAny>.instance.convertSpan(elements))
                );
            }

            addTestCase(
                args: new[] {
                    makeArrayArg(initialState: makeEmptyArrayAndImage()),
                    makeArrayArg(initialState: makeEmptyArrayAndImage(10)),
                    makeArrayArg(initialState: makeEmptyArrayAndImage(20)),
                    makeArrayArg(initialState: makeEmptyArrayAndImage(maxLength - 10))
                },
                permutations: new[] {
                    new[] {0},
                    new[] {1},
                    new[] {3},
                    new[] {0, 0, 0},
                    new[] {0, 1, 2},
                    new[] {1, 2, 1, 0, 2},
                    new[] {3, 0, 0},
                    new[] {0, 1, 2, 3},
                    new[] {3, 2, 1, 0},
                    new[] {3, 3, 3}
                }
            );

            addTestCase(
                args: new[] {
                    makeArrayArg(initialState: makeEmptyArrayAndImage()),
                    makeSingleArg(valueDomain[0]),
                    makeSingleArg(ASAny.undefined),
                    makeArrayArg(initialState: makeRandomDenseArrayAndImage(1, valueDomain)),
                    makeArrayArg(initialState: makeRandomDenseArrayAndImage(5, valueDomain)),
                    makeSingleArg(valueDomain[1]),
                    makeArrayArg(initialState: makeRandomDenseArrayAndImage(30, valueDomain)),
                    makeArrayArg(initialState: makeRandomDenseArrayAndImage(100, valueDomain)),
                },
                permutations: new[] {
                    new[] {3},
                    new[] {7},
                    new[] {0, 2},
                    new[] {0, 6},
                    new[] {3, 5},
                    new[] {6, 0},
                    new[] {6, 3},
                    new[] {6, 5},
                    new[] {3, 0, 4},
                    new[] {0, 5, 1, 2, 2, 1, 5, 5, 1, 2},
                    new[] {3, 0, 5, 2, 5, 3, 0, 5, 2, 0, 0, 1, 3, 3, 5, 1, 2, 0},
                    new[] {3, 4, 3, 6},
                    new[] {3, 3, 3, 6, 3, 4, 3, 6, 4, 4, 3, 3, 6, 3, 6, 6, 4},
                    new[] {0, 7, 7, 0, 0, 6, 0, 0, 0, 7},
                    new[] {4, 0, 2, 1, 6, 3, 4, 4, 0, 0, 2, 7, 3, 6, 0, 5, 5, 2, 6, 1, 7, 0, 2},
                    new[] {7, 2, 1, 3, 2, 4, 0, 2, 0, 4, 6, 4, 4, 7, 2, 4, 4, 6},
                }
            );

            addTestCase(
                args: new[] {
                    makeSingleArg(valueDomain[0]),
                    makeSingleArg(valueDomain[1]),
                    makeSingleArg(ASAny.@undefined),
                    makeArrayArg(initialState: makeEmptyArrayAndImage()),
                    makeArrayArg(initialState: makeRandomDenseArrayAndImage(10, valueDomain)),
                    makeArrayArg(initialState: makeRandomDenseArrayAndImage(50, valueDomain)),
                    makeVectorArg<int>(),
                    makeVectorArg<int>(rangeSelect(1, 10, i => (int)i)),
                    makeVectorArg<double>(rangeSelect(1, 50, i => (i == 50) ? Double.NaN : i * 1.4352e-10)),
                    makeVectorArg<ASObject>(rangeSelect(1, 50, i => randomSample(valueDomain).value))
                },
                permutations: new[] {
                    new[] {3, 6},
                    new[] {3, 7},
                    new[] {3, 9},
                    new[] {3, 7, 3, 6, 9},
                    new[] {3, 6, 4, 3, 6, 5},
                    new[] {4, 7, 4, 7, 5, 9, 8, 3, 4, 6, 8, 9},
                    new[] {5, 5, 5, 8, 8, 8, 6, 6, 6, 3, 3, 3, 5, 5, 5, 9, 9},
                    new[] {5, 0, 0, 0, 1, 0, 2, 2, 2, 2, 8, 0, 0, 2, 4, 6, 0, 7},
                    new[] {3, 0, 2, 3, 1, 6, 2, 1, 2, 0, 8, 4, 2, 3, 3, 5, 5, 0, 0, 1, 0, 7, 1, 0, 9, 2, 7, 7, 1, 2, 3, 0},
                }
            );

            addTestCase(
                args: new[] {
                    makeSingleArg(valueDomain[0]),
                    makeSingleArg(valueDomain[1]),
                    makeSingleArg(ASAny.@undefined),
                    makeArrayArg(
                        initialState: makeEmptyArrayAndImage()
                    ),
                    makeArrayArg(
                        initialState: makeEmptyArrayAndImage(),
                        mutations: rangeSelect(0, 29, i => mutSetUint(i, randomSample(valueDomain)))
                    ),
                    makeArrayArg(
                        initialState: makeEmptyArrayAndImage(),
                        mutations: rangeSelect(0, 199, i => mutPushOne(randomSample(valueDomain)))
                    ),
                    makeArrayArg(
                        initialState: makeEmptyArrayAndImage(30)
                    ),
                    makeArrayArg(
                        initialState: makeEmptyArrayAndImage(),
                        mutations: buildMutationList(
                            rangeSelect(0, 49, i => mutUnshift(randomSample(valueDomain))),
                            mutSetLength(120)
                        )
                    ),
                    makeArrayArg(
                        initialState: makeEmptyArrayAndImage(),
                        mutations: buildMutationList(
                            rangeSelect(0, 29, i => mutSetUint(randomIndex(100), randomSample(valueDomain))),
                            mutSetLength(100)
                        )
                    ),
                    makeArrayArg(
                        initialState: makeEmptyArrayAndImage(),
                        mutations: buildMutationList(
                            rangeSelect(0, 79, i => mutSetUint(randomIndex(100), randomSample(valueDomain))),
                            mutSetLength(100)
                        )
                    ),
                    makeArrayArg(
                        initialState: makeEmptyArrayAndImage(),
                        mutations: buildMutationList(
                            rangeSelect(0, 199, i => mutSetUint(i, randomSample(valueDomain))),
                            rangeSelect(0, 39, i => mutDelUint(i)),
                            rangeSelect(160, 199, i => mutDelUint(i)),
                            rangeSelect(0, 39, i => mutDelUint(40 + randomIndex(120)))
                        )
                    ),
                    makeVectorArg<int>(1, 2, 3, 4, 5, 1),
                    makeVectorArg<ASObject>(rangeSelect(0, 9, i => randomSample(valueDomain).value))
                },
                permutations: new[] {
                    new[] {6},
                    new[] {7},
                    new[] {8},
                    new[] {9},
                    new[] {10},
                    new[] {6, 4},
                    new[] {4, 3, 6, 6, 3, 2, 4},
                    new[] {6, 6, 6, 5, 6, 6, 6},
                    new[] {7, 4, 4},
                    new[] {4, 7, 4},
                    new[] {5, 7},
                    new[] {8, 3, 3, 4},
                    new[] {3, 8, 8, 5},
                    new[] {9, 9},
                    new[] {7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                    new[] {8, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2},
                    new[] {4, 3, 10, 5, 10},
                    new[] {7, 11, 6, 3, 4, 12, 11, 3, 4, 12, 2, 0, 0, 2, 11},
                    new[] {10, 12, 11, 2, 0, 4, 0, 1, 12},
                    new[] {6, 6, 6, 5, 6, 6, 6, 6},
                    new[] {8, 8},
                    new[] {10, 8},
                    new[] {9, 7, 3, 3, 6, 2, 1, 6},
                    new[] {10, 12, 11, 2, 0, 4, 0, 1, 12, 8, 6, 8},
                }
            );

            addTestCase(
                args: new[] {
                    makeSingleArg(valueDomain[0]),
                    makeArrayArg(
                        initialState: makeEmptyArrayAndImage()
                    ),
                    makeArrayArg(
                        initialState: makeRandomDenseArrayAndImage(10, valueDomain)
                    ),
                    makeArrayArg(
                        initialState: makeRandomDenseArrayAndImage(30, valueDomain)
                    ),
                    makeArrayArg(
                        initialState: makeEmptyArrayAndImage(),
                        mutations: rangeSelect(maxLength - 30, maxLength - 1, i => mutSetUint(i, randomSample(valueDomain)))
                    ),
                    makeArrayArg(
                        initialState: makeEmptyArrayAndImage(),
                        mutations: rangeSelect(maxLength - 60, maxLength - 31, i => mutSetUint(i, randomSample(valueDomain)))
                    ),
                    makeArrayArg(
                        initialState: makeEmptyArrayAndImage(),
                        mutations: buildMutationList(
                            rangeSelect(0, 9, i => mutSetUint(i, randomSample(valueDomain))),
                            rangeSelect(maxLength - 20, maxLength - 1, i => mutSetUint(i, randomSample(valueDomain))),
                            rangeSelect(maxLength - 10, maxLength - 1, i => mutDelUint(i))
                        )
                    ),
                    makeVectorArg<int>(0, 0, 0, 0, 1, 1, 1, 1),
                },
                permutations: new[] {
                    new[] {4},
                    new[] {5},
                    new[] {6},
                    new[] {4, 4},
                    new[] {5, 4},
                    new[] {5, 6},
                    new[] {6, 6},
                    new[] {6, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0},
                    new[] {6, 2, 1, 2, 1},
                    new[] {6, 3},
                    new[] {6, 4},
                    new[] {6, 3, 4, 1, 2, 0, 1, 3, 6},
                    new[] {4, 0, 0},
                    new[] {4, 2},
                    new[] {4, 2, 1, 0},
                    new[] {4, 0, 3},
                    new[] {5, 3},
                    new[] {5, 0, 1, 3},
                    new[] {5, 2, 2, 2},
                    new[] {5, 2, 2, 2, 2},
                    new[] {5, 2, 2, 2, 3},
                    new[] {5, 2, 2, 3, 2},
                    new[] {5, 2, 2, 2, 0},
                    new[] {6, 0, 6, 1, 6},
                    new[] {4, 7},
                    new[] {1, 7, 4},
                    new[] {2, 0, 0, 7, 4},
                    new[] {6, 7, 7},
                    new[] {5, 7, 7, 7, 0, 0, 0, 0, 0, 0, 0, 0},
                }
            );

            addTestCase(
                prototype: new IndexDict(rangeSelect(0, 19, i => KeyValuePair.Create(i, randomSample(valueDomain)))),
                args: new[] {
                    makeArrayArg(
                        initialState: makeRandomDenseArrayAndImage(10, valueDomain)
                    ),
                    makeArrayArg(
                        initialState: makeEmptyArrayAndImage(10)
                    ),
                    makeArrayArg(
                        initialState: makeRandomDenseArrayAndImage(10, valueDomain),
                        mutations: new[] {mutSetUint(99, randomSample(valueDomain))}
                    )
                },
                permutations: new[] {
                    new[] {0, 0},
                    new[] {1, 1},
                    new[] {1, 0, 1},
                    new[] {2, 0},
                    new[] {2, 1, 0, 2}
                }
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(concatMethodTest_data))]
        public void concatMethodTest(ConcatTest_Arg[] args, IndexDict prototype) {
            ASArray firstArg = (ASArray)args[0].value;

            ASAny[] rest = new ASAny[args.Length - 1];
            for (int i = 1; i < args.Length; i++)
                rest[i - 1] = args[i].value;

            ASArray result = firstArg.concat(new RestParam(rest));

            for (int i = 0; i < args.Length; i++)
                Assert.NotSame(args[i].value.value, result);

            Image resultImage = makeEmptyImage();

            for (int i = 0; i < args.Length; i++) {
                Image argImage = args[i].image;

                uint appendOffset = resultImage.length;
                uint appendLength = Math.Min(argImage.length, UInt32.MaxValue - appendOffset);

                if (appendLength == 0)
                    continue;

                resultImage = new Image(appendOffset + appendLength, resultImage.elements);
                foreach (var (key, val) in argImage.elements) {
                    if (key < appendLength)
                        resultImage.elements.Add(appendOffset + key, val);
                }
            }

            setRandomSeed(614398);
            setPrototypeProperties(prototype);

            try {
                // Verify result
                // Use primitiveValueEqual because of boxing when copying from primitive-typed vectors.
                verifyArrayMatchesImage(result, resultImage, primitiveValueEqual: true);

                // Check for mutations of any array or vector arguments.
                var checkedArgs = new ReferenceSet<object>();

                for (int i = 0; i < args.Length; i++) {
                    Image argImage = args[i].image;
                    ASObject argVal = args[i].value.value;

                    if (argVal == null || !checkedArgs.add(argVal))
                        continue;

                    if (argVal is ASArray argArray) {
                        verifyArrayMatchesImage(argArray, argImage, dontSampleOutsideArrayLength: true);
                    }
                    else if (argVal is ASVectorAny argVector) {
                        Assert.Equal(argImage.length, (uint)argVector.length);
                        for (int j = 0; j < argVector.length; j++)
                            AssertHelper.valueIdentical(argImage.elements[(uint)j], argVector.AS_getElement(j));
                    }
                }
            }
            finally {
                resetPrototypeProperties();
            }
        }

        private class ReverseMutation {}

        public static IEnumerable<object[]> reverseMethodTest_data() {
            setRandomSeed(1841);

            ASAny[] valueDomain = makeUniqueValues(50);
            valueDomain[6] = ASAny.undefined;
            valueDomain[14] = ASAny.undefined;
            valueDomain[28] = ASAny.undefined;
            valueDomain[32] = ASAny.@null;
            valueDomain[41] = ASAny.@null;

            const uint maxLength = UInt32.MaxValue;

            var testcases = new List<(ArrayWrapper, Image, IEnumerable<object>, IndexDict)>();

            void addTestCase((ASArray arr, Image img) initialState, IEnumerable<object> mutations, IndexDict prototype = null) =>
                testcases.Add((new ArrayWrapper(initialState.arr), initialState.img, mutations, prototype));

            object mutReverse = new ReverseMutation();

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: new object[] {
                    mutReverse,
                    mutReverse,
                    mutSetLength(100),
                    mutReverse,
                    mutReverse,
                    mutSetLength(maxLength),
                    mutReverse,
                    mutReverse,
                }
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(1, valueDomain),
                mutations: new object[] {mutReverse}
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(100, valueDomain),
                mutations: new object[] {
                    mutReverse,
                    mutReverse,
                    mutReverse,
                    rangeSelect(101, 200, i => mutSetUint(i, randomSample(valueDomain))),
                    mutReverse,
                    mutReverse,
                    rangeSelect(50, 129, i => mutSetUint(i, randomSample(valueDomain))),
                    mutReverse,
                    mutSetLength(60),
                    rangeSelect(50, 69, i => mutSetUint(i, randomSample(valueDomain))),
                    mutReverse,
                    mutReverse,
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: new object[] {
                    rangeSelect(0, 29, i => mutPushOne(randomSample(valueDomain))),
                    mutReverse,
                    rangeSelect(0, 29, i => mutUnshift(randomSample(valueDomain))),
                    mutReverse,
                    rangeSelect(0, 2, i => mutPush(rangeSelect(0, 9, j => randomSample(valueDomain)))),
                    mutReverse,
                    rangeSelect(0, 2, i => mutUnshift(rangeSelect(0, 9, j => randomSample(valueDomain)))),
                    mutReverse,
                    rangeSelect(0, 19, i => mutPop()),
                    mutReverse,
                    rangeSelect(0, 19, i => mutShift()),
                    mutReverse,
                    rangeSelect(99, 20, i => mutDelUint(i)),
                    rangeSelect(20, 24, i => mutSetUint(i, randomSample(valueDomain))),
                    mutReverse,
                    rangeSelect(25, 99, i => mutSetUint(i, randomSample(valueDomain))),
                    mutReverse,
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(40),
                mutations: new object[] {
                    rangeSelect(0, 29, i => mutSetUint(i, randomSample(valueDomain))),
                    mutReverse,
                    mutReverse,
                    mutSetLength(50),
                    mutReverse,
                    mutSetLength(70),
                    mutReverse,
                    rangeSelect(0, 19, i => mutSetUint(i, randomSample(valueDomain))),
                    mutReverse,
                    rangeSelect(120, 129, i => mutSetUint(i, randomSample(valueDomain))),
                    mutReverse,
                    mutReverse,
                    mutReverse,
                    rangeSelect(0, 4, i => mutDelUint(i)),
                    rangeSelect(110, 129, i => mutDelUint(i)),
                    mutSetLength(120),
                    mutReverse,
                    rangeSelect(94, 45, i => mutSetUint(i, randomSample(valueDomain))),
                    mutReverse,
                    mutSetLength(100),
                    mutReverse,
                    mutReverse
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: new object[] {
                    mutReverse,
                    rangeSelect(0, 29, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(maxLength - 30, maxLength - 1, i => mutSetUint(i, randomSample(valueDomain))),
                    mutReverse,
                    rangeSelect(maxLength / 2, maxLength / 2 + 49, i => mutSetUint(i, randomSample(valueDomain))),
                    mutReverse,
                    mutReverse,
                    mutReverse,
                    rangeSelect(maxLength - 5, maxLength - 1, i => mutDelUint(i)),
                    mutReverse,
                    mutSetLength(maxLength - 2000),
                    mutReverse,
                    mutReverse,
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: new object[] {
                    rangeSelect(0, 99, i => mutSetUint(randomIndex(120), randomSample(valueDomain))),
                    mutReverse,
                    rangeSelect(0, 19, i => mutDelUint(randomIndex(120))),
                    mutReverse,
                    rangeSelect(0, 99, i => mutSetUint(randomIndex(5000), randomSample(valueDomain))),
                    mutReverse,
                    rangeSelect(0, 99, i => mutSetUint(randomIndex(maxLength), randomSample(valueDomain))),
                    mutReverse,
                    rangeSelect(0, 99, i => mutSetUint(maxLength - 120 + randomIndex(120), randomSample(valueDomain))),
                    mutReverse,
                    rangeSelect(0, 49, i => mutDelUint(randomIndex(120))),
                    mutReverse,
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(20),
                prototype: makeIndexDict(rangeSelect(0, 29, i => i)),
                mutations: new object[] {mutReverse, mutReverse}
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(100, valueDomain),
                prototype: makeIndexDict(rangeSelect(50, 149, i => i)),
                mutations: new object[] {
                    mutReverse,
                    mutSetLength(150),
                    mutReverse,
                    mutReverse,
                    mutSetLength(300),
                    mutReverse
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                prototype: makeIndexDict(rangeSelect(0, 49, i => randomIndex(maxLength))),
                mutations: new object[] {
                    rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                    mutReverse,
                    mutReverse
                }
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(reverseMethodTest_data))]
        public void reverseMethodTest(ArrayWrapper array, Image image, IEnumerable<object> mutations, IndexDict prototype) {
            setRandomSeed(51469);
            setPrototypeProperties(prototype);

            try {
                foreach (object mutOrReverse in mutations) {
                    if (mutOrReverse is Mutation) {
                        var m = (Mutation)mutOrReverse;
                        applyMutation(array.instance, m);
                        applyMutation(ref image, m);
                    }
                    else if (mutOrReverse is IEnumerable<Mutation> mutList) {
                        applyMutations(array.instance, mutList);
                        applyMutations(ref image, mutList);
                    }
                    else if (mutOrReverse is ReverseMutation) {
                        ASArray result = array.instance.reverse();
                        Assert.Same(array.instance, result);

                        var newImage = makeEmptyImage(image.length);
                        foreach (var (key, val) in image.elements)
                            newImage.elements[image.length - key - 1] = val;

                        image = newImage;
                    }

                    verifyArrayMatchesImage(array.instance, image);
                }
            }
            finally {
                resetPrototypeProperties();
            }
        }

        private class SpliceMutation {
            public readonly uint startIndex;
            public readonly uint deleteCount;
            public readonly ASAny[] newValues;

            public SpliceMutation(uint startIndex, uint deleteCount, ASAny[] newValues) {
                this.startIndex = startIndex;
                this.deleteCount = deleteCount;
                this.newValues = newValues;
            }
        }

        public static IEnumerable<object[]> spliceMethodTest_data() {
            setRandomSeed(44143);

            ASAny[] valueDomain = makeUniqueValues(50);
            valueDomain[6] = ASAny.undefined;
            valueDomain[14] = ASAny.undefined;
            valueDomain[28] = ASAny.undefined;
            valueDomain[32] = ASAny.@null;
            valueDomain[41] = ASAny.@null;

            const uint maxLength = UInt32.MaxValue;

            var testcases = new List<(ArrayWrapper, Image, IEnumerable<object>, IndexDict)>();

            void addTestCase((ASArray arr, Image img) initialState, IEnumerable<object> mutations, IndexDict prototype = null) =>
                testcases.Add((new ArrayWrapper(initialState.arr), initialState.img, mutations, prototype));

            SpliceMutation mutSplice(uint startIndex, uint deleteCount, params ASAny[] values) =>
                new SpliceMutation(startIndex, deleteCount, values);

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: new object[] {
                    mutSplice(0, 0),
                    mutSplice(0, 0, randomSample(valueDomain)),
                    mutSplice(0, 0),
                    mutSplice(1, 0),
                    mutSplice(0, 1)
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(50),
                mutations: new object[] {
                    mutSplice(0, 0),
                    mutSplice(20, 0),
                    mutSplice(50, 0),
                    mutSplice(0, 10),
                    mutSplice(20, 10),
                    mutSplice(25, 5),
                    mutSetLength(50),
                    mutSplice(0, 20, rangeSelect(0, 9, i => randomSample(valueDomain))),
                    mutSplice(30, 10, rangeSelect(0, 9, i => randomSample(valueDomain))),
                    mutSplice(15, 0, rangeSelect(0, 9, i => randomSample(valueDomain))),
                    mutSplice(0, 50),
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: new object[] {
                    mutSplice(0, 0, rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutSplice(20, 0, rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutSplice(0, 0, rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutSplice(10, 20, rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutSplice(0, 40, rangeSelect(0, 39, i => randomSample(valueDomain))),
                    mutSplice(50, 10, rangeSelect(0, 9, i => randomSample(valueDomain))),
                    mutSplice(35, 5),
                    mutSplice(50, 5),
                    mutSplice(0, 15, rangeSelect(0, 24, i => randomSample(valueDomain))),
                    mutSplice(50, 10, rangeSelect(0, 89, i => randomSample(valueDomain))),
                    mutSplice(10, 120, rangeSelect(0, 9, i => randomSample(valueDomain))),
                    mutSplice(0, 30, rangeSelect(0, 9, i => randomSample(valueDomain))),
                    mutSplice(2, 4),
                    mutSplice(0, 6),
                }
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(50, valueDomain),
                mutations: new object[] {
                    mutSplice(20, 20, rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutSplice(48, 2),
                    mutSplice(45, 3, rangeSelect(0, 24, i => randomSample(valueDomain))),
                    rangeSelect(50, 69, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(70, 79, i => mutPush(randomSample(valueDomain))),
                    rangeSelect(0, 9, i => mutUnshift(randomSample(valueDomain))),
                    mutSplice(5, 12, rangeSelect(0, 16, i => randomSample(valueDomain))),
                    mutSplice(40, 15, rangeSelect(0, 9, i => randomSample(valueDomain))),
                    mutSplice(90, 0),
                    mutSplice(0, 0),
                    mutSplice(80, 10, rangeSelect(0, 9, i => randomSample(valueDomain))),
                    rangeSelect(0, 14, i => mutPop()),
                    rangeSelect(0, 14, i => mutUnshift()),
                    mutSplice(50, 10),
                    mutSplice(0, 10),
                    mutSetLength(90),
                    mutSplice(40, 50, rangeSelect(0, 9, i => randomSample(valueDomain))),
                    mutSetLength(150),
                    mutSplice(40, 100),
                    rangeSelect(30, 99, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(99, 20, i => mutDelUint(i)),
                    mutSplice(15, 85, rangeSelect(0, 14, i => randomSample(valueDomain))),
                    mutSetLength(5),
                    mutSetLength(100),
                    mutSplice(0, 100),
                    mutPush(rangeSelect(0, 99, i => randomSample(valueDomain))),
                    rangeSelect(99, 40, i => mutDelUint(i)),
                    mutSplice(30, 30, rangeSelect(0, 29, i => randomSample(valueDomain))),
                    mutSplice(65, 30, rangeSelect(0, 49, i => randomSample(valueDomain)))
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: new object[] {
                    mutSetLength(100),
                    rangeSelect(0, 29, i => mutSetUint(randomIndex(100), randomSample(valueDomain))),
                    mutSplice(25, 50, rangeSelect(0, 4, i => randomSample(valueDomain))),
                    mutSplice(0, 55),
                    rangeSelect(0, 19, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(70, 89, i => mutSetUint(i, randomSample(valueDomain))),
                    mutSplice(40, 0, rangeSelect(0, 49, i => randomSample(valueDomain))),
                    mutSplice(40, 51, randomSample(valueDomain)),
                    mutSplice(45, 5, rangeSelect(0, 49, i => randomSample(valueDomain))),
                    mutSplice(45, 50),
                    mutSplice(20, 45, rangeSelect(0, 9, i => randomSample(valueDomain))),
                    mutSetLength(145),
                    mutSplice(145, 0, rangeSelect(0, 29, i => randomSample(valueDomain))),
                    mutSplice(50, 0, rangeSelect(0, 4, i => randomSample(valueDomain))),
                    mutSplice(65, 35, rangeSelect(0, 34, i => randomSample(valueDomain))),
                    rangeSelect(65, 99, i => mutDelUint(i)),
                    mutSplice(65, 55),
                    mutSplice(65, 0, rangeSelect(0, 34, i => randomSample(valueDomain))),
                    mutSetLength(3500000030),
                    mutSplice(3500000000, 30, rangeSelect(0, 39, i => randomSample(valueDomain))),
                    mutSplice(2500000000, 40, rangeSelect(0, 39, i => randomSample(valueDomain))),
                    mutSplice(3000000000, 60, rangeSelect(0, 39, i => randomSample(valueDomain))),
                    mutSetUint(maxLength - 1, ASAny.undefined),
                    mutSplice(maxLength, 0),
                    mutSplice(maxLength, 0, randomSample(valueDomain)),
                    mutSplice(maxLength - 20, 20, rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutSplice(maxLength - 60, 30, rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutSplice(maxLength - 60, 20, rangeSelect(0, 39, i => randomSample(valueDomain))),
                    mutSplice(maxLength - 20, 0, rangeSelect(0, 9, i => randomSample(valueDomain))),
                    mutSplice(maxLength - 20, 20, rangeSelect(0, 49, i => randomSample(valueDomain))),
                    mutSplice(maxLength - 20, 5, rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutSplice(maxLength - 50, 0, rangeSelect(0, 9, i => randomSample(valueDomain))),
                    mutSplice(2500000000, 60, rangeSelect(0, 9, i => randomSample(valueDomain))),
                    mutSplice(maxLength - 50, 0, rangeSelect(0, 9, i => randomSample(valueDomain))),
                    mutSplice(3000000000, 60),
                    mutSplice(3800000000, 30),
                    mutSetLength(maxLength),
                    mutSplice(0, 10, rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutSplice(maxLength - 110, 10, rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutSplice(0, 200, rangeSelect(0, 99, i => randomSample(valueDomain))),
                    mutSplice(maxLength - 110, 10, rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutSetLength(100),
                    mutSplice(30, 30, rangeSelect(0, 29, i => randomSample(valueDomain))),
                    mutSetLength(maxLength),
                    mutSplice(30, 30, rangeSelect(0, 29, i => randomSample(valueDomain))),
                    mutSplice(50, 0, rangeSelect(0, 9, i => randomSample(valueDomain)))
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(20),
                prototype: makeIndexDict(rangeSelect(0, 99, i => i)),
                mutations: new object[] {
                    mutSplice(5, 5, rangeSelect(0, 5, i => randomSample(valueDomain))),
                    mutSplice(5, 5),
                    mutSplice(5, 5, rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutSplice(0, 30),
                    mutSetLength(120),
                    mutSplice(80, 40, rangeSelect(0, 39, i => randomSample(valueDomain))),
                    mutSplice(0, 60, rangeSelect(0, 39, i => randomSample(valueDomain))),
                    rangeSelect(60, 99, i => mutDelUint(i)),
                    mutSplice(70, 20, rangeSelect(0, 29, i => randomSample(valueDomain))),
                    mutSplice(30, 75)
                }
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(spliceMethodTest_data))]
        public void spliceMethodTest(ArrayWrapper array, Image image, IEnumerable<object> mutations, IndexDict prototype) {
            setRandomSeed(6033972);
            setPrototypeProperties(prototype);

            try {
                foreach (object mutOrSplice in mutations) {
                    if (mutOrSplice is Mutation) {
                        var m = (Mutation)mutOrSplice;
                        applyMutation(array.instance, m);
                        applyMutation(ref image, m);
                        verifyArrayMatchesImage(array.instance, image);
                    }
                    else if (mutOrSplice is IEnumerable<Mutation> mutList) {
                        applyMutations(array.instance, mutList);
                        applyMutations(ref image, mutList);
                        verifyArrayMatchesImage(array.instance, image);
                    }
                    else if (mutOrSplice is SpliceMutation spliceMutation) {
                        applySpliceMutationAndVerify(array.instance, ref image, spliceMutation);
                    }
                }
            }
            finally {
                resetPrototypeProperties();
            }
        }

        private void applySpliceMutationAndVerify(ASArray array, ref Image image, SpliceMutation mutation) {
            ASArray arrayClone = array.clone();

            Assert.True(mutation.startIndex <= array.length);
            Assert.True(mutation.deleteCount <= array.length - mutation.startIndex);

            uint delta = (uint)mutation.newValues.Length - mutation.deleteCount;

            uint splicedLength;
            if ((int)delta <= 0)
                splicedLength = image.length + delta;
            else
                splicedLength = image.length + Math.Min(delta, UInt32.MaxValue - image.length);

            Image originalImage = image;
            Image splicedImage = makeEmptyImage(splicedLength);

            foreach (var (key, val) in image.elements) {
                if (key >= mutation.startIndex + mutation.deleteCount) {
                    if ((int)delta <= 0 || key < UInt32.MaxValue - delta)
                        splicedImage.elements[key + delta] = val;
                }
                else if (key < mutation.startIndex) {
                    splicedImage.elements[key] = val;
                }
            }

            for (int i = 0; i < mutation.newValues.Length && (uint)i < UInt32.MaxValue - mutation.startIndex; i++)
                splicedImage.elements[mutation.startIndex + (uint)i] = mutation.newValues[i];

            ASAny[] spliceCallArgs = new ASAny[mutation.newValues.Length + 2];
            spliceCallArgs[0] = mutation.startIndex;
            spliceCallArgs[1] = mutation.deleteCount;
            mutation.newValues.CopyTo(spliceCallArgs, 2);

            void testSplice(ASArray testArray, int nCallArgs) {
                ASArray result = testArray.splice(new RestParam(spliceCallArgs.AsSpan(0, nCallArgs)));
                Assert.NotSame(result, testArray);
                verifySlice(result, originalImage, mutation.startIndex, mutation.startIndex + mutation.deleteCount);
                verifyArrayMatchesImage(testArray, splicedImage);
            }

            // Mutate the array under test, using the original arguments.
            testSplice(array, spliceCallArgs.Length);

            // Test argument variants on clones of the array.

            foreach (var (start, count) in generateEquivalentStartLengthRanges(mutation.startIndex, mutation.deleteCount, arrayClone.length)) {
                spliceCallArgs[0] = start;
                spliceCallArgs[1] = count;
                testSplice(arrayClone.clone(), spliceCallArgs.Length);
            }

            if (mutation.startIndex + mutation.deleteCount == arrayClone.length && mutation.newValues.Length == 0) {
                foreach (var start in generateEquivalentIndices(mutation.startIndex, arrayClone.length)) {
                    spliceCallArgs[0] = start;
                    testSplice(arrayClone.clone(), 1);
                }
            }

            if (mutation.startIndex == 0 && mutation.deleteCount == 0 && mutation.newValues.Length == 0)
                testSplice(arrayClone.clone(), 0);

            image = splicedImage;
        }

    }

}
