namespace JobHunter.Api.Common.Notifications;

internal sealed class LogNotifier(ILogger<LogNotifier> logger) : INotifier
{
    public Task NotifyAsync(Notification notification, CancellationToken ct)
    {
        logger.LogWarning("Notification {Kind}: {Message}", notification.Kind, notification.Message);
        return Task.CompletedTask;
    }
}
