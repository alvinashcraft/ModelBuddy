using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace ModelBuddy.Converters;

/// <summary>
/// Converts connection state (isConnected, isConnecting) to a color brush.
/// Use ConverterParameter="Connecting" to get the connecting state color.
/// </summary>
public sealed class ConnectionStateToBrushConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets the brush for connected state.
    /// </summary>
    public SolidColorBrush ConnectedBrush { get; set; } = new(Colors.LimeGreen);

    /// <summary>
    /// Gets or sets the brush for disconnected state.
    /// </summary>
    public SolidColorBrush DisconnectedBrush { get; set; } = new(Colors.Red);

    /// <summary>
    /// Gets or sets the brush for connecting state.
    /// </summary>
    public SolidColorBrush ConnectingBrush { get; set; } = new(Colors.Orange);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isConnected)
        {
            return isConnected ? ConnectedBrush : DisconnectedBrush;
        }
        return DisconnectedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
