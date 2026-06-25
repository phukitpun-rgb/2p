namespace JmdExplorer.Core.Models;

/// <summary>
/// The five honesty states from the product spec. This enum is the single most
/// important type in the app: it forces a clear separation between "we understood
/// the file's structure" and "we actually decoded a usable asset".
/// </summary>
public enum DecodeStatus
{
    /// <summary>Nothing has been attempted yet (no file loaded).</summary>
    NotAttempted = 0,

    /// <summary>(1) The high-level structure of the file was recognized.</summary>
    StructureRecognized = 1,

    /// <summary>(2) One or more embedded file signatures were found inside the payload.</summary>
    EmbeddedSignatureFound = 2,

    /// <summary>(3) A raw block/region was carved out to disk (bytes only, meaning unknown).</summary>
    RawBlockCarved = 3,

    /// <summary>(4) The content was decoded into a real, usable asset.</summary>
    AssetDecoded = 4,

    /// <summary>(5) No decoder exists for this format; inspection only.</summary>
    DecoderUnavailable = 5
}

public static class DecodeStatusExtensions
{
    public static string ToDisplayString(this DecodeStatus status) => status switch
    {
        DecodeStatus.NotAttempted => "No file loaded",
        DecodeStatus.StructureRecognized => "Structure recognized, decoder unavailable",
        DecodeStatus.EmbeddedSignatureFound => "Embedded signature(s) found",
        DecodeStatus.RawBlockCarved => "Raw blocks carved (meaning unverified)",
        DecodeStatus.AssetDecoded => "Asset decoded",
        DecodeStatus.DecoderUnavailable => "Inspection only — no decoder available",
        _ => status.ToString()
    };
}
