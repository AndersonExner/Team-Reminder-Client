using System.Text.Json;
using TeamReminder.Core;

namespace TeamReminder.Windows;

// Pending snoozes survive a restart (an update, a crash or signing out of Windows).
public sealed class SnoozeFileStore : ISnoozeStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeamReminder", "Settings", "snoozes.json");

    public async Task<IReadOnlyList<SnoozeEntry>> LoadAsync()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            await using var stream = File.OpenRead(FilePath);
            return await JsonSerializer.DeserializeAsync<SnoozeEntry[]>(stream) ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            ErrorLog.Write(ex);
            return [];
        }
    }

    public async Task SaveAsync(IReadOnlyList<SnoozeEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(entries));
        File.Move(temporary, FilePath, overwrite: true);
    }
}
