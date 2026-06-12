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
            var memberId = Guid.Parse(user.FindFirstValue("sub")!);
            var result = await handler.HandleAsync(new BorrowBookCommand(memberId, req.BookTitleId), ct);
            return result.IsSuccess ? Results.Created($"/api/lending/loans/{result.Value.LoanId}", result.Value)
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
            var memberId = Guid.Parse(user.FindFirstValue("sub")!);
            var result = await handler.HandleAsync(new PlaceHoldCommand(memberId, req.BookTitleId), ct);
            return result.IsSuccess ? Results.Created($"/api/lending/holds/{result.Value}", new { id = result.Value })
                                    : Results.BadRequest(result.Error);
        });

        return group;
    }
}

public sealed record BorrowBookRequest(Guid BookTitleId);
public sealed record PlaceHoldRequest(Guid BookTitleId);
