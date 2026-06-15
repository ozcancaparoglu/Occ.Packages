using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Occ.Cqrs.Messaging;
using Occ.Infrastructure.Caching.Redis.Cqrs;

namespace Occ.Infrastructure.Caching.Redis;

public static class DependencyInjection
{
    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var cacheOptions = new CacheOptions();
        configuration.GetSection(CacheOptions.SectionName).Bind(cacheOptions);

        services.Configure<CacheOptions>(options =>
            configuration.GetSection(CacheOptions.SectionName).Bind(options));

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = cacheOptions.ConnectionString;
            options.InstanceName = cacheOptions.InstanceName;
        });

        services.AddSingleton<ICacheService, RedisCacheService>();

        return services;
    }

    public static IServiceCollection AddQueryCaching(this IServiceCollection services)
    {
        try
        {
            services.Decorate(typeof(IQueryHandler<,>), typeof(CachingDecorator<,>));
        }
        catch (Scrutor.DecorationException)
        {
            // no IQueryHandler registrations yet — skip
        }

        return services;
    }
}