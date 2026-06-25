using Microsoft.AspNetCore.Mvc;
using ShelfLife.Catalog.Application;



namespace ShelfLife.Api.Endpoints;

public static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/books", async (
            [FromBody] AddBookByIsbnCommand cmd,
            AddBookByIsbnHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(cmd, ct);
            return result.IsSuccess ? Results.Created($"/api/catalog/books/{result.Value}", new { id = result.Value })
                                    : Results.BadRequest(result.Error);
        }).RequireAuthorization("Librarian");

        group.MapPost("/books/{bookTitleId:guid}/copies", async (
            Guid bookTitleId,
            [FromBody] AddCopyRequest req,
            AddCopyHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new AddCopyCommand(bookTitleId, req.Barcode), ct);
            return result.IsSuccess ? Results.Created($"/api/catalog/copies/{result.Value}", new { id = result.Value })
                                    : Results.BadRequest(result.Error);
        }).RequireAuthorization("Librarian");

        group.MapGet("/books", async (
            int page,
            int pageSize,
            string? search,
            GetBooksHandler handler,
            CancellationToken ct) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            return Results.Ok(await handler.HandleAsync(new GetBooksQuery(page, pageSize, search), ct));
        });

        return group;
    }
}

public sealed record AddCopyRequest(string Barcode);
