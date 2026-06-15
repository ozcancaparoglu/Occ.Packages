namespace Occ.Infrastructure.Authentication.Keycloak.Authentication;

public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    public string Authority { get; set; } = string.Empty;
    public string Realm { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public bool RequireHttpsMetadata { get; set; } = true;

    public string Issuer => $"{Authority}/realms/{Realm}";
    public string MetadataUrl => $"{Issuer}/.well-known/openid-configuration";
}