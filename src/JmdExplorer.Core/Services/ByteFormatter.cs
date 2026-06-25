using System.Text;

namespace JmdExplorer.Core.Services;

/// <summary>Formats byte ranges for the hex viewer and "copy as" commands.</summary>
public static class ByteFormatter
{
    public static string ToHexString(ReadOnlySpan<byte> data, bool spaced = true)
    {
        var sb = new StringBuilder(data.Length * (spaced ? 3 : 2));
        for (int i = 0; i < data.Length; i++)
        {
            sb.Append(data[i].ToString("X2"));
            if (spaced && i < data.Length - 1) sb.Append(' ');
        }
        return sb.ToString();
    }

    /// <summary>ASCII rendering with non-printable bytes shown as '.'.</summary>
    public static string ToAsciiString(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder(data.Length);
        foreach (byte b in data)
            sb.Append(b >= 0x20 && b <= 0x7E ? (char)b : '.');
        return sb.ToString();
    }

    public static string ToCSharpByteArray(ReadOnlySpan<byte> data, string variableName = "data")
    {
        var sb = new StringBuilder();
        sb.Append("byte[] ").Append(variableName).Append(" = new byte[] { ");
        for (int i = 0; i < data.Length; i++)
        {
            sb.Append("0x").Append(data[i].ToString("X2"));
            if (i < data.Length - 1) sb.Append(", ");
        }
        sb.Append(" };");
        return sb.ToString();
    }

    public static string ToBase64(ReadOnlySpan<byte> data) => Convert.ToBase64String(data);

    /// <summary>
    /// Parses a hex search pattern like "58 65 6E 6F 6E" or "586E6F6E" into bytes.
    /// Returns null when the input is not valid hex.
    /// </summary>
    public static byte[]? ParseHexPattern(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        string cleaned = new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned[2..];
        if (cleaned.Length == 0 || cleaned.Length % 2 != 0) return null;

        var bytes = new byte[cleaned.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            if (!byte.TryParse(cleaned.AsSpan(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                return null;
        }
        return bytes;
    }
}
