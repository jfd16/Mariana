using System;
using System.Collections.Generic;
using System.Threading;
using Mariana.AVM2.Core;
using Mariana.AVM2.Tests.Helpers;
using Xunit;

namespace Mariana.AVM2.Tests {

    public class ASNamespaceTest {

        private static readonly Class s_klass = Class.fromType<ASNamespace>();

        [Fact]
        public void publicNamespaceShouldHaveEmptyPrefixAndUri() {
            Assert.Equal("", ASNamespace.@public.prefix);
            Assert.Equal("", ASNamespace.@public.uri);
        }

        [Theory]
        [InlineData(null, null, "null")]
        [InlineData("", "", "")]
        [InlineData("*", null, "*")]
        [InlineData("hello", null, "hello")]
        public void constructorTest_withUri(string uri, string expectedPrefix, string expectedUri) {
            var ns = new ASNamespace(uri);
            Assert.Equal(expectedUri, ns.uri);
            Assert.Equal(expectedPrefix, ns.prefix);
        }

        [Theory]
        [InlineData(null, null, "null", "null")]
        [InlineData(null, "hello", "null", "hello")]
        [InlineData("hello", null, "hello", "null")]
        [InlineData("", "", "", "")]
        [InlineData("", "hello", "", "hello")]
        [InlineData("", null, "", "null")]
        [InlineData("abc", "hello", "abc", "hello")]
        [InlineData("abc", "*", "abc", "*")]
        [InlineData("a-b", "hello", "a-b", "hello")]
        [InlineData("_123", "hello", "_123", "hello")]
        public void constructorTest_withUriAndPrefix(string prefix, string uri, string expectedPrefix, string expectedUri) {
            var ns = new ASNamespace(prefix, uri);
            Assert.Equal(expectedUri, ns.uri);
            Assert.Equal(expectedPrefix, ns.prefix);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("a")]
        public void constructorTest_emptyUriAndNonEmptyPrefix(string prefix) {
            AssertHelper.throwsErrorWithCode(ErrorCode.XML_ILLEGAL_PREFIX_PUBLIC_NAMESPACE, () => new ASNamespace(prefix, ""));
        }

        [Theory]
        [InlineData(" ")]
        [InlineData("1ab")]
        [InlineData("-ab")]
        public void constructorTest_prefixIsNotValidXmlName(string prefix) {
            var ns = new ASNamespace(prefix, "hello");
            Assert.Null(ns.prefix);
        }

        public static IEnumerable<object[]> testNamespaceData = TupleHelper.toArrays(
            ASNamespace.@public,
            new ASNamespace(""),
            new ASNamespace("a"),
            new ASNamespace("b"),
            new ASNamespace("*"),
            new ASNamespace("", ""),
            new ASNamespace("", "a"),
            new ASNamespace("", "b"),
            new ASNamespace("a", "a"),
            new ASNamespace("b", "a"),
            new ASNamespace("b", "b"),
            new ASNamespace("b", "*")
        );

        [Theory]
        [MemberData(nameof(testNamespaceData))]
        public void prefixPropertyTest(ASNamespace ns) {
            ASAny expected = (ns.prefix == null) ? ASAny.undefined : ns.prefix;
            Assert.Equal(expected, ns.AS_prefix);
        }

        [Theory]
        [MemberData(nameof(testNamespaceData))]
        public void toStringMethodTest(ASNamespace ns) {
            Assert.Equal(ns.uri, ns.AS_toString());
        }

        [Theory]
        [MemberData(nameof(testNamespaceData))]
        public void valueOfMethodTest(ASNamespace ns) {
            Assert.Equal(ns.uri, ns.valueOf());
        }

        public static IEnumerable<object[]> equalsMethodTest_data() {
            foreach (var x in testNamespaceData) {
                foreach (var y in testNamespaceData)
                    yield return new object[] {x[0], y[0]};
            }

            yield return new object[] {null, null};
            yield return new object[] {new ASNamespace("a"), null};
            yield return new object[] {null, new ASNamespace("a")};
        }

        [Theory]
        [MemberData(nameof(equalsMethodTest_data))]
        public void equalsMethodTest(ASNamespace x, ASNamespace y) {
            Assert.Equal(x?.uri == y?.uri, ASNamespace.AS_equals(x, y));
        }

        public static IEnumerable<object[]> runtimeConstructorTest_data = TupleHelper.toArrays(
            (Array.Empty<ASAny>(), ASNamespace.@public),

            (new ASAny[] {ASAny.undefined}, new ASNamespace("undefined")),
            (new ASAny[] {ASAny.@null}, new ASNamespace("null")),
            (new ASAny[] {1}, new ASNamespace("1")),
            (new ASAny[] {""}, new ASNamespace("")),
            (new ASAny[] {"a"}, new ASNamespace("a")),
            (new ASAny[] {"*"}, new ASNamespace("*")),
            (new ASAny[] {new ASNamespace("b")}, new ASNamespace("b")),
            (new ASAny[] {new ASQName(new ASNamespace("a"), "x")}, new ASNamespace("a")),
            (new ASAny[] {new ASQName(new ASNamespace("a", "b"), "x")}, new ASNamespace("a", "b")),
            (new ASAny[] {ASQName.anyNamespace("x")}, new ASNamespace("*::x")),

            (new ASAny[] {"a", "b"}, new ASNamespace("a", "b")),
            (new ASAny[] {"", ""}, new ASNamespace("", "")),
            (new ASAny[] {"", "b"}, new ASNamespace("", "b")),
            (new ASAny[] {"a", 1}, new ASNamespace("a", "1")),
            (new ASAny[] {1, "a"}, new ASNamespace("a")),

            (new ASAny[] {ASAny.undefined, "a"}, new ASNamespace("a")),
            (new ASAny[] {ASAny.@null, "a"}, new ASNamespace("a")),
            (new ASAny[] {ASAny.undefined, ASAny.undefined}, new ASNamespace("undefined")),
            (new ASAny[] {ASAny.@null, ASAny.@null}, new ASNamespace("null")),

            (new ASAny[] {"a", new ASNamespace("b")}, new ASNamespace("a", "b")),
            (new ASAny[] {"a", new ASNamespace("b", "c")}, new ASNamespace("a", "c")),
            (new ASAny[] {new ASNamespace("a"), new ASNamespace("c", "d")}, new ASNamespace("a", "d")),
            (new ASAny[] {new ASNamespace("a", "b"), new ASNamespace("c", "d")}, new ASNamespace("b", "d")),
            (new ASAny[] {"a", new ASQName("b", "c")}, new ASNamespace("a", "b")),
            (new ASAny[] {"a", new ASQName("b", "c", "d")}, new ASNamespace("a", "c")),
            (new ASAny[] {"a", ASQName.anyNamespace("b")}, new ASNamespace("a", "*::b")),
            (new ASAny[] {new ASQName("", "a"), new ASQName("b", "c")}, new ASNamespace("a", "b")),
            (new ASAny[] {new ASQName("a", "b"), new ASQName("c", "d")}, new ASNamespace("c")),
            (new ASAny[] {new ASQName("a", "b"), new ASQName("c", "d", "e")}, new ASNamespace("d")),

            (new ASAny[] {"a", "b", "c", "d"}, new ASNamespace("a", "b")),
            (new ASAny[] {new ASNamespace("a", "b"), new ASNamespace("c", "d"), new ASNamespace("e", "f")}, new ASNamespace("b", "d"))
        );

        [Theory]
        [MemberData(nameof(testNamespaceData))]
        public void forInIterationTest(ASNamespace ns) {
            int index = 0;
            AssertHelper.valueIdentical(ASAny.undefined, ns.AS_nameAtIndex(index));
            AssertHelper.valueIdentical(ASAny.undefined, ns.AS_valueAtIndex(index));

            index = ns.AS_nextIndex(index);
            AssertHelper.valueIdentical("uri", ns.AS_nameAtIndex(index));
            AssertHelper.valueIdentical(ns.uri, ns.AS_valueAtIndex(index));

            index = ns.AS_nextIndex(index);
            AssertHelper.valueIdentical("prefix", ns.AS_nameAtIndex(index));
            AssertHelper.valueIdentical(ns.AS_prefix, ns.AS_valueAtIndex(index));

            index = ns.AS_nextIndex(index);
            Assert.Equal(0, index);
        }

        [Theory]
        [MemberData(nameof(testNamespaceData))]
        public void propertyIsEnumerableMethodTest(ASNamespace ns) {
            Assert.True(ns.propertyIsEnumerable("uri"));
            Assert.True(ns.propertyIsEnumerable("prefix"));
        }

        [Theory]
        [MemberData(nameof(runtimeConstructorTest_data))]
        public void runtimeConstructorTest(ASAny[] args, ASNamespace expected) {
            ASObject result;

            result = s_klass.invoke(args).value;
            Assert.IsType<ASNamespace>(result);
            Assert.Equal(expected.prefix, ((ASNamespace)result).prefix);
            Assert.Equal(expected.uri, ((ASNamespace)result).uri);

            result = s_klass.construct(args).value;
            Assert.IsType<ASNamespace>(result);
            Assert.Equal(expected.prefix, ((ASNamespace)result).prefix);
            Assert.Equal(expected.uri, ((ASNamespace)result).uri);
        }

        [Fact]
        public void defaultNamespaceShouldBePublicForNewThread() {
            var thread = new Thread(() => {
                Assert.Equal("", ASNamespace.getDefault().uri);
            });

            ASNamespace.setDefault(new ASNamespace("a"));
            thread.Start();
            thread.Join();
        }

        [Fact]
        public void setDefaultMethodTest() {
            ASNamespace.setDefault(new ASNamespace("a"));
            Assert.Equal("", ASNamespace.getDefault().prefix);
            Assert.Equal("a", ASNamespace.getDefault().uri);

            ASNamespace.setDefault(new ASNamespace("a", "b"));
            Assert.Equal("", ASNamespace.getDefault().prefix);
            Assert.Equal("b", ASNamespace.getDefault().uri);

            ASNamespace.setDefault(null);
            Assert.Equal("", ASNamespace.getDefault().prefix);
            Assert.Equal("", ASNamespace.getDefault().uri);

            ASNamespace.setDefault(new ASNamespace(""));
            Assert.Equal("", ASNamespace.getDefault().prefix);
            Assert.Equal("", ASNamespace.getDefault().uri);

            ASNamespace.setDefault(new ASNamespace("", "c"));
            Assert.Equal("", ASNamespace.getDefault().prefix);
            Assert.Equal("c", ASNamespace.getDefault().uri);

            var thread = new Thread(() => {
                ASNamespace.setDefault(new ASNamespace("e", "f"));
                Assert.Equal("", ASNamespace.getDefault().prefix);
                Assert.Equal("f", ASNamespace.getDefault().uri);
            });

            thread.Start();
            thread.Join();

            Assert.Equal("", ASNamespace.getDefault().prefix);
            Assert.Equal("c", ASNamespace.getDefault().uri);
        }

        [Fact]
        public void setDefaultMethodTest_getOldDefault() {
            ASNamespace oldDefault;

            ASNamespace.setDefault(new ASNamespace("a"));

            ASNamespace.setDefault(new ASNamespace("a", "b"), out oldDefault);
            Assert.Equal("", ASNamespace.getDefault().prefix);
            Assert.Equal("b", ASNamespace.getDefault().uri);
            Assert.Equal("", oldDefault.prefix);
            Assert.Equal("a", oldDefault.uri);

            ASNamespace.setDefault(null, out oldDefault);
            Assert.Equal("", ASNamespace.getDefault().prefix);
            Assert.Equal("", ASNamespace.getDefault().uri);
            Assert.Equal("", oldDefault.prefix);
            Assert.Equal("b", oldDefault.uri);

            ASNamespace.setDefault(new ASNamespace("", "c"), out oldDefault);
            Assert.Equal("", ASNamespace.getDefault().prefix);
            Assert.Equal("c", ASNamespace.getDefault().uri);
            Assert.Equal("", oldDefault.prefix);
            Assert.Equal("", oldDefault.uri);

            var thread = new Thread(() => {
                ASNamespace.setDefault(new ASNamespace("e", "f"), out oldDefault);
                Assert.Equal("", ASNamespace.getDefault().prefix);
                Assert.Equal("f", ASNamespace.getDefault().uri);
                Assert.Equal("", oldDefault.prefix);
                Assert.Equal("", oldDefault.uri);
            });

            thread.Start();
            thread.Join();

            Assert.Equal("", ASNamespace.getDefault().prefix);
            Assert.Equal("c", ASNamespace.getDefault().uri);
        }

    }

}
