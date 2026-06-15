using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Occ.Infrastructure.Authentication.Keycloak.Authorization.Policies;

namespace Occ.Infrastructure.Authentication.Keycloak.Authorization;

public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers core Project role-based authorization policies.
    /// Pass a callback to add service-specific policies alongside the built-in ones.
    /// </summary>
    /// <example>
    /// builder.Services.AddOccAuthorization(auth =>
    ///     auth.AddPolicy("LimitServiceAccess", p => p.RequireAuthenticatedUser()));
    /// </example>
    public static IServiceCollection AddOccAuthorization(
        this IServiceCollection services,
        Action<AuthorizationBuilder>? configure = null)
    {
        var builder = services.AddAuthorizationBuilder()
            .AddRolePolicies();

        configure?.Invoke(builder);

        return services;
    }
}