using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShelfLife.Notifications.Application;
using System.Net;
using System.Net.Mail;

namespace ShelfLife.Notifications.Infrastructure;

public sealed class SmtpNotificationSender : INotificationSender
{
    private readonly ILogger<SmtpNotificationSender> _logger;
    private readonly NotificationsDbContext _db;
    private readonly IConfiguration _config;

    public SmtpNotificationSender(
        ILogger<SmtpNotificationSender> logger,
        NotificationsDbContext db,
        IConfiguration config)
    {
        _logger = logger;
        _db = db;
        _config = config;
    }

    public async Task SendAsync(NotificationRequest request, CancellationToken ct = default)
    {
        var smtpHost = _config["Smtp:Host"];

        if (!string.IsNullOrWhiteSpace(smtpHost))
        {
            await SendViaSmtpAsync(request, smtpHost, ct);
        }
        else
        {
            // Dev mode: no SMTP configured — log the full email so it's visible in Seq/console.
            _logger.LogWarning(
                "[DEV — SMTP not configured] {Channel} notification would be sent | " +
                "To: {Email} | Subject: {Subject} | Body: {Body}",
                request.Channel, request.RecipientEmail, request.Subject, request.Body);
        }

        _db.DeliveryLogs.Add(new DeliveryLog
        {
            RecipientId    = request.RecipientId,
            RecipientEmail = request.RecipientEmail,
            Subject        = request.Subject,
            Channel        = request.Channel.ToString(),
            SentAt         = DateTimeOffset.UtcNow,
            Success        = true
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task SendViaSmtpAsync(NotificationRequest request, string host, CancellationToken ct)
    {
        var port      = _config.GetValue("Smtp:Port", 25);
        var enableSsl = _config.GetValue("Smtp:EnableSsl", false);
        var from      = _config["Smtp:FromAddress"] ?? "noreply@shelflife.dev";
        var fromName  = _config["Smtp:FromName"] ?? "ShelfLife Library";
        var user      = _config["Smtp:User"];
        var password  = _config["Smtp:Password"];

#pragma warning disable SYSLIB0006  // SmtpClient is superseded by MailKit but available without extra packages
        using var smtp = new SmtpClient(host, port) { EnableSsl = enableSsl };
        if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password))
            smtp.Credentials = new NetworkCredential(user, password);

        using var mail = new MailMessage(
            new MailAddress(from, fromName),
            new MailAddress(request.RecipientEmail))
        {
            Subject = request.Subject,
            Body    = request.Body,
            IsBodyHtml = false
        };

        await smtp.SendMailAsync(mail, ct);
#pragma warning restore SYSLIB0006

        _logger.LogInformation("SMTP notification sent: {Channel} → {Email} — {Subject}",
            request.Channel, request.RecipientEmail, request.Subject);
    }
}
