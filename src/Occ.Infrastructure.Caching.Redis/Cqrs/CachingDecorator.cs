using Occ.Cqrs.Messaging;
using Occ.SharedKernal;

namespace Occ.Infrastructure.Caching.Redis.Cqrs;

internal sealed class CachingDecorator<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> innerHandler,
    ICacheService cacheService)
    : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        if (query is not ICachedQuery<TResponse> cachedQuery)
            return await innerHandler.Handle(query, cancellationToken);

        var cached = await cacheService.GetAsync<TResponse>(cachedQuery.CacheKey, cancellationToken);
        if (cached is not null)
            return Result.Success(cached);

        var result = await innerHandler.Handle(query, cancellationToken);

        if (result.IsSuccess)
            await cacheService.SetAsync(cachedQuery.CacheKey, result.Value, cachedQuery.Expiration, cancellationToken);

        return result;
    }
}