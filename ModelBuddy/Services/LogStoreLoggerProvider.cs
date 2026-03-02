using Microsoft.Extensions.Logging;
using ModelBuddy.Models;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace ModelBuddy.Services;

/// <summary>
/// Logger provider that writes to the in-memory log store.
/// </summary>
public class LogStoreLoggerProvider : ILoggerProvider
{
    private readonly ILogStore _logStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogStoreLoggerProvider"/> class.
    /// </summary>
    /// <param name="logStore">The log store to write to.</param>
    public LogStoreLoggerProvider(ILogStore logStore)
    {
        _logStore = logStore;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return new LogStoreLogger(_logStore, categoryName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Logger that writes to the in-memory log store.
/// </summary>
public class LogStoreLogger : ILogger
{
    private readonly ILogStore _logStore;
    private readonly string _categoryName;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogStoreLogger"/> class.
    /// </summary>
    /// <param name="logStore">The log store to write to.</param>
    /// <param name="categoryName">The category name for this logger.</param>
    public LogStoreLogger(ILogStore logStore, string categoryName)
    {
        _logStore = logStore;
        _categoryName = categoryName;
    }

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Information;
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = ConvertLogLevel(logLevel),
            Source = _categoryName,
            Message = formatter(state, exception),
            Exception = exception?.ToString()
        };

        _logStore.Add(entry);
    }

    private static Models.LogLevel ConvertLogLevel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => Models.LogLevel.Trace,
            LogLevel.Debug => Models.LogLevel.Debug,
            LogLevel.Information => Models.LogLevel.Information,
            LogLevel.Warning => Models.LogLevel.Warning,
            LogLevel.Error => Models.LogLevel.Error,
            LogLevel.Critical => Models.LogLevel.Critical,
            _ => Models.LogLevel.Information
        };
    }
}

/// <summary>
/// Extension methods for adding the log store logger.
/// </summary>
public static class LogStoreLoggerExtensions
{
    /// <summary>
    /// Adds the log store logger provider to the logging builder.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="logStore">The log store to use.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddLogStore(this ILoggingBuilder builder, ILogStore logStore)
    {
        builder.AddProvider(new LogStoreLoggerProvider(logStore));
        return builder;
    }
}
