using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using ShelfLife.Api.IntegrationTests.Fixtures;

namespace ShelfLife.Api.IntegrationTests.Tests;

[Collection("Integration")]
public sealed class HoldsTests(ShelfLifeApiFactory factory)
{
    // ── Authorization ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHolds_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/lending/holds?page=1&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetHolds_AsMember_Returns403()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.MemberToken(Guid.NewGuid()));

        var response = await client.GetAsync("/api/v1/lending/holds?page=1&pageSize=20");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHolds_AsLibrarian_Returns200WithPagedList()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(Guid.NewGuid()));

        var response = await client.GetAsync("/api/v1/lending/holds?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedListResponse<HoldItem>>();
        body.Should().NotBeNull();
        body!.Items.Should().NotBeNull();
        body.Page.Should().Be(1);
        body.PageSize.Should().Be(20);
    }

    private sealed record HoldItem(Guid HoldId, Guid MemberId, string MemberName, Guid BookTitleId, string BookTitle, string Status, DateTimeOffset PlacedAt);
    private sealed record PagedListResponse<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
}
