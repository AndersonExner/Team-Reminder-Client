using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TeamReminder.Windows;

namespace TeamReminder.Core.Tests;

public sealed class AuthClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("https://user:secret@example.com")]
    public void RejectsUnsafeServerAddress(string address) => Assert.Throws<ArgumentException>(() => AuthClient.ServerAddress(address));

    [Fact]
    public void AllowsLocalDevelopmentAndHttps()
    {
        Assert.Equal("http://localhost:5080/", AuthClient.ServerAddress(null).AbsoluteUri);
        Assert.Equal("https://example.com/", AuthClient.ServerAddress("https://example.com").AbsoluteUri);
    }

    [Fact]
    public async Task RegisterSendsExpectedContractAndReadsSession()
    {
        var expected = new AccountSession("access", "refresh", DateTimeOffset.UtcNow.AddMinutes(15),
            DateTimeOffset.UtcNow.AddDays(30), new AccountUser(Guid.NewGuid(), "User", "user@example.com"));
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("/api/auth/register", request.RequestUri!.AbsolutePath);
            Assert.Equal(HttpMethod.Post, request.Method);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("User", body.RootElement.GetProperty("name").GetString());
            Assert.Equal("user@example.com", body.RootElement.GetProperty("email").GetString());
            Assert.Equal("a long password", body.RootElement.GetProperty("password").GetString());
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(expected) };
        })) { BaseAddress = AuthClient.ServerAddress(null) };
        var actual = await new AuthClient(http).RegisterAsync("User", "user@example.com", "a long password");
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(401, true)]
    [InlineData(409, false)]
    [InlineData(429, false)]
    [InlineData(503, false)]
    public async Task ErrorsRemainUnderstandable(int status, bool unauthorized)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))))
            { BaseAddress = AuthClient.ServerAddress(null) };
        var error = await Assert.ThrowsAsync<AccountApiException>(() => new AuthClient(http).LoginAsync("user@example.com", "secret"));
        Assert.Equal(unauthorized, error.Unauthorized);
        Assert.NotEmpty(error.Message);
    }

    [Fact]
    public async Task LogoutUsesBearerAndAcceptsNoContent()
    {
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("access-token", request.Headers.Authorization.Parameter);
            Assert.Equal("/api/auth/logout", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        })) { BaseAddress = AuthClient.ServerAddress(null) };
        await new AuthClient(http).LogoutAsync("access-token");
    }

    [Fact]
    public async Task ValidationErrorsAreDisplayed()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { errors = new { password = new[] { "Use uma senha de 10 a 128 caracteres." } } })
        }))) { BaseAddress = AuthClient.ServerAddress(null) };
        var error = await Assert.ThrowsAsync<AccountApiException>(() => new AuthClient(http).RegisterAsync("User", "user@example.com", "short"));
        Assert.Contains("10 a 128", error.Message);
    }
}
