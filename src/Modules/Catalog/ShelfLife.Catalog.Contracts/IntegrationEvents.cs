using ShelfLife.SharedKernel;

namespace ShelfLife.Catalog.Contracts;

public sealed record CatalogItemAddedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid BookTitleId,
    string Isbn,
    string Title,
    string Author) : IDomainEvent;
