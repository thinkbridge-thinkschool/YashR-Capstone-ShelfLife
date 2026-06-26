using Microsoft.EntityFrameworkCore;
using ShelfLife.Insights.Application;
using ShelfLife.Insights.Contracts;
using ShelfLife.SharedKernel;

namespace ShelfLife.Insights.Infrastructure;

public sealed class InsightsReadModel : IInsightsReadModel
{
    private readonly InsightsDbContext _db;

    public InsightsReadModel(InsightsDbContext db) => _db = db;

    public async Task<PagedList<PopularTitleDto>> GetPopularTitlesAsync(int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = _db.PopularTitleProjections.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => EF.Functions.Like(x.Title, $"%{search}%") || EF.Functions.Like(x.Author, $"%{search}%"));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.BorrowCount)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new PopularTitleDto(x.BookTitleId, x.Title, x.Author, x.BorrowCount))
            .ToListAsync(ct);
        return new PagedList<PopularTitleDto>(items, page, pageSize, total);
    }

    public async Task<PagedList<OverdueLoanDto>> GetOverdueLoansAsync(int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var query = _db.OverdueLoanProjections.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => EF.Functions.Like(x.MemberName, $"%{search}%") || EF.Functions.Like(x.BookTitle, $"%{search}%"));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.DueDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new OverdueLoanDto(x.LoanId, x.MemberId, x.MemberName, x.BookTitle, x.DueDate,
                (int)(now - x.DueDate).TotalDays))
            .ToListAsync(ct);
        return new PagedList<OverdueLoanDto>(items, page, pageSize, total);
    }

    public async Task<PagedList<MemberActivityDto>> GetMemberActivityAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var total = await _db.MemberActivityProjections.CountAsync(ct);
        var items = await _db.MemberActivityProjections
            .OrderByDescending(x => x.TotalBorrows)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new MemberActivityDto(x.MemberId, x.FullName, x.TotalBorrows, x.ActiveLoans, x.OverdueLoans))
            .ToListAsync(ct);
        return new PagedList<MemberActivityDto>(items, page, pageSize, total);
    }
}
