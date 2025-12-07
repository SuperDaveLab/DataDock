using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace DataDock.Gui.Converters;

/// <summary>
/// Simple helper to flip boolean bindings in XAML when avoiding extra triggers.
/// </summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public static readonly InverseBooleanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool flag ? !flag : AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool flag ? !flag : AvaloniaProperty.UnsetValue;
    }
}
