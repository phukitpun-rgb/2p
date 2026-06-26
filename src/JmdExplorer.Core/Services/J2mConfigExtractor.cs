using System.Text;

namespace JmdExplorer.Core.Services;

/// <summary>
/// Extracts car/stat configuration blocks from "J2m Data Format 1.0" archives (e.g.
/// ai.jmd) and renders each as XML. The configs are stored as a serialized property
/// tree of length-prefixed UTF-16 key/value strings (a "param" node per car); this
/// reader walks that stream and reconstructs the same XML the game tools export.
/// </summary>
public static class J2mConfigExtractor
{
    /// <summary>One recovered config block: the emblem/name (if present) and its XML.</summary>
    public sealed record Config(int Index, string Label, string Xml);

    // Keys that begin the trailing collision-mesh node — config ends here.
    private static readonly HashSet<string> StopKeys = new(StringComparer.Ordinal)
    {
        "mesh", "vertex", "face", "WH_FRadius"
    };

    public static IReadOnlyList<Config> Extract(byte[] data)
    {
        var marker = Encoding.Unicode.GetBytes("param");
        var results = new List<Config>();
        int search = 0, index = 0;
        while (true)
        {
            int p = IndexOf(data, marker, search);
            if (p < 0) break;
            search = p + marker.Length;

            // The node body begins after "param" + 8 pad bytes + a u32 child count.
            int i = p + marker.Length + 8 + 4;
            var (xml, label) = ReadBlock(data, i);
            if (xml is not null)
                results.Add(new Config(index++, label, xml));
        }
        return results;
    }

    private static (string? xml, string label) ReadBlock(byte[] data, int start)
    {
        // First collect the flat run of strings (keys and values interleaved) until the
        // config ends, then assemble XML with the few known nested sections.
        var toks = new List<string>();
        int i = start;
        while (i < data.Length && toks.Count < 1000)
        {
            var (s, ni) = ReadString(data, i);
            if (s is null) { i += 2; continue; }
            if (StopKeys.Contains(s)) break;
            toks.Add(s);
            i = ni;
        }
        if (toks.Count == 0) return (null, "");

        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n<car>\r\n");
        string label = "";
        int j = 0;
        while (j < toks.Count)
        {
            string key = toks[j];
            if (key == "defaultPaint")
            {
                sb.Append("\t<defaultPaint>\r\n"); j++;
                while (j + 1 < toks.Count && toks[j] == "paint")
                { sb.Append("\t\t<paint>").Append(Escape(toks[j + 1])).Append("</paint>\r\n"); j += 2; }
                sb.Append("\t</defaultPaint>\r\n");
            }
            else if (key == "cameraOffset")
            {
                sb.Append("\t<cameraOffset>\r\n"); j++;
                while (j < toks.Count && (toks[j] == "bumper" || toks[j] == "bonnet"))
                {
                    string sec = toks[j++];
                    sb.Append("\t\t<").Append(sec).Append(">\r\n");
                    while (j + 1 < toks.Count && (toks[j] == "dist" || toks[j] == "height"))
                    { sb.Append("\t\t\t<").Append(toks[j]).Append('>').Append(Escape(toks[j + 1])).Append("</").Append(toks[j]).Append(">\r\n"); j += 2; }
                    sb.Append("\t\t</").Append(sec).Append(">\r\n");
                }
                sb.Append("\t</cameraOffset>\r\n");
            }
            else if (j + 1 < toks.Count)
            {
                string val = toks[j + 1];
                if (key == "emblem") label = val;
                sb.Append('\t').Append('<').Append(key).Append('>')
                  .Append(Escape(val)).Append("</").Append(key).Append(">\r\n");
                j += 2;
            }
            else j++;
        }
        sb.Append("</car>\r\n");
        return (sb.ToString(), label);
    }

    private static (string? s, int next) ReadString(byte[] d, int i)
    {
        if (i + 4 > d.Length) return (null, i);
        uint len = (uint)(d[i] | (d[i + 1] << 8) | (d[i + 2] << 16) | (d[i + 3] << 24));
        if (len is < 1 or > 256 || i + 4 + (int)len * 2 > d.Length) return (null, i);
        int start = i + 4;
        for (int k = 0; k < len; k++)
        {
            byte lo = d[start + k * 2], hi = d[start + k * 2 + 1];
            // Printable ASCII plus tab (0x09): some values are stored tab-wrapped, e.g. "\t130\t".
            if (hi != 0 || !(lo == 0x09 || (lo >= 0x20 && lo <= 0x7e))) return (null, i);
        }
        string s = Encoding.Unicode.GetString(d, start, (int)len * 2).Trim('\t', ' ');
        return (s, start + (int)len * 2);
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        int limit = haystack.Length - needle.Length;
        for (int i = start; i <= limit; i++)
        {
            int j = 0;
            while (j < needle.Length && haystack[i + j] == needle[j]) j++;
            if (j == needle.Length) return i;
        }
        return -1;
    }
}
