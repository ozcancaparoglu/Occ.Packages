namespace Occ.SharedKernal;

public interface IMessagePublisher
{
    Task PublishAsync<T>(
        T message,
        string messageType,
        string? messageId = null,
        string? correlationId = null,
        string? routingKey = null,
        CancellationToken cancellationToken = default) where T : class;

    Task PublishRawAsync(
        string payload,
        string messageType,
        string? messageId = null,
        string? correlationId = null,
        string? routingKey = null,
        CancellationToken cancellationToken = default);
}
