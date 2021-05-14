using System;
using System.Collections.Generic;
using System.Linq;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    using IndexDict = Dictionary<uint, ASAny>;

    public partial class ASArrayTest {

        private static void verifyPropertyBindingHelper(
            Func<BindOptions, bool> hasPropFunc,
            Func<BindOptions, (BindStatus, ASAny)> getPropFunc,
            BindStatus statusOnObject = BindStatus.SUCCESS,
            BindStatus statusOnPrototype = BindStatus.SUCCESS,
            ASAny valueOnObject = default,
            ASAny valueOnPrototype = default,
            BindOptions additionalBindOpts = 0
        ) {
            void success(BindOptions bindOpts, ASAny expectedValue) {
                Assert.True(hasPropFunc(bindOpts | additionalBindOpts));
                var (status, value) = getPropFunc(bindOpts | additionalBindOpts);
                Assert.Equal(BindStatus.SUCCESS, status);
                AssertHelper.identical(expectedValue, value);
            }

            void fail(BindOptions bindOpts, BindStatus expectedStatus) {
                Assert.False(hasPropFunc(bindOpts | additionalBindOpts));
                var (status, value) = getPropFunc(bindOpts | additionalBindOpts);
                Assert.Equal(expectedStatus, status);
                AssertHelper.identical(ASAny.undefined, value);
            }

            if (statusOnObject == BindStatus.SUCCESS && statusOnPrototype == BindStatus.SUCCESS) {
                success(BindOptions.SEARCH_DYNAMIC, valueOnObject);
                success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE, valueOnObject);
                success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS, valueOnObject);
                success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE, valueOnObject);
                success(BindOptions.SEARCH_PROTOTYPE, valueOnPrototype);
                success(BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_TRAITS, valueOnPrototype);
            }
            else if (statusOnObject == BindStatus.SUCCESS) {
                success(BindOptions.SEARCH_DYNAMIC, valueOnObject);
                success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE, valueOnObject);
                success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS, valueOnObject);
                success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE, valueOnObject);
                fail(BindOptions.SEARCH_PROTOTYPE, statusOnPrototype);
                fail(BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_TRAITS, statusOnPrototype);
            }
            else if (statusOnPrototype == BindStatus.SUCCESS) {
                fail(BindOptions.SEARCH_DYNAMIC, statusOnObject);
                fail(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS, statusOnObject);
                success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE, valueOnPrototype);
                success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE, valueOnPrototype);
                success(BindOptions.SEARCH_PROTOTYPE, valueOnPrototype);
                success(BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_TRAITS, valueOnPrototype);
            }
            else {
                fail(BindOptions.SEARCH_DYNAMIC, statusOnObject);
                fail(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS, statusOnObject);
                fail(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE, statusOnObject);
                fail(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE, statusOnObject);
                fail(BindOptions.SEARCH_PROTOTYPE, statusOnPrototype);
                fail(BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_TRAITS, statusOnPrototype);
            }

            fail(BindOptions.SEARCH_TRAITS, BindStatus.NOT_FOUND);
            fail(0, BindStatus.NOT_FOUND);
        }

        private void verifyPropertyBindingCallConstructHelper(
            Func<BindOptions, ASAny[], (BindStatus, ASAny)> callPropFunc,
            Func<BindOptions, ASAny[], (BindStatus, ASAny)> constructPropFunc,
            ASObject receiver,
            ASAny[] callArgs,
            bool isValidKey = true,
            bool valueExistsOnObject = false,
            bool valueExistsOnPrototype = false,
            ASAny valueOnObject = default,
            ASAny valueOnPrototype = default,
            BindOptions additionalBindOpts = 0
        ) {
            void success(BindOptions bindOpts, SpyFunctionObject spyFuncObj) {
                BindStatus status;
                ASAny retval;

                ASObject actualReceiver = spyFuncObj.isMethodClosure ? spyFuncObj.storedReceiver : receiver;
                int callCount = spyFuncObj.getCallRecords().Length;

                (status, retval) = callPropFunc(bindOpts | additionalBindOpts, callArgs);

                Assert.Equal(BindStatus.SUCCESS, status);
                Assert.True(spyFuncObj.lastCall.isEqualTo(new SpyFunctionObject.CallRecord(actualReceiver, callArgs, retval, false)));

                (status, retval) = constructPropFunc(bindOpts | additionalBindOpts, callArgs);

                if (spyFuncObj.isMethodClosure) {
                    Assert.Equal(BindStatus.FAILED_NOTCONSTRUCTOR, status);
                    Assert.Equal(callCount + 1, spyFuncObj.getCallRecords().Length);
                }
                else {
                    Assert.Equal(BindStatus.SUCCESS, status);
                    Assert.Equal(callCount + 2, spyFuncObj.getCallRecords().Length);
                    Assert.True(spyFuncObj.lastCall.isEqualTo(new SpyFunctionObject.CallRecord(null, callArgs, retval, true)));
                }
            }

            void fail(BindOptions bindOpts, BindStatus expectedCallStatus, BindStatus expectedConstructStatus) {
                BindStatus status;
                ASAny retval;

                (status, retval) = callPropFunc(bindOpts | additionalBindOpts, callArgs);
                Assert.Equal(expectedCallStatus, status);
                AssertHelper.identical(ASAny.undefined, retval);

                (status, retval) = constructPropFunc(bindOpts | additionalBindOpts, callArgs);
                Assert.Equal(expectedConstructStatus, status);
                AssertHelper.identical(ASAny.undefined, retval);
            }

            var funcOnObject = valueOnObject.value as SpyFunctionObject;
            var funcOnPrototype = valueOnPrototype.value as SpyFunctionObject;

            BindStatus callStatusOnObject = BindStatus.SUCCESS;
            BindStatus constructStatusOnObject = BindStatus.SUCCESS;
            BindStatus callStatusOnPrototype = BindStatus.SUCCESS;
            BindStatus constructStatusOnPrototype = BindStatus.SUCCESS;

            if (!isValidKey) {
                callStatusOnObject = BindStatus.NOT_FOUND;
                constructStatusOnObject = BindStatus.NOT_FOUND;
            }
            else if (funcOnObject == null) {
                callStatusOnObject = BindStatus.FAILED_NOTFUNCTION;
                constructStatusOnObject = BindStatus.FAILED_NOTCONSTRUCTOR;
            }

            if (!isValidKey || !valueExistsOnPrototype) {
                callStatusOnPrototype = BindStatus.NOT_FOUND;
                constructStatusOnPrototype = BindStatus.NOT_FOUND;
            }
            else if (funcOnPrototype == null) {
                callStatusOnPrototype = BindStatus.FAILED_NOTFUNCTION;
                constructStatusOnPrototype = BindStatus.FAILED_NOTCONSTRUCTOR;
            }

            if (funcOnObject != null && funcOnPrototype != null) {
                success(BindOptions.SEARCH_DYNAMIC, funcOnObject);
                success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE, funcOnObject);
                success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS, funcOnObject);
                success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE, funcOnObject);
                success(BindOptions.SEARCH_PROTOTYPE, funcOnPrototype);
                success(BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_TRAITS, funcOnPrototype);
            }
            else if (funcOnObject != null) {
                success(BindOptions.SEARCH_DYNAMIC, funcOnObject);
                success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE, funcOnObject);
                success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS, funcOnObject);
                success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE, funcOnObject);
                fail(BindOptions.SEARCH_PROTOTYPE, callStatusOnPrototype, constructStatusOnPrototype);
                fail(BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_TRAITS, callStatusOnPrototype, constructStatusOnPrototype);
            }
            else if (funcOnPrototype != null) {
                fail(BindOptions.SEARCH_DYNAMIC, callStatusOnObject, constructStatusOnObject);
                fail(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS, callStatusOnObject, constructStatusOnObject);

                if (valueExistsOnObject) {
                    fail(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE, callStatusOnObject, constructStatusOnObject);
                    fail(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE, callStatusOnObject, constructStatusOnObject);
                }
                else {
                    success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE, funcOnPrototype);
                    success(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE, funcOnPrototype);
                }

                success(BindOptions.SEARCH_PROTOTYPE, funcOnPrototype);
                success(BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_TRAITS, funcOnPrototype);
            }
            else {
                fail(BindOptions.SEARCH_DYNAMIC, callStatusOnObject, constructStatusOnObject);
                fail(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS, callStatusOnObject, constructStatusOnObject);
                fail(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE, callStatusOnObject, constructStatusOnObject);
                fail(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE, callStatusOnObject, constructStatusOnObject);
                fail(BindOptions.SEARCH_PROTOTYPE, callStatusOnPrototype, constructStatusOnPrototype);
                fail(BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_TRAITS, callStatusOnPrototype, constructStatusOnPrototype);
            }

            fail(BindOptions.SEARCH_TRAITS, BindStatus.NOT_FOUND, BindStatus.NOT_FOUND);
            fail(0, BindStatus.NOT_FOUND, BindStatus.NOT_FOUND);
        }

        private static void verifyArrayPropBindingMatchesImage(ASArray array, Image image) {
            var nsSet1 = new NamespaceSet(Namespace.@public);
            var nsSet2 = new NamespaceSet(new Namespace("a"), Namespace.@public, new Namespace(NamespaceKind.PROTECTED, "b"));
            var nsSet3 = new NamespaceSet(new Namespace("a"), new Namespace(NamespaceKind.PROTECTED, "b"));

            const uint smallArrayLength = 500;
            const int lessThanLengthSampleCount = 500;
            const int moreThanLengthSampleCount = 500;

            var sampledIndices = new HashSet<uint>();

            if (image.length <= smallArrayLength) {
                // If the length of the array is small then do an exhaustive check.
                for (uint i = 0; i <= image.length; i++)
                    testIndex(i);
            }
            else {
                // For large arrays, check all nonempty indices and a random sample of empty indices.
                var nonEmptyIndices = new HashSet<uint>();

                nonEmptyIndices.UnionWith(image.elements.Keys);
                if (s_currentProtoProperties != null)
                    nonEmptyIndices.UnionWith(s_currentProtoProperties.Keys);

                foreach (var index in nonEmptyIndices)
                    testIndex(index);

                for (int i = 0; i < lessThanLengthSampleCount; i++) {
                    uint index = randomIndex(image.length);

                    if (sampledIndices.Add(index)
                        && !image.elements.ContainsKey(index)
                        && !(s_currentProtoProperties != null && s_currentProtoProperties.ContainsKey(index)))
                    {
                        testIndex(index);
                    }
                }
            }

            // Check a random sample of indices greater than or equal to the array length
            // and verify that they do not contain any value.
            if (image.length < UInt32.MaxValue) {
                for (int i = 0; i < moreThanLengthSampleCount; i++) {
                    uint index = image.length + randomIndex(UInt32.MaxValue - image.length);
                    if (sampledIndices.Add(index))
                        testIndex(index);
                }
            }

            void testIndex(uint index) {
                BindStatus statusOnObject = BindStatus.SUCCESS;
                BindStatus statusOnPrototype = BindStatus.SUCCESS;

                if (!image.elements.TryGetValue(index, out ASAny valueOnObject))
                    statusOnObject = BindStatus.SOFT_SUCCESS;

                if (s_currentProtoProperties == null || !s_currentProtoProperties.TryGetValue(index, out ASAny valueOnPrototype))
                    statusOnPrototype = BindStatus.NOT_FOUND;

                string indexStr = indexToString(index);
                ASAny tmp;

                void check(Func<BindOptions, bool> hasPropFunc, Func<BindOptions, (BindStatus, ASAny)> getPropFunc) =>
                    verifyPropertyBindingHelper(hasPropFunc, getPropFunc, statusOnObject, statusOnPrototype, valueOnObject, valueOnPrototype);

                check(
                    bindOpts => array.AS_hasProperty(QName.publicName(indexStr), bindOpts),
                    bindOpts => (array.AS_tryGetProperty(QName.publicName(indexStr), out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasProperty(indexStr, nsSet1, bindOpts),
                    bindOpts => (array.AS_tryGetProperty(indexStr, nsSet1, out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasProperty(indexStr, nsSet2, bindOpts),
                    bindOpts => (array.AS_tryGetProperty(indexStr, nsSet2, out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasPropertyObj(indexStr, bindOpts),
                    bindOpts => (array.AS_tryGetPropertyObj(indexStr, out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasPropertyObj(indexStr, nsSet1, bindOpts),
                    bindOpts => (array.AS_tryGetPropertyObj(indexStr, nsSet1, out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasPropertyObj(indexStr, nsSet2, bindOpts),
                    bindOpts => (array.AS_tryGetPropertyObj(indexStr, nsSet2, out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasPropertyObj(new ASQName("", indexStr), bindOpts),
                    bindOpts => (array.AS_tryGetPropertyObj(new ASQName("", indexStr), out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasPropertyObj(new ASQName("", indexStr), nsSet1, bindOpts),
                    bindOpts => (array.AS_tryGetPropertyObj(new ASQName("", indexStr), nsSet1, out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasPropertyObj(new ASQName("", indexStr), nsSet2, bindOpts),
                    bindOpts => (array.AS_tryGetPropertyObj(new ASQName("", indexStr), nsSet2, out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasPropertyObj(new ASQName("", indexStr), nsSet3, bindOpts),
                    bindOpts => (array.AS_tryGetPropertyObj(new ASQName("", indexStr), nsSet3, out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasPropertyObj(index, bindOpts),
                    bindOpts => (array.AS_tryGetPropertyObj(index, out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasPropertyObj(index, nsSet1, bindOpts),
                    bindOpts => (array.AS_tryGetPropertyObj(index, nsSet1, out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasPropertyObj(index, nsSet2, bindOpts),
                    bindOpts => (array.AS_tryGetPropertyObj(index, nsSet2, out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasPropertyObj((double)index, bindOpts),
                    bindOpts => (array.AS_tryGetPropertyObj((double)index, out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasPropertyObj((double)index, nsSet1, bindOpts),
                    bindOpts => (array.AS_tryGetPropertyObj((double)index, nsSet1, out tmp, bindOpts), tmp)
                );
                check(
                    bindOpts => array.AS_hasPropertyObj((double)index, nsSet2, bindOpts),
                    bindOpts => (array.AS_tryGetPropertyObj((double)index, nsSet2, out tmp, bindOpts), tmp)
                );

                if (index <= (uint)Int32.MaxValue) {
                    check(
                        bindOpts => array.AS_hasPropertyObj((int)index, bindOpts),
                        bindOpts => (array.AS_tryGetPropertyObj((int)index, out tmp, bindOpts), tmp)
                    );
                    check(
                        bindOpts => array.AS_hasPropertyObj((int)index, nsSet1, bindOpts),
                        bindOpts => (array.AS_tryGetPropertyObj((int)index, nsSet1, out tmp, bindOpts), tmp)
                    );
                    check(
                        bindOpts => array.AS_hasPropertyObj((int)index, nsSet2, bindOpts),
                        bindOpts => (array.AS_tryGetPropertyObj((int)index, nsSet2, out tmp, bindOpts), tmp)
                    );
                }
            }
        }

        public static IEnumerable<object[]> propertyBindingAccessAndIteration_testData() {
            setRandomSeed(521704);

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

            void addTestCase(
                (ASArray arr, Image img) initialState, IEnumerable<Mutation> mutations = null, IndexDict prototype = null)
            {
                var (curArr, curImg) = initialState;
                applyMutations(curArr, mutations);
                applyMutations(ref curImg, mutations);

                testcases.Add((new ArrayWrapper(curArr), curImg, prototype));
            }

            addTestCase(makeEmptyArrayAndImage());
            addTestCase(makeEmptyArrayAndImage(20));
            addTestCase(makeEmptyArrayAndImage(maxLength));

            addTestCase(makeRandomDenseArrayAndImage(50, valueDomain));

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetRandomIndexType(randomIndex(100), randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetRandomIndexType(randomIndex(maxLength), randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 29, i => mutPushOne(randomSample(valueDomain))),
                    mutPush(rangeSelect(0, 29, i => randomSample(valueDomain))),
                    rangeSelect(0, 29, i => mutUnshift(randomSample(valueDomain))),
                    mutUnshift(rangeSelect(0, 29, i => randomSample(valueDomain))),
                    rangeMultiSelect(0, 9, i => new[] {mutPop(), mutShift()})
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(50, valueDomain),
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(0, 19, i => mutPush(randomSample(valueDomain))),
                    rangeSelect(0, 19, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(130, 139, i => mutDelRandomIndexType(i)),
                    rangeSelect(0, 19, i => mutDelRandomIndexType(randomIndex(130)))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 119, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(0, 39, i => mutDelRandomIndexType(i * 3)),
                    rangeSelect(0, 39, i => mutDelRandomIndexType(i * 3 + 1))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 29, i => mutPushOne(randomSample(valueDomain))),
                    mutSetLength(40),
                    rangeSelect(0, 29, i => mutPushOne(randomSample(valueDomain))),
                    mutSetLength(65),
                    mutSetLength(80)
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: buildMutationList(
                    rangeSelect(0, 49, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(maxIndex - 49, maxIndex, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(50, valueDomain),
                mutations: new[] {mutSetLength(70)},
                prototype: makeIndexDict(1, 15, 45, 50, 54, 59, 65, 69, 73, 89)
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(100),
                mutations: rangeSelect(0, 19, i => mutSetUint(randomIndex(100), randomSample(valueDomain))),
                prototype: makeIndexDict(rangeSelect(0, 109, i => i))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                prototype: makeIndexDict(0, (uint)Int32.MaxValue, maxIndex)
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(propertyBindingAccessAndIteration_testData))]
        public void propertyBindingAccessTest(ArrayWrapper array, Image image, IndexDict prototype) {
            setRandomSeed(748661);
            setPrototypeProperties(prototype);
            try {
                verifyArrayPropBindingMatchesImage(array.instance, image);
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> propertyBindingIteration_testData_withStringProps() {
            setRandomSeed(8719924);

            var (array1, image1) = makeEmptyArrayAndImage();
            var (array2, image2) = makeEmptyArrayAndImage(600);
            var (array3, image3) = makeEmptyArrayAndImage();

            ASAny[] valueDomain = makeUniqueValues(50);

            for (uint i = 0; i < 50; i++) {
                Mutation mutation = mutSetUint(i, randomSample(valueDomain));
                applyMutation(array1, mutation);
                applyMutation(ref image1, mutation);
            }

            for (int i = 0; i < 100; i++) {
                Mutation mutation = mutSetUint(randomIndex(500), randomSample(valueDomain));
                applyMutation(array2, mutation);
                applyMutation(ref image2, mutation);
            }

            array1.AS_dynamicProps.setValue("hello", new ASObject());
            array1.AS_dynamicProps.setValue("world", ASAny.undefined);
            array1.AS_dynamicProps.setValue("abc", "*****");
            array1.AS_dynamicProps.setValue("def", "*****", isEnum: false);

            for (int i = 0; i < 26; i++)
                array2.AS_dynamicProps.setValue(new string((char)('a' + i), 4), i);

            for (int i = 1; i < 26; i += 3)
                array2.AS_dynamicProps.delete(new string((char)('a' + i), 4));

            array3.AS_dynamicProps.setValue("hello", new ASObject());
            array3.AS_dynamicProps.setValue("world", ASAny.undefined);
            array3.AS_dynamicProps.setValue("abc", "*****");
            array3.AS_dynamicProps.setValue("def", "*****", isEnum: false);

            yield return new object[] {new ArrayWrapper(array1), image1, null};
            yield return new object[] {new ArrayWrapper(array2), image2, makeIndexDict(0, 550, 599)};
            yield return new object[] {new ArrayWrapper(array3), image3, null};
        }

        [Theory]
        [MemberData(nameof(propertyBindingAccessAndIteration_testData))]
        [MemberData(nameof(propertyBindingIteration_testData_withStringProps))]
        public void propertyBindingIterationTest(ArrayWrapper array, Image image, IndexDict prototype) {
            setRandomSeed(748661);
            setPrototypeProperties(prototype);

            try {
                var indices = new HashSet<uint>();
                var stringProperties = new Dictionary<string, ASAny>();

                int curPropIndex = array.instance.AS_nextIndex(0);
                while (curPropIndex != 0) {
                    ASAny name = array.instance.AS_nameAtIndex(curPropIndex);
                    ASAny value = array.instance.AS_valueAtIndex(curPropIndex);

                    if (ASObject.AS_isUint(name.value)) {
                        uint nameIndex = (uint)name;
                        Assert.True(indices.Add(nameIndex));
                        AssertHelper.identical(image.elements[nameIndex], value);
                    }
                    else {
                        Assert.IsType<ASString>(name.value);
                        stringProperties[(string)name] = value;
                    }

                    curPropIndex = array.instance.AS_nextIndex(curPropIndex);
                }

                var dynamicProps = array.instance.AS_dynamicProps;

                if (dynamicProps != null) {
                    curPropIndex = dynamicProps.getNextIndex(-1);
                    while (curPropIndex != -1) {
                        string name = dynamicProps.getNameFromIndex(curPropIndex);
                        AssertHelper.identical(stringProperties[name], dynamicProps.getValueFromIndex(curPropIndex));
                        stringProperties.Remove(name);
                        curPropIndex = dynamicProps.getNextIndex(curPropIndex);
                    }
                }

                Assert.Empty(stringProperties);
                Assert.Equal((image.elements == null) ? 0 : image.elements.Count, indices.Count);
            }
            finally {
                resetPrototypeProperties();
            }
        }

        public static IEnumerable<object[]> propertyBindingMutationTest_data() {
            setRandomSeed(17233);

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

            void addTestCase(
                (ASArray arr, Image img) initialState, IEnumerable<Mutation> mutations, IndexDict prototype = null)
            {
                testcases.Add((new ArrayWrapper(initialState.arr), initialState.img, mutations, prototype));
            }

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 39, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(0, 19, i => mutSetUint(i, randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(40),
                mutations: rangeSelect(39, 0, i => mutSetUint(i, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(20, valueDomain),
                mutations: buildMutationList(
                    rangeSelect(20, 39, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(0, 19, i => mutSetUint(i * 2, randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(40),
                mutations: rangeSelect(0, 39, i => mutSetUint(randomIndex(40), randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 39, i => mutSetUint(randomIndex(maxLength), randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutDelUint(i)),
                    rangeSelect(0, 19, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(19, 0, i => mutDelUint(i)),
                    rangeSelect(0, 19, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(0, 19, i => mutDelUint(i)),
                    rangeSelect(19, 0, i => mutSetUint(i, randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(40, valueDomain),
                mutations: buildMutationList(
                    rangeSelect(0, 19, i => mutDelUint(randomIndex(40))),
                    rangeSelect(0, 19, i => mutSetUint(randomIndex(40), randomSample(valueDomain))),
                    rangeSelect(0, 29, i => mutDelUint(randomIndex(40))),
                    rangeSelect(0, 29, i => mutSetUint(randomIndex(40), randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 9, i => mutSetUint(i, randomSample(valueDomain))),
                    mutSetLength(15),
                    rangeSelect(0, 9, i => mutPushOne(randomSample(valueDomain))),
                    rangeSelect(50, 59, i => mutSetUint(i, randomSample(valueDomain))),
                    mutSetLength(30),
                    rangeSelect(0, 9, i => mutUnshift(randomSample(valueDomain))),
                    mutSetLength(45),
                    rangeSelect(46, 49, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(3, 12, i => mutDelUint(i)),
                    rangeSelect(0, 5, i => mutShift()),
                    rangeSelect(40, 52, i => mutSetUint(i, ASAny.undefined)),
                    rangeSelect(0, 5, i => mutPop()),
                    rangeSelect(40, 49, i => mutDelUint(i))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: buildMutationList(
                    rangeSelect(maxIndex - 9, maxIndex, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(maxIndex / 2 - 5, maxIndex / 2 + 5, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(maxIndex - 9, maxIndex, i => mutDelUint(i)),
                    rangeSelect(maxIndex / 2 - 5, maxIndex / 2 + 5, i => mutDelUint(i))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                prototype: makeIndexDict(3, 4, 12, 17, 18, 28, 36, 41, 49, 58, 63, 67, 78, 82, 88, 99),
                mutations: buildMutationList(
                    rangeSelect(0, 9, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(0, 9, i => mutSetUint(i, ASAny.undefined)),
                    rangeSelect(90, 99, i => mutSetUint(i, randomSample(valueDomain))),
                    rangeSelect(90, 99, i => mutDelUint(i)),
                    rangeSelect(9, 0, i => mutDelUint(i))
                )
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(propertyBindingMutationTest_data))]
        public void propertyBindingMutationTest(
            ArrayWrapper array, Image image, IEnumerable<Mutation> mutations, IndexDict prototype)
        {
            ASArray currentArray = array.instance;
            Image currentImage = image;

            var nsSet1 = new NamespaceSet(Namespace.@public);
            var nsSet2 = new NamespaceSet(new Namespace("a"), Namespace.@public, new Namespace(NamespaceKind.PROTECTED, "b"));
            var nsSet3 = new NamespaceSet(new Namespace("a"), new Namespace(NamespaceKind.PROTECTED, "b"));

            var bindOpts = new[] {
                BindOptions.SEARCH_DYNAMIC,
                BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE,
                BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS,
                BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE,
                BindOptions.SEARCH_PROTOTYPE,
                BindOptions.SEARCH_TRAITS,
                BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE,
                BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS | BindOptions.SEARCH_PROTOTYPE | BindOptions.ATTRIBUTE,
            };

            setRandomSeed(8512199);
            setPrototypeProperties(prototype);

            try {
                foreach (Mutation mut in mutations) {
                    if (mut.kind != MutationKind.SET && mut.kind != MutationKind.DELETE) {
                        applyMutation(currentArray, mut);
                        applyMutation(ref currentImage, mut);
                        continue;
                    }

                    Image nextImage = new Image(currentImage.length, new IndexDict(currentImage.elements));
                    object applyMutResult = applyMutation(ref nextImage, mut);

                    uint index = (uint)mut.args[0];

                    if (mut.kind == MutationKind.SET)
                        checkSetMutation(index, (ASAny)mut.args[1], nextImage);
                    else
                        checkDeleteMutation(index, (bool)applyMutResult, nextImage);

                    applyMutation(currentArray, mut);
                    currentImage = nextImage;
                }

                verifyArrayPropBindingMatchesImage(currentArray, currentImage);
            }
            finally {
                resetPrototypeProperties();
            }

            void checkSetMutation(uint index, ASAny value, Image expectedImage) {
                void check(Func<ASArray, BindStatus> func, bool isSuccess) {
                    if (isSuccess) {
                        ASArray clone = currentArray.clone();
                        Assert.Equal(BindStatus.SUCCESS, func(clone));
                        verifyArrayMatchesImage(clone, expectedImage, dontSampleOutsideArrayLength: true);
                    }
                    else {
                        // No need to clone, as a failure should not result in any mutation to the current instance state.
                        Assert.Equal(BindStatus.NOT_FOUND, func(currentArray));
                        verifyArrayMatchesImage(currentArray, currentImage, dontSampleOutsideArrayLength: true);
                    }
                }

                string indexStr = indexToString(index);

                for (int i = 0; i < bindOpts.Length; i++) {
                    bool isSuccess = (bindOpts[i] & BindOptions.SEARCH_DYNAMIC) != 0
                        && (bindOpts[i] & BindOptions.ATTRIBUTE) == 0;

                    check(x => x.AS_trySetProperty(QName.publicName(indexStr), value, bindOpts[i]), isSuccess);
                    check(x => x.AS_trySetProperty(indexStr, nsSet1, value, bindOpts[i]), isSuccess);
                    check(x => x.AS_trySetProperty(indexStr, nsSet2, value, bindOpts[i]), isSuccess);

                    check(x => x.AS_trySetPropertyObj(index, value, bindOpts[i]), isSuccess);
                    check(x => x.AS_trySetPropertyObj(index, nsSet1, value, bindOpts[i]), isSuccess);
                    check(x => x.AS_trySetPropertyObj(index, nsSet2, value, bindOpts[i]), isSuccess);
                    check(x => x.AS_trySetPropertyObj((double)index, value, bindOpts[i]), isSuccess);
                    check(x => x.AS_trySetPropertyObj((double)index, nsSet1, value, bindOpts[i]), isSuccess);
                    check(x => x.AS_trySetPropertyObj((double)index, nsSet2, value, bindOpts[i]), isSuccess);

                    if (index <= (uint)Int32.MaxValue) {
                        check(x => x.AS_trySetPropertyObj((int)index, value, bindOpts[i]), isSuccess);
                        check(x => x.AS_trySetPropertyObj((int)index, nsSet1, value, bindOpts[i]), isSuccess);
                        check(x => x.AS_trySetPropertyObj((int)index, nsSet2, value, bindOpts[i]), isSuccess);
                    }

                    check(x => x.AS_trySetPropertyObj(indexStr, value, bindOpts[i]), isSuccess);
                    check(x => x.AS_trySetPropertyObj(indexStr, nsSet1, value, bindOpts[i]), isSuccess);
                    check(x => x.AS_trySetPropertyObj(indexStr, nsSet2, value, bindOpts[i]), isSuccess);

                    check(x => x.AS_trySetPropertyObj(new ASQName("", indexStr), value, bindOpts[i]), isSuccess);
                    check(x => x.AS_trySetPropertyObj(new ASQName("", indexStr), nsSet1, value, bindOpts[i]), isSuccess);
                    check(x => x.AS_trySetPropertyObj(new ASQName("", indexStr), nsSet2, value, bindOpts[i]), isSuccess);
                    check(x => x.AS_trySetPropertyObj(new ASQName("", indexStr), nsSet3, value, bindOpts[i]), isSuccess);
                }
            }

            void checkDeleteMutation(uint index, bool deleteResult, Image expectedImage) {
                void check(Func<ASArray, bool> func, bool isSuccess) {
                    if (isSuccess) {
                        ASArray clone = currentArray.clone();
                        Assert.True(func(clone));
                        verifyArrayMatchesImage(clone, expectedImage);
                    }
                    else {
                        // No need to clone, as a failure should not result in any mutation to the current instance state.
                        Assert.False(func(currentArray));
                        verifyArrayMatchesImage(currentArray, currentImage);
                    }
                }

                string indexStr = indexToString(index);

                for (int i = 0; i < bindOpts.Length; i++) {
                    bool isSuccess = deleteResult
                        && (bindOpts[i] & BindOptions.SEARCH_DYNAMIC) != 0
                        && (bindOpts[i] & BindOptions.ATTRIBUTE) == 0;

                    check(x => x.AS_deleteProperty(QName.publicName(indexStr), bindOpts[i]), isSuccess);
                    check(x => x.AS_deleteProperty(indexStr, nsSet1, bindOpts[i]), isSuccess);
                    check(x => x.AS_deleteProperty(indexStr, nsSet2, bindOpts[i]), isSuccess);

                    check(x => x.AS_deletePropertyObj(index, bindOpts[i]), isSuccess);
                    check(x => x.AS_deletePropertyObj(index, nsSet1, bindOpts[i]), isSuccess);
                    check(x => x.AS_deletePropertyObj(index, nsSet2, bindOpts[i]), isSuccess);
                    check(x => x.AS_deletePropertyObj((double)index, bindOpts[i]), isSuccess);
                    check(x => x.AS_deletePropertyObj((double)index, nsSet1, bindOpts[i]), isSuccess);
                    check(x => x.AS_deletePropertyObj((double)index, nsSet2, bindOpts[i]), isSuccess);

                    if (index <= (uint)Int32.MaxValue) {
                        check(x => x.AS_deletePropertyObj((int)index, bindOpts[i]), isSuccess);
                        check(x => x.AS_deletePropertyObj((int)index, nsSet1, bindOpts[i]), isSuccess);
                        check(x => x.AS_deletePropertyObj((int)index, nsSet2, bindOpts[i]), isSuccess);
                    }

                    check(x => x.AS_deletePropertyObj(indexStr, bindOpts[i]), isSuccess);
                    check(x => x.AS_deletePropertyObj(indexStr, nsSet1, bindOpts[i]), isSuccess);
                    check(x => x.AS_deletePropertyObj(indexStr, nsSet2, bindOpts[i]), isSuccess);

                    check(x => x.AS_deletePropertyObj(new ASQName("", indexStr), bindOpts[i]), isSuccess);
                    check(x => x.AS_deletePropertyObj(new ASQName("", indexStr), nsSet1, bindOpts[i]), isSuccess);
                    check(x => x.AS_deletePropertyObj(new ASQName("", indexStr), nsSet2, bindOpts[i]), isSuccess);
                    check(x => x.AS_deletePropertyObj(new ASQName("", indexStr), nsSet3, bindOpts[i]), isSuccess);
                }
            }
        }

        public static IEnumerable<object[]> propertyBindingAccessTest_nonIndexNames_data() {
            var values = makeUniqueValues(20);

            var arr1 = new ASArray();
            var arr2 = new ASArray();

            for (int i = 0; i < 10; i++) {
                arr1.AS_setElement(i, values[i]);
                arr2.AS_setElement(i, values[i]);
            }

            for (int i = 10; i < 20; i++)
                arr2.AS_setElement(i + 100, values[i]);

            return TupleHelper.toArrays(new ArrayWrapper(arr1), new ArrayWrapper(arr2));
        }

        [Theory]
        [MemberData(nameof(propertyBindingAccessTest_nonIndexNames_data))]
        public void propertyBindingAccessTest_nonIndexNames(ArrayWrapper array) {
            var nsSet1 = new NamespaceSet(Namespace.@public);
            var nsSet2 = new NamespaceSet(new Namespace("a"), Namespace.@public, new Namespace(NamespaceKind.PROTECTED, "b"));
            var nsSet3 = new NamespaceSet(new Namespace("a"), new Namespace(NamespaceKind.PROTECTED, "b"));

            var prototypeDPs = new Dictionary<string, ASAny> {
                ["+9"] = new ASObject(),
                ["6.0"] = new ASObject(),
                [" 8 "] = new ASObject(),
                ["4294967295"] = new ASObject(),
                ["NaN"] = new ASObject(),
                ["def"] = new ASObject(),
                ["ghi"] = new ASObject(),
            };

            var arrayDPs = new Dictionary<string, ASAny> {
                ["+5"] = new ASObject(),
                [" 8"] = new ASObject(),
                [" 8 "] = new ASObject(),
                ["-1"] = new ASObject(),
                ["-001"] = new ASObject(),
                ["-10000"] = default,
                ["010"] = new ASObject(),
                ["+0010"] = new ASObject(),
                ["4."] = new ASObject(),
                ["4.0"] = default,
                ["4.3"] = new ASObject(),
                ["6.0"] = new ASObject(),
                ["1e+1"] = new ASObject(),
                ["4294967295"] = new ASObject(),
                ["6781000394"] = default,
                ["Infinity"] = new ASObject(),
                ["NaN"] = new ASObject(),
                ["abc"] = new ASObject(),
                ["def"] = new ASObject(),
            };

            string[] nonIndexStringKeys = {
                // Negative signs
                "-1", "-10000", "-2", "-0",
                // Positive signs
                "+1", "+2", "+5", "+9", "+0",
                // Leading zeros
                "00", "010", "+0010", "-001",
                // Spaces
                " 8", "8 ", " 8 ",
                // Decimal points
                "0.", "0.0", "4.", "4.0", "6.0", "4.3", "3.5", "0.0001", ".4",
                // E-notation
                "0e+1", "2e0", "2e+0", "1e1", "1e+1",
                // Large numbers
                "4294967295", "6781000394", "10000000000",
                // Infinity/NaN
                "Infinity", "+Infinity", "-Infinity", "NaN",
                // Non-numeric strings
                "", "abc", "def", "ghi"
            };

            ASAny[] nonIndexObjectKeys = {
                // Negative numbers
                -1, -1.0, -2, -2.0, -10000, -10000.0,
                // Fractions
                4.3, 3.5, 0.5, 0.0001,
                // Large numbers
                4294967295u, 4294967295.0, 4294967296.0, 6781000394.0, 10000000000.0,
                // Infinity/NaN
                Double.PositiveInfinity, Double.NegativeInfinity, Double.NaN
            };

            setPrototypeProperties(null); // Take prototype lock
            ASObject arrayProto = null;

            try {
                arrayProto = s_arrayClassPrototype;
                foreach (var (k, v) in prototypeDPs)
                    arrayProto.AS_dynamicProps.setValue(k, v);

                foreach (var (k, v) in arrayDPs)
                    array.instance.AS_dynamicProps.setValue(k, v);

                for (uint i = 0; i < 10; i++) {
                    testStringKey(indexToString(i), true);
                    testObjectKey(indexToString(i), true);
                    testObjectKey(i, true);
                    testObjectKey((int)i, true);
                    testObjectKey((double)i, true);
                }

                for (int i = 0; i < nonIndexStringKeys.Length; i++) {
                    testStringKey(nonIndexStringKeys[i], false);
                    testObjectKey(nonIndexStringKeys[i], false);
                }

                for (int i = 0; i < nonIndexObjectKeys.Length; i++) {
                    testObjectKey(nonIndexObjectKeys[i], false);
                }
            }
            finally {
                foreach (var (k, _) in prototypeDPs)
                    arrayProto.AS_dynamicProps.delete(k);

                resetPrototypeProperties();     // Release lock
            }

            void testStringKey(string key, bool isIndex) {
                if (!isIndex) {
                    BindStatus statusOnObject = BindStatus.SOFT_SUCCESS;
                    BindStatus statusOnPrototype = BindStatus.NOT_FOUND;
                    ASAny valueOnObject;
                    ASAny valueOnPrototype;

                    if (arrayDPs.TryGetValue(key, out valueOnObject))
                        statusOnObject = BindStatus.SUCCESS;

                    if (prototypeDPs.TryGetValue(key, out valueOnPrototype))
                        statusOnPrototype = BindStatus.SUCCESS;

                    verifyPropertyBindingHelper(
                        bindOpts => array.instance.AS_hasProperty(QName.publicName(key), bindOpts),
                        bindOpts => (array.instance.AS_tryGetProperty(QName.publicName(key), out var temp, bindOpts), temp),
                        statusOnObject, statusOnPrototype, valueOnObject, valueOnPrototype
                    );

                    verifyPropertyBindingHelper(
                        bindOpts => array.instance.AS_hasProperty(key, nsSet1, bindOpts),
                        bindOpts => (array.instance.AS_tryGetProperty(key, nsSet1, out var temp, bindOpts), temp),
                        statusOnObject, statusOnPrototype, valueOnObject, valueOnPrototype
                    );

                    verifyPropertyBindingHelper(
                        bindOpts => array.instance.AS_hasProperty(key, nsSet2, bindOpts),
                        bindOpts => (array.instance.AS_tryGetProperty(key, nsSet2, out var temp, bindOpts), temp),
                        statusOnObject, statusOnPrototype, valueOnObject, valueOnPrototype
                    );

                    verifyPropertyBindingHelper(
                        bindOpts => array.instance.AS_hasPropertyObj(new ASQName("", key), bindOpts),
                        bindOpts => (array.instance.AS_tryGetPropertyObj(new ASQName("", key), out var temp, bindOpts), temp),
                        statusOnObject, statusOnPrototype, valueOnObject, valueOnPrototype
                    );

                    verifyPropertyBindingHelper(
                        bindOpts => array.instance.AS_hasPropertyObj(new ASQName("", key), nsSet1, bindOpts),
                        bindOpts => (array.instance.AS_tryGetPropertyObj(new ASQName("", key), nsSet1, out var temp, bindOpts), temp),
                        statusOnObject, statusOnPrototype, valueOnObject, valueOnPrototype
                    );

                    verifyPropertyBindingHelper(
                        bindOpts => array.instance.AS_hasPropertyObj(new ASQName("", key), nsSet2, bindOpts),
                        bindOpts => (array.instance.AS_tryGetPropertyObj(new ASQName("", key), nsSet2, out var temp, bindOpts), temp),
                        statusOnObject, statusOnPrototype, valueOnObject, valueOnPrototype
                    );

                    verifyPropertyBindingHelper(
                        bindOpts => array.instance.AS_hasPropertyObj(new ASQName("", key), nsSet3, bindOpts),
                        bindOpts => (array.instance.AS_tryGetPropertyObj(new ASQName("", key), nsSet3, out var temp, bindOpts), temp),
                        statusOnObject, statusOnPrototype, valueOnObject, valueOnPrototype
                    );
                }

                checkNonPublicNamespaceFail(
                    bindOpts => array.instance.AS_hasProperty(new QName("a", key), bindOpts),
                    bindOpts => (array.instance.AS_tryGetProperty(new QName("a", key), out var temp, bindOpts), temp)
                );

                checkNonPublicNamespaceFail(
                    bindOpts => array.instance.AS_hasProperty(new QName(new Namespace(NamespaceKind.PACKAGE_INTERNAL, ""), key), bindOpts),
                    bindOpts => (array.instance.AS_tryGetProperty(new QName(new Namespace(NamespaceKind.PACKAGE_INTERNAL, ""), key), out var temp, bindOpts), temp)
                );

                checkNonPublicNamespaceFail(
                    bindOpts => array.instance.AS_hasProperty(new QName(Namespace.any, key), bindOpts),
                    bindOpts => (array.instance.AS_tryGetProperty(new QName(Namespace.any, key), out var temp, bindOpts), temp)
                );

                checkNonPublicNamespaceFail(
                    bindOpts => array.instance.AS_hasProperty(key, nsSet3, bindOpts),
                    bindOpts => (array.instance.AS_tryGetProperty(key, nsSet3, out var temp, bindOpts), temp)
                );

                checkNonPublicNamespaceFail(
                    bindOpts => array.instance.AS_hasPropertyObj(new ASQName("a", key), bindOpts),
                    bindOpts => (array.instance.AS_tryGetPropertyObj(new ASQName("a", key), out var temp, bindOpts), temp)
                );

                checkNonPublicNamespaceFail(
                    bindOpts => array.instance.AS_hasPropertyObj(new ASQName("*", key), bindOpts),
                    bindOpts => (array.instance.AS_tryGetPropertyObj(new ASQName("*", key), out var temp, bindOpts), temp)
                );

                checkNonPublicNamespaceFail(
                    bindOpts => array.instance.AS_hasPropertyObj(new ASQName("a", key), nsSet2, bindOpts),
                    bindOpts => (array.instance.AS_tryGetPropertyObj(new ASQName("a", key), nsSet2, out var temp, bindOpts), temp)
                );

                checkNonPublicNamespaceFail(
                    bindOpts => array.instance.AS_hasPropertyObj(new ASQName("a", key), nsSet3, bindOpts),
                    bindOpts => (array.instance.AS_tryGetPropertyObj(new ASQName("a", key), nsSet3, out var temp, bindOpts), temp)
                );

                checkNonPublicNamespaceFail(
                    bindOpts => array.instance.AS_hasPropertyObj(new ASQName("*", key), nsSet2, bindOpts),
                    bindOpts => (array.instance.AS_tryGetPropertyObj(new ASQName("*", key), nsSet2, out var temp, bindOpts), temp)
                );

                checkNonPublicNamespaceFail(
                    bindOpts => array.instance.AS_hasPropertyObj(new ASQName("*", key), nsSet3, bindOpts),
                    bindOpts => (array.instance.AS_tryGetPropertyObj(new ASQName("*", key), nsSet3, out var temp, bindOpts), temp)
                );

                checkAttributeFlagFail(
                    bindOpts => array.instance.AS_hasProperty(QName.publicName(key), bindOpts),
                    bindOpts => (array.instance.AS_tryGetProperty(QName.publicName(key), out var temp, bindOpts), temp)
                );

                checkAttributeFlagFail(
                    bindOpts => array.instance.AS_hasProperty(key, nsSet1, bindOpts),
                    bindOpts => (array.instance.AS_tryGetProperty(key, nsSet1, out var temp, bindOpts), temp)
                );

                checkAttributeFlagFail(
                    bindOpts => array.instance.AS_hasProperty(key, nsSet2, bindOpts),
                    bindOpts => (array.instance.AS_tryGetProperty(key, nsSet2, out var temp, bindOpts), temp)
                );

                checkAttributeFlagFail(
                    bindOpts => array.instance.AS_hasPropertyObj(new ASQName("", key), bindOpts),
                    bindOpts => (array.instance.AS_tryGetPropertyObj(new ASQName("", key), out var temp, bindOpts), temp)
                );

                checkAttributeFlagFail(
                    bindOpts => array.instance.AS_hasPropertyObj(new ASQName("", key), nsSet1, bindOpts),
                    bindOpts => (array.instance.AS_tryGetPropertyObj(new ASQName("", key), nsSet1, out var temp, bindOpts), temp)
                );

                checkAttributeFlagFail(
                    bindOpts => array.instance.AS_hasPropertyObj(new ASQName("", key), nsSet2, bindOpts),
                    bindOpts => (array.instance.AS_tryGetPropertyObj(new ASQName("", key), nsSet2, out var temp, bindOpts), temp)
                );
            }

            void testObjectKey(ASAny key, bool isIndex) {
                if (!isIndex) {
                    string keyStr = ASAny.AS_convertString(key);

                    BindStatus statusOnObject = BindStatus.SOFT_SUCCESS;
                    BindStatus statusOnPrototype = BindStatus.NOT_FOUND;
                    ASAny valueOnObject;
                    ASAny valueOnPrototype;

                    if (arrayDPs.TryGetValue(keyStr, out valueOnObject))
                        statusOnObject = BindStatus.SUCCESS;

                    if (prototypeDPs.TryGetValue(keyStr, out valueOnPrototype))
                        statusOnPrototype = BindStatus.SUCCESS;

                    verifyPropertyBindingHelper(
                        bindOpts => array.instance.AS_hasPropertyObj(key, bindOpts),
                        bindOpts => (array.instance.AS_tryGetPropertyObj(key, out var temp, bindOpts), temp),
                        statusOnObject, statusOnPrototype, valueOnObject, valueOnPrototype
                    );

                    verifyPropertyBindingHelper(
                        bindOpts => array.instance.AS_hasPropertyObj(key, nsSet1, bindOpts),
                        bindOpts => (array.instance.AS_tryGetPropertyObj(key, nsSet1, out var temp, bindOpts), temp),
                        statusOnObject, statusOnPrototype, valueOnObject, valueOnPrototype
                    );

                    verifyPropertyBindingHelper(
                        bindOpts => array.instance.AS_hasPropertyObj(key, nsSet2, bindOpts),
                        bindOpts => (array.instance.AS_tryGetPropertyObj(key, nsSet2, out var temp, bindOpts), temp),
                        statusOnObject, statusOnPrototype, valueOnObject, valueOnPrototype
                    );
                }

                checkNonPublicNamespaceFail(
                    bindOpts => array.instance.AS_hasPropertyObj(key, nsSet3, bindOpts),
                    bindOpts => (array.instance.AS_tryGetPropertyObj(key, nsSet3, out var temp, bindOpts), temp)
                );

                checkAttributeFlagFail(
                    bindOpts => array.instance.AS_hasPropertyObj(key, bindOpts),
                    bindOpts => (array.instance.AS_tryGetPropertyObj(key, out var temp, bindOpts), temp)
                );

                checkAttributeFlagFail(
                    bindOpts => array.instance.AS_hasPropertyObj(key, nsSet1, bindOpts),
                    bindOpts => (array.instance.AS_tryGetPropertyObj(key, nsSet1, out var temp, bindOpts), temp)
                );

                checkAttributeFlagFail(
                    bindOpts => array.instance.AS_hasPropertyObj(key, nsSet2, bindOpts),
                    bindOpts => (array.instance.AS_tryGetPropertyObj(key, nsSet2, out var temp, bindOpts), temp)
                );
            }

            void checkNonPublicNamespaceFail(
                Func<BindOptions, bool> hasPropFunc,
                Func<BindOptions, (BindStatus, ASAny)> getPropFunc
            ) {
                verifyPropertyBindingHelper(
                    hasPropFunc,
                    getPropFunc,
                    statusOnObject: BindStatus.NOT_FOUND,
                    statusOnPrototype: BindStatus.NOT_FOUND
                );
            }

            void checkAttributeFlagFail(
                Func<BindOptions, bool> hasPropFunc,
                Func<BindOptions, (BindStatus, ASAny)> getPropFunc
            ) {
                verifyPropertyBindingHelper(
                    hasPropFunc,
                    getPropFunc,
                    statusOnObject: BindStatus.NOT_FOUND,
                    statusOnPrototype: BindStatus.NOT_FOUND,
                    additionalBindOpts: BindOptions.ATTRIBUTE
                );
            }
        }

        public static IEnumerable<object[]> propertyBindingMutationTest_nonIndexNames_data() {
            var values = makeUniqueValues(15);

            var (arr1, image1) = makeEmptyArrayAndImage();
            var (arr2, image2) = makeEmptyArrayAndImage();

            for (uint i = 0; i < 10; i++) {
                arr1.AS_setElement(i, values[i]);
                arr2.AS_setElement(i, values[i]);
                applyMutation(ref image1, mutSetUint(i, values[i]));
                applyMutation(ref image2, mutSetUint(i, values[i]));
            }

            for (uint i = 10; i < 15; i++) {
                arr2.AS_setElement(i + 30, values[i]);
                applyMutation(ref image2, mutSetUint(i + 30, values[i]));
            }

            return TupleHelper.toArrays(
                (new ArrayWrapper(arr1), image1),
                (new ArrayWrapper(arr2), image2)
            );
        }

        [Theory]
        [MemberData(nameof(propertyBindingMutationTest_nonIndexNames_data))]
        public void propertyBindingMutationTest_nonIndexNames(ArrayWrapper array, Image image) {
            setRandomSeed(478911);

            var nsSet1 = new NamespaceSet(Namespace.@public);
            var nsSet2 = new NamespaceSet(new Namespace("a"), Namespace.@public, new Namespace(NamespaceKind.PROTECTED, "b"));
            var nsSet3 = new NamespaceSet(new Namespace("a"), new Namespace(NamespaceKind.PROTECTED, "b"));

            string[] nonIndexStringKeys = {
                // Negative signs
                "-1", "-10000", "-2", "-0",
                // Positive signs
                "+1", "+2", "+5", "+9", "+0",
                // Leading zeros
                "00", "010", "+0010", "-001",
                // Spaces
                " 8", "8 ", " 8 ",
                // Decimal points
                "0.", "0.0", "4.", "4.0", "6.0", "4.3", "3.5", "0.0001", ".4",
                // E-notation
                "0e+1", "2e0", "2e+0", "1e1", "1e+1",
                // Large numbers
                "4294967295", "6781000394", "10000000000",
                // Infinity/NaN
                "Infinity", "+Infinity", "-Infinity", "NaN",
                // Non-numeric strings
                "", "abc", "def"
            };

            ASAny[] nonIndexObjectKeys = {
                // Negative numbers
                -1, -1.0, -2, -2.0, -10000, -10000.0,
                // Fractions
                4.3, 3.5, 0.5, 0.0001,
                // Large numbers
                4294967295u, 4294967295.0, 4294967296.0, 6781000394.0, 10000000000.0,
                // Infinity/NaN
                Double.PositiveInfinity, Double.NegativeInfinity, Double.NaN
            };

            ASAny[] valueDomain = makeUniqueValues(50);

            setPrototypeProperties(null); // Take prototype lock

            try {
                testMutationsWithBindOpts(BindOptions.SEARCH_DYNAMIC);
                testMutationsWithBindOpts(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS);
                testMutationsWithBindOpts(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE);
                testMutationsWithBindOpts(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_TRAITS);

                clearArrayDynamicProps();

                for (uint i = 0; i < 10; i++) {
                    testFailedMutationsWithStringKey(indexToString(i));
                    testFailedMutationsWithObjectKey(indexToString(i));
                    testFailedMutationsWithObjectKey(i);
                    testFailedMutationsWithObjectKey((int)i);
                    testFailedMutationsWithObjectKey((double)i);
                }

                for (int i = 0; i < nonIndexStringKeys.Length; i++) {
                    testFailedMutationsWithStringKey(nonIndexStringKeys[i]);
                    testFailedMutationsWithObjectKey(nonIndexStringKeys[i]);
                }

                for (int i = 0; i < nonIndexObjectKeys.Length; i++)
                    testFailedMutationsWithObjectKey(nonIndexObjectKeys[i]);
            }
            finally {
                resetPrototypeProperties();     // Release lock
            }

            void checkArrayStateNoChange() {
                Assert.Equal(image.length, array.instance.length);

                for (uint i = 0; i < image.length; i++) {
                    if (image.elements.TryGetValue(i, out ASAny expectedValue)) {
                        Assert.True(array.instance.AS_hasElement(i));
                        AssertHelper.identical(expectedValue, array.instance.AS_getElement(i));
                    }
                    else {
                        Assert.False(array.instance.AS_hasElement(i));
                        AssertHelper.identical(ASAny.undefined, array.instance.AS_getElement(i));
                    }
                }
            }

            void testMutationsWithBindOpts(BindOptions bindOpts) {
                void checkSet(string key, ASAny value, Func<ASAny, BindStatus> func) {
                    Assert.Equal(BindStatus.SUCCESS, func(value));
                    checkArrayStateNoChange();
                    Assert.True(array.instance.AS_dynamicProps.hasValue(key));
                    AssertHelper.identical(value, array.instance.AS_dynamicProps.getValue(key));
                    Assert.True(array.instance.AS_dynamicProps.isEnumerable(key));
                }

                void checkDelete(string key, Func<bool> func) {
                    bool exists = array.instance.AS_dynamicProps.hasValue(key);
                    Assert.Equal(exists, func());
                    checkArrayStateNoChange();
                    Assert.False(array.instance.AS_dynamicProps.hasValue(key));
                }

                void singlePassSetAndDeleteWithFuncs<T>(
                    T[] keys,
                    Func<T, string> keyToString,
                    Func<T, ASAny, BindStatus>[] setPropFuncs,
                    Func<T, bool>[] deletePropFuncs
                ) {
                    for (int i = 0; i < setPropFuncs.Length; i++) {
                        for (int j = 0; j < keys.Length; j++) {
                            checkSet(keyToString(keys[j]), randomSample(valueDomain), val => setPropFuncs[i](keys[j], val));
                            checkSet(keyToString(keys[j]), randomSample(valueDomain), val => setPropFuncs[i](keys[j], val));
                        }

                        for (int j = 0; j < keys.Length; j++) {
                            checkDelete(keyToString(keys[j]), () => deletePropFuncs[i](keys[j]));
                            checkDelete(keyToString(keys[j]), () => deletePropFuncs[i](keys[j]));
                        }
                    }

                    for (int j = 0; j < keys.Length; j++) {
                        var setFunc = randomSample(setPropFuncs);
                        checkSet(keyToString(keys[j]), randomSample(valueDomain), val => setFunc(keys[j], val));
                        checkSet(keyToString(keys[j]), randomSample(valueDomain), val => setFunc(keys[j], val));
                    }

                    for (int j = 0; j < keys.Length; j++) {
                        var delFunc = randomSample(deletePropFuncs);
                        checkDelete(keyToString(keys[j]), () => delFunc(keys[j]));
                        checkDelete(keyToString(keys[j]), () => delFunc(keys[j]));
                    }
                }

                singlePassSetAndDeleteWithFuncs(
                    nonIndexStringKeys,
                    key => key,
                    new Func<string, ASAny, BindStatus>[] {
                        (key, val) => array.instance.AS_trySetProperty(QName.publicName(key), val, bindOpts),
                        (key, val) => array.instance.AS_trySetProperty(key, nsSet1, val, bindOpts),
                        (key, val) => array.instance.AS_trySetProperty(key, nsSet2, val, bindOpts),
                        (key, val) => array.instance.AS_trySetPropertyObj(key, val, bindOpts),
                        (key, val) => array.instance.AS_trySetPropertyObj(key, nsSet1, val, bindOpts),
                        (key, val) => array.instance.AS_trySetPropertyObj(key, nsSet2, val, bindOpts),
                        (key, val) => array.instance.AS_trySetPropertyObj(new ASQName("", key), val, bindOpts),
                        (key, val) => array.instance.AS_trySetPropertyObj(new ASQName("", key), nsSet1, val, bindOpts),
                        (key, val) => array.instance.AS_trySetPropertyObj(new ASQName("", key), nsSet2, val, bindOpts),
                        (key, val) => array.instance.AS_trySetPropertyObj(new ASQName("", key), nsSet3, val, bindOpts),
                    },
                    new Func<string, bool>[] {
                        key => array.instance.AS_deleteProperty(QName.publicName(key), bindOpts),
                        key => array.instance.AS_deleteProperty(key, nsSet1, bindOpts),
                        key => array.instance.AS_deleteProperty(key, nsSet2, bindOpts),
                        key => array.instance.AS_deletePropertyObj(key, bindOpts),
                        key => array.instance.AS_deletePropertyObj(key, nsSet1, bindOpts),
                        key => array.instance.AS_deletePropertyObj(key, nsSet2, bindOpts),
                        key => array.instance.AS_deletePropertyObj(new ASQName("", key), bindOpts),
                        key => array.instance.AS_deletePropertyObj(new ASQName("", key), nsSet1, bindOpts),
                        key => array.instance.AS_deletePropertyObj(new ASQName("", key), nsSet2, bindOpts),
                        key => array.instance.AS_deletePropertyObj(new ASQName("", key), nsSet3, bindOpts),
                    }
                );

                singlePassSetAndDeleteWithFuncs(
                    nonIndexObjectKeys,
                    key => ASAny.AS_convertString(key),
                    new Func<ASAny, ASAny, BindStatus>[] {
                        (key, val) => array.instance.AS_trySetPropertyObj(key, val, bindOpts),
                        (key, val) => array.instance.AS_trySetPropertyObj(key, nsSet1, val, bindOpts),
                        (key, val) => array.instance.AS_trySetPropertyObj(key, nsSet2, val, bindOpts),
                    },
                    new Func<ASAny, bool>[] {
                        key => array.instance.AS_deletePropertyObj(key, bindOpts),
                        key => array.instance.AS_deletePropertyObj(key, nsSet1, bindOpts),
                        key => array.instance.AS_deletePropertyObj(key, nsSet2, bindOpts),
                    }
                );
            }

            void clearArrayDynamicProps() {
                List<string> propNames = new List<string>();
                int curIndex = array.instance.AS_dynamicProps.getNextIndex(-1);

                while (curIndex != -1) {
                    propNames.Add(array.instance.AS_dynamicProps.getNameFromIndex(curIndex));
                    curIndex = array.instance.AS_dynamicProps.getNextIndex(curIndex);
                }

                for (int i = 0; i < propNames.Count; i++)
                    array.instance.AS_dynamicProps.delete(propNames[i]);
            }

            void testFailedMutationsWithStringKey(string key) {
                checkNonPublicNamespaceFail(
                    (bindOpts, val) => array.instance.AS_trySetProperty(new QName("a", key), val, bindOpts),
                    bindOpts => array.instance.AS_deleteProperty(new QName("a", key), bindOpts)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, val) => array.instance.AS_trySetProperty(new QName(new Namespace(NamespaceKind.PACKAGE_INTERNAL, ""), key), val, bindOpts),
                    bindOpts => array.instance.AS_deleteProperty(new QName(new Namespace(NamespaceKind.PACKAGE_INTERNAL, ""), key), bindOpts)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, val) => array.instance.AS_trySetProperty(new QName(Namespace.any, key), val, bindOpts),
                    bindOpts => array.instance.AS_deleteProperty(new QName(Namespace.any, key), bindOpts),
                    setPropExpectedStatus: BindStatus.NOT_FOUND
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, val) => array.instance.AS_trySetProperty(key, nsSet3, val, bindOpts),
                    bindOpts => array.instance.AS_deleteProperty(key, nsSet3, bindOpts)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(new ASQName("a", key), val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(new ASQName("a", key), bindOpts)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(new ASQName("*", key), val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(new ASQName("*", key), bindOpts)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(new ASQName("a", key), nsSet2, val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(new ASQName("a", key), nsSet2, bindOpts)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(new ASQName("a", key), nsSet3, val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(new ASQName("a", key), nsSet3, bindOpts)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(new ASQName("*", key), nsSet2, val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(new ASQName("*", key), nsSet2, bindOpts)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(new ASQName("*", key), nsSet3, val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(new ASQName("*", key), nsSet3, bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetProperty(QName.publicName(key), val, bindOpts),
                    bindOpts => array.instance.AS_deleteProperty(QName.publicName(key), bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetProperty(new QName("a", key), val, bindOpts),
                    bindOpts => array.instance.AS_deleteProperty(new QName("a", key), bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetProperty(key, nsSet1, val, bindOpts),
                    bindOpts => array.instance.AS_deleteProperty(key, nsSet1, bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetProperty(key, nsSet2, val, bindOpts),
                    bindOpts => array.instance.AS_deleteProperty(key, nsSet2, bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetProperty(key, nsSet3, val, bindOpts),
                    bindOpts => array.instance.AS_deleteProperty(key, nsSet3, bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(new ASQName("", key), val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(new ASQName("", key), bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(new ASQName("a", key), val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(new ASQName("a", key), bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(new ASQName("", key), nsSet1, val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(new ASQName("", key), nsSet1, bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(new ASQName("", key), nsSet2, val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(new ASQName("", key), nsSet2, bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(new ASQName("", key), nsSet3, val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(new ASQName("", key), nsSet3, bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(new ASQName("a", key), nsSet2, val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(new ASQName("a", key), nsSet2, bindOpts)
                );
            }

            void testFailedMutationsWithObjectKey(ASAny key) {
                checkNonPublicNamespaceFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(key, nsSet3, val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(key, nsSet3, bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(key, val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(key, bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(key, nsSet1, val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(key, nsSet1, bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(key, nsSet2, val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(key, nsSet2, bindOpts)
                );

                checkWrongBindOptionsFail(
                    (bindOpts, val) => array.instance.AS_trySetPropertyObj(key, nsSet3, val, bindOpts),
                    bindOpts => array.instance.AS_deletePropertyObj(key, nsSet3, bindOpts)
                );
            }

            void checkNonPublicNamespaceFail(
                Func<BindOptions, ASAny, BindStatus> setPropFunc,
                Func<BindOptions, bool> deletePropFunc,
                BindStatus setPropExpectedStatus = BindStatus.FAILED_CREATEDYNAMICNONPUBLIC
            ) {
                void checkWithBindOpts(BindOptions bindOpts) {
                    Assert.Equal(setPropExpectedStatus, setPropFunc(bindOpts, randomSample(valueDomain)));
                    checkArrayStateNoChange();
                    Assert.Equal(0, array.instance.AS_dynamicProps.count);
                    Assert.False(deletePropFunc(bindOpts));
                    checkArrayStateNoChange();
                    Assert.Equal(0, array.instance.AS_dynamicProps.count);
                }

                checkWithBindOpts(BindOptions.SEARCH_DYNAMIC);
                checkWithBindOpts(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS);
                checkWithBindOpts(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE);
                checkWithBindOpts(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_TRAITS);
            }

            void checkWrongBindOptionsFail(
                Func<BindOptions, ASAny, BindStatus> setPropFunc,
                Func<BindOptions, bool> deletePropFunc
            ) {
                void checkWithBindOpts(BindOptions bindOpts) {
                    Assert.Equal(BindStatus.NOT_FOUND, setPropFunc(bindOpts, randomSample(valueDomain)));
                    checkArrayStateNoChange();
                    Assert.False(deletePropFunc(bindOpts));
                    checkArrayStateNoChange();
                }

                checkWithBindOpts(BindOptions.SEARCH_TRAITS);
                checkWithBindOpts(BindOptions.SEARCH_PROTOTYPE);
                checkWithBindOpts(BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_TRAITS);

                checkWithBindOpts(BindOptions.SEARCH_DYNAMIC | BindOptions.ATTRIBUTE);
                checkWithBindOpts(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE | BindOptions.ATTRIBUTE);
                checkWithBindOpts(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_TRAITS | BindOptions.ATTRIBUTE);
                checkWithBindOpts(BindOptions.SEARCH_DYNAMIC | BindOptions.SEARCH_PROTOTYPE | BindOptions.SEARCH_TRAITS | BindOptions.ATTRIBUTE);
            }
        }

        public static IEnumerable<object[]> propertyBindingCallAndConstructTest_data() {
            setRandomSeed(521704);

            const uint maxLength = UInt32.MaxValue;
            const uint maxIndex = UInt32.MaxValue - 1;

            var valueDomain = new ASAny[50];

            for (int i = 0; i < 3; i++)
                valueDomain[i] = ASAny.undefined;

            for (int i = 3; i < 5; i++)
                valueDomain[i] = ASAny.@null;

            for (int i = 5; i < 20; i++)
                valueDomain[i] = new ASObject();

            for (int i = 20; i < 40; i++)
                valueDomain[i] = new SpyFunctionObject((recv, args) => new ASObject());

            for (int i = 40; i < 50; i++)
                valueDomain[i] = new SpyFunctionObject((recv, args) => new ASObject(), new ASObject());

            var testcases = new List<(ArrayWrapper, Image, IndexDict)>();

            void addTestCase(
                (ASArray arr, Image img) initialState, IEnumerable<Mutation> mutations = null, IndexDict prototype = null)
            {
                var (curArr, curImg) = initialState;
                applyMutations(curArr, mutations);
                applyMutations(ref curImg, mutations);

                testcases.Add((new ArrayWrapper(curArr), curImg, prototype));
            }

            addTestCase(makeEmptyArrayAndImage());
            addTestCase(makeEmptyArrayAndImage(20));
            addTestCase(makeEmptyArrayAndImage(maxLength));

            addTestCase(makeRandomDenseArrayAndImage(100, valueDomain));

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetRandomIndexType(randomIndex(100), randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: rangeSelect(0, 99, i => mutSetRandomIndexType(randomIndex(maxLength), randomSample(valueDomain)))
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 29, i => mutPushOne(randomSample(valueDomain))),
                    mutPush(rangeSelect(0, 29, i => randomSample(valueDomain))),
                    rangeSelect(0, 29, i => mutUnshift(randomSample(valueDomain))),
                    mutUnshift(rangeSelect(0, 29, i => randomSample(valueDomain))),
                    rangeMultiSelect(0, 9, i => new[] {mutPop(), mutShift()})
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(50, valueDomain),
                mutations: buildMutationList(
                    rangeSelect(0, 99, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(0, 19, i => mutPush(randomSample(valueDomain))),
                    rangeSelect(0, 19, i => mutUnshift(randomSample(valueDomain))),
                    rangeSelect(130, 139, i => mutDelRandomIndexType(i)),
                    rangeSelect(0, 19, i => mutDelRandomIndexType(randomIndex(130)))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 119, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(0, 39, i => mutDelRandomIndexType(i * 3)),
                    rangeSelect(0, 39, i => mutDelRandomIndexType(i * 3 + 1))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(),
                mutations: buildMutationList(
                    rangeSelect(0, 29, i => mutPushOne(randomSample(valueDomain))),
                    mutSetLength(40),
                    rangeSelect(0, 29, i => mutPushOne(randomSample(valueDomain))),
                    mutSetLength(65),
                    mutSetLength(80)
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                mutations: buildMutationList(
                    rangeSelect(0, 49, i => mutSetRandomIndexType(i, randomSample(valueDomain))),
                    rangeSelect(maxIndex - 49, maxIndex, i => mutSetRandomIndexType(i, randomSample(valueDomain)))
                )
            );

            addTestCase(
                initialState: makeRandomDenseArrayAndImage(50, valueDomain),
                mutations: new[] {mutSetLength(70)},
                prototype: new IndexDict {
                    [0] = ASAny.@null,
                    [5] = ASAny.undefined,
                    [14] = new ASObject(),
                    [34] = new SpyFunctionObject(),
                    [45] = new SpyFunctionObject(),
                    [52] = new ASObject(),
                    [59] = ASAny.undefined,
                    [68] = new SpyFunctionObject(),
                    [70] = new SpyFunctionObject(),
                    [74] = ASAny.@null,
                    [79] = new ASObject(),
                    [10000] = new SpyFunctionObject()
                }
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(100),
                mutations: rangeSelect(0, 19, i => mutSetUint(randomIndex(100), randomSample(valueDomain))),
                prototype: new IndexDict(
                    rangeSelect(0, 99, i => new KeyValuePair<uint, ASAny>(i, (i % 5 == 0) ? new ASObject() : new SpyFunctionObject()))
                )
            );

            addTestCase(
                initialState: makeEmptyArrayAndImage(maxLength),
                prototype: new IndexDict {
                    [0] = new SpyFunctionObject(),
                    [(uint)Int32.MaxValue] = new SpyFunctionObject(),
                    [maxIndex] = new SpyFunctionObject()
                }
            );

            return testcases.Select(x => TupleHelper.toArray(x));
        }

        [Theory]
        [MemberData(nameof(propertyBindingCallAndConstructTest_data))]
        public void propertyBindingCallAndConstructTest(ArrayWrapper array, Image image, IndexDict prototype) {
            var nsSet1 = new NamespaceSet(Namespace.@public);
            var nsSet2 = new NamespaceSet(new Namespace("a"), Namespace.@public, new Namespace(NamespaceKind.PROTECTED, "b"));
            var nsSet3 = new NamespaceSet(new Namespace("a"), new Namespace(NamespaceKind.PROTECTED, "b"));

            var argValueDomain = makeUniqueValues(20);
            var argArrays = new ASAny[10][];

            argArrays[0] = Array.Empty<ASAny>();
            for (int i = 1; i < argArrays.Length; i++) {
                argArrays[i] = new ASAny[i];
                for (int j = 0; j < i; j++)
                    argArrays[i][j] = randomSample(argValueDomain);
            }

            setRandomSeed(10455);
            setPrototypeProperties(prototype);

            try {
                var sampledIndices = new HashSet<uint>();

                const uint smallArrayLength = 500;
                const int lessThanLengthSampleCount = 500;
                const int moreThanLengthSampleCount = 500;

                if (image.length <= smallArrayLength) {
                    // If the length of the array is small then do an exhaustive check.
                    for (uint i = 0; i <= image.length; i++)
                        testIndex(i);
                }
                else {
                    // For large arrays, check all nonempty indices and a random sample of empty indices.
                    var nonEmptyIndices = new HashSet<uint>();

                    nonEmptyIndices.UnionWith(image.elements.Keys);
                    if (s_currentProtoProperties != null)
                        nonEmptyIndices.UnionWith(s_currentProtoProperties.Keys);

                    foreach (var index in nonEmptyIndices)
                        testIndex(index);

                    for (int i = 0; i < lessThanLengthSampleCount; i++) {
                        uint index = randomIndex(image.length);

                        if (sampledIndices.Add(index)
                            && !image.elements.ContainsKey(index)
                            && !(s_currentProtoProperties != null && s_currentProtoProperties.ContainsKey(index)))
                        {
                            testIndex(index);
                        }
                    }
                }

                // Check a random sample of indices greater than or equal to the array length
                // and verify that they do not contain any value.
                if (image.length < UInt32.MaxValue) {
                    for (int i = 0; i < moreThanLengthSampleCount; i++) {
                        uint index = image.length + randomIndex(UInt32.MaxValue - image.length);
                        if (sampledIndices.Add(index))
                            testIndex(index);
                    }
                }
            }
            finally {
                resetPrototypeProperties();
            }

            void testIndex(uint index) {
                ASAny valueOnObject = default, valueOnPrototype = default;

                bool valueExistsOnObject = image.elements.TryGetValue(index, out valueOnObject);

                bool valueExistsOnProto = s_currentProtoProperties != null
                    && s_currentProtoProperties.TryGetValue(index, out valueOnPrototype);

                string indexStr = indexToString(index);
                ASAny tmp;

                void check(
                    Func<BindOptions, ASAny[], (BindStatus, ASAny)> callPropFunc,
                    Func<BindOptions, ASAny[], (BindStatus, ASAny)> constructPropFunc
                ) {
                    verifyPropertyBindingCallConstructHelper(
                        callPropFunc,
                        constructPropFunc,
                        array.instance,
                        randomSample(argArrays),
                        isValidKey: true,
                        valueExistsOnObject,
                        valueExistsOnProto,
                        valueOnObject,
                        valueOnPrototype
                    );
                }

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallProperty(QName.publicName(indexStr), args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructProperty(QName.publicName(indexStr), args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallProperty(indexStr, nsSet1, args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructProperty(indexStr, nsSet1, args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallProperty(indexStr, nsSet2, args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructProperty(indexStr, nsSet2, args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(indexStr, args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(indexStr, args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(indexStr, nsSet1, args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(indexStr, nsSet1, args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(indexStr, nsSet2, args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(indexStr, nsSet2, args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("", indexStr), args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("", indexStr), args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("", indexStr), nsSet1, args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("", indexStr), nsSet1, args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("", indexStr), nsSet2, args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("", indexStr), nsSet2, args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("", indexStr), nsSet3, args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("", indexStr), nsSet3, args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(index, args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(index, args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(index, nsSet1, args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(index, nsSet1, args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(index, nsSet2, args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(index, nsSet2, args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj((double)index, args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj((double)index, args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj((double)index, nsSet1, args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj((double)index, nsSet1, args, out tmp, bindOpts), tmp)
                );

                check(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj((double)index, nsSet2, args, out tmp, bindOpts), tmp),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj((double)index, nsSet2, args, out tmp, bindOpts), tmp)
                );

                if (index <= (uint)Int32.MaxValue) {
                    check(
                        (bindOpts, args) => (array.instance.AS_tryCallPropertyObj((int)index, args, out tmp, bindOpts), tmp),
                        (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj((int)index, args, out tmp, bindOpts), tmp)
                    );
                    check(
                        (bindOpts, args) => (array.instance.AS_tryCallPropertyObj((int)index, nsSet1, args, out tmp, bindOpts), tmp),
                        (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj((int)index, nsSet1, args, out tmp, bindOpts), tmp)
                    );
                    check(
                        (bindOpts, args) => (array.instance.AS_tryCallPropertyObj((int)index, nsSet2, args, out tmp, bindOpts), tmp),
                        (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj((int)index, nsSet2, args, out tmp, bindOpts), tmp)
                    );
                }
            }
        }

        [Theory]
        [MemberData(nameof(propertyBindingAccessTest_nonIndexNames_data))]
        public void propertyBindingCallAndConstructTest_nonIndexNames(ArrayWrapper array) {
            setRandomSeed(84930);

            var nsSet1 = new NamespaceSet(Namespace.@public);
            var nsSet2 = new NamespaceSet(new Namespace("a"), Namespace.@public, new Namespace(NamespaceKind.PROTECTED, "b"));
            var nsSet3 = new NamespaceSet(new Namespace("a"), new Namespace(NamespaceKind.PROTECTED, "b"));

            var prototypeDPs = new Dictionary<string, ASAny> {
                ["+9"] = new SpyFunctionObject((obj, args) => new ASObject()),
                ["6.0"] = new ASObject(),
                [" 8 "] = ASAny.@null,
                ["4294967295"] = new SpyFunctionObject((obj, args) => ASAny.undefined, new ASObject()),
                ["NaN"] = new SpyFunctionObject(),
                ["def"] = new SpyFunctionObject(),
                ["ghi"] = new ASObject(),
            };

            var arrayDPs = new Dictionary<string, ASAny> {
                ["+5"] = new SpyFunctionObject((obj, args) => new ASObject()),
                [" 8"] = new SpyFunctionObject(),
                [" 8 "] = new SpyFunctionObject(),
                ["-1"] = new ASObject(),
                ["-001"] = new SpyFunctionObject(),
                ["-10000"] = default,
                ["010"] = new SpyFunctionObject((obj, args) => new ASObject()),
                ["+0010"] = new ASObject(),
                ["4."] = new ASObject(),
                ["4.0"] = default,
                ["4.3"] = new SpyFunctionObject(),
                ["6.0"] = ASAny.@null,
                ["1e+1"] = new ASObject(),
                ["4294967295"] = new SpyFunctionObject((obj, args) => new ASObject()),
                ["6781000394"] = ASAny.undefined,
                ["Infinity"] = new SpyFunctionObject(),
                ["-Infinity"] = ASAny.@null,
                ["NaN"] = new SpyFunctionObject((obj, args) => ASAny.undefined, new ASObject()),
                ["abc"] = new SpyFunctionObject(),
                ["def"] = new ASObject(),
            };

            string[] nonIndexStringKeys = {
                // Negative signs
                "-1", "-10000", "-2", "-0",
                // Positive signs
                "+1", "+2", "+5", "+9", "+0",
                // Leading zeros
                "00", "010", "+0010", "-001",
                // Spaces
                " 8", "8 ", " 8 ",
                // Decimal points
                "0.", "0.0", "4.", "4.0", "6.0", "4.3", "3.5", "0.0001", ".4",
                // E-notation
                "0e+1", "2e0", "2e+0", "1e1", "1e+1",
                // Large numbers
                "4294967295", "6781000394", "10000000000",
                // Infinity/NaN
                "Infinity", "+Infinity", "-Infinity", "NaN",
                // Non-numeric strings
                "", "abc", "def", "ghi"
            };

            ASAny[] nonIndexObjectKeys = {
                // Negative numbers
                -1, -1.0, -2, -2.0, -10000, -10000.0,
                // Fractions
                4.3, 3.5, 0.5, 0.0001,
                // Large numbers
                4294967295u, 4294967295.0, 4294967296.0, 6781000394.0, 10000000000.0,
                // Infinity/NaN
                Double.PositiveInfinity, Double.NegativeInfinity, Double.NaN
            };

            var argValueDomain = makeUniqueValues(20);
            var argArrays = new ASAny[10][];

            argArrays[0] = Array.Empty<ASAny>();
            for (int i = 1; i < argArrays.Length; i++) {
                argArrays[i] = new ASAny[i];
                for (int j = 0; j < i; j++)
                    argArrays[i][j] = randomSample(argValueDomain);
            }

            setPrototypeProperties(null); // Take prototype lock
            ASObject arrayProto = null;

            try {
                arrayProto = s_arrayClassPrototype;
                foreach (var (k, v) in prototypeDPs)
                    arrayProto.AS_dynamicProps.setValue(k, v);

                foreach (var (k, v) in arrayDPs)
                    array.instance.AS_dynamicProps.setValue(k, v);

                for (uint i = 0; i < 10; i++) {
                    testStringKey(indexToString(i), true);
                    testObjectKey(indexToString(i), true);
                    testObjectKey(i, true);
                    testObjectKey((int)i, true);
                    testObjectKey((double)i, true);
                }

                for (int i = 0; i < nonIndexStringKeys.Length; i++) {
                    testStringKey(nonIndexStringKeys[i], false);
                    testObjectKey(nonIndexStringKeys[i], false);
                }

                for (int i = 0; i < nonIndexObjectKeys.Length; i++) {
                    testObjectKey(nonIndexObjectKeys[i], false);
                }
            }
            finally {
                foreach (var (k, _) in prototypeDPs)
                    arrayProto.AS_dynamicProps.delete(k);

                resetPrototypeProperties();     // Release lock
            }

            void testStringKey(string key, bool isIndex) {
                ASAny ret;

                if (!isIndex) {
                    string keyStr = ASAny.AS_convertString(key);

                    ASAny valueOnObject;
                    ASAny valueOnPrototype;

                    bool valueExistsOnObject = arrayDPs.TryGetValue(keyStr, out valueOnObject);
                    bool valueExistsOnPrototype = prototypeDPs.TryGetValue(keyStr, out valueOnPrototype);

                    void checkSuccess(
                        Func<BindOptions, ASAny[], (BindStatus, ASAny)> callPropFunc,
                        Func<BindOptions, ASAny[], (BindStatus, ASAny)> constructPropFunc
                    ) {
                        verifyPropertyBindingCallConstructHelper(
                            callPropFunc,
                            constructPropFunc,
                            array.instance,
                            randomSample(argArrays),
                            isValidKey: true,
                            valueExistsOnObject,
                            valueExistsOnPrototype,
                            valueOnObject,
                            valueOnPrototype
                        );
                    }

                    checkSuccess(
                        (bindOpts, args) => (array.instance.AS_tryCallProperty(QName.publicName(key), args, out ret, bindOpts), ret),
                        (bindOpts, args) => (array.instance.AS_tryConstructProperty(QName.publicName(key), args, out ret, bindOpts), ret)
                    );
                    checkSuccess(
                        (bindOpts, args) => (array.instance.AS_tryCallProperty(key, nsSet1, args, out ret, bindOpts), ret),
                        (bindOpts, args) => (array.instance.AS_tryConstructProperty(key, nsSet1, args, out ret, bindOpts), ret)
                    );
                    checkSuccess(
                        (bindOpts, args) => (array.instance.AS_tryCallProperty(key, nsSet2, args, out ret, bindOpts), ret),
                        (bindOpts, args) => (array.instance.AS_tryConstructProperty(key, nsSet2, args, out ret, bindOpts), ret)
                    );
                    checkSuccess(
                        (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("", key), args, out ret, bindOpts), ret),
                        (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("", key), args, out ret, bindOpts), ret)
                    );
                    checkSuccess(
                        (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("", key), nsSet1, args, out ret, bindOpts), ret),
                        (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("", key), nsSet1, args, out ret, bindOpts), ret)
                    );
                    checkSuccess(
                        (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("", key), nsSet2, args, out ret, bindOpts), ret),
                        (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("", key), nsSet2, args, out ret, bindOpts), ret)
                    );
                    checkSuccess(
                        (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("", key), nsSet3, args, out ret, bindOpts), ret),
                        (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("", key), nsSet3, args, out ret, bindOpts), ret)
                    );
                }

                checkNonPublicNamespaceFail(
                    (bindOpts, args) => (array.instance.AS_tryCallProperty(new QName("a", key), args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructProperty(new QName("a", key), args, out ret, bindOpts), ret)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, args) => (array.instance.AS_tryCallProperty(new QName(new Namespace(NamespaceKind.PACKAGE_INTERNAL, ""), key), args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructProperty(new QName(new Namespace(NamespaceKind.PACKAGE_INTERNAL, ""), key), args, out ret, bindOpts), ret)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, args) => (array.instance.AS_tryCallProperty(new QName(Namespace.any, key), args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructProperty(new QName(Namespace.any, key), args, out ret, bindOpts), ret)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, args) => (array.instance.AS_tryCallProperty(key, nsSet3, args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructProperty(key, nsSet3, args, out ret, bindOpts), ret)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("a", key), args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("a", key), args, out ret, bindOpts), ret)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("*", key), args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("*", key), args, out ret, bindOpts), ret)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("a", key), nsSet2, args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("a", key), nsSet2, args, out ret, bindOpts), ret)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("a", key), nsSet3, args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("a", key), nsSet3, args, out ret, bindOpts), ret)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("*", key), nsSet2, args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("*", key), nsSet2, args, out ret, bindOpts), ret)
                );

                checkNonPublicNamespaceFail(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("*", key), nsSet3, args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("*", key), nsSet3, args, out ret, bindOpts), ret)
                );

                checkAttributeFlagFail(
                    (bindOpts, args) => (array.instance.AS_tryCallProperty(QName.publicName(key), args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructProperty(QName.publicName(key), args, out ret, bindOpts), ret)
                );

                checkAttributeFlagFail(
                    (bindOpts, args) => (array.instance.AS_tryCallProperty(key, nsSet1, args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructProperty(key, nsSet1, args, out ret, bindOpts), ret)
                );

                checkAttributeFlagFail(
                    (bindOpts, args) => (array.instance.AS_tryCallProperty(key, nsSet2, args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructProperty(key, nsSet2, args, out ret, bindOpts), ret)
                );

                checkAttributeFlagFail(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("", key), args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("", key), args, out ret, bindOpts), ret)
                );

                checkAttributeFlagFail(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("", key), nsSet1, args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("", key), nsSet1, args, out ret, bindOpts), ret)
                );

                checkAttributeFlagFail(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(new ASQName("", key), nsSet2, args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(new ASQName("", key), nsSet2, args, out ret, bindOpts), ret)
                );
            }

            void testObjectKey(ASAny key, bool isIndex) {
                ASAny ret;

                if (!isIndex) {
                    string keyStr = ASAny.AS_convertString(key);

                    ASAny valueOnObject;
                    ASAny valueOnPrototype;

                    bool valueExistsOnObject = arrayDPs.TryGetValue(keyStr, out valueOnObject);
                    bool valueExistsOnPrototype = prototypeDPs.TryGetValue(keyStr, out valueOnPrototype);

                    void checkSuccess(
                        Func<BindOptions, ASAny[], (BindStatus, ASAny)> callPropFunc,
                        Func<BindOptions, ASAny[], (BindStatus, ASAny)> constructPropFunc
                    ) {
                        verifyPropertyBindingCallConstructHelper(
                            callPropFunc,
                            constructPropFunc,
                            array.instance,
                            randomSample(argArrays),
                            isValidKey: true,
                            valueExistsOnObject,
                            valueExistsOnPrototype,
                            valueOnObject,
                            valueOnPrototype
                        );
                    }

                    checkSuccess(
                        (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(key, args, out ret, bindOpts), ret),
                        (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(key, args, out ret, bindOpts), ret)
                    );
                    checkSuccess(
                        (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(key, nsSet1, args, out ret, bindOpts), ret),
                        (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(key, nsSet1, args, out ret, bindOpts), ret)
                    );
                    checkSuccess(
                        (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(key, nsSet2, args, out ret, bindOpts), ret),
                        (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(key, nsSet2, args, out ret, bindOpts), ret)
                    );
                }

                checkNonPublicNamespaceFail(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(key, nsSet3, args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(key, nsSet3, args, out ret, bindOpts), ret)
                );

                checkAttributeFlagFail(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(key, args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(key, args, out ret, bindOpts), ret)
                );

                checkAttributeFlagFail(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(key, nsSet1, args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(key, nsSet1, args, out ret, bindOpts), ret)
                );

                checkAttributeFlagFail(
                    (bindOpts, args) => (array.instance.AS_tryCallPropertyObj(key, nsSet2, args, out ret, bindOpts), ret),
                    (bindOpts, args) => (array.instance.AS_tryConstructPropertyObj(key, nsSet2, args, out ret, bindOpts), ret)
                );
            }

            void checkNonPublicNamespaceFail(
                Func<BindOptions, ASAny[], (BindStatus, ASAny)> callPropFunc,
                Func<BindOptions, ASAny[], (BindStatus, ASAny)> constructPropFunc
            ) {
                verifyPropertyBindingCallConstructHelper(
                    callPropFunc,
                    constructPropFunc,
                    array.instance,
                    randomSample(argArrays),
                    isValidKey: false
                );
            }

            void checkAttributeFlagFail(
                Func<BindOptions, ASAny[], (BindStatus, ASAny)> callPropFunc,
                Func<BindOptions, ASAny[], (BindStatus, ASAny)> constructPropFunc
            ) {
                verifyPropertyBindingCallConstructHelper(
                    callPropFunc,
                    constructPropFunc,
                    array.instance,
                    randomSample(argArrays),
                    isValidKey: false,
                    additionalBindOpts: BindOptions.ATTRIBUTE
                );
            }
        }

    }

}
