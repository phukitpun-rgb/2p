using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;

namespace JmdExplorer.Decoders.Plugins;

/// <summary>
/// PLACEHOLDER for a future, real Xenon v4s decoder. It is intentionally shipped as
/// "Not Available": <see cref="CanDecode"/> always returns false and <see cref="Decode"/>
/// returns <see cref="DecodeStatus.DecoderUnavailable"/>.
///
/// This is the documented extension point. When the real container format is reverse
/// engineered, implement the decoding here and produce genuine asset files — never
/// fabricate output. See docs/plugin-guide.md.
/// </summary>
public sealed class XenonV4sDecoderPlugin : IJmdDecoderPlugin
{
    public string Name => "Xenon v4s Decoder";
    public string Version => "N/A";

    public bool CanDecode(JmdFileContext context) => false;

    public DecodeResult Decode(JmdFileContext context) => DecodeResult.Unsupported(
        "No verified decoder is available for 'Xenon Data Format v4s'. This plugin is a " +
        "placeholder for a future implementation. The file's internal payload format is not " +
        "yet known, so no asset can be produced.");
}
