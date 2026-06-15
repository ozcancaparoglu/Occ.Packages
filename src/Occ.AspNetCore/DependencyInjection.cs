using Microsoft.Extensions.DependencyInjection;
using Occ.AspNetCore.Exceptions;
using Occ.AspNetCore.Http;
using Occ.Infrastructure.Correlation;

namespace Occ.AspNetCore;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddHttpContextAccessor();
        services.AddCorrelation();
        services.AddTransient<CorrelationIdDelegatingHandler>();
        return services;
    }
}