using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShelfLife.Api.IntegrationTests.Fixtures;
using ShelfLife.Identity.Infrastructure;

namespace ShelfLife.Api.IntegrationTests.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class IdentityTests(ShelfLifeApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidPayload_Returns201AndPersistsMember()
    {
        var email = $"member-{Guid.NewGuid()}@test.com";
        var payload = new { Email = email, FullName = "Test Member", Password = "Pass123!" };

        var response = await _client.PostAsJsonAsync("/api/v1/identity/register", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<IdResponse>();
        body!.Id.Should().NotBeEmpty();

        // Verify the member actually landed in the database
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var member = await db.Members.FirstOrDefaultAsync(m => m.Email == email);
        member.Should().NotBeNull();
        member!.FullName.Should().Be("Test Member");
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var email = $"dupe-{Guid.NewGuid()}@test.com";
        var payload = new { Email = email, FullName = "Original", Password = "Pass123!" };

        await _client.PostAsJsonAsync("/api/v1/identity/register", payload);
        var response = await _client.PostAsJsonAsync("/api/v1/identity/register", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_MissingEmail_Returns400()
    {
        var payload = new { FullName = "No Email", Password = "Pass123!" };

        var response = await _client.PostAsJsonAsync("/api/v1/identity/register", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        var email    = $"login-{Guid.NewGuid()}@test.com";
        var password = "Pass123!";
        await _client.PostAsJsonAsync("/api/v1/identity/register",
            new { Email = email, FullName = "Login Member", Password = password });

        var response = await _client.PostAsJsonAsync("/api/v1/identity/login",
            new { Email = email, Password = password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
        body.MemberId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var email = $"wrongpw-{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/v1/identity/register",
            new { Email = email, FullName = "Wrong PW", Password = "CorrectPass1!" });

        var response = await _client.PostAsJsonAsync("/api/v1/identity/login",
            new { Email = email, Password = "WrongPass!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record IdResponse(Guid Id);
    private sealed record LoginResponse(string Token, Guid MemberId);
}
