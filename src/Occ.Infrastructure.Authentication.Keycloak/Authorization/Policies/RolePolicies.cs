using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Occ.Infrastructure.Authentication.Keycloak.Authorization.Policies;

public static class RolePolicies
{
    public static AuthorizationBuilder AddRolePolicies(this AuthorizationBuilder builder)
    {
        builder
            .AddPolicy(PolicyNames.RequireAdminRole, policy =>
                policy.RequireClaim(ClaimTypes.Role, Roles.Admin))

            .AddPolicy(PolicyNames.RequireUserRole, policy =>
                policy.RequireClaim(ClaimTypes.Role, Roles.User))

            .AddPolicy(PolicyNames.RequireManagerRole, policy =>
                policy.RequireClaim(ClaimTypes.Role, Roles.Manager))

            .AddPolicy(PolicyNames.RequireAdminOrManager, policy =>
                policy.RequireAssertion(ctx =>
                    ctx.User.HasClaim(ClaimTypes.Role, Roles.Admin) ||
                    ctx.User.HasClaim(ClaimTypes.Role, Roles.Manager)))

            .AddPolicy(PolicyNames.RequireAuthenticatedUser, policy =>
                policy.RequireAssertion(ctx =>
                    ctx.User.HasClaim(ClaimTypes.Role, Roles.User) ||
                    ctx.User.HasClaim(ClaimTypes.Role, Roles.Admin) ||
                    ctx.User.HasClaim(ClaimTypes.Role, Roles.Manager)));

        return builder;
    }
}