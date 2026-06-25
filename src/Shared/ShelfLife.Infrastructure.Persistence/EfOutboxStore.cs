using Microsoft.EntityFrameworkCore;
using ShelfLife.Infrastructure.Outbox;

namespace ShelfLife.Infrastructure.Persistence;

public sealed class EfOutboxStore : IOutboxStore
{
    private readonly ShelfLifeDbContext _db;

    public EfOutboxStore(ShelfLifeDbContext db) => _db = db;

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _db.OutboxMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.OutboxMessages
            .Where(m => m.ProcessedAt == null &&
                        (m.NextRetryAt == null || m.NextRetryAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await _db.OutboxMessages.FindAsync([id], cancellationToken);
        if (message is null) return;
        message.ProcessedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        var message = await _db.OutboxMessages.FindAsync([id], cancellationToken);
        if (message is null) return;
        message.RetryCount++;
        message.Error = error;
        message.NextRetryAt = DateTimeOffset.UtcNow.Add(
            OutboxRelayProcessor.ComputeRetryDelay(message.RetryCount));
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveToDeadLetterAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        var message = await _db.OutboxMessages.FindAsync([id], cancellationToken);
        if (message is null) return;

        var deadLetter = new DeadLetterMessage
        {
            OriginalMessageId = message.Id,
            Type = message.Type,
            Payload = message.Payload,
            TopicName = message.TopicName,
            CreatedAt = message.CreatedAt,
            LastError = error,
            RetryCount = message.RetryCount + 1,
        };

        _db.DeadLetterMessages.Add(deadLetter);
        _db.OutboxMessages.Remove(message);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
