using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ModelBuddy.Messages;
using ModelBuddy.Models;
using ModelBuddy.Services;
using Windows.Storage;

namespace ModelBuddy.ViewModels;

/// <summary>
/// Application-wide ViewModel for shared state like connection status and selected model.
/// </summary>
public partial class AppViewModel : ObservableObject
{
    private const string SelectedModelKey = "SelectedChatModelId";

    private readonly IFoundryService _foundryService;
    private readonly IMessenger _messenger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppViewModel"/> class.
    /// </summary>
    /// <param name="foundryService">The Foundry service.</param>
    /// <param name="messenger">The messenger for pub/sub communication.</param>
    public AppViewModel(IFoundryService foundryService, IMessenger messenger)
    {
        _foundryService = foundryService;
        _messenger = messenger;

        // Load previously selected model ID from settings
        LoadSavedModelId();
    }

    /// <summary>
    /// Gets or sets the previously saved model ID (before models are loaded).
    /// </summary>
    public string? SavedModelId { get; private set; }

    /// <summary>
    /// Gets or sets whether the app is connected to Foundry Local (truly connected, not sample data).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReconnect))]
    private bool _isConnected;

    /// <summary>
    /// Gets or sets whether a connection attempt is in progress.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReconnect))]
    private bool _isConnecting;

    /// <summary>
    /// Gets whether the reconnect button should be visible.
    /// </summary>
    public bool CanReconnect => !IsConnected && !IsConnecting;

    /// <summary>
    /// Gets or sets the connection status message.
    /// </summary>
    [ObservableProperty]
    private string _connectionStatus = "Not connected";

    /// <summary>
    /// Gets or sets the Foundry Local endpoint.
    /// </summary>
    [ObservableProperty]
    private string? _endpoint;

    /// <summary>
    /// Gets or sets the selected model for chat.
    /// </summary>
    [ObservableProperty]
    private LocalModel? _selectedChatModel;

    /// <summary>
    /// Initializes the connection to Foundry Local.
    /// </summary>
    [RelayCommand]
    public async Task ConnectAsync()
    {
        if (IsConnecting)
        {
            return;
        }

        IsConnecting = true;
        ConnectionStatus = "Connecting...";
        System.Diagnostics.Debug.WriteLine("AppViewModel: ConnectAsync started");

        try
        {
            var connected = await _foundryService.InitializeAsync();
            System.Diagnostics.Debug.WriteLine($"AppViewModel: InitializeAsync returned {connected}, endpoint={_foundryService.Endpoint}");
            UpdateConnectionState(connected);

            if (!connected)
            {
                ConnectionStatus = "Not connected";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppViewModel: ConnectAsync error {ex}");
            ConnectionStatus = $"Error: {ex.Message}";
            IsConnected = false;
            Endpoint = null;
        }
        finally
        {
            IsConnecting = false;
            // Notify subscribers that connection state has changed
            _messenger.Send(new ConnectionStateChangedMessage(IsConnected, ConnectionStatus));
            System.Diagnostics.Debug.WriteLine($"AppViewModel: ConnectAsync finished IsConnected={IsConnected} Status={ConnectionStatus}");
        }
    }

    /// <summary>
    /// Reconnects to Foundry Local.
    /// </summary>
    [RelayCommand]
    public async Task ReconnectAsync()
    {
        if (IsConnecting)
        {
            return;
        }

        IsConnecting = true;
        ConnectionStatus = "Reconnecting...";
        System.Diagnostics.Debug.WriteLine("AppViewModel: ReconnectAsync started");

        try
        {
            var connected = await _foundryService.ReconnectAsync();
            System.Diagnostics.Debug.WriteLine($"AppViewModel: ReconnectAsync returned {connected}, endpoint={_foundryService.Endpoint}");
            UpdateConnectionState(connected);

            if (!connected)
            {
                ConnectionStatus = "Not connected";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppViewModel: ReconnectAsync error {ex}");
            ConnectionStatus = $"Connection failed: {ex.Message}";
            IsConnected = false;
            Endpoint = null;
        }
        finally
        {
            IsConnecting = false;
            // Notify subscribers that connection state has changed
            _messenger.Send(new ConnectionStateChangedMessage(IsConnected, ConnectionStatus));
            System.Diagnostics.Debug.WriteLine($"AppViewModel: ReconnectAsync finished IsConnected={IsConnected} Status={ConnectionStatus}");
        }
    }

    /// <summary>
    /// Updates the connection state from the service.
    /// </summary>
    private void UpdateConnectionState(bool serviceConnected)
    {
        IsConnected = serviceConnected && _foundryService.Endpoint is not null;
        Endpoint = _foundryService.Endpoint?.ToString();

        ConnectionStatus = IsConnected ? "Connected" : "Not connected";
    }

    /// <summary>
    /// Sets the selected model for chat and persists the selection.
    /// </summary>
    /// <param name="model">The model to select.</param>
    public void SelectModelForChat(LocalModel model)
    {
        SelectedChatModel = model;
        SaveModelId(model.ModelId);
    }

    /// <summary>
    /// Clears the selected chat model.
    /// </summary>
    public void ClearSelectedModel()
    {
        SelectedChatModel = null;
        SaveModelId(null);
    }

    /// <summary>
    /// Restores the selected model from a list of available models.
    /// </summary>
    /// <param name="models">The available models to search.</param>
    /// <returns>True if a saved model was restored.</returns>
    public bool TryRestoreSelectedModel(IEnumerable<LocalModel> models)
    {
        if (string.IsNullOrEmpty(SavedModelId) || SelectedChatModel is not null)
        {
            return false;
        }

        var savedModel = models.FirstOrDefault(m => 
            m.ModelId == SavedModelId && 
            (m.Status == ModelStatus.Downloaded || m.Status == ModelStatus.Loaded));

        if (savedModel is not null)
        {
            SelectedChatModel = savedModel;
            return true;
        }

        return false;
    }

    private void LoadSavedModelId()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            SavedModelId = localSettings.Values[SelectedModelKey] as string;
        }
        catch
        {
            // Ignore settings errors
        }
    }

    private void SaveModelId(string? modelId)
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (modelId is not null)
            {
                localSettings.Values[SelectedModelKey] = modelId;
            }
            else
            {
                localSettings.Values.Remove(SelectedModelKey);
            }
        }
        catch
        {
            // Ignore settings errors
        }
    }
}
