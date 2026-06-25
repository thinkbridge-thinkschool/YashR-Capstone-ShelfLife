namespace ShelfLife.Infrastructure.Outbox;

public sealed class DeadLetterMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OriginalMessageId { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public string TopicName { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset DeadLetteredAt { get; init; } = DateTimeOffset.UtcNow;
    public string LastError { get; init; } = string.Empty;
    public int RetryCount { get; init; }
}
