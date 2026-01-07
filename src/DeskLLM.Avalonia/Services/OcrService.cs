using System;
using System.Threading.Tasks;
#if WINDOWS
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
#endif

namespace DeskLLM.Avalonia.Services;

public class OcrService
{
    public async Task<string?> TryExtractTextAsync(string base64Png)
    {
        try
        {
#if WINDOWS
            var bytes = Convert.FromBase64String(base64Png);
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var bitmap = await decoder.GetSoftwareBitmapAsync();
            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (engine == null)
            {
                return null;
            }
            var result = await engine.RecognizeAsync(bitmap);
            return string.IsNullOrWhiteSpace(result?.Text) ? null : result.Text;
#else
            await Task.CompletedTask;
            return null;
#endif
        }
        catch
        {
            return null;
        }
    }
}

