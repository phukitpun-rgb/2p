namespace JmdExplorer.Core.Services;

/// <summary>Streaming forward search for a byte pattern. Never loads the whole file.</summary>
public static class PatternSearch
{
    /// <summary>
    /// Finds the first occurrence of <paramref name="pattern"/> at or after
    /// <paramref name="startOffset"/>. Returns -1 if not found.
    /// </summary>
    public static long FindFirst(
        Stream stream, byte[] pattern, long startOffset = 0, CancellationToken ct = default)
    {
        if (pattern is null || pattern.Length == 0) return -1;

        const int chunk = 256 * 1024;
        int keep = pattern.Length - 1;
        byte[] buf = new byte[chunk + keep];
        stream.Seek(startOffset, SeekOrigin.Begin);

        long bufStart = startOffset;
        int carried = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            int read = stream.Read(buf, carried, chunk);
            int available = carried + read;
            if (available <= 0) break;
            bool eof = read == 0;
            int limit = eof ? available : available - keep;

            for (int i = 0; i < limit; i++)
            {
                bool ok = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (buf[i + j] != pattern[j]) { ok = false; break; }
                }
                if (ok) return bufStart + i;
            }
            if (eof) break;

            Array.Copy(buf, available - keep, buf, 0, keep);
            bufStart += available - keep;
            carried = keep;
        }
        return -1;
    }
}
