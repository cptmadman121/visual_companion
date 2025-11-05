using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TrayVisionPrompt.Configuration;

public class ConfigurationManager
{
    private readonly string _configPath;
    private ILogger _logger;

    public AppConfiguration CurrentConfiguration { get; private set; } = new();

    public ConfigurationManager(string appFolder, ILogger logger)
    {
        _configPath = Path.Combine(appFolder, "config.json");
        _logger = logger;
    }

    public void UpdateLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void Load()
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogInformation("Configuration file not found. Creating default at {Path}", _configPath);
            CurrentConfiguration = new AppConfiguration();
            CurrentConfiguration.EnsureDefaults();
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<AppConfiguration>(json);
            if (config != null)
            {
                config.EnsureDefaults();
                CurrentConfiguration = config;
            }
            else
            {
                CurrentConfiguration = new AppConfiguration();
                CurrentConfiguration.EnsureDefaults();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration. Using defaults");
            CurrentConfiguration = new AppConfiguration();
            CurrentConfiguration.EnsureDefaults();
        }
    }

    public void Save()
    {
        CurrentConfiguration.SyncLegacyHotkeys();
        var json = JsonSerializer.Serialize(CurrentConfiguration, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_configPath, json);
    }
}
