using System;

namespace DeskLLM.Avalonia.Services;

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

    public static string BuildForSelection(string? selectionText, string? extra, string? fallbackLanguage)
    {
        var detected = LanguageDetector.Detect(selectionText) ?? (fallbackLanguage ?? "English");
        return Build(detected, extra);
    }

    public static string BuildForInstruction(string? instructionText, string? extra, string? fallbackLanguage)
    {
        var detected = LanguageDetector.Detect(instructionText) ?? (fallbackLanguage ?? "English");
        return Build(detected, extra);
    }
}
