using JmdExplorer.Core.Models;

namespace JmdExplorer.Core.Services;

/// <summary>
/// Describes a known file/magic signature we can search for inside a binary.
/// </summary>
public sealed class FileSignature
{
    public required string Type { get; init; }
    public required byte[] Magic { get; init; }

    public Confidence BaseConfidence { get; init; } = Confidence.Medium;
    public string Action { get; init; } = "Extract Raw";

    /// <summary>
    /// Optional size estimator: given the host stream and the match offset, returns
    /// an estimated embedded file length, or null if it cannot be derived. The
    /// scanner saves/restores the stream position around this call.
    /// </summary>
    public Func<Stream, long, long?>? EstimateSize { get; init; }
}

/// <summary>
/// Streams over a file looking for embedded file signatures. The scanner reads in
/// overlapping windows so it never loads the whole file into memory.
/// </summary>
public sealed class SignatureScanner
{
    private readonly IReadOnlyList<FileSignature> _signatures;
    private readonly int _maxMagicLength;

    public SignatureScanner(IReadOnlyList<FileSignature>? signatures = null)
    {
        _signatures = signatures ?? DefaultSignatures.All;
        _maxMagicLength = _signatures.Count == 0 ? 1 : _signatures.Max(s => s.Magic.Length);
    }

    public IReadOnlyList<FileSignature> Signatures => _signatures;

    /// <summary>
    /// Scans the stream. Reports progress in [0,1]. Honors cancellation. Returns an
    /// empty list when nothing matches (callers should surface the honest message).
    /// </summary>
    public IReadOnlyList<EmbeddedSignatureMatch> Scan(
        Stream stream,
        long length,
        CancellationToken ct = default,
        IProgress<double>? progress = null)
    {
        var matches = new List<EmbeddedSignatureMatch>();
        if (length <= 0 || _signatures.Count == 0) return matches;

        const int chunkSize = 256 * 1024;
        int overlap = Math.Max(0, _maxMagicLength - 1);
        byte[] buffer = new byte[chunkSize + overlap];

        stream.Seek(0, SeekOrigin.Begin);
        long bufferStartFileOffset = 0;
        int carried = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            int read = stream.Read(buffer, carried, chunkSize);
            int available = carried + read;
            if (available <= 0) break;

            bool eof = read == 0;
            int scanLimit = eof ? available : available - overlap;

            for (int i = 0; i < scanLimit; i++)
            {
                foreach (var sig in _signatures)
                {
                    if (MatchesAt(buffer, i, available, sig.Magic))
                    {
                        matches.Add(new EmbeddedSignatureMatch
                        {
                            Type = sig.Type,
                            Offset = bufferStartFileOffset + i,
                            Confidence = sig.BaseConfidence,
                            Action = sig.Action,
                            MagicBytes = sig.Magic
                        });
                    }
                }
            }

            if (eof) break;

            Array.Copy(buffer, available - overlap, buffer, 0, overlap);
            bufferStartFileOffset += available - overlap;
            carried = overlap;

            progress?.Report(Math.Clamp((double)stream.Position / length, 0, 1));
        }

        var deduped = Deduplicate(matches);

        // Second pass: size estimates (may seek the stream).
        var enriched = new List<EmbeddedSignatureMatch>(deduped.Count);
        foreach (var m in deduped)
        {
            ct.ThrowIfCancellationRequested();
            var sig = _signatures.First(s => s.Type == m.Type);
            long? size = m.SizeEstimate;
            if (sig.EstimateSize is not null)
            {
                long savedPos = stream.Position;
                try { size = sig.EstimateSize(stream, m.Offset); }
                catch { size = null; }
                finally { try { stream.Seek(savedPos, SeekOrigin.Begin); } catch { /* ignore */ } }
            }
            enriched.Add(new EmbeddedSignatureMatch
            {
                Type = m.Type,
                Offset = m.Offset,
                Confidence = m.Confidence,
                Action = m.Action,
                MagicBytes = m.MagicBytes,
                SizeEstimate = size
            });
        }

        progress?.Report(1d);
        return enriched.OrderBy(m => m.Offset).ThenBy(m => m.Type).ToList();
    }

    private static bool MatchesAt(byte[] buffer, int index, int available, byte[] magic)
    {
        if (index + magic.Length > available) return false;
        for (int j = 0; j < magic.Length; j++)
        {
            if (buffer[index + j] != magic[j]) return false;
        }
        return true;
    }

    private static List<EmbeddedSignatureMatch> Deduplicate(List<EmbeddedSignatureMatch> matches)
    {
        return matches
            .GroupBy(m => (m.Type, m.Offset))
            .Select(g => g.First())
            .ToList();
    }
}
