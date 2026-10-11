using System.Runtime.InteropServices;
using Microsoft.Win32;
using TeamReminder.Core;
using TeamReminder.Local;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace TeamReminder.Windows;

public sealed class AlarmSoundPlayer(LocalSoundLibrary library, Action<string> reportStatus) : IDisposable
{
    private MediaPlayer? player;
    private MediaSource? source;
    private bool disposed;
    private int generation;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
    // System event playback is process-wide. Only the current owner may stop it.
    private static AlarmSoundPlayer? systemSoundOwner;
    public bool LastPlaybackUsedFallback { get; private set; }
    public event Action? AlarmStarting;

    public async Task<bool> PlayAsync(AlarmSound selection, bool isAlarm = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (isAlarm) AlarmStarting?.Invoke();
        Stop();
        LastPlaybackUsedFallback = false;
        var operation = generation;
        var sound = selection.Kind == AlarmSoundKind.Default ? library.DefaultSound : selection;
        if (sound.Kind == AlarmSoundKind.Silent) return true;
        if (sound.Kind != AlarmSoundKind.Custom) return PlaySystem(sound.Kind);
        try
        {
            var path = library.ResolvePath(sound) ?? throw new FileNotFoundException();
            var opened = new MediaPlayer { AutoPlay = false, IsLoopingEnabled = false };
            player = opened;
            opened.CommandManager.IsEnabled = false;
            var prepared = await PrepareAsync(opened, path);
            if (operation != generation) { prepared.Dispose(); return false; }
            source = prepared;
            opened.MediaFailed += PlaybackFailed;
            opened.MediaEnded += PlaybackEnded;
            opened.Play();
            return true;
        }
        catch (Exception)
        {
            if (operation != generation) return false;
            Stop();
            LastPlaybackUsedFallback = true;
            reportStatus("Áudio personalizado indisponível. Usando Notificação do Windows; o aviso visual continua disponível.");
            return PlaySystem(AlarmSoundKind.Notification);
        }
    }

    public static async Task ValidateFileAsync(string path)
    {
        using var probe = new MediaPlayer { AutoPlay = false };
        probe.CommandManager.IsEnabled = false;
        using var prepared = await PrepareAsync(probe, path);
        var duration = probe.PlaybackSession.NaturalDuration;
        if (duration <= TimeSpan.Zero || probe.PlaybackSession.NaturalVideoWidth > 0) throw new InvalidDataException("O arquivo não contém áudio reproduzível.");
    }

    private static async Task<MediaSource> PrepareAsync(MediaPlayer player, string path)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Opened(MediaPlayer sender, object args) => ready.TrySetResult();
        void Failed(MediaPlayer sender, MediaPlayerFailedEventArgs args) =>
            ready.TrySetException(new InvalidDataException("O Windows não conseguiu ler este áudio."));
        var source = MediaSource.CreateFromStorageFile(await StorageFile.GetFileFromPathAsync(path));
        player.MediaOpened += Opened;
        player.MediaFailed += Failed;
        try
        {
            player.Source = source;
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return source;
        }
        catch
        {
            try { player.Source = null; } catch (ObjectDisposedException) { }
            source.Dispose(); throw;
        }
        finally { player.MediaOpened -= Opened; player.MediaFailed -= Failed; }
    }

    private void PlaybackFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        dispatcher.TryEnqueue(() =>
        {
            if (disposed || player != sender) return;
            Stop();
            LastPlaybackUsedFallback = true;
            reportStatus("Falha ao reproduzir o áudio personalizado. Usando Notificação do Windows.");
            PlaySystem(AlarmSoundKind.Notification);
        });
    }

    private void PlaybackEnded(MediaPlayer sender, object args) => dispatcher.TryEnqueue(() =>
    {
        if (!disposed && player == sender) Stop();
    });

    private bool PlaySystem(AlarmSoundKind kind)
    {
        var alias = kind switch
        {
            AlarmSoundKind.Exclamation => "SystemExclamation",
            AlarmSoundKind.Critical => "SystemHand",
            _ => "SystemNotification"
        };
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"AppEvents\Schemes\Apps\.Default\" + alias + @"\.Current");
            if (key is not null && string.IsNullOrEmpty(key.GetValue(null) as string)) return true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        systemSoundOwner = this;
        var accepted = PlaySound(alias, IntPtr.Zero, 0x00010000 | 0x0001 | 0x0002 | 0x00200000);
        if (!accepted) reportStatus("Não foi possível reproduzir o som do Windows. O aviso visual continua disponível.");
        return accepted;
    }

    public void Stop()
    {
        generation++;
        if (systemSoundOwner == this)
        {
            PlaySound(null, IntPtr.Zero, 0);
            systemSoundOwner = null;
        }
        if (player is not null)
        {
            player.MediaFailed -= PlaybackFailed;
            player.MediaEnded -= PlaybackEnded;
            player.Dispose(); player = null;
        }
        source?.Dispose(); source = null;
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string? sound, IntPtr module, uint flags);

    public void Dispose() { if (disposed) return; Stop(); disposed = true; }
}
