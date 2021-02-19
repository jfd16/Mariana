using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Mariana.Common.Tests {

    public sealed class ReferenceSetTest {

        public enum CmdType {
            CREATE,
            ADD,
            DEL,
            CLEAR,
            UNION,
            INTERSECT,
        }

        public struct Command {
            public CmdType type;
            public object arg;
            public IEnumerable<int> keysInSet;
        }

        private class Key : IEquatable<Key> {
            public bool Equals(Key other) => true;
            public override bool Equals(object obj) => true;
            public override int GetHashCode() => 0;
        }

        private static Key[] makeKeys(int count) =>
            Enumerable.Range(0, count).Select(x => new Key()).ToArray();

        private static bool[] makeKeyMap(int keyDomainSize, IEnumerable<int> keys) {
            var keyMap = new bool[keyDomainSize];
            if (keys != null) {
                foreach (int k in keys)
                    keyMap[k] = true;
            }
            return keyMap;
        }

        private static int getDomainSize(IEnumerable<Command> commands) {
            commands = commands.Where(x => x.type != CmdType.CREATE && x.type != CmdType.CLEAR && x.arg != null);
            if (!commands.Any())
                return 0;

            int maxIndex = commands.Max(x => {
                if (!(x.arg is IEnumerable<int> seq))
                    return (int)x.arg;
                return seq.Any() ? seq.Max() : 0;
            });

            return maxIndex + 1;
        }

        private static ReferenceSet<Key> makeSet(Key[] domain, IEnumerable<int> keys) {
            var set = new ReferenceSet<Key>();
            if (keys != null) {
                foreach (int k in keys)
                    set.add(domain[k]);
            }
            return set;
        }

        private static void assertSetHasKeys(ReferenceSet<Key> set, Key[] domain, IEnumerable<int> keys) {
            int keyCount = (keys == null) ? 0 : keys.Count();
            Assert.Equal(keyCount, set.count);

            bool[] keyMap = makeKeyMap(domain.Length, keys);

            for (int i = 0; i < domain.Length; i++) {
                if (keyMap[i]) {
                    Assert.True(set.find(domain[i]));
                    Assert.False(set.add(domain[i]));
                }
                else {
                    Assert.False(set.find(domain[i]));
                    Assert.False(set.delete(domain[i]));
                }
            }

            Assert.Equal(keyCount, set.count);
        }

        private static bool runCommand(ref ReferenceSet<Key> set, Key[] domain, Command cmd) {
            switch (cmd.type) {
                case CmdType.CREATE:
                    set = new ReferenceSet<Key>((int)cmd.arg);
                    break;
                case CmdType.ADD:
                    return set.add(domain[(int)cmd.arg]);
                case CmdType.DEL:
                    return set.delete(domain[(int)cmd.arg]);
                case CmdType.CLEAR:
                    set.clear();
                    break;
                case CmdType.UNION:
                    set.unionWith(makeSet(domain, (IEnumerable<int>)cmd.arg));
                    break;
                case CmdType.INTERSECT:
                    set.intersectWith(makeSet(domain, (IEnumerable<int>)cmd.arg));
                    break;
            }
            return true;
        }

        private static IEnumerable<Command> genRandomCommands(
            int domainSize, int commandCount, int initCapacity, int seed, int clearRatio = 0, int addRatio = 0, int delRatio = 0)
        {
            yield return new Command {type = CmdType.CREATE, arg = initCapacity};

            var random = new Random(seed);
            var keysNotInSet = Enumerable.Range(0, domainSize).ToList();
            var keysInSet = new List<int>(domainSize);

            int commandsGenerated = 0;

            double pClear = (double)clearRatio / (double)(clearRatio + addRatio + delRatio);
            double pDel = pClear + (double)delRatio / (double)(clearRatio + addRatio + delRatio);

            while (commandsGenerated < commandCount) {
                double decision = random.NextDouble();
                if (decision < pClear) {
                    if (keysInSet.Count == 0)
                        continue;

                    keysNotInSet.AddRange(keysInSet);
                    keysInSet.Clear();
                    commandsGenerated++;
                    yield return new Command {type = CmdType.CLEAR};
                }
                else if (decision < pDel) {
                    if (keysInSet.Count == 0)
                        continue;

                    int index = random.Next(keysInSet.Count);
                    int key = keysInSet[index];
                    keysInSet.RemoveAt(index);
                    keysNotInSet.Add(key);
                    commandsGenerated++;
                    yield return new Command {type = CmdType.DEL, arg = key, keysInSet = keysInSet.ToArray()};
                }
                else {
                    if (keysNotInSet.Count == 0)
                        continue;

                    int index = random.Next(keysNotInSet.Count);
                    int key = keysNotInSet[index];
                    keysNotInSet.RemoveAt(index);
                    keysInSet.Add(key);
                    commandsGenerated++;
                    yield return new Command {type = CmdType.ADD, arg = key, keysInSet = keysInSet.ToArray()};
                }
            }
        }

        [Fact]
        public void constructorTest() {
            ReferenceSet<Key> set;

            set = new ReferenceSet<Key>();
            Assert.Equal(0, set.count);

            set = new ReferenceSet<Key>(100);
            Assert.Equal(0, set.count);

            Assert.Throws<ArgumentOutOfRangeException>(() => new ReferenceSet<Key>(-1));
        }

        public static IEnumerable<object[]> findAndAddTest_data() {
            Command[] testCase1 = {
                new Command {type = CmdType.CREATE, arg = 0},

                new Command {type = CmdType.ADD, arg = 2, keysInSet = new[] {2}},
                new Command {type = CmdType.ADD, arg = 4, keysInSet = new[] {2, 4}},
                new Command {type = CmdType.ADD, arg = 0, keysInSet = new[] {2, 4, 0}},
                new Command {type = CmdType.ADD, arg = 3, keysInSet = new[] {2, 4, 0, 3}},
                new Command {type = CmdType.ADD, arg = 7, keysInSet = new[] {2, 4, 0, 3, 7}},
                new Command {type = CmdType.ADD, arg = 13, keysInSet = new[] {2, 4, 0, 3, 7, 13}},
                new Command {type = CmdType.ADD, arg = 12, keysInSet = new[] {2, 4, 0, 3, 7, 13, 12}},
                new Command {type = CmdType.ADD, arg = 8, keysInSet = new[] {2, 4, 0, 3, 7, 13, 12, 8}},
                new Command {type = CmdType.ADD, arg = 14, keysInSet = new[] {2, 4, 0, 3, 7, 13, 12, 8, 14}},
                new Command {type = CmdType.ADD, arg = 10, keysInSet = new[] {2, 4, 0, 3, 7, 13, 12, 8, 14, 10}},
            };

            Command[] testCase2 = testCase1.AsSpan().ToArray();
            testCase2[0] = new Command {type = CmdType.CREATE, arg = 10};

            IEnumerable<Command> testCase3 = genRandomCommands(
                domainSize: 1500, commandCount: 1500, initCapacity: 0, seed: 194858112, addRatio: 1);
            IEnumerable<Command> testCase4 = genRandomCommands(
                domainSize: 1500, commandCount: 1500, initCapacity: 1500, seed: 194858112, addRatio: 1);

            yield return new object[] {testCase1};
            yield return new object[] {testCase2};
            yield return new object[] {testCase3};
            yield return new object[] {testCase4};
        }

        [Theory]
        [MemberData(nameof(findAndAddTest_data))]
        public void findAndAddTest(IEnumerable<Command> commands) {
            ReferenceSet<Key> set = null;
            Key[] domain = makeKeys(getDomainSize(commands));

            foreach (var cmd in commands) {
                bool result = runCommand(ref set, domain, cmd);
                Assert.True(result);
                assertSetHasKeys(set, domain, cmd.keysInSet);
            }
        }

        public static IEnumerable<object[]> findAddAndDeleteTest_data() {
            Command[] testCase1 = {
                new Command {type = CmdType.CREATE, arg = 0},

                new Command {type = CmdType.ADD, arg = 2, keysInSet = new[] {2}},
                new Command {type = CmdType.ADD, arg = 4, keysInSet = new[] {2, 4}},
                new Command {type = CmdType.ADD, arg = 1, keysInSet = new[] {2, 4, 1}},
                new Command {type = CmdType.DEL, arg = 2, keysInSet = new[] {4, 1}},
                new Command {type = CmdType.ADD, arg = 3, keysInSet = new[] {4, 1, 3}},
                new Command {type = CmdType.DEL, arg = 3, keysInSet = new[] {4, 1}},
                new Command {type = CmdType.ADD, arg = 7, keysInSet = new[] {4, 1, 7}},
                new Command {type = CmdType.ADD, arg = 13, keysInSet = new[] {4, 1, 7, 13}},
                new Command {type = CmdType.ADD, arg = 2, keysInSet = new[] {4, 1, 7, 13, 2}},
                new Command {type = CmdType.DEL, arg = 7, keysInSet = new[] {4, 1, 13, 2}},
                new Command {type = CmdType.DEL, arg = 13, keysInSet = new[] {4, 1, 2}},
                new Command {type = CmdType.DEL, arg = 1, keysInSet = new[] {4, 2}},
                new Command {type = CmdType.DEL, arg = 2, keysInSet = new[] {4}},
                new Command {type = CmdType.DEL, arg = 4},
                new Command {type = CmdType.ADD, arg = 13, keysInSet = new int[] {13}},
                new Command {type = CmdType.ADD, arg = 6, keysInSet = new int[] {6, 13}},
                new Command {type = CmdType.ADD, arg = 4, keysInSet = new int[] {4, 13, 6}},
                new Command {type = CmdType.ADD, arg = 1, keysInSet = new int[] {4, 1, 13, 6}},
                new Command {type = CmdType.ADD, arg = 8, keysInSet = new[] {4, 1, 13, 6, 8}},
                new Command {type = CmdType.ADD, arg = 14, keysInSet = new[] {4, 1, 13, 6, 8, 14}},
                new Command {type = CmdType.ADD, arg = 10, keysInSet = new[] {4, 1, 13, 6, 8, 14, 10}},
                new Command {type = CmdType.ADD, arg = 11, keysInSet = new[] {4, 1, 13, 6, 8, 14, 10, 11}},
                new Command {type = CmdType.ADD, arg = 17, keysInSet = new[] {4, 1, 13, 6, 8, 14, 10, 11, 17}},
                new Command {type = CmdType.DEL, arg = 4, keysInSet = new[] {1, 13, 6, 8, 14, 10, 11, 17}},
                new Command {type = CmdType.DEL, arg = 11, keysInSet = new[] {1, 13, 6, 8, 14, 10, 17}},
                new Command {type = CmdType.ADD, arg = 11, keysInSet = new[] {1, 13, 6, 8, 14, 10, 17, 11}},
                new Command {type = CmdType.DEL, arg = 11, keysInSet = new[] {1, 13, 6, 8, 14, 10, 17}},
                new Command {type = CmdType.DEL, arg = 8, keysInSet = new[] {1, 13, 6, 14, 10, 17}},
            };

            Command[] testCase2 = testCase1.AsSpan().ToArray();
            testCase2[0] = new Command {type = CmdType.CREATE, arg = 10};

            IEnumerable<Command> testCase3 = genRandomCommands(
                domainSize: 1000, commandCount: 2000, initCapacity: 0, seed: 473811936, addRatio: 2, delRatio: 1);
            IEnumerable<Command> testCase4 = genRandomCommands(
                domainSize: 1000, commandCount: 2000, initCapacity: 1000, seed: 473811936, addRatio: 2, delRatio: 1);

            IEnumerable<Command> testCase5 = Enumerable.Empty<Command>()
                .Append(
                    new Command {type = CmdType.CREATE, arg = 0}
                )
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.ADD, arg = i, keysInSet = Enumerable.Range(0, i + 1)
                }))
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.DEL, arg = i, keysInSet = Enumerable.Range(i + 1, 299 - i)
                }))
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.ADD, arg = i, keysInSet = Enumerable.Range(0, i + 1)
                }))
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.DEL, arg = 299 - i, keysInSet = Enumerable.Range(0, 299 - i)
                }));

            yield return new object[] {testCase1};
            yield return new object[] {testCase2};
            yield return new object[] {testCase3};
            yield return new object[] {testCase4};
            yield return new object[] {testCase5};
        }

        [Theory]
        [MemberData(nameof(findAddAndDeleteTest_data))]
        public void findAddAndDeleteTest(IEnumerable<Command> commands) {
            ReferenceSet<Key> set = null;
            Key[] domain = makeKeys(getDomainSize(commands));

            foreach (var cmd in commands) {
                bool result = runCommand(ref set, domain, cmd);
                Assert.True(result);
                assertSetHasKeys(set, domain, cmd.keysInSet);
            }
        }

        public static IEnumerable<object[]> findAddDeleteAndClearTest_data() {
            Command[] testCase1 = {
                new Command {type = CmdType.CREATE, arg = 0},

                new Command {type = CmdType.CLEAR},
                new Command {type = CmdType.ADD, arg = 2, keysInSet = new[] {2}},
                new Command {type = CmdType.ADD, arg = 4, keysInSet = new[] {2, 4}},
                new Command {type = CmdType.ADD, arg = 1, keysInSet = new[] {2, 4, 1}},
                new Command {type = CmdType.DEL, arg = 2, keysInSet = new[] {4, 1}},
                new Command {type = CmdType.ADD, arg = 3, keysInSet = new[] {4, 1, 3}},
                new Command {type = CmdType.DEL, arg = 3, keysInSet = new[] {4, 1}},
                new Command {type = CmdType.ADD, arg = 7, keysInSet = new[] {4, 1, 7}},
                new Command {type = CmdType.ADD, arg = 13, keysInSet = new[] {4, 1, 7, 13}},
                new Command {type = CmdType.ADD, arg = 2, keysInSet = new[] {4, 1, 7, 13, 2}},
                new Command {type = CmdType.DEL, arg = 7, keysInSet = new[] {4, 1, 13, 2}},
                new Command {type = CmdType.DEL, arg = 13, keysInSet = new[] {4, 1, 2}},
                new Command {type = CmdType.ADD, arg = 9, keysInSet = new[] {4, 1, 2, 9}},
                new Command {type = CmdType.CLEAR},
                new Command {type = CmdType.ADD, arg = 13, keysInSet = new int[] {13}},
                new Command {type = CmdType.ADD, arg = 6, keysInSet = new int[] {6, 13}},
                new Command {type = CmdType.ADD, arg = 4, keysInSet = new int[] {4, 13, 6}},
                new Command {type = CmdType.ADD, arg = 1, keysInSet = new int[] {4, 1, 13, 6}},
                new Command {type = CmdType.ADD, arg = 8, keysInSet = new[] {4, 1, 13, 6, 8}},
                new Command {type = CmdType.ADD, arg = 14, keysInSet = new[] {4, 1, 13, 6, 8, 14}},
                new Command {type = CmdType.ADD, arg = 10, keysInSet = new[] {4, 1, 13, 6, 8, 14, 10}},
                new Command {type = CmdType.ADD, arg = 11, keysInSet = new[] {4, 1, 13, 6, 8, 14, 10, 11}},
                new Command {type = CmdType.ADD, arg = 17, keysInSet = new[] {4, 1, 13, 6, 8, 14, 10, 11, 17}},
                new Command {type = CmdType.DEL, arg = 4, keysInSet = new[] {1, 13, 6, 8, 14, 10, 11, 17}},
                new Command {type = CmdType.DEL, arg = 11, keysInSet = new[] {1, 13, 6, 8, 14, 10, 17}},
                new Command {type = CmdType.ADD, arg = 11, keysInSet = new[] {1, 13, 6, 8, 14, 10, 17, 11}},
                new Command {type = CmdType.ADD, arg = 20, keysInSet = new[] {1, 13, 6, 8, 14, 10, 17, 11, 20}},
                new Command {type = CmdType.ADD, arg = 9, keysInSet = new[] {1, 13, 6, 8, 14, 10, 17, 11, 20, 9}},
                new Command {type = CmdType.CLEAR},
                new Command {type = CmdType.ADD, arg = 20, keysInSet = new[] {20}},
                new Command {type = CmdType.DEL, arg = 20},
                new Command {type = CmdType.CLEAR},
                new Command {type = CmdType.CLEAR},
                new Command {type = CmdType.ADD, arg = 9, keysInSet = new[] {9}},
                new Command {type = CmdType.ADD, arg = 13, keysInSet = new int[] {13, 9}},
                new Command {type = CmdType.ADD, arg = 6, keysInSet = new int[] {6, 13, 9}},
                new Command {type = CmdType.ADD, arg = 4, keysInSet = new int[] {4, 13, 6, 9}},
            };

            Command[] testCase2 = testCase1.AsSpan().ToArray();
            testCase2[0] = new Command {type = CmdType.CREATE, arg = 10};

            IEnumerable<Command> testCase3 = genRandomCommands(
                domainSize: 1000, commandCount: 3000, initCapacity: 0, seed: 473811936, clearRatio: 1, addRatio: 180, delRatio: 150);

            IEnumerable<Command> testCase4 = Enumerable.Empty<Command>()
                .Append(
                    new Command {type = CmdType.CREATE, arg = 0}
                )
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.ADD, arg = i, keysInSet = Enumerable.Range(0, i + 1)
                }))
                .Append(
                    new Command {type = CmdType.CLEAR}
                )
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.ADD, arg = i, keysInSet = Enumerable.Range(0, i + 1)
                }))
                .Concat(Enumerable.Range(0, 150).Select(i => new Command {
                    type = CmdType.DEL, arg = 299 - i, keysInSet = Enumerable.Range(0, 299 - i)
                }))
                .Append(
                    new Command {type = CmdType.CLEAR}
                );

            yield return new object[] {testCase1};
            yield return new object[] {testCase2};
            yield return new object[] {testCase3};
            yield return new object[] {testCase4};
        }

        [Theory]
        [MemberData(nameof(findAddDeleteAndClearTest_data))]
        public void findAddDeleteAndClearTest(IEnumerable<Command> commands) {
            ReferenceSet<Key> set = null;
            Key[] domain = makeKeys(getDomainSize(commands));

            foreach (var cmd in commands) {
                bool result = runCommand(ref set, domain, cmd);
                Assert.True(result);
                assertSetHasKeys(set, domain, cmd.keysInSet);
            }
        }

        [Fact]
        public void findAddDeleteAndClearTest_nullKey() {
            var set = new ReferenceSet<Key>();
            set.add(new Key());
            set.add(new Key());

            Assert.Throws<ArgumentNullException>(() => set.add(null));
            Assert.Throws<ArgumentNullException>(() => set.find(null));
            Assert.Throws<ArgumentNullException>(() => set.delete(null));
        }

        public static IEnumerable<object[]> toArrayTest_data = new object[][] {
            new object[] {
                genRandomCommands(
                    domainSize: 30, commandCount: 60, initCapacity: 0, seed: 87211499,
                    addRatio: 2, delRatio: 1
                )
            },
            new object[] {
                genRandomCommands(
                    domainSize: 600, commandCount: 1200, initCapacity: 300, seed: 87211499,
                    clearRatio: 1, addRatio: 120, delRatio: 100
                )
            },
        };

        [Theory]
        [MemberData(nameof(toArrayTest_data))]
        public void toArrayTest(IEnumerable<Command> commands) {
            ReferenceSet<Key> set = null;
            Key[] domain = makeKeys(getDomainSize(commands));

            foreach (Command cmd in commands) {
                runCommand(ref set, domain, cmd);

                Key[] array = set.toArray();
                bool[] keyMap = makeKeyMap(domain.Length, cmd.keysInSet);

                Assert.Equal((cmd.keysInSet == null) ? 0 : cmd.keysInSet.Count(), array.Length);

                for (int i = 0; i < domain.Length; i++)
                    Assert.Equal(keyMap[i], array.Any(x => x == domain[i]));
            }
        }

        public static IEnumerable<object[]> enumeratorTest_data = toArrayTest_data;

        [Theory]
        [MemberData(nameof(toArrayTest_data))]
        public void enumeratorTest(IEnumerable<Command> commands) {
            ReferenceSet<Key> set = null;
            Key[] domain = makeKeys(getDomainSize(commands));

            foreach (Command cmd in commands) {
                runCommand(ref set, domain, cmd);

                var enumerator = set.GetEnumerator();
                var enumerator2 = ((IEnumerable<Key>)set).GetEnumerator();
                var enumerator3 = ((IEnumerable)set).GetEnumerator();

                var enumResult1 = new List<Key>();
                while (enumerator.MoveNext())
                    enumResult1.Add(enumerator.Current);

                var enumResult2 = new List<Key>();
                while (enumerator2.MoveNext())
                    enumResult2.Add(enumerator2.Current);

                var enumResult3 = new List<Key>();
                while (enumerator3.MoveNext())
                    enumResult3.Add(Assert.IsType<Key>(enumerator3.Current));

                bool[] keyMap = makeKeyMap(domain.Length, cmd.keysInSet);

                for (int i = 0; i < domain.Length; i++) {
                    Assert.Equal(keyMap[i], enumResult1.Any(x => x == domain[i]));
                    Assert.Equal(keyMap[i], enumResult2.Any(x => x == domain[i]));
                    Assert.Equal(keyMap[i], enumResult3.Any(x => x == domain[i]));
                }

                Assert.Throws<NotImplementedException>(() => enumerator2.Reset());
                Assert.Throws<NotImplementedException>(() => enumerator3.Reset());

                enumerator.Dispose();
                enumerator2.Dispose();
            }
        }

        public static IEnumerable<object[]> unionAndIntersectWithTest_data() {
            Command[] testCases1 = {
                new Command {type = CmdType.CREATE, arg = 0},
                new Command {type = CmdType.UNION, arg = null, keysInSet = null},
                new Command {type = CmdType.ADD, arg = 1, keysInSet = new[] {1}},
                new Command {type = CmdType.UNION, arg = new[] {5, 12, 13, 17, 16}, keysInSet = new[] {5, 12, 13, 17, 16, 1}},
                new Command {type = CmdType.ADD, arg = 9, keysInSet = new[] {5, 12, 13, 17, 16, 1, 9}},
                new Command {type = CmdType.ADD, arg = 18, keysInSet = new[] {5, 12, 13, 17, 16, 1, 9, 18}},
                new Command {type = CmdType.UNION, arg = new[] {18, 5, 17, 1}, keysInSet = new[] {5, 12, 13, 17, 16, 1, 9, 18}},
                new Command {type = CmdType.UNION, arg = new[] {18, 5, 19, 17, 25, 1, 26, 3}, keysInSet = new[] {5, 12, 13, 17, 16, 1, 9, 18, 3, 25, 26, 19}},
                new Command {type = CmdType.UNION, arg = null, keysInSet = new[] {5, 12, 13, 17, 16, 1, 9, 18, 3, 25, 26, 19}},
            };

            Command[] testCases2 = {
                new Command {type = CmdType.CREATE, arg = 0},
                new Command {type = CmdType.INTERSECT, arg = null, keysInSet = null},
                new Command {type = CmdType.INTERSECT, arg = new[] {1, 2, 3, 4}, keysInSet = null},

                new Command {type = CmdType.ADD, arg = 2, keysInSet = new[] {2}},
                new Command {type = CmdType.ADD, arg = 4, keysInSet = new[] {2, 4}},
                new Command {type = CmdType.ADD, arg = 1, keysInSet = new[] {2, 4, 1}},
                new Command {type = CmdType.ADD, arg = 3, keysInSet = new[] {2, 4, 1, 3}},
                new Command {type = CmdType.ADD, arg = 7, keysInSet = new[] {2, 4, 1, 3, 7}},
                new Command {type = CmdType.ADD, arg = 13, keysInSet = new[] {2, 4, 1, 3, 7, 13}},
                new Command {type = CmdType.ADD, arg = 12, keysInSet = new[] {2, 4, 1, 3, 7, 13, 12}},
                new Command {type = CmdType.ADD, arg = 8, keysInSet = new[] {2, 4, 1, 3, 7, 13, 12, 8}},
                new Command {type = CmdType.ADD, arg = 14, keysInSet = new[] {2, 4, 1, 3, 7, 13, 12, 8, 14}},
                new Command {type = CmdType.ADD, arg = 10, keysInSet = new[] {2, 4, 1, 3, 7, 13, 12, 8, 14, 10}},

                new Command {type = CmdType.INTERSECT, arg = new[] {2, 4, 1, 3, 7, 13, 12, 8, 14, 10}, keysInSet = new[] {2, 4, 1, 3, 7, 13, 12, 8, 14, 10}},
                new Command {type = CmdType.INTERSECT, arg = new[] {4, 1, 3, 7, 12, 8, 14, 10}, keysInSet = new[] {4, 1, 3, 7, 12, 8, 14, 10}},
                new Command {type = CmdType.INTERSECT, arg = new[] {4, 3, 7, 8, 10, 2, 11, 19}, keysInSet = new[] {4, 3, 7, 8, 10}},
                new Command {type = CmdType.INTERSECT, arg = new[] {4, 3, 7, 8, 10, 11, 19}, keysInSet = new[] {4, 3, 7, 8, 10}},

                new Command {type = CmdType.ADD, arg = 12, keysInSet = new[] {4, 3, 7, 8, 10, 12}},
                new Command {type = CmdType.ADD, arg = 18, keysInSet = new[] {4, 3, 7, 8, 10, 12, 18}},
                new Command {type = CmdType.ADD, arg = 21, keysInSet = new[] {4, 3, 7, 8, 10, 12, 18, 21}},
                new Command {type = CmdType.ADD, arg = 27, keysInSet = new[] {4, 3, 7, 8, 10, 12, 18, 21, 27}},
                new Command {type = CmdType.INTERSECT, arg = new[] {10, 21, 9, 15, 8, 12, 7}, keysInSet = new[] {7, 8, 10, 12, 21}},
                new Command {type = CmdType.INTERSECT, arg = new[] {5, 9, 13, 3, 17, 20}, keysInSet = null},
            };

            Command[] testCases3 = {
                new Command {type = CmdType.CREATE, arg = 0},

                new Command {type = CmdType.UNION, arg = new[] {1, 2, 8, 4, 10}, keysInSet = new[] {1, 2, 8, 4, 10}},
                new Command {type = CmdType.UNION, arg = new[] {8, 11, 3, 9, 4}, keysInSet = new[] {1, 2, 8, 4, 10, 3, 9, 11}},
                new Command {type = CmdType.INTERSECT, arg = new[] {1, 2, 4, 10, 9, 11}, keysInSet = new[] {1, 2, 4, 10, 9, 11}},
                new Command {type = CmdType.INTERSECT, arg = new[] {1, 2, 4, 10, 9, 11}, keysInSet = new[] {1, 2, 4, 10, 9, 11}},
                new Command {type = CmdType.UNION, arg = new[] {5, 10, 15, 25}, keysInSet = new[] {1, 2, 4, 9, 11, 10, 5, 15, 25}},
                new Command {type = CmdType.UNION, arg = new[] {8}, keysInSet = new[] {1, 2, 8, 9, 11, 4, 10, 5, 15, 25}},
                new Command {type = CmdType.INTERSECT, arg = new[] {1, 2, 4, 8, 10, 5, 15, 25}, keysInSet = new[] {1, 2, 4, 8, 10, 5, 15, 25}},
                new Command {type = CmdType.UNION, arg = new[] {11, 14, 16, 19, 23, 26}, keysInSet = new[] {1, 2, 8, 4, 10, 5, 15, 25, 11, 14, 16, 19, 23, 26}},
                new Command {type = CmdType.INTERSECT, arg = Enumerable.Range(0, 30), keysInSet = new[] {1, 2, 8, 4, 10, 5, 15, 25, 11, 14, 16, 19, 23, 26}},
                new Command {type = CmdType.INTERSECT, arg = null, keysInSet = null},
                new Command {type = CmdType.UNION, arg = new[] {1, 2, 8, 4, 10, 5, 15, 25}, keysInSet = new[] {1, 2, 8, 4, 10, 5, 15, 25}},
                new Command {type = CmdType.UNION, arg = new[] {1, 2, 8, 4, 10, 5, 15, 27}, keysInSet = new[] {1, 2, 8, 4, 10, 5, 15, 25, 27}},
                new Command {type = CmdType.INTERSECT, arg = new[] {1, 2, 8, 10, 5, 15, 27, 22, 12, 16}, keysInSet = new[] {1, 2, 8, 10, 5, 15, 27}},
                new Command {type = CmdType.INTERSECT, arg = new[] {28, 29}, keysInSet = null},
                new Command {type = CmdType.INTERSECT, arg = new[] {28, 29}, keysInSet = null},
            };

            IEnumerable<Command> testCases4 = new[] {
                new Command {type = CmdType.CREATE, arg = 0},

                new Command {
                    type = CmdType.UNION,
                    arg = Enumerable.Range(0, 500).Where(x => x % 2 == 0),
                    keysInSet = Enumerable.Range(0, 500).Where(x => x % 2 == 0)
                },
                new Command {
                    type = CmdType.UNION,
                    arg = Enumerable.Range(0, 500).Where(x => x % 3 == 0),
                    keysInSet = Enumerable.Range(0, 500).Where(x => x % 2 == 0 || x % 3 == 0)
                },
                new Command {
                    type = CmdType.UNION,
                    arg = Enumerable.Range(0, 500).Where(x => x % 4 == 0),
                    keysInSet = Enumerable.Range(0, 500).Where(x => x % 2 == 0 || x % 3 == 0)
                },
                new Command {
                    type = CmdType.INTERSECT,
                    arg = Enumerable.Range(0, 500).Where(x => x % 18 == 0),
                    keysInSet = Enumerable.Range(0, 500).Where(x => x % 18 == 0)
                },
                new Command {
                    type = CmdType.INTERSECT,
                    arg = Enumerable.Range(0, 500).Where(x => x % 4 == 0),
                    keysInSet = Enumerable.Range(0, 500).Where(x => x % 36 == 0)
                },
                new Command {
                    type = CmdType.UNION,
                    arg = Enumerable.Range(0, 500).Where(x => x % 7 == 0),
                    keysInSet = Enumerable.Range(0, 500).Where(x => x % 36 == 0 || x % 7 == 0)
                },
                new Command {
                    type = CmdType.INTERSECT,
                    arg = Enumerable.Range(0, 500).Where(x => x > 0 && x % 89 == 0),
                    keysInSet = null
                },
            };

            yield return new object[] {testCases1};
            yield return new object[] {testCases2};
            yield return new object[] {testCases3};
            yield return new object[] {testCases4};
        }

        [Theory]
        [MemberData(nameof(unionAndIntersectWithTest_data))]
        public void unionAndIntersectWithTest(IEnumerable<Command> commands) {
            ReferenceSet<Key> set = null;
            Key[] domain = makeKeys(getDomainSize(commands));

            foreach (var cmd in commands) {
                bool result = runCommand(ref set, domain, cmd);
                Assert.True(result);
                assertSetHasKeys(set, domain, cmd.keysInSet);
            }
        }

        [Fact]
        public void unionAndIntersectWithTest_selfArg() {
            Key[] domain = makeKeys(30);
            ReferenceSet<Key> set;

            set = new ReferenceSet<Key>();
            set.unionWith(set);
            assertSetHasKeys(set, domain, Enumerable.Empty<int>());
            set.intersectWith(set);
            assertSetHasKeys(set, domain, Enumerable.Empty<int>());

            set = makeSet(domain, new[] {1, 4, 5, 9, 11, 15, 17, 22, 23, 28});
            set.unionWith(set);
            assertSetHasKeys(set, domain, new[] {1, 4, 5, 9, 11, 15, 17, 22, 23, 28});
            set.intersectWith(set);
            assertSetHasKeys(set, domain, new[] {1, 4, 5, 9, 11, 15, 17, 22, 23, 28});
        }

        [Fact]
        public void unionAndIntersectWithTest_nullArg() {
            ReferenceSet<Key> set = new ReferenceSet<Key>();
            Assert.Throws<ArgumentNullException>(() => set.unionWith(null));
            Assert.Throws<ArgumentNullException>(() => set.intersectWith(null));
        }

    }

}
