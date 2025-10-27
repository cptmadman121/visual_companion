using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TrayVisionPrompt.Avalonia.Configuration;
using TrayVisionPrompt.Avalonia.Services;
using TrayVisionPrompt.Avalonia.ViewModels;
using TrayVisionPrompt.Avalonia.Views;
using TrayVisionPrompt.Configuration;

namespace TrayVisionPrompt.Avalonia;

public partial class App : global::Avalonia.Application
{
    private TrayService? _tray;
    private WinHotkeyRegistrar? _hotkey;
    private Window? _hiddenWindow;
    private readonly ConfigurationStore _store = new();
    private readonly ForegroundTextService _textService = new();
    private LocalApiServer? _api;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var hidden = new Window
            {
                Width = 1,
                Height = 1,
                ShowInTaskbar = false,
                SystemDecorations = SystemDecorations.None,
                Opacity = 0,
                WindowState = WindowState.Minimized
            };
            desktop.MainWindow = hidden;
            _hiddenWindow = hidden;

            _store.Load();
            hidden.Icon = IconProvider.LoadWindowIcon(_store.Current.IconAsset);

            ConfigurationStore.ConfigurationChanged += OnConfigurationChanged;

            _tray = new TrayService(_store.Current.IconAsset);
            _tray.Initialize();
            _tray.OpenRequested += (_, _) => ShowMainWindow();
            _tray.SettingsRequested += (_, _) => ShowSettings();
            _tray.TestRequested += async (_, _) => await TestBackendAsync();
            _tray.ExitRequested += (_, _) => desktop.Shutdown();
            _tray.PromptRequested += async (_, shortcut) => await ExecutePromptAsync(shortcut);
            _tray.UpdatePrompts(_store.Current.PromptShortcuts);

            _hotkey = new WinHotkeyRegistrar();
            RegisterHotkeys();

            // Start local API server for browser extensions
            _api = new LocalApiServer(27124);
            _api.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ShowMainWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var win = new MainWindow { DataContext = new MainViewModel() };
            win.Show();
        });
    }

    private async System.Threading.Tasks.Task StartCaptureAsync(PromptShortcutConfiguration? shortcut = null)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var annot = new AnnotationWindow();
            await annot.ShowDialog((ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow);

            if (annot.CapturedImageBase64 is string img && !string.IsNullOrWhiteSpace(img))
            {
                var fallbackInstruction = string.IsNullOrWhiteSpace(_store.Current.CaptureInstruction)
                    ? "Describe the selected region succinctly."
                    : _store.Current.CaptureInstruction;
                var prefill = string.IsNullOrWhiteSpace(shortcut?.Prompt) ? fallbackInstruction : shortcut!.Prompt;
                var ask = new InstructionDialog { Instruction = prefill };
                if (!string.IsNullOrWhiteSpace(shortcut?.Name))
                {
                    ask.Title = shortcut!.Name;
                }
                ask.SetThumbnail(img);
                await ask.ShowDialog((ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow);
                if (!ask.Confirmed) return;

                var resp = new ResponseDialog { ResponseText = "Analyzing selection… contacting backend…" };
                _ = resp.ShowDialog((ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow);

                try
                {
                    string? ocr = null;
                    if (_store.Current.UseOcrFallback)
                    {
                        var ocrSvc = new OcrService();
                        ocr = await ocrSvc.TryExtractTextAsync(img);
                    }
                    var prompt = string.IsNullOrWhiteSpace(ocr) ? ask.Instruction : $"{ask.Instruction}\n\nOCR-Fallback:\n{ocr}";
                    using var llm = new LlmService();
                    var text = await llm.SendAsync(prompt, img, forceVision: true, systemPrompt: ComposeSystemPrompt());
                    resp.ResponseText = string.IsNullOrWhiteSpace(text) ? "(empty response)" : text;
                }
                catch (Exception ex)
                {
                    resp.ResponseText = $"Error: {ex.Message}";
                }
            }
        });
    }

    private async System.Threading.Tasks.Task ExecutePromptAsync(PromptShortcutConfiguration shortcut)
    {
        switch (shortcut.Activation)
        {
            case PromptActivationMode.CaptureScreen:
                await StartCaptureAsync(shortcut);
                break;
            case PromptActivationMode.ForegroundSelection:
                await RunForegroundPromptAsync(shortcut);
                break;
            case PromptActivationMode.TextDialog:
                await RunTextDialogPromptAsync(shortcut);
                break;
        }
    }

    private async System.Threading.Tasks.Task RunForegroundPromptAsync(PromptShortcutConfiguration shortcut)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var capture = await _textService.CaptureAsync();
            if (string.IsNullOrWhiteSpace(capture.Text))
            {
                await ShowMessageAsync("No text selection or clipboard text found.");
                return;
            }

            try
            {
                using var llm = new LlmService();
                var extra = string.IsNullOrWhiteSpace(shortcut.Prompt)
                    ? PromptShortcutConfiguration.DefaultProofreadPrompt
                    : shortcut.Prompt;
                var systemPrompt = ComposeSystemPrompt(extra);
                var response = await llm.SendAsync(capture.Text!, systemPrompt: systemPrompt);
                if (string.IsNullOrWhiteSpace(response))
                {
                    await ShowMessageAsync("(empty response)");
                    return;
                }

                if (capture.HasSelection)
                {
                    await _textService.ReplaceSelectionAsync(capture.WindowHandle, response);
                }
                else
                {
                    await _textService.SetClipboardTextAsync(response);
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"Error: {ex.Message}");
            }
        });
    }

    private async System.Threading.Tasks.Task RunTextDialogPromptAsync(PromptShortcutConfiguration shortcut)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow;
            var dialog = new TextPromptDialog
            {
                Title = string.IsNullOrWhiteSpace(shortcut.Name) ? "Prompt" : shortcut.Name,
                InputText = shortcut.Prefill ?? string.Empty
            };
            var input = await dialog.ShowDialog<string?>(owner);
            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            try
            {
                using var llm = new LlmService();
                var systemPrompt = ComposeSystemPrompt(string.IsNullOrWhiteSpace(shortcut.Prompt) ? null : shortcut.Prompt);
                var response = await llm.SendAsync(input, systemPrompt: systemPrompt);
                if (string.IsNullOrWhiteSpace(response))
                {
                    await ShowMessageAsync("(empty response)");
                    return;
                }

                await _textService.SetClipboardTextAsync(response);
                var dlg = new ResponseDialog { ResponseText = response };
                await dlg.ShowDialog(owner);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"Error: {ex.Message}");
            }
        });
    }

    private async System.Threading.Tasks.Task TestBackendAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dlg = new ResponseDialog { ResponseText = "Testing backend…" };
            _ = dlg.ShowDialog((ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow);
            try
            {
                using var llm = new LlmService();
                var text = await llm.SendAsync("TrayVisionPrompt Backend-Test: Bitte antworte mit 'Bereit'.", systemPrompt: ComposeSystemPrompt());
                dlg.ResponseText = string.IsNullOrWhiteSpace(text) ? "(no response)" : text;
            }
            catch (Exception ex)
            {
                dlg.ResponseText = $"Error: {ex.Message}";
            }
        });
    }

    private void ShowSettings()
    {
        Dispatcher.UIThread.Post(async () =>
        {
            var wnd = new SettingsWindow();
            await wnd.ShowDialog((ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow);
        });
    }

    private void RegisterHotkeys()
    {
        if (_hotkey is null)
        {
            return;
        }

        _hotkey.Clear();
        var failed = new System.Collections.Generic.List<string>();
        foreach (var prompt in _store.Current.PromptShortcuts)
        {
            if (string.IsNullOrWhiteSpace(prompt.Hotkey))
            {
                continue;
            }

            var local = prompt;
            if (!_hotkey.TryRegister(local.Hotkey, () => _ = ExecutePromptAsync(local)))
            {
                failed.Add($"{local.Name} ({local.Hotkey})");
            }
        }

        if (failed.Count > 0)
        {
            _ = ShowMessageAsync("Some hotkeys could not be registered. Change them in Settings.\n" + string.Join("\n", failed));
        }
    }

    private string ComposeSystemPrompt(string? extra = null) => SystemPromptBuilder.Build(_store.Current.Language ?? "English", extra);

    private async System.Threading.Tasks.Task ShowMessageAsync(string message)
    {
        var owner = (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow;
        var dlg = new ResponseDialog { ResponseText = message };
        await dlg.ShowDialog(owner);
    }

    private void OnConfigurationChanged(object? sender, EventArgs e)
    {
        _store.Load(reload: true);

        Dispatcher.UIThread.Post(() =>
        {
            _tray?.UpdateIcon(_store.Current.IconAsset);
            _tray?.UpdatePrompts(_store.Current.PromptShortcuts);
            if (_hiddenWindow is not null)
            {
                _hiddenWindow.Icon = IconProvider.LoadWindowIcon(_store.Current.IconAsset);
            }
            RegisterHotkeys();
        });
    }
}
