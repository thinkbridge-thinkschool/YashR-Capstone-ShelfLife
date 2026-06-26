using Microsoft.EntityFrameworkCore;
using ShelfLife.Notifications.Application;

namespace ShelfLife.Notifications.Infrastructure;

public sealed class EfIdempotencyStore : IIdempotencyStore
{
    private readonly NotificationsDbContext _db;

    public EfIdempotencyStore(NotificationsDbContext db) => _db = db;

    public Task<bool> HasBeenProcessedAsync(Guid eventId, CancellationToken ct = default) =>
        _db.IdempotencyKeys.AnyAsync(k => k.EventId == eventId, ct);

    public async Task MarkProcessedAsync(Guid eventId, CancellationToken ct = default)
    {
        _db.IdempotencyKeys.Add(new IdempotencyKey { EventId = eventId, ProcessedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(ct);
    }
}
