using ShelfLife.Identity.Domain;
using ShelfLife.SharedKernel;

namespace ShelfLife.Identity.Application;

// ── Register ──────────────────────────────────────────────────────────────────

public sealed record RegisterMemberCommand(string Email, string FullName, string Password);

public sealed class RegisterMemberHandler
{
    private readonly IMemberRepository _members;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;

    public RegisterMemberHandler(IMemberRepository members, IPasswordHasher hasher, IUnitOfWork uow)
    {
        _members = members;
        _hasher = hasher;
        _uow = uow;
    }

    public async Task<Result<Guid>> HandleAsync(RegisterMemberCommand cmd, CancellationToken ct = default)
    {
        var existing = await _members.FindByEmailAsync(cmd.Email, ct);
        if (existing is not null)
            return Result.Failure<Guid>("Email already registered.");

        var hash = _hasher.Hash(cmd.Password);
        var member = Member.Register(Guid.NewGuid(), cmd.Email, cmd.FullName, hash);
        await _members.AddAsync(member, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(member.Id);
    }
}

// ── Login ─────────────────────────────────────────────────────────────────────

public sealed record LoginCommand(string Email, string Password);
public sealed record LoginResult(string Token, Guid MemberId);

public sealed class LoginHandler
{
    private readonly IMemberRepository _members;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtService _jwt;

    public LoginHandler(IMemberRepository members, IPasswordHasher hasher, IJwtService jwt)
    {
        _members = members;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<Result<LoginResult>> HandleAsync(LoginCommand cmd, CancellationToken ct = default)
    {
        var member = await _members.FindByEmailAsync(cmd.Email, ct);
        if (member is null || !_hasher.Verify(cmd.Password, member.PasswordHash))
            return Result.Failure<LoginResult>("Invalid credentials.");

        if (member.IsBlocked)
            return Result.Failure<LoginResult>("Account is blocked.");

        var token = _jwt.GenerateToken(member);
        return Result.Success(new LoginResult(token, member.Id));
    }
}
