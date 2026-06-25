using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;

namespace JmdExplorer.Core.Services;

/// <summary>
/// Runs the registered <see cref="IFormatProfile"/> instances against a file and
/// returns the highest-confidence analysis. The <c>UnknownBinaryProfile</c> acts as
/// the guaranteed fallback so this never returns null.
/// </summary>
public sealed class FormatDetector
{
    private readonly IReadOnlyList<IFormatProfile> _profiles;
    private readonly IAppLogger? _logger;

    public FormatDetector(IEnumerable<IFormatProfile> profiles, IAppLogger? logger = null)
    {
        _profiles = profiles.ToList();
        _logger = logger;
    }

    public FormatAnalysisResult Detect(JmdFileContext context)
    {
        FormatAnalysisResult? best = null;

        foreach (var profile in _profiles)
        {
            try
            {
                using var probeStream = context.OpenStream();
                using var reader = new BinaryReader(probeStream, System.Text.Encoding.ASCII, leaveOpen: true);
                if (!profile.CanHandle(reader)) continue;

                using var analyzeStream = context.OpenStream();
                var result = profile.Analyze(analyzeStream);
                if (best is null || result.Confidence > best.Confidence)
                    best = result;
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Format profile '{profile.Name}' failed: {ex.Message}");
            }
        }

        return best ?? FormatAnalysisResult.Unknown(context.Length);
    }
}
