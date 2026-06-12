using ShelfLife.Insights.Application;

namespace ShelfLife.Api.Endpoints;

public static class InsightsEndpoints
{
    public static RouteGroupBuilder MapInsightsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/popular-titles", async (
            int page, int pageSize,
            InsightsQueryHandler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new GetPopularTitlesQuery(page, pageSize), ct)));

        group.MapGet("/overdue-loans", async (
            int page, int pageSize,
            InsightsQueryHandler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new GetOverdueLoansQuery(page, pageSize), ct)));

        group.MapGet("/member-activity", async (
            int page, int pageSize,
            InsightsQueryHandler handler,
            CancellationToken ct) =>
            Results.Ok(await handler.HandleAsync(new GetMemberActivityQuery(page, pageSize), ct)));

        return group;
    }
}
