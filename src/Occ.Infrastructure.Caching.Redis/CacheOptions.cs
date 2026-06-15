namespace Occ.Infrastructure.Caching.Redis;

public sealed record CacheOptions
{
    public const string SectionName = "Cache";

    public string ConnectionString { get; init; } = string.Empty;
    public string InstanceName { get; init; } = string.Empty;
    public int DefaultExpirationInMinutes { get; init; } = 60;
}