using System.Text.Json;
using JmdExplorer.Core.Models;

namespace JmdExplorer.Core.Services;

/// <summary>A request to carve one byte range to disk.</summary>
public sealed class ExtractionRequest
{
    public required string Name { get; init; }
    public long StartOffset { get; init; }

    /// <summary>Exclusive end offset. Use -1 to mean "until EOF".</summary>
    public long EndOffset { get; init; }

    public string Type { get; init; } = "unknown_structured_binary";
}

public sealed class ExtractionOutcome
{
    public required string ManifestPath { get; init; }
    public required IReadOnlyList<string> BinFiles { get; init; }
    public required ExtractionManifest Manifest { get; init; }
}

/// <summary>
/// Carves raw byte ranges from a source file into ".bin" files and writes a JSON
/// manifest. It streams bytes; it never claims the result is a decoded asset.
/// </summary>
public sealed class RawBlockExtractor
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Extracts the requested ranges from <paramref name="sourcePath"/> into
    /// <paramref name="outputDirectory"/>. The base name (e.g. "carinfo") is used in
    /// the file naming scheme: {base}_{name}_{start}_{end}.bin
    /// </summary>
    public ExtractionOutcome Extract(
        string sourcePath,
        string outputDirectory,
        IReadOnlyList<ExtractionRequest> requests,
        string format = "Unknown binary",
        string? sha256 = null,
        CancellationToken ct = default,
        IProgress<double>? progress = null)
    {
        Directory.CreateDirectory(outputDirectory);
        string baseName = Path.GetFileNameWithoutExtension(sourcePath);

        var manifest = new ExtractionManifest
        {
            SourceFile = Path.GetFileName(sourcePath),
            Format = format,
            Sha256 = sha256
        };
        var binFiles = new List<string>();

        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        long fileLength = source.Length;

        for (int i = 0; i < requests.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var req = requests[i];
            long end = req.EndOffset < 0 ? fileLength : Math.Min(req.EndOffset, fileLength);
            long start = Math.Clamp(req.StartOffset, 0, fileLength);
            if (end < start) end = start;
            long size = end - start;

            string endLabel = req.EndOffset < 0 ? "EOF" : $"0x{end:X8}";
            string fileName = $"{baseName}_{Sanitize(req.Name)}_0x{start:X8}_{endLabel}.bin";
            string outPath = Path.Combine(outputDirectory, fileName);

            CopyRange(source, start, size, outPath, ct);
            binFiles.Add(outPath);

            manifest.Regions.Add(new ExtractedRegionEntry
            {
                Name = req.Name,
                StartOffset = $"0x{start:X}",
                EndOffset = req.EndOffset < 0 ? "EOF" : $"0x{end:X}",
                Size = size,
                Type = req.Type,
                FileName = fileName
            });

            progress?.Report(Math.Clamp((double)(i + 1) / requests.Count, 0, 1));
        }

        string manifestPath = Path.Combine(outputDirectory, $"{baseName}_manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOpts));

        return new ExtractionOutcome
        {
            ManifestPath = manifestPath,
            BinFiles = binFiles,
            Manifest = manifest
        };
    }

    private static void CopyRange(Stream source, long start, long size, string outPath, CancellationToken ct)
    {
        source.Seek(start, SeekOrigin.Begin);
        using var dest = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[256 * 1024];
        long remaining = size;
        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();
            int toRead = (int)Math.Min(buffer.Length, remaining);
            int read = source.Read(buffer, 0, toRead);
            if (read <= 0) break;
            dest.Write(buffer, 0, read);
            remaining -= read;
        }
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray();
        return new string(chars).ToLowerInvariant();
    }
}
