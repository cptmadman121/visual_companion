using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using DeskLLM.Avalonia.Configuration;

namespace DeskLLM.Avalonia.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private string? _iconAsset;
    private const string DefaultIconAsset = "ollama-companion.ico";
    private readonly ContextMenuStrip _menu = new();
    private readonly System.Windows.Forms.Timer _animTimer = new();
    private double _edgeProgress;
    private int _busyCount;
    private Icon? _baseIcon;
    private Icon? _dynamicIcon;
    private bool _pendingMode;
    private bool _flashMode;
    private int _flashLoops;
    private long _flashStartMs;
    private const int FlashLoopDurationMs = 1000;

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
        _animTimer.Interval = 120; // ms
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
                _pendingMode = false;
                _flashMode = false;
                _flashLoops = 0;
                _edgeProgress = 0;
                _animTimer.Start();
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
                _pendingMode = false;
                _flashMode = false;
                _flashLoops = 0;
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
            _busyCount = 0;
            _pendingMode = true;
            _flashMode = false;
            _flashLoops = 0;
            _edgeProgress = 0;
            _animTimer.Start();
            OnAnimate();
        }
        catch { }
    }

    public void ClearStatus()
    {
        try
        {
            _busyCount = 0;
            _pendingMode = false;
            _flashMode = false;
            _flashLoops = 0;
            _animTimer.Stop();
            RestoreBaseIcon();
        }
        catch { }
    }

    public void FlashCompleted()
    {
        try
        {
            _busyCount = 0;
            _pendingMode = false;
            _flashMode = true;
            _flashLoops = 3;
            _flashStartMs = Environment.TickCount64;
            _edgeProgress = 0;
            _animTimer.Start();
            OnAnimate();
        }
        catch { }
    }

    public void RefreshShellIcon()
    {
        try
        {
            var wasPending = _dynamicIcon != null && _busyCount == 0;
            var previousCurrent = _notifyIcon.Icon;
            var previousBase = _baseIcon;
            var previousDynamic = _dynamicIcon;
            _animTimer.Stop();
            _dynamicIcon?.Dispose();
            _dynamicIcon = null;

            var icon = LoadIcon(_iconAsset);
            _notifyIcon.Icon = icon;
            _notifyIcon.Visible = true;
            _baseIcon = icon != null ? (Icon)icon.Clone() : null;

            if (!ReferenceEquals(previousCurrent, icon) && !ReferenceEquals(previousCurrent, previousBase) && !ReferenceEquals(previousCurrent, previousDynamic))
            {
                previousCurrent?.Dispose();
            }
            if (!ReferenceEquals(previousBase, _baseIcon) && !ReferenceEquals(previousBase, previousCurrent))
            {
                previousBase?.Dispose();
            }

            if (_busyCount > 0)
            {
                _pendingMode = false;
                _flashMode = false;
                _flashLoops = 0;
                _edgeProgress = 0;
                _animTimer.Start();
                OnAnimate();
            }
            else if (wasPending || _pendingMode)
            {
                ShowPending();
            }
        }
        catch { }
    }

    private void OnAnimate()
    {
        try
        {
            if (_flashMode)
            {
                var elapsed = Math.Max(0, Environment.TickCount64 - _flashStartMs);
                var completedLoops = (int)(elapsed / FlashLoopDurationMs);
                if (completedLoops >= _flashLoops)
                {
                    _flashMode = false;
                    _animTimer.Stop();
                    RestoreBaseIcon();
                    return;
                }

                var loopProgress = (elapsed % FlashLoopDurationMs) / (double)FlashLoopDurationMs;
                var pulse = 0.5 + 0.5 * Math.Sin(loopProgress * Math.PI * 4); // small-big-small-big over 1s
                var iconFlash = CreateOverlayIcon(pulse, pending: true, flash: true);
                var oldFlash = _dynamicIcon;
                _dynamicIcon = iconFlash;
                _notifyIcon.Icon = iconFlash;
                oldFlash?.Dispose();
                return;
            }

            _edgeProgress = (_edgeProgress + 0.08) % 1.0;
            var icon = CreateOverlayIcon(_edgeProgress, pending: _pendingMode || _busyCount == 0, flash: false);
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

    private Icon CreateOverlayIcon(double progress, bool pending, bool flash)
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

            if (flash)
            {
                var pulse = Math.Max(0f, Math.Min(1f, (float)progress));
                var baseInset = 3f;
                var inset = baseInset + (1.2f * (1f - pulse));
                var rect = new RectangleF(inset, inset, 32 - inset * 2, 32 - inset * 2);
                var alpha = 255;
                var (r, gC, b) = (52, 152, 219); // blue
                using var pen = new Pen(Color.FromArgb(alpha, r, gC, b), 3.2f + 1.4f * pulse)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }
            else
            {
                var inset = 3f;
                var rect = new RectangleF(inset, inset, 32 - inset * 2, 32 - inset * 2);
                var alpha = 230;
                var (r, gC, b) = pending ? (241, 196, 15) : (46, 204, 113); // yellow vs green
                using var pen = new Pen(Color.FromArgb(alpha, r, gC, b), 3.4f)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };

                var perimeter = 2 * (rect.Width + rect.Height);
                var start = (float)(perimeter * progress);
                var length = perimeter * 0.35f;
                DrawPerimeterSegment(g, rect, start, length, pen);
            }
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

    private static void DrawPerimeterSegment(Graphics g, RectangleF rect, float start, float length, Pen pen)
    {
        var perimeter = 2 * (rect.Width + rect.Height);
        float remaining = length;
        float pos = Wrap(start, perimeter);

        while (remaining > 0.01f)
        {
            GetEdge(pos, rect, out var edgeStart, out var direction, out var edgeRemaining);
            var take = Math.Min(remaining, edgeRemaining);
            var p1 = edgeStart;
            var p2 = new PointF(edgeStart.X + direction.X * take, edgeStart.Y + direction.Y * take);
            g.DrawLine(pen, p1, p2);

            pos = Wrap(pos + take, perimeter);
            remaining -= take;
        }
    }

    private static void GetEdge(float pos, RectangleF rect, out PointF start, out PointF direction, out float remaining)
    {
        var top = rect.Width;
        var right = top + rect.Height;
        var bottom = right + rect.Width;

        if (pos < top)
        {
            start = new PointF(rect.Left + pos, rect.Top);
            direction = new PointF(1, 0);
            remaining = top - pos;
            return;
        }

        if (pos < right)
        {
            var offset = pos - top;
            start = new PointF(rect.Right, rect.Top + offset);
            direction = new PointF(0, 1);
            remaining = rect.Height - offset;
            return;
        }

        if (pos < bottom)
        {
            var offset = pos - right;
            start = new PointF(rect.Right - offset, rect.Bottom);
            direction = new PointF(-1, 0);
            remaining = rect.Width - offset;
            return;
        }

        var leftOffset = pos - bottom;
        start = new PointF(rect.Left, rect.Bottom - leftOffset);
        direction = new PointF(0, -1);
        remaining = rect.Height - leftOffset;
    }

    private static float Wrap(float value, float max) => value >= max ? value - max : value < 0 ? value + max : value;


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



