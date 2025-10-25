using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using TrayVisionPrompt.Configuration;
using Xunit;

namespace TvPrompt.Tests;

public class ConfigurationManagerTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsConfiguration()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var manager = new ConfigurationManager(tempDir, NullLogger.Instance);
            manager.CurrentConfiguration.Hotkey = "Ctrl+Shift+S";
            manager.CurrentConfiguration.Backend = "vllm";
            manager.Save();

            var manager2 = new ConfigurationManager(tempDir, NullLogger.Instance);
            manager2.Load();

            Assert.Equal("Ctrl+Shift+S", manager2.CurrentConfiguration.Hotkey);
            Assert.Equal("vllm", manager2.CurrentConfiguration.Backend);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
