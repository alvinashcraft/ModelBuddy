# Copilot Instructions for ModelBuddy

> **Before starting any task, scan `.github/skills/` for relevant skills.** Each subfolder contains a `SKILL.md` with its description and detailed guidance — read and follow any that apply to the work at hand.

## Project Overview

ModelBuddy is a WinUI 3 desktop application targeting Windows 10/11 that uses the Windows App SDK `systemAIModels` capability to interact with on-device AI models. It is packaged as an MSIX app.

## Build Commands

```powershell
# Restore and build (default platform is x64)
dotnet build ModelBuddy\ModelBuddy.csproj -p:Platform=x64

# Build for other platforms
dotnet build ModelBuddy\ModelBuddy.csproj -p:Platform=ARM64
dotnet build ModelBuddy\ModelBuddy.csproj -p:Platform=x86
```

The solution file is `ModelBuddy.slnx` (XML-based solution format). Supported platforms: x64, x86, ARM64.

## Architecture

- **Single-project MSIX packaging** — the app project handles both build and packaging (no separate `.wapproj`).
- **WinUI 3 / Windows App SDK** — uses `Microsoft.WindowsAppSDK` and `Microsoft.UI.Xaml` (not UWP's `Windows.UI.Xaml`).
- **System AI capability** — the `Package.appxmanifest` declares `systemai:Capability Name="systemAIModels"`, which grants access to Windows on-device AI models. This requires the `systemai` XML namespace.
- **Mica backdrop** — `MainWindow` uses `<MicaBackdrop />` for the system material effect.
- **Settings service** — `ISettingsService` / `SettingsService` persists user preferences to `ApplicationData.Current.LocalSettings`. The `SettingsViewModel` binds to the Settings page UI and applies changes (e.g. theme) immediately.
- **Content safety** — `ContentSafetyConstants` is split into `DefaultInstructions` (editable personality) and `SafetyGuidelines` (immutable). `BuildSystemPrompt()` concatenates them. `ChatViewModel` always sends the combined prompt — settings only expose the instructions portion.

## Foundry Local Integration

This app manages on-device AI models via [Foundry Local](https://github.com/microsoft/Foundry-Local).

### Prerequisites

```powershell
# Install the Foundry Local runtime
winget install Microsoft.FoundryLocal

# Verify installation
foundry --version
```

### REST API Approach (Recommended)

ModelBuddy uses the Foundry Local REST API directly instead of the SDK. This approach:
- Provides full model metadata (file size, RAM requirements, device type, max tokens)
- Has no dependency on the Foundry Local SDK NuGet packages
- Is more reliable for auto-discovery via port scanning
- Mirrors the battle-tested [FoundryWebUI](https://github.com/itopstalk/FoundryWebUI) implementation

**Key REST Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/openai/status` | Health check |
| `GET` | `/foundry/list` | Full catalog with metadata (fileSizeMb, publisher, runtime, etc.) |
| `GET` | `/openai/models` | List downloaded models (string array) |
| `GET` | `/openai/loadedmodels` | List models loaded in memory (string array) |
| `POST` | `/openai/download` | Download model with progress streaming |
| `DELETE` | `/openai/models/{name}` | Remove model from cache |
| `GET` | `/openai/load/{name}` | Load model into memory |
| `POST` | `/v1/chat/completions` | Chat completion (streaming supported) |

**Auto-Discovery:**

The service scans common ports (5273, 5272, 5274, 5275, 5276) to find Foundry Local:

```csharp
foreach (var port in PortsToScan)
{
    var endpoint = $"http://localhost:{port}";
    var response = await _httpClient.GetAsync($"{endpoint}/openai/status");
    if (response.IsSuccessStatusCode)
    {
        return endpoint;
    }
}
```

**Parsing Catalog Models:**

The `/foundry/list` endpoint returns rich metadata:

```csharp
// Parse file size and estimate RAM
if (model.TryGetProperty("fileSizeMb", out var fsz))
{
    fileSizeMb = fsz.GetDouble();
    sizeBytes = (long)(fileSizeMb * 1024 * 1024);
}

// Estimated RAM: ~1.2x file size
var estimatedRamBytes = (long)(fileSizeMb * 1.2 * 1024 * 1024);

// Parse device type
if (model.TryGetProperty("runtime", out var rt) && rt.TryGetProperty("deviceType", out var dt))
{
    deviceType = dt.GetString(); // "CPU", "GPU", "NPU"
}
```

### C# SDK Usage (Alternative)

If you prefer the SDK approach, use v0.8.0+ with the object-oriented API:

```csharp
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;

// Configuration
var config = new Configuration
{
    AppName = "MyApp",
    LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information
};

// Create a logger
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
});
var logger = loggerFactory.CreateLogger<Program>();

// Initialize the singleton instance
await FoundryLocalManager.CreateAsync(config, logger);
var mgr = FoundryLocalManager.Instance;

// Ensure execution providers are downloaded (may take time on first run)
await mgr.EnsureEpsDownloadedAsync();

// Get the model catalog
var catalog = await mgr.GetCatalogAsync();

// List available models
var models = await catalog.ListModelsAsync();
foreach (var model in models)
{
    foreach (var variant in model.Variants)
    {
        Console.WriteLine($"- {variant.Alias} ({variant.Id})");
    }
}

// Get a specific model by alias
var model = await catalog.GetModelAsync("phi-3.5-mini");

// Download the model (with progress callback)
await model.DownloadAsync(progress =>
{
    Console.Write($"\rDownloading: {progress:F2}%");
});

// Load the model into memory
await model.LoadAsync();

// Get a chat client for inference
var chatClient = await model.GetChatClientAsync();

// Unload when done
await model.UnloadAsync();
```

### Key SDK Patterns (v0.8.0+)

| Operation | Code |
|-----------|------|
| Initialize manager | `await FoundryLocalManager.CreateAsync(config, logger);` |
| Get manager instance | `var mgr = FoundryLocalManager.Instance;` |
| Get catalog | `var catalog = await mgr.GetCatalogAsync();` |
| List all models | `var models = await catalog.ListModelsAsync();` |
| Get model by alias | `var model = await catalog.GetModelAsync("alias");` |
| Get variant by ID | `var variant = await catalog.GetModelVariantAsync("variant-id");` |
| List cached models | `var cached = await catalog.GetCachedModelsAsync();` |
| List loaded models | `var loaded = await catalog.GetLoadedModelsAsync();` |
| Download model | `await model.DownloadAsync(progress => { });` |
| Load model | `await model.LoadAsync();` |
| Unload model | `await model.UnloadAsync();` |
| Get chat client | `var chatClient = await model.GetChatClientAsync();` |
| Start web server | `await mgr.StartWebServerAsync();` |
| Stop web server | `await mgr.StopWebServerAsync();` |

### Model Variants

Models have multiple variants for different hardware (CPU, GPU, NPU). The SDK automatically selects the best variant, but you can override:

```csharp
// Get a specific variant
var cpuVariant = model.Variants.First(v => v.Info.Runtime?.DeviceType == DeviceType.CPU);

// Select it for use
model.SelectVariant(cpuVariant);

// Or use the variant directly
await cpuVariant.DownloadAsync();
await cpuVariant.LoadAsync();
```

### Foundry Local CLI (useful for debugging)

```powershell
foundry model list                  # Browse available models
foundry model download <alias>      # Download without running
foundry model run <alias>           # Download, load, and start interactive chat
foundry model info <alias>          # Show model details (size, license, variants)
foundry service status              # Check if the local inference service is running
foundry cache list                  # List cached model files
```

### Reference Implementation: FoundryWebUI

For feature and UI design reference, see [FoundryWebUI](https://github.com/itopstalk/FoundryWebUI) — a web-based Foundry Local manager with similar functionality. Key features to implement in ModelBuddy:

| Feature | Description |
|---------|-------------|
| **Chat Interface** | Streaming responses via SSE, message history, markdown rendering |
| **Model Management** | Browse full catalog (40+ models), download with progress tracking, remove downloaded models |
| **Sortable Model Table** | Click column headers to sort by name, status, size, RAM, device type |
| **Can Run Indicator** | Estimate RAM requirements (~1.2× file size) and show whether system can run model |
| **Connection Status** | Green/red indicator with endpoint display and reconnect button |
| **Auto-Discovery** | Detect Foundry Local endpoint via port scanning |
| **Logs Page** | View application logs with filtering and search |

## MVVM Pattern & Dependency Injection

The app uses the **MVVM Toolkit** (`CommunityToolkit.Mvvm`) with source generators and **Microsoft.Extensions.DependencyInjection** for DI.

### NuGet Packages

```powershell
dotnet add package CommunityToolkit.Mvvm
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Hosting  # if using IHost
```

### Source Generators

ViewModels must be `partial` classes extending `ObservableObject` (or `ObservableRecipient` if they receive messages). Use attributes instead of hand-written boilerplate:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class ModelListViewModel : ObservableObject
{
    // Generates public property "SearchText" with INotifyPropertyChanged
    [ObservableProperty]
    private string? _searchText;

    // Runs custom logic when the generated property changes
    partial void OnSearchTextChanged(string? value) { /* filter list, etc. */ }

    // Generates public IRelayCommand LoadModelsCommand
    [RelayCommand]
    private async Task LoadModelsAsync() { /* ... */ }

    // Notify a command to re-evaluate CanExecute when a property changes
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteModelCommand))]
    private ModelInfo? _selectedModel;

    [RelayCommand(CanExecute = nameof(CanDeleteModel))]
    private void DeleteModel() { /* ... */ }
    private bool CanDeleteModel() => SelectedModel is not null;
}
```

**Key rules:**
- Declare backing fields (e.g., `_searchText`); the source generator creates the `PascalCase` property.
- Always mark ViewModel classes `partial` — the generator emits the other half.
- Use `[RelayCommand]` on `async Task` methods to get built-in cancellation support.
- Use `[NotifyCanExecuteChangedFor]` to tie property changes to command re-evaluation.

### Messaging

Use `WeakReferenceMessenger` for loosely-coupled communication between ViewModels and for UI operations:

```csharp
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;

// Define a message
public class ModelLoadedMessage : ValueChangedMessage<string>
{
    public ModelLoadedMessage(string modelAlias) : base(modelAlias) { }
}

// Receiving ViewModel — extend ObservableRecipient and implement IRecipient<T>
public partial class StatusViewModel : ObservableRecipient, IRecipient<ModelLoadedMessage>
{
    public StatusViewModel(IMessenger messenger) : base(messenger)
    {
        IsActive = true; // auto-registers all IRecipient<T> interfaces
    }

    public void Receive(ModelLoadedMessage message) { /* update UI state */ }
}

// Sending — inject IMessenger and call Send
_messenger.Send(new ModelLoadedMessage("phi-3.5-mini"));
```

### UI Operations via Messaging

**ViewModels should never directly perform UI operations** like navigation, showing dialogs, or displaying notifications. Instead, use the messaging system to request these operations, and handle them in code-behind files where UI APIs are available.

**Common UI operation messages:**

```csharp
// Navigation request
public class NavigateToPageMessage : ValueChangedMessage<Type>
{
    public NavigateToPageMessage(Type pageType) : base(pageType) { }
}

// Dialog request with async response
public class ShowConfirmDialogMessage : AsyncRequestMessage<bool>
{
    public string Title { get; }
    public string Content { get; }
    public ShowConfirmDialogMessage(string title, string content)
    {
        Title = title;
        Content = content;
    }
}

// Notification/InfoBar message
public class ShowNotificationMessage
{
    public string Message { get; }
    public InfoBarSeverity Severity { get; }
    public ShowNotificationMessage(string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        Message = message;
        Severity = severity;
    }
}
```

**Handle in code-behind (where XamlRoot and navigation are available):**

```csharp
public sealed partial class MainWindow : Window, IRecipient<ShowConfirmDialogMessage>
{
    public MainWindow()
    {
        InitializeComponent();
        WeakReferenceMessenger.Default.Register(this);
    }

    public async void Receive(ShowConfirmDialogMessage message)
    {
        var dialog = new ContentDialog
        {
            Title = message.Title,
            Content = message.Content,
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
            XamlRoot = Content.XamlRoot
        };
        var result = await dialog.ShowAsync();
        message.Reply(result == ContentDialogResult.Primary);
    }
}
```

**Request from ViewModel:**

```csharp
// Fire-and-forget notification
_messenger.Send(new ShowNotificationMessage("Model downloaded successfully"));

// Async dialog with response
var confirmed = await _messenger.Send(new ShowConfirmDialogMessage(
    "Delete Model",
    "Are you sure you want to delete this model?"));
if (confirmed)
{
    await DeleteModelAsync();
}
```

### Dispatcher Service for UI Thread Access

When ViewModels need to update UI-bound collections from background threads (e.g., event handlers), use the `IDispatcherService` abstraction instead of directly accessing `DispatcherQueue`:

```csharp
// Interface in Services/IDispatcherService.cs
public interface IDispatcherService
{
    bool TryEnqueue(Action action);
    bool HasThreadAccess { get; }
}

// Implementation in Services/DispatcherService.cs
public sealed class DispatcherService : IDispatcherService
{
    private readonly DispatcherQueue _dispatcherQueue;

    public DispatcherService(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public bool HasThreadAccess => _dispatcherQueue.HasThreadAccess;

    public bool TryEnqueue(Action action) => _dispatcherQueue.TryEnqueue(() => action());
}
```

**Register in App.xaml.cs** (capture DispatcherQueue on UI thread):

```csharp
public App()
{
    var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    Services = ConfigureServices(dispatcherQueue);
    InitializeComponent();
}

private static IServiceProvider ConfigureServices(DispatcherQueue dispatcherQueue)
{
    var services = new ServiceCollection();
    services.AddSingleton<IDispatcherService>(new DispatcherService(dispatcherQueue));
    // ... other registrations
}
```

**Use in ViewModels:**

```csharp
public partial class LogsViewModel : ObservableObject
{
    private readonly IDispatcherService _dispatcherService;

    public LogsViewModel(ILogStore logStore, IDispatcherService dispatcherService)
    {
        _dispatcherService = dispatcherService;
        logStore.LogAdded += OnLogAdded;
    }

    private void OnLogAdded(object? sender, LogEntry entry)
    {
        _dispatcherService.TryEnqueue(() =>
        {
            Logs.Insert(0, entry);
        });
    }
}
```

This pattern:
- Keeps ViewModels testable (mock `IDispatcherService` in tests)
- Avoids direct dependency on WinUI-specific `DispatcherQueue` API
- Follows the same DI pattern used for other services

### Dependency Injection Setup

Configure the DI container in `App.xaml.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;

public partial class App : Application
{
    public IServiceProvider Services { get; }

    public App()
    {
        Services = ConfigureServices();
        InitializeComponent();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Messenger
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        // ViewModels
        services.AddTransient<ModelListViewModel>();

        // Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IModelService, FoundryModelService>();

        return services.BuildServiceProvider();
    }
}
```

Resolve ViewModels in code-behind to bind to the view:

```csharp
public sealed partial class ModelListPage : Page
{
    public ModelListViewModel ViewModel { get; }

    public ModelListPage()
    {
        ViewModel = (Application.Current as App)!.Services.GetRequiredService<ModelListViewModel>();
        InitializeComponent();
    }
}
```

### Event-to-Command Pattern

Prefer using event-to-command patterns in XAML over code-behind event handlers when possible for better MVVM practice. In WinUI 3, where EventTriggerBehavior is not readily available, use minimal code-behind handlers that simply delegate to ViewModel commands (expression-body syntax preferred).

### Project Structure Convention

```
ModelBuddy/
  ViewModels/        # ObservableObject / ObservableRecipient partial classes
  Views/             # XAML pages/windows + code-behind
  Services/          # Interfaces and implementations (e.g., IFoundryService, ISettingsService)
  Models/            # Plain data classes / DTOs
  Messages/          # Messenger message types
  Constants/         # App-wide constants (e.g., ContentSafetyConstants)
  Converters/        # IValueConverter implementations for XAML bindings
```

## UI Design

Follow **Fluent UI** design guidelines for Windows 11:

- Use **Mica** (`<MicaBackdrop />`) on main windows and **Acrylic** only on transient/light-dismiss surfaces (flyouts, context menus).
- Use the WinUI 3 control library (`Microsoft.UI.Xaml.Controls`) — prefer built-in controls (NavigationView, InfoBar, ContentDialog, TeachingTip) over custom implementations.
- **Check the Windows Community Toolkit** for additional controls not available in core WinUI 3 (e.g., `MarkdownTextBlock`, `TokenizingTextBox`, `SettingsCard`, `Segmented`, etc.). See [Windows Community Toolkit Gallery](https://apps.microsoft.com/detail/9nblggh4tlcq) and [docs](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/windows/).
- Support **light and dark mode** — use theme resources (`ApplicationTheme`) instead of hardcoded colors.
- Apply Windows 11 **rounded geometry** (default corner radius from WinUI) — don't override to sharp corners.
- Use **Segoe UI Variable** (the WinUI default) and the built-in type ramp for hierarchy.

### Windows Community Toolkit Controls

The Windows Community Toolkit provides many useful controls for WinUI 3 apps. Always check the toolkit before building custom controls:

```powershell
# Add the toolkit controls package
dotnet add package CommunityToolkit.WinUI.UI.Controls.Markdown
dotnet add package CommunityToolkit.WinUI.Controls.SettingsControls
dotnet add package CommunityToolkit.WinUI.Controls.Segmented
```

**Commonly Used Controls:**

| Control | Package | Description |
|---------|---------|-------------|
| `MarkdownTextBlock` | `CommunityToolkit.WinUI.UI.Controls.Markdown` | Renders markdown content |
| `SettingsCard` | `CommunityToolkit.WinUI.Controls.SettingsControls` | Settings page cards |
| `SettingsExpander` | `CommunityToolkit.WinUI.Controls.SettingsControls` | Expandable settings groups |
| `Segmented` | `CommunityToolkit.WinUI.Controls.Segmented` | Segmented button control |
| `TokenizingTextBox` | `CommunityToolkit.WinUI.Controls.TokenizingTextBox` | Tag/token input |

**Usage Example (MarkdownTextBlock):**

```xml
<Page
    xmlns:controls="using:CommunityToolkit.WinUI.UI.Controls">

    <controls:MarkdownTextBlock 
        Text="{x:Bind ViewModel.MarkdownContent, Mode=OneWay}"
        Background="Transparent"/>
</Page>
```

References:
- [Fluent UI](https://developer.microsoft.com/en-us/fluentui#/)
- [Windows Community Toolkit](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/windows/)
- [Windows design principles](https://learn.microsoft.com/en-us/windows/apps/design/design-principles)
- [Windows app design guidance](https://learn.microsoft.com/en-us/windows/apps/design/)
- [Materials (Mica, Acrylic, Smoke)](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/materials)

## Key Conventions

- **Nullable reference types** are enabled (`<Nullable>enable</Nullable>`).
- **Target framework**: `net8.0-windows10.0.19041.0` with minimum version `10.0.17763.0`.
- **Namespace**: all code lives under the `ModelBuddy` namespace.
- **XAML pattern**: code-behind files (`.xaml.cs`) accompany each XAML view. Views are partial classes calling `InitializeComponent()`.
- When adding new windows or pages, follow the existing pattern: XAML file + code-behind partial class in the `ModelBuddy` namespace.
- When adding NuGet packages, keep the `<PackageReference>` items grouped in the `.csproj`.
- **ViewModel naming**: `{Feature}ViewModel` in the `ViewModels/` folder; corresponding view is `{Feature}Page` or `{Feature}Window` in `Views/`.
- **Service interfaces**: define in `Services/I{Name}Service.cs`, implement in `Services/{Name}Service.cs`. Inject via constructor.
