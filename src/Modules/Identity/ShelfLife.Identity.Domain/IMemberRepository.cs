namespace ShelfLife.Identity.Domain;

public interface IMemberRepository
{
    Task<Member?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Member?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(Member member, CancellationToken cancellationToken = default);
}
