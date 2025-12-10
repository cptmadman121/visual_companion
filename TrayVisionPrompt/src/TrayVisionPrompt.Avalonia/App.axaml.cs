using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Win32;
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
    private ClipboardLogService? _clipboardLog;
    private ForegroundTextService? _textService;
    private LocalApiServer? _api;
    private DateTimeOffset _lastClipboardReset = DateTimeOffset.MinValue;
    private readonly TimeSpan _clipboardResetCooldown = TimeSpan.FromSeconds(45);
    private DispatcherTimer? _healthTimer;
    private readonly TimeSpan _healthInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _trayRefreshInterval = TimeSpan.FromMinutes(30);
    private DateTimeOffset _lastTrayRefresh = DateTimeOffset.MinValue;
    private DateTimeOffset _lastInteraction = DateTimeOffset.Now;
    private readonly TimeSpan _clipboardIdleAutotestThreshold = TimeSpan.FromHours(1);
    private bool _isRestarting;
    private bool _disposed;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += (_, __) => Cleanup();

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
            InitializeClipboardServices();
            hidden.Icon = IconProvider.LoadWindowIcon(_store.Current.IconAsset);

            ConfigurationStore.ConfigurationChanged += OnConfigurationChanged;

            _tray = new TrayService(_store.Current.IconAsset);
            _tray.Initialize();
            _tray.OpenRequested += (_, _) =>
            {
                RecordInteraction();
                ShowMainWindow();
            };
            _tray.SettingsRequested += (_, _) =>
            {
                RecordInteraction();
                ShowSettings();
            };
            _tray.TestRequested += async (_, _) =>
            {
                if (!await RunIdleClipboardAutotestAsync(nameof(TestBackendAsync)))
                {
                    _tray?.ClearStatus();
                    return;
                }
                RecordInteraction();
                _tray?.ShowPending();
                await TestBackendAsync();
            };
            _tray.ExitRequested += (_, _) =>
            {
                Cleanup();
                desktop.Shutdown();
            };
            _tray.PromptRequested += async (_, shortcut) =>
            {
                _tray?.ShowPending();
                await ExecutePromptAsync(shortcut, useClipboardFallback: true);
            };
            _tray.UpdatePrompts(_store.Current.PromptShortcuts);

            _hotkey = new WinHotkeyRegistrar();
            RegisterHotkeys();

            // Start local API server for browser extensions
            _api = new LocalApiServer(27124);
            _api.Start();

            SubscribeSystemEvents();
            StartHealthMonitor();
            _ = RunHealthMaintenanceAsync(force: true);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeClipboardServices()
    {
        _clipboardLog = new ClipboardLogService(_store);
        _textService = new ForegroundTextService(_clipboardLog);
        _textService.ClipboardFaulted += OnClipboardFaulted;
    }

    private void ShowMainWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var win = new MainWindow { DataContext = new MainViewModel() };
            win.Show();
        });
    }

    private void OnClipboardFaulted(object? sender, EventArgs e)
    {
        var now = DateTimeOffset.Now;
        if (now - _lastClipboardReset < _clipboardResetCooldown)
        {
            _clipboardLog?.Log("Clipboard fault detected; reset skipped due to cooldown");
            return;
        }

        _lastClipboardReset = now;
        Dispatcher.UIThread.Post(ReinitializeClipboardServices);
    }

    private void ReinitializeClipboardServices()
    {
        if (_disposed)
        {
            return;
        }

        _clipboardLog?.Log("Reinitializing clipboard services after fault");
        DisposeClipboardServices();
        InitializeClipboardServices();
        _clipboardLog?.Log("Clipboard services reinitialized");
    }

    private void DisposeClipboardServices()
    {
        if (_textService != null)
        {
            _textService.ClipboardFaulted -= OnClipboardFaulted;
        }
        _textService = null;
        _clipboardLog?.Dispose();
        _clipboardLog = null;
    }

    private void SubscribeSystemEvents()
    {
        try { SystemEvents.PowerModeChanged += OnPowerModeChanged; } catch { }
        try { SystemEvents.SessionSwitch += OnSessionSwitch; } catch { }
    }

    private void UnsubscribeSystemEvents()
    {
        try { SystemEvents.PowerModeChanged -= OnPowerModeChanged; } catch { }
        try { SystemEvents.SessionSwitch -= OnSessionSwitch; } catch { }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _clipboardLog?.Log("Power resume detected; refreshing tray and clipboard services");
            Dispatcher.UIThread.Post(() => _ = RunHealthMaintenanceAsync(force: true));
        }
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.SessionLogon)
        {
            _clipboardLog?.Log("Session unlock detected; refreshing tray and clipboard services");
            Dispatcher.UIThread.Post(() => _ = RunHealthMaintenanceAsync(force: true));
        }
    }

    private void StartHealthMonitor()
    {
        _healthTimer?.Stop();
        if (_healthTimer != null)
        {
            _healthTimer.Tick -= OnHealthTimerTick;
        }

        _healthTimer = new DispatcherTimer { Interval = _healthInterval };
        _healthTimer.Tick += OnHealthTimerTick;
        _healthTimer.Start();
    }

    private async void OnHealthTimerTick(object? sender, EventArgs e) => await RunHealthMaintenanceAsync();

    private async System.Threading.Tasks.Task RunHealthMaintenanceAsync(bool force = false)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_textService == null)
            {
                _clipboardLog?.Log("Health monitor: clipboard service missing; rebuilding");
                ReinitializeClipboardServices();
            }
            else if (!await _textService.ProbeClipboardAsync())
            {
                _clipboardLog?.Log("Health monitor: clipboard probe failed; rebuilding services");
                ReinitializeClipboardServices();
            }

            RefreshTrayIconIfNeeded(force);
        }
        catch (Exception ex)
        {
            _clipboardLog?.Log($"Health monitor error: {ex.Message}");
        }
    }

    private void RefreshTrayIconIfNeeded(bool force)
    {
        if (_tray == null)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        if (!force && now - _lastTrayRefresh < _trayRefreshInterval)
        {
            return;
        }

        _lastTrayRefresh = now;
        _tray.RefreshShellIcon();
    }

    private async System.Threading.Tasks.Task<bool> RunIdleClipboardAutotestAsync(string context)
    {
        if (_disposed)
        {
            return false;
        }

        var now = DateTimeOffset.Now;
        var idleDuration = now - _lastInteraction;
        if (idleDuration < _clipboardIdleAutotestThreshold)
        {
            RecordInteraction();
            return true;
        }

        _clipboardLog?.Log($"Idle auto-test triggered after {idleDuration.TotalMinutes:F1} minutes before {context}");
        if (!EnsureClipboardServiceAvailable(context))
        {
            _clipboardLog?.Log("Idle auto-test: clipboard service unavailable; restarting application");
            RestartApplication();
            return false;
        }

        var passed = await _textService!.SelfTestClipboardAsync();
        if (passed)
        {
            _clipboardLog?.Log("Idle auto-test succeeded");
            RecordInteraction();
            return true;
        }

        _clipboardLog?.Log("Idle auto-test failed; restarting application");
        RestartApplication();
        return false;
    }

    private void RecordInteraction() => _lastInteraction = DateTimeOffset.Now;

    private void Cleanup()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        ConfigurationStore.ConfigurationChanged -= OnConfigurationChanged;
        UnsubscribeSystemEvents();

        if (_healthTimer != null)
        {
            _healthTimer.Tick -= OnHealthTimerTick;
            _healthTimer.Stop();
        }
        _healthTimer = null;

        try { _hotkey?.Dispose(); } catch { }
        try { _tray?.Dispose(); } catch { }
        try { _api?.Dispose(); } catch { }
        DisposeClipboardServices();
    }

    private bool EnsureClipboardServiceAvailable(string context)
    {
        if (_textService != null)
        {
            return true;
        }

        _clipboardLog?.Log($"{context}: clipboard service unavailable; rebuilding");
        ReinitializeClipboardServices();
        return _textService != null;
    }

    private async System.Threading.Tasks.Task StartCaptureAsync(PromptShortcutConfiguration? shortcut = null, bool skipInstructionDialog = false)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var annot = new AnnotationWindow();
            await annot.ShowDialog(GetOwnerWindow());

            if (annot.CapturedImageBase64 is string img && !string.IsNullOrWhiteSpace(img))
            {
                var baseInstruction = string.IsNullOrWhiteSpace(shortcut?.Prompt)
                    ? PromptShortcutConfiguration.DefaultCapturePrompt
                    : shortcut!.Prompt;
                var instruction = baseInstruction;

                if (!skipInstructionDialog)
                {
                    var ask = new InstructionDialog { Instruction = baseInstruction };
                    if (!string.IsNullOrWhiteSpace(shortcut?.Name))
                    {
                        ask.Title = shortcut!.Name;
                    }
                    ask.SetThumbnail(img);
                    await ask.ShowDialog(GetOwnerWindow());
                    if (!ask.Confirmed)
                    {
                        _tray?.ClearStatus();
                        return;
                    }
                    instruction = ask.Instruction;
                }

                _tray?.ShowPending();
                ResponseDialog? resp = null;
                var wantsDialog = shortcut?.ShowResponseDialog ?? true;
                if (wantsDialog)
                {
                    resp = new ResponseDialog { ResponseText = "Analyzing selection... contacting backend..." };
                    _ = resp.ShowDialog(GetOwnerWindow());
                }

                var flashCompleted = false;
                try
                {
                    _tray?.StartBusy();
                    string? ocr = null;
                    if (_store.Current.UseOcrFallback)
                    {
                        var ocrSvc = new OcrService();
                        ocr = await ocrSvc.TryExtractTextAsync(img);
                    }
                    var prompt = string.IsNullOrWhiteSpace(ocr) ? instruction : $"{instruction}\n\nOCR-Fallback:\n{ocr}";
                    using var llm = new LlmService();
                    var sys = SystemPromptBuilder.BuildForInstruction(instruction, null, _store.Current.Language);
                    var text = await llm.SendAsync(prompt, img, forceVision: true, systemPrompt: sys);
                    text = TextUtilities.TrimTrailingNewlines(text);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        if (resp is not null)
                        {
                            resp.ResponseText = "(empty response)";
                        }
                        else
                        {
                            await ShowMessageAsync("(empty response)");
                        }
                    }
                    else if (resp is not null)
                    {
                        resp.ResponseText = text;
                        flashCompleted = true;
                    }
                    else
                    {
                        if (!EnsureClipboardServiceAvailable(nameof(StartCaptureAsync)))
                        {
                            _tray?.ClearStatus();
                            await ShowMessageAsync("Clipboard services are restarting. Please try again in a moment.");
                            return;
                        }

                        await _textService!.SetClipboardTextAsync(text);
                        flashCompleted = true;
                    }
                }
                catch (Exception ex)
                {
                    if (resp is not null)
                    {
                        resp.ResponseText = $"Error: {ex.Message}";
                    }
                    else
                    {
                        await ShowMessageAsync($"Error: {ex.Message}");
                    }
                }
                finally
                {
                    _tray?.StopBusy();
                    if (flashCompleted)
                    {
                        _tray?.FlashCompleted();
                    }
                }
            }
            else
            {
                _tray?.ClearStatus();
                return;
            }
        });
    }

    private async System.Threading.Tasks.Task ExecutePromptAsync(PromptShortcutConfiguration shortcut, bool useClipboardFallback = false)
    {
        if (!await RunIdleClipboardAutotestAsync(nameof(ExecutePromptAsync)))
        {
            _tray?.ClearStatus();
            return;
        }

        _tray?.ShowPending();
        switch (shortcut.Activation)
        {
            case PromptActivationMode.CaptureScreen:
                await StartCaptureAsync(shortcut);
                break;
            case PromptActivationMode.CaptureScreenFast:
                await StartCaptureAsync(shortcut, skipInstructionDialog: true);
                break;
            case PromptActivationMode.ForegroundSelection:
                if (useClipboardFallback)
                {
                    await RunClipboardPromptAsync(shortcut, showResponseDialog: shortcut.ShowResponseDialog);
                }
                else
                {
                    await RunForegroundPromptAsync(shortcut);
                }
                break;
            case PromptActivationMode.TextDialog:
                if (useClipboardFallback)
                {
                    await RunClipboardPromptAsync(shortcut, showResponseDialog: shortcut.ShowResponseDialog);
                }
                else
                {
                    await RunTextDialogPromptAsync(shortcut);
                }
                break;
        }
    }

    private async System.Threading.Tasks.Task RunForegroundPromptAsync(PromptShortcutConfiguration shortcut)
    {
        if (!EnsureClipboardServiceAvailable(nameof(RunForegroundPromptAsync)))
        {
            _tray?.ClearStatus();
            await ShowMessageAsync("Clipboard services are restarting. Please try again in a moment.");
            return;
        }

        if (await _textService!.IsRocketChatForegroundAsync())
        {
            await RunClipboardPromptAsync(shortcut, showResponseDialog: shortcut.ShowResponseDialog);
            return;
        }

        // Capture on a background STA first to avoid stealing focus from the target app
        var capture = await _textService!.CaptureAsync();
        if (string.IsNullOrWhiteSpace(capture.Text))
        {
            _tray?.ClearStatus();
            await ShowMessageAsync("No text selection or clipboard text found.");
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var flashCompleted = false;
            try
            {
                _tray?.ShowPending();
                _tray?.StartBusy();
                var response = await ExecuteTextPromptAsync(capture.Text!, shortcut);
                response = TextUtilities.TrimTrailingNewlines(response ?? string.Empty);
                if (IsTranslateShortcut(shortcut))
                {
                    var sanitized = TextUtilities.SanitizeTranslationStrict(response, capture.Text!);
                    response = string.IsNullOrWhiteSpace(sanitized) ? response : sanitized;
                }
                if (string.IsNullOrWhiteSpace(response))
                {
                    await ShowMessageAsync("(empty response)");
                    return;
                }

                var shouldCopyToClipboard = !shortcut.ShowResponseDialog || !capture.HasSelection;
                if (shouldCopyToClipboard)
                {
                    await _textService!.SetClipboardTextAsync(response);
                }

                if (capture.HasSelection)
                {
                    await _textService!.ReplaceSelectionAsync(capture.WindowHandle, response);
                }
                flashCompleted = true;
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"Error: {ex.Message}");
            }
            finally
            {
                _tray?.StopBusy();
                if (flashCompleted)
                {
                    _tray?.FlashCompleted();
                }
            }
        });
    }

    private async System.Threading.Tasks.Task RunTextDialogPromptAsync(PromptShortcutConfiguration shortcut)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (!EnsureClipboardServiceAvailable(nameof(RunTextDialogPromptAsync)))
            {
                _tray?.ClearStatus();
                await ShowMessageAsync("Clipboard services are restarting. Please try again in a moment.");
                return;
            }

            var owner = GetOwnerWindow();
            var dialog = new TextPromptDialog
            {
                Title = string.IsNullOrWhiteSpace(shortcut.Name) ? "Prompt" : shortcut.Name,
                InputText = shortcut.Prefill ?? string.Empty
            };
            var input = await dialog.ShowDialog<string?>(owner);
            if (string.IsNullOrWhiteSpace(input))
            {
                _tray?.ClearStatus();
                return;
            }

            _tray?.ShowPending();
            var flashCompleted = false;
            try
            {
                _tray?.StartBusy();
                var response = await ExecuteTextPromptAsync(input, shortcut);
                response = TextUtilities.TrimTrailingNewlines(response ?? string.Empty);
                if (string.IsNullOrWhiteSpace(response))
                {
                    await ShowMessageAsync("(empty response)");
                    return;
                }

                await _textService!.SetClipboardTextAsync(response);
                if (shortcut.ShowResponseDialog)
                {
                    var dlg = new ResponseDialog { ResponseText = response };
                    await dlg.ShowDialog(owner);
                }
                flashCompleted = true;
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"Error: {ex.Message}");
            }
            finally
            {
                _tray?.StopBusy();
                if (flashCompleted)
                {
                    _tray?.FlashCompleted();
                }
            }
        });
    }

    private async System.Threading.Tasks.Task RunClipboardPromptAsync(PromptShortcutConfiguration shortcut, bool showResponseDialog)
    {
        if (!EnsureClipboardServiceAvailable(nameof(RunClipboardPromptAsync)))
        {
            _tray?.ClearStatus();
            await ShowMessageAsync("Clipboard services are restarting. Please try again in a moment.");
            return;
        }

        _clipboardLog?.Log($"Clipboard prompt requested: {shortcut.Name} ({shortcut.Activation})");
        var clipboardText = await _textService!.GetClipboardTextAsync();
        _clipboardLog?.Log(string.IsNullOrEmpty(clipboardText)
            ? "Clipboard read returned empty text"
            : $"Clipboard read returned {clipboardText.Length} characters");
        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            _tray?.ClearStatus();
            await ShowMessageAsync("Clipboard is empty. Copy text before using the tray prompt.");
            return;
        }

        var shouldShowDialog = showResponseDialog && shortcut.ShowResponseDialog;
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var flashCompleted = false;
            try
            {
                _tray?.ShowPending();
                _tray?.StartBusy();
                var response = await ExecuteTextPromptAsync(clipboardText, shortcut);
                response = TextUtilities.TrimTrailingNewlines(response ?? string.Empty);
                if (IsTranslateShortcut(shortcut))
                {
                    var sanitized = TextUtilities.SanitizeTranslationStrict(response, clipboardText);
                    response = string.IsNullOrWhiteSpace(sanitized) ? response : sanitized;
                }
                if (string.IsNullOrWhiteSpace(response))
                {
                    await ShowMessageAsync("(empty response)");
                    return;
                }

                await _textService!.SetClipboardTextAsync(response);
                _clipboardLog?.Log($"Clipboard response written with {response.Length} characters");
                if (shouldShowDialog)
                {
                    var dlg = new ResponseDialog { ResponseText = response };
                    await dlg.ShowDialog(GetOwnerWindow());
                }
                flashCompleted = true;
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"Error: {ex.Message}");
            }
            finally
            {
                _tray?.StopBusy();
                if (flashCompleted)
                {
                    _tray?.FlashCompleted();
                }
            }
        });
    }

    private async System.Threading.Tasks.Task<string?> ExecuteTextPromptAsync(string input, PromptShortcutConfiguration shortcut)
    {
        var tokenBudget = Math.Max(2048, Math.Min(_store.Current.MaxTokens, 8192));
        var chunks = TextChunker.Split(input, tokenBudget);
        if (chunks.Count == 0)
        {
            return string.Empty;
        }

        var isTranslate = IsTranslateShortcut(shortcut);
        var effectivePrompt = string.IsNullOrWhiteSpace(shortcut.Prompt)
            ? (isTranslate
                ? PromptShortcutConfiguration.DefaultTranslatePrompt
                : PromptShortcutConfiguration.DefaultProofreadPrompt)
            : shortcut.Prompt;

        using var llm = new LlmService();
        var combined = new System.Text.StringBuilder();

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            var systemPrompt = SystemPromptBuilder.BuildForSelection(chunk, effectivePrompt, _store.Current.Language);
            var userContent = BuildChunkUserContent(chunk, effectivePrompt, isTranslate, index, chunks.Count);
            var response = await llm.SendAsync(userContent, systemPrompt: systemPrompt);
            response = TextUtilities.TrimTrailingNewlines(response);
            if (!string.IsNullOrWhiteSpace(response))
            {
                combined.Append(response.TrimEnd());
                if (index < chunks.Count - 1)
                {
                    combined.AppendLine();
                }
            }
        }

        return combined.ToString();
    }

    private static string BuildChunkUserContent(string chunk, string? instructions, bool isTranslate, int index, int total)
    {
        var builder = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(instructions))
        {
            builder.AppendLine(instructions.Trim());
            builder.AppendLine();
        }

        if (isTranslate)
        {
            var prefix = total > 1 ? $"Part {index + 1}/{total}. " : string.Empty;
            builder.AppendLine($"{prefix}Translate the following text exactly as instructed above. Return only the translated text.");
            builder.AppendLine();
            builder.AppendLine("Text:");
            builder.AppendLine(chunk);
            return builder.ToString();
        }

        if (total > 1)
        {
            builder.AppendLine($"[Part {index + 1}/{total}] Apply the instructions to this portion only and preserve formatting.");
            builder.AppendLine();
        }

        builder.Append(chunk);
        return builder.ToString();
    }

    private async System.Threading.Tasks.Task TestBackendAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dlg = new ResponseDialog { ResponseText = "Testing backend…" };
            _ = dlg.ShowDialog(GetOwnerWindow());
            _tray?.ShowPending();
            try
            {
                using var llm = new LlmService();
                _tray?.StartBusy();
                var text = await llm.SendAsync("TrayVisionPrompt Backend-Test: Bitte antworte mit 'Bereit'.", systemPrompt: ComposeSystemPrompt());
                dlg.ResponseText = string.IsNullOrWhiteSpace(text) ? "(no response)" : text;
            }
            catch (Exception ex)
            {
                dlg.ResponseText = $"Error: {ex.Message}";
            }
            finally
            {
                _tray?.StopBusy();
            }
        });
    }

    private void ShowSettings()
    {
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var wnd = new SettingsWindow();
                await wnd.ShowDialog(GetOwnerWindow());
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"Settings error: {ex.Message}");
            }
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

    private static bool IsTranslateShortcut(PromptShortcutConfiguration shortcut)
    {
        if (!string.IsNullOrWhiteSpace(shortcut.Name) && shortcut.Name.Contains("Translate", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (!string.IsNullOrWhiteSpace(shortcut.Prompt) && shortcut.Prompt.Contains("translate", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }

    private Window GetOwnerWindow()
    {
        var owner = (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null)
        {
            return owner;
        }
        if (_hiddenWindow is not null)
        {
            return _hiddenWindow;
        }
        var hidden = new Window
        {
            Width = 1,
            Height = 1,
            ShowInTaskbar = false,
            SystemDecorations = SystemDecorations.None,
            Opacity = 0,
            WindowState = WindowState.Minimized
        };
        _hiddenWindow = hidden;
        return hidden;
    }

    private async System.Threading.Tasks.Task ShowMessageAsync(string message)
    {
        var owner = GetOwnerWindow();
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

    private void RestartApplication()
    {
        if (_isRestarting)
        {
            return;
        }

        _isRestarting = true;
        try
        {
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                var args = Environment.GetCommandLineArgs().Skip(1).Select(QuoteArgument);
                var psi = new ProcessStartInfo(exePath)
                {
                    UseShellExecute = false,
                    Arguments = string.Join(' ', args)
                };
                Process.Start(psi);
            }
        }
        catch (Exception ex)
        {
            _clipboardLog?.Log($"RestartApplication failed to launch new process: {ex.Message}");
        }
        finally
        {
            Dispatcher.UIThread.Post(() =>
            {
                Cleanup();
                (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            });
        }
    }

    private static string QuoteArgument(string arg) => arg.Contains(' ') ? $"\"{arg}\"" : arg;
}
