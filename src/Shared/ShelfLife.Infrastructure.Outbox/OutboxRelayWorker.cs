using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShelfLife.Infrastructure.Messaging;

namespace ShelfLife.Infrastructure.Outbox;

public sealed class OutboxRelayWorker : BackgroundService
{
    private readonly IOutboxStore _store;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OutboxRelayWorker> _logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    public OutboxRelayWorker(IOutboxStore store, IMessagePublisher publisher, ILogger<OutboxRelayWorker> logger)
    {
        _store = store;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var messages = await _store.GetPendingAsync(batchSize: 20, cancellationToken);
        foreach (var message in messages)
        {
            try
            {
                await _publisher.PublishAsync(message.TopicName, message.Payload, cancellationToken);
                await _store.MarkProcessedAsync(message.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to relay outbox message {MessageId}", message.Id);
                await _store.MarkFailedAsync(message.Id, ex.Message, cancellationToken);
            }
        }
    }
}
