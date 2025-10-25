using System.Windows.Input;
using TrayVisionPrompt.Avalonia.Configuration;
using TrayVisionPrompt.Configuration; // core AppConfiguration

namespace TrayVisionPrompt.Avalonia.ViewModels;

public class SettingsViewModel
{
    private readonly ConfigurationStore _store = new();

    public string Hotkey { get; set; } = "Ctrl+Shift+Space";
    public string Backend { get; set; } = "ollama";
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3";
    public int TimeoutMs { get; set; } = 60000;
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.7;
    public bool UseVision { get; set; }
    public bool UseOcrFallback { get; set; }
    public string? Proxy { get; set; }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public SettingsViewModel()
    {
        _store.Load();
        FromConfig(_store.Current);

        SaveCommand = new RelayCommand(_ => Save());
        CancelCommand = new RelayCommand(_ => { /* window closes itself */ });
    }

    private void FromConfig(AppConfiguration c)
    {
        Hotkey = c.Hotkey;
        Backend = c.Backend;
        Endpoint = c.Endpoint;
        Model = c.Model;
        TimeoutMs = c.RequestTimeoutMs;
        MaxTokens = c.MaxTokens;
        Temperature = c.Temperature;
        UseVision = c.UseVision;
        UseOcrFallback = c.UseOcrFallback;
        Proxy = c.Proxy;
    }

    public void Save()
    {
        _store.Current.Hotkey = Hotkey;
        _store.Current.Backend = Backend;
        _store.Current.Endpoint = Endpoint;
        _store.Current.Model = Model;
        _store.Current.RequestTimeoutMs = TimeoutMs;
        _store.Current.MaxTokens = MaxTokens;
        _store.Current.Temperature = Temperature;
        _store.Current.UseVision = UseVision;
        _store.Current.UseOcrFallback = UseOcrFallback;
        _store.Current.Proxy = Proxy;
        _store.Save();
    }
}
