using Microsoft.Win32;

namespace TeamReminder.Windows;

internal static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TeamReminder";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        if (enabled)
        {
            var exe = Environment.ProcessPath ?? throw new IOException("Executável não encontrado.");
            key.SetValue(ValueName, $"\"{exe}\" --tray", RegistryValueKind.String);
        }
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
