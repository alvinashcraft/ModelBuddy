using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModelBuddy.ViewModels;

namespace ModelBuddy.Views;

/// <summary>
/// Page for managing Foundry Local AI models.
/// </summary>
public sealed partial class ModelsPage : Page
{
    /// <summary>
    /// Gets the ViewModel for this page.
    /// </summary>
    public ModelsViewModel ViewModel { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelsPage"/> class.
    /// </summary>
    public ModelsPage()
    {
        ViewModel = (Application.Current as App)!.Services.GetRequiredService<ModelsViewModel>();
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e) =>
        ViewModel.InitializeCommand.Execute(null);

    /// <summary>
    /// Helper method to invert a boolean for visibility binding.
    /// </summary>
    /// <param name="value">The boolean value to invert.</param>
    /// <returns>The inverted visibility.</returns>
    public static Visibility InvertBool(bool value) =>
        value ? Visibility.Collapsed : Visibility.Visible;
}
