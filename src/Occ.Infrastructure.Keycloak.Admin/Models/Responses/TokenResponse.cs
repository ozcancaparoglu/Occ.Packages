namespace Occ.Infrastructure.Keycloak.Admin.Models.Responses;

public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType);