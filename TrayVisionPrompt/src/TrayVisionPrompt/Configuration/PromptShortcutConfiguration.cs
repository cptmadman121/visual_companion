using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TrayVisionPrompt.Configuration;

public class PromptShortcutConfiguration : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = string.Empty;
    private string _hotkey = string.Empty;
    private string _prompt = string.Empty;
    private string? _prefill = string.Empty;
    private PromptActivationMode _activation = PromptActivationMode.ForegroundSelection;

    [JsonPropertyName("id")]
    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    [JsonPropertyName("hotkey")]
    public string Hotkey
    {
        get => _hotkey;
        set => SetField(ref _hotkey, value);
    }

    [JsonPropertyName("prompt")]
    public string Prompt
    {
        get => _prompt;
        set => SetField(ref _prompt, value);
    }

    [JsonPropertyName("prefill")]
    public string? Prefill
    {
        get => _prefill;
        set => SetField(ref _prefill, value);
    }

    [JsonPropertyName("activation")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PromptActivationMode Activation
    {
        get => _activation;
        set => SetField(ref _activation, value);
    }

    public static PromptShortcutConfiguration CreateCapture(string name, string hotkey, string prompt)
    {
        return new PromptShortcutConfiguration
        {
            Name = name,
            Hotkey = hotkey,
            Prompt = prompt,
            Activation = PromptActivationMode.CaptureScreen
        };
    }

    public static PromptShortcutConfiguration CreateTextSelection(string name, string hotkey, string prompt)
    {
        return new PromptShortcutConfiguration
        {
            Name = name,
            Hotkey = hotkey,
            Prompt = prompt,
            Activation = PromptActivationMode.ForegroundSelection
        };
    }

    public static PromptShortcutConfiguration CreateTextDialog(string name, string hotkey, string prompt, string? prefill = null)
    {
        return new PromptShortcutConfiguration
        {
            Name = name,
            Hotkey = hotkey,
            Prompt = prompt,
            Prefill = prefill,
            Activation = PromptActivationMode.TextDialog
        };
    }

    public PromptShortcutConfiguration Clone()
    {
        return new PromptShortcutConfiguration
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = Name,
            Hotkey = Hotkey,
            Prompt = Prompt,
            Prefill = Prefill,
            Activation = Activation
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public const string DefaultProofreadPrompt = "Proofread and improve grammar, spelling, and clarity. Preserve tone and meaning. Return only the corrected text. Keep formatting, newlines, tabs etc. exactly as in the original text.";
    public const string DefaultTranslatePrompt = "If the text is english, translate it to German. If the text is German, translate it to English. All while preserving meaning, tone, and formatting. Return only the translation.";
    public const string DefaultAnonymizePrompt = "Anonymize the provided text. Replace personal data such as real names, email addresses, phone numbers, or postal addresses with realistic but fictitious placeholders. Preserve formatting and return only the sanitized text.";
}

public enum PromptActivationMode
{
    CaptureScreen,
    ForegroundSelection,
    TextDialog
}
