using System.Text.Json;
using JmdExplorer.Core.Models;
using JmdExplorer.Core.Services;
using JmdExplorer.Tools.SampleGenerator;
using Xunit;

namespace JmdExplorer.Tests;

public class RawBlockExtractorTests : IDisposable
{
    private readonly string _workDir;

    public RawBlockExtractorTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), "jmd_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_workDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void ExtractRange_ProducesByteAccurateBin()
    {
        var data = SampleFileBuilder.BuildXenonSample();
        string source = Path.Combine(_workDir, "carinfo.jmd");
        File.WriteAllBytes(source, data);

        var extractor = new RawBlockExtractor();
        string outDir = Path.Combine(_workDir, "out");
        var req = new ExtractionRequest { Name = "header", StartOffset = 0, EndOffset = 256 };

        var outcome = extractor.Extract(source, outDir, new[] { req }, "Xenon Data Format v4s", "ABC123");

        Assert.Single(outcome.BinFiles);
        byte[] extracted = File.ReadAllBytes(outcome.BinFiles[0]);
        Assert.Equal(256, extracted.Length);
        Assert.Equal(data.Take(256).ToArray(), extracted);
    }

    [Fact]
    public void FileName_FollowsNamingScheme()
    {
        var data = SampleFileBuilder.BuildXenonSample();
        string source = Path.Combine(_workDir, "carinfo.jmd");
        File.WriteAllBytes(source, data);

        var extractor = new RawBlockExtractor();
        string outDir = Path.Combine(_workDir, "out2");
        var req = new ExtractionRequest { Name = "header", StartOffset = 0, EndOffset = 0xFF };
        var outcome = extractor.Extract(source, outDir, new[] { req });

        string name = Path.GetFileName(outcome.BinFiles[0]);
        Assert.StartsWith("carinfo_header_0x00000000_0x000000FF", name);
        Assert.EndsWith(".bin", name);
    }

    [Fact]
    public void EndOffsetMinusOne_MeansEof()
    {
        var data = SampleFileBuilder.BuildXenonSample();
        string source = Path.Combine(_workDir, "carinfo.jmd");
        File.WriteAllBytes(source, data);

        var extractor = new RawBlockExtractor();
        string outDir = Path.Combine(_workDir, "out3");
        var req = new ExtractionRequest { Name = "payload", StartOffset = 256, EndOffset = -1 };
        var outcome = extractor.Extract(source, outDir, new[] { req });

        byte[] extracted = File.ReadAllBytes(outcome.BinFiles[0]);
        Assert.Equal(data.Length - 256, extracted.Length);
        Assert.Contains("EOF", Path.GetFileName(outcome.BinFiles[0]));
    }

    [Fact]
    public void Manifest_IsWrittenAndValid()
    {
        var data = SampleFileBuilder.BuildXenonSample();
        string source = Path.Combine(_workDir, "carinfo.jmd");
        File.WriteAllBytes(source, data);

        var extractor = new RawBlockExtractor();
        string outDir = Path.Combine(_workDir, "out4");
        var reqs = new[]
        {
            new ExtractionRequest { Name = "header", StartOffset = 0, EndOffset = 256, Type = "header" },
            new ExtractionRequest { Name = "payload", StartOffset = 256, EndOffset = -1, Type = "unknown_structured_binary" }
        };
        var outcome = extractor.Extract(source, outDir, reqs, "Xenon Data Format v4s", "HASH");

        Assert.True(File.Exists(outcome.ManifestPath));
        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var manifest = JsonSerializer.Deserialize<ExtractionManifest>(File.ReadAllText(outcome.ManifestPath), jsonOpts);
        Assert.NotNull(manifest);
        Assert.Equal("carinfo.jmd", manifest!.SourceFile);
        Assert.Equal("Xenon Data Format v4s", manifest.Format);
        Assert.Equal(2, manifest.Regions.Count);
        Assert.Equal("EOF", manifest.Regions[1].EndOffset);
        Assert.False(string.IsNullOrWhiteSpace(manifest.Disclaimer));
    }
}
