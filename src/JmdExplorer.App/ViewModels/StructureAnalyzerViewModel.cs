using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using JmdExplorer.App.Services;
using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;

namespace JmdExplorer.App.ViewModels;

public sealed partial class StructureAnalyzerViewModel : ScanViewModelBase
{
    private readonly IMessenger _messenger;

    public StructureAnalyzerViewModel(AppSession session, IAppLogger logger, IMessenger messenger)
        : base(session, logger)
    {
        _messenger = messenger;
    }

    public override string Title => "Structure Analyzer";
    public override string Glyph => "E9D9"; // chart

    public ObservableCollection<Region> Regions => Session.Regions;
    public ObservableCollection<RecordPatternCandidate> RecordPatterns => Session.RecordPatterns;

    [ObservableProperty] private Region? _selectedRegion;

    [RelayCommand]
    private async Task Analyze()
    {
        await RunScanAsync(async (ct, progress) =>
        {
            await Session.DetectRegionsAsync(ct, progress);

            // Run repeating-record detection over the largest structured region. High-entropy
            // (likely compressed/encrypted) and zero-padding regions are skipped because fixed
            // record layouts are not detectable there.
            var target = Session.Regions
                .Where(r => r.Type is RegionType.StructuredBinary
                              or RegionType.RepeatingRecord or RegionType.Metadata)
                .OrderByDescending(r => r.Size)
                .FirstOrDefault();
            if (target is not null)
                await Session.DetectRecordsAsync(target.StartOffset, target.Size, ct);
        }, "Analyzing file structure...");
    }

    [RelayCommand]
    private void OpenInHex(Region? region)
    {
        if (region is null) return;
        int len = (int)Math.Min(region.Size, 256);
        _messenger.Send(new NavigateToHexOffsetMessage(region.StartOffset, len));
    }
}
