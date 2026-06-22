using Microsoft.AspNetCore.Mvc;
using ShelfLife.Lending.Application;
using System.Security.Claims;

namespace ShelfLife.Api.Endpoints;

public static class LendingEndpoints
{
    public static RouteGroupBuilder MapLendingEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/loans", async (
            [FromBody] BorrowBookRequest req,
            BorrowBookHandler handler,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            // Guard against a malformed or absent 'sub' claim (E-03 in threat model).
            // Entra-issued tokens always carry a valid GUID sub; dev HS256 tokens
            // might not — return 401 rather than letting FormatException bubble to a 500.
            if (!Guid.TryParse(user.FindFirstValue("sub"), out var memberId))
                return Results.Unauthorized();

            var result = await handler.HandleAsync(new BorrowBookCommand(memberId, req.BookTitleId), ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/lending/loans/{result.Value.LoanId}", result.Value)
                : Results.BadRequest(result.Error);
        });

        group.MapPost("/loans/{loanId:guid}/return", async (
            Guid loanId,
            ReturnBookHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new ReturnBookCommand(loanId), ct);
            return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
        });

        group.MapPost("/holds", async (
            [FromBody] PlaceHoldRequest req,
            PlaceHoldHandler handler,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue("sub"), out var memberId))
                return Results.Unauthorized();

            var result = await handler.HandleAsync(new PlaceHoldCommand(memberId, req.BookTitleId), ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/lending/holds/{result.Value}", new { id = result.Value })
                : Results.BadRequest(result.Error);
        });

        return group;
    }
}

public sealed record BorrowBookRequest(Guid BookTitleId);
public sealed record PlaceHoldRequest(Guid BookTitleId);
