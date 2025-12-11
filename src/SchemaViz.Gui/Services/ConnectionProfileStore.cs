using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SchemaViz.Gui.Models;

namespace SchemaViz.Gui.Services;

public sealed class ConnectionProfileStore
{
    private readonly string _storagePath;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ConnectionProfileStore(string? storagePath = null)
    {
        if (!string.IsNullOrWhiteSpace(storagePath))
        {
            _storagePath = storagePath;
            return;
        }

        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (string.IsNullOrWhiteSpace(baseDir))
        {
            baseDir = Directory.GetCurrentDirectory();
        }

        var folder = Path.Combine(baseDir, "SchemaViz");
        _storagePath = Path.Combine(folder, "connections.json");
    }

    public IReadOnlyList<ConnectionProfile> LoadProfiles()
    {
        try
        {
            if (!File.Exists(_storagePath))
            {
                return Array.Empty<ConnectionProfile>();
            }

            var json = File.ReadAllText(_storagePath);
            var profiles = JsonSerializer.Deserialize<List<ConnectionProfile>>(json, SerializerOptions);
            return profiles ?? new List<ConnectionProfile>();
        }
        catch
        {
            return Array.Empty<ConnectionProfile>();
        }
    }

    public void SaveProfiles(IEnumerable<ConnectionProfile> profiles)
    {
        try
        {
            var directory = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(profiles, SerializerOptions);
            File.WriteAllText(_storagePath, json);
        }
        catch
        {
            // Ignore persistence failures; the UI can continue operating with in-memory profiles.
        }
    }
}
