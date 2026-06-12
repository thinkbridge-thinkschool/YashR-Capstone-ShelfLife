using Microsoft.Extensions.Logging;
using ShelfLife.Notifications.Application;

namespace ShelfLife.Notifications.Infrastructure;

public sealed class SmtpNotificationSender : INotificationSender
{
    private readonly ILogger<SmtpNotificationSender> _logger;
    private readonly NotificationsDbContext _db;

    public SmtpNotificationSender(ILogger<SmtpNotificationSender> logger, NotificationsDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task SendAsync(NotificationRequest request, CancellationToken ct = default)
    {
        // Production: replace with SendGrid/SMTP client
        _logger.LogInformation("Sending {Channel} to {Email}: {Subject}",
            request.Channel, request.RecipientEmail, request.Subject);

        var log = new DeliveryLog
        {
            RecipientId = request.RecipientId,
            RecipientEmail = request.RecipientEmail,
            Subject = request.Subject,
            Channel = request.Channel.ToString(),
            SentAt = DateTimeOffset.UtcNow,
            Success = true
        };
        _db.DeliveryLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }
}
