namespace ModelBuddy.Models;

/// <summary>
/// Represents a log entry.
/// </summary>
public class LogEntry
{
    /// <summary>
    /// Gets or sets the timestamp of the log entry.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the log level.
    /// </summary>
    public LogLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the source/category of the log.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the log message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional exception details.
    /// </summary>
    public string? Exception { get; set; }
}

/// <summary>
/// Log severity levels.
/// </summary>
public enum LogLevel
{
    /// <summary>Trace level logging.</summary>
    Trace,
    /// <summary>Debug level logging.</summary>
    Debug,
    /// <summary>Information level logging.</summary>
    Information,
    /// <summary>Warning level logging.</summary>
    Warning,
    /// <summary>Error level logging.</summary>
    Error,
    /// <summary>Critical level logging.</summary>
    Critical
}
