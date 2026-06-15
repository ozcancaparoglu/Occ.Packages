namespace Occ.Infrastructure.Keycloak.Admin.Models.Requests;

public record RegisterUserRequest(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName);