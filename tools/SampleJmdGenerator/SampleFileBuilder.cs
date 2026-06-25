using System.Text;

namespace JmdExplorer.Tools.SampleGenerator;

/// <summary>
/// Builds synthetic but realistic .jmd test fixtures in memory. Used by both the CLI
/// generator and the unit tests. The bytes are deterministic for a given seed so tests
/// are repeatable.
/// </summary>
public static class SampleFileBuilder
{
    public const string XenonMarker = "Xenon Data Format v4s";

    // PNG 8-byte signature + IEND terminator, so the signature scanner can size it.
    private static readonly byte[] PngSig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] PngIend = { 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };

    /// <summary>
    /// Layout:
    ///   [0]        256-byte header (Xenon marker + readable metadata strings)
    ///   [256]      low-entropy metadata/index region
    ///   [.. ]      embedded PNG block (signature + body + IEND)
    ///   [.. ]      repeating 64-byte record table
    ///   [.. ]      high-entropy payload (pseudo-random)
    ///   [.. ]      zero padding tail
    /// </summary>
    public static byte[] BuildXenonSample(int seed = 1234)
    {
        var rng = new Random(seed);
        using var ms = new MemoryStream();

        // --- Header (256 bytes) ---
        byte[] header = new byte[256];
        byte[] markerBytes = Encoding.ASCII.GetBytes(XenonMarker);
        Array.Copy(markerBytes, 0, header, 0, markerBytes.Length);
        byte[] meta = Encoding.ASCII.GetBytes("\0vehicle_name=demo_car;version=4s;author=sample;");
        Array.Copy(meta, 0, header, 0x40, meta.Length);
        ms.Write(header, 0, header.Length);

        // --- Low entropy metadata/index region (mostly small repeating values) ---
        byte[] metaRegion = new byte[2048];
        for (int i = 0; i < metaRegion.Length; i++)
            metaRegion[i] = (byte)(i % 8); // very low entropy
        ms.Write(metaRegion, 0, metaRegion.Length);

        // Add a UTF-16LE string so the string scanner can demonstrate UTF-16 detection.
        byte[] u16 = Encoding.Unicode.GetBytes("vehicle_name");
        ms.Write(u16, 0, u16.Length);
        ms.Write(new byte[] { 0, 0 }, 0, 2);

        // --- Embedded PNG block ---
        ms.Write(PngSig, 0, PngSig.Length);
        byte[] pngBody = new byte[4096];
        rng.NextBytes(pngBody);
        ms.Write(pngBody, 0, pngBody.Length);
        ms.Write(PngIend, 0, PngIend.Length);

        // --- Repeating 64-byte records (structured, partially constant columns) ---
        const int recordSize = 64;
        const int recordCount = 300;
        for (int r = 0; r < recordCount; r++)
        {
            byte[] rec = new byte[recordSize];
            // constant-ish columns 0..15 (record type/flags), variable tail.
            for (int c = 0; c < 16; c++) rec[c] = (byte)(c);
            for (int c = 16; c < recordSize; c++) rec[c] = (byte)rng.Next(0, 4);
            ms.Write(rec, 0, rec.Length);
        }

        // --- High entropy payload ---
        byte[] payload = new byte[64 * 1024];
        rng.NextBytes(payload);
        ms.Write(payload, 0, payload.Length);

        // --- Zero padding tail ---
        ms.Write(new byte[1024], 0, 1024);

        return ms.ToArray();
    }

    /// <summary>A plain unknown-binary sample (no recognizable header).</summary>
    public static byte[] BuildUnknownSample(int seed = 99)
    {
        var rng = new Random(seed);
        byte[] data = new byte[32 * 1024];
        rng.NextBytes(data);
        return data;
    }

    /// <summary>A tiny file that contains a readable ASCII string for string-scanner tests.</summary>
    public static byte[] BuildStringSample()
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[16], 0, 16);
        byte[] s = Encoding.ASCII.GetBytes("HelloWorldString");
        ms.Write(s, 0, s.Length);
        ms.Write(new byte[] { 0x00, 0x01, 0x02 }, 0, 3);
        byte[] u16 = Encoding.Unicode.GetBytes("UnicodeName");
        ms.Write(u16, 0, u16.Length);
        ms.Write(new byte[8], 0, 8);
        return ms.ToArray();
    }
}
