using FluentAssertions;
using NSubstitute;
using Xunit;
using ShelfLife.Lending.Contracts;
using ShelfLife.Notifications.Application;

namespace ShelfLife.Notifications.Application.Tests;

public sealed class IdempotencyTests
{
    [Fact]
    public async Task BookBorrowedHandler_WhenAlreadyProcessed_DoesNotSend()
    {
        var sender = Substitute.For<INotificationSender>();
        var idempotency = Substitute.For<IIdempotencyStore>();
        var members = Substitute.For<IMemberLookup>();
        var eventId = Guid.NewGuid();

        idempotency.HasBeenProcessedAsync(eventId, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new BookBorrowedNotificationHandler(sender, idempotency, members);
        var evt = new BookBorrowedEvent(eventId, DateTimeOffset.UtcNow,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(14));

        await handler.HandleAsync(evt);

        await sender.DidNotReceive().SendAsync(Arg.Any<NotificationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BookBorrowedHandler_WhenNew_SendsAndMarksProcessed()
    {
        var sender = Substitute.For<INotificationSender>();
        var idempotency = Substitute.For<IIdempotencyStore>();
        var members = Substitute.For<IMemberLookup>();
        var eventId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        idempotency.HasBeenProcessedAsync(eventId, Arg.Any<CancellationToken>()).Returns(false);
        members.GetEmailAsync(memberId, Arg.Any<CancellationToken>()).Returns("member@test.com");

        var handler = new BookBorrowedNotificationHandler(sender, idempotency, members);
        var evt = new BookBorrowedEvent(eventId, DateTimeOffset.UtcNow,
            Guid.NewGuid(), memberId, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(14));

        await handler.HandleAsync(evt);

        await sender.Received(1).SendAsync(Arg.Any<NotificationRequest>(), Arg.Any<CancellationToken>());
        await idempotency.Received(1).MarkProcessedAsync(eventId, Arg.Any<CancellationToken>());
    }
}
