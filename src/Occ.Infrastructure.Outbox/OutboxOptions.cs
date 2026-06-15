namespace Occ.Infrastructure.Outbox;

public sealed record OutboxOptions
{
    public const string SectionName = "Outbox";

    public int IntervalInSeconds { get; init; }
    public int BatchSize { get; init; }
    public int MaxRetryCount { get; init; }
    public int RetryDelayInSeconds { get; init; }
}