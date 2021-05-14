using System;
using System.Collections.Generic;
using System.Linq;
using Mariana.AVM2.Core;
using Mariana.AVM2.Native;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class DynamicPropertyCollectionTest {

        public enum MutationKind {
            SET,
            DELETE,
            SET_ENUM,
        }

        public struct Mutation {
            public MutationKind type;
            public int key;
            public int value;
            public bool? isEnum;
            public IEnumerable<(int, int, bool)> state;
        }

        public (int keySize, int valSize) getDomainSizes(IEnumerable<Mutation> mutations) {
            int maxKey = 0, maxVal = 0;
            foreach (Mutation mut in mutations) {
                maxKey = Math.Max(maxKey, mut.key);
                maxVal = Math.Max(maxVal, mut.value);
            }
            return (maxKey + 1, maxVal + 1);
        }

        private static string[] makeKeys(int seed, int domainSize) {
            var random = new Random(seed);
            var stringSet = new HashSet<string>();

            while (stringSet.Count < domainSize)
                stringSet.Add(RandomHelper.randomString(random, 0, 60, ' ', 'z'));

            return stringSet.ToArray();
        }

        private static ASAny[] makeValues(int domainSize) {
            var array = new ASAny[domainSize];

            // Leave the first element as undefined.
            for (int i = 1; i < domainSize; i++)
                array[i] = new ASObject();

            return array;
        }

        private static (int value, bool isEnum)[] makeStateMap(IEnumerable<(int, int, bool)> state, int keyDomainSize) {
            var map = new (int, bool)[keyDomainSize];
            for (int i = 0; i < keyDomainSize; i++)
                map[i] = (-1, false);

            foreach (var (key, val, isEnum) in state)
                map[key] = (val, isEnum);

            return map;
        }

        private static void applyMutation(
            DynamicPropertyCollection instance, in Mutation mutation, string[] keyDomain, ASAny[] valueDomain)
        {
            switch (mutation.type) {
                case MutationKind.SET:
                    instance.setValue(keyDomain[mutation.key], valueDomain[mutation.value], mutation.isEnum ?? true);
                    break;
                case MutationKind.SET_ENUM:
                    instance.setEnumerable(keyDomain[mutation.key], mutation.isEnum ?? true);
                    break;
                case MutationKind.DELETE:
                    Assert.True(instance.delete(keyDomain[mutation.key]));
                    break;
            }
        }

        private static void applyMutationsAndVerify(
            DynamicPropertyCollection instance, IEnumerable<Mutation> mutations, string[] keyDomain, ASAny[] valueDomain)
        {
            var keyCopies = new string[keyDomain.Length];
            for (int i = 0; i < keyDomain.Length; i++)
                keyCopies[i] = new string(keyDomain[i].AsSpan());

            foreach (Mutation mut in mutations) {
                applyMutation(instance, mut, keyDomain, valueDomain);

                var stateMap = makeStateMap(mut.state, keyDomain.Length);
                int propCount = 0;

                for (int i = 0; i < keyDomain.Length; i++) {
                    var (expectedVal, expectedIsEnum) = stateMap[i];

                    if (expectedVal != -1) {
                        AssertHelper.identical(valueDomain[expectedVal], instance[keyCopies[i]]);
                        AssertHelper.identical(valueDomain[expectedVal], instance.getValue(keyCopies[i]));

                        Assert.True(instance.hasValue(keyCopies[i]));
                        Assert.Equal(expectedIsEnum, instance.isEnumerable(keyCopies[i]));

                        Assert.True(instance.tryGetValue(keyCopies[i], out ASAny tryGetValResult));
                        AssertHelper.identical(valueDomain[expectedVal], tryGetValResult);

                        propCount++;
                    }
                    else {
                        AssertHelper.identical(ASAny.undefined, instance[keyCopies[i]]);
                        AssertHelper.identical(ASAny.undefined, instance.getValue(keyCopies[i]));

                        Assert.False(instance.hasValue(keyCopies[i]));
                        Assert.False(instance.isEnumerable(keyCopies[i]));

                        Assert.False(instance.tryGetValue(keyCopies[i], out ASAny tryGetValResult));
                        AssertHelper.identical(ASAny.undefined, tryGetValResult);

                        Assert.False(instance.delete(keyCopies[i]));
                    }
                }

                Assert.Equal(propCount, instance.count);
            }
        }

        private static IEnumerable<Mutation> genRandomTestCase(
            int numCommands, int numKeys, int numValues, int seed, int setRatio = 0, int deleteRatio = 0, int setEnumRatio = 0)
        {
            var random = new Random(seed);
            string[] keyDomain = makeKeys(random.Next(), numKeys);
            ASAny[] valueDomain = makeValues(numValues);

            int[] keyValues = new int[numKeys];
            bool[] keyIsEnum = new bool[numKeys];

            keyValues.AsSpan().Fill(-1);

            IEnumerable<(int, int, bool)> createState() =>
                Enumerable.Range(0, numKeys).Where(i => keyValues[i] != -1).Select(i => (i, keyValues[i], keyIsEnum[i])).ToArray();

            (bool, Mutation) genSet() {
                var mutation = new Mutation {
                    type = MutationKind.SET,
                    key = random.Next(numKeys),
                    value = random.Next(numValues),
                    isEnum = random.Next(2) != 0,
                };

                if (keyValues[mutation.key] == -1)
                    keyIsEnum[mutation.key] = mutation.isEnum.Value;

                keyValues[mutation.key] = mutation.value;
                mutation.state = createState();

                return (true, mutation);
            }

            (bool, Mutation) genDelete() {
                int key = random.Next(numKeys);
                if (keyValues[key] == -1)
                    return (false, default);

                var mutation = new Mutation {
                    type = MutationKind.DELETE,
                    key = key
                };

                keyValues[mutation.key] = -1;
                keyIsEnum[mutation.key] = false;
                mutation.state = createState();

                return (true, mutation);
            }

            (bool, Mutation) genSetEnum() {
                int key = random.Next(numKeys);
                if (keyValues[key] == -1)
                    return (false, default);

                var mutation = new Mutation {
                    type = MutationKind.SET_ENUM,
                    key = key,
                    isEnum = random.Next(2) != 0,
                };

                keyIsEnum[mutation.key] = mutation.isEnum.Value;
                mutation.state = createState();

                return (true, mutation);
            }

            var funcs = new Func<(bool, Mutation)>[] {genSet, genDelete, genSetEnum};
            var weights = new int[] {setRatio, deleteRatio, setEnumRatio};

            return RandomHelper.sampleFunctions(random, funcs, weights).Take(numCommands).ToArray();
        }

        public static IEnumerable<object[]> getSetValueTest_data() {
            var testCase1 = new Mutation[] {
                new Mutation {
                    type = MutationKind.SET, key = 0, value = 0, state = new[] {(0, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 1, value = 4, state = new[] {(0, 0, true), (1, 4, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 2, state = new[] {(0, 0, true), (1, 4, true), (4, 2, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 5, value = 0, state = new[] {(0, 0, true), (1, 4, true), (4, 2, true), (5, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 1, value = 3,
                    state = new[] {(0, 0, true), (1, 3, true), (4, 2, true), (5, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 6, value = 6,
                    state = new[] {(0, 0, true), (1, 3, true), (4, 2, true), (5, 0, true), (6, 6, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 3, value = 6,
                    state = new[] {(0, 0, true), (1, 3, true), (4, 2, true), (5, 0, true), (6, 6, true), (3, 6, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 2,
                    state = new[] {(0, 0, true), (1, 3, true), (4, 2, true), (5, 0, true), (6, 6, true), (3, 6, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 5,
                    state = new[] {(0, 0, true), (1, 3, true), (4, 5, true), (5, 0, true), (6, 6, true), (3, 6, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 9, value = 1,
                    state = new[] {(0, 0, true), (1, 3, true), (4, 5, true), (5, 0, true), (6, 6, true), (3, 6, true), (9, 1, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 9, value = 7,
                    state = new[] {(0, 0, true), (1, 3, true), (4, 5, true), (5, 0, true), (6, 6, true), (3, 6, true), (9, 7, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 0, value = 6,
                    state = new[] {(0, 6, true), (1, 3, true), (4, 5, true), (5, 0, true), (6, 6, true), (3, 6, true), (9, 7, true)}
                },
            };

            var testCase2 = new Mutation[] {
                new Mutation {
                    type = MutationKind.SET, key = 0, value = 2, isEnum = false, state = new[] {(0, 2, false)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 2, isEnum = true, state = new[] {(0, 2, false), (4, 2, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 0, value = 2, isEnum = true, state = new[] {(0, 2, false), (4, 2, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 0, value = 2, isEnum = false, state = new[] {(0, 2, false), (4, 2, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 5, isEnum = true, state = new[] {(0, 2, false), (4, 5, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 3, isEnum = false, state = new[] {(0, 2, false), (4, 3, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 3, value = 2, isEnum = true, state = new[] {(0, 2, false), (4, 3, true), (3, 2, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 6, value = 7, isEnum = false,
                    state = new[] {(0, 2, false), (4, 3, true), (3, 2, true), (6, 7, false)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 2, value = 4, isEnum = false,
                    state = new[] {(0, 2, false), (4, 3, true), (3, 2, true), (6, 7, false), (2, 4, false)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 5, value = 8, isEnum = true,
                    state = new[] {(0, 2, false), (4, 3, true), (3, 2, true), (6, 7, false), (2, 4, false), (5, 8, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 1, value = 4, isEnum = true,
                    state = new[] {(0, 2, false), (4, 3, true), (3, 2, true), (6, 7, false), (2, 4, false), (5, 8, true), (1, 4, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 6, value = 7, isEnum = true,
                    state = new[] {(0, 2, false), (4, 3, true), (3, 2, true), (6, 7, false), (2, 4, false), (5, 8, true), (1, 4, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 6, value = 5, isEnum = true,
                    state = new[] {(0, 2, false), (4, 3, true), (3, 2, true), (6, 5, false), (2, 4, false), (5, 8, true), (1, 4, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 0, value = 3, isEnum = true,
                    state = new[] {(0, 3, false), (4, 3, true), (3, 2, true), (6, 5, false), (2, 4, false), (5, 8, true), (1, 4, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 5, value = 8, isEnum = false,
                    state = new[] {(0, 3, false), (4, 3, true), (3, 2, true), (6, 5, false), (2, 4, false), (5, 8, true), (1, 4, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 8, value = 10, isEnum = false,
                    state = new[] {(0, 3, false), (4, 3, true), (3, 2, true), (6, 5, false), (2, 4, false), (5, 8, true), (1, 4, true), (8, 10, false)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 8, value = 0, isEnum = true,
                    state = new[] {(0, 3, false), (4, 3, true), (3, 2, true), (6, 5, false), (2, 4, false), (5, 8, true), (1, 4, true), (8, 0, false)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 6, value = 7, isEnum = false,
                    state = new[] {(0, 3, false), (4, 3, true), (3, 2, true), (6, 7, false), (2, 4, false), (5, 8, true), (1, 4, true), (8, 0, false)}
                },
            };

            var testCase3 = Enumerable.Empty<Mutation>()
                .Concat(Enumerable.Range(0, 300).Select(i => new Mutation {
                    type = MutationKind.SET, key = i, value = i, isEnum = (i % 3) == 0,
                    state = Enumerable.Range(0, i + 1).Select(j => (j, j, j % 3 == 0))
                }))
                .Concat(Enumerable.Range(0, 300).Select(i => new Mutation {
                    type = MutationKind.SET, key = i, value = 300 - i, isEnum = i % 2 == 0,
                    state = Enumerable.Range(0, i + 1).Select(j => (j, 300 - j, j % 3 == 0))
                        .Concat(Enumerable.Range(i + 1, 300 - i - 1).Select(j => (j, j, j % 3 == 0)))
                }));

            var testCase4 = genRandomTestCase(
                numCommands: 500, numKeys: 200, numValues: 150, seed: 859483312, setRatio: 1
            );

            yield return new object[] {testCase1};
            yield return new object[] {testCase2};
            yield return new object[] {testCase3};
            yield return new object[] {testCase4};
        }

        [Theory]
        [MemberData(nameof(getSetValueTest_data))]
        public void getSetValueTest(IEnumerable<Mutation> mutations) {
            var (keyDomainSize, valueDomainSize) = getDomainSizes(mutations);
            string[] keyDomain = makeKeys(1983941119, keyDomainSize);
            ASAny[] valueDomain = makeValues(valueDomainSize);
            var instance = new DynamicPropertyCollection();

            applyMutationsAndVerify(instance, mutations, keyDomain, valueDomain);
        }

        public static IEnumerable<object[]> getSetDeleteTest_data() {
            var testCase1 = new Mutation[] {
                new Mutation {
                    type = MutationKind.SET, key = 0, value = 0, state = new[] {(0, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 1, value = 4, state = new[] {(0, 0, true), (1, 4, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 2, state = new[] {(0, 0, true), (1, 4, true), (4, 2, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 5, value = 0, state = new[] {(0, 0, true), (1, 4, true), (4, 2, true), (5, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 1, value = 3,
                    state = new[] {(0, 0, true), (1, 3, true), (4, 2, true), (5, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 6, value = 6,
                    state = new[] {(0, 0, true), (1, 3, true), (4, 2, true), (5, 0, true), (6, 6, true)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 0,
                    state = new[] {(1, 3, true), (4, 2, true), (5, 0, true), (6, 6, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 3, value = 6,
                    state = new[] {(1, 3, true), (4, 2, true), (5, 0, true), (6, 6, true), (3, 6, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 2,
                    state = new[] {(1, 3, true), (4, 2, true), (5, 0, true), (6, 6, true), (3, 6, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 5,
                    state = new[] {(1, 3, true), (4, 5, true), (5, 0, true), (6, 6, true), (3, 6, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 9, value = 1,
                    state = new[] {(1, 3, true), (4, 5, true), (5, 0, true), (6, 6, true), (3, 6, true), (9, 1, true)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 9,
                    state = new[] {(1, 3, true), (4, 5, true), (5, 0, true), (6, 6, true), (3, 6, true)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 4,
                    state = new[] {(1, 3, true), (5, 0, true), (6, 6, true), (3, 6, true)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 3,
                    state = new[] {(1, 3, true), (5, 0, true), (6, 6, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 10, value = 0,
                    state = new[] {(1, 3, true), (5, 0, true), (6, 6, true), (10, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 4,
                    state = new[] {(1, 3, true), (5, 0, true), (6, 6, true), (10, 0, true), (4, 4, true)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 1,
                    state = new[] {(5, 0, true), (6, 6, true), (10, 0, true), (4, 4, true)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 4,
                    state = new[] {(5, 0, true), (6, 6, true), (10, 0, true)}
                },
            };

            var testCase2 = new Mutation[] {
                new Mutation {
                    type = MutationKind.SET, key = 0, value = 2, isEnum = false, state = new[] {(0, 2, false)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 2, isEnum = true, state = new[] {(0, 2, false), (4, 2, true)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 0, state = new[] {(4, 2, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 0, value = 2, isEnum = true, state = new[] {(0, 2, true), (4, 2, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 5, value = 1, isEnum = false, state = new[] {(0, 2, true), (4, 2, true), (5, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 0, value = 4, isEnum = false, state = new[] {(0, 4, true), (4, 2, true), (5, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 3, value = 8, isEnum = false,
                    state = new[] {(0, 4, true), (4, 2, true), (5, 1, false), (3, 8, false)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 6, value = 0, isEnum = true,
                    state = new[] {(0, 4, true), (4, 2, true), (5, 1, false), (3, 8, false), (6, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 8, value = 4, isEnum = false,
                    state = new[] {(0, 4, true), (4, 2, true), (5, 1, false), (3, 8, false), (6, 0, true), (8, 4, false)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 0,
                    state = new[] {(4, 2, true), (5, 1, false), (3, 8, false), (6, 0, true), (8, 4, false)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 3,
                    state = new[] {(4, 2, true), (5, 1, false), (6, 0, true), (8, 4, false)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 8,
                    state = new[] {(4, 2, true), (5, 1, false), (6, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 8, isEnum = false,
                    state = new[] {(4, 8, true), (5, 1, false), (6, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 8, value = 5, isEnum = true,
                    state = new[] {(4, 8, true), (5, 1, false), (6, 0, true), (8, 5, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 3, value = 4, isEnum = false,
                    state = new[] {(4, 8, true), (5, 1, false), (6, 0, true), (8, 5, true), (3, 4, false)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 5,
                    state = new[] {(4, 8, true), (6, 0, true), (8, 5, true), (3, 4, false)}
                },
            };

            var testCase3 = Enumerable.Empty<Mutation>()
                .Concat(Enumerable.Range(0, 300).Select(i => new Mutation {
                    type = MutationKind.SET, key = i, value = i, isEnum = false,
                    state = Enumerable.Range(0, i + 1).Select(j => (j, j, false))
                }))
                .Concat(Enumerable.Range(0, 300).Select(i => new Mutation {
                    type = MutationKind.DELETE, key = i,
                    state = Enumerable.Range(i + 1, 300 - i - 1).Select(j => (j, j, false))
                }))
                .Concat(Enumerable.Range(0, 300).Select(i => new Mutation {
                    type = MutationKind.SET, key = 299 - i, value = i, isEnum = true,
                    state = Enumerable.Range(0, i + 1).Select(j => (299 - j, j, true))
                }))
                .Concat(Enumerable.Range(0, 300).Select(i => new Mutation {
                    type = MutationKind.DELETE, key = i,
                    state = Enumerable.Range(i + 1, 300 - i - 1).Select(j => (j, 299 - j, true))
                }));

            var testCase4 = genRandomTestCase(
                numCommands: 750, numKeys: 200, numValues: 200, seed: 563371, setRatio: 2, deleteRatio: 1
            );

            yield return new object[] {testCase1};
            yield return new object[] {testCase2};
            yield return new object[] {testCase3};
            yield return new object[] {testCase4};
        }

        [Theory]
        [MemberData(nameof(getSetDeleteTest_data))]
        public void getSetDeleteTest(IEnumerable<Mutation> mutations) {
            var (keyDomainSize, valueDomainSize) = getDomainSizes(mutations);
            string[] keyDomain = makeKeys(758716334, keyDomainSize);
            ASAny[] valueDomain = makeValues(valueDomainSize);
            var instance = new DynamicPropertyCollection();

            applyMutationsAndVerify(instance, mutations, keyDomain, valueDomain);
        }

        public static IEnumerable<object[]> setEnumTest_data() {
            var testCase1 = new Mutation[] {
                new Mutation {
                    type = MutationKind.SET, key = 0, value = 2, state = new[] {(0, 2, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 1, value = 3, isEnum = false, state = new[] {(0, 2, true), (1, 3, false)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 0, isEnum = true, state = new[] {(0, 2, true), (1, 3, false), (4, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 0, isEnum = false, state = new[] {(0, 2, false), (1, 3, false), (4, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 4, isEnum = true, state = new[] {(0, 2, false), (1, 3, false), (4, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 1, isEnum = true, state = new[] {(0, 2, false), (1, 3, true), (4, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 2, isEnum = false, state = new[] {(0, 2, false), (1, 3, true), (4, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 2, value = 5, state = new[] {(0, 2, false), (1, 3, true), (4, 0, true), (2, 5, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 1, isEnum = false,
                    state = new[] {(0, 2, false), (1, 3, true), (4, 1, true), (2, 5, true)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 4, isEnum = false,
                    state = new[] {(0, 2, false), (1, 3, true), (4, 1, false), (2, 5, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 6, value = 3, isEnum = false,
                    state = new[] {(0, 2, false), (1, 3, true), (4, 1, false), (2, 5, true), (6, 3, false)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 8, value = 7, isEnum = true,
                    state = new[] {(0, 2, false), (1, 3, true), (4, 1, false), (2, 5, true), (6, 3, false), (8, 7, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 5, value = 8,
                    state = new[] {(0, 2, false), (1, 3, true), (4, 1, false), (2, 5, true), (6, 3, false), (8, 7, true), (5, 8, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 10, value = 1, isEnum = false,
                    state = new[] {(0, 2, false), (1, 3, true), (4, 1, false), (2, 5, true), (6, 3, false), (8, 7, true), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 4,
                    state = new[] {(0, 2, false), (1, 3, true), (4, 4, false), (2, 5, true), (6, 3, false), (8, 7, true), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 6, isEnum = true,
                    state = new[] {(0, 2, false), (1, 3, true), (4, 4, false), (2, 5, true), (6, 3, true), (8, 7, true), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 0, isEnum = true,
                    state = new[] {(0, 2, true), (1, 3, true), (4, 4, false), (2, 5, true), (6, 3, true), (8, 7, true), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 6, isEnum = true,
                    state = new[] {(0, 2, true), (1, 3, true), (4, 4, false), (2, 5, true), (6, 3, true), (8, 7, true), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 6, isEnum = false,
                    state = new[] {(0, 2, true), (1, 3, true), (4, 4, false), (2, 5, true), (6, 3, false), (8, 7, true), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 4, isEnum = true,
                    state = new[] {(0, 2, true), (1, 3, true), (4, 4, true), (2, 5, true), (6, 3, false), (8, 7, true), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 2, isEnum = true,
                    state = new[] {(0, 2, true), (1, 3, true), (4, 4, true), (2, 5, true), (6, 3, false), (8, 7, true), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 8, isEnum = false,
                    state = new[] {(0, 2, true), (1, 3, true), (4, 4, true), (2, 5, true), (6, 3, false), (8, 7, false), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 11, isEnum = false,
                    state = new[] {(0, 2, true), (1, 3, true), (4, 4, true), (2, 5, true), (6, 3, false), (8, 7, false), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 3, isEnum = true,
                    state = new[] {(0, 2, true), (1, 3, true), (4, 4, true), (2, 5, true), (6, 3, false), (8, 7, false), (5, 8, true), (10, 1, false)}
                },
            };

            var testCase2 = new Mutation[] {
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 1, isEnum = false, state = Enumerable.Empty<(int, int, bool)>()
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 4, isEnum = false, state = Enumerable.Empty<(int, int, bool)>()
                },
                new Mutation {
                    type = MutationKind.SET, key = 0, value = 2, state = new[] {(0, 2, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 1, value = 3, isEnum = false, state = new[] {(0, 2, true), (1, 3, false)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 0, isEnum = true, state = new[] {(0, 2, true), (1, 3, false), (4, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 1, isEnum = true, state = new[] {(0, 2, true), (1, 3, true), (4, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 0, isEnum = false, state = new[] {(0, 2, false), (1, 3, true), (4, 0, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 2, value = 5, state = new[] {(0, 2, false), (1, 3, true), (4, 0, true), (2, 5, true)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 0, state = new[] {(1, 3, true), (4, 0, true), (2, 5, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 1, isEnum = false, state = new[] {(1, 3, true), (4, 1, true), (2, 5, true)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 4, isEnum = false, state = new[] {(1, 3, true), (4, 1, false), (2, 5, true)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 4, state = new[] {(1, 3, true), (2, 5, true)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 4, isEnum = false, state = new[] {(1, 3, true), (2, 5, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 4, value = 7, state = new[] {(1, 3, true), (2, 5, true), (4, 7, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 6, value = 3, isEnum = false,
                    state = new[] {(1, 3, true), (2, 5, true), (4, 7, true), (6, 3, false)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 8, value = 7, isEnum = true,
                    state = new[] {(1, 3, true), (2, 5, true), (4, 7, true), (6, 3, false), (8, 7, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 5, value = 8,
                    state = new[] {(1, 3, true), (2, 5, true), (4, 7, true), (6, 3, false), (8, 7, true), (5, 8, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 10, value = 1, isEnum = false,
                    state = new[] {(1, 3, true), (2, 5, true), (4, 7, true), (6, 3, false), (8, 7, true), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 1,
                    state = new[] {(2, 5, true), (4, 7, true), (6, 3, false), (8, 7, true), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 6,
                    state = new[] {(2, 5, true), (4, 7, true), (8, 7, true), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 0, isEnum = true,
                    state = new[] {(2, 5, true), (4, 7, true), (8, 7, true), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 6, isEnum = true,
                    state = new[] {(2, 5, true), (4, 7, true), (8, 7, true), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 6, isEnum = false,
                    state = new[] {(2, 5, true), (4, 7, true), (8, 7, true), (5, 8, true), (10, 1, false)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 10, isEnum = true,
                    state = new[] {(2, 5, true), (4, 7, true), (8, 7, true), (5, 8, true), (10, 1, true)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 2, isEnum = false,
                    state = new[] {(2, 5, false), (4, 7, true), (8, 7, true), (5, 8, true), (10, 1, true)}
                },
                new Mutation {
                    type = MutationKind.DELETE, key = 8,
                    state = new[] {(2, 5, false), (4, 7, true), (5, 8, true), (10, 1, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 8, value = 9, isEnum = false,
                    state = new[] {(2, 5, false), (4, 7, true), (5, 8, true), (8, 9, false), (10, 1, true)}
                },
                new Mutation {
                    type = MutationKind.SET_ENUM, key = 8, isEnum = true,
                    state = new[] {(2, 5, false), (4, 7, true), (5, 8, true), (8, 9, true), (10, 1, true)}
                },
                new Mutation {
                    type = MutationKind.SET, key = 6, value = 3,
                    state = new[] {(2, 5, false), (4, 7, true), (5, 8, true), (8, 9, true), (10, 1, true), (6, 3, true)}
                },
            };

            var testCase3 = Enumerable.Empty<Mutation>()
                .Concat(Enumerable.Range(0, 200).Select(i => new Mutation {
                    type = MutationKind.SET, key = i, value = i, isEnum = false,
                    state = Enumerable.Range(0, i + 1).Select(j => (j, j, false))
                }))
                .Concat(Enumerable.Range(0, 200).Select(i => new Mutation {
                    type = MutationKind.SET_ENUM, key = i, isEnum = i % 2 == 0,
                    state = Enumerable.Range(0, 200).Select(j => (j, j, j <= i && j % 2 == 0))
                }))
                .Concat(Enumerable.Range(0, 200).Select(i => new Mutation {
                    type = MutationKind.DELETE, key = 199 - i,
                    state = Enumerable.Range(0, 199 - i).Select(j => (j, j, j % 2 == 0))
                }))
                .Concat(Enumerable.Range(0, 200).SelectMany(i => new Mutation[] {
                    new Mutation {
                        type = MutationKind.SET, key = i, value = 0, isEnum = i % 2 != 0,
                        state = Enumerable.Range(0, i + 1).Select(j => (j, 0, (j < i) ? j % 3 != 0 : j % 2 != 0))
                    },
                    new Mutation {
                        type = MutationKind.SET_ENUM, key = i, value = 0, isEnum = i % 3 != 0,
                        state = Enumerable.Range(0, i + 1).Select(j => (j, 0, j % 3 != 0))
                    },
                }));

            var testCase4 = genRandomTestCase(
                numCommands: 800, numKeys: 150, numValues: 150, seed: 85744, setRatio: 3, deleteRatio: 1, setEnumRatio: 2
            );

            yield return new object[] {testCase1};
            yield return new object[] {testCase2};
            yield return new object[] {testCase3};
            yield return new object[] {testCase4};
        }

        [Theory]
        [MemberData(nameof(setEnumTest_data))]
        public void setEnumTest(IEnumerable<Mutation> mutations) {
            var (keyDomainSize, valueDomainSize) = getDomainSizes(mutations);
            string[] keyDomain = makeKeys(46350001, keyDomainSize);
            ASAny[] valueDomain = makeValues(valueDomainSize);
            var instance = new DynamicPropertyCollection();

            applyMutationsAndVerify(instance, mutations, keyDomain, valueDomain);
        }

        public static IEnumerable<object[]> indexAccessTest_data() {
            var testCase1 = genRandomTestCase(
                numCommands: 600, numKeys: 150, numValues: 150, seed: 1884030, setRatio: 1
            );

            var testCase2 = genRandomTestCase(
                numCommands: 800, numKeys: 150, numValues: 150, seed: 1958044, setRatio: 3, deleteRatio: 1, setEnumRatio: 2
            );

            yield return new object[] {testCase1};
            yield return new object[] {testCase2};
        }

        [Theory]
        [MemberData(nameof(indexAccessTest_data))]
        public void indexAccessTest(IEnumerable<Mutation> mutations) {
            var (keyDomainSize, valueDomainSize) = getDomainSizes(mutations);
            string[] keyDomain = makeKeys(994723, keyDomainSize);
            ASAny[] valueDomain = makeValues(valueDomainSize);
            var instance = new DynamicPropertyCollection();

            var keyCopies = new string[keyDomain.Length];
            for (int i = 0; i < keyDomain.Length; i++)
                keyCopies[i] = new string(keyDomain[i].AsSpan());

            foreach (Mutation mut in mutations) {
                applyMutation(instance, mut, keyDomain, valueDomain);

                var stateMap = makeStateMap(mut.state, keyDomain.Length);
                var enumerableIndexSet = new HashSet<int>();

                for (int i = 0; i < keyDomain.Length; i++) {
                    var (expectedVal, expectedIsEnum) = stateMap[i];

                    if (expectedVal != -1) {
                        int index = instance.getIndex(keyCopies[i]);
                        Assert.NotEqual(-1, index);
                        Assert.Equal(keyDomain[i], instance.getNameFromIndex(index));
                        AssertHelper.identical(valueDomain[expectedVal], instance.getValueFromIndex(index));

                        if (expectedIsEnum)
                            Assert.True(enumerableIndexSet.Add(index));
                    }
                    else {
                        Assert.Equal(-1, instance.getIndex(keyCopies[i]));
                    }
                }

                int curIndex = instance.getNextIndex(-1);
                while (curIndex != -1) {
                    Assert.True(enumerableIndexSet.Remove(curIndex));
                    curIndex = instance.getNextIndex(curIndex);
                }

                Assert.Empty(enumerableIndexSet);
            }
        }

        public static IEnumerable<object[]> prototypeChainSearchTest_data() {
            string[] keys = makeKeys(847331, 12);
            ASAny[] values = makeValues(12);

            ASObject makeObject((int, int, bool)[] props, ASObject prototype) {
                ASObject obj = ASObject.AS_createWithPrototype(prototype);
                for (int i = 0; i < props.Length; i++) {
                    var (key, value, isEnum) = props[i];
                    obj.AS_dynamicProps.setValue(keys[key], values[value], isEnum);
                }
                return obj;
            }

            object[] testCase1() {
                (int, int, bool)[] props1 = {(0, 1, true), (2, 1, false), (4, 5, false), (8, 6, true), (10, 3, false)};
                (int, int, bool)[] props2 = {(1, 0, true), (3, 2, true), (5, 7, false), (8, 5, false), (9, 1, true), (10, 6, true)};
                (int, int, bool)[] props3 = {(2, 5, true), (5, 9, true), (6, 3, false), (10, 7, false)};

                ASObject obj1 = makeObject(props1, null);
                ASObject obj2 = makeObject(props2, obj1);
                ASObject obj3 = makeObject(props3, obj2);

                return new object[] {keys, values, new[] {obj1, obj2, obj3}, new[] {props1, props2, props3}};
            }

            object[] testCase2() {
                (int, int, bool)[] props1 = {(0, 1, true), (2, 1, false), (4, 5, false), (8, 6, true), (10, 3, false)};
                (int, int, bool)[] props2 = {(1, 0, true), (3, 2, true), (5, 7, false), (8, 5, false), (9, 1, true), (10, 6, true)};
                (int, int, bool)[] props3 = {(2, 5, true), (5, 9, true), (6, 3, false), (10, 7, false)};

                ASObject nonDynamicObject = new DynamicPropertyCollectionTest_NonDynamicClass();
                ASObject nonDynamicClassPrototype = nonDynamicObject.AS_proto;

                for (int i = 0; i < props1.Length; i++) {
                    var (key, value, isEnum) = props1[i];
                    nonDynamicClassPrototype.AS_dynamicProps.setValue(keys[key], values[value], isEnum);
                }

                ASObject obj2 = makeObject(props2, nonDynamicObject);
                ASObject obj3 = makeObject(props3, obj2);

                return new object[] {
                    keys,
                    values,
                    new[] {nonDynamicClassPrototype, nonDynamicObject, obj2, obj3},
                    new[] {props1, Array.Empty<(int, int, bool)>(), props2, props3}
                };
            }

            yield return testCase1();
            yield return testCase2();
        }

        [Theory]
        [MemberData(nameof(prototypeChainSearchTest_data))]
        public void prototypeChainSearchTest(string[] keys, ASAny[] values, ASObject[] objects, (int, int, bool)[][] props) {
            string[] keyCopies = new string[keys.Length];
            for (int i = 0; i < keyCopies.Length; i++)
                keyCopies[i] = new string(keys[i].AsSpan());

            var stateMaps = new (int value, bool isEnum)[objects.Length][];
            for (int i = 0; i < stateMaps.Length; i++)
                stateMaps[i] = makeStateMap(props[i], keys.Length);

            for (int i = 0; i < keys.Length; i++) {
                var expectedValues = new int[objects.Length];

                for (int j = 0; j < objects.Length; j++) {
                    if (stateMaps[j][i].value != -1)
                        expectedValues[j] = stateMaps[j][i].value;
                    else
                        expectedValues[j] = (j > 0) ? expectedValues[j - 1] : -1;
                }

                for (int j = 0; j < objects.Length; j++) {
                    bool result = DynamicPropertyCollection.searchPrototypeChain(objects[j], keyCopies[i], out ASAny propVal);
                    Assert.Equal(expectedValues[j] != -1, result);
                    AssertHelper.identical((expectedValues[j] != -1) ? values[expectedValues[j]] : default, propVal);
                }
            }
        }

        [Fact]
        public void prototypeChainSearchTest_nullObject() {
            Assert.False(DynamicPropertyCollection.searchPrototypeChain(null, "a", out ASAny val));
            AssertHelper.identical(ASAny.undefined, val);
        }

        [Fact]
        public void nullNameThrowTest() {
            var instance = new DynamicPropertyCollection();

            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_NULL, () => instance[null]);
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_NULL, () => instance[null] = default);
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_NULL, () => instance.getValue(null));
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_NULL, () => instance.setValue(null, default));
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_NULL, () => instance.tryGetValue(null, out _));
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_NULL, () => instance.getIndex(null));
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_NULL, () => instance.delete(null));

            AssertHelper.throwsErrorWithCode(
                ErrorCode.MARIANA__ARGUMENT_NULL,
                () => DynamicPropertyCollection.searchPrototypeChain(new ASObject(), null, out _)
            );
        }

        [Fact]
        public void getNameValueFromInvalidIndexTest() {
            var instance = new DynamicPropertyCollection();

            instance["a"] = default;
            instance["b"] = default;
            instance["c"] = default;
            instance["d"] = default;
            instance["e"] = default;
            instance["f"] = default;

            instance.delete("c");
            instance.delete("e");

            var validIndices = new HashSet<int>();
            int curIndex = instance.getNextIndex(-1);

            while (curIndex != -1) {
                validIndices.Add(curIndex);
                curIndex = instance.getNextIndex(curIndex);
            }

            for (int i = -2; i <= 10; i++) {
                if (!validIndices.Contains(i)) {
                    Assert.Null(instance.getNameFromIndex(i));
                    AssertHelper.identical(ASAny.undefined, instance.getValueFromIndex(i));
                }
            }
        }

    }

    [AVM2ExportClass]
    public class DynamicPropertyCollectionTest_NonDynamicClass : ASObject {
        static DynamicPropertyCollectionTest_NonDynamicClass() =>
            TestAppDomain.ensureClassesLoaded(typeof(DynamicPropertyCollectionTest_NonDynamicClass));
    }

}
