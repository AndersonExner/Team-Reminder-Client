using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Windows.AppNotifications;
using TeamReminder.Core;

namespace TeamReminder.Windows;

public sealed class WindowsAlarmNotifier(Action<AlarmDelivery> reportAlert,
    Action<string> reportStatus, Action activateWindow) : IAlarmNotifier, IDisposable
{
    private bool registered;

    public void Initialize()
    {
        try
        {
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            AppNotificationManager.Default.Register();
            registered = true;
        }
        catch (Exception)
        {
            reportStatus("Os avisos do Windows estão indisponíveis. Os alarmes continuarão com som e aviso nesta janela.");
        }
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args) => activateWindow();

    public Task NotifyAsync(AlarmDelivery delivery)
    {
        var soundFile = Path.Combine(AppContext.BaseDirectory, "Assets", "alarm.wav");
        var soundAccepted = PlaySound(soundFile, IntPtr.Zero, 0x00020000 | 0x0001 | 0x0002);
        if (!soundAccepted) reportStatus("Não foi possível reproduzir o som. Confira o áudio; o aviso permanece nesta janela.");
        uint notificationId = 0;
        if (registered)
        {
            try
            {
                var title = SecurityElement.Escape(delivery.Alarm.Title);
                var schedule = delivery.OccurrenceUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                var prefix = delivery.IsLate ? "Alarme atrasado" : "Hora do alarme";
                var notification = new AppNotification($"<toast><visual><binding template='ToastGeneric'><text>Team Reminder</text><text>{title}</text><text>{prefix} — {schedule}</text></binding></visual><audio silent='true'/></toast>");
                AppNotificationManager.Default.Show(notification);
                notificationId = notification.Id;
                if (notificationId == 0) reportStatus("O Windows não aceitou este aviso. Confira as notificações; o som e o aviso nesta janela continuam disponíveis.");
            }
            catch (Exception)
            {
                reportStatus("Não foi possível enviar o aviso do Windows. O som e o aviso nesta janela continuam disponíveis.");
            }
        }
        reportAlert(delivery);
#if DEBUG
        // Optional diagnostic output used with a separate temporary database, never with user alarms.
        var logPath = Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_DELIVERY_LOG");
        if (!string.IsNullOrEmpty(logPath))
        {
            try
            {
                File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new
                {
                    delivery.Alarm.Id, delivery.Alarm.Title, delivery.OccurrenceUtc,
                    DeliveredAtUtc = DateTime.UtcNow, SoundAccepted = soundAccepted,
                    NotificationId = notificationId, NotificationRegistered = registered
                }) + Environment.NewLine);
            }
            catch (IOException) { /* Diagnostic logging must not repeat an accepted alert. */ }
            catch (UnauthorizedAccessException) { /* Delivery is already accepted. */ }
        }
#endif
        return Task.CompletedTask;
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string sound, IntPtr module, uint flags);

    public void Dispose()
    {
        try
        {
            AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
            if (registered) AppNotificationManager.Default.Unregister();
        }
        catch (Exception) { /* App teardown must still release the database and mutex. */ }
    }
}
