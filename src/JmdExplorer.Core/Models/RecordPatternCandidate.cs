namespace JmdExplorer.Core.Models;

/// <summary>
/// A candidate fixed-size record layout discovered by the repeating-record heuristic.
/// This is a hypothesis, never a confirmed structure.
/// </summary>
public sealed class RecordPatternCandidate
{
    public int RecordSize { get; init; }
    public long EstimatedRecordCount { get; init; }

    /// <summary>0..1 ratio describing how similar consecutive records look.</summary>
    public double SimilarityRatio { get; init; }

    public double Entropy { get; init; }

    public Confidence Confidence { get; init; } = Confidence.Low;

    public string Interpretation { get; init; } = "Possible structured serialized data";
}
