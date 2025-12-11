using System;
using System.Globalization;
using DataDock.Core.Models;

namespace DataDock.Core.Services;

public static class ValueConverter
{
    public static ValueConversionResult Convert(FieldType fieldType, string? raw)
    {
        // Treat null/empty as "no value"
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ValueConversionResult.Ok(null);
        }

        raw = raw.Trim();

        try
        {
            switch (fieldType)
            {
                case FieldType.String:
                    return ValueConversionResult.Ok(raw);

                case FieldType.Int:
                    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                        return ValueConversionResult.Ok(i);
                    return ValueConversionResult.Fail($"Cannot parse '{raw}' as Int.");

                case FieldType.Decimal:
                    if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                        return ValueConversionResult.Ok(d);
                    return ValueConversionResult.Fail($"Cannot parse '{raw}' as Decimal.");

                case FieldType.Bool:
                    if (TryParseBool(raw, out var b))
                        return ValueConversionResult.Ok(b);
                    return ValueConversionResult.Fail($"Cannot parse '{raw}' as Bool.");

                case FieldType.DateTime:
                    if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
                        return ValueConversionResult.Ok(dt);
                    // Try a few common formats explicitly, if needed later
                    return ValueConversionResult.Fail($"Cannot parse '{raw}' as DateTime.");

                default:
                    return ValueConversionResult.Fail($"Unsupported field type {fieldType}.");
            }
        }
        catch (Exception ex)
        {
            return ValueConversionResult.Fail($"Exception parsing '{raw}' as {fieldType}: {ex.Message}");
        }
    }

    private static bool TryParseBool(string raw, out bool value)
    {
        var s = raw.Trim().ToLowerInvariant();

        switch (s)
        {
            case "true":
            case "t":
            case "yes":
            case "y":
            case "1":
                value = true;
                return true;

            case "false":
            case "f":
            case "no":
            case "n":
            case "0":
                value = false;
                return true;

            default:
                value = false;
                return false;
        }
    }
}
