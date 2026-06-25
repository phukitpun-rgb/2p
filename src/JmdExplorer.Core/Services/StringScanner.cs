using System.Text;
using JmdExplorer.Core.Models;

namespace JmdExplorer.Core.Services;

public sealed class StringScanOptions
{
    public int MinLength { get; init; } = 4;
    public bool ScanAscii { get; init; } = true;
    public bool ScanUtf16LE { get; init; } = true;
    public bool ScanUtf16BE { get; init; } = false;

    /// <summary>Case-insensitive keyword filter; null/empty means no filter.</summary>
    public string? Keyword { get; init; }

    /// <summary>If true, only keep strings composed entirely of printable characters.</summary>
    public bool PrintableOnly { get; init; } = true;

    /// <summary>Hard cap to keep the UI responsive on huge files.</summary>
    public int MaxResults { get; init; } = 50_000;
}

/// <summary>
/// Extracts human-readable strings from a binary stream. ASCII/UTF-8 runs are found
/// by the printable-byte heuristic; UTF-16 (LE/BE) runs by the "char, 0x00" cadence.
/// </summary>
public sealed class StringScanner
{
    public IReadOnlyList<ScannedString> Scan(
        Stream stream,
        long length,
        StringScanOptions options,
        CancellationToken ct = default,
        IProgress<double>? progress = null)
    {
        var results = new List<ScannedString>();
        if (length <= 0) return results;

        // ASCII/UTF-8: we treat bytes 0x20..0x7E (+ tab) as printable. UTF-8 multibyte
        // sequences mostly survive this as their lead/continuation bytes are >= 0x80,
        // so we additionally decode candidate runs as UTF-8 when high bytes appear.
        const int chunkSize = 256 * 1024;
        byte[] buffer = new byte[chunkSize];

        stream.Seek(0, SeekOrigin.Begin);
        long filePos = 0;

        var asciiRun = new List<byte>();
        long asciiRunStart = 0;
        bool asciiHasHighByte = false;

        // UTF-16 LE/BE state machines run over the same byte stream.
        var u16le = new Utf16RunState(littleEndian: true);
        var u16be = new Utf16RunState(littleEndian: false);

        while (results.Count < options.MaxResults)
        {
            ct.ThrowIfCancellationRequested();
            int read = stream.Read(buffer, 0, chunkSize);
            if (read <= 0) break;

            for (int i = 0; i < read; i++)
            {
                byte b = buffer[i];
                long pos = filePos + i;

                if (options.ScanAscii)
                {
                    if (IsAsciiPrintable(b) || b >= 0x80)
                    {
                        if (asciiRun.Count == 0) asciiRunStart = pos;
                        asciiRun.Add(b);
                        if (b >= 0x80) asciiHasHighByte = true;
                    }
                    else
                    {
                        FlushAscii(results, asciiRun, asciiRunStart, asciiHasHighByte, options);
                        asciiRun.Clear();
                        asciiHasHighByte = false;
                    }
                }

                if (options.ScanUtf16LE) u16le.Push(b, pos, results, options);
                if (options.ScanUtf16BE) u16be.Push(b, pos, results, options);
            }

            filePos += read;
            progress?.Report(Math.Clamp((double)filePos / length, 0, 1));
        }

        FlushAscii(results, asciiRun, asciiRunStart, asciiHasHighByte, options);
        u16le.Flush(results, options);
        u16be.Flush(results, options);

        progress?.Report(1d);

        IEnumerable<ScannedString> q = results;
        if (!string.IsNullOrEmpty(options.Keyword))
        {
            q = q.Where(s => s.Value.Contains(options.Keyword!, StringComparison.OrdinalIgnoreCase));
        }
        return q.OrderBy(s => s.Offset).Take(options.MaxResults).ToList();
    }

    private static bool IsAsciiPrintable(byte b) => b == 0x09 || (b >= 0x20 && b <= 0x7E);

    private static void FlushAscii(
        List<ScannedString> results, List<byte> run, long start, bool hasHighByte, StringScanOptions options)
    {
        if (run.Count < options.MinLength) return;
        string value;
        StringEncodingKind kind;
        if (hasHighByte)
        {
            try { value = Encoding.UTF8.GetString(run.ToArray()); kind = StringEncodingKind.Utf8; }
            catch { value = Encoding.ASCII.GetString(run.ToArray()); kind = StringEncodingKind.Ascii; }
        }
        else
        {
            value = Encoding.ASCII.GetString(run.ToArray());
            kind = StringEncodingKind.Ascii;
        }

        if (options.PrintableOnly && !IsMostlyPrintable(value)) return;
        if (value.Trim().Length < options.MinLength) return;

        results.Add(new ScannedString { Offset = start, Encoding = kind, Value = value });
    }

    private static bool IsMostlyPrintable(string value)
    {
        if (value.Length == 0) return false;
        int printable = value.Count(c => !char.IsControl(c) || c == '\t');
        return printable >= value.Length * 0.95;
    }

    /// <summary>Tracks an in-progress UTF-16 run for a given endianness.</summary>
    private sealed class Utf16RunState
    {
        private readonly bool _le;
        private readonly List<char> _chars = new();
        private long _start = -1;
        private int _pending = -1;       // first byte of the current 2-byte unit
        private long _pendingPos = -1;

        public Utf16RunState(bool littleEndian) => _le = littleEndian;

        public void Push(byte b, long pos, List<ScannedString> results, StringScanOptions options)
        {
            if (_pending < 0)
            {
                _pending = b;
                _pendingPos = pos;
                return;
            }

            byte lo = _le ? (byte)_pending : b;
            byte hi = _le ? b : (byte)_pending;
            _pending = -1;

            char c = (char)(lo | (hi << 8));
            if (IsAsciiPrintable(c))
            {
                if (_chars.Count == 0) _start = _pendingPos;
                _chars.Add(c);
            }
            else
            {
                Flush(results, options);
            }
        }

        public void Flush(List<ScannedString> results, StringScanOptions options)
        {
            if (_chars.Count >= options.MinLength)
            {
                string value = new string(_chars.ToArray());
                if (!(options.PrintableOnly && !IsMostlyPrintable(value)))
                {
                    results.Add(new ScannedString
                    {
                        Offset = _start,
                        Encoding = _le ? StringEncodingKind.Utf16LE : StringEncodingKind.Utf16BE,
                        Value = value
                    });
                }
            }
            _chars.Clear();
            _start = -1;
        }

        private static bool IsAsciiPrintable(char c) => c == '\t' || (c >= ' ' && c <= '~');
    }
}
