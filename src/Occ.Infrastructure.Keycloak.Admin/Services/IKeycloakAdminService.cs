using Occ.Infrastructure.Keycloak.Admin.Models.Requests;
using Occ.Infrastructure.Keycloak.Admin.Models.Responses;

namespace Occ.Infrastructure.Keycloak.Admin.Services;

public interface IKeycloakAdminService
{
    #region Authentication

    Task<TokenResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<TokenResponse?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<bool> LogoutAsync(string refreshToken, CancellationToken ct = default);

    #endregion

    #region User Management

    Task<UserResponse?> RegisterUserAsync(RegisterUserRequest request, CancellationToken ct = default);
    Task<UserResponse?> GetUserByIdAsync(string userId, CancellationToken ct = default);

    #endregion

    #region Role Management

    Task<bool> AssignRoleAsync(string userId, string roleName, CancellationToken ct = default);
    Task<bool> RemoveRoleAsync(string userId, string roleName, CancellationToken ct = default);

    #endregion

    #region Permission Management (Client Roles)

    Task<bool> AssignPermissionAsync(string userId, string permission, CancellationToken ct = default);
    Task<bool> AssignPermissionsAsync(string userId, IEnumerable<string> permissions, CancellationToken ct = default);
    Task<bool> RemovePermissionAsync(string userId, string permission, CancellationToken ct = default);
    Task<bool> RemovePermissionsAsync(string userId, IEnumerable<string> permissions, CancellationToken ct = default);
    Task<IEnumerable<string>> GetUserPermissionsAsync(string userId, CancellationToken ct = default);

    #endregion

    #region Group Management

    Task<bool> AssignToGroupAsync(string userId, string groupName, CancellationToken ct = default);
    Task<bool> RemoveFromGroupAsync(string userId, string groupName, CancellationToken ct = default);
    Task<IEnumerable<GroupResponse>> GetUserGroupsAsync(string userId, CancellationToken ct = default);
    Task<IEnumerable<GroupResponse>> GetAllGroupsAsync(CancellationToken ct = default);

    #endregion
}