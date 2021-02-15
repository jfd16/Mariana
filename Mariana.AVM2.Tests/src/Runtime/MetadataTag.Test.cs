using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;

namespace Mariana.AVM2.Tests {

    public class MetadataTagTest {

        public static IEnumerable<object[]> shouldCreateFromKeysAndValues_data = new (string name, string[] keys, string[] values)[] {
            (
                "A", null, null
            ),
            (
                "B", new string[2], new[] {"a", "b"}
            ),
            (
                "B", new string[4], new[] {"a", "a", "a", "a"}
            ),
            (
                "B", new[] {"a", "b", "c"}, new[] {"c", "b", "a"}
            ),
            (
                "B", new[] {"a", "b", "c", ""}, new[] {"x", "x", "x", "x"}
            ),
            (
                "B",
                new[] {"a", "b", "a", "c", "b", "b"},
                new[] {"1", "2", "3", "4", "5", "2"}
            ),
            (
                "CCC",
                new string[15],
                new[] {"abcd", "efgh", "ijkl", "mnop", "qrst", "uvwx", "yzAB", "<>", "Q", "", "{44", "fj", "OP4", "lli", "9003"}
            ),
            (
                "ddd",
                new[] {"1", "a", null, "f", null, "..fef4", "::12000", null, null, null, null, "ff", "", "gg", "hij"},
                new[] {"abcd", "efgh", "ijkl", "mnop", "qrst", "uvwx", "yzAB", "<>", "Q", "", "{44", "fj", "OP4", "lli", "9003"}
            ),
            (
                "1234",
                new[] {"a", "b", "c", "d", "e", "f", "g", "1", "2", "3", "4", "5", "6", "7", "8"},
                new[] {"abcd", "efgh", "ijkl", "mnop", "qrst", "uvwx", "yzAB", "<>", "Q", "", "{44", "fj", "OP4", "lli", "9003"}
            ),
            (
                "1234",
                new[] {"a", "b", "c", "d", "e", "f", "g", "g", "f", null, "d", "c", null, "b", "a"},
                new[] {"abcd", "efgh", "ijkl", "mnop", "qrst", "uvwx", "yzAB", "<>", "Q", "", "{44", "fj", "OP4", "lli", "9003"}
            )
        }.Select(x => new object[] {x.name, x.keys, x.values});

        [Theory]
        [MemberData(nameof(shouldCreateFromKeysAndValues_data))]
        public void shouldCreateFromKeysAndValues(string name, IEnumerable<string> keys, IEnumerable<string> values) {
            values = values ?? Enumerable.Empty<string>();
            keys = keys ?? Enumerable.Repeat<string>(null, values.Count());

            var metadataTag = new MetadataTag(name, keys.ToArray(), values.ToArray());

            Assert.Equal(name, metadataTag.name);
            Assert.Equal<(string, string)>(
                keys.Zip(values),
                metadataTag.getKeyValuePairs().Select(x => (x.Key, x.Value))
            );
        }

        public static IEnumerable<object[]> hasValueAndIndexerTest_data = new (string name, string[] keys, string[] values, string[] failKeys)[] {
            (
                "A", null, null, new[] {"a", "b", "c", "d", ""}
            ),
            (
                "B", new string[2], new[] {"a", "b"}, new[] {"a", "b", "c", "d", ""}
            ),
            (
                "B", new string[4], new[] {"a", "a", "a", "a"}, new[] {"a", "b", "c", "d", ""}
            ),
            (
                "B", new[] {"a", "b", "c"}, new[] {"c", "b", "a"}, new[] {"d", "A", "B", "C", "x", "ab", ""}
            ),
            (
                "B", new[] {"a", "b", "c", ""}, new[] {"x", "x", "x", "x"}, new[] {"d", "A", "B", "C", "x", "ab"}
            ),
            (
                "B",
                new[] {"a", "b", "a", "c", "b", "b"},
                new[] {"1", "2", "3", "4", "5", "2"},
                new[] {"1", "2", "3" , "d", "A", ""}
            ),
            (
                "CCC",
                new string[15],
                new[] {"abcd", "efgh", "ijkl", "mnop", "qrst", "uvwx", "yzAB", "<>", "Q", "", "{44", "fj", "OP4", "lli", "9003"},
                new[] {"abcd", "efgh", "ijkl", "", "<>"}
            ),
            (
                "ddd",
                new[] {"1", "a", null, "f", null, "..fef4", "::12000", null, null, null, null, "ff", "", "gg", "hij"},
                new[] {"abcd", "efgh", "ijkl", "mnop", "qrst", "uvwx", "yzAB", "<>", "Q", "", "{44", "fj", "OP4", "lli", "9003"},
                new[] {"abcd", "efgh", "ijkl", "<>", "null", "{44"}
            ),
            (
                "1234",
                new[] {"a", "b", "c", "d", "e", "f", "g", "1", "2", "3", "4", "5", "6", "7", "8"},
                new[] {"abcd", "efgh", "ijkl", "mnop", "qrst", "uvwx", "yzAB", "<>", "Q", "", "{44", "fj", "OP4", "lli", "9003"},
                new[] {"abcd", "efgh", "ijkl", "<>", "null", "{44"}
            ),
            (
                "1234",
                new[] {"a", "b", "c", "d", "e", "f", "g", "g", "f", null, "d", "c", null, "b", "a"},
                new[] {"abcd", "efgh", "ijkl", "mnop", "qrst", "uvwx", "yzAB", "<>", "Q", "", "{44", "fj", "OP4", "lli", "9003"},
                new[] {"abcd", "efgh", "ijkl", "<>", "null", "{44"}
            )
        }.Select(x => new object[] {x.name, x.keys, x.values, x.failKeys});

        [Theory]
        [MemberData(nameof(hasValueAndIndexerTest_data))]
        public void hasValueAndIndexerTest(
            string name, IEnumerable<string> keys, IEnumerable<string> values, IEnumerable<string> failKeys)
        {
            values = values ?? Enumerable.Empty<string>();
            keys = keys ?? Enumerable.Repeat<string>(null, values.Count());
            failKeys = failKeys ?? Enumerable.Empty<string>();

            var metadataTag = new MetadataTag(name, keys.ToArray(), values.ToArray());

            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_NULL, () => metadataTag.hasValue(null));
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__ARGUMENT_NULL, () => metadataTag[null]);

            var nonNullKeyPairs = keys.Zip(values).Where(x => x.First != null);

            foreach (var (k, v) in nonNullKeyPairs)
                Assert.True(metadataTag.hasValue(k));

            HashSet<string> seenKeys = new HashSet<string>();
            foreach (var (k, v) in nonNullKeyPairs.Reverse()) {
                if (seenKeys.Add(k))
                    Assert.Equal(v, metadataTag[k]);
            }

            foreach (var k in failKeys) {
                Assert.False(metadataTag.hasValue(k));
                Assert.Null(metadataTag[k]);
            }
        }

        public static IEnumerable<object[]> toStringTest_data = new (string name, string[] keys, string[] values, string expected)[] {
            (
                "A", null, null, "[A]"
            ),
            (
                "A", null, new[] {"a"}, "[A(a)]"
            ),
            (
                "A", null, new[] {"a", "b"}, "[A(a, b)]"
            ),
            (
                "A", new[] {"a", "b"}, new[] {"x", "y"}, "[A(a = x, b = y)]"
            ),
            (
                "A", new[] {"b", "a"}, new[] {"y", "x"}, "[A(b = y, a = x)]"
            ),
            (
                "A", new[] {"a", "b", null, null}, new[] {"p", "q", "r", "s"}, "[A(a = p, b = q, r, s)]"
            ),
            (
                "A", new[] {null, null, "a", "b"}, new[] {"r", "s", "p", "q"}, "[A(r, s, a = p, b = q)]"
            ),
            (
                "A", new[] {null, null, "b", "a"}, new[] {"r", "s", "q", "p"}, "[A(r, s, b = q, a = p)]"
            ),
            (
                "A", new[] {"a", null, "b", null}, new[] {"p", "q", "r", "s"}, "[A(a = p, q, b = r, s)]"
            ),
            (
                "A", new[] {"a", null, "b", null}, new[] {null, null, "p", "q"}, "[A(a = null, null, b = p, q)]"
            ),
            (
                "B", new[] {"a", null}, new[] {"x", "y"}, "[B(a = x, y)]"
            ),
            (
                "B", new[] {null, "a"}, new[] {"y", "x"}, "[B(y, a = x)]"
            ),
            (
                "B", new[] {"a", "b", "c"}, new[] {"c", "b", "a"}, "[B(a = c, b = b, c = a)]"
            ),
            (
                "B", new[] {"a", "b", "c"}, new[] {"1", "2", "3"}, "[B(a = 1, b = 2, c = 3)]"
            ),
            (
                "", null, null, @"[""""]"
            ),
            (
                "", new string[2], new[] {"", ""}, @"[""""("""", """")]"
            ),
            (
                "", new string[2], new[] {"a", ""}, @"[""""(a, """")]"
            ),
            (
                "A", new string[] {null, "a", "b", null, ""}, new string[] {null, "x", "", "", null}, @"[A(null, a = x, b = """", """", """" = null)]"
            ),
            (
                "B",
                new[] {"a", "b", "a", "c", "b", "b"},
                new[] {"1", "2", "3", "4", "5", "2"},
                "[B(a = 1, b = 2, a = 3, c = 4, b = 5, b = 2)]"
            ),
            (
                "ddd",
                new[] {"1", "a", null, "f__g", null, "..fef4", "::12000", null, null, null, null, "ff", "", "gg", "h-ij"},
                new[] {"abcd", "efgh", "ijkl", "mnop", "qrst", "uvwx", "yzAB", "<>", "Q", "", "{44", "fj", "OP4", "lli", "9003"},
                @"[ddd(1 = abcd, a = efgh, ijkl, f__g = mnop, qrst, ""..fef4"" = uvwx, ""::12000"" = yzAB, ""<>"", Q, """", ""{44"", ff = fj, """" = OP4, gg = lli, ""h-ij"" = 9003)]"
            ),
            (
                "ABC\u3904", new[] {"abc\u4958"}, new[] {"abc\u7362"}, "[\"ABC\u3904\"(\"abc\u4958\" = \"abc\u7362\")]"
            ),
            (
                "A", new string[] {null}, new[] {"def\\\"'"}, @"[A(""def\\\""'"")]"
            ),
            (
                "A", new[] {"x"}, new[] {"def\\\"'"}, @"[A(x = ""def\\\""'"")]"
            ),
            (
                "ABC\\\"'", new[] {"abc\\\"'"}, new[] {"def\\\"'"}, @"[""ABC\\\""'""(""abc\\\""'"" = ""def\\\""'"")]"
            ),
        }.Select(x => new object[] {x.name, x.keys, x.values, x.expected});

        [Theory]
        [MemberData(nameof(toStringTest_data))]
        public void toStringTest(string name, IEnumerable<string> keys, IEnumerable<string> values, string expectedString) {
            values = values ?? Enumerable.Empty<string>();
            keys = keys ?? Enumerable.Repeat<string>(null, values.Count());

            var metadataTag = new MetadataTag(name, keys.ToArray(), values.ToArray());
            Assert.Equal(expectedString, metadataTag.ToString());
        }

    }

}
