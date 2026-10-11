#if DEBUG
using System.Text.Json;
using TeamReminder.Core;
using TeamReminder.Local;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace TeamReminder.Windows;

// Opt-in diagnostic: the launcher must provide a separate test database and report path.
internal static class AudioSmokeProbe
{
    public static async Task RunAsync(LocalSoundLibrary sounds, string reportPath, IntPtr hwnd, AlarmsViewModel vm)
    {
        var checks = new List<string>();
        var statuses = new List<string>();
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
            Directory.CreateDirectory(directory);
            var wavePath = Path.Combine(directory, "source.wav");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets", "alarm.wav"), wavePath, true);
            var wave = await sounds.ImportAsync(wavePath, AlarmSoundPlayer.ValidateFileAsync);
            File.Delete(wavePath);
            checks.Add("WAV validado e copiado; original removido");
            var source = await StorageFile.GetFileFromPathAsync(sounds.ResolvePath(wave)!);
            var folder = await StorageFolder.GetFolderFromPathAsync(directory);
            var mp3File = await folder.CreateFileAsync("source.mp3", CreationCollisionOption.ReplaceExisting);
            var transcode = await new MediaTranscoder().PrepareFileTranscodeAsync(source, mp3File,
                MediaEncodingProfile.CreateMp3(AudioEncodingQuality.Low));
            if (!transcode.CanTranscode) throw new InvalidDataException("Não foi possível gerar o MP3 de teste.");
            await transcode.TranscodeAsync();
            var mp3 = await sounds.ImportAsync(mp3File.Path, AlarmSoundPlayer.ValidateFileAsync);
            await mp3File.DeleteAsync();
            checks.Add("MP3 validado e copiado; original removido");
            using var playback = new AlarmSoundPlayer(sounds, text => statuses.Add(text));
            foreach (var sound in new[] { wave, mp3, new AlarmSound(AlarmSoundKind.Notification),
                new AlarmSound(AlarmSoundKind.Exclamation), new AlarmSound(AlarmSoundKind.Critical),
                new AlarmSound(AlarmSoundKind.Silent) })
            {
                if (!await playback.PlayAsync(sound)) throw new InvalidOperationException("Reprodução recusada: " + sound.Label);
                await Task.Delay(300);
                playback.Stop();
                checks.Add("Reprodução aceita: " + sound.Label);
            }
            if (statuses.Count != 0) throw new InvalidOperationException(string.Join("; ", statuses));
            await sounds.SaveDefaultAsync(mp3);
            var restored = new LocalSoundLibrary(directory);
            await restored.LoadDefaultAsync();
            if (restored.DefaultSound != mp3) throw new InvalidOperationException("Padrão não persistiu.");
            if (!await playback.PlayAsync(AlarmSound.Default)) throw new InvalidOperationException("Padrão recusado.");
            playback.Stop();
            checks.Add("Padrão personalizado persistido e reproduzido");
            var missing = new AlarmSound(AlarmSoundKind.Custom, Guid.NewGuid(), "ausente.mp3");
            if (!await playback.PlayAsync(missing) || !statuses.Any(s => s.Contains("indisponível")))
                throw new InvalidOperationException("Fallback não registrado.");
            playback.Stop(); statuses.Clear();
            checks.Add("Arquivo ausente usa fallback com aviso");
            var invalidPath = Path.Combine(directory, "invalid.mp3");
            await File.WriteAllTextAsync(invalidPath, "isto não é áudio");
            var rejected = false;
            try { await sounds.ImportAsync(invalidPath, AlarmSoundPlayer.ValidateFileAsync); }
            catch (Exception) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Arquivo inválido foi aceito.");
            checks.Add("Decodificador rejeita MP3 inválido");
            vm.BeginCreate();
            var dialog = new AlarmEditorDialog(vm, sounds, hwnd);
            dialog.DisposeSoundEditor();
            checks.Add("Modal com seletor de som carrega recursos XAML");
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new { Success = true, Checks = checks }));
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new { Success = false, Checks = checks, Error = ex.ToString() }));
        }
    }
}
#endif
