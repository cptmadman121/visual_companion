using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace DeskLLM.Avalonia.ViewModels;

public class MainViewModel
{
    public string Prompt { get; set; } = string.Empty;
    public string Backend { get; set; } = "ollama";
    public string Model { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string MaxTokens { get; set; } = "4096";
    public string Temperature { get; set; } = "0.7";
    public bool UseVision { get; set; }
    public bool UseOcrFallback { get; set; }

    public ObservableCollection<string> Messages { get; } = new();

    public ICommand SendCommand { get; }
    public ICommand CaptureCommand { get; }
    public ICommand NewSessionCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    public MainViewModel()
    {
        SendCommand = new RelayCommand(_ => Send());
        CaptureCommand = new RelayCommand(_ => Capture());
        NewSessionCommand = new RelayCommand(_ => NewSession());
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
    }

    private void Send()
    {
        if (!string.IsNullOrWhiteSpace(Prompt))
        {
            Messages.Add($"You: {Prompt}");
            Messages.Add("Assistant: …");
            Prompt = string.Empty;
        }
    }

    private void Capture()
    {
        Messages.Add("[Capture initiated]");
    }

    private void NewSession()
    {
        Messages.Clear();
    }

    private void OpenSettings()
    {
        Messages.Add("[Open Settings]");
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
