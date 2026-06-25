using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using ShelfLife.Api.IntegrationTests.Fixtures;

namespace ShelfLife.Api.IntegrationTests.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class MembersTests(ShelfLifeApiFactory factory)
{
    // ── Authorization ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMembers_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/identity/members?page=1&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMembers_AsMember_Returns403()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.MemberToken(Guid.NewGuid()));

        var response = await client.GetAsync("/api/v1/identity/members?page=1&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMembers_AsLibrarian_Returns200WithPagedList()
    {
        var client = factory.CreateClient();

        // Register a member to ensure at least one result
        var email = $"list-member-{Guid.NewGuid()}@test.com";
        var fullName = "List Test Member";
        await client.PostAsJsonAsync("/api/v1/identity/register",
            new { Email = email, FullName = fullName, Password = "Pass123!" });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(Guid.NewGuid()));

        var response = await client.GetAsync("/api/v1/identity/members?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedListResponse<MemberItem>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().BeGreaterThan(0);
        body.Items.Should().Contain(m => m.Email == email && m.FullName == fullName);
    }

    private sealed record MemberItem(Guid MemberId, string FullName, string Email, string Role, bool IsBlocked, DateTimeOffset RegisteredAt);
    private sealed record PagedListResponse<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
}
