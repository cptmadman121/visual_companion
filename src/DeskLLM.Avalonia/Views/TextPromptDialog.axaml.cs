using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace DeskLLM.Avalonia.Views;

public partial class TextPromptDialog : Window
{
    public TextPromptDialog()
    {
        InitializeComponent();
    }

    public string InputText
    {
        get => InputBox.Text ?? string.Empty;
        set => InputBox.Text = value;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        InputBox.Focus();
        InputBox.CaretIndex = InputBox.Text?.Length ?? 0;
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(InputText);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
