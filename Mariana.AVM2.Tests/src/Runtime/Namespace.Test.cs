using System;
using System.Collections.Generic;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class NamespaceTest {

        [Fact]
        public void anyNamespaceShouldHaveKindAnyAndUriNull() {
            Assert.Equal(NamespaceKind.ANY, Namespace.any.kind);
            Assert.Equal(NamespaceKind.ANY, default(Namespace).kind);
            Assert.Null(Namespace.any.uri);
            Assert.Null(default(Namespace).uri);
        }

        [Fact]
        public void publicNamespaceShouldHaveKindNamespaceAndUriEmpty() {
            Assert.Equal(NamespaceKind.NAMESPACE, Namespace.@public.kind);
            Assert.Equal("", Namespace.@public.uri);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("*")]
        [InlineData("hello")]
        public void singleArgCtor_shouldCreateNamespaceWithUri(string uri) {
            var ns = new Namespace(uri);
            Assert.Equal((uri != null) ? NamespaceKind.NAMESPACE : NamespaceKind.ANY, ns.kind);
            Assert.Equal(uri, ns.uri);
        }

        [Theory]
        [InlineData(NamespaceKind.NAMESPACE, "")]
        [InlineData(NamespaceKind.NAMESPACE, "hello")]
        [InlineData(NamespaceKind.EXPLICIT, "")]
        [InlineData(NamespaceKind.EXPLICIT, "hello")]
        [InlineData(NamespaceKind.PACKAGE_INTERNAL, "")]
        [InlineData(NamespaceKind.PACKAGE_INTERNAL, "hello")]
        [InlineData(NamespaceKind.PROTECTED, "")]
        [InlineData(NamespaceKind.PROTECTED, "hello")]
        [InlineData(NamespaceKind.STATIC_PROTECTED, "")]
        [InlineData(NamespaceKind.STATIC_PROTECTED, "hello")]
        public void constructorTest_withKindAndUri(NamespaceKind kind, string uri) {
            var ns = new Namespace(kind, uri);
            Assert.Equal(kind, ns.kind);
            Assert.Equal(uri, ns.uri);
        }

        [Theory]
        [InlineData(NamespaceKind.NAMESPACE, null)]
        [InlineData(NamespaceKind.EXPLICIT, null)]
        [InlineData(NamespaceKind.PACKAGE_INTERNAL, null)]
        [InlineData(NamespaceKind.PROTECTED, null)]
        [InlineData(NamespaceKind.STATIC_PROTECTED, null)]
        internal void constructorTest_withKindAndNullUri(NamespaceKind kind, string uri) {
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__NAMESPACE_NULL_NAME, () => new Namespace(kind, uri));
        }

        [Theory]
        [InlineData(NamespaceKind.PRIVATE, null, ErrorCode.MARIANA__NAMESPACE_CTOR_PRIVATE)]
        [InlineData(NamespaceKind.PRIVATE, "", ErrorCode.MARIANA__NAMESPACE_CTOR_PRIVATE)]
        [InlineData(NamespaceKind.PRIVATE, "hello", ErrorCode.MARIANA__NAMESPACE_CTOR_PRIVATE)]
        [InlineData(NamespaceKind.ANY, null, ErrorCode.MARIANA__NAMESPACE_CTOR_ANY)]
        [InlineData(NamespaceKind.ANY, "", ErrorCode.MARIANA__NAMESPACE_CTOR_ANY)]
        [InlineData(NamespaceKind.ANY, "hello", ErrorCode.MARIANA__NAMESPACE_CTOR_ANY)]
        [InlineData(unchecked((NamespaceKind)(-1)), null, ErrorCode.MARIANA__INVALID_NS_CATEGORY)]
        [InlineData(unchecked((NamespaceKind)(-1)), "", ErrorCode.MARIANA__INVALID_NS_CATEGORY)]
        [InlineData(unchecked((NamespaceKind)(-1)), "hello", ErrorCode.MARIANA__INVALID_NS_CATEGORY)]
        [InlineData((NamespaceKind)((int)NamespaceKind.PRIVATE + 1), null, ErrorCode.MARIANA__INVALID_NS_CATEGORY)]
        [InlineData((NamespaceKind)((int)NamespaceKind.PRIVATE + 1), "", ErrorCode.MARIANA__INVALID_NS_CATEGORY)]
        [InlineData((NamespaceKind)((int)NamespaceKind.PRIVATE + 1), "hello", ErrorCode.MARIANA__INVALID_NS_CATEGORY)]
        internal void constructorTest_withInvalidKind(NamespaceKind kind, string uri, ErrorCode errCode) {
            var exc = Assert.Throws<AVM2Exception>(() => new Namespace(kind, uri));
            Assert.Equal(errCode, (ErrorCode)((ASError)exc.thrownValue).errorID);
        }

        [Fact]
        public void createPrivateMethodTest() {
            var ns1 = Namespace.createPrivate();
            var ns2 = Namespace.createPrivate();
            var ns3 = Namespace.createPrivate();

            Assert.Equal(NamespaceKind.PRIVATE, ns1.kind);
            Assert.Equal(NamespaceKind.PRIVATE, ns2.kind);
            Assert.Equal(NamespaceKind.PRIVATE, ns3.kind);

            Assert.Null(ns1.uri);
            Assert.Null(ns2.uri);
            Assert.Null(ns3.uri);

            Assert.NotEqual(ns1.privateNamespaceId, ns2.privateNamespaceId);
            Assert.NotEqual(ns1.privateNamespaceId, ns3.privateNamespaceId);
            Assert.NotEqual(ns2.privateNamespaceId, ns3.privateNamespaceId);
        }

        [Fact]
        public void createPrivateMethodTest_withIdSpecified() {
            var ns1 = Namespace.createPrivate(0);
            var ns2 = Namespace.createPrivate(10);

            Assert.Equal(0, ns1.privateNamespaceId);
            Assert.Equal(10, ns2.privateNamespaceId);

            Assert.Equal(NamespaceKind.PRIVATE, ns1.kind);
            Assert.Equal(NamespaceKind.PRIVATE, ns2.kind);

            Assert.Null(ns1.uri);
            Assert.Null(ns2.uri);
        }

        [Fact]
        public void createPrivateMethodTest_idExceedsMaxValue() {
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__PRIVATE_NS_LIMIT_EXCEEDED, () => Namespace.createPrivate(-1));
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__PRIVATE_NS_LIMIT_EXCEEDED, () => Namespace.createPrivate(0xFFFFFFF + 1));
        }

        [Fact]
        public void privateNamespaceIdPropertyTest() {
            Assert.Equal(0, Namespace.createPrivate(0).privateNamespaceId);
            Assert.Equal(10, Namespace.createPrivate(10).privateNamespaceId);

            Assert.Equal(-1, Namespace.any.privateNamespaceId);
            Assert.Equal(-1, Namespace.@public.privateNamespaceId);
            Assert.Equal(-1, (new Namespace("a")).privateNamespaceId);
            Assert.Equal(-1, (new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a")).privateNamespaceId);
        }

        [Fact]
        public void isPublicPropertyTest() {
            Assert.True(Namespace.@public.isPublic);
            Assert.True((new Namespace("")).isPublic);
            Assert.True((new Namespace(NamespaceKind.NAMESPACE, "")).isPublic);
            Assert.False(Namespace.any.isPublic);
            Assert.False((new Namespace("hello")).isPublic);
            Assert.False((new Namespace(NamespaceKind.EXPLICIT, "")).isPublic);
            Assert.False((new Namespace(NamespaceKind.PACKAGE_INTERNAL, "")).isPublic);
            Assert.False((new Namespace(NamespaceKind.NAMESPACE, "hello")).isPublic);
            Assert.False((new Namespace(NamespaceKind.PROTECTED, "hello")).isPublic);
            Assert.False(Namespace.createPrivate(1).isPublic);
        }

        public static IEnumerable<object[]> equals_getHashCode_testData = TupleHelper.toArrays(
            (Namespace.any, Namespace.any, true),
            (new Namespace(""), Namespace.@public, true),
            (new Namespace("hello"), new Namespace("hello"), true),
            (new Namespace("hello"), new Namespace(NamespaceKind.NAMESPACE, "hello"), true),
            (new Namespace(NamespaceKind.PROTECTED, "hello"), new Namespace(NamespaceKind.PROTECTED, "hello"), true),
            (Namespace.createPrivate(0), Namespace.createPrivate(0), true),
            (Namespace.createPrivate(100), Namespace.createPrivate(100), true),
            (new Namespace(""), Namespace.any, false),
            (new Namespace(""), new Namespace("hello"), false),
            (new Namespace("world"), new Namespace("hello"), false),
            (new Namespace("world"), Namespace.any, false),
            (new Namespace("hello"), new Namespace(NamespaceKind.EXPLICIT, "hello"), false),
            (new Namespace(NamespaceKind.PACKAGE_INTERNAL, "hello"), new Namespace(NamespaceKind.EXPLICIT, "hello"), false),
            (new Namespace(NamespaceKind.PACKAGE_INTERNAL, "hello"), new Namespace(NamespaceKind.EXPLICIT, "world"), false),
            (Namespace.any, Namespace.createPrivate(0), false),
            (new Namespace("hello"), Namespace.createPrivate(0), false),
            (Namespace.createPrivate(0), Namespace.createPrivate(1), false),
            (Namespace.createPrivate(), Namespace.createPrivate(), false)
        );

        [Theory]
        [MemberData(nameof(equals_getHashCode_testData))]
        public void equalsMethodTest(Namespace ns1, Namespace ns2, bool areEqual) {
            Assert.Equal(areEqual, ns1.Equals(ns2));
            Assert.Equal(areEqual, ns2.Equals(ns1));
            Assert.Equal(areEqual, ns1.Equals((object)ns2));
            Assert.Equal(areEqual, ns2.Equals((object)ns1));
        }

        [Theory]
        [MemberData(nameof(equals_getHashCode_testData))]
        public void equalsOperatorTest(Namespace ns1, Namespace ns2, bool areEqual) {
            Assert.Equal(areEqual, ns1 == ns2);
            Assert.Equal(areEqual, ns2 == ns1);
            Assert.Equal(!areEqual, ns1 != ns2);
            Assert.Equal(!areEqual, ns2 != ns1);
        }

        [Theory]
        [MemberData(nameof(equals_getHashCode_testData))]
        public void getHashCodeMethodTest(Namespace ns1, Namespace ns2, bool areEqual) {
            if (areEqual)
                Assert.Equal(ns1.GetHashCode(), ns2.GetHashCode());
        }

        [Fact]
        public void equalsMethodTest_withNonNamespaceObject() {
            Assert.False((new Namespace("hello")).Equals((object)"hello"));
            Assert.False((new Namespace("hello")).Equals((object)null));
            Assert.False((new Namespace("hello")).Equals(new object()));
            Assert.False(Namespace.any.Equals((object)null));
        }

        public static IEnumerable<object[]> fromASNamespaceMethodTest_data = TupleHelper.toArrays(
            (null, Namespace.any),
            (new ASNamespace(""), Namespace.@public),
            (new ASNamespace("hello"), new Namespace("hello")),
            (new ASNamespace("p", "hello"), new Namespace("hello"))
        );

        [Theory]
        [MemberData(nameof(fromASNamespaceMethodTest_data))]
        public void fromASNamespaceMethodTest(ASNamespace namespaceObj, Namespace expected) {
            Assert.Equal(expected, Namespace.fromASNamespace(namespaceObj));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("*")]
        [InlineData("hello")]
        public void implicitConvFromStringTest(string strToConvert) {
            Namespace ns = strToConvert;
            Assert.Equal((strToConvert == null) ? NamespaceKind.ANY : NamespaceKind.NAMESPACE, ns.kind);
            Assert.Equal(strToConvert, ns.uri);
        }

        public static IEnumerable<object[]> toStringMethodTest_data = TupleHelper.toArrays(
            (Namespace.any, "*"),
            (Namespace.@public, ""),
            (new Namespace("hello"), "hello"),
            (new Namespace("*"), "*"),
            (new Namespace(NamespaceKind.NAMESPACE, "hello"), "hello"),
            (new Namespace(NamespaceKind.EXPLICIT, ""), "<explicit >"),
            (new Namespace(NamespaceKind.EXPLICIT, "hello"), "<explicit hello>"),
            (new Namespace(NamespaceKind.PACKAGE_INTERNAL, "hello"), "<internal hello>"),
            (new Namespace(NamespaceKind.PROTECTED, "hello"), "<protected hello>"),
            (new Namespace(NamespaceKind.STATIC_PROTECTED, "hello"), "<static protected hello>"),
            (Namespace.createPrivate(0), "<private #0>"),
            (Namespace.createPrivate(100), "<private #100>")
        );

        [Theory]
        [MemberData(nameof(toStringMethodTest_data))]
        public void toStringMethodTest(Namespace ns, string expected) {
            Assert.Equal(expected, ns.ToString());
        }

    }

}
