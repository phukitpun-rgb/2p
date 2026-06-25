namespace JmdExplorer.Core.Services;

/// <summary>
/// Shannon entropy over byte data. Result is in bits/byte in the range [0, 8].
/// 0 means perfectly uniform (e.g. all zero), ~8 means high randomness
/// (compressed/encrypted data).
/// </summary>
public static class EntropyCalculator
{
    /// <summary>Computes entropy from a 256-bucket histogram and a total count.</summary>
    public static double FromHistogram(long[] histogram, long total)
    {
        if (histogram is null || histogram.Length != 256)
            throw new ArgumentException("Histogram must have 256 buckets.", nameof(histogram));
        if (total <= 0) return 0d;

        double entropy = 0d;
        for (int i = 0; i < 256; i++)
        {
            long count = histogram[i];
            if (count == 0) continue;
            double p = (double)count / total;
            entropy -= p * Math.Log2(p);
        }
        // Floating point can produce tiny negatives / overshoots; clamp.
        if (entropy < 0) entropy = 0;
        if (entropy > 8) entropy = 8;
        return entropy;
    }

    /// <summary>Computes entropy over a byte span.</summary>
    public static double Compute(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return 0d;
        Span<long> histogram = stackalloc long[256];
        foreach (byte b in data) histogram[b]++;
        var arr = histogram.ToArray();
        return FromHistogram(arr, data.Length);
    }

    /// <summary>
    /// Streams entropy over a (possibly huge) stream window without loading it all.
    /// </summary>
    public static double Compute(Stream stream, long start, long count, CancellationToken ct = default)
    {
        if (count <= 0) return 0d;
        long[] histogram = new long[256];
        long remaining = count;
        stream.Seek(start, SeekOrigin.Begin);

        byte[] buffer = new byte[Math.Min(64 * 1024, count)];
        long total = 0;
        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();
            int toRead = (int)Math.Min(buffer.Length, remaining);
            int read = stream.Read(buffer, 0, toRead);
            if (read <= 0) break;
            for (int i = 0; i < read; i++) histogram[buffer[i]]++;
            total += read;
            remaining -= read;
        }
        return FromHistogram(histogram, total);
    }
}
