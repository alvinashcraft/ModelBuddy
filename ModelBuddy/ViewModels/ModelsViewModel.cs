using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModelBuddy.Models;
using ModelBuddy.Services;

namespace ModelBuddy.ViewModels;

/// <summary>
/// ViewModel for the Models management page.
/// </summary>
public partial class ModelsViewModel : ObservableObject
{
    private readonly IFoundryService _foundryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelsViewModel"/> class.
    /// </summary>
    /// <param name="foundryService">The Foundry Local service.</param>
    public ModelsViewModel(IFoundryService foundryService)
    {
        _foundryService = foundryService;
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
    /// Gets or sets a value indicating whether Foundry Local is connected.
    /// </summary>
    [ObservableProperty]
    private bool _isConnected;

    /// <summary>
    /// Gets or sets the status message to display.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Not connected";

    /// <summary>
    /// Gets or sets the currently selected model.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadModelCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteModelCommand))]
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
    /// Initializes the ViewModel and connects to Foundry Local.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "Connecting to Foundry Local...";

        try
        {
            IsConnected = await _foundryService.InitializeAsync();

            if (IsConnected)
            {
                StatusMessage = $"Connected to {_foundryService.Endpoint}";
                await LoadModelsAsync();
            }
            else
            {
                StatusMessage = "Failed to connect to Foundry Local. Is the service running?";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsConnected = false;
        }
        finally
        {
            IsLoading = false;
        }
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
    /// Refreshes the connection to Foundry Local.
    /// </summary>
    [RelayCommand]
    private async Task RefreshConnectionAsync()
    {
        await InitializeAsync();
    }
}
