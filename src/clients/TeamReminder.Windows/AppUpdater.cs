using System.Diagnostics;
using System.Net.Http;
using TeamReminder.Core;

namespace TeamReminder.Windows;

public sealed class AppUpdater
{
    private static readonly string UpdatesDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeamReminder", "Updates");
    private readonly UpdateDownloader downloader = new(new HttpClient { Timeout = TimeSpan.FromMinutes(10) });
    private bool checking;

    // Only an installed copy updates itself; development builds and portable runs never replace files.
    public static bool IsInstalled => File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));
    public static Version CurrentVersion => UpdatePolicy.Normalize(typeof(AppUpdater).Assembly.GetName().Version ?? new Version(0, 0, 0));
    public UpdateCandidate? Ready { get; private set; }
    public DateTime? LastCheckUtc { get; private set; }
    private string? installerPath;

    public async Task<string> CheckAsync()
    {
        if (!IsInstalled) return "Atualização automática disponível apenas na versão instalada.";
        if (checking) return "Verificando atualizações...";
        checking = true;
        try
        {
            var candidate = await downloader.CheckAsync(CurrentVersion);
            LastCheckUtc = DateTime.UtcNow;
            if (candidate is null) return $"Você está na versão mais recente ({CurrentVersion}).";
            if (Ready?.Version == candidate.Version && installerPath is not null) return ReadyText;
            installerPath = await downloader.DownloadAsync(candidate, UpdatesDirectory);
            Ready = candidate;
            return ReadyText;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            LastCheckUtc = DateTime.UtcNow;
            ErrorLog.Write(ex);
            return "Não foi possível verificar atualizações agora. Uma nova tentativa será feita mais tarde.";
        }
        finally { checking = false; }
    }

    private string ReadyText => $"Versão {Ready!.Version} pronta para instalar (atual: {CurrentVersion}).";

    // The installer closes this process, replaces the files and starts the app again (see TeamReminder.iss).
    public bool Launch(bool showWindow)
    {
        if (installerPath is null || !File.Exists(installerPath)) return false;
        try
        {
            Process.Start(new ProcessStartInfo(installerPath,
                $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /relaunch={(showWindow ? "--updated" : "--tray")}")
            { UseShellExecute = false });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            ErrorLog.Write(ex);
            return false;
        }
    }
}
