using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace DataDock.Gui.Converters;

/// <summary>
/// Converts nullable integers to text for binding to TextBox controls and back again.
/// </summary>
public sealed class NullableIntToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int i
            ? i.ToString(culture)
            : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return int.TryParse(text, NumberStyles.Integer, culture, out var parsed) && parsed > 0
            ? parsed
            : BindingOperations.DoNothing;
    }
}
