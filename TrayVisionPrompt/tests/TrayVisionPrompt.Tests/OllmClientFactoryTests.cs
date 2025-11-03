using System.Linq;
using TrayVisionPrompt.Configuration;
using Xunit;

namespace TvPrompt.Tests;

public class PromptShortcutConfigurationTests
{
    [Fact]
    public void Clone_CreatesDistinctInstance()
    {
        var prompt = PromptShortcutConfiguration.CreateTextSelection("Test", "Ctrl+Alt+T", "Do something");
        var clone = prompt.Clone();

        Assert.NotSame(prompt, clone);
        Assert.NotEqual(prompt.Id, clone.Id);
        Assert.Equal(prompt.Name, clone.Name);
        Assert.Equal(prompt.Hotkey, clone.Hotkey);
        Assert.Equal(prompt.Prompt, clone.Prompt);
        Assert.Equal(prompt.Prefill, clone.Prefill);
        Assert.Equal(prompt.Activation, clone.Activation);
    }

    [Fact]
    public void EnsureDefaults_PopulatesPromptShortcuts()
    {
        var config = new AppConfiguration();
        config.EnsureDefaults();

        Assert.NotEmpty(config.PromptShortcuts);
        Assert.Contains(config.PromptShortcuts, p => p.Activation == PromptActivationMode.CaptureScreen);
        Assert.Contains(config.PromptShortcuts, p => p.Activation == PromptActivationMode.ForegroundSelection);
    }
}
