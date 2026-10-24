using System.Text.Json;
using TeamReminder.Core;

namespace TeamReminder.Local;

public sealed class LocalSoundLibrary(string localRoot)
{
    private readonly object fileGate = new();
    private readonly HashSet<Guid> pendingImports = [];
    public const long MaxBytes = 20 * 1024 * 1024;
    private readonly string soundsPath = Path.Combine(Path.GetFullPath(localRoot), "Sounds");
    private readonly string settingsPath = Path.Combine(Path.GetFullPath(localRoot), "Settings", "default-sound.json");
    public AlarmSound DefaultSound { get; private set; } = AlarmSound.InitialDefault;

    public async Task LoadDefaultAsync()
    {
        try
        {
            if (!File.Exists(settingsPath)) return;
            var saved = JsonSerializer.Deserialize<AlarmSound>(await File.ReadAllTextAsync(settingsPath));
            if (saved is { IsValid: true } && saved.Kind != AlarmSoundKind.Default) DefaultSound = saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }

    public async Task SaveDefaultAsync(AlarmSound sound)
    {
        sound.Validate();
        if (sound.Kind == AlarmSoundKind.Default) throw new ArgumentException("O padrão não pode referenciar a si próprio.");
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var temp = settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(sound));
            File.Move(temp, settingsPath, overwrite: true);
            DefaultSound = sound;
        }
        finally { TryDelete(temp); }
    }

    public string? ResolvePath(AlarmSound sound)
    {
        if (sound.Kind != AlarmSoundKind.Custom || sound.FileId is not { } id || id == Guid.Empty) return null;
        foreach (var extension in new[] { ".wav", ".mp3" })
        {
            var path = Path.Combine(soundsPath, id.ToString("N") + extension);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    public async Task<AlarmSound> ImportAsync(string sourcePath, Func<string, Task> validateAudio)
    {
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not (".wav" or ".mp3")) throw new InvalidDataException("Escolha um arquivo MP3 ou WAV.");
        Directory.CreateDirectory(soundsPath);
        var id = Guid.NewGuid();
        lock (fileGate) pendingImports.Add(id);
        var path = Path.Combine(soundsPath, id.ToString("N") + extension);
        try
        {
            await using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            {
                if (source.Length is <= 0 or > MaxBytes) throw new InvalidDataException("O áudio deve ter conteúdo e até 20 MiB.");
                await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                var buffer = new byte[81920];
                long copied = 0;
                int count;
                while ((count = await source.ReadAsync(buffer)) > 0)
                {
                    copied += count;
                    if (copied > MaxBytes) throw new InvalidDataException("O áudio deve ter até 20 MiB.");
                    await target.WriteAsync(buffer.AsMemory(0, count));
                }
            }
            await validateAudio(path);
            return new AlarmSound(AlarmSoundKind.Custom, id, Path.GetFileName(sourcePath));
        }
        catch
        {
            lock (fileGate) { pendingImports.Remove(id); TryDelete(path); }
            throw;
        }
    }

    // Call only after a successful repository read, and include any open editor's draft.
    public Task CleanupAsync(IEnumerable<AlarmSound> referencedSounds, IEnumerable<Guid>? releaseImports = null) => Task.Run(() =>
    {
        lock (fileGate)
        {
            if (releaseImports is not null) pendingImports.ExceptWith(releaseImports);
            var used = referencedSounds.Append(DefaultSound).Where(s => s.Kind == AlarmSoundKind.Custom)
                .Select(s => s.FileId).ToHashSet();
            if (!Directory.Exists(soundsPath)) return;
            foreach (var file in Directory.EnumerateFiles(soundsPath))
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension is not (".wav" or ".mp3") || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var id)) continue;
                if (!used.Contains(id) && !pendingImports.Contains(id)) TryDelete(file);
            }
        }
    });

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
