using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace TrayVisionPrompt.Services;

public class ScreenshotService
{
    private readonly ILogger _logger;

    public ScreenshotService(ILogger logger)
    {
        _logger = logger;
    }

    public string CaptureRegion(Rect rect, double dpiScale)
    {
        var width = Math.Max(1, (int)Math.Round(rect.Width));
        var height = Math.Max(1, (int)Math.Round(rect.Height));

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen((int)Math.Round(rect.X), (int)Math.Round(rect.Y), 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        var base64 = Convert.ToBase64String(stream.ToArray());
        _logger.LogInformation("Captured screenshot of size {Width}x{Height}", rect.Width, rect.Height);
        return base64;
    }

    public string CaptureFullScreen()
    {
        var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new System.Drawing.Size(bounds.Width, bounds.Height), CopyPixelOperation.SourceCopy);

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        var base64 = Convert.ToBase64String(stream.ToArray());
        _logger.LogInformation("Captured full screen of size {Width}x{Height}", bounds.Width, bounds.Height);
        return base64;
    }
}
