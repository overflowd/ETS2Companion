using System.Text.Json;
using ETS2CompanionLauncher.Models;

namespace ETS2CompanionLauncher.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string ConfigDirectory { get; }
    public string ConfigPath { get; }

    public ConfigService(string? baseDirectory = null)
    {
        ConfigDirectory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ETS2CompanionLauncher");
        ConfigPath = Path.Combine(ConfigDirectory, "config.json");
    }

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (config is not null)
                {
                    config.Apps ??= new List<CompanionApp>();
                    config.MigrateLegacyFieldsIfNeeded();

                    if (config.PollIntervalMs < 500 || config.PollIntervalMs > 30000)
                    {
                        config.PollIntervalMs = 2000;
                    }

                    return config;
                }
            }
        }
        catch
        {
            // Fall through to default config if the file is unreadable or corrupt.
        }

        var defaultConfig = AppConfig.CreateDefault();
        Save(defaultConfig);
        return defaultConfig;
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        var tempPath = ConfigPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, ConfigPath, overwrite: true);
    }
}
