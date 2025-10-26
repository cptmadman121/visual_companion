using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace TrayVisionPrompt.Avalonia.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private string? _iconAsset;
    private const string DefaultIconAsset = "ollama-companion.ico";

    public event EventHandler? OpenRequested;
    public event EventHandler? CaptureRequested;
    public event EventHandler? ProofreadRequested;
    public event EventHandler? TranslateRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? TestRequested;
    public event EventHandler? ExitRequested;

    public TrayService(string? iconAsset)
    {
        _iconAsset = string.IsNullOrWhiteSpace(iconAsset) ? DefaultIconAsset : iconAsset;
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(_iconAsset),
            Visible = false,
            Text = "TrayVisionPrompt"
        };
    }

    public void Initialize()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Capture", null, (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Proofread", null, (_, _) => ProofreadRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Translate", null, (_, _) => TranslateRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Settings", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Test Backend", null, (_, _) => TestRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Open Logs", null, (_, _) => OpenLogs());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.Visible = true;
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
        }
        catch
        {
            // ignore icon load errors to avoid crashing the tray
        }
    }

    private void OpenLogs()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "TrayVisionPrompt");
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }

    public void Dispose()
    {
        _notifyIcon.Dispose();
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

