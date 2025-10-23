using System.Threading.Tasks;
using TrayVisionPrompt.Configuration;
using TrayVisionPrompt.Models;

namespace TrayVisionPrompt.Services;

public interface IOllmClient : System.IDisposable
{
    Task<LlmResponse> GetResponseAsync(LlmRequest request);
}

public interface IOllmClientFactory
{
    IOllmClient Create(AppConfiguration configuration);
}
