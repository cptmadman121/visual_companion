using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DeskLLM.Avalonia.ViewModels;
using DeskLLM.Avalonia.Configuration;
using DeskLLM.Avalonia.Services;

namespace DeskLLM.Avalonia.Views;

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

