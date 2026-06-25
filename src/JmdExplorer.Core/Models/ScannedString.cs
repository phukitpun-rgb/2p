namespace JmdExplorer.Core.Models;

public enum StringEncodingKind
{
    Ascii,
    Utf8,
    Utf16LE,
    Utf16BE
}

/// <summary>A readable string discovered inside a binary file.</summary>
public sealed class ScannedString
{
    public long Offset { get; init; }
    public StringEncodingKind Encoding { get; init; }

    /// <summary>Number of characters (not bytes) in <see cref="Value"/>.</summary>
    public int Length => Value.Length;

    public required string Value { get; init; }

    public string OffsetHex => $"0x{Offset:X8}";

    public string EncodingDisplay => Encoding switch
    {
        StringEncodingKind.Ascii => "ASCII",
        StringEncodingKind.Utf8 => "UTF-8",
        StringEncodingKind.Utf16LE => "UTF-16 LE",
        StringEncodingKind.Utf16BE => "UTF-16 BE",
        _ => Encoding.ToString()
    };
}
