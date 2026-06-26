using ShelfLife.Identity.Domain;

namespace ShelfLife.Identity.Application;

public interface IJwtService
{
    string GenerateToken(Member member);
}
