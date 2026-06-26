namespace ShelfLife.Lending.Infrastructure;

public sealed class AuditLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Action { get; init; } = string.Empty;
    public Guid ActorId { get; init; }
    public Guid? LoanId { get; init; }
    public Guid? BookTitleId { get; init; }
    public Guid? CopyId { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
