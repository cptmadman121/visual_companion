using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;

namespace TrayVisionPrompt.Avalonia.Views;

public partial class ResponseDialog : Window
{
    private TextBlock? _responseTextBlock;
    public string ResponseText
    {
        get => _responseTextBlock?.Text ?? string.Empty;
        set { if (_responseTextBlock is not null) _responseTextBlock.Text = value; }
    }

    public ResponseDialog()
    {
        InitializeComponent();
        _responseTextBlock = this.FindControl<TextBlock>("ResponseTextBlock");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public async void OnCopy(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(ResponseText);
        }
    }

    public void OnClose(object? sender, RoutedEventArgs e) => Close();
}
