namespace Occ.Infrastructure.Keycloak.Admin.Models.Requests;

public record AssignPermissionRequest(string UserId, string Permission);