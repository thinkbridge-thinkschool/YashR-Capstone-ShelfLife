using ShelfLife.SharedKernel;

namespace ShelfLife.Lending.Contracts;

public sealed record BookBorrowedEvent(
    Guid EventId, DateTimeOffset OccurredAt,
    Guid LoanId, Guid MemberId, Guid BookTitleId, Guid CopyId,
    DateTimeOffset DueDate) : IDomainEvent;

public sealed record BookReturnedEvent(
    Guid EventId, DateTimeOffset OccurredAt,
    Guid LoanId, Guid MemberId, Guid BookTitleId, Guid CopyId) : IDomainEvent;

public sealed record HoldReadyEvent(
    Guid EventId, DateTimeOffset OccurredAt,
    Guid HoldId, Guid MemberId, Guid BookTitleId,
    DateTimeOffset ExpiresAt) : IDomainEvent;

public sealed record HoldPlacedEvent(
    Guid EventId, DateTimeOffset OccurredAt,
    Guid HoldId, Guid MemberId, Guid BookTitleId) : IDomainEvent;

public sealed record LoanOverdueEvent(
    Guid EventId, DateTimeOffset OccurredAt,
    Guid LoanId, Guid MemberId, Guid BookTitleId,
    DateTimeOffset DueDate) : IDomainEvent;
