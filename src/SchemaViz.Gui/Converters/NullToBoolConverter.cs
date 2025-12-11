using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SchemaViz.Gui.Converters;

public sealed class NullToBoolConverter : IValueConverter
{
    public bool TrueWhenNull { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNull = value is null;
        return TrueWhenNull ? isNull : !isNull;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
