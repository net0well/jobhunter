namespace JobHunter.Api.Common.Notifications;

internal interface INotifier
{
    Task NotifyAsync(Notification notification, CancellationToken ct);
}
