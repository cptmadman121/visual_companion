using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using System;
using System.IO;

namespace TrayVisionPrompt.Avalonia.Views;

public partial class InstructionDialog : Window
{
    private global::Avalonia.Controls.TextBox? _instructionBox;
    private global::Avalonia.Controls.Image? _thumbnail;
    public string Instruction
    {
        get => _instructionBox?.Text ?? string.Empty;
        set { if (_instructionBox is not null) _instructionBox.Text = value; }
    }

    public bool Confirmed { get; private set; }

    public InstructionDialog()
    {
        InitializeComponent();
        _instructionBox = this.FindControl<global::Avalonia.Controls.TextBox>("InstructionBox");
        _thumbnail = this.FindControl<global::Avalonia.Controls.Image>("Thumbnail");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public void OnCancel(object? sender, RoutedEventArgs e)
        => Close();

    public void OnOk(object? sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    public void SetThumbnail(string base64Png)
    {
        if (_thumbnail is null) return;
        try
        {
            var bytes = Convert.FromBase64String(base64Png);
            using var ms = new MemoryStream(bytes, writable: false);
            _thumbnail.Source = new global::Avalonia.Media.Imaging.Bitmap(ms);
        }
        catch
        {
            // ignore invalid image
        }
    }
}
