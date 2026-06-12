using ShelfLife.Catalog.Domain;
using ShelfLife.Lending.Domain;
using ShelfLife.SharedKernel;

namespace ShelfLife.Lending.Application;

// ── Borrow Book ───────────────────────────────────────────────────────────────

public sealed record BorrowBookCommand(Guid MemberId, Guid BookTitleId);
public sealed record BorrowBookResult(Guid LoanId, DateTimeOffset DueDate);

public sealed class BorrowBookHandler
{
    private readonly ILoanRepository _loans;
    private readonly IBookTitleRepository _books;
    private readonly IUnitOfWork _uow;

    public BorrowBookHandler(ILoanRepository loans, IBookTitleRepository books, IUnitOfWork uow)
    {
        _loans = loans;
        _books = books;
        _uow = uow;
    }

    public async Task<Result<BorrowBookResult>> HandleAsync(BorrowBookCommand cmd, CancellationToken ct = default)
    {
        var book = await _books.FindByIdAsync(cmd.BookTitleId, ct);
        if (book is null) return Result.Failure<BorrowBookResult>("Book title not found.");

        var copy = book.GetAvailableCopy();
        if (copy is null) return Result.Failure<BorrowBookResult>("No copy available. Place a hold instead.");

        var period = LoanPeriod.Create(DateTimeOffset.UtcNow);
        var loan = Loan.Create(Guid.NewGuid(), cmd.MemberId, cmd.BookTitleId, copy.Id, period);
        book.LoanCopy(copy.Id, loan.Id);

        await _loans.AddAsync(loan, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(new BorrowBookResult(loan.Id, period.DueDate));
    }
}

// ── Return Book ───────────────────────────────────────────────────────────────

public sealed record ReturnBookCommand(Guid LoanId);

public sealed class ReturnBookHandler
{
    private readonly ILoanRepository _loans;
    private readonly IBookTitleRepository _books;
    private readonly IUnitOfWork _uow;

    public ReturnBookHandler(ILoanRepository loans, IBookTitleRepository books, IUnitOfWork uow)
    {
        _loans = loans;
        _books = books;
        _uow = uow;
    }

    public async Task<Result> HandleAsync(ReturnBookCommand cmd, CancellationToken ct = default)
    {
        var loan = await _loans.FindByIdAsync(cmd.LoanId, ct);
        if (loan is null) return Result.Failure("Loan not found.");
        if (loan.Status == LoanStatus.Returned) return Result.Failure("Loan already returned.");

        var book = await _books.FindByIdAsync(loan.BookTitleId, ct);
        if (book is null) return Result.Failure("Book title not found.");

        loan.Return();
        book.ReturnCopy(loan.CopyId);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ── Place Hold ────────────────────────────────────────────────────────────────

public sealed record PlaceHoldCommand(Guid MemberId, Guid BookTitleId);

public sealed class PlaceHoldHandler
{
    private readonly ILoanRepository _loans;
    private readonly IBookTitleRepository _books;
    private readonly IUnitOfWork _uow;

    public PlaceHoldHandler(ILoanRepository loans, IBookTitleRepository books, IUnitOfWork uow)
    {
        _loans = loans;
        _books = books;
        _uow = uow;
    }

    public async Task<Result<Guid>> HandleAsync(PlaceHoldCommand cmd, CancellationToken ct = default)
    {
        var book = await _books.FindByIdAsync(cmd.BookTitleId, ct);
        if (book is null) return Result.Failure<Guid>("Book title not found.");
        if (book.HasAvailableCopy()) return Result.Failure<Guid>("Book is available — borrow it directly.");

        var activeLoan = await _loans.FindActiveLoanByCopyAsync(
            book.Copies.First(c => c.Status == Catalog.Domain.CopyStatus.OnLoan).Id, ct);

        if (activeLoan is null) return Result.Failure<Guid>("No active loan found to place hold against.");

        var hold = activeLoan.PlaceHold(Guid.NewGuid(), cmd.MemberId);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(hold.Id);
    }
}
