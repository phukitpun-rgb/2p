using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;
using JmdExplorer.Core.Services;
using JmdExplorer.Decoders.Plugins;
using JmdExplorer.Decoders.Profiles;
using JmdExplorer.Infrastructure.Files;
using JmdExplorer.Infrastructure.Logging;
using JmdExplorer.Infrastructure.Reporting;

// Headless analyzer that runs the real JMD Explorer pipeline on a file and prints an
// honest report. Same services the WPF app uses — no overclaiming.

if (args.Length == 0)
{
    Console.WriteLine("Usage: JmdAnalyze <file> [--strings N]");
    return 1;
}

string path = args[0];
int maxStrings = 25;
for (int i = 1; i < args.Length - 1; i++)
    if (args[i] == "--strings" && int.TryParse(args[i + 1], out int n)) maxStrings = n;

IAppLogger logger = new FileAppLogger();
var fileService = new FileService(logger);
var formatDetector = new FormatDetector(
    new IFormatProfile[] { new XenonDataFormatV4sProfile(), new UnknownBinaryProfile() }, logger);
var regionDetector = new RegionDetector();
var signatureScanner = new SignatureScanner();
var stringScanner = new StringScanner();
var recordDetector = new RecordPatternDetector();
var reportWriter = new ReportWriter();

JmdFileContext ctx;
try
{
    ctx = fileService.Load(path);
}
catch (JmdExplorer.Infrastructure.Files.FileLoadException ex)
{
    Console.WriteLine($"Could not open file ({ex.Error}): {ex.Message}");
    return 2;
}

ctx.Sha256 = fileService.ComputeSha256(ctx);
var format = formatDetector.Detect(ctx);
ctx.Format = format;

IReadOnlyList<Region> regions;
IReadOnlyList<EmbeddedSignatureMatch> sigs;
IReadOnlyList<ScannedString> strings;
using (var s = ctx.OpenStream())
    regions = regionDetector.Detect(s, ctx.Length, new RegionDetectionOptions { Hints = format.RegionHints });
using (var s = ctx.OpenStream())
    sigs = signatureScanner.Scan(s, ctx.Length);
using (var s = ctx.OpenStream())
    strings = stringScanner.Scan(s, ctx.Length, new StringScanOptions
    {
        MinLength = 5, ScanAscii = true, ScanUtf16LE = true, ScanUtf16BE = false, PrintableOnly = true
    });

// Repeating-record detection on the largest structured region.
var recordTarget = regions
    .Where(r => r.Type is RegionType.StructuredBinary or RegionType.RepeatingRecord or RegionType.Metadata)
    .OrderByDescending(r => r.Size)
    .FirstOrDefault();
IReadOnlyList<RecordPatternCandidate> records = Array.Empty<RecordPatternCandidate>();
if (recordTarget is not null)
{
    using var s = ctx.OpenStream();
    records = recordDetector.Detect(s, recordTarget.StartOffset, recordTarget.Size);
}

// Decoder plugin honesty statuses.
var plugins = new IJmdDecoderPlugin[]
{
    new GenericSignatureScannerPlugin(signatureScanner),
    new XenonV4sInspectorPlugin(),
    new XenonV4sDecoderPlugin()
};

var report = new AnalysisReport
{
    FileName = ctx.FileName,
    FullPath = ctx.FilePath,
    FileSize = ctx.Length,
    Sha256 = ctx.Sha256,
    CreatedUtc = ctx.CreatedUtc,
    ModifiedUtc = ctx.ModifiedUtc,
    HeaderText = format.HeaderText,
    DetectedFormat = format.FormatName,
    Confidence = format.Confidence.ToString(),
    DecodeStatus = format.Status.ToDisplayString(),
    DecoderAvailable = format.DecoderAvailable,
    Mode = format.Mode,
    Regions = regions.ToList(),
    Signatures = sigs.ToList(),
    RecordPatterns = records.ToList(),
    DecoderPlugins = plugins.Select(p => new DecoderPluginStatus
    {
        Name = p.Name, Version = p.Version,
        Status = string.Equals(p.Version, "N/A", StringComparison.OrdinalIgnoreCase) ? "Not Available" : "Enabled",
        SupportedFormat = p.Name.Contains("Generic") ? "Any binary" : "Xenon Data Format v4s"
    }).ToList()
};

Console.WriteLine(reportWriter.Render(report, ReportFormat.Txt));

Console.WriteLine("STRINGS (first {0} of {1} found)", Math.Min(maxStrings, strings.Count), strings.Count);
foreach (var str in strings.Take(maxStrings))
    Console.WriteLine($"  {str.OffsetHex}  {str.EncodingDisplay,-9} len={str.Length,-4} {Truncate(str.Value, 80)}");

return 0;

static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
