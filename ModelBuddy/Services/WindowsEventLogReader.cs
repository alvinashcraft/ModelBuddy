using System.Diagnostics.Eventing.Reader;
using ModelBuddy.Models;

namespace ModelBuddy.Services;

/// <summary>
/// Interface for reading logs from external sources.
/// </summary>
public interface IExternalLogReader
{
    /// <summary>
    /// Reads recent log entries from the external source.
    /// </summary>
    /// <param name="maxEntries">Maximum number of entries to read.</param>
    /// <param name="since">Only read entries after this time (optional).</param>
    /// <returns>A collection of log entries.</returns>
    Task<IReadOnlyList<LogEntry>> ReadLogsAsync(int maxEntries = 100, DateTime? since = null);
}

/// <summary>
/// Reads logs from Windows Event Log.
/// </summary>
public class WindowsEventLogReader : IExternalLogReader
{
    private readonly string[] _logNames = ["Application", "System"];
    private readonly string[] _relevantSources = ["Foundry", "ModelBuddy", "Windows AI", "ONNX", "DirectML"];

    /// <inheritdoc/>
    public Task<IReadOnlyList<LogEntry>> ReadLogsAsync(int maxEntries = 100, DateTime? since = null)
    {
        var entries = new List<LogEntry>();
        var cutoffTime = since ?? DateTime.Now.AddHours(-24);

        try
        {
            foreach (var logName in _logNames)
            {
                var query = CreateQuery(logName, cutoffTime);
                entries.AddRange(ReadFromLog(query, maxEntries - entries.Count));

                if (entries.Count >= maxEntries)
                {
                    break;
                }
            }
        }
        catch (Exception)
        {
            // Event log access may fail due to permissions - return what we have
        }

        var result = entries
            .OrderByDescending(e => e.Timestamp)
            .Take(maxEntries)
            .ToList();

        return Task.FromResult<IReadOnlyList<LogEntry>>(result);
    }

    private static string CreateQuery(string logName, DateTime cutoffTime)
    {
        var timeFilter = cutoffTime.ToUniversalTime().ToString("o");
        return $"*[System[TimeCreated[@SystemTime >= '{timeFilter}']]]";
    }

    private IEnumerable<LogEntry> ReadFromLog(string query, int maxEntries)
    {
        var entries = new List<LogEntry>();

        foreach (var logName in _logNames)
        {
            if (entries.Count >= maxEntries)
            {
                break;
            }

            try
            {
                using var eventLog = new EventLogReader(new EventLogQuery(logName, PathType.LogName, query));
                
                EventRecord? record;
                while ((record = eventLog.ReadEvent()) != null && entries.Count < maxEntries)
                {
                    using (record)
                    {
                        var source = record.ProviderName ?? "Unknown";
                        
                        // Filter to relevant sources or include errors/warnings
                        var isRelevant = _relevantSources.Any(s => 
                            source.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                            record.Level <= (byte)StandardEventLevel.Warning;

                        if (!isRelevant)
                        {
                            continue;
                        }

                        entries.Add(new LogEntry
                        {
                            Timestamp = record.TimeCreated?.ToLocalTime() ?? DateTime.Now,
                            Level = ConvertEventLevel(record.Level),
                            Source = $"{logName}/{source}",
                            Message = GetEventMessage(record),
                            SourceType = LogSourceType.WindowsEvent
                        });
                    }
                }
            }
            catch (EventLogNotFoundException)
            {
                // Log doesn't exist, skip it
            }
            catch (UnauthorizedAccessException)
            {
                // No permission to read this log
            }
        }

        return entries;
    }

    private static string GetEventMessage(EventRecord record)
    {
        try
        {
            return record.FormatDescription() ?? $"Event ID: {record.Id}";
        }
        catch
        {
            return $"Event ID: {record.Id}";
        }
    }

    private static Models.LogLevel ConvertEventLevel(byte? level)
    {
        return level switch
        {
            (byte)StandardEventLevel.Critical => Models.LogLevel.Critical,
            (byte)StandardEventLevel.Error => Models.LogLevel.Error,
            (byte)StandardEventLevel.Warning => Models.LogLevel.Warning,
            (byte)StandardEventLevel.Informational => Models.LogLevel.Information,
            (byte)StandardEventLevel.Verbose => Models.LogLevel.Debug,
            _ => Models.LogLevel.Information
        };
    }
}
