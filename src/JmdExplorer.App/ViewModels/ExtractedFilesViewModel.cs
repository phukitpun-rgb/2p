using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JmdExplorer.App.Services;
using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;
using JmdExplorer.Core.Services;

namespace JmdExplorer.App.ViewModels;

/// <summary>
/// Raw block extraction UI. Everything it produces is explicitly raw bytes — never a
/// decoded asset. Manifests are written alongside the .bin files.
/// </summary>
public sealed partial class ExtractedFilesViewModel : ObservableObject, IPageViewModel
{
    private readonly AppSession _session;
    private readonly RawBlockExtractor _extractor;
    private readonly IDialogService _dialogs;
    private readonly IAppLogger _logger;

    public ExtractedFilesViewModel(
        AppSession session, RawBlockExtractor extractor, IDialogService dialogs, IAppLogger logger)
    {
        _session = session;
        _extractor = extractor;
        _dialogs = dialogs;
        _logger = logger;
        _session.FileLoaded += (_, _) => OnPropertyChanged(nameof(HasFile));
    }

    public string Title => "Extracted Files";
    public string Glyph => "E8B7"; // folder

    public bool HasFile => _session.Context is not null;

    public ObservableCollection<string> ExtractedFiles => _session.ExtractedFiles;

    [ObservableProperty] private string _rangeStart = "0x0";
    [ObservableProperty] private string _rangeEnd = "";
    [ObservableProperty] private string _statusMessage = "";

    [RelayCommand]
    private void ExtractRange()
    {
        if (_session.Context is null) return;
        long? start = ParseOffset(RangeStart);
        long? end = string.IsNullOrWhiteSpace(RangeEnd) ? _session.Context.Length : ParseOffset(RangeEnd);
        if (start is null || end is null || end <= start)
        {
            StatusMessage = "Invalid range. Example: start 0x100, end 0x12C00.";
            return;
        }
        ExtractRequests(new[]
        {
            new ExtractionRequest { Name = "range", StartOffset = start.Value, EndOffset = end.Value }
        });
    }

    [RelayCommand]
    private void ExtractAllRegions()
    {
        if (_session.Context is null) return;
        if (_session.Regions.Count == 0)
        {
            StatusMessage = "No regions detected yet. Run the Structure Analyzer first.";
            return;
        }
        var requests = _session.Regions.Select(r => new ExtractionRequest
        {
            Name = r.Name,
            StartOffset = r.StartOffset,
            EndOffset = r.EndOffset,
            Type = r.Type.ToString()
        }).ToList();
        ExtractRequests(requests);
    }

    private void ExtractRequests(IReadOnlyList<ExtractionRequest> requests)
    {
        if (_session.Context is null) return;
        string? folder = _dialogs.PickFolder("Choose output folder for extracted blocks");
        if (folder is null) return;

        try
        {
            var outcome = _extractor.Extract(
                _session.Context.FilePath, folder, requests,
                _session.Format?.FormatName ?? "Unknown binary", _session.Context.Sha256);

            foreach (var f in outcome.BinFiles) _session.ExtractedFiles.Add(f);
            _session.ExtractedFiles.Add(outcome.ManifestPath);

            StatusMessage = $"Extracted {outcome.BinFiles.Count} block(s) + manifest to {folder}";
            _dialogs.ShowMessage(
                $"Wrote {outcome.BinFiles.Count} raw .bin file(s) and a manifest.\n\n" +
                "These are raw binary blocks. They are NOT guaranteed to be usable as a model, " +
                "texture, or configuration file unless a verified decoder exists.",
                "Extraction complete");
        }
        catch (Exception ex)
        {
            _logger.Error("Extraction failed", ex);
            _dialogs.ShowMessage($"Extraction failed: {ex.Message}", "Error", isError: true);
        }
    }

    [RelayCommand]
    private void OpenContainingFolder(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); }
        catch (Exception ex) { _logger.Warn($"Could not open folder: {ex.Message}"); }
    }

    private static long? ParseOffset(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(input.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out long h) ? h : null;
        return long.TryParse(input, out long d) ? d : null;
    }
}
