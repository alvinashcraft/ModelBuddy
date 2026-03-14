using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using ModelBuddy.Services;
using ModelBuddy.ViewModels;

namespace ModelBuddy;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Gets the main application window.
    /// </summary>
    public Window? MainWindow { get; private set; }

    /// <summary>
    /// Gets the current application's service provider.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        // Capture the UI thread's DispatcherQueue before configuring services
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        Services = ConfigureServices(dispatcherQueue);
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    /// <summary>
    /// Applies the specified theme to the main window.
    /// </summary>
    /// <param name="theme">The theme name: "Auto", "Light", or "Dark".</param>
    public void ApplyTheme(string theme)
    {
        if (MainWindow?.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    private static IServiceProvider ConfigureServices(DispatcherQueue dispatcherQueue)
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
        });

        // Messenger
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        // Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDispatcherService>(new DispatcherService(dispatcherQueue));
        services.AddSingleton<ILogStore, InMemoryLogStore>();
        services.AddSingleton<IFoundryService, FoundryService>();

        // External log readers
        services.AddSingleton<WindowsEventLogReader>();
        services.AddSingleton<FoundryLogReader>();

        // Add logging to in-memory store
        services.AddLogging(builder =>
        {
            var logStore = services.BuildServiceProvider().GetRequiredService<ILogStore>();
            builder.AddLogStore(logStore);
        });

        // ViewModels
        services.AddSingleton<AppViewModel>(); // Singleton for shared app state
        services.AddTransient<ModelsViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }
}
