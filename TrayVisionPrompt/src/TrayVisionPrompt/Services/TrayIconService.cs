using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TrayVisionPrompt.Configuration;
using TrayVisionPrompt.Infrastructure;
using TrayVisionPrompt.Models;

namespace TrayVisionPrompt.Services;

public class TrayIconService : IDisposable
{
    private readonly IServiceLocator _serviceLocator;
    private readonly NotifyIcon _notifyIcon;
    private readonly ResponseCache _responseCache;
    private readonly ContextMenuStrip _menu = new();

    public event EventHandler<PromptShortcutConfiguration>? PromptRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? TestBackendRequested;
    public event EventHandler? CopyLastResponseRequested;
    public event EventHandler? OpenLogsRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService(IServiceLocator serviceLocator)
    {
        _serviceLocator = serviceLocator;
        _responseCache = serviceLocator.Resolve<ResponseCache>();
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Visible = false,
            Text = "TrayVisionPrompt"
        };
    }

    public void Initialize()
    {
        _notifyIcon.ContextMenuStrip = _menu;
        UpdatePrompts(Array.Empty<PromptShortcutConfiguration>());
        _notifyIcon.Visible = true;
    }

    public void UpdatePrompts(IEnumerable<PromptShortcutConfiguration> prompts)
    {
        var promptList = prompts?.ToList() ?? new List<PromptShortcutConfiguration>();

        _menu.Items.Clear();

        foreach (var prompt in promptList)
        {
            var text = string.IsNullOrWhiteSpace(prompt.Hotkey)
                ? prompt.Name
                : $"{prompt.Name} ({prompt.Hotkey})";
            var item = new ToolStripMenuItem(text);
            item.Click += (_, _) =>
            {
                PromptRequested?.Invoke(this, prompt);
            };
            _menu.Items.Add(item);
        }

        if (promptList.Count > 0)
        {
            _menu.Items.Add(new ToolStripSeparator());
        }

        _menu.Items.Add("Settings", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add("Test Backend", null, (_, _) => TestBackendRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add("Copy Last Response", null, (_, _) => CopyLastResponseRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add("Open Logs", null, (_, _) => OpenLogsRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
    }

    public void CopyLastResponseToClipboard()
    {
        if (_responseCache.LastResponse == null)
        {
            System.Windows.Forms.MessageBox.Show("Keine Antwort verfügbar.", "TrayVisionPrompt", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        System.Windows.Forms.Clipboard.SetText(_responseCache.LastResponse.Text);
    }

    public void OpenLogsFolder()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "TrayVisionPrompt");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    public void Dispose()
    {
        _notifyIcon.Dispose();
    }

    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            var accent = Color.FromArgb(0x24, 0x6B, 0xC1);
            using var backgroundBrush = new SolidBrush(Color.FromArgb(230, accent));
            graphics.FillEllipse(backgroundBrush, new Rectangle(2, 2, 28, 28));

            using var pen = new Pen(Color.White, 2.8f);
            var rect = new RectangleF(8, 8, 16, 16);
            graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

            using var arrowPen = new Pen(Color.White, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawLine(arrowPen, new PointF(12, 12), new PointF(20, 20));
            graphics.DrawLine(arrowPen, new PointF(16, 20), new PointF(20, 20));
            graphics.DrawLine(arrowPen, new PointF(20, 16), new PointF(20, 20));
        }

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

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }
}
