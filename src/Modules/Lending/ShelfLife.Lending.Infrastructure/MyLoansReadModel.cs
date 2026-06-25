using Microsoft.EntityFrameworkCore;
using ShelfLife.Lending.Application;
using ShelfLife.Lending.Domain;
using ShelfLife.SharedKernel;

namespace ShelfLife.Lending.Infrastructure;

public sealed class MyLoansReadModel : IMyLoansReadModel
{
    private readonly LendingDbContext _db;

    public MyLoansReadModel(LendingDbContext db) => _db = db;

    public async Task<PagedList<MyLoanDto>> GetMyLoansAsync(Guid memberId, int page, int pageSize, bool activeOnly, CancellationToken ct = default)
    {
        var skip = (page - 1) * pageSize;

        var total = activeOnly
            ? await _db.Loans.CountAsync(l => l.MemberId == memberId && l.Status != LoanStatus.Returned, ct)
            : await _db.Loans.CountAsync(l => l.MemberId == memberId, ct);

        List<MyLoanRow> rows;

        if (activeOnly)
        {
            rows = await _db.Database
                .SqlQuery<MyLoanRow>($"""
                    SELECT
                        l.[Id]          AS LoanId,
                        l.[BookTitleId],
                        bt.[Title]      AS BookTitle,
                        bt.[Author],
                        l.[BorrowedAt],
                        l.[DueDate],
                        l.[Status],
                        l.[ReturnedAt]
                    FROM   [lending].[Loans]          AS l
                    INNER JOIN [catalog].[BookTitles] AS bt ON bt.[Id] = l.[BookTitleId]
                    WHERE  l.[MemberId] = {memberId}
                      AND  l.[Status]   != 'Returned'
                    ORDER BY l.[BorrowedAt] DESC
                    OFFSET {skip} ROWS FETCH NEXT {pageSize} ROWS ONLY
                    """)
                .ToListAsync(ct);
        }
        else
        {
            rows = await _db.Database
                .SqlQuery<MyLoanRow>($"""
                    SELECT
                        l.[Id]          AS LoanId,
                        l.[BookTitleId],
                        bt.[Title]      AS BookTitle,
                        bt.[Author],
                        l.[BorrowedAt],
                        l.[DueDate],
                        l.[Status],
                        l.[ReturnedAt]
                    FROM   [lending].[Loans]          AS l
                    INNER JOIN [catalog].[BookTitles] AS bt ON bt.[Id] = l.[BookTitleId]
                    WHERE  l.[MemberId] = {memberId}
                    ORDER BY l.[BorrowedAt] DESC
                    OFFSET {skip} ROWS FETCH NEXT {pageSize} ROWS ONLY
                    """)
                .ToListAsync(ct);
        }

        var items = rows.Select(r => new MyLoanDto(
            r.LoanId,
            r.BookTitleId,
            r.BookTitle,
            r.Author,
            r.BorrowedAt,
            r.DueDate,
            r.Status,
            r.Status == "Overdue" || (r.Status == "Active" && r.DueDate < DateTimeOffset.UtcNow)))
            .ToList();

        return new PagedList<MyLoanDto>(items, page, pageSize, total);
    }

    private sealed class MyLoanRow
    {
        public Guid LoanId { get; set; }
        public Guid BookTitleId { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public DateTimeOffset BorrowedAt { get; set; }
        public DateTimeOffset DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset? ReturnedAt { get; set; }
    }
}
