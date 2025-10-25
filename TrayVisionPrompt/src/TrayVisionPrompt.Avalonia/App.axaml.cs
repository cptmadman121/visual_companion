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
    private readonly ConfigurationStore _store = new();

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
                Width = 0,
                Height = 0,
                ShowInTaskbar = false,
                SystemDecorations = SystemDecorations.None
            };
            desktop.MainWindow = hidden;

            _store.Load();
            _tray = new TrayService();
            _tray.Initialize();
            _tray.OpenRequested += (_, _) => ShowMainWindow();
            _tray.CaptureRequested += async (_, _) => await StartCaptureAsync();
            _tray.SettingsRequested += (_, _) => ShowSettings();
            _tray.TestRequested += async (_, _) => await TestBackendAsync();
            _tray.ExitRequested += (_, _) => desktop.Shutdown();

            _hotkey = new WinHotkeyRegistrar();
            if (_hotkey.TryRegister(_store.Current.Hotkey))
            {
                _hotkey.HotkeyPressed += async (_, _) => await StartCaptureAsync();
            }
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
                var ask = new InstructionDialog { Instruction = "Describe the selected region succinctly." };
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
                    var text = await llm.SendAsync(prompt, img, forceVision: true);
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
                var text = await llm.SendAsync("TrayVisionPrompt Backend-Test: Bitte antworte mit 'Bereit'.");
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
}
