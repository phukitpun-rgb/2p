using JmdExplorer.Core.Models;
using JmdExplorer.Core.Services;
using Xunit;

namespace JmdExplorer.Tests;

public class RecordPatternDetectorTests
{
    [Fact]
    public void DetectsFixed64ByteRecords()
    {
        // Build 500 records of 64 bytes with constant leading columns.
        const int recordSize = 64;
        const int count = 500;
        using var ms = new MemoryStream();
        var rng = new Random(11);
        for (int r = 0; r < count; r++)
        {
            var rec = new byte[recordSize];
            for (int c = 0; c < 32; c++) rec[c] = (byte)c;      // constant columns
            for (int c = 32; c < recordSize; c++) rec[c] = (byte)rng.Next(0, 2);
            ms.Write(rec);
        }
        var data = ms.ToArray();

        var detector = new RecordPatternDetector();
        using var stream = new MemoryStream(data);
        var candidates = detector.Detect(stream, 0, data.Length);

        Assert.Contains(candidates, c => c.RecordSize == 64 && c.Confidence >= Confidence.Medium);
    }

    [Fact]
    public void HighEntropyRandom_YieldsNoStrongCandidate()
    {
        var data = new byte[64 * 1024];
        new Random(5).NextBytes(data);

        var detector = new RecordPatternDetector();
        using var stream = new MemoryStream(data);
        var candidates = detector.Detect(stream, 0, data.Length);

        Assert.DoesNotContain(candidates, c => c.Confidence == Confidence.High);
    }

    [Fact]
    public void TooSmallInput_ReturnsEmpty()
    {
        var data = new byte[16];
        var detector = new RecordPatternDetector();
        using var stream = new MemoryStream(data);
        Assert.Empty(detector.Detect(stream, 0, data.Length));
    }
}
