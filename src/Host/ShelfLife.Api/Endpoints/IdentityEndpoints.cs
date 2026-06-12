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
            var result = await handler.HandleAsync(cmd, ct);
            return result.IsSuccess ? Results.Created($"/api/identity/{result.Value}", new { id = result.Value })
                                    : Results.BadRequest(result.Error);
        });

        group.MapPost("/login", async (
            [FromBody] LoginCommand cmd,
            LoginHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(cmd, ct);
            return result.IsSuccess ? Results.Ok(result.Value)
                                    : Results.Unauthorized();
        });

        return group;
    }
}
