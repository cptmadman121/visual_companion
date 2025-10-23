using System.Collections.Generic;
using System.Windows;
using TrayVisionPrompt.Models;

namespace TrayVisionPrompt.Views;

public partial class InstructionDialog : Window
{
    public InstructionContext InstructionContext { get; private set; } = new();

    private readonly Dictionary<string, string> _presets = new()
    {
        { "Bug beschreiben", "Beschreibe den markierten Bereich und mögliche Fehlerursachen." },
        { "UI-Text extrahieren", "Extrahiere alle sichtbaren Texte und fasse sie zusammen." },
        { "Code erklären", "Erkläre den dargestellten Codeabschnitt und mögliche Verbesserungen." }
    };

    public InstructionDialog(CaptureResult captureResult)
    {
        InitializeComponent();
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
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PromptBox.Text))
        {
            MessageBox.Show("Bitte Prompt eingeben.", "TrayVisionPrompt", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        InstructionContext = new InstructionContext
        {
            Prompt = PromptBox.Text,
            SelectedPreset = PresetCombo.SelectedItem as string
        };

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
