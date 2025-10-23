using System;
using System.Windows;

namespace TrayVisionPrompt.Models;

public class CaptureResult
{
    public Rect Bounds { get; init; }
    public string ImageBase64 { get; init; } = string.Empty;
    public double DisplayScaling { get; init; }
    public string? OcrText { get; init; }
}
