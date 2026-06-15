using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Occ.Infrastructure.Authentication.Keycloak.Authentication;
using Occ.Infrastructure.Keycloak.Admin.Models.Requests;
using Occ.Infrastructure.Keycloak.Admin.Models.Responses;
using Occ.Infrastructure.Keycloak.Admin.Options;

namespace Occ.Infrastructure.Keycloak.Admin.Services;

public sealed class KeycloakAdminService(
    HttpClient httpClient,
    IOptionsMonitor<KeycloakOptions> keycloakOptions,
    IOptionsMonitor<KeycloakAdminOptions> adminOptions,
    ILogger<KeycloakAdminService> logger)
    : IKeycloakAdminService
{
    private KeycloakOptions _keycloak => keycloakOptions.CurrentValue;
    private KeycloakAdminOptions _admin => adminOptions.CurrentValue;

    // Admin token cache — re-used until 30 s before expiry
    private string? _adminToken;
    private DateTime _adminTokenExpiry = DateTime.MinValue;

    // Cached Keycloak internal UUID for the permissions client
    private string? _permissionsClientUuid;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    #region Authentication

    public async Task<TokenResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("Attempting login for user {Username}", request.Username);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _keycloak.ClientId,
            ["client_secret"] = _keycloak.ClientSecret,
            ["username"] = request.Username,
            ["password"] = request.Password,
            ["scope"] = "openid profile email"
        };

        var response = await httpClient.PostAsync(
            $"{_keycloak.Authority}/realms/{_keycloak.Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(form), ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Login failed for {Username}: {Error}", request.Username, error);
            return null;
        }

        return await ParseTokenResponse(response, ct);
    }

    public async Task<TokenResponse?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        logger.LogInformation("Refreshing access token");

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _keycloak.ClientId,
            ["client_secret"] = _keycloak.ClientSecret,
            ["refresh_token"] = refreshToken
        };

        var response = await httpClient.PostAsync(
            $"{_keycloak.Authority}/realms/{_keycloak.Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(form), ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Token refresh failed");
            return null;
        }

        return await ParseTokenResponse(response, ct);
    }

    public async Task<bool> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        logger.LogInformation("Logging out user session");

        var form = new Dictionary<string, string>
        {
            ["client_id"] = _keycloak.ClientId,
            ["client_secret"] = _keycloak.ClientSecret,
            ["refresh_token"] = refreshToken
        };

        var response = await httpClient.PostAsync(
            $"{_keycloak.Authority}/realms/{_keycloak.Realm}/protocol/openid-connect/logout",
            new FormUrlEncodedContent(form), ct);

        return response.IsSuccessStatusCode;
    }

    #endregion

    #region User Management

    public async Task<UserResponse?> GetUserByIdAsync(string userId, CancellationToken ct = default)
    {
        var token = await GetAdminTokenAsync(ct);
        if (token is null) return null;

        var userReq = AuthorizedGet($"{_admin.UsersUrl}/{userId}", token);
        var userResp = await httpClient.SendAsync(userReq, ct);

        if (!userResp.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to get user {UserId}", userId);
            return null;
        }

        var json = await userResp.Content.ReadFromJsonAsync<JsonElement>(ct);

        var rolesReq = AuthorizedGet($"{_admin.UsersUrl}/{userId}/role-mappings/realm", token);
        var rolesResp = await httpClient.SendAsync(rolesReq, ct);
        var roles = new List<string>();

        if (rolesResp.IsSuccessStatusCode)
        {
            var rolesJson = await rolesResp.Content.ReadFromJsonAsync<JsonElement>(ct);
            foreach (var role in rolesJson.EnumerateArray())
            {
                var name = role.GetProperty("name").GetString();
                if (name is not null && !name.StartsWith("default-roles"))
                    roles.Add(name);
            }
        }

        return new UserResponse(
            Id: json.GetProperty("id").GetString()!,
            Username: json.GetProperty("username").GetString()!,
            Email: json.GetProperty("email").GetString() ?? string.Empty,
            FirstName: json.GetProperty("firstName").GetString() ?? string.Empty,
            LastName: json.GetProperty("lastName").GetString() ?? string.Empty,
            Roles: roles);
    }

    public async Task<UserResponse?> RegisterUserAsync(RegisterUserRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("Registering user {Username}", request.Username);

        var token = await GetAdminTokenAsync(ct);
        if (token is null) return null;

        var payload = new
        {
            username = request.Username,
            email = request.Email,
            firstName = request.FirstName,
            lastName = request.LastName,
            enabled = true,
            emailVerified = true,
            credentials = new[]
            {
                new { type = "password", value = request.Password, temporary = false }
            }
        };

        var req = new HttpRequestMessage(HttpMethod.Post, _admin.UsersUrl)
        {
            Content = JsonContent(payload)
        };
        req.Headers.Authorization = Bearer(token);

        var resp = await httpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var error = await resp.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Registration failed for {Username}: {Error}", request.Username, error);
            return null;
        }

        var userId = resp.Headers.Location?.ToString().Split('/').Last();
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("Could not extract user ID from location header after registration");
            return null;
        }

        await AssignRoleAsync(userId, "user", ct);

        logger.LogInformation("User {Username} registered with ID {UserId}", request.Username, userId);

        return new UserResponse(userId, request.Username, request.Email,
            request.FirstName, request.LastName, ["user"]);
    }

    #endregion

    #region Role Management

    public async Task<bool> AssignRoleAsync(string userId, string roleName, CancellationToken ct = default)
    {
        logger.LogInformation("Assigning role {Role} to {UserId}", roleName, userId);

        var token = await GetAdminTokenAsync(ct);
        if (token is null) return false;

        var roleJson = await FetchRoleJsonAsync(roleName, token, ct);
        if (roleJson is null) return false;

        var req = new HttpRequestMessage(HttpMethod.Post, $"{_admin.UsersUrl}/{userId}/role-mappings/realm")
        {
            Content = new StringContent($"[{roleJson}]", Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = Bearer(token);

        var resp = await httpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to assign role {Role} to {UserId}: {Error}",
                roleName, userId, await resp.Content.ReadAsStringAsync(ct));
            return false;
        }

        return true;
    }

    public async Task<bool> RemoveRoleAsync(string userId, string roleName, CancellationToken ct = default)
    {
        logger.LogInformation("Removing role {Role} from {UserId}", roleName, userId);

        var token = await GetAdminTokenAsync(ct);
        if (token is null) return false;

        var roleJson = await FetchRoleJsonAsync(roleName, token, ct);
        if (roleJson is null) return false;

        var req = new HttpRequestMessage(HttpMethod.Delete, $"{_admin.UsersUrl}/{userId}/role-mappings/realm")
        {
            Content = new StringContent($"[{roleJson}]", Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = Bearer(token);

        var resp = await httpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to remove role {Role} from {UserId}: {Error}",
                roleName, userId, await resp.Content.ReadAsStringAsync(ct));
            return false;
        }

        return true;
    }

    #endregion

    #region Permission Management

    public Task<bool> AssignPermissionAsync(string userId, string permission, CancellationToken ct = default) =>
        AssignPermissionsAsync(userId, [permission], ct);

    public async Task<bool> AssignPermissionsAsync(
        string userId, IEnumerable<string> permissions, CancellationToken ct = default)
    {
        var list = permissions.ToList();
        logger.LogInformation("Assigning permissions [{Permissions}] to {UserId}",
            string.Join(", ", list), userId);

        var token = await GetAdminTokenAsync(ct);
        if (token is null) return false;

        var clientUuid = await GetPermissionsClientUuidAsync(token, ct);
        if (clientUuid is null) return false;

        var roles = await FetchClientRolesAsync(clientUuid, list, token, ct);
        if (roles.Count == 0) return false;

        var req = new HttpRequestMessage(HttpMethod.Post,
            $"{_admin.UsersUrl}/{userId}/role-mappings/clients/{clientUuid}")
        {
            Content = new StringContent(JsonSerializer.Serialize(roles), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = Bearer(token);

        var resp = await httpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to assign permissions to {UserId}: {Error}",
                userId, await resp.Content.ReadAsStringAsync(ct));
            return false;
        }

        logger.LogInformation("Assigned {Count} permissions to {UserId}", roles.Count, userId);
        return true;
    }

    public Task<bool> RemovePermissionAsync(string userId, string permission, CancellationToken ct = default) =>
        RemovePermissionsAsync(userId, [permission], ct);

    public async Task<bool> RemovePermissionsAsync(
        string userId, IEnumerable<string> permissions, CancellationToken ct = default)
    {
        var list = permissions.ToList();
        logger.LogInformation("Removing permissions [{Permissions}] from {UserId}",
            string.Join(", ", list), userId);

        var token = await GetAdminTokenAsync(ct);
        if (token is null) return false;

        var clientUuid = await GetPermissionsClientUuidAsync(token, ct);
        if (clientUuid is null) return false;

        var roles = await FetchClientRolesAsync(clientUuid, list, token, ct);
        if (roles.Count == 0) return false;

        var req = new HttpRequestMessage(HttpMethod.Delete,
            $"{_admin.UsersUrl}/{userId}/role-mappings/clients/{clientUuid}")
        {
            Content = new StringContent(JsonSerializer.Serialize(roles), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = Bearer(token);

        var resp = await httpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to remove permissions from {UserId}: {Error}",
                userId, await resp.Content.ReadAsStringAsync(ct));
            return false;
        }

        logger.LogInformation("Removed {Count} permissions from {UserId}", roles.Count, userId);
        return true;
    }

    public async Task<IEnumerable<string>> GetUserPermissionsAsync(string userId, CancellationToken ct = default)
    {
        var token = await GetAdminTokenAsync(ct);
        if (token is null) return [];

        var clientUuid = await GetPermissionsClientUuidAsync(token, ct);
        if (clientUuid is null) return [];

        var req = AuthorizedGet($"{_admin.UsersUrl}/{userId}/role-mappings/clients/{clientUuid}", token);
        var resp = await httpClient.SendAsync(req, ct);

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to get permissions for {UserId}", userId);
            return [];
        }

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        return json.EnumerateArray()
            .Select(r => r.GetProperty("name").GetString())
            .Where(n => n is not null)
            .Cast<string>()
            .ToList();
    }

    #endregion

    #region Group Management

    public async Task<bool> AssignToGroupAsync(string userId, string groupName, CancellationToken ct = default)
    {
        logger.LogInformation("Assigning {UserId} to group {Group}", userId, groupName);

        var token = await GetAdminTokenAsync(ct);
        if (token is null) return false;

        var groupId = await GetGroupIdByNameAsync(groupName, token, ct);
        if (groupId is null)
        {
            logger.LogWarning("Group {Group} not found", groupName);
            return false;
        }

        var req = new HttpRequestMessage(HttpMethod.Put, $"{_admin.UsersUrl}/{userId}/groups/{groupId}");
        req.Headers.Authorization = Bearer(token);

        var resp = await httpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to assign {UserId} to group {Group}: {Error}",
                userId, groupName, await resp.Content.ReadAsStringAsync(ct));
            return false;
        }

        return true;
    }

    public async Task<bool> RemoveFromGroupAsync(string userId, string groupName, CancellationToken ct = default)
    {
        logger.LogInformation("Removing {UserId} from group {Group}", userId, groupName);

        var token = await GetAdminTokenAsync(ct);
        if (token is null) return false;

        var groupId = await GetGroupIdByNameAsync(groupName, token, ct);
        if (groupId is null)
        {
            logger.LogWarning("Group {Group} not found", groupName);
            return false;
        }

        var req = new HttpRequestMessage(HttpMethod.Delete, $"{_admin.UsersUrl}/{userId}/groups/{groupId}");
        req.Headers.Authorization = Bearer(token);

        var resp = await httpClient.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to remove {UserId} from group {Group}: {Error}",
                userId, groupName, await resp.Content.ReadAsStringAsync(ct));
            return false;
        }

        return true;
    }

    public async Task<IEnumerable<GroupResponse>> GetUserGroupsAsync(string userId, CancellationToken ct = default)
    {
        var token = await GetAdminTokenAsync(ct);
        if (token is null) return [];

        var req = AuthorizedGet($"{_admin.UsersUrl}/{userId}/groups", token);
        var resp = await httpClient.SendAsync(req, ct);

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to get groups for {UserId}", userId);
            return [];
        }

        return await ParseGroupsAsync(resp, ct);
    }

    public async Task<IEnumerable<GroupResponse>> GetAllGroupsAsync(CancellationToken ct = default)
    {
        var token = await GetAdminTokenAsync(ct);
        if (token is null) return [];

        var req = AuthorizedGet(_admin.GroupsUrl, token);
        var resp = await httpClient.SendAsync(req, ct);

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to get all groups");
            return [];
        }

        return await ParseGroupsAsync(resp, ct);
    }

    #endregion

    #region Private Helpers

    private async Task<string?> GetAdminTokenAsync(CancellationToken ct)
    {
        if (_adminToken is not null && DateTime.UtcNow < _adminTokenExpiry)
            return _adminToken;

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _admin.ClientId,
            ["username"] = _admin.AdminUsername,
            ["password"] = _admin.AdminPassword
        };

        var resp = await httpClient.PostAsync(_admin.TokenUrl, new FormUrlEncodedContent(form), ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogError("Failed to obtain Keycloak admin token");
            return null;
        }

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        _adminToken = json.GetProperty("access_token").GetString();
        var expiresIn = json.GetProperty("expires_in").GetInt32();
        _adminTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 30);

        return _adminToken;
    }

    private async Task<string?> GetPermissionsClientUuidAsync(string adminToken, CancellationToken ct)
    {
        if (_permissionsClientUuid is not null)
            return _permissionsClientUuid;

        var req = AuthorizedGet($"{_admin.ClientsUrl}?clientId={_admin.PermissionsClientId}", adminToken);
        var resp = await httpClient.SendAsync(req, ct);

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogError("Failed to find client {ClientId}", _admin.PermissionsClientId);
            return null;
        }

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        var clients = json.EnumerateArray().ToList();

        if (clients.Count == 0)
        {
            logger.LogError("Client {ClientId} not found in realm", _admin.PermissionsClientId);
            return null;
        }

        _permissionsClientUuid = clients[0].GetProperty("id").GetString();
        return _permissionsClientUuid;
    }

    private async Task<string?> GetGroupIdByNameAsync(string groupName, string adminToken, CancellationToken ct)
    {
        var req = AuthorizedGet(
            $"{_admin.GroupsUrl}?search={Uri.EscapeDataString(groupName)}", adminToken);
        var resp = await httpClient.SendAsync(req, ct);

        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        foreach (var group in json.EnumerateArray())
        {
            var name = group.GetProperty("name").GetString();
            if (string.Equals(name, groupName, StringComparison.OrdinalIgnoreCase))
                return group.GetProperty("id").GetString();
        }

        return null;
    }

    private async Task<string?> FetchRoleJsonAsync(string roleName, string adminToken, CancellationToken ct)
    {
        var req = AuthorizedGet($"{_admin.RolesUrl}/{roleName}", adminToken);
        var resp = await httpClient.SendAsync(req, ct);

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Role {Role} not found in realm", roleName);
            return null;
        }

        return await resp.Content.ReadAsStringAsync(ct);
    }

    private async Task<List<object>> FetchClientRolesAsync(
        string clientUuid, IEnumerable<string> permissions, string adminToken, CancellationToken ct)
    {
        var result = new List<object>();

        foreach (var permission in permissions)
        {
            var req = AuthorizedGet($"{_admin.ClientsUrl}/{clientUuid}/roles/{permission}", adminToken);
            var resp = await httpClient.SendAsync(req, ct);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Permission (client role) {Permission} not found", permission);
                continue;
            }

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            result.Add(new
            {
                id = json.GetProperty("id").GetString(),
                name = json.GetProperty("name").GetString()
            });
        }

        return result;
    }

    private static async Task<List<GroupResponse>> ParseGroupsAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        return json.EnumerateArray()
            .Select(g => new GroupResponse(
                Id: g.GetProperty("id").GetString()!,
                Name: g.GetProperty("name").GetString()!,
                Path: g.GetProperty("path").GetString()!))
            .ToList();
    }

    private static async Task<TokenResponse> ParseTokenResponse(HttpResponseMessage resp, CancellationToken ct)
    {
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        return new TokenResponse(
            AccessToken: json.GetProperty("access_token").GetString()!,
            RefreshToken: json.GetProperty("refresh_token").GetString()!,
            ExpiresIn: json.GetProperty("expires_in").GetInt32(),
            TokenType: json.GetProperty("token_type").GetString()!);
    }

    private static HttpRequestMessage AuthorizedGet(string url, string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = Bearer(token);
        return req;
    }

    private static AuthenticationHeaderValue Bearer(string token) => new("Bearer", token);

    private static StringContent JsonContent(object value) =>
        new(JsonSerializer.Serialize(value, JsonOpts), Encoding.UTF8, "application/json");

    #endregion
}