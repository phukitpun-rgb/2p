namespace JmdExplorer.Core.Models;

/// <summary>
/// A potential embedded file signature located inside a binary. The presence of a
/// signature is never treated as proof that a complete, valid file is embedded —
/// hence <see cref="SizeEstimate"/> is nullable and confidence is qualitative.
/// </summary>
public sealed class EmbeddedSignatureMatch
{
    public required string Type { get; init; }
    public long Offset { get; init; }
    public Confidence Confidence { get; init; } = Confidence.Low;

    /// <summary>Estimated size in bytes if it could be derived; null if unknown.</summary>
    public long? SizeEstimate { get; init; }

    /// <summary>Suggested user action, e.g. "Extract", "Extract Raw", "View".</summary>
    public string Action { get; init; } = "Extract Raw";

    /// <summary>The matched magic bytes (for display).</summary>
    public byte[] MagicBytes { get; init; } = Array.Empty<byte>();

    public string OffsetHex => $"0x{Offset:X8}";
}
