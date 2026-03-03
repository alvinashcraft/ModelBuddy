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
    private readonly IDispatcherService _dispatcherService;
    private readonly WindowsEventLogReader _windowsEventLogReader;
    private readonly FoundryLogReader _foundryLogReader;
    private IReadOnlyList<LogEntry> _allLogs = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="LogsViewModel"/> class.
    /// </summary>
    /// <param name="logStore">The log store service.</param>
    /// <param name="dispatcherService">The dispatcher service for UI thread access.</param>
    /// <param name="windowsEventLogReader">The Windows Event Log reader.</param>
    /// <param name="foundryLogReader">The Foundry Local log reader.</param>
    public LogsViewModel(
        ILogStore logStore, 
        IDispatcherService dispatcherService,
        WindowsEventLogReader windowsEventLogReader,
        FoundryLogReader foundryLogReader)
    {
        _logStore = logStore;
        _dispatcherService = dispatcherService;
        _windowsEventLogReader = windowsEventLogReader;
        _foundryLogReader = foundryLogReader;
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
    /// Gets or sets the selected source type filter.
    /// </summary>
    [ObservableProperty]
    private LogSourceType _selectedSourceType = LogSourceType.All;

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
    /// Gets or sets whether external logs are being loaded.
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingExternalLogs;

    /// <summary>
    /// Gets the available log levels for filtering.
    /// </summary>
    public LogLevel[] LogLevels { get; } = Enum.GetValues<LogLevel>();

    /// <summary>
    /// Gets the available log source types for filtering.
    /// </summary>
    public LogSourceType[] SourceTypes { get; } = Enum.GetValues<LogSourceType>();

    partial void OnSearchTextChanged(string? value)
    {
        FilterLogs();
    }

    partial void OnSelectedLogLevelChanged(LogLevel value)
    {
        FilterLogs();
    }

    partial void OnSelectedSourceTypeChanged(LogSourceType value)
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
    /// Loads logs from all sources including external sources.
    /// </summary>
    [RelayCommand]
    private async Task LoadAllLogsAsync()
    {
        IsLoadingExternalLogs = true;

        try
        {
            // Load external logs
            var windowsLogs = await _windowsEventLogReader.ReadLogsAsync(100, DateTime.Now.AddHours(-24));
            var foundryLogs = await _foundryLogReader.ReadLogsAsync(100, DateTime.Now.AddHours(-24));

            // Add to store
            _logStore.AddRange(windowsLogs);
            _logStore.AddRange(foundryLogs);

            // Refresh view
            _allLogs = _logStore.GetAll();
            TotalLogCount = _allLogs.Count;
            FilterLogs();
        }
        finally
        {
            IsLoadingExternalLogs = false;
        }
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

        // Filter by source type
        if (SelectedSourceType != LogSourceType.All)
        {
            filtered = filtered.Where(e => e.SourceType == SelectedSourceType);
        }

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
        _dispatcherService.TryEnqueue(() =>
        {
            _allLogs = _logStore.GetAll();
            TotalLogCount = _allLogs.Count;

            // Check if entry matches current filters
            var matchesLevel = entry.Level >= SelectedLogLevel;
            var matchesSource = SelectedSourceType == LogSourceType.All || entry.SourceType == SelectedSourceType;
            var matchesSearch = string.IsNullOrWhiteSpace(SearchText) ||
                entry.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                entry.Source.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

            if (matchesLevel && matchesSource && matchesSearch)
            {
                Logs.Insert(0, entry);
            }
        });
    }
}
