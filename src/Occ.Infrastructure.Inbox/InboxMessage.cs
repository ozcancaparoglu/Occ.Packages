namespace Occ.Infrastructure.Inbox;

public sealed class InboxMessage
{
    private InboxMessage()
    {
    }

    private InboxMessage(
        Guid id,
        string messageId,
        string messageType,
        string? payload,
        string? correlationId,
        DateTime occurredOnUtc)
    {
        Id = id;
        MessageId = messageId;
        MessageType = messageType;
        Payload = payload;
        CorrelationId = correlationId;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid Id { get; private set; }
    public string MessageId { get; private set; } = null!;
    public string MessageType { get; private set; } = null!;
    public string? Payload { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public DateTime? ProcessedOnUtc { get; private set; }
    public int RetryCount { get; private set; }
    public string? Error { get; private set; }
    public DateTime? NextRetryUtc { get; private set; }

    public static InboxMessage Create(
        string messageId,
        string messageType,
        string? payload,
        string? correlationId = null) =>
        new(Guid.CreateVersion7(), messageId, messageType, payload, correlationId, DateTime.UtcNow);

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