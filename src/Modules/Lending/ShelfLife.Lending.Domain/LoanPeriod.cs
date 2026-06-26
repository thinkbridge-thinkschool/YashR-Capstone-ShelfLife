using ShelfLife.SharedKernel;

namespace ShelfLife.Lending.Domain;

public sealed class LoanPeriod : ValueObject
{
    public DateTimeOffset BorrowedAt { get; }
    public DateTimeOffset DueDate { get; }

    private LoanPeriod(DateTimeOffset borrowedAt, DateTimeOffset dueDate)
    {
        BorrowedAt = borrowedAt;
        DueDate = dueDate;
    }

    public static LoanPeriod Create(DateTimeOffset borrowedAt, int loanDays = 14)
    {
        if (loanDays <= 0) throw new ArgumentException("Loan days must be positive.");
        return new LoanPeriod(borrowedAt, borrowedAt.AddDays(loanDays));
    }

    public bool IsOverdue(DateTimeOffset now) => now > DueDate;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return BorrowedAt;
        yield return DueDate;
    }
}
