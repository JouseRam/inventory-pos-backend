using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace InventoryPOS.Desktop;

/// <summary>Daily log file (warnings and errors) in the data folder, for support when something fails at the register.</summary>
public sealed class FileLoggerProvider(string folder, int keepDays = 14) : ILoggerProvider
{
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new FileLogger(this, name));

    public void Dispose() { }

    internal void Write(string category, LogLevel level, string message, Exception? exception)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {category}: {message}{(exception == null ? "" : Environment.NewLine + exception)}{Environment.NewLine}";
        lock (_lock)
        {
            try { File.AppendAllText(Path.Combine(folder, $"pos-{DateTime.Now:yyyyMMdd}.log"), line); }
            catch (IOException) { /* never let logging break a sale */ }
        }
    }

    public void PruneOldFiles()
    {
        foreach (var file in new DirectoryInfo(folder).GetFiles("pos-*.log").Where(f => f.LastWriteTime < DateTime.Now.AddDays(-keepDays)))
        {
            try { file.Delete(); } catch (IOException) { }
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel)) provider.Write(category, logLevel, formatter(state, exception), exception);
        }
    }
}
