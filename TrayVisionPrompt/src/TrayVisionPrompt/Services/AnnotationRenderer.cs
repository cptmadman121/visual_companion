using System;
using System.IO;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TrayVisionPrompt.Services;

public static class AnnotationRenderer
{
    public static string Render(string base64Png, StrokeCollection strokes, int width, int height)
    {
        return Render(base64Png, strokes, width, height, 0, 0);
    }

    public static string Render(string base64Png, StrokeCollection strokes, int width, int height, double offsetX, double offsetY)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
        using (var stream = new MemoryStream(Convert.FromBase64String(base64Png)))
        {
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            bitmap = new WriteableBitmap(frame);
        }

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(bitmap, new System.Windows.Rect(0, 0, width, height));
            if (offsetX != 0 || offsetY != 0)
            {
                dc.PushTransform(new TranslateTransform(-offsetX, -offsetY));
                strokes.Draw(dc);
                dc.Pop();
            }
            else
            {
                strokes.Draw(dc);
            }
        }

        var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        renderTarget.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderTarget));
        using var outputStream = new MemoryStream();
        encoder.Save(outputStream);
        return Convert.ToBase64String(outputStream.ToArray());
    }
}
