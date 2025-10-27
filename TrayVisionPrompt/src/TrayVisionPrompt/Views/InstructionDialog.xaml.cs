using System.Collections.Generic;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using TrayVisionPrompt.Models;
using MessageBox = System.Windows.MessageBox;

namespace TrayVisionPrompt.Views;

public partial class InstructionDialog : Window
{
    public InstructionContext InstructionContext { get; private set; } = new();

    private readonly Dictionary<string, string> _presets = new()
    {
        { "Bug beschreiben", "Beschreibe den markierten Bereich und mÃ¶gliche Fehlerursachen." },
        { "UI-Text extrahieren", "Extrahiere alle sichtbaren Texte und fasse sie zusammen." },
        { "Code erklÃ¤ren", "ErklÃ¤re den dargestellten Codeabschnitt und mÃ¶gliche Verbesserungen." }
    };
    private readonly string? _shortcutId;

    public InstructionDialog(CaptureResult captureResult, string? defaultPrompt = null, string? title = null, string? shortcutId = null)
    {
        _shortcutId = shortcutId;
        InitializeComponent();
        // Show preview image
        try
        {
            if (!string.IsNullOrWhiteSpace(captureResult.ImageBase64))
            {
                var bytes = Convert.FromBase64String(captureResult.ImageBase64);
                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                PreviewImage.Source = bmp;
            }
        }
        catch { /* ignore preview errors */ }
        foreach (var preset in _presets)
        {
            PresetCombo.Items.Add(preset.Key);
        }

        PresetCombo.SelectionChanged += (_, _) =>
        {
            if (PresetCombo.SelectedItem is string presetKey && _presets.TryGetValue(presetKey, out var prompt))
            {
                PromptBox.Text = prompt;
            }
        };

        PresetCombo.SelectedIndex = 0;

        if (!string.IsNullOrWhiteSpace(defaultPrompt))
        {
            PromptBox.Text = defaultPrompt;
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title;
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PromptBox.Text))
        {
            System.Windows.MessageBox.Show("Bitte Prompt eingeben.", "TrayVisionPrompt", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        InstructionContext = new InstructionContext
        {
            Prompt = PromptBox.Text,
            SelectedPreset = PresetCombo.SelectedItem as string,
            ShortcutId = _shortcutId
        };

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

