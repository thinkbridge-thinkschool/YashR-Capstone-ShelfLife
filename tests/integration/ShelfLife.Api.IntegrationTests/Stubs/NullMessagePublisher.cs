using ShelfLife.Infrastructure.Messaging;

namespace ShelfLife.Api.IntegrationTests.Stubs;

/// <summary>
/// No-op IMessagePublisher for integration tests.
/// Prevents any attempt to connect to Azure Service Bus.
/// </summary>
internal sealed class NullMessagePublisher : IMessagePublisher
{
    public Task PublishAsync<T>(string topicName, T message, CancellationToken cancellationToken = default)
        where T : class => Task.CompletedTask;
}
