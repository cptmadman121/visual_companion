using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TrayVisionPrompt.Services;

public sealed class ForegroundTextService
{
    public async Task<TextCaptureResult> CaptureAsync(CancellationToken cancellationToken = default)
    {
        return await RunStaAsync(() => CaptureInternal(cancellationToken));
    }

    public async Task ReplaceSelectionAsync(IntPtr windowHandle, string replacement, CancellationToken cancellationToken = default)
    {
        await RunStaAsync(() => ReplaceSelectionInternal(windowHandle, replacement), cancellationToken);
    }

    public async Task SetClipboardTextAsync(string text, CancellationToken cancellationToken = default)
    {
        await RunStaAsync(() =>
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
                // ignore clipboard failures
            }
        }, cancellationToken);
    }

    private static TextCaptureResult CaptureInternal(CancellationToken cancellationToken)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return TextCaptureResult.Empty;
        }

        IDataObject? originalData = null;
        try
        {
            originalData = Clipboard.GetDataObject();
        }
        catch
        {
            // unable to read clipboard; continue without original data
        }

        var originalText = TryGetText(originalData);
        var sentinel = Guid.NewGuid().ToString("N");

        try
        {
            Clipboard.SetDataObject(sentinel, true);
        }
        catch
        {
            return TextCaptureResult.Empty;
        }

        SendCopy(foreground);

        string? capturedText = null;
        var start = Environment.TickCount64;
        while (Environment.TickCount64 - start < 1500)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if (!string.Equals(text, sentinel, StringComparison.Ordinal))
                {
                    capturedText = text;
                    break;
                }
            }

            Thread.Sleep(20);
        }

        RestoreClipboard(originalData);

        if (!string.IsNullOrWhiteSpace(capturedText))
        {
            return new TextCaptureResult(true, capturedText!, originalText, foreground);
        }

        if (!string.IsNullOrWhiteSpace(originalText))
        {
            return new TextCaptureResult(false, originalText!, originalText, foreground);
        }

        return TextCaptureResult.Empty;
    }

    private static void ReplaceSelectionInternal(IntPtr windowHandle, string replacement)
    {
        if (string.IsNullOrEmpty(replacement) || windowHandle == IntPtr.Zero)
        {
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
        }

        try
        {
            Clipboard.SetText(replacement);
        }
        catch
        {
            return;
        }

        BringToForeground(windowHandle);
        SendPaste(windowHandle);

        RestoreClipboard(originalData);
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

    private static void SendCopy(IntPtr windowHandle)
    {
        BringToForeground(windowHandle);
        if (!SendMessage(windowHandle, WM_COPY, IntPtr.Zero, IntPtr.Zero))
        {
            SendCopyShortcut(windowHandle);
        }
    }

    private static void SendPaste(IntPtr windowHandle)
    {
        BringToForeground(windowHandle);
        if (!SendMessage(windowHandle, WM_PASTE, IntPtr.Zero, IntPtr.Zero))
        {
            SendPasteShortcut(windowHandle);
        }
    }

    private static void BringToForeground(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        SetForegroundWindow(hWnd);
    }

    private static void ReleaseActiveModifiers()
    {
        var modifiers = new[] { Keys.ShiftKey, Keys.ControlKey, Keys.Menu, Keys.LWin, Keys.RWin };
        foreach (var key in modifiers)
        {
            keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }

    private static void SendShortcut(Keys modifier, Keys key)
    {
        keybd_event((byte)modifier, 0, 0, UIntPtr.Zero);
        keybd_event((byte)key, 0, 0, UIntPtr.Zero);
        keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event((byte)modifier, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private static void RestoreClipboard(IDataObject? data)
    {
        if (data == null)
        {
            Clipboard.Clear();
            return;
        }

        try
        {
            Clipboard.SetDataObject(data);
        }
        catch
        {
            // ignore restore errors
        }
    }

    private static string? TryGetText(IDataObject? data)
    {
        try
        {
            if (data?.GetDataPresent(DataFormats.Text) == true)
            {
                return data.GetData(DataFormats.Text)?.ToString();
            }
        }
        catch
        {
            // ignore clipboard access errors
        }

        return null;
    }

    private static bool SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        return PostMessage(hWnd, msg, wParam, lParam);
    }

    private const int WM_COPY = 0x0301;
    private const int WM_PASTE = 0x0302;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}

public readonly struct TextCaptureResult
{
    public static TextCaptureResult Empty { get; } = new(false, null, null, IntPtr.Zero);

    public TextCaptureResult(bool hasSelection, string? text, string? fallbackClipboardText, IntPtr windowHandle)
    {
        HasSelection = hasSelection;
        Text = text;
        FallbackClipboardText = fallbackClipboardText;
        WindowHandle = windowHandle;
    }

    public bool HasSelection { get; }
    public string? Text { get; }
    public string? FallbackClipboardText { get; }
    public IntPtr WindowHandle { get; }
}
