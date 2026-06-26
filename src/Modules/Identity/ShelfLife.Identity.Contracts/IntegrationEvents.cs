using ShelfLife.SharedKernel;

namespace ShelfLife.Identity.Contracts;

public sealed record MemberRegisteredEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid MemberId,
    string Email,
    string FullName) : IDomainEvent;
