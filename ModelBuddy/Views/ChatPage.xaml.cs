using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ModelBuddy.Models;
using ModelBuddy.ViewModels;

namespace ModelBuddy.Views;

/// <summary>
/// Page for chatting with local AI models.
/// </summary>
public sealed partial class ChatPage : Page
{
    /// <summary>
    /// Gets the ViewModel for this page.
    /// </summary>
    public ChatViewModel ViewModel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatPage"/> class.
    /// </summary>
    public ChatPage()
    {
        ViewModel = (Application.Current as App)!.Services.GetRequiredService<ChatViewModel>();
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e) =>
        ViewModel.InitializeCommand.Execute(null);

    private void SendAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.SendMessageCommand.CanExecute(null))
        {
            ViewModel.SendMessageCommand.Execute(null);
            args.Handled = true;
        }
    }

    /// <summary>
    /// Gets visibility based on whether the message is from the user.
    /// </summary>
    public static Visibility GetUserVisibility(ChatMessage message) =>
        message.IsUser ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Gets visibility based on whether the message is from the assistant.
    /// </summary>
    public static Visibility GetAssistantVisibility(ChatMessage message) =>
        message.IsAssistant ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Returns Visible if the collection has items.
    /// </summary>
    public static Visibility HasItems(int count) =>
        count > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Returns Visible if the collection is empty.
    /// </summary>
    public static Visibility IsEmpty(int count) =>
        count == 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Gets background brush based on connection status.
    /// </summary>
    public static SolidColorBrush GetConnectionBackground(bool isConnected) =>
        isConnected 
            ? new SolidColorBrush(Colors.Green) { Opacity = 0.2 }
            : new SolidColorBrush(Colors.Orange) { Opacity = 0.2 };

    /// <summary>
    /// Gets visibility for the connection status message.
    /// </summary>
    public static Visibility GetConnectionVisibility(string? statusMessage) =>
        string.IsNullOrWhiteSpace(statusMessage) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// Gets visibility for the reconnect button.
    /// </summary>
    public static Visibility GetReconnectVisibility(bool isConnected, bool isLoading) =>
        !isConnected && !isLoading ? Visibility.Visible : Visibility.Collapsed;
}
