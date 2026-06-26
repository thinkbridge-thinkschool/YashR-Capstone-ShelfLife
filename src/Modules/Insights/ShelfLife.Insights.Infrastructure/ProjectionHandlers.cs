using Microsoft.EntityFrameworkCore;
using ShelfLife.Catalog.Infrastructure;
using ShelfLife.Identity.Infrastructure;
using ShelfLife.Lending.Contracts;

namespace ShelfLife.Insights.Infrastructure;

public sealed class BookBorrowedProjectionHandler
{
    private readonly InsightsDbContext _db;
    private readonly CatalogDbContext _catalog;
    private readonly IdentityDbContext _identity;

    public BookBorrowedProjectionHandler(InsightsDbContext db, CatalogDbContext catalog, IdentityDbContext identity)
    {
        _db = db;
        _catalog = catalog;
        _identity = identity;
    }

    public async Task HandleAsync(Guid messageId, BookBorrowedEvent evt, CancellationToken ct)
    {
        if (await _db.ProcessedProjectionEvents.AnyAsync(p => p.MessageId == messageId, ct))
            return;

        var popularTitle = await _db.PopularTitleProjections.FindAsync([evt.BookTitleId], ct);
        if (popularTitle is null)
        {
            var book = await _catalog.BookTitles.FindAsync([evt.BookTitleId], ct);
            _db.PopularTitleProjections.Add(new PopularTitleProjection
            {
                BookTitleId = evt.BookTitleId,
                Title = book?.Title ?? "Unknown",
                Author = book?.Author ?? "Unknown",
                BorrowCount = 1
            });
        }
        else
        {
            popularTitle.BorrowCount++;
        }

        var activity = await _db.MemberActivityProjections.FindAsync([evt.MemberId], ct);
        if (activity is null)
        {
            var member = await _identity.Members.FindAsync([evt.MemberId], ct);
            _db.MemberActivityProjections.Add(new MemberActivityProjection
            {
                MemberId = evt.MemberId,
                FullName = member?.FullName ?? "Unknown",
                TotalBorrows = 1,
                ActiveLoans = 1,
                OverdueLoans = 0
            });
        }
        else
        {
            activity.TotalBorrows++;
            activity.ActiveLoans++;
        }

        _db.ProcessedProjectionEvents.Add(new ProcessedProjectionEvent { MessageId = messageId });
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class BookReturnedProjectionHandler
{
    private readonly InsightsDbContext _db;

    public BookReturnedProjectionHandler(InsightsDbContext db) => _db = db;

    public async Task HandleAsync(Guid messageId, BookReturnedEvent evt, CancellationToken ct)
    {
        if (await _db.ProcessedProjectionEvents.AnyAsync(p => p.MessageId == messageId, ct))
            return;

        var activity = await _db.MemberActivityProjections.FindAsync([evt.MemberId], ct);
        if (activity is not null)
        {
            activity.ActiveLoans = Math.Max(0, activity.ActiveLoans - 1);
            activity.OverdueLoans = Math.Max(0, activity.OverdueLoans - 1);
        }

        var overdue = await _db.OverdueLoanProjections.FindAsync([evt.LoanId], ct);
        if (overdue is not null)
            _db.OverdueLoanProjections.Remove(overdue);

        _db.ProcessedProjectionEvents.Add(new ProcessedProjectionEvent { MessageId = messageId });
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class LoanOverdueProjectionHandler
{
    private readonly InsightsDbContext _db;
    private readonly CatalogDbContext _catalog;
    private readonly IdentityDbContext _identity;

    public LoanOverdueProjectionHandler(InsightsDbContext db, CatalogDbContext catalog, IdentityDbContext identity)
    {
        _db = db;
        _catalog = catalog;
        _identity = identity;
    }

    public async Task HandleAsync(Guid messageId, LoanOverdueEvent evt, CancellationToken ct)
    {
        if (await _db.ProcessedProjectionEvents.AnyAsync(p => p.MessageId == messageId, ct))
            return;

        var existing = await _db.OverdueLoanProjections.FindAsync([evt.LoanId], ct);
        if (existing is null)
        {
            var book = await _catalog.BookTitles.FindAsync([evt.BookTitleId], ct);
            var member = await _identity.Members.FindAsync([evt.MemberId], ct);

            _db.OverdueLoanProjections.Add(new OverdueLoanProjection
            {
                LoanId = evt.LoanId,
                MemberId = evt.MemberId,
                MemberName = member?.FullName ?? "Unknown",
                BookTitle = book?.Title ?? "Unknown",
                DueDate = evt.DueDate
            });
        }

        var activity = await _db.MemberActivityProjections.FindAsync([evt.MemberId], ct);
        if (activity is not null)
            activity.OverdueLoans++;

        _db.ProcessedProjectionEvents.Add(new ProcessedProjectionEvent { MessageId = messageId });
        await _db.SaveChangesAsync(ct);
    }
}
