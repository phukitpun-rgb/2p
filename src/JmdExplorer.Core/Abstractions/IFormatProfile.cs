using JmdExplorer.Core.Models;

namespace JmdExplorer.Core.Abstractions;

/// <summary>
/// A pluggable recognizer for a binary file format. Profiles only describe and
/// recognize structure; they never claim to decode the payload into an asset.
/// </summary>
public interface IFormatProfile
{
    string Name { get; }

    /// <summary>
    /// Cheap probe over the header. The reader is positioned at the start of the
    /// file; implementations must not throw on short/garbage input.
    /// </summary>
    bool CanHandle(BinaryReader reader);

    /// <summary>
    /// Full analysis pass. The stream is seekable and positioned at 0.
    /// Implementations must not dispose the stream.
    /// </summary>
    FormatAnalysisResult Analyze(Stream stream);
}
