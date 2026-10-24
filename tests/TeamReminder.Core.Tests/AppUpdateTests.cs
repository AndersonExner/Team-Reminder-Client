using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using TeamReminder.Core;

namespace TeamReminder.Core.Tests;

public class AppUpdateTests
{
    private static readonly byte[] Installer = "installer bytes"u8.ToArray();
    private static readonly string InstallerSha = Convert.ToHexString(SHA256.HashData(Installer)).ToLowerInvariant();
    private const string Prefix = "https://github.com/AndersonExner/Team-Reminder-Client/releases/download/";

    private static string Manifest(string tag = "v0.5.0", string? url = null, string? sha = null, bool prerelease = false, bool draft = false)
    {
        var version = tag.TrimStart('v');
        return JsonSerializer.Serialize(new
        {
            tag_name = tag, draft, prerelease,
            assets = new[] { new {
                name = $"TeamReminder-Setup-{version}-x64.exe",
                browser_download_url = url ?? $"{Prefix}{tag}/TeamReminder-Setup-{version}-x64.exe",
                sha256 = sha ?? InstallerSha } }
        });
    }

    [Fact]
    public void SelectsOnlyNewerStableReleaseWithChecksumFromOurDownloads()
    {
        var candidate = UpdatePolicy.Select(Manifest(), new Version(0, 4, 0, 0));
        Assert.Equal(new Version(0, 5, 0), candidate!.Version);
        Assert.Equal(InstallerSha, candidate.Sha256);
        Assert.Null(UpdatePolicy.Select(Manifest("v0.4.0"), new Version(0, 4, 0, 0)));
        Assert.Null(UpdatePolicy.Select(Manifest("v0.3.9"), new Version(0, 4, 0)));
        Assert.Null(UpdatePolicy.Select(Manifest(prerelease: true), new Version(0, 4, 0)));
        Assert.Null(UpdatePolicy.Select(Manifest(draft: true), new Version(0, 4, 0)));
        Assert.Null(UpdatePolicy.Select(Manifest(url: "https://example.com/TeamReminder-Setup-0.5.0-x64.exe"), new Version(0, 4, 0)));
        Assert.Null(UpdatePolicy.Select(Manifest(url: "https://github.com/someone/else/releases/download/v0.5.0/TeamReminder-Setup-0.5.0-x64.exe"), new Version(0, 4, 0)));
        Assert.Null(UpdatePolicy.Select(Manifest(sha: "not-a-hash"), new Version(0, 4, 0)));
        Assert.Null(UpdatePolicy.Select(Manifest("v0.5.0-beta"), new Version(0, 4, 0)));
        Assert.Null(UpdatePolicy.Select("{ not json", new Version(0, 4, 0)));
    }

    [Fact]
    public void QuietPeriodExcludesDueAndUpcomingAlarms()
    {
        var now = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        Alarm At(DateTime? due) => new(Guid.NewGuid(), "A", now.AddDays(-1), now.AddDays(-2), "UTC") { NextOccurrenceUtc = due, LastDeliveredOccurrenceUtc = now.AddDays(-1) };
        var window = TimeSpan.FromMinutes(10);
        Assert.True(UpdatePolicy.IsQuietPeriod([At(now.AddMinutes(30)), At(null)], now, window));
        Assert.False(UpdatePolicy.IsQuietPeriod([At(now.AddMinutes(5))], now, window));
        Assert.False(UpdatePolicy.IsQuietPeriod([At(now.AddMinutes(-1))], now, window));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return Task.FromResult(respond(request)); }
    }

    private static string TempDirectory() => Path.Combine(Path.GetTempPath(), "tr-update-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DownloadVerifiesChecksumAndReusesVerifiedFile()
    {
        var directory = TempDirectory();
        try
        {
            var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Installer) });
            var downloader = new UpdateDownloader(new HttpClient(handler));
            var candidate = UpdatePolicy.Select(Manifest(), new Version(0, 4, 0))!;
            var path = await downloader.DownloadAsync(candidate, directory);
            Assert.Equal(Installer, await File.ReadAllBytesAsync(path));
            await downloader.DownloadAsync(candidate, directory);
            Assert.Equal(1, handler.Calls);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task TamperedDownloadIsRejectedAndRemoved()
    {
        var directory = TempDirectory();
        try
        {
            var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("tampered"u8.ToArray()) });
            var candidate = UpdatePolicy.Select(Manifest(), new Version(0, 4, 0))!;
            await Assert.ThrowsAsync<InvalidDataException>(() => new UpdateDownloader(new HttpClient(handler)).DownloadAsync(candidate, directory));
            Assert.Empty(Directory.EnumerateFiles(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task MissingManifestMeansNoUpdate()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        Assert.Null(await new UpdateDownloader(new HttpClient(handler)).CheckAsync(new Version(0, 4, 0)));
    }
}
