using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShelfLife.Identity.Application;

namespace ShelfLife.Api.Endpoints;

public static class IdentityEndpoints
{
    public static RouteGroupBuilder MapIdentityEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (
            [FromBody] RegisterMemberCommand cmd,
            RegisterMemberHandler handler,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(cmd?.Email) ||
                string.IsNullOrWhiteSpace(cmd?.Password) ||
                string.IsNullOrWhiteSpace(cmd?.FullName))
                return Results.BadRequest("Email, password, and name are required.");

            var result = await handler.HandleAsync(cmd, ct);
            return result.IsSuccess ? Results.Created($"/api/v1/identity/{result.Value}", new { id = result.Value })
                                    : Results.BadRequest(result.Error);
        });

        group.MapPost("/login", async (
            [FromBody] LoginCommand cmd,
            LoginHandler handler,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(cmd?.Email) || string.IsNullOrWhiteSpace(cmd?.Password))
                return Results.BadRequest("Email and password are required.");

            var result = await handler.HandleAsync(cmd, ct);
            return result.IsSuccess ? Results.Ok(result.Value)
                                    : Results.Unauthorized();
        });

        group.MapGet("/members", async (
            int page,
            int pageSize,
            string? search,
            GetMembersHandler handler,
            CancellationToken ct) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            return Results.Ok(await handler.HandleAsync(new GetMembersQuery(page, pageSize, search), ct));
        }).RequireAuthorization("Librarian");

        return group;
    }
}
