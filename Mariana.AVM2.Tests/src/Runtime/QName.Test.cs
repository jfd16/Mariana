using System;
using System.Collections.Generic;
using System.Linq;
using Mariana.AVM2.Core;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class QNameTest {

        public static IEnumerable<object[]> ctor_shouldCreateFromNamespaceAndLocalName_data = new object[][] {
            new object[] { Namespace.any, null },
            new object[] { Namespace.@public, null },
            new object[] { new Namespace("hello"), null },
            new object[] { new Namespace(NamespaceKind.PACKAGE_INTERNAL, "hello"), null },
            new object[] { Namespace.createPrivate(1), null },
            new object[] { Namespace.any, "*" },
            new object[] { Namespace.@public, "*" },
            new object[] { new Namespace("hello"), "*" },
            new object[] { new Namespace(NamespaceKind.PACKAGE_INTERNAL, "hello"), "*" },
            new object[] { Namespace.createPrivate(1), "*" },
            new object[] { Namespace.any, "abc" },
            new object[] { Namespace.@public, "abc" },
            new object[] { new Namespace("hello"), "abc" },
            new object[] { new Namespace(NamespaceKind.PACKAGE_INTERNAL, "hello"), "abc" },
            new object[] { Namespace.createPrivate(1), "abc" },
        };

        [Theory]
        [MemberData(nameof(ctor_shouldCreateFromNamespaceAndLocalName_data))]
        public void ctor_shouldCreateFromNamespaceAndLocalName(Namespace ns, string localName) {
            var qname = new QName(ns, localName);
            Assert.Equal(ns, qname.ns);
            Assert.Equal(localName, qname.localName);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(null, "*")]
        [InlineData(null, "hello")]
        [InlineData("*", null)]
        [InlineData("*", "*")]
        [InlineData("*", "hello")]
        [InlineData("hello", null)]
        [InlineData("hello", "*")]
        [InlineData("hello", "hello")]
        public void ctor_shouldCreateFromUriAndLocalName(string uri, string localName) {
            var qname = new QName(uri, localName);
            Assert.Equal(new Namespace(uri), qname.ns);
            Assert.Equal(localName, qname.localName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("*")]
        [InlineData("hello")]
        [InlineData("foo.bar")]
        [InlineData("foo::bar")]
        public void publicName_shouldCreatePublicQName(string localName) {
            var qname = QName.publicName(localName);
            Assert.True(qname.ns.isPublic);
            Assert.Equal(localName, qname.localName);
        }

        public static IEnumerable<object[]> parse_shouldParseStringToQName_data = new object[][] {
            new object[] { null, default(QName) },
            new object[] { "", new QName("", "") },
            new object[] { "*", new QName(Namespace.any, "*") },
            new object[] { "a", QName.publicName("a") },
            new object[] { "hello", QName.publicName("hello") },

            new object[] { "a.b", new QName("a", "b") },
            new object[] { "a.", new QName("a", "") },
            new object[] { ".b", new QName("", "b") },
            new object[] { ".", new QName("", "") },
            new object[] { "a.b.c", new QName("a.b", "c") },
            new object[] { ".b.c", new QName(".b", "c") },
            new object[] { "a.b.", new QName("a.b", "") },
            new object[] { "*.b", new QName("*", "b") },
            new object[] { "a.*", new QName("a", "*") },

            new object[] { "a::b", new QName("a", "b") },
            new object[] { "a::", new QName("a", "") },
            new object[] { "::b", new QName("", "b") },
            new object[] { "*::b", new QName(Namespace.any, "b") },
            new object[] { "a::*", new QName("a", "*") },
            new object[] { "a::b::c", new QName("a::b", "c") },
            new object[] { "a::b::", new QName("a::b", "") },
            new object[] { "::b::c", new QName("::b", "c") },
            new object[] { "a::b.c", new QName("a", "b.c") },
            new object[] { "a.b::c", new QName("a.b", "c") },

            new object[] { "a.<b>", new QName("", "a.<b>") },
            new object[] { "a.<b.<c,d>>", new QName("", "a.<b.<c,d>>") },
            new object[] { "x.y.a.<b.<c,d>>", new QName("x.y", "a.<b.<c,d>>") },
            new object[] { "x.y::a.<b.<c,d>>", new QName("x.y", "a.<b.<c,d>>") },
            new object[] { "x.y.a.<b::<c,d>>", new QName("x.y.a.<b", "<c,d>>") },
        };

        [Theory]
        [MemberData(nameof(parse_shouldParseStringToQName_data))]
        public void parse_shouldParseStringToQName(string str, QName expected) {
            var qname = QName.parse(str);
            Assert.Equal(expected.ns, qname.ns);
            Assert.Equal(expected.localName, qname.localName);
        }

        public static IEnumerable<object[]> equals_shouldCheckForQNameEquality_data = new object[][] {
            new object[] { default(QName), new QName(Namespace.any, null), true },
            new object[] { new QName(Namespace.any, "hello"), new QName(Namespace.any, "hello"), true },
            new object[] { QName.publicName("hello"), new QName(Namespace.@public, "hello"), true },
            new object[] { new QName("a", "b"), new QName("a", "b"), true },
            new object[] {
                new QName(Namespace.createPrivate(3), ""),
                new QName(Namespace.createPrivate(3), ""),
                true
            },
            new object[] {
                new QName(new Namespace(NamespaceKind.PROTECTED, "foo"), "bar"),
                new QName(new Namespace(NamespaceKind.PROTECTED, "foo"), "bar"),
                true
            },

            new object[] { default(QName), new QName("*", null), false },
            new object[] { new QName(Namespace.any, "hello"), new QName("", "hello"), false },
            new object[] { new QName("a", "b"), new QName("a", "c"), false },
            new object[] { new QName("a", "b"), new QName("c", "b"), false },
            new object[] { new QName("a", "*"), new QName("a", "c"), false },
            new object[] { new QName("*", "b"), new QName("a", "b"), false },
            new object[] {
                new QName(Namespace.createPrivate(2), "a"),
                new QName(Namespace.createPrivate(3), "a"),
                false
            },
            new object[] {
                new QName(Namespace.createPrivate(2), "a"),
                new QName(Namespace.createPrivate(2), "b"),
                false
            },
            new object[] {
                new QName(new Namespace(NamespaceKind.PROTECTED, "foo"), "bar"),
                new QName(new Namespace(NamespaceKind.PROTECTED, "foo"), "foo"),
                false
            },
            new object[] {
                new QName(new Namespace(NamespaceKind.PROTECTED, "foo"), "bar"),
                new QName(new Namespace(NamespaceKind.STATIC_PROTECTED, "foo"), "bar"),
                false
            },
            new object[] {
                new QName(new Namespace(NamespaceKind.PROTECTED, "foo"), "bar"),
                new QName(new Namespace(NamespaceKind.PROTECTED, "bar"), "foo"),
                false
            },
            new object[] {
                new QName(Namespace.createPrivate(2), "a"),
                new QName(new Namespace(NamespaceKind.PROTECTED, ""), "a"),
                false
            },
            new object[] {
                new QName(Namespace.any, "a"),
                new QName(new Namespace(NamespaceKind.PROTECTED, ""), "a"),
                false
            },
        };

        [Theory]
        [MemberData(nameof(equals_shouldCheckForQNameEquality_data))]
        public void equals_shouldCheckForQNameEquality(QName x, QName y, bool areEqual) {
            Assert.Equal(areEqual, x.Equals(y));
            Assert.Equal(areEqual, y.Equals(x));
            Assert.Equal(areEqual, x.Equals((object)y));
            Assert.Equal(areEqual, y.Equals((object)x));
            Assert.Equal(areEqual, x == y);
            Assert.Equal(areEqual, y == x);
            Assert.Equal(!areEqual, x != y);
            Assert.Equal(!areEqual, y != x);
        }

        [Fact]
        public void equals_shouldNotEqualOtherObject() {
            Assert.False(default(QName).Equals(null));
            Assert.False((new QName("", "a")).Equals(null));
            Assert.False((new QName("", "a")).Equals(new object()));
            Assert.False((new QName("", "a")).Equals(new ASQName("", "a")));
        }

        [Theory]
        [MemberData(nameof(equals_shouldCheckForQNameEquality_data))]
        public void getHashCode_shouldReturnSameHashCodeForEqual(QName x, QName y, bool areEqual) {
            if (areEqual)
                Assert.Equal(x.GetHashCode(), y.GetHashCode());
        }

        public static IEnumerable<object[]> toString_shouldReturnStringRepr_data = new object[][] {
            new object[] { default(QName), "*::null" },
            new object[] { new QName(Namespace.any, "*"), "*::*" },
            new object[] { QName.publicName("foo"), "foo" },

            new object[] { new QName("", "bar"), "bar" },
            new object[] { new QName("", null), "null" },
            new object[] { new QName("", "bar.foo"), "bar.foo" },
            new object[] { new QName("", "bar::foo"), "bar::foo" },
            new object[] { new QName("a", "b"), "a::b" },
            new object[] { new QName("a", null), "a::null" },
            new object[] { new QName("a", "*"), "a::*" },
            new object[] { new QName("a::b", "c"), "a::b::c" },
            new object[] { new QName("a", "b::c"), "a::b::c" },

            new object[] { new QName(new Namespace(NamespaceKind.PACKAGE_INTERNAL, "foo"), "bar"), "<internal foo>::bar" },
            new object[] { new QName(Namespace.createPrivate(1), "bar"), "<private #1>::bar" },
        };

        [Theory]
        [MemberData(nameof(toString_shouldReturnStringRepr_data))]
        public void toString_shouldReturnStringRepr(QName qname, string str) {
            Assert.Equal(str, qname.ToString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("*")]
        [InlineData("hello")]
        [InlineData("foo.bar")]
        [InlineData("foo::bar")]
        public void implicitFromString_shouldCreatePublicQName(string localName) {
            QName qname = localName;
            Assert.True(qname.ns.isPublic);
            Assert.Equal(localName, qname.localName);
        }

        public static IEnumerable<object[]> fromObject_shouldCreateQNameFromASObject_data = new (ASObject, QName)[] {
            (1, QName.publicName("1")),
            (true, QName.publicName("true")),
            ("*", QName.publicName("*")),
            (null, default(QName)),
            ("abc", QName.publicName("abc")),
            ("a.b", QName.publicName("a.b")),
            ("a::b", QName.publicName("a::b")),
            (new ASQName("abc"), QName.publicName("abc")),
            (new ASQName("ab.c"), QName.publicName("ab.c")),
            (new ASQName("ab::c"), QName.publicName("ab::c")),
            (new ASQName("a", "b"), new QName("a", "b")),
            (new ASQName("a", "b", "c"), new QName("b", "c")),
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(fromObject_shouldCreateQNameFromASObject_data))]
        public void fromObject_shouldCreateQNameFromASObject(ASObject obj, QName expected) {
            Assert.Equal(expected, QName.fromObject(obj));
        }

        public static IEnumerable<object[]> fromASQName_shouldCreateQNameFromASQName_data = new (ASQName, QName)[] {
            (null, default(QName)),
            (new ASQName("abc"), QName.publicName("abc")),
            (new ASQName("ab.c"), QName.publicName("ab.c")),
            (new ASQName("ab::c"), QName.publicName("ab::c")),
            (new ASQName("a", "b"), new QName("a", "b")),
            (new ASQName("a", "b", "c"), new QName("b", "c")),
        }.Select(x => new object[] {x.Item1, x.Item2});

        [Theory]
        [MemberData(nameof(fromASQName_shouldCreateQNameFromASQName_data))]
        public void fromASQName_shouldCreateQNameFromASQName(ASQName obj, QName expected) {
            Assert.Equal(expected, QName.fromASQName(obj));
        }
    }

}
