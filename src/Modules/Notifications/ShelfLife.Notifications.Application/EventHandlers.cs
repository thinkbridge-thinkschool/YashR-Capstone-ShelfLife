using ShelfLife.Lending.Contracts;
using ShelfLife.SharedKernel;

namespace ShelfLife.Notifications.Application;

public sealed class BookBorrowedNotificationHandler : IMessageConsumer<BookBorrowedEvent>
{
    private readonly INotificationSender _sender;
    private readonly IIdempotencyStore _idempotency;
    private readonly IMemberLookup _members;

    public BookBorrowedNotificationHandler(
        INotificationSender sender, IIdempotencyStore idempotency, IMemberLookup members)
    {
        _sender = sender;
        _idempotency = idempotency;
        _members = members;
    }

    public async Task HandleAsync(BookBorrowedEvent message, CancellationToken ct = default)
    {
        if (await _idempotency.HasBeenProcessedAsync(message.EventId, ct)) return;

        var member = await _members.GetEmailAsync(message.MemberId, ct);
        await _sender.SendAsync(new NotificationRequest(
            message.MemberId, member,
            "Loan Confirmed",
            $"You have borrowed a book. Due date: {message.DueDate:dd MMM yyyy}."), ct);

        await _idempotency.MarkProcessedAsync(message.EventId, ct);
    }
}

public sealed class HoldReadyNotificationHandler : IMessageConsumer<HoldReadyEvent>
{
    private readonly INotificationSender _sender;
    private readonly IIdempotencyStore _idempotency;
    private readonly IMemberLookup _members;

    public HoldReadyNotificationHandler(
        INotificationSender sender, IIdempotencyStore idempotency, IMemberLookup members)
    {
        _sender = sender;
        _idempotency = idempotency;
        _members = members;
    }

    public async Task HandleAsync(HoldReadyEvent message, CancellationToken ct = default)
    {
        if (await _idempotency.HasBeenProcessedAsync(message.EventId, ct)) return;

        var member = await _members.GetEmailAsync(message.MemberId, ct);
        await _sender.SendAsync(new NotificationRequest(
            message.MemberId, member,
            "Your Hold Is Ready",
            $"Your held book is available for pickup until {message.ExpiresAt:dd MMM yyyy}."), ct);

        await _idempotency.MarkProcessedAsync(message.EventId, ct);
    }
}

public sealed class LoanOverdueNotificationHandler : IMessageConsumer<LoanOverdueEvent>
{
    private readonly INotificationSender _sender;
    private readonly IIdempotencyStore _idempotency;
    private readonly IMemberLookup _members;

    public LoanOverdueNotificationHandler(
        INotificationSender sender, IIdempotencyStore idempotency, IMemberLookup members)
    {
        _sender = sender;
        _idempotency = idempotency;
        _members = members;
    }

    public async Task HandleAsync(LoanOverdueEvent message, CancellationToken ct = default)
    {
        if (await _idempotency.HasBeenProcessedAsync(message.EventId, ct)) return;

        var member = await _members.GetEmailAsync(message.MemberId, ct);
        await _sender.SendAsync(new NotificationRequest(
            message.MemberId, member,
            "Overdue Book Reminder",
            $"Your loan is overdue since {message.DueDate:dd MMM yyyy}. Please return it promptly."), ct);

        await _idempotency.MarkProcessedAsync(message.EventId, ct);
    }
}

public interface IMemberLookup
{
    Task<string> GetEmailAsync(Guid memberId, CancellationToken ct = default);
}
