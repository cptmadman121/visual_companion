using System;
using System.IO;
using System.Text.Json;
using TrayVisionPrompt.Configuration; // use core AppConfiguration

namespace TrayVisionPrompt.Avalonia.Configuration;

public class ConfigurationStore
{
    private static readonly object Sync = new();
    private static readonly string ConfigPath;
    private static AppConfiguration _current = new();
    private static bool _loaded;

    static ConfigurationStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "TrayVisionPrompt");
        Directory.CreateDirectory(appFolder);
        ConfigPath = Path.Combine(appFolder, "config.json");
    }

    public ConfigurationStore()
    {
        Load();
    }

    public AppConfiguration Current
    {
        get
        {
            Load();
            return _current;
        }
    }

    public static event EventHandler? ConfigurationChanged;

    public void Load(bool reload = false)
    {
        lock (Sync)
        {
            if (_loaded && !reload)
            {
                return;
            }

            if (!File.Exists(ConfigPath))
            {
                _current = new AppConfiguration();
                SaveInternal();
                _loaded = true;
                return;
            }

            try
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfiguration>(json);
                if (cfg != null)
                {
                    _current = cfg;
                }
                else
                {
                    _current = new AppConfiguration();
                }
            }
            catch
            {
                _current = new AppConfiguration();
            }

            _loaded = true;
        }
    }

    public void Save()
    {
        lock (Sync)
        {
            SaveInternal();
        }

        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void SaveInternal()
    {
        var json = JsonSerializer.Serialize(_current, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
