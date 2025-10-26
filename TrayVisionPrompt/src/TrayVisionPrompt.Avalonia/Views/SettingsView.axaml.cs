using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using TrayVisionPrompt.Avalonia.ViewModels;
using TrayVisionPrompt.Avalonia.Services;
using TrayVisionPrompt.Avalonia.Configuration;

namespace TrayVisionPrompt.Avalonia.Views;

public partial class SettingsView : global::Avalonia.Controls.UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public void OnSave(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.Save();
        }
        if (this.VisualRoot is Window w)
        {
            w.Close();
        }
    }

    public void OnCancel(object? sender, RoutedEventArgs e)
    {
        if (this.VisualRoot is Window w)
        {
            w.Close();
        }
    }

    public async void OnTest(object? sender, RoutedEventArgs e)
    {
        var dlg = new ResponseDialog { ResponseText = "Testing backend…" };
        var window = this.VisualRoot as Window;
        if (window is not null)
        {
            _ = dlg.ShowDialog(window);
        }
        else
        {
            dlg.Show();
        }

        try
        {
            using var svc = new LlmService();
            var store = new ConfigurationStore();
            var text = await svc.SendAsync("TrayVisionPrompt Backend-Test: Bitte antworte mit 'Bereit'.", systemPrompt: ComposeSystemPrompt(store.Current.Language));
            dlg.ResponseText = string.IsNullOrWhiteSpace(text) ? "(no response)" : text;
        }
        catch (Exception ex)
        {
            dlg.ResponseText = $"Error: {ex.Message}";
        }
    }

    private static string ComposeSystemPrompt(string language) => SystemPromptBuilder.Build(language);
}
