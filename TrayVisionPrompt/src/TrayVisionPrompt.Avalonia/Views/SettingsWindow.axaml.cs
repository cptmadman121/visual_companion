using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TrayVisionPrompt.Avalonia.ViewModels;
using TrayVisionPrompt.Avalonia.Configuration;
using TrayVisionPrompt.Avalonia.Services;

namespace TrayVisionPrompt.Avalonia.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        try
        {
            var store = new ConfigurationStore();
            Icon = IconProvider.LoadWindowIcon(store.Current.IconAsset);
            DataContext = new SettingsViewModel();
        }
        catch
        {
            DataContext = new SettingsViewModel();
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}

