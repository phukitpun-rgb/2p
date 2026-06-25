using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using JmdExplorer.App.Services;
using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;
using JmdExplorer.Core.Services;

namespace JmdExplorer.App.ViewModels;

public sealed partial class EmbeddedScannerViewModel : ScanViewModelBase
{
    private readonly IMessenger _messenger;
    private readonly IDialogService _dialogs;
    private readonly RawBlockExtractor _extractor;

    public EmbeddedScannerViewModel(
        AppSession session, IAppLogger logger, IMessenger messenger,
        IDialogService dialogs, RawBlockExtractor extractor)
        : base(session, logger)
    {
        _messenger = messenger;
        _dialogs = dialogs;
        _extractor = extractor;
    }

    public override string Title => "Embedded File Scanner";
    public override string Glyph => "E721"; // search

    public ObservableCollection<EmbeddedSignatureMatch> Signatures => Session.Signatures;

    [ObservableProperty] private bool _scanned;
    [ObservableProperty] private string _emptyMessage = "";

    [RelayCommand]
    private async Task Scan()
    {
        await RunScanAsync(async (ct, progress) =>
        {
            await Session.ScanSignaturesAsync(ct, progress);
        }, "Scanning for embedded file signatures...");

        Scanned = true;
        EmptyMessage = Session.Signatures.Count == 0
            ? "No standard embedded file signatures found.\n" +
              "This does not mean the file is empty.\n" +
              "The content may be compressed, encrypted, serialized, or proprietary."
            : "";
    }

    [RelayCommand]
    private void OpenInHex(EmbeddedSignatureMatch? match)
    {
        if (match is null) return;
        _messenger.Send(new NavigateToHexOffsetMessage(match.Offset, match.MagicBytes.Length));
    }

    /// <summary>Maps a known signature type to a real file extension. Only used when the
    /// exact, complete file size is known, so a carved file is never given a real
    /// extension unless it is genuinely a complete asset.</summary>
    private static string ExtensionFor(string type) => type switch
    {
        "DDS" => "dds",
        "PNG" => "png",
        "JPEG" => "jpg",
        "BMP" => "bmp",
        "WAV (RIFF)" => "wav",
        "OGG" => "ogg",
        "ZIP" => "zip",
        _ => "bin"
    };

    private ExtractionRequest BuildRequest(EmbeddedSignatureMatch match)
    {
        bool sizeKnown = match.SizeEstimate is > 0;
        long size = sizeKnown
            ? match.SizeEstimate!.Value
            // Unknown size: carve a conservative window so we never fabricate an asset.
            : Math.Min(64 * 1024, Session.Context!.Length - match.Offset);

        return new ExtractionRequest
        {
            Name = $"embedded_{match.Type}_0x{match.Offset:X}",
            StartOffset = match.Offset,
            EndOffset = match.Offset + size,
            Type = $"embedded_signature:{match.Type}",
            // Real extension only when the complete length was parsed from the header.
            Extension = sizeKnown ? ExtensionFor(match.Type) : "bin"
        };
    }

    [RelayCommand]
    private void ExtractRaw(EmbeddedSignatureMatch? match)
    {
        if (match is null || Session.Context is null) return;

        string? folder = _dialogs.PickFolder("Choose output folder for extracted file");
        if (folder is null) return;

        try
        {
            var outcome = _extractor.Extract(
                Session.Context.FilePath, folder, new[] { BuildRequest(match) },
                Session.Format?.FormatName ?? "Unknown binary", Session.Context.Sha256);

            foreach (var f in outcome.BinFiles) Session.ExtractedFiles.Add(f);
            bool complete = match.SizeEstimate is > 0;
            _dialogs.ShowMessage(
                complete
                    ? $"Extracted a complete {match.Type} ({match.SizeEstimate} bytes) using its parsed header size."
                    : $"Carved {outcome.BinFiles.Count} raw block(s).\n\n" +
                      "Note: a signature is not proof of a complete, valid embedded file. " +
                      "The carved bytes are raw and unverified.",
                "Extraction complete");
        }
        catch (Exception ex)
        {
            Logger.Error("Embedded extract failed", ex);
            _dialogs.ShowMessage($"Extraction failed: {ex.Message}", "Error", isError: true);
        }
    }

    /// <summary>Extracts every scanned signature in one pass. Complete-size matches
    /// (e.g. DDS) become real assets; unknown-size matches are carved raw as .bin.</summary>
    [RelayCommand]
    private void ExtractAll()
    {
        if (Session.Context is null || Signatures.Count == 0) return;

        string? folder = _dialogs.PickFolder("Choose output folder for all extracted files");
        if (folder is null) return;

        try
        {
            var requests = Signatures.Select(BuildRequest).ToList();
            var outcome = _extractor.Extract(
                Session.Context.FilePath, folder, requests,
                Session.Format?.FormatName ?? "Unknown binary", Session.Context.Sha256);

            foreach (var f in outcome.BinFiles) Session.ExtractedFiles.Add(f);
            int complete = Signatures.Count(s => s.SizeEstimate is > 0);
            _dialogs.ShowMessage(
                $"Extracted {outcome.BinFiles.Count} file(s) to:\n{folder}\n\n" +
                $"{complete} complete asset(s) with header-derived sizes; " +
                $"{outcome.BinFiles.Count - complete} raw carve(s) as .bin.\n" +
                "A manifest.json was written alongside the files.",
                "Extract all complete");
        }
        catch (Exception ex)
        {
            Logger.Error("Extract-all failed", ex);
            _dialogs.ShowMessage($"Extraction failed: {ex.Message}", "Error", isError: true);
        }
    }

    protected override void OnFileLoaded()
    {
        base.OnFileLoaded();
        Scanned = false;
        EmptyMessage = "";
    }
}
