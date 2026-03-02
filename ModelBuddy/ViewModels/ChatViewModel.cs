using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModelBuddy.Models;
using ModelBuddy.Services;

namespace ModelBuddy.ViewModels;

/// <summary>
/// ViewModel for the Chat page.
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    private readonly IFoundryService _foundryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatViewModel"/> class.
    /// </summary>
    /// <param name="foundryService">The Foundry service.</param>
    public ChatViewModel(IFoundryService foundryService)
    {
        _foundryService = foundryService;
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
    /// Gets or sets the selected model.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private LocalModel? _selectedModel;

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
    /// Gets or sets the system prompt.
    /// </summary>
    [ObservableProperty]
    private string _systemPrompt = "You are a helpful AI assistant.";

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Gets or sets whether the service is connected.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private bool _isConnected;

    private CancellationTokenSource? _generationCts;

    /// <summary>
    /// Initializes the chat page and connects to Foundry Local.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        StatusMessage = "Connecting to Foundry Local...";

        try
        {
            IsConnected = await _foundryService.InitializeAsync();

            if (IsConnected)
            {
                StatusMessage = string.Empty;
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
        foreach (var model in models.Where(m => m.Task == "chat-completions" && 
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
