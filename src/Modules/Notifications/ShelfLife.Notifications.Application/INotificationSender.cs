namespace ShelfLife.Notifications.Application;

public enum NotificationChannel { Email, Sms }

public sealed record NotificationRequest(
    Guid RecipientId,
    string RecipientEmail,
    string Subject,
    string Body,
    NotificationChannel Channel = NotificationChannel.Email);

public interface INotificationSender
{
    Task SendAsync(NotificationRequest request, CancellationToken ct = default);
}
