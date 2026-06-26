using Microsoft.EntityFrameworkCore;
using ShelfLife.Identity.Infrastructure;
using ShelfLife.Notifications.Application;

namespace ShelfLife.Notifications.Infrastructure;

public sealed class MemberLookup : IMemberLookup
{
    private readonly IdentityDbContext _identityDb;

    public MemberLookup(IdentityDbContext identityDb) => _identityDb = identityDb;

    public async Task<string> GetEmailAsync(Guid memberId, CancellationToken ct = default)
    {
        var email = await _identityDb.Members
            .Where(m => m.Id == memberId)
            .Select(m => m.Email)
            .FirstOrDefaultAsync(ct);
        return email ?? string.Empty;
    }
}
