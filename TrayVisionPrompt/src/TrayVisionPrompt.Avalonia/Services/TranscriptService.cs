using System;
using System.IO;
using System.Text;
using TrayVisionPrompt.Avalonia.Configuration;

namespace TrayVisionPrompt.Avalonia.Services;

public sealed class TranscriptService : IDisposable
{
    private readonly ConfigurationStore _store;
    private string? _currentPath;
    private StreamWriter? _writer;

    public string? CurrentFile => _currentPath;

    public TranscriptService(ConfigurationStore store)
    {
        _store = store;
        ConfigurationStore.ConfigurationChanged += OnConfigurationChanged;
    }

    public void StartNewSession()
    {
        DisposeWriter();

        if (!_store.Current.KeepTranscripts)
        {
            _currentPath = null;
            return;
        }

        var dir = ResolveTranscriptDirectory();
        Directory.CreateDirectory(dir);

        var name = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _currentPath = Path.Combine(dir, $"session_{name}.txt");

        _writer = new StreamWriter(new FileStream(_currentPath, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false))
        {
            AutoFlush = true
        };
        _writer.WriteLine($"# Transcript - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _writer.WriteLine("# ----");
    }

    public void Append(string line)
    {
        if (!_store.Current.KeepTranscripts)
        {
            return;
        }

        if (_writer == null)
        {
            StartNewSession();
        }

        _writer?.WriteLine(line);
    }

    private void DisposeWriter()
    {
        if (_writer != null)
        {
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }
    }

    public void Dispose()
    {
        DisposeWriter();
        ConfigurationStore.ConfigurationChanged -= OnConfigurationChanged;
    }

    private static string ResolveTranscriptDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "deskLLM", "logs", "transcripts");
    }

    private void OnConfigurationChanged(object? sender, EventArgs e)
    {
        if (!_store.Current.KeepTranscripts)
        {
            DisposeWriter();
            _currentPath = null;
        }
    }
}
