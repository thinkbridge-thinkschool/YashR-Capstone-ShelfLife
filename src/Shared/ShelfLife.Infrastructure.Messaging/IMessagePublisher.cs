namespace ShelfLife.Infrastructure.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync<T>(string topicName, T message, CancellationToken cancellationToken = default)
        where T : class;
}
