namespace Occ.Infrastructure.Keycloak.Admin.Models.Responses;

public record UserResponse(
    string Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    IEnumerable<string> Roles);