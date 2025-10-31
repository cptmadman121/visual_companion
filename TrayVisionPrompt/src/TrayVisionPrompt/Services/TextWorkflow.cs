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
    private readonly TrayIconService _trayIconService;

    public TextWorkflow(
        ForegroundTextService textService,
        IOllmClientFactory ollmClientFactory,
        ConfigurationManager configurationManager,
        DialogService dialogService,
        ResponseCache responseCache,
        ILogger<TextWorkflow> logger,
        TrayIconService trayIconService)
    {
        _textService = textService;
        _ollmClientFactory = ollmClientFactory;
        _configurationManager = configurationManager;
        _dialogService = dialogService;
        _responseCache = responseCache;
        _logger = logger;
        _trayIconService = trayIconService;
    }

    public async Task ExecuteAsync(PromptShortcutConfiguration shortcut, bool useClipboardFallback = false)
    {
        switch (shortcut.Activation)
        {
            case PromptActivationMode.CaptureScreen:
                throw new InvalidOperationException("CaptureScreen shortcuts must be handled by CaptureWorkflow.");
            case PromptActivationMode.ForegroundSelection:
                if (useClipboardFallback)
                {
                    await ExecuteClipboardAsync(shortcut, showResponseDialog: true);
                }
                else
                {
                    await ExecuteSelectionAsync(shortcut);
                }
                break;
            case PromptActivationMode.TextDialog:
                if (useClipboardFallback)
                {
                    await ExecuteClipboardAsync(shortcut, showResponseDialog: true);
                }
                else
                {
                    await ExecuteDialogAsync(shortcut);
                }
                break;
        }
    }

    private async Task ExecuteSelectionAsync(PromptShortcutConfiguration shortcut)
    {
        if (await _textService.IsRocketChatForegroundAsync())
        {
            await ExecuteClipboardAsync(shortcut, showResponseDialog: false);
            return;
        }

        var capture = await _textService.CaptureAsync();
        var text = capture.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            _trayIconService.ClearStatus();
            System.Windows.MessageBox.Show(
                "Keine Textauswahl gefunden. Bitte markieren Sie Text oder kopieren Sie Inhalt in die Zwischenablage.",
                "deskLLM",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        // Now that we have the selection, show pending immediately
        _trayIconService.ShowPending();

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
                    "deskLLM",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply response to selection");
        }
    }

    private async Task ExecuteClipboardAsync(PromptShortcutConfiguration shortcut, bool showResponseDialog)
    {
        var clipboardText = await _textService.GetClipboardTextAsync();
        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            _trayIconService.ClearStatus();
            System.Windows.MessageBox.Show(
                "Die Zwischenablage enthält keinen Text. Bitte kopieren Sie Inhalt, bevor Sie den Tray-Prompt verwenden.",
                "deskLLM",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        _trayIconService.ShowPending();
        var response = await SendAsync(shortcut, clipboardText);
        if (response == null)
        {
            return;
        }

        await _textService.SetClipboardTextAsync(response);
        if (showResponseDialog)
        {
            var cached = _responseCache.LastResponse;
            _dialogService.ShowResponseDialog(cached ?? new LlmResponse { Text = response }, allowClipboardReplacement: false);
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
            _trayIconService.ClearStatus();
            return;
        }

        var text = dialog.InputText;
        if (string.IsNullOrWhiteSpace(text))
        {
            _trayIconService.ClearStatus();
            return;
        }

        _trayIconService.ShowPending();
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

        var tokenBudget = Math.Max(2048, Math.Min(configuration.MaxTokens, 8192));

        _trayIconService.StartBusy();
        try
        {
            var isTranslate = (!string.IsNullOrWhiteSpace(shortcut.Name) && shortcut.Name.Contains("translate", StringComparison.OrdinalIgnoreCase))
                              || (!string.IsNullOrWhiteSpace(shortcut.Prompt) && shortcut.Prompt.Contains("translate", StringComparison.OrdinalIgnoreCase));
            var languageHint = BuildLanguageHint(text, isTranslate);
            var promptTemplate = string.IsNullOrWhiteSpace(shortcut.Prompt) ? null : shortcut.Prompt.Trim();
            var chunks = TextChunker.Split(text, tokenBudget);
            if (chunks.Count > 1)
            {
                _logger.LogInformation("Input exceeds Gemma 3 27B comfort window ({TokenBudget} tokens). Splitting into {ChunkCount} segments.", tokenBudget, chunks.Count);
            }

            var aggregatedText = new StringBuilder();
            LlmResponse? lastResponse = null;
            TimeSpan? totalDuration = null;

            for (var index = 0; index < chunks.Count; index++)
            {
                var chunkText = chunks[index];
                var promptBuilder = new StringBuilder();
                if (!string.IsNullOrEmpty(languageHint))
                {
                    promptBuilder.AppendLine(languageHint);
                    promptBuilder.AppendLine();
                }

                if (!string.IsNullOrEmpty(promptTemplate))
                {
                    promptBuilder.AppendLine(promptTemplate);
                    promptBuilder.AppendLine();
                }

                if (chunks.Count > 1)
                {
                    promptBuilder.AppendLine($"You are processing part {index + 1} of {chunks.Count} of a larger text. Apply the instructions to this part only and preserve formatting. Return only the transformed text for this part.");
                    promptBuilder.AppendLine();
                }

                promptBuilder.AppendLine("---");
                promptBuilder.AppendLine(chunkText);

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

                if (chunks.Count > 1)
                {
                    request.Metadata["ChunkIndex"] = (index + 1).ToString();
                    request.Metadata["ChunkCount"] = chunks.Count.ToString();
                }

                var response = await client.GetResponseAsync(request);
                lastResponse = response;
                if (response.Duration.HasValue)
                {
                    totalDuration = (totalDuration ?? TimeSpan.Zero) + response.Duration;
                }

                var chunkResult = response.Text ?? string.Empty;
                aggregatedText.Append(chunkResult.TrimEnd());
                if (index < chunks.Count - 1)
                {
                    aggregatedText.AppendLine();
                }
            }

            if (lastResponse != null)
            {
                var combined = aggregatedText.ToString();
                var aggregatedResponse = new LlmResponse
                {
                    Text = combined,
                    Model = lastResponse.Model,
                    Duration = totalDuration ?? lastResponse.Duration
                };
                _responseCache.LastResponse = aggregatedResponse;
                return combined;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Text workflow failed");
            _dialogService.ShowError(ex.Message);
            return null;
        }
        finally
        {
            _trayIconService.StopBusy();
        }
    }

    private static string? BuildLanguageHint(string sourceText, bool isTranslate)
    {
        if (isTranslate)
        {
            return null;
        }

        var lang = LanguageDetector.Detect(sourceText);
        if (string.Equals(lang, "German", StringComparison.OrdinalIgnoreCase))
        {
            return "Bitte antworte ausschließlich auf Deutsch, sofern nicht anders angewiesen.";
        }
        if (string.Equals(lang, "English", StringComparison.OrdinalIgnoreCase))
        {
            return "Please respond exclusively in English unless instructed otherwise.";
        }

        return null;
    }
}
