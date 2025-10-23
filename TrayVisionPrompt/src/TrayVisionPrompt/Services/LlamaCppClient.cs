using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using TrayVisionPrompt.Configuration;
using TrayVisionPrompt.Models;

namespace TrayVisionPrompt.Services;

public class LlamaCppClient : OpenAiCompatibleClient
{
    private readonly AppConfiguration _configuration;

    public LlamaCppClient(AppConfiguration configuration, ILogger logger)
        : base(configuration, logger)
    {
        _configuration = configuration;
    }

    protected override object BuildPayload(LlmRequest request)
    {
        var content = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["type"] = "text",
                ["text"] = request.Prompt
            }
        };

        if (request.UseVision && !string.IsNullOrEmpty(request.ImageBase64))
        {
            content.Add(new Dictionary<string, object?>
            {
                ["type"] = "image_url",
                ["image_url"] = new Dictionary<string, object?>
                {
                    ["url"] = $"data:image/png;base64,{request.ImageBase64}"
                }
            });
        }
        else if (!string.IsNullOrWhiteSpace(request.OcrText))
        {
            content.Add(new Dictionary<string, object?>
            {
                ["type"] = "text",
                ["text"] = $"OCR-Fallback:\n{request.OcrText}"
            });
        }

        return new
        {
            model = _configuration.Model,
            temperature = _configuration.Temperature,
            max_tokens = _configuration.MaxTokens,
            messages = new[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = content
                }
            }
        };
    }
}
