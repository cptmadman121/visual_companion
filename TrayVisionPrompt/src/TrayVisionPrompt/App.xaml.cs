using System;
using System.Windows;
using Microsoft.Extensions.Logging;
using TrayVisionPrompt.Infrastructure;

namespace TrayVisionPrompt;

public partial class App : System.Windows.Application
{
    private IServiceLocator? _serviceLocator;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _serviceLocator = new ServiceLocator();
            _serviceLocator.Initialize();
            _serviceLocator.Logger.LogInformation("deskLLM starting up");

            var shell = _serviceLocator.Resolve<Views.ShellWindow>();
            MainWindow = shell;
            shell.Show();
            shell.Hide();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"deskLLM konnte nicht gestartet werden: {ex.Message}",
                "deskLLM", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_serviceLocator != null)
        {
            _serviceLocator.Logger.LogInformation("deskLLM shutting down");
            _serviceLocator.Dispose();
        }

        base.OnExit(e);
    }
}

