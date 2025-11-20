using System;
using System.IO;
using DataDock.Core.Models;
using DataDock.Core.Services;

namespace DataDock.Tests;

public class AppConfigLoaderTests
{
    [Fact]
    public void Load_FindsConfigInParentDirectory()
    {
        var originalCwd = Directory.GetCurrentDirectory();
        var tempRoot = Path.Combine(Path.GetTempPath(), "datadock-tests", Guid.NewGuid().ToString("N"));
        var childDir = Path.Combine(tempRoot, "nested", "deep");
        Directory.CreateDirectory(childDir);

        var configPath = Path.Combine(tempRoot, "datadock.config.json");
        File.WriteAllText(configPath, """
        {
          "defaults": {
            "columnNameStyle": "PascalCase"
          }
        }
        """);

        try
        {
            Directory.SetCurrentDirectory(childDir);
            var config = AppConfigLoader.Load();
            Assert.Equal(ColumnNameStyle.PascalCase, config.Defaults.ColumnNameStyle);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
