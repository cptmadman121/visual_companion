using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
#if WINDOWS
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
#endif

namespace TrayVisionPrompt.Services;

public class OcrService
{
    private readonly ILogger _logger;

    public OcrService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<string?> TryExtractTextAsync(string base64Png)
    {
        try
        {
#if WINDOWS
            var bytes = Convert.FromBase64String(base64Png);
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
            var bitmap = await decoder.GetSoftwareBitmapAsync();
            var engine = Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
            if (engine == null)
            {
                _logger.LogWarning("OCR engine could not be created.");
                return null;
            }

            var result = await engine.RecognizeAsync(bitmap);
            return string.IsNullOrWhiteSpace(result?.Text) ? null : result.Text;
#else
            await Task.CompletedTask;
            return null;
#endif
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR extraction failed");
            return null;
        }
    }
}
