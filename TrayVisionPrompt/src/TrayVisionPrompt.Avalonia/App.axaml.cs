using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TrayVisionPrompt.Avalonia.ViewModels;
using TrayVisionPrompt.Avalonia.Views;
using TrayVisionPrompt.Avalonia.Services;
using TrayVisionPrompt.Avalonia.Configuration;

namespace TrayVisionPrompt.Avalonia;

public partial class App : global::Avalonia.Application
{
    private TrayService? _tray;
    private WinHotkeyRegistrar? _hotkey;
    private Window? _hiddenWindow;
    private readonly ConfigurationStore _store = new();
    private readonly ForegroundTextService _textService = new();

    private enum TextWorkflowMode
    {
        Proofread,
        Translate
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Keep app alive with a hidden window; UI opens on-demand via tray/hotkey
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
            _tray.CaptureRequested += async (_, _) => await StartCaptureAsync();
            _tray.ProofreadRequested += async (_, _) => await StartProofreadSilentFlow();
            _tray.TranslateRequested += async (_, _) => await StartTranslateSilentFlow();
            _tray.SettingsRequested += (_, _) => ShowSettings();
            _tray.TestRequested += async (_, _) => await TestBackendAsync();
            _tray.ExitRequested += (_, _) => desktop.Shutdown();

            _hotkey = new WinHotkeyRegistrar();
            RegisterHotkeys();
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

    private async System.Threading.Tasks.Task StartCaptureAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var annot = new AnnotationWindow();
            await annot.ShowDialog((ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow);

            if (annot.CapturedImageBase64 is string img && !string.IsNullOrWhiteSpace(img))
            {
                var ask = new InstructionDialog { Instruction = string.IsNullOrWhiteSpace(_store.Current.CaptureInstruction) ? "Describe the selected region succinctly." : _store.Current.CaptureInstruction };
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

    private async System.Threading.Tasks.Task StartProofreadSilentFlow()
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
                var extra = string.IsNullOrWhiteSpace(_store.Current.ProofreadPrompt) ? ProofreadPrompt : _store.Current.ProofreadPrompt;
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

    private async System.Threading.Tasks.Task StartTranslateSilentFlow()
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
                var extra = string.IsNullOrWhiteSpace(_store.Current.TranslatePrompt) ? TranslatePrompt : _store.Current.TranslatePrompt;
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
        _hotkey.TryRegister(_store.Current.Hotkey, () => _ = StartCaptureAsync());
        _hotkey.TryRegister(_store.Current.ProofreadHotkey, () => _ = StartProofreadSilentFlow());
        _hotkey.TryRegister(_store.Current.TranslateHotkey, () => _ = StartTranslateSilentFlow());
    }

    private async System.Threading.Tasks.Task StartTextWorkflowAsync(TextWorkflowMode mode)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var capture = await _textService.CaptureAsync();
            if (string.IsNullOrWhiteSpace(capture.Text))
            {
                await ShowMessageAsync("No text selection or clipboard text found.");
                return;
            }

            var owner = (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow;
            var status = mode == TextWorkflowMode.Proofread ? "Proofreading text…" : "Translating text…";
            var dialog = new ResponseDialog { ResponseText = status };
            _ = dialog.ShowDialog(owner);

            try
            {
                using var llm = new LlmService();
                var proof = _store.Current.ProofreadPrompt;
                var transl = _store.Current.TranslatePrompt;
                var systemPrompt = ComposeSystemPrompt(mode == TextWorkflowMode.Proofread ? proof : transl);
                var response = await llm.SendAsync(capture.Text!, systemPrompt: systemPrompt);
                if (string.IsNullOrWhiteSpace(response))
                {
                    dialog.ResponseText = "(empty response)";
                    return;
                }

                dialog.ResponseText = response;

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
                dialog.ResponseText = $"Error: {ex.Message}";
            }
        });
    }

    private static readonly string ProofreadPrompt = "Proofread and improve grammar, spelling, and clarity. Preserve tone and meaning. Return only the corrected text. Keep formatting, newlines, tabs etc. exactly as in the original text.";
    private static readonly string TranslatePrompt = "If the text is english, translate it to German. If the text is German, translate it to English. All while preserving meaning, tone, and formatting. Return only the translation.";

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
            if (_hiddenWindow is not null)
            {
                _hiddenWindow.Icon = IconProvider.LoadWindowIcon(_store.Current.IconAsset);
            }
            RegisterHotkeys();
        });
    }
}
