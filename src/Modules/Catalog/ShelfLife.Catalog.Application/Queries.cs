using ShelfLife.SharedKernel;

namespace ShelfLife.Catalog.Application;

public sealed record BookSummaryDto(
    Guid BookTitleId,
    string Title,
    string Author,
    string Isbn,
    string Status,
    int AvailableCopies,
    int TotalCopies);

public interface IBooksReadModel
{
    Task<PagedList<BookSummaryDto>> GetBooksAsync(int page, int pageSize, string? search, CancellationToken ct = default);
}

public sealed record GetBooksQuery(int Page = 1, int PageSize = 20, string? Search = null);

public sealed class GetBooksHandler
{
    private readonly IBooksReadModel _readModel;
    public GetBooksHandler(IBooksReadModel readModel) => _readModel = readModel;
    public Task<PagedList<BookSummaryDto>> HandleAsync(GetBooksQuery q, CancellationToken ct = default) =>
        _readModel.GetBooksAsync(q.Page, q.PageSize, q.Search, ct);
}
