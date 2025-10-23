using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace TrayVisionPrompt.Services;

public class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private readonly ILogger _logger;
    private HwndSource? _source;
    private int _hotkeyId = 0;

    public event EventHandler? HotkeyPressed;

    public HotkeyService(ILogger logger)
    {
        _logger = logger;
    }

    public bool TryRegister(string hotkeyString)
    {
        try
        {
            var hotkey = HotkeyParser.Parse(hotkeyString);
            _hotkeyId = GetHashCode();
            if (Application.Current.MainWindow == null)
            {
                throw new InvalidOperationException("Main window is not available for hotkey registration.");
            }

            _source = HwndSource.FromHwnd(new WindowInteropHelper(Application.Current.MainWindow).Handle);
            _source.AddHook(WndProc);
            if (!RegisterHotKey(_source.Handle, _hotkeyId, hotkey.Modifiers, hotkey.VirtualKeyCode))
            {
                _logger.LogWarning("RegisterHotKey failed for {Hotkey}", hotkeyString);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register hotkey {Hotkey}", hotkeyString);
            return false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source != null)
        {
            UnregisterHotKey(_source.Handle, _hotkeyId);
            _source.RemoveHook(WndProc);
            _source.Dispose();
        }
    }

    private static class HotkeyParser
    {
        public static (uint Modifiers, uint VirtualKeyCode) Parse(string hotkey)
        {
            uint modifiers = 0;
            uint key = 0;
            foreach (var part in hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim().ToLowerInvariant();
                switch (trimmed)
                {
                    case "ctrl":
                    case "control":
                        modifiers |= 0x2;
                        break;
                    case "alt":
                        modifiers |= 0x1;
                        break;
                    case "shift":
                        modifiers |= 0x4;
                        break;
                    case "win":
                    case "windows":
                        modifiers |= 0x8;
                        break;
                    default:
                        key = (uint)KeyInterop.VirtualKeyFromKey((Key)Enum.Parse(typeof(Key), trimmed, true));
                        break;
                }
            }

            if (key == 0)
            {
                throw new ArgumentException("No key specified in hotkey string.");
            }

            return (modifiers, key);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
