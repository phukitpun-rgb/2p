using JmdExplorer.Core.Models;

namespace JmdExplorer.Core.Services;

public sealed class RegionDetectionOptions
{
    /// <summary>Size of each analysis block. Smaller = finer regions, more CPU.</summary>
    public int BlockSize { get; init; } = 4096;

    /// <summary>Entropy at/above which a block is considered high-entropy payload.</summary>
    public double HighEntropyThreshold { get; init; } = 7.2;

    /// <summary>Entropy at/below which a block is considered low-entropy/metadata.</summary>
    public double LowEntropyThreshold { get; init; } = 4.0;

    /// <summary>Optional hints from the active format profile.</summary>
    public IReadOnlyList<RegionHint> Hints { get; init; } = Array.Empty<RegionHint>();
}

/// <summary>
/// Classifies a file into regions based on per-block entropy and zero-fill ratio.
/// Offsets are discovered statistically — never hard-coded — although profile hints
/// can refine the first region's label.
/// </summary>
public sealed class RegionDetector
{
    public IReadOnlyList<Region> Detect(
        Stream stream,
        long length,
        RegionDetectionOptions? options = null,
        CancellationToken ct = default,
        IProgress<double>? progress = null)
    {
        options ??= new RegionDetectionOptions();
        var regions = new List<Region>();
        if (length <= 0) return regions;

        int block = options.BlockSize;
        byte[] buffer = new byte[block];
        stream.Seek(0, SeekOrigin.Begin);

        // First pass: classify each block.
        var blocks = new List<BlockInfo>();
        long pos = 0;
        while (pos < length)
        {
            ct.ThrowIfCancellationRequested();
            int read = stream.Read(buffer, 0, block);
            if (read <= 0) break;

            var span = buffer.AsSpan(0, read);
            double entropy = EntropyCalculator.Compute(span);
            double zeroRatio = ZeroRatio(span);
            blocks.Add(new BlockInfo(pos, read, entropy, zeroRatio));
            pos += read;
            progress?.Report(Math.Clamp((double)pos / length, 0, 1) * 0.7);
        }

        if (blocks.Count == 0) return regions;

        // The very first block is (almost always) the header region.
        blocks[0] = blocks[0] with { ForcedType = RegionType.Header };

        // Second pass: merge contiguous blocks of the same classification.
        RegionType currentType = Classify(blocks[0], options);
        long regionStart = blocks[0].Offset;
        var entropyAccum = new List<double>();

        for (int i = 0; i < blocks.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var b = blocks[i];
            RegionType t = b.ForcedType ?? Classify(b, options);

            if (t != currentType)
            {
                regions.Add(BuildRegion(currentType, regionStart, b.Offset, length, entropyAccum, options));
                currentType = t;
                regionStart = b.Offset;
                entropyAccum = new List<double>();
            }
            entropyAccum.Add(b.Entropy);
        }
        long fileEnd = blocks[^1].Offset + blocks[^1].Length;
        regions.Add(BuildRegion(currentType, regionStart, fileEnd, length, entropyAccum, options));

        ApplyHints(regions, options.Hints);

        progress?.Report(1d);
        return regions;
    }

    private static double ZeroRatio(ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty) return 0;
        int zeros = 0;
        foreach (byte b in span) if (b == 0) zeros++;
        return (double)zeros / span.Length;
    }

    private static RegionType Classify(BlockInfo b, RegionDetectionOptions o)
    {
        if (b.ForcedType is { } forced) return forced;
        if (b.ZeroRatio >= 0.97) return RegionType.ZeroPadding;
        if (b.Entropy >= o.HighEntropyThreshold) return RegionType.HighEntropyPayload;
        if (b.Entropy <= o.LowEntropyThreshold) return RegionType.Metadata;
        return RegionType.StructuredBinary;
    }

    private static Region BuildRegion(
        RegionType type, long start, long end, long fileLength, List<double> entropies, RegionDetectionOptions o)
    {
        double avgEntropy = entropies.Count > 0 ? entropies.Average() : 0;
        long size = end - start;
        return new Region
        {
            Name = NameFor(type),
            StartOffset = start,
            EndOffset = end,
            Type = type,
            Entropy = avgEntropy,
            PercentOfFile = fileLength > 0 ? (double)size / fileLength : 0,
            RepetitionScore = type == RegionType.RepeatingRecord ? 0.7 : EstimateRepetition(avgEntropy),
            SuggestedInterpretation = InterpretationFor(type)
        };
    }

    private static double EstimateRepetition(double entropy)
    {
        // Lower entropy loosely correlates with more repetition; this is a rough hint only.
        return Math.Clamp(1.0 - (entropy / 8.0), 0, 1);
    }

    private static string NameFor(RegionType type) => type switch
    {
        RegionType.Header => "Header",
        RegionType.Metadata => "Metadata",
        RegionType.ZeroPadding => "Zero padding",
        RegionType.HighEntropyPayload => "Payload",
        RegionType.RepeatingRecord => "Record table",
        RegionType.StructuredBinary => "Structured binary",
        _ => "Unknown"
    };

    private static string InterpretationFor(RegionType type) => type switch
    {
        RegionType.Header => "Format header",
        RegionType.Metadata => "Possible metadata / index (low entropy)",
        RegionType.ZeroPadding => "Zero / alignment padding",
        RegionType.HighEntropyPayload => "Compressed, encrypted, or packed payload (high entropy)",
        RegionType.RepeatingRecord => "Repeating fixed-size records",
        RegionType.StructuredBinary => "Structured binary data",
        _ => "Unknown binary region"
    };

    private static void ApplyHints(List<Region> regions, IReadOnlyList<RegionHint> hints)
    {
        if (hints.Count == 0) return;
        foreach (var hint in hints)
        {
            // Find the region that contains the hint's start offset and annotate it.
            var match = regions.FirstOrDefault(r => hint.StartOffset >= r.StartOffset && hint.StartOffset < r.EndOffset);
            if (match is null) continue;
            int idx = regions.IndexOf(match);
            regions[idx] = new Region
            {
                Name = hint.Name,
                StartOffset = match.StartOffset,
                EndOffset = match.EndOffset,
                Type = match.Type,
                Entropy = match.Entropy,
                PercentOfFile = match.PercentOfFile,
                RepetitionScore = match.RepetitionScore,
                SuggestedInterpretation = hint.Note ?? match.SuggestedInterpretation
            };
        }
    }

    private readonly record struct BlockInfo(long Offset, int Length, double Entropy, double ZeroRatio)
    {
        public RegionType? ForcedType { get; init; }
    }
}
