using Microsoft.EntityFrameworkCore;
using ShelfLife.Catalog.Application;
using ShelfLife.Catalog.Domain;
using ShelfLife.SharedKernel;

namespace ShelfLife.Catalog.Infrastructure;

public sealed class BooksReadModel : IBooksReadModel
{
    private readonly CatalogDbContext _db;

    public BooksReadModel(CatalogDbContext db) => _db = db;

    public async Task<PagedList<BookSummaryDto>> GetBooksAsync(int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = _db.BookTitles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(bt => bt.Title.Contains(search) || bt.Author.Contains(search));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(bt => bt.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(bt => new BookSummaryDto(
                bt.Id,
                bt.Title,
                bt.Author,
                bt.Isbn.Value,
                bt.Status.ToString(),
                bt.Copies.Count(c => c.Status == CopyStatus.Available),
                bt.Copies.Count))
            .ToListAsync(ct);

        return new PagedList<BookSummaryDto>(items, page, pageSize, total);
    }
}
