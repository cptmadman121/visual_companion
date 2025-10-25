using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TrayVisionPrompt.Avalonia.ViewModels;

namespace TrayVisionPrompt.Avalonia.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}

