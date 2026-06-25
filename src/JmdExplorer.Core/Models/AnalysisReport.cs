namespace JmdExplorer.Core.Models;

/// <summary>
/// Aggregated, serializable snapshot of everything the app learned about a file.
/// Used by the report writers (TXT/JSON/HTML).
/// </summary>
public sealed class AnalysisReport
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long FileSize { get; set; }
    public string? Sha256 { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    public string? HeaderText { get; set; }
    public string DetectedFormat { get; set; } = "Unknown binary";
    public string Confidence { get; set; } = "None";
    public string DecodeStatus { get; set; } = "";
    public bool DecoderAvailable { get; set; }
    public string Mode { get; set; } = "Inspection only";

    public List<Region> Regions { get; set; } = new();
    public List<EmbeddedSignatureMatch> Signatures { get; set; } = new();
    public List<RecordPatternCandidate> RecordPatterns { get; set; } = new();
    public List<DecoderPluginStatus> DecoderPlugins { get; set; } = new();
    public List<string> ExtractedFiles { get; set; } = new();

    public List<string> Warnings { get; set; } = new()
    {
        "The extracted payload is raw binary data and is not guaranteed to be directly " +
        "usable as a model, texture, or configuration file."
    };
}

public sealed class DecoderPluginStatus
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Status { get; set; } = "";
    public string SupportedFormat { get; set; } = "";
}
