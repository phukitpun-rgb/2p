using JmdExplorer.Core.Models;

namespace JmdExplorer.Core.Services;

/// <summary>
/// Heuristic detector for fixed-size repeating records. It samples a window of the
/// file, lays it out as a matrix of candidate record sizes, and measures how similar
/// the records are column-by-column. High column similarity across many records is a
/// (weak) signal of a serialized record table.
/// </summary>
public sealed class RecordPatternDetector
{
    private static readonly int[] DefaultCandidateSizes = { 8, 16, 24, 32, 48, 64, 96, 128, 192, 256 };

    public IReadOnlyList<RecordPatternCandidate> Detect(
        Stream stream,
        long start,
        long count,
        IReadOnlyList<int>? candidateSizes = null,
        CancellationToken ct = default)
    {
        candidateSizes ??= DefaultCandidateSizes;
        var results = new List<RecordPatternCandidate>();
        if (count < 64) return results;

        // Sample up to 1 MiB so the heuristic stays fast on large files.
        long sampleLen = Math.Min(count, 1L * 1024 * 1024);
        byte[] sample = new byte[sampleLen];
        stream.Seek(start, SeekOrigin.Begin);
        int filled = ReadFully(stream, sample, (int)sampleLen);
        if (filled <= 0) return results;
        var data = sample.AsSpan(0, filled);

        foreach (int recordSize in candidateSizes)
        {
            ct.ThrowIfCancellationRequested();
            if (recordSize <= 0 || filled < recordSize * 4) continue;

            int recordCount = filled / recordSize;
            double similarity = ColumnSimilarity(data, recordSize, recordCount);
            double entropy = EntropyCalculator.Compute(data);
            long estimatedTotal = count / recordSize;

            Confidence confidence = similarity switch
            {
                >= 0.85 => Confidence.High,
                >= 0.65 => Confidence.Medium,
                >= 0.45 => Confidence.Low,
                _ => Confidence.None
            };

            if (confidence == Confidence.None) continue;

            results.Add(new RecordPatternCandidate
            {
                RecordSize = recordSize,
                EstimatedRecordCount = estimatedTotal,
                SimilarityRatio = similarity,
                Entropy = entropy,
                Confidence = confidence,
                Interpretation = InterpretFor(confidence, entropy)
            });
        }

        return results
            .OrderByDescending(r => r.SimilarityRatio)
            .ThenByDescending(r => r.Confidence)
            .ToList();
    }

    /// <summary>
    /// For each byte column across records, measures the fraction of records whose
    /// value equals the column's modal value. Averaged across columns gives a 0..1
    /// similarity score. Constant/low-cardinality columns push this up.
    /// </summary>
    private static double ColumnSimilarity(ReadOnlySpan<byte> data, int recordSize, int recordCount)
    {
        if (recordCount < 2) return 0;
        double totalColScore = 0;

        for (int col = 0; col < recordSize; col++)
        {
            Span<int> hist = stackalloc int[256];
            for (int r = 0; r < recordCount; r++)
            {
                byte v = data[r * recordSize + col];
                hist[v]++;
            }
            int modal = 0;
            for (int v = 0; v < 256; v++) if (hist[v] > modal) modal = hist[v];
            totalColScore += (double)modal / recordCount;
        }
        return totalColScore / recordSize;
    }

    private static string InterpretFor(Confidence confidence, double entropy)
    {
        if (entropy >= 7.5) return "High-entropy fixed-size blocks (possibly packed/encrypted records)";
        return confidence switch
        {
            Confidence.High => "Likely structured serialized records",
            Confidence.Medium => "Possible structured serialized data",
            _ => "Weak repeating pattern (inconclusive)"
        };
    }

    private static int ReadFully(Stream s, byte[] buffer, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = s.Read(buffer, total, count - total);
            if (read <= 0) break;
            total += read;
        }
        return total;
    }
}
