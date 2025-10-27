using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TrayVisionPrompt.Configuration;

namespace TrayVisionPrompt.Views;

public partial class SettingsWindow : Window
{
    private readonly ConfigurationManager _configurationManager;
    private readonly ObservableCollection<PromptShortcutConfiguration> _promptShortcuts = new();
    private PromptShortcutConfiguration? _currentPrompt;
    private bool _isUpdatingPrompt;

    public SettingsWindow(ConfigurationManager configurationManager)
    {
        InitializeComponent();
        _configurationManager = configurationManager;
        PromptList.ItemsSource = _promptShortcuts;
        PromptActivationBox.ItemsSource = System.Enum.GetValues(typeof(PromptActivationMode));
        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        var config = _configurationManager.CurrentConfiguration;

        _promptShortcuts.Clear();
        foreach (var prompt in config.PromptShortcuts)
        {
            _promptShortcuts.Add(prompt.Clone());
        }

        if (_promptShortcuts.Count == 0)
        {
            var fallback = PromptShortcutConfiguration.CreateCapture("Capture Screen", "Ctrl+Shift+S", config.CaptureInstruction);
            _promptShortcuts.Add(fallback);
        }

        PromptList.SelectedIndex = _promptShortcuts.Count > 0 ? 0 : -1;

        BackendCombo.SelectedItem = BackendCombo.Items.Cast<ComboBoxItem>().FirstOrDefault(item => (string)item.Content == config.Backend);
        EndpointBox.Text = config.Endpoint;
        ModelBox.Text = config.Model;
        TimeoutBox.Text = config.RequestTimeoutMs.ToString();
        MaxTokensBox.Text = config.MaxTokens.ToString();
        TemperatureBox.Text = config.Temperature.ToString("0.##");
        VisionCheck.IsChecked = config.UseVision;
        OcrCheck.IsChecked = config.UseOcrFallback;
        ProxyBox.Text = config.Proxy ?? string.Empty;
        LogLevelCombo.SelectedItem = LogLevelCombo.Items.Cast<ComboBoxItem>().FirstOrDefault(item => (string)item.Content == config.LogLevel);
    }

    private void DisplayPrompt(PromptShortcutConfiguration? prompt)
    {
        _isUpdatingPrompt = true;
        _currentPrompt = prompt;
        PromptNameBox.Text = prompt?.Name ?? string.Empty;
        PromptHotkeyBox.Text = prompt?.Hotkey ?? string.Empty;
        PromptActivationBox.SelectedItem = prompt?.Activation ?? PromptActivationMode.CaptureScreen;
        PromptTextBox.Text = prompt?.Prompt ?? string.Empty;
        PromptPrefillBox.Text = prompt?.Prefill ?? string.Empty;
        _isUpdatingPrompt = false;
    }

    private void ApplyPromptEditorChanges()
    {
        if (_currentPrompt == null || _isUpdatingPrompt)
        {
            return;
        }

        _currentPrompt.Name = PromptNameBox.Text ?? string.Empty;
        _currentPrompt.Hotkey = PromptHotkeyBox.Text ?? string.Empty;
        if (PromptActivationBox.SelectedItem is PromptActivationMode mode)
        {
            _currentPrompt.Activation = mode;
        }
        _currentPrompt.Prompt = PromptTextBox.Text ?? string.Empty;
        _currentPrompt.Prefill = PromptPrefillBox.Text ?? string.Empty;
        PromptList.Items.Refresh();
    }

    private void OnPromptSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyPromptEditorChanges();
        if (PromptList.SelectedItem is PromptShortcutConfiguration selected)
        {
            DisplayPrompt(selected);
        }
        else
        {
            DisplayPrompt(null);
        }
    }

    private void OnAddPrompt(object sender, RoutedEventArgs e)
    {
        ApplyPromptEditorChanges();
        var prompt = PromptShortcutConfiguration.CreateTextSelection("Neue Prompt", string.Empty, PromptShortcutConfiguration.DefaultProofreadPrompt);
        _promptShortcuts.Add(prompt);
        PromptList.SelectedItem = prompt;
        DisplayPrompt(prompt);
    }

    private void OnRemovePrompt(object sender, RoutedEventArgs e)
    {
        if (PromptList.SelectedItem is PromptShortcutConfiguration prompt)
        {
            var index = PromptList.SelectedIndex;
            _promptShortcuts.Remove(prompt);
            if (_promptShortcuts.Count > 0)
            {
                PromptList.SelectedIndex = System.Math.Clamp(index - 1, 0, _promptShortcuts.Count - 1);
            }
            else
            {
                DisplayPrompt(null);
            }
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        ApplyPromptEditorChanges();

        var config = _configurationManager.CurrentConfiguration;
        config.PromptShortcuts = _promptShortcuts.Select(p => p.Clone()).ToList();

        if (BackendCombo.SelectedItem is ComboBoxItem backendItem)
        {
            config.Backend = backendItem.Content?.ToString() ?? config.Backend;
        }
        config.Endpoint = EndpointBox.Text;
        config.Model = ModelBox.Text;
        if (int.TryParse(TimeoutBox.Text, out var timeout))
        {
            config.RequestTimeoutMs = timeout;
        }
        if (int.TryParse(MaxTokensBox.Text, out var maxTokens))
        {
            config.MaxTokens = maxTokens;
        }
        if (double.TryParse(TemperatureBox.Text, out var temp))
        {
            config.Temperature = temp;
        }
        config.UseVision = VisionCheck.IsChecked == true;
        config.UseOcrFallback = OcrCheck.IsChecked == true;
        config.Proxy = string.IsNullOrWhiteSpace(ProxyBox.Text) ? null : ProxyBox.Text;
        if (LogLevelCombo.SelectedItem is ComboBoxItem levelItem)
        {
            config.LogLevel = levelItem.Content?.ToString() ?? config.LogLevel;
        }

        _configurationManager.Save();
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void PromptNameBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingPrompt)
        {
            return;
        }
        ApplyPromptEditorChanges();
    }

    private void PromptHotkeyBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingPrompt)
        {
            return;
        }
        ApplyPromptEditorChanges();
    }

    private void PromptActivationBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPrompt)
        {
            return;
        }
        ApplyPromptEditorChanges();
    }

    private void PromptTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingPrompt)
        {
            return;
        }
        ApplyPromptEditorChanges();
    }

    private void PromptPrefillBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingPrompt)
        {
            return;
        }
        ApplyPromptEditorChanges();
    }
}
