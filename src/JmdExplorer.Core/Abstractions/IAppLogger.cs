namespace JmdExplorer.Core.Abstractions;

/// <summary>Minimal logging abstraction. Implementations must never throw.</summary>
public interface IAppLogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}
