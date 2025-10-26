using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using TrayVisionPrompt.Avalonia.ViewModels;
using TrayVisionPrompt.Avalonia.Services;
using TrayVisionPrompt.Avalonia.Views;
using TrayVisionPrompt.Avalonia.Configuration;

namespace TrayVisionPrompt.Avalonia.Views;

public partial class MainWindow : Window
{
    private readonly TranscriptService _transcript = new();
    private readonly ConfigurationStore _store = new();
    public MainWindow()
    {
        InitializeComponent();
        Icon = IconProvider.LoadWindowIcon(_store.Current.IconAsset);
        _transcript.StartNewSession();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public void OnClose(object? sender, RoutedEventArgs e) => Close();

    public async void OnOpenSettings(object? sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow();
        await dlg.ShowDialog(this);
    }

    public async void OnSend(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || string.IsNullOrWhiteSpace(vm.Prompt))
            return;

        var userText = vm.Prompt;
        vm.Messages.Add($"You: {userText}");
        _transcript.Append($"You: {userText}");
        vm.Prompt = string.Empty;

        var dlg = new ResponseDialog { ResponseText = "Thinking… contacting backend…" };
        _ = dlg.ShowDialog(this);

        try
        {
            using var llm = new LlmService();
            // Simple context: join previous messages for grounding
            var text = await llm.SendAsync(string.Join("\n", vm.Messages), systemPrompt: SystemPromptBuilder.Build(_store.Current.Language));
            dlg.ResponseText = string.IsNullOrWhiteSpace(text) ? "(empty response)" : text;
            vm.Messages.Add($"Assistant: {text}");
            _transcript.Append($"Assistant: {text}");
        }
        catch (Exception ex)
        {
            dlg.ResponseText = $"Error: {ex.Message}";
        }
    }

    public async void OnCapture(object? sender, RoutedEventArgs e)
    {
        var annot = new AnnotationWindow();
        await annot.ShowDialog(this);

        if (annot.CapturedImageBase64 is string img && !string.IsNullOrWhiteSpace(img))
        {
            var ask = new InstructionDialog
            {
                Instruction = "Describe the selected region succinctly."
            };
            ask.SetThumbnail(img);
            await ask.ShowDialog(this);
            if (!ask.Confirmed) return;

            var resp = new ResponseDialog { ResponseText = "Analyzing selection… contacting backend…" };
            _ = resp.ShowDialog(this);

            try
            {
                // Try OCR fallback if configured
                string? ocr = null;
                if (DataContext is MainViewModel svm && svm.UseOcrFallback)
                {
                    var ocrSvc = new OcrService();
                    ocr = await ocrSvc.TryExtractTextAsync(img);
                }

                var prompt = string.IsNullOrWhiteSpace(ocr)
                    ? ask.Instruction
                    : $"{ask.Instruction}\n\nOCR-Fallback:\n{ocr}";

                using var llm = new LlmService();
                var text = await llm.SendAsync(prompt, img, forceVision: true, systemPrompt: SystemPromptBuilder.Build(_store.Current.Language));
                resp.ResponseText = string.IsNullOrWhiteSpace(text) ? "(empty response)" : text;
                if (DataContext is MainViewModel vm)
                {
                    vm.Messages.Add($"You (capture): {ask.Instruction}");
                    _transcript.Append($"You (capture): {ask.Instruction}");
                    vm.Messages.Add($"Assistant: {text}");
                    _transcript.Append($"Assistant: {text}");
                }
            }
            catch (Exception ex)
            {
                resp.ResponseText = $"Error: {ex.Message}";
            }
        }
    }

    public void OnNewSession(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Messages.Clear();
        }
    }
}
