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
    private Window? _window;

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
        _window = new MainWindow();
        _window.Activate();
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
        services.AddSingleton<IDispatcherService>(new DispatcherService(dispatcherQueue));
        services.AddSingleton<ILogStore, InMemoryLogStore>();
        services.AddSingleton<IFoundryService, FoundryService>();

        // Add logging to in-memory store
        services.AddLogging(builder =>
        {
            var logStore = services.BuildServiceProvider().GetRequiredService<ILogStore>();
            builder.AddLogStore(logStore);
        });

        // ViewModels
        services.AddTransient<ModelsViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<LogsViewModel>();

        return services.BuildServiceProvider();
    }
}
