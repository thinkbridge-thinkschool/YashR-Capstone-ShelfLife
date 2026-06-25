using ShelfLife.SharedKernel;

namespace ShelfLife.Identity.Application;

public sealed record MemberSummaryDto(
    Guid MemberId,
    string FullName,
    string Email,
    string Role,
    bool IsBlocked,
    DateTimeOffset RegisteredAt);

public interface IMembersReadModel
{
    Task<PagedList<MemberSummaryDto>> GetMembersAsync(int page, int pageSize, string? search, CancellationToken ct = default);
}

public sealed record GetMembersQuery(int Page = 1, int PageSize = 20, string? Search = null);

public sealed class GetMembersHandler
{
    private readonly IMembersReadModel _readModel;

    public GetMembersHandler(IMembersReadModel readModel) => _readModel = readModel;

    public Task<PagedList<MemberSummaryDto>> HandleAsync(GetMembersQuery q, CancellationToken ct = default) =>
        _readModel.GetMembersAsync(q.Page, q.PageSize, q.Search, ct);
}
