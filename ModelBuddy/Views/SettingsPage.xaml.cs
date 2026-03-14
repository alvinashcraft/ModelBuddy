using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModelBuddy.ViewModels;

namespace ModelBuddy.Views;

/// <summary>
/// Page for application settings.
/// </summary>
public sealed partial class SettingsPage : Page
{
    /// <summary>
    /// Gets the ViewModel for the settings page.
    /// </summary>
    public SettingsViewModel ViewModel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsPage"/> class.
    /// </summary>
    public SettingsPage()
    {
        ViewModel = (Application.Current as App)!.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }
}
