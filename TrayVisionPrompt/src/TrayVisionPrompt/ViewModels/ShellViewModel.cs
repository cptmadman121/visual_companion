using System;
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
    private readonly TextWorkflow _textWorkflow;
    private readonly DialogService _dialogService;
    private readonly ConfigurationManager _configurationManager;
    private readonly ILogger<ShellViewModel> _logger;

    public ShellViewModel(
        TrayIconService trayIconService,
        HotkeyService hotkeyService,
        CaptureWorkflow captureWorkflow,
        TextWorkflow textWorkflow,
        DialogService dialogService,
        ConfigurationManager configurationManager,
        ILogger<ShellViewModel> logger)
    {
        _trayIconService = trayIconService;
        _hotkeyService = hotkeyService;
        _captureWorkflow = captureWorkflow;
        _textWorkflow = textWorkflow;
        _dialogService = dialogService;
        _configurationManager = configurationManager;
        _logger = logger;
    }

    public void Initialize()
    {
        _trayIconService.Initialize();
        _trayIconService.PromptRequested += async (_, shortcut) => await ExecutePromptAsync(shortcut);
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

        RegisterHotkeys();
        _trayIconService.UpdatePrompts(_configurationManager.CurrentConfiguration.PromptShortcuts);
    }

    private void RegisterHotkeys()
    {
        var config = _configurationManager.CurrentConfiguration;
        _hotkeyService.Clear();

        foreach (var shortcut in config.PromptShortcuts)
        {
            if (string.IsNullOrWhiteSpace(shortcut.Hotkey))
            {
                continue;
            }

            var localShortcut = shortcut;
            if (!_hotkeyService.TryRegister(localShortcut.Hotkey, () => _ = ExecutePromptAsync(localShortcut)))
            {
                _logger.LogWarning("Unable to register hotkey {Hotkey}.", localShortcut.Hotkey);
                System.Windows.MessageBox.Show($"Der Hotkey {localShortcut.Hotkey} konnte nicht registriert werden.",
                    "deskLLM", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private async Task ExecutePromptAsync(PromptShortcutConfiguration shortcut)
    {
        try
        {
            switch (shortcut.Activation)
            {
                case PromptActivationMode.CaptureScreen:
                    await _captureWorkflow.ExecuteAsync(shortcut);
                    break;
                default:
                    await _textWorkflow.ExecuteAsync(shortcut);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Prompt execution failed");
            System.Windows.MessageBox.Show($"Die Ausführung des Hotkeys ist fehlgeschlagen: {ex.Message}",
                "deskLLM", MessageBoxButton.OK, MessageBoxImage.Error);
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
                "deskLLM", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenSettings()
    {
        _dialogService.ShowSettingsDialog();
        _configurationManager.Load();
        RegisterHotkeys();
        _trayIconService.UpdatePrompts(_configurationManager.CurrentConfiguration.PromptShortcuts);
    }

    private void CopyLastResponse()
    {
        _trayIconService.CopyLastResponseToClipboard();
    }

    private void OpenLogs()
    {
        _trayIconService.OpenLogsFolder();
    }

}

