using Microsoft.Extensions.Logging.Abstractions;
using TrayVisionPrompt.Configuration;
using TrayVisionPrompt.Services;
using Xunit;

namespace TrayVisionPrompt.Tests;

public class OllmClientFactoryTests
{
    [Theory]
    [InlineData("ollama", typeof(OllamaClient))]
    [InlineData("vllm", typeof(VllmClient))]
    [InlineData("llamacpp", typeof(LlamaCppClient))]
    public void Create_ReturnsExpectedClientType(string backend, System.Type expectedType)
    {
        var config = new AppConfiguration
        {
            Backend = backend
        };

        var factory = new OllmClientFactory(NullLogger.Instance);
        var client = factory.Create(config);

        Assert.IsType(expectedType, client);
    }
}
