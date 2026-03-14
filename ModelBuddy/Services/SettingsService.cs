using ModelBuddy.Constants;
using Windows.Storage;

namespace ModelBuddy.Services;

/// <summary>
/// Settings service backed by <see cref="ApplicationData.Current.LocalSettings"/>.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private const string ThemeKey = "AppTheme";
    private const string SystemInstructionsKey = "SystemInstructions";
    private const string EndpointKey = "CustomEndpoint";

    private readonly ApplicationDataContainer _localSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// </summary>
    public SettingsService()
    {
        _localSettings = ApplicationData.Current.LocalSettings;
    }

    /// <inheritdoc />
    public string AppTheme
    {
        get => ReadString(ThemeKey, "Auto");
        set => Write(ThemeKey, value);
    }

    /// <inheritdoc />
    public string SystemInstructions
    {
        get => ReadString(SystemInstructionsKey, ContentSafetyConstants.DefaultInstructions);
        set => Write(SystemInstructionsKey, value);
    }

    /// <inheritdoc />
    public string CustomEndpoint
    {
        get => ReadString(EndpointKey, string.Empty);
        set => Write(EndpointKey, value);
    }

    private string ReadString(string key, string defaultValue)
    {
        try
        {
            return _localSettings.Values[key] as string ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private void Write(string key, string? value)
    {
        try
        {
            if (string.IsNullOrEmpty(value))
            {
                _localSettings.Values.Remove(key);
            }
            else
            {
                _localSettings.Values[key] = value;
            }
        }
        catch
        {
            // Ignore write errors
        }
    }
}
