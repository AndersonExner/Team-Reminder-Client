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
                _ => "O servidor não conseguiu concluir a solicitação. Tente novamente."
            };
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
