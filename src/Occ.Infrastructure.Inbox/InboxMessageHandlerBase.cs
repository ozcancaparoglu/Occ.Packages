using System.Text.Json;
using Occ.SharedKernal;

namespace Occ.Infrastructure.Inbox;

public abstract class InboxMessageHandlerBase<TMessage> : IInboxMessageHandler<TMessage>
    where TMessage : class
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public abstract string MessageType { get; }

    public async Task<Result> HandleAsync(string payload, CancellationToken cancellationToken)
    {
        try
        {
            var message = JsonSerializer.Deserialize<TMessage>(payload, JsonOptions);

            if (message is null)
            {
                return Result.Failure(Error.Failure(
                    "InboxMessage.DeserializationFailed",
                    $"Failed to deserialize message to type {typeof(TMessage).Name}"));
            }

            return await HandleAsync(message, cancellationToken);
        }
        catch (JsonException ex)
        {
            return Result.Failure(Error.Failure(
                "InboxMessage.InvalidJson",
                $"Invalid JSON payload: {ex.Message}"));
        }
    }

    public abstract Task<Result> HandleAsync(TMessage message, CancellationToken cancellationToken);
}