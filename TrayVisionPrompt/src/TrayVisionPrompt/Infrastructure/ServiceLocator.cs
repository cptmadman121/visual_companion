using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using TrayVisionPrompt.Configuration;
using TrayVisionPrompt.Services;
using TrayVisionPrompt.ViewModels;
using TrayVisionPrompt.Views;

namespace TrayVisionPrompt.Infrastructure;

public interface IServiceLocator : IDisposable
{
    void Initialize();
    T Resolve<T>();
    ILogger Logger { get; }
}

public class ServiceLocator : IServiceLocator
{
    private readonly Dictionary<Type, object> _services = new();
    private ILoggerFactory? _loggerFactory;
    private Serilog.ILogger? _serilogLogger;

    public ILogger Logger { get; private set; } = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    public void Initialize()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "TrayVisionPrompt");
        Directory.CreateDirectory(appFolder);

        var configurationManager = new ConfigurationManager(appFolder, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        configurationManager.Load();

        var levelSwitch = new LoggingLevelSwitch(MapLogLevel(configurationManager.CurrentConfiguration.LogLevel));

        var logFile = Path.Combine(appFolder, "TrayVisionPrompt.log");
        _serilogLogger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.File(logFile, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();

        _loggerFactory = new LoggerFactory(new[] { new SerilogLoggerProvider(_serilogLogger) });
        Logger = _loggerFactory.CreateLogger("TrayVisionPrompt");
        configurationManager.UpdateLogger(Logger);

        RegisterSingleton(configurationManager);
        RegisterSingleton(Logger);

        RegisterSingleton(new ResponseCache());

        RegisterSingleton(new HotkeyService(Logger));
        RegisterSingleton(new ScreenshotService(Logger));
        RegisterSingleton(new OcrService(Logger));
        RegisterSingleton(new DialogService(this));
        RegisterSingleton(new TrayIconService(this));
        RegisterSingleton<IOllmClientFactory>(new OllmClientFactory(Logger));
        RegisterSingleton(new ForegroundTextService());

        RegisterSingleton(new CaptureWorkflow(
            Resolve<DialogService>(),
            Resolve<IOllmClientFactory>(),
            Resolve<OcrService>(),
            configurationManager,
            Resolve<ResponseCache>(),
            _loggerFactory.CreateLogger<CaptureWorkflow>()));

        RegisterSingleton(new TextWorkflow(
            Resolve<ForegroundTextService>(),
            Resolve<IOllmClientFactory>(),
            configurationManager,
            Resolve<DialogService>(),
            Resolve<ResponseCache>(),
            _loggerFactory.CreateLogger<TextWorkflow>()));

        RegisterSingleton(new ViewModels.ShellViewModel(
            Resolve<TrayIconService>(),
            Resolve<HotkeyService>(),
            Resolve<CaptureWorkflow>(),
            Resolve<TextWorkflow>(),
            Resolve<DialogService>(),
            configurationManager,
            _loggerFactory.CreateLogger<ViewModels.ShellViewModel>()));

        RegisterSingleton(new Views.ShellWindow(Resolve<ViewModels.ShellViewModel>()));
    }

    public T Resolve<T>()
    {
        if (_services.TryGetValue(typeof(T), out var service))
        {
            return (T)service;
        }

        throw new InvalidOperationException($"Service {typeof(T).Name} not registered");
    }

    public void Dispose()
    {
        foreach (var service in _services.Values)
        {
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _loggerFactory?.Dispose();
        if (_serilogLogger is IDisposable disposableLogger)
        {
            disposableLogger.Dispose();
        }
    }

    private void RegisterSingleton<T>(T instance)
    {
        _services[typeof(T)] = instance!;
    }

    private static LogEventLevel MapLogLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "trace" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "info" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}
