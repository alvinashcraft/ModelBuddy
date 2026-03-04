using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ModelBuddy.Models;

namespace ModelBuddy.Services;

/// <summary>
/// Implementation of <see cref="IFoundryService"/> that interacts with Microsoft Foundry Local
/// using the REST API directly (similar to FoundryWebUI approach).
/// </summary>
public partial class FoundryService : IFoundryService
{
    private const string DefaultEndpoint = "http://127.0.0.1:5272";
    private readonly HttpClient _httpClient;
    private readonly ILogger<FoundryService>? _logger;
    private string? _cachedEndpoint;
    private List<JsonElement>? _catalogCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="FoundryService"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public FoundryService(ILogger<FoundryService>? logger = null)
    {
        _logger = logger;
        // Disable proxy to ensure localhost requests aren't intercepted
        var handler = new HttpClientHandler { UseProxy = false };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <inheritdoc />
    public bool IsConnected => _cachedEndpoint is not null;

    /// <inheritdoc />
    public Uri? Endpoint => _cachedEndpoint is not null ? new Uri(_cachedEndpoint) : null;

    /// <inheritdoc />
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Detect the endpoint from 'foundry service status' (or use default)
            var endpoint = await DetectEndpointAsync();
            Debug.WriteLine($"FoundryService: detected endpoint {endpoint}");

            // Verify the service is actually running
            if (await IsServiceRunningAsync(endpoint, cancellationToken))
            {
                _cachedEndpoint = endpoint;
                _logger?.LogInformation("Connected to Foundry Local at {Endpoint}", endpoint);
                Debug.WriteLine($"FoundryService: connected to {endpoint}");
                return true;
            }

            _logger?.LogWarning("Foundry Local not responding at {Endpoint}", endpoint);
            Debug.WriteLine($"FoundryService: service not responding at {endpoint}");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize Foundry Local.");
            Debug.WriteLine($"FoundryService: InitializeAsync error {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ReconnectAsync(CancellationToken cancellationToken = default)
    {
        _cachedEndpoint = null;
        _catalogCache = null;
        _logger?.LogInformation("Foundry Local endpoint cache cleared, re-discovering...");
        return await InitializeAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocalModel>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedEndpoint is null)
        {
            return [];
        }

        var models = new List<LocalModel>();
        var totalRam = GetTotalSystemRam();

        try
        {
            // Get the full catalog from /foundry/list
            _logger?.LogInformation("Fetching catalog from {Endpoint}/foundry/list", _cachedEndpoint);
            var response = await _httpClient.GetAsync($"{_cachedEndpoint}/foundry/list", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Catalog request failed: {Status}", response.StatusCode);
                return models;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            // Response is either a plain array [...] or { "models": [...] }
            JsonElement modelsArray;
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                modelsArray = doc.RootElement;
            }
            else if (doc.RootElement.TryGetProperty("models", out var nested) && nested.ValueKind == JsonValueKind.Array)
            {
                modelsArray = nested;
            }
            else
            {
                _logger?.LogWarning("Unexpected catalog response format");
                return models;
            }

            // Cache for download operations
            _catalogCache = modelsArray.EnumerateArray().Select(e => e.Clone()).ToList();

            // Get downloaded and loaded model sets
            var downloadedSet = await GetDownloadedModelNamesAsync(cancellationToken);
            var loadedSet = await GetLoadedModelNamesAsync(cancellationToken);

            foreach (var model in modelsArray.EnumerateArray())
            {
                var localModel = ParseCatalogModel(model, downloadedSet, loadedSet, totalRam);
                if (localModel is not null)
                {
                    models.Add(localModel);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get available models from Foundry Local");
        }

        return models;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocalModel>> GetCachedModelsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedEndpoint is null)
        {
            return [];
        }

        var models = new List<LocalModel>();
        var totalRam = GetTotalSystemRam();

        try
        {
            var downloadedNames = await GetDownloadedModelNamesAsync(cancellationToken);
            var loadedNames = await GetLoadedModelNamesAsync(cancellationToken);

            foreach (var name in downloadedNames)
            {
                var isLoaded = loadedNames.Contains(name);
                models.Add(new LocalModel
                {
                    ModelId = name,
                    Alias = name,
                    DisplayName = name,
                    Provider = "Unknown",
                    Task = "chat-completions",
                    Device = "Unknown",
                    Status = isLoaded ? ModelStatus.Loaded : ModelStatus.Downloaded,
                    SizeInBytes = 0,
                    EstimatedRamInBytes = 0,
                    MaxTokens = 0,
                    CanRun = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get cached models");
        }

        return models;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocalModel>> GetLoadedModelsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedEndpoint is null)
        {
            return [];
        }

        var models = new List<LocalModel>();

        try
        {
            var loadedNames = await GetLoadedModelNamesAsync(cancellationToken);

            foreach (var name in loadedNames)
            {
                models.Add(new LocalModel
                {
                    ModelId = name,
                    Alias = name,
                    DisplayName = name,
                    Provider = "Unknown",
                    Task = "chat-completions",
                    Device = "Unknown",
                    Status = ModelStatus.Loaded,
                    SizeInBytes = 0,
                    EstimatedRamInBytes = 0,
                    MaxTokens = 0,
                    CanRun = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get loaded models");
        }

        return models;
    }

    /// <inheritdoc />
    public async Task DownloadModelAsync(string aliasOrModelId, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_cachedEndpoint is null)
        {
            throw new InvalidOperationException("Foundry Local is not connected.");
        }

        // Find the catalog entry for this model
        if (_catalogCache is null)
        {
            await GetAvailableModelsAsync(cancellationToken);
        }

        JsonElement? catalogEntry = null;
        if (_catalogCache is not null)
        {
            foreach (var entry in _catalogCache)
            {
                var alias = entry.TryGetProperty("alias", out var a) ? a.GetString() : null;
                var name = entry.TryGetProperty("name", out var n) ? n.GetString() : null;
                var displayName = entry.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;

                if (string.Equals(alias, aliasOrModelId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, aliasOrModelId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(displayName, aliasOrModelId, StringComparison.OrdinalIgnoreCase))
                {
                    catalogEntry = entry;
                    break;
                }
            }
        }

        if (catalogEntry is null)
        {
            throw new InvalidOperationException($"Model '{aliasOrModelId}' not found in catalog.");
        }

        var cat = catalogEntry.Value;
        var modelUri = cat.TryGetProperty("uri", out var u) ? u.GetString() ?? "" : "";
        var modelName = cat.TryGetProperty("name", out var mn) ? mn.GetString() ?? aliasOrModelId : aliasOrModelId;
        var publisher = cat.TryGetProperty("publisher", out var pub) ? pub.GetString() ?? "" : "";

        // Build the download request body per Foundry Local REST API
        var downloadBody = new
        {
            model = new
            {
                Uri = modelUri,
                Name = modelName,
                ProviderType = "AzureFoundryLocal",
                Publisher = publisher
            },
            ignorePipeReport = true
        };

        var jsonBody = JsonSerializer.Serialize(downloadBody);
        _logger?.LogInformation("Starting download of {Model}", aliasOrModelId);

        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_cachedEndpoint}/openai/download")
        {
            Content = content
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Download failed: HTTP {response.StatusCode} — {errBody}");
        }

        // Parse progress from streaming response
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var buffer = new char[4096];

        while (!cancellationToken.IsCancellationRequested)
        {
            int read = await reader.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;

            var text = new string(buffer, 0, read);

            // Parse progress lines: "Total X.XXX% Downloading filename"
            var matches = ProgressRegex().Matches(text);
            if (matches.Count > 0)
            {
                var latestMatch = matches[^1];
                if (double.TryParse(latestMatch.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var percent))
                {
                    progress?.Report(percent);
                }
            }
        }

        progress?.Report(100);
    }

    /// <inheritdoc />
    public async Task LoadModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (_cachedEndpoint is null)
        {
            throw new InvalidOperationException("Foundry Local is not connected.");
        }

        var response = await _httpClient.GetAsync(
            $"{_cachedEndpoint}/openai/load/{Uri.EscapeDataString(modelId)}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Load failed: HTTP {response.StatusCode} — {errBody}");
        }

        _logger?.LogInformation("Model {Model} loaded", modelId);
    }

    /// <inheritdoc />
    public async Task UnloadModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (_cachedEndpoint is null)
        {
            throw new InvalidOperationException("Foundry Local is not connected.");
        }

        // Note: Foundry Local doesn't have a direct unload endpoint
        // Models are unloaded automatically when another model is loaded or the service restarts
        _logger?.LogWarning("Unload operation not directly supported by REST API");
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task DeleteModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        if (_cachedEndpoint is null)
        {
            throw new InvalidOperationException("Foundry Local is not connected.");
        }

        var response = await _httpClient.DeleteAsync(
            $"{_cachedEndpoint}/openai/models/{Uri.EscapeDataString(modelId)}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Delete failed: HTTP {response.StatusCode} — {errBody}");
        }

        _logger?.LogInformation("Model {Model} deleted", modelId);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ChatCompletionStreamAsync(
        string modelId,
        IReadOnlyList<ChatMessage> messages,
        string? systemPrompt = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_cachedEndpoint is null)
        {
            throw new InvalidOperationException("Foundry Local is not connected.");
        }

        // Build messages array for API
        var apiMessages = new List<object>();

        // Add system prompt if provided
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            apiMessages.Add(new { role = "system", content = systemPrompt });
        }

        // Add conversation messages
        foreach (var msg in messages)
        {
            apiMessages.Add(new { role = msg.Role.ToLowerInvariant(), content = msg.Content });
        }

        var requestBody = new
        {
            model = modelId,
            messages = apiMessages,
            stream = true
        };

        var jsonBody = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_cachedEndpoint}/v1/chat/completions")
        {
            Content = content
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Chat completion failed: HTTP {response.StatusCode} — {errBody}");
        }

        // Parse SSE stream
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;

            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..]; // Remove "data: " prefix
            if (data == "[DONE]") break;

            // Parse the JSON chunk
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("content", out var contentProp))
                {
                    var chunk = contentProp.GetString();
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        yield return chunk;
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public long GetTotalSystemRam()
    {
        try
        {
            return (long)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        }
        catch
        {
            // Fallback: assume 16 GB
            return 16L * 1024 * 1024 * 1024;
        }
    }

    [GeneratedRegex(@"Total\s+([\d.]+)%")]
    private static partial Regex ProgressRegex();

    [GeneratedRegex(@"http://127\.0\.0\.1:\d+")]
    private static partial Regex EndpointUrlRegex();

    /// <summary>
    /// Detects the actual Foundry Local endpoint by running 'foundry service status'.
    /// </summary>
    /// <returns>The detected endpoint URL, or the default endpoint if detection fails.</returns>
    private static async Task<string> DetectEndpointAsync()
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "foundry",
                Arguments = "service status",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process is not null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                Debug.WriteLine($"FoundryService: foundry service status: {output}");

                // Extract the URL from the output (e.g., "http://127.0.0.1:51600")
                var urlMatch = EndpointUrlRegex().Match(output);
                if (urlMatch.Success)
                {
                    Debug.WriteLine($"FoundryService: detected endpoint: {urlMatch.Value}");
                    return urlMatch.Value;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FoundryService: error detecting endpoint: {ex.Message}");
        }

        return DefaultEndpoint;
    }

    /// <summary>
    /// Checks if the Foundry Local service is running at the given endpoint.
    /// </summary>
    private async Task<bool> IsServiceRunningAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var response = await _httpClient.GetAsync($"{endpoint}/openai/models", cts.Token);
            Debug.WriteLine($"FoundryService: service check at {endpoint}: {response.StatusCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FoundryService: service not running at {endpoint}: {ex.Message}");
            return false;
        }
    }

    private async Task<HashSet<string>> GetDownloadedModelNamesAsync(CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_cachedEndpoint is null) return names;

        try
        {
            var response = await _httpClient.GetAsync($"{_cachedEndpoint}/openai/models", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var val = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                        if (val is not null) names.Add(val);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get downloaded models");
        }

        return names;
    }

    private async Task<HashSet<string>> GetLoadedModelNamesAsync(CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_cachedEndpoint is null) return names;

        try
        {
            var response = await _httpClient.GetAsync($"{_cachedEndpoint}/openai/loadedmodels", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var val = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                        if (val is not null) names.Add(val);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get loaded models");
        }

        return names;
    }

    private LocalModel? ParseCatalogModel(
        JsonElement model,
        HashSet<string> downloadedSet,
        HashSet<string> loadedSet,
        long totalRam)
    {
        var name = model.TryGetProperty("name", out var n) ? n.GetString() : null;
        if (string.IsNullOrEmpty(name)) return null;

        var alias = model.TryGetProperty("alias", out var a) ? a.GetString() : name;
        var displayName = model.TryGetProperty("displayName", out var dn) ? dn.GetString() : name;
        var publisher = model.TryGetProperty("publisher", out var pub) ? pub.GetString() : "Unknown";
        var task = model.TryGetProperty("task", out var t) ? t.GetString() : "chat-completions";

        // Parse file size
        long sizeBytes = 0;
        double? fileSizeMb = null;
        if (model.TryGetProperty("fileSizeMb", out var fsz) && fsz.ValueKind == JsonValueKind.Number)
        {
            fileSizeMb = fsz.GetDouble();
            sizeBytes = (long)(fileSizeMb.Value * 1024 * 1024);
        }

        // Estimated RAM: ~1.2x file size (model weights + KV cache + runtime overhead)
        var estimatedRamBytes = fileSizeMb.HasValue ? (long)(fileSizeMb.Value * 1.2 * 1024 * 1024) : 0;

        // Parse device type from runtime
        var deviceType = "Unknown";
        if (model.TryGetProperty("runtime", out var rt) && rt.TryGetProperty("deviceType", out var dt))
        {
            deviceType = dt.GetString() ?? "Unknown";
        }

        // Parse max output tokens
        int maxTokens = 0;
        if (model.TryGetProperty("maxOutputTokens", out var mot) && mot.ValueKind == JsonValueKind.Number)
        {
            maxTokens = mot.GetInt32();
        }

        // Determine status
        var status = ModelStatus.Available;
        if (loadedSet.Contains(name))
        {
            status = ModelStatus.Loaded;
        }
        else if (downloadedSet.Contains(name))
        {
            status = ModelStatus.Downloaded;
        }

        return new LocalModel
        {
            ModelId = name,
            Alias = alias ?? name,
            DisplayName = displayName ?? name,
            Provider = publisher ?? "Unknown",
            Task = task ?? "chat-completions",
            Device = deviceType,
            Status = status,
            SizeInBytes = sizeBytes,
            EstimatedRamInBytes = estimatedRamBytes,
            MaxTokens = maxTokens,
            CanRun = estimatedRamBytes <= totalRam || estimatedRamBytes == 0
        };
    }
}
