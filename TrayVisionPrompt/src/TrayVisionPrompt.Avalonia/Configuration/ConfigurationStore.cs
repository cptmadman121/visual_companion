using System;
using System.IO;
using System.Text.Json;
using TrayVisionPrompt.Configuration; // use core AppConfiguration

namespace TrayVisionPrompt.Avalonia.Configuration;

public class ConfigurationStore
{
    private readonly string _configPath;
    public AppConfiguration Current { get; private set; } = new();

    public ConfigurationStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "TrayVisionPrompt");
        Directory.CreateDirectory(appFolder);
        _configPath = Path.Combine(appFolder, "config.json");
    }

    public void Load()
    {
        if (!File.Exists(_configPath)) { Save(); return; }
        try
        {
            var json = File.ReadAllText(_configPath);
            var cfg = JsonSerializer.Deserialize<AppConfiguration>(json);
            if (cfg != null) Current = cfg;
        }
        catch
        {
            Current = new AppConfiguration();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }
}
