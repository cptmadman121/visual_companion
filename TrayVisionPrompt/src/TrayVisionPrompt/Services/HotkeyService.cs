using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace TrayVisionPrompt.Services;

public class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private readonly ILogger _logger;
    private readonly Dictionary<int, Action?> _callbacks = new();
    private HwndSource? _source;
    private int _nextId = 1;

    public event EventHandler? HotkeyPressed;

    public HotkeyService(ILogger logger)
    {
        _logger = logger;
    }

    public bool TryRegister(string hotkeyString, Action? callback = null)
    {
        try
        {
            var hotkey = HotkeyParser.Parse(hotkeyString);
            EnsureSource();
            if (_source == null)
            {
                return false;
            }

            var id = _nextId++;
            if (!RegisterHotKey(_source.Handle, id, hotkey.Modifiers, hotkey.VirtualKeyCode))
            {
                _logger.LogWarning("RegisterHotKey failed for {Hotkey}", hotkeyString);
                return false;
            }

            _callbacks[id] = callback;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register hotkey {Hotkey}", hotkeyString);
            return false;
        }
    }

    public void Clear()
    {
        if (_source == null)
        {
            _callbacks.Clear();
            return;
        }

        foreach (var id in _callbacks.Keys)
        {
            UnregisterHotKey(_source.Handle, id);
        }
        _callbacks.Clear();
    }

    private void EnsureSource()
    {
        if (_source != null)
        {
            return;
        }

        if (Application.Current.MainWindow == null)
        {
            throw new InvalidOperationException("Main window is not available for hotkey registration.");
        }

        _source = HwndSource.FromHwnd(new WindowInteropHelper(Application.Current.MainWindow).Handle);
        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_callbacks.TryGetValue(id, out var callback))
            {
                try
                {
                    callback?.Invoke();
                    if (callback == null)
                    {
                        HotkeyPressed?.Invoke(this, EventArgs.Empty);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing hotkey callback");
                }
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Clear();
        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
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
