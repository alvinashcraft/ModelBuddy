# ModelBuddy — Microsoft Store Listing

## Short Title

ModelBuddy

## Voice Title

Model Buddy

## Short Description

Manage and chat with on-device AI models powered by Foundry Local.

## Description

ModelBuddy is a desktop companion for Windows that makes it easy to discover, download, and chat with on-device AI models through Microsoft Foundry Local — no cloud required.

Browse the full Foundry Local model catalog to see what's available, check file sizes and RAM requirements, and download models with a single click. Once a model is downloaded, start a streaming chat conversation right inside the app with full Markdown rendering.

ModelBuddy connects to the Foundry Local inference service automatically, but you can point it at a custom endpoint if needed. Built-in content safety guidelines are always applied to every conversation to help keep interactions responsible and respectful.

The Settings page lets you personalise the experience: choose a light, dark, or system-matched theme; customise the system instructions sent to the model; and override the Foundry Local endpoint. Application, Foundry Local, and Windows Event logs are available on the Logs page for troubleshooting.

ModelBuddy is free and open source, built with WinUI 3 and .NET 8.

## What's New

- Initial release
- Chat with on-device AI models using streaming responses and Markdown rendering
- Browse, download, and manage models from the Foundry Local catalog
- Settings page with theme selection, customisable system instructions, and custom endpoint
- Application and Foundry Local log viewer with filtering

## Features

- Chat with on-device AI models in real time
- Browse and download from the full model catalog
- Light, dark, and system theme with Mica backdrop
- View and filter application and Foundry Local logs

## Keywords

AI, local AI, on-device, Foundry Local, chat, LLM, model manager, WinUI, open source, privacy

## Privacy Notice

ModelBuddy does not collect, transmit, or store any personal data. All AI inference runs locally on your device through Microsoft Foundry Local. No data is sent to external servers. The app stores user preferences (theme, system instructions, custom endpoint, selected model) in local Windows application settings on your device only.

## Testing Notes for Store Reviewers

### Prerequisites

ModelBuddy requires **Microsoft Foundry Local** to be installed. Without it, the app will launch but display "Not connected" in the status bar. To install:

```
winget install Microsoft.FoundryLocal
```

After installation, run `foundry --version` to verify. The Foundry Local service starts on-demand when the app connects.

### Testing steps

1. **Launch the app.** It opens on the Models page and attempts to connect to Foundry Local automatically. If connected, a green dot and "Connected" appear in the status bar. If Foundry Local is not installed, the status bar shows "Not connected" with Reconnect and getting-started links.

2. **Models page.** Once connected, the full catalog loads. Filter by status (Available / Downloaded / Loaded) and search by name. To download a model, click the download button — this may take several minutes depending on model size and network speed. After downloading, the model can be selected for chat.

3. **Chat page.** Select a downloaded model from the dropdown and type a message. The assistant streams its response in real time with Markdown rendering. "Stop" cancels generation mid-stream. "Clear chat" resets the conversation.

4. **Settings page.** Click the gear icon. Verify:
   - **Theme** — switching between Light / Dark / System updates the UI immediately.
   - **System instructions** — edit the text and navigate to Chat. The new instructions are used for subsequent messages. The content safety guidelines shown in the expander are read-only and always applied.
   - **Foundry endpoint** — enter a custom URL. Reconnect from the status bar to apply.
   - **About** — shows version, GitHub link, and license link.

5. **Logs page.** View application logs with level (Information, Warning, Error) and source filters. External log sources (Foundry Local, Windows Events) can be loaded on demand.

### Notes

- The app requires the `systemAIModels` restricted capability and `runFullTrust`.
- A model must be downloaded before it can be used for chat. No models are bundled with the app.
- If no Foundry Local instance is available, all features except Settings and Logs are non-functional but the app remains stable and shows a helpful getting-started link.
