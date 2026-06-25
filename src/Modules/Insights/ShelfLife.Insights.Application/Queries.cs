using ShelfLife.Insights.Contracts;
using ShelfLife.SharedKernel;

namespace ShelfLife.Insights.Application;

public interface IInsightsReadModel
{
    Task<PagedList<PopularTitleDto>> GetPopularTitlesAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<PagedList<OverdueLoanDto>> GetOverdueLoansAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<PagedList<MemberActivityDto>> GetMemberActivityAsync(int page, int pageSize, CancellationToken ct = default);
}

public sealed record GetPopularTitlesQuery(int Page = 1, int PageSize = 20, string? Search = null);
public sealed record GetOverdueLoansQuery(int Page = 1, int PageSize = 20, string? Search = null);
public sealed record GetMemberActivityQuery(int Page = 1, int PageSize = 20);

public sealed class InsightsQueryHandler
{
    private readonly IInsightsReadModel _readModel;

    public InsightsQueryHandler(IInsightsReadModel readModel) => _readModel = readModel;

    public Task<PagedList<PopularTitleDto>> HandleAsync(GetPopularTitlesQuery q, CancellationToken ct = default) =>
        _readModel.GetPopularTitlesAsync(q.Page, q.PageSize, q.Search, ct);

    public Task<PagedList<OverdueLoanDto>> HandleAsync(GetOverdueLoansQuery q, CancellationToken ct = default) =>
        _readModel.GetOverdueLoansAsync(q.Page, q.PageSize, q.Search, ct);

    public Task<PagedList<MemberActivityDto>> HandleAsync(GetMemberActivityQuery q, CancellationToken ct = default) =>
        _readModel.GetMemberActivityAsync(q.Page, q.PageSize, ct);
}
