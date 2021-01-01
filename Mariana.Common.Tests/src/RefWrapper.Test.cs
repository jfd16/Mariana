using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace Mariana.Common.Tests {

    public sealed class RefWrapperTest {

        [Fact]
        public void defaultShouldBeNull() {
            Assert.Same(default(RefWrapper<object>).value, null);
            Assert.Same(RefWrapper<object>.nullRef.value, null);
        }

        [Fact]
        public void shouldReturnWrappedValue() {
            var obj = new object();
            Assert.Same((new RefWrapper<object>(obj)).value, obj);
        }

        [Fact]
        public void shouldWrapWithConversionOperator() {
            var obj = new object();
            RefWrapper<object> wrapper = obj;
            Assert.Same(wrapper.value, obj);
        }

        [Fact]
        public void shouldUseReferenceEqualityForEquals() {
            string s1 = "Hello";
            string s2 = new string(s1.AsSpan());
            RefWrapper<string> w1 = s1, w2 = s2;

            Assert.True(w1.Equals(w1));
            Assert.True(w1.Equals((object)w1));
            Assert.True(w1.Equals(s1));
            Assert.True(w2.Equals(w2));
            Assert.True(w2.Equals((object)w2));
            Assert.True(w2.Equals(s2));
            Assert.False(w1.Equals(w2));
            Assert.False(w1.Equals((object)w2));
            Assert.False(w1.Equals(s2));
            Assert.False(w2.Equals(w1));
            Assert.False(w2.Equals((object)w1));
            Assert.False(w2.Equals(s1));
        }

        [Fact]
        public void shouldUseReferenceEqualityForHashCode() {
            string s1 = "Hello";
            string s2 = new string(s1.AsSpan());
            RefWrapper<string> w1 = s1, w2 = s2;

            Assert.Equal(w1.GetHashCode(), RuntimeHelpers.GetHashCode(s1));
            Assert.Equal(w2.GetHashCode(), RuntimeHelpers.GetHashCode(s2));
        }

        [Fact]
        public void shouldUseReferenceEqualityForOperator() {
            string s1 = "Hello";
            string s2 = new string(s1.AsSpan());
            RefWrapper<string> w1 = s1, w2 = s2;
            RefWrapper<string> w3 = w1, w4 = w2;

            Assert.True(w1 == w3);
            Assert.True(w2 == w4);
            Assert.True(w1 != w2);
            Assert.True(w1 != w4);
            Assert.False(w1 != w3);
            Assert.False(w2 != w4);
            Assert.False(w1 == w2);
            Assert.False(w1 == w4);
        }

    }

}
