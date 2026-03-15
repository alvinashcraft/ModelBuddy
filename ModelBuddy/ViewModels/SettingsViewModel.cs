using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using ModelBuddy.Constants;
using ModelBuddy.Services;

namespace ModelBuddy.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    /// <param name="settingsService">The settings service.</param>
    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        // Load current values
        _selectedThemeIndex = _settingsService.AppTheme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0 // Auto
        };
        _systemInstructions = _settingsService.SystemInstructions;
        _customEndpoint = _settingsService.CustomEndpoint;
    }

    /// <summary>
    /// Gets or sets the selected theme index (0=Auto, 1=Light, 2=Dark).
    /// </summary>
    [ObservableProperty]
    private int _selectedThemeIndex;

    /// <summary>
    /// Gets or sets the user-customizable system instructions.
    /// </summary>
    [ObservableProperty]
    private string _systemInstructions;

    /// <summary>
    /// Gets or sets the custom Foundry Local endpoint.
    /// </summary>
    [ObservableProperty]
    private string _customEndpoint;

    /// <summary>
    /// Gets whether the system instructions differ from the default.
    /// </summary>
    [ObservableProperty]
    private bool _isInstructionsModified;

    /// <summary>
    /// Gets the content safety guidelines (read-only, always applied).
    /// </summary>
    public string SafetyGuidelines => ContentSafetyConstants.SafetyGuidelines;

    /// <summary>
    /// Gets the application version string.
    /// </summary>
    public string AppVersion
    {
        get
        {
            try
            {
                var version = Windows.ApplicationModel.Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
            catch
            {
                return "Development build";
            }
        }
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        var theme = value switch
        {
            1 => "Light",
            2 => "Dark",
            _ => "Auto"
        };
        _settingsService.AppTheme = theme;

        // Apply the theme immediately
        (Application.Current as App)?.ApplyTheme(theme);
    }

    partial void OnSystemInstructionsChanged(string value)
    {
        _settingsService.SystemInstructions = value;
        IsInstructionsModified = !string.Equals(value?.Trim(), ContentSafetyConstants.DefaultInstructions.Trim(), StringComparison.Ordinal);
    }

    partial void OnCustomEndpointChanged(string value)
    {
        _settingsService.CustomEndpoint = value;
    }

    /// <summary>
    /// Resets the system instructions to the default value.
    /// </summary>
    [RelayCommand]
    private void ResetSystemInstructions()
    {
        SystemInstructions = ContentSafetyConstants.DefaultInstructions;
    }
}
