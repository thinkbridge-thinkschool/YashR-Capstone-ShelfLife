using Microsoft.Extensions.Logging;
using ShelfLife.Infrastructure.Messaging;

namespace ShelfLife.Infrastructure.Outbox;

public sealed class OutboxRelayProcessor
{
    public const int MaxRetries = 5;

    // Base delay for exponential backoff: 30 s * 2^(retryCount-1)
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(30);

    private readonly IOutboxStore _store;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OutboxRelayProcessor> _logger;

    public OutboxRelayProcessor(
        IOutboxStore store,
        IMessagePublisher publisher,
        ILogger<OutboxRelayProcessor> logger)
    {
        _store = store;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var messages = await _store.GetPendingAsync(batchSize: 20, cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await _publisher.PublishAsync(message.TopicName, message.Payload, cancellationToken);
                await _store.MarkProcessedAsync(message.Id, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "Failed to relay outbox message {MessageId} (type={Type}, attempt={Attempt})",
                    message.Id, message.Type, message.RetryCount + 1);

                if (message.RetryCount + 1 >= MaxRetries)
                {
                    await _store.MoveToDeadLetterAsync(message.Id, ex.Message, cancellationToken);

                    _logger.LogCritical(
                        "Outbox message {MessageId} (type={Type}) dead-lettered after {MaxRetries} retries — manual intervention required",
                        message.Id, message.Type, MaxRetries);
                }
                else
                {
                    await _store.MarkFailedAsync(message.Id, ex.Message, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Computes the next retry delay for a given retry count using 2x exponential backoff.
    /// retryCount should be the value AFTER incrementing (i.e. 1 on first failure).
    /// </summary>
    public static TimeSpan ComputeRetryDelay(int retryCount)
        => TimeSpan.FromTicks((long)(BaseRetryDelay.Ticks * Math.Pow(2, retryCount - 1)));
}
