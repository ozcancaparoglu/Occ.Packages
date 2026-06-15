namespace Occ.Infrastructure.Keycloak.Admin.Options;

public sealed class KeycloakAdminOptions
{
    public const string SectionName = "KeycloakAdmin";

    public string BaseUrl { get; set; } = string.Empty;
    public string Realm { get; set; } = string.Empty;

    /// <summary>Client ID used for admin operations — typically "admin-cli".</summary>
    public string ClientId { get; set; } = string.Empty;

    public string AdminUsername { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;

    /// <summary>
    /// The client whose roles are treated as permissions (e.g., "project-api").
    /// Used for assigning/removing fine-grained client roles.
    /// </summary>
    public string PermissionsClientId { get; set; } = "project-api";

    // Computed URLs
    public string TokenUrl => $"{BaseUrl}/realms/master/protocol/openid-connect/token";
    public string UsersUrl => $"{BaseUrl}/admin/realms/{Realm}/users";
    public string RolesUrl => $"{BaseUrl}/admin/realms/{Realm}/roles";
    public string ClientsUrl => $"{BaseUrl}/admin/realms/{Realm}/clients";
    public string GroupsUrl => $"{BaseUrl}/admin/realms/{Realm}/groups";
}