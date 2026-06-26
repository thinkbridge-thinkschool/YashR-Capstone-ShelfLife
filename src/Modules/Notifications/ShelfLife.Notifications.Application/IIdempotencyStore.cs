namespace ShelfLife.Notifications.Application;

public interface IIdempotencyStore
{
    Task<bool> HasBeenProcessedAsync(Guid eventId, CancellationToken ct = default);
    Task MarkProcessedAsync(Guid eventId, CancellationToken ct = default);
}
