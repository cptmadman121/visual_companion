using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TrayVisionPrompt.Configuration;
using TrayVisionPrompt.Models;
using TrayVisionPrompt.Views;

namespace TrayVisionPrompt.Services;

public class CaptureWorkflow
{
    private readonly DialogService _dialogService;
    private readonly IOllmClientFactory _ollmClientFactory;
    private readonly OcrService _ocrService;
    private readonly ConfigurationManager _configurationManager;
    private readonly ILogger<CaptureWorkflow> _logger;
    private readonly ResponseCache _responseCache;

    public CaptureWorkflow(
        DialogService dialogService,
        IOllmClientFactory ollmClientFactory,
        OcrService ocrService,
        ConfigurationManager configurationManager,
        ResponseCache responseCache,
        ILogger<CaptureWorkflow> logger)
    {
        _dialogService = dialogService;
        _ollmClientFactory = ollmClientFactory;
        _ocrService = ocrService;
        _configurationManager = configurationManager;
        _responseCache = responseCache;
        _logger = logger;
    }

    public async Task ExecuteAsync(PromptShortcutConfiguration? shortcut = null)
    {
        var captureResult = _dialogService.ShowAnnotationOverlay();
        if (captureResult == null)
        {
            _logger.LogInformation("Capture cancelled by user");
            return;
        }

        var dialogTitle = shortcut?.Name;
        var instruction = _dialogService.ShowInstructionDialog(
            captureResult,
            shortcut?.Prompt,
            dialogTitle,
            shortcut?.Id);
        if (instruction == null)
        {
            _logger.LogInformation("Instruction dialog cancelled");
            return;
        }

        await PrepareAndSendAsync(captureResult, instruction);
    }

    public async Task TestBackendAsync()
    {
        var configuration = _configurationManager.CurrentConfiguration;
        using var client = _ollmClientFactory.Create(configuration);
        var request = new LlmRequest
        {
            Prompt = "TrayVisionPrompt Backend-Test: Bitte antworte mit 'Bereit'.",
            UseVision = false
        };

        var response = await client.GetResponseAsync(request);
        _dialogService.ShowResponseDialog(response, allowClipboardReplacement: false);
    }

    private async Task PrepareAndSendAsync(CaptureResult captureResult, InstructionContext instruction)
    {
        var configuration = _configurationManager.CurrentConfiguration;
        using var client = _ollmClientFactory.Create(configuration);

        if (configuration.UseOcrFallback)
        {
            captureResult = await EnrichWithOcrAsync(captureResult);
        }

        var request = new LlmRequest
        {
            Prompt = instruction.Prompt,
            ImageBase64 = captureResult.ImageBase64,
            TimestampUtc = DateTime.UtcNow,
            DisplayScaling = captureResult.DisplayScaling,
            UseVision = configuration.UseVision,
            Metadata = new System.Collections.Generic.Dictionary<string, string>()
            {
                ["Selection"] = captureResult.Bounds.ToString(),
                ["Preset"] = instruction.SelectedPreset ?? "custom"
            }
        };

        if (!string.IsNullOrWhiteSpace(instruction.ShortcutId))
        {
            request.Metadata["ShortcutId"] = instruction.ShortcutId!;
        }

        if (!configuration.UseVision && configuration.UseOcrFallback && captureResult.OcrText != null)
        {
            request.OcrText = captureResult.OcrText;
        }

        _logger.LogInformation("Sending request to backend {Endpoint} with model {Model}", configuration.Endpoint, configuration.Model);

        try
        {
            var response = await client.GetResponseAsync(request);
            _responseCache.LastResponse = response;
            _dialogService.ShowResponseDialog(response, allowClipboardReplacement: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while communicating with backend");
            _dialogService.ShowError(ex.Message);
        }
    }
    private async Task<CaptureResult> EnrichWithOcrAsync(CaptureResult capture)
    {
        if (!string.IsNullOrWhiteSpace(capture.OcrText))
        {
            return capture;
        }

        var text = await _ocrService.TryExtractTextAsync(capture.ImageBase64);
        if (string.IsNullOrWhiteSpace(text))
        {
            return capture;
        }

        return new CaptureResult
        {
            Bounds = capture.Bounds,
            DisplayScaling = capture.DisplayScaling,
            ImageBase64 = capture.ImageBase64,
            OcrText = text + "\n(Screenshot nicht verarbeitet)"
        };
    }
}
