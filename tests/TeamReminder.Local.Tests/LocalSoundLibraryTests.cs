using TeamReminder.Core;
using TeamReminder.Local;

namespace TeamReminder.Local.Tests;

public sealed class LocalSoundLibraryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString());
    private string Source(string name, byte[]? bytes = null)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, name); File.WriteAllBytes(path, bytes ?? [1, 2, 3]); return path;
    }

    [Theory]
    [InlineData("som.mp3")]
    [InlineData("som.WAV")]
    public async Task CopyIsValidatedAndStillResolvesAfterDeletingOriginal(string name)
    {
        var library = new LocalSoundLibrary(root); var source = Source(name); string? checkedPath = null;
        var sound = await library.ImportAsync(source, copy => { checkedPath = copy; return Task.CompletedTask; });
        Assert.NotEqual(source, checkedPath); Assert.Equal(name, sound.FileName);
        File.Delete(source);
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(library.ResolvePath(sound)!));
        await library.SaveDefaultAsync(sound);
        var restarted = new LocalSoundLibrary(root); await restarted.LoadDefaultAsync();
        Assert.Equal(sound, restarted.DefaultSound);
        Assert.NotNull(restarted.ResolvePath(sound));
    }

    [Fact]
    public async Task FailedDecoderValidationRemovesPartialImport()
    {
        var library = new LocalSoundLibrary(root); var source = Source("bad.mp3");
        await Assert.ThrowsAsync<InvalidDataException>(() => library.ImportAsync(source,
            _ => Task.FromException(new InvalidDataException("Corrompido"))));
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "Sounds")));
        Assert.True(File.Exists(source));
    }

    [Theory]
    [InlineData("som.exe", 3)]
    [InlineData("som.mp3", 0)]
    [InlineData("som.wav", 20971521)]
    public async Task WrongFormatEmptyAndOversizedFilesAreRejectedBeforeDecoder(string name, int length)
    {
        var library = new LocalSoundLibrary(root); var source = Source(name, new byte[length]); var called = false;
        await Assert.ThrowsAsync<InvalidDataException>(() => library.ImportAsync(source,
            _ => { called = true; return Task.CompletedTask; }));
        Assert.False(called);
        Assert.True(File.Exists(source));
        Assert.True(!Directory.Exists(Path.Combine(root, "Sounds")) || Directory.GetFiles(Path.Combine(root, "Sounds")).Length == 0);
    }

    [Fact]
    public async Task CleanupProtectsPendingImportsSharedReferencesAndDefaultAndDeletesOnlyManagedFiles()
    {
        var library = new LocalSoundLibrary(root); var source = Source("som.mp3");
        var shared = await library.ImportAsync(source, _ => Task.CompletedTask);
        var standard = await library.ImportAsync(source, _ => Task.CompletedTask);
        var canceled = await library.ImportAsync(source, _ => Task.CompletedTask);
        var stillPicking = await library.ImportAsync(source, _ => Task.CompletedTask);
        var canceledPath = library.ResolvePath(canceled)!;
        var unrelated = Path.Combine(root, "Sounds", "do-not-remove.mp3"); File.WriteAllText(unrelated, "user");
        await library.SaveDefaultAsync(standard);
        await library.CleanupAsync([shared, shared], [shared.FileId!.Value, standard.FileId!.Value, canceled.FileId!.Value]);
        Assert.NotNull(library.ResolvePath(shared)); Assert.NotNull(library.ResolvePath(standard));
        Assert.NotNull(library.ResolvePath(stillPicking)); Assert.False(File.Exists(canceledPath)); Assert.True(File.Exists(unrelated));
        await library.CleanupAsync([shared]); Assert.NotNull(library.ResolvePath(shared));
        await library.CleanupAsync([]); Assert.Null(library.ResolvePath(shared)); Assert.NotNull(library.ResolvePath(standard));
        await library.SaveDefaultAsync(new AlarmSound(AlarmSoundKind.Silent)); await library.CleanupAsync([]);
        Assert.Null(library.ResolvePath(standard));
    }

    [Fact]
    public async Task InvalidAndUnreadablePreferencesFallBackWithoutDeletingFilesAndFailedSaveKeepsCurrentDefault()
    {
        var library = new LocalSoundLibrary(root); var settings = Path.Combine(root, "Settings", "default-sound.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, "broken"); await library.LoadDefaultAsync();
        Assert.Equal(AlarmSound.InitialDefault, library.DefaultSound);
        await library.SaveDefaultAsync(new AlarmSound(AlarmSoundKind.Silent));
        var reopened = new LocalSoundLibrary(root); await reopened.LoadDefaultAsync();
        Assert.Equal(AlarmSoundKind.Silent, reopened.DefaultSound.Kind);
        File.Delete(settings); Directory.CreateDirectory(settings);
        await Assert.ThrowsAnyAsync<Exception>(() => library.SaveDefaultAsync(new AlarmSound(AlarmSoundKind.Critical)));
        Assert.Equal(AlarmSoundKind.Silent, library.DefaultSound.Kind);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(settings)!, "*.tmp"));
        Assert.Null(library.ResolvePath(new AlarmSound(AlarmSoundKind.Custom, Guid.NewGuid(), "../../outside.mp3")));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
