namespace JmdExplorer.Infrastructure.Files;

public enum FileLoadError
{
    NotFound,
    AccessDenied,
    Locked,
    TooLarge,
    Unreadable,
    Unknown
}

/// <summary>A friendly, classified failure when loading a file for inspection.</summary>
public sealed class FileLoadException : Exception
{
    public FileLoadError Error { get; }

    public FileLoadException(FileLoadError error, string message, Exception? inner = null)
        : base(message, inner)
    {
        Error = error;
    }
}
