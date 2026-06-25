using Microsoft.EntityFrameworkCore;
using ShelfLife.Identity.Application;
using ShelfLife.SharedKernel;

namespace ShelfLife.Identity.Infrastructure;

public sealed class MembersReadModel : IMembersReadModel
{
    private readonly IdentityDbContext _db;

    public MembersReadModel(IdentityDbContext db) => _db = db;

    public async Task<PagedList<MemberSummaryDto>> GetMembersAsync(int page, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = _db.Members.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => EF.Functions.Like(m.FullName, $"%{search}%") || EF.Functions.Like(m.Email, $"%{search}%"));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(m => m.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MemberSummaryDto(
                m.Id,
                m.FullName,
                m.Email,
                m.Role.ToString(),
                m.IsBlocked,
                m.RegisteredAt))
            .ToListAsync(ct);

        return new PagedList<MemberSummaryDto>(items, page, pageSize, total);
    }
}
