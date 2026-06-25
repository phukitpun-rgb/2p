namespace JmdExplorer.Core.Models;

/// <summary>
/// Qualitative confidence level used across analyzers. We deliberately avoid
/// fabricating precise probabilities for heuristics that cannot justify them.
/// </summary>
public enum Confidence
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3
}
