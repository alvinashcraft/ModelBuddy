using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ModelBuddy.Messages;
using ModelBuddy.Models;
using ModelBuddy.Services;

namespace ModelBuddy.ViewModels;

/// <summary>
/// ViewModel for the Models management page.
/// </summary>
public partial class ModelsViewModel : ObservableRecipient, IRecipient<ConnectionStateChangedMessage>
{
    private readonly IFoundryService _foundryService;
    private readonly AppViewModel _appViewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelsViewModel"/> class.
    /// </summary>
    /// <param name="foundryService">The Foundry Local service.</param>
    /// <param name="appViewModel">The shared application ViewModel.</param>
    /// <param name="messenger">The messenger for pub/sub communication.</param>
    public ModelsViewModel(IFoundryService foundryService, AppViewModel appViewModel, IMessenger messenger)
        : base(messenger)
    {
        _foundryService = foundryService;
        _appViewModel = appViewModel;

        // Enable message receiving
        IsActive = true;
    }

    /// <summary>
    /// Handles connection state changed messages.
    /// </summary>
    public void Receive(ConnectionStateChangedMessage message)
    {
        OnPropertyChanged(nameof(IsConnected));

        // If connected and no models loaded, load them
        if (message.Value && _allModels.Count == 0)
        {
            _ = LoadModelsOnConnectedAsync();
        }
    }

    private async Task LoadModelsOnConnectedAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading models...";
        try
        {
            await LoadModelsAsync();
            StatusMessage = $"Loaded {_allModels.Count} models";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Gets the collection of all models.
    /// </summary>
    public ObservableCollection<LocalModel> Models { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the view is currently loading.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Gets or sets the status message to display.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Not connected";

    /// <summary>
    /// Gets whether the service is connected (from shared AppViewModel).
    /// </summary>
    public bool IsConnected => _appViewModel.IsConnected;

    /// <summary>
    /// Gets or sets the currently selected model.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectForChatCommand))]
    private LocalModel? _selectedModel;

    /// <summary>
    /// Gets or sets the search/filter text.
    /// </summary>
    [ObservableProperty]
    private string? _searchText;

    partial void OnSearchTextChanged(string? value)
    {
        // Re-filter the models list when search text changes
        _ = FilterModelsAsync();
    }

    private IReadOnlyList<LocalModel> _allModels = [];

    /// <summary>
    /// Initializes the ViewModel and loads models if already connected.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        // Update local state from shared AppViewModel
        OnPropertyChanged(nameof(IsConnected));

        // If already connected (connection completed before page loaded), load models
        if (_appViewModel.IsConnected && _allModels.Count == 0)
        {
            await LoadModelsOnConnectedAsync();
        }
        // Otherwise, wait for ConnectionStateChangedMessage from AppViewModel
    }

    /// <summary>
    /// Loads all models from Foundry Local.
    /// </summary>
    [RelayCommand]
    private async Task LoadModelsAsync()
    {
        if (!IsConnected)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = "Loading models...";

        try
        {
            _allModels = await _foundryService.GetAvailableModelsAsync();
            await FilterModelsAsync();
            StatusMessage = $"Loaded {_allModels.Count} models";

            // Try to restore previously selected chat model
            if (_appViewModel.TryRestoreSelectedModel(_allModels))
            {
                StatusMessage = $"Loaded {_allModels.Count} models (restored {_appViewModel.SelectedChatModel?.DisplayName} for chat)";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading models: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private Task FilterModelsAsync()
    {
        Models.Clear();

        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allModels
            : _allModels.Where(m =>
                m.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                m.Alias.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                m.Provider.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                m.Task.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var model in filtered)
        {
            Models.Add(model);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Downloads the selected model.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDownloadModel))]
    private async Task DownloadModelAsync()
    {
        if (SelectedModel is null)
        {
            return;
        }

        var modelToDownload = SelectedModel;
        IsLoading = true;
        StatusMessage = $"Downloading {modelToDownload.DisplayName}...";
        modelToDownload.Status = ModelStatus.Downloading;

        try
        {
            await _foundryService.DownloadModelAsync(modelToDownload.Alias);
            modelToDownload.Status = ModelStatus.Downloaded;
            StatusMessage = $"Downloaded {modelToDownload.DisplayName}";
            await LoadModelsAsync();
        }
        catch (Exception ex)
        {
            modelToDownload.Status = ModelStatus.Available;
            StatusMessage = $"Download failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanDownloadModel()
    {
        return SelectedModel is not null && SelectedModel.Status == ModelStatus.Available;
    }

    /// <summary>
    /// Deletes the selected model from the cache.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteModel))]
    private async Task DeleteModelAsync()
    {
        if (SelectedModel is null)
        {
            return;
        }

        var modelToDelete = SelectedModel;
        IsLoading = true;
        StatusMessage = $"Deleting {modelToDelete.DisplayName}...";

        try
        {
            await _foundryService.DeleteModelAsync(modelToDelete.ModelId);
            StatusMessage = $"Deleted {modelToDelete.DisplayName}";
            await LoadModelsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Delete failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanDeleteModel()
    {
        return SelectedModel is not null &&
               (SelectedModel.Status == ModelStatus.Downloaded || SelectedModel.Status == ModelStatus.Loaded);
    }

    /// <summary>
    /// Selects the current model for chat.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSelectForChat))]
    private void SelectForChat()
    {
        if (SelectedModel is null)
        {
            return;
        }

        _appViewModel.SelectModelForChat(SelectedModel);
        StatusMessage = $"Selected {SelectedModel.DisplayName} for chat";
    }

    private bool CanSelectForChat()
    {
        return SelectedModel is not null &&
               (SelectedModel.Task.Contains("chat", StringComparison.OrdinalIgnoreCase)) &&
               (SelectedModel.Status == ModelStatus.Downloaded || SelectedModel.Status == ModelStatus.Loaded);
    }

    /// <summary>
    /// Refreshes the models list.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            await LoadModelsAsync();
            StatusMessage = $"Loaded {_allModels.Count} models";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
