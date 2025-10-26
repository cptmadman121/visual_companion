using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using TrayVisionPrompt.Avalonia.Configuration;
using TrayVisionPrompt.Configuration; // core AppConfiguration

namespace TrayVisionPrompt.Avalonia.ViewModels;

public class SettingsViewModel
{
    private readonly ConfigurationStore _store = new();

    public string Hotkey { get; set; } = "Ctrl+Shift+S";
    public string ProofreadHotkey { get; set; } = "Ctrl+Shift+P";
    public string TranslateHotkey { get; set; } = "Ctrl+Shift+T";
    public string Backend { get; set; } = "ollama";
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3";
    public int TimeoutMs { get; set; } = 60000;
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.7;
    public bool UseVision { get; set; }
    public bool UseOcrFallback { get; set; }
    public string? Proxy { get; set; }
    public string Language { get; set; } = "English";
    public IReadOnlyList<string> Languages { get; } = new[] { "English", "German" };
    public IReadOnlyList<IconOption> AvailableIcons { get; }
    public IconOption? SelectedIcon { get; set; }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public SettingsViewModel()
    {
        AvailableIcons = LoadAvailableIcons();
        _store.Load();
        FromConfig(_store.Current);

        SaveCommand = new RelayCommand(_ => Save());
        CancelCommand = new RelayCommand(_ => { /* window closes itself */ });
    }

    private void FromConfig(AppConfiguration c)
    {
        Hotkey = c.Hotkey;
        ProofreadHotkey = c.ProofreadHotkey;
        TranslateHotkey = c.TranslateHotkey;
        Backend = c.Backend;
        Endpoint = c.Endpoint;
        Model = c.Model;
        TimeoutMs = c.RequestTimeoutMs;
        MaxTokens = c.MaxTokens;
        Temperature = c.Temperature;
        UseVision = c.UseVision;
        UseOcrFallback = c.UseOcrFallback;
        Proxy = c.Proxy;
        Language = string.IsNullOrWhiteSpace(c.Language) ? "English" : c.Language;
        SelectedIcon = AvailableIcons
            .FirstOrDefault(o => string.Equals(o.Key, c.IconAsset, StringComparison.OrdinalIgnoreCase))
            ?? AvailableIcons.FirstOrDefault();
        if (SelectedIcon is null && AvailableIcons.Count > 0)
        {
            SelectedIcon = AvailableIcons[0];
        }
    }

    public void Save()
    {
        _store.Current.Hotkey = Hotkey;
        _store.Current.ProofreadHotkey = ProofreadHotkey;
        _store.Current.TranslateHotkey = TranslateHotkey;
        _store.Current.Backend = Backend;
        _store.Current.Endpoint = Endpoint;
        _store.Current.Model = Model;
        _store.Current.RequestTimeoutMs = TimeoutMs;
        _store.Current.MaxTokens = MaxTokens;
        _store.Current.Temperature = Temperature;
        _store.Current.UseVision = UseVision;
        _store.Current.UseOcrFallback = UseOcrFallback;
        _store.Current.Proxy = Proxy;
        _store.Current.Language = Language;
        _store.Current.IconAsset = SelectedIcon?.Key ?? string.Empty;
        _store.Save();
    }

    private static IReadOnlyList<IconOption> LoadAvailableIcons()
    {
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
        var options = new List<IconOption>
        {
            new IconOption(null, "Use application default")
        };

        try
        {
            Directory.CreateDirectory(assetsDir);

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(assetsDir, "*.png"))
            {
                names.Add(Path.GetFileNameWithoutExtension(file));
            }

            foreach (var file in Directory.EnumerateFiles(assetsDir, "*.ico"))
            {
                names.Add(Path.GetFileNameWithoutExtension(file));
            }

            foreach (var name in names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                options.Add(new IconOption(name, name));
            }
        }
        catch
        {
            // ignore directory enumeration errors and fall back to default option only
        }

        return options;
    }

    public sealed class IconOption
    {
        public IconOption(string? key, string label)
        {
            Key = key;
            Label = label;
        }

        public string? Key { get; }
        public string Label { get; }

        public override string ToString() => Label;
    }
}
