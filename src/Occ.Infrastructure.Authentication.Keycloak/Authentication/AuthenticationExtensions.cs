using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Occ.Infrastructure.Authentication.Keycloak.Authentication;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Registers Keycloak JWT Bearer authentication with automatic role and permission claim mapping.
    /// Call in Program.cs: builder.Services.AddKeycloakAuthentication(builder.Configuration);
    /// </summary>
    public static IServiceCollection AddKeycloakAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new KeycloakOptions();
        configuration.GetSection(KeycloakOptions.SectionName).Bind(options);

        services.Configure<KeycloakOptions>(
            configuration.GetSection(KeycloakOptions.SectionName));

        services
            .AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(o =>
            {
                o.Authority = options.Issuer;
                o.RequireHttpsMetadata = options.RequireHttpsMetadata;

                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = "account",
                    ValidateLifetime = true,
                    NameClaimType = "preferred_username",
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                o.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerHandler>>();

                        MapKeycloakClaims(context, logger);

                        logger.LogDebug("Token validated for user {Username}",
                            context.Principal?.Identity?.Name ?? "unknown");

                        return Task.CompletedTask;
                    },

                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerHandler>>();

                        logger.LogWarning(context.Exception,
                            "JWT authentication failed for {Path}",
                            context.HttpContext.Request.Path);

                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    /// <summary>
    /// Maps Keycloak role claims from all token locations into standard ClaimTypes.Role claims.
    /// Also extracts client roles as fine-grained ProjectClaimTypes.Permission claims.
    ///
    /// Keycloak can place roles in three locations:
    ///   1. "roles": [...]                            — custom flat mapper
    ///   2. "realm_access": { "roles": [...] }        — default realm roles
    ///   3. "resource_access": { "client": { "roles": [...] } } — client roles
    /// </summary>
    /// <summary>
    /// Registers symmetric-key JWT Bearer authentication for internal service-to-service tokens.
    /// Validates tokens issued by Project.Auth (issuer: "Project-auth", audience: "Project-services").
    /// Call in Program.cs: builder.Services.AddInternalJwtAuthentication(builder.Configuration);
    /// </summary>
    public static IServiceCollection AddInternalJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new InternalJwtOptions();
        configuration.GetSection(InternalJwtOptions.SectionName).Bind(options);

        services.Configure<InternalJwtOptions>(
            configuration.GetSection(InternalJwtOptions.SectionName));

        services
            .AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false;

                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(options.SecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    NameClaimType = "preferred_username",
                    RoleClaimType = ClaimTypes.Role,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                o.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerHandler>>();
                        logger.LogWarning(context.Exception,
                            "Internal JWT authentication failed for {Path}",
                            context.HttpContext.Request.Path);
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    private static void MapKeycloakClaims(TokenValidatedContext context, ILogger logger)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
            return;

        var roles = new HashSet<string>();
        var permissions = new HashSet<string>();

        // Source 1: flat "roles" claim (custom Keycloak mapper)
        foreach (var claim in context.Principal.FindAll("roles"))
        {
            if (claim.Value.StartsWith("["))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<string[]>(claim.Value);
                    if (parsed is not null)
                        foreach (var r in parsed) roles.Add(r);
                }
                catch { /* not a JSON array */ }
            }
            else
            {
                roles.Add(claim.Value);
            }
        }

        // Source 2: realm_access.roles (Keycloak default)
        var realmAccess = context.Principal.FindFirst("realm_access");
        if (realmAccess is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(realmAccess.Value);
                if (doc.RootElement.TryGetProperty("roles", out var realmRoles))
                    foreach (var role in realmRoles.EnumerateArray())
                    {
                        var name = role.GetString();
                        if (name is not null) roles.Add(name);
                    }
            }
            catch { /* malformed */ }
        }

        // Source 3: resource_access.{client}.roles — mapped as both roles and permissions
        var resourceAccess = context.Principal.FindFirst("resource_access");
        if (resourceAccess is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(resourceAccess.Value);
                foreach (var client in doc.RootElement.EnumerateObject())
                {
                    if (client.Value.TryGetProperty("roles", out var clientRoles))
                        foreach (var role in clientRoles.EnumerateArray())
                        {
                            var name = role.GetString();
                            if (name is not null)
                            {
                                roles.Add(name);
                                permissions.Add(name);
                            }
                        }
                }
            }
            catch { /* malformed */ }
        }

        foreach (var role in roles)
            if (!string.IsNullOrEmpty(role) && !role.StartsWith("default-roles-"))
                identity.AddClaim(new Claim(ClaimTypes.Role, role));

        foreach (var permission in permissions)
            if (!string.IsNullOrEmpty(permission))
                identity.AddClaim(new Claim(OccClaimTypes.Permission, permission));

        logger.LogDebug("Mapped {RoleCount} roles and {PermCount} permissions",
            roles.Count, permissions.Count);
    }
}