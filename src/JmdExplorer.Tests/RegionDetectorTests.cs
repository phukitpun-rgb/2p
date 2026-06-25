using JmdExplorer.Core.Models;
using JmdExplorer.Core.Services;
using JmdExplorer.Tools.SampleGenerator;
using Xunit;

namespace JmdExplorer.Tests;

public class RegionDetectorTests
{
    [Fact]
    public void DistinguishesZeroPaddingFromHighEntropy()
    {
        using var ms = new MemoryStream();
        ms.Write(new byte[8192]);                   // zero padding
        var rnd = new byte[8192];
        new Random(3).NextBytes(rnd);
        ms.Write(rnd);                              // high entropy
        var data = ms.ToArray();

        var detector = new RegionDetector();
        using var stream = new MemoryStream(data);
        var regions = detector.Detect(stream, data.Length);

        Assert.Contains(regions, r => r.Type == RegionType.ZeroPadding);
        Assert.Contains(regions, r => r.Type == RegionType.HighEntropyPayload);
    }

    [Fact]
    public void RegionsCoverEntireFile_WithoutGaps()
    {
        var data = SampleFileBuilder.BuildXenonSample();
        var detector = new RegionDetector();
        using var stream = new MemoryStream(data);
        var regions = detector.Detect(stream, data.Length);

        Assert.NotEmpty(regions);
        Assert.Equal(0, regions.First().StartOffset);
        Assert.Equal(data.Length, regions.Last().EndOffset);

        // Contiguous: each region begins where the previous ended.
        for (int i = 1; i < regions.Count; i++)
            Assert.Equal(regions[i - 1].EndOffset, regions[i].StartOffset);
    }

    [Fact]
    public void FirstRegion_IsHeader()
    {
        var data = SampleFileBuilder.BuildXenonSample();
        var detector = new RegionDetector();
        using var stream = new MemoryStream(data);
        var regions = detector.Detect(stream, data.Length);
        Assert.Equal(RegionType.Header, regions.First().Type);
    }

    [Fact]
    public void PercentOfFile_SumsToApproximatelyOne()
    {
        var data = SampleFileBuilder.BuildXenonSample();
        var detector = new RegionDetector();
        using var stream = new MemoryStream(data);
        var regions = detector.Detect(stream, data.Length);
        double total = regions.Sum(r => r.PercentOfFile);
        Assert.InRange(total, 0.98, 1.02);
    }

    [Fact]
    public void Cancellation_IsHonored()
    {
        var data = SampleFileBuilder.BuildXenonSample();
        var detector = new RegionDetector();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var stream = new MemoryStream(data);
        Assert.Throws<OperationCanceledException>(() =>
            detector.Detect(stream, data.Length, null, cts.Token));
    }
}
