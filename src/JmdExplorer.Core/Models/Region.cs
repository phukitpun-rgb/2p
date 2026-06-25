namespace JmdExplorer.Core.Models;

public enum RegionType
{
    Unknown,
    Header,
    Metadata,
    ZeroPadding,
    HighEntropyPayload,
    RepeatingRecord,
    StructuredBinary
}

/// <summary>
/// A contiguous span of the file that the <see cref="JmdExplorer.Core.Services.RegionDetector"/>
/// classified by its statistical characteristics.
/// </summary>
public sealed class Region
{
    public required string Name { get; init; }
    public long StartOffset { get; init; }

    /// <summary>Exclusive end offset (one past the last byte of the region).</summary>
    public long EndOffset { get; init; }

    public long Size => EndOffset - StartOffset;

    public RegionType Type { get; init; } = RegionType.Unknown;

    /// <summary>Shannon entropy (0..8) of this region.</summary>
    public double Entropy { get; init; }

    /// <summary>Fraction of the whole file occupied by this region (0..1).</summary>
    public double PercentOfFile { get; init; }

    /// <summary>0..1 estimate of how repetitive/structured the region looks.</summary>
    public double RepetitionScore { get; init; }

    /// <summary>Best-effort, explicitly non-authoritative interpretation.</summary>
    public string SuggestedInterpretation { get; init; } = "Unknown";

    public string StartOffsetHex => $"0x{StartOffset:X8}";
    public string EndOffsetHex => EndOffset < 0 ? "EOF" : $"0x{EndOffset:X8}";
}
