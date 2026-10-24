namespace TeamReminder.Windows;

// Unexpected errors go to %LOCALAPPDATA%\TeamReminder\Logs\errors.log, capped so it cannot grow without bound.
public static class ErrorLog
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object Gate = new();
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeamReminder", "Logs", "errors.log");

    public static void Write(Exception? exception)
    {
        if (exception is null) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                    File.Move(FilePath, FilePath + ".old", overwrite: true);
                File.AppendAllText(FilePath, $"[{DateTimeOffset.Now:O}] {exception}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
