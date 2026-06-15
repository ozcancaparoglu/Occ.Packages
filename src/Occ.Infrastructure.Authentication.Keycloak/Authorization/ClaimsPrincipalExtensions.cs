using System.Security.Claims;
using Occ.Infrastructure.Authentication.Keycloak.Authentication;

namespace Occ.Infrastructure.Authentication.Keycloak.Authorization;

public static class ClaimsPrincipalExtensions
{
    public static bool HasPermission(this ClaimsPrincipal user, string permission) =>
        user.HasClaim(OccClaimTypes.Permission, permission);

    public static bool HasAnyPermission(this ClaimsPrincipal user, params string[] permissions)
    {
        foreach (var permission in permissions)
            if (user.HasClaim(OccClaimTypes.Permission, permission))
                return true;
        return false;
    }

    public static bool HasAllPermissions(this ClaimsPrincipal user, params string[] permissions)
    {
        foreach (var permission in permissions)
            if (!user.HasClaim(OccClaimTypes.Permission, permission))
                return false;
        return true;
    }

    public static IEnumerable<string> GetPermissions(this ClaimsPrincipal user) =>
        user.FindAll(OccClaimTypes.Permission).Select(c => c.Value);
}