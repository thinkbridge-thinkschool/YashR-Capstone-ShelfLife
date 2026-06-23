using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ShelfLife.Api.IntegrationTests.Fixtures;

/// <summary>
/// Generates HS256 Bearer tokens that match the test app's Jwt:* config.
/// Mirrors the claim layout used by JwtService so endpoints read them correctly.
/// </summary>
public static class TestTokenHelper
{
    public const string Issuer   = "shelflife-test-issuer";
    public const string Audience = "shelflife-test-audience";
    // Must be ≥ 32 chars for HS256
    public const string Secret   = "shelflife-test-secret-key-32-chars!";

    public static string GenerateToken(Guid memberId, string role = "Member")
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Use the same claim names as JwtService: sub + ClaimTypes.Role
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, memberId.ToString()),
            new Claim(ClaimTypes.Role, role),
        };

        var token = new JwtSecurityToken(
            issuer:            Issuer,
            audience:          Audience,
            claims:            claims,
            expires:           DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string MemberToken(Guid memberId)   => GenerateToken(memberId, "Member");
    public static string LibrarianToken(Guid memberId) => GenerateToken(memberId, "Librarian");
}
