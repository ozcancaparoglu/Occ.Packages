using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Occ.Infrastructure.Keycloak.Admin.Options;
using Occ.Infrastructure.Keycloak.Admin.Services;

namespace Occ.Infrastructure.Keycloak.Admin;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Keycloak Admin REST API client.
    /// Call AddKeycloakAuthentication() first so KeycloakOptions is available.
    /// </summary>
    /// <example>
    /// services.AddKeycloakAuthentication(configuration); // Package 1 — auth
    /// services.AddKeycloakAdmin(configuration);          // Package 2 — admin client
    ///
    /// // Then in AuthService, inject IKeycloakAdminService
    /// </example>
    public static IServiceCollection AddKeycloakAdmin(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KeycloakAdminOptions>(
            configuration.GetSection(KeycloakAdminOptions.SectionName));

        services.AddHttpClient<IKeycloakAdminService, KeycloakAdminService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}