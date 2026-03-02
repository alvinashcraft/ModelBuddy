using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ModelBuddy.Models;
using ModelBuddy.ViewModels;

namespace ModelBuddy.Views;

/// <summary>
/// Page for viewing application and model logs.
/// </summary>
public sealed partial class LogsPage : Page
{
    /// <summary>
    /// Gets the ViewModel for this page.
    /// </summary>
    public LogsViewModel ViewModel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LogsPage"/> class.
    /// </summary>
    public LogsPage()
    {
        ViewModel = (Application.Current as App)!.Services.GetRequiredService<LogsViewModel>();
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LoadLogsCommand.Execute(null);
    }

    /// <summary>
    /// Formats a timestamp for display.
    /// </summary>
    public static string FormatTimestamp(DateTime timestamp)
    {
        return timestamp.ToString("HH:mm:ss.fff");
    }

    /// <summary>
    /// Gets a brush color based on log level.
    /// </summary>
    public static SolidColorBrush GetLevelBrush(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => new SolidColorBrush(Colors.Gray),
            LogLevel.Debug => new SolidColorBrush(Colors.SlateGray),
            LogLevel.Information => new SolidColorBrush(Colors.DodgerBlue),
            LogLevel.Warning => new SolidColorBrush(Colors.Orange),
            LogLevel.Error => new SolidColorBrush(Colors.OrangeRed),
            LogLevel.Critical => new SolidColorBrush(Colors.DarkRed),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    /// <summary>
    /// Returns Visible if count is 0, otherwise Collapsed.
    /// </summary>
    public static Visibility IsEmpty(int count)
    {
        return count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Formats the status bar text.
    /// </summary>
    public static string FormatStatus(int displayed, int total)
    {
        if (displayed == total)
        {
            return $"Showing {total} log entries";
        }
        return $"Showing {displayed} of {total} log entries (filtered)";
    }
}
