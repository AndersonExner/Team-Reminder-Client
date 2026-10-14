using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TeamReminder.Core;

namespace TeamReminder.Windows;

public sealed record AccountUser(Guid Id, string Name, string Email);
public sealed record AccountSession(string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresAt,
    DateTimeOffset RefreshExpiresAt, AccountUser User);

public sealed class AccountApiException(string message, bool unauthorized = false) : Exception(message)
{
    public bool Unauthorized { get; } = unauthorized;
}

public sealed class AuthClient(HttpClient http)
{
    public static Uri ServerAddress(string? configured)
    {
        var uri = new Uri(string.IsNullOrWhiteSpace(configured) ? "http://localhost:5080/" : configured.Trim().TrimEnd('/') + "/");
        if (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
            throw new ArgumentException("O servidor deve usar HTTPS; HTTP é permitido apenas no computador local.");
        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("O endereço do servidor é inválido.");
        return uri;
    }

    public Task<AccountSession> RegisterAsync(string name, string email, string password) =>
        SendAsync<AccountSession>(HttpMethod.Post, "api/auth/register", new { name, email, password });
    public Task<AccountSession> LoginAsync(string email, string password) =>
        SendAsync<AccountSession>(HttpMethod.Post, "api/auth/login", new { email, password });
    public Task<AccountSession> RefreshAsync(string refreshToken) =>
        SendAsync<AccountSession>(HttpMethod.Post, "api/auth/refresh", new { refreshToken });
    public Task<AccountUser> MeAsync(string accessToken) =>
        SendAsync<AccountUser>(HttpMethod.Get, "api/auth/me", token: accessToken);
    public async Task LogoutAsync(string accessToken) =>
        await SendAsync<object>(HttpMethod.Post, "api/auth/logout", token: accessToken);

    public Task<AlarmChangeSet> PullAlarmsAsync(long cursor, string accessToken) =>
        SendAsync<AlarmChangeSet>(HttpMethod.Get, $"api/sync?since={cursor}", token: accessToken);
    public Task<PersonalAlarmChange> PushAlarmAsync(PendingAlarmWrite write, string accessToken) =>
        SendAsync<PersonalAlarmChange>(HttpMethod.Put, $"api/alarms/{write.Id}", write.Request, accessToken);

    public Task<GroupSummary[]> GroupsAsync(string token) => SendAsync<GroupSummary[]>(HttpMethod.Get, "api/groups", token: token);
    public Task<GroupDetails> GroupAsync(Guid id, string token) => SendAsync<GroupDetails>(HttpMethod.Get, $"api/groups/{id}", token: token);
    public Task<GroupInvitationResponse[]> InvitationsAsync(string token) => SendAsync<GroupInvitationResponse[]>(HttpMethod.Get, "api/invitations", token: token);
    public Task<GroupAuditResponse[]> GroupAuditAsync(Guid id, string token) => SendAsync<GroupAuditResponse[]>(HttpMethod.Get, $"api/groups/{id}/audit", token: token);
    public Task<GroupSummary> CreateGroupAsync(GroupCreateRequest request, string token) => SendAsync<GroupSummary>(HttpMethod.Post, "api/groups", request, token);
    public Task<object> EditGroupAsync(Guid id, GroupEditRequest request, string token) => SendAsync<object>(HttpMethod.Patch, $"api/groups/{id}", request, token);
    public Task<object> DeleteGroupAsync(Guid id, long revision, string token) => SendAsync<object>(HttpMethod.Delete, $"api/groups/{id}?revision={revision}", token: token);
    public Task<object> LeaveGroupAsync(Guid id, long revision, string token) => SendAsync<object>(HttpMethod.Post, $"api/groups/{id}/leave", new GroupRevisionRequest(revision), token);
    public Task<object> TransferGroupAsync(Guid id, GroupTransferRequest request, string token) => SendAsync<object>(HttpMethod.Post, $"api/groups/{id}/transfer", request, token);
    public Task<object> ChangeGroupRoleAsync(Guid id, Guid accountId, GroupRoleRequest request, string token) => SendAsync<object>(HttpMethod.Patch, $"api/groups/{id}/members/{accountId}", request, token);
    public Task<object> RemoveGroupMemberAsync(Guid id, Guid accountId, long revision, string token) => SendAsync<object>(HttpMethod.Delete, $"api/groups/{id}/members/{accountId}?revision={revision}", token: token);
    public Task<GroupInvitationResponse> InviteToGroupAsync(Guid id, GroupInviteRequest request, string token) => SendAsync<GroupInvitationResponse>(HttpMethod.Post, $"api/groups/{id}/invitations", request, token);
    public Task<object> CancelGroupInvitationAsync(Guid id, Guid inviteId, long revision, string token) => SendAsync<object>(HttpMethod.Post, $"api/groups/{id}/invitations/{inviteId}/cancel", new GroupRevisionRequest(revision), token);
    public Task<object> AcceptGroupInvitationAsync(Guid id, string token) => SendAsync<object>(HttpMethod.Post, $"api/invitations/{id}/accept", token: token);
    public Task<object> DeclineGroupInvitationAsync(Guid id, string token) => SendAsync<object>(HttpMethod.Post, $"api/invitations/{id}/decline", token: token);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null, string? token = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Conflict && path.StartsWith("api/alarms/", StringComparison.Ordinal))
            {
                using var conflict = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (conflict.RootElement.TryGetProperty("current", out var current) && current.ValueKind != JsonValueKind.Null)
                    throw new AlarmSyncConflictException(current.Deserialize<PersonalAlarmChange>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
                throw new AccountApiException("O alarme não está mais disponível. Tente sincronizar novamente.");
            }
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "E-mail ou senha inválidos, ou sessão expirada. Entre novamente.",
                HttpStatusCode.Conflict => "Já existe uma conta com este e-mail.",
                HttpStatusCode.TooManyRequests => "Muitas tentativas. Aguarde um minuto e tente novamente.",
                HttpStatusCode.BadRequest => "Confira os dados informados.",
                HttpStatusCode.Forbidden => "Você não tem permissão para esta ação.",
                HttpStatusCode.NotFound => "O grupo ou convite não está mais disponível. Atualize a lista.",
                _ => "O servidor não conseguiu concluir a solicitação. Tente novamente."
            };
            if (path.StartsWith("api/groups", StringComparison.Ordinal) || path.StartsWith("api/invitations", StringComparison.Ordinal))
            {
                try
                {
                    using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (problem.RootElement.TryGetProperty("message", out var text) && text.ValueKind == JsonValueKind.String)
                        message = text.GetString()!;
                }
                catch (JsonException) { }
            }
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                try
                {
                    using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (problem.RootElement.TryGetProperty("errors", out var errors))
                        message = string.Join(" ", errors.EnumerateObject().SelectMany(x => x.Value.EnumerateArray()).Select(x => x.GetString()));
                }
                catch (JsonException) { }
            }
            throw new AccountApiException(message, response.StatusCode == HttpStatusCode.Unauthorized);
        }
        if (response.StatusCode == HttpStatusCode.NoContent) return default!;
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new AccountApiException("O servidor retornou uma resposta inválida.");
    }
}
