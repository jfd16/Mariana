using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    using IndexDict = Dictionary<uint, ASAny>;

    public partial class ASArrayTest {

        /// <summary>
        /// Used in sortOn tests.
        /// </summary>
        private class ClassWithProps : MockClassInstance {
            private ASAny m_x;
            private ASAny m_y;
            private ASAny m_z;
            private ASAny m_w;

            public ClassWithProps(ASAny x, ASAny y, ASAny z, ASAny w) : base(s_mockClass) {
                m_x = x;
                m_y = y;
                m_z = z;
                m_w = w;
            }

            private static readonly MockClass s_mockClass = new MockClass(
                name: "ASArrayTest_ClassWithProps",
                fields: new[] {
                    new MockFieldTrait("x", isReadOnly: true, getValueFunc: obj => ((ClassWithProps)obj).m_x),
                    new MockFieldTrait("y", isReadOnly: true, getValueFunc: obj => ((ClassWithProps)obj).m_y),
                },
                properties: new[] {
                    new MockPropertyTrait("z", getter: new MockMethodTrait(invokeFunc: (obj, args) => ((ClassWithProps)obj).m_z)),
                    new MockPropertyTrait("w", getter: new MockMethodTrait(invokeFunc: (obj, args) => ((ClassWithProps)obj).m_w)),
                }
            );
        }

        private static int compareWithSortFlags(ASAny left, ASAny right, int sortFlags) {
            int cmpResult;

            if ((sortFlags & ASArray.NUMERIC) != 0) {
                double dLeft = (double)left;
                double dRight = (double)right;

                if ((sortFlags & ASArray.DESCENDING) != 0)
                    (dLeft, dRight) = (dRight, dLeft);

                if (Double.IsNaN(dLeft))
                    cmpResult = Double.IsNaN(dRight) ? 0 : 1;
                else if (Double.IsNaN(dRight))
                    cmpResult = -1;
                else
                    cmpResult = dLeft.CompareTo(dRight);
            }
            else {
                string sLeft = ASAny.AS_convertString(left);
                string sRight = ASAny.AS_convertString(right);

                if ((sortFlags & ASArray.DESCENDING) != 0)
                    (sLeft, sRight) = (sRight, sLeft);

                if ((sortFlags & ASArray.CASEINSENSITIVE) != 0)
                    cmpResult = String.Compare(sLeft, sRight, StringComparison.OrdinalIgnoreCase);
                else
                    cmpResult = String.Compare(sLeft, sRight, StringComparison.Ordinal);
            }

            return cmpResult;
        }

        private static Comparison<ASAny> makeArraySortCompareFunc(MockFunctionObject func, int sortFlags) {
            if (func == null)
                return (x, y) => compareWithSortFlags(x, y, sortFlags);

            // We have a user-provided comparator function. Use the function's call records
            // to build a map of comparison results.

            var compareFuncResults = new Dictionary<(ASAny, ASAny), int>(
                comparer: new FunctionEqualityComparer<(ASAny, ASAny)>(
                    (x, y) => x.Item1.value == y.Item1.value && x.Item2.value == y.Item2.value,
                    x => RuntimeHelpers.GetHashCode(x.Item1.value) ^ RuntimeHelpers.GetHashCode(x.Item2.value)
                )
            );

            var callRecords = func.getCallHistory();

            for (int i = 0; i < callRecords.Length; i++) {
                ReadOnlySpan<ASAny> callArgs = callRecords[i].getArguments();

                int result = Math.Sign(((double)callRecords[i].returnValue).CompareTo(0.0));
                compareFuncResults[(callArgs[0], callArgs[1])] = result;
                compareFuncResults[(callArgs[1], callArgs[0])] = -result;
            }

            return (x, y) => {
                int res = compareFuncResults[(x, y)];
                if ((sortFlags & ASArray.DESCENDING) != 0)
                    res = -res;
                return res;
            };
        }

        private static void verifyArraySorted(
            Image originalImage,
            ASArray sortedArray,
            Comparison<ASAny> compareFunc,
            bool isUniqueSort,
            bool isReturnIndexedArray
        ) {
            Assert.Equal(originalImage.length, sortedArray.length);

            // Elements used for sorting include those in the array + those in the prototype.
            IndexDict originalElements = new IndexDict(originalImage.elements);
            var currentProto = s_currentProtoProperties.value;

            foreach (var (key, val) in currentProto) {
                if (key < originalImage.length)
                    originalElements.TryAdd(key, val);
            }

            int valueCount = 0;
            int undefinedCount = 0;

            // If RETURNINDEXEDARRAY is not used, we need to verify that the sorted array is a
            // permutation of the original array (i.e. nothing is added or removed). To do this,
            // we create a map of the number of times each distinct object occurs in the original
            // array, and decrease each object's count by 1 when traversing the sorted array.
            // At the end, we assert that all object counts in the map are zero.

            var uniqueObjectCounts = new Dictionary<ASAny, int>(ByRefEqualityComparer.instance);

            foreach (var (key, val) in originalElements) {
                if (val.isUndefined) {
                    undefinedCount++;
                }
                else {
                    valueCount++;
                    if (!isReturnIndexedArray) {
                        uniqueObjectCounts.TryGetValue(val, out int uniqueCount);
                        uniqueObjectCounts[val] = uniqueCount + 1;
                    }
                }
            }

            if (isUniqueSort)
                Assert.True(sortedArray.length - (uint)valueCount <= 1);

            // If RETURNINDEXEDARRAY is set, we use this hashset to verify that every index is unique.
            var visitedIndices = new HashSet<uint>();

            // Run in a zone with a fresh Array prototype so that we can check holes in the sorted array.
            runInZoneWithArrayPrototype(null, () => {
                for (int i = 0; i < (int)originalImage.length; i++) {
                    ASAny value;

                    if (isReturnIndexedArray) {
                        value = sortedArray.AS_getElement(i);
                        Assert.True(ASObject.AS_isUint(value.value));

                        uint index = (uint)value;
                        Assert.True(visitedIndices.Add(index));
                        Assert.True(index < originalImage.length);

                        if (i >= valueCount + undefinedCount)
                            Assert.False(originalElements.ContainsKey(index));
                        else if (i >= valueCount)
                            Assert.True(originalElements[index].isUndefined);
                        else
                            Assert.False(originalElements[index].isUndefined);
                    }
                    else {
                        Assert.Equal(i < valueCount + undefinedCount, sortedArray.AS_hasElement(i));

                        value = sortedArray.AS_getElement(i);
                        Assert.Equal(i < valueCount, value.isDefined);

                        if (value.isDefined)
                            uniqueObjectCounts[value]--;
                    }

                    if (i > 0) {
                        // Compare with the previous value.
                        ASAny prevValue = sortedArray.AS_getElement(i - 1);

                        ASAny cmpLeft, cmpRight;
                        if (isReturnIndexedArray) {
                            originalElements.TryGetValue((uint)prevValue, out cmpLeft);
                            originalElements.TryGetValue((uint)value, out cmpRight);
                        }
                        else {
                            cmpLeft = prevValue;
                            cmpRight = value;
                        }

                        if (isUniqueSort)
                            Assert.True(cmpRight.isUndefined || compareFunc(cmpLeft, cmpRight) < 0);
                        else
                            Assert.True(cmpRight.isUndefined || compareFunc(cmpLeft, cmpRight) <= 0);
                    }
                }

                if (!isReturnIndexedArray) {
                    foreach (int count in uniqueObjectCounts.Values)
                        Assert.Equal(0, count);
                }
            });
        }

        private static void verifyUniqueSortFailed(Image testImage, MockFunctionObject compareFunc, int sortFlags) {
            bool hasEqual = false;

            if (compareFunc != null) {
                // If a user-provided compare function is used, check that at least one call returned 0.
                // [This assumes that the sorting procedure never does compareFunc(arr[i], arr[i])!]

                var callRecords = compareFunc.getCallHistory();
                for (int i = 0; i < callRecords.Length && !hasEqual; i++)
                    hasEqual = (double)callRecords[i].returnValue == 0.0;

                Assert.True(hasEqual);
                return;
            }

            var testImageElements = getArrayElementsFromImage(testImage);
            var compareDelegate = makeArraySortCompareFunc(compareFunc, sortFlags);

            Array.Sort(testImageElements, compareDelegate);

            for (int i = 0; i + 1 < testImageElements.Length && !hasEqual; i++)
                hasEqual = compareDelegate(testImageElements[i], testImageElements[i + 1]) == 0;

            Assert.True(hasEqual);
        }

        public static IEnumerable<object[]> sortMethodTest_data() {
            var random = new Random(483);
            var testcases = new List<(ArrayWrapper, Image, object, IndexDict, bool)>();

            var rndSampledValues = new HashSet<int>();

            int rndNextUnique() {
                while (true) {
                    int next = random.Next();
                    if (rndSampledValues.Add(next))
                        return next;
                }
            }

            double rndNextDoubleUnique() => (double)rndNextUnique() / (double)Int32.MaxValue;

            void addTestCase(
                (ASArray arr, Image img)[] testInstances,
                object[] compareFunctions,
                IndexDict prototype = null,
                bool testAllArgumentVariants = false
            ) {
                foreach (var (arr, img) in testInstances) {
                    foreach (var cmpFn in compareFunctions) {
                        if (cmpFn is Func<ASAny, ASAny, ASAny> del) {
                            var spyFunc = new MockFunctionObject((r, args) => del(args[0], args[1]));
                            testcases.Add((new ArrayWrapper(arr), img, spyFunc, prototype, testAllArgumentVariants));
                        }
                        else {
                            testcases.Add((new ArrayWrapper(arr), img, cmpFn, prototype, testAllArgumentVariants));
                        }
                    }
                }
            }

            Func<ASAny, ASAny, ASAny> comparePropXFunc = (x, y) => {
                if (x.isUndefined && y.isUndefined)
                    return 0;
                if (x.isUndefined)
                    return -1;
                if (y.isUndefined)
                    return 1;
                double dx = (double)x.AS_getProperty("x");
                double dy = (double)y.AS_getProperty("x");
                return dx.CompareTo(dy);
            };

            ASAny makeObjWithPropX(ASAny value) {
                ASObject obj = new ASObject();
                obj.AS_setProperty("x", value);
                return obj;
            }

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(),
                    makeArrayAndImageWithValues(0),
                    makeArrayAndImageWithValues("hello"),

                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 99, i => 0)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 99, i => i)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 99, i => 2000 - i)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 99, i => (int)i - 50)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 99, i => -(int)i)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 199, i => i * 0.001)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 199, i => i * -0.001)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 199, i => ((int)i - 100) * -0.001)),

                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 199, i => rndNextUnique())),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 499, i => rndNextUnique() - Int32.MaxValue / 2)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 199, i => random.Next(50))),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 499, i => Math.CopySign(Math.Exp(rndNextDoubleUnique() * 308), random.NextDouble() - 0.5))),

                    makeArrayAndImageWithValues(300, 3, 20, 1, 20000, 3000, 20, 30, 3000, 100, 2000, 30000, 2),
                    makeArrayAndImageWithValues(Double.NaN, 0, 40, -2, Double.NegativeInfinity, 495, 1E-200, Double.NaN, -1E+223, Double.PositiveInfinity),

                    makeArrayAndImageWithValues("", "a", "aa", "aaa", "aaaa", "ab", "aba", "abb", "ac", "b", "ba", "baaa", "bb"),
                    makeArrayAndImageWithValues("aa", "bbd", "a", "c", "baa", "az", "bac", "cb", "aaa", "bd", "ac", "cac", "abaac"),
                    makeArrayAndImageWithValues("aa", "bbd", "A", "c", "baa", "az", "bAc", "cb", "aaa", "Bd", "ac", "cac", "aC", "abaac", "aAa", "aaA"),

                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 199, i => RandomHelper.randomString(random, 0, 12, 'a', 'z'))),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 199, i => RandomHelper.randomString(random, 1, 3, 'a', 'c'))),

                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 199, i =>
                        (rndNextUnique() - Int32.MaxValue / 2).ToString(CultureInfo.InvariantCulture)
                    )),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 199, i =>
                        Math.CopySign(Math.Exp(rndNextDoubleUnique() * 308), random.NextDouble() - 0.5).ToString(CultureInfo.InvariantCulture)
                    )),

                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 199, i =>
                        (random.Next(5) == 0) ? ASAny.undefined : rndNextDoubleUnique()
                    )),

                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 199, i =>
                        (random.Next(5) == 0) ? ASAny.undefined : RandomHelper.randomString(random, 3, 5, 'a', 'c')
                    )),

                    makeArrayAndImageWithValues(
                        0, "3", 4.0, "19384", -183, "7.34190", "0.1", "0.2", 0.1 + 0.2, "4", Double.NaN, 19384u, "-99999.21", "Infinity", 44, "NaN", "-Infinity"
                    ),
                    makeArrayAndImageWithValues(
                        "3", 4.0, "19384", ASAny.undefined, -183, "7.34190", "0.1", "0.2", ASAny.@null, 0.1 + 0.2, "4", Double.NaN, 19384u, "-99999.21", "Infinity", 44, "NaN", "-Infinity"
                    ),
                    makeArrayAndImageWithValues(
                        0, "3", 4.0, "19384", ASAny.undefined, -183, "7.34190", "0.1", "0.2", ASAny.@null, 0.1 + 0.2, "4", Double.NaN, 19384u, "-99999.21", "Infinity", 44, "NaN", "-Infinity"
                    ),
                    makeArrayAndImageWithValues(
                        "null", "3", 4.0, "19384", ASAny.undefined, -183, "7.34190", "0.1", "0.2", ASAny.@null, 0.1 + 0.2, "4", Double.NaN, 19384u, "-99999.21", "Infinity", 44, "NaN", "-Infinity"
                    ),
                    makeArrayAndImageWithValues(
                        0, "3", 4.0, "19384", ASAny.undefined, -183, "7.34190",
                        "0.1", "0.2", 0.1 + 0.2, "4", Double.NaN, ASAny.@null, 19384u, "-99999.21",
                        ASAny.undefined, "Infinity", 44, "NaN", ASAny.undefined, "-Infinity"
                    ),

                    makeArrayAndImageWithValues(ASAny.undefined, ASAny.@null),
                    makeArrayAndImageWithValues(ASAny.undefined, ASAny.@null, "undefined"),
                    makeArrayAndImageWithValues(ASAny.undefined, ASAny.undefined),
                    makeArrayAndImageWithValues(ASAny.undefined, "undefined"),
                    makeArrayAndImageWithValues(ASAny.undefined, "a", "undefined"),
                    makeArrayAndImageWithValues(ASAny.undefined, "z", "undefined"),
                    makeArrayAndImageWithValues(ASAny.undefined, ASAny.@null, "undefined"),
                    makeArrayAndImageWithValues(ASAny.undefined, "UNDEFINED"),
                    makeArrayAndImageWithValues(ASAny.@null, "null"),
                    makeArrayAndImageWithValues(ASAny.@null, "Null"),
                },
                compareFunctions: new object[] {
                    0,
                    ASArray.CASEINSENSITIVE,
                    ASArray.NUMERIC
                },
                testAllArgumentVariants: true
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithValues(
                        rangeSelect<ASAny>(0, 99, i => Math.CopySign(Math.Exp(rndNextDoubleUnique() * 308), random.NextDouble() - 0.5))
                    ),
                    makeArrayAndImageWithValues(
                        rangeSelect<ASAny>(0, 99, i => Math.CopySign(Math.Exp(rndNextDoubleUnique() * 308), random.NextDouble() - 0.5).ToString(CultureInfo.InvariantCulture))
                    ),
                    makeArrayAndImageWithValues(
                        0, "3", 4.0, "19384", -183, "7.34190", "0.1", "0.2", 0.1 + 0.2, "4", Double.NaN, 19384u, "-99999.21", "Infinity", 44, "NaN", "-Infinity"
                    ),
                    makeArrayAndImageWithValues(
                        0, "3", 4.0, "19384", ASAny.undefined, -183, "7.34190", "0.1", "0.2", 0.1 + 0.2, "4", Double.NaN, 19384u, "-99999.21", "Infinity", 44, "NaN", "-Infinity"
                    ),
                    makeArrayAndImageWithValues(
                        0, "3", 4.0, "19384", ASAny.undefined, -183, "7.34190",
                        "0.1", "0.2", 0.1 + 0.2, "4", Double.NaN, 19384u, "-99999.21",
                        ASAny.undefined, "Infinity", 44, "NaN", ASAny.undefined, "-Infinity"
                    ),
                },
                compareFunctions: new object[] {
                    0,
                    ASArray.NUMERIC,
                    new Func<ASAny, ASAny, ASAny>((x, y) => 0),
                    new Func<ASAny, ASAny, ASAny>((x, y) => {
                        if (ASObject.AS_isNumeric(x.value) && !ASObject.AS_isNumeric(y.value))
                            return -1;
                        if (!ASObject.AS_isNumeric(x.value) && ASObject.AS_isNumeric(y.value))
                            return 1;
                        if (ASObject.AS_isNumeric(x.value))
                            return ((double)x).CompareTo((double)y);
                        return String.CompareOrdinal((string)x, (string)y);
                    })
                },
                testAllArgumentVariants: true
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 99, i => new ConvertibleMockObject(
                        numberValue: rndNextDoubleUnique(),
                        stringValue: RandomHelper.randomString(random, 3, 5, 'a', 'd')
                    ))),
                    makeArrayAndImageWithValues(
                        repeat<ASAny>(new ConvertibleMockObject(numberValue: 1.2, stringValue: "hello"), 1)
                    ),
                    makeArrayAndImageWithValues(
                        repeat<ASAny>(new ConvertibleMockObject(numberValue: 1.2, stringValue: "hello"), 20)
                    ),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 99, i => {
                        switch (random.Next(7)) {
                            case 0:
                                return ASAny.undefined;
                            case 1:
                                return ASAny.@null;
                        }

                        string strval = RandomHelper.randomString(random, 3, 5, 'a', 'c');
                        if (random.Next(2) == 0)
                            strval = strval.ToUpperInvariant();

                        return new ConvertibleMockObject(numberValue: random.NextDouble(), stringValue: strval);
                    })),
                },
                compareFunctions: new object[] {
                    0,
                    ASArray.NUMERIC,
                    ASArray.CASEINSENSITIVE
                }
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 69, i => makeObjWithPropX(i))),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 69, i => makeObjWithPropX(i % 68))),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 69, i => makeObjWithPropX(rndNextDoubleUnique()))),

                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 69, i =>
                        (i % 10 == 3) ? ASAny.undefined : makeObjWithPropX(rndNextUnique())
                    ))
                },
                compareFunctions: new object[] {
                    0,
                    comparePropXFunc,
                    new Func<ASAny, ASAny, ASAny>((x, y) => {
                        if (x.isUndefined && y.isUndefined)
                            return 0;
                        if (x.isUndefined)
                            return 1;
                        if (y.isUndefined)
                            return -1;
                        double dx = (double)x.AS_getProperty("x");
                        double dy = (double)y.AS_getProperty("x");
                        int sign = dx.CompareTo(dy);
                        return (sign == 0) ? 0 : Math.CopySign(0.00001, sign);
                    }),
                    new Func<ASAny, ASAny, ASAny>((x, y) => {
                        if (x.isUndefined && y.isUndefined)
                            return 0;
                        if (x.isUndefined)
                            return 1;
                        if (y.isUndefined)
                            return -1;
                        double dx = (double)x.AS_getProperty("x");
                        double dy = (double)y.AS_getProperty("x");
                        int sign = dx.CompareTo(dy);
                        return (sign == 0) ? 0 : Math.CopySign(Double.PositiveInfinity, sign);
                    }),
                },
                testAllArgumentVariants: true
            );

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(1),
                    makeEmptyArrayAndImage(2),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutPushOne(rndNextUnique()))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutPushOne(rndNextUnique())),
                        mutSetLength(31)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutPushOne(random.Next(6))),
                        mutSetLength(31)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(1, 30, i => mutSetUint(i, rndNextUnique()))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutSetUint(i, rndNextUnique())),
                        mutSetUint(30, ASAny.undefined)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutSetUint(i, random.Next(6))),
                        mutSetUint(30, ASAny.undefined)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 30, i => mutSetUint(i, rndNextUnique())),
                        mutSetUint(14, ASAny.undefined)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutPushOne(rndNextUnique())),
                        mutSetLength(32)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(0, ASAny.undefined),
                        rangeSelect(1, 31, i => mutSetUint(i, rndNextUnique())),
                        mutSetUint(23, ASAny.undefined)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutSetUint(i, rndNextUnique())),
                        mutSetUint(30, ASAny.undefined),
                        mutSetLength(32)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutPushOne(rndNextUnique())),
                        mutSetLength(80)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 39, i => mutPushOne((random.Next(4) == 0) ? ASAny.undefined : rndNextUnique())),
                        mutSetLength(50)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 39, i => mutPushOne(rndNextUnique())),
                        mutDelUint(14)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 39, i => mutPushOne(rndNextUnique())),
                        mutDelUint(14),
                        mutDelUint(23)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 39, i => mutSetUint((uint)random.Next(50), rndNextUnique()))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 39, i => mutSetUint((uint)random.Next(50), rndNextUnique()))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutSetUint(i, rndNextUnique())),
                        rangeSelect(40, 59, i => mutSetUint(i, rndNextUnique()))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(2, "a"),
                        mutSetUint(3, ASAny.undefined),
                        mutSetUint(5, "daa"),
                        mutSetUint(7, "bcc"),
                        mutSetUint(8, "AaB"),
                        mutSetUint(9, ASAny.undefined),
                        mutSetUint(11, "Cc"),
                        mutSetUint(12, "aAb"),
                        mutSetUint(14, "cc"),
                        mutSetUint(16, "b"),
                        mutSetUint(17, "B"),
                        mutSetUint(18, "cBB"),
                        mutSetUint(20, ASAny.undefined)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutPushOne(ASAny.undefined),
                        mutSetLength(2)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutPushOne("undefined"),
                        mutSetLength(2)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutPushOne("UNDEFINED"),
                        mutSetLength(2)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutPushOne(ASAny.@null),
                        mutSetLength(2)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutPushOne(ASAny.@null),
                        mutSetLength(3)
                    )),
                },
                compareFunctions: new object[] {
                    0,
                    ASArray.NUMERIC,
                    ASArray.CASEINSENSITIVE,
                    new Func<ASAny, ASAny, ASAny>((x, y) => String.CompareOrdinal((string)x, (string)y))
                }
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 39, i => mutSetUint(i, rndNextUnique())),
                        rangeSelect(100, 139, i => mutSetUint(i, rndNextUnique()))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 49, i => mutSetUint((uint)random.Next(150), i))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 49, i => mutSetUint((uint)random.Next(150), rndNextDoubleUnique()))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 49, i => mutSetUint(
                            (uint)random.Next(150),
                            (random.Next(5) == 0) ? ASAny.undefined : rndNextUnique()
                        ))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 49, i => mutSetUint((uint)random.Next(300), rndNextUnique().ToString(CultureInfo.InvariantCulture)))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 49, i => mutSetUint(
                            (uint)random.Next(300),
                            new ConvertibleMockObject(
                                numberValue: rndNextDoubleUnique(),
                                stringValue: RandomHelper.randomString(random, 2, 6, 'a', 'd')
                            )
                        )),
                        mutSetLength(300)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 49, i => mutSetUint((uint)random.Next(300), new ConvertibleMockObject(numberValue: 1.0, stringValue: ""))),
                        mutSetLength(300)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(2, "a"),
                        mutSetUint(7, ASAny.undefined),
                        mutSetUint(14, "daa"),
                        mutSetUint(16, "bcc"),
                        mutSetUint(19, "AaB"),
                        mutSetUint(28, ASAny.undefined),
                        mutSetUint(38, "Cc"),
                        mutSetUint(41, "aAb"),
                        mutSetUint(45, "cc"),
                        mutSetUint(53, "b"),
                        mutSetUint(65, "B"),
                        mutSetUint(67, "cBB"),
                        mutSetUint(78, ASAny.undefined)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(46, ASAny.undefined)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(46, ASAny.undefined),
                        mutSetUint(71, ASAny.@null),
                        mutSetUint(89, ASAny.undefined)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(35, ASAny.undefined),
                        mutSetUint(57, ASAny.@null),
                        mutSetUint(73, "null"),
                        mutSetUint(106, ASAny.undefined),
                        mutSetUint(138, "undefined")
                    )),
                },
                compareFunctions: new object[] {0, ASArray.NUMERIC, ASArray.CASEINSENSITIVE}
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 39, i => mutSetUint(i, makeObjWithPropX(rndNextUnique()))),
                        mutSetLength(41)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 39, i => mutSetUint(i, makeObjWithPropX(random.Next(8)))),
                        mutSetLength(41)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutSetUint(i, makeObjWithPropX(rndNextUnique()))),
                        mutSetLength(100)
                    )),

                    makeArrayAndImageWithMutations(rangeSelect(0, 39, i => mutSetUint((uint)random.Next(50), makeObjWithPropX(i % 37)))),
                    makeArrayAndImageWithMutations(rangeSelect(0, 39, i => mutSetUint((uint)random.Next(50), makeObjWithPropX(rndNextUnique())))),
                    makeArrayAndImageWithMutations(rangeSelect(0, 39, i => mutSetUint((uint)random.Next(200), makeObjWithPropX(i % 37)))),
                    makeArrayAndImageWithMutations(rangeSelect(0, 39, i => mutSetUint((uint)random.Next(200), makeObjWithPropX(rndNextUnique())))),

                    makeArrayAndImageWithMutations(rangeSelect(0, 39, i =>
                        mutSetUint((uint)random.Next(50), (i % 6 == 2) ? ASAny.undefined : makeObjWithPropX(rndNextUnique()))
                    )),
                    makeArrayAndImageWithMutations(rangeSelect(0, 39, i =>
                        mutSetUint((uint)random.Next(200), (i % 6 == 2) ? ASAny.undefined : makeObjWithPropX(rndNextUnique()))
                    )),
                },
                compareFunctions: new object[] {comparePropXFunc}
            );

            addTestCase(
                testInstances: new[] {makeEmptyArrayAndImage(2)},
                compareFunctions: new object[] {0},
                prototype: new IndexDict {[1] = "hello"}
            );

            addTestCase(
                testInstances: new[] {makeEmptyArrayAndImage(2)},
                compareFunctions: new object[] {0},
                prototype: new IndexDict {[1] = "undefined"}
            );

            addTestCase(
                testInstances: new[] {makeEmptyArrayAndImage(2)},
                compareFunctions: new object[] {ASArray.NUMERIC},
                prototype: new IndexDict {[1] = 123}
            );

            addTestCase(
                testInstances: new[] {makeEmptyArrayAndImage(2)},
                compareFunctions: new object[] {0, ASArray.NUMERIC},
                prototype: new IndexDict {[1] = ASAny.undefined}
            );

            addTestCase(
                testInstances: new[] {makeEmptyArrayAndImage(3)},
                compareFunctions: new object[] {0, ASArray.NUMERIC},
                prototype: new IndexDict {[1] = 1, [2] = 2}
            );

            addTestCase(
                testInstances: new[] {makeEmptyArrayAndImage(4)},
                compareFunctions: new object[] {0, ASArray.NUMERIC},
                prototype: new IndexDict {[2] = 1, [3] = 2}
            );

            addTestCase(
                testInstances: new[] {makeEmptyArrayAndImage(2)},
                compareFunctions: new object[] {comparePropXFunc},
                prototype: new IndexDict {[1] = makeObjWithPropX(1)}
            );

            addTestCase(
                testInstances: new[] {makeEmptyArrayAndImage(4)},
                compareFunctions: new object[] {comparePropXFunc},
                prototype: new IndexDict {[2] = makeObjWithPropX(1), [3] = makeObjWithPropX(2)}
            );

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(0),
                    makeEmptyArrayAndImage(10),
                    makeEmptyArrayAndImage(30),
                    makeEmptyArrayAndImage(31),
                    makeEmptyArrayAndImage(40),

                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 19, i => rndNextUnique())),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 29, i => rndNextUnique())),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 39, i => rndNextUnique())),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 19, i => random.Next(4))),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetLength(80),
                        rangeSelect(25, 79, i => mutSetUint(i, rndNextUnique()))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetLength(40),
                        rangeSelect(30, 39, i => mutSetUint(i, RandomHelper.randomString(random, 0, 5, '0', '9')))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetLength(50),
                        rangeSelect(30, 39, i => mutSetUint(i, RandomHelper.randomString(random, 1, 10, 'a', 'c'))),
                        rangeSelect(40, 49, i => mutSetUint(i, RandomHelper.randomString(random, 1, 10, 'A', 'C')))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetLength(100),
                        rangeSelect(0, 39, i => mutSetUint((uint)random.Next(100), rndNextUnique()))
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutSetUint(i, rndNextUnique())),
                        mutSetLength(31)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutSetUint(i, rndNextUnique())),
                        mutSetLength(32)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutSetUint(i, rndNextUnique())),
                        mutSetUint(17, ASAny.undefined)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutSetUint(i, rndNextUnique())),
                        mutSetUint(17, ASAny.undefined),
                        mutSetLength(31)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(1, "a"),
                        mutSetUint(2, ASAny.undefined),
                        mutSetUint(4, "daa"),
                        mutSetUint(5, "bcc"),
                        mutSetUint(7, "AaB"),
                        mutSetUint(8, ASAny.undefined),
                        mutSetUint(10, "Cc"),
                        mutSetUint(11, "aAb"),
                        mutSetUint(12, "cc"),
                        mutSetUint(14, "b"),
                        mutSetUint(17, "B"),
                        mutSetUint(18, "cBB"),
                        mutSetUint(20, ASAny.undefined)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 4, i => mutSetUint(i, rndNextUnique())),
                        rangeSelect(30, 34, i => mutSetUint(i, rndNextUnique()))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 4, i => mutSetUint(i, rndNextUnique())),
                        rangeSelect(31, 35, i => mutSetUint(i, rndNextUnique()))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 4, i => mutSetUint(i, rndNextUnique())),
                        rangeSelect(32, 36, i => mutSetUint(i, rndNextUnique()))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 4, i => mutSetUint(i, "a" + rndNextUnique().ToString(CultureInfo.InvariantCulture))),
                        rangeSelect(31, 35, i => mutSetUint(i, "a" + rndNextUnique().ToString(CultureInfo.InvariantCulture))),
                        mutSetUint(12, "Undefined")
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 4, i => mutSetUint(i, "a" + rndNextUnique().ToString(CultureInfo.InvariantCulture))),
                        rangeSelect(31, 35, i => mutSetUint(i, "a" + rndNextUnique().ToString(CultureInfo.InvariantCulture))),
                        mutSetUint(12, Double.NaN)
                    )),
                },
                prototype: new IndexDict(rangeSelect(0, 29, i => KeyValuePair.Create(i, (ASAny)rndNextUnique()))),
                compareFunctions: new object[] {0, ASArray.NUMERIC, ASArray.CASEINSENSITIVE}
            );

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(0),
                    makeEmptyArrayAndImage(10),
                    makeEmptyArrayAndImage(30),
                },
                compareFunctions: new object[] {0},
                prototype: new IndexDict(rangeSelect(0, 25, i => KeyValuePair.Create(i, (ASAny)new string((char)('a' + i), 1))))
            );

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(0),
                    makeEmptyArrayAndImage(10),
                    makeEmptyArrayAndImage(30),

                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 29, i => rndNextDoubleUnique())),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 29, i => 193843)),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 9, i => rndNextUnique())),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetLength(30),
                        rangeSelect(0, 4, i => mutSetUint((uint)random.Next(30), rndNextUnique())),
                        rangeSelect(0, 4, i => mutSetUint((uint)random.Next(30), new ConvertibleMockObject(
                            numberValue: rndNextDoubleUnique(),
                            stringValue: "aaaa"
                        )))
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetLength(31),
                        rangeSelect(0, 9, i => mutSetUint((uint)random.Next(30), rndNextUnique()))
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetLength(34),
                        rangeSelect(0, 9, i => mutSetUint((uint)random.Next(30), rndNextUnique()))
                    )),
                },
                prototype: new IndexDict(
                    rangeSelect(0, 29, i => KeyValuePair.Create(i, (ASAny)new ConvertibleMockObject(
                        numberValue: random.Next(15) - 8,
                        stringValue: RandomHelper.randomString(random, 1, 10, 'a', 'z')
                    )))
                ),
                compareFunctions: new object[] {
                    0,
                    ASArray.NUMERIC,
                    ASArray.CASEINSENSITIVE,
                    new Func<ASAny, ASAny, ASAny>((x, y) => 0)
                },
                testAllArgumentVariants: true
            );

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(),
                    makeEmptyArrayAndImage(10),
                    makeEmptyArrayAndImage(30),
                    makeEmptyArrayAndImage(31),
                    makeEmptyArrayAndImage(35),

                    makeArrayAndImageWithMutations(rangeSelect(0, 29, i => mutSetUint(i, makeObjWithPropX(rndNextUnique())))),
                    makeArrayAndImageWithMutations(rangeSelect(0, 9, i => mutSetUint((uint)random.Next(30), makeObjWithPropX(random.Next(4))))),
                    makeArrayAndImageWithMutations(rangeSelect(0, 9, i => mutSetUint((uint)random.Next(30), makeObjWithPropX(rndNextUnique())))),
                },
                prototype: new IndexDict(rangeSelect(0, 29, i => KeyValuePair.Create(i, makeObjWithPropX(rndNextUnique())))),
                compareFunctions: new object[] {comparePropXFunc}
            );

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(30),
                    makeEmptyArrayAndImage(35),

                    makeArrayAndImageWithMutations(rangeSelect(0, 19, i => mutSetUint(i, makeObjWithPropX(rndNextUnique())))),
                    makeArrayAndImageWithMutations(rangeSelect(0, 29, i => mutSetUint(i, makeObjWithPropX(rndNextUnique())))),
                    makeArrayAndImageWithMutations(rangeSelect(0, 9, i => mutSetUint((uint)random.Next(30), makeObjWithPropX(rndNextUnique())))),
                },
                prototype: new IndexDict(rangeSelect(0, 29, i => KeyValuePair.Create(i, makeObjWithPropX(i / 2)))),
                compareFunctions: new object[] {comparePropXFunc},
                testAllArgumentVariants: true
            );

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(10),
                    makeEmptyArrayAndImage(28),

                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 20, i => rndNextUnique())),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 21, i => rndNextUnique())),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 22, i => RandomHelper.randomString(random, 5, 10, 'A', 'Z'))),
                    makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 23, i => rndNextUnique())),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 22, i => mutSetUint(i, rndNextUnique() + 1000000)),
                        mutSetLength(24)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 21, i => mutSetUint(i, rndNextUnique() + 1000000)),
                        mutSetLength(24)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 22, i => mutSetUint(i, rndNextUnique() + 1000000)),
                        mutSetLength(25)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 21, i => mutSetUint(i, rndNextUnique() + 1000000)),
                        mutSetUint(16, ASAny.undefined),
                        mutSetUint(24, ASAny.undefined)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 21, i => mutSetUint(i, rndNextUnique() + 1000000)),
                        mutSetUint(24, "undefined")
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 9, i => mutSetUint(i, ASAny.undefined)),
                        mutSetLength(30)
                    )),

                    makeArrayAndImageWithMutations(rangeSelect(0, 9, i => mutSetUint((uint)random.Next(40), rndNextDoubleUnique()))),
                    makeArrayAndImageWithMutations(rangeSelect(0, 9, i => mutSetUint((uint)random.Next(40), rndNextUnique()))),
                    makeArrayAndImageWithMutations(rangeSelect(0, 29, i => mutSetUint((uint)random.Next(100), RandomHelper.randomString(random, 1, 4, 'a', 'c'))))
                },
                prototype: new IndexDict {
                    [2] = 134,
                    [9] = "0019483",
                    [14] = 1388,
                    [16] = -24821,
                    [23] = "+393211",
                    [26] = ASAny.undefined
                },
                compareFunctions: new object[] {0, ASArray.NUMERIC, ASArray.CASEINSENSITIVE}
            );

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(10),
                    makeEmptyArrayAndImage(28),

                    makeArrayAndImageWithValues(rangeSelect(0, 20, i => makeObjWithPropX(rndNextDoubleUnique()))),
                    makeArrayAndImageWithValues(rangeSelect(0, 21, i => makeObjWithPropX(rndNextUnique()))),
                    makeArrayAndImageWithValues(rangeSelect(0, 22, i => makeObjWithPropX(random.Next(8)))),
                    makeArrayAndImageWithValues(rangeSelect(0, 21, i => makeObjWithPropX(rndNextUnique()))),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 22, i => mutSetUint(i, makeObjWithPropX(rndNextUnique() + 1000000))),
                        mutSetLength(24)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 21, i => mutSetUint(i, makeObjWithPropX(rndNextUnique() + 1000000))),
                        mutSetLength(24)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 22, i => mutSetUint(i, makeObjWithPropX(rndNextUnique() + 1000000))),
                        mutSetLength(25)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 21, i => mutSetUint(i, makeObjWithPropX(rndNextUnique() + 1000000))),
                        mutSetUint(24, ASAny.undefined)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 9, i => mutSetUint(i, ASAny.undefined)),
                        mutSetLength(30)
                    )),

                    makeArrayAndImageWithMutations(
                        rangeSelect(0, 9, i => mutSetUint((uint)random.Next(40), makeObjWithPropX(rndNextUnique())))
                    ),
                },
                prototype: new IndexDict {
                    [2] = makeObjWithPropX(134),
                    [9] = makeObjWithPropX("0019483"),
                    [14] = makeObjWithPropX(1388),
                    [16] = makeObjWithPropX(-24821),
                    [23] = makeObjWithPropX("+393211"),
                    [26] = ASAny.undefined
                },
                compareFunctions: new object[] {comparePropXFunc}
            );

            var cultureCompareInfo = (new CultureInfo("en-US")).CompareInfo;
            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithValues("encyclopædia", "Archæology", "ARCHÆOLOGY", "encyclopaedia", "ARCHAEOLOGY")
                },
                compareFunctions: new object[] {
                    0,
                    ASArray.CASEINSENSITIVE,
                    new Func<ASAny, ASAny, ASAny>((x, y) => cultureCompareInfo.Compare((string)x, (string)y)),
                    new Func<ASAny, ASAny, ASAny>((x, y) => cultureCompareInfo.Compare((string)x, (string)y, CompareOptions.IgnoreCase))
                }
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(sortMethodTest_data))]
        public void sortMethodTest(
            ArrayWrapper array, Image image, object compareTypeOrFunction, IndexDict prototype, bool testAllArgumentVariants)
        {
            int[] additionalFlags = {
                0,
                ASArray.UNIQUESORT,
                ASArray.RETURNINDEXEDARRAY,
                ASArray.DESCENDING,
                ASArray.UNIQUESORT | ASArray.RETURNINDEXEDARRAY,
                ASArray.DESCENDING | ASArray.UNIQUESORT,
                ASArray.DESCENDING | ASArray.RETURNINDEXEDARRAY,
                ASArray.DESCENDING | ASArray.UNIQUESORT | ASArray.RETURNINDEXEDARRAY,
            };

            runInZoneWithArrayPrototype(prototype, () => {
                setRandomSeed(39189);

                if (compareTypeOrFunction is MockFunctionObject func) {
                    for (int i = 0; i < additionalFlags.Length; i++)
                        runTestWithCompareFuncAndFlags(func, additionalFlags[i]);
                }
                else {
                    int cmpTypeFlag = (int)compareTypeOrFunction;
                    for (int i = 0; i < additionalFlags.Length; i++)
                        runTestWithCompareFuncAndFlags(null, cmpTypeFlag | additionalFlags[i]);
                }
            });

            void runTestWithCompareFuncAndFlags(MockFunctionObject compareFunc, int flags) {
                bool isUniqueSort = (flags & ASArray.UNIQUESORT) != 0;
                bool isReturnIndexedArray = (flags & ASArray.RETURNINDEXEDARRAY) != 0;

                foreach (var (callArgs, compareFnClone) in generateArgumentVariants(compareFunc, flags)) {
                    ASArray testArray = array.instance.clone();
                    ASAny returnVal = testArray.sort(new RestParam(callArgs));

                    if (compareFnClone != null) {
                        var callRecords = compareFnClone.getCallHistory();
                        for (int i = 0; i < callRecords.Length; i++) {
                            Assert.False(callRecords[i].isConstruct);
                            Assert.Equal(2, callRecords[i].getArguments().Length);
                        }
                    }

                    if (isUniqueSort && ASAny.AS_strictEq(returnVal, 0)) {
                        verifyArrayMatchesImage(testArray, image);
                        verifyUniqueSortFailed(image, compareFnClone, flags);
                        continue;
                    }

                    if (isReturnIndexedArray) {
                        Assert.IsType<ASArray>(returnVal.value);
                        Assert.NotSame(returnVal.value, testArray);
                        verifyArrayMatchesImage(testArray, image);
                    }
                    else {
                        Assert.Same(returnVal.value, testArray);
                    }

                    verifyArraySorted(
                        image,
                        (ASArray)returnVal.value,
                        makeArraySortCompareFunc(compareFnClone, flags),
                        isUniqueSort,
                        isReturnIndexedArray
                    );
                }
            }

            IEnumerable<(ASAny[], MockFunctionObject)> generateArgumentVariants(MockFunctionObject compareFunc, int flags) {
                List<(ASAny[], MockFunctionObject)> arglists = new List<(ASAny[], MockFunctionObject)>();

                if (compareFunc != null) {
                    // We need to clone the SpyFunctionObject so that each one has its own list of tracked calls.

                    if (flags == 0) {
                        compareFunc = compareFunc.clone();
                        arglists.Add((new ASAny[] {compareFunc}, compareFunc));
                    }

                    compareFunc = compareFunc.clone();
                    arglists.Add((new ASAny[] {compareFunc, flags}, compareFunc));

                    if (testAllArgumentVariants) {
                        compareFunc = compareFunc.clone();
                        arglists.Add((new ASAny[] {compareFunc, flags | ASArray.NUMERIC}, compareFunc));

                        compareFunc = compareFunc.clone();
                        arglists.Add((new ASAny[] {compareFunc, flags | ASArray.CASEINSENSITIVE}, compareFunc));

                        compareFunc = compareFunc.clone();
                        arglists.Add((new ASAny[] {compareFunc, flags | 32}, compareFunc));

                        compareFunc = compareFunc.clone();
                        arglists.Add((new ASAny[] {compareFunc, (uint)flags}, compareFunc));

                        compareFunc = compareFunc.clone();
                        arglists.Add((new ASAny[] {compareFunc, (double)flags}, compareFunc));

                        compareFunc = compareFunc.clone();
                        arglists.Add((new ASAny[] {compareFunc, flags, ASAny.undefined}, compareFunc));

                        compareFunc = compareFunc.clone();
                        arglists.Add((new ASAny[] {compareFunc, flags, new ASObject()}, compareFunc));
                    }
                }
                else {
                    if (flags == 0)
                        arglists.Add((Array.Empty<ASAny>(), null));

                    arglists.Add((new ASAny[] {flags}, null));

                    if (testAllArgumentVariants) {
                        arglists.Add((new ASAny[] {(uint)flags}, null));
                        arglists.Add((new ASAny[] {(double)flags}, null));
                        arglists.Add((new ASAny[] {flags | 32}, null));

                        if ((flags & ASArray.NUMERIC) != 0) {
                            // NUMERIC should have priority over CASEINSENSITIVE
                            arglists.Add((new ASAny[] {flags | ASArray.CASEINSENSITIVE}, null));
                        }

                        arglists.Add((new ASAny[] {flags, ASAny.undefined}, null));
                        arglists.Add((new ASAny[] {flags, "abc", "abc"}, null));
                    }
                }

                return arglists;
            }
        }

        public static IEnumerable<object[]> sortMethodTest_arrayLengthTooBig_data() {
            var testcases = new List<(ArrayWrapper, Image)>();

            void addTestCase((ASArray arr, Image img) testInstance) =>
                testcases.Add((new ArrayWrapper(testInstance.arr), testInstance.img));

            addTestCase(makeEmptyArrayAndImage((uint)Int32.MaxValue + 1));

            addTestCase(makeEmptyArrayAndImage(UInt32.MaxValue));

            addTestCase(makeArrayAndImageWithMutations(buildMutationList(
                mutSetUint(0, 2),
                mutSetUint((uint)Int32.MaxValue + 1, 1)
            )));

            addTestCase(makeArrayAndImageWithMutations(buildMutationList(
                mutSetUint(UInt32.MaxValue - 1, 1)
            )));

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(sortMethodTest_arrayLengthTooBig_data))]
        public void sortMethodTest_arrayLengthTooBig(ArrayWrapper array, Image image) {
            int[] flags = {
                0,
                ASArray.NUMERIC,
                ASArray.CASEINSENSITIVE,
                ASArray.UNIQUESORT,
                ASArray.DESCENDING,
                ASArray.RETURNINDEXEDARRAY,
                ASArray.NUMERIC | ASArray.DESCENDING,
                ASArray.NUMERIC | ASArray.RETURNINDEXEDARRAY,
                ASArray.CASEINSENSITIVE | ASArray.DESCENDING | ASArray.RETURNINDEXEDARRAY,
                ASArray.NUMERIC | ASArray.UNIQUESORT | ASArray.DESCENDING,
                ASArray.NUMERIC | ASArray.CASEINSENSITIVE | ASArray.DESCENDING | ASArray.UNIQUESORT | ASArray.RETURNINDEXEDARRAY,
            };

            runInZoneWithArrayPrototype(null, () => {
                setRandomSeed(9471);

                testWithArgs(Array.Empty<ASAny>());
                testWithArgs(new ASAny[] {new MockFunctionObject()});

                for (int i = 0; i < flags.Length; i++) {
                    testWithArgs(new ASAny[] {flags[i]});
                    testWithArgs(new ASAny[] {new MockFunctionObject(), flags[i]});
                }
            });

            void testWithArgs(ASAny[] args) {
                ASAny returnVal = array.instance.sort(new RestParam(args));
                Assert.Same(array.instance, returnVal.value);
                verifyArrayMatchesImage(array.instance, image);
            }
        }

        public static IEnumerable<object[]> sortMethodTest_invalidArguments_data() {
            var testcases = new List<(ArrayWrapper, Image)>();

            void addTestCase((ASArray arr, Image img) testInstance) =>
                testcases.Add((new ArrayWrapper(testInstance.arr), testInstance.img));

            addTestCase(makeEmptyArrayAndImage());
            addTestCase(makeEmptyArrayAndImage(30));
            addTestCase(makeArrayAndImageWithValues(rangeSelect<ASAny>(0, 29, i => i)));
            addTestCase(makeArrayAndImageWithMutations(rangeSelect(0, 29, i => mutSetUint(i * 100, i))));
            addTestCase(makeEmptyArrayAndImage(UInt32.MaxValue));

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(sortMethodTest_invalidArguments_data))]
        public void sortMethodTest_invalidArguments(ArrayWrapper array, Image image) {
            ASAny[][] args = {
                new ASAny[] {ASAny.undefined},
                new ASAny[] {ASAny.@null},
                new ASAny[] {"0"},
                new ASAny[] {new ASObject()},
                new ASAny[] {new MockFunctionObject(), ASAny.undefined},
                new ASAny[] {new MockFunctionObject(), ASAny.@null},
                new ASAny[] {new MockFunctionObject(), "2"},
                new ASAny[] {new MockFunctionObject(), new ASObject()},
            };

            runInZoneWithArrayPrototype(null, () => {
                setRandomSeed(18378);
                for (int i = 0; i < args.Length; i++) {
                    AssertHelper.throwsErrorWithCode(
                        ErrorCode.TYPE_COERCION_FAILED,
                        () => array.instance.sort(new RestParam(args[i]))
                    );
                    verifyArrayMatchesImage(array.instance, image);
                }
            });
        }

        private static Comparison<ASAny> makeArraySortOnCompareFunc(string[] names, int[] flags) {
            return (x, y) => {
                if (x.isUndefinedOrNull)
                    return y.isUndefinedOrNull ? 0 : 1;
                if (y.isUndefinedOrNull)
                    return -1;

                int cmpResult = 0;
                for (int i = 0; i < names.Length && cmpResult == 0; i++) {
                    ASAny xval = x.AS_getProperty(QName.publicName(names[i]));
                    ASAny yval = y.AS_getProperty(QName.publicName(names[i]));
                    cmpResult = compareWithSortFlags(xval, yval, flags[i]);
                }
                return cmpResult;
            };
        }

        private static void verifyArraySortedOnProperties(
            Image originalImage,
            ASArray sortedArray,
            string[] propNames,
            int[] propFlags,
            bool isUniqueSort,
            bool isReturnIndexedArray
        ) {
            Assert.Equal(originalImage.length, sortedArray.length);

            // Elements used for sorting include those in the array + those in the prototype.
            IndexDict originalElements = new IndexDict(originalImage.elements);
            var currentProto = s_currentProtoProperties.value;

            foreach (var (key, val) in currentProto) {
                if (key < originalImage.length)
                    originalElements.TryAdd(key, val);
            }

            int valueCount = 0;
            int undefAndNullCount = 0;

            // If RETURNINDEXEDARRAY is not used, we need to verify that the sorted array is a
            // permutation of the original array (i.e. nothing is added or removed). To do this,
            // we create a map of the number of times each distinct object occurs in the original
            // array, and decrease each object's count by 1 when traversing the sorted array.
            // At the end, we assert that all object counts in the map are zero.

            var uniqueObjectCounts = new Dictionary<ASAny, int>(ByRefEqualityComparer.instance);

            foreach (var (key, val) in originalElements) {
                if (val.isUndefinedOrNull) {
                    undefAndNullCount++;
                }
                else {
                    valueCount++;
                    if (!isReturnIndexedArray) {
                        uniqueObjectCounts.TryGetValue(val, out int uniqueCount);
                        uniqueObjectCounts[val] = uniqueCount + 1;
                    }
                }
            }

            if (isUniqueSort)
                Assert.True(sortedArray.length - (uint)valueCount <= 1);

            // If RETURNINDEXEDARRAY is set, we use this hashset to verify that every index is unique.
            var visitedIndices = new HashSet<uint>();

            var compareFunc = makeArraySortOnCompareFunc(propNames, propFlags);

            // Run in a zone with a fresh Array prototype so that we can check holes in the sorted array.
            runInZoneWithArrayPrototype(null, () => {
                for (int i = 0; i < (int)originalImage.length; i++) {
                    ASAny value;

                    if (isReturnIndexedArray) {
                        value = sortedArray.AS_getElement(i);
                        Assert.True(ASObject.AS_isUint(value.value));

                        uint index = (uint)value;
                        Assert.True(visitedIndices.Add(index));
                        Assert.True(index < originalImage.length);

                        if (i >= valueCount + undefAndNullCount)
                            Assert.False(originalElements.ContainsKey(index));
                        else if (i >= valueCount)
                            Assert.True(originalElements[index].isUndefinedOrNull);
                        else
                            Assert.False(originalElements[index].isUndefinedOrNull);
                    }
                    else {
                        Assert.Equal(i < valueCount + undefAndNullCount, sortedArray.AS_hasElement(i));

                        value = sortedArray.AS_getElement(i);
                        Assert.Equal(i < valueCount, !value.isUndefinedOrNull);

                        if (!value.isUndefinedOrNull)
                            uniqueObjectCounts[value]--;
                    }

                    if (i > 0) {
                        // Compare with the previous value.
                        ASAny prevValue = sortedArray.AS_getElement(i - 1);

                        ASAny cmpLeft, cmpRight;
                        if (isReturnIndexedArray) {
                            originalElements.TryGetValue((uint)prevValue, out cmpLeft);
                            originalElements.TryGetValue((uint)value, out cmpRight);
                        }
                        else {
                            cmpLeft = prevValue;
                            cmpRight = value;
                        }

                        if (isUniqueSort)
                            Assert.True(cmpRight.isUndefinedOrNull || compareFunc(cmpLeft, cmpRight) < 0);
                        else
                            Assert.True(cmpRight.isUndefinedOrNull || compareFunc(cmpLeft, cmpRight) <= 0);
                    }
                }

                if (!isReturnIndexedArray) {
                    foreach (int count in uniqueObjectCounts.Values)
                        Assert.Equal(0, count);
                }
            });
        }

        private static void verifyUniqueSortOnFailed(Image testImage, string[] propNames, int[] propFlags) {
            bool hasEqual = false;

            var testImageElements = getArrayElementsFromImage(testImage);
            var compareDelegate = makeArraySortOnCompareFunc(propNames, propFlags);

            Array.Sort(testImageElements, compareDelegate);

            for (int i = 0; i + 1 < testImageElements.Length && !hasEqual; i++)
                hasEqual = compareDelegate(testImageElements[i], testImageElements[i + 1]) == 0;

            Assert.True(hasEqual);
        }

        public static IEnumerable<object[]> sortOnMethodTest_data() {
            var random = new Random(483);
            var testcases = new List<(ArrayWrapper, Image, string[], int[], IndexDict, bool)>();

            var rndSampledValues = new HashSet<int>();

            int rndNextUnique() {
                while (true) {
                    int next = random.Next();
                    if (rndSampledValues.Add(next))
                        return next;
                }
            }

            double rndNextDoubleUnique() => (double)rndNextUnique() / (double)Int32.MaxValue;

            ASAny makeDynamicObject(params (string name, ASAny value)[] properties) {
                var obj = new ASObject();
                for (int i = 0; i < properties.Length; i++)
                    obj.AS_setProperty(properties[i].name, properties[i].value);
                return obj;
            }

            ASAny makeTypedObject(ASAny x = default, ASAny y = default, ASAny z = default, ASAny w = default) =>
                new ClassWithProps(x, y, z, w);

            void addTestCase(
                (ASArray arr, Image img)[] testInstances,
                (string name, int flags)[][] properties,
                IndexDict prototype = null,
                bool testAllArgumentVariants = false
            ) {
                foreach (var (arr, img) in testInstances) {
                    foreach (var namesAndFlags in properties) {
                        string[] names = namesAndFlags.Select(x => x.name).ToArray();
                        int[] flags = namesAndFlags.Select(x => x.flags).ToArray();

                        testcases.Add((new ArrayWrapper(arr), img, names, flags, prototype, testAllArgumentVariants));
                    }
                }
            }

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(),

                    makeArrayAndImageWithValues(makeDynamicObject(("foo", 0), ("bar", 1))),

                    makeArrayAndImageWithValues(rangeSelect(0, 39, i =>
                        makeDynamicObject(("foo", i), ("bar", -(int)i))
                    )),

                    makeArrayAndImageWithValues(rangeSelect(0, 39, i =>
                        makeDynamicObject(("foo", i), ("bar", 0))
                    )),

                    makeArrayAndImageWithValues(rangeSelect(0, 39, i =>
                        makeDynamicObject(("foo", 0), ("bar", i))
                    )),

                    makeArrayAndImageWithValues(rangeSelect(0, 39, i =>
                        makeDynamicObject(("foo", 0), ("bar", 0))
                    )),

                    makeArrayAndImageWithValues(
                        repeat(makeDynamicObject(("foo", 0), ("bar", 0)), 39)
                    ),

                    makeArrayAndImageWithValues(rangeSelect(0, 99, i =>
                        makeDynamicObject(("foo", rndNextUnique()), ("bar", rndNextDoubleUnique()))
                    )),

                    makeArrayAndImageWithValues(rangeSelect(0, 99, i =>
                        makeDynamicObject(("foo", rndNextUnique()), ("bar", random.Next(20)))
                    )),

                    makeArrayAndImageWithValues(rangeSelect(0, 99, i =>
                        makeDynamicObject(("foo", random.Next(9)), ("bar", random.Next(9)))
                    )),

                    makeArrayAndImageWithValues(rangeSelect(0, 49, i =>
                        makeDynamicObject(("foo", rndNextUnique()), ("bar", 0), ("BAR", rndNextUnique()))
                    )),

                    makeArrayAndImageWithValues(
                        makeDynamicObject(("foo", 0), ("bar", 1201)),
                        makeDynamicObject(("foo", 100), ("bar", 1493)),
                        makeDynamicObject(("foo", 1), ("bar", -2493)),
                        makeDynamicObject(("foo", 2), ("bar", Double.PositiveInfinity)),
                        makeDynamicObject(("foo", 3000), ("bar", 0.0)),
                        makeDynamicObject(("foo", 30), ("bar", 0.00052354)),
                        makeDynamicObject(("foo", 600), ("bar", Double.NaN)),
                        makeDynamicObject(("foo", 101), ("bar", Double.NegativeInfinity)),
                        makeDynamicObject(("foo", 10), ("bar", -1.4498e+203)),
                        makeDynamicObject(("foo", 20))
                    ),
                },
                properties: new[] {
                    Array.Empty<(string, int)>(),
                    new[] {("foo", 0)},
                    new[] {("foo", ASArray.NUMERIC)},
                    new[] {("foo", ASArray.DESCENDING)},
                    new[] {("foo", ASArray.NUMERIC | ASArray.DESCENDING)},

                    new[] {("bar", 0)},
                    new[] {("bar", ASArray.NUMERIC)},
                    new[] {("bar", ASArray.DESCENDING)},
                    new[] {("bar", ASArray.NUMERIC | ASArray.DESCENDING)},

                    new[] {("foo", 0), ("bar", 0)},
                    new[] {("foo", ASArray.NUMERIC), ("bar", ASArray.NUMERIC)},
                    new[] {("bar", ASArray.NUMERIC), ("foo", ASArray.NUMERIC)},
                    new[] {("foo", ASArray.NUMERIC | ASArray.DESCENDING), ("bar", ASArray.NUMERIC)},
                    new[] {("bar", ASArray.NUMERIC | ASArray.DESCENDING), ("foo", ASArray.NUMERIC)},
                    new[] {("foo", ASArray.NUMERIC), ("bar", ASArray.NUMERIC | ASArray.DESCENDING)},
                    new[] {("bar", ASArray.NUMERIC), ("foo", ASArray.NUMERIC | ASArray.DESCENDING)},
                    new[] {("foo", ASArray.NUMERIC | ASArray.DESCENDING), ("bar", ASArray.NUMERIC | ASArray.DESCENDING)},

                    new[] {("foo", 0), ("bar", ASArray.NUMERIC)},
                    new[] {("bar", 0), ("foo", ASArray.DESCENDING)},
                    new[] {("foo", ASArray.DESCENDING), ("bar", ASArray.DESCENDING)},
                    new[] {("foo", ASArray.NUMERIC), ("bar", 0)},
                    new[] {("bar", ASArray.NUMERIC), ("foo", ASArray.DESCENDING)},
                },
                testAllArgumentVariants: true
            );

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(),

                    makeArrayAndImageWithValues(
                        makeDynamicObject(("foo", "ca"), ("bar", "xzY")),
                        makeDynamicObject(("foo", "bcDa"), ("bar", "wzxz")),
                        makeDynamicObject(("foo", "Aba"), ("bar", "uuV")),
                        makeDynamicObject(("foo", "cea"), ("bar", "zZwZ")),
                        makeDynamicObject(("foo", "daF"), ("bar", "WzXX")),
                        makeDynamicObject(("foo", "AaB"), ("bar", "WWW")),
                        makeDynamicObject(("foo", "c"), ("bar", "uvW")),
                        makeDynamicObject(("foo", "cAA"), ("bar", "XwZu")),
                        makeDynamicObject(("foo", "AaBC"), ("bar", "xzw")),
                        makeDynamicObject(("foo", "Fb"), ("bar", "uvWx")),
                        makeDynamicObject(("foo", "aaB"), ("bar", "xzy")),
                        makeDynamicObject(("foo", "Ca"), ("bar", "xzY")),
                        makeDynamicObject(("foo", "E"), ("bar", "uvwy")),
                        makeDynamicObject(("foo", "AaBd"), ("bar", "xzY")),
                        makeDynamicObject(("foo", "acaa"), ("bar", "ywUU")),
                        makeDynamicObject(("foo", "faaa"), ("bar", "ZZ")),
                        makeDynamicObject(("foo", "BB"), ("bar", "Uv"))
                    ),

                    makeArrayAndImageWithValues(
                        makeDynamicObject(("bar", ASAny.undefined)),
                        makeDynamicObject(("foo", "undefined"), ("bar", "Undefined")),
                        makeDynamicObject(("foo", ASAny.@null), ("bar", ASAny.@null)),
                        makeDynamicObject(("foo", ASAny.@null)),
                        makeDynamicObject(),
                        makeDynamicObject(("foo", ASAny.@null), ("bar", "undefined")),
                        makeDynamicObject(("foo", "Null"), ("bar", "undefineD"))
                    ),
                },
                properties: new[] {
                    Array.Empty<(string, int)>(),
                    new[] {("foo", 0)},
                    new[] {("foo", ASArray.CASEINSENSITIVE)},
                    new[] {("foo", ASArray.DESCENDING)},
                    new[] {("foo", ASArray.CASEINSENSITIVE | ASArray.DESCENDING)},

                    new[] {("bar", 0)},
                    new[] {("bar", ASArray.CASEINSENSITIVE)},
                    new[] {("bar", ASArray.DESCENDING)},
                    new[] {("bar", ASArray.CASEINSENSITIVE | ASArray.DESCENDING)},

                    new[] {("foo", 0), ("bar", ASArray.CASEINSENSITIVE)},
                    new[] {("foo", ASArray.CASEINSENSITIVE), ("bar", 0)},
                    new[] {("bar", 0), ("foo", ASArray.CASEINSENSITIVE)},
                    new[] {("bar", ASArray.CASEINSENSITIVE), ("foo", 0)},
                    new[] {("foo", ASArray.DESCENDING), ("bar", ASArray.DESCENDING)},
                    new[] {("foo", ASArray.DESCENDING), ("bar", ASArray.CASEINSENSITIVE | ASArray.DESCENDING)},
                    new[] {("foo", ASArray.CASEINSENSITIVE | ASArray.DESCENDING), ("bar", ASArray.CASEINSENSITIVE | ASArray.DESCENDING)},
                },
                testAllArgumentVariants: true
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithValues(
                        makeTypedObject(x: 0, y: 0, z: 0, w: 154),
                        makeTypedObject(x: 2, y: 0, z: 0, w: 87),
                        makeTypedObject(x: 0, y: 0, z: 6, w: -39),
                        makeTypedObject(x: 0, y: 3, z: 2, w: 201),
                        makeTypedObject(x: 1, y: 0, z: 0, w: 46),
                        makeTypedObject(x: 0, y: 5, z: 0, w: 73),
                        makeTypedObject(x: 1, y: 0, z: 0, w: 39),
                        makeTypedObject(x: 2, y: 0, z: 2, w: 56),
                        makeTypedObject(x: 2, y: 3, z: 0, w: -20),
                        makeTypedObject(x: 0, y: 5, z: 2, w: -47),
                        makeTypedObject(x: 1, y: 1, z: 0, w: 35),
                        makeTypedObject(x: 1, y: 1, z: 1, w: 67),
                        makeTypedObject(x: 2, y: 3, z: 3, w: -11),
                        makeTypedObject(x: 1, y: 0, z: 1, w: 24),
                        makeTypedObject(x: 0, y: 5, z: 0, w: 46),
                        makeTypedObject(x: 1, y: 1, z: 1, w: 79),
                        makeTypedObject(x: 0, y: 2, z: 4, w: 57),
                        makeTypedObject(x: 2, y: 5, z: 5, w: -44),
                        makeTypedObject(x: 2, y: 0, z: 5, w: -306),
                        makeTypedObject(x: 0, y: 5, z: 0, w: 78),
                        makeTypedObject(x: 1, y: 1, z: 4, w: 124),
                        makeTypedObject(x: 0, y: 2, z: 3, w: 54)
                    )
                },
                properties: new[] {
                    new[] {("x", ASArray.NUMERIC)},
                    new[] {("y", ASArray.NUMERIC)},
                    new[] {("z", ASArray.NUMERIC)},
                    new[] {("w", ASArray.NUMERIC | ASArray.DESCENDING)},
                    new[] {("x", ASArray.NUMERIC), ("y", ASArray.NUMERIC)},
                    new[] {("w", ASArray.NUMERIC | ASArray.DESCENDING), ("z", ASArray.NUMERIC | ASArray.DESCENDING)},
                    new[] {("x", ASArray.NUMERIC | ASArray.DESCENDING), ("w", ASArray.NUMERIC)},
                    new[] {("x", ASArray.NUMERIC), ("y", ASArray.NUMERIC), ("z", ASArray.NUMERIC)},
                    new[] {("x", ASArray.NUMERIC), ("z", ASArray.NUMERIC), ("y", ASArray.NUMERIC | ASArray.DESCENDING)},
                    new[] {("x", ASArray.NUMERIC), ("y", ASArray.NUMERIC), ("z", ASArray.NUMERIC), ("w", ASArray.NUMERIC)},
                    new[] {("w", ASArray.NUMERIC | ASArray.DESCENDING), ("y", ASArray.NUMERIC), ("x", ASArray.NUMERIC), ("w", ASArray.NUMERIC)},
                },
                testAllArgumentVariants: true
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 19, i => mutPushOne(makeDynamicObject(
                            ("foo", new ConvertibleMockObject(numberValue: rndNextDoubleUnique(), stringValue: "a"))
                        ))),
                        rangeSelect(0, 19, i => mutPushOne(makeDynamicObject(
                            ("foo", new ConvertibleMockObject(numberValue: rndNextDoubleUnique(), stringValue: "C"))
                        ))),
                        rangeSelect(0, 19, i => mutPushOne(makeDynamicObject(
                            ("foo", new ConvertibleMockObject(numberValue: rndNextDoubleUnique(), stringValue: "b"))
                        ))),
                        rangeSelect(0, 19, i => mutPushOne(makeDynamicObject(
                            ("foo", new ConvertibleMockObject(numberValue: rndNextDoubleUnique(), stringValue: "A"))
                        )))
                    ))
                },
                properties: new[] {
                    new[] {("foo", 0)},
                    new[] {("foo", ASArray.NUMERIC)},
                    new[] {("foo", ASArray.CASEINSENSITIVE)},
                    new[] {("foo", 0), ("foo", ASArray.NUMERIC)},
                    new[] {("foo", ASArray.NUMERIC), ("foo", 0)},
                    new[] {("foo", ASArray.CASEINSENSITIVE), ("foo", ASArray.NUMERIC | ASArray.DESCENDING)},
                }
            );

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(1),
                    makeEmptyArrayAndImage(10),

                    makeArrayAndImageWithValues(ASAny.undefined),
                    makeArrayAndImageWithValues(ASAny.@null),
                    makeArrayAndImageWithValues(ASAny.undefined, ASAny.@null),
                    makeArrayAndImageWithValues(ASAny.@null, ASAny.@null),
                    makeArrayAndImageWithValues(ASAny.undefined, ASAny.@null, ASAny.undefined),

                    makeArrayAndImageWithMutations(buildMutationList(mutSetUint(0, ASAny.undefined), mutSetLength(2))),
                    makeArrayAndImageWithMutations(buildMutationList(mutSetUint(0, ASAny.@null), mutSetLength(2))),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(0, makeDynamicObject(("foo", 0), ("bar", 10))),
                        mutSetLength(2)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(0, makeDynamicObject(("foo", 0), ("bar", 10))),
                        mutSetUint(1, ASAny.@null)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(0, makeDynamicObject(("foo", 0), ("bar", 10))),
                        mutSetUint(1, makeDynamicObject(("foo", 6), ("bar", 4))),
                        mutSetUint(2, ASAny.undefined),
                        mutSetUint(3, makeDynamicObject(("foo", 4), ("bar", 8)))
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(0, makeDynamicObject(("foo", "ab"), ("bar", "zw"))),
                        mutSetUint(1, makeDynamicObject(("foo", "ac"), ("bar", "xw"))),
                        mutSetUint(2, ASAny.@null),
                        mutSetUint(3, makeDynamicObject(("foo", "bc"), ("bar", "xy")))
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(0, makeDynamicObject(("foo", "ab"), ("bar", "zw"))),
                        mutSetUint(1, makeDynamicObject(("foo", "ac"), ("bar", "xw"))),
                        mutSetUint(2, ASAny.@null),
                        mutSetUint(3, makeDynamicObject(("foo", "bc"), ("bar", "xy"))),
                        mutSetUint(3, makeDynamicObject(("foo", "ab"), ("bar", "xy"))),
                        mutSetLength(5)
                    )),

                    makeArrayAndImageWithMutations(rangeSelect(0, 99, i => {
                        switch (random.Next(10)) {
                            case 0:
                                return mutSetUint(i, ASAny.undefined);
                            case 1:
                                return mutSetUint(i, ASAny.@null);
                            case 2:
                            case 3:
                            case 4:
                                return mutSetUint(i, makeDynamicObject(("foo", 0), ("bar", rndNextUnique())));
                            default:
                                return mutSetUint(i, makeDynamicObject(("foo", rndNextUnique()), ("bar", rndNextUnique())));
                        }
                    })),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i =>
                            mutSetUint(i, makeDynamicObject(("foo", random.Next(10)), ("bar", rndNextUnique())))
                        ),
                        mutSetLength(100)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 69, i => mutSetUint(i, ASAny.undefined)),
                        rangeSelect(70, 99, i =>
                            mutSetUint(i, makeDynamicObject(("foo", random.Next(10)), ("bar", rndNextUnique())))
                        ),
                        rangeSelect(0, 59, i => mutDelUint(i))
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 99, i =>
                            mutSetUint(i + 1, makeDynamicObject(("foo", random.Next(10)), ("bar", rndNextUnique())))
                        )
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(
                            0, 99, i => mutSetUint(i + 1, makeDynamicObject(("foo", random.Next(10)), ("bar", rndNextUnique())))
                        ),
                        mutDelUint(43)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 99, i =>
                            mutSetUint(i + 1, makeDynamicObject(("foo", random.Next(10)), ("bar", rndNextUnique())))
                        ),
                        mutDelUint(43),
                        mutDelUint(59),
                        mutDelUint(99)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 99, i =>
                            mutSetUint(i + 1, makeDynamicObject(("foo", random.Next(10)), ("bar", rndNextUnique())))
                        ),
                        mutSetLength(101)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 99, i =>
                            mutSetUint(i + 50, makeDynamicObject(("foo", random.Next(10)), ("bar", rndNextUnique())))
                        )
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutSetUint(i, makeDynamicObject(("foo", random.Next(10)), ("bar", rndNextUnique())))),
                        rangeSelect(90, 99, i => mutSetUint(i, makeDynamicObject(("foo", random.Next(10)), ("bar", rndNextUnique()))))
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 29, i => mutSetUint(
                            (uint)random.Next(100),
                            makeDynamicObject(("foo", random.Next(10)), ("bar", rndNextUnique()))
                        ))
                    )),
                },
                properties: new[] {
                    Array.Empty<(string, int)>(),
                    new[] {("foo", 0)},
                    new[] {("foo", ASArray.NUMERIC)},
                    new[] {("bar", 0)},
                    new[] {("bar", ASArray.NUMERIC)},
                    new[] {("foo", 0), ("bar", 0)},
                    new[] {("bar", ASArray.DESCENDING), ("foo", ASArray.DESCENDING)},
                    new[] {("foo", ASArray.NUMERIC), ("bar", ASArray.NUMERIC)},
                    new[] {("foo", ASArray.NUMERIC), ("bar", ASArray.NUMERIC | ASArray.DESCENDING)},
                }
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetLength(100),
                        mutSetUint(2, makeDynamicObject(("foo", "ca"), ("bar", "xzY"))),
                        mutSetUint(14, makeDynamicObject(("foo", "bcDa"), ("bar", "wzxz"))),
                        mutSetUint(17, makeDynamicObject(("foo", "Aba"), ("bar", "uuV"))),
                        mutSetUint(23, makeDynamicObject(("foo", "cea"), ("bar", "zZwZ"))),
                        mutSetUint(25, makeDynamicObject(("foo", "daF"), ("bar", "WzXX"))),
                        mutSetUint(27, makeDynamicObject(("foo", "AaB"), ("bar", "WWW"))),
                        mutSetUint(33, makeDynamicObject(("foo", "c"), ("bar", "uvW"))),
                        mutSetUint(38, makeDynamicObject(("foo", "cAA"), ("bar", "XwZu"))),
                        mutSetUint(43, makeDynamicObject(("foo", "AaBC"), ("bar", "xzw"))),
                        mutSetUint(47, makeDynamicObject(("foo", "Fb"), ("bar", "uvWx"))),
                        mutSetUint(51, makeDynamicObject(("foo", "aaB"), ("bar", "xzy"))),
                        mutSetUint(56, makeDynamicObject(("foo", "Ca"), ("bar", "xzY"))),
                        mutSetUint(73, makeDynamicObject(("foo", "E"), ("bar", "uvwy"))),
                        mutSetUint(74, makeDynamicObject(("foo", "AaBd"), ("bar", "xzY"))),
                        mutSetUint(81, makeDynamicObject(("foo", "acaa"), ("bar", "ywUU"))),
                        mutSetUint(91, makeDynamicObject(("foo", "faaa"), ("bar", "ZZ"))),
                        mutSetUint(94, makeDynamicObject(("foo", "BB"), ("bar", "Uv")))
                    )),
                },
                properties: new[] {
                    Array.Empty<(string, int)>(),
                    new[] {("foo", 0)},
                    new[] {("foo", ASArray.CASEINSENSITIVE)},
                    new[] {("foo", ASArray.DESCENDING)},
                    new[] {("foo", ASArray.CASEINSENSITIVE | ASArray.DESCENDING)},

                    new[] {("bar", 0)},
                    new[] {("bar", ASArray.CASEINSENSITIVE)},
                    new[] {("bar", ASArray.DESCENDING)},
                    new[] {("bar", ASArray.CASEINSENSITIVE | ASArray.DESCENDING)},

                    new[] {("foo", 0), ("bar", ASArray.CASEINSENSITIVE)},
                    new[] {("foo", ASArray.CASEINSENSITIVE), ("bar", 0)},
                    new[] {("bar", 0), ("foo", ASArray.CASEINSENSITIVE)},
                    new[] {("bar", ASArray.CASEINSENSITIVE), ("foo", 0)},
                    new[] {("foo", ASArray.CASEINSENSITIVE | ASArray.DESCENDING), ("bar", ASArray.CASEINSENSITIVE | ASArray.DESCENDING)},
                }
            );

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(),
                    makeEmptyArrayAndImage(1),
                    makeEmptyArrayAndImage(2),
                    makeEmptyArrayAndImage(3),
                    makeEmptyArrayAndImage(4),

                    makeArrayAndImageWithValues(
                        makeDynamicObject(("foo", 8), ("bar", 100))
                    ),

                    makeArrayAndImageWithValues(
                        ASAny.undefined,
                        makeDynamicObject(("foo", 8), ("bar", 100))
                    ),

                    makeArrayAndImageWithValues(
                        makeDynamicObject(("foo", 4), ("bar", 200)),
                        makeDynamicObject(("foo", 4), ("bar", 100))
                    ),

                    makeArrayAndImageWithValues(
                        makeDynamicObject(("foo", 4), ("bar", 200)),
                        ASAny.undefined,
                        makeDynamicObject(("foo", 4), ("bar", 100))
                    ),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutPushOne(makeDynamicObject(("foo", 4), ("bar", 200))),
                        mutSetLength(2)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutPushOne(makeDynamicObject(("foo", 4), ("bar", 200))),
                        mutSetLength(3)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(1, makeDynamicObject(("foo", 1), ("bar", 100))),
                        mutSetLength(3)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(1, makeDynamicObject(("foo", 1), ("bar", 100))),
                        mutSetUint(3, ASAny.@null)
                    )),
                },
                prototype: new IndexDict {
                    [0] = makeDynamicObject(("foo", 1), ("bar", 100)),
                    [1] = makeDynamicObject(("foo", 5), ("bar", 100))
                },
                properties: new[] {
                    Array.Empty<(string, int)>(),
                    new[] {("foo", ASArray.NUMERIC)},
                    new[] {("bar", ASArray.NUMERIC)},
                    new[] {("bar", ASArray.NUMERIC), ("foo", ASArray.NUMERIC)},
                    new[] {("bar", ASArray.NUMERIC), ("foo", ASArray.NUMERIC | ASArray.DESCENDING)}
                }
            );

            addTestCase(
                testInstances: new[] {
                    makeEmptyArrayAndImage(3),
                    makeEmptyArrayAndImage(5),
                    makeEmptyArrayAndImage(6),
                    makeEmptyArrayAndImage(10),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(0, makeDynamicObject(("foo", "ada"), ("bar", "prq"))),
                        mutSetUint(1, makeDynamicObject(("foo", "aaa"), ("bar", "prq"))),
                        mutSetUint(2, makeDynamicObject(("foo", "ada"), ("bar", "pps"))),
                        mutSetLength(6)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(0, makeDynamicObject(("foo", "ada"), ("bar", "prq"))),
                        mutSetUint(1, makeDynamicObject(("foo", "aac"), ("bar", "pqr"))),
                        mutSetLength(5)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(0, makeDynamicObject(("foo", "ada"), ("bar", "prq"))),
                        mutSetUint(1, makeDynamicObject(("foo", "aac"), ("bar", "pqr"))),
                        mutSetLength(6)
                    )),

                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(0, makeDynamicObject(("foo", "ada"), ("bar", "prq"))),
                        mutSetUint(1, makeDynamicObject(("foo", "aac"), ("bar", "pqr"))),
                        mutSetUint(6, makeDynamicObject(("foo", "acd"), ("bar", "psr"))),
                        mutSetUint(7, makeDynamicObject(("foo", "aab"), ("bar", "pqp"))),
                        mutSetLength(9)
                    )),
                },
                prototype: new IndexDict {
                    [3] = makeDynamicObject(("foo", "abc"), ("bar", "pqs")),
                    [4] = makeDynamicObject(("foo", "aac"), ("bar", "pqr")),
                    [5] = makeDynamicObject(("foo", "aac"), ("bar", "pqs")),
                    [6] = ASAny.undefined,
                    [8] = ASAny.@null,
                },
                properties: new[] {
                    Array.Empty<(string, int)>(),
                    new[] {("foo", 0)},
                    new[] {("bar", 0)},
                    new[] {("foo", 0), ("bar", 0)},
                    new[] {("bar", 0), ("foo", 0)},
                }
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetLength(100),
                        mutSetUint(2, makeDynamicObject(("foo", "ca"), ("bar", "xzY"))),
                        mutSetUint(17, makeDynamicObject(("foo", "Aba"), ("bar", "uuV"))),
                        mutSetUint(25, makeDynamicObject(("foo", "daF"), ("bar", "WzXX"))),
                        mutSetUint(27, makeDynamicObject(("foo", "AaB"), ("bar", "WWW"))),
                        mutSetUint(33, makeDynamicObject(("foo", "c"), ("bar", "uvW"))),
                        mutSetUint(43, makeDynamicObject(("foo", "AaBC"), ("bar", "xzw"))),
                        mutSetUint(47, makeDynamicObject(("foo", "Fb"), ("bar", "uvWx"))),
                        mutSetUint(56, makeDynamicObject(("foo", "Ca"), ("bar", "xzY"))),
                        mutSetUint(73, makeDynamicObject(("foo", "E"), ("bar", "uvwy"))),
                        mutSetUint(74, makeDynamicObject(("foo", "AaBd"), ("bar", "xzY"))),
                        mutSetUint(81, makeDynamicObject(("foo", "acaa"), ("bar", "ywUU"))),
                        mutSetUint(91, makeDynamicObject(("foo", "faaa"), ("bar", "ZZ"))),
                        mutSetUint(94, makeDynamicObject(("foo", "BB"), ("bar", "Uv")))
                    )),
                },
                prototype: new IndexDict {
                    [14] = makeDynamicObject(("foo", "bcDa"), ("bar", "wzxz")),
                    [23] = makeDynamicObject(("foo", "cea"), ("bar", "zZwZ")),
                    [27] = makeDynamicObject(("foo", "AaB"), ("bar", "WWW")),
                    [38] = makeDynamicObject(("foo", "cAA"), ("bar", "XwZu")),
                    [51] = makeDynamicObject(("foo", "aaB"), ("bar", "xzy")),
                    [66] = makeDynamicObject(("foo", "Ecc"), ("bar", "zwUu")),
                    [81] = makeDynamicObject(("foo", "faaa"), ("bar", "ZZ")),
                },
                properties: new[] {
                    Array.Empty<(string, int)>(),
                    new[] {("foo", 0)},
                    new[] {("foo", ASArray.CASEINSENSITIVE)},
                    new[] {("foo", ASArray.DESCENDING)},
                    new[] {("foo", ASArray.CASEINSENSITIVE | ASArray.DESCENDING)},

                    new[] {("bar", 0)},
                    new[] {("bar", ASArray.CASEINSENSITIVE)},
                    new[] {("bar", ASArray.DESCENDING)},
                    new[] {("bar", ASArray.CASEINSENSITIVE | ASArray.DESCENDING)},

                    new[] {("foo", 0), ("bar", ASArray.CASEINSENSITIVE)},
                    new[] {("foo", ASArray.CASEINSENSITIVE), ("bar", 0)},
                    new[] {("bar", 0), ("foo", ASArray.CASEINSENSITIVE)},
                    new[] {("bar", ASArray.CASEINSENSITIVE), ("foo", 0)},
                    new[] {("foo", ASArray.CASEINSENSITIVE | ASArray.DESCENDING), ("bar", ASArray.CASEINSENSITIVE | ASArray.DESCENDING)},
                }
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 4, i => mutPushOne(
                            makeDynamicObject(("foo", random.Next(10)), ("bar", random.Next(20)))
                        )),
                        mutSetLength(75)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 39, i => mutPushOne(
                            makeDynamicObject(("foo", random.Next(10)), ("bar", random.Next(20)))
                        )),
                        mutSetLength(75)
                    )),
                    makeArrayAndImageWithMutations(buildMutationList(
                        rangeSelect(0, 19, i => mutSetUint(
                            (uint)random.Next(60),
                            makeDynamicObject(("foo", random.Next(10)), ("bar", random.Next(20)))
                        )),
                        mutSetLength(75)
                    ))
                },
                prototype: new IndexDict(rangeSelect(60, 74, i =>
                    KeyValuePair.Create(i, makeDynamicObject(("foo", random.Next(10)), ("bar", random.Next(20))))
                )),
                properties: new[] {
                    Array.Empty<(string, int)>(),
                    new[] {("foo", ASArray.NUMERIC)},
                    new[] {("bar", ASArray.NUMERIC)},
                    new[] {("foo", ASArray.NUMERIC), ("bar", ASArray.NUMERIC)}
                }
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithMutations(buildMutationList(
                        mutSetUint(0, makeTypedObject(x: 0, y: 0, z: 0, w: 154)),
                        mutSetUint(1, makeTypedObject(x: 2, y: 0, z: 0, w: 87)),
                        mutSetUint(5, makeTypedObject(x: 0, y: 5, z: 0, w: 73)),
                        mutSetUint(6, makeTypedObject(x: 1, y: 0, z: 0, w: 39)),
                        mutSetUint(7, makeTypedObject(x: 2, y: 0, z: 2, w: 56)),
                        mutSetUint(8, makeTypedObject(x: 2, y: 3, z: 0, w: -20)),
                        mutSetUint(9, makeTypedObject(x: 0, y: 5, z: 2, w: -47)),
                        mutSetUint(10, makeTypedObject(x: 1, y: 1, z: 0, w: 35)),
                        mutSetUint(11, makeTypedObject(x: 1, y: 1, z: 1, w: 67)),
                        mutSetUint(12, makeTypedObject(x: 2, y: 3, z: 3, w: -11)),
                        mutSetUint(13, makeTypedObject(x: 1, y: 0, z: 1, w: 24)),
                        mutSetUint(15, makeTypedObject(x: 1, y: 1, z: 1, w: 79)),
                        mutSetUint(16, makeTypedObject(x: 0, y: 2, z: 4, w: 57)),
                        mutSetUint(17, makeTypedObject(x: 2, y: 5, z: 5, w: -44)),
                        mutSetUint(18, makeTypedObject(x: 2, y: 0, z: 5, w: -306)),
                        mutSetUint(19, makeTypedObject(x: 0, y: 5, z: 0, w: 78)),
                        mutSetUint(20, makeTypedObject(x: 1, y: 1, z: 4, w: 124)),
                        mutSetLength(22)
                    ))
                },
                prototype: new IndexDict {
                    [2] = makeTypedObject(x: 0, y: 0, z: 6, w: -39),
                    [3] = makeTypedObject(x: 0, y: 3, z: 2, w: 201),
                    [4] = makeTypedObject(x: 1, y: 0, z: 0, w: 46),
                    [9] = makeTypedObject(x: 0, y: 5, z: 2, w: -47),
                    [12] = makeTypedObject(x: 0, y: 2, z: 4, w: 57),
                    [14] = makeTypedObject(x: 0, y: 5, z: 0, w: 46),
                    [18] = makeTypedObject(x: 2, y: 6, z: -3, w: -306),
                    [21] = makeTypedObject(x: 0, y: 2, z: 3, w: 54)
                },
                properties: new[] {
                    new[] {("x", ASArray.NUMERIC)},
                    new[] {("y", ASArray.NUMERIC)},
                    new[] {("z", ASArray.NUMERIC)},
                    new[] {("w", ASArray.NUMERIC | ASArray.DESCENDING)},
                    new[] {("x", ASArray.NUMERIC), ("y", ASArray.NUMERIC)},
                    new[] {("w", ASArray.NUMERIC | ASArray.DESCENDING), ("z", ASArray.NUMERIC | ASArray.DESCENDING)},
                    new[] {("x", ASArray.NUMERIC | ASArray.DESCENDING), ("w", ASArray.NUMERIC)},
                    new[] {("x", ASArray.NUMERIC), ("y", ASArray.NUMERIC), ("z", ASArray.NUMERIC)},
                    new[] {("x", ASArray.NUMERIC), ("z", ASArray.NUMERIC), ("y", ASArray.NUMERIC | ASArray.DESCENDING)},
                    new[] {("x", ASArray.NUMERIC), ("y", ASArray.NUMERIC), ("z", ASArray.NUMERIC), ("w", ASArray.NUMERIC)},
                    new[] {("w", ASArray.NUMERIC | ASArray.DESCENDING), ("y", ASArray.NUMERIC), ("x", ASArray.NUMERIC), ("w", ASArray.NUMERIC)},
                }
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithValues(
                        makeDynamicObject(("x", "encyclopædia")),
                        makeDynamicObject(("x", "Archæology")),
                        makeDynamicObject(("x", "ARCHÆOLOGY")),
                        makeDynamicObject(("x", "encyclopaedia")),
                        makeDynamicObject(("x", "ARCHAEOLOGY"))
                    )
                },
                properties: new[] {
                    new[] {("x", 0)},
                    new[] {("x", ASArray.CASEINSENSITIVE)}
                }
            );

            addTestCase(
                testInstances: new[] {
                    makeArrayAndImageWithValues("a", "aa", "aaa", "aaaa", "aaaaa", "aaaaaa"),
                    makeArrayAndImageWithValues("a", "aaaaaa", "aa", "", "aaaa", "aaa", "aaaaa", "aaaaaaaaaaa", "aaaaaaaa"),
                    makeArrayAndImageWithValues("a", "aaaaaa", "aa", "aaaa", "aaa", "a", "aaaaa", "aaaaaaaaaaa", "aaaaaaaa"),
                    makeArrayAndImageWithValues("a", new ASArray(3), "aaaaa", "aaa", new ASArray(10), "aaaaaaa"),
                },
                properties: new[] {
                    new[] {("length", ASArray.NUMERIC)},
                    new[] {("length", ASArray.NUMERIC | ASArray.DESCENDING)}
                }
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(sortOnMethodTest_data))]
        public void sortOnMethodTest(
            ArrayWrapper array, Image image, string[] propNames, int[] propFlags, IndexDict prototype, bool testAllArgumentVariants)
        {
            int[] globalFlags = {0, ASArray.UNIQUESORT, ASArray.RETURNINDEXEDARRAY, ASArray.UNIQUESORT | ASArray.RETURNINDEXEDARRAY};

            runInZoneWithArrayPrototype(prototype, () => {
                setRandomSeed(78666);

                for (int i = 0; i < globalFlags.Length; i++) {
                    bool isUniqueSort = (globalFlags[i] & ASArray.UNIQUESORT) != 0;
                    bool isReturnIndexedArray = (globalFlags[i] & ASArray.RETURNINDEXEDARRAY) != 0;

                    foreach (ASAny[] callArgs in generateArgumentVariants(globalFlags[i])) {
                        ASArray testArray = array.instance.clone();

                        ASAny arg1 = callArgs[0];
                        ASAny arg2 = (callArgs.Length > 1) ? callArgs[1] : default;
                        RestParam rest = (callArgs.Length > 2) ? new RestParam(callArgs.AsSpan(2)) : default;

                        ASAny returnVal = testArray.sortOn(arg1, arg2, rest);

                        if (isUniqueSort && ASAny.AS_strictEq(returnVal, 0)) {
                            verifyArrayMatchesImage(testArray, image);
                            verifyUniqueSortOnFailed(image, propNames, propFlags);
                            continue;
                        }

                        if (isReturnIndexedArray) {
                            Assert.IsType<ASArray>(returnVal.value);
                            Assert.NotSame(returnVal.value, testArray);
                            verifyArrayMatchesImage(testArray, image);
                        }
                        else {
                            Assert.Same(returnVal.value, testArray);
                        }

                        verifyArraySortedOnProperties(
                            image,
                            (ASArray)returnVal.value,
                            propNames,
                            propFlags,
                            isUniqueSort,
                            isReturnIndexedArray
                        );
                    }
                }
            });

            IEnumerable<ASAny[]> generateArgumentVariants(int gFlags) {
                List<ASAny[]> arglists = new List<ASAny[]>();

                if (propNames.Length == 0) {
                    arglists.Add(new ASAny[] {new ASArray(), gFlags});

                    if (testAllArgumentVariants)
                        arglists.Add(new ASAny[] {new ASArray(), gFlags, 31});

                    if (gFlags == 0) {
                        arglists.Add(new ASAny[] {new ASArray()});

                        if (testAllArgumentVariants)
                            arglists.Add(new ASAny[] {new ASArray(), new ASArray(new ASAny[] {31})});
                    }

                    return arglists;
                }

                ASArray propNamesArray = ASArray.fromTypedArray(propNames);
                ASArray propFlagsArray = ASArray.fromTypedArray(propFlags);

                propFlagsArray[0] = propFlags[0] | gFlags;

                ASArray propFlagsArray2 = new ASArray();
                for (int i = 0; i < propFlags.Length; i++) {
                    int f = propFlags[i];

                    if (i == 0) {
                        f |= gFlags;
                    }
                    else {
                        // These flags should be ignored except for the first element.
                        f |= ASArray.UNIQUESORT | ASArray.RETURNINDEXEDARRAY;
                    }

                    if ((f & ASArray.NUMERIC) != 0)
                        f |= ASArray.CASEINSENSITIVE;

                    f |= 32;

                    if (i % 2 == 0)
                        propFlagsArray2[i] = ASint.AS_convertString(f);
                    else
                        propFlagsArray2[i] = f;
                }

                arglists.Add(new ASAny[] {propNamesArray, propFlagsArray});

                if (testAllArgumentVariants) {
                    arglists.Add(new ASAny[] {propNamesArray, propFlagsArray2});
                    arglists.Add(new ASAny[] {propNamesArray, propFlagsArray, 6, "aaa"});
                }

                if (propNames.Length == 1) {
                    arglists.Add(new ASAny[] {propNames[0], propFlags[0] | gFlags});

                    if (testAllArgumentVariants) {
                        arglists.Add(new ASAny[] {propNames[0], (propFlags[0] | gFlags).ToString()});
                        arglists.Add(new ASAny[] {propNames[0], (double)(propFlags[0] | gFlags | 32), "abc", "def"});
                        arglists.Add(new ASAny[] {propNames[0], propFlagsArray});
                        arglists.Add(new ASAny[] {propNames[0], propFlagsArray2, ASAny.undefined, "p"});
                    }

                    if ((propFlags[0] | gFlags) == 0) {
                        arglists.Add(new ASAny[] {propNames[0]});

                        if (testAllArgumentVariants) {
                            arglists.Add(new ASAny[] {propNames[0], ASAny.@null});
                            arglists.Add(new ASAny[] {propNames[0], new ASArray()});
                            arglists.Add(new ASAny[] {propNames[0], new ASArray(new ASAny[] {2, 2})});
                        }
                    }
                }

                bool propFlagsCommon = true;
                for (int i = 1; i < propFlags.Length && propFlagsCommon; i++)
                    propFlagsCommon = propFlags[i] == propFlags[0];

                if (propFlagsCommon) {
                    arglists.Add(new ASAny[] {propNamesArray, propFlags[0] | gFlags});

                    if (testAllArgumentVariants) {
                        arglists.Add(new ASAny[] {propNamesArray, (propFlags[0] | gFlags).ToString()});
                        arglists.Add(new ASAny[] {propNamesArray, (double)(propFlags[0] | gFlags | 32), "abc", "def"});
                    }

                    if ((propFlags[0] | gFlags) == 0) {
                        arglists.Add(new ASAny[] {propNamesArray});

                        if (testAllArgumentVariants) {
                            arglists.Add(new ASAny[] {propNamesArray, ASAny.undefined});
                            arglists.Add(new ASAny[] {propNamesArray, new ASArray()});
                            arglists.Add(new ASAny[] {propNamesArray, new ASArray(propNames.Length)});

                            if (propNames.Length > 1)
                                arglists.Add(new ASAny[] {propNamesArray, ASArray.fromTypedArray(repeat(2, propNames.Length - 1))});

                            arglists.Add(new ASAny[] {propNamesArray, ASArray.fromTypedArray(repeat(2, propNames.Length + 1))});
                        }
                    }
                }

                return arglists;
            }
        }

        public static IEnumerable<object[]> sortOnMethodTest_arrayLengthTooBig_data() {
            var testcases = new List<(ArrayWrapper, Image)>();

            void addTestCase((ASArray arr, Image img) testInstance) =>
                testcases.Add((new ArrayWrapper(testInstance.arr), testInstance.img));

            addTestCase(makeEmptyArrayAndImage((uint)Int32.MaxValue + 1));

            addTestCase(makeEmptyArrayAndImage(UInt32.MaxValue));

            addTestCase(makeArrayAndImageWithMutations(buildMutationList(
                mutSetUint(0, 2),
                mutSetUint((uint)Int32.MaxValue + 1, new ClassWithProps(x: 123, y: 0, z: 0, w: 0))
            )));

            addTestCase(makeArrayAndImageWithMutations(buildMutationList(
                mutSetUint(UInt32.MaxValue - 1, new ASObject())
            )));

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(sortOnMethodTest_arrayLengthTooBig_data))]
        public void sortOnMethodTest_arrayLengthTooBig(ArrayWrapper array, Image image) {
            runInZoneWithArrayPrototype(null, () => {
                setRandomSeed(573711);

                testWithArgs("x", default);
                testWithArgs("x", 0);
                testWithArgs("x", ASArray.NUMERIC | ASArray.DESCENDING | ASArray.UNIQUESORT | ASArray.RETURNINDEXEDARRAY);
                testWithArgs(new ASArray(new ASAny[] { "x", "y" }), default);
                testWithArgs(new ASArray(new ASAny[] { "x", "y" }), 0);
                testWithArgs(new ASArray(new ASAny[] { "x", "y" }), ASArray.NUMERIC | ASArray.DESCENDING | ASArray.UNIQUESORT | ASArray.RETURNINDEXEDARRAY);
                testWithArgs(new ASArray(new ASAny[] { "x", "y" }), new ASArray(new ASAny[] { ASArray.NUMERIC, ASArray.DESCENDING }));
                testWithArgs("x", 0, "abc", "def");
                testWithArgs(new ASArray(new ASAny[] { "x", "y" }), new ASArray(new ASAny[] { ASArray.NUMERIC, ASArray.DESCENDING }), ASAny.undefined, 123);
            });

            void testWithArgs(ASAny names, ASAny options, params ASAny[] rest) {
                ASAny returnVal = array.instance.sortOn(names, options, new RestParam(rest));
                Assert.Same(returnVal.value, array.instance);
                verifyArrayMatchesImage(array.instance, image);
            }
        }

        public static IEnumerable<object[]> sortOnMethodTest_propDoesNotExist_data() {
            var testcases = new List<(ArrayWrapper, string[], int[], IndexDict)>();

            void addTestCase(
                ASArray[] testInstances,
                (string name, int flags)[][] properties,
                IndexDict prototype = null
            ) {
                foreach (var arr in testInstances) {
                    foreach (var namesAndFlags in properties) {
                        string[] names = namesAndFlags.Select(x => x.name).ToArray();
                        int[] flags = namesAndFlags.Select(x => x.flags).ToArray();

                        testcases.Add((new ArrayWrapper(arr), names, flags, prototype));
                    }
                }
            }

            addTestCase(
                testInstances: new[] {
                    new ASArray(new ASAny[] {1, 2, 3, 4, 5, "hello", true})
                },
                properties: new[] {
                    new[] {("x", 0)}
                }
            );

            addTestCase(
                testInstances: new[] {
                    new ASArray(new ASAny[] {
                        new ClassWithProps(x: 0, y: 0, z: 0, w: 0),
                        ASAny.undefined,
                        new ClassWithProps(x: 1, y: 1, z: 1, w: 1)
                    }),
                },
                properties: new[] {
                    new[] {("a", 0)},
                    new[] {("X", 0)},
                    new[] {("x", 0), ("y", 0), ("z", 0), ("a", 0)},
                }
            );

            addTestCase(
                testInstances: new[] {
                    new ASArray(2)
                },
                prototype: new IndexDict {
                    [1] = new ClassWithProps(x: 1, y: 1, z: 1, w: 1)
                },
                properties: new[] {
                    new[] {("a", 0)},
                    new[] {("X", 0)},
                    new[] {("x", 0), ("y", 0), ("z", 0), ("a", 0)},
                }
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(sortOnMethodTest_propDoesNotExist_data))]
        public void sortOnMethodTest_propDoesNotExist(ArrayWrapper array, string[] propNames, int[] propFlags, IndexDict prototype) {
            runInZoneWithArrayPrototype(prototype, () => {
                AssertHelper.throwsErrorWithCode(
                    ErrorCode.PROPERTY_NOT_FOUND,
                    () => array.instance.sortOn(ASArray.fromTypedArray(propNames), ASArray.fromTypedArray(propFlags))
                );
            });
        }

    }

}
