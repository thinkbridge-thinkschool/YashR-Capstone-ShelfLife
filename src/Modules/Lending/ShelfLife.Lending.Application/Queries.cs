using ShelfLife.SharedKernel;

namespace ShelfLife.Lending.Application;

// ── All Loans (librarian view) ───────────────────────────────────────────────

public sealed record LoanSummaryDto(
    Guid LoanId,
    Guid MemberId,
    string MemberName,
    string MemberEmail,
    Guid BookTitleId,
    string BookTitle,
    string Author,
    DateTimeOffset BorrowedAt,
    DateTimeOffset DueDate,
    string Status,
    bool IsOverdue);

public interface ILoansReadModel
{
    Task<PagedList<LoanSummaryDto>> GetLoansAsync(int page, int pageSize, bool activeOnly, string? search, CancellationToken ct = default);
}

public sealed record GetLoansQuery(int Page = 1, int PageSize = 20, bool ActiveOnly = false, string? Search = null);

public sealed class GetLoansHandler
{
    private readonly ILoansReadModel _readModel;
    public GetLoansHandler(ILoansReadModel readModel) => _readModel = readModel;
    public Task<PagedList<LoanSummaryDto>> HandleAsync(GetLoansQuery q, CancellationToken ct = default) =>
        _readModel.GetLoansAsync(q.Page, q.PageSize, q.ActiveOnly, q.Search, ct);
}

// ── My Loans (member-scoped) ─────────────────────────────────────────────────

public sealed record MyLoanDto(
    Guid LoanId,
    Guid BookTitleId,
    string BookTitle,
    string Author,
    DateTimeOffset BorrowedAt,
    DateTimeOffset DueDate,
    string Status,
    bool IsOverdue);

public interface IMyLoansReadModel
{
    Task<PagedList<MyLoanDto>> GetMyLoansAsync(Guid memberId, int page, int pageSize, bool activeOnly, CancellationToken ct = default);
}

public sealed record GetMyLoansQuery(Guid MemberId, int Page = 1, int PageSize = 20, bool ActiveOnly = false);

public sealed class GetMyLoansHandler
{
    private readonly IMyLoansReadModel _readModel;
    public GetMyLoansHandler(IMyLoansReadModel readModel) => _readModel = readModel;
    public Task<PagedList<MyLoanDto>> HandleAsync(GetMyLoansQuery q, CancellationToken ct = default) =>
        _readModel.GetMyLoansAsync(q.MemberId, q.Page, q.PageSize, q.ActiveOnly, ct);
}

// ── My Holds (member-scoped) ─────────────────────────────────────────────────

public sealed record MyHoldDto(
    Guid HoldId,
    Guid BookTitleId,
    string BookTitle,
    string Author,
    string Status,
    DateTimeOffset PlacedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? ExpiresAt);

public interface IMyHoldsReadModel
{
    Task<PagedList<MyHoldDto>> GetMyHoldsAsync(Guid memberId, int page, int pageSize, CancellationToken ct = default);
}

public sealed record GetMyHoldsQuery(Guid MemberId, int Page = 1, int PageSize = 20);

public sealed class GetMyHoldsHandler
{
    private readonly IMyHoldsReadModel _readModel;
    public GetMyHoldsHandler(IMyHoldsReadModel readModel) => _readModel = readModel;
    public Task<PagedList<MyHoldDto>> HandleAsync(GetMyHoldsQuery q, CancellationToken ct = default) =>
        _readModel.GetMyHoldsAsync(q.MemberId, q.Page, q.PageSize, ct);
}

public sealed record HoldDto(
    Guid HoldId,
    Guid MemberId,
    string MemberName,
    Guid BookTitleId,
    string BookTitle,
    string Status,
    DateTimeOffset PlacedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? ExpiresAt);

public interface IHoldsReadModel
{
    Task<PagedList<HoldDto>> GetHoldsAsync(int page, int pageSize, string? search, CancellationToken ct = default);
}

public sealed record GetHoldsQuery(int Page = 1, int PageSize = 20, string? Search = null);

public sealed class GetHoldsHandler
{
    private readonly IHoldsReadModel _readModel;

    public GetHoldsHandler(IHoldsReadModel readModel) => _readModel = readModel;

    public Task<PagedList<HoldDto>> HandleAsync(GetHoldsQuery q, CancellationToken ct = default) =>
        _readModel.GetHoldsAsync(q.Page, q.PageSize, q.Search, ct);
}
