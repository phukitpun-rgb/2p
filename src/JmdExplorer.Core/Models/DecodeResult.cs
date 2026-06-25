namespace JmdExplorer.Core.Models;

/// <summary>
/// Outcome of an <see cref="JmdExplorer.Core.Abstractions.IJmdDecoderPlugin"/> run.
/// A plugin must NOT report <see cref="DecodeStatus.AssetDecoded"/> unless it truly
/// produced a usable asset file.
/// </summary>
public sealed class DecodeResult
{
    public DecodeStatus Status { get; init; } = DecodeStatus.NotAttempted;
    public bool Success { get; init; }
    public required string Message { get; init; }

    /// <summary>Paths of files produced (raw blocks and/or real assets).</summary>
    public IReadOnlyList<ProducedFile> ProducedFiles { get; init; } = Array.Empty<ProducedFile>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static DecodeResult Unsupported(string message) => new()
    {
        Status = DecodeStatus.DecoderUnavailable,
        Success = false,
        Message = message
    };
}

public sealed class ProducedFile
{
    public required string Path { get; init; }

    /// <summary>True when the file is raw carved bytes (not a verified asset).</summary>
    public bool IsRawBlock { get; init; } = true;

    public string Description { get; init; } = "Raw binary block (meaning unverified)";
}
