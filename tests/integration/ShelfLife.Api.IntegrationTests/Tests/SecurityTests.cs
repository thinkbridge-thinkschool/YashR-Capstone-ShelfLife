using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using ShelfLife.Api.IntegrationTests.Fixtures;
using Xunit;

namespace ShelfLife.Api.IntegrationTests.Tests;

/// <summary>
/// Security regression tests.
/// Covers: JWT validation, role-based authorization, expired/tampered tokens,
/// input safety (SQL metacharacters in query params).
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class SecurityTests(ShelfLifeApiFactory factory)
{
    // ── Unauthenticated access ────────────────────────────────────────────────

    [Theory]
    [InlineData("POST",   "/api/v1/catalog/books",         null)]
    [InlineData("POST",   "/api/v1/catalog/books/manual",  null)]
    [InlineData("POST",   "/api/v1/lending/loans",         null)]
    [InlineData("GET",    "/api/v1/lending/loans",         null)]
    [InlineData("GET",    "/api/v1/lending/holds",         null)]
    [InlineData("GET",    "/api/v1/identity/members",      null)]
    public async Task LibrarianEndpoint_WithNoToken_Returns401(string method, string url, object? _)
    {
        var client = factory.CreateClient();
        var response = await SendAsync(client, method, url);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            $"{method} {url} must reject unauthenticated requests");
    }

    // ── Role enforcement: Member cannot access Librarian endpoints ────────────

    [Theory]
    [InlineData("POST",  "/api/v1/catalog/books")]
    [InlineData("POST",  "/api/v1/catalog/books/manual")]
    [InlineData("POST",  "/api/v1/lending/loans")]
    [InlineData("GET",   "/api/v1/lending/loans")]
    [InlineData("GET",   "/api/v1/lending/holds")]
    [InlineData("GET",   "/api/v1/identity/members")]
    public async Task LibrarianEndpoint_WithMemberToken_Returns403(string method, string url)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.MemberToken(Guid.NewGuid()));

        var response = await SendAsync(client, method, url);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            $"Member role must be denied access to {method} {url}");
    }

    // ── JWT token tampering ───────────────────────────────────────────────────

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        var token = GenerateExpiredToken();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/identity/members?page=1&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "expired JWT must be rejected");
    }

    [Fact]
    public async Task MalformedToken_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "this.is.not.a.jwt");

        var response = await client.GetAsync("/api/v1/identity/members?page=1&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "malformed JWT must be rejected");
    }

    [Fact]
    public async Task TokenSignedWithWrongKey_Returns401()
    {
        var token = GenerateTokenWithWrongKey();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/identity/members?page=1&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "JWT signed with a different key must be rejected");
    }

    // ── Input safety: SQL metacharacters must not cause 500 ──────────────────

    [Theory]
    [InlineData("' OR 1=1--")]
    [InlineData("'; DROP TABLE Members;--")]
    [InlineData("\" OR \"\"=\"")]
    [InlineData("%27 OR %271%27=%271")]
    public async Task SearchParam_WithSqlMetachars_DoesNotReturn500(string payload)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(Guid.NewGuid()));

        var encoded  = Uri.EscapeDataString(payload);
        var response = await client.GetAsync(
            $"/api/v1/identity/members?page=1&pageSize=20&search={encoded}");

        ((int)response.StatusCode).Should().NotBe(500,
            $"SQL metacharacter payload '{payload}' must not cause a server error");
    }

    [Theory]
    [InlineData("' OR 1=1--")]
    [InlineData("'; DROP TABLE BookTitles;--")]
    public async Task CatalogSearchParam_WithSqlMetachars_DoesNotReturn500(string payload)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(Guid.NewGuid()));

        var encoded  = Uri.EscapeDataString(payload);
        var response = await client.GetAsync(
            $"/api/v1/catalog/books?page=1&pageSize=20&search={encoded}");

        ((int)response.StatusCode).Should().NotBe(500,
            $"SQL metacharacter '{payload}' in catalog search must be safely parameterized");
    }

    [Theory]
    [InlineData("' OR 1=1--")]
    [InlineData("'; DROP TABLE Loans;--")]
    public async Task LoanSearchParam_WithSqlMetachars_DoesNotReturn500(string payload)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(Guid.NewGuid()));

        var encoded  = Uri.EscapeDataString(payload);
        var response = await client.GetAsync(
            $"/api/v1/lending/loans?page=1&pageSize=20&activeOnly=false&search={encoded}");

        ((int)response.StatusCode).Should().NotBe(500,
            $"SQL metacharacter '{payload}' in loan search must be safely parameterized");
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url)
    {
        return method.ToUpper() switch
        {
            "GET"    => await client.GetAsync(url),
            "POST"   => await client.PostAsJsonAsync(url, new { }),
            "DELETE" => await client.DeleteAsync(url),
            _        => throw new ArgumentOutOfRangeException(nameof(method)),
        };
    }

    private static string GenerateExpiredToken()
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestTokenHelper.Secret));
        var token = new JwtSecurityToken(
            issuer:             TestTokenHelper.Issuer,
            audience:           TestTokenHelper.Audience,
            claims:             [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()), new Claim("role", "Librarian")],
            notBefore:          DateTime.UtcNow.AddHours(-2),
            expires:            DateTime.UtcNow.AddHours(-1),  // expired 1 h ago
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateTokenWithWrongKey()
    {
        var wrongKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("wrong-secret-key-that-is-32chars!!"));
        var token = new JwtSecurityToken(
            issuer:             TestTokenHelper.Issuer,
            audience:           TestTokenHelper.Audience,
            claims:             [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()), new Claim("role", "Librarian")],
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(wrongKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
