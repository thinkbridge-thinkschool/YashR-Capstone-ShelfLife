using Microsoft.AspNetCore.Authorization;
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
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new BorrowBookCommand(req.MemberId, req.BookTitleId), ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/lending/loans/{result.Value.LoanId}", result.Value)
                : Results.BadRequest(result.Error);
        }).RequireAuthorization("Librarian");

        group.MapGet("/loans", async (
            int page,
            int pageSize,
            bool activeOnly,
            string? search,
            GetLoansHandler handler,
            CancellationToken ct) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            return Results.Ok(await handler.HandleAsync(new GetLoansQuery(page, pageSize, activeOnly, search), ct));
        }).RequireAuthorization("Librarian");

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

        group.MapGet("/holds", async (
            int page,
            int pageSize,
            string? search,
            GetHoldsHandler handler,
            CancellationToken ct) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            return Results.Ok(await handler.HandleAsync(new GetHoldsQuery(page, pageSize, search), ct));
        }).RequireAuthorization("Librarian");

        group.MapGet("/my-loans", async (
            int page,
            int pageSize,
            bool activeOnly,
            GetMyLoansHandler handler,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue("sub"), out var memberId))
                return Results.Unauthorized();
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            return Results.Ok(await handler.HandleAsync(new GetMyLoansQuery(memberId, page, pageSize, activeOnly), ct));
        });

        group.MapGet("/my-holds", async (
            int page,
            int pageSize,
            GetMyHoldsHandler handler,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue("sub"), out var memberId))
                return Results.Unauthorized();
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            return Results.Ok(await handler.HandleAsync(new GetMyHoldsQuery(memberId, page, pageSize), ct));
        });

        return group;
    }
}

public sealed record BorrowBookRequest(Guid MemberId, Guid BookTitleId);
public sealed record PlaceHoldRequest(Guid BookTitleId);
