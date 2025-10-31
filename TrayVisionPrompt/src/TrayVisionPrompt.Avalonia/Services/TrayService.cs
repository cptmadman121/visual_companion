using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using TrayVisionPrompt.Configuration;

namespace TrayVisionPrompt.Avalonia.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private string? _iconAsset;
    private const string DefaultIconAsset = "ollama-companion.ico";
    private readonly ContextMenuStrip _menu = new();
    private readonly System.Windows.Forms.Timer _animTimer = new();
    private int _pulseStep;
    private int _busyCount;
    private Icon? _baseIcon;
    private Icon? _dynamicIcon;

    public event EventHandler? OpenRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? TestRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<PromptShortcutConfiguration>? PromptRequested;

    public TrayService(string? iconAsset)
    {
        _iconAsset = string.IsNullOrWhiteSpace(iconAsset) ? DefaultIconAsset : iconAsset;
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(_iconAsset),
            Visible = false,
            Text = "deskLLM"
        };

        _baseIcon = _notifyIcon.Icon != null ? (Icon)_notifyIcon.Icon.Clone() : null;
        _animTimer.Interval = 300; // ms
        _animTimer.Tick += (_, _) => OnAnimate();
    }

    public void Initialize()
    {
        _notifyIcon.ContextMenuStrip = _menu;
        UpdatePrompts(Array.Empty<PromptShortcutConfiguration>());
        _notifyIcon.Visible = true;
    }

    public void StartBusy()
    {
        try
        {
            if (System.Threading.Interlocked.Increment(ref _busyCount) == 1)
            {
                _pulseStep = 0;
                _animTimer.Start();
                // switch immediately to animated green state without waiting for first tick
                OnAnimate();
            }
        }
        catch { }
    }

    public void StopBusy()
    {
        try
        {
            if (System.Threading.Interlocked.Decrement(ref _busyCount) <= 0)
            {
                _busyCount = 0;
                _animTimer.Stop();
                RestoreBaseIcon();
            }
        }
        catch { }
    }

    public void ShowPending()
    {
        try
        {
            _animTimer.Stop();
            var icon = CreateOverlayIcon(1.0f, pending: true);
            var old = _dynamicIcon;
            _dynamicIcon = icon;
            _notifyIcon.Icon = icon;
            old?.Dispose();
        }
        catch { }
    }

    public void ClearStatus()
    {
        try
        {
            _busyCount = 0;
            _animTimer.Stop();
            RestoreBaseIcon();
        }
        catch { }
    }

    private void OnAnimate()
    {
        try
        {
            _pulseStep = (_pulseStep + 1) % 8;
            var scale = 0.6f + 0.4f * (float)Math.Abs(Math.Sin(_pulseStep * Math.PI / 4.0));
            var icon = CreateOverlayIcon(scale, pending: false);
            var old = _dynamicIcon;
            _dynamicIcon = icon;
            _notifyIcon.Icon = icon;
            old?.Dispose();
        }
        catch { }
    }

    private void RestoreBaseIcon()
    {
        try
        {
            if (_baseIcon == null)
            {
                _baseIcon = LoadIcon(_iconAsset);
            }
            var old = _dynamicIcon;
            _dynamicIcon = null;
            _notifyIcon.Icon = _baseIcon;
            old?.Dispose();
        }
        catch { }
    }

    public void UpdatePrompts(IEnumerable<PromptShortcutConfiguration> prompts)
    {
        var promptList = prompts?.ToList() ?? new List<PromptShortcutConfiguration>();

        _menu.Items.Clear();
        _menu.Items.Add("Open", null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));

        if (promptList.Count > 0)
        {
            _menu.Items.Add(new ToolStripSeparator());
            foreach (var prompt in promptList)
            {
                var text = string.IsNullOrWhiteSpace(prompt.Hotkey)
                    ? prompt.Name
                    : $"{prompt.Name} ({prompt.Hotkey})";
                var item = new ToolStripMenuItem(text);
                item.Click += (_, _) => PromptRequested?.Invoke(this, prompt);
                _menu.Items.Add(item);
            }
            _menu.Items.Add(new ToolStripSeparator());
        }

        _menu.Items.Add("Settings", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add("Test Backend", null, (_, _) => TestRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add("Open Logs", null, (_, _) => OpenLogs());
        _menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
    }

    public void UpdateIcon(string? iconAsset)
    {
        var effective = string.IsNullOrWhiteSpace(iconAsset) ? DefaultIconAsset : iconAsset;
        if (string.Equals(_iconAsset, effective, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var icon = LoadIcon(effective);
            var previous = _notifyIcon.Icon;
            _notifyIcon.Icon = icon;
            previous?.Dispose();
            _iconAsset = effective;
            _baseIcon = icon != null ? (Icon)icon.Clone() : null;
        }
        catch
        {
            // ignore icon load errors to avoid crashing the tray
        }
    }

    private void OpenLogs()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "deskLLM");
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }

    public void Dispose()
    {
        try { _notifyIcon.Visible = false; } catch { }
        _menu.Dispose();
        _animTimer.Stop();
        _dynamicIcon?.Dispose();
        _baseIcon?.Dispose();
        _notifyIcon.Dispose();
    }

    private Icon CreateOverlayIcon(float overlayScale, bool pending)
    {
        var baseIcon = _baseIcon ?? _notifyIcon.Icon ?? LoadIcon(_iconAsset);
        using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            if (baseIcon != null)
            {
                g.DrawIcon(baseIcon, new Rectangle(0, 0, 32, 32));
            }

            var radius = Math.Max(5f, 6.5f * overlayScale);
            var center = new System.Drawing.PointF(25.5f, 25.5f);
            var alpha = (int)(200 + 55 * overlayScale);
            var (r, gC, b) = pending ? (241, 196, 15) : (46, 204, 113); // yellow vs green
            using var dotBrush = new SolidBrush(Color.FromArgb(Math.Min(255, Math.Max(0, alpha)), r, gC, b));
            using var outline = new Pen(Color.White, 1.6f);
            var x = center.X - radius;
            var y = center.Y - radius;
            var d = radius * 2f;
            g.FillEllipse(dotBrush, x, y, d, d);
            g.DrawEllipse(outline, x, y, d, d);
        }

        var handle = bmp.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }


    private static Icon LoadIcon(string? iconAsset)
    {
        var effective = string.IsNullOrWhiteSpace(iconAsset) ? DefaultIconAsset : iconAsset;
        // Try embedded resources first, then files
        var icon = TryLoadIconFromEmbedded(effective) ?? TryLoadIcon(effective);
        return icon ?? (Icon)SystemIcons.Application.Clone();
    }

    private static Icon? TryLoadIcon(string? iconAsset)
    {
        foreach (var path in EnumerateCandidatePaths(iconAsset))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var stream = File.OpenRead(path);
                    return new Icon(stream);
                }
                catch
                {
                    continue;
                }
            }

            if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var bitmap = (Bitmap)Image.FromFile(path);
                    var handle = bitmap.GetHicon();
                    try
                    {
                        using var icon = Icon.FromHandle(handle);
                        return (Icon)icon.Clone();
                    }
                    finally
                    {
                        NativeMethods.DestroyIcon(handle);
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        return null;
    }

    private static Icon? TryLoadIconFromEmbedded(string iconAsset)
    {
        var asm = Assembly.GetExecutingAssembly();
        var candidates = new[]
        {
            iconAsset,
            iconAsset.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ? iconAsset : iconAsset + ".ico",
            iconAsset.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? iconAsset : iconAsset + ".png",
        }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        foreach (var name in asm.GetManifestResourceNames())
        {
            foreach (var cand in candidates)
            {
                if (!name.EndsWith("." + cand.Replace('/', '.').Replace('\\', '.'), StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    using var s = asm.GetManifestResourceStream(name);
                    if (s == null) continue;
                    if (name.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    {
                        return new Icon(s);
                    }
                    if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        using var bmp = (Bitmap)Image.FromStream(s);
                        var h = bmp.GetHicon();
                        try
                        {
                            using var ic = Icon.FromHandle(h);
                            return (Icon)ic.Clone();
                        }
                        finally
                        {
                            NativeMethods.DestroyIcon(h);
                        }
                    }
                }
                catch
                {
                    // ignore and continue
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidatePaths(string? iconAsset)
    {
        if (string.IsNullOrWhiteSpace(iconAsset))
        {
            yield break;
        }

        var hasExtension = Path.HasExtension(iconAsset);
        if (Path.IsPathRooted(iconAsset))
        {
            if (hasExtension)
            {
                yield return iconAsset;
            }
            else
            {
                yield return iconAsset + ".ico";
                yield return iconAsset + ".png";
            }

            yield break;
        }

        var baseDir = AppContext.BaseDirectory;
        if (iconAsset.Contains(Path.DirectorySeparatorChar) || iconAsset.Contains(Path.AltDirectorySeparatorChar))
        {
            var combined = Path.Combine(baseDir, iconAsset);
            if (hasExtension)
            {
                yield return combined;
            }
            else
            {
                yield return combined + ".ico";
                yield return combined + ".png";
            }
            yield break;
        }

        var assetsDir = Path.Combine(baseDir, "Assets");
        if (hasExtension)
        {
            yield return Path.Combine(assetsDir, iconAsset);
        }
        else
        {
            yield return Path.Combine(assetsDir, iconAsset + ".ico");
            yield return Path.Combine(assetsDir, iconAsset + ".png");
        }
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }
}



