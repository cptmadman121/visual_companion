using System;
using System.IO;
using DeskLLM.Avalonia.Configuration;

namespace DeskLLM.Avalonia.Services;

public sealed class ClipboardLogService : IDisposable
{
    private readonly ConfigurationStore _store;
    private readonly string _logPath;
    private readonly object _sync = new();
    private bool _disposed;

    public ClipboardLogService(ConfigurationStore store)
    {
        _store = store;
        _logPath = ResolveLogPath();
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
        ConfigurationStore.ConfigurationChanged += OnConfigurationChanged;
        WriteHeader("Clipboard logger initialised");
    }

    public string LogFilePath => _logPath;

    public void Log(string message)
    {
        if (_disposed || !_store.Current.EnableClipboardLogging)
        {
            return;
        }

        var line = $"{DateTimeOffset.Now:O} {message}";
        lock (_sync)
        {
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        ConfigurationStore.ConfigurationChanged -= OnConfigurationChanged;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnConfigurationChanged(object? sender, EventArgs e)
    {
        if (_store.Current.EnableClipboardLogging)
        {
            WriteHeader("Clipboard logging enabled");
        }
        else
        {
            WriteHeader("Clipboard logging disabled");
        }
    }

    private void WriteHeader(string message)
    {
        var line = $"{DateTimeOffset.Now:O} --- {message} ---";
        lock (_sync)
        {
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
    }

    private static string ResolveLogPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "deskLLM", "logs");
        return Path.Combine(folder, "clipboard.log");
    }
}
