using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JmdExplorer.App.Services;
using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;

namespace JmdExplorer.App.ViewModels;

public sealed partial class DecoderPluginsViewModel : ObservableObject, IPageViewModel
{
    private readonly AppSession _session;
    private readonly IReadOnlyList<IJmdDecoderPlugin> _plugins;
    private readonly IDialogService _dialogs;
    private readonly IAppLogger _logger;

    public DecoderPluginsViewModel(
        AppSession session, IEnumerable<IJmdDecoderPlugin> plugins, IDialogService dialogs, IAppLogger logger)
    {
        _session = session;
        _plugins = plugins.ToList();
        _dialogs = dialogs;
        _logger = logger;

        foreach (var p in _plugins)
        {
            Plugins.Add(new PluginRow
            {
                Plugin = p,
                Name = p.Name,
                Version = p.Version,
                Status = ResolveStatus(p),
                SupportedFormat = ResolveSupportedFormat(p)
            });
        }
        _session.FileLoaded += (_, _) => OnPropertyChanged(nameof(HasFile));
    }

    public string Title => "Decoder Plugins";
    public string Glyph => "E74C"; // plugin

    public bool HasFile => _session.Context is not null;

    public ObservableCollection<PluginRow> Plugins { get; } = new();

    [ObservableProperty] private string _lastResult = "";

    [RelayCommand]
    private void Run(PluginRow? row)
    {
        if (row is null) return;
        if (_session.Context is null)
        {
            _dialogs.ShowMessage("Load a file first.", "No file");
            return;
        }
        try
        {
            DecodeResult result = row.Plugin.CanDecode(_session.Context)
                ? row.Plugin.Decode(_session.Context)
                : DecodeResult.Unsupported($"'{row.Name}' does not handle this file.");

            LastResult =
                $"[{row.Name} v{row.Version}]\n" +
                $"Status : {result.Status.ToDisplayString()}\n" +
                $"Success: {result.Success}\n" +
                $"Message: {result.Message}\n" +
                (result.ProducedFiles.Count > 0
                    ? "Produced:\n" + string.Join("\n", result.ProducedFiles.Select(f => "  " + f.Path))
                    : "Produced: (no files — inspection only)");
        }
        catch (Exception ex)
        {
            // A crashing plugin must never take down the app.
            _logger.Error($"Plugin '{row.Name}' threw", ex);
            LastResult = $"[{row.Name}] crashed but was contained: {ex.Message}";
        }
    }

    private static string ResolveStatus(IJmdDecoderPlugin p)
    {
        if (string.Equals(p.Version, "N/A", StringComparison.OrdinalIgnoreCase)) return "Not Available";
        return "Enabled";
    }

    private static string ResolveSupportedFormat(IJmdDecoderPlugin p) => p.Name switch
    {
        "Generic Signature Scanner" => "Any binary",
        "Xenon v4s Inspector" => "Xenon Data Format v4s",
        "Xenon v4s Decoder" => "Xenon Data Format v4s (future)",
        _ => "Unknown"
    };
}

public sealed class PluginRow
{
    public required IJmdDecoderPlugin Plugin { get; init; }
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Status { get; init; } = "";
    public string SupportedFormat { get; init; } = "";
}
