using ShelfLife.Catalog.Domain;
using ShelfLife.SharedKernel;

namespace ShelfLife.Catalog.Application;

// ── Add Book By ISBN ──────────────────────────────────────────────────────────

public sealed record AddBookByIsbnCommand(string Isbn);

public sealed class AddBookByIsbnHandler
{
    private readonly IBookTitleRepository _repo;
    private readonly IIsbnEnrichmentService _isbn;
    private readonly IUnitOfWork _uow;

    public AddBookByIsbnHandler(IBookTitleRepository repo, IIsbnEnrichmentService isbn, IUnitOfWork uow)
    {
        _repo = repo;
        _isbn = isbn;
        _uow = uow;
    }

    public async Task<Result<Guid>> HandleAsync(AddBookByIsbnCommand cmd, CancellationToken ct = default)
    {
        var isbnVo = Isbn.Create(cmd.Isbn);
        var existing = await _repo.FindByIsbnAsync(isbnVo.Value, ct);
        if (existing is not null)
            return Result.Failure<Guid>("Book with this ISBN already exists.");

        var metadata = await _isbn.LookupAsync(isbnVo.Value, ct); // http call to open library api
        if (metadata is null)
            return Result.Failure<Guid>("ISBN not found in external catalog.");

        var book = BookTitle.Create(Guid.NewGuid(), isbnVo, metadata.Title, metadata.Author, metadata.PublicationYear);
        await _repo.AddAsync(book, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(book.Id);
    }
}

// ── Add Book Manually (no ISBN lookup) ───────────────────────────────────────

public sealed record AddBookManuallyCommand(string Title, string Author, int PublicationYear);

public sealed class AddBookManuallyHandler
{
    private readonly IBookTitleRepository _repo;
    private readonly IUnitOfWork _uow;

    public AddBookManuallyHandler(IBookTitleRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Result<Guid>> HandleAsync(AddBookManuallyCommand cmd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cmd.Title) || string.IsNullOrWhiteSpace(cmd.Author))
            return Result.Failure<Guid>("Title and author are required.");

        if (cmd.PublicationYear < 1000 || cmd.PublicationYear > DateTimeOffset.UtcNow.Year + 1)
            return Result.Failure<Guid>("Publication year is not valid.");

        var id = Guid.NewGuid();
        var isbn = Isbn.CreateManual(id);

        var collision = await _repo.FindByIsbnAsync(isbn.Value, ct);
        if (collision is not null)
            return Result.Failure<Guid>("Internal ID collision — please try again.");

        var book = BookTitle.Create(id, isbn, cmd.Title.Trim(), cmd.Author.Trim(), cmd.PublicationYear);
        await _repo.AddAsync(book, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(book.Id);
    }
}

// ── Add Copy ──────────────────────────────────────────────────────────────────

public sealed record AddCopyCommand(Guid BookTitleId, string Barcode);

public sealed class AddCopyHandler
{
    private readonly IBookTitleRepository _repo;
    private readonly IUnitOfWork _uow;

    public AddCopyHandler(IBookTitleRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Result<Guid>> HandleAsync(AddCopyCommand cmd, CancellationToken ct = default)
    {
        var book = await _repo.FindByIdAsync(cmd.BookTitleId, ct);
        if (book is null)
            return Result.Failure<Guid>("Book title not found.");

        var barcode = CopyBarcode.Create(cmd.Barcode);
        var copy = book.AddCopy(Guid.NewGuid(), barcode);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(copy.Id);
    }
}
