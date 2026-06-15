namespace Occ.Infrastructure.Authentication.Keycloak.Authentication;

public static class OccClaimTypes
{
    /// <summary>
    /// Fine-grained permission claim extracted from resource_access in the Keycloak token.
    /// Examples: "limit:read", "fee:create".
    /// </summary>
    public const string Permission = "permission";
    
}