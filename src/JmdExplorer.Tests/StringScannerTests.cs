using System.Text;
using JmdExplorer.Core.Models;
using JmdExplorer.Core.Services;
using JmdExplorer.Tools.SampleGenerator;
using Xunit;

namespace JmdExplorer.Tests;

public class StringScannerTests
{
    private static List<ScannedString> Scan(byte[] data, StringScanOptions options)
    {
        var scanner = new StringScanner();
        using var stream = new MemoryStream(data);
        return scanner.Scan(stream, data.Length, options).ToList();
    }

    [Fact]
    public void FindsAsciiString()
    {
        var data = SampleFileBuilder.BuildStringSample();
        var results = Scan(data, new StringScanOptions { MinLength = 4, ScanAscii = true, ScanUtf16LE = false });
        Assert.Contains(results, s => s.Encoding == StringEncodingKind.Ascii && s.Value.Contains("HelloWorldString"));
    }

    [Fact]
    public void FindsUtf16LeString()
    {
        var data = SampleFileBuilder.BuildStringSample();
        var results = Scan(data, new StringScanOptions { MinLength = 4, ScanAscii = false, ScanUtf16LE = true });
        Assert.Contains(results, s => s.Encoding == StringEncodingKind.Utf16LE && s.Value.Contains("UnicodeName"));
    }

    [Fact]
    public void FindsUtf16BeString()
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[8]);
        ms.Write(Encoding.BigEndianUnicode.GetBytes("BigEndianText"));
        ms.Write(new byte[4]);
        var data = ms.ToArray();

        var results = Scan(data, new StringScanOptions
        {
            MinLength = 4, ScanAscii = false, ScanUtf16LE = false, ScanUtf16BE = true
        });
        Assert.Contains(results, s => s.Encoding == StringEncodingKind.Utf16BE && s.Value.Contains("BigEndianText"));
    }

    [Fact]
    public void MinLengthFilter_ExcludesShortStrings()
    {
        var data = Encoding.ASCII.GetBytes("\0ab\0abcdefghij\0");
        var results = Scan(data, new StringScanOptions { MinLength = 5, ScanAscii = true, ScanUtf16LE = false });
        Assert.DoesNotContain(results, s => s.Value == "ab");
        Assert.Contains(results, s => s.Value.Contains("abcdefghij"));
    }

    [Fact]
    public void KeywordFilter_KeepsOnlyMatching()
    {
        var data = Encoding.ASCII.GetBytes("\0AlphaString\0BetaString\0GammaValue\0");
        var results = Scan(data, new StringScanOptions
        {
            MinLength = 4, ScanAscii = true, ScanUtf16LE = false, Keyword = "Beta"
        });
        Assert.All(results, s => Assert.Contains("Beta", s.Value, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, s => s.Value.Contains("BetaString"));
    }

    [Fact]
    public void XenonSample_ContainsHeaderMarkerString()
    {
        var data = SampleFileBuilder.BuildXenonSample();
        var results = Scan(data, new StringScanOptions { MinLength = 6, ScanAscii = true });
        Assert.Contains(results, s => s.Value.Contains("Xenon Data Format v4s"));
    }
}
