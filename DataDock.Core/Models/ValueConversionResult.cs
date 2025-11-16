namespace DataDock.Core.Models;

public class ValueConversionResult
{
    public bool Success { get; init; }
    public object? Value { get; init; }
    public string? ErrorMessage { get; init; }

    public static ValueConversionResult Ok(object? value) => new()
    {
        Success = true,
        Value = value
    };

    public static ValueConversionResult Fail(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
}
