using Microsoft.EntityFrameworkCore;
using ShelfLife.Lending.Application;
using ShelfLife.Lending.Domain;
using ShelfLife.SharedKernel;

namespace ShelfLife.Lending.Infrastructure;

public sealed class LoansReadModel : ILoansReadModel
{
    private readonly LendingDbContext _db;

    public LoansReadModel(LendingDbContext db) => _db = db;

    public async Task<PagedList<LoanSummaryDto>> GetLoansAsync(int page, int pageSize, bool activeOnly, string? search, CancellationToken ct = default)
    {
        var skip = (page - 1) * pageSize;
        bool hasSearch = !string.IsNullOrWhiteSpace(search);

        int total;
        List<LoanRow> rows;

        if (!hasSearch)
        {
            total = activeOnly
                ? await _db.Loans.CountAsync(l => l.Status != LoanStatus.Returned, ct)
                : await _db.Loans.CountAsync(ct);

            rows = activeOnly
                ? await _db.Database.SqlQuery<LoanRow>($"""
                    SELECT
                        l.[Id]          AS LoanId,
                        l.[MemberId],
                        m.[FullName]    AS MemberName,
                        m.[Email]       AS MemberEmail,
                        l.[BookTitleId],
                        bt.[Title]      AS BookTitle,
                        bt.[Author],
                        l.[BorrowedAt],
                        l.[DueDate],
                        l.[Status],
                        l.[ReturnedAt]
                    FROM   [lending].[Loans]          AS l
                    INNER JOIN [identity].[Members]   AS m  ON m.[Id]  = l.[MemberId]
                    INNER JOIN [catalog].[BookTitles] AS bt ON bt.[Id] = l.[BookTitleId]
                    WHERE  l.[Status] != 'Returned'
                    ORDER BY l.[BorrowedAt] DESC
                    OFFSET {skip} ROWS FETCH NEXT {pageSize} ROWS ONLY
                    """).ToListAsync(ct)
                : await _db.Database.SqlQuery<LoanRow>($"""
                    SELECT
                        l.[Id]          AS LoanId,
                        l.[MemberId],
                        m.[FullName]    AS MemberName,
                        m.[Email]       AS MemberEmail,
                        l.[BookTitleId],
                        bt.[Title]      AS BookTitle,
                        bt.[Author],
                        l.[BorrowedAt],
                        l.[DueDate],
                        l.[Status],
                        l.[ReturnedAt]
                    FROM   [lending].[Loans]          AS l
                    INNER JOIN [identity].[Members]   AS m  ON m.[Id]  = l.[MemberId]
                    INNER JOIN [catalog].[BookTitles] AS bt ON bt.[Id] = l.[BookTitleId]
                    ORDER BY l.[BorrowedAt] DESC
                    OFFSET {skip} ROWS FETCH NEXT {pageSize} ROWS ONLY
                    """).ToListAsync(ct);
        }
        else
        {
            string p = $"%{search}%";
            if (activeOnly)
            {
                total = (await _db.Database.SqlQuery<CountRow>($"""
                    SELECT COUNT(*) AS Value
                    FROM [lending].[Loans] AS l
                    INNER JOIN [identity].[Members]   AS m  ON m.[Id]  = l.[MemberId]
                    INNER JOIN [catalog].[BookTitles] AS bt ON bt.[Id] = l.[BookTitleId]
                    WHERE l.[Status] != 'Returned'
                      AND (m.[FullName] LIKE {p} OR m.[Email] LIKE {p} OR bt.[Title] LIKE {p} OR bt.[Author] LIKE {p})
                    """).SingleAsync(ct)).Value;

                rows = await _db.Database.SqlQuery<LoanRow>($"""
                    SELECT
                        l.[Id]          AS LoanId,
                        l.[MemberId],
                        m.[FullName]    AS MemberName,
                        m.[Email]       AS MemberEmail,
                        l.[BookTitleId],
                        bt.[Title]      AS BookTitle,
                        bt.[Author],
                        l.[BorrowedAt],
                        l.[DueDate],
                        l.[Status],
                        l.[ReturnedAt]
                    FROM   [lending].[Loans]          AS l
                    INNER JOIN [identity].[Members]   AS m  ON m.[Id]  = l.[MemberId]
                    INNER JOIN [catalog].[BookTitles] AS bt ON bt.[Id] = l.[BookTitleId]
                    WHERE l.[Status] != 'Returned'
                      AND (m.[FullName] LIKE {p} OR m.[Email] LIKE {p} OR bt.[Title] LIKE {p} OR bt.[Author] LIKE {p})
                    ORDER BY l.[BorrowedAt] DESC
                    OFFSET {skip} ROWS FETCH NEXT {pageSize} ROWS ONLY
                    """).ToListAsync(ct);
            }
            else
            {
                total = (await _db.Database.SqlQuery<CountRow>($"""
                    SELECT COUNT(*) AS Value
                    FROM [lending].[Loans] AS l
                    INNER JOIN [identity].[Members]   AS m  ON m.[Id]  = l.[MemberId]
                    INNER JOIN [catalog].[BookTitles] AS bt ON bt.[Id] = l.[BookTitleId]
                    WHERE (m.[FullName] LIKE {p} OR m.[Email] LIKE {p} OR bt.[Title] LIKE {p} OR bt.[Author] LIKE {p})
                    """).SingleAsync(ct)).Value;

                rows = await _db.Database.SqlQuery<LoanRow>($"""
                    SELECT
                        l.[Id]          AS LoanId,
                        l.[MemberId],
                        m.[FullName]    AS MemberName,
                        m.[Email]       AS MemberEmail,
                        l.[BookTitleId],
                        bt.[Title]      AS BookTitle,
                        bt.[Author],
                        l.[BorrowedAt],
                        l.[DueDate],
                        l.[Status],
                        l.[ReturnedAt]
                    FROM   [lending].[Loans]          AS l
                    INNER JOIN [identity].[Members]   AS m  ON m.[Id]  = l.[MemberId]
                    INNER JOIN [catalog].[BookTitles] AS bt ON bt.[Id] = l.[BookTitleId]
                    WHERE (m.[FullName] LIKE {p} OR m.[Email] LIKE {p} OR bt.[Title] LIKE {p} OR bt.[Author] LIKE {p})
                    ORDER BY l.[BorrowedAt] DESC
                    OFFSET {skip} ROWS FETCH NEXT {pageSize} ROWS ONLY
                    """).ToListAsync(ct);
            }
        }

        var items = rows.Select(r => new LoanSummaryDto(
            r.LoanId,
            r.MemberId,
            r.MemberName,
            r.MemberEmail,
            r.BookTitleId,
            r.BookTitle,
            r.Author,
            r.BorrowedAt,
            r.DueDate,
            r.Status,
            r.Status == "Overdue" || (r.Status == "Active" && r.DueDate < DateTimeOffset.UtcNow)))
            .ToList();

        return new PagedList<LoanSummaryDto>(items, page, pageSize, total);
    }

    private sealed class CountRow { public int Value { get; set; } }

    private sealed class LoanRow
    {
        public Guid LoanId { get; set; }
        public Guid MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public string MemberEmail { get; set; } = string.Empty;
        public Guid BookTitleId { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public DateTimeOffset BorrowedAt { get; set; }
        public DateTimeOffset DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset? ReturnedAt { get; set; }
    }
}
