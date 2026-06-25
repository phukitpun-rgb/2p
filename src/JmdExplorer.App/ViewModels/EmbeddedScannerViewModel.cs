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

    [RelayCommand]
    private void ExtractRaw(EmbeddedSignatureMatch? match)
    {
        if (match is null || Session.Context is null) return;

        long size = match.SizeEstimate ?? 0;
        if (size <= 0)
        {
            // Unknown size: extract a conservative window so we never fabricate an asset.
            size = Math.Min(64 * 1024, Session.Context.Length - match.Offset);
        }

        string? folder = _dialogs.PickFolder("Choose output folder for raw block");
        if (folder is null) return;

        try
        {
            var req = new ExtractionRequest
            {
                Name = $"embedded_{match.Type}",
                StartOffset = match.Offset,
                EndOffset = match.Offset + size,
                Type = $"embedded_signature:{match.Type}"
            };
            var outcome = _extractor.Extract(
                Session.Context.FilePath, folder, new[] { req },
                Session.Format?.FormatName ?? "Unknown binary", Session.Context.Sha256);

            foreach (var f in outcome.BinFiles) Session.ExtractedFiles.Add(f);
            _dialogs.ShowMessage(
                $"Carved {outcome.BinFiles.Count} raw block(s).\n\n" +
                "Note: a signature is not proof of a complete, valid embedded file. " +
                "The carved bytes are raw and unverified.",
                "Raw extraction complete");
        }
        catch (Exception ex)
        {
            Logger.Error("Embedded extract failed", ex);
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
