using System.Security.Cryptography;
using JmdExplorer.Core.Abstractions;
using JmdExplorer.Core.Models;

namespace JmdExplorer.Infrastructure.Files;

public sealed class FileServiceOptions
{
    /// <summary>Bytes of the header to cache eagerly. Default 4 KiB.</summary>
    public int HeaderBufferSize { get; init; } = 4096;

    /// <summary>Reject files larger than this (bytes). Default 4 GiB. Set 0 to disable.</summary>
    public long MaxFileSize { get; init; } = 4L * 1024 * 1024 * 1024;
}

/// <summary>
/// Opens files for inspection with friendly error classification and streamed access.
/// It never reads the whole file into memory; only a small header buffer is cached.
/// </summary>
public sealed class FileService
{
    private readonly FileServiceOptions _options;
    private readonly IAppLogger _logger;

    public FileService(IAppLogger logger, FileServiceOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new FileServiceOptions();
    }

    /// <summary>Loads a file context. Throws <see cref="FileLoadException"/> on failure.</summary>
    public JmdFileContext Load(string path)
    {
        FileInfo info;
        try
        {
            info = new FileInfo(path);
        }
        catch (Exception ex)
        {
            throw new FileLoadException(FileLoadError.Unknown, $"Could not read path: {ex.Message}", ex);
        }

        if (!info.Exists)
            throw new FileLoadException(FileLoadError.NotFound, $"File not found: {path}");

        if (_options.MaxFileSize > 0 && info.Length > _options.MaxFileSize)
            throw new FileLoadException(FileLoadError.TooLarge,
                $"File is too large ({info.Length:N0} bytes). Limit is {_options.MaxFileSize:N0} bytes.");

        byte[] headerBuffer;
        try
        {
            using var probe = OpenRead(path);
            int toRead = (int)Math.Min(_options.HeaderBufferSize, info.Length);
            headerBuffer = new byte[toRead];
            int total = 0;
            while (total < toRead)
            {
                int read = probe.Read(headerBuffer, total, toRead - total);
                if (read <= 0) break;
                total += read;
            }
            if (total != toRead) Array.Resize(ref headerBuffer, total);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new FileLoadException(FileLoadError.AccessDenied, $"Access denied: {path}", ex);
        }
        catch (IOException ex) when (IsSharingViolation(ex))
        {
            throw new FileLoadException(FileLoadError.Locked, $"File is locked by another process: {path}", ex);
        }
        catch (Exception ex)
        {
            throw new FileLoadException(FileLoadError.Unreadable, $"Could not read file: {ex.Message}", ex);
        }

        _logger.Info($"Loaded file '{path}' ({info.Length:N0} bytes).");

        return new JmdFileContext(
            filePath: info.FullName,
            length: info.Length,
            headerBuffer: headerBuffer,
            streamFactory: () => OpenRead(info.FullName),
            createdUtc: info.CreationTimeUtc,
            modifiedUtc: info.LastWriteTimeUtc);
    }

    /// <summary>Reads <paramref name="count"/> bytes at <paramref name="offset"/> for the hex viewer.</summary>
    public byte[] ReadRange(JmdFileContext context, long offset, int count)
    {
        if (offset < 0) offset = 0;
        if (offset >= context.Length) return Array.Empty<byte>();
        int toRead = (int)Math.Min(count, context.Length - offset);
        var buffer = new byte[toRead];
        using var stream = context.OpenStream();
        stream.Seek(offset, SeekOrigin.Begin);
        int total = 0;
        while (total < toRead)
        {
            int read = stream.Read(buffer, total, toRead - total);
            if (read <= 0) break;
            total += read;
        }
        if (total != toRead) Array.Resize(ref buffer, total);
        return buffer;
    }

    /// <summary>Computes the SHA-256 of the file by streaming it.</summary>
    public string ComputeSha256(JmdFileContext context, CancellationToken ct = default)
    {
        using var stream = context.OpenStream();
        using var sha = SHA256.Create();
        byte[] buffer = new byte[1024 * 1024];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            sha.TransformBlock(buffer, 0, read, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());
    }

    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 64, FileOptions.SequentialScan);

    private static bool IsSharingViolation(IOException ex)
    {
        // HResult 0x80070020 = ERROR_SHARING_VIOLATION, 0x80070021 = ERROR_LOCK_VIOLATION.
        int code = ex.HResult & 0xFFFF;
        return code is 32 or 33;
    }
}
