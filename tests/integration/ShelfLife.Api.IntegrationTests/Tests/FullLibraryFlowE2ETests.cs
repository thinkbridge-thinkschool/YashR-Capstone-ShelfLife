using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShelfLife.Api.IntegrationTests.Fixtures;
using ShelfLife.Lending.Domain;
using ShelfLife.Lending.Infrastructure;

namespace ShelfLife.Api.IntegrationTests.Tests;

/// <summary>
/// Single end-to-end test that walks the complete library workflow:
/// Register → Add book manually → Add copy → Member search → Borrow → View loans → Return → Verify returned.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class FullLibraryFlowE2ETests(ShelfLifeApiFactory factory)
{
    [Fact]
    public async Task FullFlow_ManualBook_RegisterBorrowReturn_StateConsistentAtEveryStep()
    {
        // ── 1. Register a member ─────────────────────────────────────────────
        var publicClient = factory.CreateClient();
        var registerResp = await publicClient.PostAsJsonAsync("/api/v1/identity/register", new
        {
            Email = $"e2e-{Guid.NewGuid()}@library.test",
            FullName = "E2E Reader",
            Password = "E2ePass123!",
        });
        registerResp.StatusCode.Should().Be(HttpStatusCode.Created, "member registration must succeed");
        var registered = await registerResp.Content.ReadFromJsonAsync<IdBody>();
        var memberId = registered!.Id;

        // ── 2. Librarian adds a book manually (no ISBN lookup needed) ────────
        var libClient = factory.CreateClient();
        libClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.LibrarianToken(Guid.NewGuid()));

        var addBookResp = await libClient.PostAsJsonAsync("/api/v1/catalog/books/manual", new
        {
            Title = "Domain-Driven Design",
            Author = "Eric Evans",
            PublicationYear = 2003,
        });
        addBookResp.StatusCode.Should().Be(HttpStatusCode.Created, "manual book add must succeed");
        var addBook = await addBookResp.Content.ReadFromJsonAsync<IdBody>();
        var bookTitleId = addBook!.Id;

        // ── 3. Librarian adds one physical copy ──────────────────────────────
        var addCopyResp = await libClient.PostAsJsonAsync(
            $"/api/v1/catalog/books/{bookTitleId}/copies",
            new { Barcode = $"E2E-{Guid.NewGuid():N}" });
        addCopyResp.StatusCode.Should().Be(HttpStatusCode.Created, "copy add must succeed");

        // ── 4. Librarian searches for the member by name ─────────────────────
        var searchResp = await libClient.GetAsync(
            "/api/v1/identity/members?page=1&pageSize=10&search=E2E+Reader");
        searchResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchResult = await searchResp.Content.ReadFromJsonAsync<PagedResult<MemberItem>>();
        searchResult!.Items.Should().Contain(m => m.MemberId == memberId,
            "member search must surface the registered member");

        // ── 5. Librarian issues the loan ─────────────────────────────────────
        var borrowResp = await libClient.PostAsJsonAsync("/api/v1/lending/loans",
            new { MemberId = memberId, BookTitleId = bookTitleId });
        borrowResp.StatusCode.Should().Be(HttpStatusCode.Created, "borrow must succeed for available copy");
        var borrowed = await borrowResp.Content.ReadFromJsonAsync<BorrowBody>();
        var loanId = borrowed!.LoanId;

        // ── 6. Member views their active loans ───────────────────────────────
        var memberClient = factory.CreateClient();
        memberClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenHelper.MemberToken(memberId));

        var myLoansResp = await memberClient.GetAsync(
            "/api/v1/lending/my-loans?page=1&pageSize=20&activeOnly=true");
        myLoansResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var myLoans = await myLoansResp.Content.ReadFromJsonAsync<PagedResult<LoanItem>>();
        myLoans!.Items.Should().Contain(l => l.LoanId == loanId,
            "member's active loans must include the just-issued loan");

        // ── 7. Librarian returns the book ────────────────────────────────────
        var returnResp = await libClient.PostAsJsonAsync(
            $"/api/v1/lending/loans/{loanId}/return", new { });
        returnResp.StatusCode.Should().Be(HttpStatusCode.OK, "return must succeed for active loan");

        // ── 8. Loan status in DB must be Returned ────────────────────────────
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LendingDbContext>();
        var loan = await db.Loans.FirstOrDefaultAsync(l => l.Id == loanId);
        loan.Should().NotBeNull();
        loan!.Status.Should().Be(LoanStatus.Returned,
            "loan status must flip to Returned after processing");
    }

    // ── Private DTOs (mirror API response shapes) ────────────────────────────

    private sealed record IdBody(Guid Id);
    private sealed record BorrowBody(Guid LoanId, DateTimeOffset DueDate);
    private sealed record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);
    private sealed record MemberItem(Guid MemberId, string FullName, string Email);
    private sealed record LoanItem(Guid LoanId, Guid MemberId, DateTimeOffset DueDate, string Status);
}
