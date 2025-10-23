using TrayVisionPrompt.Models;

namespace TrayVisionPrompt.Services;

public class ResponseCache
{
    public LlmResponse? LastResponse { get; set; }
}
