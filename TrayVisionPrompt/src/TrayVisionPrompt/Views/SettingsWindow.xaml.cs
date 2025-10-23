using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TrayVisionPrompt.Configuration;

namespace TrayVisionPrompt.Views;

public partial class SettingsWindow : Window
{
    private readonly ConfigurationManager _configurationManager;

    public SettingsWindow(ConfigurationManager configurationManager)
    {
        InitializeComponent();
        _configurationManager = configurationManager;
        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        var config = _configurationManager.CurrentConfiguration;
        HotkeyBox.Text = config.Hotkey;
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

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var config = _configurationManager.CurrentConfiguration;
        config.Hotkey = HotkeyBox.Text;
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
}
