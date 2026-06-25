using System.IO.Compression;

namespace JmdExplorer.Core.Services;

/// <summary>
/// A tiny, dependency-free PNG encoder for 32-bit RGBA images. It exists so the
/// decoded textures can be written as universally-viewable .png files without
/// pulling in System.Drawing (which is Windows-only and deprecated for libraries).
/// </summary>
public static class PngWriter
{
    public static void WriteRgba(string path, int width, int height, byte[] rgba)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        WriteRgba(fs, width, height, rgba);
    }

    public static void WriteRgba(Stream output, int width, int height, byte[] rgba)
    {
        Span<byte> sig = stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        output.Write(sig);

        // IHDR
        var ihdr = new byte[13];
        WriteBe(ihdr, 0, (uint)width);
        WriteBe(ihdr, 4, (uint)height);
        ihdr[8] = 8;   // bit depth
        ihdr[9] = 6;   // color type RGBA
        ihdr[10] = 0;  // compression
        ihdr[11] = 0;  // filter
        ihdr[12] = 0;  // interlace
        WriteChunk(output, "IHDR", ihdr);

        // IDAT: each scanline prefixed with filter byte 0 (none), then zlib-compressed.
        var raw = new byte[height * (width * 4 + 1)];
        int o = 0;
        for (int y = 0; y < height; y++)
        {
            raw[o++] = 0; // filter: none
            Array.Copy(rgba, y * width * 4, raw, o, width * 4);
            o += width * 4;
        }
        WriteChunk(output, "IDAT", ZlibCompress(raw));

        WriteChunk(output, "IEND", Array.Empty<byte>());
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78); // zlib header: CMF
        ms.WriteByte(0x9C); // FLG (default compression)
        using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(data, 0, data.Length);
        uint adler = Adler32(data);
        ms.WriteByte((byte)(adler >> 24));
        ms.WriteByte((byte)(adler >> 16));
        ms.WriteByte((byte)(adler >> 8));
        ms.WriteByte((byte)adler);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        WriteBe(len, 0, (uint)data.Length);
        s.Write(len);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);

        uint crc = Crc32(typeBytes, data);
        Span<byte> crcb = stackalloc byte[4];
        WriteBe(crcb, 0, crc);
        s.Write(crcb);
    }

    private static void WriteBe(Span<byte> b, int o, uint v)
    {
        b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
    }

    private static uint Adler32(byte[] data)
    {
        const uint mod = 65521;
        uint a = 1, b = 0;
        foreach (byte x in data) { a = (a + x) % mod; b = (b + a) % mod; }
        return (b << 16) | a;
    }

    private static readonly uint[] CrcTable = BuildCrcTable();
    private static uint[] BuildCrcTable()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }

    private static uint Crc32(byte[] a, byte[] b)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte x in a) c = CrcTable[(c ^ x) & 0xFF] ^ (c >> 8);
        foreach (byte x in b) c = CrcTable[(c ^ x) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFF;
    }
}
