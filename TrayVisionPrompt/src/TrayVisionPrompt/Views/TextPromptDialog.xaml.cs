using System;
using System.Windows;

namespace TrayVisionPrompt.Views;

public partial class TextPromptDialog : Window
{
    public TextPromptDialog()
    {
        InitializeComponent();
    }

    public string InputText
    {
        get => InputBox.Text;
        set => InputBox.Text = value;
    }

    public bool Confirmed { get; private set; }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        InputBox.Focus();
        InputBox.CaretIndex = InputBox.Text?.Length ?? 0;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
