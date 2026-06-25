namespace JmdExplorer.Core.Services;

/// <summary>
/// Decodes the common block-compressed DDS texture formats (DXT1/DXT2/DXT3/DXT4/DXT5)
/// to raw 32-bit RGBA pixels. This is an honest decoder: it only handles formats it
/// truly understands and throws for anything else, rather than producing a wrong image.
/// </summary>
public static class DdsImage
{
    public sealed record Decoded(int Width, int Height, byte[] Rgba);

    public static bool IsSupported(ReadOnlySpan<byte> dds)
    {
        if (dds.Length < 128) return false;
        if (dds[0] != 'D' || dds[1] != 'D' || dds[2] != 'S' || dds[3] != ' ') return false;
        string fourCc = System.Text.Encoding.ASCII.GetString(dds.Slice(84, 4));
        return fourCc is "DXT1" or "DXT2" or "DXT3" or "DXT4" or "DXT5";
    }

    /// <summary>Decodes the top mip level of a DXT-compressed DDS to RGBA8888.</summary>
    public static Decoded Decode(byte[] dds)
    {
        if (!IsSupported(dds)) throw new NotSupportedException("Not a supported DXT-compressed DDS.");

        int height = (int)U32(dds, 12);
        int width = (int)U32(dds, 16);
        string fourCc = System.Text.Encoding.ASCII.GetString(dds, 84, 4);
        bool hasExplicitAlpha = fourCc is "DXT2" or "DXT3";
        bool hasInterpAlpha = fourCc is "DXT4" or "DXT5";
        bool dxt1 = fourCc == "DXT1";

        var rgba = new byte[width * height * 4];
        int p = 128;
        int blocksX = (width + 3) / 4;
        int blocksY = (height + 3) / 4;

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                Span<byte> alpha = stackalloc byte[16];
                for (int i = 0; i < 16; i++) alpha[i] = 255;

                if (hasExplicitAlpha)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        byte two = dds[p + i];
                        alpha[i * 2] = (byte)((two & 0x0F) * 17);
                        alpha[i * 2 + 1] = (byte)((two >> 4) * 17);
                    }
                    p += 8;
                }
                else if (hasInterpAlpha)
                {
                    DecodeDxt5Alpha(dds, p, alpha);
                    p += 8;
                }

                ushort c0 = (ushort)U16(dds, p);
                ushort c1 = (ushort)U16(dds, p + 2);
                uint bits = U32(dds, p + 4);
                p += 8;

                Span<(byte r, byte g, byte b)> palette = stackalloc (byte, byte, byte)[4];
                palette[0] = Rgb565(c0);
                palette[1] = Rgb565(c1);
                if (dxt1 && c0 <= c1)
                {
                    palette[2] = ((byte)((palette[0].r + palette[1].r) / 2),
                                 (byte)((palette[0].g + palette[1].g) / 2),
                                 (byte)((palette[0].b + palette[1].b) / 2));
                    palette[3] = (0, 0, 0); // index 3 is transparent black in DXT1 1-bit-alpha mode
                }
                else
                {
                    palette[2] = ((byte)((2 * palette[0].r + palette[1].r) / 3),
                                 (byte)((2 * palette[0].g + palette[1].g) / 3),
                                 (byte)((2 * palette[0].b + palette[1].b) / 3));
                    palette[3] = ((byte)((palette[0].r + 2 * palette[1].r) / 3),
                                 (byte)((palette[0].g + 2 * palette[1].g) / 3),
                                 (byte)((palette[0].b + 2 * palette[1].b) / 3));
                }

                for (int yy = 0; yy < 4; yy++)
                {
                    for (int xx = 0; xx < 4; xx++)
                    {
                        int texel = 4 * yy + xx;
                        int idx = (int)((bits >> (2 * texel)) & 3);
                        int x = bx * 4 + xx, y = by * 4 + yy;
                        if (x >= width || y >= height) continue;
                        var col = palette[idx];
                        byte a = dxt1 && c0 <= c1 && idx == 3 ? (byte)0 : alpha[texel];
                        int o = (y * width + x) * 4;
                        rgba[o] = col.r; rgba[o + 1] = col.g; rgba[o + 2] = col.b; rgba[o + 3] = a;
                    }
                }
            }
        }
        return new Decoded(width, height, rgba);
    }

    private static void DecodeDxt5Alpha(byte[] d, int p, Span<byte> alpha)
    {
        byte a0 = d[p], a1 = d[p + 1];
        Span<byte> a = stackalloc byte[8];
        a[0] = a0; a[1] = a1;
        if (a0 > a1)
            for (int i = 1; i < 7; i++) a[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
        else
        {
            for (int i = 1; i < 5; i++) a[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
            a[6] = 0; a[7] = 255;
        }
        ulong bits = 0;
        for (int i = 0; i < 6; i++) bits |= (ulong)d[p + 2 + i] << (8 * i);
        for (int i = 0; i < 16; i++) alpha[i] = a[(int)((bits >> (3 * i)) & 7)];
    }

    private static (byte r, byte g, byte b) Rgb565(ushort c)
    {
        int r = (c >> 11) & 0x1F, g = (c >> 5) & 0x3F, b = c & 0x1F;
        return ((byte)(r << 3 | r >> 2), (byte)(g << 2 | g >> 4), (byte)(b << 3 | b >> 2));
    }

    private static uint U32(byte[] b, int o) => (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
    private static int U16(byte[] b, int o) => b[o] | (b[o + 1] << 8);
}
