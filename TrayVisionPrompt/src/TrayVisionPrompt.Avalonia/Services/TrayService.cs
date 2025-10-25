using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace TrayVisionPrompt.Avalonia.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public event EventHandler? OpenRequested;
    public event EventHandler? CaptureRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? TestRequested;
    public event EventHandler? ExitRequested;

    public TrayService()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Visible = false,
            Text = "TrayVisionPrompt"
        };
    }

    public void Initialize()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Capture", null, (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Settings", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Test Backend", null, (_, _) => TestRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Open Logs", null, (_, _) => OpenLogs());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.Visible = true;
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

