using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;

namespace Mariana.AVM2.Tests {

    public class ASQNameTest {

        private static readonly Class s_klass = Class.fromType<ASQName>();

        [Fact]
        public void anyName_shouldHaveNullNamespaceAndAnyLocalName() {
            Assert.Null(ASQName.any.uri);
            Assert.Null(ASQName.any.prefix);
            Assert.Equal("*", ASQName.any.localName);
        }

        [Theory]
        [InlineData(null, "null")]
        [InlineData("", "")]
        [InlineData("*", "*")]
        [InlineData("a", "a")]
        [InlineData("abc", "abc")]
        public void anyNamespace_shouldHaveNullNamespace(string localName, string expectedLocalName) {
            var qname = ASQName.anyNamespace(localName);
            Assert.Null(qname.uri);
            Assert.Null(qname.prefix);
            Assert.Equal(expectedLocalName, qname.localName);
        }

        [Theory]
        [InlineData(null, "", "a", "null")]
        [InlineData("", "", "a", "")]
        [InlineData("*", null, null, "*")]
        [InlineData("b", "", "a", "b")]
        public void constructor_shouldCreateFromLocalName(
            string localName, string expectedPrefix, string expectedUri, string expectedLocalName)
        {
            var oldDefault = ASNamespace.getDefault();
            ASNamespace.setDefault(new ASNamespace("a"));

            try {
                var qname = new ASQName(localName);
                Assert.Equal(expectedPrefix, qname.prefix);
                Assert.Equal(expectedLocalName, qname.localName);
                Assert.Equal(expectedUri, qname.uri);
            }
            finally {
                ASNamespace.setDefault(oldDefault);
            }
        }

        [Theory]
        [InlineData(null, null, null, null, "null")]
        [InlineData(null, "", null, null, "")]
        [InlineData(null, "a", null, null, "a")]
        [InlineData(null, "*", null, null, "*")]
        [InlineData("", null, "", "", "null")]
        [InlineData("", "", "", "", "")]
        [InlineData("", "a", "", "", "a")]
        [InlineData("", "*", "", "", "*")]
        [InlineData("a", null, null, "a", "null")]
        [InlineData("a", "", null, "a", "")]
        [InlineData("a", "a", null, "a", "a")]
        [InlineData("a", "*", null, "a", "*")]
        [InlineData("b", null, null, "b", "null")]
        [InlineData("b", "", null, "b", "")]
        [InlineData("b", "a", null, "b", "a")]
        [InlineData("b", "*", null, "b", "*")]
        [InlineData("*", null, null, "*", "null")]
        [InlineData("*", "", null, "*", "")]
        [InlineData("*", "a", null, "*", "a")]
        [InlineData("*", "*", null, "*", "*")]
        public void constructor_shouldCreateFromUriAndLocalName(
            string uri, string localName, string expectedPrefix, string expectedUri, string expectedLocalName)
        {
            var oldDefault = ASNamespace.getDefault();
            ASNamespace.setDefault(new ASNamespace("a"));

            try {
                var qname = new ASQName(uri, localName);
                Assert.Equal(expectedPrefix, qname.prefix);
                Assert.Equal(expectedLocalName, qname.localName);
                Assert.Equal(expectedUri, qname.uri);
            }
            finally {
                ASNamespace.setDefault(oldDefault);
            }
        }

        public static IEnumerable<object[]> constructor_shouldCreateFromPrefixUriAndLocalName_data =
            TupleHelper.toArrays(
                (null, null, null, null, null, "null"),
                (null, null, "", null, null, ""),
                (null, null, "a", null, null, "a"),
                (null, null, "*", null, null, "*"),
                ("", null, null, null, null, "null"),
                ("", null, "", null, null, ""),
                ("", null, "a", null, null, "a"),
                ("", null, "*", null, null, "*"),
                ("a", null, null, null, null, "null"),
                ("a", null, "", null, null, ""),
                ("a", null, "a", null, null, "a"),
                ("a", null, "*", null, null, "*"),
                (null, "", null, "", "", "null"),
                (null, "", "", "", "", ""),
                (null, "", "a", "", "", "a"),
                (null, "", "*", "", "", "*"),
                ("", "", null, "", "", "null"),
                ("", "", "", "", "", ""),
                ("", "", "a", "", "", "a"),
                ("", "", "*", "", "", "*"),
                (null, "b", null, null, "b", "null"),
                (null, "b", "", null, "b", ""),
                (null, "b", "a", null, "b", "a"),
                (null, "b", "*", null, "b", "*"),
                (null, "*", null, null, "*", "null"),
                (null, "*", "", null, "*", ""),
                (null, "*", "a", null, "*", "a"),
                (null, "*", "*", null, "*", "*"),
                ("", "b", null, "", "b", "null"),
                ("", "b", "", "", "b", ""),
                ("", "b", "a", "", "b", "a"),
                ("", "b", "*", "", "b", "*"),
                ("c", "b", null, "c", "b", "null"),
                ("c", "b", "", "c", "b", ""),
                ("c", "b", "a", "c", "b", "a"),
                ("c", "b", "*", "c", "b", "*"),
                ("1", "b", null, null, "b", "null"),
                ("1", "b", "", null, "b", ""),
                ("1", "b", "a", null, "b", "a"),
                ("1", "b", "*", null, "b", "*")
            );

        [Theory]
        [MemberData(nameof(constructor_shouldCreateFromPrefixUriAndLocalName_data))]
        public void constructor_shouldCreateFromPrefixUriAndLocalName(
            string prefix, string uri, string localName, string expectedPrefix, string expectedUri, string expectedLocalName)
        {
            var qname = new ASQName(prefix, uri, localName);
            Assert.Equal(expectedPrefix, qname.prefix);
            Assert.Equal(expectedLocalName, qname.localName);
            Assert.Equal(expectedUri, qname.uri);
        }

        public static IEnumerable<object[]> constructor_shouldCreateFromNamespaceAndLocalName_data = TupleHelper.toArrays(
            (ASNamespace.@public, null, "null"),
            (new ASNamespace("a"), null, "null"),
            (new ASNamespace("*"), null, "null"),
            (new ASNamespace("a", "b"), null, "null"),
            (ASNamespace.@public, "*", "*"),
            (new ASNamespace("a"), "*", "*"),
            (new ASNamespace("*"), "*", "*"),
            (new ASNamespace("a", "b"), "*", "*"),
            (ASNamespace.@public, "x", "x"),
            (new ASNamespace("a"), "x", "x"),
            (new ASNamespace("*"), "x", "x"),
            (new ASNamespace("a", "b"), "x", "x")
        );

        [Theory]
        [MemberData(nameof(constructor_shouldCreateFromNamespaceAndLocalName_data))]
        public void constructor_shouldCreateFromNamespaceAndLocalName(
            ASNamespace ns, string localName, string expectedLocalName)
        {
            var qname = new ASQName(ns, localName);
            Assert.Equal(ns?.prefix, qname.prefix);
            Assert.Equal(ns?.uri, qname.uri);
            Assert.Equal(expectedLocalName, qname.localName);
        }

        [Fact]
        public void valueOf_shouldReturnSameObject() {
            var qname1 = new ASQName("a");
            var qname2 = new ASQName("a", "b");
            var qname3 = new ASQName("a", "b", "c");

            Assert.Same(qname1, qname1.valueOf());
            Assert.Same(qname2, qname2.valueOf());
            Assert.Same(qname3, qname3.valueOf());
        }

        [Fact]
        public void constructor_shouldThrowIfEmptyUriAndNonEmptyPrefix() {
            AssertHelper.throwsErrorWithCode(ErrorCode.XML_ILLEGAL_PREFIX_PUBLIC_NAMESPACE, () => new ASQName("a", "", "b"));
        }

        public static IEnumerable<object[]> toString_shouldReturnStringReprOfQName_data = TupleHelper.toArrays(
            (ASQName.any, "*::*"),
            (ASQName.anyNamespace(""), "*::"),
            (ASQName.anyNamespace("*"), "*::*"),
            (ASQName.anyNamespace("a"), "*::a"),
            (new ASQName("", "*"), "*"),
            (new ASQName("", ""), ""),
            (new ASQName("", "a"), "a"),
            (new ASQName("a", "*"), "a::*"),
            (new ASQName("a", ""), "a::"),
            (new ASQName("a", "a"), "a::a"),
            (new ASQName("a", "b"), "a::b"),
            (new ASQName("x", "a", "*"), "a::*"),
            (new ASQName("x", "a", ""), "a::"),
            (new ASQName("x", "a", "a"), "a::a"),
            (new ASQName("x", "a", "b"), "a::b"),
            (new ASQName("abc", "def"), "abc::def"),
            (new ASQName("abc", "def", "ghi"), "def::ghi")
        );

        [Theory]
        [MemberData(nameof(toString_shouldReturnStringReprOfQName_data))]
        public void toString_shouldReturnStringReprOfQName(ASQName qname, string expected) {
            Assert.Equal(expected, qname.AS_toString());
        }

        public static IEnumerable<object[]> getNamespace_shouldGetQNameNamespace_data = TupleHelper.toArrays(
            (ASQName.any, null),
            (ASQName.anyNamespace(""), null),
            (ASQName.anyNamespace("*"), null),
            (ASQName.anyNamespace("a"), null),
            (new ASQName("", "*"), ASNamespace.@public),
            (new ASQName("", ""), ASNamespace.@public),
            (new ASQName("", "a"), ASNamespace.@public),
            (new ASQName("a", "*"), new ASNamespace("a")),
            (new ASQName("a", ""), new ASNamespace("a")),
            (new ASQName("a", "a"), new ASNamespace("a")),
            (new ASQName("a", "b"), new ASNamespace("a")),
            (new ASQName("x", "a", "*"), new ASNamespace("x", "a")),
            (new ASQName("x", "a", ""), new ASNamespace("x", "a")),
            (new ASQName("x", "a", "a"), new ASNamespace("x", "a")),
            (new ASQName("x", "a", "b"), new ASNamespace("x", "a")),
            (new ASQName("abc", "def"), new ASNamespace("abc")),
            (new ASQName("abc", "def", "ghi"), new ASNamespace("abc", "def"))
        );

        [Theory]
        [MemberData(nameof(getNamespace_shouldGetQNameNamespace_data))]
        public void getNamespace_shouldGetQNameNamespace(ASQName qname, ASNamespace expectedNs) {
            var ns = qname.getNamespace();
            if (ns == null) {
                Assert.Null(expectedNs);
            }
            else {
                Assert.Equal(expectedNs.prefix, ns.prefix);
                Assert.Equal(expectedNs.uri, ns.uri);
            }
        }

        public static IEnumerable<object[]> equals_shouldCheckIfUriAndLocalNameEqual_data() {
            ASQName n1a = ASQName.any,
                    n1b = ASQName.anyNamespace("*"),
                    n1c = new ASQName("*");

            ASQName n2a = ASQName.anyNamespace("a"),
                    n2b = new ASQName((string)null, "a");

            ASQName n3 = new ASQName("", "abc"),
                    n4 = new ASQName("", "def"),
                    n5 = new ASQName("a", "abc"),
                    n6 = new ASQName("b", "abc"),
                    n7 = new ASQName("b", "def"),
                    n8a = new ASQName("c", "abc"),
                    n8b = new ASQName(new ASNamespace("c"), "abc"),
                    n8c = new ASQName("x", "c", "abc"),
                    n8d = new ASQName(new ASNamespace("x", "c"), "abc"),
                    n8e = new ASQName("", "c", "abc"),
                    n8f = new ASQName(new ASNamespace("", "c"), "abc"),
                    n9 = new ASQName("*", "a");

            return TupleHelper.toArrays(
                (null, null, true),
                (n1a, null, false),
                (null, n2b, false),

                (n1a, n1a, true),
                (n2a, n2a, true),
                (n5, n5, true),
                (n8c, n8c, true),

                (n1a, n1b, true),
                (n1a, n1c, true),
                (n1b, n1c, true),
                (n1b, n1a, true),

                (n2a, n2b, true),
                (n2b, n2a, true),

                (n8a, n8b, true),
                (n8a, n8c, true),
                (n8a, n8d, true),
                (n8a, n8e, true),
                (n8a, n8f, true),
                (n8d, n8c, true),
                (n8e, n8c, true),
                (n8f, n8c, true),

                (n1a, n2a, false),

                (n2b, n3, false),
                (n6, n2b, false),
                (n2a, n9, false),
                (n9, n2b, false),
                (n2a, n8c, false),

                (n3, n4, false),
                (n3, n5, false),
                (n3, n6, false),
                (n3, n7, false),
                (n4, n5, false),
                (n4, n6, false),
                (n5, n6, false),
                (n4, n7, false),
                (n5, n7, false),
                (n6, n7, false),

                (n3, n8a, false),
                (n8b, n5, false),
                (n8e, n6, false),
                (n4, n8b, false),
                (n7, n8f, false),
                (n6, n8a, false),
                (n8a, n4, false),
                (n8d, n5, false),

                (n9, n5, false),
                (n9, n8b, false),
                (n6, n9, false)
            );
        }

        [Theory]
        [MemberData(nameof(equals_shouldCheckIfUriAndLocalNameEqual_data))]
        public void equals_shouldCheckIfUriAndLocalNameEqual(ASQName x, ASQName y, bool expected) {
            Assert.Equal(expected, ASQName.AS_equals(x, y));
        }

        [Theory]
        [MemberData(nameof(equals_shouldCheckIfUriAndLocalNameEqual_data))]
        public void getHashCode_shouldReturnSameHashCodeForEqual(ASQName x, ASQName y, bool expected) {
            if (expected && x != null && y != null)
                Assert.Equal(x.internalGetHashCode(), y.internalGetHashCode());
        }

        public static IEnumerable<object[]> parse_shouldParseStringToQName_data = TupleHelper.toArrays(
            (null, ASQName.any),
            ("*", ASQName.any),
            ("*::*", ASQName.any),

            ("", new ASQName("", "a", "")),
            ("x", new ASQName("", "a", "x")),
            ("x.y", new ASQName("", "a", "x.y")),
            ("x:y", new ASQName("", "a", "x:y")),

            ("*::", ASQName.anyNamespace("")),
            ("*::x", ASQName.anyNamespace("x")),

            ("a::", new ASQName("a", "")),
            ("a::*", new ASQName("a", "*")),
            ("a::x", new ASQName("a", "x")),

            ("::*", new ASQName("", "*")),
            ("::x", new ASQName("", "x")),
            ("::abc", new ASQName("", "abc")),

            ("b::", new ASQName("b", "")),
            ("b::*", new ASQName("b", "*")),
            ("b::x", new ASQName("b", "x")),

            ("a::b:c", new ASQName("a", "b:c")),
            ("a::b::c", new ASQName("a::b", "c")),
            ("abc::def", new ASQName("abc", "def")),
            ("abc::def::", new ASQName("abc::def", "")),
            ("::abc::def", new ASQName("::abc", "def")),
            ("abc::def::ghi", new ASQName("abc::def", "ghi")),

            ("  ", new ASQName("", "a", "  ")),
            (" * ", new ASQName("", "a", " * ")),
            ("a::b ", new ASQName("a", "b ")),
            (" a::b", new ASQName(" a", "b")),
            ("a ::b", new ASQName("a ", "b")),
            ("a:: b", new ASQName("a", " b")),
            (" a :: b ", new ASQName(" a ", " b "))
        );

        [Theory]
        [MemberData(nameof(parse_shouldParseStringToQName_data))]
        public void parse_shouldParseStringToQName(string str, ASQName expected) {
            var oldDefault = ASNamespace.getDefault();
            ASNamespace.setDefault(new ASNamespace("a"));

            try {
                var qname = ASQName.parse(str);
                Assert.Equal(expected.prefix, qname.prefix);
                Assert.Equal(expected.uri, qname.uri);
                Assert.Equal(expected.localName, qname.localName);
            }
            finally {
                ASNamespace.setDefault(oldDefault);
            }
        }

        public static IEnumerable<object[]> runtimeConstructorTest_data = TupleHelper.toArrays(
            (Array.Empty<ASAny>(), new ASQName("", "")),

            (new ASAny[] {ASAny.undefined}, new ASQName("", "default", "undefined")),
            (new ASAny[] {ASAny.@null}, new ASQName("", "default", "null")),
            (new ASAny[] {"a"}, new ASQName("", "default", "a")),
            (new ASAny[] {"abc"}, new ASQName("", "default", "abc")),
            (new ASAny[] {1}, new ASQName("", "default", "1")),
            (new ASAny[] {"*"}, ASQName.any),
            (new ASAny[] {new ASNamespace("a")}, new ASQName("a", "")),
            (new ASAny[] {new ASNamespace("a", "b")}, new ASQName("a", "b", "")),
            (new ASAny[] {new ASQName("*")}, ASQName.any),
            (new ASAny[] {new ASQName("a")}, new ASQName("a")),
            (new ASAny[] {new ASQName("a", "b")}, new ASQName("a", "b")),
            (new ASAny[] {new ASQName("a", "b", "c")}, new ASQName("a", "b", "c")),

            (new ASAny[] {ASAny.@undefined, ASAny.@undefined}, new ASQName("", "default", "")),
            (new ASAny[] {ASAny.@undefined, ASAny.@null}, new ASQName("", "default", "null")),
            (new ASAny[] {ASAny.@undefined, "a"}, new ASQName("", "default", "a")),
            (new ASAny[] {ASAny.@undefined, "abc"}, new ASQName("", "default", "abc")),
            (new ASAny[] {ASAny.@undefined, 1}, new ASQName("", "default", "1")),
            (new ASAny[] {ASAny.@undefined, "*"}, new ASQName("", "default", "*")),
            (new ASAny[] {ASAny.@undefined, new ASQName("a", "b")}, new ASQName("", "default", "b")),
            (new ASAny[] {ASAny.@undefined, new ASQName("a", "*")}, new ASQName("", "default", "*")),

            (new ASAny[] {ASAny.@null, ASAny.@undefined}, ASQName.anyNamespace("")),
            (new ASAny[] {ASAny.@null, ASAny.@null}, ASQName.anyNamespace("null")),
            (new ASAny[] {ASAny.@null, "a"}, ASQName.anyNamespace("a")),
            (new ASAny[] {ASAny.@null, "abc"}, ASQName.anyNamespace("abc")),
            (new ASAny[] {ASAny.@null, 1}, ASQName.anyNamespace("1")),
            (new ASAny[] {ASAny.@null, "*"}, ASQName.any),
            (new ASAny[] {ASAny.@null, new ASQName("a", "b")}, ASQName.anyNamespace("b")),
            (new ASAny[] {ASAny.@null, new ASQName("a", "*")}, ASQName.any),

            (new ASAny[] {"*", ASAny.@undefined}, new ASQName("*", "")),
            (new ASAny[] {"*", ASAny.@null}, new ASQName("*", "null")),
            (new ASAny[] {"*", "a"}, new ASQName("*", "a")),
            (new ASAny[] {"*", "abc"}, new ASQName("*", "abc")),
            (new ASAny[] {"*", 1}, new ASQName("*", "1")),
            (new ASAny[] {"*", "*"}, new ASQName("*", "*")),
            (new ASAny[] {"*", new ASQName("a", "b")}, new ASQName("*", "b")),
            (new ASAny[] {"*", new ASQName("a", "*")}, new ASQName("*", "*")),

            (new ASAny[] {"", ASAny.@undefined}, new ASQName("", "")),
            (new ASAny[] {"", ASAny.@null}, new ASQName("", "null")),
            (new ASAny[] {"", "a"}, new ASQName("", "a")),
            (new ASAny[] {"", "abc"}, new ASQName("", "abc")),
            (new ASAny[] {"", 1}, new ASQName("", "1")),
            (new ASAny[] {"", "*"}, new ASQName("", "*")),
            (new ASAny[] {"", new ASQName("a", "b")}, new ASQName("", "b")),
            (new ASAny[] {"", new ASQName("a", "*")}, new ASQName("", "*")),

            (new ASAny[] {"xyz", ASAny.@undefined}, new ASQName("xyz", "")),
            (new ASAny[] {"xyz", ASAny.@null}, new ASQName("xyz", "null")),
            (new ASAny[] {"xyz", "a"}, new ASQName("xyz", "a")),
            (new ASAny[] {"xyz", "abc"}, new ASQName("xyz", "abc")),
            (new ASAny[] {"xyz", 1}, new ASQName("xyz", "1")),
            (new ASAny[] {"xyz", "*"}, new ASQName("xyz", "*")),
            (new ASAny[] {"xyz", new ASQName("a", "b")}, new ASQName("xyz", "b")),
            (new ASAny[] {"xyz", new ASQName("a", "*")}, new ASQName("xyz", "*")),

            (new ASAny[] {123, ASAny.@undefined}, new ASQName("123", "")),
            (new ASAny[] {123, ASAny.@null}, new ASQName("123", "null")),
            (new ASAny[] {123, "a"}, new ASQName("123", "a")),
            (new ASAny[] {123, "abc"}, new ASQName("123", "abc")),
            (new ASAny[] {123, 1}, new ASQName("123", "1")),
            (new ASAny[] {123, "*"}, new ASQName("123", "*")),
            (new ASAny[] {123, new ASQName("a", "b")}, new ASQName("123", "b")),
            (new ASAny[] {123, new ASQName("a", "*")}, new ASQName("123", "*")),

            (new ASAny[] {new ASNamespace("xyz"), ASAny.@undefined}, new ASQName("xyz", "")),
            (new ASAny[] {new ASNamespace("xyz"), ASAny.@null}, new ASQName("xyz", "null")),
            (new ASAny[] {new ASNamespace("xyz"), "a"}, new ASQName("xyz", "a")),
            (new ASAny[] {new ASNamespace("xyz"), "abc"}, new ASQName("xyz", "abc")),
            (new ASAny[] {new ASNamespace("xyz"), 1}, new ASQName("xyz", "1")),
            (new ASAny[] {new ASNamespace("xyz"), "*"}, new ASQName("xyz", "*")),
            (new ASAny[] {new ASNamespace("xyz"), new ASQName("a", "b")}, new ASQName("xyz", "b")),
            (new ASAny[] {new ASNamespace("xyz"), new ASQName("a", "*")}, new ASQName("xyz", "*")),

            (new ASAny[] {new ASNamespace("p", "xyz"), ASAny.@undefined}, new ASQName("p", "xyz", "")),
            (new ASAny[] {new ASNamespace("p", "xyz"), ASAny.@null}, new ASQName("p", "xyz", "null")),
            (new ASAny[] {new ASNamespace("p", "xyz"), "a"}, new ASQName("p", "xyz", "a")),
            (new ASAny[] {new ASNamespace("p", "xyz"), "abc"}, new ASQName("p", "xyz", "abc")),
            (new ASAny[] {new ASNamespace("p", "xyz"), 1}, new ASQName("p", "xyz", "1")),
            (new ASAny[] {new ASNamespace("p", "xyz"), "*"}, new ASQName("p", "xyz", "*")),
            (new ASAny[] {new ASNamespace("p", "xyz"), new ASQName("a", "b")}, new ASQName("p", "xyz", "b")),
            (new ASAny[] {new ASNamespace("p", "xyz"), new ASQName("a", "*")}, new ASQName("p", "xyz", "*")),

            (new ASAny[] {new ASQName("p", "xyz"), "a"}, new ASQName("p", "a")),
            (new ASAny[] {ASQName.any, "a"}, new ASQName("*::*", "a")),
            (new ASAny[] {ASQName.anyNamespace("x"), "a"}, new ASQName("*::x", "a")),
            (new ASAny[] {new ASQName("p", "q", "r"), "a"}, new ASQName("p", "q", "a")),

            (new ASAny[] {"a", "b", "c"}, new ASQName("a", "b")),
            (new ASAny[] {new ASNamespace("x", "y"), "z", "a", "b"}, new ASQName("x", "y", "z"))
        );

        [Theory]
        [MemberData(nameof(runtimeConstructorTest_data))]
        public void runtimeConstructorTest(ASAny[] args, ASQName expected) {
            var oldDefault = ASNamespace.getDefault();
            ASNamespace.setDefault(new ASNamespace("default"));

            try {
                ASObject result;

                result = s_klass.invoke(args).value;
                Assert.IsType<ASQName>(result);
                Assert.Equal(expected.prefix, ((ASQName)result).prefix);
                Assert.Equal(expected.uri, ((ASQName)result).uri);
                Assert.Equal(expected.localName, ((ASQName)result).localName);

                result = s_klass.construct(args).value;
                Assert.IsType<ASQName>(result);
                Assert.Equal(expected.prefix, ((ASQName)result).prefix);
                Assert.Equal(expected.uri, ((ASQName)result).uri);
                Assert.Equal(expected.localName, ((ASQName)result).localName);
            }
            finally {
                ASNamespace.setDefault(oldDefault);
            }
        }

    }

}
