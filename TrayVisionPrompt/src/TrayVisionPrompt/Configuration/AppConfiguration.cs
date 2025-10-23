using System.Text.Json.Serialization;

namespace TrayVisionPrompt.Configuration;

public class AppConfiguration
{
    [JsonPropertyName("hotkey")]
    public string Hotkey { get; set; } = "Win+Shift+Q";

    [JsonPropertyName("backend")]
    public string Backend { get; set; } = "ollama";

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = "http://127.0.0.1:11434/v1/chat/completions";

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
}
