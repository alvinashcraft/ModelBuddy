using System.Collections.Concurrent;
using ModelBuddy.Models;

namespace ModelBuddy.Services;

/// <summary>
/// Interface for the in-memory log store.
/// </summary>
public interface ILogStore
{
    /// <summary>
    /// Adds a log entry to the store.
    /// </summary>
    /// <param name="entry">The log entry to add.</param>
    void Add(LogEntry entry);

    /// <summary>
    /// Gets all log entries.
    /// </summary>
    /// <returns>A read-only list of log entries.</returns>
    IReadOnlyList<LogEntry> GetAll();

    /// <summary>
    /// Gets log entries filtered by level.
    /// </summary>
    /// <param name="minLevel">The minimum log level to include.</param>
    /// <returns>A read-only list of filtered log entries.</returns>
    IReadOnlyList<LogEntry> GetByLevel(Models.LogLevel minLevel);

    /// <summary>
    /// Clears all log entries.
    /// </summary>
    void Clear();

    /// <summary>
    /// Event raised when a new log entry is added.
    /// </summary>
    event EventHandler<LogEntry>? LogAdded;
}

/// <summary>
/// In-memory ring buffer log store for capturing application logs.
/// </summary>
public class InMemoryLogStore : ILogStore
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly int _maxEntries;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryLogStore"/> class.
    /// </summary>
    /// <param name="maxEntries">Maximum number of entries to retain.</param>
    public InMemoryLogStore(int maxEntries = 1000)
    {
        _maxEntries = maxEntries;
    }

    /// <inheritdoc />
    public event EventHandler<LogEntry>? LogAdded;

    /// <inheritdoc />
    public void Add(LogEntry entry)
    {
        _entries.Enqueue(entry);

        // Trim if over capacity
        while (_entries.Count > _maxEntries && _entries.TryDequeue(out _))
        {
        }

        LogAdded?.Invoke(this, entry);
    }

    /// <inheritdoc />
    public IReadOnlyList<LogEntry> GetAll()
    {
        return _entries.ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<LogEntry> GetByLevel(Models.LogLevel minLevel)
    {
        return _entries.Where(e => e.Level >= minLevel).ToList();
    }

    /// <inheritdoc />
    public void Clear()
    {
        while (_entries.TryDequeue(out _))
        {
        }
    }
}
