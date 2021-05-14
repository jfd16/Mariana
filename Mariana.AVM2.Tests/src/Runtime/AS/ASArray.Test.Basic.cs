using System;
using System.Collections.Generic;
using System.Linq;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    using IndexDict = Dictionary<uint, ASAny>;

    public partial class ASArrayTest {

        public static IEnumerable<object[]> constructEmptyArrayTest_data = TupleHelper.toArrays(
            null,
            makeIndexDict(0, 1, 2, 3, 4, 5),
            makeIndexDict(10, 100, 1000)
        );

        [Theory]
        [MemberData(nameof(constructEmptyArrayTest_data))]
        public void constructEmptyArrayTest(IndexDict prototype) {
            setPrototypeProperties(prototype);
            setRandomSeed(67003741);

            try {
                verifyArrayMatchesImage(new ASArray(), makeEmptyImage());
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> constructEmptyArrayWithLengthTest_data = TupleHelper.toArrays<uint, IndexDict>(
            (0, null),
            (1, null),
            (4, null),
            (64, null),
            (1000, null),
            (10000, null),
            (Int32.MaxValue, null),
            (UInt32.MaxValue, null),

            (0, makeIndexDict(0, 1, 3)),
            (4, makeIndexDict(0, 1, 3)),
            (20, makeIndexDict(0, 2, 4, 6, 8, 10, 13, 15, 17, 19)),
            (20, makeIndexDict(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20)),
            (20, makeIndexDict(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25)),
            (100, makeIndexDict(0, 10, 99, 100, 1000)),
            (Int32.MaxValue, makeIndexDict(0, Int32.MaxValue, (uint)Int32.MaxValue + 1, UInt32.MaxValue - 1)),
            (UInt32.MaxValue, makeIndexDict(0, Int32.MaxValue, (uint)Int32.MaxValue + 1, UInt32.MaxValue - 1))
        );

        [Theory]
        [MemberData(nameof(constructEmptyArrayWithLengthTest_data))]
        public void constructEmptyArrayWithLengthTest(uint length, IndexDict prototype) {
            setPrototypeProperties(prototype);
            setRandomSeed(84711489);

            try {
                verifyArrayMatchesImage(new ASArray(length), makeEmptyImage(length));
                if (length <= (uint)Int32.MaxValue)
                    verifyArrayMatchesImage(new ASArray((int)length), makeEmptyImage(length));
            }
            finally {
                resetPrototypeProperties();
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(Int32.MinValue)]
        public void constructEmptyArrayWithInvalidLengthTest(int length) {
            AssertHelper.throwsErrorWithCode(ErrorCode.ARRAY_LENGTH_NOT_POSITIVE_INTEGER, () => new ASArray(length));
        }

        public static IEnumerable<object[]> constructArrayWithElementsTest_data() {
            ASAny[] v = makeUniqueValues(20);
            ASAny undef = ASAny.undefined;
            ASAny nul = ASAny.@null;

            return TupleHelper.toArrays(
                (Array.Empty<ASAny>(), null),
                (Array.Empty<ASAny>(), makeIndexDict(0, 2, 10)),
                (Array.Empty<ASAny>(), makeIndexDict(100)),

                (new[] {v[0]}, null),
                (new[] {v[0], v[1], v[2]}, null),
                (new[] {v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9]}, null),
                (v, null),
                (new[] {v[0], v[1], v[2], v[3], v[4], v[5], v[0], v[7], v[2], v[3], v[6], v[0], v[0], v[2], v[5], v[9], v[5], v[4], v[10]}, null),
                (new[] {v[0], v[1], v[2], undef, v[4], nul, undef, v[7], v[2], v[3], undef, v[0], undef, undef, undef, v[9], nul, v[4], v[10]}, null),
                (new[] {undef, undef, undef, undef, undef, undef, undef, undef, undef, undef}, null),
                (new[] {undef, undef, nul, undef, nul, undef, nul, undef, nul, undef}, null),

                (new[] {v[0], v[1], v[2], v[3], v[4]}, makeIndexDict(0, 2, 4)),
                (new[] {v[0], v[1], v[2], v[3], v[4]}, makeIndexDict(0, 1, 2, 3, 4)),
                (new[] {undef, v[1], undef, undef, v[4]}, makeIndexDict(0, 1, 2, 3, 4)),
                (new[] {v[0], v[1], v[2], v[3], v[4]}, makeIndexDict(5, 7, 10, 20)),
                (new[] {v[0], v[1], v[2], v[3], v[4]}, makeIndexDict(0, 1, 2, 3, 4, 5, 6, 7, 8, 9)),
                (new[] {v[0], v[1], v[2], v[3], v[4], undef, undef, undef, nul}, makeIndexDict(0, 1, 2, 3, 4, 5, 6, 7, 8, 9)),
                (new[] {v[0], v[1], v[2], v[3], v[4]}, makeIndexDict(1, 3, 4, 7, 10, 23)),
                (new[] {v[0], v[1], v[2], v[3], v[4]}, makeIndexDict(2, 9, 10000, UInt32.MaxValue - 1)),

                (makeUniqueValues(2000), null),
                (repeat(v[0], 2000), null),
                (makeUniqueValues(2000), makeIndexDict(500, 1000, 1500, 2000, 2500, 3000, 5000, 10000))
            );
        }

        [Theory]
        [MemberData(nameof(constructArrayWithElementsTest_data))]
        public void constructArrayWithElementsTest(ASAny[] elements, IndexDict prototype) {
            setPrototypeProperties(prototype);
            setRandomSeed(3647110);

            try {
                verifyArrayMatchesImage(new ASArray(elements), makeImageWithValues(elements));
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> constructArrayWithConstructorTest_data() {
            ASAny[] v = makeUniqueValues(20);
            ASAny undef = ASAny.undefined;
            ASAny nul = ASAny.@null;

            var withElements = new[] {
                (Array.Empty<ASAny>(), null),
                (Array.Empty<ASAny>(), makeIndexDict(0, 2, 10)),
                (Array.Empty<ASAny>(), makeIndexDict(100)),

                (new[] {v[0]}, null),
                (new[] {v[0], v[1], v[2]}, null),
                (new[] {v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9]}, null),
                (v, null),
                (new[] {v[0], v[1], v[2], v[3], v[4], v[5], v[0], v[7], v[2], v[3], v[6], v[0], v[0], v[2], v[5], v[9], v[5], v[4], v[10]}, null),
                (new[] {v[0], v[1], v[2], undef, v[4], nul, undef, v[7], v[2], v[3], undef, v[0], undef, undef, undef, v[9], nul, v[4], v[10]}, null),
                (new[] {undef, undef, undef, undef, undef, undef, undef, undef, undef, undef}, null),
                (new[] {undef, undef, nul, undef, nul, undef, nul, undef, nul, undef}, null),

                (new[] {v[0], v[1], v[2], v[3], v[4]}, makeIndexDict(0, 2, 4)),
                (new[] {v[0], v[1], v[2], v[3], v[4]}, makeIndexDict(0, 1, 2, 3, 4)),
                (new[] {undef, v[1], undef, undef, v[4]}, makeIndexDict(0, 1, 2, 3, 4)),
                (new[] {v[0], v[1], v[2], v[3], v[4]}, makeIndexDict(5, 7, 10, 20)),
                (new[] {v[0], v[1], v[2], v[3], v[4]}, makeIndexDict(0, 1, 2, 3, 4, 5, 6, 7, 8, 9)),
                (new[] {v[0], v[1], v[2], v[3], v[4], undef, undef, undef, nul}, makeIndexDict(0, 1, 2, 3, 4, 5, 6, 7, 8, 9)),
                (new[] {v[0], v[1], v[2], v[3], v[4]}, makeIndexDict(1, 3, 4, 7, 10, 23)),
                (new[] {v[0], v[1], v[2], v[3], v[4]}, makeIndexDict(2, 9, 10000, UInt32.MaxValue - 1)),

                (makeUniqueValues(2000), null),
                (repeat(v[0], 2000), null),
                (makeUniqueValues(2000), makeIndexDict(500, 1000, 1500, 2000, 2500, 3000, 5000, 10000))
            };

            var withLength = new (uint, IndexDict)[] {
                (0, null),
                (4, null),
                (64, null),
                (1000, null),
                (Int32.MaxValue, null),
                (UInt32.MaxValue, null),

                (0, makeIndexDict(0, 1, 3)),
                (20, makeIndexDict(0, 2, 4, 6, 8, 10, 13, 15, 17, 19)),
                (20, makeIndexDict(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25)),
                (100, makeIndexDict(0, 10, 99, 100, 1000)),
                (Int32.MaxValue, makeIndexDict(0, Int32.MaxValue, (uint)Int32.MaxValue + 1, UInt32.MaxValue - 1)),
                (UInt32.MaxValue, makeIndexDict(0, Int32.MaxValue, (uint)Int32.MaxValue + 1, UInt32.MaxValue - 1))
            };

            var withLengthInvalid = new ASAny[] {
                -1,
                -1.0,
                Int32.MinValue,
                (double)Int32.MinValue,
                Math.BitIncrement((double)UInt32.MaxValue),
                (double)UInt32.MaxValue + 1.0,
                0.1,
                1.5,
                Double.PositiveInfinity,
                Double.NegativeInfinity,
                Double.NaN,
            };

            foreach (var (elements, prototype) in withElements)
                yield return new object[] {elements, prototype, makeImageWithValues(elements)};

            foreach (var (length, prototype) in withLength) {
                yield return new object[] {new ASAny[] {length}, prototype, makeEmptyImage(length)};
                yield return new object[] {new ASAny[] {(double)length}, prototype, makeEmptyImage(length)};

                if (length <= (uint)Int32.MaxValue)
                    yield return new object[] {new ASAny[] {(int)length}, prototype, makeEmptyImage(length)};
            }

            foreach (var invalidLength in withLengthInvalid)
                yield return new object[] {new ASAny[] {invalidLength}, null, ErrorCode.ARRAY_LENGTH_NOT_POSITIVE_INTEGER};
        }

        [Theory]
        [MemberData(nameof(constructArrayWithConstructorTest_data))]
        public void constructArrayWithConstructorTest(ASAny[] args, IndexDict prototype, object imageOrError) {
            if (imageOrError is Image image) {
                setPrototypeProperties(prototype);
                setRandomSeed(3647110);

                try {
                    verifyArrayMatchesImage(new ASArray(new RestParam(args)), image);
                    verifyArrayMatchesImage((ASArray)Class.fromType(typeof(ASArray)).invoke(args), image);
                }
                finally {
                    resetPrototypeProperties();
                }
            }
            else {
                AssertHelper.throwsErrorWithCode((ErrorCode)imageOrError, () => new ASArray(new RestParam(args)));
            }
        }

        public static IEnumerable<object[]> accessTestWithSetMutations_data() {
            setRandomSeed(85710039);

            const int numValues = 50;
            const uint maxLength = UInt32.MaxValue;
            const uint maxIndex = UInt32.MaxValue - 1;

            var valueDomain = makeUniqueValues(numValues);
            valueDomain[6] = ASAny.undefined;
            valueDomain[14] = ASAny.undefined;
            valueDomain[28] = ASAny.undefined;
            valueDomain[32] = ASAny.@null;
            valueDomain[41] = ASAny.@null;

            var testcases = new List<(ArrayWrapper, Image, IEnumerable<Mutation>, IndexDict)>();

            void addTestCase((ASArray arr, Image img) initialState, IEnumerable<Mutation> mutations, IndexDict prototype = null) =>
                testcases.Add((new ArrayWrapper(initialState.arr), initialState.img, mutations, prototype));

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain)))
            );
            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetInt(i, randomSample(valueDomain)))
            );
            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetNumber(i, randomSample(valueDomain)))
            );
            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetIndexInt(i, randomSample(valueDomain)))
            );
            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetIndexUint(i, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(32),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(100),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain)))
            );
            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(99, 0, i => mutSetUint(i, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(32),
                mutations: rangeSelect(99, 0, i => mutSetUint(i, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(100),
                mutations: rangeSelect(99, 0, i => mutSetUint(i, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: rangeSelect(99, 0, i => mutSetUint(i, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(99, 0, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(99, 0, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(100),
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(99, 0, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(99, 0, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(32, valueDomain),
                mutations: rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(100, valueDomain),
                mutations: rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 399, i => mutSetRandomIndexType(randomIndex(100), randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(100),
                mutations: rangeSelect(0, 399, i => mutSetRandomIndexType(randomIndex(100), randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 999, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
            );
            addTestCase(
                initialState: makeEmptyArrayAndImage(1000),
                mutations: rangeSelect(0, 999, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
            );
            addTestCase(
                initialState: makeRandomDenseArrayAndImage(500, valueDomain),
                mutations: rangeSelect(0, 999, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(40, 59, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(80, 99, i => mutSetUint(i, randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(50, 69, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(100, 119, i => mutSetUint(i, randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(1, 32, i => mutSetInt((1u << 26) * i - 1u, randomSample(valueDomain)))
            );
            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(1, 64, i => mutSetUint((1u << 26) * i - 2u, randomSample(valueDomain)))
            );
            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(1, 64, i => mutSetNumber((1u << 26) * i - 2u, randomSample(valueDomain)))
            );
            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(1, 32, i => mutSetIndexInt((1u << 26) * i - 1u, randomSample(valueDomain)))
            );
            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(1, 64, i => mutSetIndexUint((1u << 26) * i - 2u, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: rangeSelect(1, 64, i => mutSetUint((1u << 26) * i - 2u, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(2000),
                mutations: rangeSelect(0, 99, i => mutSetRandomIndexType(randomIndex(2000), randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 999, i => mutSetRandomIndexType(randomIndex(maxLength), randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: rangeSelect(0, 999, i => mutSetRandomIndexType(randomIndex(maxLength), randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                prototype: makeIndexDict(0, 5, 10, 16, 29, 61, 78, 99, 103, 139)
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(100),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                prototype: makeIndexDict(0, 5, 10, 16, 29, 61, 78, 99, 103, 139)
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(50, valueDomain),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                prototype: makeIndexDict(0, 5, 10, 16, 29, 61, 78, 99, 103, 139)
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(100, valueDomain),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                prototype: makeIndexDict(0, 5, 10, 16, 29, 61, 78, 99, 103, 139)
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                prototype: makeIndexDict(rangeSelect(0, 99, i => i))
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(50, valueDomain),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                prototype: makeIndexDict(rangeSelect(0, 99, i => i))
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(100, valueDomain),
                mutations: rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                prototype: makeIndexDict(rangeSelect(0, 99, i => i))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(1, 64, i => mutSetUint((1u << 26) * i - 2u, randomSample(valueDomain))),
                prototype: makeIndexDict(0, (1u << 26) - 2u, (20u << 26) - 2u, 21u << 26, maxIndex)
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: rangeSelect(1, 64, i => mutSetUint((1u << 26) * i - 2u, randomSample(valueDomain))),
                prototype: makeIndexDict(0, (1u << 26) - 2u, (20u << 26) - 2u, 21u << 26, maxIndex)
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 499, i => mutSetRandomIndexType(randomIndex(maxLength), randomSample(valueDomain))),
                prototype: makeIndexDict(rangeSelect(0, 99, i => randomIndex(maxLength)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: rangeSelect(0, 499, i => mutSetRandomIndexType(randomIndex(maxLength), randomSample(valueDomain))),
                prototype: makeIndexDict(rangeSelect(0, 99, i => randomIndex(maxLength)))
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(100, valueDomain),
                mutations: rangeSelect(0, 299, i => mutSetRandomIndexType(randomIndex(maxLength), randomSample(valueDomain))),
                prototype: makeIndexDict(rangeSelect(0, 99, i => randomIndex(maxLength)))
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(accessTestWithSetMutations_data))]
        public void accessTestWithSetMutations(ArrayWrapper array, Image image, IEnumerable<Mutation> mutations, IndexDict prototype) {
            setPrototypeProperties(prototype);
            setRandomSeed(9877146);
            try {
                verifyArrayMatchesImage(array.instance, image);
                foreach (var mut in mutations)
                    applyMutationAndVerify(array.instance, ref image, mut);
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> accessTestWithSetAndDeleteMutations_data() {
            setRandomSeed(85710039);

            const int numValues = 50;
            const uint maxLength = UInt32.MaxValue;
            const uint maxIndex = UInt32.MaxValue - 1;

            var valueDomain = makeUniqueValues(numValues);
            valueDomain[6] = ASAny.undefined;
            valueDomain[14] = ASAny.undefined;
            valueDomain[28] = ASAny.undefined;
            valueDomain[32] = ASAny.@null;
            valueDomain[41] = ASAny.@null;

            var testcases = new List<(ArrayWrapper, Image, IEnumerable<Mutation>, IndexDict)>();

            void addTestCase((ASArray arr, Image img) initialState, IEnumerable<Mutation> mutations, IndexDict prototype = null) =>
                testcases.Add((new ArrayWrapper(initialState.arr), initialState.img, mutations, prototype));

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 49, i => mutSetInt(i, randomSample(valueDomain))),
                    rangeSelect(49, 0, i => mutDelInt(i)),
                    rangeSelect(0, 49, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(49, 0, i => mutDelUint(i)),
                    rangeSelect(0, 49, i => mutSetNumber(i, randomSample(valueDomain))),
                    rangeSelect(49, 0, i => mutDelNumber(i)),
                    rangeSelect(49, 0, i => mutDelUint(i))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(50),
                mutations: buildMutationList(
                    rangeSelect(49, 0, i => mutDelUint(i)),
                    rangeSelect(0, 49, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(49, 0, i => mutDelUint(i)),
                    rangeSelect(0, 49, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(49, 0, i => mutDelUint(i))
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(50, valueDomain),
                mutations: buildMutationList(
                    rangeSelect(49, 0, i => mutDelUint(i)),
                    rangeSelect(0, 49, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(49, 0, i => mutDelUint(i)),
                    rangeSelect(0, 49, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(49, 0, i => mutDelUint(i))
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(60, valueDomain),
                mutations: buildMutationList(
                    rangeSelect(40, 59, i => mutDelUint(i)),
                    rangeSelect(20, 39, i => mutDelUint(i)),
                    rangeSelect(0, 19, i => mutDelUint(i))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(1000),
                mutations: buildMutationList(
                    rangeSelect(0, 999, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(999, 0, i => mutDelRandomIndexType(i)),
                    rangeSelect(0, 999, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(999, 0, i => mutDelRandomIndexType(i))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 49, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(49, 0, i => mutDelRandomIndexType(i)),
                    rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(99, 0, i => mutDelRandomIndexType(i)),
                    rangeSelect(0, 199, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(199, 100, i => mutDelRandomIndexType(i)),
                    rangeSelect(100, 399, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(399, 50, i => mutDelRandomIndexType(i)),
                    rangeSelect(50, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(99, 0, i => mutDelRandomIndexType(i))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(400),
                mutations: buildMutationList(
                    rangeSelect(0, 49, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(49, 0, i => mutDelRandomIndexType(i)),
                    rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(99, 0, i => mutDelRandomIndexType(i)),
                    rangeSelect(0, 199, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(199, 100, i => mutDelRandomIndexType(i)),
                    rangeSelect(100, 399, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(399, 50, i => mutDelRandomIndexType(i)),
                    rangeSelect(50, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(99, 0, i => mutDelRandomIndexType(i))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(0, 99, i => mutDelRandomIndexType(i)),
                    rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(0, 99, i => mutDelRandomIndexType(i))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(200),
                mutations: buildMutationList(
                    rangeSelect(0, 199, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(0, 49, i => mutDelRandomIndexType(4 * i)),
                    rangeSelect(0, 49, i => mutDelRandomIndexType(4 * i + 2)),
                    rangeSelect(0, 49, i => mutDelRandomIndexType(4 * i + 1)),
                    rangeSelect(0, 49, i => mutSetRandomIndexType(4 * i + 2, randomSample(valueDomain))),
                    rangeSelect(0, 49, i => mutDelRandomIndexType(4 * i + 1)),
                    rangeSelect(0, 49, i => mutDelRandomIndexType(4 * i + 2)),
                    rangeSelect(0, 49, i => mutDelRandomIndexType(4 * i + 3))
                )
            );

            addTestCase(
                initialState: makeArrayAndImageWithValues(ASAny.undefined, ASAny.undefined, ASAny.@null, ASAny.undefined),
                mutations: new[] {mutDelUint(0), mutDelUint(1), mutDelUint(2)}
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 34, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(20, 34, i => mutDelRandomIndexType(i)),
                    rangeSelect(30, 64, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(50, 64, i => mutDelRandomIndexType(i)),
                    rangeSelect(60, 94, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(80, 94, i => mutDelRandomIndexType(i))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 34, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(15, 34, i => mutDelRandomIndexType(i)),
                    rangeSelect(30, 64, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(45, 64, i => mutDelRandomIndexType(i)),
                    rangeSelect(60, 94, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(75, 94, i => mutDelRandomIndexType(i))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 499, i => {
                    return (randomIndex(5) <= 2)
                        ? mutSetRandomIndexType(randomIndex(100), randomSample(valueDomain))
                        : mutDelRandomIndexType(randomIndex(100));
                })
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(100, valueDomain),
                mutations: rangeSelect(0, 499, i => {
                    return (randomIndex(2) == 0)
                        ? mutSetRandomIndexType(randomIndex(100), randomSample(valueDomain))
                        : mutDelRandomIndexType(randomIndex(100));
                })
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeMultiSelect(0, 49, i => new[] {mutSetUint(i, randomSample(valueDomain)), mutDelUint(i)})
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(1, 64, i => mutDelUint((1u << 26) * i - 2)),
                    rangeSelect(1, 64, i => mutSetUint((1u << 26) * i - 2, randomSample(valueDomain))),
                    rangeSelect(1, 64, i => mutDelUint((1u << 26) * i - 2)),
                    rangeSelect(64, 1, i => mutSetUint((1u << 26) * i - 2, randomSample(valueDomain))),
                    rangeSelect(1, 64, i => mutDelUint((1u << 26) * i - 2))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: buildMutationList(
                    rangeSelect(1, 64, i => mutDelUint((1u << 26) * i - 2)),
                    rangeSelect(1, 64, i => mutSetUint((1u << 26) * i - 2, randomSample(valueDomain))),
                    rangeSelect(1, 64, i => mutDelUint((1u << 26) * i - 2))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(1, 32, i => mutDelInt((1u << 26) * i - 1)),
                    rangeSelect(1, 32, i => mutSetInt((1u << 26) * i - 1, randomSample(valueDomain))),
                    rangeSelect(1, 32, i => mutDelInt((1u << 26) * i - 1))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(1, 64, i => mutDelNumber((1u << 26) * i - 2)),
                    rangeSelect(1, 64, i => mutSetNumber((1u << 26) * i - 2, randomSample(valueDomain))),
                    rangeSelect(1, 64, i => mutDelNumber((1u << 26) * i - 2))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(49, 0, i => mutDelUint(i)),
                    rangeSelect(0, 49, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(49, 0, i => mutDelUint(i)),
                    rangeSelect(0, 49, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(49, 0, i => mutDelUint(i))
                ),
                prototype: makeIndexDict(rangeSelect(0, 24, i => i * 2 + 1))
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(50, valueDomain),
                mutations: buildMutationList(
                    rangeSelect(49, 0, i => mutDelUint(i)),
                    rangeSelect(0, 49, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(49, 0, i => mutDelUint(i)),
                    rangeSelect(0, 49, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(49, 0, i => mutDelUint(i))
                ),
                prototype: makeIndexDict(rangeSelect(0, 24, i => i * 2 + 1))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(200),
                mutations: buildMutationList(
                    rangeSelect(0, 199, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(0, 49, i => mutDelRandomIndexType(4 * i)),
                    rangeSelect(0, 49, i => mutDelRandomIndexType(4 * i + 2)),
                    rangeSelect(0, 49, i => mutDelRandomIndexType(4 * i + 1)),
                    rangeSelect(0, 49, i => mutSetRandomIndexType(4 * i + 2, randomSample(valueDomain))),
                    rangeSelect(0, 49, i => mutDelRandomIndexType(4 * i + 1)),
                    rangeSelect(0, 49, i => mutDelRandomIndexType(4 * i + 2)),
                    rangeSelect(0, 49, i => mutDelRandomIndexType(4 * i + 3))
                ),
                prototype: makeIndexDict(0, 1, 7, 8, 15, 23, 34, 37, 45, 47, 50, 56, 72, 104, 143, 161, 166, 189, 197, 200, 204, 301)
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(1, 64, i => mutDelUint((1u << 26) * i - 2)),
                    rangeSelect(1, 64, i => mutSetUint((1u << 26) * i - 2, randomSample(valueDomain))),
                    rangeSelect(1, 64, i => mutDelUint((1u << 26) * i - 2))
                ),
                prototype: makeIndexDict(0, (1u << 26) - 2u, (20u << 26) - 2u, 21u << 26, maxIndex)
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: buildMutationList(
                    rangeSelect(1, 64, i => mutDelUint((1u << 26) * i - 2)),
                    rangeSelect(1, 64, i => mutSetUint((1u << 26) * i - 2, randomSample(valueDomain))),
                    rangeSelect(1, 64, i => mutDelUint((1u << 26) * i - 2))
                ),
                prototype: makeIndexDict(0, (1u << 26) - 2u, (20u << 26) - 2u, 21u << 26, maxIndex)
            );

            uint[] randomIndices = rangeSelect(1, 1000, i => randomIndex());

            Mutation[] randomSparseMutationSelector(int index) {
                var setMutation = mutSetRandomIndexType(randomIndices[index], randomSample(valueDomain));

                if (index > 0 && randomIndex(5) > 2) {
                    var delMutation = mutDelRandomIndexType(randomIndices[randomInt(index)]);
                    return new[] {setMutation, delMutation};
                }
                else {
                    return new[] {setMutation};
                }
            }

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeMultiSelect(0, 599, i => randomSparseMutationSelector((int)i))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: rangeMultiSelect(0, 599, i => randomSparseMutationSelector((int)i))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeMultiSelect(0, 399, i => randomSparseMutationSelector((int)i)),
                prototype: makeIndexDict(rangeSelect(1, 100, i => randomIndex()))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: rangeMultiSelect(0, 399, i => randomSparseMutationSelector((int)i)),
                prototype: makeIndexDict(rangeSelect(1, 100, i => randomIndex()))
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(200, valueDomain),
                mutations: rangeMultiSelect(0, 299, i => randomSparseMutationSelector((int)i)),
                prototype: makeIndexDict(rangeSelect(1, 50, i => randomIndex()))
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(accessTestWithSetAndDeleteMutations_data))]
        public void accessTestWithSetAndDeleteMutations(
            ArrayWrapper array, Image image, IEnumerable<Mutation> mutations, IndexDict prototype)
        {
            setPrototypeProperties(prototype);
            setRandomSeed(307114);
            try {
                verifyArrayMatchesImage(array.instance, image);
                foreach (var mut in mutations)
                    applyMutationAndVerify(array.instance, ref image, mut);
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> accessTestWithSetDeleteAndLengthChange_data() {
            setRandomSeed(71120);

            const int numValues = 50;
            const uint maxLength = UInt32.MaxValue;
            const uint maxIndex = UInt32.MaxValue - 1;

            var valueDomain = makeUniqueValues(numValues);
            valueDomain[6] = ASAny.undefined;
            valueDomain[14] = ASAny.undefined;
            valueDomain[28] = ASAny.undefined;
            valueDomain[32] = ASAny.@null;
            valueDomain[41] = ASAny.@null;

            var testcases = new List<(ArrayWrapper, Image, IEnumerable<Mutation>, IndexDict)>();

            void addTestCase((ASArray arr, Image img) initialState, IEnumerable<Mutation> mutations, IndexDict prototype = null) =>
                testcases.Add((new ArrayWrapper(initialState.arr), initialState.img, mutations, prototype));

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: new[] {
                    mutSetLength(0),
                    mutSetLength(4),
                    mutSetLength(1000),
                    mutSetLength(60),
                    mutSetLength(0),
                    mutSetLength(10000),
                    mutSetLength(maxLength),
                    mutSetLength(0),
                    mutSetLength(200),
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(100),
                mutations: new[] {
                    mutSetLength(200),
                    mutSetLength(400),
                    mutSetLength(400),
                    mutSetLength(100),
                    mutSetLength(0),
                    mutSetLength(0),
                    mutSetLength(maxLength),
                    mutSetLength(100),
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: new[] {
                    mutSetLength(maxLength),
                    mutSetLength(maxLength - 1),
                    mutSetLength(maxLength / 2),
                    mutSetLength(1),
                }
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(100, valueDomain),
                mutations: new[] {
                    mutSetLength(100),
                    mutSetLength(101),
                    mutSetLength(200),
                    mutSetLength(50),
                    mutSetLength(100),
                    mutSetLength(maxLength),
                    mutSetLength(100),
                    mutSetLength(25),
                    mutSetLength(24),
                    mutSetLength(0),
                    mutSetLength(50),
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    mutSetLength(200),
                    rangeSelect(100, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    mutSetLength(220),
                    rangeSelect(200, 224, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(50),
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    mutSetLength(50),
                    rangeSelect(50, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    mutSetLength(10),
                    rangeSelect(10, 49, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    mutSetLength(80),
                    rangeSelect(50, 89, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    mutSetLength(0),
                    rangeSelect(0, 49, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    mutSetLength(maxLength),
                    rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(100, valueDomain),
                mutations: buildMutationList(
                    mutSetLength(50),
                    rangeSelect(50, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    mutSetLength(20),
                    rangeSelect(20, 49, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    mutSetLength(0),
                    rangeSelect(0, 49, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    mutSetLength(60),
                    rangeSelect(50, 79, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeArrayAndImageWithValues(new ASAny[50]),
                mutations: buildMutationList(
                    mutSetLength(100),
                    mutSetLength(10),
                    rangeSelect(5, 39, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    mutSetLength(0),
                    rangeSelect(0, 39, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(99, 50, i => mutDelRandomIndexType(i)),
                    mutSetLength(50),
                    rangeSelect(49, 10, i => mutDelRandomIndexType(i)),
                    mutSetLength(10),
                    rangeSelect(9, 0, i => mutDelRandomIndexType(i)),
                    mutSetLength(0),
                    rangeSelect(0, 49, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    mutSetLength(100),
                    rangeSelect(50, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(99, 15, i => mutDelRandomIndexType(i)),
                    mutSetLength(20),
                    mutSetLength(10),
                    rangeSelect(20, 5, i => mutDelRandomIndexType(i)),
                    mutSetLength(20),
                    rangeSelect(5, 29, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(100, valueDomain),
                mutations: buildMutationList(
                    rangeSelect(60, 99, i => mutDelRandomIndexType(i)),
                    mutSetLength(60),
                    rangeSelect(30, 69, i => mutDelRandomIndexType(i)),
                    mutSetLength(30),
                    rangeSelect(99, 30, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(30, 99, i => mutDelRandomIndexType(i)),
                    mutSetLength(90),
                    mutSetLength(40),
                    mutSetLength(30),
                    mutSetLength(20)
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(40, 59, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(80, 99, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(120, 139, i => mutSetUint(i, randomSample(valueDomain))),
                    mutSetLength(120),
                    mutSetLength(100),
                    mutSetLength(80),
                    mutSetLength(60),
                    mutSetLength(40),
                    mutSetLength(20),
                    rangeSelect(50, 69, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(100, 119, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(150, 169, i => mutSetUint(i, randomSample(valueDomain))),
                    mutSetLength(150),
                    mutSetLength(120),
                    mutSetLength(100),
                    mutSetLength(70),
                    mutSetLength(50),
                    mutSetLength(20)
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(maxIndex, maxIndex - 19, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(maxIndex, maxIndex - 19, i => mutDelUint(i)),
                    mutSetLength(maxIndex - 19),
                    mutSetLength(20)
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(20, valueDomain),
                mutations: buildMutationList(
                    mutSetLength(maxLength),
                    rangeSelect(1, 40, i => mutSetUint(randomIndex(1000), randomSample(valueDomain))),
                    rangeSelect(1, 40, i => mutSetUint(maxIndex / 2 - 500 + randomIndex(1000), randomSample(valueDomain))),
                    rangeSelect(1, 40, i => mutSetUint(maxIndex - 999 + randomIndex(1000), randomSample(valueDomain))),
                    mutSetLength(maxIndex - 499),
                    mutSetLength(maxIndex - 999),
                    mutSetLength(maxIndex / 2),
                    mutSetLength(maxIndex / 2 - 500),
                    mutSetLength(maxIndex / 4),
                    mutSetLength(1000),
                    mutSetLength(25),
                    rangeSelect(1, 10, i => mutSetUint(maxIndex - 999 + randomIndex(1000), randomSample(valueDomain))),
                    mutSetLength(25)
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                prototype: makeIndexDict(1, 4, 5, 10, 29, 34, 68, 93, 104, 1500, maxIndex),
                mutations: buildMutationList(
                    mutSetLength(100),
                    mutSetLength(50),
                    mutSetLength(5),
                    rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                    mutSetLength(50),
                    mutSetLength(0),
                    rangeSelect(0, 49, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(1500, 1549, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(1500, 1549, i => mutDelUint(i)),
                    mutSetLength(1500),
                    mutSetLength(50),
                    mutSetLength(5),
                    mutSetLength(100),
                    mutSetLength(maxLength)
                )
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(accessTestWithSetDeleteAndLengthChange_data))]
        public void accessTestWithSetDeleteAndLengthChange(
            ArrayWrapper array, Image image, IEnumerable<Mutation> mutations, IndexDict prototype)
        {
            setPrototypeProperties(prototype);
            setRandomSeed(541169);
            try {
                verifyArrayMatchesImage(array.instance, image);
                foreach (var mut in mutations)
                    applyMutationAndVerify(array.instance, ref image, mut);
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> accessTestWithPushAndPop_data() {
            setRandomSeed(570016);

            const int numValues = 50;
            const uint maxLength = UInt32.MaxValue;
            const uint maxIndex = UInt32.MaxValue - 1;

            var valueDomain = makeUniqueValues(numValues);
            valueDomain[6] = ASAny.undefined;
            valueDomain[14] = ASAny.undefined;
            valueDomain[28] = ASAny.undefined;
            valueDomain[32] = ASAny.@null;
            valueDomain[41] = ASAny.@null;

            var testcases = new List<(ArrayWrapper, Image, IEnumerable<Mutation>, IndexDict)>();

            void addTestCase((ASArray arr, Image img) initialState, IEnumerable<Mutation> mutations, IndexDict prototype = null) =>
                testcases.Add((new ArrayWrapper(initialState.arr), initialState.img, mutations, prototype));

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(1, 100, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(1, 102, i => mutPop()),
                    rangeSelect(1, 100, i => mutPush(randomSample(valueDomain))),
                    rangeSelect(1, 100, i => mutPop()),
                    mutPushOne(randomSample(valueDomain)),
                    mutPushOne(randomSample(valueDomain)),
                    mutPushOne(randomSample(valueDomain)),
                    mutPush(randomSample(valueDomain)),
                    mutPush(randomSample(valueDomain)),
                    rangeSelect(1, 100, i => mutPop()),
                    rangeSelect(1, 20, i => mutPushOne(ASAny.undefined)),
                    rangeSelect(1, 25, i => mutPop())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(20),
                mutations: rangeSelect(1, 25, i => mutPop())
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(20, valueDomain),
                mutations: rangeSelect(1, 25, i => mutPop())
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(20),
                mutations: buildMutationList(
                    rangeSelect(1, 20, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(1, 25, i => mutPop()),
                    rangeSelect(1, 20, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(1, 30, i => mutPop()),
                    rangeSelect(1, 10, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(1, 15, i => mutPop())
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(20, valueDomain),
                mutations: buildMutationList(
                    rangeSelect(1, 20, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(1, 25, i => mutPop()),
                    rangeSelect(1, 20, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(1, 30, i => mutPop()),
                    rangeSelect(1, 10, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(1, 15, i => mutPop())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(0),
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(0, 99, i => mutPop())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(20, 39, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(40, 59, i => mutSetUint(i, randomSample(valueDomain))),
                    mutPush(rangeSelect(60, 69, i => randomSample(valueDomain))),
                    rangeSelect(70, 79, i => mutSetUint(i, randomSample(valueDomain))),
                    mutPush(rangeSelect(80, 89, i => randomSample(valueDomain))),
                    rangeSelect(90, 99, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(99, 0, i => mutPop())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 29, i => mutPush(randomSample(valueDomain))),
                    rangeSelect(29, 20, i => mutDelUint(i)),
                    rangeSelect(30, 59, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(59, 50, i => mutDelUint(i)),
                    rangeSelect(60, 89, i => mutPush(randomSample(valueDomain))),
                    rangeSelect(89, 80, i => mutDelUint(i)),
                    mutPush(rangeSelect(90, 119, i => randomSample(valueDomain))),
                    rangeSelect(119, 110, i => mutDelUint(i)),
                    rangeSelect(119, 0, i => mutPop())
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(50, valueDomain),
                mutations: buildMutationList(
                    mutSetLength(40),
                    rangeSelect(40, 69, i => mutPushOne(randomSample(valueDomain))),
                    mutSetLength(60),
                    mutPush(rangeSelect(60, 89, i => randomSample(valueDomain))),
                    mutSetLength(80),
                    rangeSelect(80, 99, i => mutPushOne(randomSample(valueDomain))),
                    mutSetLength(110),
                    rangeSelect(109, 90, i => mutPop()),
                    mutSetLength(95),
                    rangeSelect(94, 65, i => mutPop()),
                    mutSetLength(75),
                    rangeSelect(74, 30, i => mutPop()),
                    mutSetLength(40),
                    rangeSelect(39, 0, i => mutPop()),
                    mutSetLength(5),
                    rangeSelect(9, 0, i => mutPop())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutPushOne(randomSample(valueDomain))),
                    mutSetLength(40),
                    mutPush(rangeSelect(40, 49, i => randomSample(valueDomain))),
                    rangeSelect(50, 59, i => mutPush(randomSample(valueDomain))),
                    mutSetUint(80, randomSample(valueDomain)),
                    rangeSelect(81, 99, i => mutPushOne(randomSample(valueDomain))),
                    mutSetLength(120),
                    rangeSelect(120, 139, i => mutPushOne(randomSample(valueDomain))),
                    mutSetLength(150),
                    rangeSelect(149, 70, i => mutPop()),
                    rangeSelect(70, 79, i => mutPushOne(randomSample(valueDomain))),
                    mutSetIndexUint(40, randomSample(valueDomain)),
                    rangeSelect(79, 0, i => mutPop())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutPushOne(randomSample(valueDomain))),
                    mutSetLength(50),
                    rangeSelect(50, 99, i => mutPushOne(randomSample(valueDomain))),
                    mutSetLength(160),
                    rangeSelect(160, 169, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(169, 140, i => mutPop()),
                    rangeSelect(140, 149, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(149, 0, i => mutPop())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    mutPush(rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutSetUint(50, ASAny.undefined),
                    mutPush(rangeSelect(50, 99, i => randomSample(valueDomain))),
                    mutSetLength(160),
                    mutPush(rangeSelect(160, 169, i => randomSample(valueDomain))),
                    rangeSelect(169, 140, i => mutPop()),
                    mutPush(rangeSelect(140, 149, i => randomSample(valueDomain))),
                    rangeSelect(149, 0, i => mutPop())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    mutSetUint(0, randomSample(valueDomain)),
                    mutSetLength(14),
                    mutPush(rangeSelect(1, 4, i => randomSample(valueDomain))),
                    rangeSelect(25, 49, i => mutSetUint(i, randomSample(valueDomain))),
                    mutSetLength(70),
                    mutPush(rangeSelect(70, 74, i => randomSample(valueDomain))),
                    mutPush(rangeSelect(75, 94, i => randomSample(valueDomain))),
                    rangeSelect(94, 0, i => mutPop())
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(30, valueDomain),
                mutations: buildMutationList(
                    mutSetLength(maxIndex / 2),
                    rangeSelect(1, 20, i => mutPushOne(randomSample(valueDomain))),
                    mutSetLength(maxLength - 20),
                    mutPush(rangeSelect(1, 20, i => randomSample(valueDomain))),
                    rangeSelect(1, 20, i => mutPop()),
                    mutSetLength(maxIndex / 2 + 10),
                    rangeSelect(1, 15, i => mutPop()),
                    mutSetUint(10000, randomSample(valueDomain)),
                    rangeSelect(1, 10, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(0, 4, i => mutDelUint(10000 + i)),
                    rangeSelect(1, 12, i => mutPop()),
                    mutSetLength(25),
                    rangeSelect(1, 25, i => mutPop())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: new[] {mutPushOne(valueDomain[0]), mutPushOne(valueDomain[1])}
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength - 10),
                mutations: buildMutationList(
                    rangeSelect(0, 9, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(1, 12, i => mutPop()),
                    rangeSelect(1, 15, i => mutPushOne(randomSample(valueDomain))),
                    mutSetLength(maxLength - 20),
                    rangeSelect(1, 25, i => mutPushOne(randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength - 10),
                mutations: buildMutationList(
                    mutPush(rangeSelect(0, 9, i => randomSample(valueDomain))),
                    rangeSelect(1, 12, i => mutPop()),
                    rangeSelect(1, 15, i => mutPush(randomSample(valueDomain))),
                    mutSetLength(maxLength - 20),
                    mutPush(rangeSelect(1, 10, i => randomSample(valueDomain))),
                    mutPush(rangeSelect(1, 15, i => randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(10),
                prototype: makeIndexDict(0, 4, 12, 23, 26, 29, 35, 43, 47, 70, 103, 14832),
                mutations: buildMutationList(
                    rangeSelect(9, 0, i => mutPop()),
                    rangeSelect(0, 19, i => mutPushOne(randomSample(valueDomain))),
                    mutSetLength(30),
                    rangeSelect(30, 49, i => mutPush(randomSample(valueDomain))),
                    mutSetUint(75, randomSample(valueDomain)),
                    rangeSelect(75, 0, i => mutPop())
                )
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(accessTestWithPushAndPop_data))]
        public void accessTestWithPushAndPop(
            ArrayWrapper array, Image image, IEnumerable<Mutation> mutations, IndexDict prototype)
        {
            setPrototypeProperties(prototype);
            setRandomSeed(7013492);
            try {
                verifyArrayMatchesImage(array.instance, image);
                foreach (var mut in mutations) {
                    applyMutationAndVerify(array.instance, ref image, mut);
                    Assert.False(array.instance.AS_hasElement(UInt32.MaxValue));
                }
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> accessTestWithShiftAndUnshift_data() {
            setRandomSeed(570016);

            const int numValues = 50;
            const uint maxLength = UInt32.MaxValue;

            var valueDomain = makeUniqueValues(numValues);
            valueDomain[6] = ASAny.undefined;
            valueDomain[14] = ASAny.undefined;
            valueDomain[28] = ASAny.undefined;
            valueDomain[32] = ASAny.@null;
            valueDomain[41] = ASAny.@null;

            var testcases = new List<(ArrayWrapper, Image, IEnumerable<Mutation>, IndexDict)>();

            void addTestCase((ASArray arr, Image img) initialState, IEnumerable<Mutation> mutations, IndexDict prototype = null) =>
                testcases.Add((new ArrayWrapper(initialState.arr), initialState.img, mutations, prototype));

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(1, 100, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(1, 102, i => mutShift()),
                    rangeSelect(1, 100, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(1, 100, i => mutShift()),
                    mutUnshift(randomSample(valueDomain)),
                    mutUnshift(randomSample(valueDomain)),
                    mutUnshift(randomSample(valueDomain)),
                    mutUnshift(randomSample(valueDomain)),
                    mutUnshift(randomSample(valueDomain)),
                    rangeSelect(1, 100, i => mutShift()),
                    rangeSelect(1, 20, i => mutUnshift(ASAny.undefined)),
                    rangeSelect(1, 25, i => mutShift())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(20),
                mutations: rangeSelect(1, 25, i => mutShift())
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(20, valueDomain),
                mutations: rangeSelect(1, 25, i => mutShift())
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(20),
                mutations: buildMutationList(
                    rangeSelect(1, 20, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(1, 25, i => mutShift()),
                    rangeSelect(1, 20, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(1, 30, i => mutShift()),
                    rangeSelect(1, 10, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(1, 15, i => mutShift())
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(20, valueDomain),
                mutations: buildMutationList(
                    rangeSelect(1, 20, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(1, 25, i => mutShift()),
                    rangeSelect(1, 20, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(1, 30, i => mutShift()),
                    rangeSelect(1, 10, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(1, 15, i => mutShift())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(0, 99, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(0, 99, i => mutShift())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(20, 39, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(40, 59, i => mutSetUint(i, randomSample(valueDomain))),
                    mutUnshift(rangeSelect(60, 69, i => randomSample(valueDomain))),
                    rangeSelect(70, 79, i => mutSetUint(i, randomSample(valueDomain))),
                    mutUnshift(rangeSelect(80, 89, i => randomSample(valueDomain))),
                    rangeSelect(90, 99, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(99, 0, i => mutShift())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 29, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(29, 25, i => mutDelUint(i)),
                    rangeSelect(0, 4, i => mutDelUint(i)),
                    mutUnshift(rangeSelect(0, 29, i => randomSample(valueDomain))),
                    rangeSelect(29, 25, i => mutDelUint(i)),
                    rangeSelect(0, 4, i => mutDelUint(i)),
                    rangeSelect(0, 29, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(0, 38, i => mutDelUint(i)),
                    rangeSelect(45, 54, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(80, 89, i => mutDelUint(i)),
                    rangeSelect(0, 49, i => mutUnshift()),
                    rangeSelect(0, 9, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(0, 51, i => mutUnshift())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 29, i => mutUnshift(randomSample(valueDomain))),
                    mutSetLength(40),
                    rangeSelect(0, 29, i => mutUnshift(randomSample(valueDomain))),
                    mutSetLength(80),
                    rangeSelect(0, 44, i => mutShift()),
                    mutSetLength(25),
                    rangeSelect(0, 24, i => mutShift())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(30),
                mutations: buildMutationList(
                    rangeSelect(30, 39, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(0, 29, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(0, 69, i => mutShift()),
                    mutSetLength(30),
                    mutPush(rangeSelect(0, 9, i => randomSample(valueDomain))),
                    mutUnshift(rangeSelect(0, 14, i => randomSample(valueDomain))),
                    mutUnshift(rangeSelect(0, 14, i => randomSample(valueDomain))),
                    rangeSelect(0, 69, i => mutShift())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(0, 19, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(0, 19, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(0, 19, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(0, 29, i => mutPop()),
                    rangeSelect(0, 29, i => mutUnshift()),
                    mutUnshift(rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutPush(rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutUnshift(rangeSelect(0, 19, i => randomSample(valueDomain))),
                    mutPush(rangeSelect(0, 19, i => randomSample(valueDomain))),
                    rangeSelect(0, 29, i => mutPop()),
                    rangeSelect(0, 29, i => mutUnshift()),
                    rangeSelect(0, 19, i => mutUnshift()),
                    rangeSelect(0, 19, i => mutPop())
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(40, valueDomain),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(0, 19, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(0, 39, i => mutPop()),
                    rangeSelect(0, 39, i => mutShift())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(20),
                mutations: buildMutationList(
                    rangeSelect(20, 39, i => mutSetUint(i, randomSample(valueDomain))),
                    mutSetLength(80),
                    rangeSelect(80, 99, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(0, 79, i => mutUnshift(randomSample(valueDomain))),
                    mutSetLength(140),
                    rangeSelect(0, 69, i => mutShift()),
                    mutSetLength(0),
                    rangeSelect(20, 39, i => mutSetUint(i, randomSample(valueDomain))),
                    mutSetLength(80),
                    mutPush(rangeSelect(80, 99, i => randomSample(valueDomain))),
                    mutSetLength(140),
                    mutUnshift(rangeSelect(0, 79, i => randomSample(valueDomain))),
                    rangeSelect(0, 89, i => mutShift())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    mutSetUint(12, randomSample(valueDomain)),
                    mutUnshift(rangeSelect(0, 3, i => randomSample(valueDomain))),
                    mutUnshift(rangeSelect(0, 39, i => randomSample(valueDomain))),
                    rangeSelect(30, 39, i => mutDelUint(i)),
                    rangeSelect(9, 0, i => mutDelUint(i)),
                    mutUnshift(rangeSelect(0, 39, i => randomSample(valueDomain))),
                    rangeSelect(0, 19, i => mutUnshift(randomSample(valueDomain))),
                    mutUnshift(rangeSelect(0, 14, i => ASAny.undefined)),
                    rangeSelect(0, 14, i => mutDelUint(i)),
                    rangeSelect(1, 92, i => mutUnshift())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(10000000),
                mutations: buildMutationList(
                    rangeSelect(1, 20, i => mutSetUint(10000 * i, randomSample(valueDomain))),
                    rangeSelect(1, 10, i => mutUnshift(randomSample(valueDomain))),
                    mutSetLength(120000),
                    mutUnshift(rangeSelect(1, 20, i => randomSample(valueDomain))),
                    rangeSelect(1, 20, i => mutShift()),
                    mutSetLength(20),
                    rangeSelect(1, 20, i => mutShift())
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength - 30),
                mutations: buildMutationList(
                    mutPush(rangeSelect(1, 30, i => randomSample(valueDomain))),
                    rangeSelect(1, 10, i => mutShift()),
                    mutPush(rangeSelect(1, 10, i => randomSample(valueDomain))),
                    rangeSelect(1, 10, i => mutUnshift(randomSample(valueDomain))),
                    mutUnshift(rangeSelect(1, 20, i => randomSample(valueDomain))),
                    mutSetLength(30),
                    mutSetLength(maxLength),
                    rangeSelect(1, 10, i => mutUnshift(randomSample(valueDomain))),
                    mutUnshift(rangeSelect(1, 10, i => randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                prototype: makeIndexDict(0, 1, 10, 11, 12, 13, 14, 25, 26, 27, 28, 29, 45, 90, 1000),
                mutations: buildMutationList(
                    rangeSelect(0, 4, i => mutShift()),
                    rangeSelect(0, 9, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(23, 27, i => mutSetUint(i, randomSample(valueDomain))),
                    mutUnshift(rangeSelect(0, 4, i => randomSample(valueDomain))),
                    rangeSelect(0, 32, i => mutShift())
                )
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(accessTestWithShiftAndUnshift_data))]
        public void accessTestWithShiftAndUnshift(
            ArrayWrapper array, Image image, IEnumerable<Mutation> mutations, IndexDict prototype)
        {
            setPrototypeProperties(prototype);
            setRandomSeed(7013492);
            try {
                verifyArrayMatchesImage(array.instance, image);
                foreach (var mut in mutations) {
                    applyMutationAndVerify(array.instance, ref image, mut);
                    Assert.False(array.instance.AS_hasElement(UInt32.MaxValue));
                }
            }
            finally {
                resetPrototypeProperties();
            }
        }

        [Fact]
        public void accessTestWithPrototypeChange() {
            ASArray array = new ASArray();
            Image image = makeEmptyImage();

            setPrototypeProperties(makeIndexDict(1, 5, 10, 12));
            setRandomSeed(58912);

            try {
                verifyArrayMatchesImage(array, image);

                applyMutationAndVerify(array, ref image, mutSetUint(4, new ASObject()));
                applyMutationAndVerify(array, ref image, mutSetUint(7, new ASObject()));
                applyMutationAndVerify(array, ref image, mutSetUint(11, new ASObject()));

                setPrototypeProperties(makeIndexDict(3, 4, 9, 11, 12));
                verifyArrayMatchesImage(array, image);

                applyMutationAndVerify(array, ref image, mutDelUint(7));

                setPrototypeProperties(makeIndexDict(4, 7, 11, 12, 13));
                verifyArrayMatchesImage(array, image);
            }
            finally {
                resetPrototypeProperties();
            }
        }

        [Fact]
        public void accessTestWithInvalidIndices() {
            var values = makeUniqueValues(20);
            setPrototypeProperties(null);

            try {
                var array = new ASArray();
                var arrayProto = Class.fromType(typeof(ASArray)).prototypeObject.AS_dynamicProps;

                array.AS_setElement(0, values[14]);
                array.AS_setElement(12, values[15]);
                array.AS_setElement(5, values[16]);

                array.AS_dynamicProps["+12"] = values[0];
                array.AS_dynamicProps["012"] = values[1];
                array.AS_dynamicProps["00"] = values[2];
                array.AS_dynamicProps["-12"] = values[3];
                array.AS_dynamicProps["5.0"] = values[4];
                array.AS_dynamicProps["5.5"] = values[5];
                array.AS_dynamicProps["0.5"] = values[6];
                array.AS_dynamicProps["-1.5"] = values[7];
                array.AS_dynamicProps["4294967295"] = values[8];
                array.AS_dynamicProps["4294967296"] = values[9];
                array.AS_dynamicProps["Infinity"] = values[10];
                array.AS_dynamicProps["-Infinity"] = values[11];
                array.AS_dynamicProps["NaN"] = values[12];
                array.AS_dynamicProps["-2147483648"] = values[13];

                AssertHelper.identical(values[14], array.AS_getElement(0));
                AssertHelper.identical(values[15], array.AS_getElement(12));
                AssertHelper.identical(values[16], array.AS_getElement(5));

                Assert.True(array.AS_hasElement(-12));
                Assert.True(array.AS_hasElement(-12.0));
                Assert.True(array.AS_hasElement(-2147483648));
                Assert.True(array.AS_hasElement(-2147483648.0));
                Assert.True(array.AS_hasElement(5.5));
                Assert.True(array.AS_hasElement(0.5));
                Assert.True(array.AS_hasElement(-1.5));
                Assert.True(array.AS_hasElement(4294967295));
                Assert.True(array.AS_hasElement(4294967295.0));
                Assert.True(array.AS_hasElement(4294967296.0));
                Assert.True(array.AS_hasElement(Double.PositiveInfinity));
                Assert.True(array.AS_hasElement(Double.NegativeInfinity));
                Assert.True(array.AS_hasElement(Double.NaN));

                AssertHelper.identical(values[3], array.AS_getElement(-12));
                AssertHelper.identical(values[3], array.AS_getElement(-12.0));
                AssertHelper.identical(values[13], array.AS_getElement(-2147483648));
                AssertHelper.identical(values[13], array.AS_getElement(-2147483648.0));
                AssertHelper.identical(values[5], array.AS_getElement(5.5));
                AssertHelper.identical(values[6], array.AS_getElement(0.5));
                AssertHelper.identical(values[7], array.AS_getElement(-1.5));
                AssertHelper.identical(values[8], array.AS_getElement(4294967295));
                AssertHelper.identical(values[8], array.AS_getElement(4294967295.0));
                AssertHelper.identical(values[9], array.AS_getElement(4294967296.0));
                AssertHelper.identical(values[10], array.AS_getElement(Double.PositiveInfinity));
                AssertHelper.identical(values[11], array.AS_getElement(Double.NegativeInfinity));
                AssertHelper.identical(values[12], array.AS_getElement(Double.NaN));

                AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, () => array[-1]);
                AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, () => array[-12]);
                AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, () => array[-2147483648]);

                array.AS_setElement(-12.0, values[19]);
                array.AS_setElement(-2147483648, values[19]);
                array.AS_setElement(0.5, values[19]);
                array.AS_setElement(-1.5, values[19]);
                array.AS_setElement(4294967295, values[19]);
                array.AS_setElement(Double.NaN, values[19]);
                array.AS_setElement(1E+148, values[19]);

                AssertHelper.throwsErrorWithCode(
                    ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, () => array[-12] = values[0]
                );
                AssertHelper.throwsErrorWithCode(
                    ErrorCode.MARIANA__ARGUMENT_OUT_OF_RANGE, () => array[-2147483648] = values[0]
                );

                Assert.True(array.AS_hasElement(1E+148));

                AssertHelper.identical(values[19], array.AS_getElement(-12));
                AssertHelper.identical(values[19], array.AS_getElement(-12.0));
                AssertHelper.identical(values[19], array.AS_getElement(-2147483648));
                AssertHelper.identical(values[19], array.AS_getElement(-2147483648.0));
                AssertHelper.identical(values[19], array.AS_getElement(0.5));
                AssertHelper.identical(values[19], array.AS_getElement(-1.5));
                AssertHelper.identical(values[19], array.AS_getElement(4294967295));
                AssertHelper.identical(values[19], array.AS_getElement(4294967295.0));
                AssertHelper.identical(values[19], array.AS_getElement(1E+148));
                AssertHelper.identical(values[19], array.AS_getElement(Double.NaN));

                AssertHelper.identical(values[0], array.AS_dynamicProps["+12"]);
                AssertHelper.identical(values[1], array.AS_dynamicProps["012"]);
                AssertHelper.identical(values[2], array.AS_dynamicProps["00"]);
                AssertHelper.identical(values[19], array.AS_dynamicProps["-12"]);
                AssertHelper.identical(values[4], array.AS_dynamicProps["5.0"]);
                AssertHelper.identical(values[5], array.AS_dynamicProps["5.5"]);
                AssertHelper.identical(values[19], array.AS_dynamicProps["0.5"]);
                AssertHelper.identical(values[19], array.AS_dynamicProps["-1.5"]);
                AssertHelper.identical(values[19], array.AS_dynamicProps["-2147483648"]);
                AssertHelper.identical(values[19], array.AS_dynamicProps["4294967295"]);
                AssertHelper.identical(values[9], array.AS_dynamicProps["4294967296"]);
                AssertHelper.identical(values[19], array.AS_dynamicProps[NumberFormatHelper.doubleToString(1E+148)]);
                AssertHelper.identical(values[10], array.AS_dynamicProps["Infinity"]);
                AssertHelper.identical(values[11], array.AS_dynamicProps["-Infinity"]);
                AssertHelper.identical(values[19], array.AS_dynamicProps["NaN"]);

                array.AS_deleteElement(Double.NegativeInfinity);
                array.AS_deleteElement(Double.NaN);
                array.AS_deleteElement(4294967295);
                array.AS_deleteElement(4294967296.0);
                array.AS_deleteElement(5.5);
                array.AS_deleteElement(-12);

                Assert.False(array.AS_dynamicProps.hasValue("-Infinity"));
                Assert.False(array.AS_dynamicProps.hasValue("NaN"));
                Assert.False(array.AS_dynamicProps.hasValue("4294967295"));
                Assert.False(array.AS_dynamicProps.hasValue("4294967296"));
                Assert.False(array.AS_dynamicProps.hasValue("5.5"));
                Assert.False(array.AS_dynamicProps.hasValue("-12"));

                Assert.False(array.AS_hasElement(-12));
                Assert.False(array.AS_hasElement(-12.0));
                Assert.True(array.AS_hasElement(-2147483648));
                Assert.True(array.AS_hasElement(-2147483648.0));
                Assert.False(array.AS_hasElement(5.5));
                Assert.True(array.AS_hasElement(0.5));
                Assert.True(array.AS_hasElement(-1.5));
                Assert.False(array.AS_hasElement(4294967295));
                Assert.False(array.AS_hasElement(4294967295.0));
                Assert.False(array.AS_hasElement(4294967296.0));
                Assert.True(array.AS_hasElement(1E+148));
                Assert.True(array.AS_hasElement(Double.PositiveInfinity));
                Assert.False(array.AS_hasElement(Double.NegativeInfinity));
                Assert.False(array.AS_hasElement(Double.NaN));

                arrayProto["0.5"] = values[1];
                arrayProto["1.5"] = values[2];
                arrayProto["5.5"] = values[3];
                arrayProto["-1.5"] = values[4];
                arrayProto["-5.5"] = values[5];
                arrayProto["-16"] = values[6];
                arrayProto["4294967295"] = values[7];
                arrayProto["NaN"] = values[8];

                Assert.False(array.AS_deleteElement(1.5));
                Assert.False(array.AS_deleteElement(5.5));
                Assert.False(array.AS_deleteElement(-5.5));
                Assert.False(array.AS_deleteElement(-16));
                Assert.False(array.AS_deleteElement(-16.0));
                Assert.False(array.AS_deleteElement(4294967295));
                Assert.False(array.AS_deleteElement(4294967295.0));
                Assert.False(array.AS_deleteElement(Double.NaN));

                Assert.True(array.AS_hasElement(-2147483648));
                Assert.True(array.AS_hasElement(-2147483648.0));
                Assert.True(array.AS_hasElement(5.5));
                Assert.True(array.AS_hasElement(-5.5));
                Assert.True(array.AS_hasElement(0.5));
                Assert.True(array.AS_hasElement(1.5));
                Assert.True(array.AS_hasElement(-1.5));
                Assert.True(array.AS_hasElement(-16));
                Assert.True(array.AS_hasElement(-16.0));
                Assert.True(array.AS_hasElement(4294967295));
                Assert.True(array.AS_hasElement(4294967295.0));
                Assert.True(array.AS_hasElement(1E+148));
                Assert.True(array.AS_hasElement(Double.PositiveInfinity));
                Assert.True(array.AS_hasElement(Double.NaN));

                AssertHelper.identical(values[6], array.AS_getElement(-16));
                AssertHelper.identical(values[6], array.AS_getElement(-16.0));
                AssertHelper.identical(values[19], array.AS_getElement(-2147483648));
                AssertHelper.identical(values[19], array.AS_getElement(-2147483648.0));
                AssertHelper.identical(values[19], array.AS_getElement(0.5));
                AssertHelper.identical(values[3], array.AS_getElement(5.5));
                AssertHelper.identical(values[5], array.AS_getElement(-5.5));
                AssertHelper.identical(values[19], array.AS_getElement(-1.5));
                AssertHelper.identical(values[2], array.AS_getElement(1.5));
                AssertHelper.identical(values[7], array.AS_getElement(4294967295));
                AssertHelper.identical(values[7], array.AS_getElement(4294967295.0));
                AssertHelper.identical(values[19], array.AS_getElement(1E+148));
                AssertHelper.identical(values[10], array.AS_getElement(Double.PositiveInfinity));
                AssertHelper.identical(values[8], array.AS_getElement(Double.NaN));

                arrayProto.delete("0.5");
                arrayProto.delete("1.5");
                arrayProto.delete("5.5");
                arrayProto.delete("-1.5");
                arrayProto.delete("-5.5");
                arrayProto.delete("-16");
                arrayProto.delete("4294967295");
                arrayProto.delete("NaN");
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> cloneTest_data() {
            setRandomSeed(4766033);

            const int numValues = 50;
            const uint maxLength = UInt32.MaxValue;
            const uint maxIndex = UInt32.MaxValue - 1;

            var valueDomain = makeUniqueValues(numValues);
            valueDomain[6] = ASAny.undefined;
            valueDomain[14] = ASAny.undefined;
            valueDomain[28] = ASAny.undefined;
            valueDomain[32] = ASAny.@null;
            valueDomain[41] = ASAny.@null;

            var testcases = new List<(ArrayWrapper, Image, IndexDict)>();

            void addTestCase((ASArray arr, Image img) initialState, IEnumerable<Mutation> mutations = null, IndexDict prototype = null) {
                applyMutations(initialState.arr, mutations);
                applyMutations(ref initialState.img, mutations);
                testcases.Add((new ArrayWrapper(initialState.arr), initialState.img, prototype));
            }

            addTestCase(makeEmptyArrayAndImage());
            addTestCase(makeEmptyArrayAndImage(16));
            addTestCase(makeEmptyArrayAndImage(1000));
            addTestCase(makeEmptyArrayAndImage(maxLength));
            addTestCase(makeRandomDenseArrayAndImage(10, valueDomain));
            addTestCase(makeRandomDenseArrayAndImage(1000, valueDomain));

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 199, i => mutSetUint(randomIndex(100), randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 199, i => mutSetUint(randomIndex(), randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: buildMutationList(
                    rangeSelect(0, 199, i => mutSetUint(randomIndex(100), randomSample(valueDomain))),
                    mutSetUint(maxIndex, randomSample(valueDomain))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 29, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(0, 29, i => mutUnshift(randomSample(valueDomain))),
                    mutPush(rangeSelect(0, 29, i => randomSample(valueDomain))),
                    mutUnshift(rangeSelect(0, 29, i => randomSample(valueDomain))),
                    rangeSelect(0, 9, i => mutPop()),
                    rangeSelect(0, 9, i => mutPop()),
                    rangeSelect(50, 59, i => mutDelUint(i)),
                    mutSetLength(90)
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                prototype: makeIndexDict(1, 4, 7, 14, 17, 18, 23),
                mutations: buildMutationList(
                    rangeSelect(0, 9, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(20, 39, i => mutSetUint(i, randomSample(valueDomain)))
                )
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(cloneTest_data))]
        public void cloneTest(ArrayWrapper array, Image image, IndexDict prototype) {
            setRandomSeed(74009);
            setPrototypeProperties(prototype);

            try {
                ASArray clone = array.instance.clone();
                verifyArrayMatchesImage(clone, image);

                // Mutate the original array and check that the clone is not mutated.

                if (array.instance.length > 0) {
                    for (int i = 0; i < 10; i++)
                        array.instance.AS_setElement(randomIndex(array.instance.length), new ASObject());

                    verifyArrayMatchesImage(clone, image);

                    for (int i = 0; i < 10; i++)
                        array.instance.AS_deleteElement(randomIndex(array.instance.length));

                    verifyArrayMatchesImage(clone, image);
                }

                for (int i = 0; i < 10; i++)
                    array.instance.push(new ASObject());

                verifyArrayMatchesImage(clone, image);
            }
            finally {
                resetPrototypeProperties();
            }
        }

    }

}
