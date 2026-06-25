using JmdExplorer.Core.Models;

namespace JmdExplorer.Core.Abstractions;

/// <summary>
/// Extension point for future decoders. Implementations are discovered through DI.
/// A plugin MUST be honest about what it produced (see <see cref="DecodeResult.Status"/>).
/// </summary>
public interface IJmdDecoderPlugin
{
    string Name { get; }
    string Version { get; }

    /// <summary>Whether this plugin recognizes/handles the given file.</summary>
    bool CanDecode(JmdFileContext context);

    /// <summary>Runs the plugin. Must never throw; wrap failures into a DecodeResult.</summary>
    DecodeResult Decode(JmdFileContext context);
}
