using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModelBuddy.Views;

namespace ModelBuddy;

/// <summary>
/// The main application window with navigation.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Navigate to Models page by default
        ContentFrame.Navigate(typeof(ModelsPage));
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem selectedItem)
        {
            var tag = selectedItem.Tag?.ToString();
            switch (tag)
            {
                case "Chat":
                    ContentFrame.Navigate(typeof(ChatPage));
                    break;
                case "Models":
                    ContentFrame.Navigate(typeof(ModelsPage));
                    break;
                case "Logs":
                    ContentFrame.Navigate(typeof(LogsPage));
                    break;
            }
        }
    }
}
