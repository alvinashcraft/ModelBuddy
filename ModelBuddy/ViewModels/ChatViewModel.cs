using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ModelBuddy.Constants;
using ModelBuddy.Messages;
using ModelBuddy.Models;
using ModelBuddy.Services;

namespace ModelBuddy.ViewModels;

/// <summary>
/// ViewModel for the Chat page.
/// </summary>
public partial class ChatViewModel : ObservableRecipient, IRecipient<ConnectionStateChangedMessage>
{
    private readonly IFoundryService _foundryService;
    private readonly AppViewModel _appViewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatViewModel"/> class.
    /// </summary>
    /// <param name="foundryService">The Foundry service.</param>
    /// <param name="appViewModel">The shared application ViewModel.</param>
    /// <param name="messenger">The messenger for pub/sub communication.</param>
    public ChatViewModel(IFoundryService foundryService, AppViewModel appViewModel, IMessenger messenger)
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

        if (IsConnected)
        {
            StatusMessage = string.Empty;
            if (AvailableModels.Count == 0)
            {
                _ = LoadModelsOnConnectedAsync();
            }
        }
        else
        {
            StatusMessage = message.StatusMessage;
        }
    }

    private async Task LoadModelsOnConnectedAsync()
    {
        IsLoading = true;
        try
        {
            await LoadModelsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Gets the collection of chat messages.
    /// </summary>
    public ObservableCollection<ChatMessage> Messages { get; } = [];

    /// <summary>
    /// Gets the available models for chat.
    /// </summary>
    public ObservableCollection<LocalModel> AvailableModels { get; } = [];

    /// <summary>
    /// Gets or sets the selected model (synced with AppViewModel).
    /// </summary>
    public LocalModel? SelectedModel
    {
        get => _appViewModel.SelectedChatModel;
        set
        {
            if (_appViewModel.SelectedChatModel != value)
            {
                _appViewModel.SelectedChatModel = value;
                OnPropertyChanged();
                SendMessageCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the user input text.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string _userInput = string.Empty;

    /// <summary>
    /// Gets or sets whether a message is currently being sent.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopGenerationCommand))]
    private bool _isGenerating;

    /// <summary>
    /// Gets or sets the current assistant response being streamed.
    /// </summary>
    [ObservableProperty]
    private string _currentResponse = string.Empty;

    /// <summary>
    /// Gets or sets the system prompt with RAI content safety guidelines.
    /// </summary>
    [ObservableProperty]
    private string _systemPrompt = ContentSafetyConstants.SystemPrompt;

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Gets whether the service is connected.
    /// </summary>
    public bool IsConnected => _foundryService.IsConnected;

    /// <summary>
    /// Gets or sets whether the page is loading.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    private CancellationTokenSource? _generationCts;


    /// <summary>
    /// Initializes the chat page using shared connection state.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        // Notify UI of connection state from shared ViewModel
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(SelectedModel));

        if (!IsConnected)
        {
            StatusMessage = _appViewModel.ConnectionStatus;
            return;
        }

        StatusMessage = string.Empty;

        // Load available models if we don't have any yet
        if (AvailableModels.Count == 0)
        {
            IsLoading = true;
            try
            {
                await LoadModelsAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }
        // If not connected yet, the Receive handler will load when connection completes
    }

    /// <summary>
    /// Loads available models for chat.
    /// </summary>
    [RelayCommand]
    private async Task LoadModelsAsync()
    {
        if (!IsConnected)
        {
            return;
        }

        AvailableModels.Clear();

        var models = await _foundryService.GetAvailableModelsAsync();
        foreach (var model in models.Where(m => m.Task.Contains("chat", StringComparison.OrdinalIgnoreCase) && 
                                                (m.Status == ModelStatus.Downloaded || m.Status == ModelStatus.Loaded)))
        {
            AvailableModels.Add(model);
        }

        // Select first model if none selected
        if (SelectedModel is null && AvailableModels.Count > 0)
        {
            SelectedModel = AvailableModels[0];
        }
    }

    /// <summary>
    /// Sends a message to the chat.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || SelectedModel is null)
            return;

        var userMessage = new ChatMessage
        {
            Role = "user",
            Content = UserInput.Trim()
        };
        Messages.Add(userMessage);

        var input = UserInput;
        UserInput = string.Empty;

        IsGenerating = true;
        _generationCts = new CancellationTokenSource();

        try
        {
            var responseBuilder = new StringBuilder();
            var assistantMessage = new ChatMessage
            {
                Role = "assistant",
                Content = string.Empty
            };
            Messages.Add(assistantMessage);

            await foreach (var chunk in _foundryService.ChatCompletionStreamAsync(
                SelectedModel.ModelId,
                Messages.ToList(),
                SystemPrompt,
                _generationCts.Token))
            {
                responseBuilder.Append(chunk);
                assistantMessage.Content = responseBuilder.ToString();
                CurrentResponse = assistantMessage.Content;

                // Force UI update by removing and re-adding
                var index = Messages.IndexOf(assistantMessage);
                if (index >= 0)
                {
                    Messages.RemoveAt(index);
                    Messages.Insert(index, assistantMessage);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // User cancelled generation
        }
        catch (Exception ex)
        {
            var errorMessage = new ChatMessage
            {
                Role = "assistant",
                Content = $"Error: {ex.Message}"
            };
            Messages.Add(errorMessage);
        }
        finally
        {
            IsGenerating = false;
            CurrentResponse = string.Empty;
            _generationCts?.Dispose();
            _generationCts = null;
        }
    }

    private bool CanSendMessage() =>
        IsConnected &&
        !IsGenerating &&
        !string.IsNullOrWhiteSpace(UserInput) &&
        SelectedModel is not null;

    /// <summary>
    /// Stops the current generation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopGeneration))]
    private void StopGeneration()
    {
        _generationCts?.Cancel();
    }

    private bool CanStopGeneration() => IsGenerating;

    /// <summary>
    /// Clears the chat history.
    /// </summary>
    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        CurrentResponse = string.Empty;
    }
}
