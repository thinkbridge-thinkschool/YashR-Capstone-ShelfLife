using ShelfLife.Infrastructure.Messaging;

namespace ShelfLife.Api.IntegrationTests.Stubs;

/// <summary>
/// Always throws to simulate a broken Service Bus connection.
/// Used to drive retry and dead-letter paths in outbox relay tests.
/// </summary>
internal sealed class FailingMessagePublisher : IMessagePublisher
{
    public Task PublishAsync<T>(string topicName, T message, CancellationToken cancellationToken = default)
        where T : class =>
        throw new InvalidOperationException("Simulated Service Bus failure");
}
