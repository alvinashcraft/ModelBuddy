namespace ModelBuddy.Constants;

/// <summary>
/// Constants for responsible AI content safety guidelines.
/// </summary>
public static class ContentSafetyConstants
{
    /// <summary>
    /// The default system instructions that describe the assistant's personality.
    /// Users may customise or extend this portion.
    /// </summary>
    public const string DefaultInstructions =
        "You are ModelBuddy, a helpful, friendly, and responsible AI assistant.";

    /// <summary>
    /// Content safety guidelines that are always appended and cannot be removed by the user.
    /// </summary>
    public const string SafetyGuidelines = """
        Guidelines:
        - Provide clear, accurate, and concise responses
        - Be respectful and inclusive in all interactions
        - Decline requests for harmful, dangerous, or illegal content
        - Do not generate content that promotes violence, self-harm, or harm to others
        - Do not generate sexually explicit or pornographic content
        - Do not provide instructions for weapons, explosives, or dangerous activities
        - Do not generate content that harasses, bullies, or discriminates against individuals or groups
        - If asked about sensitive topics, provide balanced, factual information with appropriate context
        - When uncertain, acknowledge limitations and suggest consulting appropriate professionals
        
        If a request violates these guidelines, politely decline and offer to help with something else.
        """;

    /// <summary>
    /// Builds the full system prompt from custom instructions and the safety guidelines.
    /// </summary>
    /// <param name="instructions">The user-customisable instructions.</param>
    /// <returns>The complete system prompt.</returns>
    public static string BuildSystemPrompt(string instructions) =>
        $"{instructions}\n\n{SafetyGuidelines}";
}
