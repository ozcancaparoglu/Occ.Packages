using Microsoft.Extensions.DependencyInjection;
using Occ.SharedKernal;

namespace Occ.Infrastructure.DomainEvents;

public static class DependencyInjection
{
    public static IServiceCollection AddDomainEvents(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        return services;
    }
}