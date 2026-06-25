using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;
using JmdExplorer.Core.Services;
using JmdExplorer.Infrastructure.Files;

namespace JmdExplorer.App.Services;

/// <summary>
/// Single source of truth for the currently loaded file and all analysis results.
/// ViewModels observe this; it owns no UI. Heavy scans are explicit async methods so
/// the UI can show progress and cancel.
/// </summary>
public sealed partial class AppSession : ObservableObject
{
    private readonly FileService _fileService;
    private readonly FormatDetector _formatDetector;
    private readonly RegionDetector _regionDetector;
    private readonly SignatureScanner _signatureScanner;
    private readonly StringScanner _stringScanner;
    private readonly RecordPatternDetector _recordDetector;
    private readonly IAppLogger _logger;

    public AppSession(
        FileService fileService,
        FormatDetector formatDetector,
        RegionDetector regionDetector,
        SignatureScanner signatureScanner,
        StringScanner stringScanner,
        RecordPatternDetector recordDetector,
        IAppLogger logger)
    {
        _fileService = fileService;
        _formatDetector = formatDetector;
        _regionDetector = regionDetector;
        _signatureScanner = signatureScanner;
        _stringScanner = stringScanner;
        _recordDetector = recordDetector;
        _logger = logger;
    }

    [ObservableProperty]
    private JmdFileContext? _context;

    [ObservableProperty]
    private FormatAnalysisResult? _format;

    [ObservableProperty]
    private bool _hasFile;

    public ObservableCollection<Region> Regions { get; } = new();
    public ObservableCollection<EmbeddedSignatureMatch> Signatures { get; } = new();
    public ObservableCollection<ScannedString> Strings { get; } = new();
    public ObservableCollection<RecordPatternCandidate> RecordPatterns { get; } = new();
    public ObservableCollection<string> ExtractedFiles { get; } = new();

    public bool SignatureScanRun { get; private set; }

    public event EventHandler? FileLoaded;

    public FileService Files => _fileService;

    /// <summary>Loads a file and runs the fast analyses (hash + format detection).</summary>
    public async Task LoadAsync(string path, CancellationToken ct = default)
    {
        var context = await Task.Run(() => _fileService.Load(path), ct);
        context.Sha256 = await Task.Run(() => _fileService.ComputeSha256(context, ct), ct);
        var format = await Task.Run(() => _formatDetector.Detect(context), ct);
        context.Format = format;

        Context = context;
        Format = format;
        HasFile = true;

        Regions.Clear();
        Signatures.Clear();
        Strings.Clear();
        RecordPatterns.Clear();
        ExtractedFiles.Clear();
        SignatureScanRun = false;

        _logger.Info($"Analysis ready for '{context.FileName}': {format.FormatName} ({format.Confidence}).");
        FileLoaded?.Invoke(this, EventArgs.Empty);
    }

    public async Task DetectRegionsAsync(CancellationToken ct, IProgress<double>? progress = null)
    {
        if (Context is null) return;
        var hints = Format?.RegionHints ?? Array.Empty<RegionHint>();
        var options = new RegionDetectionOptions { Hints = hints };
        var regions = await Task.Run(() =>
        {
            using var stream = Context.OpenStream();
            return _regionDetector.Detect(stream, Context.Length, options, ct, progress);
        }, ct);

        Regions.Clear();
        foreach (var r in regions) Regions.Add(r);
    }

    public async Task ScanSignaturesAsync(CancellationToken ct, IProgress<double>? progress = null)
    {
        if (Context is null) return;
        var matches = await Task.Run(() =>
        {
            using var stream = Context.OpenStream();
            return _signatureScanner.Scan(stream, Context.Length, ct, progress);
        }, ct);

        Signatures.Clear();
        foreach (var m in matches) Signatures.Add(m);
        SignatureScanRun = true;
    }

    public async Task ScanStringsAsync(StringScanOptions options, CancellationToken ct, IProgress<double>? progress = null)
    {
        if (Context is null) return;
        var found = await Task.Run(() =>
        {
            using var stream = Context.OpenStream();
            return _stringScanner.Scan(stream, Context.Length, options, ct, progress);
        }, ct);

        Strings.Clear();
        foreach (var s in found) Strings.Add(s);
    }

    public async Task DetectRecordsAsync(long start, long count, CancellationToken ct)
    {
        if (Context is null) return;
        var found = await Task.Run(() =>
        {
            using var stream = Context.OpenStream();
            return _recordDetector.Detect(stream, start, count, null, ct);
        }, ct);

        RecordPatterns.Clear();
        foreach (var r in found) RecordPatterns.Add(r);
    }
}
