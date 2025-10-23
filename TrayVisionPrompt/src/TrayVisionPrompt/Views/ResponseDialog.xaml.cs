using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using TrayVisionPrompt.Models;

namespace TrayVisionPrompt.Views;

public partial class ResponseDialog : Window
{
    private readonly bool _allowClipboardReplacement;
    private readonly LlmResponse _response;

    public ResponseDialog(LlmResponse response, bool allowClipboardReplacement)
    {
        InitializeComponent();
        _response = response;
        _allowClipboardReplacement = allowClipboardReplacement;
        ResponseText.Text = response.Text;
        ReplaceButton.IsEnabled = allowClipboardReplacement;
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ResponseText.Text);
    }

    private void OnReplace(object sender, RoutedEventArgs e)
    {
        if (!_allowClipboardReplacement)
        {
            return;
        }

        Clipboard.Clear();
        Clipboard.SetText(ResponseText.Text);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Textdatei (*.txt)|*.txt|Markdown (*.md)|*.md",
            FileName = $"TrayVisionPrompt_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            File.WriteAllText(dialog.FileName, ResponseText.Text);
        }
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
