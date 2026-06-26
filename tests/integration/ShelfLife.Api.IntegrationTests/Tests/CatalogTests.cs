using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShelfLife.Api.IntegrationTests.Fixtures;
using ShelfLife.Catalog.Infrastructure;

namespace ShelfLife.Api.IntegrationTests.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class CatalogTests(ShelfLifeApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // Pool of known-valid ISBN-13s for isolated test use.
    // Each call to UniqueIsbn() takes the next entry so tests never share an ISBN.
    private static readonly string[] IsbnPool =
    [
        "9780306406157", "9780132350884", "9780596517748", "9781491950357",
        "9780134494166", "9780135957059", "9781617294433", "9781680507225",
        "9780321125217", "9780137081073",
    ];

    private static int _isbnIndex;

    private static string UniqueIsbn()
    {
        var idx = Interlocked.Increment(ref _isbnIndex);
        return IsbnPool[(idx - 1) % IsbnPool.Length];
    }

    // ── Add Book ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddBook_ValidIsbn_LibrarianRole_Returns201AndPersistsTitle()
    {
        var librarianId = Guid.NewGuid();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(librarianId));

        var isbn = UniqueIsbn();
        var response = await _client.PostAsJsonAsync("/api/v1/catalog/books", new { Isbn = isbn });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IdResponse>();
        body!.Id.Should().NotBeEmpty();

        // Verify row persisted in the catalog schema
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var title = await db.BookTitles.FirstOrDefaultAsync(b => b.Id == body.Id);
        title.Should().NotBeNull();
        title!.Title.Should().Contain(isbn);
    }

    [Fact]
    public async Task AddBook_WithoutToken_Returns401()
    {
        var client = factory.CreateClient(); // fresh client with no default headers
        var response = await client.PostAsJsonAsync("/api/v1/catalog/books", new { Isbn = UniqueIsbn() });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddBook_MemberRole_Returns403()
    {
        var memberId = Guid.NewGuid();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.MemberToken(memberId));

        var response = await client.PostAsJsonAsync("/api/v1/catalog/books", new { Isbn = UniqueIsbn() });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddBook_DuplicateIsbn_Returns400()
    {
        var librarianId = Guid.NewGuid();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(librarianId));

        var isbn = UniqueIsbn();
        await client.PostAsJsonAsync("/api/v1/catalog/books", new { Isbn = isbn });
        var response = await client.PostAsJsonAsync("/api/v1/catalog/books", new { Isbn = isbn });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Add Book Manually ─────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddBookManually_ValidInput_LibrarianRole_Returns201AndPersistsTitle()
    {
        var librarianId = Guid.NewGuid();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(librarianId));

        var response = await client.PostAsJsonAsync("/api/v1/catalog/books/manual", new
        {
            Title = "The Pragmatic Programmer",
            Author = "David Thomas",
            PublicationYear = 1999,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IdResponse>();
        body!.Id.Should().NotBeEmpty();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var title = await db.BookTitles.FirstOrDefaultAsync(b => b.Id == body.Id);
        title.Should().NotBeNull();
        title!.Title.Should().Be("The Pragmatic Programmer");
        title.Author.Should().Be("David Thomas");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddBookManually_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/catalog/books/manual",
                new { Title = "X", Author = "Y", PublicationYear = 2000 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddBookManually_MemberRole_Returns403()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.MemberToken(Guid.NewGuid()));

        var response = await client.PostAsJsonAsync("/api/v1/catalog/books/manual",
            new { Title = "X", Author = "Y", PublicationYear = 2000 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddBookManually_EmptyTitle_Returns400()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(Guid.NewGuid()));

        var response = await client.PostAsJsonAsync("/api/v1/catalog/books/manual",
            new { Title = "", Author = "Author", PublicationYear = 2000 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record IdResponse(Guid Id);
}
