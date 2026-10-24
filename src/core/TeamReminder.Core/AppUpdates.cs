using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TeamReminder.Core;

public sealed record UpdateCandidate(Version Version, Uri DownloadUrl, string Sha256, string FileName);

public static partial class UpdatePolicy
{
    // A static release asset: no GitHub API rate limits and no dependency on the hosted API waking up.
    public static readonly Uri ManifestUrl = new("https://github.com/AndersonExner/Team-Reminder-Client/releases/latest/download/release.json");
    private const string DownloadPrefix = "/AndersonExner/Team-Reminder-Client/releases/download/";

    [GeneratedRegex(@"^v(\d+)\.(\d+)\.(\d+)$")]
    private static partial Regex Tag();
    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256();

    public static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));

    // Only a stable, newer installer from our own release downloads with a published checksum is accepted.
    public static UpdateCandidate? Select(string manifestJson, Version current)
    {
        try
        {
            using var document = JsonDocument.Parse(manifestJson);
            var root = document.RootElement;
            if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) return null;
            if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True) return null;
            var match = Tag().Match(root.GetProperty("tag_name").GetString() ?? "");
            if (!match.Success) return null;
            var version = new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
            if (version <= Normalize(current)) return null;
            var fileName = $"TeamReminder-Setup-{version}-x64.exe";
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != fileName) continue;
                var sha = asset.TryGetProperty("sha256", out var hash) ? hash.GetString()?.ToLowerInvariant() : null;
                if (sha is null || !Sha256().IsMatch(sha)) return null;
                if (!Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var url)
                    || url.Scheme != Uri.UriSchemeHttps || url.Host != "github.com"
                    || !url.AbsolutePath.StartsWith(DownloadPrefix, StringComparison.Ordinal)
                    || !url.AbsolutePath.EndsWith("/" + fileName, StringComparison.Ordinal)) return null;
                return new UpdateCandidate(version, url, sha, fileName);
            }
            return null;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            return null;
        }
    }

    // Restarting takes seconds; never do it right before an alarm or while one waits for delivery.
    public static bool IsQuietPeriod(IEnumerable<Alarm> alarms, DateTime nowUtc, TimeSpan window) =>
        !alarms.Any(alarm => alarm.DueAtUtc is { } due && due <= nowUtc + window);
}

public sealed class UpdateDownloader(HttpClient http)
{
    public const long MaxBytes = 300L * 1024 * 1024;

    public async Task<UpdateCandidate?> CheckAsync(Version current, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(UpdatePolicy.ManifestUrl, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return UpdatePolicy.Select(await response.Content.ReadAsStringAsync(ct), current);
    }

    // Returns the verified installer path; a partial or tampered file is never left under the final name.
    public async Task<string> DownloadAsync(UpdateCandidate candidate, string directory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, candidate.FileName);
        if (File.Exists(target) && await HashAsync(target, ct) == candidate.Sha256) return target;
        var partial = target + ".partial";
        try
        {
            using (var response = await http.GetAsync(candidate.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaxBytes) throw new InvalidDataException("Instalador maior que o permitido.");
                await using var source = await response.Content.ReadAsStreamAsync(ct);
                await using var file = File.Create(partial);
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    total += read;
                    if (total > MaxBytes) throw new InvalidDataException("Instalador maior que o permitido.");
                    await file.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }
            if (await HashAsync(partial, ct) != candidate.Sha256)
                throw new InvalidDataException("O instalador baixado não confere com o SHA-256 publicado.");
            File.Move(partial, target, overwrite: true);
            foreach (var old in Directory.EnumerateFiles(directory, "TeamReminder-Setup-*.exe").Where(x => x != target))
                try { File.Delete(old); } catch (IOException) { }
            return target;
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }

    private static async Task<string> HashAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
    }
}
