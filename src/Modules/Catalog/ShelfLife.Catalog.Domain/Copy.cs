using ShelfLife.SharedKernel;

namespace ShelfLife.Catalog.Domain;

public enum CopyCondition { Good, Damaged, Lost }
public enum CopyStatus { Available, OnLoan, Lost }

public sealed class Copy : Entity<Guid>
{
    public CopyBarcode Barcode { get; private set; } = null!;
    public CopyCondition Condition { get; private set; } = CopyCondition.Good;
    public CopyStatus Status { get; private set; } = CopyStatus.Available;
    public Guid? CurrentLoanId { get; private set; }

    private Copy() { }

    internal static Copy Create(Guid id, CopyBarcode barcode)
        => new() { Id = id, Barcode = barcode };

    internal void Loan(Guid loanId)
    {
        if (Status != CopyStatus.Available)
            throw new InvalidOperationException($"Copy {Barcode} is not available.");
        Status = CopyStatus.OnLoan;
        CurrentLoanId = loanId;
    }

    internal void Return()
    {
        Status = CopyStatus.Available;
        CurrentLoanId = null;
    }

    internal void MarkLost()
    {
        Status = CopyStatus.Lost;
        Condition = CopyCondition.Lost;
    }
}
