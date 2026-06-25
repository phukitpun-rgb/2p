namespace JmdExplorer.Core.Models;

/// <summary>
/// The result of running an <see cref="JmdExplorer.Core.Abstractions.IFormatProfile"/>
/// against a file. Describes what was recognized without ever asserting that the
/// payload was decoded.
/// </summary>
public sealed class FormatAnalysisResult
{
    /// <summary>Human readable format name, e.g. "Xenon Data Format v4s".</summary>
    public string FormatName { get; init; } = "Unknown binary";

    public Confidence Confidence { get; init; } = Confidence.None;

    /// <summary>Version string if one could be parsed from the header (may be null).</summary>
    public string? Version { get; init; }

    /// <summary>Offset where the recognized header text/magic begins.</summary>
    public long HeaderOffset { get; init; }

    /// <summary>The decoded header text if it is human readable.</summary>
    public string? HeaderText { get; init; }

    /// <summary>Raw magic bytes used for detection (for display).</summary>
    public byte[] MagicBytes { get; init; } = Array.Empty<byte>();

    /// <summary>True only if a verified decoder can turn this into a usable asset.</summary>
    public bool DecoderAvailable { get; init; }

    /// <summary>Short status word for the decoder, e.g. "Not implemented".</summary>
    public string DecoderStatus { get; init; } = "Not implemented";

    /// <summary>Operating mode, e.g. "Inspection only".</summary>
    public string Mode { get; init; } = "Inspection only";

    /// <summary>The honesty state this analysis maps to.</summary>
    public DecodeStatus Status { get; init; } = DecodeStatus.DecoderUnavailable;

    /// <summary>Optional heuristic hints (region boundaries) the profile can supply.</summary>
    public IReadOnlyList<RegionHint> RegionHints { get; init; } = Array.Empty<RegionHint>();

    /// <summary>Free-form notes / limitations.</summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    public static FormatAnalysisResult Unknown(long size) => new()
    {
        FormatName = "Unknown binary",
        Confidence = Confidence.None,
        DecoderAvailable = false,
        DecoderStatus = "Not applicable",
        Mode = "Inspection only",
        Status = DecodeStatus.DecoderUnavailable,
        Notes = new[]
        {
            "No known format profile matched this file.",
            "The content may be compressed, encrypted, serialized, or proprietary."
        }
    };
}

/// <summary>A non-authoritative hint about where a region is likely to be.</summary>
public sealed class RegionHint
{
    public required string Name { get; init; }
    public long StartOffset { get; init; }
    public long? EndOffset { get; init; }
    public RegionType ExpectedType { get; init; } = RegionType.Unknown;
    public string? Note { get; init; }
}
