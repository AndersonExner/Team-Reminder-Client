using System.Text.Json;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;

namespace TeamReminder.Windows;

public sealed class AccountSessionStore(string serverAddress)
{
    private sealed record StoredSession(string ServerAddress, AccountSession Session);
    private static string StoragePath
    {
        get
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeamReminder");
#if DEBUG
            var testData = Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_DATA_FILE");
            if (!string.IsNullOrWhiteSpace(testData)) root = Path.GetDirectoryName(Path.GetFullPath(testData))!;
#endif
            return Path.Combine(root, "Account", "session.bin");
        }
    }

    public async Task<AccountSession?> LoadAsync()
    {
        if (!File.Exists(StoragePath)) return null;
        try
        {
            var encrypted = CryptographicBuffer.CreateFromByteArray(await File.ReadAllBytesAsync(StoragePath));
            var plain = await new DataProtectionProvider().UnprotectAsync(encrypted);
            CryptographicBuffer.CopyToByteArray(plain, out var bytes);
            var stored = JsonSerializer.Deserialize<StoredSession>(bytes);
            if (stored?.ServerAddress != serverAddress) return null;
            return stored.Session;
        }
        catch (Exception ex) when (ex is JsonException or System.Runtime.InteropServices.COMException)
        {
            Clear();
            return null;
        }
    }

    public async Task SaveAsync(AccountSession session)
    {
        var plain = CryptographicBuffer.CreateFromByteArray(JsonSerializer.SerializeToUtf8Bytes(new StoredSession(serverAddress, session)));
        var encrypted = await new DataProtectionProvider("LOCAL=user").ProtectAsync(plain);
        CryptographicBuffer.CopyToByteArray(encrypted, out var bytes);
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
        var temporary = StoragePath + ".tmp";
        await File.WriteAllBytesAsync(temporary, bytes);
        File.Move(temporary, StoragePath, overwrite: true);
    }

    public void Clear() => File.Delete(StoragePath);
}
