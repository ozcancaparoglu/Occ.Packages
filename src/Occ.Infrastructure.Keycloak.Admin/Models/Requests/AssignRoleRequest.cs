namespace Occ.Infrastructure.Keycloak.Admin.Models.Requests;

public record AssignRoleRequest(string UserId, string RoleName);