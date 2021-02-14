using System;
using System.Collections.Generic;
using System.Linq;
using Mariana.AVM2.Core;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class NamespaceSetTest {

        [Fact]
        public void defaultValue_shouldBeEmpty() {
            NamespaceSet nsSet = default;
            Assert.Equal(0, nsSet.count);
            Assert.Equal(0, nsSet.getNamespaces().length);
        }

        [Fact]
        public void ctor_shouldCreateEmptySetFromNullOrEmptyArray() {
            NamespaceSet nsSet = new NamespaceSet(null);
            Assert.Equal(0, nsSet.count);
            Assert.Equal(0, nsSet.getNamespaces().length);

            nsSet = new NamespaceSet(new Namespace[0]);
            Assert.Equal(0, nsSet.count);
            Assert.Equal(0, nsSet.getNamespaces().length);

            nsSet = new NamespaceSet(ReadOnlySpan<Namespace>.Empty);
            Assert.Equal(0, nsSet.count);
            Assert.Equal(0, nsSet.getNamespaces().length);
        }

        public static IEnumerable<object[]> ctor_shouldCreateSet_data = new Namespace[][] {
            new Namespace[0],
            new[] { Namespace.any },
            new[] { Namespace.@public },
            new[] { new Namespace("hello") },
            new[] { new Namespace(NamespaceKind.EXPLICIT, "hello") },
            new[] { Namespace.any, Namespace.any },
            new[] { new Namespace("a"), new Namespace("b"), new Namespace("a"), new Namespace("b"), new Namespace("c") },
            new[] { Namespace.createPrivate(0), new Namespace("a"), Namespace.createPrivate(0), Namespace.createPrivate(1) },
            new[] {
                new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a"),
                new Namespace(NamespaceKind.PROTECTED, "a"),
                new Namespace(NamespaceKind.STATIC_PROTECTED, "a"),
                new Namespace(NamespaceKind.PROTECTED, "a")
            },
            new[] {
                Namespace.@public,
                new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a"),
                new Namespace(NamespaceKind.PROTECTED, "b"),
                new Namespace(NamespaceKind.STATIC_PROTECTED, "c")
            },
        }.Select(x => new object[] {x});

        [Theory]
        [MemberData(nameof(ctor_shouldCreateSet_data))]
        public void ctor_shouldCreateSet(Namespace[] elements) {
            NamespaceSet nsSet;
            HashSet<Namespace> hashSet = elements.ToHashSet();

            nsSet = new NamespaceSet(elements);
            Assert.Equal(nsSet.count, nsSet.getNamespaces().length);
            Assert.Equal(hashSet.Count, nsSet.getNamespaces().length);
            Assert.Subset(hashSet, nsSet.getNamespaces().ToHashSet());

            nsSet = new NamespaceSet(elements.AsSpan());
            Assert.Equal(nsSet.count, nsSet.getNamespaces().length);
            Assert.Equal(hashSet.Count, nsSet.getNamespaces().length);
            Assert.Subset(hashSet, nsSet.getNamespaces().ToHashSet());
        }

        public static IEnumerable<object[]> toString_shouldReturnStringRepr_data = ctor_shouldCreateSet_data;

        [Theory]
        [MemberData(nameof(toString_shouldReturnStringRepr_data))]
        public void toString_shouldReturnStringRepr(Namespace[] elements) {
            var nsSet = new NamespaceSet(elements);
            var str = nsSet.ToString();

            if (nsSet.count == 0) {
                Assert.Equal("[]", str);
                return;
            }

            Assert.Equal('[', str[0]);
            Assert.Equal(']', str[str.Length - 1]);

            var parts = str.Substring(1, str.Length - 2).Split(", ");
            Assert.Equal(nsSet.count, parts.Length);

            var namespaces = nsSet.getNamespaces();
            for (int i = 0; i < namespaces.length; i++)
                Assert.Contains(namespaces[i].isPublic ? "<public>" : namespaces[i].ToString(), parts);
        }

        public static IEnumerable<object[]> containsAny_shouldCheckIfSetContainsAny_data = new (Namespace[], bool)[] {
            (new Namespace[0], false),
            (new[] { Namespace.@public }, false),
            (new[] { Namespace.any }, true),
            (new[] { new Namespace("a"), Namespace.any }, true),
            (new[] { Namespace.any, new Namespace("a") }, true),
            (new[] { Namespace.@public, new Namespace("a") }, false),
            (new[] { Namespace.createPrivate(1), new Namespace("a") }, false),
            (
                new[] {
                    Namespace.@public,
                    new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a"),
                    new Namespace(NamespaceKind.PROTECTED, "b"),
                    new Namespace(NamespaceKind.STATIC_PROTECTED, "c")
                },
                false
            ),
            (
                new[] {
                    new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a"),
                    new Namespace(NamespaceKind.PROTECTED, "b"),
                    Namespace.any,
                    new Namespace(NamespaceKind.STATIC_PROTECTED, "c")
                },
                true
            ),
            (
                new[] {
                    Namespace.any,
                    Namespace.@public,
                    new Namespace("a"),
                    new Namespace(NamespaceKind.EXPLICIT, "b"),
                    new Namespace(NamespaceKind.PACKAGE_INTERNAL, "c"),
                    new Namespace(NamespaceKind.PROTECTED, "d"),
                    new Namespace(NamespaceKind.STATIC_PROTECTED, "e"),
                    Namespace.createPrivate(100),
                },
                true
            )
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(containsAny_shouldCheckIfSetContainsAny_data))]
        public void containsAny_shouldCheckIfSetContainsAny(Namespace[] elements, bool expected) {
            Assert.Equal(expected, (new NamespaceSet(elements)).containsAny);
        }

        public static IEnumerable<object[]> containsPublic_shouldCheckIfSetContainsPublic_data = new (Namespace[], bool)[] {
            (new Namespace[0], false),
            (new[] { Namespace.@public }, true),
            (new[] { Namespace.any }, false),
            (new[] { new Namespace("a"), Namespace.@public }, true),
            (new[] { Namespace.@public, new Namespace("a") }, true),
            (new[] { new Namespace("b"), new Namespace("a") }, false),
            (new[] { Namespace.createPrivate(1), new Namespace("a") }, false),
            (
                new[] {
                    Namespace.@public,
                    new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a"),
                    new Namespace(NamespaceKind.PROTECTED, "b"),
                    new Namespace(NamespaceKind.STATIC_PROTECTED, "c")
                },
                true
            ),
            (
                new[] {
                    new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a"),
                    new Namespace(NamespaceKind.PROTECTED, "b"),
                    new Namespace(NamespaceKind.STATIC_PROTECTED, "c")
                },
                false
            ),
            (
                new[] {
                    Namespace.any,
                    Namespace.@public,
                    new Namespace("a"),
                    new Namespace(NamespaceKind.EXPLICIT, "b"),
                    new Namespace(NamespaceKind.PACKAGE_INTERNAL, "c"),
                    new Namespace(NamespaceKind.PROTECTED, "d"),
                    new Namespace(NamespaceKind.STATIC_PROTECTED, "e"),
                    Namespace.createPrivate(100),
                },
                true
            )
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(containsPublic_shouldCheckIfSetContainsPublic_data))]
        public void containsPublic_shouldCheckIfSetContainsPublic(Namespace[] elements, bool expected) {
            Assert.Equal(expected, (new NamespaceSet(elements)).containsPublic);
        }

        public static IEnumerable<object[]> contains_shouldCheckIfSetContainsGivenKind_data = new Namespace[][] {
            new Namespace[0],

            new[] { Namespace.any },
            new[] { Namespace.@public },
            new[] { new Namespace("a") },
            new[] { new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a") },
            new[] { Namespace.createPrivate(0) },

            new[] { new Namespace("a"), new Namespace("b") },
            new[] { Namespace.any, new Namespace("a") },
            new[] { new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a"), new Namespace("a") },
            new[] { Namespace.createPrivate(0), Namespace.createPrivate(1) },
            new[] { new Namespace(NamespaceKind.PROTECTED, "a"), new Namespace(NamespaceKind.STATIC_PROTECTED, "a") },
            new[] { new Namespace(NamespaceKind.EXPLICIT, "a"), new Namespace(NamespaceKind.PROTECTED, "b") },

            new[] {
                Namespace.any,
                Namespace.@public,
                new Namespace("a"),
                new Namespace(NamespaceKind.EXPLICIT, "b"),
                new Namespace(NamespaceKind.PACKAGE_INTERNAL, "c"),
                new Namespace(NamespaceKind.PROTECTED, "d"),
                new Namespace(NamespaceKind.STATIC_PROTECTED, "e"),
                Namespace.createPrivate(100),
            }
        }.Select(x => new object[] {x});

        [Theory]
        [MemberData(nameof(contains_shouldCheckIfSetContainsGivenKind_data))]
        public void contains_shouldCheckIfSetContainsGivenKind(Namespace[] elements) {
            var kinds = (NamespaceKind[])Enum.GetValues(typeof(NamespaceKind));
            var set = new NamespaceSet(elements);

            for (int i = 0; i < kinds.Length; i++) {
                var hasKind = elements.Any(x => x.kind == kinds[i]);
                Assert.Equal(hasKind, set.contains(kinds[i]));
            }
        }

        public static IEnumerable<object[]> contains_shouldCheckIfSetContainsGivenUri_data() {
            var elements = new Namespace[][] {
                new Namespace[0],
                new[] { Namespace.any },
                new[] { Namespace.@public },
                new[] { new Namespace("a") },
                new[] { new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a") },
                new[] { Namespace.createPrivate(0) },

                new[] { new Namespace("a"), new Namespace("b") },
                new[] { new Namespace("b"), new Namespace("a") },
                new[] { new Namespace("b"), new Namespace("c") },
                new[] { Namespace.any, new Namespace("a") },
                new[] { Namespace.any, new Namespace("b") },
                new[] { new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a"), new Namespace("a") },
                new[] { Namespace.createPrivate(0), new Namespace("a") },
                new[] { new Namespace(NamespaceKind.PROTECTED, "a"), new Namespace(NamespaceKind.STATIC_PROTECTED, "a") },
                new[] { new Namespace(NamespaceKind.EXPLICIT, "a"), new Namespace(NamespaceKind.PROTECTED, "b") },

                new[] {
                    Namespace.any,
                    Namespace.@public,
                    new Namespace("a"),
                    new Namespace(NamespaceKind.EXPLICIT, "b"),
                    new Namespace(NamespaceKind.PACKAGE_INTERNAL, "c"),
                    new Namespace(NamespaceKind.PROTECTED, "d"),
                    new Namespace(NamespaceKind.STATIC_PROTECTED, "e"),
                    Namespace.createPrivate(100),
                }
            };

            var uris = new string[] { null, "", "a", "b" };

            foreach (var e in elements) {
                foreach (var u in uris) {
                    yield return new object[] { e, u };
                }
            }
        }

        [Theory]
        [MemberData(nameof(contains_shouldCheckIfSetContainsGivenUri_data))]
        public void contains_shouldCheckIfSetContainsGivenUri(Namespace[] elements, string uri) {
            var nsSet = new NamespaceSet(elements);
            var hasUri = uri != null && elements.Contains(new Namespace(uri));
            Assert.Equal(hasUri, nsSet.contains(uri));
        }

        public static IEnumerable<object[]> contains_shouldCheckIfSetContainsGivenNamespace_data() {
            var elements = new Namespace[][] {
                new Namespace[0],
                new[] { Namespace.any },
                new[] { Namespace.@public },
                new[] { new Namespace("a") },
                new[] { new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a") },
                new[] { Namespace.createPrivate(0) },

                new[] { new Namespace("a"), new Namespace("b") },
                new[] { new Namespace("b"), new Namespace("a") },
                new[] { new Namespace("b"), new Namespace("c") },
                new[] { Namespace.any, new Namespace("a") },
                new[] { Namespace.any, new Namespace("b") },
                new[] { new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a"), new Namespace("a") },
                new[] { Namespace.createPrivate(0), new Namespace("a") },
                new[] { new Namespace(NamespaceKind.PROTECTED, "a"), new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a") },
                new[] { new Namespace(NamespaceKind.EXPLICIT, "a"), new Namespace(NamespaceKind.STATIC_PROTECTED, "b") },

                new[] {
                    Namespace.any,
                    Namespace.@public,
                    new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a"),
                    new Namespace(NamespaceKind.EXPLICIT, "b"),
                    new Namespace("c"),
                    new Namespace(NamespaceKind.PROTECTED, "d"),
                    new Namespace(NamespaceKind.STATIC_PROTECTED, "e"),
                    Namespace.createPrivate(100),
                }
            };

            var test = new Namespace[] {
                Namespace.any,
                Namespace.@public,
                new Namespace("a"),
                new Namespace("b"),
                new Namespace("aa"),
                new Namespace(NamespaceKind.PROTECTED, "a"),
                new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a"),
                new Namespace(NamespaceKind.STATIC_PROTECTED, "a"),
                Namespace.createPrivate(0)
            };

            foreach (var e in elements) {
                foreach (var ns in test) {
                    yield return new object[] { e, ns };
                }
            }
        }

        [Theory]
        [MemberData(nameof(contains_shouldCheckIfSetContainsGivenNamespace_data))]
        public void contains_shouldCheckIfSetContainsGivenNamespace(Namespace[] elements, Namespace ns) {
            var nsSet = new NamespaceSet(elements);
            Assert.Equal(elements.Contains(ns), nsSet.contains(ns));
        }

    }

}
