using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TrayVisionPrompt.Configuration;
using TrayVisionPrompt.Services;

namespace TrayVisionPrompt.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly TrayIconService _trayIconService;
    private readonly HotkeyService _hotkeyService;
    private readonly CaptureWorkflow _captureWorkflow;
    private readonly DialogService _dialogService;
    private readonly ConfigurationManager _configurationManager;
    private readonly ILogger<ShellViewModel> _logger;

    public ShellViewModel(
        TrayIconService trayIconService,
        HotkeyService hotkeyService,
        CaptureWorkflow captureWorkflow,
        DialogService dialogService,
        ConfigurationManager configurationManager,
        ILogger<ShellViewModel> logger)
    {
        _trayIconService = trayIconService;
        _hotkeyService = hotkeyService;
        _captureWorkflow = captureWorkflow;
        _dialogService = dialogService;
        _configurationManager = configurationManager;
        _logger = logger;
    }

    public void Initialize()
    {
        _trayIconService.Initialize();
        _trayIconService.HotkeyTriggered += OnTrayHotkeyTriggered;
        _trayIconService.TestBackendRequested += async (_, _) => await TestBackendAsync();
        _trayIconService.SettingsRequested += (_, _) => OpenSettings();
        _trayIconService.CopyLastResponseRequested += (_, _) => CopyLastResponse();
        _trayIconService.OpenLogsRequested += (_, _) => OpenLogs();
        _trayIconService.ExitRequested += (_, _) =>
        {
            var app = System.Windows.Application.Current;
            app.Properties["IsShuttingDown"] = true;
            app.Shutdown();
        };

        RegisterHotkey();
    }

    private void RegisterHotkey()
    {
        var config = _configurationManager.CurrentConfiguration;
        if (!_hotkeyService.TryRegister(config.Hotkey))
        {
            _logger.LogWarning("Unable to register hotkey {Hotkey}.", config.Hotkey);
            System.Windows.MessageBox.Show($"Der Hotkey {config.Hotkey} konnte nicht registriert werden.",
                "TrayVisionPrompt", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        }
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        try
        {
            await _captureWorkflow.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Capture workflow failed");
            System.Windows.MessageBox.Show($"Der Capture-Workflow ist fehlgeschlagen: {ex.Message}",
                "TrayVisionPrompt", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task TestBackendAsync()
    {
        try
        {
            await _captureWorkflow.TestBackendAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backend test failed");
            System.Windows.MessageBox.Show($"Backend-Test fehlgeschlagen: {ex.Message}",
                "TrayVisionPrompt", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenSettings()
    {
        _dialogService.ShowSettingsDialog();
    }

    private void CopyLastResponse()
    {
        _trayIconService.CopyLastResponseToClipboard();
    }

    private void OpenLogs()
    {
        _trayIconService.OpenLogsFolder();
    }

    private void OnTrayHotkeyTriggered(object? sender, EventArgs e)
    {
        OnHotkeyPressed(sender, e);
    }
}
