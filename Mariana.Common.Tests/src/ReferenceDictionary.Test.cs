using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Mariana.Common.Tests {

    public sealed class ReferenceDictionaryTest {

        public enum CmdType {
            CREATE,
            SET,
            TEST_SET,
            DEL,
            CLEAR,
        }

        public struct Command {
            public CmdType type;
            public int arg;
            public int arg2;
            public int arg3;
            public IEnumerable<(int, int)> contents;
        }

        private class Key : IEquatable<Key> {
            public bool Equals(Key other) => true;
            public override bool Equals(object obj) => true;
            public override int GetHashCode() => 0;
        }

        private static Key[] makeKeys(int count) =>
            Enumerable.Range(0, count).Select(x => new Key()).ToArray();

        private static int getKeyDomainSize(IEnumerable<Command> commands) {
            commands = commands.Where(x => x.type != CmdType.CREATE && x.type != CmdType.CLEAR);
            return commands.Any() ? commands.Max(x => x.arg) + 1 : 0;
        }

        private static int?[] makeKeyValueMap(int keyDomainSize, IEnumerable<(int, int)> pairs) {
            int?[] map = new int?[keyDomainSize];
            if (pairs != null) {
                foreach (var (k, v) in pairs)
                    map[k] = v;
            }
            return map;
        }

        private static object runCommand(ref ReferenceDictionary<Key, int> dict, Key[] keyDomain, Command cmd) {
            switch (cmd.type) {
                case CmdType.CREATE:
                    dict = new ReferenceDictionary<Key, int>(cmd.arg);
                    break;
                case CmdType.CLEAR:
                    dict.clear();
                    break;
                case CmdType.SET:
                    dict[keyDomain[cmd.arg]] = cmd.arg2;
                    break;
                case CmdType.TEST_SET:
                    ref int valueRef = ref dict.getValueRef(keyDomain[cmd.arg], true);
                    Assert.Equal(cmd.arg2, valueRef);
                    valueRef = cmd.arg3;
                    break;
                case CmdType.DEL:
                    Assert.True(dict.delete(keyDomain[cmd.arg]));
                    break;
            }
            return null;
        }

        private static void assertDictHasContents(
            ReferenceDictionary<Key, int> dict, Key[] keyDomain, IEnumerable<(int, int)> pairs, bool checkExceptions = true)
        {
            int count = (pairs == null) ? 0 : pairs.Count();
            Assert.Equal(count, dict.count);

            int?[] map = makeKeyValueMap(keyDomain.Length, pairs);

            for (int i = 0; i < keyDomain.Length; i++) {
                Key key = keyDomain[i];
                int? value = map[i];

                if (value.HasValue) {
                    Assert.Equal(value.Value, dict[key]);
                    Assert.Equal(value.Value, dict.getValueOrDefault(key));
                    Assert.Equal(value.Value, dict.getValueRef(key, false));
                    Assert.Equal(value.Value, dict.getValueRef(key, true));
                    Assert.True(dict.containsKey(key));

                    Assert.True(dict.tryGetValue(key, out int tgvResult));
                    Assert.Equal(value.Value, tgvResult);
                }
                else {
                    Assert.Equal(0, dict.getValueOrDefault(key));
                    Assert.False(dict.containsKey(key));

                    if (checkExceptions) {
                        Assert.Throws<ArgumentException>(() => dict[key]);
                        Assert.Throws<ArgumentException>(() => dict.getValueRef(key, false));
                    }

                    Assert.False(dict.tryGetValue(key, out int tgvResult));
                    Assert.Equal(0, tgvResult);

                    Assert.False(dict.delete(key));
                }
            }
        }

        private static IEnumerable<Command> genRandomCommands(
            int keyDomainSize, int commandCount, int initCapacity, int seed,
            int setRatio = 0, int testSetRatio = 0, int delRatio = 0, int clearRatio = 0)
        {
            return generator().ToArray();

            IEnumerable<Command> generator() {
                yield return new Command {type = CmdType.CREATE, arg = initCapacity};

                var random = new Random(seed);
                var keysNotInDict = Enumerable.Range(0, keyDomainSize).ToList();
                var keysInDict = new List<int>(keyDomainSize);
                var valuesInDict = new int[keyDomainSize];

                int commandsGenerated = 0;

                double totalRatio = (double)(setRatio + testSetRatio + delRatio + clearRatio);
                double pClear = (double)clearRatio / totalRatio;
                double pDel = pClear + (double)delRatio / totalRatio;
                double pSet = pDel + (double)setRatio / totalRatio;

                while (commandsGenerated < commandCount) {
                    double decision = random.NextDouble();
                    if (decision < pClear) {
                        if (keysInDict.Count == 0)
                            continue;

                        keysNotInDict.AddRange(keysInDict);
                        keysInDict.Clear();
                        commandsGenerated++;
                        yield return new Command {type = CmdType.CLEAR};
                    }
                    else if (decision < pDel) {
                        if (keysInDict.Count == 0)
                            continue;

                        int index = random.Next(keysInDict.Count);
                        int key = keysInDict[index];
                        keysInDict.RemoveAt(index);
                        keysNotInDict.Add(key);
                        commandsGenerated++;

                        yield return new Command {
                            type = CmdType.DEL, arg = key,
                            contents = keysInDict.Select(x => (x, valuesInDict[x])).ToArray()
                        };
                    }
                    else {
                        int key = random.Next(keyDomainSize);
                        int value = random.Next();
                        int oldValue = 0;

                        if (keysNotInDict.Remove(key))
                            keysInDict.Add(key);
                        else
                            oldValue = valuesInDict[key];

                        valuesInDict[key] = value;
                        commandsGenerated++;

                        if (decision < pSet) {
                            yield return new Command {
                                type = CmdType.SET, arg = key, arg2 = value,
                                contents = keysInDict.Select(x => (x, valuesInDict[x])).ToArray()
                            };
                        }
                        else {
                            yield return new Command {
                                type = CmdType.TEST_SET, arg = key, arg2 = oldValue, arg3 = value,
                                contents = keysInDict.Select(x => (x, valuesInDict[x])).ToArray()
                            };
                        }
                    }
                }
            }
        }

        [Fact]
        public void constructorTest() {
            ReferenceDictionary<Key, int> dict;

            dict = new ReferenceDictionary<Key, int>();
            Assert.Equal(0, dict.count);

            dict = new ReferenceDictionary<Key, int>(100);
            Assert.Equal(0, dict.count);

            Assert.Throws<ArgumentOutOfRangeException>(() => new ReferenceDictionary<Key, int>(-1));
        }

        public static IEnumerable<object[]> getSetTest_data() {
            Command[] testCase1 = {
                new Command {type = CmdType.CREATE, arg = 0},

                new Command {
                    type = CmdType.SET, arg = 0, arg2 = 10,
                    contents = new[] {(0, 10)}
                },
                new Command {
                    type = CmdType.SET, arg = 2, arg2 = 14,
                    contents = new[] {(0, 10), (2, 14)}
                },
                new Command {
                    type = CmdType.SET, arg = 3, arg2 = 0,
                    contents = new[] {(0, 10), (2, 14), (3, 0)}
                },
                new Command {
                    type = CmdType.SET, arg = 7, arg2 = 10,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10)}
                },
                new Command {
                    type = CmdType.SET, arg = 4, arg2 = 16,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16)}
                },
                new Command {
                    type = CmdType.SET, arg = 8, arg2 = 35,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35)}
                },
                new Command {
                    type = CmdType.SET, arg = 15, arg2 = 451,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35), (15, 451)}
                },
                new Command {
                    type = CmdType.SET, arg = 19, arg2 = 331,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35), (15, 451), (19, 331)}
                },
                new Command {
                    type = CmdType.SET, arg = 12, arg2 = 6,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35), (15, 451), (19, 331), (12, 6)}
                },
                new Command {
                    type = CmdType.SET, arg = 14, arg2 = 0,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35), (15, 451), (19, 331), (12, 6), (14, 0)}
                },
                new Command {
                    type = CmdType.SET, arg = 21, arg2 = 5,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35), (15, 451), (19, 331), (12, 6), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.SET, arg = 4, arg2 = 27,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 27), (8, 35), (15, 451), (19, 331), (12, 6), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.SET, arg = 4, arg2 = 27,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 27), (8, 35), (15, 451), (19, 331), (12, 6), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 15, arg2 = 451, arg3 = 90,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 27), (8, 35), (15, 90), (19, 331), (12, 6), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 15, arg2 = 90, arg3 = 100,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 27), (8, 35), (15, 100), (19, 331), (12, 6), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 3, arg2 = 0, arg3 = 5,
                    contents = new[] {(0, 10), (2, 14), (3, 5), (7, 10), (4, 27), (8, 35), (15, 100), (19, 331), (12, 6), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 3, arg2 = 5, arg3 = 5,
                    contents = new[] {(0, 10), (2, 14), (3, 5), (7, 10), (4, 27), (8, 35), (15, 100), (19, 331), (12, 6), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 3, arg2 = 5, arg3 = 0,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 27), (8, 35), (15, 100), (19, 331), (12, 6), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 25, arg2 = 0, arg3 = 99,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 27), (8, 35), (15, 100), (19, 331), (12, 6), (14, 0), (21, 5), (25, 99)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 26, arg2 = 0, arg3 = 98,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 27), (8, 35), (15, 100), (19, 331), (12, 6), (14, 0), (21, 5), (25, 99), (26, 98)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 26, arg2 = 98, arg3 = 1,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 27), (8, 35), (15, 100), (19, 331), (12, 6), (14, 0), (21, 5), (25, 99), (26, 1)}
                },
            };

            Command[] testCase2 = testCase1.AsSpan().ToArray();
            testCase2[0] = new Command {type = CmdType.CREATE, arg = 10};

            IEnumerable<Command> testCase3 = genRandomCommands(
                keyDomainSize: 1500, commandCount: 1500, initCapacity: 0, seed: 6758113, setRatio: 1, testSetRatio: 1);
            IEnumerable<Command> testCase4 = genRandomCommands(
                keyDomainSize: 1500, commandCount: 1500, initCapacity: 0, seed: 6758113, setRatio: 1, testSetRatio: 1);

            IEnumerable<Command> testCase5 = Enumerable.Empty<Command>()
                .Append(
                    new Command {type = CmdType.CREATE, arg = 300}
                )
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.SET, arg = i, arg2 = i * i, contents = Enumerable.Range(0, i + 1).Select(x => (x, x * x))
                }))
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.TEST_SET, arg = i, arg2 = i * i, arg3 = 5 * i * i,
                    contents = Enumerable.Range(0, 300).Select(x => (x, (x <= i) ? 5 * x * x : x * x))
                }));

            yield return new object[] {testCase1, true};
            yield return new object[] {testCase2, true};
            yield return new object[] {testCase3, false};
            yield return new object[] {testCase4, false};
            yield return new object[] {testCase5, false};
        }

        [Theory]
        [MemberData(nameof(getSetTest_data))]
        public void getSetTest(IEnumerable<Command> commands, bool checkExceptions) {
            Key[] keyDomain = makeKeys(getKeyDomainSize(commands));
            ReferenceDictionary<Key, int> dict = null;

            foreach (var cmd in commands) {
                runCommand(ref dict, keyDomain, cmd);
                assertDictHasContents(dict, keyDomain, cmd.contents, checkExceptions);
            }
        }

        public static IEnumerable<object[]> getSetDeleteTest_data() {
            Command[] testCase1 = {
                new Command {type = CmdType.CREATE, arg = 0},

                new Command {
                    type = CmdType.SET, arg = 0, arg2 = 10,
                    contents = new[] {(0, 10)}
                },
                new Command {
                    type = CmdType.SET, arg = 2, arg2 = 14,
                    contents = new[] {(0, 10), (2, 14)}
                },
                new Command {
                    type = CmdType.SET, arg = 3, arg2 = 0,
                    contents = new[] {(0, 10), (2, 14), (3, 0)}
                },
                new Command {
                    type = CmdType.SET, arg = 7, arg2 = 10,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10)}
                },
                new Command {
                    type = CmdType.SET, arg = 4, arg2 = 16,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16)}
                },
                new Command {
                    type = CmdType.SET, arg = 8, arg2 = 35,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35)}
                },
                new Command {
                    type = CmdType.SET, arg = 15, arg2 = 451,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35), (15, 451)}
                },
                new Command {
                    type = CmdType.SET, arg = 19, arg2 = 331,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35), (15, 451), (19, 331)}
                },
                new Command {
                    type = CmdType.SET, arg = 12, arg2 = 6,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35), (15, 451), (19, 331), (12, 6)}
                },
                new Command {
                    type = CmdType.SET, arg = 14, arg2 = 0,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35), (15, 451), (19, 331), (12, 6), (14, 0)}
                },
                new Command {
                    type = CmdType.SET, arg = 21, arg2 = 5,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35), (15, 451), (19, 331), (12, 6), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.SET, arg = 4, arg2 = 27,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 27), (8, 35), (15, 451), (19, 331), (12, 6), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.SET, arg = 4, arg2 = 27,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 27), (8, 35), (15, 451), (19, 331), (12, 6), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.DEL, arg = 3,
                    contents = new[] {(0, 10), (2, 14), (7, 10), (4, 27), (8, 35), (15, 451), (19, 331), (12, 6), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.DEL, arg = 8,
                    contents = new[] {(0, 10), (2, 14), (7, 10), (4, 27), (15, 451), (19, 331), (12, 6), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.DEL, arg = 12,
                    contents = new[] {(0, 10), (2, 14), (7, 10), (4, 27), (15, 451), (19, 331), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.SET, arg = 15, arg2 = 90,
                    contents = new[] {(0, 10), (2, 14), (7, 10), (4, 27), (15, 90), (19, 331), (14, 0), (21, 5)}
                },
                new Command {
                    type = CmdType.SET, arg = 23, arg2 = 109,
                    contents = new[] {(0, 10), (2, 14), (7, 10), (4, 27), (15, 90), (19, 331), (14, 0), (21, 5), (23, 109)}
                },
                new Command {
                    type = CmdType.SET, arg = 8, arg2 = 51,
                    contents = new[] {(0, 10), (2, 14), (7, 10), (4, 27), (15, 90), (19, 331), (14, 0), (21, 5), (23, 109), (8, 51)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 7, arg2 = 10, arg3 = 16,
                    contents = new[] {(0, 10), (2, 14), (7, 16), (4, 27), (15, 90), (19, 331), (14, 0), (21, 5), (23, 109), (8, 51)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 24, arg2 = 0, arg3 = 56,
                    contents = new[] {(0, 10), (2, 14), (7, 16), (4, 27), (15, 90), (19, 331), (14, 0), (21, 5), (23, 109), (8, 51), (24, 56)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 12, arg2 = 0, arg3 = 76,
                    contents = new[] {(0, 10), (2, 14), (7, 16), (4, 27), (15, 90), (19, 331), (14, 0), (21, 5), (23, 109), (8, 51), (24, 56), (12, 76)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 12, arg2 = 76, arg3 = 0,
                    contents = new[] {(0, 10), (2, 14), (7, 16), (4, 27), (15, 90), (19, 331), (14, 0), (21, 5), (23, 109), (8, 51), (24, 56), (12, 0)}
                },
                new Command {
                    type = CmdType.DEL, arg = 4,
                    contents = new[] {(0, 10), (2, 14), (7, 16), (15, 90), (19, 331), (14, 0), (21, 5), (23, 109), (8, 51), (24, 56), (12, 0)}
                },
                new Command {
                    type = CmdType.DEL, arg = 7,
                    contents = new[] {(0, 10), (2, 14), (15, 90), (19, 331), (14, 0), (21, 5), (23, 109), (8, 51), (24, 56), (12, 0)}
                },
                new Command {
                    type = CmdType.DEL, arg = 0,
                    contents = new[] {(2, 14), (15, 90), (19, 331), (14, 0), (21, 5), (23, 109), (8, 51), (24, 56), (12, 0)}
                },
                new Command {
                    type = CmdType.DEL, arg = 24,
                    contents = new[] {(2, 14), (15, 90), (19, 331), (14, 0), (21, 5), (23, 109), (8, 51), (12, 0)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 24, arg2 = 0, arg3 = 99,
                    contents = new[] {(2, 14), (15, 90), (19, 331), (14, 0), (21, 5), (23, 109), (8, 51), (24, 99), (12, 0)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 24, arg2 = 99, arg3 = 44,
                    contents = new[] {(2, 14), (15, 90), (19, 331), (14, 0), (21, 5), (23, 109), (8, 51), (24, 44), (12, 0)}
                },
            };

            Command[] testCase2 = testCase1.AsSpan().ToArray();
            testCase2[0] = new Command {type = CmdType.CREATE, arg = 10};

            IEnumerable<Command> testCase3 = genRandomCommands(
                keyDomainSize: 1000, commandCount: 2000, initCapacity: 0, seed: 6758113, setRatio: 1, testSetRatio: 1, delRatio: 1);
            IEnumerable<Command> testCase4 = genRandomCommands(
                keyDomainSize: 1000, commandCount: 2000, initCapacity: 1000, seed: 6758113, setRatio: 1, testSetRatio: 1, delRatio: 1);

            IEnumerable<Command> testCase5 = Enumerable.Empty<Command>()
                .Append(
                    new Command {type = CmdType.CREATE, arg = 300}
                )
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.SET, arg = i, arg2 = i * i, contents = Enumerable.Range(0, i + 1).Select(x => (x, x * x))
                }))
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.DEL, arg = i, contents = Enumerable.Range(i + 1, 299 - i).Select(x => (x, x * x))
                }))
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.TEST_SET, arg = 299 - i, arg2 = 0, arg3 = 5 * (299 - i) * (299 - i),
                    contents = Enumerable.Range(299 - i, i + 1).Select(x => (x, 5 * x * x))
                }))
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.DEL, arg = 299 - i,
                    contents = Enumerable.Range(0, 299 - i).Select(x => (x, 5 * x * x))
                }));

            yield return new object[] {testCase1, true};
            yield return new object[] {testCase2, true};
            yield return new object[] {testCase3, false};
            yield return new object[] {testCase4, false};
            yield return new object[] {testCase5, false};
        }

        [Theory]
        [MemberData(nameof(getSetDeleteTest_data))]
        public void getSetDeleteTest(IEnumerable<Command> commands, bool checkExceptions) {
            Key[] keyDomain = makeKeys(getKeyDomainSize(commands));
            ReferenceDictionary<Key, int> dict = null;

            foreach (var cmd in commands) {
                runCommand(ref dict, keyDomain, cmd);
                assertDictHasContents(dict, keyDomain, cmd.contents, checkExceptions);
            }
        }

        public static IEnumerable<object[]> getSetDeleteClearTest_data() {
            Command[] testCase1 = {
                new Command {type = CmdType.CREATE, arg = 0},

                new Command {
                    type = CmdType.CLEAR
                },
                new Command {
                    type = CmdType.SET, arg = 0, arg2 = 10,
                    contents = new[] {(0, 10)}
                },
                new Command {
                    type = CmdType.SET, arg = 2, arg2 = 14,
                    contents = new[] {(0, 10), (2, 14)}
                },
                new Command {
                    type = CmdType.SET, arg = 3, arg2 = 0,
                    contents = new[] {(0, 10), (2, 14), (3, 0)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 7, arg2 = 0, arg3 = 10,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 4, arg2 = 0, arg3 = 16,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16)}
                },
                new Command {
                    type = CmdType.SET, arg = 8, arg2 = 35,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35)}
                },
                new Command {
                    type = CmdType.SET, arg = 15, arg2 = 451,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35), (15, 451)}
                },
                new Command {
                    type = CmdType.SET, arg = 19, arg2 = 331,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35), (15, 451), (19, 331)}
                },
                new Command {
                    type = CmdType.SET, arg = 12, arg2 = 6,
                    contents = new[] {(0, 10), (2, 14), (3, 0), (7, 10), (4, 16), (8, 35), (15, 451), (19, 331), (12, 6)}
                },
                new Command {
                    type = CmdType.CLEAR
                },
                new Command {
                    type = CmdType.SET, arg = 21, arg2 = 5,
                    contents = new[] {(21, 5)}
                },
                new Command {
                    type = CmdType.DEL, arg = 21,
                },
                new Command {
                    type = CmdType.CLEAR
                },
                new Command {
                    type = CmdType.SET, arg = 21, arg2 = 5,
                    contents = new[] {(21, 5)}
                },
                new Command {
                    type = CmdType.SET, arg = 4, arg2 = 27,
                    contents = new[] {(4, 27), (21, 5)}
                },
                new Command {
                    type = CmdType.SET, arg = 4, arg2 = 27,
                    contents = new[] {(4, 27), (21, 5)}
                },
                new Command {
                    type = CmdType.SET, arg = 15, arg2 = 90,
                    contents = new[] {(4, 27), (21, 5), (15, 90)}
                },
                new Command {
                    type = CmdType.SET, arg = 23, arg2 = 109,
                    contents = new[] {(4, 27), (21, 5), (15, 90), (23, 109)}
                },
                new Command {
                    type = CmdType.SET, arg = 8, arg2 = 51,
                    contents = new[] {(4, 27), (21, 5), (15, 90), (23, 109), (8, 51)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 7, arg2 = 0, arg3 = 16,
                    contents = new[] {(4, 27), (21, 5), (15, 90), (23, 109), (8, 51), (7, 16)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 7, arg2 = 16, arg3 = 19,
                    contents = new[] {(4, 27), (21, 5), (15, 90), (23, 109), (8, 51), (7, 19)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 12, arg2 = 0, arg3 = 0,
                    contents = new[] {(4, 27), (21, 5), (15, 90), (23, 109), (8, 51), (7, 19), (12, 0)}
                },
                new Command {
                    type = CmdType.DEL, arg = 4,
                    contents = new[] {(21, 5), (15, 90), (23, 109), (8, 51), (7, 19), (12, 0)}
                },
                new Command {
                    type = CmdType.DEL, arg = 7,
                    contents = new[] {(21, 5), (15, 90), (23, 109), (8, 51), (12, 0)}
                },
                new Command {
                    type = CmdType.CLEAR
                },
                new Command {
                    type = CmdType.CLEAR
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 24, arg2 = 0, arg3 = 99,
                    contents = new[] {(24, 99)}
                },
                new Command {
                    type = CmdType.TEST_SET, arg = 24, arg2 = 99, arg3 = 44,
                    contents = new[] {(24, 44)}
                },
            };

            Command[] testCase2 = testCase1.AsSpan().ToArray();
            testCase2[0] = new Command {type = CmdType.CREATE, arg = 10};

            IEnumerable<Command> testCase3 = genRandomCommands(
                keyDomainSize: 1000, commandCount: 3000, initCapacity: 0, seed: 98170012,
                setRatio: 80, testSetRatio: 80, delRatio: 100, clearRatio: 1
            );
            IEnumerable<Command> testCase4 = genRandomCommands(
                keyDomainSize: 1000, commandCount: 3000, initCapacity: 1000, seed: 98170012,
                setRatio: 80, testSetRatio: 80, delRatio: 100, clearRatio: 1
            );

            IEnumerable<Command> testCase5 = Enumerable.Empty<Command>()
                .Append(
                    new Command {type = CmdType.CREATE, arg = 300}
                )
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.SET, arg = i, arg2 = i * i, contents = Enumerable.Range(0, i + 1).Select(x => (x, x * x))
                }))
                .Append(
                    new Command {type = CmdType.CLEAR}
                )
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.SET, arg = i, arg2 = i * i, contents = Enumerable.Range(0, i + 1).Select(x => (x, x * x))
                }))
                .Concat(Enumerable.Range(0, 150).Select(i => new Command {
                    type = CmdType.DEL, arg = 299 - i,
                    contents = Enumerable.Range(0, 299 - i).Select(x => (x, x * x))
                }))
                .Append(
                    new Command {type = CmdType.CLEAR}
                )
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.TEST_SET, arg = 299 - i, arg2 = 0, arg3 = 5 * (299 - i) * (299 - i),
                    contents = Enumerable.Range(299 - i, i + 1).Select(x => (x, 5 * x * x))
                }))
                .Concat(Enumerable.Range(0, 300).Select(i => new Command {
                    type = CmdType.DEL, arg = 299 - i,
                    contents = Enumerable.Range(0, 299 - i).Select(x => (x, 5 * x * x))
                }))
                .Append(
                    new Command {type = CmdType.CLEAR}
                );

            yield return new object[] {testCase1, true};
            yield return new object[] {testCase2, true};
            yield return new object[] {testCase3, false};
            yield return new object[] {testCase4, false};
            yield return new object[] {testCase5, false};
        }

        [Theory]
        [MemberData(nameof(getSetDeleteClearTest_data))]
        public void getSetDeleteClearTest(IEnumerable<Command> commands, bool checkExceptions) {
            Key[] keyDomain = makeKeys(getKeyDomainSize(commands));
            ReferenceDictionary<Key, int> dict = null;

            foreach (var cmd in commands) {
                runCommand(ref dict, keyDomain, cmd);
                assertDictHasContents(dict, keyDomain, cmd.contents, checkExceptions);
            }
        }

        public static IEnumerable<object[]> enumeratorTest_data = new object[][] {
            new object[] {
                genRandomCommands(
                    keyDomainSize: 30, commandCount: 60, initCapacity: 0, seed: 87211499,
                    setRatio: 1, testSetRatio: 1, delRatio: 1
                )
            },
            new object[] {
                genRandomCommands(
                    keyDomainSize: 600, commandCount: 1200, initCapacity: 300, seed: 87211499,
                    clearRatio: 1, setRatio: 70, testSetRatio: 60, delRatio: 100
                )
            },
        };

        [Theory]
        [MemberData(nameof(enumeratorTest_data))]
        public void enumeratorTest(IEnumerable<Command> commands) {
            Key[] keyDomain = makeKeys(getKeyDomainSize(commands));
            ReferenceDictionary<Key, int> dict = null;

            foreach (var cmd in commands) {
                runCommand(ref dict, keyDomain, cmd);

                var enumerator = dict.GetEnumerator();
                var enumerator2 = ((IEnumerable<KeyValuePair<Key, int>>)dict).GetEnumerator();
                var enumerator3 = ((IEnumerable)dict).GetEnumerator();

                var enumResult1 = new List<KeyValuePair<Key, int>>();
                while (enumerator.MoveNext())
                    enumResult1.Add(enumerator.Current);

                var enumResult2 = new List<KeyValuePair<Key, int>>();
                while (enumerator2.MoveNext())
                    enumResult2.Add(enumerator2.Current);

                var enumResult3 = new List<KeyValuePair<Key, int>>();
                while (enumerator3.MoveNext())
                    enumResult3.Add(Assert.IsType<KeyValuePair<Key, int>>(enumerator3.Current));

                int?[] kvMap = makeKeyValueMap(keyDomain.Length, cmd.contents);

                int? findValue(IEnumerable<KeyValuePair<Key, int>> pairs, Key key) =>
                    pairs.Where(x => x.Key == key).Select(x => (int?)x.Value).SingleOrDefault();

                for (int i = 0; i < keyDomain.Length; i++) {
                    Assert.Equal(kvMap[i], findValue(enumResult1, keyDomain[i]));
                    Assert.Equal(kvMap[i], findValue(enumResult1, keyDomain[i]));
                    Assert.Equal(kvMap[i], findValue(enumResult1, keyDomain[i]));
                }

                Assert.Throws<NotImplementedException>(() => enumerator2.Reset());
                Assert.Throws<NotImplementedException>(() => enumerator3.Reset());

                enumerator.Dispose();
                enumerator2.Dispose();
            }
        }

        [Fact]
        public void getSetDeleteTest_nullKeys() {
            var dict = new ReferenceDictionary<Key, int>();

            Assert.Throws<ArgumentNullException>(() => dict[null]);
            Assert.Throws<ArgumentNullException>(() => dict.getValueRef(null, false));
            Assert.Throws<ArgumentNullException>(() => dict.getValueRef(null, true));
            Assert.Throws<ArgumentNullException>(() => dict.getValueOrDefault(null));
            Assert.Throws<ArgumentNullException>(() => dict.tryGetValue(null, out _));
            Assert.Throws<ArgumentNullException>(() => dict.containsKey(null));
            Assert.Throws<ArgumentNullException>(() => dict.delete(null));
        }

    }

}
