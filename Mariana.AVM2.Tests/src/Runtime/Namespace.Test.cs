using System;
using System.Collections.Generic;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class NamespaceTest {

        [Fact]
        public void anyNamespace_shouldHaveKindAnyAndUriNull() {
            Assert.Equal(NamespaceKind.ANY, Namespace.any.kind);
            Assert.Equal(NamespaceKind.ANY, default(Namespace).kind);
            Assert.Null(Namespace.any.uri);
            Assert.Null(default(Namespace).uri);
        }

        [Fact]
        public void publicNamespace_shouldHaveKindNamespaceAndUriEmpty() {
            Assert.Equal(NamespaceKind.NAMESPACE, Namespace.@public.kind);
            Assert.Equal("", Namespace.@public.uri);
        }

        [Fact]
        public void singleArgCtor_shouldCreateAnyNamespaceWthNullUri() {
            var ns = new Namespace(null);
            Assert.Equal(NamespaceKind.ANY, ns.kind);
            Assert.Null(ns.uri);
        }

        [Theory]
        [InlineData("")]
        [InlineData("*")]
        [InlineData("hello")]
        public void singleArgCtor_shouldCreateNamespaceWithUri(string uri) {
            var ns = new Namespace(uri);
            Assert.Equal(NamespaceKind.NAMESPACE, ns.kind);
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
        public void ctor_shouldCreateNamespaceWithKindAndUri(NamespaceKind kind, string uri) {
            var ns = new Namespace(kind, uri);
            Assert.Equal(kind, ns.kind);
            Assert.Equal(uri, ns.uri);
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
        internal void ctor_shouldThrowOnInvalidKind(NamespaceKind kind, string uri, ErrorCode errCode) {
            var exc = Assert.Throws<AVM2Exception>(() => { new Namespace(kind, uri); });
            Assert.Equal(errCode, (ErrorCode)((ASError)exc.thrownValue).errorID);
        }

        [Theory]
        [InlineData(NamespaceKind.NAMESPACE, null)]
        [InlineData(NamespaceKind.EXPLICIT, null)]
        [InlineData(NamespaceKind.PACKAGE_INTERNAL, null)]
        [InlineData(NamespaceKind.PROTECTED, null)]
        [InlineData(NamespaceKind.STATIC_PROTECTED, null)]
        internal void ctor_shouldThrowOnNullUri(NamespaceKind kind, string uri) {
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__NAMESPACE_NULL_NAME, () => new Namespace(kind, uri));
        }

        [Fact]
        public void createPrivate_shouldCreatePrivateNamespace() {
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

            ns1 = Namespace.createPrivate(0);
            ns2 = Namespace.createPrivate(10);

            Assert.Equal(0, ns1.privateNamespaceId);
            Assert.Equal(10, ns2.privateNamespaceId);

            Assert.Equal(NamespaceKind.PRIVATE, ns1.kind);
            Assert.Equal(NamespaceKind.PRIVATE, ns2.kind);

            Assert.Null(ns1.uri);
            Assert.Null(ns2.uri);
        }

        [Fact]
        public void privateNamespaceId_shouldGetId() {
            Assert.Equal(0, Namespace.createPrivate(0).privateNamespaceId);
            Assert.Equal(10, Namespace.createPrivate(10).privateNamespaceId);
            Assert.Equal(-1, Namespace.any.privateNamespaceId);
            Assert.Equal(-1, Namespace.@public.privateNamespaceId);
            Assert.Equal(-1, (new Namespace("a")).privateNamespaceId);
            Assert.Equal(-1, (new Namespace(NamespaceKind.PACKAGE_INTERNAL, "a")).privateNamespaceId);
        }

        [Fact]
        public void createPrivate_shouldThrowWhenIdExceedsMaxValue() {
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__PRIVATE_NS_LIMIT_EXCEEDED, () => Namespace.createPrivate(-1));
            AssertHelper.throwsErrorWithCode(ErrorCode.MARIANA__PRIVATE_NS_LIMIT_EXCEEDED, () => Namespace.createPrivate(0xFFFFFFF + 1));
        }

        [Fact]
        public void isPublic_shouldReturnTrueForPublicNamespace() {
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

        public static IEnumerable<object[]> equals_shouldCheckForNamespaceEquality_data = new object[][] {
            new object[] { Namespace.any, Namespace.any, true },
            new object[] { new Namespace(""), Namespace.@public, true },
            new object[] { new Namespace("hello"), new Namespace("hello"), true },
            new object[] { new Namespace("hello"), new Namespace(NamespaceKind.NAMESPACE, "hello"), true },
            new object[] {
                new Namespace(NamespaceKind.PROTECTED, "hello"),
                new Namespace(NamespaceKind.PROTECTED, "hello"),
                true
            },
            new object[] { Namespace.createPrivate(0), Namespace.createPrivate(0), true },
            new object[] { Namespace.createPrivate(100), Namespace.createPrivate(100), true },

            new object[] { new Namespace(""), Namespace.any, false },
            new object[] { new Namespace(""), new Namespace("hello"), false },
            new object[] { new Namespace("world"), new Namespace("hello"), false },
            new object[] { new Namespace("world"), Namespace.any, false },
            new object[] { new Namespace("hello"), new Namespace(NamespaceKind.EXPLICIT, "hello"), false },
            new object[] {
                new Namespace(NamespaceKind.PACKAGE_INTERNAL, "hello"),
                new Namespace(NamespaceKind.EXPLICIT, "hello"),
                false
            },
            new object[] {
                new Namespace(NamespaceKind.PACKAGE_INTERNAL, "hello"),
                new Namespace(NamespaceKind.EXPLICIT, "world"),
                false
            },
            new object[] { Namespace.any, Namespace.createPrivate(0), false },
            new object[] { new Namespace("hello"), Namespace.createPrivate(0), false },
            new object[] { Namespace.createPrivate(0), Namespace.createPrivate(1), false },
            new object[] { Namespace.createPrivate(), Namespace.createPrivate(), false },
        };

        [Theory]
        [MemberData(nameof(equals_shouldCheckForNamespaceEquality_data))]
        public void equals_shouldCheckForNamespaceEquality(Namespace ns1, Namespace ns2, bool areEqual) {
            Assert.Equal(areEqual, ns1.Equals(ns2));
            Assert.Equal(areEqual, ns2.Equals(ns1));
            Assert.Equal(areEqual, ns1.Equals((object)ns2));
            Assert.Equal(areEqual, ns2.Equals((object)ns1));
            Assert.Equal(areEqual, ns1 == ns2);
            Assert.Equal(areEqual, ns2 == ns1);
            Assert.Equal(!areEqual, ns1 != ns2);
            Assert.Equal(!areEqual, ns2 != ns1);
        }

        [Theory]
        [MemberData(nameof(equals_shouldCheckForNamespaceEquality_data))]
        public void getHashCode_shouldReturnSameHashCodeForEqual(Namespace ns1, Namespace ns2, bool areEqual) {
            if (areEqual)
                Assert.Equal(ns1.GetHashCode(), ns2.GetHashCode());
        }

        [Fact]
        public void equals_shouldNotEqualOtherObject() {
            Assert.False((new Namespace("hello")).Equals((object)"hello"));
            Assert.False((new Namespace("hello")).Equals((object)null));
            Assert.False((new Namespace("hello")).Equals(new object()));
        }

        [Fact]
        public void fromASNamespace_shouldCreateNamespaceWithSameUri() {
            Assert.Equal(Namespace.any, Namespace.fromASNamespace(null));
            Assert.Equal(Namespace.@public, Namespace.fromASNamespace(new ASNamespace("")));
            Assert.Equal(new Namespace("hello"), Namespace.fromASNamespace(new ASNamespace("hello")));
            Assert.Equal(new Namespace("hello"), Namespace.fromASNamespace(new ASNamespace("p", "hello")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("*")]
        [InlineData("hello")]
        public void implicitConvFromString_shouldCreateNamespaceWithUri(string s) {
            Namespace ns = s;
            Assert.Equal((s == null) ? NamespaceKind.ANY : NamespaceKind.NAMESPACE, ns.kind);
            Assert.Equal(s, ns.uri);
        }

        public static IEnumerable<object[]> toString_shouldReturnStringRepr_data = new object[][] {
            new object[] { Namespace.any, "*" },
            new object[] { Namespace.@public, "" },
            new object[] { new Namespace("hello"), "hello" },
            new object[] { new Namespace("*"), "*" },
            new object[] { new Namespace(NamespaceKind.NAMESPACE, "hello"), "hello" },
            new object[] { new Namespace(NamespaceKind.EXPLICIT, ""), "<explicit >" },
            new object[] { new Namespace(NamespaceKind.EXPLICIT, "hello"), "<explicit hello>" },
            new object[] { new Namespace(NamespaceKind.PACKAGE_INTERNAL, "hello"), "<internal hello>" },
            new object[] { new Namespace(NamespaceKind.PROTECTED, "hello"), "<protected hello>" },
            new object[] { new Namespace(NamespaceKind.STATIC_PROTECTED, "hello"), "<static protected hello>" },
            new object[] { Namespace.createPrivate(0), "<private #0>" },
            new object[] { Namespace.createPrivate(100), "<private #100>" },
        };

        [Theory]
        [MemberData(nameof(toString_shouldReturnStringRepr_data))]
        public void toString_shouldReturnStringRepr(Namespace ns, string expected) {
            Assert.Equal(expected, ns.ToString());
        }

    }

}
