using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShelfLife.Api.IntegrationTests.Fixtures;
using ShelfLife.Catalog.Domain;
using ShelfLife.Catalog.Infrastructure;
using ShelfLife.Lending.Domain;
using ShelfLife.Lending.Infrastructure;

namespace ShelfLife.Api.IntegrationTests.Tests;

/// <summary>
/// Integration tests for the borrow / return lifecycle.
/// Each test provisions its own member + book so tests are fully independent.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class LendingTests(ShelfLifeApiFactory factory)
{
    // Independent ISBN pool so lending tests never collide with catalog tests
    private static readonly string[] IsbnPool =
    [
        "9780201896831", "9780743269513", "9780061965784", "9780316769174",
        "9780385333849", "9780525559474", "9780525478812", "9781501156700",
    ];

    private static int _isbnIndex;

    private static string UniqueIsbn()
    {
        var idx = Interlocked.Increment(ref _isbnIndex);
        return IsbnPool[(idx - 1) % IsbnPool.Length];
    }

    // ── Test helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a fresh member via the API and returns their ID.
    /// </summary>
    private async Task<Guid> RegisterMemberAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/identity/register", new
        {
            Email    = $"lender-{Guid.NewGuid()}@test.com",
            FullName = "Integration Lender",
            Password = "Pass123!",
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<IdBody>();
        return body!.Id;
    }

    /// <summary>
    /// Adds a book title plus one physical copy and returns the BookTitleId.
    /// Uses a librarian token generated directly (no DB round-trip needed for role).
    /// </summary>
    private async Task<Guid> AddBookWithCopyAsync()
    {
        var libClient = factory.CreateClient();
        libClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(Guid.NewGuid()));

        var addBookResp = await libClient.PostAsJsonAsync("/api/v1/catalog/books",
            new { Isbn = UniqueIsbn() });
        addBookResp.EnsureSuccessStatusCode();
        var bookBody = await addBookResp.Content.ReadFromJsonAsync<IdBody>();
        var bookTitleId = bookBody!.Id;

        var addCopyResp = await libClient.PostAsJsonAsync(
            $"/api/v1/catalog/books/{bookTitleId}/copies",
            new { Barcode = $"BC-{Guid.NewGuid():N}" });
        addCopyResp.EnsureSuccessStatusCode();

        return bookTitleId;
    }

    /// <summary>
    /// Creates a librarian HTTP client (no DB round-trip needed — token is signed directly).
    /// </summary>
    private HttpClient LibrarianClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(Guid.NewGuid()));
        return client;
    }

    // ── Borrow Book ───────────────────────────────────────────────────────────

    [Fact]
    public async Task BorrowBook_AvailableCopy_Returns201AndLoanAndCopyStatusUpdated()
    {
        // Arrange — librarian issues the loan on behalf of a registered member
        var bookTitleId = await AddBookWithCopyAsync();
        var memberId = await RegisterMemberAsync(factory.CreateClient());
        var libClient = LibrarianClient();

        // Act
        var response = await libClient.PostAsJsonAsync("/api/v1/lending/loans",
            new { MemberId = memberId, BookTitleId = bookTitleId });

        // Assert — HTTP 201 with loan ID + due date
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<BorrowBody>();
        body!.LoanId.Should().NotBeEmpty();
        body.DueDate.Should().BeAfter(DateTimeOffset.UtcNow);

        // Assert — loan row in DB
        await using var scope = factory.Services.CreateAsyncScope();
        var lendingDb = scope.ServiceProvider.GetRequiredService<LendingDbContext>();
        var loan = await lendingDb.Loans.FirstOrDefaultAsync(l => l.Id == body.LoanId);
        loan.Should().NotBeNull("a Loan row must be persisted");
        loan!.MemberId.Should().Be(memberId);
        loan.BookTitleId.Should().Be(bookTitleId);
        loan.Status.Should().Be(LoanStatus.Active);

        // Assert — copy status flipped to OnLoan
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var title = await catalogDb.BookTitles
            .Include(b => b.Copies)
            .FirstOrDefaultAsync(b => b.Id == bookTitleId);
        title.Should().NotBeNull();
        title!.Copies.Should().ContainSingle(c => c.Status == CopyStatus.OnLoan);
    }

    [Fact]
    public async Task BorrowBook_NoCopyAvailable_Returns400()
    {
        // Arrange — librarian loans the one copy to member1
        var bookTitleId = await AddBookWithCopyAsync();
        var member1Id = await RegisterMemberAsync(factory.CreateClient());
        var libClient = LibrarianClient();
        (await libClient.PostAsJsonAsync("/api/v1/lending/loans",
            new { MemberId = member1Id, BookTitleId = bookTitleId }))
            .EnsureSuccessStatusCode();

        // Act — librarian tries to loan the same (now unavailable) book to member2
        var member2Id = await RegisterMemberAsync(factory.CreateClient());
        var response = await libClient.PostAsJsonAsync("/api/v1/lending/loans",
            new { MemberId = member2Id, BookTitleId = bookTitleId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Return Book ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ReturnBook_ActiveLoan_Returns200AndLoanAndCopyStatusUpdated()
    {
        // Arrange — librarian issues a loan
        var bookTitleId = await AddBookWithCopyAsync();
        var memberId = await RegisterMemberAsync(factory.CreateClient());
        var libClient = LibrarianClient();

        var borrowResp = await libClient.PostAsJsonAsync("/api/v1/lending/loans",
            new { MemberId = memberId, BookTitleId = bookTitleId });
        borrowResp.EnsureSuccessStatusCode();
        var borrowed = await borrowResp.Content.ReadFromJsonAsync<BorrowBody>();

        // Act — librarian processes the return
        var returnResp = await libClient.PostAsJsonAsync(
            $"/api/v1/lending/loans/{borrowed!.LoanId}/return", new { });

        // Assert — HTTP 200
        returnResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — loan status = Returned in DB
        await using var scope = factory.Services.CreateAsyncScope();
        var lendingDb = scope.ServiceProvider.GetRequiredService<LendingDbContext>();
        var loan = await lendingDb.Loans.FirstOrDefaultAsync(l => l.Id == borrowed.LoanId);
        loan.Should().NotBeNull();
        loan!.Status.Should().Be(LoanStatus.Returned);

        // Assert — copy is Available again
        var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var title = await catalogDb.BookTitles
            .Include(b => b.Copies)
            .FirstOrDefaultAsync(b => b.Id == bookTitleId);
        title!.Copies.Should().OnlyContain(c => c.Status == CopyStatus.Available);
    }

    [Fact]
    public async Task ReturnBook_AlreadyReturned_Returns400()
    {
        // Arrange — librarian borrows then returns
        var bookTitleId = await AddBookWithCopyAsync();
        var memberId = await RegisterMemberAsync(factory.CreateClient());
        var libClient = LibrarianClient();

        var borrowResp = await libClient.PostAsJsonAsync("/api/v1/lending/loans",
            new { MemberId = memberId, BookTitleId = bookTitleId });
        borrowResp.EnsureSuccessStatusCode();
        var borrowed = await borrowResp.Content.ReadFromJsonAsync<BorrowBody>();

        (await libClient.PostAsJsonAsync($"/api/v1/lending/loans/{borrowed!.LoanId}/return", new { }))
            .EnsureSuccessStatusCode();

        // Act — attempt a second return on the same loan
        var response = await libClient.PostAsJsonAsync(
            $"/api/v1/lending/loans/{borrowed.LoanId}/return", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record IdBody(Guid Id);
    private sealed record BorrowBody(Guid LoanId, DateTimeOffset DueDate);
}
