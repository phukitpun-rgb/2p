using CommunityToolkit.Mvvm.ComponentModel;
using JmdExplorer.App.Services;
using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;

namespace JmdExplorer.App.ViewModels;

/// <summary>
/// The central "what is this file?" page. Surfaces the honesty fields from the spec:
/// it always separates "structure recognized" from "asset decoded".
/// </summary>
public sealed partial class FileInspectorViewModel : ObservableObject, IPageViewModel
{
    private readonly AppSession _session;

    public FileInspectorViewModel(AppSession session)
    {
        _session = session;
        _session.FileLoaded += (_, _) => Refresh();
    }

    public string Title => "File Inspector";
    public string Glyph => ""; // document

    public bool HasFile => _session.Context is not null;

    [ObservableProperty] private string _fileName = "";
    [ObservableProperty] private string _fullPath = "";
    [ObservableProperty] private string _fileSize = "";
    [ObservableProperty] private string _sha256 = "";
    [ObservableProperty] private string _createdDate = "";
    [ObservableProperty] private string _modifiedDate = "";
    [ObservableProperty] private string _headerText = "";
    [ObservableProperty] private string _detectedFormat = "";
    [ObservableProperty] private string _confidence = "";
    [ObservableProperty] private string _decodeStatus = "";

    // Honesty status lines (mirrors the spec examples).
    [ObservableProperty] private string _detectedLine = "";
    [ObservableProperty] private string _statusLine = "";
    [ObservableProperty] private string _embeddedAssetsLine = "";
    [ObservableProperty] private string _payloadRegionLine = "";
    [ObservableProperty] private string _extractionResultLine = "";

    [ObservableProperty] private string _confidenceColorKey = "BadgeNeutralBrush";

    private void Refresh()
    {
        var ctx = _session.Context;
        var fmt = _session.Format;
        OnPropertyChanged(nameof(HasFile));
        if (ctx is null || fmt is null) return;

        FileName = ctx.FileName;
        FullPath = ctx.FilePath;
        FileSize = $"{ctx.Length:N0} bytes ({FormatBytes(ctx.Length)})";
        Sha256 = ctx.Sha256 ?? "(computing...)";
        CreatedDate = ctx.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        ModifiedDate = ctx.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        HeaderText = fmt.HeaderText ?? "(no readable header text)";
        DetectedFormat = fmt.FormatName;
        Confidence = fmt.Confidence.ToString();
        DecodeStatus = fmt.Status.ToDisplayString();

        DetectedLine = $"Detected: {fmt.FormatName}";
        StatusLine = fmt.DecoderAvailable
            ? "Status: Decoder available"
            : "Status: Structure recognized, decoder unavailable";
        EmbeddedAssetsLine = _session.SignatureScanRun
            ? (_session.Signatures.Count > 0
                ? $"Embedded assets: {_session.Signatures.Count} signature(s) found (unverified)"
                : "Embedded assets: None confirmed")
            : "Embedded assets: Not scanned yet";
        PayloadRegionLine = _session.Regions.Any(r => r.Type == RegionType.HighEntropyPayload)
            ? "Payload region: Detected"
            : "Payload region: Run Structure Analyzer to detect";
        ExtractionResultLine = _session.ExtractedFiles.Count > 0
            ? "Extraction result: Raw blocks only"
            : "Extraction result: Nothing extracted yet";

        ConfidenceColorKey = fmt.Confidence switch
        {
            Confidence.High => "BadgeGoodBrush",
            Confidence.Medium => "BadgeWarnBrush",
            Confidence.Low => "BadgeWarnBrush",
            _ => "BadgeNeutralBrush"
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.##} {units[unit]}";
    }
}
