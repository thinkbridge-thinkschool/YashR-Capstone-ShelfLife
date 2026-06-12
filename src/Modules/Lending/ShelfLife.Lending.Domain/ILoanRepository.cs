namespace ShelfLife.Lending.Domain;

public interface ILoanRepository
{
    Task<Loan?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<Loan?> FindActiveLoanByCopyAsync(Guid copyId, CancellationToken ct = default);
    Task<IReadOnlyList<Loan>> GetOverdueLoansAsync(CancellationToken ct = default);
    Task AddAsync(Loan loan, CancellationToken ct = default);
}
