using ShelfLife.SharedKernel;

namespace ShelfLife.Lending.Domain;

public sealed record LoanCreatedDomainEvent(
    Guid EventId, DateTimeOffset OccurredAt,
    Guid LoanId, Guid MemberId, Guid BookTitleId, Guid CopyId, DateTimeOffset DueDate) : IDomainEvent;

public sealed record LoanReturnedDomainEvent(
    Guid EventId, DateTimeOffset OccurredAt,
    Guid LoanId, Guid MemberId, Guid BookTitleId, Guid CopyId) : IDomainEvent;

public sealed record HoldPlacedDomainEvent(
    Guid EventId, DateTimeOffset OccurredAt,
    Guid HoldId, Guid MemberId, Guid BookTitleId) : IDomainEvent;

public sealed record HoldReadyDomainEvent(
    Guid EventId, DateTimeOffset OccurredAt,
    Guid HoldId, Guid MemberId, Guid BookTitleId, DateTimeOffset ExpiresAt) : IDomainEvent;
