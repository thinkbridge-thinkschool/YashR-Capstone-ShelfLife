using ShelfLife.SharedKernel;

namespace ShelfLife.Catalog.Domain;

public sealed record BookTitleCreatedEvent(
    Guid EventId, DateTimeOffset OccurredAt,
    Guid BookTitleId, string Isbn, string Title, string Author) : IDomainEvent;

public sealed record CopyAddedEvent(
    Guid EventId, DateTimeOffset OccurredAt,
    Guid BookTitleId, Guid CopyId, string Barcode) : IDomainEvent;

public sealed record CopyMarkedLostEvent(
    Guid EventId, DateTimeOffset OccurredAt,
    Guid BookTitleId, Guid CopyId) : IDomainEvent;
