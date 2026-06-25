using JmdExplorer.Core.Abstractions;

namespace JmdExplorer.Infrastructure.Logging;

/// <summary>
/// Writes to logs/app-yyyyMMdd.log. Thread-safe and guaranteed never to throw — a
/// logger that crashes the app would defeat its purpose.
/// </summary>
public sealed class FileAppLogger : IAppLogger
{
    private readonly object _gate = new();
    private readonly string _logDirectory;

    public FileAppLogger(string? logDirectory = null)
    {
        _logDirectory = logDirectory ?? Path.Combine(AppContext.BaseDirectory, "logs");
        try { Directory.CreateDirectory(_logDirectory); } catch { /* ignore */ }
    }

    public void Info(string message) => Write("INFO", message, null);
    public void Warn(string message) => Write("WARN", message, null);
    public void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private void Write(string level, string message, Exception? ex)
    {
        try
        {
            string file = Path.Combine(_logDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            if (ex is not null) line += Environment.NewLine + ex;
            lock (_gate)
            {
                File.AppendAllText(file, line + Environment.NewLine);
            }
        }
        catch
        {
            // Never let logging failures bubble up.
        }
    }
}
