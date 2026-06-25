using System.Text;
using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;

namespace JmdExplorer.Decoders.Profiles;

/// <summary>
/// Recognizes files carrying the header text "Xenon Data Format v4s". Real-world Xenon
/// v4s files store this marker as UTF-16LE, but ASCII and UTF-16BE are also accepted.
/// This profile ONLY recognizes structure — it explicitly reports that no decoder exists.
/// </summary>
public sealed class XenonDataFormatV4sProfile : IFormatProfile
{
    public const string Marker = "Xenon Data Format v4s";

    private static readonly (byte[] Bytes, string Encoding)[] MarkerVariants =
    {
        (Encoding.Unicode.GetBytes(Marker),        "UTF-16LE"),  // observed in real files
        (Encoding.ASCII.GetBytes(Marker),          "ASCII"),
        (Encoding.BigEndianUnicode.GetBytes(Marker), "UTF-16BE")
    };

    public string Name => "Xenon Data Format v4s";

    public bool CanHandle(BinaryReader reader)
    {
        try
        {
            var stream = reader.BaseStream;
            stream.Seek(0, SeekOrigin.Begin);
            // The marker is expected within the first 512 bytes.
            int probe = (int)Math.Min(512, stream.Length);
            byte[] buf = reader.ReadBytes(probe);
            return FindMarker(buf).Offset >= 0;
        }
        catch
        {
            return false;
        }
    }

    public FormatAnalysisResult Analyze(Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        int probe = (int)Math.Min(512, stream.Length);
        byte[] buf = new byte[probe];
        int read = stream.Read(buf, 0, probe);

        var (offset, encoding, matchedBytes) = FindMarker(buf.AsSpan(0, read));

        // Heuristic hints only — the region detector still discovers boundaries itself.
        var hints = new List<RegionHint>
        {
            new() { Name = "Header", StartOffset = 0, ExpectedType = RegionType.Header,
                    Note = $"Xenon v4s format header ({encoding})" }
        };

        return new FormatAnalysisResult
        {
            FormatName = Name,
            Confidence = Confidence.High,
            Version = "4s",
            HeaderOffset = offset < 0 ? 0 : offset,
            HeaderText = $"{Marker}  [{encoding}]",
            MagicBytes = matchedBytes,
            DecoderAvailable = false,            // <-- the whole point: no real decoder yet
            DecoderStatus = "Not implemented",
            Mode = "Inspection only",
            Status = DecodeStatus.StructureRecognized,
            RegionHints = hints,
            Notes = new[]
            {
                $"Format header recognized as 'Xenon Data Format v4s' (encoded as {encoding}).",
                "No verified decoder is currently available for this format.",
                "Inspection, region analysis, and raw block extraction are available; " +
                "decoding into a usable asset is NOT."
            }
        };
    }

    /// <summary>Finds the marker in any supported encoding; returns offset -1 if absent.</summary>
    private static (int Offset, string Encoding, byte[] Bytes) FindMarker(ReadOnlySpan<byte> haystack)
    {
        foreach (var (bytes, enc) in MarkerVariants)
        {
            int idx = IndexOf(haystack, bytes);
            if (idx >= 0) return (idx, enc, bytes);
        }
        return (-1, "ASCII", MarkerVariants[1].Bytes);
    }

    private static int IndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.IsEmpty || haystack.Length < needle.Length) return -1;
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool ok = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { ok = false; break; }
            }
            if (ok) return i;
        }
        return -1;
    }
}
