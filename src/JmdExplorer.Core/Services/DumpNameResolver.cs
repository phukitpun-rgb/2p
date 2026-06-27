using System.Text;

namespace JmdExplorer.Core.Services;

/// <summary>
/// Recovers the real internal file names for a Xenon .jmd by reading the decrypted
/// structures the official XenonFileSystem tool keeps in memory. Given a process
/// memory dump (taken while that tool had the .jmd open, with the whole list scrolled
/// so every row is realised), this maps each plaintext DDS texture in the .jmd to its
/// real name.
///
/// How it works (no decryption needed — the tool already decrypted everything):
///   * The dump holds file-entry structs: ext("dds\0"/"png\0") + hash(@+8) + UTF-16 name(@+16).
///   * It also holds the decrypted index, where each entry's name-hash sits immediately
///     before that entry's data offset.
///   * We anchor on the real DDS offsets in the .jmd (ground truth), look up the hash
///     stored just before that offset in the dump, then the name for that hash.
/// </summary>
public static class DumpNameResolver
{
    /// <summary>Maps DDS data offset (in the .jmd) -> real name (without extension).</summary>
    public static Dictionary<long, string> ResolveDdsNames(byte[] jmd, byte[] dump)
    {
        var hashToName = BuildHashToName(dump);
        var ddsOffsets = new HashSet<uint>();
        foreach (long o in FindDdsOffsets(jmd)) ddsOffsets.Add((uint)o);

        // Single pass over the dump: wherever a DDS offset appears as a u32, the u32 just
        // before it is that entry's name-hash. First confirmed hit per offset wins.
        var result = new Dictionary<long, string>();
        int n = dump.Length;
        for (int i = 4; i + 4 <= n; i++)
        {
            uint val = U32(dump, i);
            if (val == 0 || !ddsOffsets.Contains(val) || result.ContainsKey(val)) continue;
            uint hash = U32(dump, i - 4);
            if (hash != 0 && hashToName.TryGetValue(hash, out var name))
                result[val] = name;
        }
        return result;
    }

    /// <summary>Number of distinct file names found in the dump (a quick coverage gauge).</summary>
    public static int CountNames(byte[] dump) => BuildHashToName(dump).Count;

    private static Dictionary<uint, string> BuildHashToName(byte[] d)
    {
        var map = new Dictionary<uint, string>();
        int n = d.Length;
        // entry struct: "dds\0"/"png\0"/"jpg\0"/"tga\0", hash at +8, UTF-16 name at +16.
        for (int i = 0; i + 16 < n; i++)
        {
            if (d[i + 3] != 0) continue;
            // cheap first-byte gate before the full ext check
            byte b0 = d[i];
            if (b0 != 'd' && b0 != 'p' && b0 != 'j' && b0 != 't') continue;
            string ext = Encoding.ASCII.GetString(d, i, 3);
            if (ext is not ("dds" or "png" or "jpg" or "tga")) continue;

            uint hash = U32(d, i + 8);
            if (hash == 0 || map.ContainsKey(hash)) continue;
            string? name = ReadUtf16(d, i + 16, 64);
            if (name is { Length: > 0 }) map[hash] = name;
        }
        return map;
    }

    private static List<long> FindDdsOffsets(byte[] data)
    {
        var offs = new List<long>();
        for (int i = 0; i + 4 <= data.Length; i++)
            if (data[i] == 'D' && data[i + 1] == 'D' && data[i + 2] == 'S' && data[i + 3] == ' ')
                offs.Add(i);
        return offs;
    }

    private static string? ReadUtf16(byte[] d, int start, int maxChars)
    {
        int end = start;
        for (int c = 0; c < maxChars && end + 1 < d.Length; c++, end += 2)
        {
            byte lo = d[end], hi = d[end + 1];
            if (lo == 0 && hi == 0) break;
            if (hi != 0 || lo < 0x20 || lo > 0x7e) return null;
        }
        int len = end - start;
        return len >= 2 ? Encoding.Unicode.GetString(d, start, len) : null;
    }

    private static uint U32(byte[] b, int o) =>
        (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
}
