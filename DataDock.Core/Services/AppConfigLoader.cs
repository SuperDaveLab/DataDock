using System.Text.Json;
using DataDock.Core.Models;

namespace DataDock.Core.Services;

public static class AppConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
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
        yield return Path.Combine(Directory.GetCurrentDirectory(), "datadock.config.json");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            yield return Path.Combine(home, ".datadock", "config.json");
        }

        yield return "/etc/datadock/config.json";
    }
}
