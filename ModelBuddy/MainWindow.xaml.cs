using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModelBuddy.Services;
using ModelBuddy.ViewModels;
using ModelBuddy.Views;

namespace ModelBuddy;

/// <summary>
/// The main application window with navigation.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly CancellationTokenSource _windowClosingCts = new();
    private bool _isClosing;

    /// <summary>
    /// Gets the application-wide ViewModel.
    /// </summary>
    public AppViewModel ViewModel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        var app = (Application.Current as App)!;
        ViewModel = app.Services.GetRequiredService<AppViewModel>();

        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Apply saved theme
        var settingsService = app.Services.GetRequiredService<ISettingsService>();
        app.ApplyTheme(settingsService.AppTheme);

        // Handle window close to clean up resources
        Closed += MainWindow_Closed;

        // Navigate to Models page by default
        ContentFrame.Navigate(typeof(ModelsPage));
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _isClosing = true;

        // Cancel any pending async operations
        _windowClosingCts.Cancel();
        _windowClosingCts.Dispose();

        // Deactivate ViewModels to unsubscribe from messenger
        try
        {
            var services = (Application.Current as App)?.Services;
            if (services is null) return;

            var modelsVm = services.GetService<ModelsViewModel>();
            if (modelsVm is not null)
                modelsVm.IsActive = false;

            var chatVm = services.GetService<ChatViewModel>();
            if (chatVm is not null)
                chatVm.IsActive = false;
        }
        catch
        {
            // Ignore errors during shutdown
        }
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isClosing) return;

        try
        {
            // Connect to Foundry Local when the window is loaded
            await ViewModel.ConnectCommand.ExecuteAsync(null);
        }
        catch (OperationCanceledException)
        {
            // Window closed during connection
        }
        catch
        {
            // Ignore errors if window is closing
            if (_isClosing) return;
            throw;
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem selectedItem)
        {
            var tag = selectedItem.Tag?.ToString();
            switch (tag)
            {
                case "Chat":
                    ContentFrame.Navigate(typeof(ChatPage));
                    break;
                case "Models":
                    ContentFrame.Navigate(typeof(ModelsPage));
                    break;
                case "Logs":
                    ContentFrame.Navigate(typeof(LogsPage));
                    break;
            }
        }
    }
}
