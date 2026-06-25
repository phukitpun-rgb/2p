using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JmdExplorer.App.Services;
using JmdExplorer.Core.Abstractions;

namespace JmdExplorer.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject, IPageViewModel
{
    private readonly IAppLogger _logger;

    public SettingsViewModel(IAppLogger logger)
    {
        _logger = logger;
        LogDirectory = System.IO.Path.Combine(AppContext.BaseDirectory, "logs");
    }

    public string Title => "Settings";
    public string Glyph => ""; // settings

    [ObservableProperty] private int _defaultMinStringLength = 4;
    [ObservableProperty] private bool _defaultScanUtf16 = true;
    [ObservableProperty] private string _logDirectory = "";

    public string AppVersion => typeof(SettingsViewModel).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    public string About =>
        "JMD Explorer is an honest binary inspector. It separates 'structure recognized' " +
        "from 'asset decoded' and never fabricates decoded files. Add real decoders via the " +
        "IJmdDecoderPlugin interface (see docs/plugin-guide.md).";

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            System.IO.Directory.CreateDirectory(LogDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{LogDirectory}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.Warn($"Could not open log folder: {ex.Message}");
        }
    }
}
