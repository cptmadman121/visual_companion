using System;
using System.IO;

namespace DeskLLM.Avalonia.Services;

public sealed class StartupTutorialService
{
    private readonly string _logFolder;
    private readonly string _stateFilePath;
    private readonly string _errorFilePath;

    public StartupTutorialService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _logFolder = Path.Combine(appData, "deskLLM", "logs");
        _stateFilePath = Path.Combine(_logFolder, "startup-tutorial.log");
        _errorFilePath = Path.Combine(_logFolder, "startup-tutorial-errors.log");
    }

    public bool ShouldRunTutorial()
    {
        try
        {
            return !File.Exists(_stateFilePath);
        }
        catch
        {
            return true;
        }
    }

    public void RecordOutcome(TutorialOutcome outcome, string details)
    {
        try
        {
            Directory.CreateDirectory(_logFolder);
            var line = $"{DateTimeOffset.Now:O}\t{outcome}\t{details}";
            File.AppendAllText(_stateFilePath, line + Environment.NewLine);
        }
        catch
        {
            // Swallow logging errors to keep the tutorial resilient
        }
    }

    public void RecordFailure(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(_logFolder);
            var line = $"{DateTimeOffset.Now:O}\tFAILED\t{ex.GetType().FullName}: {ex.Message}{Environment.NewLine}{ex}";
            File.AppendAllText(_errorFilePath, line + Environment.NewLine);
        }
        catch
        {
            // Ignore
        }
    }
}

public enum TutorialOutcome
{
    Completed,
    Skipped
}
