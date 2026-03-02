# Copilot Instructions for ModelBuddy

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

## Foundry Local Integration

This app manages on-device AI models via [Foundry Local](https://github.com/microsoft/Foundry-Local).

### Prerequisites

```powershell
# Install the Foundry Local runtime
winget install Microsoft.FoundryLocal

# Verify installation
foundry --version
```

### NuGet Packages

```powershell
dotnet add package Microsoft.AI.Foundry.Local
dotnet add package OpenAI    # Foundry Local exposes an OpenAI-compatible API
```

### C# SDK Usage

```csharp
using Microsoft.AI.Foundry.Local;
using OpenAI;
using OpenAI.Chat;

// Bootstrap the service and load a model by alias
var manager = await FoundryLocalManager.StartModelAsync(aliasOrModelId: "phi-3.5-mini");

// Get model metadata
var model = await manager.GetModelInfoAsync(aliasOrModelId: "phi-3.5-mini");

// Create an OpenAI-compatible client pointing at the local endpoint
var key = new ApiKeyCredential(manager.ApiKey);
var client = new OpenAIClient(key, new OpenAIClientOptions { Endpoint = manager.Endpoint });
var chatClient = client.GetChatClient(model.ModelId);
```

Key SDK patterns:
- **Never hardcode the endpoint port** — always read `manager.Endpoint` at runtime.
- The SDK auto-discovers the Foundry Local installation, starts the service, and selects the best hardware variant (CPU/GPU/NPU).
- Use `manager.ListLoadedModelsAsync()` to enumerate active models and `manager.ListCachedModelsAsync()` for downloaded ones.

### Foundry Local CLI (useful for debugging)

```powershell
foundry model list                  # Browse available models
foundry model download <alias>      # Download without running
foundry model run <alias>           # Download, load, and start interactive chat
foundry model info <alias>          # Show model details (size, license, variants)
foundry service status              # Check if the local inference service is running
foundry cache list                  # List cached model files
```

## Key Conventions

- **Nullable reference types** are enabled (`<Nullable>enable</Nullable>`).
- **Target framework**: `net8.0-windows10.0.19041.0` with minimum version `10.0.17763.0`.
- **Namespace**: all code lives under the `ModelBuddy` namespace.
- **XAML pattern**: code-behind files (`.xaml.cs`) accompany each XAML view. Views are partial classes calling `InitializeComponent()`.
- When adding new windows or pages, follow the existing pattern: XAML file + code-behind partial class in the `ModelBuddy` namespace.
- When adding NuGet packages, keep the `<PackageReference>` items grouped in the `.csproj`.
