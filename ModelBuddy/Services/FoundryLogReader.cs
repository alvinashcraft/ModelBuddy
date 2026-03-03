using System.Text.Json;
using ModelBuddy.Models;

namespace ModelBuddy.Services;

/// <summary>
/// Reads logs from Foundry Local service.
/// </summary>
/// <remarks>
/// Foundry Local stores logs in the user's local app data folder.
/// This reader attempts to find and parse those log files.
/// </remarks>
public class FoundryLogReader : IExternalLogReader
{
    private static readonly string[] LogPaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Foundry", "logs"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FoundryLocal", "logs"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".foundry", "logs")
    ];

    /// <inheritdoc/>
    public Task<IReadOnlyList<LogEntry>> ReadLogsAsync(int maxEntries = 100, DateTime? since = null)
    {
        var entries = new List<LogEntry>();
        var cutoffTime = since ?? DateTime.Now.AddHours(-24);

        foreach (var logPath in LogPaths)
        {
            if (!Directory.Exists(logPath))
            {
                continue;
            }

            try
            {
                var logFiles = Directory.GetFiles(logPath, "*.log", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(logPath, "*.json", SearchOption.AllDirectories))
                    .Concat(Directory.GetFiles(logPath, "*.txt", SearchOption.AllDirectories))
                    .Where(f => File.GetLastWriteTime(f) >= cutoffTime)
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .Take(5); // Only read most recent log files

                foreach (var logFile in logFiles)
                {
                    entries.AddRange(ReadLogFile(logFile, cutoffTime, maxEntries - entries.Count));

                    if (entries.Count >= maxEntries)
                    {
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // Log directory access failed, continue to next path
            }

            if (entries.Count >= maxEntries)
            {
                break;
            }
        }

        var result = entries
            .OrderByDescending(e => e.Timestamp)
            .Take(maxEntries)
            .ToList();

        return Task.FromResult<IReadOnlyList<LogEntry>>(result);
    }

    private static IEnumerable<LogEntry> ReadLogFile(string filePath, DateTime cutoffTime, int maxEntries)
    {
        var entries = new List<LogEntry>();
        var fileName = Path.GetFileName(filePath);

        try
        {
            // Read last N lines from the file (tail)
            var lines = ReadLastLines(filePath, maxEntries * 2);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var entry = ParseLogLine(line, fileName);
                if (entry != null && entry.Timestamp >= cutoffTime)
                {
                    entries.Add(entry);
                }

                if (entries.Count >= maxEntries)
                {
                    break;
                }
            }
        }
        catch (Exception)
        {
            // File read failed, return what we have
        }

        return entries;
    }

    private static IEnumerable<string> ReadLastLines(string filePath, int lineCount)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            
            var lines = new Queue<string>();
            string? line;
            
            while ((line = reader.ReadLine()) != null)
            {
                lines.Enqueue(line);
                if (lines.Count > lineCount)
                {
                    lines.Dequeue();
                }
            }
            
            return lines.Reverse(); // Return newest first
        }
        catch
        {
            return [];
        }
    }

    private static LogEntry? ParseLogLine(string line, string fileName)
    {
        // Try JSON format first (common for structured logging)
        if (line.TrimStart().StartsWith('{'))
        {
            return ParseJsonLogLine(line, fileName);
        }

        // Try common log formats: [timestamp] [level] message
        return ParseTextLogLine(line, fileName);
    }

    private static LogEntry? ParseJsonLogLine(string line, string fileName)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            var timestamp = DateTime.Now;
            if (root.TryGetProperty("timestamp", out var ts) || 
                root.TryGetProperty("time", out ts) ||
                root.TryGetProperty("@t", out ts))
            {
                if (ts.ValueKind == JsonValueKind.String && DateTime.TryParse(ts.GetString(), out var parsed))
                {
                    timestamp = parsed;
                }
            }

            var level = Models.LogLevel.Information;
            if (root.TryGetProperty("level", out var lvl) || root.TryGetProperty("@l", out lvl))
            {
                level = ParseLogLevel(lvl.GetString());
            }

            var message = "";
            if (root.TryGetProperty("message", out var msg) || 
                root.TryGetProperty("msg", out msg) ||
                root.TryGetProperty("@m", out msg))
            {
                message = msg.GetString() ?? line;
            }
            else
            {
                message = line;
            }

            return new LogEntry
            {
                Timestamp = timestamp,
                Level = level,
                Source = $"Foundry/{fileName}",
                Message = message,
                SourceType = LogSourceType.FoundryLocal
            };
        }
        catch
        {
            return null;
        }
    }

    private static LogEntry? ParseTextLogLine(string line, string fileName)
    {
        var timestamp = DateTime.Now;
        var level = Models.LogLevel.Information;
        var message = line;

        // Try to extract timestamp from beginning of line
        // Common formats: "2024-01-15 10:30:45" or "[2024-01-15T10:30:45]"
        var trimmed = line.TrimStart('[');
        if (trimmed.Length > 19)
        {
            var dateStr = trimmed[..19].Replace('T', ' ');
            if (DateTime.TryParse(dateStr, out var parsed))
            {
                timestamp = parsed;
                message = line[(line.IndexOf(']') + 1)..].TrimStart();
            }
        }

        // Try to extract log level
        var upperLine = line.ToUpperInvariant();
        if (upperLine.Contains("ERROR") || upperLine.Contains("ERR"))
        {
            level = Models.LogLevel.Error;
        }
        else if (upperLine.Contains("WARN"))
        {
            level = Models.LogLevel.Warning;
        }
        else if (upperLine.Contains("DEBUG") || upperLine.Contains("DBG"))
        {
            level = Models.LogLevel.Debug;
        }
        else if (upperLine.Contains("TRACE") || upperLine.Contains("TRC"))
        {
            level = Models.LogLevel.Trace;
        }
        else if (upperLine.Contains("CRITICAL") || upperLine.Contains("FATAL"))
        {
            level = Models.LogLevel.Critical;
        }

        return new LogEntry
        {
            Timestamp = timestamp,
            Level = level,
            Source = $"Foundry/{fileName}",
            Message = message,
            SourceType = LogSourceType.FoundryLocal
        };
    }

    private static Models.LogLevel ParseLogLevel(string? level)
    {
        return level?.ToUpperInvariant() switch
        {
            "TRACE" or "TRC" or "VERBOSE" => Models.LogLevel.Trace,
            "DEBUG" or "DBG" => Models.LogLevel.Debug,
            "INFO" or "INFORMATION" or "INF" => Models.LogLevel.Information,
            "WARN" or "WARNING" or "WRN" => Models.LogLevel.Warning,
            "ERROR" or "ERR" or "FAIL" => Models.LogLevel.Error,
            "CRITICAL" or "FATAL" or "CRT" => Models.LogLevel.Critical,
            _ => Models.LogLevel.Information
        };
    }
}
