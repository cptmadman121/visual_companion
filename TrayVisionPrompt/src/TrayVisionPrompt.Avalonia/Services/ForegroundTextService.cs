using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TrayVisionPrompt.Avalonia.Services;

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
        SendCopyShortcut(foreground);

        string? capturedText = null;
        var start = Environment.TickCount64;
        while (Environment.TickCount64 - start < 500)
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
        SendPasteShortcut(windowHandle);

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
        SendShortcut(Keys.ControlKey, Keys.C);
    }

    private static void SendPasteShortcut(IntPtr windowHandle)
    {
        BringToForeground(windowHandle);
        SendShortcut(Keys.ControlKey, Keys.V);
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

        ShowWindow(windowHandle, 5);
        SetForegroundWindow(windowHandle);

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

    private static void RestoreClipboard(IDataObject? data)
    {
        if (data == null)
        {
            try
            {
                Clipboard.Clear();
            }
            catch
            {
                // ignore
            }
            return;
        }

        try
        {
            Clipboard.SetDataObject(data, true);
        }
        catch
        {
            // ignore clipboard restore issues
        }
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
}

public readonly record struct TextCaptureResult(bool HasSelection, string? Text, string? OriginalClipboardText, IntPtr WindowHandle)
{
    public static TextCaptureResult Empty { get; } = new(false, null, null, IntPtr.Zero);
}
