using System.Net;
using System.Text;
using System.Text.Json;
using JmdExplorer.Core.Models;

namespace JmdExplorer.Infrastructure.Reporting;

public enum ReportFormat { Txt, Json, Html }

/// <summary>
/// Renders an <see cref="AnalysisReport"/> to TXT / JSON / HTML. The honesty wording
/// from the spec is baked into every format so a report can never overstate results.
/// </summary>
public sealed class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public string Render(AnalysisReport report, ReportFormat format) => format switch
    {
        ReportFormat.Txt => RenderTxt(report),
        ReportFormat.Json => JsonSerializer.Serialize(report, JsonOpts),
        ReportFormat.Html => RenderHtml(report),
        _ => RenderTxt(report)
    };

    public void Write(AnalysisReport report, ReportFormat format, string path)
        => File.WriteAllText(path, Render(report, format), Encoding.UTF8);

    // --- TXT -------------------------------------------------------------

    private static string RenderTxt(AnalysisReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("JMD EXPLORER — ANALYSIS REPORT");
        sb.AppendLine("==============================");
        sb.AppendLine($"Generated (UTC): {r.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("FILE INFORMATION");
        sb.AppendLine($"  Name        : {r.FileName}");
        sb.AppendLine($"  Path        : {r.FullPath}");
        sb.AppendLine($"  Size        : {r.FileSize:N0} bytes");
        sb.AppendLine($"  SHA-256     : {r.Sha256}");
        sb.AppendLine($"  Created UTC : {r.CreatedUtc:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"  Modified UTC: {r.ModifiedUtc:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("FORMAT DETECTION");
        sb.AppendLine($"  Header text     : {r.HeaderText}");
        sb.AppendLine($"  Detected format : {r.DetectedFormat}");
        sb.AppendLine($"  Confidence      : {r.Confidence}");
        sb.AppendLine($"  Decoder         : {(r.DecoderAvailable ? "Available" : "Not available")}");
        sb.AppendLine($"  Mode            : {r.Mode}");
        sb.AppendLine($"  Decode status   : {r.DecodeStatus}");
        sb.AppendLine();

        sb.AppendLine("REGIONS");
        if (r.Regions.Count == 0) sb.AppendLine("  (none detected)");
        foreach (var region in r.Regions)
        {
            sb.AppendLine($"  {region.Name,-20} {region.StartOffsetHex} .. {region.EndOffsetHex}  " +
                          $"{region.Size,12:N0} B  entropy={region.Entropy:F2}  {region.SuggestedInterpretation}");
        }
        sb.AppendLine();

        sb.AppendLine("EMBEDDED SIGNATURES");
        if (r.Signatures.Count == 0)
        {
            sb.AppendLine("  No standard embedded file signatures found.");
            sb.AppendLine("  This does not mean the file is empty. The content may be");
            sb.AppendLine("  compressed, encrypted, serialized, or proprietary.");
        }
        foreach (var sig in r.Signatures)
        {
            string size = sig.SizeEstimate.HasValue ? $"{sig.SizeEstimate.Value:N0} B" : "Unknown";
            sb.AppendLine($"  {sig.Type,-18} {sig.OffsetHex}  {sig.Confidence,-6}  {size,14}  [{sig.Action}]");
        }
        sb.AppendLine();

        sb.AppendLine("REPEATING RECORD ANALYSIS");
        if (r.RecordPatterns.Count == 0) sb.AppendLine("  (no convincing repeating-record pattern detected)");
        foreach (var rec in r.RecordPatterns)
        {
            sb.AppendLine($"  Record size {rec.RecordSize,4} B  count~{rec.EstimatedRecordCount,10:N0}  " +
                          $"similarity={rec.SimilarityRatio:P0}  {rec.Confidence}  {rec.Interpretation}");
        }
        sb.AppendLine();

        sb.AppendLine("DECODER PLUGINS");
        foreach (var p in r.DecoderPlugins)
            sb.AppendLine($"  {p.Name,-26} {p.Version,-6} {p.Status,-14} {p.SupportedFormat}");
        sb.AppendLine();

        sb.AppendLine("EXTRACTED FILES");
        if (r.ExtractedFiles.Count == 0) sb.AppendLine("  (none)");
        foreach (var f in r.ExtractedFiles) sb.AppendLine($"  {f}");
        sb.AppendLine();

        sb.AppendLine("WARNINGS / LIMITATIONS");
        foreach (var w in r.Warnings) sb.AppendLine($"  - {w}");

        return sb.ToString();
    }

    // --- HTML ------------------------------------------------------------

    private static string RenderHtml(AnalysisReport r)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
        sb.AppendLine("<title>JMD Explorer Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:'Segoe UI',sans-serif;background:#14161b;color:#d7dae0;margin:24px;}");
        sb.AppendLine("h1{color:#7aa2f7;} h2{color:#9ece6a;border-bottom:1px solid #2a2f3a;padding-bottom:4px;}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin:8px 0;} td,th{border:1px solid #2a2f3a;padding:6px 10px;text-align:left;font-size:13px;}");
        sb.AppendLine("th{background:#1b1e26;color:#c0caf5;} .warn{background:#2a1f1f;border-left:3px solid #e0af68;padding:10px;margin:6px 0;border-radius:4px;}");
        sb.AppendLine("code{color:#7dcfff;}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>JMD Explorer — Analysis Report</h1>");
        sb.AppendLine($"<p>Generated (UTC): {E(r.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"))}</p>");

        sb.AppendLine("<h2>File Information</h2><table>");
        sb.AppendLine($"<tr><th>Name</th><td>{E(r.FileName)}</td></tr>");
        sb.AppendLine($"<tr><th>Path</th><td><code>{E(r.FullPath)}</code></td></tr>");
        sb.AppendLine($"<tr><th>Size</th><td>{r.FileSize:N0} bytes</td></tr>");
        sb.AppendLine($"<tr><th>SHA-256</th><td><code>{E(r.Sha256)}</code></td></tr>");
        sb.AppendLine($"<tr><th>Created (UTC)</th><td>{E(r.CreatedUtc.ToString("u"))}</td></tr>");
        sb.AppendLine($"<tr><th>Modified (UTC)</th><td>{E(r.ModifiedUtc.ToString("u"))}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Format Detection</h2><table>");
        sb.AppendLine($"<tr><th>Header text</th><td><code>{E(r.HeaderText)}</code></td></tr>");
        sb.AppendLine($"<tr><th>Detected format</th><td>{E(r.DetectedFormat)}</td></tr>");
        sb.AppendLine($"<tr><th>Confidence</th><td>{E(r.Confidence)}</td></tr>");
        sb.AppendLine($"<tr><th>Decoder</th><td>{(r.DecoderAvailable ? "Available" : "Not available")}</td></tr>");
        sb.AppendLine($"<tr><th>Mode</th><td>{E(r.Mode)}</td></tr>");
        sb.AppendLine($"<tr><th>Decode status</th><td>{E(r.DecodeStatus)}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Regions</h2><table><tr><th>Name</th><th>Start</th><th>End</th><th>Size</th><th>Entropy</th><th>Interpretation</th></tr>");
        foreach (var region in r.Regions)
            sb.AppendLine($"<tr><td>{E(region.Name)}</td><td><code>{region.StartOffsetHex}</code></td><td><code>{region.EndOffsetHex}</code></td><td>{region.Size:N0} B</td><td>{region.Entropy:F2}</td><td>{E(region.SuggestedInterpretation)}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Embedded Signatures</h2>");
        if (r.Signatures.Count == 0)
            sb.AppendLine("<div class=\"warn\">No standard embedded file signatures found. This does not mean the file is empty — the content may be compressed, encrypted, serialized, or proprietary.</div>");
        else
        {
            sb.AppendLine("<table><tr><th>Type</th><th>Offset</th><th>Confidence</th><th>Size estimate</th><th>Action</th></tr>");
            foreach (var sig in r.Signatures)
            {
                string size = sig.SizeEstimate.HasValue ? $"{sig.SizeEstimate.Value:N0} B" : "Unknown";
                sb.AppendLine($"<tr><td>{E(sig.Type)}</td><td><code>{sig.OffsetHex}</code></td><td>{sig.Confidence}</td><td>{size}</td><td>{E(sig.Action)}</td></tr>");
            }
            sb.AppendLine("</table>");
        }

        sb.AppendLine("<h2>Repeating Record Analysis</h2>");
        if (r.RecordPatterns.Count == 0)
            sb.AppendLine("<p>No convincing repeating-record pattern detected.</p>");
        else
        {
            sb.AppendLine("<table><tr><th>Record size</th><th>Est. count</th><th>Similarity</th><th>Confidence</th><th>Interpretation</th></tr>");
            foreach (var rec in r.RecordPatterns)
                sb.AppendLine($"<tr><td>{rec.RecordSize} B</td><td>{rec.EstimatedRecordCount:N0}</td><td>{rec.SimilarityRatio:P0}</td><td>{rec.Confidence}</td><td>{E(rec.Interpretation)}</td></tr>");
            sb.AppendLine("</table>");
        }

        sb.AppendLine("<h2>Decoder Plugins</h2><table><tr><th>Plugin</th><th>Version</th><th>Status</th><th>Supported format</th></tr>");
        foreach (var p in r.DecoderPlugins)
            sb.AppendLine($"<tr><td>{E(p.Name)}</td><td>{E(p.Version)}</td><td>{E(p.Status)}</td><td>{E(p.SupportedFormat)}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Extracted Files</h2>");
        if (r.ExtractedFiles.Count == 0) sb.AppendLine("<p>(none)</p>");
        else { sb.AppendLine("<ul>"); foreach (var f in r.ExtractedFiles) sb.AppendLine($"<li><code>{E(f)}</code></li>"); sb.AppendLine("</ul>"); }

        sb.AppendLine("<h2>Warnings / Limitations</h2>");
        foreach (var w in r.Warnings) sb.AppendLine($"<div class=\"warn\">{E(w)}</div>");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
