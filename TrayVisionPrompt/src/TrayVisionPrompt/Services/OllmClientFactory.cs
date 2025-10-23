using System;
using Microsoft.Extensions.Logging;
using TrayVisionPrompt.Configuration;

namespace TrayVisionPrompt.Services;

public class OllmClientFactory : IOllmClientFactory
{
    private readonly ILogger _logger;

    public OllmClientFactory(ILogger logger)
    {
        _logger = logger;
    }

    public IOllmClient Create(AppConfiguration configuration)
    {
        return configuration.Backend.ToLowerInvariant() switch
        {
            "ollama" => new OllamaClient(configuration, _logger),
            "vllm" => new VllmClient(configuration, _logger),
            "llamacpp" => new LlamaCppClient(configuration, _logger),
            _ => throw new InvalidOperationException($"Unsupported backend: {configuration.Backend}")
        };
    }
}
