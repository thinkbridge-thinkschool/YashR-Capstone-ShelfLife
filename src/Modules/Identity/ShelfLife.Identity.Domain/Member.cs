using ShelfLife.SharedKernel;

namespace ShelfLife.Identity.Domain;

public enum MemberRole { Member, Librarian, Admin }

public sealed class Member : AggregateRoot<Guid>
{
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public MemberRole Role { get; private set; } = MemberRole.Member;
    public DateTimeOffset RegisteredAt { get; private set; }
    public bool IsBlocked { get; private set; }

    private Member() { }

    public static Member Register(Guid id, string email, string fullName, string passwordHash)
    {
        var member = new Member
        {
            Id = id,
            Email = email,
            FullName = fullName,
            PasswordHash = passwordHash,
            RegisteredAt = DateTimeOffset.UtcNow
        };
        member.Raise(new MemberRegisteredDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, id, email, fullName));
        return member;
    }

    public void AssignRole(MemberRole role) => Role = role;

    public void Block() => IsBlocked = true;

    public void Unblock() => IsBlocked = false;
}

public sealed record MemberRegisteredDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid MemberId,
    string Email,
    string FullName) : IDomainEvent;
