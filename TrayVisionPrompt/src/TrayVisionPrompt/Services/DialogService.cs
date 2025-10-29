using System.Windows;
using TrayVisionPrompt.Infrastructure;
using TrayVisionPrompt.Models;
using TrayVisionPrompt.Views;

namespace TrayVisionPrompt.Services;

public class DialogService
{
    private readonly IServiceLocator _serviceLocator;
    private readonly ScreenshotService _screenshotService;

    public DialogService(IServiceLocator serviceLocator)
    {
        _serviceLocator = serviceLocator;
        _screenshotService = _serviceLocator.Resolve<ScreenshotService>();
    }

    public CaptureResult? ShowAnnotationOverlay()
    {
        var overlay = new AnnotationWindow(_screenshotService);
        overlay.Owner = System.Windows.Application.Current.MainWindow;
        if (overlay.ShowDialog() == true)
        {
            return overlay.CaptureResult;
        }

        return null;
    }

    public InstructionContext? ShowInstructionDialog(CaptureResult capture, string? defaultPrompt = null, string? title = null, string? shortcutId = null)
    {
        var dialog = new InstructionDialog(capture, defaultPrompt, title, shortcutId);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() == true)
        {
            return dialog.InstructionContext;
        }

        return null;
    }

    public void ShowResponseDialog(LlmResponse response, bool allowClipboardReplacement)
    {
        var dialog = new ResponseDialog(response, allowClipboardReplacement);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.ShowDialog();
    }

    public void ShowError(string message)
    {
        System.Windows.MessageBox.Show(message, "deskLLM", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowSettingsDialog()
    {
        var dialog = new SettingsWindow(_serviceLocator.Resolve<Configuration.ConfigurationManager>());
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.ShowDialog();
    }
}

