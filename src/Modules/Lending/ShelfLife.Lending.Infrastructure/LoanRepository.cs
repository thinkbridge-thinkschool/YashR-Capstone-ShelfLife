using Microsoft.EntityFrameworkCore;
using ShelfLife.Lending.Domain;

namespace ShelfLife.Lending.Infrastructure;

public sealed class LoanRepository : ILoanRepository
{
    private readonly LendingDbContext _db;

    public LoanRepository(LendingDbContext db) => _db = db;

    public Task<Loan?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Loans.Include("_holds").FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<Loan?> FindActiveLoanByCopyAsync(Guid copyId, CancellationToken ct = default) =>
        _db.Loans.Include("_holds")
            .FirstOrDefaultAsync(l => l.CopyId == copyId && l.Status == LoanStatus.Active, ct);

    public async Task<IReadOnlyList<Loan>> GetOverdueLoansAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.Loans
            .Where(l => l.Status == LoanStatus.Active && l.Period.DueDate < now && l.ReminderSentAt == null)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Loan loan, CancellationToken ct = default) =>
        await _db.Loans.AddAsync(loan, ct);
}
