// Copyright © 2025 HemSoft

namespace TickDown.Converters;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

/// <summary>
/// Converts a boolean value to a <see cref="Visibility"/> value.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts a boolean value to a <see cref="Visibility"/> value.
    /// </summary>
    /// <param name="value">The boolean value to convert.</param>
    /// <param name="targetType">The target type of the conversion.</param>
    /// <param name="parameter">An optional parameter to invert the conversion.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns>Visibility.Visible if true (or false if inverted), otherwise Visibility.Collapsed.</returns>
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

    /// <summary>
    /// Converts a <see cref="Visibility"/> value back to a boolean value.
    /// </summary>
    /// <param name="value">The visibility value to convert.</param>
    /// <param name="targetType">The target type of the conversion.</param>
    /// <param name="parameter">An optional parameter to invert the conversion.</param>
    /// <param name="language">The language of the conversion.</param>
    /// <returns>True if Visible (or Collapsed if inverted), otherwise false.</returns>
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