using JmdExplorer.Core.Services;

namespace JmdExplorer.App.ViewModels;

/// <summary>One row of the hex viewer: 16 bytes rendered as offset / hex / ASCII.</summary>
public sealed class HexLine
{
    public HexLine(long offset, byte[] bytes)
    {
        Offset = offset;
        OffsetText = $"0x{offset:X8}";
        HexText = ByteFormatter.ToHexString(bytes).PadRight(16 * 3 - 1);
        AsciiText = ByteFormatter.ToAsciiString(bytes);
    }

    public long Offset { get; }
    public string OffsetText { get; }
    public string HexText { get; }
    public string AsciiText { get; }
}
