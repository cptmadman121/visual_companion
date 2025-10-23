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
        overlay.Owner = Application.Current.MainWindow;
        if (overlay.ShowDialog() == true)
        {
            return overlay.CaptureResult;
        }

        return null;
    }

    public InstructionContext? ShowInstructionDialog(CaptureResult capture)
    {
        var dialog = new InstructionDialog(capture);
        dialog.Owner = Application.Current.MainWindow;
        if (dialog.ShowDialog() == true)
        {
            return dialog.InstructionContext;
        }

        return null;
    }

    public void ShowResponseDialog(LlmResponse response, bool allowClipboardReplacement)
    {
        var dialog = new ResponseDialog(response, allowClipboardReplacement);
        dialog.Owner = Application.Current.MainWindow;
        dialog.ShowDialog();
    }

    public void ShowError(string message)
    {
        MessageBox.Show(message, "TrayVisionPrompt", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowSettingsDialog()
    {
        var dialog = new SettingsWindow(_serviceLocator.Resolve<Configuration.ConfigurationManager>());
        dialog.Owner = Application.Current.MainWindow;
        dialog.ShowDialog();
    }
}
