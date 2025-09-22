using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace TickDown.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            bool invert = parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
            bool result = invert ? !boolValue : boolValue;
            return result ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility visibility)
        {
            bool invert = parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
            bool result = visibility == Visibility.Visible;
            return invert ? !result : result;
        }
        return false;
    }
}
