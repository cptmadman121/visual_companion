using System;

namespace TrayVisionPrompt.Avalonia.Services;

public static class SystemPromptBuilder
{
    public static string Build(string language, string? extra = null)
    {
        var basePrompt = language.Equals("German", StringComparison.OrdinalIgnoreCase)
            ? "You are TrayVisionPrompt, a helpful assistant that responds in fluent German unless explicitly instructed otherwise."
            : "You are TrayVisionPrompt, a helpful assistant that responds in fluent English unless explicitly instructed otherwise.";

        if (string.IsNullOrWhiteSpace(extra))
        {
            return basePrompt;
        }

        return $"{basePrompt}\n\n{extra}";
    }
}
