using JmdExplorer.Core.Services;
using Xunit;

namespace JmdExplorer.Tests;

public class DdsImageTests
{
    /// <summary>Builds a 4x4 DXT1 DDS whose single block is solid white (all texels use color0).</summary>
    private static byte[] BuildDxt1White4x4()
    {
        var d = new byte[128 + 8];
        void U32(int o, uint v) { d[o] = (byte)v; d[o + 1] = (byte)(v >> 8); d[o + 2] = (byte)(v >> 16); d[o + 3] = (byte)(v >> 24); }
        d[0] = (byte)'D'; d[1] = (byte)'D'; d[2] = (byte)'S'; d[3] = (byte)' ';
        U32(4, 124);
        U32(12, 4); // height
        U32(16, 4); // width
        U32(28, 1); // mip count
        U32(80, 0x4); // DDPF_FOURCC
        d[84] = (byte)'D'; d[85] = (byte)'X'; d[86] = (byte)'T'; d[87] = (byte)'1';
        // block: color0 = 0xFFFF (white, > color1 so 4-color mode), color1 = 0, indices = 0
        d[128] = 0xFF; d[129] = 0xFF; d[130] = 0x00; d[131] = 0x00;
        // 4 index bytes already 0 -> every texel = color0 (white)
        return d;
    }

    [Fact]
    public void Decode_Dxt1_ProducesOpaqueWhite()
    {
        var img = DdsImage.Decode(BuildDxt1White4x4());
        Assert.Equal(4, img.Width);
        Assert.Equal(4, img.Height);
        // First pixel should be white, fully opaque.
        Assert.Equal(255, img.Rgba[0]); // R
        Assert.Equal(255, img.Rgba[1]); // G
        Assert.Equal(255, img.Rgba[2]); // B
        Assert.Equal(255, img.Rgba[3]); // A
    }

    [Fact]
    public void IsSupported_RejectsNonDxt()
    {
        var notDds = new byte[128];
        Assert.False(DdsImage.IsSupported(notDds));
    }

    [Fact]
    public void PngWriter_EmitsValidPngSignatureAndChunks()
    {
        var img = DdsImage.Decode(BuildDxt1White4x4());
        using var ms = new MemoryStream();
        PngWriter.WriteRgba(ms, img.Width, img.Height, img.Rgba);
        var bytes = ms.ToArray();

        // PNG signature.
        ReadOnlySpan<byte> sig = stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Assert.True(bytes.AsSpan(0, 8).SequenceEqual(sig));
        // Must contain IHDR, IDAT, IEND chunk type markers.
        string asText = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("IHDR", asText);
        Assert.Contains("IDAT", asText);
        Assert.Contains("IEND", asText);
    }
}
