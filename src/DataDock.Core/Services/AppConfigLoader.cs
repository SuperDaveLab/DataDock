using System.Text.Json;
using System.Text.Json.Serialization;
using DataDock.Core.Models;

namespace DataDock.Core.Services;

public static class AppConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppConfig Load()
    {
        foreach (var path in GetCandidatePaths())
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions);
                if (config != null)
                {
                    return config;
                }
            }
            catch
            {
                // Ignore malformed configs and continue to the next location.
            }
        }

        return new AppConfig();
    }

    private static IEnumerable<string> GetCandidatePaths()
    {
        foreach (var path in EnumerateWorkingDirectoryAncestry())
        {
            yield return path;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            yield return Path.Combine(home, ".datadock", "config.json");
        }

        yield return "/etc/datadock/config.json";
    }

    private static IEnumerable<string> EnumerateWorkingDirectoryAncestry()
    {
        var current = Directory.GetCurrentDirectory();
        if (string.IsNullOrWhiteSpace(current))
        {
            yield break;
        }

        while (true)
        {
            yield return Path.Combine(current, "datadock.config.json");

            var parent = Directory.GetParent(current);
            if (parent == null)
            {
                yield break;
            }

            current = parent.FullName;
        }
    }
}
