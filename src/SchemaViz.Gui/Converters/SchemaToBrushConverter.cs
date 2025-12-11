using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SchemaViz.Gui.Converters;

public sealed class SchemaToBrushConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var schema = (value as string ?? string.Empty).Trim().ToLowerInvariant();

		// Add simple palette mapping. Defaults to a neutral gray.
		return schema switch
		{
			"dbo" => new SolidColorBrush(Color.Parse("#1D4ED8")),
			"ops" => new SolidColorBrush(Color.Parse("#059669")),
			"sales" => new SolidColorBrush(Color.Parse("#C026D3")),
			_ => new SolidColorBrush(Color.Parse("#4B5563"))
		};
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

