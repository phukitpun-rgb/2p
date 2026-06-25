using JmdExplorer.Core.Services;
using JmdExplorer.Tools.SampleGenerator;
using Xunit;

namespace JmdExplorer.Tests;

public class SignatureScannerTests
{
    private static readonly byte[] PngSig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] PngIend = { 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };

    [Fact]
    public void FindsPngSignatureAtKnownOffset()
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[100]);              // padding
        long pngOffset = ms.Position;
        ms.Write(PngSig);
        ms.Write(new byte[50]);
        ms.Write(PngIend);
        var data = ms.ToArray();

        var scanner = new SignatureScanner();
        using var stream = new MemoryStream(data);
        var matches = scanner.Scan(stream, data.Length);

        Assert.Contains(matches, m => m.Type == "PNG" && m.Offset == pngOffset);
    }

    [Fact]
    public void PngSizeEstimate_ReachesIend()
    {
        using var ms = new MemoryStream();
        ms.Write(PngSig);
        ms.Write(new byte[40]);
        ms.Write(PngIend);
        var data = ms.ToArray();

        var scanner = new SignatureScanner();
        using var stream = new MemoryStream(data);
        var match = scanner.Scan(stream, data.Length).First(m => m.Type == "PNG");

        Assert.NotNull(match.SizeEstimate);
        Assert.Equal(data.Length, match.SizeEstimate);
    }

    [Fact]
    public void EmptyOfSignatures_ReturnsNoMatches()
    {
        var data = new byte[8192]; // all zeros, no signatures
        var scanner = new SignatureScanner();
        using var stream = new MemoryStream(data);
        var matches = scanner.Scan(stream, data.Length);
        Assert.Empty(matches);
    }

    [Fact]
    public void FindsSignatureSpanningChunkBoundary()
    {
        // Place a gzip signature so it straddles the 256 KiB internal chunk boundary.
        const int chunk = 256 * 1024;
        using var ms = new MemoryStream();
        ms.Write(new byte[chunk - 1]);
        long off = ms.Position;
        ms.Write(new byte[] { 0x1F, 0x8B, 0x08 }); // gzip
        ms.Write(new byte[100]);
        var data = ms.ToArray();

        var scanner = new SignatureScanner();
        using var stream = new MemoryStream(data);
        var matches = scanner.Scan(stream, data.Length);

        Assert.Contains(matches, m => m.Type == "gzip" && m.Offset == off);
    }

    [Fact]
    public void XenonSample_ContainsEmbeddedPng()
    {
        var data = SampleFileBuilder.BuildXenonSample();
        var scanner = new SignatureScanner();
        using var stream = new MemoryStream(data);
        var matches = scanner.Scan(stream, data.Length);
        Assert.Contains(matches, m => m.Type == "PNG");
    }
}
