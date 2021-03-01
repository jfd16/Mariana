using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace Mariana.Common.Tests {

    public class DynamicArrayPoolTest {

        private struct StructWithRef : IEquatable<StructWithRef> {
            public int p;
            public string q;

            public bool Equals(StructWithRef other) => p == other.p && (object)q == other.q;
        }

        public enum CmdType {
            ALLOC,
            ALLOC_WRITE,
            WRITE,
            RESIZE,
            RESIZE_WRITE,
            APPEND,
            FREE,
            CLEAR,
        }

        public struct Command {
            public CmdType type;
            public int token;
            public object arg;
        }

        private class RefEqualityComparer : IEqualityComparer<object> {
            public static RefEqualityComparer instance = new RefEqualityComparer();
            public new bool Equals(object x, object y) => x == y;
            public int GetHashCode(object x) => RuntimeHelpers.GetHashCode(x);
        }

        private static IEqualityComparer<T> getEqualityComparer<T>() =>
            typeof(T).IsValueType ? EqualityComparer<T>.Default : (IEqualityComparer<T>)RefEqualityComparer.instance;

        private void runCommandsWithChecks<T>(DynamicArrayPool<T> pool, IEnumerable<Command> commands) {
            if (!commands.Any())
                return;

            int numTokens = commands.Max(x => x.token) + 1;
            var tokens = new DynamicArrayPoolToken<T>[numTokens];
            var contents = new List<T>[numTokens];

            foreach (Command cmd in commands) {
                runCommand(pool, cmd, tokens, contents);

                int elementCount = 0;
                IEqualityComparer<T> comparer = getEqualityComparer<T>();

                for (int i = 0; i < tokens.Length; i++) {
                    if (tokens[i].isDefault)
                        continue;

                    Span<T> span = pool.getSpan(tokens[i]);
                    Assert.Equal(span.Length, pool.getLength(tokens[i]));
                    elementCount += span.Length;

                    if (contents[i] != null)
                        Assert.Equal<T>(contents[i], span.ToArray(), comparer);
                }
            }
        }

        private void runCommand<T>(
            DynamicArrayPool<T> pool, Command cmd, DynamicArrayPoolToken<T>[] poolTokens, List<T>[] poolContents, bool doChecks = true)
        {
            ref DynamicArrayPoolToken<T> token = ref poolTokens[cmd.token];
            ref List<T> contents = ref poolContents[cmd.token];

            switch (cmd.type) {
                case CmdType.CLEAR:
                    pool.clear();
                    poolTokens.AsSpan().Clear();
                    poolContents.AsSpan().Clear();
                    break;

                case CmdType.ALLOC: {
                    Assert.True(token.isDefault);

                    int length = (int)cmd.arg;
                    token = pool.allocate(length);

                    if (doChecks)
                        Assert.Equal(length, pool.getSpan(token).Length);

                    contents = RuntimeHelpers.IsReferenceOrContainsReferences<T>()
                        ? Enumerable.Repeat<T>(default, length).ToList()
                        : null;

                    break;
                }

                case CmdType.ALLOC_WRITE: {
                    Assert.True(token.isDefault);

                    var writeData = (IEnumerable<T>)cmd.arg;
                    int length = writeData.Count();
                    token = pool.allocate(length, out Span<T> span);

                    if (doChecks) {
                        Assert.True(span == pool.getSpan(token));
                        Assert.Equal(length, span.Length);

                        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                            Assert.Equal<T>(Enumerable.Repeat<T>(default, length), span.ToArray(), getEqualityComparer<T>());
                    }

                    contents = new List<T>();
                    int curIndex = 0;

                    foreach (T item in writeData) {
                        span[curIndex++] = item;
                        contents.Add(item);
                    }
                    break;
                }

                case CmdType.WRITE: {
                    Assert.False(token.isDefault);
                    contents = contents ?? new List<T>();
                    contents.Clear();
                    contents.AddRange((IEnumerable<T>)cmd.arg);

                    var span = pool.getSpan(token);
                    for (int i = 0; i < contents.Count; i++)
                        span[i] = contents[i];

                    break;
                }

                case CmdType.FREE: {
                    Assert.False(token.isDefault);
                    var oldSpan = pool.getSpan(token);

                    pool.free(token);
                    token = default;

                    if (doChecks && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                        Assert.Equal<T>(Enumerable.Repeat<T>(default, oldSpan.Length), oldSpan.ToArray(), getEqualityComparer<T>());

                    contents = null;
                    break;
                }

                case CmdType.RESIZE: {
                    int oldLength = pool.getLength(token);
                    int newLength = (int)cmd.arg;

                    Span<T> newSpan;
                    if (doChecks) {
                        newSpan = resizeWithChecks(token, newLength, false);
                    }
                    else {
                        pool.resize(token, newLength);
                        newSpan = pool.getSpan(token);
                    }

                    if (contents != null) {
                        if (newLength > oldLength) {
                            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
                                // Clearing here so that the contents check passes.
                                newSpan.Slice(oldLength).Clear();
                            }

                            for (int i = oldLength; i < newLength; i++)
                                contents.Add(default);
                        }
                        else if (newLength < oldLength) {
                            contents.RemoveRange(newLength, oldLength - newLength);
                        }
                    }
                    break;
                }

                case CmdType.RESIZE_WRITE: {
                    var writeData = (IEnumerable<T>)cmd.arg;
                    int length = writeData.Count();

                    Span<T> newSpan;
                    if (doChecks)
                        newSpan = resizeWithChecks(token, length, true);
                    else
                        pool.resize(token, length, out newSpan);

                    int curIndex = 0;

                    contents = contents ?? new List<T>();
                    contents.Clear();

                    foreach (T item in writeData) {
                        newSpan[curIndex++] = item;
                        contents.Add(item);
                    }
                    break;
                }

                case CmdType.APPEND: {
                    T item = (T)cmd.arg;

                    Span<T> oldSpan = pool.getSpan(token);
                    T[] oldSpanCopy = (doChecks || contents == null) ? oldSpan.ToArray() : null;

                    pool.append(token, item);
                    Span<T> newSpan = pool.getSpan(token);

                    if (doChecks) {
                        Assert.Equal(oldSpan.Length + 1, newSpan.Length);
                        Assert.Equal<T>(oldSpanCopy.Append(item), newSpan.ToArray(), getEqualityComparer<T>());

                        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() && !oldSpan.Overlaps(newSpan))
                            Assert.Equal<T>(Enumerable.Repeat<T>(default, oldSpan.Length), oldSpan.ToArray(), getEqualityComparer<T>());
                    }

                    contents = contents ?? new List<T>(oldSpanCopy);
                    contents.Add(item);
                    break;
                }
            }

            Span<T> resizeWithChecks(DynamicArrayPoolToken<T> _token, int newLength, bool useMethodWithOutParam) {
                Span<T> oldSpan = pool.getSpan(_token);
                T[] oldSpanCopy = oldSpan.ToArray();
                Span<T> newSpan;

                if (useMethodWithOutParam) {
                    pool.resize(_token, newLength, out newSpan);
                    Assert.True(newSpan == pool.getSpan(_token));
                }
                else {
                    pool.resize(_token, newLength);
                    newSpan = pool.getSpan(_token);
                }

                Assert.Equal(newLength, newSpan.Length);

                if (newLength == oldSpan.Length)
                    Assert.True(newSpan == oldSpan);

                if (newLength < oldSpan.Length)
                    Assert.Equal<T>(newSpan.ToArray(), oldSpanCopy.Take(newLength), getEqualityComparer<T>());
                else
                    Assert.Equal<T>(oldSpanCopy, newSpan.Slice(0, oldSpan.Length).ToArray(), getEqualityComparer<T>());

                if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
                    Span<T> zeroedPart;

                    if (!newSpan.Overlaps(oldSpan))
                        zeroedPart = oldSpan;
                    else if (newLength < oldSpan.Length)
                        zeroedPart = oldSpan.Slice(newLength);
                    else
                        zeroedPart = newSpan.Slice(oldSpan.Length);

                    for (int i = 0; i < zeroedPart.Length; i++)
                        Assert.Equal(default(T), zeroedPart[i], getEqualityComparer<T>());
                }

                return newSpan;
            }
        }

        private static IEnumerable<Command> genRandomTestCase(
            int numTokens,
            int maxLength,
            int numCommands,
            int seed,
            int allocRatio = 0,
            int writeRatio = 0,
            int freeRatio = 0,
            int resizeRatio = 0,
            int appendRatio = 0,
            int clearRatio = 0
        ) {
            return generator().ToArray();

            IEnumerable<Command> generator() {
                var random = new Random(seed);

                var availableTokens = Enumerable.Range(0, numTokens).ToList();
                var usedTokensAndLengths = new List<(int, int)>();

                double totalRatio = allocRatio + writeRatio + freeRatio + resizeRatio + appendRatio + clearRatio;
                double pAlloc = (double)allocRatio / totalRatio;
                double pWrite = pAlloc + (double)writeRatio / totalRatio;
                double pFree = pWrite + (double)freeRatio / totalRatio;
                double pResize = pFree + (double)resizeRatio / totalRatio;
                double pAppend = pResize + (double)appendRatio / totalRatio;

                int commandsGenerated = 0;
                while (commandsGenerated < numCommands) {
                    double choice = random.NextDouble();

                    if (choice < pAlloc) {
                        if (availableTokens.Count == 0)
                            continue;

                        int tokenIndex = random.Next(availableTokens.Count);
                        int length = random.Next(maxLength + 1);

                        if (random.Next(2) == 1) {
                            yield return new Command {
                                type = CmdType.ALLOC_WRITE,
                                token = availableTokens[tokenIndex],
                                arg = Enumerable.Range(random.Next(100000000), length)
                            };
                        }
                        else {
                            yield return new Command {type = CmdType.ALLOC, token = availableTokens[tokenIndex], arg = length};
                        }

                        usedTokensAndLengths.Add((availableTokens[tokenIndex], length));
                        availableTokens.RemoveAt(tokenIndex);
                        commandsGenerated++;
                    }
                    else if (choice < pWrite) {
                        if (usedTokensAndLengths.Count == 0)
                            continue;

                        int tokenIndex = random.Next(usedTokensAndLengths.Count);
                        yield return new Command {
                            type = CmdType.WRITE,
                            token = usedTokensAndLengths[tokenIndex].Item1,
                            arg = Enumerable.Range(random.Next(1000000000), usedTokensAndLengths[tokenIndex].Item2)
                        };
                        commandsGenerated++;
                    }
                    else if (choice < pFree) {
                        if (usedTokensAndLengths.Count == 0)
                            continue;

                        int tokenIndex = random.Next(usedTokensAndLengths.Count);
                        yield return new Command {type = CmdType.FREE, token = usedTokensAndLengths[tokenIndex].Item1};

                        availableTokens.Add(usedTokensAndLengths[tokenIndex].Item1);
                        usedTokensAndLengths.RemoveAt(tokenIndex);
                        commandsGenerated++;
                    }
                    else if (choice < pResize) {
                        if (usedTokensAndLengths.Count == 0)
                            continue;

                        int tokenIndex = random.Next(usedTokensAndLengths.Count);
                        var token = usedTokensAndLengths[tokenIndex].Item1;
                        int newLength = random.Next(maxLength + 1);

                        if (random.Next(2) == 1) {
                            yield return new Command {
                                type = CmdType.RESIZE_WRITE,
                                token = token,
                                arg = Enumerable.Range(random.Next(100000000), newLength)
                            };
                        }
                        else {
                            yield return new Command {type = CmdType.RESIZE, token = token, arg = newLength};
                        }

                        usedTokensAndLengths[tokenIndex] = (token, newLength);
                        commandsGenerated++;
                    }
                    else if (choice < pAppend) {
                        if (usedTokensAndLengths.Count == 0)
                            continue;

                        int tokenIndex = random.Next(usedTokensAndLengths.Count);
                        var (token, length) = usedTokensAndLengths[tokenIndex];

                        yield return new Command {type = CmdType.APPEND, token = token, arg = random.Next(1000000000)};

                        usedTokensAndLengths[tokenIndex] = (token, length + 1);
                        commandsGenerated++;
                    }
                    else {
                        yield return new Command {type = CmdType.CLEAR};

                        for (int i = 0; i < usedTokensAndLengths.Count; i++)
                            availableTokens.Add(usedTokensAndLengths[i].Item1);

                        usedTokensAndLengths.Clear();
                        commandsGenerated++;
                    }
                }
            }
        }

        [Fact]
        public void defaultTokenValueTest() {
            Assert.True(default(DynamicArrayPoolToken<int>).isDefault);

            var pool = new DynamicArrayPool<int>();
            Assert.Equal(0, pool.getSpan(default).Length);
            Assert.Equal(0, pool.getLength(default));

            Assert.Throws<ArgumentException>(() => pool.free(default));
            Assert.Throws<ArgumentException>(() => pool.resize(default, 1));
            Assert.Throws<ArgumentException>(() => pool.resize(default, 1, out _));
            Assert.Throws<ArgumentException>(() => pool.append(default, 1));
        }

        public static IEnumerable<object[]> allocateAndWriteTest_valueType_data() {
            var testCase1 = new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.ALLOC, token = 1, arg = 1},
                new Command {type = CmdType.ALLOC, token = 2, arg = 2},
                new Command {type = CmdType.ALLOC, token = 3, arg = 4},
                new Command {type = CmdType.ALLOC, token = 4, arg = 8},
                new Command {type = CmdType.ALLOC, token = 5, arg = 12},
                new Command {type = CmdType.ALLOC, token = 6, arg = 16},
                new Command {type = CmdType.ALLOC, token = 7, arg = 32},
                new Command {type = CmdType.ALLOC, token = 8, arg = 127},
                new Command {type = CmdType.ALLOC, token = 9, arg = 318},

                new Command {type = CmdType.WRITE, token = 1, arg = new[] {1000}},
                new Command {type = CmdType.WRITE, token = 2, arg = new[] {100, 200}},
                new Command {type = CmdType.WRITE, token = 3, arg = new[] {100, 200, 300, 400}},
                new Command {type = CmdType.WRITE, token = 4, arg = new[] {100, 200, 300, 400, 500, 600, 700, 800}},
                new Command {type = CmdType.WRITE, token = 5, arg = Enumerable.Range(1847, 12)},
                new Command {type = CmdType.WRITE, token = 6, arg = Enumerable.Range(65139, 16)},
                new Command {type = CmdType.WRITE, token = 7, arg = Enumerable.Range(981772, 32)},
                new Command {type = CmdType.WRITE, token = 8, arg = Enumerable.Range(7633, 127)},
                new Command {type = CmdType.WRITE, token = 9, arg = Enumerable.Range(4651130, 318)},
            };

            var testCase2 = new Command[] {
                new Command {type = CmdType.ALLOC_WRITE, token = 0, arg = Enumerable.Empty<int>()},
                new Command {type = CmdType.ALLOC_WRITE, token = 1, arg = new[] {1000}},
                new Command {type = CmdType.ALLOC_WRITE, token = 2, arg = new[] {100, 200}},
                new Command {type = CmdType.ALLOC_WRITE, token = 3, arg = new[] {100, 200, 300, 400}},
                new Command {type = CmdType.ALLOC_WRITE, token = 4, arg = new[] {100, 200, 300, 400, 500, 600, 700, 800}},
                new Command {type = CmdType.ALLOC_WRITE, token = 5, arg = Enumerable.Range(1847, 12)},
                new Command {type = CmdType.ALLOC_WRITE, token = 6, arg = Enumerable.Range(65139, 16)},
                new Command {type = CmdType.ALLOC_WRITE, token = 7, arg = Enumerable.Range(981772, 32)},
                new Command {type = CmdType.ALLOC_WRITE, token = 8, arg = Enumerable.Range(7633, 127)},
                new Command {type = CmdType.ALLOC_WRITE, token = 9, arg = Enumerable.Range(4651130, 318)},
            };

            var testCase3 = new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 1 << 21},
                new Command {type = CmdType.ALLOC, token = 1, arg = 1 << 21},
                new Command {type = CmdType.ALLOC, token = 2, arg = 1 << 21},

                new Command {type = CmdType.WRITE, token = 1, arg = Enumerable.Range(0, 1 << 21)},
                new Command {type = CmdType.ALLOC_WRITE, token = 3, arg = Enumerable.Range(1 << 21, 1 << 21)},
            };

            yield return new object[] {testCase1};
            yield return new object[] {testCase2};
            yield return new object[] {testCase3};

            yield return new object[] {
                genRandomTestCase(
                    numTokens: 400, numCommands: 800, maxLength: 512, seed: 194855113,
                    allocRatio: 1, writeRatio: 1
                )
            };
        }

        [Theory]
        [MemberData(nameof(allocateAndWriteTest_valueType_data))]
        public void allocateAndWriteTest_valueType(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<int>();
            runCommandsWithChecks(pool, commands);
        }

        public static IEnumerable<object[]> allocateAndWriteTest_refType_data() {
            var testCase1 = new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.ALLOC, token = 1, arg = 1},
                new Command {type = CmdType.ALLOC, token = 2, arg = 2},
                new Command {type = CmdType.ALLOC, token = 3, arg = 8},
                new Command {type = CmdType.ALLOC, token = 4, arg = 32},
                new Command {type = CmdType.ALLOC, token = 5, arg = 318},

                new Command {type = CmdType.WRITE, token = 1, arg = new[] {"A"}},
                new Command {type = CmdType.WRITE, token = 2, arg = new[] {"A", "B"}},
                new Command {type = CmdType.WRITE, token = 3, arg = new[] {"A", "B", "C", "D", "E", "F", "G", "H"}},
                new Command {type = CmdType.WRITE, token = 4, arg = Enumerable.Range(0, 32).Select(x => new string('a', x))},
                new Command {type = CmdType.WRITE, token = 5, arg = Enumerable.Range(0, 318).Select(x => new string((char)(x + 32), 3))},
            };

            var testCase2 = new Command[] {
                new Command {type = CmdType.ALLOC_WRITE, token = 0, arg = Enumerable.Empty<string>()},
                new Command {type = CmdType.ALLOC_WRITE, token = 1, arg = new[] {"abc", "xyz"}},
                new Command {type = CmdType.ALLOC_WRITE, token = 2, arg = Enumerable.Range(0, 32).Select(x => new string('a', x))},
                new Command {type = CmdType.ALLOC_WRITE, token = 3, arg = Enumerable.Range(0, 279).Select(x => new string((char)(x + 32), 3))},
            };

            yield return new object[] {testCase1};
            yield return new object[] {testCase2};
        }

        [Theory]
        [MemberData(nameof(allocateAndWriteTest_refType_data))]
        public void allocateAndWriteTest_refType(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<string>();
            runCommandsWithChecks(pool, commands);
        }

        public static IEnumerable<object[]> allocateAndWriteTest_valueTypeWithRefs_data() {
            var testCase1 = new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.ALLOC, token = 1, arg = 2},
                new Command {type = CmdType.ALLOC, token = 2, arg = 32},
                new Command {type = CmdType.ALLOC, token = 3, arg = 318},

                new Command {type = CmdType.WRITE, token = 1, arg = new[] {new StructWithRef {p = 1, q = "abc"}, new StructWithRef {p = 444, q = "pq"}}},
                new Command {type = CmdType.WRITE, token = 2, arg = Enumerable.Range(0, 32).Select(x => new StructWithRef {p = x, q = new string('a', x)})},
                new Command {type = CmdType.WRITE, token = 3, arg = Enumerable.Range(0, 318).Select(x => new StructWithRef {p = x, q = new string('a', 1)})},
            };

            var testCase2 = new Command[] {
                new Command {type = CmdType.ALLOC_WRITE, token = 0, arg = new[] {new StructWithRef {p = 1, q = "abc"}, new StructWithRef {p = 444, q = "pq"}}},
                new Command {type = CmdType.ALLOC_WRITE, token = 1, arg = Enumerable.Range(0, 32).Select(x => new StructWithRef {p = x, q = new string('a', x)})},
                new Command {type = CmdType.ALLOC_WRITE, token = 2, arg = Enumerable.Range(0, 279).Select(x => new StructWithRef {p = x, q = new string('a', 1)})},
            };

            yield return new object[] {testCase1};
            yield return new object[] {testCase2};
        }

        [Theory]
        [MemberData(nameof(allocateAndWriteTest_valueTypeWithRefs_data))]
        public void allocateAndWriteTest_valueTypeWithRefs(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<StructWithRef>();
            runCommandsWithChecks(pool, commands);
        }

        public static IEnumerable<object[]> allocateAndFreeTest_valueType_data() {
            var testCase1 = new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.ALLOC, token = 1, arg = 1},
                new Command {type = CmdType.ALLOC, token = 2, arg = 4},
                new Command {type = CmdType.ALLOC, token = 3, arg = 28},
                new Command {type = CmdType.ALLOC, token = 4, arg = 32},
                new Command {type = CmdType.ALLOC, token = 5, arg = 269},
                new Command {type = CmdType.ALLOC, token = 6, arg = 318},

                new Command {type = CmdType.WRITE, token = 1, arg = new[] {1000}},
                new Command {type = CmdType.WRITE, token = 2, arg = new[] {100, 200, 300, 400}},
                new Command {type = CmdType.WRITE, token = 3, arg = Enumerable.Range(6712, 28)},
                new Command {type = CmdType.WRITE, token = 4, arg = Enumerable.Range(981772, 32)},
                new Command {type = CmdType.WRITE, token = 5, arg = Enumerable.Range(7633, 269)},
                new Command {type = CmdType.WRITE, token = 6, arg = Enumerable.Range(4651130, 318)},

                new Command {type = CmdType.FREE, token = 6},
                new Command {type = CmdType.FREE, token = 5},
                new Command {type = CmdType.FREE, token = 4},
                new Command {type = CmdType.FREE, token = 3},
                new Command {type = CmdType.FREE, token = 2},
                new Command {type = CmdType.FREE, token = 1},
                new Command {type = CmdType.FREE, token = 0},

                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.ALLOC, token = 1, arg = 8},
                new Command {type = CmdType.ALLOC_WRITE, token = 2, arg = Enumerable.Range(0, 36)},
                new Command {type = CmdType.ALLOC_WRITE, token = 3, arg = Enumerable.Range(0, 46)},
                new Command {type = CmdType.ALLOC_WRITE, token = 4, arg = Enumerable.Range(0, 288)},
                new Command {type = CmdType.ALLOC_WRITE, token = 5, arg = Enumerable.Range(0, 318)},

                new Command {type = CmdType.FREE, token = 0},
                new Command {type = CmdType.FREE, token = 1},
                new Command {type = CmdType.FREE, token = 2},
                new Command {type = CmdType.FREE, token = 3},
                new Command {type = CmdType.FREE, token = 4},
                new Command {type = CmdType.FREE, token = 5},

                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.ALLOC, token = 1, arg = 8},
                new Command {type = CmdType.ALLOC_WRITE, token = 2, arg = Enumerable.Range(1000, 68)},
                new Command {type = CmdType.ALLOC_WRITE, token = 3, arg = Enumerable.Range(1000, 127)},
                new Command {type = CmdType.ALLOC_WRITE, token = 4, arg = Enumerable.Range(1000, 257)},
                new Command {type = CmdType.ALLOC_WRITE, token = 5, arg = Enumerable.Range(1000, 318)},
                new Command {type = CmdType.WRITE, token = 1, arg = Enumerable.Range(2000, 8)},
                new Command {type = CmdType.WRITE, token = 4, arg = Enumerable.Range(2000, 257)},
                new Command {type = CmdType.ALLOC, token = 6, arg = 444},
                new Command {type = CmdType.WRITE, token = 6, arg = Enumerable.Range(2000, 444)},

                new Command {type = CmdType.FREE, token = 5},
                new Command {type = CmdType.FREE, token = 1},
                new Command {type = CmdType.FREE, token = 3},
                new Command {type = CmdType.FREE, token = 0},
                new Command {type = CmdType.FREE, token = 4},
                new Command {type = CmdType.FREE, token = 2},

                new Command {type = CmdType.ALLOC_WRITE, token = 2, arg = Enumerable.Range(5000, 50)},
                new Command {type = CmdType.ALLOC_WRITE, token = 4, arg = Enumerable.Range(6000, 300)},

                new Command {type = CmdType.FREE, token = 6},
                new Command {type = CmdType.FREE, token = 2},
            };

            var testCase2 = Enumerable.Range(0, 1 << 10).SelectMany(i => new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 1 << 21},
                new Command {type = CmdType.ALLOC, token = 1, arg = 1 << 21},
                new Command {type = CmdType.FREE, token = i & 1},
                new Command {type = CmdType.FREE, token = 1 - (i & 1)},
            });

            yield return new object[] {testCase1};
            yield return new object[] {testCase2};

            yield return new object[] {
                genRandomTestCase(
                    numTokens: 200, numCommands: 1000, maxLength: 512, seed: 230087761,
                    allocRatio: 3, writeRatio: 1, freeRatio: 2
                )
            };
        }

        [Theory]
        [MemberData(nameof(allocateAndFreeTest_valueType_data))]
        public void allocateAndFreeTest_valueType(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<int>();
            runCommandsWithChecks(pool, commands);
        }

        public static IEnumerable<object[]> allocateAndFreeTest_refType_data() {
            var testCase1 = new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.ALLOC, token = 1, arg = 2},
                new Command {type = CmdType.ALLOC, token = 2, arg = 32},
                new Command {type = CmdType.ALLOC, token = 3, arg = 144},
                new Command {type = CmdType.ALLOC, token = 4, arg = 189},

                new Command {type = CmdType.WRITE, token = 1, arg = new[] {"A", "B"}},
                new Command {type = CmdType.WRITE, token = 2, arg = Enumerable.Range(0, 32).Select(x => new string('a', x))},
                new Command {type = CmdType.WRITE, token = 3, arg = Enumerable.Range(0, 144).Select(x => new string((char)(x + 32), 3))},
                new Command {type = CmdType.WRITE, token = 4, arg = Enumerable.Range(0, 189).Select(x => new string((char)(x + 45), 3))},

                new Command {type = CmdType.FREE, token = 4},
                new Command {type = CmdType.FREE, token = 3},
                new Command {type = CmdType.FREE, token = 2},
                new Command {type = CmdType.FREE, token = 1},

                new Command {type = CmdType.ALLOC_WRITE, token = 3, arg = Enumerable.Range(0, 127).Select(x => new string((char)(x + 32), 3))},
                new Command {type = CmdType.ALLOC_WRITE, token = 4, arg = Enumerable.Range(0, 37).Select(x => new string((char)(x + 32), 3))},
                new Command {type = CmdType.ALLOC_WRITE, token = 1, arg = Enumerable.Range(0, 115).Select(x => new string((char)(x + 32), 3))},
                new Command {type = CmdType.ALLOC_WRITE, token = 2, arg = Enumerable.Range(0, 46).Select(x => new string((char)(x + 32), 3))},

                new Command {type = CmdType.FREE, token = 0},
                new Command {type = CmdType.FREE, token = 1},
                new Command {type = CmdType.FREE, token = 2},
                new Command {type = CmdType.FREE, token = 3},
                new Command {type = CmdType.FREE, token = 4},
            };

            yield return new object[] {testCase1};
        }

        [Theory]
        [MemberData(nameof(allocateAndFreeTest_refType_data))]
        public void allocateAndFreeTest_refType(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<string>();
            runCommandsWithChecks(pool, commands);
        }

        public static IEnumerable<object[]> allocateAndFreeTest_valueTypeWithRefs_data() {
            var testCase1 = new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.ALLOC, token = 1, arg = 2},
                new Command {type = CmdType.ALLOC, token = 2, arg = 32},
                new Command {type = CmdType.ALLOC, token = 3, arg = 144},
                new Command {type = CmdType.ALLOC, token = 4, arg = 189},

                new Command {type = CmdType.WRITE, token = 1, arg = new[] {new StructWithRef {p = 1, q = "abc"}, new StructWithRef {p = 444, q = "pq"}}},
                new Command {type = CmdType.WRITE, token = 2, arg = Enumerable.Range(0, 32).Select(x => new StructWithRef {p = x, q = new string('a', x)})},
                new Command {type = CmdType.WRITE, token = 3, arg = Enumerable.Range(0, 144).Select(x => new StructWithRef {p = x, q = new string('a', 1)})},
                new Command {type = CmdType.WRITE, token = 4, arg = Enumerable.Range(0, 189).Select(x => new StructWithRef {p = x, q = new string('b', 1)})},

                new Command {type = CmdType.FREE, token = 4},
                new Command {type = CmdType.FREE, token = 3},
                new Command {type = CmdType.FREE, token = 2},
                new Command {type = CmdType.FREE, token = 1},

                new Command {type = CmdType.ALLOC_WRITE, token = 3, arg = Enumerable.Range(0, 127).Select(x => new StructWithRef {p = x, q = new string('b', 1)})},
                new Command {type = CmdType.ALLOC_WRITE, token = 4, arg = Enumerable.Range(0, 115).Select(x => new StructWithRef {p = x, q = new string('b', 1)})},
                new Command {type = CmdType.ALLOC_WRITE, token = 1, arg = Enumerable.Range(0, 45).Select(x => new StructWithRef {p = x, q = new string('c', 1)})},
                new Command {type = CmdType.ALLOC_WRITE, token = 2, arg = Enumerable.Range(0, 37).Select(x => new StructWithRef {p = x, q = new string('c', 1)})},

                new Command {type = CmdType.FREE, token = 0},
                new Command {type = CmdType.FREE, token = 1},
                new Command {type = CmdType.FREE, token = 2},
                new Command {type = CmdType.FREE, token = 3},
                new Command {type = CmdType.FREE, token = 4},
            };

            yield return new object[] {testCase1};
        }

        [Theory]
        [MemberData(nameof(allocateAndFreeTest_valueTypeWithRefs_data))]
        public void allocateAndFreeTest_valueTypeWithRefs(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<StructWithRef>();
            runCommandsWithChecks(pool, commands);
        }

        public static IEnumerable<object[]> allocateAndResizeTest_valueType_data() {
            var testCase1 = new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.ALLOC, token = 1, arg = 1},
                new Command {type = CmdType.ALLOC, token = 2, arg = 2},
                new Command {type = CmdType.ALLOC, token = 3, arg = 64},

                new Command {type = CmdType.RESIZE, arg = 0, token = 0},
                new Command {type = CmdType.RESIZE, arg = 1, token = 0},
                new Command {type = CmdType.RESIZE, arg = 2, token = 0},
                new Command {type = CmdType.RESIZE, arg = 4, token = 0},
                new Command {type = CmdType.RESIZE, arg = 16, token = 0},
                new Command {type = CmdType.RESIZE, arg = 14, token = 0},
                new Command {type = CmdType.RESIZE, arg = 4, token = 0},
                new Command {type = CmdType.RESIZE, arg = 0, token = 0},
                new Command {type = CmdType.RESIZE, arg = 16, token = 0},
                new Command {type = CmdType.RESIZE, arg = 3, token = 0},

                new Command {type = CmdType.RESIZE, arg = 1, token = 1},
                new Command {type = CmdType.RESIZE, arg = 2, token = 1},
                new Command {type = CmdType.RESIZE, arg = 4, token = 1},
                new Command {type = CmdType.RESIZE, arg = 16, token = 1},
                new Command {type = CmdType.RESIZE, arg = 14, token = 1},
                new Command {type = CmdType.RESIZE, arg = 4, token = 1},
                new Command {type = CmdType.RESIZE, arg = 0, token = 1},
                new Command {type = CmdType.RESIZE, arg = 16, token = 1},
                new Command {type = CmdType.RESIZE, arg = 1, token = 1},

                new Command {type = CmdType.RESIZE, arg = 2, token = 2},
                new Command {type = CmdType.RESIZE, arg = 4, token = 2},
                new Command {type = CmdType.RESIZE, arg = 16, token = 2},
                new Command {type = CmdType.RESIZE, arg = 14, token = 2},
                new Command {type = CmdType.RESIZE, arg = 2, token = 2},
                new Command {type = CmdType.RESIZE, arg = 0, token = 2},
                new Command {type = CmdType.RESIZE, arg = 0, token = 2},
                new Command {type = CmdType.RESIZE, arg = 31, token = 2},
                new Command {type = CmdType.RESIZE, arg = 31, token = 2},
                new Command {type = CmdType.RESIZE, arg = 4, token = 2},

                new Command {type = CmdType.RESIZE, arg = 63, token = 3},
                new Command {type = CmdType.RESIZE, arg = 62, token = 3},
                new Command {type = CmdType.RESIZE, arg = 63, token = 3},
                new Command {type = CmdType.RESIZE, arg = 32, token = 3},
                new Command {type = CmdType.RESIZE, arg = 31, token = 3},
                new Command {type = CmdType.RESIZE, arg = 30, token = 3},
                new Command {type = CmdType.RESIZE, arg = 60, token = 3},
                new Command {type = CmdType.RESIZE, arg = 63, token = 3},
                new Command {type = CmdType.RESIZE, arg = 64, token = 3},
                new Command {type = CmdType.RESIZE, arg = 255, token = 3},
                new Command {type = CmdType.RESIZE, arg = 256, token = 3},
                new Command {type = CmdType.RESIZE, arg = 0, token = 3},
                new Command {type = CmdType.RESIZE, arg = 0, token = 3},
                new Command {type = CmdType.RESIZE, arg = 2, token = 3},
                new Command {type = CmdType.RESIZE, arg = 64, token = 3},
            };

            var testCase2 = new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.ALLOC, token = 1, arg = 8},
                new Command {type = CmdType.ALLOC_WRITE, token = 2, arg = Enumerable.Repeat(111, 64)},

                new Command {type = CmdType.RESIZE_WRITE, token = 0, arg = Enumerable.Empty<int>()},
                new Command {type = CmdType.RESIZE_WRITE, token = 0, arg = Enumerable.Repeat(1337, 6)},
                new Command {type = CmdType.RESIZE_WRITE, token = 0, arg = Enumerable.Repeat(1562, 9)},
                new Command {type = CmdType.RESIZE_WRITE, token = 0, arg = Enumerable.Repeat(1568, 12)},
                new Command {type = CmdType.RESIZE_WRITE, token = 0, arg = Enumerable.Repeat(1763, 39)},
                new Command {type = CmdType.RESIZE, token = 0, arg = 45},
                new Command {type = CmdType.RESIZE_WRITE, token = 0, arg = Enumerable.Repeat(1801, 33)},
                new Command {type = CmdType.RESIZE_WRITE, token = 0, arg = Enumerable.Repeat(1845, 25)},
                new Command {type = CmdType.RESIZE_WRITE, token = 0, arg = Enumerable.Repeat(1895, 18)},
                new Command {type = CmdType.RESIZE, token = 0, arg = 11},
                new Command {type = CmdType.RESIZE_WRITE, token = 0, arg = Enumerable.Repeat(1906, 14)},

                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Repeat(1337, 8)},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Repeat(1562, 9)},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Repeat(1568, 12)},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Repeat(1763, 39)},
                new Command {type = CmdType.RESIZE, token = 1, arg = 45},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Repeat(1801, 33)},
                new Command {type = CmdType.RESIZE, token = 1, arg = 25},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Repeat(1895, 18)},
                new Command {type = CmdType.RESIZE, token = 1, arg = 11},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Repeat(1906, 14)},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Empty<int>()},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Repeat(1999, 26)},

                new Command {type = CmdType.RESIZE_WRITE, token = 2, arg = Enumerable.Repeat(1033, 76)},
                new Command {type = CmdType.RESIZE_WRITE, token = 2, arg = Enumerable.Repeat(1069, 132)},
                new Command {type = CmdType.RESIZE_WRITE, token = 2, arg = Enumerable.Repeat(1078, 196)},
                new Command {type = CmdType.RESIZE_WRITE, token = 2, arg = Enumerable.Repeat(1093, 251)},
                new Command {type = CmdType.RESIZE, token = 2, arg = 269},
                new Command {type = CmdType.RESIZE_WRITE, token = 2, arg = Enumerable.Repeat(1105, 283)},
                new Command {type = CmdType.RESIZE_WRITE, token = 2, arg = Enumerable.Repeat(1109, 283)},
                new Command {type = CmdType.RESIZE_WRITE, token = 2, arg = Enumerable.Repeat(1136, 271)},
                new Command {type = CmdType.RESIZE, token = 2, arg = 244},
                new Command {type = CmdType.RESIZE_WRITE, token = 2, arg = Enumerable.Repeat(1146, 219)},
                new Command {type = CmdType.RESIZE, token = 2, arg = 199},
                new Command {type = CmdType.RESIZE_WRITE, token = 2, arg = Enumerable.Repeat(1217, 133)},
                new Command {type = CmdType.RESIZE_WRITE, token = 2, arg = Enumerable.Repeat(1273, 112)},
                new Command {type = CmdType.RESIZE_WRITE, token = 2, arg = Enumerable.Repeat(1407, 45)},
                new Command {type = CmdType.RESIZE, token = 2, arg = 36},
                new Command {type = CmdType.RESIZE, token = 2, arg = 28},
                new Command {type = CmdType.RESIZE_WRITE, token = 2, arg = Enumerable.Repeat(1444, 11)},
                new Command {type = CmdType.RESIZE_WRITE, token = 2, arg = Enumerable.Repeat(1533, 2)},
                new Command {type = CmdType.RESIZE, token = 2, arg = 0},
                new Command {type = CmdType.RESIZE_WRITE, token = 2, arg = Enumerable.Repeat(1609, 1000)},
            };

            var testCase3 = new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.ALLOC_WRITE, token = 1, arg = Enumerable.Repeat(111, 6)},
                new Command {type = CmdType.ALLOC_WRITE, token = 2, arg = Enumerable.Repeat(222, 12)},
                new Command {type = CmdType.ALLOC_WRITE, token = 3, arg = Enumerable.Repeat(333, 36)},
                new Command {type = CmdType.ALLOC_WRITE, token = 4, arg = Enumerable.Repeat(444, 36)},

                new Command {type = CmdType.RESIZE, token = 0, arg = 12},
                new Command {type = CmdType.RESIZE, token = 1, arg = 12},
                new Command {type = CmdType.RESIZE, token = 2, arg = 12},

                new Command {type = CmdType.ALLOC_WRITE, token = 5, arg = Enumerable.Repeat(555, 45)},
                new Command {type = CmdType.ALLOC_WRITE, token = 6, arg = Enumerable.Repeat(666, 45)},

                new Command {type = CmdType.RESIZE_WRITE, token = 0, arg = Enumerable.Repeat(1112, 9)},
                new Command {type = CmdType.RESIZE_WRITE, token = 3, arg = Enumerable.Repeat(3335, 28)},
                new Command {type = CmdType.RESIZE_WRITE, token = 4, arg = Enumerable.Repeat(4447, 312)},

                new Command {type = CmdType.RESIZE, token = 6, arg = 70},
                new Command {type = CmdType.RESIZE_WRITE, token = 3, arg = Enumerable.Repeat(3331, 41)},
                new Command {type = CmdType.RESIZE_WRITE, token = 4, arg = Enumerable.Repeat(4442, 15)},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Repeat(1117, 6)},
                new Command {type = CmdType.RESIZE, token = 4, arg = 150},
            };

            yield return new object[] {testCase1};
            yield return new object[] {testCase2};
            yield return new object[] {testCase3};

            yield return new object[] {
                genRandomTestCase(
                    numTokens: 60, numCommands: 1000, maxLength: 400, seed: 800183, allocRatio: 1, resizeRatio: 2
                )
            };
        }

        [Theory]
        [MemberData(nameof(allocateAndResizeTest_valueType_data))]
        public void allocateAndResizeTest_valueType(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<int>();
            runCommandsWithChecks(pool, commands);
        }

        [Fact]
        public void allocateAndResizeTest_valueType_specialCases() {
            var specialTestCase = (new Command[] {
                    new Command {type = CmdType.ALLOC, token = 0, arg = 1 << 20},
                    new Command {type = CmdType.ALLOC, token = 1, arg = 1 << 20},
                })
                .Concat(Enumerable.Range(0, (1 << 14) + 1).SelectMany(i => new Command[] {
                    new Command {type = CmdType.RESIZE, token = 0, arg = (1 << 20) + i},
                    new Command {type = CmdType.RESIZE, token = 1, arg = (1 << 20) + 2 * i},
                }));

            var pool = new DynamicArrayPool<int>();
            var tokens = new DynamicArrayPoolToken<int>[2];
            var contents = new List<int>[2];

            foreach (Command cmd in specialTestCase)
                runCommand(pool, cmd, tokens, contents, doChecks: false);

            Assert.Equal((1 << 20) + (1 << 14), pool.getSpan(tokens[0]).Length);
            Assert.Equal((1 << 20) + (1 << 15), pool.getSpan(tokens[1]).Length);
        }

        public static IEnumerable<object[]> allocateAndResizeTest_refType_data() {
            var testCase1 = new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.RESIZE, token = 0, arg = 1},
                new Command {type = CmdType.RESIZE, token = 0, arg = 2},
                new Command {type = CmdType.RESIZE, token = 0, arg = 3},
                new Command {type = CmdType.RESIZE, token = 0, arg = 4},
                new Command {type = CmdType.RESIZE, token = 0, arg = 6},
                new Command {type = CmdType.RESIZE, token = 0, arg = 8},
                new Command {type = CmdType.RESIZE, token = 0, arg = 12},
                new Command {type = CmdType.RESIZE, token = 0, arg = 16},
                new Command {type = CmdType.RESIZE, token = 0, arg = 256},

                new Command {type = CmdType.ALLOC_WRITE, token = 1, arg = new[] {"a", "b"}},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 6).Select(x => new string('a', 4))},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 18).Select(x => new string('a', 4))},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 25).Select(x => new string('a', 4))},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 45).Select(x => new string('a', 4))},
                new Command {type = CmdType.RESIZE, token = 1, arg = 57},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 63).Select(x => new string('a', 4))},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 97).Select(x => new string('a', 4))},
                new Command {type = CmdType.RESIZE, token = 1, arg = 145},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 217).Select(x => new string('b', 4))},
                new Command {type = CmdType.RESIZE, token = 1, arg = 217},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 185).Select(x => new string('b', 4))},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 138).Select(x => new string('b', 4))},
                new Command {type = CmdType.RESIZE, token = 1, arg = 119},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 78).Select(x => new string('b', 4))},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 63).Select(x => new string('b', 4))},
                new Command {type = CmdType.RESIZE, token = 1, arg = 63},
                new Command {type = CmdType.RESIZE, token = 1, arg = 59},
                new Command {type = CmdType.RESIZE, token = 1, arg = 0},
                new Command {type = CmdType.RESIZE, token = 1, arg = 120},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 380).Select(x => new string('c', 4))},
            };

            yield return new object[] {testCase1};
        }

        [Theory]
        [MemberData(nameof(allocateAndResizeTest_refType_data))]
        public void allocateAndResizeTest_refType(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<string>();
            runCommandsWithChecks(pool, commands);
        }

        public static IEnumerable<object[]> allocateAndResizeTest_valueTypeWithRefs_data() {
            var testCase1 = new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.RESIZE, token = 0, arg = 1},
                new Command {type = CmdType.RESIZE, token = 0, arg = 2},
                new Command {type = CmdType.RESIZE, token = 0, arg = 3},
                new Command {type = CmdType.RESIZE, token = 0, arg = 4},
                new Command {type = CmdType.RESIZE, token = 0, arg = 6},
                new Command {type = CmdType.RESIZE, token = 0, arg = 8},
                new Command {type = CmdType.RESIZE, token = 0, arg = 12},
                new Command {type = CmdType.RESIZE, token = 0, arg = 16},
                new Command {type = CmdType.RESIZE, token = 0, arg = 256},

                new Command {type = CmdType.ALLOC_WRITE, token = 1, arg = new[] {new StructWithRef {p = 1, q = "A"}, new StructWithRef {p = 4, q = "C"}}},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 6).Select(x => new StructWithRef{p = x * x, q = new string('a', 4)})},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 18).Select(x => new StructWithRef{p = x * x, q = new string('a', 4)})},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 25).Select(x => new StructWithRef{p = x * x, q = new string('a', 4)})},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 45).Select(x => new StructWithRef{p = x * x, q = new string('a', 4)})},
                new Command {type = CmdType.RESIZE, token = 1, arg = 57},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 63).Select(x => new StructWithRef{p = x * x, q = new string('a', 4)})},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 97).Select(x => new StructWithRef{p = x * x, q = new string('a', 4)})},
                new Command {type = CmdType.RESIZE, token = 1, arg = 145},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 217).Select(x => new StructWithRef{p = x * x, q = new string('b', 4)})},
                new Command {type = CmdType.RESIZE, token = 1, arg = 217},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 185).Select(x => new StructWithRef{p = x * x, q = new string('b', 4)})},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 138).Select(x => new StructWithRef{p = x * x, q = new string('b', 4)})},
                new Command {type = CmdType.RESIZE, token = 1, arg = 119},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 78).Select(x => new StructWithRef{p = x * x, q = new string('b', 4)})},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 63).Select(x => new StructWithRef{p = x * x, q = new string('b', 4)})},
                new Command {type = CmdType.RESIZE, token = 1, arg = 63},
                new Command {type = CmdType.RESIZE, token = 1, arg = 59},
                new Command {type = CmdType.RESIZE, token = 1, arg = 0},
                new Command {type = CmdType.RESIZE, token = 1, arg = 120},
                new Command {type = CmdType.RESIZE_WRITE, token = 1, arg = Enumerable.Range(0, 380).Select(x => new StructWithRef{p = x * 14, q = new string('c', 4)})},
            };

            yield return new object[] {testCase1};
        }

        [Theory]
        [MemberData(nameof(allocateAndResizeTest_valueTypeWithRefs_data))]
        public void allocateAndResizeTest_valueTypeWithRefs(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<StructWithRef>();
            runCommandsWithChecks(pool, commands);
        }

        public static IEnumerable<object[]> allocateAndAppendTest_valueType_data() {
            var testCase1 = (new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.ALLOC_WRITE, token = 1, arg = Enumerable.Range(0, 15)},
            })
            .Concat(Enumerable.Range(0, 25).Select(
                i => new Command {type = CmdType.APPEND, token = 0, arg = i}
            ))
            .Concat(Enumerable.Range(0, 25).Select(
                i => new Command {type = CmdType.APPEND, token = 1, arg = i * i}
            ))
            .Concat(Enumerable.Range(25, 25).Select(
                i => new Command {type = CmdType.APPEND, token = 0, arg = i * 2}
            ))
            .Concat(Enumerable.Range(25, 25).Select(
                i => new Command {type = CmdType.APPEND, token = 1, arg = i * i * 3}
            ));

            var testCase2 = Enumerable.Range(0, 50).SelectMany(i => new Command[] {
                new Command {type = CmdType.ALLOC_WRITE, token = i, arg = Enumerable.Range(0, i)},
                new Command {type = CmdType.APPEND, token = i, arg = 1320},
                new Command {type = CmdType.APPEND, token = i, arg = 4254},
                new Command {type = CmdType.APPEND, token = i, arg = 7531},
                new Command {type = CmdType.APPEND, token = i, arg = 1933},
                new Command {type = CmdType.APPEND, token = i, arg = 5482},
                new Command {type = CmdType.APPEND, token = i, arg = 1039},
                new Command {type = CmdType.APPEND, token = i, arg = 4402},
                new Command {type = CmdType.APPEND, token = i, arg = 4891},
                new Command {type = CmdType.APPEND, token = i, arg = 6049},
                new Command {type = CmdType.APPEND, token = i, arg = 8808},
                new Command {type = CmdType.APPEND, token = i, arg = 7516},
                new Command {type = CmdType.APPEND, token = i, arg = 2842},
                new Command {type = CmdType.APPEND, token = i, arg = 9001},
                new Command {type = CmdType.APPEND, token = i, arg = 4884},
                new Command {type = CmdType.APPEND, token = i, arg = 5097},
                new Command {type = CmdType.APPEND, token = i, arg = 3301},
                new Command {type = CmdType.APPEND, token = i, arg = 2245},
            });

            yield return new object[] {testCase1};
            yield return new object[] {testCase2};

            yield return new object[] {
                genRandomTestCase(
                    numTokens: 50, maxLength: 512, numCommands: 850, seed: 5968113, allocRatio: 1, appendRatio: 16
                )
            };
        }

        [Theory]
        [MemberData(nameof(allocateAndAppendTest_valueType_data))]
        public void allocateAndAppendTest_valueType(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<int>();
            runCommandsWithChecks(pool, commands);
        }

        public static IEnumerable<object[]> allocateAndAppendTest_refType_data() {
            var testCase1 = new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.APPEND, token = 0, arg = "1320"},
                new Command {type = CmdType.APPEND, token = 0, arg = "4254"},
                new Command {type = CmdType.APPEND, token = 0, arg = "7531"},
                new Command {type = CmdType.APPEND, token = 0, arg = "1933"},
                new Command {type = CmdType.APPEND, token = 0, arg = "5482"},
                new Command {type = CmdType.APPEND, token = 0, arg = "1039"},
                new Command {type = CmdType.APPEND, token = 0, arg = "4402"},
                new Command {type = CmdType.APPEND, token = 0, arg = "4891"},
                new Command {type = CmdType.APPEND, token = 0, arg = "6049"},
                new Command {type = CmdType.APPEND, token = 0, arg = "8808"},
                new Command {type = CmdType.APPEND, token = 0, arg = "7516"},
                new Command {type = CmdType.APPEND, token = 0, arg = "2842"},
                new Command {type = CmdType.APPEND, token = 0, arg = "9001"},
                new Command {type = CmdType.APPEND, token = 0, arg = "4884"},
                new Command {type = CmdType.APPEND, token = 0, arg = "5097"},
                new Command {type = CmdType.APPEND, token = 0, arg = "3301"},
                new Command {type = CmdType.APPEND, token = 0, arg = "2245"},
            };

            yield return new object[] {testCase1};
        }

        [Fact]
        public void allocateAndAppendTest_valueType_specialCases() {
            var specialTestCase = (new Command[] {
                    new Command {type = CmdType.ALLOC_WRITE, token = 0, arg = Enumerable.Range(0, 1 << 20)},
                })
                .Concat(Enumerable.Range(0, 1 << 14).Select(
                    i => new Command {type = CmdType.APPEND, token = 0, arg = (1 << 20) + i}
                ));

            var pool = new DynamicArrayPool<int>();
            var tokens = new DynamicArrayPoolToken<int>[1];
            var contents = new List<int>[1];

            foreach (Command cmd in specialTestCase)
                runCommand(pool, cmd, tokens, contents, doChecks: false);

            var span = pool.getSpan(tokens[0]);
            bool isCorrect = true;
            for (int i = 0; i < span.Length && isCorrect; i++)
                isCorrect &= i == span[i];

            Assert.True(isCorrect);
        }

        [Theory]
        [MemberData(nameof(allocateAndAppendTest_refType_data))]
        public void allocateAndAppendTest_refType(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<string>();
            runCommandsWithChecks(pool, commands);
        }

        public static IEnumerable<object[]> allocateAndAppendTest_valueTypeWithRefs_data() {
            var testCase1 = new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 0},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 1320, q = "1320"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 4254, q = "4254"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 7531, q = "7531"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 1933, q = "1933"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 5482, q = "5482"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 1039, q = "1039"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 4402, q = "4402"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 4891, q = "4891"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 6049, q = "6049"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 8808, q = "8808"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 7516, q = "7516"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 2842, q = "2842"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 9001, q = "9001"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 4884, q = "4884"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 5097, q = "5097"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 3301, q = "3301"}},
                new Command {type = CmdType.APPEND, token = 0, arg = new StructWithRef {p = 2245, q = "2245"}},
            };

            yield return new object[] {testCase1};
        }

        [Theory]
        [MemberData(nameof(allocateAndAppendTest_valueTypeWithRefs_data))]
        public void allocateAndAppendTest_valueTypeWithRefs(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<StructWithRef>();
            runCommandsWithChecks(pool, commands);
        }

        public static IEnumerable<object[]> allocateAndClearTest_valueType_data() {
            var testCase1 = new Command[] {
                new Command {type = CmdType.CLEAR},
                new Command {type = CmdType.ALLOC, token = 0, arg = 10},
                new Command {type = CmdType.ALLOC, token = 1, arg = 20},
                new Command {type = CmdType.ALLOC, token = 2, arg = 30},
                new Command {type = CmdType.CLEAR},
                new Command {type = CmdType.ALLOC_WRITE, token = 0, arg = Enumerable.Range(1000, 100)},
                new Command {type = CmdType.ALLOC_WRITE, token = 1, arg = Enumerable.Range(2000, 200)},
                new Command {type = CmdType.ALLOC_WRITE, token = 2, arg = Enumerable.Range(3000, 400)},
                new Command {type = CmdType.CLEAR},
                new Command {type = CmdType.CLEAR},
                new Command {type = CmdType.CLEAR},
                new Command {type = CmdType.ALLOC_WRITE, token = 0, arg = Enumerable.Repeat(188849, 30)},
                new Command {type = CmdType.ALLOC, token = 1, arg = 30},
                new Command {type = CmdType.ALLOC_WRITE, token = 2, arg = Enumerable.Repeat(188849, 30)},
                new Command {type = CmdType.ALLOC, token = 3, arg = 30},
                new Command {type = CmdType.ALLOC_WRITE, token = 4, arg = Enumerable.Repeat(188849, 30)},
                new Command {type = CmdType.CLEAR},
            };

            var testCase2 = Enumerable.Range(0, 1 << 11).SelectMany(i => new Command[] {
                new Command {type = CmdType.ALLOC, token = 0, arg = 1 << 21},
                new Command {type = CmdType.ALLOC, token = 1, arg = 1 << 21},
                new Command {type = CmdType.CLEAR},
            });

            yield return new object[] {
                genRandomTestCase(
                    numTokens: 200, numCommands: 500, maxLength: 512, seed: 446371, allocRatio: 20, writeRatio: 10, clearRatio: 1
                )
            };
        }

        [Theory]
        [MemberData(nameof(allocateAndClearTest_valueType_data))]
        public void allocateAndClearTest_valueType(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<int>();
            runCommandsWithChecks(pool, commands);
        }

        [Fact]
        public void allocateAndClearTest_refType() {
            var pool = new DynamicArrayPool<string>();

            var token1 = pool.allocate(4);
            var token2 = pool.allocate(35);
            var token3 = pool.allocate(40);
            var token4 = pool.allocate(120);

            var span1 = pool.getSpan(token1);
            var span2 = pool.getSpan(token2);
            var span3 = pool.getSpan(token3);
            var span4 = pool.getSpan(token4);

            span1.Fill("a");
            span2.Fill("a");
            span3.Fill("a");
            span4.Fill("a");

            pool.clear();

            Span<string> nullSequence = new string[120];
            Assert.True(span1.SequenceEqual(nullSequence.Slice(0, span1.Length)));
            Assert.True(span2.SequenceEqual(nullSequence.Slice(0, span2.Length)));
            Assert.True(span3.SequenceEqual(nullSequence.Slice(0, span3.Length)));
            Assert.True(span4.SequenceEqual(nullSequence.Slice(0, span4.Length)));
        }

        [Fact]
        public void allocateAndClearTest_valueTypeWithRefs() {
            var pool = new DynamicArrayPool<StructWithRef>();

            var token1 = pool.allocate(4);
            var token2 = pool.allocate(35);
            var token3 = pool.allocate(40);
            var token4 = pool.allocate(120);

            var span1 = pool.getSpan(token1);
            var span2 = pool.getSpan(token2);
            var span3 = pool.getSpan(token3);
            var span4 = pool.getSpan(token4);

            span1.Fill(new StructWithRef {p = 1, q = "a"});
            span2.Fill(new StructWithRef {p = 1, q = "a"});
            span3.Fill(new StructWithRef {p = 1, q = "a"});
            span4.Fill(new StructWithRef {p = 1, q = "a"});

            pool.clear();

            Span<StructWithRef> defaultSequence = new StructWithRef[120];
            Assert.True(span1.SequenceEqual(defaultSequence.Slice(0, span1.Length)));
            Assert.True(span2.SequenceEqual(defaultSequence.Slice(0, span2.Length)));
            Assert.True(span3.SequenceEqual(defaultSequence.Slice(0, span3.Length)));
            Assert.True(span4.SequenceEqual(defaultSequence.Slice(0, span4.Length)));
        }

        public static IEnumerable<object[]> mixedOperationsTest_data = new object[][] {
            new object[] {
                genRandomTestCase(
                    numTokens: 60, numCommands: 1200, maxLength: 512, seed: 3419945,
                    allocRatio: 10, writeRatio: 5, freeRatio: 5, resizeRatio: 5, appendRatio: 10, clearRatio: 1
                )
            }
        };

        [Theory]
        [MemberData(nameof(mixedOperationsTest_data))]
        public void mixedOperationsTest(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<int>();
            runCommandsWithChecks(pool, commands);
        }

        [Fact]
        public void allocateTest_negativeLength() {
            var pool = new DynamicArrayPool<int>();
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.allocate(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.allocate(-1, out _));
        }

        [Fact]
        public void resizeTest_negativeLength() {
            var pool = new DynamicArrayPool<int>();
            var token = pool.allocate(100);

            Assert.Throws<ArgumentOutOfRangeException>(() => pool.resize(token, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.resize(token, -1, out _));
        }

        public static IEnumerable<object[]> arrayToStringTest_data = new object[][] {
            new object[] {
                genRandomTestCase(
                    numTokens: 60, numCommands: 1200, maxLength: 256, seed: 571838,
                    allocRatio: 10, writeRatio: 5, freeRatio: 5, resizeRatio: 5, appendRatio: 10, clearRatio: 1
                )
            }
        };

        [Theory]
        [MemberData(nameof(arrayToStringTest_data))]
        public void arrayToStringTest(IEnumerable<Command> commands) {
            var pool = new DynamicArrayPool<int>();

            Assert.Equal("[]", pool.arrayToString(default));

            int numTokens = commands.Max(x => x.token) + 1;
            var tokens = new DynamicArrayPoolToken<int>[numTokens];
            var contents = new List<int>[numTokens];

            var sb = new StringBuilder();

            foreach (Command cmd in commands) {
                runCommand(pool, cmd, tokens, contents, doChecks: false);

                for (int i = 0; i < tokens.Length; i++) {
                    if (tokens[i].isDefault || contents[i] == null)
                        continue;

                    sb.Clear();
                    sb.Append('[');

                    var data = contents[i];
                    for (int j = 0; j < data.Count; j++)
                        sb.Append((j == 0) ? "" : ", ").Append(data[j]);

                    sb.Append(']');
                    Assert.Equal(sb.ToString(), pool.arrayToString(tokens[i]));
                }
            }
        }

    }

}
