using System.Security.Cryptography;
using System.Text;
using JmdExplorer.Core.Services;

// jmdextract — a small, honest command-line companion to JMD Explorer.
//
// It opens a .jmd (or any binary) file, reports the "Xenon Data Format v4s" header
// when present, scans for embedded file signatures, and extracts them. Assets whose
// exact length can be derived from their own header (e.g. DDS textures) are written
// with their real extension; everything else is carved raw as .bin. It never claims
// a carved block is a valid asset unless the full size was parsed from a header.

return Cli.Run(args);

internal static class Cli
{
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string command = args[0].ToLowerInvariant();
        try
        {
            return command switch
            {
                "info" when args.Length >= 2 => Info(args[1]),
                "list" when args.Length >= 2 => List(args[1]),
                "extract" when args.Length >= 2 => Extract(
                    args[1],
                    args.Skip(2).FirstOrDefault(a => !a.StartsWith("--")),
                    extractAll: args.Contains("--all"),
                    toPng: args.Contains("--png")),
                "batch" when args.Length >= 2 => Batch(
                    args[1],
                    args.Skip(2).FirstOrDefault(a => !a.StartsWith("--")),
                    extractAll: args.Contains("--all"),
                    toPng: !args.Contains("--no-png")),
                "config" when args.Length >= 2 => Config(
                    args[1], args.Skip(2).FirstOrDefault(a => !a.StartsWith("--"))),
                _ => Fail()
            };
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"error: file not found: {ex.FileName ?? ex.Message}");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
    }

    private static int Fail()
    {
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            jmdextract — open & extract embedded files from .jmd (Xenon Data Format v4s)

            Usage:
              jmdextract info    <file.jmd>            Show header, size, SHA-256
              jmdextract list    <file.jmd>            Scan and list embedded signatures
              jmdextract extract <file.jmd> [outdir]   Extract embedded files
                                                       (outdir defaults to <file>_extracted)
                  --all                                Also carve low-confidence matches
                                                       (2-byte magics; noisy on big files)
                  --png                                Also decode DXT DDS textures to .png
                                                       (universally viewable)
              jmdextract batch   <folder> [outdir]     Extract every *.jmd under a folder
                                                       (recursive; one subfolder per file).
                                                       DDS->PNG on by default here.
                  --all                                Also carve low-confidence matches
                  --no-png                             Skip DDS->PNG decoding
              jmdextract config  <file.jmd> [outdir]   Export car/stat config blocks as .xml
                                                       (J2m param trees, e.g. ai.jmd)

            Notes:
              * By default, extract pulls assets with a header-derived size (e.g. DDS)
                plus High-confidence matches, skipping noisy 2-byte-magic false positives.
                Use --all to carve every signature.
              * DDS textures are written as real .dds files (exact size from the header).
              * Unknown-size matches are carved raw as .bin (a signature is not proof of
                a complete, valid file).
              * A manifest.json is written next to the extracted files.
            """);
    }

    private static string ReadHeaderText(string path)
    {
        using var fs = File.OpenRead(path);
        Span<byte> head = stackalloc byte[64];
        int n = fs.Read(head);
        // The header is stored as UTF-16LE text, zero-padded.
        string text = Encoding.Unicode.GetString(head[..(n - (n % 2))]);
        int z = text.IndexOf('\0');
        return (z >= 0 ? text[..z] : text).Trim();
    }

    private static int Info(string path)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists) throw new FileNotFoundException(null, path);

        string header = ReadHeaderText(path);
        string sha;
        using (var fs = File.OpenRead(path))
            sha = Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();

        Console.WriteLine($"File   : {fi.FullName}");
        Console.WriteLine($"Size   : {fi.Length:n0} bytes (0x{fi.Length:X})");
        Console.WriteLine($"Header : {(string.IsNullOrEmpty(header) ? "(no text header)" : header)}");
        Console.WriteLine($"Xenon  : {(header.StartsWith("Xenon Data Format", StringComparison.OrdinalIgnoreCase) ? "yes" : "unrecognized")}");
        Console.WriteLine($"SHA-256: {sha}");
        return 0;
    }

    private static int List(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(null, path);

        var scanner = new SignatureScanner();
        using var fs = File.OpenRead(path);
        var matches = scanner.Scan(fs, fs.Length, CancellationToken.None,
            new Progress<double>(p => { }));

        if (matches.Count == 0)
        {
            Console.WriteLine("No standard embedded file signatures found.");
            Console.WriteLine("This does not mean the file is empty — content may be compressed,");
            Console.WriteLine("serialized, or in a proprietary layout.");
            return 0;
        }

        Console.WriteLine($"{matches.Count} signature(s) found:\n");
        Console.WriteLine($"  {"#",-4}{"Type",-14}{"Offset",-14}{"Size",-12}Confidence");
        Console.WriteLine($"  {new string('-', 56)}");
        int i = 0;
        foreach (var m in matches)
        {
            string size = m.SizeEstimate is > 0 ? $"{m.SizeEstimate:n0}" : "unknown";
            Console.WriteLine($"  {i++,-4}{m.Type,-14}{m.OffsetHex,-14}{size,-12}{m.Confidence}");
        }
        return 0;
    }

    private static int Extract(string path, string? outDir, bool extractAll, bool toPng)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(null, path);

        outDir ??= Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".",
            Path.GetFileNameWithoutExtension(path) + "_extracted");

        Console.Write("Scanning... ");
        var r = ExtractCore(path, outDir, extractAll, toPng);
        Console.WriteLine($"{r.SignaturesFound} signature(s) found.");
        if (r.Skipped > 0)
            Console.WriteLine($"Keeping {r.Extracted} useful match(es) (use --all for {r.Skipped} more).");
        if (r.Extracted == 0)
        {
            Console.WriteLine("Nothing to extract.");
            return 0;
        }
        Console.WriteLine();
        Console.WriteLine($"Extracted {r.Extracted} file(s) to:");
        Console.WriteLine($"  {Path.GetFullPath(outDir)}");
        Console.WriteLine($"  {r.CompleteAssets} complete asset(s) with header-derived sizes; " +
                          $"{r.Extracted - r.CompleteAssets} raw carve(s) as .bin.");
        if (toPng) Console.WriteLine($"  Converted {r.PngCount} DDS texture(s) to .png.");
        Console.WriteLine("Manifest: manifest.json");
        return 0;
    }

    private sealed record ExtractStats(int SignaturesFound, int Extracted, int CompleteAssets, int PngCount, int Skipped);

    /// <summary>Scans, extracts useful assets, and optionally decodes DDS->PNG. No console
    /// output, so it can be reused for both single-file and batch modes.</summary>
    private static ExtractStats ExtractCore(string path, string outDir, bool extractAll, bool toPng)
    {
        var scanner = new SignatureScanner();
        IReadOnlyList<JmdExplorer.Core.Models.EmbeddedSignatureMatch> matches;
        long fileLen;
        using (var fs = File.OpenRead(path))
        {
            fileLen = fs.Length;
            matches = scanner.Scan(fs, fs.Length);
        }
        int found = matches.Count;
        int skipped = 0;
        if (!extractAll)
        {
            var kept = matches.Where(m =>
                m.SizeEstimate is > 0 ||
                m.Confidence == JmdExplorer.Core.Models.Confidence.High).ToList();
            skipped = found - kept.Count;
            matches = kept;
        }
        if (matches.Count == 0) return new ExtractStats(found, 0, 0, 0, skipped);

        string sha;
        using (var fs = File.OpenRead(path))
            sha = Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();

        var requests = matches.Select(m =>
        {
            bool sizeKnown = m.SizeEstimate is > 0;
            long size = sizeKnown ? m.SizeEstimate!.Value : Math.Min(64 * 1024, fileLen - m.Offset);
            return new ExtractionRequest
            {
                Name = $"{m.Type}_0x{m.Offset:X}",
                StartOffset = m.Offset,
                EndOffset = m.Offset + size,
                Type = $"embedded_signature:{m.Type}",
                Extension = sizeKnown ? ExtensionFor(m.Type) : "bin"
            };
        }).ToList();

        var outcome = new RawBlockExtractor().Extract(path, outDir, requests,
            ReadHeaderText(path) is { Length: > 0 } h ? h : "Unknown binary", sha);
        int complete = matches.Count(m => m.SizeEstimate is > 0);
        int png = toPng ? ConvertDdsToPng(outcome.BinFiles) : 0;
        return new ExtractStats(found, outcome.BinFiles.Count, complete, png, skipped);
    }

    /// <summary>Recursively extracts every *.jmd under a folder into one subfolder each.</summary>
    private static int Batch(string folder, string? outDir, bool extractAll, bool toPng)
    {
        if (!Directory.Exists(folder)) { Console.Error.WriteLine($"error: folder not found: {folder}"); return 2; }
        outDir ??= Path.Combine(Path.GetFullPath(folder), "_extracted");
        var files = Directory.EnumerateFiles(folder, "*.jmd", SearchOption.AllDirectories).ToList();
        Console.WriteLine($"Found {files.Count} .jmd file(s) under {Path.GetFullPath(folder)}");
        if (files.Count == 0) return 0;

        int done = 0, totalAssets = 0, totalPng = 0, failed = 0;
        var baseFull = Path.GetFullPath(folder);
        foreach (var f in files)
        {
            string rel = Path.GetRelativePath(baseFull, f);
            string dest = Path.Combine(outDir, Path.ChangeExtension(rel, null)!);
            try
            {
                var r = ExtractCore(f, dest, extractAll, toPng);
                if (r.Extracted > 0) { totalAssets += r.Extracted; totalPng += r.PngCount; }
                else if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true); // no empty dirs
            }
            catch (Exception ex) { failed++; Console.Error.WriteLine($"  ! {rel}: {ex.Message}"); }
            done++;
            if (done % 25 == 0 || done == files.Count)
                Console.WriteLine($"  [{done}/{files.Count}] {totalAssets} assets, {totalPng} PNG so far...");
        }
        Console.WriteLine();
        Console.WriteLine($"Done. {done} file(s) processed, {failed} failed.");
        Console.WriteLine($"  {totalAssets} asset(s) extracted, {totalPng} PNG, to:");
        Console.WriteLine($"  {Path.GetFullPath(outDir)}");
        return 0;
    }

    /// <summary>Exports embedded car/stat config blocks (J2m "param" trees) as .xml files.</summary>
    private static int Config(string path, string? outDir)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(null, path);
        outDir ??= Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".",
            Path.GetFileNameWithoutExtension(path) + "_config");

        Console.Write("Reading config tree... ");
        var configs = J2mConfigExtractor.Extract(File.ReadAllBytes(path));
        Console.WriteLine($"{configs.Count} config block(s) found.");
        if (configs.Count == 0)
        {
            Console.WriteLine("No 'param' config blocks in this file (try ai.jmd).");
            return 0;
        }

        Directory.CreateDirectory(outDir);
        int written = 0;
        foreach (var c in configs)
        {
            // Name by emblem when available, else by index; keep names unique.
            string baseName = string.IsNullOrWhiteSpace(c.Label) ? $"config_{c.Index:D4}" : Sanitize(c.Label);
            string file = Path.Combine(outDir, baseName + ".xml");
            int dup = 1;
            while (File.Exists(file)) file = Path.Combine(outDir, $"{baseName}_{dup++}.xml");
            File.WriteAllText(file, c.Xml, new UTF8Encoding(false));
            written++;
        }
        Console.WriteLine($"Wrote {written} XML file(s) to:\n  {Path.GetFullPath(outDir)}");
        return 0;
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    /// <summary>Decodes every carved .dds to a sibling .png. Skips unsupported formats.</summary>
    private static int ConvertDdsToPng(IEnumerable<string> files)
    {
        int count = 0;
        foreach (var f in files.Where(f => f.EndsWith(".dds", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                byte[] dds = File.ReadAllBytes(f);
                if (!DdsImage.IsSupported(dds)) continue;
                var img = DdsImage.Decode(dds);
                PngWriter.WriteRgba(Path.ChangeExtension(f, ".png"), img.Width, img.Height, img.Rgba);
                count++;
            }
            catch { /* leave the .dds in place if decode fails */ }
        }
        return count;
    }

    // Real extension only ever used when the exact, complete size was parsed from a header.
    private static string ExtensionFor(string type) => type switch
    {
        "DDS" => "dds",
        "PNG" => "png",
        "JPEG" => "jpg",
        "BMP" => "bmp",
        "WAV (RIFF)" => "wav",
        "OGG" => "ogg",
        "ZIP" => "zip",
        _ => "bin"
    };
}
