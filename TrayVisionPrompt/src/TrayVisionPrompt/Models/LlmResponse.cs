using System;

namespace TrayVisionPrompt.Models;

public class LlmResponse
{
    public string Text { get; set; } = string.Empty;
    public string? Model { get; set; }
    public TimeSpan? Duration { get; set; }
}
