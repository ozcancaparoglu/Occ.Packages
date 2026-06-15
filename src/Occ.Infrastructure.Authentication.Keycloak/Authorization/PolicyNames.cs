namespace Occ.Infrastructure.Authentication.Keycloak.Authorization;

public static class PolicyNames
{
    public const string RequireAdminRole = nameof(RequireAdminRole);
    public const string RequireUserRole = nameof(RequireUserRole);
    public const string RequireManagerRole = nameof(RequireManagerRole);
    public const string RequireAdminOrManager = nameof(RequireAdminOrManager);
    public const string RequireAuthenticatedUser = nameof(RequireAuthenticatedUser);
}