using ModelBuddy.Models;

namespace ModelBuddy.Services;

/// <summary>
/// Interface for interacting with Microsoft Foundry Local.
/// </summary>
public interface IFoundryService
{
    /// <summary>
    /// Gets a value indicating whether the Foundry Local service is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets a value indicating whether sample data is being used (for UI development).
    /// </summary>
    bool UsingSampleData { get; }

    /// <summary>
    /// Gets the Foundry Local endpoint URI.
    /// </summary>
    Uri? Endpoint { get; }

    /// <summary>
    /// Initializes the connection to Foundry Local.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True if the connection was successful; otherwise, false.</returns>
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconnects to Foundry Local, clearing any cached endpoint.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True if the reconnection was successful; otherwise, false.</returns>
    Task<bool> ReconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available models from the Foundry Local catalog.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A list of available models.</returns>
    Task<IReadOnlyList<LocalModel>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all cached (downloaded) models.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A list of cached models.</returns>
    Task<IReadOnlyList<LocalModel>> GetCachedModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all currently loaded models.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A list of loaded models.</returns>
    Task<IReadOnlyList<LocalModel>> GetLoadedModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a model by its alias or ID.
    /// </summary>
    /// <param name="aliasOrModelId">The model alias or ID.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the download operation.</returns>
    Task DownloadModelAsync(string aliasOrModelId, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a model into memory for inference.
    /// </summary>
    /// <param name="modelId">The model ID to load.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the load operation.</returns>
    Task LoadModelAsync(string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads a model from memory.
    /// </summary>
    /// <param name="modelId">The model ID to unload.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the unload operation.</returns>
    Task UnloadModelAsync(string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a downloaded model from the cache.
    /// </summary>
    /// <param name="modelId">The model ID to delete.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the delete operation.</returns>
    Task DeleteModelAsync(string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a chat completion request and streams the response.
    /// </summary>
    /// <param name="modelId">The model ID to use for completion.</param>
    /// <param name="messages">The chat messages.</param>
    /// <param name="systemPrompt">The system prompt.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An async enumerable of response chunks.</returns>
    IAsyncEnumerable<string> ChatCompletionStreamAsync(
        string modelId,
        IReadOnlyList<ChatMessage> messages,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total system RAM in bytes.
    /// </summary>
    /// <returns>The total system RAM in bytes.</returns>
    long GetTotalSystemRam();
}
