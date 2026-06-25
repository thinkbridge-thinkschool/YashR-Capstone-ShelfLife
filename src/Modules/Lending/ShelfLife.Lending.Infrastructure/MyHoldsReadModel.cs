using Microsoft.EntityFrameworkCore;
using ShelfLife.Lending.Application;
using ShelfLife.SharedKernel;

namespace ShelfLife.Lending.Infrastructure;

public sealed class MyHoldsReadModel : IMyHoldsReadModel
{
    private readonly LendingDbContext _db;

    public MyHoldsReadModel(LendingDbContext db) => _db = db;

    public async Task<PagedList<MyHoldDto>> GetMyHoldsAsync(Guid memberId, int page, int pageSize, CancellationToken ct = default)
    {
        var total = (await _db.Database
            .SqlQuery<int>($"SELECT COUNT(1) AS [Value] FROM [lending].[Holds] WHERE [MemberId] = {memberId}")
            .ToListAsync(ct))
            .First();

        var skip = (page - 1) * pageSize;

        var rows = await _db.Database
            .SqlQuery<MyHoldRow>($"""
                SELECT
                    h.[Id]          AS HoldId,
                    h.[BookTitleId],
                    bt.[Title]      AS BookTitle,
                    bt.[Author],
                    h.[Status],
                    h.[PlacedAt],
                    h.[ReadyAt],
                    h.[ExpiresAt]
                FROM   [lending].[Holds]          AS h
                INNER JOIN [catalog].[BookTitles] AS bt ON bt.[Id] = h.[BookTitleId]
                WHERE  h.[MemberId] = {memberId}
                ORDER BY h.[PlacedAt] DESC
                OFFSET {skip} ROWS FETCH NEXT {pageSize} ROWS ONLY
                """)
            .ToListAsync(ct);

        var items = rows.Select(r => new MyHoldDto(
            r.HoldId,
            r.BookTitleId,
            r.BookTitle,
            r.Author,
            r.Status,
            r.PlacedAt,
            r.ReadyAt,
            r.ExpiresAt))
            .ToList();

        return new PagedList<MyHoldDto>(items, page, pageSize, total);
    }

    private sealed class MyHoldRow
    {
        public Guid HoldId { get; set; }
        public Guid BookTitleId { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset PlacedAt { get; set; }
        public DateTimeOffset? ReadyAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
