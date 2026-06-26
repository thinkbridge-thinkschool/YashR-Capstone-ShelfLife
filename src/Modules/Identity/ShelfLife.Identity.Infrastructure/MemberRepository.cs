using Microsoft.EntityFrameworkCore;
using ShelfLife.Identity.Domain;

namespace ShelfLife.Identity.Infrastructure;

public sealed class MemberRepository : IMemberRepository
{
    private readonly IdentityDbContext _db;

    public MemberRepository(IdentityDbContext db) => _db = db;

    public Task<Member?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Members.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<Member?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Members.FirstOrDefaultAsync(m => m.Email == email, ct);

    public async Task AddAsync(Member member, CancellationToken ct = default) =>
        await _db.Members.AddAsync(member, ct);
}
