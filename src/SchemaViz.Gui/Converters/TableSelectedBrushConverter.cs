using System;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Data.Converters;

namespace SchemaViz.Gui.Converters;

public sealed class TableSelectedBrushConverter : IValueConverter
{
    public IBrush SelectedBrush { get; set; } = new SolidColorBrush(Color.FromArgb(0xFF, 0x35, 0x4F, 0x70));
    public IBrush DefaultBrush { get; set; } = new SolidColorBrush(Color.FromArgb(0xFF, 0x2B, 0x2F, 0x3A));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected)
        {
            return isSelected ? SelectedBrush : DefaultBrush;
        }

        return DefaultBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
