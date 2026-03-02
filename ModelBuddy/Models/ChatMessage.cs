namespace ModelBuddy.Models;

/// <summary>
/// Represents a message in a chat conversation.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Gets or sets the role of the message sender (user, assistant, system).
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// Gets or sets the content of the message.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the message.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets whether this message is from the user.
    /// </summary>
    public bool IsUser => Role.Equals("user", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether this message is from the assistant.
    /// </summary>
    public bool IsAssistant => Role.Equals("assistant", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether this message is a system message.
    /// </summary>
    public bool IsSystem => Role.Equals("system", StringComparison.OrdinalIgnoreCase);
}
