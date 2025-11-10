using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using TrayVisionPrompt.Avalonia.Configuration;
using TrayVisionPrompt.Avalonia.Services;
using TrayVisionPrompt.Avalonia.ViewModels;
using TrayVisionPrompt.Avalonia.Views;
using ComboBox = Avalonia.Controls.ComboBox;
using Control = Avalonia.Controls.Control;
using Button = Avalonia.Controls.Button;
using TextBox = Avalonia.Controls.TextBox;

namespace TrayVisionPrompt.Avalonia.Views;

public partial class MainWindow : Window
{
    private readonly ConfigurationStore _store = new();
    private readonly TranscriptService _transcript;
    private readonly ObservableCollection<string> _availableModels = new();
    private readonly StartupTutorialService _tutorialService = new();

    private ComboBox? _modelPicker;
    private ScrollViewer? _transcriptViewer;
    private bool _modelsLoaded;
    private bool _loadingModels;
    private bool _autoScroll = true;
    private bool _tutorialRunning;
    private Control? _tutorialHighlight;
    private TutorialHintWindow? _tutorialHintWindow;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnWindowOpened;
        _transcript = new TranscriptService(_store);
        Icon = IconProvider.LoadWindowIcon(_store.Current.IconAsset);

        InitializeModelPicker();
        InitializeTranscriptView();

        DataContextChanged += (_, _) => SyncViewModelFromConfiguration();
        ConfigurationStore.ConfigurationChanged += OnConfigurationChanged;

        SyncViewModelFromConfiguration();
        _transcript.StartNewSession();
        _autoScroll = true;
        ScrollTranscriptToEnd();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public async void OnOpenSettings(object? sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow();
        await dlg.ShowDialog(this);
    }

    public async void OnSend(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || string.IsNullOrWhiteSpace(vm.Prompt))
        {
            return;
        }

        var userText = vm.Prompt;
        vm.Messages.Add($"You: {userText}");
        _transcript.Append($"You: {userText}");
        vm.Prompt = string.Empty;
        ScrollTranscriptToEnd();

        var dlg = new ResponseDialog { ResponseText = "Thinking… contacting backend…" };
        _ = dlg.ShowDialog(this);

        try
        {
            using var llm = new LlmService();
            var text = await llm.SendAsync(string.Join("\n", vm.Messages), systemPrompt: SystemPromptBuilder.Build(_store.Current.Language));
            dlg.ResponseText = string.IsNullOrWhiteSpace(text) ? "(empty response)" : text;
            vm.Messages.Add($"Assistant: {text}");
            _transcript.Append($"Assistant: {text}");
            ScrollTranscriptToEnd();
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

        if (annot.CapturedImageBase64 is not string img || string.IsNullOrWhiteSpace(img))
        {
            return;
        }

        var ask = new InstructionDialog
        {
            Instruction = "Describe the selected region succinctly."
        };
        ask.SetThumbnail(img);
        await ask.ShowDialog(this);
        if (!ask.Confirmed)
        {
            return;
        }

        var resp = new ResponseDialog { ResponseText = "Analyzing selection… contacting backend…" };
        _ = resp.ShowDialog(this);

        try
        {
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
                ScrollTranscriptToEnd();
            }
        }
        catch (Exception ex)
        {
            resp.ResponseText = $"Error: {ex.Message}";
        }
    }

    public void OnNewSession(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Messages.Clear();
        }

        _transcript.StartNewSession();
        _autoScroll = true;
        ScrollTranscriptToEnd();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_tutorialHintWindow is not null)
        {
            _tutorialHintWindow.Close();
            _tutorialHintWindow = null;
        }
        ClearTutorialHighlight();
        if (_transcriptViewer is not null)
        {
            _transcriptViewer.ScrollChanged -= OnTranscriptScrollChanged;
        }
        _transcript.Dispose();
        ConfigurationStore.ConfigurationChanged -= OnConfigurationChanged;
    }

    private void InitializeModelPicker()
    {
        _modelPicker = this.FindControl<ComboBox>("ModelPicker");
        if (_modelPicker is null)
        {
            return;
        }

        _modelPicker.ItemsSource = _availableModels;
        _modelPicker.IsEnabled = IsOllamaBackend();
    }

    private void InitializeTranscriptView()
    {
        _transcriptViewer = this.FindControl<ScrollViewer>("TranscriptViewer");
        if (_transcriptViewer is not null)
        {
            _transcriptViewer.AttachedToVisualTree += (_, _) => ScrollTranscriptToEnd();
            _transcriptViewer.ScrollChanged += OnTranscriptScrollChanged;
        }
    }

    private void SyncViewModelFromConfiguration()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        vm.Model = _store.Current.Model;
        vm.UseOcrFallback = _store.Current.UseOcrFallback;

        EnsureSelectedModelInList();
    }

    private void OnConfigurationChanged(object? sender, EventArgs e)
    {
        if (_modelPicker is not null)
        {
            _modelPicker.IsEnabled = IsOllamaBackend();
        }

        _availableModels.Clear();
        _modelsLoaded = false;
        EnsureSelectedModelInList();
        SyncViewModelFromConfiguration();
    }

    private void EnsureSelectedModelInList()
    {
        var current = _store.Current.Model;
        if (!string.IsNullOrWhiteSpace(current) && !_availableModels.Any(m => string.Equals(m, current, StringComparison.OrdinalIgnoreCase)))
        {
            _availableModels.Add(current);
        }

        if (_modelPicker != null && !string.IsNullOrWhiteSpace(current))
        {
            _modelPicker.SelectedItem = _availableModels.FirstOrDefault(m => string.Equals(m, current, StringComparison.OrdinalIgnoreCase)) ?? current;
        }
    }

    private async Task LoadModelsAsync()
    {
        if (_modelsLoaded || _loadingModels)
        {
            return;
        }

        if (!IsOllamaBackend())
        {
            AppendSystemMessage("Model picker currently only supports the Ollama backend. Switch backend in Settings to use it.");
            _modelsLoaded = true;
            return;
        }

        var tagsUri = ResolveOllamaTagsUri(_store.Current.Endpoint);
        if (tagsUri is null)
        {
            AppendSystemMessage("Cannot determine Ollama endpoint. Check the endpoint in Settings.");
            _modelsLoaded = true;
            return;
        }

        try
        {
            _loadingModels = true;
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.GetAsync(tagsUri);
            if (!response.IsSuccessStatusCode)
            {
                AppendSystemMessage($"Failed to load models from Ollama ({(int)response.StatusCode} {response.ReasonPhrase}).");
                return;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            if (!document.RootElement.TryGetProperty("models", out var modelsElement) || modelsElement.ValueKind != JsonValueKind.Array)
            {
                AppendSystemMessage("Unexpected response from Ollama when listing models.");
                return;
            }

            var names = modelsElement
                .EnumerateArray()
                .Select(element => element.ValueKind == JsonValueKind.Object && element.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString()
                    : null)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0)
            {
                AppendSystemMessage("No models reported by Ollama. Pull or create a model first.");
                return;
            }

            _availableModels.Clear();
            foreach (var name in names)
            {
                if (name is not null)
                {
                    _availableModels.Add(name);
                }
            }

            _modelsLoaded = true;
            EnsureSelectedModelInList();
        }
        catch (Exception ex)
        {
            AppendSystemMessage($"Failed to fetch models from Ollama: {ex.Message}");
        }
        finally
        {
            _loadingModels = false;
        }
    }

    private static Uri? ResolveOllamaTagsUri(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var builder = new UriBuilder(uri.Scheme, uri.Host, uri.IsDefaultPort ? -1 : uri.Port);
        return new Uri(builder.Uri, "/api/tags");
    }

    private void AppendSystemMessage(string message)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Messages.Add($"[Info] {message}");
            ScrollTranscriptToEnd();
        }
    }

    public async void OnModelPickerDropDownOpened(object? sender, EventArgs e)
    {
        await LoadModelsAsync();
    }

    public void OnModelPickerSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_modelPicker?.SelectedItem is not string model || string.IsNullOrWhiteSpace(model))
        {
            return;
        }

        if (!string.Equals(_store.Current.Model, model, StringComparison.OrdinalIgnoreCase))
        {
            _store.Current.Model = model;
            _store.Save();
        }

        if (DataContext is MainViewModel vm)
        {
            vm.Model = model;
        }

        ScrollTranscriptToEnd();
    }

    private bool IsOllamaBackend() =>
        string.Equals(_store.Current.Backend, "ollama", StringComparison.OrdinalIgnoreCase);

    private void ScrollTranscriptToEnd()
    {
        if (_transcriptViewer is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_autoScroll)
            {
                _transcriptViewer.ScrollToEnd();
            }
        }, DispatcherPriority.Background);
    }

    private void OnTranscriptScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_transcriptViewer is null)
        {
            return;
        }

        var maxOffset = Math.Max(0, _transcriptViewer.Extent.Height - _transcriptViewer.Viewport.Height);
        var currentOffset = _transcriptViewer.Offset.Y;
        _autoScroll = maxOffset - currentOffset <= 12;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        if (_tutorialRunning || !_tutorialService.ShouldRunTutorial())
        {
            return;
        }

        _tutorialRunning = true;
        try
        {
            await RunStartupTutorialAsync();
        }
        finally
        {
            _tutorialRunning = false;
        }
    }

    private async Task RunStartupTutorialAsync()
    {
        var steps = CreateTutorialSteps();
        if (steps.Count == 0)
        {
            _tutorialService.RecordOutcome(TutorialOutcome.Completed, "No tutorial steps configured");
            return;
        }

        try
        {
            for (var i = 0; i < steps.Count; i++)
            {
                var continueTutorial = await ShowTutorialStepAsync(steps[i], i + 1, steps.Count);
                if (!continueTutorial)
                {
                    await Dispatcher.UIThread.InvokeAsync(ResetPromptText);
                    _tutorialService.RecordOutcome(TutorialOutcome.Skipped, $"Skipped at step {i + 1}: {steps[i].Title}");
                    return;
                }
            }

            await Dispatcher.UIThread.InvokeAsync(ResetPromptText);
            _tutorialService.RecordOutcome(TutorialOutcome.Completed, $"Completed {steps.Count} steps");
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(ResetPromptText);
            _tutorialService.RecordFailure(ex);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_tutorialHintWindow is not null)
                {
                    _tutorialHintWindow.Close();
                    _tutorialHintWindow = null;
                }

                ClearTutorialHighlight();
            });
        }
    }

    private async Task<bool> ShowTutorialStepAsync(TutorialStep step, int stepNumber, int totalSteps)
    {
        var tcs = new TaskCompletionSource<bool>();
        Control? target = null;
        EventHandler? nextHandler = null;
        EventHandler? skipHandler = null;
        EventHandler? closedHandler = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            target = step.TargetResolver(this);
            ClearTutorialHighlight();
            step.OnEnter?.Invoke(this);

            if (target is Control highlight)
            {
                _tutorialHighlight = highlight;
                if (!highlight.Classes.Contains("tutorial-highlight"))
                {
                    highlight.Classes.Add("tutorial-highlight");
                }

                highlight.BringIntoView();
            }

            _tutorialHintWindow?.Close();
            _tutorialHintWindow = new TutorialHintWindow();
            var buttonText = step.PrimaryButtonText;
            if (stepNumber == totalSteps && string.Equals(buttonText, "Next", StringComparison.OrdinalIgnoreCase))
            {
                buttonText = "Finish";
            }

            _tutorialHintWindow.SetContent(step.Title, step.Message, $"Step {stepNumber} of {totalSteps}", buttonText);
            _tutorialHintWindow.AttachToTarget(target);

            nextHandler = (_, _) => tcs.TrySetResult(true);
            skipHandler = (_, _) => tcs.TrySetResult(false);
            closedHandler = (_, _) =>
            {
                if (!tcs.Task.IsCompleted)
                {
                    tcs.TrySetResult(false);
                }
            };

            _tutorialHintWindow.NextRequested += nextHandler;
            _tutorialHintWindow.SkipRequested += skipHandler;
            _tutorialHintWindow.Closed += closedHandler;
            _tutorialHintWindow.Show(this);
        });

        var result = await tcs.Task;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_tutorialHintWindow is not null)
            {
                if (nextHandler is not null)
                {
                    _tutorialHintWindow.NextRequested -= nextHandler;
                }

                if (skipHandler is not null)
                {
                    _tutorialHintWindow.SkipRequested -= skipHandler;
                }

                if (closedHandler is not null)
                {
                    _tutorialHintWindow.Closed -= closedHandler;
                }

                _tutorialHintWindow.Close();
                _tutorialHintWindow = null;
            }

            ClearTutorialHighlight();
        });

        return result;
    }

    private void ResetPromptText()
    {
        if (this.FindControl<TextBox>("PromptBox") is { } prompt)
        {
            prompt.Text = string.Empty;
            prompt.SelectionStart = 0;
            prompt.SelectionEnd = 0;
        }

        if (DataContext is MainViewModel vm)
        {
            vm.Prompt = string.Empty;
        }
    }

    private void ClearTutorialHighlight()
    {
        if (_tutorialHighlight is { } control)
        {
            control.Classes.Remove("tutorial-highlight");
            _tutorialHighlight = null;
        }
    }

    private List<TutorialStep> CreateTutorialSteps()
    {
        return new List<TutorialStep>
        {
            new(
                "Welcome to TrayVisionPrompt",
                "This quick tour highlights the core tools. You can stop at any time, and it will never show again once finished.",
                _ => null,
                "Start"),
            new(
                "Compose your prompt",
                "Use the prompt card to type what you need. You can paste descriptions, questions, or notes before sending them to the assistant.",
                window => window.FindControl<Border>("PromptCard"),
                "Next",
                window =>
                {
                    const string sample = "Summarize the highlighted screen selection.";
                    if (window.FindControl<TextBox>("PromptBox") is { } prompt)
                    {
                        prompt.Text = sample;
                        prompt.SelectionStart = sample.Length;
                        prompt.SelectionEnd = sample.Length;
                        prompt.Focus();
                    }

                    if (window.DataContext is MainViewModel vm)
                    {
                        vm.Prompt = sample;
                    }
                }),
            new(
                "Choose a model",
                "Select the Ollama model that should answer your request. The list refreshes from the Ollama server when opened.",
                window => window.FindControl<ComboBox>("ModelPicker")),
            new(
                "Capture the screen",
                "Use Capture to snip an area of your desktop. You'll be able to annotate the screenshot before TrayVisionPrompt analyzes it.",
                window => window.FindControl<Button>("CaptureButton")),
            new(
                "Send text prompts",
                "When you just need text assistance, press Send. We'll contact the model with whatever you've typed in the prompt box.",
                window => window.FindControl<Button>("SendButton")),
            new(
                "Review the conversation",
                "All messages appear in this conversation panel. Scroll to revisit answers or copy useful excerpts whenever you like.",
                window => window.FindControl<Border>("ConversationCard")),
            new(
                "Open settings",
                "Settings lets you configure models, shortcuts, and logging. Adjust the app to match your workflow.",
                window => window.FindControl<Button>("SettingsButton")),
            new(
                "Start a new session",
                "Clear the conversation and begin again with New Session. It's helpful when switching tasks or topics.",
                window => window.FindControl<Button>("NewSessionButton")),
            new(
                "You're all set",
                "That's everything! TrayVisionPrompt will keep this record so you won't see the tutorial again. Have fun exploring.",
                _ => null,
                "Finish",
                window =>
                {
                    if (window.FindControl<TextBox>("PromptBox") is { } prompt)
                    {
                        prompt.Text = string.Empty;
                        prompt.SelectionStart = 0;
                        prompt.SelectionEnd = 0;
                    }

                    if (window.DataContext is MainViewModel vm)
                    {
                        vm.Prompt = string.Empty;
                    }
                })
        };
    }

    private sealed record TutorialStep(
        string Title,
        string Message,
        Func<MainWindow, Control?> TargetResolver,
        string PrimaryButtonText = "Next",
        Action<MainWindow>? OnEnter = null);
}
