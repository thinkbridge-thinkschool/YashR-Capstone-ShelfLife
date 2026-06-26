using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShelfLife.Api.IntegrationTests.Fixtures;
using ShelfLife.Api.IntegrationTests.Stubs;
using ShelfLife.Identity.Infrastructure;
using ShelfLife.Infrastructure.Messaging;
using ShelfLife.Infrastructure.Outbox;
using ShelfLife.Infrastructure.Persistence;

namespace ShelfLife.Api.IntegrationTests.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class OutboxRelayWorkerTests(ShelfLifeApiFactory factory)
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<(IOutboxStore store, IdentityDbContext db)> CreateScopeAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var store = new EfOutboxStore(db);
        return (store, db);
    }

    private static OutboxRelayProcessor MakeProcessor(IOutboxStore store, IMessagePublisher publisher) =>
        new(store, publisher, NullLogger<OutboxRelayProcessor>.Instance);

    private static OutboxMessage NewMessage() => new()
    {
        Type = "test.event",
        Payload = """{"test":true}""",
        TopicName = "test-topic",
    };

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessBatch_PendingMessage_IsPublishedAndMarkedProcessed()
    {
        // Arrange
        var (store, db) = await CreateScopeAsync();
        var publisher = new CapturingMessagePublisher();
        var processor = MakeProcessor(store, publisher);

        var message = NewMessage();
        await store.AddAsync(message);

        // Act
        await processor.ProcessBatchAsync(CancellationToken.None);

        // Assert — publisher received the call
        publisher.Published.Should().ContainSingle(p => p.TopicName == "test-topic");

        // Assert — DB row marked processed
        var row = await db.OutboxMessages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == message.Id);
        row.Should().NotBeNull();
        row!.ProcessedAt.Should().NotBeNull("message must be marked processed");
    }

    [Fact]
    public async Task ProcessBatch_PublisherFails_IncrementsRetryCountAndSetsNextRetryAt()
    {
        // Arrange
        var (store, db) = await CreateScopeAsync();
        var processor = MakeProcessor(store, new FailingMessagePublisher());

        var message = NewMessage();
        await store.AddAsync(message);

        // Act
        await processor.ProcessBatchAsync(CancellationToken.None);

        // Assert
        var row = await db.OutboxMessages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == message.Id);
        row.Should().NotBeNull();
        row!.RetryCount.Should().Be(1, "first failure increments retry count");
        row.NextRetryAt.Should().NotBeNull("exponential backoff schedules the next attempt");
        row.NextRetryAt.Should().BeAfter(DateTimeOffset.UtcNow, "retry is in the future");
        row.ProcessedAt.Should().BeNull("message was not successfully processed");
    }

    [Fact]
    public async Task ProcessBatch_MaxRetriesExceeded_MessageMovedToDeadLetter()
    {
        // Arrange — seed a message already at the retry threshold
        var (store, db) = await CreateScopeAsync();
        var processor = MakeProcessor(store, new FailingMessagePublisher());

        // RetryCount = MaxRetries - 1 means the next failure will dead-letter it
        var message = new OutboxMessage
        {
            Type = "test.event",
            Payload = """{"test":true}""",
            TopicName = "test-topic",
            RetryCount = OutboxRelayProcessor.MaxRetries - 1,
        };
        await store.AddAsync(message);

        // Act
        await processor.ProcessBatchAsync(CancellationToken.None);

        // Assert — removed from OutboxMessages
        var outboxRow = await db.OutboxMessages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == message.Id);
        outboxRow.Should().BeNull("dead-lettered messages are removed from the outbox");

        // Assert — present in DeadLetterMessages
        var dlRow = await db.DeadLetterMessages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.OriginalMessageId == message.Id);
        dlRow.Should().NotBeNull("message must appear in the dead-letter table");
        dlRow!.TopicName.Should().Be("test-topic");
        dlRow.RetryCount.Should().Be(OutboxRelayProcessor.MaxRetries);
        dlRow.LastError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessBatch_MessageWithFutureNextRetryAt_IsSkipped()
    {
        // Arrange — persist a message whose retry is due in the future
        var (store, db) = await CreateScopeAsync();
        var publisher = new CapturingMessagePublisher();
        var processor = MakeProcessor(store, publisher);

        var futureMessage = new OutboxMessage
        {
            Type = "test.event",
            Payload = """{"test":true}""",
            TopicName = "test-topic",
            RetryCount = 1,
            NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(10),
        };
        db.OutboxMessages.Add(futureMessage);
        await db.SaveChangesAsync();

        // Act
        await processor.ProcessBatchAsync(CancellationToken.None);

        // Assert — publisher never called
        publisher.Published.Should().BeEmpty("message retry is not yet due");
    }

    [Fact]
    public async Task E2E_PendingOutboxMessage_IsRelayedSuccessfully()
    {
        // This test verifies the full pipeline:
        //   write to outbox → processor runs → publisher called → row marked processed

        // Arrange
        await using var scope = factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<IdentityDbContext>();
        var store = new EfOutboxStore(db);
        var publisher = new CapturingMessagePublisher();
        var processor = MakeProcessor(store, publisher);

        var message = new OutboxMessage
        {
            Type = "catalog.book_added",
            Payload = """{"isbn":"9780201633610","title":"The Pragmatic Programmer"}""",
            TopicName = "catalog.book-added",
        };
        await store.AddAsync(message);

        // Act — relay worker batch
        await processor.ProcessBatchAsync(CancellationToken.None);

        // Assert — dispatched exactly once
        publisher.Published.Should().ContainSingle(
            p => p.TopicName == "catalog.book-added",
            "the outbox message must be relayed to the publisher");

        // Assert — persistence state
        var row = await db.OutboxMessages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == message.Id);
        row!.ProcessedAt.Should().NotBeNull();
        row.RetryCount.Should().Be(0, "successful dispatch requires no retries");
    }

    [Fact]
    public async Task ExponentialBackoff_DelayDoublesWithEachRetry()
    {
        // Verify the delay formula: base * 2^(retryCount - 1)
        var delay1 = OutboxRelayProcessor.ComputeRetryDelay(1);
        var delay2 = OutboxRelayProcessor.ComputeRetryDelay(2);
        var delay3 = OutboxRelayProcessor.ComputeRetryDelay(3);
        var delay4 = OutboxRelayProcessor.ComputeRetryDelay(4);

        delay2.Should().Be(delay1 * 2, "each retry doubles the previous delay");
        delay3.Should().Be(delay2 * 2);
        delay4.Should().Be(delay3 * 2);
    }
}
