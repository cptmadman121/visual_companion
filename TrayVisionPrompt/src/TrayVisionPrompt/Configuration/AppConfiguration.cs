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
}
