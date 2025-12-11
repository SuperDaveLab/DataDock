using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace DataDock.Gui.Converters;

/// <summary>
/// Subtracts the second binding value (plus optional offset) from the first to produce a capped height.
/// </summary>
public sealed class HeightDifferenceConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
        {
            return 520d; // sensible default height
        }

        if (values[0] is double total && double.IsFinite(total) &&
            values[1] is double header && double.IsFinite(header))
        {
            var offset = ParseOffset(parameter, culture);
            var result = total - header - offset;
            return result > 0 ? result : 200d;
        }

        return 520d;
    }

    public object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;

    private static double ParseOffset(object? parameter, CultureInfo culture)
    {
        if (parameter is double d)
        {
            return d;
        }

        if (parameter is string s && double.TryParse(s, NumberStyles.Float, culture, out var parsed))
        {
            return parsed;
        }

        return 0d;
    }
}
