using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Mariana.Common.Tests {

    public sealed class URIUtilTest {

        [Fact]
        public void encode_shouldFailIfGivenNull() {
            Assert.False(URIUtil.tryEncode(null, "", true, out string encoded));
            Assert.Null(encoded);
        }

        [Fact]
        public void encode_shouldReturnEmptyStringIfGivenEmpty() {
            Assert.True(URIUtil.tryEncode("", "", true, out string encoded));
            Assert.Equal("", encoded);
        }

        [Theory]
        [InlineData("hello")]
        [InlineData("helloabcd12345")]
        public void encode_shouldNotEscapeLettersAndDigits(string str) {
            string encoded;

            Assert.True(URIUtil.tryEncode(str, "", true, out encoded));
            Assert.Equal(str, encoded);
            Assert.True(URIUtil.tryEncode(str, "abc123", true, out encoded));
            Assert.Equal(str, encoded);
        }

        [Theory]
        [InlineData(" ", "%20")]
        [InlineData("%", "%25")]
        [InlineData("hello-world", "hello%2Dworld")]
        [InlineData("abc%r$@!493{}+-=`?/<>,.gf", "abc%25r%24%40%21493%7B%7D%2B%2D%3D%60%3F%2F%3C%3E%2C%2Egf")]
        public void encode_shouldEscapeNonLettersAndDigits(string str, string expected) {
            Assert.True(URIUtil.tryEncode(str, "", true, out string encoded));
            Assert.Equal(expected, encoded);
        }

        [Theory]
        [InlineData("\u0080", "%C2%80")]
        [InlineData("\u00A0\u00CF\u00FF\u0356\u04AD\u07FF", "%C2%A0%C3%8F%C3%BF%CD%96%D2%AD%DF%BF")]
        [InlineData("\u0800", "%E0%A0%80")]
        [InlineData("\u1045\u20DA\u44FF\uBC00\uD7FF\uE000\uFFFE\uFFFF", "%E1%81%85%E2%83%9A%E4%93%BF%EB%B0%80%ED%9F%BF%EE%80%80%EF%BF%BE%EF%BF%BF")]
        [InlineData("\uD800\uDC00\uD913\uDE34\uDBFF\uDFFF", "%F0%90%80%80%F1%94%B8%B4%F4%8F%BF%BF")]
        [InlineData("\uD800\uDC00 \uD913\uDE34 \uDBFF\uDFFF", "%F0%90%80%80%20%F1%94%B8%B4%20%F4%8F%BF%BF")]
        public void encode_shouldEncodeNonAsciiAsUtf8(string str, string expected) {
            Assert.True(URIUtil.tryEncode(str, "", true, out string encoded));
            Assert.Equal(expected, encoded);
        }

        [Theory]
        [InlineData('\uD800')]
        [InlineData('\uD800', '\uDBFF')]
        [InlineData('\uDBFF', '\uD800')]
        [InlineData('\uDC00')]
        [InlineData('\uDC00', '\uD800')]
        [InlineData('a', 'b', 'c', '\uD800', '\uD900', '\uDD00')]
        [InlineData('a', 'b', 'c', '\uD800', 'd', '\uDC00')]
        [InlineData('a', 'b', 'c', '\uD800', '\uDC00', 'd', 'e', 'f', '\uDFFF', '\uDBFF', 'g', 'h', 'i', '\uD900', '\uDEFF')]
        public void encode_shouldFailOnInvalidSurrogatesIfOptionSet(params char[] chars) {
            Assert.False(URIUtil.tryEncode(new string(chars), "", true, out _));
        }

        [Theory]
        [InlineData("?", '\uD800')]
        [InlineData("??", '\uD800', '\uDBFF')]
        [InlineData("??", '\uDBFF', '\uD800')]
        [InlineData("?", '\uDC00')]
        [InlineData("??", '\uDC00', '\uD800')]
        [InlineData("abc?%F1%90%84%80", 'a', 'b', 'c', '\uD800', '\uD900', '\uDD00')]
        [InlineData("abc?d?", 'a', 'b', 'c', '\uD800', 'd', '\uDC00')]
        [InlineData("abc%F0%90%80%80def??ghi%F1%90%8B%BF", 'a', 'b', 'c', '\uD800', '\uDC00', 'd', 'e', 'f', '\uDFFF', '\uDBFF', 'g', 'h', 'i', '\uD900', '\uDEFF')]
        public void encode_shouldReplaceInvalidSurrogates(string expected, params char[] chars) {
            Assert.True(URIUtil.tryEncode(new string(chars), "", false, out string encoded));
            Assert.Equal(expected, encoded);
        }

        [Theory]
        [InlineData("abc%r$@!493{}+-=`?/<>,.gf", "$@!{+", "abc%25r$@!493{%7D+%2D%3D%60%3F%2F%3C%3E%2C%2Egf")]
        [InlineData("abc???def///gh??i?j", "?", "abc???def%2F%2F%2Fgh??i?j")]
        [InlineData("abc???def///gh??i?j", "/", "abc%3F%3F%3Fdef///gh%3F%3Fi%3Fj")]
        [InlineData("abc???def///gh??i?j", "?/", "abc???def///gh??i?j")]
        [InlineData("??//\u0080\uD800\uDC00", "?\u0080\uD800\uDC00", "??%2F%2F%C2%80%F0%90%80%80")]
        public void encode_shouldNotEncodeAsciiCharsInNoEncodeSet(string str, string noEncode, string expected) {
            Assert.True(URIUtil.tryEncode(str, noEncode, true, out string encoded));
            Assert.Equal(expected, encoded);
        }

        [Fact]
        public void decode_shouldFailIfGivenNull() {
            Assert.False(URIUtil.tryDecode(null, "", true, out string decoded));
            Assert.Null(decoded);
        }

        [Fact]
        public void decode_shouldReturnEmptyStringIfGivenEmpty() {
            Assert.True(URIUtil.tryDecode("", "", true, out string decoded));
            Assert.Equal("", decoded);
        }

        [Theory]
        [InlineData("hello")]
        [InlineData("world")]
        [InlineData("abcd12345")]
        [InlineData("!@#$gsd^&*()_+34-={bns}[]:\"df;'<>?,bg./d23`~")]
        public void decode_shouldReturnSametringIfNoPercentSign(string str) {
            string decoded;
            Assert.True(URIUtil.tryDecode(str, "", true, out decoded));
            Assert.Equal(str, decoded);
            Assert.True(URIUtil.tryDecode(str, "abcd", true, out decoded));
            Assert.Equal(str, decoded);
        }

        [Theory]
        [InlineData("%25", "%")]
        [InlineData("%36", "6")]
        [InlineData("ab4%3d%4cde^&%71%7F%20 %20___%1d$", "ab4=Lde^&q\u007f   ___\u001d$")]
        [InlineData("%C2%80%C3%83%CF%B5%DD%94%DF%BF%E0%A0%80%E0%A5%84%E2%93%9D%E4%9F%B6", "\u0080\u00c3\u03f5\u0754\u07ff\u0800\u0944\u24dd\u47f6")]
        [InlineData("%F0%90%80%80%F0%90%8F%BF%F4%8F%BF%BF%F1%A2%85%8A%EE%80%80%EF%BF%BF", "\ud800\udc00\ud800\udfff\udbff\udfff\ud948\udd4a\ue000\uffff")]
        public void decode_shouldDecodePercentEncoding(string str, string expected) {
            Assert.True(URIUtil.tryDecode(str, "", true, out string decoded));
            Assert.Equal(expected, decoded);
        }

        public static readonly IEnumerable<object[]> invalidUriEncodings = new string[] {
            "%", "%2", "%20  %20%", "20%", "%2G", "abc%20%2@", "   %2`"
        }.Select(x => new object[] {x});

        [Theory]
        [MemberData(nameof(invalidUriEncodings))]
        public void decode_shouldFailOnInvalidEncoding(string str) {
            Assert.False(URIUtil.tryDecode(str, "", true, out _));
            Assert.False(URIUtil.tryDecode(str, "", false, out _));
        }

        public static readonly IEnumerable<object[]> invalidUtf8Encodings = new string[] {
            "%7f%7f%80", "%80", "%C0", "%C01", "%80%01", "%80%7F",
            "%B4%80", "%C0%01", "%C0%81", "%C1%BF", "%C2%00", "%C2%4F",
            "%E0", "%E0%BF", "%E0 ", "%E0%BF ", "%E0%A0 %80", "%E0%7F",
            "%E0%BF%5C", "%E0%80%80", "%E0%91%BF",
            "%F0", "%F0%BF", "%F0%BF%BF", "%F0 ", "%F0%BF ", "%F0%90%80 %80",
            "%F0%80%80%8D", "%F0%8F%BF%80", "%F0%8F%BF%8F", "%F4%90%80%80",
        }.Select(x => new object[] {x});

        [Theory]
        [MemberData(nameof(invalidUtf8Encodings))]
        public void decode_shouldFailOnInvalidUtf8(string str) {
            Assert.False(URIUtil.tryDecode(str, "", true, out _));
            Assert.False(URIUtil.tryDecode(str, "", false, out _));
        }

        [Theory]
        [InlineData("ab4%3d%4cde^&%7C%7F%20 %20___%1d%c2%80", "", "ab4=Lde^&|\u007f   ___\u001d\u0080")]
        [InlineData("ab4%3d%4cde^&%7C%7F%20 %20___%1d%c2%80", "=", "ab4%3dLde^&|\u007f   ___\u001d\u0080")]
        [InlineData("ab4%3d%4cde^&%7C%7F%20 %20___%1d%c2%80", "L|", "ab4=%4cde^&%7C\u007f   ___\u001d\u0080")]
        [InlineData("ab4%3d%4cde^&%7C%7F%20 %20___%1d%c2%80", "^_\u001d", "ab4=Lde^&|\u007f   ___%1d\u0080")]
        [InlineData("ab4%3d%4cde^&%7C%7F%20 %20___%1d%c2%80", "\u0080", "ab4=Lde^&|\u007f   ___\u001d\u0080")]
        public void decode_shouldNotDecodeAsciiCharsInNoDecodeSet(string str, string noDecode, string expected) {
            Assert.True(URIUtil.tryDecode(str, noDecode, true, out string decoded));
            Assert.Equal(expected, decoded);
        }

        [Theory]
        [InlineData("%ed%a0%80")]
        [InlineData("%ed%af%bf")]
        [InlineData("%ed%b0%80")]
        [InlineData("%ed%bf%bf")]
        [InlineData("%ed%a0%80%ed%b0%80")]
        [InlineData("%ed%a0%80%ed%bf%bf")]
        [InlineData("%ed%a0%80%ed%af%bf")]
        [InlineData("%ed%b0%80%ed%bf%bf")]
        [InlineData("%ed%b0%80%ed%a0%80")]
        [InlineData("%ed%b0%80%ed%af%bf")]
        [InlineData("%ed%bf%bf%ed%af%bf")]
        public void decode_shouldFailOnSurrogateCharIfOptionSet(string str) {
            Assert.False(URIUtil.tryDecode(str, "", true, out _));
        }

        [Theory]
        [InlineData("%ed%a0%80", '\ud800')]
        [InlineData("%ed%af%bf", '\udbff')]
        [InlineData("%ed%b0%80", '\udc00')]
        [InlineData("%ed%bf%bf", '\udfff')]
        [InlineData("%ed%a0%80%ed%b0%80", '\ud800', '\udc00')]
        [InlineData("%ed%a0%80%ed%bf%bf", '\ud800', '\udfff')]
        [InlineData("%ed%a0%80%ed%af%bf", '\ud800', '\udbff')]
        [InlineData("%ed%b0%80%ed%bf%bf", '\udc00', '\udfff')]
        [InlineData("%ed%b0%80%ed%a0%80", '\udc00', '\ud800')]
        [InlineData("%ed%b0%80%ed%af%bf", '\udc00', '\udbff')]
        [InlineData("%ed%bf%bf%ed%af%bf", '\udfff', '\udbff')]
        public void decode_shouldDecodeSurrogateChar(string str, params char[] expected) {
            Assert.True(URIUtil.tryDecode(str, "", false, out string decoded));
            Assert.Equal(new string(expected), decoded);
        }

        [Theory]
        [InlineData("00", 0x00)]
        [InlineData("05", 0x05)]
        [InlineData("0B", 0x0b)]
        [InlineData("0d", 0x0d)]
        [InlineData("24", 0x24)]
        [InlineData("99", 0x99)]
        [InlineData("6f", 0x6f)]
        [InlineData("a5", 0xa5)]
        [InlineData("Ca", 0xca)]
        [InlineData("F0", 0xf0)]
        [InlineData("FE", 0xfe)]
        [InlineData("0D ", 0x0d)]
        [InlineData("D411", 0xd4)]
        [InlineData("99-", 0x99)]
        public void hexToByte_shouldGetByteValue(string str, byte expected) {
            Assert.True(URIUtil.hexToByte(str, out byte value));
            Assert.Equal(expected, value);
        }

        [Theory]
        [InlineData(0x00, "00")]
        [InlineData(0x05, "05")]
        [InlineData(0x0b, "0B")]
        [InlineData(0x0d, "0D")]
        [InlineData(0x24, "24")]
        [InlineData(0x99, "99")]
        [InlineData(0x6f, "6F")]
        [InlineData(0xa5, "A5")]
        [InlineData(0xca, "CA")]
        [InlineData(0xf0, "F0")]
        [InlineData(0xfe, "FE")]
        public void byteToHex_shouldWriteHexString(byte value, string expected) {
            Span<char> span = stackalloc char[4];
            URIUtil.byteToHex(value, span);
            Assert.Equal(expected, new string(span.Slice(0, 2)));
        }

    }

}
