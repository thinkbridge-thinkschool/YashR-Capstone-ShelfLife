using ShelfLife.SharedKernel;

namespace ShelfLife.Lending.Domain;

public enum LoanStatus { Active, Returned, Overdue }

public sealed class Loan : AggregateRoot<Guid>
{
    private readonly List<Hold> _holds = [];

    public Guid MemberId { get; private set; }
    public Guid BookTitleId { get; private set; }
    public Guid CopyId { get; private set; }
    public LoanPeriod Period { get; private set; } = null!;
    public LoanStatus Status { get; private set; } = LoanStatus.Active;
    public DateTimeOffset? ReturnedAt { get; private set; }
    public DateTimeOffset? ReminderSentAt { get; private set; }

    public IReadOnlyList<Hold> Holds => _holds.AsReadOnly();

    private Loan() { }

    public static Loan Create(Guid id, Guid memberId, Guid bookTitleId, Guid copyId, LoanPeriod period)
    {
        var loan = new Loan
        {
            Id = id,
            MemberId = memberId,
            BookTitleId = bookTitleId,
            CopyId = copyId,
            Period = period
        };
        loan.Raise(new LoanCreatedDomainEvent(
            Guid.NewGuid(), DateTimeOffset.UtcNow,
            id, memberId, bookTitleId, copyId, period.DueDate));
        return loan;
    }

    public void Return()
    {
        if (Status == LoanStatus.Returned)
            throw new InvalidOperationException("Loan already returned.");

        Status = LoanStatus.Returned;
        ReturnedAt = DateTimeOffset.UtcNow;
        Raise(new LoanReturnedDomainEvent(
            Guid.NewGuid(), DateTimeOffset.UtcNow,
            Id, MemberId, BookTitleId, CopyId));

        var nextHold = _holds.FirstOrDefault(h => h.Status == HoldStatus.Pending);
        if (nextHold is not null)
        {
            nextHold.MarkReady();
            Raise(new HoldReadyDomainEvent(
                Guid.NewGuid(), DateTimeOffset.UtcNow,
                nextHold.Id, nextHold.MemberId, BookTitleId, nextHold.ExpiresAt!.Value));
        }
    }

    public Hold PlaceHold(Guid holdId, Guid memberId)
    {
        if (_holds.Any(h => h.MemberId == memberId && h.Status == HoldStatus.Pending))
            throw new InvalidOperationException("Member already has an active hold on this title.");

        var hold = Hold.Create(holdId, memberId, BookTitleId);
        _holds.Add(hold);
        Raise(new HoldPlacedDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, holdId, memberId, BookTitleId));
        return hold;
    }

    public void MarkOverdue() => Status = LoanStatus.Overdue;

    public void RecordReminderSent() => ReminderSentAt = DateTimeOffset.UtcNow;
}
