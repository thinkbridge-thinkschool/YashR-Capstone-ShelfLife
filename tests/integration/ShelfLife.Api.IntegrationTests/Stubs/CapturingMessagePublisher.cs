using ShelfLife.Infrastructure.Messaging;

namespace ShelfLife.Api.IntegrationTests.Stubs;

/// <summary>
/// Captures every publish call so tests can assert what was dispatched.
/// </summary>
internal sealed class CapturingMessagePublisher : IMessagePublisher
{
    private readonly List<(string TopicName, object Message)> _published = [];

    public IReadOnlyList<(string TopicName, object Message)> Published => _published;

    public Task PublishAsync<T>(string topicName, T message, CancellationToken cancellationToken = default)
        where T : class
    {
        _published.Add((topicName, message));
        return Task.CompletedTask;
    }
}
