namespace Occ.Infrastructure.Keycloak.Admin.Models.Requests;

public record AssignPermissionsRequest(string UserId, IEnumerable<string> Permissions);