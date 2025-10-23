using System;
using System.Collections.Generic;

namespace TrayVisionPrompt.Models;

public class LlmRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? ImageBase64 { get; set; }
    public bool UseVision { get; set; }
    public string? OcrText { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public double DisplayScaling { get; set; }
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
