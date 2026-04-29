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
    private static readonly int[] PortsToScan = [5272, 5273, 5274, 5275, 5276];
    private readonly HttpClient _httpClient;
    private readonly ILogger<FoundryService>? _logger;
    private readonly ISettingsService _settingsService;
    private string? _cachedEndpoint;
    private List<JsonElement>? _catalogCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="FoundryService"/> class.
    /// </summary>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="logger">Optional logger instance.</param>
    public FoundryService(ISettingsService settingsService, ILogger<FoundryService>? logger = null)
    {
        _settingsService = settingsService;
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
            // Check for user-configured endpoint first
            var customEndpoint = _settingsService.CustomEndpoint;
            if (!string.IsNullOrWhiteSpace(customEndpoint))
            {
                Debug.WriteLine($"FoundryService: trying custom endpoint {customEndpoint}");
                if (await IsServiceRunningAsync(customEndpoint, cancellationToken))
                {
                    _cachedEndpoint = customEndpoint;
                    _logger?.LogInformation("Connected to Foundry Local at custom endpoint {Endpoint}", customEndpoint);
                    return true;
                }

                _logger?.LogWarning("Custom endpoint {Endpoint} not responding, falling back to auto-detect", customEndpoint);
            }

            // Try to detect a running service (CLI status + port scan)
            var endpoint = await DetectEndpointAsync(cancellationToken);
            if (endpoint is not null)
            {
                _cachedEndpoint = endpoint;
                _logger?.LogInformation("Connected to Foundry Local at {Endpoint}", endpoint);
                Debug.WriteLine($"FoundryService: connected to {endpoint}");
                return true;
            }

            // Service not running — try to start it
            _logger?.LogInformation("Foundry Local not detected, attempting to start the service...");
            Debug.WriteLine("FoundryService: no service detected, starting it...");

            endpoint = await TryStartServiceAsync(cancellationToken);
            if (endpoint is not null)
            {
                _cachedEndpoint = endpoint;
                _logger?.LogInformation("Connected to Foundry Local at {Endpoint} after starting service", endpoint);
                Debug.WriteLine($"FoundryService: connected after start to {endpoint}");
                return true;
            }

            _logger?.LogWarning("Foundry Local could not be started or connected");
            Debug.WriteLine("FoundryService: failed to start or connect");
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

        _logger?.LogInformation("Attempting to delete model {Model}", modelId);
        Debug.WriteLine($"FoundryService: DeleteModelAsync - Deleting {modelId}");

        // The modelId might have a variant suffix like "qwen3-0.6b-generic-cpu:4"
        // Extract the base name for deletion (foundry expects just the model name)
        var baseName = modelId;
        if (modelId.Contains(':'))
        {
            baseName = modelId.Substring(0, modelId.IndexOf(':'));
            _logger?.LogInformation("Extracted base model name: {BaseName} from {ModelId}", baseName, modelId);
            Debug.WriteLine($"FoundryService: DeleteModelAsync - Extracted base name: {baseName} from {modelId}");
        }

        try
        {
            // Use the foundry CLI cache remove command to delete the model
            // Per official docs: foundry cache remove <model>
            var cliPath = GetFoundryCliPath();
            if (string.IsNullOrEmpty(cliPath))
            {
                _logger?.LogWarning("Foundry CLI not found, attempting REST API fallback");
                await DeleteModelViaRestApiAsync(baseName, cancellationToken);
                return;
            }

            var (output, exitCode) = await RunFoundryCliAsync($"cache remove {baseName}");
            
            // Check for success — CLI returns 0 on success
            if (exitCode == 0)
            {
                _logger?.LogInformation("Model {Model} deleted via CLI cache remove", baseName);
                Debug.WriteLine($"FoundryService: DeleteModelAsync - Successfully deleted {baseName}");

                // Verify deletion by checking if model still appears in downloaded list
                var stillDownloaded = await GetDownloadedModelNamesAsync(cancellationToken);
                if (stillDownloaded.Contains(baseName))
                {
                    _logger?.LogWarning("Model {Model} still appears in cache after delete", baseName);
                    Debug.WriteLine($"FoundryService: DeleteModelAsync - WARNING: {baseName} still in cache");
                }
                else
                {
                    _logger?.LogInformation("Model {Model} confirmed removed from cache", baseName);
                    Debug.WriteLine($"FoundryService: DeleteModelAsync - Verified: {baseName} removed");
                }
                return;
            }

            // If exit code indicates failure, log and try REST API fallback
            _logger?.LogWarning("Model deletion CLI command failed with exit code {ExitCode}: {Output}", exitCode, output);
            Debug.WriteLine($"FoundryService: DeleteModelAsync - CLI failed with code {exitCode}: {output}");
            
            // Fallback to REST API if CLI fails
            await DeleteModelViaRestApiAsync(baseName, cancellationToken);
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            _logger?.LogWarning(ex, "CLI deletion failed, attempting REST API fallback");
            Debug.WriteLine($"FoundryService: DeleteModelAsync - CLI error, trying REST API: {ex.Message}");
            
            // Fallback to REST API if CLI throws
            try
            {
                await DeleteModelViaRestApiAsync(baseName, cancellationToken);
            }
            catch (Exception restEx)
            {
                _logger?.LogError(restEx, "Both CLI and REST API deletion failed for {Model}", modelId);
                throw new InvalidOperationException($"Model deletion failed via both CLI and REST API: {restEx.Message}", restEx);
            }
        }
    }

    private async Task DeleteModelViaRestApiAsync(string modelId, CancellationToken cancellationToken)
    {
        var url = $"{_cachedEndpoint}/openai/models/{Uri.EscapeDataString(modelId)}";
        _logger?.LogInformation("Attempting model deletion via REST API at {Url}", url);
        Debug.WriteLine($"FoundryService: DeleteModelViaRestApiAsync - DELETE {url}");

        var response = await _httpClient.DeleteAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger?.LogError("REST API delete failed with status {Status}: {Error}", response.StatusCode, errBody);
            Debug.WriteLine($"FoundryService: DeleteModelViaRestApiAsync - HTTP {response.StatusCode}: {errBody}");
            throw new InvalidOperationException($"REST API delete failed: HTTP {response.StatusCode} — {errBody}");
        }

        _logger?.LogInformation("Model {Model} deleted via REST API", modelId);
        Debug.WriteLine($"FoundryService: DeleteModelViaRestApiAsync - Success");

        // Verify deletion by checking if model still appears in downloaded list
        var stillDownloaded = await GetDownloadedModelNamesAsync(cancellationToken);
        if (stillDownloaded.Contains(modelId))
        {
            _logger?.LogWarning("Model {Model} still appears in cache after delete", modelId);
            Debug.WriteLine($"FoundryService: DeleteModelViaRestApiAsync - WARNING: {modelId} still in cache");
        }
        else
        {
            _logger?.LogInformation("Model {Model} confirmed removed from cache", modelId);
            Debug.WriteLine($"FoundryService: DeleteModelViaRestApiAsync - Verified: {modelId} removed");
        }
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
    /// Resolves the Foundry Local CLI path, checking well-known locations.
    /// </summary>
    private static string GetFoundryCliPath()
    {
        // The Foundry CLI is a Store/MSIX app installed under WindowsApps
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windowsAppsPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "foundry.exe");

        if (File.Exists(windowsAppsPath))
        {
            return windowsAppsPath;
        }

        // Fall back to bare name (relies on PATH)
        return "foundry";
    }

    /// <summary>
    /// Runs a Foundry CLI command and returns its combined stdout+stderr output.
    /// </summary>
    private static async Task<(string Output, int ExitCode)> RunFoundryCliAsync(
        string arguments, int timeoutSeconds = 15)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = GetFoundryCliPath(),
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process is null)
            {
                return ("", -1);
            }

            // Read both streams concurrently to avoid pipe deadlocks
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            // Wait for everything to complete within the timeout
            var allDone = Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync());
            if (await Task.WhenAny(allDone, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))) != allDone)
            {
                // Timed out — kill process and don't await the stream tasks
                try { process.Kill(entireProcessTree: true); } catch { }
                Debug.WriteLine($"FoundryService: CLI '{arguments}' timed out after {timeoutSeconds}s");

                // Return whatever we got so far (may be partial)
                var partialStdout = stdoutTask.IsCompleted ? stdoutTask.Result : "";
                var partialStderr = stderrTask.IsCompleted ? stderrTask.Result : "";
                return ($"{partialStdout}\n{partialStderr}".Trim(), -1);
            }

            var combined = $"{stdoutTask.Result}\n{stderrTask.Result}".Trim();
            Debug.WriteLine($"FoundryService: CLI '{arguments}' output: {combined}");
            return (combined, process.ExitCode);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FoundryService: CLI '{arguments}' error: {ex.Message}");
            return ("", -1);
        }
    }

    /// <summary>
    /// Detects the actual Foundry Local endpoint by running 'foundry service status',
    /// then falls back to scanning common ports.
    /// </summary>
    /// <returns>The detected endpoint URL, or the default endpoint if detection fails.</returns>
    private async Task<string?> DetectEndpointAsync(CancellationToken cancellationToken = default)
    {
        // Strategy 1: Parse endpoint from CLI output
        var (output, _) = await RunFoundryCliAsync("service status");
        if (!string.IsNullOrWhiteSpace(output))
        {
            var urlMatch = EndpointUrlRegex().Match(output);
            if (urlMatch.Success)
            {
                var endpoint = urlMatch.Value;
                Debug.WriteLine($"FoundryService: CLI detected endpoint: {endpoint}");
                return endpoint;
            }
        }

        // Strategy 2: Scan well-known ports
        Debug.WriteLine("FoundryService: CLI detection failed, scanning ports...");
        foreach (var port in PortsToScan)
        {
            var endpoint = $"http://127.0.0.1:{port}";
            if (await IsServiceRunningAsync(endpoint, cancellationToken))
            {
                Debug.WriteLine($"FoundryService: found service on port {port}");
                return endpoint;
            }
        }

        Debug.WriteLine("FoundryService: no running service found");
        return null;
    }

    /// <summary>
    /// Attempts to start the Foundry Local service and waits for it to become responsive.
    /// </summary>
    /// <returns>The endpoint if the service was started successfully; otherwise null.</returns>
    private async Task<string?> TryStartServiceAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Attempting to start Foundry Local service...");

        // Fire off the start command — don't wait for it to complete since it may
        // run as a foreground process that doesn't exit until the service stops.
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = GetFoundryCliPath(),
                Arguments = "service start",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };
            Process.Start(processInfo);
            Debug.WriteLine("FoundryService: launched 'foundry service start'");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to launch foundry service start");
            Debug.WriteLine($"FoundryService: failed to launch service start: {ex.Message}");
            return null;
        }

        // Poll for readiness — the service may take a few seconds to bind a port
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            var endpoint = await DetectEndpointAsync(cancellationToken);
            if (endpoint is not null)
            {
                _logger?.LogInformation("Foundry Local started on attempt {Attempt} at {Endpoint}", attempt + 1, endpoint);
                return endpoint;
            }

            Debug.WriteLine($"FoundryService: waiting for service (attempt {attempt + 1}/10)...");
        }

        return null;
    }

    /// <summary>
    /// Checks if the Foundry Local service is running at the given endpoint.
    /// Uses /openai/status which works even when no models are downloaded.
    /// </summary>
    private async Task<bool> IsServiceRunningAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var response = await _httpClient.GetAsync($"{endpoint}/openai/status", cts.Token);
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
