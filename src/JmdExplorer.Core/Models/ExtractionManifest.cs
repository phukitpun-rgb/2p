namespace JmdExplorer.Core.Models;

/// <summary>Sidecar manifest describing what was carved out of a source file.</summary>
public sealed class ExtractionManifest
{
    public string SourceFile { get; set; } = "";
    public string Format { get; set; } = "Unknown binary";
    public string? Sha256 { get; set; }
    public DateTime ExtractedAtUtc { get; set; } = DateTime.UtcNow;
    public string Disclaimer { get; set; } =
        "Extracted blocks are raw binary data. They are not guaranteed to be directly " +
        "usable as a model, texture, or configuration file unless a verified decoder exists.";
    public List<ExtractedRegionEntry> Regions { get; set; } = new();
}

public sealed class ExtractedRegionEntry
{
    public string Name { get; set; } = "";
    public string StartOffset { get; set; } = "0x0";
    public string EndOffset { get; set; } = "0x0";
    public long Size { get; set; }
    public string Type { get; set; } = "unknown_structured_binary";
    public string FileName { get; set; } = "";
}
