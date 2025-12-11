using DataDock.Core.Models;

namespace DataDock.Cli;

internal static class WriteModeParser
{
    public static bool TryParse(string? value, out WriteMode mode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            mode = WriteMode.Insert;
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "insert":
                mode = WriteMode.Insert;
                return true;
            case "truncate-insert":
            case "truncateinsert":
            case "truncate":
                mode = WriteMode.TruncateInsert;
                return true;
            case "upsert":
                mode = WriteMode.Upsert;
                return true;
            default:
                mode = WriteMode.Insert;
                return false;
        }
    }
}
