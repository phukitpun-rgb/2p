using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JmdExplorer.App.Services;
using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;
using JmdExplorer.Infrastructure.Reporting;

namespace JmdExplorer.App.ViewModels;

public sealed partial class ExportReportViewModel : ObservableObject, IPageViewModel
{
    private readonly AppSession _session;
    private readonly ReportWriter _writer;
    private readonly IDialogService _dialogs;
    private readonly IReadOnlyList<IJmdDecoderPlugin> _plugins;
    private readonly IAppLogger _logger;

    public ExportReportViewModel(
        AppSession session, ReportWriter writer, IDialogService dialogs,
        IEnumerable<IJmdDecoderPlugin> plugins, IAppLogger logger)
    {
        _session = session;
        _writer = writer;
        _dialogs = dialogs;
        _plugins = plugins.ToList();
        _logger = logger;
        _session.FileLoaded += (_, _) =>
        {
            OnPropertyChanged(nameof(HasFile));
            Preview = BuildReport() is { } r ? _writer.Render(r, ReportFormat.Txt) : "";
        };
    }

    public string Title => "Export Report";
    public string Glyph => ""; // download

    public bool HasFile => _session.Context is not null;

    [ObservableProperty] private string _preview = "";

    [RelayCommand] private void ExportTxt() => Export(ReportFormat.Txt, "Text report (*.txt)|*.txt", ".txt");
    [RelayCommand] private void ExportJson() => Export(ReportFormat.Json, "JSON report (*.json)|*.json", ".json");
    [RelayCommand] private void ExportHtml() => Export(ReportFormat.Html, "HTML report (*.html)|*.html", ".html");

    [RelayCommand]
    private void RefreshPreview()
    {
        var report = BuildReport();
        Preview = report is null ? "Load a file first." : _writer.Render(report, ReportFormat.Txt);
    }

    private void Export(ReportFormat format, string filter, string ext)
    {
        var report = BuildReport();
        if (report is null) { _dialogs.ShowMessage("Load a file first.", "No file"); return; }
        string defaultName = System.IO.Path.GetFileNameWithoutExtension(report.FileName) + "_report" + ext;
        string? path = _dialogs.SaveFile(filter, defaultName, "Export report");
        if (path is null) return;
        try
        {
            _writer.Write(report, format, path);
            _dialogs.ShowMessage($"Report saved:\n{path}", "Export complete");
        }
        catch (Exception ex)
        {
            _logger.Error("Report export failed", ex);
            _dialogs.ShowMessage($"Export failed: {ex.Message}", "Error", isError: true);
        }
    }

    private AnalysisReport? BuildReport()
    {
        var ctx = _session.Context;
        var fmt = _session.Format;
        if (ctx is null || fmt is null) return null;

        return new AnalysisReport
        {
            FileName = ctx.FileName,
            FullPath = ctx.FilePath,
            FileSize = ctx.Length,
            Sha256 = ctx.Sha256,
            CreatedUtc = ctx.CreatedUtc,
            ModifiedUtc = ctx.ModifiedUtc,
            HeaderText = fmt.HeaderText,
            DetectedFormat = fmt.FormatName,
            Confidence = fmt.Confidence.ToString(),
            DecodeStatus = fmt.Status.ToDisplayString(),
            DecoderAvailable = fmt.DecoderAvailable,
            Mode = fmt.Mode,
            Regions = _session.Regions.ToList(),
            Signatures = _session.Signatures.ToList(),
            RecordPatterns = _session.RecordPatterns.ToList(),
            ExtractedFiles = _session.ExtractedFiles.ToList(),
            DecoderPlugins = _plugins.Select(p => new DecoderPluginStatus
            {
                Name = p.Name,
                Version = p.Version,
                Status = string.Equals(p.Version, "N/A", StringComparison.OrdinalIgnoreCase) ? "Not Available" : "Enabled",
                SupportedFormat = p.Name.Contains("Generic") ? "Any binary" : "Xenon Data Format v4s"
            }).ToList()
        };
    }
}
