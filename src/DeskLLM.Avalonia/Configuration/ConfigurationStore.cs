using System;
using System.IO;
using System.Text.Json;

namespace DeskLLM.Avalonia.Configuration;

public class ConfigurationStore
{
    private static readonly object Sync = new();
    private static readonly string ConfigPath;
    private static AppConfiguration _current = new();
    private static bool _loaded;

    static ConfigurationStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "deskLLM");
        Directory.CreateDirectory(appFolder);
        ConfigPath = Path.Combine(appFolder, "config.json");
        try
        {
            var oldFolder = Path.Combine(appData, "DeskLLM");
            var oldConfig = Path.Combine(oldFolder, "config.json");
            if (!File.Exists(ConfigPath) && File.Exists(oldConfig))
            {
                File.Copy(oldConfig, ConfigPath, overwrite: false);
            }
        }
        catch { /* ignore migration issues */ }
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
                _current.EnsureDefaults();
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
                    cfg.EnsureDefaults();
                    _current = cfg;
                }
                else
                {
                    _current = new AppConfiguration();
                    _current.EnsureDefaults();
                }
            }
            catch
            {
                _current = new AppConfiguration();
                _current.EnsureDefaults();
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
        _current.SyncLegacyHotkeys();
        var json = JsonSerializer.Serialize(_current, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
