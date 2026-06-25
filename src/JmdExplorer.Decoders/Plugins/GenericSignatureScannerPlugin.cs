using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;
using JmdExplorer.Core.Services;

namespace JmdExplorer.Decoders.Plugins;

/// <summary>
/// Runs the signature scanner over any binary and reports what it found. It does NOT
/// produce any asset files — it only confirms the presence of embedded signatures.
/// This keeps the "found a signature" state strictly separate from "decoded an asset".
/// </summary>
public sealed class GenericSignatureScannerPlugin : IJmdDecoderPlugin
{
    private readonly SignatureScanner _scanner;

    public GenericSignatureScannerPlugin(SignatureScanner scanner) => _scanner = scanner;

    public string Name => "Generic Signature Scanner";
    public string Version => "1.0";

    public bool CanDecode(JmdFileContext context) => true; // works on any binary

    public DecodeResult Decode(JmdFileContext context)
    {
        try
        {
            using var stream = context.OpenStream();
            var matches = _scanner.Scan(stream, context.Length);

            if (matches.Count == 0)
            {
                return new DecodeResult
                {
                    Status = DecodeStatus.DecoderUnavailable,
                    Success = false,
                    Message = "No standard embedded file signatures found. The content may be " +
                              "compressed, encrypted, serialized, or proprietary."
                };
            }

            return new DecodeResult
            {
                Status = DecodeStatus.EmbeddedSignatureFound,
                Success = true,
                Message = $"Found {matches.Count} embedded signature(s). Use the Embedded File " +
                          "Scanner to extract raw blocks. Note: a signature is not proof of a " +
                          "complete, valid embedded file.",
                ProducedFiles = Array.Empty<ProducedFile>()
            };
        }
        catch (Exception ex)
        {
            return new DecodeResult
            {
                Status = DecodeStatus.NotAttempted,
                Success = false,
                Message = $"Signature scan failed: {ex.Message}"
            };
        }
    }
}
