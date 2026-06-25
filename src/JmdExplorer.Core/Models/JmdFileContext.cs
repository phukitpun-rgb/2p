namespace JmdExplorer.Core.Models;

/// <summary>
/// Lightweight, stream-oriented handle to a loaded file. It deliberately does NOT
/// hold the whole file in memory. Only a small header buffer is cached eagerly;
/// everything else is read on demand through <see cref="OpenStream"/>.
/// </summary>
public sealed class JmdFileContext
{
    private readonly Func<Stream> _streamFactory;

    public JmdFileContext(
        string filePath,
        long length,
        byte[] headerBuffer,
        Func<Stream> streamFactory,
        DateTime createdUtc,
        DateTime modifiedUtc)
    {
        FilePath = filePath;
        Length = length;
        HeaderBuffer = headerBuffer;
        _streamFactory = streamFactory;
        CreatedUtc = createdUtc;
        ModifiedUtc = modifiedUtc;
    }

    public string FilePath { get; }
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public long Length { get; }

    /// <summary>First N bytes of the file (default 4 KiB), cached for fast header probes.</summary>
    public byte[] HeaderBuffer { get; }

    public DateTime CreatedUtc { get; }
    public DateTime ModifiedUtc { get; }

    /// <summary>SHA-256 hex string; populated by the loader. May be null until computed.</summary>
    public string? Sha256 { get; set; }

    /// <summary>The format analysis result; populated after header analysis. May be null.</summary>
    public FormatAnalysisResult? Format { get; set; }

    /// <summary>Opens a fresh read-only stream positioned at 0. Caller must dispose.</summary>
    public Stream OpenStream() => _streamFactory();
}
