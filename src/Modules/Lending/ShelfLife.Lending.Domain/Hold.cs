using ShelfLife.SharedKernel;

namespace ShelfLife.Lending.Domain;

public enum HoldStatus { Pending, Ready, Cancelled, Expired }

public sealed class Hold : Entity<Guid>
{
    public Guid MemberId { get; private set; }
    public Guid BookTitleId { get; private set; }
    public DateTimeOffset PlacedAt { get; private set; }
    public DateTimeOffset? ReadyAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public HoldStatus Status { get; private set; } = HoldStatus.Pending;

    private Hold() { }

    internal static Hold Create(Guid id, Guid memberId, Guid bookTitleId) =>
        new() { Id = id, MemberId = memberId, BookTitleId = bookTitleId, PlacedAt = DateTimeOffset.UtcNow };

    internal void MarkReady(int pickupWindowDays = 3)
    {
        Status = HoldStatus.Ready;
        ReadyAt = DateTimeOffset.UtcNow;
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(pickupWindowDays);
    }

    internal void Cancel() => Status = HoldStatus.Cancelled;

    internal void Expire() => Status = HoldStatus.Expired;
}
