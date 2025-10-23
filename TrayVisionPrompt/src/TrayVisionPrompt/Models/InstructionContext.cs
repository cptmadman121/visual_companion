namespace TrayVisionPrompt.Models;

public class InstructionContext
{
    public string Prompt { get; init; } = string.Empty;
    public string? SelectedPreset { get; init; }
}
