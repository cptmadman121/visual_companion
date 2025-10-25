using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayVisionPrompt.Avalonia.Services;

public sealed class WinHotkeyRegistrar : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private int _hotkeyId;

    public event EventHandler? HotkeyPressed;

    public bool TryRegister(string hotkey)
    {
        _hotkeyId = GetHashCode();
        CreateHandle(new CreateParams());

        try
        {
            var (mods, key) = Parse(hotkey);
            return RegisterHotKey(Handle, _hotkeyId, mods, key);
        }
        catch
        {
            return false;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == _hotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        try { UnregisterHotKey(Handle, _hotkeyId); } catch { /* ignore */ }
        DestroyHandle();
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
