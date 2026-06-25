using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;
using JmdExplorer.Decoders.Profiles;

namespace JmdExplorer.Decoders.Plugins;

/// <summary>
/// Inspects Xenon v4s files. It recognizes structure and can drive raw extraction, but
/// it never decodes the payload into a usable asset. Its highest honest outcome is
/// <see cref="DecodeStatus.StructureRecognized"/>.
/// </summary>
public sealed class XenonV4sInspectorPlugin : IJmdDecoderPlugin
{
    private readonly XenonDataFormatV4sProfile _profile = new();

    public string Name => "Xenon v4s Inspector";
    public string Version => "1.0";

    public bool CanDecode(JmdFileContext context)
    {
        try
        {
            using var stream = context.OpenStream();
            using var reader = new BinaryReader(stream);
            return _profile.CanHandle(reader);
        }
        catch
        {
            return false;
        }
    }

    public DecodeResult Decode(JmdFileContext context)
    {
        return new DecodeResult
        {
            Status = DecodeStatus.StructureRecognized,
            Success = true,
            Message = "Recognized as 'Xenon Data Format v4s'. Structure analysis and raw block " +
                      "extraction are available. No verified decoder exists, so the payload is " +
                      "NOT decoded into a usable asset.",
            ProducedFiles = Array.Empty<ProducedFile>(),
            Warnings = new[]
            {
                "Inspection only — decoding into model/texture/config is not implemented."
            }
        };
    }
}
