using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;

namespace JmdExplorer.Decoders.Profiles;

/// <summary>
/// Guaranteed fallback profile. It always handles a file but only with
/// <see cref="Confidence.None"/>, so any more specific profile wins.
/// </summary>
public sealed class UnknownBinaryProfile : IFormatProfile
{
    public string Name => "Unknown binary";

    public bool CanHandle(BinaryReader reader) => true;

    public FormatAnalysisResult Analyze(Stream stream) => FormatAnalysisResult.Unknown(stream.Length);
}
