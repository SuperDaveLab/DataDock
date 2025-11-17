using DataDock.Cli;
using DataDock.Core.Models;

namespace DataDock.Tests;

public class ConnectionSettingsResolverTests
{
    [Fact]
    public void CliOverridesProfileAndConfig()
    {
        var options = new CliOptions
        {
            ConnectionString = "cli-conn",
            DatabaseSchema = "cliSchema"
        };

        var profile = new ImportProfile
        {
            TableName = "dbo.Tickets",
            TableConnectionString = "profile-conn",
            TableSchema = "profileSchema"
        };

        var config = new AppConfig
        {
            Database = new DatabaseConfig
            {
                DefaultConnectionString = "config-conn",
                DefaultSchema = "configSchema"
            }
        };

        var settings = ConnectionSettingsResolver.Resolve(options, profile, config);

        Assert.Equal("cli-conn", settings.ConnectionString);
        Assert.Equal("cliSchema", settings.Schema);
    }

    [Fact]
    public void ProfileOverridesConfigWhenCliMissing()
    {
        var options = new CliOptions();
        var profile = new ImportProfile
        {
            TableName = "Tickets",
            TableConnectionString = "profile-conn",
            TableSchema = "profileSchema"
        };

        var config = new AppConfig
        {
            Database = new DatabaseConfig
            {
                DefaultConnectionString = "config-conn",
                DefaultSchema = "configSchema"
            }
        };

        var settings = ConnectionSettingsResolver.Resolve(options, profile, config);

        Assert.Equal("profile-conn", settings.ConnectionString);
        Assert.Equal("profileSchema", settings.Schema);
    }

    [Fact]
    public void ConfigFallbackUsesDefaults()
    {
        var options = new CliOptions();
        var profile = new ImportProfile
        {
            TableName = "Tickets"
        };

        var config = new AppConfig
        {
            Database = new DatabaseConfig
            {
                DefaultConnectionString = "config-conn",
                DefaultSchema = "configSchema"
            }
        };

        var settings = ConnectionSettingsResolver.Resolve(options, profile, config);

        Assert.Equal("config-conn", settings.ConnectionString);
        Assert.Equal("configSchema", settings.Schema);
    }

    [Fact]
    public void SchemaFallsBackToDbo()
    {
        var options = new CliOptions();
        var profile = new ImportProfile
        {
            TableName = "Tickets"
        };

        var config = new AppConfig();

        var settings = ConnectionSettingsResolver.Resolve(options, profile, config);

        Assert.Equal("dbo", settings.Schema);
    }
}
