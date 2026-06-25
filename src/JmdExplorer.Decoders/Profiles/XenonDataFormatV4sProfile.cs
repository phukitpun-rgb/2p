using System.Text;
using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;

namespace JmdExplorer.Decoders.Profiles;

/// <summary>
/// Recognizes files carrying the ASCII header text "Xenon Data Format v4s". This
/// profile ONLY recognizes structure — it explicitly reports that no decoder exists.
/// </summary>
public sealed class XenonDataFormatV4sProfile : IFormatProfile
{
    public const string Marker = "Xenon Data Format v4s";
    private static readonly byte[] MarkerBytes = Encoding.ASCII.GetBytes(Marker);

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
            return IndexOf(buf, MarkerBytes) >= 0;
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
        int markerOffset = IndexOf(buf.AsSpan(0, read), MarkerBytes);

        string? version = TryParseVersion(buf.AsSpan(0, read), markerOffset);
        string headerText = ExtractHeaderText(buf.AsSpan(0, read), markerOffset);

        // Heuristic hints only — the region detector still discovers boundaries itself.
        var hints = new List<RegionHint>
        {
            new() { Name = "Header", StartOffset = 0, ExpectedType = RegionType.Header,
                    Note = "Xenon v4s format header" }
        };

        return new FormatAnalysisResult
        {
            FormatName = Name,
            Confidence = Confidence.High,
            Version = version,
            HeaderOffset = markerOffset < 0 ? 0 : markerOffset,
            HeaderText = headerText,
            MagicBytes = MarkerBytes,
            DecoderAvailable = false,            // <-- the whole point: no real decoder yet
            DecoderStatus = "Not implemented",
            Mode = "Inspection only",
            Status = DecodeStatus.StructureRecognized,
            RegionHints = hints,
            Notes = new[]
            {
                "Format header recognized as 'Xenon Data Format v4s'.",
                "No verified decoder is currently available for this format.",
                "Inspection, region analysis, and raw block extraction are available; " +
                "decoding into a usable asset is NOT."
            }
        };
    }

    private static string ExtractHeaderText(ReadOnlySpan<byte> buf, int markerOffset)
    {
        if (markerOffset < 0) return Marker;
        // Read printable run starting at the marker.
        int end = markerOffset;
        while (end < buf.Length && buf[end] >= 0x20 && buf[end] <= 0x7E) end++;
        return Encoding.ASCII.GetString(buf.Slice(markerOffset, end - markerOffset));
    }

    private static string? TryParseVersion(ReadOnlySpan<byte> buf, int markerOffset)
    {
        // The marker itself ends in "v4s"; surface that as the version.
        if (markerOffset < 0) return "4s";
        return "4s";
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
