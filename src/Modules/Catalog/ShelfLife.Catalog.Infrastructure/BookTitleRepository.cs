using Microsoft.EntityFrameworkCore;
using ShelfLife.Catalog.Domain;

namespace ShelfLife.Catalog.Infrastructure;

public sealed class BookTitleRepository : IBookTitleRepository
{
    private readonly CatalogDbContext _db;

    public BookTitleRepository(CatalogDbContext db) => _db = db;

    public Task<BookTitle?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.BookTitles.Include("_copies").FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<BookTitle?> FindByIsbnAsync(string isbn, CancellationToken ct = default) =>
        _db.BookTitles.Include("_copies").FirstOrDefaultAsync(b => b.Isbn.Value == isbn, ct);

    public async Task AddAsync(BookTitle bookTitle, CancellationToken ct = default) =>
        await _db.BookTitles.AddAsync(bookTitle, ct);
}
