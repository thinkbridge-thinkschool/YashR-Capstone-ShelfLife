using ShelfLife.Insights.Application;

namespace ShelfLife.Api.Endpoints;

public static class InsightsEndpoints
{
    // Maximum rows per page — prevents unbounded SQL FETCH NEXT (D-02 in threat model)
    private const int MaxPageSize = 100;

    public static RouteGroupBuilder MapInsightsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/popular-titles", async (
            int page, int pageSize, string? search,
            InsightsQueryHandler handler,
            CancellationToken ct) =>
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            return Results.Ok(await handler.HandleAsync(new GetPopularTitlesQuery(page, pageSize, search), ct));
        });

        group.MapGet("/overdue-loans", async (
            int page, int pageSize, string? search,
            InsightsQueryHandler handler,
            CancellationToken ct) =>
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            return Results.Ok(await handler.HandleAsync(new GetOverdueLoansQuery(page, pageSize, search), ct));
        });

        group.MapGet("/member-activity", async (
            int page, int pageSize,
            InsightsQueryHandler handler,
            CancellationToken ct) =>
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            return Results.Ok(await handler.HandleAsync(new GetMemberActivityQuery(page, pageSize), ct));
        });

        return group;
    }
}
