using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using TrayVisionPrompt.Avalonia.Views;

namespace TrayVisionPrompt.Avalonia.Services;

/// <summary>
/// Shows a user-facing hint when an operation takes longer than expected.
/// </summary>
public static class SlowOperationNotifier
{
    private static readonly TimeSpan DefaultThreshold = TimeSpan.FromSeconds(10);
    public const string BusyServerMessage = "This is taking longer than expected. The LLM server is likely busy at the moment.";

    public static void NotifyIfSlow(Task monitoredTask, Func<Task> showPopup, TimeSpan? threshold = null, CancellationToken cancellationToken = default)
    {
        _ = MonitorAsync(monitoredTask, showPopup, threshold ?? DefaultThreshold, cancellationToken);
    }

    public static async Task ShowBusyMessageAsync(ResponseDialog dialog)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var current = dialog.ResponseText ?? string.Empty;
            if (current.Contains(BusyServerMessage, StringComparison.Ordinal))
            {
                return;
            }

            var trimmed = current.TrimEnd();
            dialog.ResponseText = string.IsNullOrWhiteSpace(trimmed)
                ? BusyServerMessage
                : $"{trimmed}\n\n{BusyServerMessage}";
        });
    }

    private static async Task MonitorAsync(Task monitoredTask, Func<Task> showPopup, TimeSpan threshold, CancellationToken cancellationToken)
    {
        try
        {
            var completed = await Task.WhenAny(monitoredTask, Task.Delay(threshold, cancellationToken));
            if (completed != monitoredTask && !monitoredTask.IsCompleted && !cancellationToken.IsCancellationRequested)
            {
                await showPopup();
            }
        }
        catch (OperationCanceledException)
        {
            // Swallow if the operation completed before the threshold or was cancelled.
        }
        catch
        {
            // Never let the notifier crash the caller.
        }
    }
}
