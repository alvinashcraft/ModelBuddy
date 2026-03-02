using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModelBuddy.Models;
using ModelBuddy.Services;

namespace ModelBuddy.ViewModels;

/// <summary>
/// ViewModel for the Logs page.
/// </summary>
public partial class LogsViewModel : ObservableObject
{
    private readonly ILogStore _logStore;
    private IReadOnlyList<LogEntry> _allLogs = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="LogsViewModel"/> class.
    /// </summary>
    /// <param name="logStore">The log store service.</param>
    public LogsViewModel(ILogStore logStore)
    {
        _logStore = logStore;
        _logStore.LogAdded += OnLogAdded;
    }

    /// <summary>
    /// Gets the collection of log entries to display.
    /// </summary>
    public ObservableCollection<LogEntry> Logs { get; } = [];

    /// <summary>
    /// Gets or sets the search/filter text.
    /// </summary>
    [ObservableProperty]
    private string? _searchText;

    /// <summary>
    /// Gets or sets the selected minimum log level filter.
    /// </summary>
    [ObservableProperty]
    private LogLevel _selectedLogLevel = LogLevel.Information;

    /// <summary>
    /// Gets or sets whether auto-scroll is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _autoScroll = true;

    /// <summary>
    /// Gets or sets the total log count.
    /// </summary>
    [ObservableProperty]
    private int _totalLogCount;

    /// <summary>
    /// Gets the available log levels for filtering.
    /// </summary>
    public LogLevel[] LogLevels { get; } = Enum.GetValues<LogLevel>();


    partial void OnSearchTextChanged(string? value)
    {
        FilterLogs();
    }

    partial void OnSelectedLogLevelChanged(LogLevel value)
    {
        FilterLogs();
    }

    /// <summary>
    /// Loads the logs from the store.
    /// </summary>
    [RelayCommand]
    private void LoadLogs()
    {
        _allLogs = _logStore.GetAll();
        TotalLogCount = _allLogs.Count;
        FilterLogs();
    }

    /// <summary>
    /// Clears all logs.
    /// </summary>
    [RelayCommand]
    private void ClearLogs()
    {
        _logStore.Clear();
        _allLogs = [];
        Logs.Clear();
        TotalLogCount = 0;
    }

    /// <summary>
    /// Refreshes the logs from the store.
    /// </summary>
    [RelayCommand]
    private void RefreshLogs()
    {
        LoadLogs();
    }

    private void FilterLogs()
    {
        Logs.Clear();

        var filtered = _allLogs.Where(e => e.Level >= SelectedLogLevel);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(e =>
                e.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                e.Source.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (e.Exception?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        foreach (var entry in filtered.OrderByDescending(e => e.Timestamp))
        {
            Logs.Add(entry);
        }
    }

    private void OnLogAdded(object? sender, LogEntry entry)
    {
        // Update on UI thread
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
        {
            _allLogs = _logStore.GetAll();
            TotalLogCount = _allLogs.Count;

            if (entry.Level >= SelectedLogLevel)
            {
                var matchesSearch = string.IsNullOrWhiteSpace(SearchText) ||
                    entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    entry.Source.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

                if (matchesSearch)
                {
                    Logs.Insert(0, entry);
                }
            }
        });
    }
}
