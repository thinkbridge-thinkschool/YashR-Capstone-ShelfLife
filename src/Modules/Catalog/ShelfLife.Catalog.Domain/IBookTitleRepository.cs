namespace ShelfLife.Catalog.Domain;

public interface IBookTitleRepository
{
    Task<BookTitle?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<BookTitle?> FindByIsbnAsync(string isbn, CancellationToken ct = default);
    Task AddAsync(BookTitle bookTitle, CancellationToken ct = default);
}
