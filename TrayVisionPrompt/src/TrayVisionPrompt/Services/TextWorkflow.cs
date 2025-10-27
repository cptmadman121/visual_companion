using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TrayVisionPrompt.Configuration;
using TrayVisionPrompt.Models;

namespace TrayVisionPrompt.Services;

public class TextWorkflow
{
    private readonly ForegroundTextService _textService;
    private readonly IOllmClientFactory _ollmClientFactory;
    private readonly ConfigurationManager _configurationManager;
    private readonly DialogService _dialogService;
    private readonly ResponseCache _responseCache;
    private readonly ILogger<TextWorkflow> _logger;

    public TextWorkflow(
        ForegroundTextService textService,
        IOllmClientFactory ollmClientFactory,
        ConfigurationManager configurationManager,
        DialogService dialogService,
        ResponseCache responseCache,
        ILogger<TextWorkflow> logger)
    {
        _textService = textService;
        _ollmClientFactory = ollmClientFactory;
        _configurationManager = configurationManager;
        _dialogService = dialogService;
        _responseCache = responseCache;
        _logger = logger;
    }

    public async Task ExecuteAsync(PromptShortcutConfiguration shortcut)
    {
        switch (shortcut.Activation)
        {
            case PromptActivationMode.CaptureScreen:
                throw new InvalidOperationException("CaptureScreen shortcuts must be handled by CaptureWorkflow.");
            case PromptActivationMode.ForegroundSelection:
                await ExecuteSelectionAsync(shortcut);
                break;
            case PromptActivationMode.TextDialog:
                await ExecuteDialogAsync(shortcut);
                break;
        }
    }

    private async Task ExecuteSelectionAsync(PromptShortcutConfiguration shortcut)
    {
        var capture = await _textService.CaptureAsync();
        var text = capture.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            System.Windows.MessageBox.Show(
                "Keine Textauswahl gefunden. Bitte markieren Sie Text oder kopieren Sie Inhalt in die Zwischenablage.",
                "TrayVisionPrompt",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        var response = await SendAsync(shortcut, text);
        if (response == null)
        {
            return;
        }

        try
        {
            if (capture.HasSelection)
            {
                await _textService.ReplaceSelectionAsync(capture.WindowHandle, response);
            }
            else
            {
                await _textService.SetClipboardTextAsync(response);
                System.Windows.MessageBox.Show(
                    "Antwort in Zwischenablage kopiert.",
                    "TrayVisionPrompt",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply response to selection");
        }
    }

    private async Task ExecuteDialogAsync(PromptShortcutConfiguration shortcut)
    {
        var dialog = new Views.TextPromptDialog
        {
            Owner = System.Windows.Application.Current.MainWindow,
            Title = string.IsNullOrWhiteSpace(shortcut.Name) ? "Prompt" : shortcut.Name,
            InputText = shortcut.Prefill ?? string.Empty
        };

        if (dialog.ShowDialog() != true || !dialog.Confirmed)
        {
            return;
        }

        var text = dialog.InputText;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var response = await SendAsync(shortcut, text);
        if (response == null)
        {
            return;
        }

        System.Windows.Clipboard.SetText(response);
        var cached = _responseCache.LastResponse;
        _dialogService.ShowResponseDialog(cached ?? new LlmResponse { Text = response }, allowClipboardReplacement: false);
    }

    private async Task<string?> SendAsync(PromptShortcutConfiguration shortcut, string text)
    {
        var configuration = _configurationManager.CurrentConfiguration;
        using var client = _ollmClientFactory.Create(configuration);

        var promptBuilder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(shortcut.Prompt))
        {
            promptBuilder.AppendLine(shortcut.Prompt.Trim());
            promptBuilder.AppendLine();
        }
        promptBuilder.AppendLine("---");
        promptBuilder.AppendLine(text);

        var request = new LlmRequest
        {
            Prompt = promptBuilder.ToString(),
            UseVision = false,
            Metadata = new System.Collections.Generic.Dictionary<string, string>
            {
                ["ShortcutId"] = shortcut.Id,
                ["ShortcutName"] = shortcut.Name
            }
        };

        try
        {
            var response = await client.GetResponseAsync(request);
            _responseCache.LastResponse = response;
            return response.Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Text workflow failed");
            _dialogService.ShowError(ex.Message);
            return null;
        }
    }
}
