using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShelfLife.Lending.Contracts;
using ShelfLife.Notifications.Application;
using System.Text.Json;

namespace ShelfLife.Notifications.Infrastructure;

public sealed class NotificationDispatchWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    private static readonly HashSet<string> NotificationTopics =
    [
        "shelflife.lending.book-borrowed",
        "shelflife.lending.hold-ready",
        "shelflife.lending.loan-overdue"
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDispatchWorker> _logger;

    public NotificationDispatchWorker(IServiceScopeFactory scopeFactory, ILogger<NotificationDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in NotificationDispatchWorker");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<NotificationsDbContext>();

        var dispatchedIds = db.DispatchedNotifications.Select(d => d.MessageId);

        var messages = await db.OutboxMessages
            .Where(m => NotificationTopics.Contains(m.TopicName)
                     && !dispatchedIds.Contains(m.Id))
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var msg in messages)
        {
            try
            {
                await DispatchAsync(sp, msg.Id, msg.TopicName, msg.Payload, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to dispatch notification for {MessageId} ({Topic})",
                    msg.Id, msg.TopicName);
            }
        }
    }

    private static async Task DispatchAsync(
        IServiceProvider sp, Guid messageId, string topic, string payload, CancellationToken ct)
    {
        var db = sp.GetRequiredService<NotificationsDbContext>();

        switch (topic)
        {
            case "shelflife.lending.book-borrowed":
                var borrowed = JsonSerializer.Deserialize<BookBorrowedEvent>(payload);
                if (borrowed is not null)
                    await sp.GetRequiredService<BookBorrowedNotificationHandler>().HandleAsync(borrowed, ct);
                break;

            case "shelflife.lending.hold-ready":
                var holdReady = JsonSerializer.Deserialize<HoldReadyEvent>(payload);
                if (holdReady is not null)
                    await sp.GetRequiredService<HoldReadyNotificationHandler>().HandleAsync(holdReady, ct);
                break;

            case "shelflife.lending.loan-overdue":
                var overdue = JsonSerializer.Deserialize<LoanOverdueEvent>(payload);
                if (overdue is not null)
                    await sp.GetRequiredService<LoanOverdueNotificationHandler>().HandleAsync(overdue, ct);
                break;

            default:
                return;
        }

        db.DispatchedNotifications.Add(new DispatchedNotification { MessageId = messageId });
        await db.SaveChangesAsync(ct);
    }
}
