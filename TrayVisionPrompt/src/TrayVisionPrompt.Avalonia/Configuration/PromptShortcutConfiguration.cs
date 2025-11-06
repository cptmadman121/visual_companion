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
    private bool _showResponseDialog = true;

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

    [JsonPropertyName("showResponseDialog")]
    public bool ShowResponseDialog
    {
        get => _showResponseDialog;
        set => SetField(ref _showResponseDialog, value);
    }

    public static PromptShortcutConfiguration CreateCapture(string name, string hotkey, string prompt)
    {
        return new PromptShortcutConfiguration
        {
            Name = name,
            Hotkey = hotkey,
            Prompt = string.IsNullOrWhiteSpace(prompt) ? DefaultCapturePrompt : prompt,
            Activation = PromptActivationMode.CaptureScreen,
            ShowResponseDialog = true
        };
    }

    public static PromptShortcutConfiguration CreateCaptureFast(string name, string hotkey, string prompt)
    {
        return new PromptShortcutConfiguration
        {
            Name = name,
            Hotkey = hotkey,
            Prompt = string.IsNullOrWhiteSpace(prompt) ? DefaultCapturePrompt : prompt,
            Activation = PromptActivationMode.CaptureScreenFast,
            ShowResponseDialog = false
        };
    }

    public static PromptShortcutConfiguration CreateTextSelection(string name, string hotkey, string prompt)
    {
        return new PromptShortcutConfiguration
        {
            Name = name,
            Hotkey = hotkey,
            Prompt = prompt,
            Activation = PromptActivationMode.ForegroundSelection,
            ShowResponseDialog = false
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
            Activation = PromptActivationMode.TextDialog,
            ShowResponseDialog = true
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
            Activation = Activation,
            ShowResponseDialog = ShowResponseDialog
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

    public const string DefaultCapturePrompt = "Describe the selected region succinctly.";
    public const string DefaultProofreadPrompt = "Proofread and improve grammar, spelling, and clarity, while maintaining the original language of the text. Preserve tone and meaning. Keep formatting, newlines, tabs etc. exactly as in the original text. Return only the corrected text.";
    public const string DefaultTranslatePrompt = "If the provided text is not in German, translate it into German. If the provided text is in German, translate it into English. The entire translation process should preserve the tone, structure, and formatting of the original text. Return only the translated text.";
    public const string DefaultAnonymizePrompt = "Anonymize the provided text. Replace personal data such as real names, email addresses, phone numbers, or postal addresses with fictitious placeholders from the shows 'The Simpsons' or 'Futurama'. Preserve formatting and return only the sanitized text.";
}

public enum PromptActivationMode
{
    CaptureScreen,
    CaptureScreenFast,
    ForegroundSelection,
    TextDialog
}
