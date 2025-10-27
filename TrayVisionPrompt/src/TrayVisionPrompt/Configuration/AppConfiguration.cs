using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace TrayVisionPrompt.Configuration;

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
    public string Endpoint { get; set; } = "http://192.168.201.166:11434/v1/chat/completions";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "llava:latest";

    [JsonPropertyName("requestTimeoutMs")]
    public int RequestTimeoutMs { get; set; } = 45000;

    [JsonPropertyName("maxTokens")]
    public int MaxTokens { get; set; } = 1024;

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

    // Prompt customization
    [JsonPropertyName("captureInstruction")]
    public string CaptureInstruction { get; set; } = "Describe the selected region succinctly.";

    [JsonPropertyName("proofreadPrompt")]
    public string ProofreadPrompt { get; set; } = "Proofread and improve grammar, spelling, and clarity. Preserve tone and meaning. Return only the corrected text. Keep formatting, newlines, tabs etc. exactly as in the original text.";

    [JsonPropertyName("translatePrompt")]
    public string TranslatePrompt { get; set; } = "If the text is english, translate it to German. If the text is German, translate it to English. All while preserving meaning, tone, and formatting. Return only the translation.";

    public void EnsureDefaults()
    {
        if (PromptShortcuts.Count == 0)
        {
            PromptShortcuts = new List<PromptShortcutConfiguration>
            {
                PromptShortcutConfiguration.CreateCapture(
                    "Capture Screen",
                    string.IsNullOrWhiteSpace(Hotkey) ? "Ctrl+Shift+S" : Hotkey,
                    string.IsNullOrWhiteSpace(CaptureInstruction) ? "Describe the selected region succinctly." : CaptureInstruction),
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

        var capture = PromptShortcuts.FirstOrDefault(p => p.Activation == PromptActivationMode.CaptureScreen);
        if (capture != null)
        {
            Hotkey = capture.Hotkey;
            if (!string.IsNullOrWhiteSpace(capture.Prompt))
            {
                CaptureInstruction = capture.Prompt;
            }
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
