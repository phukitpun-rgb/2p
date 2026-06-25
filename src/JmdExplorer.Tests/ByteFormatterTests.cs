using JmdExplorer.Core.Services;
using Xunit;

namespace JmdExplorer.Tests;

public class ByteFormatterTests
{
    [Theory]
    [InlineData("58 65 6E 6F 6E", new byte[] { 0x58, 0x65, 0x6E, 0x6F, 0x6E })]
    [InlineData("586E6F6E", new byte[] { 0x58, 0x6E, 0x6F, 0x6E })]
    [InlineData("0x4142", new byte[] { 0x41, 0x42 })]
    public void ParseHexPattern_ParsesValidInput(string input, byte[] expected)
    {
        Assert.Equal(expected, ByteFormatter.ParseHexPattern(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ZZ")]
    [InlineData("ABC")] // odd length
    public void ParseHexPattern_RejectsInvalidInput(string input)
    {
        Assert.Null(ByteFormatter.ParseHexPattern(input));
    }

    [Fact]
    public void ToHexString_IsSpacedUppercase()
    {
        Assert.Equal("00 0A FF", ByteFormatter.ToHexString(new byte[] { 0x00, 0x0A, 0xFF }));
    }

    [Fact]
    public void ToAscii_ReplacesNonPrintableWithDot()
    {
        Assert.Equal("A.B", ByteFormatter.ToAsciiString(new byte[] { 0x41, 0x00, 0x42 }));
    }

    [Fact]
    public void ToCSharpByteArray_RoundTripsValues()
    {
        string s = ByteFormatter.ToCSharpByteArray(new byte[] { 0x01, 0xAB });
        Assert.Contains("0x01", s);
        Assert.Contains("0xAB", s);
        Assert.StartsWith("byte[]", s);
    }
}
