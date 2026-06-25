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
                    extractAll: args.Contains("--all")),
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

    private static int Extract(string path, string? outDir, bool extractAll)
    {
        if (!File.Exists(path)) throw new FileNotFoundException(null, path);

        outDir ??= Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".",
            Path.GetFileNameWithoutExtension(path) + "_extracted");

        var scanner = new SignatureScanner();
        IReadOnlyList<JmdExplorer.Core.Models.EmbeddedSignatureMatch> matches;
        long fileLen;
        using (var fs = File.OpenRead(path))
        {
            fileLen = fs.Length;
            Console.Write("Scanning... ");
            matches = scanner.Scan(fs, fs.Length);
        }
        Console.WriteLine($"{matches.Count} signature(s) found.");

        if (!extractAll)
        {
            // Default: keep only assets worth extracting — those with a real, parsed
            // size, plus High-confidence matches. This drops the 2-byte-magic noise.
            var kept = matches.Where(m =>
                m.SizeEstimate is > 0 ||
                m.Confidence == JmdExplorer.Core.Models.Confidence.High).ToList();
            if (kept.Count != matches.Count)
                Console.WriteLine($"Keeping {kept.Count} useful match(es) " +
                                  $"(use --all to carve the remaining {matches.Count - kept.Count}).");
            matches = kept;
        }

        if (matches.Count == 0)
        {
            Console.WriteLine("Nothing to extract.");
            return 0;
        }

        string sha;
        using (var fs = File.OpenRead(path))
            sha = Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();

        var requests = new List<ExtractionRequest>(matches.Count);
        foreach (var m in matches)
        {
            bool sizeKnown = m.SizeEstimate is > 0;
            long size = sizeKnown
                ? m.SizeEstimate!.Value
                : Math.Min(64 * 1024, fileLen - m.Offset);

            requests.Add(new ExtractionRequest
            {
                Name = $"{m.Type}_0x{m.Offset:X}",
                StartOffset = m.Offset,
                EndOffset = m.Offset + size,
                Type = $"embedded_signature:{m.Type}",
                Extension = sizeKnown ? ExtensionFor(m.Type) : "bin"
            });
        }

        var extractor = new RawBlockExtractor();
        var outcome = extractor.Extract(path, outDir, requests,
            ReadHeaderText(path) is { Length: > 0 } h ? h : "Unknown binary", sha);

        int complete = matches.Count(m => m.SizeEstimate is > 0);
        Console.WriteLine();
        Console.WriteLine($"Extracted {outcome.BinFiles.Count} file(s) to:");
        Console.WriteLine($"  {Path.GetFullPath(outDir)}");
        Console.WriteLine($"  {complete} complete asset(s) with header-derived sizes; " +
                          $"{outcome.BinFiles.Count - complete} raw carve(s) as .bin.");
        Console.WriteLine($"Manifest: {Path.GetFileName(outcome.ManifestPath)}");
        return 0;
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
