using System;
using System.IO;
using System.Text;

namespace TrayVisionPrompt.Avalonia.Services;

public sealed class TranscriptService : IDisposable
{
    private string? _currentPath;
    private StreamWriter? _writer;

    public string? CurrentFile => _currentPath;

    public void StartNewSession()
    {
        DisposeWriter();

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "TrayVisionPrompt", "Transcripts");
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
        if (_writer == null) StartNewSession();
        _writer!.WriteLine(line);
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
    }
}

