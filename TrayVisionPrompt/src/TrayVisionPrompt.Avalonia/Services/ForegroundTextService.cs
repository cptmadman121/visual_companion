using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace TrayVisionPrompt.Avalonia.Services;

public sealed class ForegroundTextService
{
    private readonly ClipboardLogService? _log;

    public ForegroundTextService(ClipboardLogService? logService = null)
    {
        _log = logService;
    }

    public async Task<TextCaptureResult> CaptureAsync(CancellationToken cancellationToken = default)
    {
        return await RunStaAsync(() => CaptureInternal(cancellationToken, _log));
    }

    public async Task ReplaceSelectionAsync(IntPtr windowHandle, string replacement, CancellationToken cancellationToken = default)
    {
        await RunStaAsync(() => ReplaceSelectionInternal(windowHandle, replacement, _log), cancellationToken);
    }

    public async Task SetClipboardTextAsync(string text, CancellationToken cancellationToken = default)
    {
        await RunStaAsync(() =>
        {
            try
            {
                _log?.Log($"SetClipboardTextAsync: writing {text?.Length ?? 0} characters");
                Clipboard.SetText(NormalizeNewlinesToWindows(text));
            }
            catch (Exception ex)
            {
                _log?.Log($"SetClipboardTextAsync failed: {ex.Message}");
            }
        }, cancellationToken);
    }

    public async Task<string?> GetClipboardTextAsync(CancellationToken cancellationToken = default)
    {
        return await RunStaAsync(() =>
        {
            try
            {
                var hasText = Clipboard.ContainsText();
                var text = hasText ? Clipboard.GetText() : null;
                _log?.Log(hasText
                    ? $"GetClipboardTextAsync: retrieved {text?.Length ?? 0} characters"
                    : "GetClipboardTextAsync: clipboard does not contain text");
                return text;
            }
            catch (Exception ex)
            {
                _log?.Log($"GetClipboardTextAsync failed: {ex.Message}");
                return null;
            }
        });
    }

    public async Task<bool> IsRocketChatForegroundAsync(CancellationToken cancellationToken = default)
    {
        return await RunStaAsync(() =>
        {
            var hwnd = GetForegroundWindow();
            var isRocket = IsRocketChatWindow(hwnd);
            _log?.Log($"IsRocketChatForegroundAsync: window=0x{hwnd.ToInt64():X} rocketChat={isRocket}");
            return isRocket;
        });
    }

    private static TextCaptureResult CaptureInternal(CancellationToken cancellationToken, ClipboardLogService? log)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            log?.Log("CaptureInternal: no foreground window");
            return TextCaptureResult.Empty;
        }

        log?.Log($"CaptureInternal: starting for window 0x{foreground.ToInt64():X}");
        IDataObject? originalData = null;
        try
        {
            originalData = Clipboard.GetDataObject();
        }
        catch
        {
            // unable to read clipboard; continue without original data
            log?.Log("CaptureInternal: unable to read original clipboard");
        }

        var originalText = TryGetText(originalData);
        var sentinel = Guid.NewGuid().ToString("N");

        try
        {
            Clipboard.SetDataObject(sentinel, true);
            log?.Log("CaptureInternal: sentinel placed on clipboard");
        }
        catch
        {
            log?.Log("CaptureInternal: failed to set sentinel");
            return TextCaptureResult.Empty;
        }
        // Attempt a direct copy on the focused control first; then keep nudging with Ctrl+C
        SendCopy(foreground);

        string? capturedText = null;
        var start = Environment.TickCount64;
        var nudgedAt = start;
        var isChromiumLike = IsChromiumLikeWindow(foreground);
        // Chromium/Electron apps sometimes need a bit longer
        long maxWait = isChromiumLike ? 3500 : 2500;
        while (Environment.TickCount64 - start < maxWait)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var data = Clipboard.GetDataObject();
                var maybe = TryGetText(data);
                if (!string.IsNullOrEmpty(maybe) && !string.Equals(maybe, sentinel, StringComparison.Ordinal))
                {
                    capturedText = maybe;
                    log?.Log($"CaptureInternal: captured {capturedText.Length} characters after {Environment.TickCount64 - start} ms");
                    break;
                }
            }
            catch { /* ignore clipboard probe errors */ }

            // Re-issue copy occasionally in case the target app needs a user gesture
            if (Environment.TickCount64 - nudgedAt > 150)
            {
                SendCopyShortcut(foreground);
                if (isChromiumLike)
                {
                    // Some Chromium/Electron windows honor Ctrl+Insert for Copy
                    SendCopyAlternativeShortcut(foreground);
                }
                nudgedAt = Environment.TickCount64;
            }

            Thread.Sleep(30);
        }

        RestoreClipboard(originalData, log);

        if (!string.IsNullOrWhiteSpace(capturedText))
        {
            return new TextCaptureResult(true, capturedText!, originalText, foreground);
        }

        if (!string.IsNullOrWhiteSpace(originalText))
        {
            log?.Log("CaptureInternal: falling back to original clipboard text");
            return new TextCaptureResult(false, originalText!, originalText, foreground);
        }

        log?.Log("CaptureInternal: no text captured");
        return TextCaptureResult.Empty;
    }

    private static void ReplaceSelectionInternal(IntPtr windowHandle, string replacement, ClipboardLogService? log)
    {
        if (string.IsNullOrEmpty(replacement) || windowHandle == IntPtr.Zero)
        {
            log?.Log("ReplaceSelectionInternal: nothing to replace or invalid window");
            return;
        }

        IDataObject? originalData = null;
        try
        {
            originalData = Clipboard.GetDataObject();
        }
        catch
        {
            // ignore clipboard retrieval errors
            log?.Log("ReplaceSelectionInternal: unable to read original clipboard");
        }

        try
        {
            Clipboard.SetText(NormalizeNewlinesToWindows(replacement));
            log?.Log($"ReplaceSelectionInternal: placed {replacement.Length} characters on clipboard");
        }
        catch
        {
            log?.Log("ReplaceSelectionInternal: failed to set replacement text");
            return;
        }

        BringToForeground(windowHandle);
        // Try direct paste first; then keyboard alternative if needed
        SendPaste(windowHandle);

        var isChromiumLike = IsChromiumLikeWindow(windowHandle);
        if (isChromiumLike)
        {
            // Some Chromium/Electron windows honor Shift+Insert for Paste
            SendPasteAlternativeShortcut(windowHandle);
        }

        // Give the target application ample time to process the keyboard paste
        // before restoring the original clipboard contents. Some editors
        // (e.g., Notepad++) process Ctrl+V asynchronously and can read from the
        // clipboard after a noticeable delay. Too short a delay can lead to an
        // empty replacement. Increase to improve reliability.
        var pasteDelay = isChromiumLike ? 2000 : 900;
        Thread.Sleep(pasteDelay);

        RestoreClipboard(originalData, log);
    }

    private static string NormalizeNewlinesToWindows(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var t = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return t.Replace("\n", "\r\n");
    }

    private static async Task<T> RunStaAsync<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>();
        var thread = new Thread(() =>
        {
            try
            {
                var result = action();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return await tcs.Task.ConfigureAwait(false);
    }

    private static async Task RunStaAsync(Action action, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<object?>();
        var thread = new Thread(() =>
        {
            try
            {
                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
        {
            await tcs.Task.ConfigureAwait(false);
        }
    }

    private static void SendCopy(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        BringToForeground(windowHandle);
        // Try sending WM_COPY to the currently focused control
        var target = GetFocusedControl(windowHandle);
        if (target != IntPtr.Zero)
        {
            SendMessage(target, WM_COPY, IntPtr.Zero, IntPtr.Zero);
            Thread.Sleep(10);
        }
        SendCopyShortcut(windowHandle);
    }

    private static void SendPaste(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        BringToForeground(windowHandle);
        var target = GetFocusedControl(windowHandle);
        if (target != IntPtr.Zero)
        {
            SendMessage(target, WM_PASTE, IntPtr.Zero, IntPtr.Zero);
            Thread.Sleep(10);
        }
        SendPasteShortcut(windowHandle);
    }

    private static void SendCopyShortcut(IntPtr windowHandle)
    {
        BringToForeground(windowHandle);
        ReleaseActiveModifiers();
        SendShortcut(Keys.ControlKey, Keys.C);
    }

    private static void SendPasteShortcut(IntPtr windowHandle)
    {
        BringToForeground(windowHandle);
        ReleaseActiveModifiers();
        SendShortcut(Keys.ControlKey, Keys.V);
    }

    private static void SendCopyAlternativeShortcut(IntPtr windowHandle)
    {
        BringToForeground(windowHandle);
        ReleaseActiveModifiers();
        // Ctrl+Insert is an alternative Copy accelerator in many apps
        SendShortcut(Keys.ControlKey, Keys.Insert);
    }

    private static void SendPasteAlternativeShortcut(IntPtr windowHandle)
    {
        BringToForeground(windowHandle);
        ReleaseActiveModifiers();
        // Shift+Insert is an alternative Paste accelerator in many apps
        SendShortcut(Keys.ShiftKey, Keys.Insert);
    }

    private static void SendShortcut(Keys modifier, Keys key)
    {
        var inputs = new INPUT[4];
        inputs[0] = CreateKeyInput((ushort)modifier, false);
        inputs[1] = CreateKeyInput((ushort)key, false);
        inputs[2] = CreateKeyInput((ushort)key, true);
        inputs[3] = CreateKeyInput((ushort)modifier, true);
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        Thread.Sleep(30);
    }

    private static INPUT CreateKeyInput(ushort key, bool keyUp)
    {
        return new INPUT
        {
            type = 1,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = key,
                    wScan = 0,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
                    dwExtraInfo = GetMessageExtraInfo()
                }
            }
        };
    }

    private static void ReleaseActiveModifiers()
    {
        // Best-effort: send key-up for common modifiers to avoid combinations like Ctrl+Shift+C
        var mods = new ushort[]
        {
            (ushort)Keys.ShiftKey,
            (ushort)Keys.LShiftKey,
            (ushort)Keys.RShiftKey,
            (ushort)Keys.ControlKey,
            (ushort)Keys.LControlKey,
            (ushort)Keys.RControlKey,
            (ushort)Keys.Menu, // Alt
            0x5B, // LWin
            0x5C  // RWin
        };

        var inputs = new System.Collections.Generic.List<INPUT>(mods.Length);
        foreach (var vk in mods)
        {
            inputs.Add(CreateKeyInput(vk, true));
        }
        if (inputs.Count > 0)
        {
            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
            Thread.Sleep(10);
        }
    }

    private static void BringToForeground(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var targetThread = GetWindowThreadProcessId(windowHandle, out _);
        if (foregroundThread != targetThread)
        {
            AttachThreadInput(foregroundThread, targetThread, true);
        }

        // Only restore if minimized to avoid changing window size/state (e.g., Notepad++)
        if (IsIconic(windowHandle))
        {
            ShowWindow(windowHandle, 9); // SW_RESTORE
        }
        SetForegroundWindow(windowHandle);
        Thread.Sleep(10);

        if (foregroundThread != targetThread)
        {
            AttachThreadInput(foregroundThread, targetThread, false);
        }
    }

    private static string? TryGetText(IDataObject? data)
    {
        if (data == null)
        {
            return null;
        }

        if (data.GetDataPresent(DataFormats.UnicodeText))
        {
            return data.GetData(DataFormats.UnicodeText) as string;
        }

        if (data.GetDataPresent(DataFormats.Text))
        {
            return data.GetData(DataFormats.Text) as string;
        }

        return null;
    }

    private static bool IsChromiumLikeWindow(IntPtr windowHandle)
    {
        try
        {
            if (windowHandle == IntPtr.Zero)
            {
                return false;
            }
            var threadId = GetWindowThreadProcessId(windowHandle, out var pid);
            if (pid == 0)
            {
                return false;
            }
            using var proc = Process.GetProcessById((int)pid);
            var name = string.Empty;
            var path = string.Empty;
            try { name = (proc.ProcessName ?? string.Empty).ToLowerInvariant(); } catch { }
            try { path = (proc.MainModule?.FileName ?? string.Empty).ToLowerInvariant(); } catch { }

            // Heuristics for Chromium/Electron-based apps and Rocket.Chat
            bool match(string s) =>
                s.Contains("chrome") || s.Contains("electron") || s.Contains("msedge") ||
                s.Contains("brave") || s.Contains("webview") || s.Contains("rocketchat") || s.Contains("rocket.chat");
            return match(name) || match(path);
        }
        catch
        {
            return false;
        }
    }

    private static void RestoreClipboard(IDataObject? data, ClipboardLogService? log)
    {
        if (data == null)
        {
            try
            {
                Clipboard.Clear();
                log?.Log("RestoreClipboard: cleared clipboard (no original data)");
            }
            catch
            {
                // ignore
                log?.Log("RestoreClipboard: failed to clear clipboard");
            }
            return;
        }

        try
        {
            Clipboard.SetDataObject(data, true);
            log?.Log("RestoreClipboard: restored previous clipboard data");
        }
        catch
        {
            // ignore clipboard restore issues
            log?.Log("RestoreClipboard: failed to restore clipboard data");
        }
    }

    private static bool IsRocketChatWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            _ = GetWindowThreadProcessId(hWnd, out var processId);
            if (processId == 0)
            {
                return false;
            }

            using var process = Process.GetProcessById((int)processId);
            var name = process.ProcessName;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            name = name.ToLowerInvariant();
            return name.Contains("rocket") && name.Contains("chat");
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr GetFocusedControl(IntPtr windowHandle)
    {
        uint threadId = GetWindowThreadProcessId(windowHandle, out _);
        var info = new GUITHREADINFO();
        info.cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>();
        if (GetGUIThreadInfo(threadId, ref info))
        {
            return info.hwndFocus != IntPtr.Zero ? info.hwndFocus : windowHandle;
        }
        return windowHandle;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetMessageExtraInfo();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    private const int WM_COPY = 0x0301;
    private const int WM_PASTE = 0x0302;

    private const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }
}

public readonly record struct TextCaptureResult(bool HasSelection, string? Text, string? OriginalClipboardText, IntPtr WindowHandle)
{
    public static TextCaptureResult Empty { get; } = new(false, null, null, IntPtr.Zero);
}
