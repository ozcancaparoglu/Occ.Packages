using Occ.Cqrs.Messaging;

namespace Occ.Infrastructure.Caching.Redis.Cqrs;

public interface ICachedQuery<TResponse> : IQuery<TResponse>
{
    string CacheKey { get; }
    TimeSpan? Expiration { get; }
}