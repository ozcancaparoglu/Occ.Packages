namespace Occ.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid id,
        string messageType,
        string? payload,
        string? routingKey,
        string? correlationId,
        DateTime occurredOnUtc)
    {
        Id = id;
        MessageType = messageType;
        Payload = payload;
        RoutingKey = routingKey;
        CorrelationId = correlationId;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid Id { get; private set; }
    public string MessageType { get; private set; } = null!;
    public string? Payload { get; private set; }
    public string? RoutingKey { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public DateTime? ProcessedOnUtc { get; private set; }
    public int RetryCount { get; private set; }
    public string? Error { get; private set; }
    public DateTime? NextRetryUtc { get; private set; }

    public static OutboxMessage Create(
        string messageType,
        string? payload,
        string? routingKey = null,
        string? correlationId = null) =>
        new(Guid.CreateVersion7(), messageType, payload, routingKey, correlationId, DateTime.UtcNow);

    public void MarkAsProcessed(DateTime utcNow)
    {
        ProcessedOnUtc = utcNow;
        Error = null;
        NextRetryUtc = null;
    }

    public void RecordFailure(string error, int maxRetryCount, int retryDelayInSeconds, DateTime utcNow)
    {
        RetryCount++;
        Error = error;

        if (RetryCount >= maxRetryCount)
        {
            ProcessedOnUtc = utcNow;
            NextRetryUtc = null;
        }
        else
        {
            var delay = TimeSpan.FromSeconds(retryDelayInSeconds * Math.Pow(2, RetryCount - 1));
            NextRetryUtc = utcNow.Add(delay);
        }
    }
}