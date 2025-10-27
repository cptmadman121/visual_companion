using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;

namespace TrayVisionPrompt.Avalonia.Services;

public sealed class WinHotkeyRegistrar : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private int _nextId = 1;
    private readonly Dictionary<int, Action> _handlers = new();
    private bool _handleCreated;

    public WinHotkeyRegistrar()
    {
        CreateMessageWindow();
    }

    public bool TryRegister(string hotkey, Action handler)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return false;
        }

        CreateMessageWindow();

        try
        {
            var (mods, key) = Parse(hotkey);
            var id = _nextId++;
            if (RegisterHotKey(Handle, id, mods, key))
            {
                _handlers[id] = handler;
                return true;
            }
            else
            {
                Debug.WriteLine($"RegisterHotKey failed for '{hotkey}' (mods={mods}, key={key})");
            }
        }
        catch
        {
            Debug.WriteLine($"Failed to register hotkey '{hotkey}'");
        }

        return false;
    }

    public void Clear()
    {
        foreach (var id in _handlers.Keys)
        {
            try { UnregisterHotKey(Handle, id); }
            catch { /* ignore */ }
        }
        _handlers.Clear();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            if (_handlers.TryGetValue(m.WParam.ToInt32(), out var handler))
            {
                handler?.Invoke();
            }
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        Clear();
        DestroyHandle();
    }

    private void CreateMessageWindow()
    {
        if (_handleCreated)
        {
            return;
        }

        CreateHandle(new CreateParams());
        _handleCreated = true;
    }

    private static (uint mods, uint key) Parse(string hotkey)
    {
        uint mods = 0; uint key = 0;
        foreach (var part in hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.Trim().ToLowerInvariant())
            {
                case "ctrl":
                case "control": mods |= 0x2; break;
                case "alt": mods |= 0x1; break;
                case "shift": mods |= 0x4; break;
                case "win":
                case "windows": mods |= 0x8; break;
                default:
                    var k = (Keys)Enum.Parse(typeof(Keys), part, true);
                    key = (uint)k;
                    break;
            }
        }
        if (key == 0) throw new ArgumentException("No key specified");
        return (mods, key);
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
