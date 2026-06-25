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

    /// <summary>Builds a minimal but valid DDS_HEADER (no surface data).</summary>
    private static byte[] BuildDdsHeader(uint width, uint height, uint mipCount, string fourCc)
    {
        var h = new byte[128];
        void U32(int o, uint v) { h[o] = (byte)v; h[o + 1] = (byte)(v >> 8); h[o + 2] = (byte)(v >> 16); h[o + 3] = (byte)(v >> 24); }
        h[0] = (byte)'D'; h[1] = (byte)'D'; h[2] = (byte)'S'; h[3] = (byte)' ';
        U32(4, 124);                 // dwSize
        U32(12, height);             // dwHeight
        U32(16, width);              // dwWidth
        U32(28, mipCount);           // dwMipMapCount
        U32(80, 0x4);                // ddspf.dwFlags = DDPF_FOURCC
        for (int i = 0; i < 4; i++) h[84 + i] = (byte)fourCc[i]; // ddspf.dwFourCC
        return h;
    }

    [Fact]
    public void DdsSizeEstimate_ComputesExactDxt3Length()
    {
        // DXT3 32x32 with 6 mip levels = 128-byte header + 1392 bytes of surface = 1520.
        var header = BuildDdsHeader(32, 32, 6, "DXT3");
        using var ms = new MemoryStream();
        ms.Write(header);
        ms.Write(new byte[1520 - 128]); // surface payload
        var data = ms.ToArray();

        var scanner = new SignatureScanner();
        using var stream = new MemoryStream(data);
        var match = scanner.Scan(stream, data.Length).First(m => m.Type == "DDS");

        Assert.Equal(1520L, match.SizeEstimate);
    }

    [Fact]
    public void DdsSizeEstimate_NullForMalformedHeader()
    {
        // "DDS " magic present but dwSize is not 124 -> must not fabricate a size.
        var bad = new byte[128];
        bad[0] = (byte)'D'; bad[1] = (byte)'D'; bad[2] = (byte)'S'; bad[3] = (byte)' ';
        bad[4] = 99; // wrong dwSize

        var scanner = new SignatureScanner();
        using var stream = new MemoryStream(bad);
        var match = scanner.Scan(stream, bad.Length).First(m => m.Type == "DDS");

        Assert.Null(match.SizeEstimate);
    }
}
