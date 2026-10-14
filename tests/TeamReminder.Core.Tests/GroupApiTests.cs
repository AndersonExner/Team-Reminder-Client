using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TeamReminder.Windows;

namespace TeamReminder.Core.Tests;

public sealed class GroupApiTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
    [Fact]
    public async Task CreateGroupUsesAuthenticatedContractAndReadsOwnerRole()
    {
        var group = new GroupSummary(Guid.NewGuid(), "Desenvolvimento", "Equipe", Guid.NewGuid(), GroupRole.Owner, 1, 1);
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method); Assert.Equal("/api/groups", request.RequestUri!.AbsolutePath);
            Assert.Equal("access", request.Headers.Authorization!.Parameter);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("Desenvolvimento", body.RootElement.GetProperty("name").GetString());
            return new(HttpStatusCode.Created) { Content = JsonContent.Create(group) };
        })) { BaseAddress = AuthClient.ServerAddress(null) };
        var result = await new AuthClient(http).CreateGroupAsync(new("Desenvolvimento", "Equipe"), "access");
        Assert.Equal(group, result); Assert.Equal("Proprietário", result.RoleLabel);
    }
    [Fact]
    public async Task RoleChangesIncludeExpectedRevisionAndAccountId()
    {
        var id = Guid.NewGuid(); var member = Guid.NewGuid();
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal(HttpMethod.Patch, request.Method); Assert.Equal($"/api/groups/{id}/members/{member}", request.RequestUri!.AbsolutePath);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(1, body.RootElement.GetProperty("role").GetInt32()); Assert.Equal(7, body.RootElement.GetProperty("expectedRevision").GetInt64());
            return new(HttpStatusCode.NoContent);
        })) { BaseAddress = AuthClient.ServerAddress(null) };
        await new AuthClient(http).ChangeGroupRoleAsync(id, member, new(GroupRole.Administrator, 7), "access");
    }
    [Theory]
    [InlineData(409, "O grupo mudou. Atualize a lista.")]
    [InlineData(410, "O convite expirou.")]
    [InlineData(422, "O grupo atingiu 100 participantes.")]
    public async Task GroupBusinessErrorsAreDisplayed(int status, string message)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
            { Content = JsonContent.Create(new { message }) }))) { BaseAddress = AuthClient.ServerAddress(null) };
        var error = await Assert.ThrowsAsync<AccountApiException>(() => new AuthClient(http).AcceptGroupInvitationAsync(Guid.NewGuid(), "access"));
        Assert.Equal(message, error.Message);
    }
    [Fact]
    public async Task AcceptInvitationSendsBearerAndReadsNoContent()
    {
        var id = Guid.NewGuid();
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal($"/api/invitations/{id}/accept", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        })) { BaseAddress = AuthClient.ServerAddress(null) };
        await new AuthClient(http).AcceptGroupInvitationAsync(id, "access");
    }
}
