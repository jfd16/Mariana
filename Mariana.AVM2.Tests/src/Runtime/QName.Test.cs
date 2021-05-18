using System;
using System.Collections.Generic;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class QNameTest {

        public static IEnumerable<object[]> constructorTest_withNamespaceAndLocalName_data = TupleHelper.toArrays(
            (Namespace.any, null),
            (Namespace.@public, null),
            (new Namespace("hello"), null),
            (new Namespace(NamespaceKind.PACKAGE_INTERNAL, "hello"), null),
            (Namespace.createPrivate(1), null),
            (Namespace.any, "*"),
            (Namespace.@public, "*"),
            (new Namespace("hello"), "*"),
            (new Namespace(NamespaceKind.PACKAGE_INTERNAL, "hello"), "*"),
            (Namespace.createPrivate(1), "*"),
            (Namespace.any, "abc"),
            (Namespace.@public, "abc"),
            (new Namespace("hello"), "abc"),
            (new Namespace(NamespaceKind.PACKAGE_INTERNAL, "hello"), "abc"),
            (Namespace.createPrivate(1), "abc")
        );

        [Theory]
        [MemberData(nameof(constructorTest_withNamespaceAndLocalName_data))]
        public void constructorTest_withNamespaceAndLocalName(Namespace ns, string localName) {
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
        public void constructorTest_withUriAndLocalName(string uri, string localName) {
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
        public void publicNameMethodTest(string localName) {
            var qname = QName.publicName(localName);
            Assert.True(qname.ns.isPublic);
            Assert.Equal(localName, qname.localName);
        }

        public static IEnumerable<object[]> parseMethodTest_data = TupleHelper.toArrays(
            (null, default(QName)),
            ("", new QName("", "")),
            ("*", new QName(Namespace.any, "*")),
            ("a", QName.publicName("a")),
            ("hello", QName.publicName("hello")),

            ("a.b", new QName("a", "b")),
            ("a.", new QName("a", "")),
            (".b", new QName("", "b")),
            (".", new QName("", "")),
            ("a.b.c", new QName("a.b", "c")),
            (".b.c", new QName(".b", "c")),
            ("a.b.", new QName("a.b", "")),
            ("*.b", new QName("*", "b")),
            ("a.*", new QName("a", "*")),

            ("a::b", new QName("a", "b")),
            ("a::", new QName("a", "")),
            ("::b", new QName("", "b")),
            ("*::b", new QName(Namespace.any, "b")),
            ("a::*", new QName("a", "*")),
            ("a::b::c", new QName("a::b", "c")),
            ("a::b::", new QName("a::b", "")),
            ("::b::c", new QName("::b", "c")),
            ("a::b.c", new QName("a", "b.c")),
            ("a.b::c", new QName("a.b", "c")),

            ("a.<b>", new QName("", "a.<b>")),
            ("a.<b.<c,d>>", new QName("", "a.<b.<c,d>>")),
            ("x.y.a.<b.<c,d>>", new QName("x.y", "a.<b.<c,d>>")),
            ("x.y::a.<b.<c,d>>", new QName("x.y", "a.<b.<c,d>>")),
            ("x.y.a.<b::<c,d>>", new QName("x.y.a.<b", "<c,d>>"))
        );

        [Theory]
        [MemberData(nameof(parseMethodTest_data))]
        public void parseMethodTest(string str, QName expected) {
            var qname = QName.parse(str);
            Assert.Equal(expected.ns, qname.ns);
            Assert.Equal(expected.localName, qname.localName);
        }

        public static IEnumerable<object[]> equals_getHashCode_testData = TupleHelper.toArrays(
            (default(QName), new QName(Namespace.any, null), true),
            (new QName(Namespace.any, "hello"), new QName(Namespace.any, "hello"), true),
            (QName.publicName("hello"), new QName(Namespace.@public, "hello"), true),
            (new QName("a", "b"), new QName("a", "b"), true),
            (
                new QName(Namespace.createPrivate(3), ""),
                new QName(Namespace.createPrivate(3), ""),
                true
            ),
            (
                new QName(new Namespace(NamespaceKind.PROTECTED, "foo"), "bar"),
                new QName(new Namespace(NamespaceKind.PROTECTED, "foo"), "bar"),
                true
            ),

            (default(QName), new QName("*", null), false),
            (new QName(Namespace.any, "hello"), new QName("", "hello"), false),
            (new QName("a", "b"), new QName("a", "c"), false),
            (new QName("a", "b"), new QName("c", "b"), false),
            (new QName("a", "*"), new QName("a", "c"), false),
            (new QName("*", "b"), new QName("a", "b"), false),
            (
                new QName(Namespace.createPrivate(2), "a"),
                new QName(Namespace.createPrivate(3), "a"),
                false
            ),
            (
                new QName(Namespace.createPrivate(2), "a"),
                new QName(Namespace.createPrivate(2), "b"),
                false
            ),
            (
                new QName(new Namespace(NamespaceKind.PROTECTED, "foo"), "bar"),
                new QName(new Namespace(NamespaceKind.PROTECTED, "foo"), "foo"),
                false
            ),
            (
                new QName(new Namespace(NamespaceKind.PROTECTED, "foo"), "bar"),
                new QName(new Namespace(NamespaceKind.STATIC_PROTECTED, "foo"), "bar"),
                false
            ),
            (
                new QName(new Namespace(NamespaceKind.PROTECTED, "foo"), "bar"),
                new QName(new Namespace(NamespaceKind.PROTECTED, "bar"), "foo"),
                false
            ),
            (
                new QName(Namespace.createPrivate(2), "a"),
                new QName(new Namespace(NamespaceKind.PROTECTED, ""), "a"),
                false
            ),
            (
                new QName(Namespace.any, "a"),
                new QName(new Namespace(NamespaceKind.PROTECTED, ""), "a"),
                false
            )
        );

        [Theory]
        [MemberData(nameof(equals_getHashCode_testData))]
        public void equalsMethodTest(QName x, QName y, bool areEqual) {
            Assert.Equal(areEqual, x.Equals(y));
            Assert.Equal(areEqual, y.Equals(x));
            Assert.Equal(areEqual, x.Equals((object)y));
            Assert.Equal(areEqual, y.Equals((object)x));
        }

        [Theory]
        [MemberData(nameof(equals_getHashCode_testData))]
        public void equalsOperatorTest(QName x, QName y, bool areEqual) {
            Assert.Equal(areEqual, x == y);
            Assert.Equal(areEqual, y == x);
            Assert.Equal(!areEqual, x != y);
            Assert.Equal(!areEqual, y != x);
        }

        [Fact]
        public void equalsMethodTest_withNonQNameObject() {
            Assert.False(default(QName).Equals(null));
            Assert.False((new QName("", "a")).Equals(null));
            Assert.False((new QName("", "a")).Equals(new object()));
            Assert.False((new QName("", "a")).Equals(new ASQName("", "a")));
        }

        [Theory]
        [MemberData(nameof(equals_getHashCode_testData))]
        public void getHashCodeMethodTest(QName x, QName y, bool areEqual) {
            if (areEqual)
                Assert.Equal(x.GetHashCode(), y.GetHashCode());
        }

        public static IEnumerable<object[]> toStringMethodTest_data = TupleHelper.toArrays(
            (default(QName), "*::null"),
            (new QName(Namespace.any, "*"), "*::*"),
            (QName.publicName("foo"), "foo"),

            (new QName("", "bar"), "bar"),
            (new QName("", null), "null"),
            (new QName("", "bar.foo"), "bar.foo"),
            (new QName("", "bar::foo"), "bar::foo"),
            (new QName("a", "b"), "a::b"),
            (new QName("a", null), "a::null"),
            (new QName("a", "*"), "a::*"),
            (new QName("a::b", "c"), "a::b::c"),
            (new QName("a", "b::c"), "a::b::c"),

            (new QName(new Namespace(NamespaceKind.PACKAGE_INTERNAL, "foo"), "bar"), "<internal foo>::bar"),
            (new QName(Namespace.createPrivate(1), "bar"), "<private #1>::bar")
        );

        [Theory]
        [MemberData(nameof(toStringMethodTest_data))]
        public void toStringMethodTest(QName qname, string expectedString) {
            Assert.Equal(expectedString, qname.ToString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("*")]
        [InlineData("hello")]
        [InlineData("foo.bar")]
        [InlineData("foo::bar")]
        public void implicitConvFromStringTest(string localName) {
            QName qname = localName;
            Assert.True(qname.ns.isPublic);
            Assert.Equal(localName, qname.localName);
        }

        public static IEnumerable<object[]> fromObjectMethodTest_data = TupleHelper.toArrays<ASObject, QName>(
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
            (new ASQName("a", "b", "c"), new QName("b", "c"))
        );

        [Theory]
        [MemberData(nameof(fromObjectMethodTest_data))]
        public void fromObjectMethodTest(ASObject obj, QName expected) {
            Assert.Equal(expected, QName.fromObject(obj));
        }

        public static IEnumerable<object[]> fromASQNameMethodTest_data = TupleHelper.toArrays(
            (null, default(QName)),
            (new ASQName("abc"), QName.publicName("abc")),
            (new ASQName("ab.c"), QName.publicName("ab.c")),
            (new ASQName("ab::c"), QName.publicName("ab::c")),
            (new ASQName("a", "b"), new QName("a", "b")),
            (new ASQName("a", "b", "c"), new QName("b", "c"))
        );

        [Theory]
        [MemberData(nameof(fromASQNameMethodTest_data))]
        public void fromASQNameMethodTest(ASQName obj, QName expected) {
            Assert.Equal(expected, QName.fromASQName(obj));
        }
    }

}
