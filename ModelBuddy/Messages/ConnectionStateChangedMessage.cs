using CommunityToolkit.Mvvm.Messaging.Messages;

namespace ModelBuddy.Messages;

/// <summary>
/// Message sent when the Foundry Local connection state changes.
/// </summary>
public sealed class ConnectionStateChangedMessage : ValueChangedMessage<bool>
{
    /// <summary>
    /// Gets the connection status message.
    /// </summary>
    public string StatusMessage { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionStateChangedMessage"/> class.
    /// </summary>
    /// <param name="isConnected">Whether the service is connected.</param>
    /// <param name="statusMessage">The connection status message.</param>
    public ConnectionStateChangedMessage(bool isConnected, string statusMessage) : base(isConnected)
    {
        StatusMessage = statusMessage;
    }
}
