# Model Buddy Roadmap

Future work being considered for Model Buddy. Items are not in priority order. As they're picked up they should move into branches / PRs and be removed from this file.

## Migrate `FoundryService` from REST to the C# SDK

Replace the direct HTTP calls in `Services/FoundryService.cs` (`/foundry/list`, `/openai/{status,models,loadedmodels,download,load,models/{name}}`, `/v1/chat/completions`) with the object-oriented Foundry Local C# SDK:

- `Microsoft.AI.Foundry.Local` for cross-platform.
- `Microsoft.AI.Foundry.Local.WinML` on Windows for hardware acceleration via Windows ML.

**Why:**

- App becomes self-contained — users no longer need the Foundry Local CLI separately installed.
- Automatic execution provider discovery / registration, including the new on-demand WebGPU EP plug-in (v1.1).
- Native variant selection (CPU / GPU / NPU) via `model.SelectVariant`.
- Unlocks the new audio (live transcription) and embedding clients without rolling our own HTTP code.
- Removes the port-scan / `foundry service status` / `foundry service start` auto-discovery code.

**API mapping** (from the [SDK migration guide](https://learn.microsoft.com/en-us/azure/foundry-local/reference/reference-sdk-migration?pivots=programming-language-csharp)):

| Old / REST | New SDK (v1.1+) |
|---|---|
| `GET /foundry/list` | `catalog.ListModelsAsync()` |
| `GET /openai/models` | `catalog.GetCachedModelsAsync()` |
| `GET /openai/loadedmodels` | `catalog.GetLoadedModelsAsync()` |
| `POST /openai/download` | `model.DownloadAsync(progress)` |
| `GET /openai/load/{name}` | `model.LoadAsync()` |
| (n/a) | `model.UnloadAsync()` |
| `DELETE /openai/models/{name}` / `foundry cache remove` | model-level API (keep CLI as fallback if needed) |
| `POST /v1/chat/completions` | `await model.GetChatClientAsync()` |
| Manager bootstrap | `await FoundryLocalManager.CreateAsync(config, logger); var mgr = FoundryLocalManager.Instance;` |
| Cache location | `Configuration.ModelCacheDir` |
| Start / stop optional REST server | `mgr.StartWebServerAsync()` / `mgr.StopWebServerAsync()` |

**Implementation notes:**

- Keep the `IFoundryService` interface stable so `ChatViewModel` / `ModelsViewModel` don't need to change.
- Verify the MSIX package still builds with the SDK NuGet bundled and that the `systemAIModels` capability is still required (or no longer needed).
- Test on x64 and ARM64.
- Update `.github/copilot-instructions.md` to flip the recommendation from "REST first" to "SDK first" once migrated.

## Vision support in Chat (Qwen 3.5 VL)

Foundry Local v1.1 introduced the **Qwen 3.5 VL** multimodal model. The category filter already lets vision models flow through `IsChatCapable`, but the chat UI is text-only.

- Add an image-attach button on `ChatPage` enabled only when `SelectedModel.Category == ModelCategory.Vision`.
- Submit images via multipart `/v1/chat/completions` content (or the new Responses API — see below).
- Render attached images inline in the conversation history.
- Best done together with the SDK migration since the SDK has first-class multimodal types.

## Embeddings playground

Foundry Local v1.1 ships `qwen3-0.6b-embedding` and an OpenAI-compatible `/v1/embeddings` endpoint (or `model.GetEmbeddingClientAsync()` via the SDK).

- New page that filters models to `ModelCategory.Embeddings`.
- Lets the user enter one or more strings, generates vectors, displays dimensions, and shows a basic pairwise cosine similarity matrix.
- Foundation for any future RAG / semantic-search feature.

## Live transcription page (Nemotron ASR)

Foundry Local v1.1 added the Live Transcription API for real-time speech-to-text. The streaming session API is exposed via the SDK (`audioClient.CreateLiveTranscriptionSession()`) and is **not** plain REST, so this depends on the SDK migration above.

- Capture microphone audio at 16 kHz / mono PCM (WASAPI via `Windows.Media.Capture` or NAudio).
- Feed chunks via `session.AppendAsync(pcm)`.
- Stream interim / final transcripts into the UI, distinguishing `IsFinal` results.
- Filter the model picker to `ModelCategory.Transcription`.

## Migrate Chat to the OpenAI Responses API

Foundry Local v1.1 exposes the OpenAI Responses API (`/v1/responses`) supporting tool calling, structured outputs, and multimodal vision-language input.

- Evaluate moving `ChatViewModel` from `/v1/chat/completions` to `/v1/responses` (or the SDK-equivalent client).
- Streaming, cancellation, and message-history shape will need rework.
- Coordinate with the vision-attachment work so multimodal input flows through a single path.
- Future-proofs the app for tool calling.
