namespace Occ.Infrastructure.Inbox;

public sealed record InboxOptions
{
    public const string SectionName = "Inbox";

    public int IntervalInSeconds { get; init; }
    public int BatchSize { get; init; }
    public int MaxRetryCount { get; init; }
    public int RetryDelayInSeconds { get; init; }
}