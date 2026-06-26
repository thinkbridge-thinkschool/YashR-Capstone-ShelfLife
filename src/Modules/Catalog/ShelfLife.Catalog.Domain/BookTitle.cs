using ShelfLife.SharedKernel;

namespace ShelfLife.Catalog.Domain;

public enum BookTitleStatus { Available, FullyOnLoan, Unavailable }

public sealed class BookTitle : AggregateRoot<Guid>
{
    private readonly List<Copy> _copies = [];

    public Isbn Isbn { get; private set; } = null!;
    public string Title { get; private set; } = string.Empty;
    public string Author { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int PublicationYear { get; private set; }
    public BookTitleStatus Status { get; private set; } = BookTitleStatus.Unavailable;

    public IReadOnlyList<Copy> Copies => _copies.AsReadOnly();

    private BookTitle() { }

    public static BookTitle Create(Guid id, Isbn isbn, string title, string author, int publicationYear)
    {
        var book = new BookTitle
        {
            Id = id,
            Isbn = isbn,
            Title = title,
            Author = author,
            PublicationYear = publicationYear
        };
        book.Raise(new BookTitleCreatedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, id, isbn.Value, title, author));
        return book;
    }

    public Copy AddCopy(Guid copyId, CopyBarcode barcode)
    {
        if (_copies.Any(c => c.Barcode == barcode))
            throw new InvalidOperationException($"Barcode {barcode} already exists on this title.");

        var copy = Copy.Create(copyId, barcode);
        _copies.Add(copy);
        RefreshStatus();
        Raise(new CopyAddedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, Id, copyId, barcode.Value));
        return copy;
    }

    public void LoanCopy(Guid copyId, Guid loanId)
    {
        var copy = GetCopyOrThrow(copyId);
        copy.Loan(loanId);
        RefreshStatus();
    }

    public void ReturnCopy(Guid copyId)
    {
        var copy = GetCopyOrThrow(copyId);
        copy.Return();
        RefreshStatus();
    }

    public void MarkCopyLost(Guid copyId)
    {
        var copy = GetCopyOrThrow(copyId);
        copy.MarkLost();
        RefreshStatus();
        Raise(new CopyMarkedLostEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, Id, copyId));
    }

    public bool HasAvailableCopy() => _copies.Any(c => c.Status == CopyStatus.Available);

    public Copy? GetAvailableCopy() => _copies.FirstOrDefault(c => c.Status == CopyStatus.Available);

    private Copy GetCopyOrThrow(Guid copyId) =>
        _copies.FirstOrDefault(c => c.Id == copyId)
        ?? throw new InvalidOperationException($"Copy {copyId} not found.");

    private void RefreshStatus() =>
        Status = _copies.Count == 0 ? BookTitleStatus.Unavailable
               : _copies.Any(c => c.Status == CopyStatus.Available) ? BookTitleStatus.Available
               : BookTitleStatus.FullyOnLoan;
}
