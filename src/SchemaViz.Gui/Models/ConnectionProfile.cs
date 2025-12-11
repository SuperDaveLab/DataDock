using System;
using System.Text.Json.Serialization;

namespace SchemaViz.Gui.Models;

public sealed class ConnectionProfile
{
    public string Schema { get; init; } = "dbo";
    public string Host { get; init; } = "localhost";
    public string? Port { get; init; }
    public string Database { get; init; } = string.Empty;
    public bool UseIntegratedSecurity { get; init; }
    public bool TrustServerCertificate { get; init; }

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var host = Host;
            if (!string.IsNullOrWhiteSpace(Port))
            {
                host = $"{host},{Port}";
            }

            return $"[{Schema}] {Database} @ {host}";
        }
    }

    public bool TargetsSameDatabase(ConnectionProfile other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(Schema, other.Schema, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Database, other.Database, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Port ?? string.Empty, other.Port ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
