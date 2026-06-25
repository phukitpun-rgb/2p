using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using JmdExplorer.App.Services;
using JmdExplorer.Core.Abstractions;
using JmdExplorer.Infrastructure.Files;

namespace JmdExplorer.App.ViewModels;

/// <summary>
/// Root view model: owns the sidebar navigation, the active page, file loading
/// (picker + drag/drop), and global busy state. It also reacts to "go to hex offset"
/// messages by switching to the Hex Viewer page.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IRecipient<NavigateToHexOffsetMessage>
{
    private const string JmdFilter = "JMD files (*.jmd)|*.jmd|All files (*.*)|*.*";

    private readonly AppSession _session;
    private readonly IDialogService _dialogs;
    private readonly IAppLogger _logger;

    public MainViewModel(
        AppSession session,
        IDialogService dialogs,
        IAppLogger logger,
        IMessenger messenger,
        FileInspectorViewModel fileInspector,
        HexViewerViewModel hexViewer,
        StructureAnalyzerViewModel structureAnalyzer,
        EmbeddedScannerViewModel embeddedScanner,
        StringScannerViewModel stringScanner,
        ExtractedFilesViewModel extractedFiles,
        DecoderPluginsViewModel decoderPlugins,
        ExportReportViewModel exportReport,
        SettingsViewModel settings)
    {
        _session = session;
        _dialogs = dialogs;
        _logger = logger;

        Pages = new ObservableCollection<IPageViewModel>
        {
            fileInspector, hexViewer, structureAnalyzer, embeddedScanner,
            stringScanner, extractedFiles, decoderPlugins, exportReport, settings
        };
        _selectedPage = fileInspector;
        _hexViewer = hexViewer;

        messenger.RegisterAll(this);
    }

    public ObservableCollection<IPageViewModel> Pages { get; }

    private readonly HexViewerViewModel _hexViewer;

    [ObservableProperty] private IPageViewModel _selectedPage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Drop a .jmd file here or click Open File.";
    [ObservableProperty] private string _currentFileName = "(no file loaded)";

    public void Receive(NavigateToHexOffsetMessage message)
    {
        SelectedPage = _hexViewer;
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        string? path = _dialogs.OpenFile(JmdFilter, "Open a file for inspection");
        if (path is not null) await LoadFileAsync(path);
    }

    /// <summary>Entry point used by drag/drop in the view's code-behind.</summary>
    public async Task LoadFileAsync(string path)
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = $"Loading {System.IO.Path.GetFileName(path)}...";
        try
        {
            await _session.LoadAsync(path);
            CurrentFileName = _session.Context!.FileName;
            StatusMessage = $"Loaded: {_session.Format!.FormatName} (confidence {_session.Format.Confidence}).";
            // Jump to the inspector so the user immediately sees the honesty summary.
            SelectedPage = Pages[0];
        }
        catch (FileLoadException ex)
        {
            _logger.Warn($"File load failed ({ex.Error}): {ex.Message}");
            _dialogs.ShowMessage(ex.Message, "Could not open file", isError: true);
            StatusMessage = $"Load failed: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.Error("Unexpected load error", ex);
            _dialogs.ShowMessage($"Unexpected error: {ex.Message}", "Error", isError: true);
            StatusMessage = "Load failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
