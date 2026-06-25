using Microsoft.EntityFrameworkCore;
using ShelfLife.Lending.Application;
using ShelfLife.SharedKernel;

namespace ShelfLife.Lending.Infrastructure;

public sealed class HoldsReadModel : IHoldsReadModel
{
    private readonly LendingDbContext _db;

    public HoldsReadModel(LendingDbContext db) => _db = db;

    public async Task<PagedList<HoldDto>> GetHoldsAsync(int page, int pageSize, string? search, CancellationToken ct = default)
    {
        bool hasSearch = !string.IsNullOrWhiteSpace(search);
        var skip = (page - 1) * pageSize;

        int total;
        List<HoldRow> rows;

        if (!hasSearch)
        {
            total = await _db.Loans.SelectMany(l => l.Holds).CountAsync(ct);

            rows = await _db.Database
                .SqlQuery<HoldRow>($"""
                    SELECT
                        h.[Id]          AS HoldId,
                        h.[MemberId],
                        m.[FullName]    AS MemberName,
                        h.[BookTitleId],
                        bt.[Title]      AS BookTitle,
                        h.[Status],
                        h.[PlacedAt],
                        h.[ReadyAt],
                        h.[ExpiresAt]
                    FROM   [lending].[Holds]          AS h
                    INNER JOIN [identity].[Members]   AS m  ON m.[Id]  = h.[MemberId]
                    INNER JOIN [catalog].[BookTitles] AS bt ON bt.[Id] = h.[BookTitleId]
                    ORDER BY h.[PlacedAt] DESC
                    OFFSET {skip} ROWS FETCH NEXT {pageSize} ROWS ONLY
                    """)
                .ToListAsync(ct);
        }
        else
        {
            string p = $"%{search}%";

            total = (await _db.Database.SqlQuery<CountRow>($"""
                SELECT COUNT(*) AS Value
                FROM [lending].[Holds] AS h
                INNER JOIN [identity].[Members]   AS m  ON m.[Id]  = h.[MemberId]
                INNER JOIN [catalog].[BookTitles] AS bt ON bt.[Id] = h.[BookTitleId]
                WHERE (m.[FullName] LIKE {p} OR bt.[Title] LIKE {p})
                """).SingleAsync(ct)).Value;

            rows = await _db.Database
                .SqlQuery<HoldRow>($"""
                    SELECT
                        h.[Id]          AS HoldId,
                        h.[MemberId],
                        m.[FullName]    AS MemberName,
                        h.[BookTitleId],
                        bt.[Title]      AS BookTitle,
                        h.[Status],
                        h.[PlacedAt],
                        h.[ReadyAt],
                        h.[ExpiresAt]
                    FROM   [lending].[Holds]          AS h
                    INNER JOIN [identity].[Members]   AS m  ON m.[Id]  = h.[MemberId]
                    INNER JOIN [catalog].[BookTitles] AS bt ON bt.[Id] = h.[BookTitleId]
                    WHERE (m.[FullName] LIKE {p} OR bt.[Title] LIKE {p})
                    ORDER BY h.[PlacedAt] DESC
                    OFFSET {skip} ROWS FETCH NEXT {pageSize} ROWS ONLY
                    """)
                .ToListAsync(ct);
        }

        var items = rows
            .Select(r => new HoldDto(
                r.HoldId,
                r.MemberId,
                r.MemberName,
                r.BookTitleId,
                r.BookTitle,
                r.Status,
                r.PlacedAt,
                r.ReadyAt,
                r.ExpiresAt))
            .ToList();

        return new PagedList<HoldDto>(items, page, pageSize, total);
    }

    private sealed class CountRow { public int Value { get; set; } }

    private sealed class HoldRow
    {
        public Guid HoldId { get; set; }
        public Guid MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public Guid BookTitleId { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset PlacedAt { get; set; }
        public DateTimeOffset? ReadyAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
