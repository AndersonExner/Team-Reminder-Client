using Microsoft.Windows.AppNotifications;
using TeamReminder.Core;

namespace TeamReminder.Windows;

public sealed class WindowsAlarmNotifier(Action<AlarmDelivery> reportAlert,
    Action<string> reportStatus, Action activateWindow, AlarmSoundPlayer soundPlayer) : IAlarmNotifier, IDisposable
{
    private bool registered;
    // Raised on a notification thread when the person picks Adiar; the app marshals it to the UI.
    public event Action<Guid, DateTime, int>? SnoozeRequested;

    public void Initialize()
    {
        try
        {
            AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
            // Unpackaged apps otherwise show the executable name ("TeamReminder") as the notification header.
            try { AppNotificationManager.Default.Register("Team Reminder", new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"))); }
            catch (ArgumentException) { AppNotificationManager.Default.Register(); }
            registered = true;
        }
        catch (Exception)
        {
            reportStatus("Os avisos do Windows estão indisponíveis. Os alarmes continuarão com som e aviso nesta janela.");
        }
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args) => Handle(args);

    public void Handle(AppNotificationActivatedEventArgs args)
    {
        if (AlarmNotificationXml.TryParseSnooze(args.Argument, args.UserInput, out var id, out var occurrence, out var minutes))
            SnoozeRequested?.Invoke(id, occurrence, minutes);
        else activateWindow();
    }

    public bool ShowEarlyReminder(Alarm alarm, int minutes)
    {
        if (!registered) return false;
        try
        {
            var notification = new AppNotification(AlarmNotificationXml.BuildEarly(alarm, minutes));
            AppNotificationManager.Default.Show(notification);
            return notification.Id != 0;
        }
        catch (Exception) { return false; }
    }

    public async Task NotifyAsync(AlarmDelivery delivery)
    {
        var soundAccepted = false;
        try { soundAccepted = await soundPlayer.PlayAsync(delivery.Alarm.Sound, isAlarm: true); }
        catch (Exception) { reportStatus("Não foi possível reproduzir o áudio; o aviso visual continua disponível."); }
        uint notificationId = 0;
        if (registered)
        {
            try
            {
                var notification = new AppNotification(AlarmNotificationXml.Build(delivery));
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
                    SoundKind = (int)delivery.Alarm.Sound.Kind, SoundFallback = soundPlayer.LastPlaybackUsedFallback,
                    NotificationId = notificationId, NotificationRegistered = registered, NotificationXml = AlarmNotificationXml.Build(delivery)
                }) + Environment.NewLine);
            }
            catch (IOException) { /* Diagnostic logging must not repeat an accepted alert. */ }
            catch (UnauthorizedAccessException) { /* Delivery is already accepted. */ }
        }
#endif
    }

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
