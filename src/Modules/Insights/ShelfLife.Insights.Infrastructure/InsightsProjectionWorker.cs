using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShelfLife.Lending.Contracts;
using System.Text.Json;

namespace ShelfLife.Insights.Infrastructure;

public sealed class InsightsProjectionWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    private static readonly HashSet<string> LendingTopics =
    [
        "shelflife.lending.book-borrowed",
        "shelflife.lending.book-returned",
        "shelflife.lending.loan-overdue"
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InsightsProjectionWorker> _logger;

    public InsightsProjectionWorker(IServiceScopeFactory scopeFactory, ILogger<InsightsProjectionWorker> logger)
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
                _logger.LogError(ex, "Unhandled error in InsightsProjectionWorker");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<InsightsDbContext>();

        var processedIds = db.ProcessedProjectionEvents.Select(p => p.MessageId);

        var messages = await db.OutboxMessages
            .Where(m => LendingTopics.Contains(m.TopicName)
                     && !processedIds.Contains(m.Id))
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
                _logger.LogError(ex, "Failed to project event {MessageId} ({Topic})", msg.Id, msg.TopicName);
            }
        }
    }

    private static async Task DispatchAsync(
        IServiceProvider sp, Guid messageId, string topic, string payload, CancellationToken ct)
    {
        switch (topic)
        {
            case "shelflife.lending.book-borrowed":
                var borrowed = JsonSerializer.Deserialize<BookBorrowedEvent>(payload);
                if (borrowed is not null)
                    await sp.GetRequiredService<BookBorrowedProjectionHandler>().HandleAsync(messageId, borrowed, ct);
                break;

            case "shelflife.lending.book-returned":
                var returned = JsonSerializer.Deserialize<BookReturnedEvent>(payload);
                if (returned is not null)
                    await sp.GetRequiredService<BookReturnedProjectionHandler>().HandleAsync(messageId, returned, ct);
                break;

            case "shelflife.lending.loan-overdue":
                var overdue = JsonSerializer.Deserialize<LoanOverdueEvent>(payload);
                if (overdue is not null)
                    await sp.GetRequiredService<LoanOverdueProjectionHandler>().HandleAsync(messageId, overdue, ct);
                break;
        }
    }
}
