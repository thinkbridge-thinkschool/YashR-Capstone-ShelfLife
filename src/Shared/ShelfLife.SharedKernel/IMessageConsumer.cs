namespace ShelfLife.SharedKernel;

public interface IMessageConsumer<T> where T : class
{
    Task HandleAsync(T message, CancellationToken cancellationToken = default);
}
