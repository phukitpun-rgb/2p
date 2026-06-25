using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using JmdExplorer.App.Services;
using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;
using JmdExplorer.Core.Services;

namespace JmdExplorer.App.ViewModels;

public sealed partial class StringScannerViewModel : ScanViewModelBase
{
    private readonly IMessenger _messenger;

    public StringScannerViewModel(AppSession session, IAppLogger logger, IMessenger messenger)
        : base(session, logger)
    {
        _messenger = messenger;
    }

    public override string Title => "String Scanner";
    public override string Glyph => ""; // font

    public ObservableCollection<ScannedString> Strings => Session.Strings;

    [ObservableProperty] private int _minLength = 4;
    [ObservableProperty] private bool _scanAscii = true;
    [ObservableProperty] private bool _scanUtf16LE = true;
    [ObservableProperty] private bool _scanUtf16BE;
    [ObservableProperty] private bool _printableOnly = true;
    [ObservableProperty] private string _keyword = "";

    [RelayCommand]
    private async Task Scan()
    {
        var options = new StringScanOptions
        {
            MinLength = Math.Max(1, MinLength),
            ScanAscii = ScanAscii,
            ScanUtf16LE = ScanUtf16LE,
            ScanUtf16BE = ScanUtf16BE,
            PrintableOnly = PrintableOnly,
            Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword
        };
        await RunScanAsync(async (ct, progress) =>
        {
            await Session.ScanStringsAsync(options, ct, progress);
        }, "Scanning for strings...");
    }

    [RelayCommand]
    private void OpenInHex(ScannedString? s)
    {
        if (s is null) return;
        _messenger.Send(new NavigateToHexOffsetMessage(s.Offset, Math.Min(s.Length, 256)));
    }
}
