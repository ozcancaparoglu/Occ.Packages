using Microsoft.Extensions.DependencyInjection;

namespace Occ.Infrastructure.Correlation;

public static class DependencyInjection
{
    public static IServiceCollection AddCorrelation(this IServiceCollection services)
    {
        services.AddSingleton<ICorrelationAccessor, CorrelationAccessor>();
        services.AddSingleton<CorrelationIdEnricher>();

        return services;
    }
}
