using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ModelBuddy.Converters;

/// <summary>
/// Converts a nullable object to Visibility.
/// Returns Visible if the object is not null, Collapsed if null.
/// Use ConverterParameter="True" to invert the logic.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isNull = value is null;
        var invert = parameter is string s && s.Equals("True", StringComparison.OrdinalIgnoreCase);

        if (invert)
        {
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }

        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
