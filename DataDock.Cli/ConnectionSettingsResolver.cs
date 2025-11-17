using DataDock.Core.Models;

namespace DataDock.Cli;

internal static class ConnectionSettingsResolver
{
    public static ConnectionSettings Resolve(
        CliOptions options,
        ImportProfile profile,
        AppConfig appConfig)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (appConfig == null) throw new ArgumentNullException(nameof(appConfig));

        var connectionString = FirstNonEmpty(
            options.ConnectionString,
            profile.TableConnectionString,
            appConfig.Database.DefaultConnectionString);

        var schema = FirstNonEmpty(
            options.DatabaseSchema,
            profile.TableSchema,
            appConfig.Database.DefaultSchema,
            "dbo");

        return new ConnectionSettings(connectionString, schema!);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}

internal sealed record ConnectionSettings(string? ConnectionString, string Schema);
