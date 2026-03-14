namespace ModelBuddy.Services;

/// <summary>
/// Interface for reading and writing application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets or sets the app theme. Values: "Auto", "Light", "Dark".
    /// </summary>
    string AppTheme { get; set; }

    /// <summary>
    /// Gets or sets the user-customisable system instructions (personality / behaviour).
    /// The content safety guidelines are always appended automatically.
    /// </summary>
    string SystemInstructions { get; set; }

    /// <summary>
    /// Gets or sets a custom Foundry Local endpoint (e.g. "http://127.0.0.1:5272").
    /// Empty or null means auto-detect.
    /// </summary>
    string CustomEndpoint { get; set; }
}
