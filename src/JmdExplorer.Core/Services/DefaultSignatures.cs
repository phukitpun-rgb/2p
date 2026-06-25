using JmdExplorer.Core.Models;

namespace JmdExplorer.Core.Services;

/// <summary>
/// The built-in catalogue of embedded-file signatures. Sizes are only estimated for
/// formats with a deterministic terminator/footer; everything else reports "Unknown".
/// </summary>
public static class DefaultSignatures
{
    private static byte[] B(params byte[] bytes) => bytes;
    private static byte[] Ascii(string s) => System.Text.Encoding.ASCII.GetBytes(s);

    public static readonly IReadOnlyList<FileSignature> All = new List<FileSignature>
    {
        new() { Type = "PNG", Magic = B(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
                BaseConfidence = Confidence.High, Action = "Extract Raw", EstimateSize = EstimatePng },
        new() { Type = "JPEG", Magic = B(0xFF, 0xD8, 0xFF), BaseConfidence = Confidence.Medium },
        new() { Type = "DDS", Magic = Ascii("DDS "), BaseConfidence = Confidence.High, EstimateSize = EstimateDds },
        new() { Type = "BMP", Magic = Ascii("BM"), BaseConfidence = Confidence.Low },
        new() { Type = "WAV (RIFF)", Magic = Ascii("RIFF"), BaseConfidence = Confidence.Medium, EstimateSize = EstimateRiff },
        new() { Type = "OGG", Magic = Ascii("OggS"), BaseConfidence = Confidence.High },
        new() { Type = "ZIP", Magic = B(0x50, 0x4B, 0x03, 0x04), BaseConfidence = Confidence.High },
        new() { Type = "RAR", Magic = B(0x52, 0x61, 0x72, 0x21, 0x1A, 0x07), BaseConfidence = Confidence.High },
        new() { Type = "7z", Magic = B(0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C), BaseConfidence = Confidence.High },
        new() { Type = "gzip", Magic = B(0x1F, 0x8B, 0x08), BaseConfidence = Confidence.Medium },
        // zlib streams: 0x78 followed by 0x01/0x9C/0xDA (common window/levels).
        new() { Type = "zlib (0x78 01)", Magic = B(0x78, 0x01), BaseConfidence = Confidence.Low },
        new() { Type = "zlib (0x78 9C)", Magic = B(0x78, 0x9C), BaseConfidence = Confidence.Low },
        new() { Type = "zlib (0x78 DA)", Magic = B(0x78, 0xDA), BaseConfidence = Confidence.Low },
        new() { Type = "FBX (binary)", Magic = Ascii("Kaydara FBX Binary"), BaseConfidence = Confidence.High },
        new() { Type = "glTF (binary)", Magic = Ascii("glTF"), BaseConfidence = Confidence.High },
        new() { Type = "OBJ", Magic = Ascii("# Blender"), BaseConfidence = Confidence.Low, Action = "View" },
        new() { Type = "XML", Magic = Ascii("<?xml"), BaseConfidence = Confidence.Medium, Action = "View" },
        new() { Type = "JSON (object)", Magic = Ascii("{\""), BaseConfidence = Confidence.Low, Action = "View" },
        new() { Type = "Lua (bytecode)", Magic = B(0x1B, 0x4C, 0x75, 0x61), BaseConfidence = Confidence.High },
    };

    // --- size estimators -------------------------------------------------

    /// <summary>PNG ends with an IEND chunk: length(0) + "IEND" + CRC. Find it after the match.</summary>
    private static long? EstimatePng(Stream s, long offset)
    {
        // IEND chunk bytes: 00 00 00 00 49 45 4E 44 AE 42 60 82
        ReadOnlySpan<byte> iend = stackalloc byte[] { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };
        long? idx = FindForward(s, offset, iend, maxScan: 64L * 1024 * 1024);
        if (idx is null) return null;
        // size = (idx + len of "IEND...CRC") - offset ; idx points at 'I' of IEND.
        return (idx.Value + iend.Length) - offset;
    }

    /// <summary>DDS header is 128 bytes; total size needs the surface data which we cannot know precisely.</summary>
    private static long? EstimateDds(Stream s, long offset)
    {
        // We can at least confirm the 128-byte header is present; true size needs format math.
        return null;
    }

    /// <summary>RIFF stores total size at offset+4 (little endian, excludes the first 8 bytes).</summary>
    private static long? EstimateRiff(Stream s, long offset)
    {
        s.Seek(offset + 4, SeekOrigin.Begin);
        Span<byte> sz = stackalloc byte[4];
        if (s.Read(sz) != 4) return null;
        long payload = (uint)(sz[0] | (sz[1] << 8) | (sz[2] << 16) | (sz[3] << 24));
        return payload + 8;
    }

    private static long? FindForward(Stream s, long start, ReadOnlySpan<byte> needle, long maxScan)
    {
        var pattern = needle.ToArray();
        s.Seek(start, SeekOrigin.Begin);
        const int chunk = 64 * 1024;
        byte[] buf = new byte[chunk + 16];
        long scanned = 0;
        int carried = 0;
        long bufStart = start;
        while (scanned < maxScan)
        {
            int read = s.Read(buf, carried, chunk);
            int available = carried + read;
            if (available <= 0) break;
            bool eof = read == 0;
            int limit = eof ? available : available - (pattern.Length - 1);
            for (int i = 0; i < limit; i++)
            {
                bool ok = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (buf[i + j] != pattern[j]) { ok = false; break; }
                }
                if (ok) return bufStart + i;
            }
            if (eof) break;
            int keep = pattern.Length - 1;
            Array.Copy(buf, available - keep, buf, 0, keep);
            bufStart += available - keep;
            carried = keep;
            scanned += read;
        }
        return null;
    }
}
