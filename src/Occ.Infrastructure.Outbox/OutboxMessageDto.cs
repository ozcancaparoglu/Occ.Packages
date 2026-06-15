namespace Occ.Infrastructure.Outbox;

internal sealed record OutboxMessageDto(
    Guid Id,
    string MessageType,
    string? Payload,
    string? RoutingKey,
    string? CorrelationId,
    DateTime OccurredOnUtc,
    DateTime? ProcessedOnUtc,
    int RetryCount,
    string? Error,
    DateTime? NextRetryUtc);