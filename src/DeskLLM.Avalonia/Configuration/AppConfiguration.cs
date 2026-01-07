using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DeskLLM.Avalonia.Configuration;

public class AppConfiguration
{
    [JsonPropertyName("hotkey")]
    public string Hotkey { get; set; } = "Ctrl+Shift+S";

    [JsonPropertyName("proofreadHotkey")]
    public string ProofreadHotkey { get; set; } = "Ctrl+Shift+P";

    [JsonPropertyName("translateHotkey")]
    public string TranslateHotkey { get; set; } = "Ctrl+Shift+T";

    [JsonPropertyName("promptShortcuts")]
    public List<PromptShortcutConfiguration> PromptShortcuts { get; set; } = new();

    [JsonPropertyName("language")]
    public string Language { get; set; } = "English";

    [JsonPropertyName("iconAsset")]
    public string IconAsset { get; set; } = string.Empty;

    [JsonPropertyName("backend")]
    public string Backend { get; set; } = "ollama";

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = "http://localhost:11434";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "gemma3:27b";

    [JsonPropertyName("requestTimeoutMs")]
    public int RequestTimeoutMs { get; set; } = 45000;

    [JsonPropertyName("maxTokens")]
    public int MaxTokens { get; set; } = 32000;

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.2;

    [JsonPropertyName("useVision")]
    public bool UseVision { get; set; } = true;

    [JsonPropertyName("useOcrFallback")]
    public bool UseOcrFallback { get; set; } = true;

    [JsonPropertyName("proxy")]
    public string? Proxy { get; set; }

    [JsonPropertyName("telemetry")]
    public bool Telemetry { get; set; } = false;

    [JsonPropertyName("logLevel")]
    public string LogLevel { get; set; } = "Info";

    [JsonPropertyName("enableClipboardLogging")]
    public bool EnableClipboardLogging { get; set; }

    [JsonPropertyName("keepTranscripts")]
    public bool KeepTranscripts { get; set; }

    // Prompt customization
    [JsonPropertyName("proofreadPrompt")]
    public string ProofreadPrompt { get; set; } = "Proofread and improve grammar, spelling, and clarity, while maintaining the original language of the text. Preserve tone and meaning. Keep formatting, newlines, tabs etc. exactly as in the original text. Return only the corrected text.";

    [JsonPropertyName("translatePrompt")]
    public string TranslatePrompt { get; set; } = "If the provided text is not in German, translate it into German. If the provided text is in German, translate it into English. The entire translation process should preserve the tone, structure, and formatting of the original text. Return only the translated text.";

    public void EnsureDefaults()
    {
        if (PromptShortcuts.Count == 0)
        {
            PromptShortcuts = new List<PromptShortcutConfiguration>
            {
                PromptShortcutConfiguration.CreateCapture(
                    "Capture Screen",
                    string.IsNullOrWhiteSpace(Hotkey) ? "Ctrl+Shift+S" : Hotkey,
                    PromptShortcutConfiguration.DefaultCapturePrompt),
                PromptShortcutConfiguration.CreateTextSelection(
                    "Proofread Selection",
                    string.IsNullOrWhiteSpace(ProofreadHotkey) ? "Ctrl+Shift+P" : ProofreadHotkey,
                    string.IsNullOrWhiteSpace(ProofreadPrompt) ? PromptShortcutConfiguration.DefaultProofreadPrompt : ProofreadPrompt),
                PromptShortcutConfiguration.CreateTextSelection(
                    "Translate Selection",
                    string.IsNullOrWhiteSpace(TranslateHotkey) ? "Ctrl+Shift+T" : TranslateHotkey,
                    string.IsNullOrWhiteSpace(TranslatePrompt) ? PromptShortcutConfiguration.DefaultTranslatePrompt : TranslatePrompt),
                PromptShortcutConfiguration.CreateTextSelection(
                    "Anonymize Selection",
                    "Ctrl+Shift+A",
                    PromptShortcutConfiguration.DefaultAnonymizePrompt)
            };
        }

        SyncLegacyHotkeys();
    }

    public void SyncLegacyHotkeys()
    {
        if (PromptShortcuts.Count == 0)
        {
            return;
        }

        var capture = PromptShortcuts.FirstOrDefault(p => p.Activation == PromptActivationMode.CaptureScreen)
            ?? PromptShortcuts.FirstOrDefault(p => p.Activation == PromptActivationMode.CaptureScreenFast);
        if (capture != null)
        {
            Hotkey = capture.Hotkey;
        }

        var proofread = PromptShortcuts.FirstOrDefault(p => p.Name.Contains("Proofread", StringComparison.OrdinalIgnoreCase));
        if (proofread != null)
        {
            ProofreadHotkey = proofread.Hotkey;
            ProofreadPrompt = proofread.Prompt;
        }

        var translate = PromptShortcuts.FirstOrDefault(p => p.Name.Contains("Translate", StringComparison.OrdinalIgnoreCase));
        if (translate != null)
        {
            TranslateHotkey = translate.Hotkey;
            TranslatePrompt = translate.Prompt;
        }
    }
}
